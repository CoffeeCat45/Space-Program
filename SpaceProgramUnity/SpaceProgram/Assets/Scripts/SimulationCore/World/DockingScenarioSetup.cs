using System;

static class DockingScenarioSetup
{
    public const double InitialSeparationMin = 150.0;
    public const double InitialSeparationMax = 200.0;
    public const double InitialSeparation = 175.0;
    public const double SpawnClearanceMargin = 5.0;
    public const double ApproachSpeed = 3.0;

    public static void ConfigureVacuumApproach(
        out SimVector3 stationPosition,
        out SimVector3 stationVelocity,
        out double stationHeadingRadians,
        out SimVector3 shipPosition,
        out SimVector3 shipVelocity,
        out double shipHeadingRadians
    )
    {
        SimVector3 approachDirection = SimVector3.UnitZ();

        stationPosition = SimVector3.Zero();
        stationVelocity = SimVector3.Zero();
        stationHeadingRadians = Math.PI;

        shipPosition = stationPosition.Add(
            approachDirection.Multiply(-InitialSeparation)
        );
        shipVelocity = SimVector3.Zero();
        shipHeadingRadians = 0.0;
    }

    public static void ConfigureOrbitalApproach(
        Body planet,
        out SimVector3 stationPosition,
        out SimVector3 stationVelocity,
        out double stationHeadingRadians,
        out SimVector3 shipPosition,
        out SimVector3 shipVelocity,
        out double shipHeadingRadians
    )
    {
        SimVector3 referenceOrigin = planet != null ?
            planet.position :
            SimVector3.Zero();
        SimVector3 referenceVelocity = planet != null ?
            planet.velocity :
            SimVector3.Zero();

        ConfigureOrbitalApproach(
            referenceOrigin,
            referenceVelocity,
            InitialSeparation,
            out stationPosition,
            out stationVelocity,
            out stationHeadingRadians,
            out shipPosition,
            out shipVelocity,
            out shipHeadingRadians
        );
    }

    public static double ResolveSpawnSeparation(double shipEnvelope, double stationEnvelope)
    {
        double clearanceDistance =
            shipEnvelope + stationEnvelope + SpawnClearanceMargin;
        double clampedTarget = Math.Clamp(
            InitialSeparation,
            InitialSeparationMin,
            InitialSeparationMax
        );

        return Math.Max(clampedTarget, clearanceDistance);
    }

    static void ConfigureOrbitalApproach(
        SimVector3 referenceOrigin,
        SimVector3 referenceVelocity,
        double separationMeters,
        out SimVector3 stationPosition,
        out SimVector3 stationVelocity,
        out double stationHeadingRadians,
        out SimVector3 shipPosition,
        out SimVector3 shipVelocity,
        out double shipHeadingRadians
    )
    {
        double orbitRadius = CelestialBody.Earth.GetLowOrbitRadius();
        double circularSpeed = CelestialBody.Earth.GetLowOrbitCircularSpeed();
        SimVector3 approachDirection = SimVector3.UnitZ();

        stationPosition = referenceOrigin.Add(
            new SimVector3(-orbitRadius, 0, 0)
        );
        stationVelocity = referenceVelocity.Add(
            new SimVector3(0, circularSpeed, 0)
        );

        shipPosition = stationPosition.Add(
            approachDirection.Multiply(-separationMeters)
        );
        shipVelocity = stationVelocity.Add(
            approachDirection.Multiply(ApproachSpeed)
        );

        stationHeadingRadians = Math.PI;
        shipHeadingRadians = 0.0;
    }
}
