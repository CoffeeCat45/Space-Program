/// <summary>
/// Which globe owns flight LOD / dual-frame. Starts from the scenario pad, then
/// follows Moon/Earth SoI so a Cape launch can land on the Moon.
/// </summary>
public static class CelestialTerrainRuntime
{
    public const string EarthBodyName = "Planet";
    public const string MoonBodyName = "Moon";

    public static string LodBodyName { get; private set; } = EarthBodyName;

    public static bool IsMoon => LodBodyName == MoonBodyName;

    /// <summary>
    /// Cove flatten only for the scenario pad. Mid-flight SoI swap must not punch
    /// a Cape deck into lunar LDEM (or a lunar pad into Florida).
    /// </summary>
    public static bool ApplyLaunchSiteFlatten { get; private set; } = true;

    public static bool UseLandOceanVertexPaint => !IsMoon;

    /// <summary>
    /// Earth oceans flatten GEBCO &lt; 0 to sea level. Lunar maria are real terrain.
    /// </summary>
    public static bool HasOcean => !IsMoon;

    public static float LodBodyRadiusUnity =>
        IsMoon ?
            (float)CelestialBody.Moon.Radius :
            WorldVisualBridge.FlightPlanetRadiusUnity;

    /// <summary>
    /// True-scale body radius. Shader tiling uses UnityRadius / this so 1:10 and 1:1
    /// keep the same real meters per close-up tile.
    /// </summary>
    public static float LodBodyRealRadiusMeters =>
        LodBodyRadiusUnity *
        (PlanetSurfaceConstants.RealEarthRadiusMeters /
         PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters);

    public static void BindForScenario(SimulationScenario scenario)
    {
        ApplyLaunchSiteFlatten = true;
        LaunchPadConstants.BindForScenario(scenario);
        BindLodBodyName(
            SimulationScenarios.IsMoonSurfacePad(scenario) ? MoonBodyName : EarthBodyName
        );
    }

    /// <summary>
    /// Switch flight LOD to Earth or Moon after SoI change. Returns true if the globe changed.
    /// </summary>
    public static bool TryBindLodBodyForSoI(string bodyName)
    {
        string next = bodyName == MoonBodyName ? MoonBodyName : EarthBodyName;

        if (next == LodBodyName)
        {
            return false;
        }

        ApplyLaunchSiteFlatten = false;
        return BindLodBodyName(next);
    }

    static bool BindLodBodyName(string next)
    {
        bool changed = next != LodBodyName;
        LodBodyName = next;

        if (changed)
        {
            PlanetElevationBootstrap.ReloadForLodBody();
            PlanetTerrainTextureResources.InvalidateLodBodySplatSet();
        }
        else
        {
            PlanetElevationBootstrap.EnsureLoadedForFlight();
        }

        PlanetTerrainTextureResources.EnsureLoaded();
        return changed;
    }
}
