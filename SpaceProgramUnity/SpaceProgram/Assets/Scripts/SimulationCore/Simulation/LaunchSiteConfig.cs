using System;

/// <summary>
/// Launch-pad geography for planet-surface scenarios. Active site is selected from
/// <see cref="SimulationScenario"/> before world/sim initialization.
/// </summary>
public static class LaunchSiteConfig
{
    public readonly struct Site
    {
        public Site(string localizationKey, double latitudeDegrees, double longitudeDegrees)
        {
            LocalizationKey = localizationKey;
            LatitudeRadians = latitudeDegrees * Math.PI / 180.0;
            LongitudeRadians = longitudeDegrees * Math.PI / 180.0;
        }

        public string LocalizationKey { get; }

        public double LatitudeRadians { get; }

        public double LatitudeDegrees => LatitudeRadians * 180.0 / Math.PI;

        public double LongitudeRadians { get; }

        public double LongitudeDegrees => LongitudeRadians * 180.0 / Math.PI;

        public SimVector3 GetSurfaceNormal()
        {
            return LandingConfig.DirectionFromPlanetLatitudeLongitude(
                LatitudeRadians,
                LongitudeRadians
            );
        }
    }

    public static readonly Site CapeCanaveral = new Site(
        "Launch site cape canaveral",
        28.5728,
        -80.6480
    );

    /// <summary>Atlas foothills / western Morocco.</summary>
    public static readonly Site MoroccoPlateau = new Site(
        "Launch site morocco",
        31.63,
        -7.99
    );

    /// <summary>Qinghai–Tibet plateau (high-altitude grassland).</summary>
    public static readonly Site TibetPlateau = new Site(
        "Launch site tibet",
        31.476,
        91.102
    );

    /// <summary>Prime meridian + equator — mesh-frame LOD diagnostic (sim.x = 0).</summary>
    public static readonly Site GreenwichMeridian = new Site(
        "Launch site greenwich",
        0.0,
        0.0
    );

    /// <summary>Selenographic 0°N 0°E — near side, LOD diagnostic.</summary>
    public static readonly Site MoonNearSide = new Site(
        "Launch site moon near side",
        0.0,
        0.0
    );

    static Site activeSite = CapeCanaveral;

    public static Site Active => activeSite;

    public static void SetActive(Site site)
    {
        activeSite = site;
    }

    public static void ApplyScenario(SimulationScenario scenario)
    {
        switch (scenario)
        {
            case SimulationScenario.PlanetSurfaceMorocco:
                SetActive(MoroccoPlateau);
                break;
            case SimulationScenario.PlanetSurfaceTibet:
                SetActive(TibetPlateau);
                break;
            case SimulationScenario.PlanetSurfaceGreenwich:
                SetActive(GreenwichMeridian);
                break;
            case SimulationScenario.MoonSurfaceStart:
                SetActive(MoonNearSide);
                break;
            case SimulationScenario.PlanetSurfaceStart:
            case SimulationScenario.PlanetSurfaceFlorida:
                SetActive(CapeCanaveral);
                break;
        }
    }

    public static SimVector3 GetActiveSurfaceNormal()
    {
        return activeSite.GetSurfaceNormal();
    }
}

public static class SimulationScenarios
{
    /// <summary>
    /// Must list every scenario that <see cref="LaunchSiteConfig.ApplyScenario"/> maps to a pad
    /// site. A pad scenario that is missing here still spawns from the menu, but skips the pad
    /// world setup, the spin lock, the launch stage list and the pad-locked planet rotation.
    /// </summary>
    public static bool IsPlanetSurfacePad(SimulationScenario scenario)
    {
        return
            scenario == SimulationScenario.PlanetSurfaceStart ||
            scenario == SimulationScenario.PlanetSurfaceFlorida ||
            scenario == SimulationScenario.PlanetSurfaceMorocco ||
            scenario == SimulationScenario.PlanetSurfaceTibet ||
            scenario == SimulationScenario.PlanetSurfaceGreenwich;
    }

    public static bool IsMoonSurfacePad(SimulationScenario scenario)
    {
        return scenario == SimulationScenario.MoonSurfaceStart;
    }

    public static bool IsAnySurfacePad(SimulationScenario scenario)
    {
        return IsPlanetSurfacePad(scenario) || IsMoonSurfacePad(scenario);
    }
}
