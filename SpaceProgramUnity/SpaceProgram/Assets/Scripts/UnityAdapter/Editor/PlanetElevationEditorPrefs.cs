#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu toggles for Play Mode elevation source (high-res .raw vs legacy .bin).
/// </summary>
static class PlanetElevationEditorPrefs
{
    [MenuItem("Space Program/Planet Elevation/Use High-Res GEBCO Raw (.raw)")]
    static void EnableHighResRaw()
    {
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.graphics.useHighResElevation = true;
        GameSettingsStore.SaveToDisk();
        Debug.Log(
            "Planet elevation: high-res GEBCO raw enabled for Play Mode. " +
            "Press Play and look for PlanetElevationBootstrap log with UseHighResElevation=True."
        );
    }

    [MenuItem("Space Program/Planet Elevation/Use Legacy Elevation (.bin)")]
    static void EnableLegacyBin()
    {
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.graphics.useHighResElevation = false;
        GameSettingsStore.SaveToDisk();
        Debug.Log(
            "Planet elevation: legacy .bin enabled for Play Mode. " +
            "Press Play and look for UseHighResElevation=False in PlanetElevationBootstrap log."
        );
    }

    [MenuItem("Space Program/Planet Elevation/Show Current Elevation Setting")]
    static void ShowCurrentSetting()
    {
        string planetFolder = Path.Combine(Application.streamingAssetsPath, "Planet");
        string rawPath = Path.Combine(planetFolder, PlanetSurfaceConstants.ElevationRawFileName);
        string binPath = Path.Combine(planetFolder, PlanetSurfaceConstants.ElevationBinaryFileName);
        bool rawExists = File.Exists(rawPath);
        bool binExists = File.Exists(binPath);
        GameSettingsStore.EnsureLoaded();
        bool useHighRes = GameSettingsStore.Data.graphics.useHighResElevation;

        Debug.Log(
            "Planet terrain settings:\n" +
            "  settings: " + GameSettingsStore.FilePath + "\n" +
            "  elevation: useHighResElevation = " +
            (useHighRes ? "true (high-res .raw)" : "false (legacy .bin)") + "\n" +
            "  pipeline: GEBCO-only (heights GEBCO; splat still from RSS-Terrain) = " +
            PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline + "\n" +
            "  earth_elevation.raw: " + (rawExists ? "found" : "MISSING") + "\n" +
            "  earth_elevation_5400x2700.bin: " + (binExists ? "found" : "missing")
        );
    }

    [MenuItem("Space Program/Planet Elevation/GEBCO-Only Pipeline (recommended)")]
    static void EnableGebcoOnlyPipeline()
    {
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.graphics.useHighResElevation = true;
        PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline = true;
        Debug.Log(
            "Planet terrain: GEBCO-only pipeline ON. " +
            "Heights from earth_elevation.raw; macro color from earth_geographic.png; " +
            "RSS EarthHeight.dds off. Close-up splat still loads from RSS-Terrain if present."
        );
    }

    [MenuItem("Space Program/Planet Elevation/Legacy RSS Textures + Detail")]
    static void EnableLegacyRssPipeline()
    {
        PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline = false;
        Debug.Log(
            "Planet terrain: legacy RSS pipeline ON (EarthHeight.dds + RSS-Terrain splats). " +
            "Elevation source still controlled by Use High-Res / Legacy menu."
        );
    }
}
#endif
