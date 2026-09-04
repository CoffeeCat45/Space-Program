using System;
using System.Collections.Generic;

class SimulationPreview
{
    readonly Simulation simulation;

    const int DescentPreviewMaxPoints = 180;
    const int DescentPreviewAtmosphereIntegratorStride = 8;
    const double DescentPreviewCoreStepDt = 0.02;
    const double DescentPreviewVacuumStepDt = 0.2;

    public SimulationPreview(Simulation simulation)
    {
        this.simulation = simulation;
    }

    public SimVector3[] GetPreviewShipOrbitPathRelativeToActivePrimary(
        SimVector3 plannedDeltaV,
        int pointCount
    )
    {
        return OrbitalMechanics.BuildOrbitPathRelativeToBody(
            simulation.GetActivePrimaryBody(),
            simulation.ship.position,
            simulation.ship.velocity.Add(plannedDeltaV),
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
        Simulation copy = simulation.CreateCopy();
        copy.ship.thrust = SimVector3.Zero();
        AdvancePreviewToTime(copy, maneuverTime, dt);
        copy.ApplyShipDeltaV(plannedDeltaV);

        return OrbitalMechanics.BuildOrbitPathRelativeToBody(
            copy.GetActivePrimaryBody(),
            copy.ship.position,
            copy.ship.velocity,
            pointCount
        );
    }

    public SimVector3 GetShipPositionAtTime(double time, double dt)
    {
        Simulation copy = simulation.CreateCopy();
        copy.ship.thrust = SimVector3.Zero();
        AdvancePreviewToTime(copy, time, dt);
        return copy.ship.position;
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
        if (plannedDeltaV.LengthSquared() <= 0.0 || thrustForce <= 0.0)
        {
            Simulation coastCopy = simulation.CreateCopy();
            coastCopy.ship.thrust = SimVector3.Zero();
            AdvancePreviewToTime(coastCopy, burnStartTime, coastDt);
            return OrbitalMechanics.BuildOrbitPathRelativeToBody(
                coastCopy.GetActivePrimaryBody(),
                coastCopy.ship.position,
                coastCopy.ship.velocity,
                pointCount
            );
        }

        Simulation copy = simulation.CreateCopy();
        ExecuteFiniteBurnPreview(
            copy,
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt
        );

        return OrbitalMechanics.BuildOrbitPathRelativeToBody(
            copy.GetActivePrimaryBody(),
            copy.ship.position,
            copy.ship.velocity,
            pointCount
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
        Simulation copy = simulation.CreateCopy();
        ExecuteFiniteBurnPreview(
            copy,
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt
        );

        return copy.GetShipOrbitalPeriodRelativeToActivePrimary();
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
        if (plannedDeltaV.LengthSquared() <= 0.0 || burnDuration <= 0.0)
        {
            return Array.Empty<SimVector3>();
        }

        Simulation copy = simulation.CreateCopy();
        List<SimVector3> points = new List<SimVector3>();
        ExecuteFiniteBurnPreview(
            copy,
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt,
            points
        );

        return points.ToArray();
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
        Simulation copy = simulation.CreateCopy();
        ExecuteFiniteBurnPreview(
            copy,
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt
        );

        position = copy.ship.position;
        velocity = copy.ship.velocity;
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
        if (
            !simulation.ScenarioHasMoon() ||
            plannedDeltaV.LengthSquared() <= 0.0 ||
            burnDuration <= 0.0
        )
        {
            return false;
        }

        Simulation copy = simulation.CreateCopy();
        string startPrimaryName = copy.GetActivePrimaryBody().name;

        if (
            ActivePrimaryChangesDuringFiniteBurn(
                copy,
                plannedDeltaV,
                thrustForce,
                burnDuration,
                burnStartTime,
                coastDt,
                burnDt,
                startPrimaryName
            )
        )
        {
            return true;
        }

        return false;
    }

    void ExecuteFiniteBurnPreview(
        Simulation copy,
        SimVector3 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double burnStartTime,
        double coastDt,
        double burnDt
    )
    {
        ExecuteFiniteBurnPreview(
            copy,
            plannedDeltaV,
            thrustForce,
            burnDuration,
            burnStartTime,
            coastDt,
            burnDt,
            null
        );
    }

    void ExecuteFiniteBurnPreview(
        Simulation copy,
        SimVector3 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double burnStartTime,
        double coastDt,
        double burnDt,
        List<SimVector3> pathPoints
    )
    {
        if (pathPoints != null)
        {
            pathPoints.Add(copy.ship.position);
        }

        double coastRemaining = Math.Max(0.0, burnStartTime);

        while (coastRemaining > 0.0)
        {
            double stepDt =
                coastDt > 0.0 ? Math.Min(coastDt, coastRemaining) : coastRemaining;
            copy.ship.thrust = SimVector3.Zero();
            copy.Update(stepDt);
            coastRemaining -= stepDt;

            if (pathPoints != null)
            {
                pathPoints.Add(copy.ship.position);
            }
        }

        if (
            plannedDeltaV.LengthSquared() <= 0.0 ||
            thrustForce <= 0.0 ||
            burnDuration <= 0.0
        )
        {
            copy.ship.thrust = SimVector3.Zero();
            return;
        }

        SimVector3 thrust = plannedDeltaV.Normalize().Multiply(thrustForce);
        double burnRemaining = burnDuration;

        while (burnRemaining > 0.0)
        {
            double stepDt =
                burnDt > 0.0 ? Math.Min(burnDt, burnRemaining) : burnRemaining;
            copy.ship.thrust = thrust;
            copy.Update(stepDt);
            burnRemaining -= stepDt;

            if (pathPoints != null)
            {
                pathPoints.Add(copy.ship.position);
            }
        }

        copy.ship.thrust = SimVector3.Zero();
    }

    static bool ActivePrimaryChangesDuringFiniteBurn(
        Simulation copy,
        SimVector3 plannedDeltaV,
        double thrustForce,
        double burnDuration,
        double burnStartTime,
        double coastDt,
        double burnDt,
        string startPrimaryName
    )
    {
        double coastRemaining = Math.Max(0.0, burnStartTime);

        while (coastRemaining > 0.0)
        {
            double stepDt =
                coastDt > 0.0 ? Math.Min(coastDt, coastRemaining) : coastRemaining;
            copy.ship.thrust = SimVector3.Zero();
            copy.Update(stepDt);
            coastRemaining -= stepDt;

            if (copy.GetActivePrimaryBody().name != startPrimaryName)
            {
                return true;
            }
        }

        if (thrustForce <= 0.0)
        {
            return false;
        }

        SimVector3 thrust = plannedDeltaV.Normalize().Multiply(thrustForce);
        double burnRemaining = burnDuration;

        while (burnRemaining > 0.0)
        {
            double stepDt =
                burnDt > 0.0 ? Math.Min(burnDt, burnRemaining) : burnRemaining;
            copy.ship.thrust = thrust;
            copy.Update(stepDt);
            burnRemaining -= stepDt;

            if (copy.GetActivePrimaryBody().name != startPrimaryName)
            {
                return true;
            }
        }

        return false;
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
        PreviewExecutionDriftInfo info = new PreviewExecutionDriftInfo
        {
            hasData = true,
            positionError = actualEndPosition
                .Subtract(previewEndPosition)
                .Length(),
            velocityError = actualEndVelocity
                .Subtract(previewEndVelocity)
                .Length(),
            maxPathDeviation = GetMaxPathDeviation(
                actualWorldPath,
                previewWorldPath
            )
        };

        return info;
    }

    static double GetMaxPathDeviation(
        SimVector3[] actualPath,
        SimVector3[] previewPath
    )
    {
        if (
            actualPath == null ||
            previewPath == null ||
            actualPath.Length == 0 ||
            previewPath.Length < 2
        )
        {
            return 0.0;
        }

        double maxDeviation = 0.0;

        for (int i = 0; i < actualPath.Length; i++)
        {
            double distance = GetMinDistanceToPolyline(
                actualPath[i],
                previewPath
            );

            if (distance > maxDeviation)
            {
                maxDeviation = distance;
            }
        }

        return maxDeviation;
    }

    static double GetMinDistanceToPolyline(SimVector3 point, SimVector3[] polyline)
    {
        double minDistance = double.PositiveInfinity;

        for (int i = 1; i < polyline.Length; i++)
        {
            double distance = GetDistanceToSegment(
                point,
                polyline[i - 1],
                polyline[i]
            );

            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    static double GetDistanceToSegment(SimVector3 point, SimVector3 a, SimVector3 b)
    {
        SimVector3 ab = b.Subtract(a);
        double lengthSquared = ab.LengthSquared();

        if (lengthSquared <= 0.000001)
        {
            return point.Subtract(a).Length();
        }

        double t = Clamp01(Dot(point.Subtract(a), ab) / lengthSquared);
        SimVector3 closest = a.Add(ab.Multiply(t));
        return point.Subtract(closest).Length();
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
        Simulation circularizeCopy = simulation.CreateCopy();
        circularizeCopy.ship.thrust = SimVector3.Zero();
        AdvancePreviewToTime(circularizeCopy, circularizeTime, coastDt);
        Body circularizePrimaryBody =
            circularizeCopy.GetActivePrimaryBody();
        SimVector3 candidateDeltaV =
            circularizeCopy.GetShipCircularizeDeltaVRelativeToBody(
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
            Simulation copy = simulation.CreateCopy();
            double burnDuration = copy.EstimateShipBurnDurationForDeltaV(
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

            ExecuteFiniteBurnPreview(
                copy,
                candidateDeltaV,
                thrustForce,
                burnDuration,
                burnStartTime,
                coastDt,
                burnDt
            );

            Body postBurnPrimary = copy.GetActivePrimaryBody();
            SimVector3 remainingDeltaV =
                copy.GetShipCircularizeDeltaVRelativeToBody(postBurnPrimary);

            if (remainingDeltaV.LengthSquared() < 0.000001)
            {
                break;
            }

            candidateDeltaV = candidateDeltaV.Add(remainingDeltaV);
            candidateDeltaV = simulation.ClampDeltaVToAvailableFuel(candidateDeltaV);
        }

        return simulation.ClampDeltaVToAvailableFuel(candidateDeltaV);
    }

    public ClosestApproachInfo GetClosestApproachToMoon(
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt
    )
    {
        return GetClosestApproachToBody("Moon", plannedDeltaV, stepCount, dt);
    }

    public ClosestApproachInfo GetClosestApproachToBody(
        string targetBodyName,
        SimVector3 plannedDeltaV,
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
        SimVector3 plannedDeltaV,
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
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime,
        double thrustForce,
        double burnDuration,
        double burnDt
    )
    {
        if (!simulation.HasBody(targetBodyName) || targetBodyName == simulation.ship.name)
        {
            return ClosestApproachInfo.None();
        }

        Simulation copy = simulation.CreateCopy();
        copy.ship.thrust = SimVector3.Zero();
        Body targetBody = copy.GetBodyByName(targetBodyName);

        double closestDistance = double.PositiveInfinity;
        double closestTime = 0;
        SimVector3 closestShipPosition = copy.ship.position;
        SimVector3 closestTargetPosition = targetBody.position;
        double closestRelativeSpeed = copy.ship.velocity
            .Subtract(targetBody.velocity)
            .Length();
        bool foundFutureApproach = false;

        for (int i = 0; i <= stepCount; i++)
        {
            double previewTime = i * dt;

            double distance = targetBody.position
                .Subtract(copy.ship.position)
                .Length();

            if (i > 0 && distance < closestDistance)
            {
                closestDistance = distance;
                closestTime = previewTime;
                closestShipPosition = copy.ship.position;
                closestTargetPosition = targetBody.position;
                closestRelativeSpeed = copy.ship.velocity
                    .Subtract(targetBody.velocity)
                    .Length();
                foundFutureApproach = true;
            }

            AdvancePreviewWithFiniteBurn(
                copy,
                plannedDeltaV,
                thrustForce,
                burnDuration,
                maneuverTime,
                previewTime,
                dt,
                burnDt
            );
        }

        if (!foundFutureApproach)
        {
            return ClosestApproachInfo.None();
        }

        return new ClosestApproachInfo(
            true,
            closestDistance,
            closestTime,
            closestShipPosition,
            closestTargetPosition,
            closestRelativeSpeed,
            targetBody.name,
            targetBody == copy.moon &&
                closestDistance <= copy.moonSphereOfInfluenceRadius
        );
    }

    public SphereOfInfluenceEntryPreview GetMoonSphereOfInfluenceEntryPreview(
        SimVector3 plannedDeltaV,
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
        SimVector3 plannedDeltaV,
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
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime,
        double thrustForce,
        double burnDuration,
        double burnDt
    )
    {
        if (!simulation.ScenarioHasMoon())
        {
            return SphereOfInfluenceEntryPreview.None();
        }

        Simulation copy = simulation.CreateCopy();
        copy.ship.thrust = SimVector3.Zero();

        List<SimVector3> points = new List<SimVector3>();
        SimVector3 previousShipPosition = copy.ship.position;
        SimVector3 previousMoonPosition = copy.moon.position;
        double previousDistanceToMoon = copy.GetShipDistanceToMoon();
        points.Add(previousShipPosition);

        if (previousDistanceToMoon <= copy.moonSphereOfInfluenceRadius)
        {
            SimVector3[] moonOrbitPathPoints = OrbitalMechanics.BuildOrbitPathRelativeToBody(
                copy.moon,
                copy.ship.position,
                copy.ship.velocity,
                240
            );

            return new SphereOfInfluenceEntryPreview(
                true,
                0,
                copy.ship.position,
                points.ToArray(),
                moonOrbitPathPoints
            );
        }

        for (int i = 1; i <= stepCount; i++)
        {
            double previewTime = (i - 1) * dt;

            AdvancePreviewWithFiniteBurn(
                copy,
                plannedDeltaV,
                thrustForce,
                burnDuration,
                maneuverTime,
                previewTime,
                dt,
                burnDt
            );

            double distanceToMoon = copy.GetShipDistanceToMoon();

            if (distanceToMoon <= copy.moonSphereOfInfluenceRadius)
            {
                double entryFraction = CalculateSphereOfInfluenceEntryFraction(
                    previousDistanceToMoon,
                    distanceToMoon,
                    copy.moonSphereOfInfluenceRadius
                );
                SimVector3 entryPosition = Lerp(
                    previousShipPosition,
                    copy.ship.position,
                    entryFraction
                );
                Body entryMoon = simulation.CopyBody(copy.moon);
                entryMoon.position = Lerp(
                    previousMoonPosition,
                    copy.moon.position,
                    entryFraction
                );

                points.Add(entryPosition);

                SimVector3[] moonOrbitPathPoints = OrbitalMechanics.BuildOrbitPathRelativeToBody(
                    entryMoon,
                    entryPosition,
                    copy.ship.velocity,
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

            points.Add(copy.ship.position);
            previousShipPosition = copy.ship.position;
            previousMoonPosition = copy.moon.position;
            previousDistanceToMoon = distanceToMoon;
        }

        return new SphereOfInfluenceEntryPreview(
            false,
            0,
            SimVector3.Zero(),
            Array.Empty<SimVector3>(),
            Array.Empty<SimVector3>()
        );
    }

    public SphereOfInfluenceEntryPreview GetPlanetSphereOfInfluenceExitPreview(
        SimVector3 plannedDeltaV,
        int stepCount,
        double dt,
        double maneuverTime,
        double thrustForce,
        double burnDuration,
        double burnDt
    )
    {
        if (!simulation.ScenarioHasSun())
        {
            return SphereOfInfluenceEntryPreview.None();
        }

        Simulation copy = simulation.CreateCopy();
        copy.ship.thrust = SimVector3.Zero();

        List<SimVector3> points = new List<SimVector3>();
        SimVector3 previousShipPosition = copy.ship.position;
        SimVector3 previousPlanetPosition = copy.planet.position;
        double previousDistanceToPlanet = copy.GetShipDistanceToPlanet();
        points.Add(previousShipPosition);

        if (previousDistanceToPlanet > copy.planetSphereOfInfluenceRadius)
        {
            SimVector3[] sunOrbitPathPoints = OrbitalMechanics.BuildOrbitPathRelativeToBody(
                copy.sun,
                copy.ship.position,
                copy.ship.velocity,
                240
            );

            return new SphereOfInfluenceEntryPreview(
                true,
                0,
                copy.ship.position,
                points.ToArray(),
                sunOrbitPathPoints,
                "Sun"
            );
        }

        for (int i = 1; i <= stepCount; i++)
        {
            double previewTime = (i - 1) * dt;

            AdvancePreviewWithFiniteBurn(
                copy,
                plannedDeltaV,
                thrustForce,
                burnDuration,
                maneuverTime,
                previewTime,
                dt,
                burnDt
            );

            double distanceToPlanet = copy.GetShipDistanceToPlanet();

            if (distanceToPlanet > copy.planetSphereOfInfluenceRadius)
            {
                double exitFraction = CalculateSphereOfInfluenceExitFraction(
                    previousDistanceToPlanet,
                    distanceToPlanet,
                    copy.planetSphereOfInfluenceRadius
                );
                SimVector3 exitPosition = Lerp(
                    previousShipPosition,
                    copy.ship.position,
                    exitFraction
                );
                Body exitPlanet = simulation.CopyBody(copy.planet);
                exitPlanet.position = Lerp(
                    previousPlanetPosition,
                    copy.planet.position,
                    exitFraction
                );

                points.Add(exitPosition);

                SimVector3[] sunOrbitPathPoints = OrbitalMechanics.BuildOrbitPathRelativeToBody(
                    copy.sun,
                    exitPosition,
                    copy.ship.velocity,
                    240
                );

                return new SphereOfInfluenceEntryPreview(
                    true,
                    (i - 1 + exitFraction) * dt,
                    exitPosition,
                    points.ToArray(),
                    sunOrbitPathPoints,
                    "Sun"
                );
            }

            points.Add(copy.ship.position);
            previousShipPosition = copy.ship.position;
            previousPlanetPosition = copy.planet.position;
            previousDistanceToPlanet = distanceToPlanet;
        }

        return SphereOfInfluenceEntryPreview.None();
    }

    public void AdvancePreviewToTime(Simulation copy, double time, double dt)
    {
        double remainingTime = time;

        while (remainingTime > 0.0)
        {
            double stepDt = dt > 0.0 ? Math.Min(dt, remainingTime) : remainingTime;
            copy.Update(stepDt);
            remainingTime -= stepDt;
        }
    }

    public bool ShouldShowDescentPreview()
    {
        if (!simulation.enableAtmosphericPreview)
        {
            return false;
        }

        if (
            simulation.planet == null ||
            simulation.planet.atmosphereProfile == null ||
            !simulation.planet.atmosphereProfile.HasAtmosphere ||
            simulation.ship.flightStatus != FlightStatus.Flying
        )
        {
            return false;
        }

        if (simulation.IsShipInAtmosphere())
        {
            return true;
        }

        if (
            simulation.activePrimaryBodyName != simulation.planet.name
        )
        {
            return false;
        }

        double periapsisAltitude =
            simulation.GetShipPeriapsisAltitudeRelativeToPlanet();

        if (
            periapsisAltitude < 0.0 ||
            periapsisAltitude > simulation.planet.atmosphereProfile.topAltitude
        )
        {
            return false;
        }

        return simulation.GetShipOrbitTypeRelativeToActivePrimary() == "Ellipse";
    }

    public SimVector3[] SampleDescentPreviewWorldPath(
        bool useAtmosphericSas,
        double maxDuration,
        double stepDt,
        int maxPoints
    )
    {
        if (!ShouldShowDescentPreview() || maxPoints <= 1 || maxDuration <= 0.0)
        {
            return Array.Empty<SimVector3>();
        }

        int pointCap = Math.Min(maxPoints, DescentPreviewMaxPoints);
        Simulation copy = simulation.CreateCopy();
        copy.ship.thrust = SimVector3.Zero();
        copy.atmosphericSasEnabled = useAtmosphericSas;
        ConfigureDescentPreviewOrientation(copy, useAtmosphericSas);

        List<SimVector3> points = new List<SimVector3>(pointCap);
        points.Add(copy.ship.position);

        double elapsed = 0.0;
        double vacuumStepDt = stepDt > 0.0 ? stepDt : DescentPreviewVacuumStepDt;
        double atmosphereStepDt =
            DescentPreviewCoreStepDt * DescentPreviewAtmosphereIntegratorStride;

        while (
            elapsed < maxDuration &&
            points.Count < pointCap &&
            copy.ship.flightStatus == FlightStatus.Flying
        )
        {
            bool inAtmosphere = copy.lastAtmosphericDensity > 0.0;
            double integrationStepDt = inAtmosphere ?
                atmosphereStepDt :
                vacuumStepDt;

            copy.ApplyOrientationControl(integrationStepDt);
            copy.Update(integrationStepDt);
            elapsed += integrationStepDt;
            points.Add(copy.ship.position);

            if (
                copy.planet != null &&
                copy.GetShipAltitudeAbovePlanet() <= 0.0
            )
            {
                break;
            }
        }

        return points.ToArray();
    }

    public bool TryGetDescentImpactPoint(out SimVector3 worldPosition)
    {
        worldPosition = SimVector3.Zero();

        if (!ShouldShowDescentPreview())
        {
            return false;
        }

        SimVector3[] path = SampleDescentPreviewWorldPath(
            true,
            3600.0,
            0.2,
            DescentPreviewMaxPoints
        );

        if (path.Length == 0 || simulation.planet == null)
        {
            return false;
        }

        Body planet = simulation.planet;
        double collisionRadius = simulation.ship.collisionRadius;

        for (int i = 1; i < path.Length; i++)
        {
            double altitudeBefore = GetAltitudeAbovePlanetSurface(
                path[i - 1],
                planet,
                collisionRadius
            );
            double altitudeAfter = GetAltitudeAbovePlanetSurface(
                path[i],
                planet,
                collisionRadius
            );

            if (altitudeBefore > 0.0 && altitudeAfter <= 0.0)
            {
                double denominator = altitudeBefore - altitudeAfter;

                if (denominator > 1e-9)
                {
                    double blend = altitudeBefore / denominator;
                    blend = Math.Min(1.0, Math.Max(0.0, blend));
                    worldPosition = InterpolateImpactPoint(
                        path[i - 1],
                        path[i],
                        blend
                    );
                }
                else
                {
                    worldPosition = path[i];
                }

                return true;
            }
        }

        double lastAltitude = GetAltitudeAbovePlanetSurface(
            path[path.Length - 1],
            planet,
            collisionRadius
        );

        if (lastAltitude <= 0.0)
        {
            worldPosition = path[path.Length - 1];
            return true;
        }

        return false;
    }

    static double GetAltitudeAbovePlanetSurface(
        SimVector3 worldPosition,
        Body planet,
        double collisionRadius
    )
    {
        return planet.position
            .Subtract(worldPosition)
            .Length() -
            planet.radius -
            collisionRadius;
    }

    static SimVector3 InterpolateImpactPoint(
        SimVector3 start,
        SimVector3 end,
        double blend
    )
    {
        return start.Add(end.Subtract(start).Multiply(blend));
    }

    static void ConfigureDescentPreviewOrientation(
        Simulation copy,
        bool useAtmosphericSas
    )
    {
        if (!useAtmosphericSas)
        {
            copy.orientationInput = ShipOrientationInput.Idle();
            return;
        }

        copy.orientationInput = new ShipOrientationInput
        {
            mode = ShipOrientationMode.Prograde,
            autoTurnRateRadiansPerSecond = double.MaxValue / 4.0,
            atmosphericAutoTurnRateMultiplier = 1.0,
        };
    }

    public SimVector3[] GetDescentPreviewPathRelativeToPlanet(int maxPoints)
    {
        SimVector3[] worldPath = SampleDescentPreviewWorldPath(
            true,
            3600.0,
            0.2,
            maxPoints
        );

        if (
            worldPath.Length == 0 ||
            simulation.planet == null
        )
        {
            return Array.Empty<SimVector3>();
        }

        SimVector3[] relativePath = new SimVector3[worldPath.Length];

        for (int i = 0; i < worldPath.Length; i++)
        {
            relativePath[i] = worldPath[i].Subtract(simulation.planet.position);
        }

        return relativePath;
    }

    void AdvancePreviewWithFiniteBurn(
        Simulation copy,
        SimVector3 plannedDeltaV,
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

            copy.ship.thrust = isBurning ?
                plannedDeltaV.Normalize().Multiply(thrustForce) :
                SimVector3.Zero();
            copy.Update(subStep);

            currentTime += subStep;
            remainingTime -= subStep;
        }

        copy.ship.thrust = SimVector3.Zero();
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

    double CalculateSphereOfInfluenceExitFraction(
        double previousDistance,
        double currentDistance,
        double sphereOfInfluenceRadius
    )
    {
        double distanceChange = currentDistance - previousDistance;

        if (Math.Abs(distanceChange) < 0.000001)
        {
            return 1.0;
        }

        return Clamp01((sphereOfInfluenceRadius - previousDistance) / distanceChange);
    }

    static double Clamp01(double value)
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

    SimVector3 Lerp(SimVector3 a, SimVector3 b, double t)
    {
        return a.Add(b.Subtract(a).Multiply(t));
    }

    static double Dot(SimVector3 a, SimVector3 b)
    {
        return SimVector3.Dot(a, b);
    }
}
