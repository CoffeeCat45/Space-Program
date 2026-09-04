using System;
using System.Collections.Generic;

public struct DockingConfig
{
    public double maxDistance;
    public double maxRelativeSpeed;
    public double minAlignmentDot;

    public DockingConfig(
        double maxDistance,
        double maxRelativeSpeed,
        double minAlignmentDot
    )
    {
        this.maxDistance = maxDistance;
        this.maxRelativeSpeed = maxRelativeSpeed;
        this.minAlignmentDot = minAlignmentDot;
    }

    public static DockingConfig Default =>
        new DockingConfig(0.20, 0.45, 0.94);
}

partial class Simulation : ISimulationStateView
{
    const double VacuumDockingHullContactRestitution = 0.0;
    const double DefaultShipAutoTurnRateRadiansPerSecond = 120.0 * Math.PI / 180.0;
    const double DefaultShipManualTurnRateRadiansPerSecond = 55.0 * Math.PI / 180.0;
    const double VacuumManualTurnRateMultiplier = 0.55;
    const double ManualAttitudeResponseTimeSeconds = ShipManualAttitudeRamp.DefaultResponseTimeSeconds;
    const double DefaultAtmosphericAutoTurnMultiplier = 1.5;
    const double ManualTurnReferenceMassKg = 10000.0;
    const double ManualTurnReferenceDynamicPressure = 5000.0;

    IGravityModel gravityModel;
    IAtmosphereModel atmosphereModel;

    public Body planet;
    public Body moon;
    public Body sun;
    public Vessel ship;
    public Vessel activeVessel => ship;
    public Vessel targetVessel;
    public List<Vessel> dockableVessels = new List<Vessel>();
    public DockingConfig dockingConfig = DockingConfig.Default;
    public List<Vessel> debrisVessels = new List<Vessel>();
    public List<Vessel> vessels = new List<Vessel>();
    public Body[] bodies;
    public SimulationScenario scenario;
    public long currentTick;
    public int worldRevision;
    public bool IsServerContext;

    public SimVector3 lastGravityAcceleration;
    public SimVector3 lastMoonGravityAcceleration;
    public double lastAtmosphericDensity;
    public double lastDynamicPressure;
    public double lastDragForce;
    public double lastLiftForce;
    public double lastEffectiveDragArea;
    public double lastDragTorqueZ;
    public double lastAngleOfAttack;
    public double lastLiftCoefficient;
    public double lastDragCoefficient;
    public bool atmosphericSasEnabled;
    public bool enableAtmosphericPreview;
    public ShipOrientationInput orientationInput;
    public int ClientLocalOrientationVesselId;
    public double circularSpeedAtStart;
    public double moonCircularSpeedAtStart;
    public double moonSphereOfInfluenceRadius;
    public double planetSphereOfInfluenceRadius;
    public double minShipDistanceToPlanet;
    public double maxShipDistanceToPlanet;
    public double initialShipSpecificEnergy;
    public double initialShipAngularMomentum;
    public string activePrimaryBodyName;
    string lastActivePrimaryForHysteresis = "Planet";
    SimVector3 solarStepSunPosition;
    SimVector3 solarStepSunVelocity;
    SimVector3 solarStepPlanetPosition;
    SimVector3 solarStepPlanetVelocity;
    SimVector3 solarStepMoonPosition;
    SimVector3 solarStepMoonVelocity;
    bool solarStepStartCaptured;

    const double SphereOfInfluenceExitMultiplier = 1.04;
    /// <summary>
    /// Drop Kepler rails this close to the Moon so patched-conics SoI uses the
    /// same numerical boundary as a thrusting encounter.
    /// </summary>
    const double MoonSoiNumericalPatchMultiplier = 1.25;
    const double SoftLandingTotalSpeed = 18.0;
    const double LiftoffThrustMargin = 1.05;
    const double MaxNumericalPhysicsStep = 1.0;
    const double RailsThrustEpsilonSquared = 1e-8;
    const double MaxPhysicsWarpTimeScaleWhileThrusting = 4.0;
    const double MaxAnalyticTimeScaleWhileThrusting = 1.0;
    const double MaxSimulationStepWhileThrusting = 1.0;
    const double SasOrientationKp = 8.0;
    const double SasOrientationKd = 3.0;
    const double DevSasOrientationKp = 800.0;
    const double DevSasOrientationKd = 80.0;
    const double DevSasRollTurnRateMultiplier = 4.0;

    readonly Dictionary<Vessel, int> vesselIds = new Dictionary<Vessel, int>();
    int nextVesselId = 1;

    struct ShipRespawnSnapshot
    {
        public SimVector3 position;
        public SimVector3 velocity;
        public double heading;
        public FlightStatus flightStatus;
        public string landedOnBodyName;
        public int activeStageIndex;
        public List<StageData> stages;
    }

    ShipRespawnSnapshot initialShipSnapshot;

    public double simulationTime;
    /// <summary>
    /// Orbit-preview clones: skip SAS and mass/Δv so CA/SoI search stays cheap.
    /// </summary>
    bool isOrbitPreview;

    readonly SimulationPreview preview;

    public Simulation(SimulationScenario scenario)
        : this(scenario, new NewtonianGravityModel(), new ExponentialAtmosphereModel())
    {
    }

    public Simulation(SimulationScenario scenario, IGravityModel gravityModel)
        : this(scenario, gravityModel, new ExponentialAtmosphereModel())
    {
    }

    public Simulation(
        SimulationScenario scenario,
        IGravityModel gravityModel,
        IAtmosphereModel atmosphereModel
    )
    {
        this.scenario = scenario;
        this.gravityModel = gravityModel;
        this.atmosphereModel = atmosphereModel;
        LaunchPadConstants.BindForScenario(scenario);

        SimulationWorldSetupResult worldSetup = SimulationWorldBuilder.Build(
            scenario,
            gravityModel
        );

        sun = worldSetup.sun;
        planet = worldSetup.planet;
        moon = worldSetup.moon;
        ship = worldSetup.ship;
        bodies = worldSetup.bodies;
        if (planet != null && planet.radius > 0.0)
        {
            planet.spinAxis = SimVector3.UnitY();
            planet.spinRadiansPerSecond =
                CelestialBody.Earth.GetSpinRadiansPerSecond();

            if (SimulationScenarios.IsPlanetSurfacePad(scenario))
            {
                AlignPlanetSpinToCapeMorning();
            }
        }

        if (moon != null)
        {
            moon.spinAxis = SimVector3.UnitY();
            moon.spinRadiansPerSecond = CelestialBody.Moon.GetSpinRadiansPerSecond();

            if (SimulationScenarios.IsMoonSurfacePad(scenario))
            {
                AlignMoonSpinForSurfacePadDaylight();
            }
            else
            {
                AlignMoonSpinTidalLock();
            }
        }

        moonSphereOfInfluenceRadius = worldSetup.moonSphereOfInfluenceRadius;
        planetSphereOfInfluenceRadius = worldSetup.planetSphereOfInfluenceRadius;
        circularSpeedAtStart = worldSetup.circularSpeedAtStart;
        moonCircularSpeedAtStart = worldSetup.moonCircularSpeedAtStart;

        ConfigureDefaultStages(ship);
        ship.RecalculateMassAndDeltaV(
            GetShipLocalGravityRelativeToActivePrimary()
        );

        if (ship.flightStatus == FlightStatus.Landed)
        {
            FinalizeLandedShipOnSurface();
        }

        if (SimulationScenarios.IsAnySurfacePad(scenario))
        {
            ApplyDeterministicPadGroundingLock();
        }

        RegisterVesselInRegistry(ship, 1);

        if (
            scenario == SimulationScenario.DockingApproach &&
            worldSetup.dockableStation != null
        )
        {
            ConfigureDockingScenario(worldSetup.dockableStation);
        }
        else if (ShouldCalibrateOrbitalSpawn(scenario))
        {
            ConfigureOrbitalScenario();
        }

        ResetAllVesselPilotControls();

        if (scenario != SimulationScenario.DockingApproach)
        {
            InitializeShipSpawnOrientation();
        }

        lastGravityAcceleration = SimVector3.Zero();
        lastMoonGravityAcceleration = SimVector3.Zero();
        lastAtmosphericDensity = 0.0;
        lastDynamicPressure = 0.0;
        lastDragForce = 0.0;
        lastLiftForce = 0.0;
        lastEffectiveDragArea = 0.0;
        lastDragTorqueZ = 0.0;
        lastAngleOfAttack = 0.0;
        lastLiftCoefficient = 0.0;
        lastDragCoefficient = 0.0;
        atmosphericSasEnabled = false;
        enableAtmosphericPreview = true;
        orientationInput = ShipOrientationInput.Idle();
        double shipStartRadius = ScenarioIsVacuumDocking() ?
            0.0 :
            GetShipDistanceToPlanet();
        minShipDistanceToPlanet = shipStartRadius;
        maxShipDistanceToPlanet = shipStartRadius;
        initialShipSpecificEnergy = GetShipSpecificOrbitalEnergyRelativeToPlanet();
        initialShipAngularMomentum = GetShipAngularMomentumRelativeToPlanet();
        lastActivePrimaryForHysteresis = ScenarioIsVacuumDocking() ?
            ship.name :
            ResolveActivePrimaryBody().name;
        activePrimaryBodyName = lastActivePrimaryForHysteresis;
        simulationTime = 0.0;
        currentTick = 0L;
        worldRevision = 0;
        preview = new SimulationPreview(this);
        initialShipSnapshot = CaptureShipRespawnSnapshot();
        EnforceSphereCollisionWithPrimary();
    }

    public void SetShipThrust(SimVector3 thrust)
    {
        ship.thrust = thrust;
    }

    public void SetShipThrottle(double throttle)
    {
        if (ship == null)
        {
            return;
        }

        ship.controlState.throttle = (float)Math.Clamp(throttle, 0.0, 1.0);
    }

    static void SetVesselForwardAndUp(
        Vessel vessel,
        SimVector3 forwardWorld,
        SimVector3 upWorld
    )
    {
        if (vessel == null)
        {
            return;
        }

        vessel.orientation = SimQuaternion.FromForwardAndUp(forwardWorld, upWorld);
        vessel.angularVelocity = SimVector3.Zero();
        vessel.SyncHeadingFromOrientation();
    }

    void InitializeShipSpawnOrientation()
    {
        if (ship == null || ship.flightStatus == FlightStatus.Landed)
        {
            return;
        }

        ship.orientation = SimQuaternion.Identity();
        ship.angularVelocity = SimVector3.Zero();
        ship.SyncHeadingFromOrientation();
    }

    public void ResetAllVesselPilotControls()
    {
        ResetVesselPilotControls(ship);

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            ResetVesselPilotControls(dockableVessels[i]);
        }
    }

    static void ResetVesselPilotControls(Vessel vessel)
    {
        if (vessel == null)
        {
            return;
        }

        vessel.controlState.throttle = 0.0f;
        vessel.controlState.pitchInput = 0.0f;
        vessel.controlState.yawInput = 0.0f;
        vessel.controlState.rollInput = 0.0f;
        vessel.controlState.rcsVector = SimVector3.Zero();
        vessel.controlState.stagingRequested = false;
    }

    public double GetShipThrottle()
    {
        return ship == null ? 0.0 : ship.controlState.throttle;
    }

    public bool IsThrustActive()
    {
        if (ship == null || ship.controlState.throttle <= 0.0)
        {
            return false;
        }

        if (SimulationCheatFlags.InfiniteFuel)
        {
            return ship.controlState.throttle > 0.01f;
        }

        if (ship.GetActiveStageFuelMass() <= 0.0)
        {
            return false;
        }

        return ship.controlState.throttle > 0.01f;
    }

    public bool IsThrustTimeWarpRestricted()
    {
        if (ship == null || ship.controlState.throttle <= 0.0)
        {
            return false;
        }

        if (SimulationCheatFlags.InfiniteFuel)
        {
            return true;
        }

        return ship.GetActiveStageFuelMass() > 0.0;
    }

    public double GetMaxAllowedTimeScaleWhileThrusting()
    {
        if (!IsThrustTimeWarpRestricted())
        {
            return double.PositiveInfinity;
        }

        if (IsThrustActive())
        {
            return MaxAnalyticTimeScaleWhileThrusting;
        }

        return MaxPhysicsWarpTimeScaleWhileThrusting;
    }

    public const string ThrustTimeWarpBlockedMessage =
        "Нельзя использовать аналитический TimeWarp при работающем двигателе!";

    public double GetShipHeadingRadians()
    {
        return ship.heading;
    }

    public void SetShipHeadingRadians(double radians)
    {
        ship.SetHeadingRadians(radians);
    }

    public void RotateShipHeading(double deltaRadians)
    {
        ship.RotateHeading(deltaRadians);
    }

    public void ApplyOrientationControl(double dt)
    {
        ApplyVesselOrientationControl(ship, dt);
    }

    ShipOrientationInput BuildVesselOrientationInput(Vessel vessel)
    {
        return new ShipOrientationInput
        {
            mode = vessel.controlState.orientationMode,
            maneuverTargetDirection = vessel == ship ?
                orientationInput.maneuverTargetDirection :
                SimVector3.Zero(),
            autoTurnRateRadiansPerSecond = DefaultShipAutoTurnRateRadiansPerSecond,
            atmosphericAutoTurnRateMultiplier = DefaultAtmosphericAutoTurnMultiplier,
        };
    }

    SimVector3 GetVesselOrbitVelocityForOrientation(Vessel vessel)
    {
        SimVector3 orbitVelocity = vessel.velocity;

        if (ScenarioIsVacuumDocking())
        {
            Vessel targetStation = GetVesselById(NetworkInputFrame.Player2VesselId);

            if (targetStation != null)
            {
                orbitVelocity = vessel.velocity.Subtract(targetStation.velocity);
            }
        }
        else
        {
            Body activePrimaryBody = GetActivePrimaryBody();

            if (activePrimaryBody != null)
            {
                orbitVelocity = vessel.velocity.Subtract(activePrimaryBody.velocity);
            }
        }

        return orbitVelocity;
    }

    SimVector3 GetVesselTargetDirection(Vessel vessel, ShipOrientationInput input)
    {
        if (input.mode == ShipOrientationMode.Prograde)
        {
            if (
                vessel == ship &&
                !ScenarioIsVacuumDocking() &&
                IsShipInAtmosphere()
            )
            {
                return GetShipProgradeDirectionForAtmosphere();
            }

            SimVector3 orbitVelocity = GetVesselOrbitVelocityForOrientation(vessel);

            return orbitVelocity.LengthSquared() > 1e-12 ?
                orbitVelocity.Normalize() :
                SimVector3.Zero();
        }

        if (input.mode == ShipOrientationMode.Retrograde)
        {
            if (
                vessel == ship &&
                !ScenarioIsVacuumDocking() &&
                IsShipInAtmosphere()
            )
            {
                return GetShipRetrogradeDirectionForAtmosphere();
            }

            SimVector3 orbitVelocity = GetVesselOrbitVelocityForOrientation(vessel);

            return orbitVelocity.LengthSquared() > 1e-12 ?
                orbitVelocity.Multiply(-1.0).Normalize() :
                SimVector3.Zero();
        }

        if (vessel == ship)
        {
            return ShipOrientationControl.GetTargetDirection(this, input);
        }

        if (
            input.mode == ShipOrientationMode.TargetAlignment &&
            targetVessel != null
        )
        {
            SimVector3 sourcePortToTargetPort = targetVessel
                .GetDockingPortPosition()
                .Subtract(vessel.GetDockingPortPosition());

            if (sourcePortToTargetPort.LengthSquared() <= 1e-12)
            {
                return SimVector3.Zero();
            }

            return sourcePortToTargetPort.Normalize();
        }

        return SimVector3.Zero();
    }

    double GetManualAttitudeTurnRateRadiansPerSecond(Vessel vessel)
    {
        double turnRate = DefaultShipManualTurnRateRadiansPerSecond;

        if (
            vessel == null ||
            vessel.flightStatus != FlightStatus.Flying ||
            vessel != ship
        )
        {
            return turnRate;
        }

        if (!IsShipInAtmosphere())
        {
            return turnRate * VacuumManualTurnRateMultiplier;
        }

        double mass = Math.Max(vessel.GetMass(), 1.0);
        double massFactor = Math.Sqrt(mass / ManualTurnReferenceMassKg);
        double pressureFactor =
            lastDynamicPressure /
            (lastDynamicPressure + ManualTurnReferenceDynamicPressure);
        double authorityScale = 1.0 / (1.0 + massFactor * pressureFactor * 4.0);

        return turnRate * Math.Max(0.08, authorityScale);
    }

    void ApplyManualBodyRateInput(
        Vessel vessel,
        double dt,
        double turnRate,
        bool usePadAxes
    )
    {
        SimVector3 targetBodyRates = ShipManualAttitudeRamp.ResolveManualBodyRateTargets(
            vessel.controlState.pitchInput,
            vessel.controlState.yawInput,
            vessel.controlState.rollInput,
            usePadAxes,
            turnRate
        );

        ShipManualAttitudeRamp.RampTowardBodyRates(
            vessel,
            dt,
            targetBodyRates.x,
            targetBodyRates.y,
            targetBodyRates.z,
            turnRate,
            ManualAttitudeResponseTimeSeconds
        );
    }

    void ApplyVesselOrientationControl(Vessel vessel, double dt)
    {
        if (vessel == null || !VesselAllowsPadFlightControls(vessel) || dt <= 0.0)
        {
            return;
        }

        ShipOrientationInput input = BuildVesselOrientationInput(vessel);
        ShipStabilityAssist.NotifyOrientationMode(vessel, input.mode);

        if (input.mode == ShipOrientationMode.StabilityAssist)
        {
            ShipStabilityAssist.Apply(
                vessel,
                dt,
                ScenarioUsesSurfaceLaunchPad(),
                GetManualAttitudeTurnRateRadiansPerSecond(vessel),
                DefaultShipAutoTurnRateRadiansPerSecond,
                SasOrientationKp,
                SasOrientationKd
            );
            return;
        }

        if (input.mode == ShipOrientationMode.DevStabilityAssist)
        {
            ShipStabilityAssist.Apply(
                vessel,
                dt,
                ScenarioUsesSurfaceLaunchPad(),
                DefaultShipManualTurnRateRadiansPerSecond,
                DefaultShipAutoTurnRateRadiansPerSecond,
                DevSasOrientationKp,
                DevSasOrientationKd,
                unlimitedAuthority: true,
                rollTurnRateMultiplier: DevSasRollTurnRateMultiplier
            );
            return;
        }

        if (input.mode == ShipOrientationMode.Manual)
        {
            double turnRate = GetManualAttitudeTurnRateRadiansPerSecond(vessel);
            bool hasInput =
                Math.Abs(vessel.controlState.pitchInput) > 1e-6f ||
                Math.Abs(vessel.controlState.yawInput) > 1e-6f ||
                Math.Abs(vessel.controlState.rollInput) > 1e-6f;

            if (hasInput)
            {
                ApplyManualBodyRateInput(
                    vessel,
                    dt,
                    turnRate,
                    ScenarioUsesSurfaceLaunchPad()
                );
            }
            else
            {
                ShipManualAttitudeRamp.RampTowardBodyRates(
                    vessel,
                    dt,
                    0.0,
                    0.0,
                    0.0,
                    turnRate,
                    ManualAttitudeResponseTimeSeconds
                );
            }

            return;
        }

        SimVector3 targetDirection = GetVesselTargetDirection(vessel, input);

        if (targetDirection.LengthSquared() <= 1e-12)
        {
            return;
        }

        double maxAngularRate = input.autoTurnRateRadiansPerSecond;

        if (vessel == ship && IsShipInAtmosphere())
        {
            maxAngularRate *= input.atmosphericAutoTurnRateMultiplier;
        }

        if (maxAngularRate <= 0.0)
        {
            return;
        }

        SimVector3 currentHoldAxis = vessel.GetAttitudeHoldAxisWorld(input.mode);

        if (currentHoldAxis.LengthSquared() <= 1e-18)
        {
            return;
        }

        currentHoldAxis = currentHoldAxis.Normalize();
        SimVector3 targetDir = targetDirection.Normalize();
        double alignmentDot = Math.Clamp(
            SimVector3.Dot(currentHoldAxis, targetDir),
            -1.0,
            1.0
        );

        if (alignmentDot > 0.9995)
        {
            vessel.angularVelocity = SimVector3.Zero();
            return;
        }

        SimVector3 rotationErrorBody = vessel.orientation.InverseRotate(
            SimVector3.Cross(currentHoldAxis, targetDir)
        );
        SimVector3 angularAcceleration = rotationErrorBody.Multiply(SasOrientationKp)
            .Subtract(vessel.angularVelocity.Multiply(SasOrientationKd));
        double accelerationMagnitude = angularAcceleration.Length();
        double maxAngularAcceleration = maxAngularRate / ManualAttitudeResponseTimeSeconds;

        if (accelerationMagnitude > maxAngularAcceleration)
        {
            angularAcceleration = angularAcceleration.Multiply(
                maxAngularAcceleration / accelerationMagnitude
            );
        }

        SimVector3 nextAngularVelocity = vessel.angularVelocity.Add(
            angularAcceleration.Multiply(dt)
        );
        double nextRateMagnitude = nextAngularVelocity.Length();

        if (nextRateMagnitude > maxAngularRate)
        {
            nextAngularVelocity = nextAngularVelocity.Multiply(
                maxAngularRate / nextRateMagnitude
            );
        }

        vessel.angularVelocity = nextAngularVelocity;
    }

    bool VesselRequiresOrientationAssist(Vessel vessel)
    {
        if (vessel == null)
        {
            return false;
        }

        return vessel.controlState.orientationMode != ShipOrientationMode.Manual
            || Math.Abs(vessel.controlState.pitchInput) > 1e-6f
            || Math.Abs(vessel.controlState.yawInput) > 1e-6f
            || Math.Abs(vessel.controlState.rollInput) > 1e-6f;
    }

    bool VesselHasActivePropulsion(Vessel vessel)
    {
        if (vessel == null)
        {
            return false;
        }

        return vessel.controlState.throttle > 0.01f
            || vessel.controlState.rcsVector.LengthSquared() > 0.0
            || vessel.controlState.stagingRequested;
    }

    void IntegrateVesselAttitudeSubstep(Vessel vessel, double dt)
    {
        if (isOrbitPreview || vessel == null || !VesselAllowsPadFlightControls(vessel) || dt <= 0.0)
        {
            return;
        }

        ApplyVesselOrientationControl(vessel, dt);
        vessel.IntegrateOrientation(dt);
    }

    public void SetPendingRcsThrust(SimVector3 localThrust)
    {
        if (ship != null)
        {
            ship.controlState.rcsVector = localThrust;
        }
    }

    public void SetPendingRcsThrust(double localForward, double localRight)
    {
        if (ship != null)
        {
            ship.controlState.rcsVector = new SimVector3(localForward, localRight, 0.0);
        }
    }

    void ApplyRcsThrust(double dt)
    {
        ApplyVesselRcsThrust(ship, dt);
    }

    void ApplyVesselRcsThrust(Vessel vessel, double dt)
    {
        vessel.thrust = SimVector3.Zero();
        vessel.rcsAcceleration = SimVector3.Zero();

        if (!VesselAllowsPadFlightControls(vessel) || dt <= 0.0)
        {
            return;
        }

        SimVector3 localRcsCommand = vessel.controlState.rcsVector;

        if (localRcsCommand.LengthSquared() <= 0.0)
        {
            return;
        }

        double rcsBurned = Math.Min(Vessel.rcsConsumption * dt, vessel.rcsFuel);

        if (rcsBurned <= 0.0)
        {
            return;
        }

        vessel.rcsFuel -= rcsBurned;
        double thrustScale = rcsBurned / (Vessel.rcsConsumption * dt);
        ApplyRcsCommandToVessel(vessel, localRcsCommand, thrustScale, dt);
    }

    void ApplyRcsCommandToVessel(
        Vessel vessel,
        SimVector3 rcsCommand,
        double thrustScale,
        double dt
    )
    {
        if (
            vessel == null ||
            thrustScale <= 0.0 ||
            dt <= 0.0 ||
            rcsCommand.LengthSquared() <= 0.0
        )
        {
            return;
        }

        vessel.rcsAcceleration = SimVector3.Zero();

        SimVector3 lateralCommand = new SimVector3(
            0.0,
            rcsCommand.y,
            rcsCommand.z
        );

        if (lateralCommand.LengthSquared() > 0.0)
        {
            vessel.ApplyDistributedRcsThrust(lateralCommand, thrustScale, dt);
        }

        if (Math.Abs(rcsCommand.x) > 1e-9)
        {
            ApplyAxialRcsThrust(vessel, rcsCommand.x, thrustScale, dt);
        }
    }

    void ApplyAxialRcsThrust(
        Vessel vessel,
        double commandX,
        double thrustScale,
        double dt
    )
    {
        double mass = vessel.GetMass();

        if (mass <= 0.0 || Math.Abs(commandX) <= 1e-9)
        {
            return;
        }

        double axialForce =
            GetVesselAxialRcsForce(vessel) * Math.Abs(commandX) * thrustScale;

        if (axialForce <= 1e-9)
        {
            return;
        }

        SimVector3 forceWorld = vessel
            .GetForwardDirection()
            .Multiply(axialForce * Math.Sign(commandX));
        SimVector3 acceleration = forceWorld.Multiply(1.0 / mass);
        vessel.rcsAcceleration = vessel.rcsAcceleration.Add(acceleration);
        vessel.velocity = vessel.velocity.Add(acceleration.Multiply(dt));
    }

    static double GetVesselAxialRcsForce(Vessel vessel)
    {
        const double fallbackAxialForce = 3.0;
        double alignedThrust = 0.0;

        if (vessel.stages == null)
        {
            return fallbackAxialForce;
        }

        for (int i = 0; i < vessel.stages.Count; i++)
        {
            StageData stage = vessel.stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (block.rcsThrust <= 0.0)
                {
                    continue;
                }

                if (Math.Abs(block.thrustDirection.z) >= 0.75)
                {
                    alignedThrust += block.rcsThrust;
                }
            }
        }

        return alignedThrust > 0.0 ? alignedThrust : fallbackAxialForce;
    }

    void IntegrateShipOrientation(double dt)
    {
        if (ship.flightStatus != FlightStatus.Flying || dt <= 0.0)
        {
            return;
        }

        ship.IntegrateOrientation(dt);
    }

    public void SetAtmosphericPreviewEnabled(bool enabled)
    {
        enableAtmosphericPreview = enabled;
    }

    public bool IsAtmosphericPreviewEnabled()
    {
        return enableAtmosphericPreview;
    }

    public void ToggleAtmosphericPreviewEnabled()
    {
        enableAtmosphericPreview = !enableAtmosphericPreview;
    }

    public SimVector3 GetShipForwardDirection()
    {
        return ship.GetForwardDirection();
    }

    public double GetActiveStageThrust()
    {
        return ship.GetActiveStageThrust();
    }

    public int GetActiveStageNumber()
    {
        return ship.GetActiveStageNumber();
    }

    public int GetRemainingStageCount()
    {
        return ship.GetRemainingStageCount();
    }

    public string GetActiveStageName()
    {
        StageData activeStage = ship.GetActiveStage();
        return activeStage == null ? string.Empty : activeStage.name;
    }

    public string lastStagingMessage = string.Empty;
    public string lastFlightEventMessage = string.Empty;
    public string lastDockingMessage = string.Empty;

    public bool TryJettisonStage()
    {
        if (!StageSeparation(ship))
        {
            return false;
        }

        ship.thrust = SimVector3.Zero();
        return true;
    }

    public void ApplyNetworkTickPacket(NetworkTickPacket packet)
    {
        Vessel shipVessel = GetVesselById(NetworkInputFrame.Player1VesselId);

        if (shipVessel != null)
        {
            ApplyNetworkFrameToVesselControlState(
                shipVessel,
                packet.player1Frame,
                NetworkInputFrame.Player1VesselId
            );
        }

        Vessel stationVessel = GetVesselById(NetworkInputFrame.Player2VesselId);

        if (stationVessel != null)
        {
            ApplyNetworkFrameToVesselControlState(
                stationVessel,
                packet.player2Frame,
                NetworkInputFrame.Player2VesselId
            );
        }
    }

    public void ApplyLockstepTickInputs(
        NetworkInputFrame player1Frame,
        NetworkInputFrame player2Frame
    )
    {
        ApplyNetworkTickPacket(
            new NetworkTickPacket
            {
                player1Frame = player1Frame,
                player2Frame = player2Frame,
            }
        );
    }

    void ApplyNetworkFrameToVesselControlState(
        Vessel vessel,
        NetworkInputFrame frame,
        int vesselId
    )
    {
        vessel.controlState.throttle = frame.throttle;
        vessel.controlState.rcsVector = frame.rcsVector;

        if (!ShouldProtectClientOrientationMode(vesselId))
        {
            vessel.controlState.orientationMode = frame.orientationMode;
        }

        vessel.controlState.pitchInput = frame.pitchInput;
        vessel.controlState.yawInput = frame.yawInput;
        vessel.controlState.rollInput = frame.rollInput;
        vessel.controlState.stagingRequested = frame.staging;
    }

    bool ShouldProtectClientOrientationMode(int vesselId)
    {
        return !IsServerContext &&
            ClientLocalOrientationVesselId > 0 &&
            vesselId == ClientLocalOrientationVesselId;
    }

    void ProcessVesselStagingRequest(Vessel vessel)
    {
        if (vessel == null || !vessel.controlState.stagingRequested)
        {
            return;
        }

        vessel.controlState.stagingRequested = false;

        if (vessel == ship)
        {
            TryJettisonStage();
        }
        else
        {
            StageSeparation(vessel);
        }
    }

    public void AdvanceLockstepPausedTick()
    {
        currentTick++;
    }

    public Vessel GetVesselById(int vesselId)
    {
        for (int i = 0; i < vessels.Count; i++)
        {
            Vessel vessel = vessels[i];

            if (GetVesselId(vessel) == vesselId)
            {
                return vessel;
            }
        }

        return null;
    }

    public bool StageSeparation(Vessel vessel)
    {
        if (vessel == null || vessel.stages == null || vessel.stages.Count == 0)
        {
            return false;
        }

        if (vessel.activeStageIndex < 0 || vessel.activeStageIndex >= vessel.stages.Count)
        {
            return false;
        }

        if (vessel.stages.Count <= 1)
        {
            lastStagingMessage = "No stage to jettison";
            return false;
        }

        StageData separatedStage = vessel.stages[vessel.activeStageIndex];
        string jettisonedName = separatedStage.name;
        SimVector3 comBeforeLocal = vessel.centerOfMassLocal;
        double separatedMass = Vessel.ComputeStageMass(separatedStage);
        SimVector3 separatedStageComLocal = Vessel.ComputeStageCenterOfMassLocal(
            separatedStage,
            separatedStage.stageMountOffset
        );

        Vessel debris = new Vessel(
            jettisonedName + " Debris",
            vessel.position,
            vessel.velocity
        );
        debris.orientation = vessel.orientation;
        debris.heading = vessel.heading;
        debris.flightStatus = FlightStatus.Flying;
        debris.stages = new List<StageData> { separatedStage.Copy() };
        debris.activeStageIndex = 0;
        debris.NotifyShipLayoutChanged();
        debris.RecalculateMassAndDeltaV(
            GetShipLocalGravityRelativeToActivePrimary()
        );
        TryEnterKeplerRailsForVessel(debris);

        int activeStageIndex = vessel.activeStageIndex;
        int debrisId = (int)(
            currentTick * 100000L +
            (long)GetVesselId(vessel) * 10L +
            activeStageIndex
        );
        debrisVessels.Add(debris);
        RegisterVesselInRegistry(debris, debrisId);

        vessel.stages.RemoveAt(vessel.activeStageIndex);
        vessel.thrust = SimVector3.Zero();
        vessel.NotifyShipLayoutChanged();
        vessel.ApplyStagingSeparationImpulse(
            separatedStageComLocal,
            separatedMass,
            comBeforeLocal
        );
        vessel.RecalculateMassAndDeltaV(
            GetShipLocalGravityRelativeToActivePrimary()
        );
        lastStagingMessage = jettisonedName + " jettisoned";
        return true;
    }

    public void ConfigureShipStages(List<StageData> stages)
    {
        if (stages == null || stages.Count == 0)
        {
            return;
        }

        ship.stages = Vessel.CopyStages(stages);
        ship.activeStageIndex = 0;
        ship.thrust = SimVector3.Zero();
        ship.NotifyShipLayoutChanged();
        ship.RecalculateMassAndDeltaV(
            GetShipLocalGravityRelativeToActivePrimary()
        );

        if (ship.flightStatus == FlightStatus.Landed)
        {
            FinalizeLandedShipOnSurface();
        }
    }

    public void FinalizeLandedShipOnSurface()
    {
        if (ship.flightStatus != FlightStatus.Landed)
        {
            return;
        }

        Body surfaceBody = GetActivePrimaryBody();
        ApplySurfaceLaunchOrientation(ship);
        EnsurePadLaunchBodyFixedDirection(surfaceBody);
        SimVector3 surfaceNormal = GetBodyFixedSurfaceNormal(surfaceBody);
        PinShipToSurface(surfaceBody, surfaceNormal);
        ship.velocity = GetLandedSurfaceVelocity(surfaceBody);
        ship.angularVelocity = SimVector3.Zero();
    }

    public void SaveShip(string path)
    {
        ship.SaveShip(path);
    }

    public List<StageData> LoadShip(string path)
    {
        return Vessel.LoadShip(path);
    }

    public int GetWorldRevision()
    {
        return worldRevision;
    }

    const ulong ChecksumFnvOffsetBasis = 14695981039346656037UL;
    const ulong ChecksumFnvPrime = 1099511628382471UL;

    public ulong CalculateStateChecksum()
    {
        ulong hash = ChecksumFnvOffsetBasis;
        hash = MixChecksumVessel(hash, ship);

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            hash = MixChecksumVessel(hash, dockableVessels[i]);
        }

        for (int i = 0; i < debrisVessels.Count; i++)
        {
            hash = MixChecksumVessel(hash, debrisVessels[i]);
        }

        return hash;
    }

    static ulong MixChecksumULong(ulong hash, ulong value)
    {
        return (hash ^ value) * ChecksumFnvPrime;
    }

    static ulong MixChecksumLong(ulong hash, long value)
    {
        return MixChecksumULong(hash, unchecked((ulong)value));
    }

    static ulong MixChecksumInt(ulong hash, int value)
    {
        return MixChecksumULong(hash, unchecked((uint)value));
    }

    static ulong MixChecksumDouble(ulong hash, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return hash;
        }

        double rounded = Math.Round(value, 4);
        return MixChecksumLong(hash, BitConverter.DoubleToInt64Bits(rounded));
    }

    static ulong MixChecksumBool(ulong hash, bool value)
    {
        return MixChecksumULong(hash, value ? 1UL : 0UL);
    }

    static ulong MixChecksumBody(ulong hash, Body body)
    {
        hash = MixChecksumDouble(hash, body.position.x);
        hash = MixChecksumDouble(hash, body.position.y);
        hash = MixChecksumDouble(hash, body.position.z);
        hash = MixChecksumDouble(hash, body.velocity.x);
        hash = MixChecksumDouble(hash, body.velocity.y);
        hash = MixChecksumDouble(hash, body.velocity.z);
        return hash;
    }

    static ulong MixChecksumVector3(ulong hash, SimVector3 value)
    {
        hash = MixChecksumDouble(hash, value.x);
        hash = MixChecksumDouble(hash, value.y);
        hash = MixChecksumDouble(hash, value.z);
        return hash;
    }

    static ulong MixChecksumVector3Centimeters(ulong hash, SimVector3 value)
    {
        hash = MixChecksumDoubleRounded(hash, value.x, 2);
        hash = MixChecksumDoubleRounded(hash, value.y, 2);
        hash = MixChecksumDoubleRounded(hash, value.z, 2);
        return hash;
    }

    static ulong MixChecksumDoubleRounded(ulong hash, double value, int decimals)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return hash;
        }

        double rounded = Math.Round(value, decimals);
        return MixChecksumLong(hash, BitConverter.DoubleToInt64Bits(rounded));
    }

    static ulong MixChecksumQuaternion(ulong hash, SimQuaternion value)
    {
        hash = MixChecksumDouble(hash, value.w);
        hash = MixChecksumDouble(hash, value.x);
        hash = MixChecksumDouble(hash, value.y);
        hash = MixChecksumDouble(hash, value.z);
        return hash;
    }

    ulong MixChecksumVessel(ulong hash, Vessel vessel)
    {
        if (vessel == null || vessel.IsDestroyed)
        {
            return hash;
        }

        hash = MixChecksumInt(hash, GetVesselId(vessel));
        hash = MixChecksumInt(hash, (int)vessel.flightStatus);
        hash = MixChecksumVector3Centimeters(hash, vessel.GetCenterOfMassWorld());
        hash = MixChecksumVector3Centimeters(hash, vessel.velocity);

        if (vessel.stages == null)
        {
            return hash;
        }

        for (int stageIndex = 0; stageIndex < vessel.stages.Count; stageIndex++)
        {
            StageData stage = vessel.stages[stageIndex];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int blockIndex = 0; blockIndex < stage.blocks.Count; blockIndex++)
            {
                ShipBlock block = stage.blocks[blockIndex];
                hash = MixChecksumInt(hash, block.blockId);
                hash = MixChecksumBool(hash, block.isDestroyed);
            }
        }

        return hash;
    }

    public WorldSnapshot CaptureSnapshot()
    {
        WorldSnapshot snapshot = new WorldSnapshot
        {
            currentTick = currentTick,
            universalTime = simulationTime,
            activeVesselId = GetVesselId(ship),
            scenario = scenario,
        };

        CaptureCelestialBodies(snapshot);
        CaptureAllVessels(snapshot);
        return snapshot;
    }

    public void LoadSnapshot(WorldSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        currentTick = snapshot.currentTick;
        simulationTime = snapshot.universalTime;

        if (snapshot.currentTick == 0)
        {
            currentTick = 0;
            simulationTime = 0.0;
        }

        scenario = snapshot.scenario;

        RestoreCelestialBodies(snapshot);

        List<Vessel> dockablesToRemove = new List<Vessel>(dockableVessels);

        for (int i = 0; i < dockablesToRemove.Count; i++)
        {
            RemoveVesselFromSimulation(dockablesToRemove[i]);
        }

        List<Vessel> debrisToRemove = new List<Vessel>(debrisVessels);

        for (int i = 0; i < debrisToRemove.Count; i++)
        {
            RemoveVesselFromSimulation(debrisToRemove[i]);
        }

        debrisVessels.Clear();
        dockableVessels.Clear();
        vessels.Clear();
        vesselIds.Clear();
        nextVesselId = 1;
        targetVessel = null;

        Dictionary<int, Vessel> restoredVessels = new Dictionary<int, Vessel>();
        double localGravity = GetShipLocalGravityRelativeToActivePrimary();

        for (int i = 0; i < snapshot.vessels.Count; i++)
        {
            VesselSnapshotData vesselData = snapshot.vessels[i];
            Vessel vessel;

            if (vesselData.type == VesselSnapshotType.Player)
            {
                vessel = ship;
            }
            else
            {
                vessel = new Vessel(
                    vesselData.name,
                    vesselData.position,
                    vesselData.velocity
                );
            }

            ApplyVesselSnapshotData(vessel, vesselData, localGravity);
            RegisterVesselInRegistry(vessel, vesselData.id);
            restoredVessels[vesselData.id] = vessel;

            if (vesselData.type == VesselSnapshotType.Dockable)
            {
                RegisterDockableVessel(vessel);
            }
            else if (vesselData.type == VesselSnapshotType.Debris)
            {
                debrisVessels.Add(vessel);
            }
        }

        if (
            snapshot.activeVesselId > 0 &&
            restoredVessels.TryGetValue(snapshot.activeVesselId, out Vessel activeVessel)
        )
        {
            ship = activeVessel;
        }

        for (int i = 0; i < snapshot.vessels.Count; i++)
        {
            VesselSnapshotData vesselData = snapshot.vessels[i];

            if (vesselData.targetVesselId <= 0)
            {
                continue;
            }

            if (
                !restoredVessels.TryGetValue(vesselData.id, out Vessel vessel) ||
                !restoredVessels.TryGetValue(
                    vesselData.targetVesselId,
                    out Vessel targetVesselRestored
                )
            )
            {
                continue;
            }

            if (vessel == ship)
            {
                targetVessel = targetVesselRestored;
            }
        }

        if (ScenarioIsVacuumDocking() && ship != null)
        {
            activePrimaryBodyName = ship.name;
            lastActivePrimaryForHysteresis = ship.name;
        }

        EnforceMultiplayerSlotVesselIds();

        worldRevision++;
    }

    public void EnforceMultiplayerSlotVesselIds()
    {
        if (scenario != SimulationScenario.DockingApproach)
        {
            return;
        }

        if (ship != null)
        {
            RegisterVesselInRegistry(ship, NetworkInputFrame.Player1VesselId);
        }

        Vessel station = targetVessel;

        if (station == null)
        {
            for (int i = 0; i < dockableVessels.Count; i++)
            {
                if (dockableVessels[i] != ship)
                {
                    station = dockableVessels[i];
                    break;
                }
            }
        }

        if (station != null)
        {
            RegisterVesselInRegistry(station, NetworkInputFrame.Player2VesselId);
        }
    }

    void CaptureCelestialBodies(WorldSnapshot snapshot)
    {
        if (sun != null)
        {
            snapshot.bodies.Add(CreateBodySnapshot(sun));
        }

        if (planet != null)
        {
            snapshot.bodies.Add(CreateBodySnapshot(planet));
        }

        if (moon != null)
        {
            snapshot.bodies.Add(CreateBodySnapshot(moon));
        }
    }

    static BodySnapshotData CreateBodySnapshot(Body body)
    {
        return new BodySnapshotData
        {
            name = body.name,
            position = body.position,
            velocity = body.velocity
        };
    }

    void CaptureAllVessels(WorldSnapshot snapshot)
    {
        CaptureVesselSnapshot(snapshot, ship, VesselSnapshotType.Player);

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            CaptureVesselSnapshot(
                snapshot,
                dockableVessels[i],
                VesselSnapshotType.Dockable
            );
        }

        for (int i = 0; i < debrisVessels.Count; i++)
        {
            CaptureVesselSnapshot(
                snapshot,
                debrisVessels[i],
                VesselSnapshotType.Debris
            );
        }
    }

    void CaptureVesselSnapshot(
        WorldSnapshot snapshot,
        Vessel vessel,
        VesselSnapshotType type
    )
    {
        if (vessel == null)
        {
            return;
        }

        VesselSnapshotData vesselData = new VesselSnapshotData
        {
            id = GetVesselId(vessel),
            name = vessel.name,
            type = type,
            flightStatus = vessel.flightStatus,
            targetVesselId =
                vessel == ship && targetVessel != null ?
                GetVesselId(targetVessel) :
                0,
            position = vessel.position,
            velocity = vessel.velocity,
            rotation = vessel.orientation,
            angularVelocity = vessel.angularVelocity,
            activeStageIndex = vessel.activeStageIndex,
            stages = Vessel.CopyStages(vessel.stages),
            blockStates = CaptureBlockStates(vessel),
            landedOnBodyName = vessel.landedOnBodyName,
            isDocked = vessel.IsDocked,
            isOnKeplerRails = vessel.isOnKeplerRails,
            keplerRails = vessel.keplerRails
        };
        snapshot.vessels.Add(vesselData);
    }

    static List<BlockSnapshotState> CaptureBlockStates(Vessel vessel)
    {
        List<BlockSnapshotState> blockStates = new List<BlockSnapshotState>();

        if (vessel.stages == null)
        {
            return blockStates;
        }

        for (int i = 0; i < vessel.stages.Count; i++)
        {
            StageData stage = vessel.stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];
                blockStates.Add(
                    new BlockSnapshotState
                    {
                        blockId = block.blockId,
                        fuelMass = block.fuelMass,
                        isDestroyed = block.isDestroyed
                    }
                );
            }
        }

        return blockStates;
    }

    void ApplyVesselSnapshotData(
        Vessel vessel,
        VesselSnapshotData vesselData,
        double localGravity
    )
    {
        vessel.name = vesselData.name;
        vessel.position = vesselData.position;
        vessel.velocity = vesselData.velocity;
        vessel.orientation = vesselData.rotation;
        vessel.heading = Math.Atan2(
            vessel.orientation.Forward().x,
            vessel.orientation.Forward().z
        );
        vessel.angularVelocity = vesselData.angularVelocity;
        vessel.flightStatus = vesselData.flightStatus;
        vessel.landedOnBodyName = vesselData.landedOnBodyName ?? string.Empty;
        vessel.IsDocked = vesselData.isDocked;
        vessel.isOnKeplerRails = vesselData.isOnKeplerRails;
        vessel.keplerRails = vesselData.keplerRails;
        vessel.stages = Vessel.CopyStages(vesselData.stages);
        vessel.activeStageIndex = vesselData.activeStageIndex;
        vessel.thrust = SimVector3.Zero();
        vessel.thrustAcceleration = SimVector3.Zero();
        vessel.dragAcceleration = SimVector3.Zero();
        vessel.pendingDestroyedBlockIds.Clear();
        ApplyBlockStates(vessel, vesselData.blockStates);
        vessel.NotifyShipLayoutChanged();
        vessel.RecalculateMassAndDeltaV(localGravity);

        if (vessel.flightStatus == FlightStatus.Landed)
        {
            Body surfaceBody = GetBodyByName(vessel.landedOnBodyName);

            if (surfaceBody != null)
            {
                SimVector3 surfaceNormal = GetSurfaceNormal(surfaceBody);
                vessel.StabilizeLandedOrientation(surfaceNormal);
                vessel.angularVelocity = SimVector3.Zero();
            }
        }
    }

    static void ApplyBlockStates(
        Vessel vessel,
        List<BlockSnapshotState> blockStates
    )
    {
        if (
            vessel.stages == null ||
            blockStates == null ||
            blockStates.Count == 0
        )
        {
            return;
        }

        Dictionary<int, BlockSnapshotState> blockStateById =
            new Dictionary<int, BlockSnapshotState>();

        for (int i = 0; i < blockStates.Count; i++)
        {
            BlockSnapshotState blockState = blockStates[i];
            blockStateById[blockState.blockId] = blockState;
        }

        for (int i = 0; i < vessel.stages.Count; i++)
        {
            StageData stage = vessel.stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!blockStateById.TryGetValue(block.blockId, out BlockSnapshotState blockState))
                {
                    continue;
                }

                block.fuelMass = blockState.fuelMass;
                block.isDestroyed = blockState.isDestroyed;
            }
        }
    }

    void RestoreCelestialBodies(WorldSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.bodies.Count; i++)
        {
            BodySnapshotData bodyData = snapshot.bodies[i];
            Body body = GetBodyByName(bodyData.name);

            if (body == null)
            {
                continue;
            }

            body.position = bodyData.position;
            body.velocity = bodyData.velocity;
        }
    }

    public int GetVesselId(Vessel vessel)
    {
        if (vessel == null)
        {
            return 0;
        }

        if (vesselIds.TryGetValue(vessel, out int existingId))
        {
            return existingId;
        }

        return RegisterVesselInRegistry(vessel);
    }

    int RegisterVesselInRegistry(Vessel vessel)
    {
        return RegisterVesselInRegistry(vessel, nextVesselId++);
    }

    int RegisterVesselInRegistry(Vessel vessel, int vesselId)
    {
        if (vessel == null)
        {
            return 0;
        }

        if (!vessels.Contains(vessel))
        {
            vessels.Add(vessel);
        }

        vesselIds[vessel] = vesselId;

        if (vesselId >= nextVesselId)
        {
            nextVesselId = vesselId + 1;
        }

        return vesselId;
    }

    void UnregisterVesselFromRegistry(Vessel vessel)
    {
        if (vessel == null)
        {
            return;
        }

        vessels.Remove(vessel);
        vesselIds.Remove(vessel);
    }

    public DebrisState[] GetDebrisStates()
    {
        if (debrisVessels == null || debrisVessels.Count == 0)
        {
            return Array.Empty<DebrisState>();
        }

        DebrisState[] states = new DebrisState[debrisVessels.Count];

        for (int i = 0; i < debrisVessels.Count; i++)
        {
            Vessel debris = debrisVessels[i];
            string stageName = debris.name;

            if (stageName.EndsWith(" Debris"))
            {
                stageName = stageName.Substring(0, stageName.Length - " Debris".Length);
            }

            states[i] = new DebrisState(
                debris.name,
                stageName,
                debris.GetCenterOfMassWorld(),
                debris.velocity,
                debris.heading,
                debris.centerOfMassLocal,
                debris.GetShipBlockViews()
            );
        }

        return states;
    }

    public ShipBlockView[] GetShipBlocks()
    {
        return ship.GetShipBlockViews();
    }

    public int GetShipLayoutRevision()
    {
        return ship.shipLayoutRevision;
    }

    public int[] ConsumePendingDestroyedBlockIds()
    {
        return ship.DrainPendingDestroyedBlockIds();
    }

    public SimVector3 GetShipCenterOfMassWorld()
    {
        return ship.GetCenterOfMassWorld();
    }

    public SimVector3 GetShipCenterOfMassLocal()
    {
        return ship.centerOfMassLocal;
    }

    public double GetShipEnvelopeRadius()
    {
        return ship.envelopeRadius;
    }

    public SimVector3[] GetDebrisOrbitPathRelativeToActivePrimary(
        int debrisIndex,
        int pointCount
    )
    {
        if (
            debrisVessels == null ||
            debrisIndex < 0 ||
            debrisIndex >= debrisVessels.Count
        )
        {
            return Array.Empty<SimVector3>();
        }

        Vessel debris = debrisVessels[debrisIndex];

        return OrbitalMechanics.BuildOrbitPathRelativeToBody(
            GetActivePrimaryBody(),
            debris.position,
            debris.velocity,
            pointCount
        );
    }

    public void ApplyLoadedShip(string path)
    {
        ConfigureShipStages(LoadShip(path));
    }

    public void RefreshInitialShipSnapshot()
    {
        initialShipSnapshot = CaptureShipRespawnSnapshot();
    }

    public bool TryApplySpawnShip(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            ApplyLoadedShip(path);
            RefreshInitialShipSnapshot();
            return true;
        }
        catch
        {
            return false;
        }
    }

    void ConfigureDefaultStages(Vessel vessel)
    {
        if (SimulationScenarios.IsAnySurfacePad(scenario))
        {
            vessel.stages = new List<StageData>();
            vessel.activeStageIndex = 0;
            ApplySurfaceLaunchOrientation(vessel);
            return;
        }

        vessel.stages = new List<StageData>
        {
            StageData.CreateBooster(1.0),
            StageData.CreateOrbiter(1.0),
        };
        vessel.activeStageIndex = 0;
    }

    void ConfigureDockingScenario(Vessel station)
    {
        ship.stages = new List<StageData>
        {
            StageData.CreateOrbiter(0.75),
        };
        ship.activeStageIndex = 0;
        ship.payloadMass = 0.0;
        ship.IsDocked = false;

        station.stages = new List<StageData>
        {
            StageData.CreateOrbiter(1.0),
        };
        station.activeStageIndex = 0;
        station.payloadMass = 5.0;

        StageData.PrepareDockingStage(ship.GetActiveStage());
        StageData.PrepareDockingStage(station.GetActiveStage());
        ship.NotifyShipLayoutChanged();
        station.NotifyShipLayoutChanged();
        ship.useHonestDockingEnvelope = true;
        station.useHonestDockingEnvelope = true;
        ship.SyncDockingPortForwardOffset();
        station.SyncDockingPortForwardOffset();

        double localGravity = GetShipLocalGravityRelativeToActivePrimary();
        ship.RecalculateMassAndDeltaV(localGravity);
        station.RecalculateMassAndDeltaV(localGravity);

        DockingScenarioSetup.ConfigureVacuumApproach(
            out SimVector3 stationPosition,
            out SimVector3 stationVelocity,
            out _,
            out SimVector3 shipPosition,
            out SimVector3 shipVelocity,
            out _
        );
        station.position = stationPosition;
        station.velocity = stationVelocity;
        ship.position = shipPosition;
        ship.velocity = shipVelocity;

        RegisterVesselInRegistry(ship, NetworkInputFrame.Player1VesselId);
        RegisterVesselInRegistry(station, NetworkInputFrame.Player2VesselId);
        RegisterDockableVessel(station);
        targetVessel = station;
        AlignDockingApproachOrientations(ship, station);
        EnsureVacuumDockingSpawnClearance(ship, station);
    }

    void EnsureVacuumDockingSpawnClearance(Vessel playerShip, Vessel targetStation)
    {
        if (playerShip == null || targetStation == null)
        {
            return;
        }

        playerShip.RecalculatePhysicsEnvelopeForDocking();
        targetStation.RecalculatePhysicsEnvelopeForDocking();

        double separation = DockingScenarioSetup.ResolveSpawnSeparation(
            playerShip.envelopeRadius,
            targetStation.envelopeRadius
        );
        SimVector3 approachAxis = targetStation.position.Subtract(playerShip.position);

        if (approachAxis.LengthSquared() <= 1e-12)
        {
            approachAxis = SimVector3.UnitZ();
        }
        else
        {
            approachAxis = approachAxis.Normalize();
        }

        playerShip.position = targetStation.position.Add(
            approachAxis.Multiply(-separation)
        );
        AlignDockingApproachOrientations(playerShip, targetStation);
    }

    static bool ShouldCalibrateOrbitalSpawn(SimulationScenario scenario)
    {
        return
            scenario == SimulationScenario.PlanetMoonShip ||
            scenario == SimulationScenario.MoonFlyby ||
            scenario == SimulationScenario.PlanetMoonEllipticMoon ||
            scenario == SimulationScenario.TwoBodyCircular;
    }

    void ConfigureOrbitalScenario()
    {
        if (planet == null || ship == null)
        {
            return;
        }

        double orbitRadius = CelestialBody.Earth.GetLowOrbitRadius();
        double circularSpeed = CelestialBody.Earth.GetLowOrbitCircularSpeed();

        ship.position = planet.position.Add(
            new SimVector3(-orbitRadius, 0, 0)
        );
        ship.velocity = planet.velocity.Add(
            new SimVector3(0, circularSpeed, 0)
        );
        ship.flightStatus = FlightStatus.Flying;
        ship.landedOnBodyName = string.Empty;
        ship.landedSurfaceDirectionBodyFixed = SimVector3.Zero();
    }

    static void AlignDockingApproachOrientations(Vessel playerShip, Vessel targetStation)
    {
        SimVector3 approachAxis = targetStation.position.Subtract(playerShip.position);

        if (approachAxis.LengthSquared() <= 1e-12)
        {
            approachAxis = SimVector3.UnitZ();
        }
        else
        {
            approachAxis = approachAxis.Normalize();
        }

        SetVesselForwardAndUp(playerShip, approachAxis, SimVector3.UnitY());
        SetVesselForwardAndUp(
            targetStation,
            approachAxis.Multiply(-1.0),
            SimVector3.UnitY()
        );
    }

    void ConfigureSurfaceLaunchStages(Vessel vessel)
    {
        vessel.stages = BuildSurfaceLaunchStageList();
        vessel.activeStageIndex = 0;
        ApplySurfaceLaunchOrientation(vessel);
        vessel.NotifyShipLayoutChanged();
    }

    void ApplySurfaceLaunchOrientation(Vessel vessel)
    {
        if (vessel == null)
        {
            return;
        }

        SimVector3 surfaceNormal = GetVesselSurfaceUpForOrientation(vessel);
        SimVector3 surfaceEast = LandingConfig.GetSurfaceEastTangent(surfaceNormal);
        vessel.orientation = SimQuaternion.FromForwardAndUp(surfaceEast, surfaceNormal);
        vessel.angularVelocity = SimVector3.Zero();
        vessel.SyncHeadingFromOrientation();
    }

    SimVector3 GetVesselSurfaceUpForOrientation(Vessel vessel)
    {
        Body primary = GetActivePrimaryBody();

        if (
            ScenarioUsesSurfaceLaunchPad() &&
            vessel != null &&
            vessel.landedSurfaceDirectionBodyFixed.LengthSquared() > 1e-12 &&
            primary != null
        )
        {
            return primary
                .RotateBodyFixedToWorld(vessel.landedSurfaceDirectionBodyFixed)
                .Normalize();
        }

        return GetVesselSurfaceUp(vessel);
    }

    SimVector3 GetBodyFixedSurfaceNormal(Body primary)
    {
        if (
            primary == null ||
            ship == null ||
            ship.landedSurfaceDirectionBodyFixed.LengthSquared() <= 1e-12
        )
        {
            return GetShipSurfaceUp();
        }

        return primary
            .RotateBodyFixedToWorld(ship.landedSurfaceDirectionBodyFixed)
            .Normalize();
    }

    void EnsurePadLaunchBodyFixedDirection(Body primary)
    {
        if (
            primary == null ||
            ship == null ||
            ship.landedSurfaceDirectionBodyFixed.LengthSquared() > 1e-12
        )
        {
            return;
        }

        CaptureLandedSurfaceDirectionBodyFixed(primary, GetShipSurfaceUp());
    }

    void SyncLandedVesselToRotatingSurface(Body primary, Vessel vessel)
    {
        if (primary == null || vessel == null)
        {
            return;
        }

        EnsurePadLaunchBodyFixedDirection(primary);

        SimVector3 surfaceNormal = GetBodyFixedSurfaceNormal(primary);
        PinShipToSurface(primary, surfaceNormal);
        SimVector3 surfaceEast = LandingConfig.GetSurfaceEastTangent(surfaceNormal);
        vessel.orientation = SimQuaternion.FromForwardAndUp(surfaceEast, surfaceNormal);
        vessel.SyncHeadingFromOrientation();
        vessel.velocity = GetVesselSurfaceVelocity(primary, vessel);
        vessel.angularVelocity = SimVector3.Zero();
    }

    public SimVector3 GetVesselSurfaceUp(Vessel vessel)
    {
        Body primary = GetActivePrimaryBody();

        if (primary == null || vessel == null)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        SimVector3 offset = vessel.GetCenterOfMassWorld().Subtract(primary.position);

        if (offset.LengthSquared() <= 1e-12)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        return offset.Normalize();
    }

    public SimVector3 GetShipSurfaceUp()
    {
        return GetVesselSurfaceUp(ship);
    }

    public bool IsShipOnCoveLaunchPad()
    {
        return
            ScenarioUsesSurfaceLaunchPad() &&
            GetShipSurfaceDistanceFromLaunchSiteMeters() <=
                LaunchPadConstants.LaunchSitePadVisibilityRadiusMeters;
    }

    static List<StageData> BuildSurfaceLaunchStageList()
    {
        const double s1DryMass = 15_000.0;
        const double s1FuelMass = 45_000.0;
        const double s2DryMass = 6_000.0;
        const double s2FuelMass = 24_000.0;
        const double s3DryMass = 2_500.0;
        const double s3FuelMass = 3_500.0;

        double launchMass =
            s1DryMass + s1FuelMass +
            s2DryMass + s2FuelMass +
            s3DryMass + s3FuelMass;

        double s1Thrust = LandingConfig.ComputeCappedSurfaceBoosterThrust(
            launchMass,
            s1FuelMass,
            LandingConfig.SurfaceLaunchTargetTwr
        );
        double s1MassFlow = s1FuelMass / LandingConfig.SurfaceLaunchBurnSeconds;

        double s2Mass = s2DryMass + s2FuelMass + s3DryMass + s3FuelMass;
        double s2Thrust = LandingConfig.ComputeOrbitalStageThrust(
            s2Mass,
            LandingConfig.SurfaceLowOrbitAltitude,
            LandingConfig.OrbitalStageTargetTwr
        );
        const double s2MassFlow = 90.0;

        double s3Mass = s3DryMass + s3FuelMass;
        double s3Thrust = LandingConfig.ComputeOrbitalStageThrust(
            s3Mass,
            LandingConfig.SurfaceLowOrbitAltitude,
            LandingConfig.OrbitalStageTargetTwr
        );
        const double s3MassFlow = 15.0;

        return new List<StageData>
        {
            StageData.CreateLaunchStage(
                "S1 Booster",
                s1DryMass,
                s1FuelMass,
                s1Thrust,
                s1MassFlow,
                0.25
            ),
            StageData.CreateLaunchStage(
                "S2 Orbital",
                s2DryMass,
                s2FuelMass,
                s2Thrust,
                s2MassFlow,
                0.22
            ),
            StageData.CreateLaunchStage(
                "S3 Upper",
                s3DryMass,
                s3FuelMass,
                s3Thrust,
                s3MassFlow,
                0.20
            ),
        };
    }

    internal bool ScenarioUsesSurfaceLaunchPad()
    {
        return SimulationScenarios.IsAnySurfacePad(scenario);
    }

    bool ShipUsesExportedBlockLayout()
    {
        if (ship?.stages == null)
        {
            return false;
        }

        for (int i = 0; i < ship.stages.Count; i++)
        {
            StageData stage = ship.stages[i];

            if (stage?.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (
                    block.hasVabAttach ||
                    !string.IsNullOrWhiteSpace(block.partTemplateId)
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void EnsureSurfaceLaunchThrust()
    {
        if (!SimulationScenarios.IsPlanetSurfacePad(scenario))
        {
            return;
        }

        if (ShipUsesExportedBlockLayout())
        {
            return;
        }

        if (ship.stages == null || ship.stages.Count == 0)
        {
            return;
        }

        List<StageData> tunedStages = BuildSurfaceLaunchStageList();

        if (ship.stages == null || ship.stages.Count != tunedStages.Count)
        {
            ship.stages = Vessel.CopyStages(tunedStages);
            ship.activeStageIndex = 0;
            ship.NotifyShipLayoutChanged();
            ship.RecalculateMassAndDeltaV(
                GetShipLocalGravityRelativeToActivePrimary()
            );
            RefreshInitialShipSnapshot();
            return;
        }

        bool needsRetune = false;

        for (int i = 0; i < tunedStages.Count; i++)
        {
            StageData currentStage = ship.stages[i];
            StageData tunedStage = tunedStages[i];

            if (
                Math.Abs(currentStage.thrust - tunedStage.thrust) >
                tunedStage.thrust * 0.05 ||
                Math.Abs(currentStage.dryMass - tunedStage.dryMass) >
                tunedStage.dryMass * 0.05 ||
                Math.Abs(currentStage.maxFuelMass - tunedStage.maxFuelMass) >
                tunedStage.maxFuelMass * 0.05
            )
            {
                needsRetune = true;
                break;
            }
        }

        if (!needsRetune)
        {
            return;
        }

        ship.stages = Vessel.CopyStages(tunedStages);
        ship.activeStageIndex = Math.Clamp(
            ship.activeStageIndex,
            0,
            ship.stages.Count - 1
        );
        ship.NotifyShipLayoutChanged();
        ship.RecalculateMassAndDeltaV(
            GetShipLocalGravityRelativeToActivePrimary()
        );
        RefreshInitialShipSnapshot();
    }

    public void ApplyShipDeltaV(SimVector3 deltaV)
    {
        ship.velocity = ship.velocity.Add(deltaV);
    }

    internal bool ScenarioHasMoon()
    {
        return moon != null;
    }

    /// <summary>
    /// Globe under the ship for flight LOD / dual-frame. Follows Moon SoI when the
    /// ship is inside it; otherwise Earth. Pad spawn still starts on the scenario body.
    /// </summary>
    internal Body GetVisualTerrainBody()
    {
        Body primary = GetActivePrimaryBody();

        if (primary != null && moon != null && primary == moon)
        {
            return moon;
        }

        return planet;
    }

    public double GetShipAltitudeAboveVisualTerrainBody()
    {
        return GetShipAltitudeAboveVisualTerrainBody(ship.GetCenterOfMassWorld());
    }

    public double GetShipAltitudeAboveVisualTerrainBody(SimVector3 centerOfMassWorld)
    {
        Body terrainBody = GetVisualTerrainBody();

        if (terrainBody == null)
        {
            return 0.0;
        }

        return GetShipAltitudeAboveBody(terrainBody, centerOfMassWorld);
    }

    internal bool ScenarioHasSun()
    {
        return sun != null;
    }

    /// <summary>Root parent (Sun): infinite SOI; patched conics never switch above it.</summary>
    internal bool IsRootBody(Body body)
    {
        return body != null && body.isRootBody;
    }

    /// <summary>Finite Laplace SOI exists only for non-root children (Earth, Moon, …).</summary>
    internal bool HasFiniteSphereOfInfluence(Body body)
    {
        return TryGetFiniteSphereOfInfluenceRadius(body, out _);
    }

    internal bool TryGetFiniteSphereOfInfluenceRadius(Body body, out double radiusMeters)
    {
        radiusMeters = 0.0;

        if (body == null || IsRootBody(body))
        {
            return false;
        }

        radiusMeters = body.sphereOfInfluenceRadius;

        if (radiusMeters <= 0.0 && body == planet)
        {
            radiusMeters = planetSphereOfInfluenceRadius;
        }
        else if (radiusMeters <= 0.0 && body == moon)
        {
            radiusMeters = moonSphereOfInfluenceRadius;
        }

        return radiusMeters > 0.0;
    }

    internal bool TryGetDirectionFromPlanetToSun(out SimVector3 directionTowardSun)
    {
        directionTowardSun = SimVector3.Zero();

        if (sun == null || planet == null)
        {
            return false;
        }

        directionTowardSun = PlanetSunGeometry.DirectionTowardSun(
            sun.position,
            planet.position
        );
        return directionTowardSun.LengthSquared() > 1e-24;
    }

    internal bool TryGetDirectionFromShipToSun(out SimVector3 directionTowardSun)
    {
        directionTowardSun = SimVector3.Zero();

        if (sun == null || ship == null)
        {
            return false;
        }

        directionTowardSun = PlanetSunGeometry.DirectionTowardSun(
            sun.position,
            ship.GetCenterOfMassWorld()
        );
        return directionTowardSun.LengthSquared() > 1e-24;
    }

    internal double GetDirectSunVisibilityAtShip()
    {
        if (sun == null || ship == null)
        {
            return 1.0;
        }

        SimVector3 shipWorld = ship.GetCenterOfMassWorld();
        double visibility = 1.0;
        Body terrainBody = GetVisualTerrainBody();

        if (terrainBody != null && terrainBody.radius > 0.0)
        {
            visibility = Math.Min(
                visibility,
                PlanetSunGeometry.DirectSunVisibility(
                    shipWorld,
                    sun.position,
                    terrainBody.position,
                    terrainBody.radius
                )
            );
        }

        if (
            planet != null &&
            planet != terrainBody &&
            planet.radius > 0.0
        )
        {
            visibility = Math.Min(
                visibility,
                PlanetSunGeometry.DirectSunVisibility(
                    shipWorld,
                    sun.position,
                    planet.position,
                    planet.radius
                )
            );
        }

        return visibility;
    }

    void AlignPlanetSpinToCapeMorning()
    {
        if (!TryGetDirectionFromPlanetToSun(out SimVector3 towardSun))
        {
            return;
        }

        planet.spinAngleRadians =
            PlanetSunGeometry.ComputeYawAboutYToAlignAzimuth(
                LaunchSiteConfig.CapeCanaveral.GetSurfaceNormal(),
                towardSun
            ) + PlanetSunGeometry.LaunchMorningAzimuthOffsetRadians;
    }

    void AlignMoonSpinTidalLock()
    {
        if (moon == null || planet == null)
        {
            return;
        }

        SimVector3 towardEarth = planet.position.Subtract(moon.position);

        if (towardEarth.LengthSquared() <= 1e-24)
        {
            return;
        }

        moon.spinAngleRadians = PlanetSunGeometry.ComputeYawAboutYToAlignAzimuth(
            LaunchSiteConfig.MoonNearSide.GetSurfaceNormal(),
            towardEarth
        );
    }

    /// <summary>
    /// Moon pad: yaw so the active site faces the Sun (Cape morning offset).
    /// Orbit phase for a sunlit start is authored in <see cref="SimulationWorldConfig"/>.
    /// Tidal lock to Earth is not applied on this scenario — 0°E need not face Earth at t=0.
    /// </summary>
    void AlignMoonSpinForSurfacePadDaylight()
    {
        if (moon == null || sun == null)
        {
            AlignMoonSpinTidalLock();
            return;
        }

        SimVector3 towardSun = sun.position.Subtract(moon.position);

        if (towardSun.LengthSquared() <= 1e-24)
        {
            AlignMoonSpinTidalLock();
            return;
        }

        moon.spinAngleRadians =
            PlanetSunGeometry.ComputeYawAboutYToAlignAzimuth(
                LaunchSiteConfig.Active.GetSurfaceNormal(),
                towardSun
            ) + PlanetSunGeometry.LaunchMorningAzimuthOffsetRadians;
    }

    internal bool ScenarioIsVacuumDocking()
    {
        return scenario == SimulationScenario.DockingApproach;
    }

    public Simulation CreateCopy()
    {
        Simulation copy = new Simulation(scenario);

        copy.planet = planet == null ? null : CopyBody(planet);
        copy.moon = moon == null ? null : CopyBody(moon);
        copy.sun = sun == null ? null : CopyBody(sun);
        copy.ship = CopyVessel(ship);

        copy.dockableVessels.Clear();

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            copy.dockableVessels.Add(CopyVessel(dockableVessels[i]));
        }

        copy.bodies = new Body[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            copy.bodies[i] = MapCopiedBody(bodies[i], copy);
        }

        copy.targetVessel = MapCopiedVessel(targetVessel, copy);
        copy.dockingConfig = dockingConfig;
        copy.lastDockingMessage = lastDockingMessage;
        copy.lastGravityAcceleration = lastGravityAcceleration;
        copy.lastMoonGravityAcceleration = lastMoonGravityAcceleration;
        copy.circularSpeedAtStart = circularSpeedAtStart;
        copy.moonCircularSpeedAtStart = moonCircularSpeedAtStart;
        copy.moonSphereOfInfluenceRadius = moonSphereOfInfluenceRadius;
        copy.planetSphereOfInfluenceRadius = planetSphereOfInfluenceRadius;
        copy.minShipDistanceToPlanet = minShipDistanceToPlanet;
        copy.maxShipDistanceToPlanet = maxShipDistanceToPlanet;
        copy.initialShipSpecificEnergy = initialShipSpecificEnergy;
        copy.initialShipAngularMomentum = initialShipAngularMomentum;
        copy.activePrimaryBodyName = activePrimaryBodyName;
        copy.lastActivePrimaryForHysteresis = lastActivePrimaryForHysteresis;
        copy.gravityModel = gravityModel;
        copy.atmosphereModel = atmosphereModel;
        copy.lastAtmosphericDensity = lastAtmosphericDensity;
        copy.lastDynamicPressure = lastDynamicPressure;
        copy.lastDragForce = lastDragForce;
        copy.lastLiftForce = lastLiftForce;
        copy.lastEffectiveDragArea = lastEffectiveDragArea;
        copy.lastDragTorqueZ = lastDragTorqueZ;
        copy.lastAngleOfAttack = lastAngleOfAttack;
        copy.lastLiftCoefficient = lastLiftCoefficient;
        copy.lastDragCoefficient = lastDragCoefficient;
        copy.atmosphericSasEnabled = atmosphericSasEnabled;
        copy.enableAtmosphericPreview = enableAtmosphericPreview;
        copy.orientationInput = orientationInput;
        copy.lastFlightEventMessage = lastFlightEventMessage;
        copy.simulationTime = simulationTime;
        copy.isOrbitPreview = true;

        return copy;
    }

    internal Body CopyBody(Body body)
    {
        if (body == null)
        {
            return null;
        }

        Body copy = new Body(
            body.name,
            body.position,
            body.velocity,
            body.acceleration,
            body.mu,
            body.radius,
            body.isStatic
        );
        copy.sphereOfInfluenceRadius = body.sphereOfInfluenceRadius;
        copy.isRootBody = body.isRootBody;
        copy.atmosphereProfile = body.atmosphereProfile;
        copy.spinRadiansPerSecond = body.spinRadiansPerSecond;
        copy.spinAngleRadians = body.spinAngleRadians;
        copy.spinAxis = body.spinAxis;
        return copy;
    }

    Body MapCopiedBody(Body source, Simulation copy)
    {
        if (source == planet)
        {
            return copy.planet;
        }

        if (source == moon)
        {
            return copy.moon;
        }

        if (source == sun)
        {
            return copy.sun;
        }

        if (source == ship)
        {
            return copy.ship;
        }

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            if (source == dockableVessels[i])
            {
                return copy.dockableVessels[i];
            }
        }

        return copy.ship;
    }

    Vessel MapCopiedVessel(Vessel source, Simulation copy)
    {
        if (source == null)
        {
            return null;
        }

        if (source == ship)
        {
            return copy.ship;
        }

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            if (source == dockableVessels[i])
            {
                return copy.dockableVessels[i];
            }
        }

        return null;
    }

    Vessel CopyVessel(Vessel vessel)
    {
        if (vessel == null)
        {
            return null;
        }

        Vessel copy = new Vessel(
            vessel.name,
            vessel.position,
            vessel.velocity
        );

        copy.acceleration = vessel.acceleration;
        copy.mu = vessel.mu;
        copy.radius = vessel.radius;
        copy.isStatic = vessel.isStatic;
        copy.thrust = vessel.thrust;
        copy.thrustAcceleration = vessel.thrustAcceleration;
        copy.dragAcceleration = vessel.dragAcceleration;
        copy.heading = vessel.heading;
        copy.orientation = vessel.orientation;
        copy.activeStageIndex = vessel.activeStageIndex;
        copy.flightStatus = vessel.flightStatus;
        copy.landedOnBodyName = vessel.landedOnBodyName;
        copy.collisionRadius = vessel.collisionRadius;
        copy.lastSurfaceNormal = vessel.lastSurfaceNormal;
        copy.centerOfPressureOffset = vessel.centerOfPressureOffset;
        copy.angularVelocity = vessel.angularVelocity;
        copy.isOnKeplerRails = vessel.isOnKeplerRails;
        copy.keplerRails = vessel.keplerRails;
        copy.reentryIntensity = vessel.reentryIntensity;
        copy.payloadMass = vessel.payloadMass;
        copy.IsDocked = vessel.IsDocked;
        copy.stages = Vessel.CopyStages(vessel.stages);

        return copy;
    }

    public SimVector3[] GetShipOrbitPathRelativeToActivePrimary(int pointCount)
    {
        if (
            ship != null &&
            ship.flightStatus == FlightStatus.Landed &&
            GetActivePrimaryBody() != null
        )
        {
            return Array.Empty<SimVector3>();
        }

        return OrbitalMechanics.BuildOrbitPathRelativeToBody(
            GetActivePrimaryBody(),
            ship.position,
            ship.velocity,
            pointCount
        );
    }

    public Body TryGetOrbitParentBody(string bodyName)
    {
        if (string.IsNullOrEmpty(bodyName))
        {
            return null;
        }

        if (bodyName == CelestialBody.Moon.Name && moon != null)
        {
            return planet;
        }

        if (
            bodyName == CelestialBody.Earth.Name &&
            ScenarioHasSun() &&
            sun != null
        )
        {
            return sun;
        }

        if (IsVesselBodyName(bodyName))
        {
            return GetActivePrimaryBody();
        }

        return null;
    }

    public bool TryGetCelestialBodyByName(string bodyName, out Body body)
    {
        body = null;

        if (string.IsNullOrEmpty(bodyName))
        {
            return false;
        }

        if (planet != null && bodyName == planet.name)
        {
            body = planet;
            return true;
        }

        if (moon != null && bodyName == moon.name)
        {
            body = moon;
            return true;
        }

        if (sun != null && bodyName == sun.name)
        {
            body = sun;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Map overlay: SOI radius and owning body for orbit-focus name (celestial or vessel).
    /// Vessels use the active primary body (patched-conics parent).
    /// </summary>
    public bool TryGetMapFocusSphereOfInfluence(
        string bodyName,
        out Body soiBody,
        out double soiRadiusMeters
    )
    {
        soiBody = null;
        soiRadiusMeters = 0.0;

        if (string.IsNullOrEmpty(bodyName))
        {
            return false;
        }

        if (bodyName == "Ship" || IsVesselBodyName(bodyName))
        {
            soiBody = GetActivePrimaryBody();
        }
        else if (TryGetCelestialBodyByName(bodyName, out soiBody))
        {
            // Celestial focus shows that body's own SOI.
        }
        else
        {
            return false;
        }

        if (soiBody == null)
        {
            return false;
        }

        return TryGetFiniteSphereOfInfluenceRadius(soiBody, out soiRadiusMeters);
    }

    public string[] GetMapDisplayOrbitBodyNames()
    {
        int count = 0;

        if (ScenarioHasSun() && planet != null)
        {
            count++;
        }

        if (moon != null)
        {
            count++;
        }

        if (ship != null)
        {
            count++;
        }

        if (count == 0)
        {
            return Array.Empty<string>();
        }

        string[] bodyNames = new string[count];
        int index = 0;

        if (ScenarioHasSun() && planet != null)
        {
            bodyNames[index++] = planet.name;
        }

        if (moon != null)
        {
            bodyNames[index++] = moon.name;
        }

        if (ship != null)
        {
            bodyNames[index] = ship.name;
        }

        return bodyNames;
    }

    public SimVector3[] GetBodyOrbitPathRelativeToParent(string bodyName, int pointCount)
    {
        Body orbitingBody = ResolveOrbitFocusBody(bodyName);

        if (orbitingBody == null)
        {
            return Array.Empty<SimVector3>();
        }

        if (IsVesselBodyName(bodyName))
        {
            return GetShipOrbitPathRelativeToActivePrimary(pointCount);
        }

        Body parentBody = TryGetOrbitParentBody(bodyName);

        if (parentBody == null)
        {
            return Array.Empty<SimVector3>();
        }

        return OrbitalMechanics.BuildOrbitPathRelativeToBody(
            parentBody,
            orbitingBody.position,
            orbitingBody.velocity,
            pointCount
        );
    }

    public bool TryGetBodyPeriapsisAltitudeRelativeToParent(
        string bodyName,
        out double altitude
    )
    {
        altitude = double.PositiveInfinity;

        Body orbitingBody = ResolveOrbitFocusBody(bodyName);
        Body parentBody = TryGetOrbitParentBody(bodyName);

        if (orbitingBody == null || parentBody == null)
        {
            return false;
        }

        if (IsVesselBodyName(bodyName))
        {
            altitude = GetShipPeriapsisAltitudeRelativeToActivePrimary();
            return true;
        }

        double periapsisRadius = OrbitalMechanics.GetPeriapsisRadius(
            orbitingBody.position.Subtract(parentBody.position),
            orbitingBody.velocity.Subtract(parentBody.velocity),
            parentBody.mu
        );

        if (double.IsInfinity(periapsisRadius) || double.IsNaN(periapsisRadius))
        {
            return false;
        }

        altitude = periapsisRadius - parentBody.radius;
        return true;
    }

    public bool TryGetBodyApoapsisAltitudeRelativeToParent(
        string bodyName,
        out double altitude
    )
    {
        altitude = double.PositiveInfinity;

        Body orbitingBody = ResolveOrbitFocusBody(bodyName);
        Body parentBody = TryGetOrbitParentBody(bodyName);

        if (orbitingBody == null || parentBody == null)
        {
            return false;
        }

        if (IsVesselBodyName(bodyName))
        {
            altitude = GetShipApoapsisAltitudeRelativeToActivePrimary();
            return true;
        }

        double apoapsisRadius = OrbitalMechanics.GetApoapsisRadius(
            orbitingBody.position.Subtract(parentBody.position),
            orbitingBody.velocity.Subtract(parentBody.velocity),
            parentBody.mu
        );

        if (double.IsInfinity(apoapsisRadius) || double.IsNaN(apoapsisRadius))
        {
            return false;
        }

        altitude = apoapsisRadius - parentBody.radius;
        return true;
    }

    Body ResolveOrbitFocusBody(string bodyName)
    {
        if (string.IsNullOrEmpty(bodyName))
        {
            return null;
        }

        if (IsVesselBodyName(bodyName))
        {
            return ship;
        }

        if (TryGetCelestialBodyByName(bodyName, out Body celestialBody))
        {
            return celestialBody;
        }

        return null;
    }

    string ISimulationStateView.ActivePrimaryBodyName => activePrimaryBodyName;

    public SimVector3[] GetPreviewShipOrbitPathRelativeToActivePrimary(
        SimVector3 plannedDeltaV,
        int pointCount
    )
    {
        return preview.GetPreviewShipOrbitPathRelativeToActivePrimary(
            plannedDeltaV,
            pointCount
        );
    }

    public SimVector3[] GetPreviewShipOrbitPathRelativeToActivePrimary(
        SimVector3 plannedDeltaV,
        int pointCount,
        double maneuverTime,
        double dt
    )
    {
        return preview.GetPreviewShipOrbitPathRelativeToActivePrimary(
            plannedDeltaV,
            pointCount,
            maneuverTime,
            dt
        );
    }

    public SimVector3 GetShipPositionAtTime(double time, double dt)
    {
        return preview.GetShipPositionAtTime(time, dt);
    }

    public SimVector3[] GetFiniteBurnPreviewShipOrbitPathRelativeToActivePrimary(
        SimVector3 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        int pointCount,
        double burnStartTime,
        double coastDt,
        double burnDt
    )
    {
        return preview.GetFiniteBurnPreviewShipOrbitPathRelativeToActivePrimary(
            plannedDeltaV,
            thrustForce,
            burnDuration,
            pointCount,
            burnStartTime,
            coastDt,
            burnDt
        );
    }

    public double GetFiniteBurnPreviewShipOrbitalPeriodRelativeToActivePrimary(
        SimVector3 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double burnStartTime,
        double coastDt,
        double burnDt
    )
    {
        return preview.GetFiniteBurnPreviewShipOrbitalPeriodRelativeToActivePrimary(
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt
        );
    }

    public SimVector3[] SampleFiniteBurnPreviewWorldPath(
        SimVector3 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double burnStartTime,
        double coastDt,
        double burnDt
    )
    {
        return preview.SampleFiniteBurnPreviewWorldPath(
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt
        );
    }

    public void GetFiniteBurnPreviewEndWorldState(
        SimVector3 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double burnStartTime,
        double coastDt,
        double burnDt,
        out SimVector3 position,
        out SimVector3 velocity
    )
    {
        preview.GetFiniteBurnPreviewEndWorldState(
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt,
            out position,
            out velocity
        );
    }

    public bool WillActivePrimaryChangeDuringFiniteBurn(
        SimVector3 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double burnStartTime,
        double coastDt,
        double burnDt
    )
    {
        return preview.WillActivePrimaryChangeDuringFiniteBurn(
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt
        );
    }

    public static PreviewExecutionDriftInfo CompareBurnExecution(
        SimVector3[] previewWorldPath,
        SimVector3[] actualWorldPath,
        SimVector3 previewEndPosition,
        SimVector3 previewEndVelocity,
        SimVector3 actualEndPosition,
        SimVector3 actualEndVelocity
    )
    {
        return SimulationPreview.CompareBurnExecution(
            previewWorldPath,
            actualWorldPath,
            previewEndPosition,
            previewEndVelocity,
            actualEndPosition,
            actualEndVelocity
        );
    }

    public double EstimateShipBurnDurationForDeltaV(
        SimVector3 deltaV,
        double thrustForce
    )
    {
        double deltaVLength = deltaV.Length();

        if (deltaVLength <= 0.0)
        {
            return 0.0;
        }

        if (thrustForce <= 0.0 || ship.GetMass() <= 0.0)
        {
            return double.PositiveInfinity;
        }

        double fuelConsumption = ship.GetActiveFuelConsumptionPerForceSecond();
        double fuelRate = thrustForce * fuelConsumption;

        if (fuelRate <= 0.0)
        {
            return deltaVLength / (thrustForce / ship.GetMass());
        }

        double startMass = ship.GetMass();
        double endMassAfterRequestedDeltaV =
            startMass / Math.Exp(deltaVLength * fuelConsumption);
        double lowestPossibleMass = ship.GetTotalDryMass();

        if (endMassAfterRequestedDeltaV < lowestPossibleMass)
        {
            endMassAfterRequestedDeltaV = lowestPossibleMass;
        }

        double fuelToBurn = startMass - endMassAfterRequestedDeltaV;

        if (fuelToBurn <= 0.0)
        {
            return 0.0;
        }

        if (fuelToBurn > ship.GetActiveStageFuelMass())
        {
            fuelToBurn = ship.GetActiveStageFuelMass();
        }

        return fuelToBurn / fuelRate;
    }

    public double GetShipAvailableDeltaV()
    {
        return ship.GetAvailableDeltaV();
    }

    internal SimVector3 ClampDeltaVToAvailableFuel(SimVector3 deltaV)
    {
        double deltaVLength = deltaV.Length();
        double availableDeltaV = GetShipAvailableDeltaV();

        if (
            deltaVLength <= 0.0 ||
            availableDeltaV <= 0.0 ||
            deltaVLength <= availableDeltaV
        )
        {
            return deltaV;
        }

        return deltaV.Normalize().Multiply(availableDeltaV);
    }

    public SimVector3 GetFiniteBurnCircularizeDeltaVRelativeToActivePrimary(
        double thrustForce,
        double circularizeTime,
        double burnStartTime,
        double coastDt,
        double burnDt,
        int iterationCount
    )
    {
        return preview.GetFiniteBurnCircularizeDeltaVRelativeToActivePrimary(
            thrustForce,
            circularizeTime,
            burnStartTime,
            coastDt,
            burnDt,
            iterationCount
        );
    }

    public ClosestApproachInfo GetClosestApproachToMoon(
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt
    )
    {
        return preview.GetClosestApproachToMoon(plannedDeltaV, stepCount, dt);
    }

    public ClosestApproachInfo GetClosestApproachToBody(
        string targetBodyName,
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt
    )
    {
        return preview.GetClosestApproachToBody(
            targetBodyName,
            plannedDeltaV,
            stepCount,
            dt
        );
    }

    public ClosestApproachInfo GetClosestApproachToBody(
        string targetBodyName,
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime
    )
    {
        return preview.GetClosestApproachToBody(
            targetBodyName,
            plannedDeltaV,
            stepCount,
            dt,
            maneuverTime
        );
    }

    public ClosestApproachInfo GetClosestApproachToBody(
        string targetBodyName,
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime,
        double thrustForce,
        double burnDuration,
        double burnDt
    )
    {
        return preview.GetClosestApproachToBody(
            targetBodyName,
            plannedDeltaV,
            stepCount,
            dt,
            maneuverTime,
            thrustForce,
            burnDuration,
            burnDt
        );
    }

    public SphereOfInfluenceEntryPreview GetMoonSphereOfInfluenceEntryPreview(
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt
    )
    {
        return preview.GetMoonSphereOfInfluenceEntryPreview(
            plannedDeltaV,
            stepCount,
            dt
        );
    }

    public SphereOfInfluenceEntryPreview GetMoonSphereOfInfluenceEntryPreview(
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime
    )
    {
        return preview.GetMoonSphereOfInfluenceEntryPreview(
            plannedDeltaV,
            stepCount,
            dt,
            maneuverTime
        );
    }

    public SphereOfInfluenceEntryPreview GetMoonSphereOfInfluenceEntryPreview(
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime,
        double thrustForce,
        double burnDuration,
        double burnDt
    )
    {
        return preview.GetMoonSphereOfInfluenceEntryPreview(
            plannedDeltaV,
            stepCount,
            dt,
            maneuverTime,
            thrustForce,
            burnDuration,
            burnDt
        );
    }

    public SphereOfInfluenceEntryPreview GetActiveSphereOfInfluenceEntryPreview(
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime,
        double thrustForce,
        double burnDuration,
        double burnDt
    )
    {
        if (ScenarioHasSun())
        {
            return preview.GetPlanetSphereOfInfluenceExitPreview(
                plannedDeltaV,
                stepCount,
                dt,
                maneuverTime,
                thrustForce,
                burnDuration,
                burnDt
            );
        }

        return preview.GetMoonSphereOfInfluenceEntryPreview(
            plannedDeltaV,
            stepCount,
            dt,
            maneuverTime,
            thrustForce,
            burnDuration,
            burnDt
        );
    }

    // GetBodyStates = получить снимок состояний тел для внешнего слоя, например будущего Unity-адаптера.
    public BodyState[] GetBodyStates()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        System.Collections.Generic.List<BodyState> states =
            new System.Collections.Generic.List<BodyState>(bodies.Length);

        for (int i = 0; i < bodies.Length; i++)
        {
            if (
                ScenarioIsVacuumDocking() &&
                bodies[i] != null &&
                bodies[i].name == "Planet"
            )
            {
                continue;
            }

            states.Add(
                new BodyState(
                    bodies[i].name,
                    bodies[i].position,
                    bodies[i].velocity,
                    bodies[i].isStatic,
                    bodies[i] == activePrimaryBody,
                    bodies[i].radius
                )
            );
        }

        return states.ToArray();
    }

    // Update = обновить состояние симуляции на один шаг времени.
    public void Update(double dt)
    {
        currentTick++;
        dt = CapSimulationStepForActiveThrust(dt);

        if (ScenarioHasSun())
        {
            UpdateSolarSystem(dt);
        }
        else
        {
            UpdateInertial(dt);
        }

        PropagateDebrisVessels(dt);
        PropagateDockableVessels(dt);
        TryResolveDockingHullContact();
        TryPerformDocking();

        if (ScenarioIsVacuumDocking())
        {
            AuditOfflineDockingPropulsionLeakage();
        }
    }

    void TryResolveDockingHullContact()
    {
        if (!ScenarioIsVacuumDocking() || targetVessel == null || ship.IsDocked)
        {
            return;
        }

        if (
            ship.flightStatus != FlightStatus.Flying ||
            targetVessel.flightStatus != FlightStatus.Flying
        )
        {
            return;
        }

        double minDistance = ship.envelopeRadius + targetVessel.envelopeRadius;
        DockingPortContactMetrics portContact =
            ship.GetDockingPortContactMetrics(targetVessel);

        if (portContact.HasValidPorts &&
            portContact.IsAlignedForCapture(dockingConfig.minAlignmentDot))
        {
            return;
        }

        SimVector3 shipCenter = ship.GetCenterOfMassWorld();
        SimVector3 targetCenter = targetVessel.GetCenterOfMassWorld();
        SimVector3 delta = targetCenter.Subtract(shipCenter);
        double distanceSquared = delta.LengthSquared();

        if (distanceSquared >= minDistance * minDistance || distanceSquared <= 1e-12)
        {
            return;
        }

        ResolveVacuumVesselPairContact(
            ship,
            targetVessel,
            delta,
            minDistance
        );
    }

    void ResolveVacuumVesselPairContact(
        Vessel vesselA,
        Vessel vesselB,
        SimVector3 separationVectorFromAToB,
        double minCenterDistance
    )
    {
        if (vesselA == null || vesselB == null || vesselA == vesselB)
        {
            return;
        }

        double distanceSquared = separationVectorFromAToB.LengthSquared();

        if (distanceSquared <= 1e-12)
        {
            return;
        }

        double distance = Math.Sqrt(distanceSquared);
        SimVector3 normalFromAToB = separationVectorFromAToB.Multiply(1.0 / distance);
        double penetration = minCenterDistance - distance;

        if (penetration <= 0.0)
        {
            return;
        }

        double massA = Math.Max(vesselA.GetMass(), 1e-9);
        double massB = Math.Max(vesselB.GetMass(), 1e-9);
        double invMassSum = 1.0 / (massA + massB);
        SimVector3 correction = normalFromAToB.Multiply(penetration);

        vesselA.position = vesselA.position.Subtract(
            correction.Multiply(massB * invMassSum)
        );
        vesselB.position = vesselB.position.Add(
            correction.Multiply(massA * invMassSum)
        );

        SimVector3 relativeVelocity = vesselA.velocity.Subtract(vesselB.velocity);
        double relNormalSpeed = SimVector3.Dot(relativeVelocity, normalFromAToB);

        if (relNormalSpeed <= 0.0)
        {
            return;
        }

        double impulseScalar =
            -(1.0 + VacuumDockingHullContactRestitution) *
            relNormalSpeed /
            (1.0 / massA + 1.0 / massB);
        SimVector3 impulseOnA = normalFromAToB.Multiply(impulseScalar);

        vesselA.velocity = vesselA.velocity.Add(impulseOnA.Multiply(1.0 / massA));
        vesselB.velocity = vesselB.velocity.Subtract(impulseOnA.Multiply(1.0 / massB));
    }

    bool VesselHasActivePilotControl(Vessel vessel)
    {
        return VesselRequiresOrientationAssist(vessel)
            || VesselHasActivePropulsion(vessel);
    }

    void ClearVesselPropulsionState(Vessel vessel)
    {
        if (vessel == null)
        {
            return;
        }

        vessel.thrust = SimVector3.Zero();
        vessel.thrustAcceleration = SimVector3.Zero();
        vessel.rcsAcceleration = SimVector3.Zero();
    }

    void AdvanceIndependentVesselPhysics(Vessel vessel, double dt)
    {
        if (vessel == null || vessel.flightStatus != FlightStatus.Flying || dt <= 0.0)
        {
            return;
        }

        ProcessVesselStagingRequest(vessel);
        IntegrateVesselAttitudeSubstep(vessel, dt);

        if (VesselHasActivePropulsion(vessel))
        {
            ApplyVesselMainEngineThrust(vessel, dt);
            ApplyVesselRcsThrust(vessel, dt);
        }
        else
        {
            vessel.thrust = SimVector3.Zero();
            vessel.thrustAcceleration = SimVector3.Zero();

            if (!VesselRequiresOrientationAssist(vessel))
            {
                vessel.rcsAcceleration = SimVector3.Zero();
            }
        }

        vessel.position = vessel.position.Add(vessel.velocity.Multiply(dt));
    }

    void AuditOfflineDockingPropulsionLeakage()
    {
#if UNITY
        if (IsServerContext || ship == null)
        {
            return;
        }

        bool playerMainEngineActive = ship.controlState.throttle > 0.01f;

        if (!playerMainEngineActive)
        {
            return;
        }

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            Vessel dockable = dockableVessels[i];

            if (dockable == null || dockable == ship)
            {
                continue;
            }

            if (
                dockable.thrust.LengthSquared() > 0.0 ||
                dockable.thrustAcceleration.LengthSquared() > 0.0 ||
                dockable.rcsAcceleration.LengthSquared() > 0.0 ||
                (
                    dockable.controlState.throttle > 0.01f
                )
            )
            {
                UnityEngine.Debug.LogError(
                    "[OFFLINE PHYSICAL BUG] Объект " +
                    GetVesselId(dockable) +
                    " имеет thrust = " +
                    dockable.thrust +
                    " при throttle = " +
                    dockable.controlState.throttle +
                    "! Тяга должна быть только у корабля."
                );
            }
        }

        if (
            targetVessel != null &&
            targetVessel != ship &&
            (
                targetVessel.thrust.LengthSquared() > 0.0 ||
                targetVessel.thrustAcceleration.LengthSquared() > 0.0 ||
                targetVessel.rcsAcceleration.LengthSquared() > 0.0
            )
        )
        {
            UnityEngine.Debug.LogError(
                "[OFFLINE PHYSICAL BUG] Объект " +
                GetVesselId(targetVessel) +
                " имеет thrust = " +
                targetVessel.thrust +
                " при throttle = " +
                targetVessel.controlState.throttle +
                "! Тяга должна быть только у корабля."
            );
        }
#endif
    }

    double CapSimulationStepForActiveThrust(double dt)
    {
        if (!IsThrustTimeWarpRestricted())
        {
            return dt;
        }

        return Math.Min(dt, MaxSimulationStepWhileThrusting);
    }

    SimVector3 GetVesselMainEngineThrustVector(Vessel vessel)
    {
        if (vessel == null || vessel.controlState.throttle <= 0.01f)
        {
            return SimVector3.Zero();
        }

        double scalarThrust =
            vessel.GetActiveEngineThrustNewtons() * vessel.controlState.throttle;

        if (scalarThrust <= 0.0)
        {
            return SimVector3.Zero();
        }

        return vessel.GetAggregatedMainEngineThrustWorld(vessel.controlState.throttle);
    }

    bool VesselHasMainEngineThrustCommand(Vessel vessel)
    {
        if (vessel == null)
        {
            return false;
        }

        if (
            vessel == ship &&
            vessel.thrust.LengthSquared() > RailsThrustEpsilonSquared
        )
        {
            return true;
        }

        return vessel.controlState.throttle > 0.01f;
    }

    void IntegrateShipControlledPhysicsSubstep(double dt)
    {
        if (
            ship == null ||
            ship.flightStatus != FlightStatus.Flying ||
            dt <= 0.0 ||
            ShouldUseSurfaceLaunchAssist()
        )
        {
            return;
        }

        ProcessVesselStagingRequest(ship);
        ApplyAtmosphericDrag(dt);
        ApplyVesselMainEngineThrust(ship, dt);
        ApplyVesselRcsThrust(ship, dt);
        IntegrateVesselAttitudeSubstep(ship, dt);
    }

    void CompleteKeplerRailsCoastStep(double dt)
    {
        EnforceSphereCollisionWithPrimary();
        IntegrateShipAttitudeOnKeplerRails(dt);
        simulationTime += dt;
        FinalizeUpdateStep();
    }

    void IntegrateShipAttitudeOnKeplerRails(double dt)
    {
        if (ship == null || dt <= 0.0)
        {
            return;
        }

        double remaining = dt;

        while (remaining > 1e-12)
        {
            double step = Math.Min(remaining, MaxNumericalPhysicsStep);
            IntegrateVesselAttitudeSubstep(ship, step);
            remaining -= step;
        }
    }

    void UpdateSolarSystem(double dt)
    {
        if (ship.flightStatus == FlightStatus.Crashed)
        {
            HandleCrashedStep();
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        if (ShouldUseSurfaceLaunchAssist())
        {
            HandleSurfaceLaunchAssistStep(dt);
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        if (ship.flightStatus == FlightStatus.Landed)
        {
            HandleLandedStep(dt);
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        bool nonShipBodiesAdvanced = false;
        bool keplerShipMotionFinished = false;

        if (ShouldUseKeplerRails(ship))
        {
            if (!ship.isOnKeplerRails)
            {
                TryEnterKeplerRails();
            }

            if (ship.isOnKeplerRails)
            {
                Body railsPrimary = GetActivePrimaryBody();

                if (railsPrimary.name == ship.keplerRails.primaryBodyName)
                {
                    IntegrateSolarSystemBodies(dt);
                    nonShipBodiesAdvanced = true;
                    keplerShipMotionFinished = PropagateShipOnKeplerRails(dt);
                }
                else
                {
                    ExitKeplerRails();
                }
            }
        }

        if (ship.isOnKeplerRails && ShouldUseKeplerRails(ship))
        {
            CompleteKeplerRailsCoastStep(dt);
            return;
        }

        if (keplerShipMotionFinished)
        {
            ExitKeplerRails();
            CompleteKeplerRailsCoastStep(dt);
            return;
        }

        ExitKeplerRails();

        if (nonShipBodiesAdvanced)
        {
            IntegrateShipNumericalWithSoi(dt);
            IntegrateShipControlledPhysicsSubstep(dt);
            EnforceSphereCollisionWithPrimary(dt);
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        double remaining = dt;

        while (remaining > 0.0)
        {
            double step = Math.Min(remaining, MaxNumericalPhysicsStep);
            IntegrateSolarSystemBodies(step);
            IntegrateShipNumericalWithSoi(step);
            IntegrateShipControlledPhysicsSubstep(step);
            EnforceSphereCollisionWithPrimary(step);
            remaining -= step;
        }

        simulationTime += dt;
        FinalizeUpdateStep();
    }

    void IntegrateSolarSystemBodies(double dt)
    {
        if (sun == null || planet == null)
        {
            return;
        }

        CaptureSolarSystemStepStart();

        IntegrateBodyRelativeToParent(
            planet,
            sun,
            dt,
            solarStepSunPosition,
            solarStepSunVelocity);

        if (moon != null)
        {
            IntegrateBodyRelativeToParent(
                moon,
                planet,
                dt,
                solarStepPlanetPosition,
                solarStepPlanetVelocity);
        }

        AdvanceCelestialSpins(dt);
    }

    void CaptureSolarSystemStepStart()
    {
        solarStepStartCaptured = sun != null && planet != null;

        if (!solarStepStartCaptured)
        {
            return;
        }

        solarStepSunPosition = sun.position;
        solarStepSunVelocity = sun.velocity;
        solarStepPlanetPosition = planet.position;
        solarStepPlanetVelocity = planet.velocity;

        if (moon != null)
        {
            solarStepMoonPosition = moon.position;
            solarStepMoonVelocity = moon.velocity;
        }
    }

    void AdvanceCelestialSpins(double dt)
    {
        if (planet != null)
        {
            planet.AdvanceSpin(dt);
        }

        if (moon != null)
        {
            moon.AdvanceSpin(dt);
        }
    }

    bool TryGetSolarStepStart(
        Body body,
        out SimVector3 position,
        out SimVector3 velocity
    )
    {
        if (!solarStepStartCaptured || body == null)
        {
            position = body != null ? body.position : SimVector3.Zero();
            velocity = body != null ? body.velocity : SimVector3.Zero();
            return false;
        }

        if (body == sun)
        {
            position = solarStepSunPosition;
            velocity = solarStepSunVelocity;
            return true;
        }

        if (body == planet)
        {
            position = solarStepPlanetPosition;
            velocity = solarStepPlanetVelocity;
            return true;
        }

        if (body == moon)
        {
            position = solarStepMoonPosition;
            velocity = solarStepMoonVelocity;
            return true;
        }

        position = body.position;
        velocity = body.velocity;
        return false;
    }

    void IntegrateBodyRelativeToParent(
        Body child,
        Body parent,
        double dt,
        SimVector3 parentPositionAtStepStart,
        SimVector3 parentVelocityAtStepStart
    )
    {
        if (child.isStatic || parent == null)
        {
            return;
        }

        SimVector3 relativePosition =
            child.position.Subtract(parentPositionAtStepStart);
        SimVector3 relativeVelocity =
            child.velocity.Subtract(parentVelocityAtStepStart);
        SimVector3 childPositionAtStepStart =
            parentPositionAtStepStart.Add(relativePosition);

        SimVector3 oldRelativeAcceleration = gravityModel.CalculateAcceleration(
            childPositionAtStepStart,
            parentPositionAtStepStart,
            parent.mu
        );

        relativePosition = relativePosition
            .Add(relativeVelocity.Multiply(dt))
            .Add(oldRelativeAcceleration.Multiply(0.5 * dt * dt));

        SimVector3 childPositionBeforeVelocityStep =
            parent.position.Add(relativePosition);

        SimVector3 newRelativeAcceleration = gravityModel.CalculateAcceleration(
            childPositionBeforeVelocityStep,
            parent.position,
            parent.mu
        );

        relativeVelocity = relativeVelocity.Add(
            oldRelativeAcceleration
                .Add(newRelativeAcceleration)
                .Multiply(0.5 * dt)
        );

        child.position = parent.position.Add(relativePosition);
        child.velocity = parent.velocity.Add(relativeVelocity);
        child.acceleration = newRelativeAcceleration;
    }

    void UpdateInertial(double dt)
    {
        if (ScenarioIsVacuumDocking())
        {
            UpdateVacuumDocking(dt);
            return;
        }

        if (ship.flightStatus == FlightStatus.Crashed)
        {
            HandleCrashedStep();
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        if (ShouldUseSurfaceLaunchAssist())
        {
            HandleSurfaceLaunchAssistStep(dt);
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        if (ship.flightStatus == FlightStatus.Landed)
        {
            HandleLandedStep(dt);
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        bool nonShipBodiesAdvanced = false;
        bool keplerShipMotionFinished = false;

        if (ShouldUseKeplerRails(ship))
        {
            if (!ship.isOnKeplerRails)
            {
                TryEnterKeplerRails();
            }

            if (ship.isOnKeplerRails)
            {
                Body railsPrimary = GetActivePrimaryBody();

                if (railsPrimary.name == ship.keplerRails.primaryBodyName)
                {
                    IntegrateNonShipBodies(dt);
                    nonShipBodiesAdvanced = true;
                    keplerShipMotionFinished = PropagateShipOnKeplerRails(dt);
                }
                else
                {
                    ExitKeplerRails();
                }
            }
        }

        if (ship.isOnKeplerRails && ShouldUseKeplerRails(ship))
        {
            CompleteKeplerRailsCoastStep(dt);
            return;
        }

        if (keplerShipMotionFinished)
        {
            ExitKeplerRails();
            CompleteKeplerRailsCoastStep(dt);
            return;
        }

        ExitKeplerRails();

        if (nonShipBodiesAdvanced)
        {
            EnforceSphereCollisionWithPrimary();
            IntegrateShipNumericalWithSoi(dt);
            IntegrateShipControlledPhysicsSubstep(dt);
            EnforceSphereCollisionWithPrimary(dt);
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        double remaining = dt;

        while (remaining > 0.0)
        {
            double step = Math.Min(remaining, MaxNumericalPhysicsStep);
            IntegrateNonShipBodies(step);
            EnforceSphereCollisionWithPrimary(step);
            IntegrateShipNumericalWithSoi(step);
            IntegrateShipControlledPhysicsSubstep(step);
            EnforceSphereCollisionWithPrimary(step);
            remaining -= step;
        }

        simulationTime += dt;
        FinalizeUpdateStep();
    }

    void UpdateVacuumDocking(double dt)
    {
        if (ship.flightStatus == FlightStatus.Crashed)
        {
            HandleCrashedStep();
            simulationTime += dt;
            FinalizeUpdateStep();
            return;
        }

        ship.acceleration = SimVector3.Zero();
        ship.dragAcceleration = SimVector3.Zero();
        AdvanceIndependentVesselPhysics(ship, dt);
        simulationTime += dt;
        FinalizeUpdateStep();
    }

    void IntegrateNonShipBodies(double dt)
    {
        SimVector3[] oldAccelerations = CalculateAccelerations();

        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == ship || IsRegisteredDockableVessel(bodies[i]))
            {
                continue;
            }

            bodies[i].acceleration = oldAccelerations[i];
            Integrator.StepPosition(bodies[i], oldAccelerations[i], dt);
        }

        SimVector3[] newAccelerations = CalculateAccelerations();

        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == ship || IsRegisteredDockableVessel(bodies[i]))
            {
                continue;
            }

            bodies[i].acceleration = newAccelerations[i];
            Integrator.StepVelocity(
                bodies[i],
                oldAccelerations[i],
                newAccelerations[i],
                dt
            );
        }
    }

    void IntegrateShipBody(double dt)
    {
        SimVector3[] oldAccelerations = CalculateAccelerations();
        int shipIndex = Array.IndexOf(bodies, ship);

        if (shipIndex < 0)
        {
            return;
        }

        ship.acceleration = oldAccelerations[shipIndex];
        Integrator.StepPosition(ship, oldAccelerations[shipIndex], dt);

        SimVector3[] newAccelerations = CalculateAccelerations();
        ship.acceleration = newAccelerations[shipIndex];
        Integrator.StepVelocity(
            ship,
            oldAccelerations[shipIndex],
            newAccelerations[shipIndex],
            dt
        );
    }

    internal void IntegrateShipGravityOnly(double dt)
    {
        if (ScenarioHasSun())
        {
            Body parent = GetActivePrimaryBody();

            if (parent == null)
            {
                return;
            }

            TryGetSolarStepStart(
                parent,
                out SimVector3 parentPositionAtStepStart,
                out SimVector3 parentVelocityAtStepStart
            );
            IntegrateBodyRelativeToParent(
                ship,
                parent,
                dt,
                parentPositionAtStepStart,
                parentVelocityAtStepStart
            );
            return;
        }

        IntegrateShipBody(dt);
    }

    void IntegrateShipNumericalWithSoi(double dt)
    {
        if (dt <= 0.0)
        {
            return;
        }

        SimVector3 startPosition = ship.position;
        SimVector3 startVelocity = ship.velocity;
        Body primaryAtStart = GetActivePrimaryBody();

        RestoreShipState(startPosition, startVelocity);
        IntegrateShipGravityOnly(dt);
        Body primaryAtEnd = ResolveGeometricPrimaryAtPosition(
            ship.position,
            primaryAtStart
        );

        if (primaryAtEnd != primaryAtStart)
        {
            if (
                !SoiTransition.TryFindBoundaryFraction(
                    this,
                    primaryAtStart,
                    startPosition,
                    startVelocity,
                    dt,
                    out double boundaryFraction,
                    out Body primaryAfterTransition
                )
            )
            {
                return;
            }

            ApplySoiBoundaryAndRemainder(
                startPosition,
                startVelocity,
                dt,
                boundaryFraction,
                primaryAfterTransition
            );
            return;
        }

        TryApplyMoonSoiEntryThisStep(startPosition, startVelocity, dt, primaryAtStart);
    }

    void ApplySoiBoundaryAndRemainder(
        SimVector3 startPosition,
        SimVector3 startVelocity,
        double dt,
        double boundaryFraction,
        Body primaryAfterTransition
    )
    {
        double remainderDt = dt * (1.0 - boundaryFraction);

        RestoreShipState(startPosition, startVelocity);
        IntegrateShipGravityOnly(dt * boundaryFraction);
        SoiTransition.TransformShipToPrimary(this, primaryAfterTransition);
        ExitKeplerRails();

        if (remainderDt > 1e-12)
        {
            IntegrateShipGravityOnly(remainderDt);
        }
    }

    bool TryApplyMoonSoiEntryThisStep(
        SimVector3 startPosition,
        SimVector3 startVelocity,
        double dt,
        Body primaryAtStart
    )
    {
        if (
            moon == null ||
            primaryAtStart == moon ||
            moonSphereOfInfluenceRadius <= 0.0
        )
        {
            return false;
        }

        bool endedInsideMoon =
            moon.position.Subtract(ship.position).Length() <= moonSphereOfInfluenceRadius;
        bool pathEnteredMoon = SoiGeometry.SegmentComesWithinRadius(
            startPosition,
            ship.position,
            moon.position,
            moonSphereOfInfluenceRadius
        );

        if (!endedInsideMoon && !pathEnteredMoon)
        {
            return false;
        }

        if (
            !TryFindMoonSoiEntryFraction(
                startPosition,
                out double entryFraction
            )
        )
        {
            return false;
        }

        ApplyMoonSoiEntryAlongStep(
            startPosition,
            startVelocity,
            ship.position,
            ship.velocity,
            dt,
            entryFraction
        );
        return true;
    }

    void ApplyMoonSoiEntryAlongStep(
        SimVector3 startPosition,
        SimVector3 startVelocity,
        SimVector3 endPosition,
        SimVector3 endVelocity,
        double dt,
        double entryFraction
    )
    {
        double remainderDt = dt * (1.0 - entryFraction);
        SimVector3 boundaryPosition = startPosition.Add(
            endPosition.Subtract(startPosition).Multiply(entryFraction)
        );
        SimVector3 boundaryVelocity = startVelocity.Add(
            endVelocity.Subtract(startVelocity).Multiply(entryFraction)
        );

        RestoreShipState(boundaryPosition, boundaryVelocity);
        SoiTransition.TransformShipToPrimary(this, moon);
        ExitKeplerRails();

        if (remainderDt > 1e-12)
        {
            IntegrateShipGravityOnly(remainderDt);
        }
    }

    bool TryFindMoonSoiEntryFraction(
        SimVector3 startPosition,
        out double entryFraction
    )
    {
        return SoiGeometry.TryFindSphereEntryFraction(
            startPosition,
            ship.position,
            moon.position,
            moonSphereOfInfluenceRadius,
            out entryFraction
        );
    }

    bool ShouldUseKeplerRails(Vessel vessel)
    {
        if (vessel == null || vessel.flightStatus != FlightStatus.Flying)
        {
            return false;
        }

        if (vessel.controlState.throttle > 0.01f)
        {
            return false;
        }

        if (
            Math.Abs(vessel.controlState.pitchInput) > 1e-6f ||
            Math.Abs(vessel.controlState.yawInput) > 1e-6f ||
            Math.Abs(vessel.controlState.rollInput) > 1e-6f
        )
        {
            return false;
        }

        if (vessel.controlState.rcsVector.LengthSquared() > 0.0)
        {
            return false;
        }

        if (vessel.thrust.LengthSquared() > RailsThrustEpsilonSquared)
        {
            return false;
        }

        if (HasAtmosphericInfluence())
        {
            return false;
        }

        Body primary = GetActivePrimaryBody();

        if (primary == null || primary.radius <= 0.0)
        {
            return false;
        }

        if (GetVesselAltitudeAboveBody(vessel, primary) <= 0.0)
        {
            return false;
        }

        if (
            ScenarioHasMoon() &&
            moon != null &&
            primary != moon &&
            moonSphereOfInfluenceRadius > 0.0
        )
        {
            double distanceToMoon = moon.position.Subtract(vessel.position).Length();
            if (distanceToMoon <= moonSphereOfInfluenceRadius * MoonSoiNumericalPatchMultiplier)
            {
                return false;
            }
        }

        return KeplerPropagator.TryCreateState(
            primary,
            vessel.position,
            vessel.velocity,
            simulationTime,
            out _
        );
    }

    double GetVesselAltitudeAboveBody(Vessel vessel, Body body)
    {
        if (vessel == null || body == null)
        {
            return double.NegativeInfinity;
        }

        if (vessel == ship)
        {
            return GetShipAltitudeAboveBody(body);
        }

        SimVector3 comWorld = vessel.GetCenterOfMassWorld();
        SimVector3 offsetFromBody = comWorld.Subtract(body.position);
        double distanceToCom = offsetFromBody.Length();

        if (distanceToCom <= 1e-9)
        {
            return -vessel.collisionRadius;
        }

        SimVector3 outward = offsetFromBody.Multiply(1.0 / distanceToCom);
        double bottomExtent = vessel.GetBottomExtentAlongWorldDirection(outward);

        return distanceToCom - body.radius - bottomExtent;
    }

    bool HasAtmosphericInfluence()
    {
        return GetAtmosphereBody() != null;
    }

    void TryEnterKeplerRails()
    {
        TryEnterKeplerRailsForVessel(ship);
    }

    void TryEnterKeplerRailsForVessel(Vessel vessel)
    {
        Body primary = GetActivePrimaryBody();

        if (
            KeplerPropagator.TryCreateState(
                primary,
                vessel.position,
                vessel.velocity,
                simulationTime,
                out KeplerRailsState railsState
            )
        )
        {
            vessel.keplerRails = railsState;
            vessel.isOnKeplerRails = true;
        }
    }

    void PropagateDebrisVessels(double dt)
    {
        for (int i = 0; i < debrisVessels.Count; i++)
        {
            Vessel debris = debrisVessels[i];

            if (!debris.isOnKeplerRails)
            {
                TryEnterKeplerRailsForVessel(debris);
            }

            if (!debris.isOnKeplerRails)
            {
                continue;
            }

            Body primary = GetBodyByName(debris.keplerRails.primaryBodyName);

            if (primary == null)
            {
                debris.isOnKeplerRails = false;
                debris.keplerRails = KeplerRailsState.Invalid();
                continue;
            }

            double targetTime = simulationTime + dt;

            if (
                !KeplerPropagator.TryPropagate(
                    ref debris.keplerRails,
                    primary,
                    targetTime,
                    out SimVector3 relativePosition,
                    out SimVector3 relativeVelocity
                )
            )
            {
                debris.isOnKeplerRails = false;
                debris.keplerRails = KeplerRailsState.Invalid();
                continue;
            }

            debris.position = primary.position.Add(relativePosition);
            debris.velocity = primary.velocity.Add(relativeVelocity);
        }
    }

    void PropagateDockableVessels(double dt)
    {
        for (int i = 0; i < dockableVessels.Count; i++)
        {
            Vessel dockable = dockableVessels[i];

            if (ScenarioIsVacuumDocking())
            {
                dockable.isOnKeplerRails = false;
                dockable.keplerRails = KeplerRailsState.Invalid();
                AdvanceIndependentVesselPhysics(dockable, dt);
                continue;
            }

            if (!dockable.isOnKeplerRails)
            {
                TryEnterKeplerRailsForVessel(dockable);
            }

            if (!dockable.isOnKeplerRails)
            {
                continue;
            }

            Body primary = GetBodyByName(dockable.keplerRails.primaryBodyName);

            if (primary == null)
            {
                dockable.isOnKeplerRails = false;
                dockable.keplerRails = KeplerRailsState.Invalid();
                continue;
            }

            double targetTime = simulationTime + dt;

            if (
                !KeplerPropagator.TryPropagate(
                    ref dockable.keplerRails,
                    primary,
                    targetTime,
                    out SimVector3 relativePosition,
                    out SimVector3 relativeVelocity
                )
            )
            {
                dockable.isOnKeplerRails = false;
                dockable.keplerRails = KeplerRailsState.Invalid();
                continue;
            }

            dockable.position = primary.position.Add(relativePosition);
            dockable.velocity = primary.velocity.Add(relativeVelocity);
        }
    }

    public void RegisterDockableVessel(Vessel vessel)
    {
        if (vessel == null || vessel == ship)
        {
            return;
        }

        if (dockableVessels.Contains(vessel))
        {
            return;
        }

        dockableVessels.Add(vessel);

        if (!vesselIds.ContainsKey(vessel))
        {
            RegisterVesselInRegistry(vessel);
        }

        if (!ScenarioIsVacuumDocking())
        {
            TryEnterKeplerRailsForVessel(vessel);
        }

        AppendBodyToBodies(vessel);
    }

    bool IsRegisteredDockableVessel(Body body)
    {
        for (int i = 0; i < dockableVessels.Count; i++)
        {
            if (dockableVessels[i] == body)
            {
                return true;
            }
        }

        return false;
    }

    void AppendBodyToBodies(Body body)
    {
        Body[] newBodies = new Body[bodies.Length + 1];

        for (int i = 0; i < bodies.Length; i++)
        {
            newBodies[i] = bodies[i];
        }

        newBodies[bodies.Length] = body;
        bodies = newBodies;
    }

    void TryPerformDocking()
    {
        Vessel active = activeVessel;
        Vessel target = targetVessel;

        if (active == null || target == null || active == target)
        {
            return;
        }

        if (
            active.flightStatus != FlightStatus.Flying ||
            target.flightStatus != FlightStatus.Flying
        )
        {
            return;
        }

        if (!dockableVessels.Contains(target))
        {
            return;
        }

        if (!CanDock(active, target))
        {
            return;
        }

        MergeVessels(active, target);
    }

    bool CanDock(Vessel source, Vessel target)
    {
        DockingPortContactMetrics contact =
            source.GetDockingPortContactMetrics(target);

        if (!contact.HasValidPorts)
        {
            return false;
        }

        if (contact.portSeparation > dockingConfig.maxDistance)
        {
            return false;
        }

        if (contact.relativePortSpeed > dockingConfig.maxRelativeSpeed)
        {
            return false;
        }

        return contact.IsAlignedForCapture(dockingConfig.minAlignmentDot);
    }

    bool IsAlignedForDocking(Vessel source, Vessel target)
    {
        DockingPortContactMetrics contact =
            source.GetDockingPortContactMetrics(target);

        return contact.IsAlignedForCapture(dockingConfig.minAlignmentDot);
    }

    void SnapVesselsPortToPort(Vessel source, Vessel target)
    {
        if (
            !source.TryGetPrimaryDockingPortLocal(out _) ||
            !target.TryGetPrimaryDockingPortLocal(out _)
        )
        {
            return;
        }

        SimVector3 sourceFaceWorld = source.GetDockingPortFaceWorldPosition();
        SimVector3 targetFaceWorld = target.GetDockingPortFaceWorldPosition();
        SimVector3 sourceToTarget = targetFaceWorld.Subtract(sourceFaceWorld);
        SimVector3 axis = source.GetDockingPortForwardWorld();

        if (axis.LengthSquared() <= 1e-12)
        {
            return;
        }

        axis = axis.Normalize();
        double faceGap = SimVector3.Dot(sourceToTarget, axis);

        if (faceGap <= 0.0)
        {
            return;
        }

        SimVector3 faceCorrection = axis.Multiply(faceGap * 0.5);
        source.position = source.position.Add(faceCorrection);
        target.position = target.position.Subtract(faceCorrection);

        double massSource = Math.Max(source.GetMass(), 1e-9);
        double massTarget = Math.Max(target.GetMass(), 1e-9);
        SimVector3 mergedVelocity = source.velocity
            .Multiply(massSource)
            .Add(target.velocity.Multiply(massTarget))
            .Multiply(1.0 / (massSource + massTarget));
        source.velocity = mergedVelocity;
        target.velocity = mergedVelocity;
    }

    public void MergeVessels(Vessel source, Vessel target)
    {
        if (source == null || target == null || source == target)
        {
            return;
        }

        SnapVesselsPortToPort(source, target);

        if (target.stages != null && target.stages.Count > 0)
        {
            if (source.stages == null)
            {
                source.stages = new List<StageData>();
            }

            for (int i = 0; i < target.stages.Count; i++)
            {
                StageData.IntegrateDockedPartnerIntoHostStages(
                    source,
                    target,
                    target.stages[i]
                );
            }

            target.stages.Clear();
        }

        source.payloadMass += target.payloadMass;
        source.rcsFuel += target.rcsFuel;
        source.IsDocked = true;

        if (source.stages != null && source.stages.Count > 0)
        {
            StageData.EnsureDockingPortBlock(
                source.stages[source.stages.Count - 1]
            );
        }

        source.NotifyShipLayoutChanged();
        source.RecalculateMassAndDeltaV(
            GetShipLocalGravityRelativeToActivePrimary()
        );

        RemoveVesselFromSimulation(target);
        lastDockingMessage = "Статус: Стыковка успешна. Порты сопряжены";
        worldRevision++;
    }

    public double GetDockingPortSeparationToTarget()
    {
        if (targetVessel == null)
        {
            return double.NaN;
        }

        return ship.GetDockingPortContactMetrics(targetVessel).portSeparation;
    }

    void RemoveVesselFromSimulation(Vessel vessel)
    {
        dockableVessels.Remove(vessel);
        debrisVessels.Remove(vessel);
        UnregisterVesselFromRegistry(vessel);

        if (targetVessel == vessel)
        {
            targetVessel = null;
        }

        if (vessel == ship)
        {
            return;
        }

        List<Body> remainingBodies = new List<Body>(bodies.Length);

        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != vessel)
            {
                remainingBodies.Add(bodies[i]);
            }
        }

        bodies = remainingBodies.ToArray();
    }

    public string GetLastDockingMessage()
    {
        return lastDockingMessage;
    }

    public double GetVesselHeadingRadians(string vesselName)
    {
        if (ship != null && ship.name == vesselName)
        {
            return ship.heading;
        }

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            Vessel dockable = dockableVessels[i];

            if (dockable.name == vesselName)
            {
                return dockable.heading;
            }
        }

        return 0.0;
    }

    void ExitKeplerRails()
    {
        ship.isOnKeplerRails = false;
        ship.keplerRails = KeplerRailsState.Invalid();
    }

    bool PropagateShipOnKeplerRails(double dt)
    {
        Body primary = GetActivePrimaryBody();
        SimVector3 startPosition = ship.position;
        SimVector3 startVelocity = ship.velocity;
        double targetTime = simulationTime + dt;

        if (
            !KeplerPropagator.TryPropagate(
                ref ship.keplerRails,
                primary,
                targetTime,
                out SimVector3 relativePosition,
                out SimVector3 relativeVelocity
            )
        )
        {
            ExitKeplerRails();
            return false;
        }

        ship.position = primary.position.Add(relativePosition);
        ship.velocity = primary.velocity.Add(relativeVelocity);
        SimVector3 keplerEndPosition = ship.position;
        SimVector3 keplerEndVelocity = ship.velocity;

        Body primaryAtEnd = ResolveGeometricPrimaryAtPosition(ship.position, primary);

        if (primaryAtEnd != primary)
        {
            if (
                SoiTransition.TryFindBoundaryFraction(
                    this,
                    primary,
                    startPosition,
                    startVelocity,
                    dt,
                    out double boundaryFraction,
                    out Body primaryAfterTransition
                )
            )
            {
                ApplySoiBoundaryAndRemainder(
                    startPosition,
                    startVelocity,
                    dt,
                    boundaryFraction,
                    primaryAfterTransition
                );
                return true;
            }

            RestoreShipState(keplerEndPosition, keplerEndVelocity);
        }

        if (TryApplyMoonSoiEntryThisStep(startPosition, startVelocity, dt, primary))
        {
            return true;
        }

        int shipIndex = Array.IndexOf(bodies, ship);

        if (shipIndex >= 0)
        {
            SimVector3[] accelerations = CalculateAccelerations();
            ship.acceleration = accelerations[shipIndex];
        }

        return true;
    }

    internal void RestoreShipState(SimVector3 position, SimVector3 velocity)
    {
        ship.position = position;
        ship.velocity = velocity;
    }

    internal void SetActivePrimaryForIntegration(string primaryName)
    {
        lastActivePrimaryForHysteresis = primaryName;
        activePrimaryBodyName = primaryName;
    }

    internal Body ResolveGeometricPrimaryAtPosition(
        SimVector3 shipPosition,
        Body previousPrimary
    )
    {
        if (previousPrimary == null)
        {
            previousPrimary = planet;
        }

        if (ScenarioHasMoon() && moon != null)
        {
            double distanceToMoon =
                moon.position.Subtract(shipPosition).Length();

            if (previousPrimary == moon)
            {
                if (distanceToMoon <= moonSphereOfInfluenceRadius)
                {
                    return moon;
                }
            }
            else if (distanceToMoon <= moonSphereOfInfluenceRadius)
            {
                return moon;
            }
        }

        if (ScenarioHasSun())
        {
            double distanceToPlanet =
                planet.position.Subtract(shipPosition).Length();

            if (previousPrimary == sun)
            {
                if (distanceToPlanet > planetSphereOfInfluenceRadius)
                {
                    return sun;
                }

                return planet;
            }

            if (previousPrimary == planet)
            {
                if (distanceToPlanet <= planetSphereOfInfluenceRadius)
                {
                    return planet;
                }

                return sun;
            }

            if (distanceToPlanet <= planetSphereOfInfluenceRadius)
            {
                return planet;
            }

            return sun;
        }

        return planet;
    }

    public bool IsShipOnKeplerRails()
    {
        return ship.isOnKeplerRails;
    }

    public bool CanUseLargeCoastWarpSteps()
    {
        return ShouldUseKeplerRails(ship);
    }

    void FinalizeUpdateStep()
    {
        if (ship.flightStatus != FlightStatus.Landed)
        {
            EnforceSphereCollisionWithPrimary();
        }

        if (ship.flightStatus == FlightStatus.Flying && !ScenarioIsVacuumDocking())
        {
            TryResolveSurfaceContact();
        }

        if (moon != null)
        {
            lastMoonGravityAcceleration = GetBodyAcceleration(moon);
        }
        else
        {
            lastMoonGravityAcceleration = SimVector3.Zero();
        }

        lastGravityAcceleration = GetBodyAcceleration(ship);
        Body activePrimaryBody = ResolveActivePrimaryBody();

        if (activePrimaryBody != null)
        {
            lastActivePrimaryForHysteresis = activePrimaryBody.name;
            activePrimaryBodyName = activePrimaryBody.name;
        }
        else if (ScenarioIsVacuumDocking() && ship != null)
        {
            lastActivePrimaryForHysteresis = ship.name;
            activePrimaryBodyName = ship.name;
        }

        UpdateOrbitStats();

        if (!isOrbitPreview && ship.flightStatus != FlightStatus.Landed)
        {
            ship.RecalculateMassAndDeltaV(
                GetShipLocalGravityRelativeToActivePrimary()
            );
        }
    }

    void ApplyAtmosphericDrag(double dt)
    {
        ship.dragAcceleration = SimVector3.Zero();
        lastAtmosphericDensity = 0.0;
        lastDynamicPressure = 0.0;
        lastDragForce = 0.0;
        lastLiftForce = 0.0;
        lastEffectiveDragArea = 0.0;
        lastDragTorqueZ = 0.0;
        lastAngleOfAttack = 0.0;
        lastLiftCoefficient = 0.0;
        lastDragCoefficient = 0.0;
        ship.reentryIntensity = 0.0;

        if (ship.flightStatus != FlightStatus.Flying)
        {
            ship.angularVelocity = SimVector3.Zero();
            return;
        }

        Body atmosphereBody = GetAtmosphereBody();

        if (atmosphereBody == null)
        {
            return;
        }

        StageData activeStage = ship.GetActiveStage();

        if (activeStage == null)
        {
            return;
        }

        double mass = ship.GetMass();

        if (mass <= 0.0)
        {
            return;
        }

        double altitude = GetShipAltitudeAboveBody(atmosphereBody);
        AtmosphereProfile profile = atmosphereBody.atmosphereProfile;
        SimVector3 relativeVelocity =
            ship.velocity.Subtract(atmosphereBody.velocity);
        AerodynamicProperties aerodynamics = AerodynamicProperties.FromStageData(
            activeStage,
            ship.centerOfPressureOffset
        );
        AtmosphericDragResult drag = atmosphereModel.CalculateDrag(
            profile,
            altitude,
            relativeVelocity,
            aerodynamics,
            ship.orientation,
            mass
        );

        ship.dragAcceleration = drag.acceleration;
        lastAtmosphericDensity = drag.density;
        lastDynamicPressure = drag.dynamicPressure;
        lastDragForce = drag.dragForceMagnitude;
        lastLiftForce = drag.liftForceMagnitude;
        lastEffectiveDragArea = drag.effectiveArea;
        lastDragTorqueZ = drag.torqueZ;
        lastAngleOfAttack = drag.angleOfAttack;
        lastLiftCoefficient = Aerodynamics.CalculateLiftCoefficient(
            drag.angleOfAttack,
            aerodynamics.liftCoeff
        );
        lastDragCoefficient =
            Aerodynamics.CalculateProfileDragCoefficient(
                drag.angleOfAttack,
                aerodynamics.dragCoeff,
                aerodynamics.forwardArea,
                aerodynamics.sideArea
            ) +
            Aerodynamics.CalculateInducedDragCoefficient(
                lastLiftCoefficient,
                aerodynamics.inducedDragFactor
            );
        ship.velocity = ship.velocity.Add(ship.dragAcceleration.Multiply(dt));
        UpdateReentryIntensity(relativeVelocity.Length(), drag.dynamicPressure);

        if (atmosphericSasEnabled)
        {
            const double sasDamping = 12.0;
            ship.angularVelocity = new SimVector3(
                ship.angularVelocity.x,
                ship.angularVelocity.y,
                ship.angularVelocity.z * Math.Exp(-sasDamping * dt)
            );
        }
        else if (Math.Abs(drag.torqueZ) > 0.0)
        {
            double angularAcceleration = drag.torqueZ / ship.GetYawMomentOfInertia();
            ship.ApplyYawAngularAcceleration(angularAcceleration, dt);
        }
    }

    internal Body GetAtmosphereBody()
    {
        if (
            planet != null &&
            planet.atmosphereProfile != null &&
            planet.atmosphereProfile.HasAtmosphere
        )
        {
            double altitude = GetShipAltitudeAboveBody(planet);

            if (altitude >= 0.0)
            {
                double density = atmosphereModel.GetDensity(
                    planet.atmosphereProfile,
                    altitude
                );

                if (density > 0.0)
                {
                    return planet;
                }
            }
        }

        return null;
    }

    void ApplyShipThrust(double dt)
    {
        ApplyVesselMainEngineThrust(ship, dt);
    }

    void ApplyVesselMainEngineThrust(Vessel vessel, double dt)
    {
        SimVector3 localDirectThrustOverride = vessel == ship ?
            vessel.thrust :
            SimVector3.Zero();
        vessel.rcsAcceleration = SimVector3.Zero();
        vessel.thrustAcceleration = SimVector3.Zero();

        if (vessel.flightStatus == FlightStatus.Crashed)
        {
            vessel.thrust = SimVector3.Zero();
            return;
        }

        SimVector3 localThrustWorld;

        if (localDirectThrustOverride.LengthSquared() > RailsThrustEpsilonSquared)
        {
            localThrustWorld = localDirectThrustOverride;
            vessel.thrust = localThrustWorld;
        }
        else if (vessel.controlState.throttle > 0.01f)
        {
            localThrustWorld = GetVesselMainEngineThrustVector(vessel);
            vessel.thrust = localThrustWorld;
        }
        else
        {
            vessel.thrust = SimVector3.Zero();
            return;
        }

        StageData activeStage = vessel.GetActiveStage();

        if (
            localThrustWorld.LengthSquared() <= 0.0 ||
            activeStage == null
        )
        {
            vessel.thrust = SimVector3.Zero();
            return;
        }

        double thrustMagnitude =
            localThrustWorld.Length() * SimulationCheatFlags.ThrustMultiplier;

        if (
            !vessel.TryBurnPropellantForThrust(
                thrustMagnitude,
                dt,
                out double burnScale,
                GetShipLocalGravityRelativeToActivePrimary()
            )
        )
        {
            vessel.thrust = SimVector3.Zero();
            return;
        }

        double mass = vessel.GetMass();

        if (mass <= 0.0)
        {
            vessel.thrust = SimVector3.Zero();
            return;
        }

        vessel.thrustAcceleration = localThrustWorld
            .Multiply(SimulationCheatFlags.ThrustMultiplier * burnScale)
            .Multiply(1.0 / mass);

        if (vessel.flightStatus == FlightStatus.Landed)
        {
            vessel.rcsAcceleration = SimVector3.Zero();
            return;
        }

        vessel.ApplyMainEngineThrust(
            vessel.controlState.throttle,
            burnScale,
            dt,
            ShouldSuppressMainEngineTorque(vessel)
        );
        vessel.rcsAcceleration = SimVector3.Zero();
    }

    void HandleCrashedStep()
    {
        ExitKeplerRails();
        Body surfaceBody = GetActivePrimaryBody();
        PinShipToSurface(surfaceBody);
        ship.velocity = GetLandedSurfaceVelocity(surfaceBody);
        ship.thrust = SimVector3.Zero();
        ship.thrustAcceleration = SimVector3.Zero();
        ship.rcsAcceleration = SimVector3.Zero();
        ship.dragAcceleration = SimVector3.Zero();
        ship.angularVelocity = SimVector3.Zero();
        ship.acceleration = SimVector3.Zero();
    }

    void HandleLandedStep(double dt)
    {
        ExitKeplerRails();
        AdvanceUniverseBodiesWithoutShip(dt);

        Body primary = GetActivePrimaryBody();

        if (primary == null || primary.radius <= 0.0)
        {
            return;
        }

        if (sun == null)
        {
            primary.AdvanceSpin(dt);
        }

        if (ScenarioUsesSurfaceLaunchPad())
        {
            SyncLandedVesselToRotatingSurface(primary, ship);
            return;
        }

        CoRotateLandedShipWithPlanet(primary, dt);

        SimVector3 surfaceNormal = GetLandedSurfaceDirection(primary);
        ship.angularVelocity = SimVector3.Zero();

        if (ship.controlState.throttle > 0.01f)
        {
            ApplyVesselMainEngineThrust(ship, dt);
        }
        else
        {
            ship.thrust = SimVector3.Zero();
            ship.thrustAcceleration = SimVector3.Zero();
        }

        ship.acceleration = SimVector3.Zero();
        ship.rcsAcceleration = SimVector3.Zero();
        ship.dragAcceleration = SimVector3.Zero();

        if (ShouldLiftoffFromSurface(primary))
        {
            ship.flightStatus = FlightStatus.Flying;
            ship.landedOnBodyName = string.Empty;
            ship.landedSurfaceDirectionBodyFixed = SimVector3.Zero();
            lastFlightEventMessage = "Liftoff from " + primary.name;

            double thrustForce =
                ship.GetActiveEngineThrustNewtons() * ship.controlState.throttle;
            double mass = ship.GetMass();
            double localGravity = GetBodySurfaceGravity(primary);
            double weight = mass * localGravity;

            if (mass > 0.0 && thrustForce > weight * LiftoffThrustMargin)
            {
                double netAccel = thrustForce / mass - localGravity;

                if (netAccel > 0.0)
                {
                    SimVector3 surfaceVelocity = GetLandedSurfaceVelocity(primary);
                    double liftoffSpeed = Math.Max(1.0, netAccel * Math.Max(dt, 1.0 / 60.0));
                    ship.velocity = surfaceVelocity.Add(
                        surfaceNormal.Multiply(liftoffSpeed)
                    );
                }
            }

            ship.angularVelocity = SimVector3.Zero();
            return;
        }

        PinShipToSurface(primary, surfaceNormal);
        ship.velocity = GetLandedSurfaceVelocity(primary);
        ship.angularVelocity = SimVector3.Zero();
    }

    SimVector3 GetVesselSurfaceVelocity(Body primary, Vessel vessel)
    {
        if (primary == null || vessel == null)
        {
            return SimVector3.Zero();
        }

        return primary.GetAbsoluteSurfaceVelocityAt(vessel.GetCenterOfMassWorld());
    }

    SimVector3 GetLandedSurfaceVelocity(Body primary)
    {
        return GetVesselSurfaceVelocity(primary, ship);
    }

    void CoRotateLandedShipPositionWithPlanet(Body primary, Vessel vessel, double dt)
    {
        if (
            primary == null ||
            vessel == null ||
            dt <= 0.0 ||
            Math.Abs(primary.spinRadiansPerSecond) <= 1e-12
        )
        {
            return;
        }

        SimVector3 spinAxis = primary.spinAxis.LengthSquared() > 1e-12 ?
            primary.spinAxis.Normalize() :
            SimVector3.UnitY();
        double spinStep = primary.spinRadiansPerSecond * dt;
        SimQuaternion deltaSpin = SimQuaternion.FromAxisAngle(spinAxis, spinStep);
        SimVector3 centerOfMassWorld = vessel.GetCenterOfMassWorld();
        SimVector3 radiusFromPlanet = centerOfMassWorld.Subtract(primary.position);
        SimVector3 rotatedRadius = deltaSpin.Rotate(radiusFromPlanet);
        SimVector3 newCenterOfMassWorld = primary.position.Add(rotatedRadius);

        vessel.position = newCenterOfMassWorld.Subtract(
            vessel.orientation.Rotate(vessel.centerOfMassLocal)
        );
    }

    void CoRotateLandedShipWithPlanet(Body primary, double dt)
    {
        if (
            primary == null ||
            dt <= 0.0 ||
            Math.Abs(primary.spinRadiansPerSecond) <= 1e-12
        )
        {
            return;
        }

        SimVector3 spinAxis = primary.spinAxis.LengthSquared() > 1e-12 ?
            primary.spinAxis.Normalize() :
            SimVector3.UnitY();
        double spinStep = primary.spinRadiansPerSecond * dt;
        SimQuaternion deltaSpin = SimQuaternion.FromAxisAngle(spinAxis, spinStep);
        SimVector3 centerOfMassWorld = ship.GetCenterOfMassWorld();
        SimVector3 radiusFromPlanet = centerOfMassWorld.Subtract(primary.position);
        SimVector3 rotatedRadius = deltaSpin.Rotate(radiusFromPlanet);
        SimVector3 newCenterOfMassWorld = primary.position.Add(rotatedRadius);

        ship.orientation = SimQuaternion.Multiply(deltaSpin, ship.orientation);
        ship.position = newCenterOfMassWorld.Subtract(
            ship.orientation.Rotate(ship.centerOfMassLocal)
        );
        ship.SyncHeadingFromOrientation();
    }

    void AdvanceUniverseBodiesWithoutShip(double dt)
    {
        if (dt <= 0.0)
        {
            return;
        }

        if (sun != null)
        {
            IntegrateSolarSystemBodies(dt);
            return;
        }

        IntegrateNonShipBodies(dt);
    }

    static double GetBodySurfaceGravity(Body body)
    {
        if (body == null || body.radius <= 0.0)
        {
            return 0.0;
        }

        return body.mu / (body.radius * body.radius);
    }

    SimVector3 GetLandedSurfaceDirection(Body primary)
    {
        if (
            !string.IsNullOrEmpty(ship.landedOnBodyName) &&
            ship.landedOnBodyName == primary.name &&
            ship.landedSurfaceDirectionBodyFixed.LengthSquared() > 1e-12
        )
        {
            return primary
                .RotateBodyFixedToWorld(ship.landedSurfaceDirectionBodyFixed)
                .Normalize();
        }

        SimVector3 offset = ship.position.Subtract(primary.position);

        if (offset.LengthSquared() > 1e-12)
        {
            SimVector3 worldNormal = offset.Normalize();
            ship.lastSurfaceNormal = worldNormal;
            CaptureLandedSurfaceDirectionBodyFixed(primary, worldNormal);
            return worldNormal;
        }

        ship.lastSurfaceNormal = GetDefaultLaunchSurfaceNormal();
        return ship.lastSurfaceNormal;
    }

    SimVector3 GetDefaultLaunchSurfaceNormal()
    {
        if (ship != null)
        {
            return GetShipSurfaceUp();
        }

        return ScenarioUsesSurfaceLaunchPad() ?
            LandingConfig.GetSurfaceLaunchPadNormal() :
            SimVector3.UnitY();
    }

    void CaptureLandedSurfaceDirectionBodyFixed(
        Body primary,
        SimVector3 worldNormal
    )
    {
        ship.landedSurfaceDirectionBodyFixed = primary
            .GetSpinRotation()
            .Conjugate()
            .Rotate(worldNormal)
            .Normalize();
    }

    bool ShouldSuppressMainEngineTorque(Vessel vessel)
    {
        if (!ScenarioUsesSurfaceLaunchPad() || vessel == null)
        {
            return false;
        }

        Body primary = GetActivePrimaryBody();

        if (primary == null)
        {
            return true;
        }

        return GetShipAltitudeAboveBody(primary) <
            LandingConfig.SurfaceLaunchAssistClearanceAltitude;
    }

    bool ShouldLiftoffFromSurface(Body primary)
    {
        if (primary == null || ship.controlState.throttle <= 0.01f)
        {
            return false;
        }

        double thrustForce =
            ship.GetActiveEngineThrustNewtons() * ship.controlState.throttle;
        double weight = ship.GetMass() * GetBodySurfaceGravity(primary);

        return thrustForce >= weight * LiftoffThrustMargin;
    }

    void EnforceSphereCollisionWithPrimary(double dt = 0.0)
    {
        Body surfaceBody = GetActivePrimaryBody();

        if (surfaceBody == null || surfaceBody.radius <= 0.0)
        {
            return;
        }

        double altitude = GetShipAltitudeAboveBody(surfaceBody);

        if (altitude > 0.0)
        {
            return;
        }

        SimVector3 surfaceNormal = GetSurfaceNormal(surfaceBody);
        SimVector3 relativeVelocity =
            ship.velocity.Subtract(surfaceBody.velocity);
        double verticalSpeed = SimVector3.Dot(relativeVelocity, surfaceNormal);
        double localGravity = GetBodySurfaceGravity(surfaceBody);
        double thrustForce =
            ship.GetActiveEngineThrustNewtons() * ship.controlState.throttle;
        double weight = ship.GetMass() * localGravity;
        bool climbingUnderThrust =
            ship.flightStatus == FlightStatus.Flying &&
            verticalSpeed > 0.0 &&
            ship.controlState.throttle > 0.01f &&
            thrustForce >= weight;

        if (climbingUnderThrust)
        {
            return;
        }

        if (ship.flightStatus == FlightStatus.Flying)
        {
            ship.ProcessBlockSurfaceImpacts(
                surfaceNormal,
                surfaceBody.velocity,
                localGravity
            );
        }

        PinShipToSurface(surfaceBody, surfaceNormal);

        relativeVelocity = ship.velocity.Subtract(surfaceBody.velocity);
        verticalSpeed = SimVector3.Dot(relativeVelocity, surfaceNormal);

        if (verticalSpeed < 0.0)
        {
            relativeVelocity = relativeVelocity.Subtract(
                surfaceNormal.Multiply(verticalSpeed)
            );
            verticalSpeed = 0.0;
        }

        SimVector3 tangentialVelocity = relativeVelocity.Subtract(
            surfaceNormal.Multiply(verticalSpeed)
        );
        double surfaceSpeed = tangentialVelocity.Length();

        if (
            ship.flightStatus == FlightStatus.Flying &&
            dt > 0.0 &&
            surfaceSpeed > 0.0 &&
            !climbingUnderThrust
        )
        {
            double mass = ship.GetMass();

            if (mass > 0.0)
            {
                double frictionDeceleration =
                    LandingConfig.SurfaceFrictionCoefficient * localGravity;
                double speedReduction = Math.Min(
                    frictionDeceleration * dt,
                    surfaceSpeed
                );
                double newSurfaceSpeed = surfaceSpeed - speedReduction;
                const double surfaceRestSpeedEpsilon = 0.1;

                if (newSurfaceSpeed <= surfaceRestSpeedEpsilon)
                {
                    tangentialVelocity = SimVector3.Zero();
                    surfaceSpeed = 0.0;
                    verticalSpeed = 0.0;
                    ship.flightStatus = FlightStatus.Landed;
                    ship.landedOnBodyName = surfaceBody.name;
                }
                else
                {
                    double scale = newSurfaceSpeed / surfaceSpeed;
                    tangentialVelocity = tangentialVelocity.Multiply(scale);
                    surfaceSpeed = newSurfaceSpeed;
                }
            }
        }

        relativeVelocity = tangentialVelocity.Add(
            surfaceNormal.Multiply(verticalSpeed)
        );
        ship.velocity = surfaceBody.velocity.Add(relativeVelocity);
    }

    void TryResolveSurfaceContact()
    {
        Body surfaceBody = GetActivePrimaryBody();

        if (surfaceBody == null || surfaceBody.radius <= 0.0)
        {
            return;
        }

        double altitude = GetShipAltitudeAboveBody(surfaceBody);

        if (altitude > 0.0)
        {
            return;
        }

        SimVector3 surfaceNormal = GetSurfaceNormal(surfaceBody);
        PinShipToSurface(surfaceBody, surfaceNormal);

        SimVector3 relativeVelocity =
            ship.velocity.Subtract(surfaceBody.velocity);
        double verticalSpeed = SimVector3.Dot(relativeVelocity, surfaceNormal);
        SimVector3 surfaceVelocity = relativeVelocity.Subtract(
            surfaceNormal.Multiply(verticalSpeed)
        );
        double surfaceSpeed = surfaceVelocity.Length();
        double totalSpeed = relativeVelocity.Length();

        if (
            ship.CheckCrash(
                verticalSpeed,
                surfaceSpeed,
                LandingConfig.CrashSpeedThreshold
            ) ||
            totalSpeed > SoftLandingTotalSpeed
        )
        {
            ship.velocity = surfaceBody.velocity;
            ship.thrust = SimVector3.Zero();
            lastFlightEventMessage = "Crashed on " + surfaceBody.name;
            return;
        }

        ship.flightStatus = FlightStatus.Landed;
        ship.landedOnBodyName = surfaceBody.name;
        CaptureLandedSurfaceDirectionBodyFixed(surfaceBody, surfaceNormal);
        ship.velocity = GetLandedSurfaceVelocity(surfaceBody);
        lastFlightEventMessage = IsSplashdownSurface(surfaceBody, surfaceNormal)
            ? "Splashed down on " + surfaceBody.name
            : "Landed on " + surfaceBody.name;
    }

    bool IsSplashdownSurface(Body surfaceBody, SimVector3 surfaceNormal)
    {
        if (surfaceBody != planet || surfaceNormal.LengthSquared() <= 1e-24)
        {
            return false;
        }

        if (!PlanetElevationCatalog.TryGetElevationAtDirection(
            surfaceNormal,
            out double gebcoMeters
        ))
        {
            return false;
        }

        return gebcoMeters < PlanetSurfaceConstants.SeaLevelMeters;
    }

    void PinShipToSurface(Body surfaceBody)
    {
        PinShipToSurface(surfaceBody, GetSurfaceNormal(surfaceBody));
    }

    void PinShipToSurface(Body surfaceBody, SimVector3 surfaceNormal)
    {
        bool usePadDeckHeight = IsShipOnCoveLaunchPad();
        SimVector3 contactWorld;
        SimVector3 comWorld;

        if (
            usePadDeckHeight &&
            VesselStackGrounding.TryGetLowestStackBottomOffsetFromCom(
                ship,
                surfaceNormal,
                out SimVector3 bottomNodeOffsetFromCom
            )
        )
        {
            double contactDistance = LaunchPadConstants.LaunchpadRadiusElevation;
            contactWorld = surfaceBody.position.Add(surfaceNormal.Multiply(contactDistance));
            comWorld = contactWorld.Subtract(ship.orientation.Rotate(bottomNodeOffsetFromCom));
        }
        else
        {
            double bottomExtent = LaunchPadConstants.GetEffectiveBottomExtentMeters(
                ship.GetBottomExtentAlongWorldDirection(surfaceNormal)
            );
            double comDistance = usePadDeckHeight ?
                LaunchPadConstants.GetPadRestComDistance(bottomExtent) :
                surfaceBody.radius + bottomExtent + LandingConfig.SurfacePad;
            comWorld = surfaceBody.position.Add(surfaceNormal.Multiply(comDistance));
        }

        ship.position = comWorld.Subtract(
            ship.orientation.Rotate(ship.centerOfMassLocal)
        );
        ship.lastSurfaceNormal = surfaceNormal;
    }

    void ApplyDeterministicPadGroundingLock()
    {
        if (!SimulationScenarios.IsAnySurfacePad(scenario) || ship == null)
        {
            return;
        }

        if (ship.flightStatus != FlightStatus.Landed)
        {
            return;
        }

        Body primary = GetVisualTerrainBody() ?? GetActivePrimaryBody();

        if (primary == null)
        {
            return;
        }

        if (SimulationScenarios.IsMoonSurfacePad(scenario) && primary != moon)
        {
            return;
        }

        if (SimulationScenarios.IsPlanetSurfacePad(scenario) && primary != planet)
        {
            return;
        }

        EnsurePadLaunchBodyFixedDirection(primary);
        SyncLandedVesselToRotatingSurface(primary, ship);
    }

    public void RefreshLaunchPadGrounding()
    {
        ApplyDeterministicPadGroundingLock();
    }

    SimVector3 GetSurfaceNormal(Body surfaceBody)
    {
        SimVector3 offset = ship.GetCenterOfMassWorld().Subtract(surfaceBody.position);
        double distance = offset.Length();

        if (distance > 1e-9)
        {
            ship.lastSurfaceNormal = offset.Multiply(1.0 / distance);
            return ship.lastSurfaceNormal;
        }

        if (ship.lastSurfaceNormal.LengthSquared() > 1e-12)
        {
            return ship.lastSurfaceNormal;
        }

        ship.lastSurfaceNormal = GetDefaultLaunchSurfaceNormal();
        return ship.lastSurfaceNormal;
    }

    double GetShipAltitudeAboveBody(Body body)
    {
        return GetShipAltitudeAboveBody(body, ship.GetCenterOfMassWorld());
    }

    double GetShipAltitudeAboveBody(Body body, SimVector3 comWorld)
    {
        SimVector3 offsetFromBody = comWorld.Subtract(body.position);
        double distanceToCom = offsetFromBody.Length();

        if (distanceToCom <= 1e-9)
        {
            return -ship.collisionRadius;
        }

        SimVector3 outward = offsetFromBody.Multiply(1.0 / distanceToCom);
        double bottomExtent = LaunchPadConstants.GetEffectiveBottomExtentMeters(
            ship.GetBottomExtentAlongWorldDirection(outward)
        );
        double terrainHeight = 0.0;

        Body padBody = GetVisualTerrainBody();
        if (
            padBody != null &&
            body == padBody &&
            ScenarioUsesSurfaceLaunchPad() &&
            IsShipOnCoveLaunchPad()
        )
        {
            terrainHeight = LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters();
        }

        double altitude = distanceToCom - body.radius - terrainHeight - bottomExtent;

        if (double.IsNaN(altitude) || double.IsInfinity(altitude))
        {
            return 0.0;
        }

        return altitude;
    }

    double GetShipDistanceToBodyCenter(Body body)
    {
        if (body == null || ship == null)
        {
            return 0.0;
        }

        return body.position.Subtract(ship.GetCenterOfMassWorld()).Length();
    }

    public FlightStatus GetFlightStatus()
    {
        return ship.flightStatus;
    }

    public string GetLandedOnBodyName()
    {
        return ship.landedOnBodyName;
    }

    public string GetLastFlightEventMessage()
    {
        return lastFlightEventMessage;
    }

    public double GetShipVerticalSpeedRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return 0.0;
        }

        SimVector3 surfaceVelocity = GetVesselSurfaceVelocity(activePrimaryBody, ship);
        SimVector3 relativeVelocity = ship.velocity.Subtract(surfaceVelocity);
        SimVector3 surfaceNormal = GetSurfaceNormal(activePrimaryBody);
        return SimVector3.Dot(relativeVelocity, surfaceNormal);
    }

    public double GetShipSurfaceSpeedRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return 0.0;
        }

        SimVector3 surfaceVelocity = GetVesselSurfaceVelocity(activePrimaryBody, ship);
        SimVector3 relativeVelocity = ship.velocity.Subtract(surfaceVelocity);
        SimVector3 surfaceNormal = GetSurfaceNormal(activePrimaryBody);
        double verticalSpeed = SimVector3.Dot(relativeVelocity, surfaceNormal);
        return relativeVelocity
            .Subtract(surfaceNormal.Multiply(verticalSpeed))
            .Length();
    }

    public void CalculateNavBallAngles(
        Vessel vessel,
        out double pitch,
        out double heading,
        out double roll
    )
    {
        pitch = 0.0;
        heading = 0.0;
        roll = 0.0;

        if (vessel == null)
        {
            return;
        }

        Body primary = GetActivePrimaryBody();
        SimVector3 up;

        if (primary == null)
        {
            up = new SimVector3(0.0, 1.0, 0.0);
        }
        else
        {
            SimVector3 offset = vessel.position.Subtract(primary.position);
            up = offset.LengthSquared() > 1e-18 ?
                offset.Normalize() :
                (
                    vessel.lastSurfaceNormal.LengthSquared() > 1e-12 ?
                        vessel.lastSurfaceNormal.Normalize() :
                        new SimVector3(0.0, 1.0, 0.0)
                );
        }

        SimVector3 east = LandingConfig.GetSurfaceEastTangent(up);
        SimVector3 north = SimVector3.Cross(east, up);

        if (north.LengthSquared() < 1e-18)
        {
            north = SimVector3.UnitZ().Subtract(
                up.Multiply(SimVector3.Dot(SimVector3.UnitZ(), up))
            );
        }

        north = north.Normalize();
        east = east.LengthSquared() > 1e-18 ? east.Normalize() : east;

        // Y-up VAB: nose / thrust is +Y, not Unity +Z (engines / pad-east).
        SimVector3 nose = vessel.orientation.Up();

        if (nose.LengthSquared() <= 1e-18)
        {
            nose = vessel.GetForwardDirection();
        }

        nose = nose.Normalize();
        SimVector3 rollReference = vessel.GetForwardDirection().Normalize();

        double noseUp = Math.Clamp(SimVector3.Dot(nose, up), -1.0, 1.0);
        pitch = Math.Asin(noseUp) * (180.0 / Math.PI);

        SimVector3 noseHoriz = nose.Subtract(up.Multiply(noseUp));
        double horizLen = noseHoriz.Length();

        if (horizLen > 1e-12)
        {
            noseHoriz = noseHoriz.Multiply(1.0 / horizLen);
            heading = Math.Atan2(
                SimVector3.Dot(noseHoriz, east),
                SimVector3.Dot(noseHoriz, north)
            ) * (180.0 / Math.PI);

            if (heading < 0.0)
            {
                heading += 360.0;
            }
        }

        SimVector3 upPerp = up.Subtract(nose.Multiply(SimVector3.Dot(up, nose)));
        double upPerpLen = upPerp.Length();

        if (upPerpLen > 1e-12)
        {
            upPerp = upPerp.Multiply(1.0 / upPerpLen);
            SimVector3 right = SimVector3.Cross(nose, upPerp).Normalize();
            roll = Math.Atan2(
                SimVector3.Dot(rollReference, right),
                SimVector3.Dot(rollReference, upPerp)
            ) * (180.0 / Math.PI);
        }
    }

    public double GetShipGlideRatio()
    {
        if (lastLiftCoefficient <= 0.0 || lastDragCoefficient <= 0.0)
        {
            return 0.0;
        }

        return lastLiftCoefficient / lastDragCoefficient;
    }

    public double GetShipReentryIntensity()
    {
        return ship.reentryIntensity;
    }

    public bool TryGetDescentImpactPoint(out SimVector3 worldPosition)
    {
        return preview.TryGetDescentImpactPoint(out worldPosition);
    }

    public void RespawnActiveVessel(string launchShipPath = null)
    {
        ExitKeplerRails();
        RemoveAllDebrisVessels();
        RestoreActiveVesselConfiguration(launchShipPath);
        ApplyInitialShipPose();
        ResetActiveVesselTransientState();
        ship.RecalculateMassAndDeltaV(
            GetShipLocalGravityRelativeToActivePrimary()
        );

        if (ship.flightStatus == FlightStatus.Landed)
        {
            FinalizeLandedShipOnSurface();
        }
        else
        {
            EnforceSphereCollisionWithPrimary();
        }

        lastFlightEventMessage = "Respawned";
    }

    public void RespawnVessel()
    {
        RespawnActiveVessel();
    }

    void RemoveAllDebrisVessels()
    {
        if (debrisVessels == null || debrisVessels.Count == 0)
        {
            return;
        }

        for (int i = 0; i < debrisVessels.Count; i++)
        {
            Vessel debris = debrisVessels[i];

            if (debris == null)
            {
                continue;
            }

            debris.isOnKeplerRails = false;
            debris.keplerRails = KeplerRailsState.Invalid();
        }

        debrisVessels.Clear();
    }

    void RestoreActiveVesselConfiguration(string launchShipPath)
    {
        bool shouldReloadLaunchShip =
            !string.IsNullOrWhiteSpace(launchShipPath) &&
            SimulationScenarios.IsAnySurfacePad(scenario);

        if (shouldReloadLaunchShip)
        {
            try
            {
                ConfigureShipStages(LoadShip(launchShipPath));
                return;
            }
            catch
            {
            }
        }

        ship.stages = Vessel.CopyStages(initialShipSnapshot.stages);
        ship.activeStageIndex = initialShipSnapshot.activeStageIndex;
        ship.NotifyShipLayoutChanged();
    }

    void ApplyInitialShipPose()
    {
        ship.position = initialShipSnapshot.position;
        ship.velocity = initialShipSnapshot.velocity;
        ship.SetHeadingRadians(initialShipSnapshot.heading);
        ship.flightStatus = initialShipSnapshot.flightStatus;
        ship.landedOnBodyName = initialShipSnapshot.landedOnBodyName;
    }

    void ResetActiveVesselTransientState()
    {
        ship.thrust = SimVector3.Zero();
        ship.thrustAcceleration = SimVector3.Zero();
        ship.dragAcceleration = SimVector3.Zero();
        ship.acceleration = SimVector3.Zero();
        ship.rcsAcceleration = SimVector3.Zero();
        ship.angularVelocity = SimVector3.Zero();
        ship.reentryIntensity = 0.0;
        ship.rcsFuel = 5.0;
        ship.isOnKeplerRails = false;
        ship.keplerRails = KeplerRailsState.Invalid();
        ship.IsDocked = false;
        ship.hasDockedPartnerVisual = false;
        ship.dockedPartnerName = string.Empty;
        ship.dockedPartnerLocalOffset = SimVector3.Zero();
        ship.dockedPartnerRelativeHeading = 0.0;
    }

    ShipRespawnSnapshot CaptureShipRespawnSnapshot()
    {
        return new ShipRespawnSnapshot
        {
            position = ship.position,
            velocity = ship.velocity,
            heading = ship.heading,
            flightStatus = ship.flightStatus,
            landedOnBodyName = ship.landedOnBodyName,
            activeStageIndex = ship.activeStageIndex,
            stages = Vessel.CopyStages(ship.stages),
        };
    }

    void UpdateReentryIntensity(double relativeSpeed, double dynamicPressure)
    {
        const double criticalDynamicPressure = 80000.0;
        const double criticalMach = 2.5;

        if (dynamicPressure <= 0.0 || relativeSpeed <= 0.0)
        {
            ship.reentryIntensity = 0.0;
            return;
        }

        double mach = relativeSpeed / LandingConfig.SeaLevelSpeedOfSound;
        double intensity =
            (dynamicPressure / criticalDynamicPressure) *
            (mach / criticalMach);
        ship.reentryIntensity = Math.Min(1.0, Math.Max(0.0, intensity));
    }

    public double GetShipRelativeSpeedToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return ship.velocity.Length();
        }

        return ship.velocity
            .Subtract(activePrimaryBody.velocity)
            .Length();
    }

    public double GetShipLocalGravityRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return 0.0;
        }

        double distance = GetShipDistanceToBodyCenter(activePrimaryBody);

        if (distance <= 0.0 || activePrimaryBody.mu <= 0.0)
        {
            return 0.0;
        }

        return activePrimaryBody.mu / (distance * distance);
    }

    public double GetShipWeightRelativeToActivePrimary()
    {
        double mass = ship.GetMass();

        if (mass <= 0.0)
        {
            return 0.0;
        }

        return mass * GetShipLocalGravityRelativeToActivePrimary();
    }

    public double GetShipAppliedThrustForce()
    {
        return ship.thrust.Length();
    }

    public double GetShipThrustToWeightRatio()
    {
        double weight = GetShipWeightRelativeToActivePrimary();

        if (weight <= 0.0)
        {
            return 0.0;
        }

        return GetShipAppliedThrustForce() / weight;
    }

    public double GetShipMaxThrustToWeightRatio()
    {
        double weight = GetShipWeightRelativeToActivePrimary();

        if (weight <= 0.0)
        {
            return 0.0;
        }

        return ship.GetActiveStageThrust() / weight;
    }

    static string GetFlightStatusName(FlightStatus status)
    {
        switch (status)
        {
            case FlightStatus.Landed:
                return "Landed";
            case FlightStatus.Crashed:
                return "Crashed";
            default:
                return "Flying";
        }
    }

    public string GetFlightStatusName()
    {
        return GetFlightStatusName(ship.flightStatus);
    }

    // CalculateAccelerations = вычислить ускорения для всех тел.
    SimVector3[] CalculateAccelerations()
    {
        SimVector3[] accelerations = new SimVector3[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            accelerations[i] = CalculateBodyAcceleration(bodies[i]);
        }

        return accelerations;
    }

    SimVector3 CalculateBodyAcceleration(Body body)
    {
        if (body.isStatic)
        {
            return SimVector3.Zero();
        }

        if (body == planet && ScenarioHasSun())
        {
            return gravityModel.CalculateAcceleration(
                planet.position,
                sun.position,
                sun.mu
            );
        }

        if (body == ship)
        {
            Body activePrimaryBody = GetActivePrimaryBody();

            if (activePrimaryBody == null)
            {
                return SimVector3.Zero();
            }

            SimVector3 localGravityAcceleration = gravityModel.CalculateAcceleration(
                ship.position,
                activePrimaryBody.position,
                activePrimaryBody.mu
            );

            if (ScenarioHasSun())
            {
                // Patched conics: the ship is integrated relative to the SoI parent.
                // Adding the parent's heliocentric acceleration here double-counts the sun.
                return localGravityAcceleration;
            }

            return localGravityAcceleration.Add(
                CalculatePrimaryFrameAcceleration(activePrimaryBody)
            );
        }

        if (body == moon)
        {
            return gravityModel.CalculateAcceleration(
                moon.position,
                planet.position,
                planet.mu
            );
        }

        return SimVector3.Zero();
    }

    SimVector3 GetBodyAcceleration(Body body)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == body)
            {
                return bodies[i].acceleration;
            }
        }

        return SimVector3.Zero();
    }

    // UpdateOrbitStats = обновить статистику орбиты корабля.
    SimVector3 CalculatePrimaryFrameAcceleration(Body primaryBody)
    {
        if (primaryBody == moon)
        {
            return gravityModel.CalculateAcceleration(
                moon.position,
                planet.position,
                planet.mu
            );
        }

        if (primaryBody == planet && ScenarioHasSun())
        {
            return gravityModel.CalculateAcceleration(
                planet.position,
                sun.position,
                sun.mu
            );
        }

        return SimVector3.Zero();
    }

    void UpdateOrbitStats()
    {
        double distance = GetShipDistanceToPlanet();

        if (distance < minShipDistanceToPlanet)
        {
            minShipDistanceToPlanet = distance;
        }

        if (distance > maxShipDistanceToPlanet)
        {
            maxShipDistanceToPlanet = distance;
        }
    }

    // CalculateSphereOfInfluenceRadius = вычислить радиус сферы влияния меньшего тела вокруг большего.
    double CalculateSphereOfInfluenceRadius(
        double orbitalRadiusAroundPrimary,
        double secondaryMu,
        double primaryMu
    )
    {
        if (primaryMu == 0)
        {
            return 0;
        }

        return orbitalRadiusAroundPrimary * Math.Pow(secondaryMu / primaryMu, 0.4);
    }

    // GetActivePrimaryBody = получить текущее главное тело для орбитальной аналитики.
    internal Body GetActivePrimaryBody()
    {
        return ResolveActivePrimaryBody();
    }

    Body ResolveActivePrimaryBody()
    {
        if (ScenarioIsVacuumDocking())
        {
            return null;
        }

        Body previousPrimary = GetBodyByName(lastActivePrimaryForHysteresis);

        if (previousPrimary == null)
        {
            previousPrimary = planet;
        }

        if (ScenarioHasMoon() && moon != null)
        {
            double distanceToMoon = GetShipDistanceToMoon();
            double moonExitRadius =
                moonSphereOfInfluenceRadius * SphereOfInfluenceExitMultiplier;

            if (previousPrimary == moon)
            {
                if (distanceToMoon <= moonExitRadius)
                {
                    return moon;
                }
            }
            else if (distanceToMoon <= moonSphereOfInfluenceRadius)
            {
                return moon;
            }
        }

        double distanceToPlanet = GetShipDistanceToPlanet();

        if (ScenarioHasSun())
        {
            double planetExitRadius =
                planetSphereOfInfluenceRadius * SphereOfInfluenceExitMultiplier;

            if (previousPrimary == sun)
            {
                if (distanceToPlanet > planetSphereOfInfluenceRadius)
                {
                    return sun;
                }
            }
            else if (previousPrimary == planet)
            {
                if (distanceToPlanet <= planetExitRadius)
                {
                    return planet;
                }

                return sun;
            }
            else if (distanceToPlanet <= planetSphereOfInfluenceRadius)
            {
                return planet;
            }

            return sun;
        }

        return planet;
    }

    // GetShipDistanceToActivePrimary = получить расстояние от корабля до текущего главного тела.
    public double GetShipDistanceToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null || ship == null)
        {
            return 0.0;
        }

        return activePrimaryBody.position.Subtract(ship.position).Length();
    }

    // GetShipAltitudeAboveActivePrimary = получить высоту корабля над поверхностью текущего главного тела.
    public double GetShipAltitudeAboveActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return 0.0;
        }

        return GetShipAltitudeAboveBody(activePrimaryBody);
    }

    // GetShipAltitudeAbovePlanet = высота над визуальным глобусом (Земля на мысе, Луна на лунном паде).
    public double GetShipAltitudeAbovePlanet()
    {
        return GetShipAltitudeAboveVisualTerrainBody();
    }

    // GetActivePrimaryPosition = получить позицию текущего главного тела для визуализации.
    public SimVector3 GetActivePrimaryPosition()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return SimVector3.Zero();
        }

        return activePrimaryBody.position;
    }

    // GetActivePrimaryRadius = получить радиус текущего главного тела.
    public double GetActivePrimaryRadius()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return 0.0;
        }

        return activePrimaryBody.radius;
    }

    // GetActivePrimarySphereOfInfluenceRadius = получить радиус текущей сферы влияния для активного тела.
    public double GetActivePrimarySphereOfInfluenceRadius()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (
            activePrimaryBody == null ||
            IsRootBody(activePrimaryBody) ||
            !TryGetFiniteSphereOfInfluenceRadius(
                activePrimaryBody,
                out double radiusMeters
            )
        )
        {
            return double.PositiveInfinity;
        }

        return radiusMeters;
    }

    // GetShipSpeed = получить скорость корабля (модуль мировой скорости).
    public double GetShipSpeed()
    {
        return ship.velocity.Length();
    }

    public enum ShipHudSpeedMode
    {
        Surface,
        Orbit,
    }

    public ShipHudSpeedMode GetShipHudSpeedMode()
    {
        if (
            GetShipAltitudeAboveActivePrimary() <
            CelestialBody.Earth.HudOrbitSpeedModeAltitude
        )
        {
            return ShipHudSpeedMode.Surface;
        }

        return ShipHudSpeedMode.Orbit;
    }

    public string GetShipHudSpeedModeLabel()
    {
        return GetShipHudSpeedMode() == ShipHudSpeedMode.Surface ?
            "Surface" :
            "Orbital speed";
    }

    public double GetShipHudSpeed()
    {
        Body primary = GetActivePrimaryBody();

        if (primary == null)
        {
            return GetShipSpeed();
        }

        SimVector3 relativeVelocity =
            ship.velocity.Subtract(primary.velocity);

        if (GetShipHudSpeedMode() == ShipHudSpeedMode.Surface)
        {
            SimVector3 surfaceVelocity = primary.GetSurfaceLinearVelocity(
                ship.GetCenterOfMassWorld()
            );
            return relativeVelocity.Subtract(surfaceVelocity).Length();
        }

        return relativeVelocity.Length();
    }

    // GetShipAcceleration = получить текущее полное ускорение корабля.
    public double GetShipAcceleration()
    {
        return ship.acceleration
            .Add(ship.thrustAcceleration)
            .Add(ship.dragAcceleration)
            .Length();
    }

    public double GetShipDragAcceleration()
    {
        return ship.dragAcceleration.Length();
    }

    public bool IsShipInAtmosphere()
    {
        return lastAtmosphericDensity > 1e-15;
    }

    public double GetShipAtmosphereDepthFraction()
    {
        Body atmosphereBody = GetAtmosphereBody();

        if (
            atmosphereBody == null ||
            atmosphereBody.atmosphereProfile.topAltitude <= 0.0
        )
        {
            return 0.0;
        }

        double altitude = GetShipAltitudeAboveBody(atmosphereBody);
        double depth = 1.0 - altitude / atmosphereBody.atmosphereProfile.topAltitude;

        if (depth < 0.0)
        {
            return 0.0;
        }

        if (depth > 1.0)
        {
            return 1.0;
        }

        return depth;
    }

    public double GetRecommendedMaxTimeScale()
    {
        double maxScale = double.PositiveInfinity;

        if (IsThrustTimeWarpRestricted())
        {
            maxScale = GetMaxAllowedTimeScaleWhileThrusting();
        }
        else if (ship.isOnKeplerRails)
        {
            return double.PositiveInfinity;
        }

        if (!IsShipInAtmosphere())
        {
            return maxScale;
        }

        double atmosphereMaxScale = double.PositiveInfinity;
        double depth = GetShipAtmosphereDepthFraction();

        if (depth >= 0.5)
        {
            atmosphereMaxScale = 1.0;
        }
        else if (depth >= 0.1)
        {
            atmosphereMaxScale = 10.0;
        }
        else
        {
            atmosphereMaxScale = 50.0;
        }

        return Math.Min(maxScale, atmosphereMaxScale);
    }

    public double GetShipAtmosphericDensity()
    {
        return lastAtmosphericDensity;
    }

    public double GetShipDragForce()
    {
        return lastDragForce;
    }

    public double GetShipEffectiveDragArea()
    {
        return lastEffectiveDragArea;
    }

    public double GetShipDynamicPressure()
    {
        return lastDynamicPressure;
    }

    public double GetShipAngleOfAttackDegrees()
    {
        return lastAngleOfAttack * 180.0 / Math.PI;
    }

    public double GetShipLiftForce()
    {
        return lastLiftForce;
    }

    public bool IsShipHeatingWarningActive()
    {
        return lastDynamicPressure > 25000.0;
    }

    public bool IsShipHeatingCritical()
    {
        return lastDynamicPressure > 80000.0;
    }

    public double GetShipHeatingLevel()
    {
        const double warningPressure = 25000.0;
        const double criticalPressure = 80000.0;

        if (lastDynamicPressure <= warningPressure)
        {
            return 0.0;
        }

        if (lastDynamicPressure >= criticalPressure)
        {
            return 1.0;
        }

        return (lastDynamicPressure - warningPressure) /
            (criticalPressure - warningPressure);
    }

    public bool ShouldShowDescentPreview()
    {
        return preview.ShouldShowDescentPreview();
    }

    public SimVector3[] SampleDescentPreviewWorldPath(
        bool useAtmosphericSas,
        double maxDuration,
        double stepDt,
        int maxPoints
    )
    {
        return preview.SampleDescentPreviewWorldPath(
            useAtmosphericSas,
            maxDuration,
            stepDt,
            maxPoints
        );
    }

    public SimVector3[] GetDescentPreviewPathRelativeToPlanet(int maxPoints)
    {
        return preview.GetDescentPreviewPathRelativeToPlanet(maxPoints);
    }

    public double GetShipThrustAcceleration()
    {
        return ship.thrustAcceleration.Length();
    }

    public double GetShipMass()
    {
        return ship.GetMass();
    }

    public double GetShipFuelMass()
    {
        return ship.GetTotalFuelMass();
    }

    public double GetShipFuelRatio()
    {
        return ship.GetFuelRatio();
    }

    public string GetLastStagingMessage()
    {
        return lastStagingMessage;
    }

    // GetMoonSpeed = получить скорость луны.
    public double GetMoonSpeed()
    {
        if (!ScenarioHasMoon())
        {
            return 0;
        }

        return moon.velocity.Length();
    }

    // GetShipDistanceToPlanet = получить расстояние от корабля до планеты.
    public double GetShipDistanceToPlanet()
    {
        if (planet == null)
        {
            return 0.0;
        }

        return planet.position.Subtract(ship.position).Length();
    }

    public double GetShipDistanceToPlanetCenter()
    {
        if (planet == null || ship == null)
        {
            return 0.0;
        }

        return GetShipDistanceToBodyCenter(planet);
    }

    // GetShipDistanceToMoon = получить расстояние от корабля до луны.
    public double GetShipDistanceToMoon()
    {
        if (!ScenarioHasMoon())
        {
            return 0;
        }

        return moon.position.Subtract(ship.position).Length();
    }

    public bool HasBody(string bodyName)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].name == bodyName)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsVesselBodyName(string bodyName)
    {
        if (string.IsNullOrEmpty(bodyName))
        {
            return false;
        }

        if (ship != null && ship.name == bodyName)
        {
            return true;
        }

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            if (dockableVessels[i].name == bodyName)
            {
                return true;
            }
        }

        return false;
    }

    public double GetBodyRadius(string bodyName)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].name == bodyName)
            {
                return bodies[i].radius;
            }
        }

        return 0.0;
    }

    internal Body GetBodyByName(string bodyName)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].name == bodyName)
            {
                return bodies[i];
            }
        }

        return ship;
    }

    public double GetShipDistanceToBody(string bodyName)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].name == bodyName)
            {
                return bodies[i].position.Subtract(ship.position).Length();
            }
        }

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            Vessel dockable = dockableVessels[i];

            if (dockable.name == bodyName)
            {
                return dockable
                    .GetCenterOfMassWorld()
                    .Subtract(ship.GetCenterOfMassWorld())
                    .Length();
            }
        }

        return double.PositiveInfinity;
    }

    public SimVector3 GetShipDirectionToBody(string bodyName)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].name == bodyName)
            {
                return bodies[i].position.Subtract(ship.position).Normalize();
            }
        }

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            Vessel dockable = dockableVessels[i];

            if (dockable.name == bodyName)
            {
                SimVector3 direction = dockable
                    .GetCenterOfMassWorld()
                    .Subtract(ship.GetCenterOfMassWorld());

                if (direction.LengthSquared() <= 1e-12)
                {
                    return SimVector3.Zero();
                }

                return direction.Normalize();
            }
        }

        return SimVector3.Zero();
    }

    public SimVector3 GetShipRelativeVelocityToBody(string bodyName)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            Body body = bodies[i];

            if (body.name == bodyName)
            {
                return ship.velocity.Subtract(
                    GetBodyReferenceVelocityAtShip(body)
                );
            }
        }

        for (int i = 0; i < dockableVessels.Count; i++)
        {
            Vessel dockable = dockableVessels[i];

            if (dockable.name == bodyName)
            {
                return ship.velocity.Subtract(dockable.velocity);
            }
        }

        return SimVector3.Zero();
    }

    SimVector3 GetBodyReferenceVelocityAtShip(Body body)
    {
        if (body == null)
        {
            return SimVector3.Zero();
        }

        if (Math.Abs(body.spinRadiansPerSecond) > 1e-12)
        {
            return body.GetAbsoluteSurfaceVelocityAt(ship.GetCenterOfMassWorld());
        }

        return body.velocity;
    }

    public double GetShipRelativeSpeedToBody(string bodyName)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            Body body = bodies[i];

            if (body.name == bodyName)
            {
                return ship.velocity
                    .Subtract(GetBodyReferenceVelocityAtShip(body))
                    .Length();
            }
        }

        return double.PositiveInfinity;
    }

    // GetMoonDistanceToPlanet = получить расстояние от луны до планеты.
    public double GetMoonDistanceToPlanet()
    {
        if (!ScenarioHasMoon())
        {
            return 0;
        }

        return planet.position.Subtract(moon.position).Length();
    }

    // GetShipSpecificOrbitalEnergyRelativeToPlanet = получить удельную орбитальную энергию корабля относительно планеты.
    public double GetShipSpecificOrbitalEnergyRelativeToPlanet()
    {
        if (planet == null)
        {
            return 0.0;
        }

        return GetShipSpecificOrbitalEnergyRelativeToBody(planet);
    }

    // GetShipAngularMomentumRelativeToPlanet = получить угловой момент корабля относительно планеты.
    public double GetShipAngularMomentumRelativeToPlanet()
    {
        if (planet == null)
        {
            return 0.0;
        }

        return GetShipAngularMomentumRelativeToBody(planet);
    }

    // GetShipOrbitalEccentricityRelativeToPlanet = получить эксцентриситет орбиты корабля относительно планеты.
    public double GetShipOrbitalEccentricityRelativeToPlanet()
    {
        return GetShipOrbitalEccentricityRelativeToBody(planet);
    }

    // GetShipSpecificOrbitalEnergyRelativeToActivePrimary = получить удельную орбитальную энергию корабля относительно текущего главного тела.
    public double GetShipSpecificOrbitalEnergyRelativeToActivePrimary()
    {
        return GetShipSpecificOrbitalEnergyRelativeToBody(GetActivePrimaryBody());
    }

    // GetShipOrbitTypeRelativeToPlanet = получить тип орбиты корабля относительно планеты.
    public string GetShipOrbitTypeRelativeToPlanet()
    {
        return GetShipOrbitTypeRelativeToBody(planet);
    }

    // GetShipOrbitTypeRelativeToActivePrimary = получить тип орбиты корабля относительно текущего главного тела.
    public string GetShipOrbitTypeRelativeToActivePrimary()
    {
        return GetShipOrbitTypeRelativeToBody(GetActivePrimaryBody());
    }

    // GetShipEscapeSpeedRelativeToPlanet = получить вторую космическую скорость относительно планеты.
    public double GetShipEscapeSpeedRelativeToPlanet()
    {
        return GetShipEscapeSpeedRelativeToBody(planet);
    }

    // GetShipEscapeSpeedRelativeToActivePrimary = получить вторую космическую скорость относительно текущего главного тела.
    public double GetShipEscapeSpeedRelativeToActivePrimary()
    {
        return GetShipEscapeSpeedRelativeToBody(GetActivePrimaryBody());
    }

      // GetShipProgradeDirectionRelativeToActivePrimary = получить направление по скорости относительно текущего главного тела.
    public SimVector3 GetShipProgradeDirectionRelativeToActivePrimary()
    {
        SimVector3 orbitVelocity = GetVesselOrbitVelocityForOrientation(ship);

        return orbitVelocity.LengthSquared() > 1e-12 ?
            orbitVelocity.Normalize() :
            SimVector3.Zero();
    }

    public SimVector3 GetShipProgradeDirectionForAtmosphere()
    {
        Body atmosphereBody = GetAtmosphereBody();

        if (atmosphereBody != null)
        {
            SimVector3 relativeVelocity =
                ship.velocity.Subtract(atmosphereBody.velocity);

            if (relativeVelocity.LengthSquared() > 1e-12)
            {
                return relativeVelocity.Normalize();
            }
        }

        return GetShipProgradeDirectionRelativeToActivePrimary();
    }

    public SimVector3 GetShipRetrogradeDirectionForAtmosphere()
    {
        return GetShipProgradeDirectionForAtmosphere().Multiply(-1);
    }

    // GetShipRetrogradeDirectionRelativeToActivePrimary = получить направление против скорости относительно текущего главного тела.
    public SimVector3 GetShipRetrogradeDirectionRelativeToActivePrimary()
    {
        return GetShipProgradeDirectionRelativeToActivePrimary().Multiply(-1);
    }

    public SimVector3 GetShipProgradeDirectionRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        Simulation copy = CreateCopy();
        copy.ship.thrust = SimVector3.Zero();
        preview.AdvancePreviewToTime(copy, time, dt);
        return copy.GetShipProgradeDirectionRelativeToActivePrimary();
    }

    public SimVector3 GetShipRetrogradeDirectionRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        return GetShipProgradeDirectionRelativeToActivePrimaryAtTime(
            time,
            dt
        ).Multiply(-1);
    }

    public SimVector3 GetShipCircularizeDeltaVRelativeToActivePrimary()
    {
        return GetShipCircularizeDeltaVRelativeToBody(GetActivePrimaryBody());
    }

    internal SimVector3 GetShipCircularizeDeltaVRelativeToBody(Body activePrimaryBody)
    {
        if (activePrimaryBody == null)
        {
            return SimVector3.Zero();
        }

        SimVector3 relativePosition = ship.position.Subtract(activePrimaryBody.position);
        SimVector3 relativeVelocity = ship.velocity.Subtract(activePrimaryBody.velocity);

        return OrbitalMechanics.GetCircularizeDeltaV(
            relativePosition,
            relativeVelocity,
            activePrimaryBody.mu,
            gravityModel
        );
    }

    public SimVector3 GetShipCircularizeDeltaVRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        Simulation copy = CreateCopy();
        copy.ship.thrust = SimVector3.Zero();
        preview.AdvancePreviewToTime(copy, time, dt);
        return copy.GetShipCircularizeDeltaVRelativeToActivePrimary();
    }

    public SimVector3 GetShipRadialPlusDirectionRelativeToActivePrimary()
    {
        SimVector3 prograde = GetShipProgradeDirectionRelativeToActivePrimary();
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return SimVector3.Zero();
        }

        SimVector3 relativePosition = ship.position.Subtract(activePrimaryBody.position);
        SimVector3 relativeVelocity = ship.velocity.Subtract(activePrimaryBody.velocity);
        SimVector3 orbitNormal = SimVector3.Cross(relativePosition, relativeVelocity);

        if (orbitNormal.LengthSquared() <= 1e-12)
        {
            return SimVector3.Zero();
        }

        return SimVector3.Cross(orbitNormal.Normalize(), prograde).Normalize();
    }

    public SimVector3 GetShipRadialMinusDirectionRelativeToActivePrimary()
    {
        return GetShipRadialPlusDirectionRelativeToActivePrimary().Multiply(-1);
    }

    public SimVector3 GetShipTargetAlignmentDirection()
    {
        if (targetVessel == null)
        {
            return SimVector3.Zero();
        }

        SimVector3 sourcePortToTargetPort = targetVessel
            .GetDockingPortPosition()
            .Subtract(ship.GetDockingPortPosition());

        if (sourcePortToTargetPort.LengthSquared() <= 1e-12)
        {
            return SimVector3.Zero();
        }

        return sourcePortToTargetPort.Normalize();
    }

    // GetShipRadialOutDirectionRelativeToActivePrimary = получить направление от текущего главного тела наружу.
    public SimVector3 GetShipRadialOutDirectionRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return SimVector3.Zero();
        }

        return GetVesselSurfaceUp(ship).Normalize();
    }

    /// <summary>Great-circle distance from the ship to the Cape launch site (sim meters).</summary>
    public double GetShipSurfaceDistanceFromLaunchSiteMeters()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return double.PositiveInfinity;
        }

        SimVector3 shipOut = GetShipRadialOutDirectionRelativeToActivePrimary();

        if (shipOut.LengthSquared() <= 1e-12)
        {
            return double.PositiveInfinity;
        }

        SimVector3 launchOut = activePrimaryBody
            .RotateBodyFixedToWorld(LandingConfig.GetSurfaceLaunchPadNormal())
            .Normalize();
        double dot = SimVector3.Dot(shipOut, launchOut);
        dot = Math.Clamp(dot, -1.0, 1.0);
        double angle = Math.Acos(dot);
        return angle * activePrimaryBody.radius;
    }

    // GetShipRadialInDirectionRelativeToActivePrimary = получить направление к текущему главному телу.
    public SimVector3 GetShipRadialInDirectionRelativeToActivePrimary()
    {
        return GetShipRadialOutDirectionRelativeToActivePrimary().Multiply(-1);
    }

    public SimVector3 GetShipRadialOutDirectionRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        Simulation copy = CreateCopy();
        copy.ship.thrust = SimVector3.Zero();
        preview.AdvancePreviewToTime(copy, time, dt);
        return copy.GetShipRadialOutDirectionRelativeToActivePrimary();
    }

    public SimVector3 GetShipRadialInDirectionRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        return GetShipRadialOutDirectionRelativeToActivePrimaryAtTime(
            time,
            dt
        ).Multiply(-1);
    }

    // IsShipBoundToPlanet = проверить, связана ли орбита корабля с планетой.
    public bool IsShipBoundToPlanet()
    {
        return GetShipSpecificOrbitalEnergyRelativeToPlanet() < 0;
    }

    // IsShipBoundToActivePrimary = проверить, связана ли орбита корабля с текущим главным телом.
    public bool IsShipBoundToActivePrimary()
    {
        return GetShipSpecificOrbitalEnergyRelativeToActivePrimary() < 0;
    }

    // GetShipAngularMomentumRelativeToActivePrimary = получить угловой момент корабля относительно текущего главного тела.
    public double GetShipAngularMomentumRelativeToActivePrimary()
    {
        return GetShipAngularMomentumRelativeToBody(GetActivePrimaryBody());
    }

    // GetShipOrbitalEccentricityRelativeToActivePrimary = получить эксцентриситет орбиты корабля относительно текущего главного тела.
    public double GetShipOrbitalEccentricityRelativeToActivePrimary()
    {
        return GetShipOrbitalEccentricityRelativeToBody(GetActivePrimaryBody());
    }

    double GetShipOrbitalEccentricityRelativeToBody(Body body)
    {
        return GetShipOrbitalEccentricityRelativeToBody(body, ship.velocity);
    }

    double GetShipOrbitalEccentricityRelativeToBody(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return 0.0;
        }

        return OrbitalMechanics.GetOrbitalEccentricity(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity),
            body.mu
        );
    }

    // GetShipSemiMajorAxisRelativeToPlanet = получить большую полуось орбиты корабля относительно планеты.
    public double GetShipSemiMajorAxisRelativeToPlanet()
    {
        return GetShipSemiMajorAxisRelativeToBody(planet);
    }

    // GetShipSemiMajorAxisRelativeToActivePrimary = получить большую полуось орбиты корабля относительно текущего главного тела.
    public double GetShipSemiMajorAxisRelativeToActivePrimary()
    {
        return GetShipSemiMajorAxisRelativeToBody(GetActivePrimaryBody());
    }

    double GetShipSemiMajorAxisRelativeToBody(Body body)
    {
        return GetShipSemiMajorAxisRelativeToBody(body, ship.velocity);
    }

    double GetShipSemiMajorAxisRelativeToBody(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return 0.0;
        }

        return OrbitalMechanics.GetSemiMajorAxis(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity),
            body.mu
        );
    }

    // GetShipPeriapsisRelativeToPlanet = получить перицентр орбиты корабля относительно планеты.
    public double GetShipPeriapsisRelativeToPlanet()
    {
        return GetShipPeriapsisRelativeToBody(planet);
    }

    // GetShipPeriapsisRelativeToActivePrimary = получить перицентр орбиты корабля относительно текущего главного тела.
    public double GetShipPeriapsisRelativeToActivePrimary()
    {
        return GetShipPeriapsisRelativeToBody(GetActivePrimaryBody());
    }

    public double GetShipPeriapsisAltitudeRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return double.PositiveInfinity;
        }

        return GetShipPeriapsisRelativeToBody(activePrimaryBody) - activePrimaryBody.radius;
    }

    public double GetShipPeriapsisAltitudeRelativeToPlanet()
    {
        if (planet == null)
        {
            return double.PositiveInfinity;
        }

        return GetShipPeriapsisRelativeToBody(planet) - planet.radius;
    }

    double GetShipPeriapsisRelativeToBody(Body body)
    {
        return GetShipPeriapsisRelativeToBody(body, ship.velocity);
    }

    double GetShipPeriapsisRelativeToBody(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return double.PositiveInfinity;
        }

        return OrbitalMechanics.GetPeriapsisRadius(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity),
            body.mu
        );
    }

    // GetShipApoapsisRelativeToPlanet = получить апоцентр орбиты корабля относительно планеты.
    public double GetShipApoapsisRelativeToPlanet()
    {
        return GetShipApoapsisRelativeToBody(planet);
    }

    // GetShipApoapsisRelativeToActivePrimary = получить апоцентр орбиты корабля относительно текущего главного тела.
    public double GetShipApoapsisRelativeToActivePrimary()
    {
        return GetShipApoapsisRelativeToBody(GetActivePrimaryBody());
    }

    public double GetShipApoapsisAltitudeRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return double.PositiveInfinity;
        }

        double apoapsis = GetShipApoapsisRelativeToBody(activePrimaryBody);

        if (double.IsInfinity(apoapsis))
        {
            return double.PositiveInfinity;
        }

        return apoapsis - activePrimaryBody.radius;
    }

    // GetPreviewShipOrbitTypeRelativeToActivePrimary = получить тип орбиты корабля после запланированного delta-v.
    public string GetPreviewShipOrbitTypeRelativeToActivePrimary(SimVector3 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        SimVector3 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipOrbitTypeRelativeToBody(activePrimaryBody, previewVelocity);
    }

    // GetPreviewShipOrbitalEccentricityRelativeToActivePrimary = получить эксцентриситет орбиты корабля после запланированного delta-v.
    public double GetPreviewShipOrbitalEccentricityRelativeToActivePrimary(SimVector3 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        SimVector3 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipOrbitalEccentricityRelativeToBody(activePrimaryBody, previewVelocity);
    }

    // GetPreviewShipSemiMajorAxisRelativeToActivePrimary = получить большую полуось орбиты корабля после запланированного delta-v.
    public double GetPreviewShipSemiMajorAxisRelativeToActivePrimary(SimVector3 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        SimVector3 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipSemiMajorAxisRelativeToBody(activePrimaryBody, previewVelocity);
    }

    public double GetShipOrbitalPeriodRelativeToActivePrimary()
    {
        return GetShipOrbitalPeriodRelativeToBody(
            GetActivePrimaryBody(),
            ship.velocity
        );
    }

    public double GetShipTimeToPeriapsisRelativeToActivePrimary()
    {
        return GetShipTimeToOrbitalEventRelativeToBody(
            GetActivePrimaryBody(),
            ship.velocity,
            0.0
        );
    }

    public double GetShipTimeToApoapsisRelativeToActivePrimary()
    {
        return GetShipTimeToOrbitalEventRelativeToBody(
            GetActivePrimaryBody(),
            ship.velocity,
            Math.PI
        );
    }

    public double GetPreviewShipOrbitalPeriodRelativeToActivePrimary(SimVector3 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        SimVector3 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipOrbitalPeriodRelativeToBody(
            activePrimaryBody,
            previewVelocity
        );
    }

    double GetShipOrbitalPeriodRelativeToBody(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return double.PositiveInfinity;
        }

        return OrbitalMechanics.GetOrbitalPeriod(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity),
            body.mu
        );
    }

    double GetShipTimeToOrbitalEventRelativeToBody(
        Body body,
        SimVector3 shipVelocity,
        double targetMeanAnomaly
    )
    {
        if (body == null)
        {
            return double.PositiveInfinity;
        }

        return OrbitalMechanics.GetTimeToOrbitalEvent(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity),
            body.mu,
            targetMeanAnomaly
        );
    }

    // GetPreviewShipPeriapsisRelativeToActivePrimary = получить перицентр орбиты корабля после запланированного delta-v.
    public double GetPreviewShipPeriapsisRelativeToActivePrimary(SimVector3 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        SimVector3 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipPeriapsisRelativeToBody(activePrimaryBody, previewVelocity);
    }

    public double GetPreviewShipPeriapsisAltitudeRelativeToActivePrimary(SimVector3 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return double.PositiveInfinity;
        }

        SimVector3 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipPeriapsisRelativeToBody(activePrimaryBody, previewVelocity) -
            activePrimaryBody.radius;
    }

    // GetPreviewShipApoapsisRelativeToActivePrimary = получить апоцентр орбиты корабля после запланированного delta-v.
    public double GetPreviewShipApoapsisRelativeToActivePrimary(SimVector3 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        SimVector3 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipApoapsisRelativeToBody(activePrimaryBody, previewVelocity);
    }

    public double GetPreviewShipApoapsisAltitudeRelativeToActivePrimary(SimVector3 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == null)
        {
            return double.PositiveInfinity;
        }

        SimVector3 previewVelocity = ship.velocity.Add(plannedDeltaV);
        double apoapsis = GetShipApoapsisRelativeToBody(activePrimaryBody, previewVelocity);

        if (double.IsInfinity(apoapsis))
        {
            return double.PositiveInfinity;
        }

        return apoapsis - activePrimaryBody.radius;
    }

    double GetShipApoapsisRelativeToBody(Body body)
    {
        return GetShipApoapsisRelativeToBody(body, ship.velocity);
    }

    double GetShipApoapsisRelativeToBody(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return double.PositiveInfinity;
        }

        return OrbitalMechanics.GetApoapsisRadius(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity),
            body.mu
        );
    }

    double GetShipSpecificOrbitalEnergyRelativeToBody(Body body)
    {
        return GetShipSpecificOrbitalEnergyRelativeToBody(body, ship.velocity);
    }

    double GetShipSpecificOrbitalEnergyRelativeToBody(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return 0.0;
        }

        return OrbitalMechanics.GetSpecificOrbitalEnergy(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity),
            body.mu
        );
    }

    string GetShipOrbitTypeRelativeToBody(Body body)
    {
        return GetShipOrbitTypeRelativeToBody(body, ship.velocity);
    }

    string GetShipOrbitTypeRelativeToBody(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return "None";
        }

        return OrbitalMechanics.GetOrbitType(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity),
            body.mu
        );
    }

    double GetShipEscapeSpeedRelativeToBody(Body body)
    {
        if (body == null)
        {
            return 0.0;
        }

        return OrbitalMechanics.GetEscapeSpeed(
            GetRelativePosition(body).Length(),
            body.mu
        );
    }

    double GetShipAngularMomentumRelativeToBody(Body body)
    {
        return GetShipAngularMomentumRelativeToBody(body, ship.velocity);
    }

    double GetShipAngularMomentumRelativeToBody(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return 0.0;
        }

        return OrbitalMechanics.GetSignedAngularMomentum(
            GetRelativePosition(body),
            GetRelativeVelocity(body, shipVelocity)
        );
    }

    SimVector3 GetRelativePosition(Body body)
    {
        if (body == null)
        {
            return SimVector3.Zero();
        }

        return ship.position.Subtract(body.position);
    }

    SimVector3 GetRelativeVelocity(Body body, SimVector3 shipVelocity)
    {
        if (body == null)
        {
            return shipVelocity;
        }

        return shipVelocity.Subtract(body.velocity);
    }
}

struct BodyState
{
    public string name;
    public SimVector3 position;
    public SimVector3 velocity;
    public bool isStatic;
    public bool isActivePrimary;
    public double radius;

    public BodyState(
        string name,
        SimVector3 position,
        SimVector3 velocity,
        bool isStatic,
        bool isActivePrimary,
        double radius
    )
    {
        this.name = name;
        this.position = position;
        this.velocity = velocity;
        this.isStatic = isStatic;
        this.isActivePrimary = isActivePrimary;
        this.radius = radius;
    }
}

struct DebrisState
{
    public string id;
    public string stageName;
    public SimVector3 position;
    public SimVector3 velocity;
    public double headingRadians;
    public SimVector3 centerOfMassLocal;
    public ShipBlockView[] blocks;

    public DebrisState(
        string id,
        string stageName,
        SimVector3 position,
        SimVector3 velocity,
        double headingRadians,
        SimVector3 centerOfMassLocal,
        ShipBlockView[] blocks
    )
    {
        this.id = id;
        this.stageName = stageName;
        this.position = position;
        this.velocity = velocity;
        this.headingRadians = headingRadians;
        this.centerOfMassLocal = centerOfMassLocal;
        this.blocks = blocks;
    }
}

struct PreviewExecutionDriftInfo
{
    public bool hasData;
    public double positionError;
    public double velocityError;
    public double maxPathDeviation;
    public double previewThrustForce;
    public double actualThrustForce;
    public double throttle;
}

struct ClosestApproachInfo
{
    public bool hasApproach;
    public double distance;
    public double time;
    public SimVector3 shipPosition;
    public SimVector3 targetPosition;
    public double relativeSpeed;
    public string targetBodyName;
    public bool entersMoonSphereOfInfluence;

    public ClosestApproachInfo(
        bool hasApproach,
        double distance,
        double time,
        SimVector3 shipPosition,
        SimVector3 targetPosition,
        double relativeSpeed,
        string targetBodyName,
        bool entersMoonSphereOfInfluence
    )
    {
        this.hasApproach = hasApproach;
        this.distance = distance;
        this.time = time;
        this.shipPosition = shipPosition;
        this.targetPosition = targetPosition;
        this.relativeSpeed = relativeSpeed;
        this.targetBodyName = targetBodyName;
        this.entersMoonSphereOfInfluence = entersMoonSphereOfInfluence;
    }

    public static ClosestApproachInfo None()
    {
        return new ClosestApproachInfo(
            false,
            double.PositiveInfinity,
            0,
            SimVector3.Zero(),
            SimVector3.Zero(),
            0,
            "",
            false
        );
    }
}

struct SphereOfInfluenceEntryPreview
{
    public bool entersSphereOfInfluence;
    public double entryTime;
    public SimVector3 entryPosition;
    public SimVector3[] pathPoints;
    public SimVector3[] moonOrbitPathPoints;
    public string targetBodyName;

    public SphereOfInfluenceEntryPreview(
        bool entersSphereOfInfluence,
        double entryTime,
        SimVector3 entryPosition,
        SimVector3[] pathPoints,
        SimVector3[] moonOrbitPathPoints,
        string targetBodyName = "Moon"
    )
    {
        this.entersSphereOfInfluence = entersSphereOfInfluence;
        this.entryTime = entryTime;
        this.entryPosition = entryPosition;
        this.pathPoints = pathPoints;
        this.moonOrbitPathPoints = moonOrbitPathPoints;
        this.targetBodyName = targetBodyName;
    }

    public static SphereOfInfluenceEntryPreview None()
    {
        return new SphereOfInfluenceEntryPreview(
            false,
            0,
            SimVector3.Zero(),
            Array.Empty<SimVector3>(),
            Array.Empty<SimVector3>(),
            string.Empty
        );
    }
}
