using System;
using System.Collections.Generic;

struct VesselControlState
{
    public float throttle;
    public SimVector3 rcsVector;
    public ShipOrientationMode orientationMode;
    public float pitchInput;
    public float yawInput;
    public float rollInput;
    public bool stagingRequested;
}

class Vessel : Body
{
    public SimVector3 thrust;
    public SimVector3 thrustAcceleration;
    public SimVector3 dragAcceleration;
    public SimQuaternion orientation;
    public double heading;
    public List<StageData> stages;
    public int activeStageIndex;
    public FlightStatus flightStatus;
    public string landedOnBodyName;
    public SimVector3 landedSurfaceDirectionBodyFixed;
    public double collisionRadius;
    public SimVector3 lastSurfaceNormal;
    public SimVector3 centerOfPressureOffset;
    public SimVector3 angularVelocity;
    public RigidBodyInertia inertiaLocal;
    public double envelopeRadius;
    public bool useHonestDockingEnvelope;
    public bool isOnKeplerRails;
    double cachedBottomExtent;
    SimVector3 cachedBottomExtentNormal;
    int cachedBottomExtentLayoutRevision;
    public KeplerRailsState keplerRails;
    public double reentryIntensity;
    public double payloadMass;
    public bool IsDocked;
    public double dockingPortForwardOffset;
    public bool hasDockedPartnerVisual;
    public string dockedPartnerName;
    public SimVector3 dockedPartnerLocalOffset;
    public double dockedPartnerRelativeHeading;

    public const double rcsConsumption = 0.08;
    public const double rcsThrustPerUnit = 4.0;
    public double rcsFuel;
    public SimVector3 rcsAcceleration;

    public double totalMass;
    public double activeStageDeltaV;
    public double activeStageMaxThrustToWeight;
    public SimVector3 centerOfMassLocal;
    public int shipLayoutRevision;
    bool hasCenterOfMassSnapshot;
    public FixedQueue<int> pendingDestroyedBlockIds;

    public const int MaxPendingDestroyedBlocks = 64;

    public bool IsDestroyed => flightStatus == FlightStatus.Crashed;

    public VesselControlState controlState = new VesselControlState();
    public SimQuaternion stabilityAssistTargetOrientation;
    public bool stabilityAssistTargetInitialized;
    public float stabilityAssistPrevPitchInput;
    public float stabilityAssistPrevYawInput;
    public float stabilityAssistPrevRollInput;
    public ShipOrientationMode stabilityAssistLastMode;

    public Vessel(string name, SimVector3 position, SimVector3 velocity)
        : base(name, position, velocity, SimVector3.Zero(), 0, 0, false)
    {
        controlState.orientationMode = ShipOrientationMode.Manual;
        thrust = SimVector3.Zero();
        thrustAcceleration = SimVector3.Zero();
        dragAcceleration = SimVector3.Zero();
        heading = Math.Atan2(velocity.x, velocity.z);
        orientation = SimQuaternion.FromYawRadians(heading);
        stages = new List<StageData>();
        activeStageIndex = 0;
        flightStatus = FlightStatus.Flying;
        landedOnBodyName = string.Empty;
        collisionRadius = LandingConfig.ShipCollisionRadius;
        lastSurfaceNormal = SimVector3.UnitY();
        centerOfPressureOffset = new SimVector3(0.0, 0.0, -0.4);
        angularVelocity = SimVector3.Zero();
        inertiaLocal = RigidBodyInertia.Zero();
        envelopeRadius = LandingConfig.ShipCollisionRadius;
        isOnKeplerRails = false;
        keplerRails = KeplerRailsState.Invalid();
        reentryIntensity = 0.0;
        payloadMass = 0.0;
        IsDocked = false;
        dockingPortForwardOffset = 4.0;
        hasDockedPartnerVisual = false;
        dockedPartnerName = string.Empty;
        dockedPartnerLocalOffset = SimVector3.Zero();
        dockedPartnerRelativeHeading = 0.0;
        rcsFuel = 5.0;
        rcsAcceleration = SimVector3.Zero();
        totalMass = 0.0;
        activeStageDeltaV = 0.0;
        activeStageMaxThrustToWeight = 0.0;
        centerOfMassLocal = SimVector3.Zero();
        shipLayoutRevision = 0;
        hasCenterOfMassSnapshot = false;
        pendingDestroyedBlockIds = new FixedQueue<int>(MaxPendingDestroyedBlocks);
    }

    public void SaveShip(string path)
    {
        ShipPersistence.SaveShip(path, stages);
    }

    public static List<StageData> LoadShip(string path)
    {
        return ShipPersistence.LoadShip(path);
    }

    static bool IsActiveBlock(ShipBlock block)
    {
        return block != null && !block.isDestroyed;
    }

    public int[] DrainPendingDestroyedBlockIds()
    {
        if (pendingDestroyedBlockIds.Count <= 0)
        {
            return Array.Empty<int>();
        }

        int[] drained = new int[pendingDestroyedBlockIds.Count];
        pendingDestroyedBlockIds.DrainTo(drained, 0, drained.Length);
        return drained;
    }

    public bool ProcessBlockSurfaceImpacts(
        SimVector3 surfaceNormal,
        SimVector3 surfaceBodyVelocity,
        double localGravity
    )
    {
        if (flightStatus != FlightStatus.Flying || stages == null)
        {
            return false;
        }

        if (SimulationCheatFlags.DisableCrashDamage)
        {
            return false;
        }

        EnsureStageBlocks();
        SimVector3 outward = surfaceNormal.LengthSquared() > 1e-18 ?
            surfaceNormal.Normalize() :
            lastSurfaceNormal.Normalize();
        bool anyDestroyed = false;

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!IsActiveBlock(block))
                {
                    continue;
                }

                if (!IsBlockContactingSurface(block, stage, outward))
                {
                    continue;
                }

                SimVector3 blockOffsetLocal = stage.stageMountOffset
                    .Add(block.localOffset)
                    .Subtract(centerOfMassLocal);
                SimVector3 rBlockWorld = orientation.Rotate(blockOffsetLocal);
                SimVector3 rotationalVelocity = SimVector3.Cross(
                    angularVelocity,
                    rBlockWorld
                );
                SimVector3 vContact = velocity
                    .Add(rotationalVelocity)
                    .Subtract(surfaceBodyVelocity);
                double contactSpeed = vContact.Length();

                if (contactSpeed <= block.structuralStrength)
                {
                    continue;
                }

                block.isDestroyed = true;
                pendingDestroyedBlockIds.Enqueue(block.blockId);
                AbsorbImpactEnergy(surfaceNormal, surfaceBodyVelocity, block.structuralStrength);
                anyDestroyed = true;
            }
        }

        if (!anyDestroyed)
        {
            return false;
        }

        RemoveDestroyedBlocks(localGravity);
        return true;
    }

    void AbsorbImpactEnergy(
        SimVector3 surfaceNormal,
        SimVector3 surfaceBodyVelocity,
        double structuralStrength
    )
    {
        SimVector3 outward = surfaceNormal.Normalize();
        SimVector3 relativeVelocity = velocity.Subtract(surfaceBodyVelocity);
        double verticalSpeed = SimVector3.Dot(relativeVelocity, outward);

        if (verticalSpeed >= 0.0)
        {
            return;
        }

        double absorbed = Math.Min(structuralStrength, -verticalSpeed);
        relativeVelocity = relativeVelocity.Add(outward.Multiply(absorbed));
        velocity = surfaceBodyVelocity.Add(relativeVelocity);
    }

    bool IsBlockContactingSurface(
        ShipBlock block,
        StageData stage,
        SimVector3 outward
    )
    {
        SimVector3 blockCenter = stage.stageMountOffset.Add(block.localOffset);
        SimVector3 halfExtents = new SimVector3(
            block.dimensions.x * 0.5,
            block.dimensions.y * 0.5,
            block.dimensions.z * 0.5
        );
        double minProjection = double.MaxValue;
        bool hasProjection = false;
        const double contactEpsilon = 0.05;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    SimVector3 cornerLocal = blockCenter.Add(
                        new SimVector3(
                            halfExtents.x * sx,
                            halfExtents.y * sy,
                            halfExtents.z * sz
                        )
                    );
                    SimVector3 cornerFromCom = cornerLocal.Subtract(centerOfMassLocal);
                    SimVector3 cornerWorld = orientation.Rotate(cornerFromCom);
                    double projection = SimVector3.Dot(cornerWorld, outward);

                    if (!hasProjection || projection < minProjection)
                    {
                        minProjection = projection;
                        hasProjection = true;
                    }
                }
            }
        }

        return hasProjection && minProjection <= contactEpsilon;
    }

    void RemoveDestroyedBlocks(double localGravity)
    {
        bool removedAny = false;

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = stage.blocks.Count - 1; b >= 0; b--)
            {
                ShipBlock block = stage.blocks[b];

                if (!block.isDestroyed)
                {
                    continue;
                }

                if (block.isFuelTank)
                {
                    stage.fuelMass = Math.Max(0.0, stage.fuelMass - block.fuelMass);
                    stage.maxFuelMass = Math.Max(
                        0.0,
                        stage.maxFuelMass - block.fuelCapacity
                    );
                }

                stage.dryMass = Math.Max(0.0, stage.dryMass - block.dryMass);
                stage.blocks.RemoveAt(b);
                removedAny = true;
            }
        }

        if (!removedAny)
        {
            return;
        }

        NotifyShipLayoutChanged();
        for (int i = 0; i < stages.Count; i++)
        {
            StageData.ReconcileScalarsFromBlocks(stages[i]);
        }

        RecalculateMassAndDeltaV(localGravity);

        angularVelocity = SimVector3.ClampMagnitude(
            angularVelocity,
            PhysicsConstants.MaxPhysicsAngularRate
        );
    }

    public bool CheckCrash(
        double verticalSpeed,
        double surfaceSpeed,
        double crashSpeedThreshold
    )
    {
        if (SimulationCheatFlags.DisableCrashDamage)
        {
            return false;
        }

        if (flightStatus != FlightStatus.Flying)
        {
            return IsDestroyed;
        }

        if (
            Math.Abs(verticalSpeed) > crashSpeedThreshold ||
            surfaceSpeed > crashSpeedThreshold
        )
        {
            flightStatus = FlightStatus.Crashed;
            landedOnBodyName = string.Empty;
            return true;
        }

        return false;
    }

    public void SetHeadingRadians(double radians)
    {
        heading = radians;
        orientation = SimQuaternion.FromYawRadians(heading);
        angularVelocity = SimVector3.Zero();
    }

    public void RotateHeading(double deltaRadians)
    {
        heading += deltaRadians;
        orientation = SimQuaternion.FromYawRadians(heading);
    }

    public SimVector3 GetForwardDirection()
    {
        return orientation.Forward();
    }

    public SimVector3 GetAttitudeHoldAxisWorld(ShipOrientationMode mode)
    {
        if (mode == ShipOrientationMode.TargetAlignment)
        {
            SimVector3 portForward = GetDockingPortForwardWorld();

            return portForward.LengthSquared() > 1e-18 ?
                portForward :
                GetForwardDirection();
        }

        SimVector3 thrustAxisWorld = orientation.Rotate(GetMainEngineThrustAxisLocal());

        return thrustAxisWorld.LengthSquared() > 1e-18 ?
            thrustAxisWorld :
            GetForwardDirection();
    }

    public SimVector3 GetUpDirection()
    {
        return orientation.Up();
    }

    public bool IsDockingPortBlock(ShipBlock block)
    {
        return
            block != null &&
            IsActiveBlock(block) &&
            (block.isDockingPort || block.blockType == ShipBlockType.DockingPort);
    }

    public bool TryGetPrimaryDockingPortLocal(out SimVector3 portCenterLocal)
    {
        portCenterLocal = SimVector3.Zero();
        EnsureStageBlocks();

        if (!TryGetPrimaryDockingPortBlock(out ShipBlock block))
        {
            portCenterLocal = new SimVector3(0.0, 0.0, dockingPortForwardOffset);
            return false;
        }

        if (stages == null)
        {
            return false;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                if (!ReferenceEquals(stage.blocks[b], block))
                {
                    continue;
                }

                portCenterLocal = stage.stageMountOffset.Add(block.localOffset);
                return true;
            }
        }

        return false;
    }

    public bool TryGetPrimaryDockingPortBlock(out ShipBlock block)
    {
        block = null;
        EnsureStageBlocks();

        if (stages == null)
        {
            return false;
        }

        double bestNoseZ = double.NegativeInfinity;

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock candidate = stage.blocks[b];

                if (!IsDockingPortBlock(candidate))
                {
                    continue;
                }

                double noseZ = candidate.localOffset.z +
                    candidate.dimensions.z * 0.5;
                SimVector3 stageOffset = stage.stageMountOffset;
                double worldNoseZ = stageOffset.z + noseZ;

                if (block == null || worldNoseZ > bestNoseZ)
                {
                    block = candidate;
                    bestNoseZ = worldNoseZ;
                }
            }
        }

        return block != null;
    }

    public SimVector3 GetDockingPortForwardLocal()
    {
        if (
            TryGetPrimaryDockingPortBlock(out ShipBlock block) &&
            block.thrustDirection.LengthSquared() > 1e-12
        )
        {
            return block.thrustDirection.Normalize();
        }

        return SimVector3.UnitZ();
    }

    public SimVector3 GetDockingPortForwardWorld()
    {
        return orientation.Rotate(GetDockingPortForwardLocal()).Normalize();
    }

    public SimVector3 GetDockingPortWorldPosition()
    {
        if (TryGetPrimaryDockingPortLocal(out SimVector3 portCenterLocal))
        {
            SimVector3 offsetFromCom = portCenterLocal.Subtract(centerOfMassLocal);
            return GetCenterOfMassWorld().Add(orientation.Rotate(offsetFromCom));
        }

        return position.Add(
            GetForwardDirection().Multiply(dockingPortForwardOffset)
        );
    }

    public SimVector3 GetDockingPortPosition()
    {
        return GetDockingPortWorldPosition();
    }

    public void SyncDockingPortForwardOffset()
    {
        SimVector3 comToPort = GetDockingPortWorldPosition().Subtract(
            GetCenterOfMassWorld()
        );
        SimVector3 forward = GetForwardDirection();
        dockingPortForwardOffset = SimVector3.Dot(comToPort, forward);
    }

    double GetDockingPortHalfLength()
    {
        if (
            TryGetPrimaryDockingPortBlock(out ShipBlock block) &&
            block.dimensions.z > 0.0
        )
        {
            return block.dimensions.z * 0.5;
        }

        return 0.3;
    }

    public DockingPortContactMetrics GetDockingPortContactMetrics(Vessel other)
    {
        DockingPortContactMetrics metrics = new DockingPortContactMetrics
        {
            hasSourcePort = TryGetPrimaryDockingPortLocal(out _),
            hasTargetPort = other != null && other.TryGetPrimaryDockingPortLocal(out _)
        };

        if (!metrics.HasValidPorts)
        {
            return metrics;
        }

        SimVector3 sourcePortWorld = GetDockingPortWorldPosition();
        SimVector3 targetPortWorld = other.GetDockingPortWorldPosition();
        SimVector3 sourceFaceWorld = GetDockingPortFaceWorldPosition();
        SimVector3 targetFaceWorld = other.GetDockingPortFaceWorldPosition();
        SimVector3 sourceToTarget = targetPortWorld.Subtract(sourcePortWorld);
        double distanceSquared = sourceToTarget.LengthSquared();
        double centerDistance = Math.Sqrt(distanceSquared);
        SimVector3 sourceForward = GetDockingPortForwardWorld();
        SimVector3 faceDelta = targetFaceWorld.Subtract(sourceFaceWorld);
        metrics.portSeparation = Math.Max(0.0, SimVector3.Dot(faceDelta, sourceForward));

        SimVector3 rSource = sourcePortWorld.Subtract(GetCenterOfMassWorld());
        SimVector3 rTarget = targetPortWorld.Subtract(other.GetCenterOfMassWorld());
        SimVector3 sourcePortVelocity = velocity.Add(
            SimVector3.Cross(angularVelocity, rSource)
        );
        SimVector3 targetPortVelocity = other.velocity.Add(
            SimVector3.Cross(other.angularVelocity, rTarget)
        );
        metrics.relativePortSpeed = sourcePortVelocity
            .Subtract(targetPortVelocity)
            .Length();

        if (distanceSquared <= 1e-12)
        {
            return metrics;
        }

        SimVector3 directionSourceToTarget = sourceToTarget.Multiply(
            1.0 / centerDistance
        );
        SimVector3 directionTargetToSource = directionSourceToTarget.Multiply(-1.0);
        metrics.sourcePortAlignment = SimVector3.Dot(
            GetDockingPortForwardWorld(),
            directionSourceToTarget
        );
        metrics.targetPortAlignment = SimVector3.Dot(
            other.GetDockingPortForwardWorld(),
            directionTargetToSource
        );
        return metrics;
    }

    public double GetYawMomentOfInertia()
    {
        return Math.Max(inertiaLocal.izz, 0.001);
    }

    public SimVector3 GetMaxAttitudeControlTorqueLocal(
        double maxAngularRateRadiansPerSecond
    )
    {
        const double responseTimeSeconds = 0.25;
        double scale = maxAngularRateRadiansPerSecond / responseTimeSeconds;

        return new SimVector3(
            Math.Max(inertiaLocal.ixx, 0.001) * scale,
            Math.Max(inertiaLocal.iyy, 0.001) * scale,
            Math.Max(inertiaLocal.izz, 0.001) * scale
        );
    }

    public void ApplyOrientationTorque(SimVector3 torqueLocal, double dt)
    {
        if (dt <= 0.0)
        {
            return;
        }

        SimVector3 angularAcceleration = inertiaLocal.SolveAngularAcceleration(
            torqueLocal,
            angularVelocity
        );
        angularVelocity = angularVelocity.Add(angularAcceleration.Multiply(dt));
        ClampAngularVelocity(PhysicsConstants.MaxPhysicsAngularRate);
    }

    public void ClampAngularVelocity(double maxRateRadiansPerSecond)
    {
        if (!IsFiniteVector(angularVelocity))
        {
            angularVelocity = SimVector3.Zero();
            return;
        }

        angularVelocity = SimVector3.ClampMagnitude(
            angularVelocity,
            Math.Min(maxRateRadiansPerSecond, PhysicsConstants.MaxPhysicsAngularRate)
        );
    }

    static bool IsFiniteVector(SimVector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public void IntegrateOrientation(double dt)
    {
        ClampAngularVelocity(PhysicsConstants.MaxPhysicsAngularRate);

        SimVector3 comBefore = position.Add(orientation.Rotate(centerOfMassLocal));
        SimVector3 deltaAngleBody = angularVelocity.Multiply(dt);
        double angle = deltaAngleBody.Length();

        if (angle <= 1e-12)
        {
            SyncHeadingFromOrientation();
            return;
        }

        SimVector3 axisBody = deltaAngleBody.Multiply(1.0 / angle);
        SimQuaternion deltaBody = SimQuaternion.FromAxisAngle(axisBody, angle);
        orientation = SimQuaternion.Multiply(orientation, deltaBody).Normalize();
        SyncHeadingFromOrientation();

        SimVector3 comAfter = position.Add(orientation.Rotate(centerOfMassLocal));
        position = position.Add(comBefore.Subtract(comAfter));
    }

    /// <summary>
    /// World-space nose slerp with a radian cap. Used on Kepler coast warp so
    /// SAS tracking cannot overshoot a 1–120 s step at 120 °/s.
    /// </summary>
    public void RotateNoseTowardWorld(SimVector3 worldDirection, double maxRadians)
    {
        VesselWarpAttitude.RotateNoseTowardWorld(
            ref orientation,
            ref position,
            ref angularVelocity,
            centerOfMassLocal,
            worldDirection,
            maxRadians
        );
        SyncHeadingFromOrientation();
    }

    public void ApplyYawAngularAcceleration(double angularAcceleration, double dt)
    {
        ApplyOrientationTorque(
            new SimVector3(0.0, 0.0, angularAcceleration * inertiaLocal.izz),
            dt
        );
    }

    public void StabilizeLandedOrientation(SimVector3 surfaceNormal)
    {
        if (surfaceNormal.LengthSquared() <= 1e-18)
        {
            SetHeadingRadians(heading);
            return;
        }

        SimVector3 outward = surfaceNormal.Normalize();

        if (SimVector3.Dot(outward, SimVector3.UnitY()) > 0.999)
        {
            orientation = SimQuaternion.Identity();
            SyncHeadingFromOrientation();
            angularVelocity = SimVector3.Zero();
            return;
        }

        double targetHeading = Math.Atan2(outward.y, outward.x);
        SetHeadingRadians(targetHeading);
    }

    public void ApplyMainEngineThrust(
        double throttle,
        double burnScale,
        double dt,
        bool suppressEngineTorque = false
    )
    {
        if (flightStatus == FlightStatus.Landed)
        {
            thrustAcceleration = SimVector3.Zero();
            return;
        }

        if (throttle <= 0.0 || burnScale <= 0.0 || dt <= 0.0)
        {
            thrustAcceleration = SimVector3.Zero();
            return;
        }

        burnScale *= SimulationCheatFlags.ThrustMultiplier;

        if (burnScale <= 0.0)
        {
            thrustAcceleration = SimVector3.Zero();
            return;
        }

        StageData activeStage = GetActiveStage();

        if (activeStage == null || activeStage.blocks == null)
        {
            thrustAcceleration = SimVector3.Zero();
            return;
        }

        double mass = GetMass();

        if (mass <= 0.0)
        {
            thrustAcceleration = SimVector3.Zero();
            return;
        }

        if (suppressEngineTorque)
        {
            double totalThrust = GetActiveEngineThrustNewtons() * throttle * burnScale;

            if (totalThrust <= 0.0)
            {
                thrustAcceleration = SimVector3.Zero();
                return;
            }

            SimVector3 forceWorld = orientation.Rotate(
                GetMainEngineThrustAxisLocal().Multiply(totalThrust)
            );
            thrustAcceleration = forceWorld.Multiply(1.0 / mass);
            velocity = velocity.Add(thrustAcceleration.Multiply(dt));
            angularVelocity = SimVector3.Zero();
            return;
        }

        SimVector3 totalForceWorld = SimVector3.Zero();
        SimVector3 totalTorqueWorld = SimVector3.Zero();
        SimVector3 comWorld = GetCenterOfMassWorld();

        for (int i = 0; i < activeStage.blocks.Count; i++)
        {
            ShipBlock block = activeStage.blocks[i];

            if (!IsActiveBlock(block) || !block.IsEngineBlock())
            {
                continue;
            }

            double engineThrust = block.maxThrustNewtons * throttle * burnScale;

            if (engineThrust <= 0.0)
            {
                continue;
            }

            SimVector3 forceLocal = block.thrustDirection.Normalize().Multiply(engineThrust);
            SimVector3 forceWorld = orientation.Rotate(forceLocal);
            SimVector3 thrustPointLocal = activeStage.stageMountOffset
                .Add(block.localOffset)
                .Add(block.thrustLocalOffset);
            SimVector3 thrustPointWorld = position.Add(orientation.Rotate(thrustPointLocal));
            SimVector3 armWorld = thrustPointWorld.Subtract(comWorld);
            totalForceWorld = totalForceWorld.Add(forceWorld);
            totalTorqueWorld = totalTorqueWorld.Add(
                SimVector3.Cross(armWorld, forceWorld)
            );
        }

        if (totalForceWorld.LengthSquared() <= 0.0)
        {
            thrustAcceleration = SimVector3.Zero();
            return;
        }

        thrustAcceleration = totalForceWorld.Multiply(1.0 / mass);
        velocity = velocity.Add(thrustAcceleration.Multiply(dt));
        ApplyOrientationTorque(
            orientation.InverseRotate(totalTorqueWorld),
            dt
        );
    }

    public SimVector3 GetMainEngineThrustAxisLocal()
    {
        StageData activeStage = GetActiveStage();

        if (activeStage == null || activeStage.blocks == null)
        {
            return SimVector3.UnitY();
        }

        SimVector3 axisSum = SimVector3.Zero();

        for (int i = 0; i < activeStage.blocks.Count; i++)
        {
            ShipBlock block = activeStage.blocks[i];

            if (!IsActiveBlock(block) || !block.IsEngineBlock())
            {
                continue;
            }

            if (block.maxThrustNewtons <= 0.0)
            {
                continue;
            }

            if (block.thrustDirection.LengthSquared() <= 1e-12)
            {
                continue;
            }

            axisSum = axisSum.Add(
                block.thrustDirection.Normalize().Multiply(block.maxThrustNewtons)
            );
        }

        if (axisSum.LengthSquared() > 1e-12)
        {
            return axisSum.Normalize();
        }

        return SimVector3.UnitY();
    }

    public double GetActiveEngineThrustNewtons()
    {
        StageData activeStage = GetActiveStage();

        if (activeStage == null)
        {
            return 0.0;
        }

        if (activeStage.blocks != null)
        {
            double blockThrust = 0.0;

            for (int i = 0; i < activeStage.blocks.Count; i++)
            {
                ShipBlock block = activeStage.blocks[i];

                if (!IsActiveBlock(block) || !block.IsEngineBlock())
                {
                    continue;
                }

                blockThrust += Math.Max(0.0, block.maxThrustNewtons);
            }

            return SimulationCheatFlags.ScaleThrustNewtons(blockThrust);
        }

        return SimulationCheatFlags.ScaleThrustNewtons(
            Math.Max(0.0, activeStage.thrust)
        );
    }

    public bool HasLiveMainEngine()
    {
        return GetActiveEngineThrustNewtons() > 1e-9;
    }

    public SimVector3 GetAggregatedMainEngineThrustLocal(double throttle)
    {
        if (throttle <= 0.0)
        {
            return SimVector3.Zero();
        }

        StageData activeStage = GetActiveStage();

        if (activeStage == null || activeStage.blocks == null)
        {
            return SimVector3.Zero();
        }

        SimVector3 totalForceLocal = SimVector3.Zero();

        for (int i = 0; i < activeStage.blocks.Count; i++)
        {
            ShipBlock block = activeStage.blocks[i];

            if (!IsActiveBlock(block) || !block.IsEngineBlock())
            {
                continue;
            }

            double engineThrust = block.maxThrustNewtons * throttle;

            if (engineThrust <= 0.0)
            {
                continue;
            }

            totalForceLocal = totalForceLocal.Add(
                block.thrustDirection.Normalize().Multiply(engineThrust)
            );
        }

        return totalForceLocal;
    }

    public SimVector3 GetAggregatedMainEngineThrustWorld(double throttle)
    {
        SimVector3 thrustLocal = GetAggregatedMainEngineThrustLocal(throttle);

        if (thrustLocal.LengthSquared() <= 0.0)
        {
            return SimVector3.Zero();
        }

        return orientation.Rotate(thrustLocal);
    }

    public void ApplyStagingSeparationImpulse(
        SimVector3 separatedStageComLocal,
        double separatedMass,
        SimVector3 comBeforeLocal
    )
    {
        if (separatedMass <= 0.0)
        {
            return;
        }

        const double separationSpeed = 1.5;
        SimVector3 separationDirectionLocal = new SimVector3(-1.0, 0.0, 0.0);
        SimVector3 arm = separatedStageComLocal.Subtract(comBeforeLocal);
        SimVector3 linearImpulse = separationDirectionLocal.Multiply(
            separatedMass * separationSpeed
        );
        SimVector3 angularImpulse = SimVector3.Cross(arm, linearImpulse);
        angularVelocity = angularVelocity.Add(
            inertiaLocal.ApplyAngularImpulse(angularImpulse)
        );
    }

    public static double ComputeStageMass(StageData stage)
    {
        if (stage == null)
        {
            return 0.0;
        }

        return stage.dryMass + stage.fuelMass;
    }

    public static SimVector3 ComputeStageCenterOfMassLocal(
        StageData stage,
        SimVector3 mountOffset
    )
    {
        if (stage == null || stage.blocks == null || stage.blocks.Count == 0)
        {
            return mountOffset;
        }

        SimVector3 weightedOffset = SimVector3.Zero();
        double massSum = 0.0;

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (!IsActiveBlock(block))
            {
                continue;
            }

            double blockMass = block.GetMass();

            if (blockMass <= 0.0)
            {
                continue;
            }

            weightedOffset = weightedOffset.Add(
                mountOffset
                    .Add(block.localOffset)
                    .Multiply(blockMass)
            );
            massSum += blockMass;
        }

        return massSum > 0.0 ?
            weightedOffset.Multiply(1.0 / massSum) :
            mountOffset;
    }

    SimVector3 GetActiveEngineThrustPointLocal()
    {
        StageData activeStage = GetActiveStage();

        if (activeStage == null || activeStage.blocks == null)
        {
            return centerOfMassLocal;
        }

        SimVector3 thrustPointSum = SimVector3.Zero();
        int engineCount = 0;

        for (int i = 0; i < activeStage.blocks.Count; i++)
        {
            ShipBlock block = activeStage.blocks[i];

            if (!IsActiveBlock(block))
            {
                continue;
            }

            if (!block.isEngine && block.blockType != ShipBlockType.Engine)
            {
                continue;
            }

            thrustPointSum = thrustPointSum.Add(
                activeStage.stageMountOffset
                    .Add(block.localOffset)
                    .Add(block.thrustLocalOffset)
            );
            engineCount++;
        }

        return engineCount > 0 ?
            thrustPointSum.Multiply(1.0 / engineCount) :
            centerOfMassLocal;
    }

    public void SyncHeadingFromOrientation()
    {
        SimVector3 forward = orientation.Forward();
        heading = Math.Atan2(forward.x, forward.z);
    }

    public void ApplyDistributedRcsThrust(
        SimVector3 localThrustCommand,
        double thrustScale,
        double dt
    )
    {
        if (
            thrustScale <= 0.0 ||
            localThrustCommand.LengthSquared() <= 0.0
        )
        {
            return;
        }

        SimVector3 totalForceLocal = SimVector3.Zero();
        SimVector3 totalTorqueLocal = SimVector3.Zero();
        AccumulateThrustersAlongDirection(
            new SimVector3(1.0, 0.0, 0.0),
            localThrustCommand.x * thrustScale,
            ref totalForceLocal,
            ref totalTorqueLocal
        );
        AccumulateThrustersAlongDirection(
            new SimVector3(0.0, 1.0, 0.0),
            localThrustCommand.y * thrustScale,
            ref totalForceLocal,
            ref totalTorqueLocal
        );
        AccumulateThrustersAlongDirection(
            SimVector3.UnitZ(),
            localThrustCommand.z * thrustScale,
            ref totalForceLocal,
            ref totalTorqueLocal
        );

        double mass = GetMass();

        if (mass <= 0.0)
        {
            return;
        }

        SimVector3 forceWorld = orientation.Rotate(totalForceLocal);
        rcsAcceleration = forceWorld.Multiply(1.0 / mass);
        velocity = velocity.Add(rcsAcceleration.Multiply(dt));
        ApplyOrientationTorque(totalTorqueLocal, dt);
    }

    void AccumulateThrustersAlongDirection(
        SimVector3 desiredDirection,
        double commandedThrust,
        ref SimVector3 totalForceLocal,
        ref SimVector3 totalTorqueLocal
    )
    {
        if (Math.Abs(commandedThrust) <= 1e-9)
        {
            return;
        }

        SimVector3 direction = desiredDirection.Normalize();
        double remainingThrust = Math.Abs(commandedThrust);
        double sign = Math.Sign(commandedThrust);

        if (stages == null)
        {
            return;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!IsActiveBlock(block))
                {
                    continue;
                }

                if (block.rcsThrust <= 0.0)
                {
                    continue;
                }

                double alignment = SimVector3.Dot(
                    block.thrustDirection.Normalize(),
                    direction.Multiply(sign)
                );

                if (alignment <= 0.25)
                {
                    continue;
                }

                double thrusterForce = Math.Min(block.rcsThrust, remainingThrust);
                remainingThrust -= thrusterForce;
                SimVector3 forceLocal = direction.Multiply(thrusterForce * sign);
                SimVector3 arm = stage.stageMountOffset
                    .Add(block.localOffset)
                    .Subtract(centerOfMassLocal);
                totalForceLocal = totalForceLocal.Add(forceLocal);
                totalTorqueLocal = totalTorqueLocal.Add(
                    SimVector3.Cross(arm, forceLocal)
                );

                if (remainingThrust <= 1e-9)
                {
                    return;
                }
            }
        }
    }

    public double GetEnvelopeRadius()
    {
        return envelopeRadius > 0.0 ? envelopeRadius : collisionRadius;
    }

    public void RecalculateMassAndDeltaV(double localGravity)
    {
        SimVector3 previousCenterOfMassLocal = centerOfMassLocal;
        EnsureStageBlocks();
        SyncBlockFuelFromStages();

        totalMass = 0.0;

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];
            totalMass += stage.dryMass + stage.fuelMass;
        }

        RecalculateCenterOfMass();

        if (useHonestDockingEnvelope)
        {
            RecalculatePhysicsEnvelopeForDocking();
        }
        else
        {
            RecalculatePhysicsEnvelope();
        }

        if (hasCenterOfMassSnapshot && flightStatus != FlightStatus.Landed)
        {
            ApplyCenterOfMassPositionCorrection(previousCenterOfMassLocal);
        }

        hasCenterOfMassSnapshot = true;
        InvalidateBottomExtentCache();
        SyncDockingPortForwardOffset();

        StageData activeStage = GetActiveStage();

        if (activeStage == null || localGravity <= 0.0 || totalMass <= 0.0)
        {
            activeStageDeltaV = 0.0;
            activeStageMaxThrustToWeight = 0.0;
            return;
        }

        activeStageMaxThrustToWeight =
            GetActiveEngineThrustNewtons() / (totalMass * localGravity);

        double endMass = totalMass - activeStage.fuelMass;

        if (
            activeStage.isp > 0.0 &&
            activeStage.fuelMass > 0.0 &&
            endMass > 0.0 &&
            totalMass > endMass
        )
        {
            activeStageDeltaV =
                activeStage.isp *
                PhysicsConstants.StandardGravity *
                Math.Log(totalMass / endMass);
        }
        else
        {
            activeStageDeltaV = 0.0;
        }
    }

    public void RecalculateCenterOfMass()
    {
        EnsureStageBlocks();
        SyncBlockFuelFromStages();

        SimVector3 weightedOffset = SimVector3.Zero();
        double massSum = 0.0;

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!IsActiveBlock(block))
                {
                    continue;
                }

                double blockMass = block.GetMass();

                if (blockMass <= 0.0)
                {
                    continue;
                }

                SimVector3 globalOffset = stage.stageMountOffset.Add(block.localOffset);
                weightedOffset = weightedOffset.Add(
                    globalOffset.Multiply(blockMass)
                );
                massSum += blockMass;
            }
        }

        centerOfMassLocal = massSum > 0.0 ?
            weightedOffset.Multiply(1.0 / massSum) :
            SimVector3.Zero();
    }

    public void RecalculatePhysicsEnvelope()
    {
        RecalculatePhysicsEnvelope(applySurfaceLaunchFloor: true);
    }

    public void RecalculatePhysicsEnvelopeForDocking()
    {
        RecalculatePhysicsEnvelope(applySurfaceLaunchFloor: false);
    }

    void RecalculatePhysicsEnvelope(bool applySurfaceLaunchFloor)
    {
        inertiaLocal = RigidBodyInertia.Compute(stages, centerOfMassLocal);
        centerOfPressureOffset = ComputeCenterOfPressureLocal();
        envelopeRadius = ComputeEnvelopeRadius(applySurfaceLaunchFloor);
        collisionRadius = envelopeRadius;
    }

    SimVector3 ComputeCenterOfPressureLocal()
    {
        SimVector3 weightedOffset = SimVector3.Zero();
        double areaSum = 0.0;

        if (stages == null)
        {
            return centerOfPressureOffset;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!IsActiveBlock(block))
                {
                    continue;
                }

                if (
                    block.blockType == ShipBlockType.RcsThruster ||
                    block.rcsThrust > 0.0 ||
                    block.blockType == ShipBlockType.DockingPort ||
                    block.isDockingPort
                )
                {
                    continue;
                }

                double frontalArea =
                    block.dimensions.y * block.dimensions.z;

                if (frontalArea <= 0.0)
                {
                    continue;
                }

                SimVector3 blockCenter = stage.stageMountOffset.Add(block.localOffset);
                weightedOffset = weightedOffset.Add(
                    blockCenter.Multiply(frontalArea)
                );
                areaSum += frontalArea;
            }
        }

        if (areaSum <= 0.0)
        {
            return new SimVector3(-0.4, 0.0, 0.0);
        }

        return weightedOffset.Multiply(1.0 / areaSum);
    }

    double ComputeEnvelopeRadius()
    {
        return ComputeEnvelopeRadius(applySurfaceLaunchFloor: true);
    }

    double ComputeEnvelopeRadius(bool applySurfaceLaunchFloor)
    {
        const double minimumDockingEnvelope = 0.35;
        double maxExtent = 0.0;
        bool hasExtent = false;

        if (stages == null)
        {
            return applySurfaceLaunchFloor ?
                LandingConfig.ShipCollisionRadius :
                minimumDockingEnvelope;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!IsActiveBlock(block))
                {
                    continue;
                }

                SimVector3 blockCenter = stage.stageMountOffset.Add(block.localOffset);
                SimVector3 halfExtents = new SimVector3(
                    block.dimensions.x * 0.5,
                    block.dimensions.y * 0.5,
                    block.dimensions.z * 0.5
                );

                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            SimVector3 corner = blockCenter.Add(
                                new SimVector3(
                                    halfExtents.x * sx,
                                    halfExtents.y * sy,
                                    halfExtents.z * sz
                                )
                            );
                            double extent = corner
                                .Subtract(centerOfMassLocal)
                                .Length();

                            if (!hasExtent || extent > maxExtent)
                            {
                                maxExtent = extent;
                                hasExtent = true;
                            }
                        }
                    }
                }
            }
        }

        if (!hasExtent)
        {
            return applySurfaceLaunchFloor ?
                LandingConfig.ShipCollisionRadius :
                minimumDockingEnvelope;
        }

        if (applySurfaceLaunchFloor)
        {
            return Math.Max(maxExtent, LandingConfig.ShipCollisionRadius);
        }

        return Math.Max(maxExtent, minimumDockingEnvelope);
    }

    public AerodynamicProperties GetAerodynamicProperties()
    {
        StageData activeStage = GetActiveStage();
        double dragCoeff = activeStage == null ? 0.25 : activeStage.dragCoefficient;
        double forwardArea = 0.0;
        double sideArea = 0.0;
        double topArea = 0.0;

        if (stages != null)
        {
            for (int i = 0; i < stages.Count; i++)
            {
                StageData stage = stages[i];

                if (stage.blocks == null)
                {
                    continue;
                }

                for (int b = 0; b < stage.blocks.Count; b++)
                {
                    ShipBlock block = stage.blocks[b];

                    if (!IsActiveBlock(block))
                    {
                        continue;
                    }

                    if (
                        block.blockType == ShipBlockType.RcsThruster ||
                        block.rcsThrust > 0.0 ||
                        block.blockType == ShipBlockType.DockingPort ||
                        block.isDockingPort
                    )
                    {
                        continue;
                    }

                    forwardArea += block.dimensions.x * block.dimensions.y;
                    sideArea += block.dimensions.z * block.dimensions.y;
                    topArea += block.dimensions.z * block.dimensions.x;
                }
            }
        }

        if (forwardArea <= 0.0)
        {
            forwardArea = 6.0;
            sideArea = 10.0;
            topArea = 8.0;
        }

        return new AerodynamicProperties
        {
            dragCoeff = dragCoeff,
            liftCoeff = 0.8,
            inducedDragFactor = 0.45,
            forwardArea = forwardArea,
            sideArea = sideArea,
            topArea = topArea,
            centerOfPressureOffset = centerOfPressureOffset,
            centerOfMassLocal = centerOfMassLocal
        };
    }

    public void ApplyCenterOfMassPositionCorrection(
        SimVector3 previousCenterOfMassLocal
    )
    {
        SimVector3 delta = orientation.Rotate(
            previousCenterOfMassLocal.Subtract(centerOfMassLocal)
        );
        position = position.Add(delta);
    }

    public SimVector3 GetCenterOfMassWorld()
    {
        return position.Add(orientation.Rotate(centerOfMassLocal));
    }

    public SimVector3 WorldFromVesselLocal(SimVector3 vesselLocalPosition)
    {
        SimVector3 offsetFromCom = vesselLocalPosition.Subtract(centerOfMassLocal);
        return GetCenterOfMassWorld().Add(orientation.Rotate(offsetFromCom));
    }

    public SimVector3 VesselLocalFromWorld(SimVector3 worldPosition)
    {
        SimVector3 offsetFromCom = orientation.InverseRotate(
            worldPosition.Subtract(GetCenterOfMassWorld())
        );
        return centerOfMassLocal.Add(offsetFromCom);
    }

    public SimVector3 DirectionWorldFromLocal(SimVector3 localDirection)
    {
        return orientation.Rotate(localDirection);
    }

    public SimVector3 DirectionLocalFromWorld(SimVector3 worldDirection)
    {
        return orientation.InverseRotate(worldDirection);
    }

    public SimVector3 GetDockingPortFaceWorldPosition()
    {
        if (!TryGetPrimaryDockingPortBlock(out ShipBlock block))
        {
            return GetDockingPortWorldPosition();
        }

        SimVector3 forward = GetDockingPortForwardWorld();
        double halfLength = block.dimensions.z * 0.5;
        return GetDockingPortWorldPosition().Add(forward.Multiply(halfLength));
    }

    void InvalidateBottomExtentCache()
    {
        cachedBottomExtentLayoutRevision = -1;
    }

    public double GetBottomExtentAlongWorldDirection(
        SimVector3 worldDirectionOutward
    )
    {
        EnsureStageBlocks();

        if (worldDirectionOutward.LengthSquared() <= 1e-18)
        {
            return collisionRadius;
        }

        SimVector3 outward = worldDirectionOutward.Normalize();

        if (
            cachedBottomExtentLayoutRevision == shipLayoutRevision &&
            SimVector3.Dot(cachedBottomExtentNormal, outward) > 0.999999
        )
        {
            return cachedBottomExtent;
        }

        if (
            VesselStackGrounding.TryMeasureBottomExtentAlongWorldDirection(
                this,
                outward,
                out double stackBottomExtent
            )
        )
        {
            cachedBottomExtent = stackBottomExtent;
            cachedBottomExtentNormal = outward;
            cachedBottomExtentLayoutRevision = shipLayoutRevision;
            return cachedBottomExtent;
        }

        double minProjection = double.MaxValue;
        bool hasExtent = false;

        if (stages == null)
        {
            return collisionRadius;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!IsActiveBlock(block))
                {
                    continue;
                }

                if (
                    block.blockType == ShipBlockType.RcsThruster ||
                    block.rcsThrust > 0.0 ||
                    block.blockType == ShipBlockType.DockingPort ||
                    block.isDockingPort
                )
                {
                    continue;
                }

                SimVector3 blockCenter = stage.stageMountOffset.Add(block.localOffset);
                SimVector3 halfExtents = new SimVector3(
                    block.dimensions.x * 0.5,
                    block.dimensions.y * 0.5,
                    block.dimensions.z * 0.5
                );

                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            SimVector3 cornerLocal = blockCenter.Add(
                                new SimVector3(
                                    halfExtents.x * sx,
                                    halfExtents.y * sy,
                                    halfExtents.z * sz
                                )
                            );
                            SimVector3 cornerFromCom = cornerLocal.Subtract(
                                centerOfMassLocal
                            );
                            SimVector3 cornerWorld = orientation.Rotate(
                                cornerFromCom
                            );
                            double projection = SimVector3.Dot(cornerWorld, outward);

                            if (!hasExtent || projection < minProjection)
                            {
                                minProjection = projection;
                                hasExtent = true;
                            }
                        }
                    }
                }
            }
        }

        if (!hasExtent)
        {
            return collisionRadius;
        }

        double bottomExtent = Math.Max(-minProjection, 0.0);
        cachedBottomExtent = LaunchPadConstants.GetEffectiveBottomExtentMeters(bottomExtent);
        cachedBottomExtentNormal = outward;
        cachedBottomExtentLayoutRevision = shipLayoutRevision;
        return cachedBottomExtent;
    }

    public void EnsureStageBlocks()
    {
        if (stages == null)
        {
            return;
        }

        int blockCountBefore = CountBlocks();
        StageData.PrepareStages(stages);
        int blockCountAfter = CountBlocks();

        if (blockCountBefore != blockCountAfter)
        {
            shipLayoutRevision++;
        }
    }

    public void NotifyShipLayoutChanged()
    {
        shipLayoutRevision++;
        InvalidateBottomExtentCache();
    }

    void SyncBlockFuelFromStages()
    {
        if (stages == null)
        {
            return;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            double totalTankCapacity = 0.0;

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (block.isFuelTank)
                {
                    totalTankCapacity += Math.Max(0.0, block.fuelCapacity);
                }
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!block.isFuelTank)
                {
                    continue;
                }

                if (block.fuelCapacity <= 0.0)
                {
                    block.fuelCapacity = stage.maxFuelMass;
                }

                if (totalTankCapacity > 0.0)
                {
                    double tankFraction = block.fuelCapacity / totalTankCapacity;
                    block.fuelMass = stage.fuelMass * tankFraction;
                }
                else
                {
                    block.fuelMass = stage.fuelMass;
                    block.fuelCapacity = stage.maxFuelMass;
                }
            }
        }
    }

    int CountBlocks()
    {
        int count = 0;

        if (stages == null)
        {
            return count;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks != null)
            {
                count += stage.blocks.Count;
            }
        }

        return count;
    }

    public ShipBlockView[] GetShipBlockViews()
    {
        if (stages == null || stages.Count == 0)
        {
            return Array.Empty<ShipBlockView>();
        }

        EnsureStageBlocks();

        List<ShipBlockView> views = new List<ShipBlockView>();

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (!IsActiveBlock(block))
                {
                    continue;
                }

                views.Add(
                    new ShipBlockView(
                        block.blockId,
                        block.id,
                        block.partTemplateId,
                        block.parentBlockId,
                        block.hasVabAttach,
                        block.attachLocalPosition,
                        block.attachLocalRotation,
                        block.localRotation,
                        block.blockType,
                        stage.stageMountOffset.Add(block.localOffset),
                        block.dimensions,
                        block.thrustLocalOffset,
                        i,
                        block.GetMass()
                    )
                );
            }
        }

        return views.ToArray();
    }

    public double GetMass()
    {
        return totalMass;
    }

    public double GetTotalDryMass()
    {
        double mass = 0.0;

        for (int i = 0; i < stages.Count; i++)
        {
            mass += stages[i].dryMass;
        }

        return mass;
    }

    public double GetTotalFuelMass()
    {
        double fuel = 0.0;

        for (int i = 0; i < stages.Count; i++)
        {
            fuel += stages[i].fuelMass;
        }

        return fuel;
    }

    public double GetTotalFuelCapacity()
    {
        double capacity = 0.0;

        for (int i = 0; i < stages.Count; i++)
        {
            capacity += stages[i].maxFuelMass;
        }

        return capacity;
    }

    public double GetFuelRatio()
    {
        double capacity = GetTotalFuelCapacity();

        if (capacity <= 0.0)
        {
            return 0.0;
        }

        return GetTotalFuelMass() / capacity;
    }

    public StageData GetActiveStage()
    {
        if (
            stages == null ||
            activeStageIndex < 0 ||
            activeStageIndex >= stages.Count
        )
        {
            return null;
        }

        return stages[activeStageIndex];
    }

    public double GetActiveStageThrust()
    {
        return GetActiveEngineThrustNewtons();
    }

    public double GetActiveFuelConsumptionPerForceSecond()
    {
        StageData activeStage = GetActiveStage();
        return activeStage == null ?
            0.0 :
            activeStage.GetFuelConsumptionPerForceSecond();
    }

    public double GetActiveStageFuelMass()
    {
        StageData activeStage = GetActiveStage();
        return activeStage == null ? 0.0 : activeStage.fuelMass;
    }

    public bool TryBurnPropellantForThrust(
        double thrustNewtons,
        double dt,
        out double burnScale,
        double localGravity
    )
    {
        burnScale = 0.0;

        if (thrustNewtons <= 0.0 || dt <= 0.0)
        {
            return false;
        }

        if (SimulationCheatFlags.InfiniteFuel)
        {
            burnScale = 1.0;
            return true;
        }

        StageData activeStage = GetActiveStage();

        if (activeStage == null)
        {
            return false;
        }

        double availableFuel = GetActiveStageTankFuelMass();

        if (availableFuel <= 0.0)
        {
            availableFuel = activeStage.fuelMass;
        }

        if (availableFuel <= 0.0)
        {
            return false;
        }

        double fuelRate = activeStage.GetFuelConsumptionPerForceSecond();

        if (fuelRate <= 0.0)
        {
            return false;
        }

        double requestedFuel = thrustNewtons * fuelRate * dt;
        burnScale = 1.0;

        if (requestedFuel > availableFuel)
        {
            burnScale = availableFuel / requestedFuel;
            requestedFuel = availableFuel;
        }

        if (!TryConsumeActiveStageFuel(requestedFuel))
        {
            burnScale = 0.0;
            return false;
        }

        RecalculateMassAndDeltaV(localGravity);
        return burnScale > 0.0;
    }

    double GetActiveStageTankFuelMass()
    {
        StageData activeStage = GetActiveStage();

        if (activeStage?.blocks == null)
        {
            return 0.0;
        }

        double tankFuel = 0.0;

        for (int i = 0; i < activeStage.blocks.Count; i++)
        {
            ShipBlock block = activeStage.blocks[i];

            if (block == null || block.isDestroyed || !block.isFuelTank)
            {
                continue;
            }

            tankFuel += Math.Max(0.0, block.fuelMass);
        }

        return tankFuel;
    }

    bool TryConsumeActiveStageFuel(double fuelAmount)
    {
        if (fuelAmount <= 0.0)
        {
            return false;
        }

        StageData activeStage = GetActiveStage();

        if (activeStage == null)
        {
            return false;
        }

        if (activeStage.blocks != null)
        {
            double tankFuel = 0.0;

            for (int i = 0; i < activeStage.blocks.Count; i++)
            {
                ShipBlock block = activeStage.blocks[i];

                if (block == null || block.isDestroyed || !block.isFuelTank)
                {
                    continue;
                }

                tankFuel += Math.Max(0.0, block.fuelMass);
            }

            if (tankFuel > 0.0)
            {
                double remaining = fuelAmount;

                for (int i = 0; i < activeStage.blocks.Count; i++)
                {
                    ShipBlock block = activeStage.blocks[i];

                    if (block == null || block.isDestroyed || !block.isFuelTank)
                    {
                        continue;
                    }

                    double blockFuel = Math.Max(0.0, block.fuelMass);

                    if (blockFuel <= 0.0)
                    {
                        continue;
                    }

                    double burnAmount = Math.Min(
                        blockFuel,
                        fuelAmount * (blockFuel / tankFuel)
                    );
                    block.fuelMass = Math.Max(0.0, blockFuel - burnAmount);
                    remaining -= burnAmount;
                }

                if (remaining > 1e-9)
                {
                    for (int i = 0; i < activeStage.blocks.Count && remaining > 1e-9; i++)
                    {
                        ShipBlock block = activeStage.blocks[i];

                        if (block == null || block.isDestroyed || !block.isFuelTank)
                        {
                            continue;
                        }

                        double blockFuel = Math.Max(0.0, block.fuelMass);

                        if (blockFuel <= 0.0)
                        {
                            continue;
                        }

                        double burnAmount = Math.Min(blockFuel, remaining);
                        block.fuelMass = Math.Max(0.0, blockFuel - burnAmount);
                        remaining -= burnAmount;
                    }
                }

                return remaining <= 1e-6;
            }
        }

        if (activeStage.fuelMass + 1e-6 < fuelAmount)
        {
            return false;
        }

        activeStage.fuelMass = Math.Max(0.0, activeStage.fuelMass - fuelAmount);
        return true;
    }

    public int GetRemainingStageCount()
    {
        return Math.Max(0, stages.Count - activeStageIndex);
    }

    public int GetActiveStageNumber()
    {
        return activeStageIndex + 1;
    }

    public double GetAvailableDeltaV()
    {
        return activeStageDeltaV;
    }

    public static List<StageData> CopyStages(List<StageData> source)
    {
        if (source == null)
        {
            return new List<StageData>();
        }

        List<StageData> copy = new List<StageData>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            copy.Add(source[i].Copy());
        }

        StageData.PrepareStages(copy);
        return copy;
    }
}
