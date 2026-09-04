public enum SimulationScenario
{
    TwoBodyCircular,
    PlanetMoonShip,
    PlanetMoonEllipticMoon,
    MoonFlyby,
    SolarTransfer,
    PlanetSurfaceStart,
    PlanetAtmosphericEntry,
    InclinedOrbit,
    DockingApproach,
    PlanetSurfaceFlorida,
    PlanetSurfaceMorocco,
    PlanetSurfaceTibet,
    /// <summary>LOD / mesh-frame diagnostic: equator + prime meridian (sin(lon)=0 → sim.x=0).</summary>
    PlanetSurfaceGreenwich,
    /// <summary>Landed on the Moon (LOD / spin / orbit checks).</summary>
    MoonSurfaceStart,
}
