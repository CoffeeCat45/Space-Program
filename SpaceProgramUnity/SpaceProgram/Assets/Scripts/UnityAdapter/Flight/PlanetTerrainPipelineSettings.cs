/// <summary>
/// Play-mode toggles for flight terrain data sources.
/// GEBCO-only skips RSS EarthHeight.dds (mesh heights stay GEBCO). Close-up splat
/// still loads from RSS-Terrain when the pack is on disk.
/// </summary>
public static class PlanetTerrainPipelineSettings
{
    /// <summary>
    /// When true: mesh heights from GEBCO mmap only; macro color from earth_geographic.png.
    /// RSS-Terrain close-up splat still loads when present (same as Moon).
    /// </summary>
    public static bool UseGebcoOnlyPipeline
    {
        get
        {
            GameSettingsStore.EnsureLoaded();
            return GameSettingsStore.Data.graphics.gebcoOnlyPipeline;
        }
        set
        {
            GameSettingsStore.EnsureLoaded();
            GameSettingsStore.Data.graphics.gebcoOnlyPipeline = value;
            GameSettingsStore.SaveToDisk();
        }
    }
}
