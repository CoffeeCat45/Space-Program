using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu: probe on-disk earth_elevation.raw and earth_geographic.png layout vs canonical UV.
/// </summary>
public static class PlanetAssetProberMenu
{
    const string MenuPath = "Space Program/Probe Earth Assets (Canonical Layout)";

    [MenuItem(MenuPath)]
    public static void RunFromMenu()
    {
        PlanetAssetProberCombinedReport report = RunProbe(logToConsole: true);

        EditorUtility.DisplayDialog(
            "Earth Asset Probe",
            "See Console for full per-site report.\n\n" +
            "Elevation: " + report.Combined.Elevation.Layout + "\n" +
            "Color U: " + report.Combined.Color.Layout + "\n\n" +
            report.Combined.RecommendedPolicy + "\n\n" +
            (report.Combined.RecommendedPolicy.ColorMapRequiresVerticalFlip
                ? "Color needs flipV."
                : "No runtime color flips (canonical bake)."),
            "OK"
        );
    }

    public static PlanetAssetProberCombinedReport RunProbe(bool logToConsole)
    {
        string rawPath = Path.Combine(
            Application.streamingAssetsPath,
            "Planet",
            PlanetSurfaceConstants.ElevationRawFileName
        );
        string colorPath = ResolveColorMapPath();

        PlanetAssetProber.ColorSampleDelegate sampler = null;
        Texture2D loadedTexture = null;

        if (!string.IsNullOrEmpty(colorPath) && File.Exists(colorPath))
        {
            byte[] bytes = File.ReadAllBytes(colorPath);
            loadedTexture = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
                bytes,
                "PlanetAssetProbeColor"
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

        PlanetAssetProber.CombinedProbeReport combined = PlanetAssetProber.ProbeStandardAssets(
            rawPath,
            colorPath,
            sampler
        );

        if (loadedTexture != null)
        {
            Object.DestroyImmediate(loadedTexture);
        }

        var wrapper = new PlanetAssetProberCombinedReport
        {
            Combined = combined,
            ElevationPath = rawPath,
            ColorPath = colorPath,
        };

        if (logToConsole)
        {
            Debug.Log(wrapper.Combined.BuildFullText());
        }

        return wrapper;
    }

    static string ResolveColorMapPath()
    {
        string[] candidates =
        {
            Path.Combine(Application.streamingAssetsPath, "MapMode", "earth_geographic.png"),
            Path.Combine(Application.streamingAssetsPath, "MapMode", "earth_geographic.jpg"),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return candidates[0];
    }
}

public sealed class PlanetAssetProberCombinedReport
{
    public PlanetAssetProber.CombinedProbeReport Combined { get; set; }

    public string ElevationPath { get; set; }

    public string ColorPath { get; set; }
}
