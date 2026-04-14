using System;
using System.Collections.Generic;

class Simulation
{
    const double OrbitPathNearCircularEccentricity = 0.001;

    public Body planet;
    public Body moon;
    public Vessel ship;
    public Body[] bodies;
    public SimulationScenario scenario;

    public Vector2 lastGravityAcceleration;
    public Vector2 lastMoonGravityAcceleration;
    public double circularSpeedAtStart;
    public double moonCircularSpeedAtStart;
    public double moonSphereOfInfluenceRadius;
    public double minShipDistanceToPlanet;
    public double maxShipDistanceToPlanet;
    public double initialShipSpecificEnergy;
    public double initialShipAngularMomentum;
    public string activePrimaryBodyName;

    public Simulation(SimulationScenario scenario)
    {
        this.scenario = scenario;

        planet = new Body(
            "Planet",
            Vector2.Zero(),
            Vector2.Zero(),
            Vector2.Zero(),
            400000,
            20,
            true
        );

        double moonOrbitRadius = 650;
        double moonSphereOfInfluenceReferenceRadius = moonOrbitRadius;
        Vector2 moonStartPosition = new Vector2(moonOrbitRadius, 0);
        double moonStartRadius = planet.position.Subtract(moonStartPosition).Length();
        moonCircularSpeedAtStart = Gravity.CalculateCircularOrbitSpeed(
            planet.mu,
            moonStartRadius
        );
        Vector2 moonStartVelocity = new Vector2(0, moonCircularSpeedAtStart);

        if (scenario == SimulationScenario.MoonFlyby)
        {
            // Ставим луну в фазу, удобную для первого учебного пролета корабля.
            double moonStartAngle = -2.17;
            moonStartPosition = new Vector2(
                moonOrbitRadius * Math.Cos(moonStartAngle),
                moonOrbitRadius * Math.Sin(moonStartAngle)
            );

            moonStartVelocity = new Vector2(
                -Math.Sin(moonStartAngle),
                Math.Cos(moonStartAngle)
            ).Multiply(moonCircularSpeedAtStart);
        }

        if (scenario == SimulationScenario.PlanetMoonEllipticMoon)
        {
            double moonOrbitSemiMajorAxis = moonOrbitRadius;
            double moonOrbitEccentricity = 0.12;
            double moonPeriapsisRadius =
                moonOrbitSemiMajorAxis * (1.0 - moonOrbitEccentricity);
            double moonPeriapsisSpeed = Math.Sqrt(
                planet.mu *
                (
                    (2.0 / moonPeriapsisRadius) -
                    (1.0 / moonOrbitSemiMajorAxis)
                )
            );

            moonStartPosition = new Vector2(moonPeriapsisRadius, 0);
            moonStartVelocity = new Vector2(0, moonPeriapsisSpeed);
            moonStartRadius = moonPeriapsisRadius;
            moonSphereOfInfluenceReferenceRadius = moonOrbitSemiMajorAxis;
        }

        moon = new Body(
            "Moon",
            moonStartPosition,
            moonStartVelocity,
            Vector2.Zero(),
            70000,
            6,
            false
        );
        moonSphereOfInfluenceRadius = CalculateSphereOfInfluenceRadius(
            moonSphereOfInfluenceReferenceRadius,
            moon.mu,
            planet.mu
        );

        Vector2 shipStartPosition = new Vector2(-220, 0);
        double shipStartRadius = planet.position.Subtract(shipStartPosition).Length();
        circularSpeedAtStart = Gravity.CalculateCircularOrbitSpeed(
            planet.mu,
            shipStartRadius
        );
        Vector2 shipStartVelocity = new Vector2(0, circularSpeedAtStart);

        if (scenario == SimulationScenario.MoonFlyby)
        {
            // Эллиптический перелет: перицентр у старта корабля, апоцентр около орбиты луны.
            double transferSemiMajorAxis = (shipStartRadius + moonOrbitRadius) * 0.5;
            double transferSpeedAtPeriapsis = Math.Sqrt(
                planet.mu *
                (
                    (2.0 / shipStartRadius) -
                    (1.0 / transferSemiMajorAxis)
                )
            );

            shipStartVelocity = new Vector2(0, transferSpeedAtPeriapsis);
        }

        ship = new Vessel(
            "Ship",
            shipStartPosition,
            shipStartVelocity
        );

        if (
            ScenarioHasMoon()
        )
        {
            bodies = new Body[]
            {
                planet,
                moon,
                ship
            };
        }
        else
        {
            bodies = new Body[]
            {
                planet,
                ship
            };
        }

        lastGravityAcceleration = Vector2.Zero();
        lastMoonGravityAcceleration = Vector2.Zero();
        minShipDistanceToPlanet = shipStartRadius;
        maxShipDistanceToPlanet = shipStartRadius;
        initialShipSpecificEnergy = GetShipSpecificOrbitalEnergyRelativeToPlanet();
        initialShipAngularMomentum = GetShipAngularMomentumRelativeToPlanet();
        activePrimaryBodyName = GetActivePrimaryBody().name;
    }

    public void SetShipThrust(Vector2 thrust)
    {
        ship.thrust = thrust;
    }

    public void ApplyShipDeltaV(Vector2 deltaV)
    {
        ship.velocity = ship.velocity.Add(deltaV);
    }

    bool ScenarioHasMoon()
    {
        return
            scenario == SimulationScenario.PlanetMoonShip ||
            scenario == SimulationScenario.PlanetMoonEllipticMoon ||
            scenario == SimulationScenario.MoonFlyby;
    }

    public Simulation CreateCopy()
    {
        Simulation copy = new Simulation(scenario);

        copy.planet = CopyBody(planet);
        copy.moon = CopyBody(moon);
        copy.ship = CopyVessel(ship);

        if (
            ScenarioHasMoon()
        )
        {
            copy.bodies = new Body[]
            {
                copy.planet,
                copy.moon,
                copy.ship
            };
        }
        else
        {
            copy.bodies = new Body[]
            {
                copy.planet,
                copy.ship
            };
        }

        copy.lastGravityAcceleration = lastGravityAcceleration;
        copy.lastMoonGravityAcceleration = lastMoonGravityAcceleration;
        copy.circularSpeedAtStart = circularSpeedAtStart;
        copy.moonCircularSpeedAtStart = moonCircularSpeedAtStart;
        copy.moonSphereOfInfluenceRadius = moonSphereOfInfluenceRadius;
        copy.minShipDistanceToPlanet = minShipDistanceToPlanet;
        copy.maxShipDistanceToPlanet = maxShipDistanceToPlanet;
        copy.initialShipSpecificEnergy = initialShipSpecificEnergy;
        copy.initialShipAngularMomentum = initialShipAngularMomentum;
        copy.activePrimaryBodyName = activePrimaryBodyName;

        return copy;
    }

    Body CopyBody(Body body)
    {
        return new Body(
            body.name,
            body.position,
            body.velocity,
            body.acceleration,
            body.mu,
            body.radius,
            body.isStatic
        );
    }

    Vessel CopyVessel(Vessel vessel)
    {
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
        copy.dryMass = vessel.dryMass;
        copy.fuelMass = vessel.fuelMass;
        copy.fuelCapacity = vessel.fuelCapacity;
        copy.fuelConsumptionPerForceSecond =
            vessel.fuelConsumptionPerForceSecond;

        return copy;
    }

    public Vector2[] GetShipOrbitPathRelativeToActivePrimary(int pointCount)
    {
        return GetOrbitPathRelativeToBody(
            GetActivePrimaryBody(),
            ship.position,
            ship.velocity,
            pointCount
        );
    }

    public Vector2[] GetPreviewShipOrbitPathRelativeToActivePrimary(
        Vector2 plannedDeltaV,
        int pointCount
    )
    {
        return GetOrbitPathRelativeToBody(
            GetActivePrimaryBody(),
            ship.position,
            ship.velocity.Add(plannedDeltaV),
            pointCount
        );
    }

    public Vector2[] GetPreviewShipOrbitPathRelativeToActivePrimary(
        Vector2 plannedDeltaV,
        int pointCount,
        double maneuverTime,
        double dt
    )
    {
        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();
        AdvancePreviewToTime(preview, maneuverTime, dt);
        preview.ApplyShipDeltaV(plannedDeltaV);

        return preview.GetShipOrbitPathRelativeToActivePrimary(pointCount);
    }

    public Vector2 GetShipPositionAtTime(double time, double dt)
    {
        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();
        AdvancePreviewToTime(preview, time, dt);
        return preview.ship.position;
    }

    public Vector2[] GetFiniteBurnPreviewShipOrbitPathRelativeToActivePrimary(
        Vector2 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        int pointCount,
        double maneuverTime,
        double coastDt,
        double burnDt
    )
    {
        if (plannedDeltaV.LengthSquared() <= 0.0 || thrustForce <= 0.0)
        {
            Simulation coastPreview = CreateCopy();
            coastPreview.ship.thrust = Vector2.Zero();
            AdvancePreviewToTime(coastPreview, maneuverTime, coastDt);
            return coastPreview.GetShipOrbitPathRelativeToActivePrimary(pointCount);
        }

        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();
        AdvancePreviewToTime(preview, maneuverTime, coastDt);
        preview.ship.thrust = plannedDeltaV.Normalize().Multiply(thrustForce);
        AdvancePreviewToTime(preview, burnDuration, burnDt);
        preview.ship.thrust = Vector2.Zero();

        return preview.GetShipOrbitPathRelativeToActivePrimary(pointCount);
    }

    public double GetFiniteBurnPreviewShipOrbitalPeriodRelativeToActivePrimary(
        Vector2 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double maneuverTime,
        double coastDt,
        double burnDt
    )
    {
        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();
        AdvancePreviewToTime(preview, maneuverTime, coastDt);

        if (
            plannedDeltaV.LengthSquared() > 0.0 &&
            thrustForce > 0.0 &&
            burnDuration > 0.0
        )
        {
            preview.ship.thrust = plannedDeltaV
                .Normalize()
                .Multiply(thrustForce);
            AdvancePreviewToTime(preview, burnDuration, burnDt);
            preview.ship.thrust = Vector2.Zero();
        }

        return preview.GetShipOrbitalPeriodRelativeToActivePrimary();
    }

    public double EstimateShipBurnDurationForDeltaV(
        Vector2 deltaV,
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

        double fuelRate =
            thrustForce * ship.fuelConsumptionPerForceSecond;

        if (fuelRate <= 0.0)
        {
            return deltaVLength / (thrustForce / ship.GetMass());
        }

        double startMass = ship.GetMass();
        double endMassAfterRequestedDeltaV =
            startMass / Math.Exp(
                deltaVLength * ship.fuelConsumptionPerForceSecond
            );
        double lowestPossibleMass = ship.dryMass;

        if (endMassAfterRequestedDeltaV < lowestPossibleMass)
        {
            endMassAfterRequestedDeltaV = lowestPossibleMass;
        }

        double fuelToBurn = startMass - endMassAfterRequestedDeltaV;

        if (fuelToBurn <= 0.0)
        {
            return 0.0;
        }

        if (fuelToBurn > ship.fuelMass)
        {
            fuelToBurn = ship.fuelMass;
        }

        return fuelToBurn / fuelRate;
    }

    public double GetShipAvailableDeltaV()
    {
        double currentMass = ship.GetMass();

        if (
            currentMass <= 0.0 ||
            ship.dryMass <= 0.0 ||
            currentMass <= ship.dryMass ||
            ship.fuelConsumptionPerForceSecond <= 0.0
        )
        {
            return 0.0;
        }

        return Math.Log(currentMass / ship.dryMass) /
            ship.fuelConsumptionPerForceSecond;
    }

    Vector2 ClampDeltaVToAvailableFuel(Vector2 deltaV)
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

    public Vector2 GetFiniteBurnCircularizeDeltaVRelativeToActivePrimary(
        double thrustForce,
        double circularizeTime,
        double burnStartTime,
        double coastDt,
        double burnDt,
        int iterationCount
    )
    {
        Simulation circularizePreview = CreateCopy();
        circularizePreview.ship.thrust = Vector2.Zero();
        AdvancePreviewToTime(circularizePreview, circularizeTime, coastDt);
        string primaryBodyName =
            circularizePreview.GetActivePrimaryBody().name;
        Body circularizePrimaryBody =
            circularizePreview.GetBodyByName(primaryBodyName);
        Vector2 candidateDeltaV =
            circularizePreview.GetShipCircularizeDeltaVRelativeToBody(
                circularizePrimaryBody
            );

        if (
            candidateDeltaV.LengthSquared() <= 0.0 ||
            thrustForce <= 0.0 ||
            iterationCount <= 0
        )
        {
            return candidateDeltaV;
        }

        for (int i = 0; i < iterationCount; i++)
        {
            Simulation preview = CreateCopy();
            preview.ship.thrust = Vector2.Zero();
            AdvancePreviewToTime(preview, burnStartTime, coastDt);

            double burnDuration = preview.EstimateShipBurnDurationForDeltaV(
                candidateDeltaV,
                thrustForce
            );

            if (
                burnDuration <= 0.0 ||
                double.IsInfinity(burnDuration) ||
                double.IsNaN(burnDuration)
            )
            {
                break;
            }

            preview.ship.thrust =
                candidateDeltaV.Normalize().Multiply(thrustForce);
            AdvancePreviewToTime(preview, burnDuration, burnDt);
            preview.ship.thrust = Vector2.Zero();

            Vector2 remainingDeltaV =
                preview.GetShipCircularizeDeltaVRelativeToBody(
                    preview.GetBodyByName(primaryBodyName)
                );

            if (remainingDeltaV.LengthSquared() < 0.000001)
            {
                break;
            }

            candidateDeltaV = candidateDeltaV.Add(remainingDeltaV);
            candidateDeltaV = ClampDeltaVToAvailableFuel(candidateDeltaV);
        }

        return ClampDeltaVToAvailableFuel(candidateDeltaV);
    }

    public ClosestApproachInfo GetClosestApproachToMoon(
        Vector2 plannedDeltaV,
        int stepCount,
        double dt
    )
    {
        return GetClosestApproachToBody("Moon", plannedDeltaV, stepCount, dt);
    }

    public ClosestApproachInfo GetClosestApproachToBody(
        string targetBodyName,
        Vector2 plannedDeltaV,
        int stepCount,
        double dt
    )
    {
        return GetClosestApproachToBody(
            targetBodyName,
            plannedDeltaV,
            stepCount,
            dt,
            0
        );
    }

    public ClosestApproachInfo GetClosestApproachToBody(
        string targetBodyName,
        Vector2 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime
    )
    {
        return GetClosestApproachToBody(
            targetBodyName,
            plannedDeltaV,
            stepCount,
            dt,
            maneuverTime,
            0.0,
            0.0,
            dt
        );
    }

    public ClosestApproachInfo GetClosestApproachToBody(
        string targetBodyName,
        Vector2 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime,
        double thrustForce,
        double burnDuration,
        double burnDt
    )
    {
        if (!HasBody(targetBodyName) || targetBodyName == ship.name)
        {
            return ClosestApproachInfo.None();
        }

        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();
        Body targetBody = preview.GetBodyByName(targetBodyName);

        double closestDistance = double.PositiveInfinity;
        double closestTime = 0;
        Vector2 closestShipPosition = preview.ship.position;
        Vector2 closestTargetPosition = targetBody.position;
        double closestRelativeSpeed = preview.ship.velocity
            .Subtract(targetBody.velocity)
            .Length();

        for (int i = 0; i <= stepCount; i++)
        {
            double previewTime = i * dt;

            double distance = targetBody.position
                .Subtract(preview.ship.position)
                .Length();

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTime = i * dt;
                closestShipPosition = preview.ship.position;
                closestTargetPosition = targetBody.position;
                closestRelativeSpeed = preview.ship.velocity
                    .Subtract(targetBody.velocity)
                    .Length();
            }

            AdvancePreviewWithFiniteBurn(
                preview,
                plannedDeltaV,
                thrustForce,
                burnDuration,
                maneuverTime,
                previewTime,
                dt,
                burnDt
            );
        }

        return new ClosestApproachInfo(
            true,
            closestDistance,
            closestTime,
            closestShipPosition,
            closestTargetPosition,
            closestRelativeSpeed,
            targetBody.name,
            targetBody == preview.moon &&
                closestDistance <= preview.moonSphereOfInfluenceRadius
        );
    }

    public SphereOfInfluenceEntryPreview GetMoonSphereOfInfluenceEntryPreview(
        Vector2 plannedDeltaV,
        int stepCount,
        double dt
    )
    {
        return GetMoonSphereOfInfluenceEntryPreview(
            plannedDeltaV,
            stepCount,
            dt,
            0
        );
    }

    public SphereOfInfluenceEntryPreview GetMoonSphereOfInfluenceEntryPreview(
        Vector2 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime
    )
    {
        return GetMoonSphereOfInfluenceEntryPreview(
            plannedDeltaV,
            stepCount,
            dt,
            maneuverTime,
            0.0,
            0.0,
            dt
        );
    }

    public SphereOfInfluenceEntryPreview GetMoonSphereOfInfluenceEntryPreview(
        Vector2 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime,
        double thrustForce,
        double burnDuration,
        double burnDt
    )
    {
        if (!ScenarioHasMoon())
        {
            return SphereOfInfluenceEntryPreview.None();
        }

        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();

        List<Vector2> points = new List<Vector2>();
        Vector2 previousShipPosition = preview.ship.position;
        Vector2 previousMoonPosition = preview.moon.position;
        double previousDistanceToMoon = preview.GetShipDistanceToMoon();
        points.Add(previousShipPosition);

        if (previousDistanceToMoon <= preview.moonSphereOfInfluenceRadius)
        {
            Vector2[] moonOrbitPathPoints = preview.GetOrbitPathRelativeToBody(
                preview.moon,
                preview.ship.position,
                preview.ship.velocity,
                240
            );

            return new SphereOfInfluenceEntryPreview(
                true,
                0,
                preview.ship.position,
                points.ToArray(),
                moonOrbitPathPoints
            );
        }

        for (int i = 1; i <= stepCount; i++)
        {
            double previewTime = (i - 1) * dt;

            AdvancePreviewWithFiniteBurn(
                preview,
                plannedDeltaV,
                thrustForce,
                burnDuration,
                maneuverTime,
                previewTime,
                dt,
                burnDt
            );

            double distanceToMoon = preview.GetShipDistanceToMoon();

            if (distanceToMoon <= preview.moonSphereOfInfluenceRadius)
            {
                double entryFraction = CalculateSphereOfInfluenceEntryFraction(
                    previousDistanceToMoon,
                    distanceToMoon,
                    preview.moonSphereOfInfluenceRadius
                );
                Vector2 entryPosition = Lerp(
                    previousShipPosition,
                    preview.ship.position,
                    entryFraction
                );
                Body entryMoon = CopyBody(preview.moon);
                entryMoon.position = Lerp(
                    previousMoonPosition,
                    preview.moon.position,
                    entryFraction
                );

                points.Add(entryPosition);

                Vector2[] moonOrbitPathPoints = preview.GetOrbitPathRelativeToBody(
                    entryMoon,
                    entryPosition,
                    preview.ship.velocity,
                    240
                );

                return new SphereOfInfluenceEntryPreview(
                    true,
                    (i - 1 + entryFraction) * dt,
                    entryPosition,
                    points.ToArray(),
                    moonOrbitPathPoints
                );
            }

            points.Add(preview.ship.position);
            previousShipPosition = preview.ship.position;
            previousMoonPosition = preview.moon.position;
            previousDistanceToMoon = distanceToMoon;
        }

        return new SphereOfInfluenceEntryPreview(
            false,
            0,
            Vector2.Zero(),
            Array.Empty<Vector2>(),
            Array.Empty<Vector2>()
        );
    }

    double CalculateSphereOfInfluenceEntryFraction(
        double previousDistance,
        double currentDistance,
        double sphereOfInfluenceRadius
    )
    {
        double distanceChange = previousDistance - currentDistance;

        if (Math.Abs(distanceChange) < 0.000001)
        {
            return 1.0;
        }

        return Clamp01((previousDistance - sphereOfInfluenceRadius) / distanceChange);
    }

    double Clamp01(double value)
    {
        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }

    Vector2 Lerp(Vector2 a, Vector2 b, double t)
    {
        return a.Add(b.Subtract(a).Multiply(t));
    }

    void AdvancePreviewToTime(Simulation preview, double time, double dt)
    {
        double remainingTime = time;

        while (remainingTime > 0.0)
        {
            double stepDt = dt > 0.0 ? Math.Min(dt, remainingTime) : remainingTime;
            preview.Update(stepDt);
            remainingTime -= stepDt;
        }
    }

    void AdvancePreviewWithFiniteBurn(
        Simulation preview,
        Vector2 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double maneuverTime,
        double previewTime,
        double stepDt,
        double burnDt
    )
    {
        double remainingTime = stepDt;
        double currentTime = previewTime;
        bool canBurn =
            plannedDeltaV.LengthSquared() > 0.0 &&
            thrustForce > 0.0 &&
            burnDuration > 0.0;
        double burnEndTime = maneuverTime + burnDuration;

        while (remainingTime > 0.0)
        {
            bool isBurning =
                canBurn &&
                currentTime >= maneuverTime &&
                currentTime < burnEndTime;
            double subStep = remainingTime;

            if (canBurn && currentTime < maneuverTime)
            {
                subStep = Math.Min(subStep, maneuverTime - currentTime);
            }

            if (isBurning)
            {
                subStep = Math.Min(subStep, burnEndTime - currentTime);

                if (burnDt > 0.0)
                {
                    subStep = Math.Min(subStep, burnDt);
                }
            }

            if (subStep <= 0.0)
            {
                subStep = remainingTime;
            }

            preview.ship.thrust = isBurning ?
                plannedDeltaV.Normalize().Multiply(thrustForce) :
                Vector2.Zero();
            preview.Update(subStep);

            currentTime += subStep;
            remainingTime -= subStep;
        }

        preview.ship.thrust = Vector2.Zero();
    }

    Vector2[] GetOrbitPathRelativeToBody(
        Body primaryBody,
        Vector2 orbitingPosition,
        Vector2 orbitingVelocity,
        int pointCount
    )
    {
        if (pointCount < 2 || primaryBody.mu <= 0)
        {
            return Array.Empty<Vector2>();
        }

        Vector2 relativePosition = orbitingPosition.Subtract(primaryBody.position);
        Vector2 relativeVelocity = orbitingVelocity.Subtract(primaryBody.velocity);
        double radius = relativePosition.Length();

        if (radius == 0)
        {
            return Array.Empty<Vector2>();
        }

        double angularMomentum =
            relativePosition.x * relativeVelocity.y -
            relativePosition.y * relativeVelocity.x;

        if (Math.Abs(angularMomentum) < 0.000001)
        {
            return Array.Empty<Vector2>();
        }

        double speedSquared = relativeVelocity.LengthSquared();
        double radialVelocity = Dot(relativePosition, relativeVelocity);
        Vector2 eccentricityVector = relativePosition
            .Multiply((speedSquared - primaryBody.mu / radius) / primaryBody.mu)
            .Subtract(relativeVelocity.Multiply(radialVelocity / primaryBody.mu));
        double eccentricity = eccentricityVector.Length();
        double semiLatusRectum =
            angularMomentum * angularMomentum / primaryBody.mu;

        if (semiLatusRectum <= 0)
        {
            return Array.Empty<Vector2>();
        }

        double pathEccentricity =
            eccentricity < OrbitPathNearCircularEccentricity ?
            0.0 :
            eccentricity;
        Vector2 periapsisDirection =
            pathEccentricity > 0.0 ?
            eccentricityVector.Normalize() :
            relativePosition.Normalize();
        Vector2 sideDirection = new Vector2(
            -periapsisDirection.y,
            periapsisDirection.x
        );

        if (angularMomentum < 0)
        {
            sideDirection = sideDirection.Multiply(-1);
        }

        List<Vector2> points = new List<Vector2>();

        if (pathEccentricity < 1.0)
        {
            for (int i = 0; i <= pointCount; i++)
            {
                double trueAnomaly =
                    (Math.PI * 2.0 * i) / pointCount;
                AddOrbitPathPoint(
                    points,
                    primaryBody.position,
                    periapsisDirection,
                    sideDirection,
                    semiLatusRectum,
                    pathEccentricity,
                    trueAnomaly
                );
            }
        }
        else
        {
            double maxTrueAnomaly = pathEccentricity == 1.0 ?
                Math.PI * 0.85 :
                Math.Acos(-1.0 / pathEccentricity) * 0.92;

            for (int i = 0; i < pointCount; i++)
            {
                double t = pointCount == 1 ?
                    0 :
                    (double)i / (pointCount - 1);
                double trueAnomaly =
                    -maxTrueAnomaly + 2.0 * maxTrueAnomaly * t;
                AddOrbitPathPoint(
                    points,
                    primaryBody.position,
                    periapsisDirection,
                    sideDirection,
                    semiLatusRectum,
                    pathEccentricity,
                    trueAnomaly
                );
            }
        }

        return points.ToArray();
    }

    void AddOrbitPathPoint(
        List<Vector2> points,
        Vector2 primaryPosition,
        Vector2 periapsisDirection,
        Vector2 sideDirection,
        double semiLatusRectum,
        double eccentricity,
        double trueAnomaly
    )
    {
        double denominator = 1.0 + eccentricity * Math.Cos(trueAnomaly);

        if (denominator <= 0.000001)
        {
            return;
        }

        double radius = semiLatusRectum / denominator;
        Vector2 direction = periapsisDirection
            .Multiply(Math.Cos(trueAnomaly))
            .Add(sideDirection.Multiply(Math.Sin(trueAnomaly)));

        points.Add(primaryPosition.Add(direction.Multiply(radius)));
    }

    double Dot(Vector2 a, Vector2 b)
    {
        return a.x * b.x + a.y * b.y;
    }

    // GetBodyStates = получить снимок состояний тел для внешнего слоя, например будущего Unity-адаптера.
    public BodyState[] GetBodyStates()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        BodyState[] states = new BodyState[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            states[i] = new BodyState(
                bodies[i].name,
                bodies[i].position,
                bodies[i].velocity,
                bodies[i].isStatic,
                bodies[i] == activePrimaryBody
            );
        }

        return states;
    }

    // Update = обновить состояние симуляции на один шаг времени.
    public void Update(double dt)
    {
        // Порядок шага оставляем строго таким:
        // 1. CalculateAccelerations()
        // 2. StepPosition()
        // 3. CalculateAccelerations()
        // 4. StepVelocity()
        Vector2[] oldAccelerations = CalculateAccelerations();

        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].acceleration = oldAccelerations[i];
            Integrator.StepPosition(bodies[i], oldAccelerations[i], dt);
        }

        Vector2[] newAccelerations = CalculateAccelerations();

        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].acceleration = newAccelerations[i];

            Integrator.StepVelocity(
                bodies[i],
                oldAccelerations[i],
                newAccelerations[i],
                dt
            );
        }

        ApplyShipThrust(dt);

        if (ScenarioHasMoon())
        {
            lastMoonGravityAcceleration = newAccelerations[1];
            lastGravityAcceleration = newAccelerations[2];
        }
        else
        {
            lastMoonGravityAcceleration = Vector2.Zero();
            lastGravityAcceleration = newAccelerations[1];
        }

        activePrimaryBodyName = GetActivePrimaryBody().name;
        UpdateOrbitStats();
    }

    void ApplyShipThrust(double dt)
    {
        ship.thrustAcceleration = Vector2.Zero();

        if (ship.thrust.LengthSquared() <= 0.0 || ship.fuelMass <= 0.0)
        {
            return;
        }

        double requestedFuel =
            ship.thrust.Length() *
            ship.fuelConsumptionPerForceSecond *
            dt;
        double burnScale = 1.0;

        if (requestedFuel > ship.fuelMass)
        {
            burnScale = ship.fuelMass / requestedFuel;
            requestedFuel = ship.fuelMass;
        }

        ship.fuelMass -= requestedFuel;

        double mass = ship.GetMass();

        if (mass <= 0.0)
        {
            return;
        }

        ship.thrustAcceleration = ship.thrust
            .Multiply(burnScale)
            .Multiply(1.0 / mass);
        ship.velocity = ship.velocity.Add(ship.thrustAcceleration.Multiply(dt));
    }

    // CalculateAccelerations = вычислить ускорения для всех тел.
    Vector2[] CalculateAccelerations()
    {
        Vector2[] accelerations = new Vector2[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            accelerations[i] = CalculateBodyAcceleration(bodies[i]);
        }

        return accelerations;
    }

    Vector2 CalculateBodyAcceleration(Body body)
    {
        if (body.isStatic)
        {
            return Vector2.Zero();
        }

        if (body == ship)
        {
            Body activePrimaryBody = GetActivePrimaryBody();
            Vector2 localGravityAcceleration = Gravity.CalculateAcceleration(
                ship.position,
                activePrimaryBody.position,
                activePrimaryBody.mu
            );

            return localGravityAcceleration.Add(
                CalculatePrimaryFrameAcceleration(activePrimaryBody)
            );
        }

        if (body == moon)
        {
            return Gravity.CalculateAcceleration(
                moon.position,
                planet.position,
                planet.mu
            );
        }

        return Vector2.Zero();
    }

    // UpdateOrbitStats = обновить статистику орбиты корабля.
    Vector2 CalculatePrimaryFrameAcceleration(Body primaryBody)
    {
        if (primaryBody == moon)
        {
            return Gravity.CalculateAcceleration(
                moon.position,
                planet.position,
                planet.mu
            );
        }

        return Vector2.Zero();
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
    Body GetActivePrimaryBody()
    {
        if (ScenarioHasMoon())
        {
            double distanceToMoon = GetShipDistanceToMoon();

            if (distanceToMoon <= moonSphereOfInfluenceRadius)
            {
                return moon;
            }
        }

        return planet;
    }

    // GetShipDistanceToActivePrimary = получить расстояние от корабля до текущего главного тела.
    public double GetShipDistanceToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        return activePrimaryBody.position.Subtract(ship.position).Length();
    }

    // GetShipAltitudeAboveActivePrimary = получить высоту корабля над поверхностью текущего главного тела.
    public double GetShipAltitudeAboveActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        return GetShipDistanceToActivePrimary() - activePrimaryBody.radius;
    }

    // GetShipAltitudeAbovePlanet = получить высоту корабля над поверхностью планеты.
    public double GetShipAltitudeAbovePlanet()
    {
        return GetShipDistanceToPlanet() - planet.radius;
    }

    // GetActivePrimaryPosition = получить позицию текущего главного тела для визуализации.
    public Vector2 GetActivePrimaryPosition()
    {
        return GetActivePrimaryBody().position;
    }

    // GetActivePrimaryRadius = получить радиус текущего главного тела.
    public double GetActivePrimaryRadius()
    {
        return GetActivePrimaryBody().radius;
    }

    // GetActivePrimarySphereOfInfluenceRadius = получить радиус текущей сферы влияния для активного тела.
    public double GetActivePrimarySphereOfInfluenceRadius()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == moon)
        {
            return moonSphereOfInfluenceRadius;
        }

        return double.PositiveInfinity;
    }

    // GetShipSpeed = получить скорость корабля.
    public double GetShipSpeed()
    {
        return ship.velocity.Length();
    }

    // GetShipAcceleration = получить текущее полное ускорение корабля.
    public double GetShipAcceleration()
    {
        return ship.acceleration.Add(ship.thrustAcceleration).Length();
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
        return ship.fuelMass;
    }

    public double GetShipFuelRatio()
    {
        return ship.GetFuelRatio();
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
        return planet.position.Subtract(ship.position).Length();
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

    Body GetBodyByName(string bodyName)
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

        return double.PositiveInfinity;
    }

    public double GetShipRelativeSpeedToBody(string bodyName)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].name == bodyName)
            {
                return ship.velocity.Subtract(bodies[i].velocity).Length();
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
        return GetShipSpecificOrbitalEnergyRelativeToBody(planet);
    }

    // GetShipAngularMomentumRelativeToPlanet = получить угловой момент корабля относительно планеты.
    public double GetShipAngularMomentumRelativeToPlanet()
    {
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
    public Vector2 GetShipProgradeDirectionRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        return ship.velocity.Subtract(activePrimaryBody.velocity).Normalize();
    }

    // GetShipRetrogradeDirectionRelativeToActivePrimary = получить направление против скорости относительно текущего главного тела.
    public Vector2 GetShipRetrogradeDirectionRelativeToActivePrimary()
    {
        return GetShipProgradeDirectionRelativeToActivePrimary().Multiply(-1);
    }

    public Vector2 GetShipProgradeDirectionRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();
        AdvancePreviewToTime(preview, time, dt);
        return preview.GetShipProgradeDirectionRelativeToActivePrimary();
    }

    public Vector2 GetShipRetrogradeDirectionRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        return GetShipProgradeDirectionRelativeToActivePrimaryAtTime(
            time,
            dt
        ).Multiply(-1);
    }

    public Vector2 GetShipCircularizeDeltaVRelativeToActivePrimary()
    {
        return GetShipCircularizeDeltaVRelativeToBody(GetActivePrimaryBody());
    }

    Vector2 GetShipCircularizeDeltaVRelativeToBody(Body activePrimaryBody)
    {
        Vector2 relativePosition = ship.position.Subtract(activePrimaryBody.position);
        Vector2 relativeVelocity = ship.velocity.Subtract(activePrimaryBody.velocity);
        double radius = relativePosition.Length();

        if (radius == 0 || activePrimaryBody.mu <= 0)
        {
            return Vector2.Zero();
        }

        double angularMomentum =
            relativePosition.x * relativeVelocity.y -
            relativePosition.y * relativeVelocity.x;
        Vector2 circularDirection = new Vector2(
            -relativePosition.y,
            relativePosition.x
        ).Normalize();

        if (angularMomentum < 0)
        {
            circularDirection = circularDirection.Multiply(-1);
        }

        double circularSpeed = Gravity.CalculateCircularOrbitSpeed(
            activePrimaryBody.mu,
            radius
        );
        Vector2 targetRelativeVelocity =
            circularDirection.Multiply(circularSpeed);

        return targetRelativeVelocity.Subtract(relativeVelocity);
    }

    public Vector2 GetShipCircularizeDeltaVRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();
        AdvancePreviewToTime(preview, time, dt);
        return preview.GetShipCircularizeDeltaVRelativeToActivePrimary();
    }

    // GetShipRadialOutDirectionRelativeToActivePrimary = получить направление от текущего главного тела наружу.
    public Vector2 GetShipRadialOutDirectionRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        return ship.position.Subtract(activePrimaryBody.position).Normalize();
    }

    // GetShipRadialInDirectionRelativeToActivePrimary = получить направление к текущему главному телу.
    public Vector2 GetShipRadialInDirectionRelativeToActivePrimary()
    {
        return GetShipRadialOutDirectionRelativeToActivePrimary().Multiply(-1);
    }

    public Vector2 GetShipRadialOutDirectionRelativeToActivePrimaryAtTime(
        double time,
        double dt
    )
    {
        Simulation preview = CreateCopy();
        preview.ship.thrust = Vector2.Zero();
        AdvancePreviewToTime(preview, time, dt);
        return preview.GetShipRadialOutDirectionRelativeToActivePrimary();
    }

    public Vector2 GetShipRadialInDirectionRelativeToActivePrimaryAtTime(
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

    double GetShipOrbitalEccentricityRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double specificEnergy = GetShipSpecificOrbitalEnergyRelativeToBody(body, shipVelocity);
        double angularMomentum = GetShipAngularMomentumRelativeToBody(body, shipVelocity);
        double eccentricitySquared =
            1.0 +
            (
                2.0 *
                specificEnergy *
                angularMomentum *
                angularMomentum
            ) /
            (body.mu * body.mu);

        if (eccentricitySquared < 0)
        {
            eccentricitySquared = 0;
        }

        return Math.Sqrt(eccentricitySquared);
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

    double GetShipSemiMajorAxisRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double specificEnergy = GetShipSpecificOrbitalEnergyRelativeToBody(body, shipVelocity);

        if (specificEnergy == 0)
        {
            return double.PositiveInfinity;
        }

        return -body.mu / (2.0 * specificEnergy);
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
        return GetShipPeriapsisRelativeToBody(activePrimaryBody) - activePrimaryBody.radius;
    }

    double GetShipPeriapsisRelativeToBody(Body body)
    {
        return GetShipPeriapsisRelativeToBody(body, ship.velocity);
    }

    double GetShipPeriapsisRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double semiMajorAxis = GetShipSemiMajorAxisRelativeToBody(body, shipVelocity);
        double eccentricity = GetShipOrbitalEccentricityRelativeToBody(body, shipVelocity);

        if (double.IsInfinity(semiMajorAxis))
        {
            return double.PositiveInfinity;
        }

        return semiMajorAxis * (1.0 - eccentricity);
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
        double apoapsis = GetShipApoapsisRelativeToBody(activePrimaryBody);

        if (double.IsInfinity(apoapsis))
        {
            return double.PositiveInfinity;
        }

        return apoapsis - activePrimaryBody.radius;
    }

    // GetPreviewShipOrbitTypeRelativeToActivePrimary = получить тип орбиты корабля после запланированного delta-v.
    public string GetPreviewShipOrbitTypeRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipOrbitTypeRelativeToBody(activePrimaryBody, previewVelocity);
    }

    // GetPreviewShipOrbitalEccentricityRelativeToActivePrimary = получить эксцентриситет орбиты корабля после запланированного delta-v.
    public double GetPreviewShipOrbitalEccentricityRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipOrbitalEccentricityRelativeToBody(activePrimaryBody, previewVelocity);
    }

    // GetPreviewShipSemiMajorAxisRelativeToActivePrimary = получить большую полуось орбиты корабля после запланированного delta-v.
    public double GetPreviewShipSemiMajorAxisRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
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

    public double GetPreviewShipOrbitalPeriodRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipOrbitalPeriodRelativeToBody(
            activePrimaryBody,
            previewVelocity
        );
    }

    double GetShipOrbitalPeriodRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double eccentricity =
            GetShipOrbitalEccentricityRelativeToBody(body, shipVelocity);
        double semiMajorAxis =
            GetShipSemiMajorAxisRelativeToBody(body, shipVelocity);

        if (
            eccentricity >= 1.0 ||
            semiMajorAxis <= 0 ||
            double.IsInfinity(semiMajorAxis) ||
            body.mu <= 0
        )
        {
            return double.PositiveInfinity;
        }

        return Math.PI * 2.0 * Math.Sqrt(
            semiMajorAxis * semiMajorAxis * semiMajorAxis / body.mu
        );
    }

    double GetShipTimeToOrbitalEventRelativeToBody(
        Body body,
        Vector2 shipVelocity,
        double targetMeanAnomaly
    )
    {
        Vector2 relativePosition = ship.position.Subtract(body.position);
        Vector2 relativeVelocity = shipVelocity.Subtract(body.velocity);
        double radius = relativePosition.Length();
        double semiMajorAxis = GetShipSemiMajorAxisRelativeToBody(
            body,
            shipVelocity
        );

        if (
            radius <= 0.0 ||
            body.mu <= 0.0 ||
            semiMajorAxis <= 0.0 ||
            double.IsInfinity(semiMajorAxis)
        )
        {
            return double.PositiveInfinity;
        }

        double speedSquared = relativeVelocity.LengthSquared();
        double radialVelocity = Dot(relativePosition, relativeVelocity);
        Vector2 eccentricityVector = relativePosition
            .Multiply((speedSquared - body.mu / radius) / body.mu)
            .Subtract(relativeVelocity.Multiply(radialVelocity / body.mu));
        double eccentricity = eccentricityVector.Length();

        if (eccentricity < 0.01 || eccentricity >= 1.0)
        {
            return double.PositiveInfinity;
        }

        double angularMomentum =
            relativePosition.x * relativeVelocity.y -
            relativePosition.y * relativeVelocity.x;
        Vector2 periapsisDirection = eccentricityVector.Normalize();
        Vector2 sideDirection = new Vector2(
            -periapsisDirection.y,
            periapsisDirection.x
        );

        if (angularMomentum < 0.0)
        {
            sideDirection = sideDirection.Multiply(-1);
        }

        double trueAnomaly = Math.Atan2(
            Dot(relativePosition, sideDirection),
            Dot(relativePosition, periapsisDirection)
        );
        double eccentricAnomaly = 2.0 * Math.Atan2(
            Math.Sqrt(1.0 - eccentricity) * Math.Sin(trueAnomaly * 0.5),
            Math.Sqrt(1.0 + eccentricity) * Math.Cos(trueAnomaly * 0.5)
        );
        double meanAnomaly = NormalizeAngle(
            eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly)
        );
        double target = NormalizeAngle(targetMeanAnomaly);
        double deltaMeanAnomaly = target - meanAnomaly;

        if (deltaMeanAnomaly < 0.0)
        {
            deltaMeanAnomaly += Math.PI * 2.0;
        }

        double meanMotion = Math.Sqrt(
            body.mu /
            (semiMajorAxis * semiMajorAxis * semiMajorAxis)
        );

        if (meanMotion <= 0.0)
        {
            return double.PositiveInfinity;
        }

        return deltaMeanAnomaly / meanMotion;
    }

    double NormalizeAngle(double angle)
    {
        double fullTurn = Math.PI * 2.0;
        double normalizedAngle = angle % fullTurn;

        if (normalizedAngle < 0.0)
        {
            normalizedAngle += fullTurn;
        }

        return normalizedAngle;
    }

    // GetPreviewShipPeriapsisRelativeToActivePrimary = получить перицентр орбиты корабля после запланированного delta-v.
    public double GetPreviewShipPeriapsisRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipPeriapsisRelativeToBody(activePrimaryBody, previewVelocity);
    }

    public double GetPreviewShipPeriapsisAltitudeRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipPeriapsisRelativeToBody(activePrimaryBody, previewVelocity) -
            activePrimaryBody.radius;
    }

    // GetPreviewShipApoapsisRelativeToActivePrimary = получить апоцентр орбиты корабля после запланированного delta-v.
    public double GetPreviewShipApoapsisRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipApoapsisRelativeToBody(activePrimaryBody, previewVelocity);
    }

    public double GetPreviewShipApoapsisAltitudeRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
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

    double GetShipApoapsisRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double semiMajorAxis = GetShipSemiMajorAxisRelativeToBody(body, shipVelocity);
        double eccentricity = GetShipOrbitalEccentricityRelativeToBody(body, shipVelocity);

        if (double.IsInfinity(semiMajorAxis) || eccentricity >= 1.0)
        {
            return double.PositiveInfinity;
        }

        return semiMajorAxis * (1.0 + eccentricity);
    }

    double GetShipSpecificOrbitalEnergyRelativeToBody(Body body)
    {
        return GetShipSpecificOrbitalEnergyRelativeToBody(body, ship.velocity);
    }

    double GetShipSpecificOrbitalEnergyRelativeToBody(Body body, Vector2 shipVelocity)
    {
        Vector2 relativePosition = ship.position.Subtract(body.position);
        Vector2 relativeVelocity = shipVelocity.Subtract(body.velocity);
        double radius = relativePosition.Length();
        double speed = relativeVelocity.Length();

        if (radius == 0)
        {
            return 0;
        }

        double kineticPart = 0.5 * speed * speed;
        double potentialPart = -body.mu / radius;

        return kineticPart + potentialPart;
    }

    string GetShipOrbitTypeRelativeToBody(Body body)
    {
        return GetShipOrbitTypeRelativeToBody(body, ship.velocity);
    }

    string GetShipOrbitTypeRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double specificEnergy = GetShipSpecificOrbitalEnergyRelativeToBody(body, shipVelocity);
        double epsilon = 0.000001;

        if (specificEnergy < -epsilon)
        {
            return "Ellipse";
        }

        if (specificEnergy > epsilon)
        {
            return "Hyperbola";
        }

        return "Parabola";
    }

    double GetShipEscapeSpeedRelativeToBody(Body body)
    {
        double radius = body.position.Subtract(ship.position).Length();

        if (radius == 0)
        {
            return 0;
        }

        return Math.Sqrt(2.0 * body.mu / radius);
    }

    double GetShipAngularMomentumRelativeToBody(Body body)
    {
        return GetShipAngularMomentumRelativeToBody(body, ship.velocity);
    }

    double GetShipAngularMomentumRelativeToBody(Body body, Vector2 shipVelocity)
    {
        Vector2 relativePosition = ship.position.Subtract(body.position);
        Vector2 relativeVelocity = shipVelocity.Subtract(body.velocity);

        return
            relativePosition.x * relativeVelocity.y -
            relativePosition.y * relativeVelocity.x;
    }
}

struct BodyState
{
    public string name;
    public Vector2 position;
    public Vector2 velocity;
    public bool isStatic;
    public bool isActivePrimary;

    public BodyState(
        string name,
        Vector2 position,
        Vector2 velocity,
        bool isStatic,
        bool isActivePrimary
    )
    {
        this.name = name;
        this.position = position;
        this.velocity = velocity;
        this.isStatic = isStatic;
        this.isActivePrimary = isActivePrimary;
    }
}

struct ClosestApproachInfo
{
    public bool hasApproach;
    public double distance;
    public double time;
    public Vector2 shipPosition;
    public Vector2 targetPosition;
    public double relativeSpeed;
    public string targetBodyName;
    public bool entersMoonSphereOfInfluence;

    public ClosestApproachInfo(
        bool hasApproach,
        double distance,
        double time,
        Vector2 shipPosition,
        Vector2 targetPosition,
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
            Vector2.Zero(),
            Vector2.Zero(),
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
    public Vector2 entryPosition;
    public Vector2[] pathPoints;
    public Vector2[] moonOrbitPathPoints;

    public SphereOfInfluenceEntryPreview(
        bool entersSphereOfInfluence,
        double entryTime,
        Vector2 entryPosition,
        Vector2[] pathPoints,
        Vector2[] moonOrbitPathPoints
    )
    {
        this.entersSphereOfInfluence = entersSphereOfInfluence;
        this.entryTime = entryTime;
        this.entryPosition = entryPosition;
        this.pathPoints = pathPoints;
        this.moonOrbitPathPoints = moonOrbitPathPoints;
    }

    public static SphereOfInfluenceEntryPreview None()
    {
        return new SphereOfInfluenceEntryPreview(
            false,
            0,
            Vector2.Zero(),
            Array.Empty<Vector2>(),
            Array.Empty<Vector2>()
        );
    }
}
