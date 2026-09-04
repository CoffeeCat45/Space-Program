#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu toggles for Play Mode elevation source (high-res .raw vs legacy .bin).
/// </summary>
static class PlanetElevationEditorPrefs
{
    const string UseHighResPlayerPrefsKey = "SpaceProgram.PlanetElevation.UseHighResRaw";

    [MenuItem("Space Program/Planet Elevation/Use High-Res GEBCO Raw (.raw)")]
    static void EnableHighResRaw()
    {
        PlayerPrefs.SetInt(UseHighResPlayerPrefsKey, 1);
        PlayerPrefs.Save();
        Debug.Log(
            "Planet elevation: high-res GEBCO raw enabled for Play Mode. " +
            "Press Play and look for PlanetElevationBootstrap log with UseHighResElevation=True."
        );
    }

    [MenuItem("Space Program/Planet Elevation/Use Legacy Elevation (.bin)")]
    static void EnableLegacyBin()
    {
        PlayerPrefs.SetInt(UseHighResPlayerPrefsKey, 0);
        PlayerPrefs.Save();
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
        int stored = PlayerPrefs.GetInt(UseHighResPlayerPrefsKey, binExists ? 0 : 1);
        bool useHighRes = stored == 1;

        Debug.Log(
            "Planet terrain settings:\n" +
            "  elevation: " + UseHighResPlayerPrefsKey + " = " + stored +
            " (" + (useHighRes ? "high-res .raw" : "legacy .bin") + ")\n" +
            "  pipeline: GEBCO-only (heights GEBCO; splat still from RSS-Terrain) = " +
            PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline + "\n" +
            "  earth_elevation.raw: " + (rawExists ? "found" : "MISSING") + "\n" +
            "  earth_elevation_5400x2700.bin: " + (binExists ? "found" : "missing")
        );
    }

    [MenuItem("Space Program/Planet Elevation/GEBCO-Only Pipeline (recommended)")]
    static void EnableGebcoOnlyPipeline()
    {
        PlayerPrefs.SetInt(UseHighResPlayerPrefsKey, 1);
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
