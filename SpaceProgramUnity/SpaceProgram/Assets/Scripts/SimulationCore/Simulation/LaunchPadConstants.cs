using System;

/// <summary>
/// Deterministic launch-pad geometry shared by SimulationCore and UnityAdapter.
/// </summary>
public static class LaunchPadConstants
{
    /// <summary>Earth 1:10 scale radius in simulation meters.</summary>
    public const double EarthRadiusMeters = CelestialBody.Earth.Radius;

    /// <summary>Cove concrete deck elevation above the local terrain (sim meters).</summary>
    public const double PadDeckHeightAboveSphereMeters = 7.0;

    /// <summary>Extra gap between the pad deck and the vessel contact plane.</summary>
    public const double PadVesselClearanceAboveDeckMeters = 2.0;

    /// <summary>Surface arc distance within which the Cove launch pad stays visible.</summary>
    public const double LaunchSitePadVisibilityRadiusMeters = 800.0;

    /// <summary>
    /// VAB block layouts can report absurd projections; cap grounding and universe slide.
    /// </summary>
    public const double MaxReasonableVesselBottomExtentMeters = 256.0;

    static double surfacePadBodyRadiusMeters = EarthRadiusMeters;
    static bool surfacePadIsMoon;
    static double launchSiteTerrainHeightAboveSphereMeters;
    static double launchSiteSpawnPointHeightAbovePadBaseMeters = double.NaN;
    static double launchSiteSpawnPointTangentEastMeters;
    static double launchSiteSpawnPointTangentNorthMeters;

    /// <summary>
    /// Sphere radius of the body that currently owns the Cove pad (Earth or Moon).
    /// </summary>
    public static double SurfacePadBodyRadiusMeters => surfacePadBodyRadiusMeters;

    public static void SetSurfacePadBodyRadiusMeters(double meters)
    {
        if (double.IsNaN(meters) || double.IsInfinity(meters) || meters <= 0.0)
        {
            surfacePadBodyRadiusMeters = EarthRadiusMeters;
            return;
        }

        surfacePadBodyRadiusMeters = meters;
    }

    public static void BindForScenario(SimulationScenario scenario)
    {
        surfacePadIsMoon = SimulationScenarios.IsMoonSurfacePad(scenario);
        SetSurfacePadBodyRadiusMeters(
            surfacePadIsMoon ? CelestialBody.Moon.Radius : EarthRadiusMeters
        );
    }

    /// <summary>
    /// Planet-center distance to the pad deck contact plane (sim meters).
    /// Bottom of a landed vessel is pinned to this radius.
    /// </summary>
    public static double LaunchpadRadiusElevation =>
        surfacePadBodyRadiusMeters + GetLaunchSiteVesselBottomHeightAboveSphereMeters();

    public static void SetLaunchSiteTerrainHeightAboveSphereMeters(double meters)
    {
        if (double.IsNaN(meters) || double.IsInfinity(meters))
        {
            launchSiteTerrainHeightAboveSphereMeters = 0.0;
            return;
        }

        launchSiteTerrainHeightAboveSphereMeters = meters;
    }

    /// <summary>
    /// Spawn contact plane authored on the pad prefab (<c>SpawnPoint</c> above <c>Node_Bottom</c>).
    /// </summary>
    public static void SetLaunchSiteSpawnPointOffsetMeters(
        double heightAbovePadBaseMeters,
        double tangentEastMeters,
        double tangentNorthMeters
    )
    {
        launchSiteSpawnPointHeightAbovePadBaseMeters = heightAbovePadBaseMeters;
        launchSiteSpawnPointTangentEastMeters = tangentEastMeters;
        launchSiteSpawnPointTangentNorthMeters = tangentNorthMeters;
    }

    public static double GetLaunchSiteSpawnPointHeightAbovePadBaseMeters()
    {
        if (
            !double.IsNaN(launchSiteSpawnPointHeightAbovePadBaseMeters) &&
            !double.IsInfinity(launchSiteSpawnPointHeightAbovePadBaseMeters)
        )
        {
            return Math.Max(0.0, launchSiteSpawnPointHeightAbovePadBaseMeters);
        }

        return PadDeckHeightAboveSphereMeters + PadVesselClearanceAboveDeckMeters;
    }

    public static double GetLaunchSiteSpawnPointTangentEastMeters()
    {
        return launchSiteSpawnPointTangentEastMeters;
    }

    public static double GetLaunchSiteSpawnPointTangentNorthMeters()
    {
        return launchSiteSpawnPointTangentNorthMeters;
    }

    /// <summary>
    /// Flatten / pad deck rest height above the reference sphere.
    /// Earth: clamp to ≥0 (ocean floor is not a pad). Moon: keep signed LDEM (maria below 0).
    /// </summary>
    public static double GetLaunchSitePadBaseHeightAboveSphereMeters()
    {
        if (surfacePadIsMoon)
        {
            return launchSiteTerrainHeightAboveSphereMeters;
        }

        return Math.Max(0.0, launchSiteTerrainHeightAboveSphereMeters);
    }

    public static double GetLaunchSiteTerrainHeightAboveSphereMeters()
    {
        return launchSiteTerrainHeightAboveSphereMeters;
    }

    public static double GetLaunchSiteDeckHeightAboveSphereMeters()
    {
        return PadDeckHeightAboveSphereMeters;
    }

    public static double GetLaunchSitePadDeckHeightAboveSphereMeters()
    {
        return GetLaunchSitePadBaseHeightAboveSphereMeters() +
            GetLaunchSiteDeckHeightAboveSphereMeters();
    }

    public static double GetLaunchSiteVesselBottomHeightAboveSphereMeters()
    {
        return GetLaunchSitePadBaseHeightAboveSphereMeters() +
            GetLaunchSiteSpawnPointHeightAbovePadBaseMeters();
    }

    public static double GetLaunchSiteSurfaceHeightAboveSphereMeters()
    {
        return GetLaunchSitePadDeckHeightAboveSphereMeters();
    }

    public static double GetPadRestComDistance(double bottomExtentMeters)
    {
        return LaunchpadRadiusElevation + GetEffectiveBottomExtentMeters(bottomExtentMeters);
    }

    public static double GetEffectiveBottomExtentMeters(double measuredBottomExtentMeters)
    {
        if (double.IsNaN(measuredBottomExtentMeters) || double.IsInfinity(measuredBottomExtentMeters))
        {
            return LandingConfig.ShipCollisionRadius;
        }

        double minimum = LandingConfig.ShipCollisionRadius * 0.1;
        double clamped = Math.Max(measuredBottomExtentMeters, minimum);
        return Math.Min(clamped, MaxReasonableVesselBottomExtentMeters);
    }
}
