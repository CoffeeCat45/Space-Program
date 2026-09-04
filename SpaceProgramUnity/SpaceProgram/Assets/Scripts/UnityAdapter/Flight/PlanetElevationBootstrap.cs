using System.IO;
using UnityEngine;

/// <summary>
/// Loads baked GEBCO elevation for play mode (SimulationCore catalog + Unity mesh sampling).
/// Loads only the active source to avoid duplicate RAM use and noisy logs.
/// </summary>
static class PlanetElevationBootstrap
{
    const string UseHighResPlayerPrefsKey = "SpaceProgram.PlanetElevation.UseHighResRaw";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoLoad()
    {
        if (CelestialTerrainRuntime.IsMoon)
        {
            return;
        }

        if (ShouldSkipElevationBootstrap())
        {
            return;
        }

        TryLoadElevationData();
    }

    /// <summary>
    /// Flight / pad launch — elevation may have been skipped on sandbox cold start.
    /// </summary>
    public static void EnsureLoadedForFlight()
    {
        if (CelestialTerrainRuntime.IsMoon)
        {
            if (PlanetElevationCatalog.TryActivateCachedHighRes(moon: true))
            {
                return;
            }

            TryLoadMoonElevation();
            return;
        }

        if (PlanetElevationCatalog.TryActivateCachedHighRes(moon: false))
        {
            return;
        }

        if (PlanetElevationCatalog.IsLoaded)
        {
            return;
        }

        if (EarthSandboxScene.IsActive() && EarthSandboxScene.EnteredFromMainMenu)
        {
            return;
        }

        TryLoadElevationData();
    }

    public static void ReloadForLodBody()
    {
        if (CelestialTerrainRuntime.IsMoon)
        {
            if (PlanetElevationCatalog.TryActivateCachedHighRes(moon: true))
            {
                return;
            }

            TryLoadMoonElevation();
            return;
        }

        if (PlanetElevationCatalog.TryActivateCachedHighRes(moon: false))
        {
            return;
        }

        TryLoadElevationData();
    }

    static void TryLoadMoonElevation()
    {
        string rawPath = Path.Combine(
            PlanetDataPaths.PlanetFolder,
            MoonSurfaceConstants.ElevationRawFileName
        );
        string sizePath = Path.Combine(
            PlanetDataPaths.PlanetFolder,
            MoonSurfaceConstants.ElevationSizeFileName
        );

        if (!File.Exists(rawPath))
        {
            Debug.LogWarning(
                "PlanetElevation: moon_elevation.raw missing at " + rawPath +
                ". Moon LOD will be a smooth sphere."
            );
            return;
        }

        int width = MoonSurfaceConstants.DefaultElevationWidth;
        int height = MoonSurfaceConstants.DefaultElevationHeight;
        MoonElevationCatalog.TryReadSizeFile(sizePath, out width, out height);
        PlanetElevationCatalog.LoadHighResGridFromFile(rawPath, width, height);
        Debug.Log(
            "PlanetElevation: Moon LDEM 64 ppd " +
            PlanetElevationCatalog.HighResField.Width + "x" +
            PlanetElevationCatalog.HighResField.Height +
            " Int16 mmap (active). Visual height scale " +
            PlanetSurfaceConstants.UnityVisualHeightScale.ToString("F2") + "."
        );
    }

    static bool ShouldSkipElevationBootstrap()
    {
        if (EarthSandboxScene.IsActive() && EarthSandboxScene.EnteredFromMainMenu)
        {
            return true;
        }

        return PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded;
    }

    static void TryLoadElevationData()
    {
        string rawPath = PlanetDataPaths.ElevationRawPath;
        string binPath = PlanetDataPaths.ElevationBinaryPath;
        bool rawExists = File.Exists(rawPath);
        bool binExists = File.Exists(binPath);
        int defaultHighRes = rawExists ? 1 : 0;
        bool useHighRes = PlayerPrefs.GetInt(UseHighResPlayerPrefsKey, defaultHighRes) == 1;
#if !UNITY_EDITOR
        if (rawExists)
        {
            useHighRes = true;
        }
#endif

        if (useHighRes && rawExists)
        {
            if (TryLoadHighResRaw(rawPath))
            {
                return;
            }

            Debug.LogWarning(
                "PlanetElevation: earth_elevation.raw failed to mmap at " +
                rawPath + ". Falling back to legacy .bin if present."
            );
        }

        if (useHighRes && !rawExists)
        {
            Debug.LogWarning(
                "PlanetElevation: UseHighResRaw=1 but earth_elevation.raw is missing at " +
                rawPath + ". Falling back to legacy .bin if present."
            );
        }

        if (TryLoadLegacyActive(binPath, binExists))
        {
            return;
        }

        if (rawExists && TryLoadHighResRaw(rawPath))
        {
            return;
        }

        Debug.LogWarning(
            "PlanetElevation: no elevation data in StreamingAssets/Planet " +
            "(expected earth_elevation.raw and/or earth_elevation_5400x2700.bin)."
        );
    }

    static bool TryLoadHighResRaw(string rawPath)
    {
        try
        {
            PlanetElevationCatalog.LoadHighResFromFile(rawPath);
            PlanetElevationCatalog.UseHighResElevation = true;
            Debug.Log(
                "PlanetElevation: high-res GEBCO " +
                PlanetElevationCatalog.HighResField.Width + "x" +
                PlanetElevationCatalog.HighResField.Height +
                " Int16 mmap (active)." +
                (PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline ?
                    " GEBCO-only pipeline: heights from GEBCO, splat from RSS-Terrain if present." :
                    "")
            );
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "PlanetElevation: mmap failed for " + rawPath + " — " + exception.Message
            );
            return false;
        }
    }

    static bool TryLoadLegacyActive(string binPath, bool binExists)
    {
        if (!binExists)
        {
            return false;
        }

        PlanetElevationCatalog.LoadFromFile(binPath);
        PlanetElevationCatalog.UseHighResElevation = false;
        Debug.Log(
            "PlanetElevation: legacy SPEL " +
            PlanetElevationCatalog.ActiveField.Width + "x" +
            PlanetElevationCatalog.ActiveField.Height +
            " (active). Menu: Space Program > Planet Elevation > Use High-Res GEBCO Raw."
        );
        return true;
    }
}
