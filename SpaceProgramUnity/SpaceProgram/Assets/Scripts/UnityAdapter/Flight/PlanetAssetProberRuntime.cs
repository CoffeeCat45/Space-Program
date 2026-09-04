using UnityEngine;

/// <summary>
/// Logs on-disk Earth asset layout once per Play session (after StreamingAssets paths exist).
/// </summary>
static class PlanetAssetProberRuntime
{
    static bool loggedThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void LogOnceAfterSceneLoad()
    {
        if (loggedThisSession)
        {
            return;
        }

        if (EarthSandboxScene.IsActive() && EarthSandboxScene.EnteredFromMainMenu)
        {
            return;
        }

        loggedThisSession = true;

        if (!Application.isEditor)
        {
            return;
        }

        LogProbeReport();
    }

    public static void LogProbeReport()
    {
        PlanetElevationBootstrap.EnsureLoadedForFlight();

        if (!PlanetElevationCatalog.IsHighResLoaded && !PlanetElevationCatalog.IsLoaded)
        {
            Debug.LogWarning(
                "[PlanetAssetProber] Skipping editor probe — elevation not loaded " +
                "(expected StreamingAssets/Planet/earth_elevation.raw or legacy .bin)."
            );
            return;
        }

        string rawPath = PlanetDataPaths.ElevationRawPath;
        string colorPath = ResolveColorMapPath();

        Texture2D loadedTexture = null;
        PlanetAssetProber.ColorSampleDelegate sampler = null;

        if (System.IO.File.Exists(colorPath))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(colorPath);
            loadedTexture = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
                bytes,
                "PlanetAssetProbeColorRuntime"
            );

            if (loadedTexture != null)
            {
                sampler = (float u, float v, out byte r, out byte g, out byte b) =>
                {
                    Color sample = loadedTexture.GetPixelBilinear(u, v);
                    r = (byte)Mathf.RoundToInt(sample.r * 255f);
                    g = (byte)Mathf.RoundToInt(sample.g * 255f);
                    b = (byte)Mathf.RoundToInt(sample.b * 255f);
                    return true;
                };
            }
        }

        PlanetAssetProber.CombinedProbeReport report;

        try
        {
            report = PlanetAssetProber.ProbeStandardAssets(
                rawPath,
                colorPath,
                sampler
            );
        }
        catch (System.InvalidOperationException exception)
        {
            Debug.LogWarning(
                "[PlanetAssetProber] Skipping editor probe: " + exception.Message
            );
            return;
        }

        if (loadedTexture != null)
        {
            Object.Destroy(loadedTexture);
        }

        Debug.Log(report.BuildFullText());
    }

    static string ResolveColorMapPath()
    {
        string[] candidates =
        {
            System.IO.Path.Combine(Application.streamingAssetsPath, "MapMode", "earth_geographic.png"),
            System.IO.Path.Combine(Application.streamingAssetsPath, "MapMode", "earth_geographic.jpg"),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (System.IO.File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return candidates[0];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        loggedThisSession = false;
    }
}
