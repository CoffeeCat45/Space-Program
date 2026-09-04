using System.IO;
using UnityEngine;

/// <summary>
/// matteason daily/live cloud maps.
/// daily-cloud-maps *-clouds-alpha.png: dummy white RGB, coverage in A.
/// live-cloud-maps clouds-alpha.png: gray is lifted (~128–255), real holes are in A.
/// JPG greyscale uses luma. Coverage is baked into RGB so the shader samples .r.
/// </summary>
public static class NasaCloudMapResources
{
    const string TextureName = "EarthCloudsNasaGeographic";

    static Texture2D cloudMap;
    static bool loadAttempted;

    public static Texture2D Map
    {
        get
        {
            EnsureLoaded();
            return cloudMap;
        }
    }

    public static string MapPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, PlanetSurfaceConstants.CloudsPngFileName);

    static string ResolveMapPath()
    {
        string[] candidates =
        {
            MapPath,
            Path.Combine(PlanetDataPaths.MapModeFolder, "earth_clouds.png"),
            Path.Combine(PlanetDataPaths.MapModeFolder, "earth_clouds.jpg"),
            Path.Combine(PlanetDataPaths.MapModeFolder, "8192x4096-clouds-alpha.png"),
            Path.Combine(PlanetDataPaths.MapModeFolder, "8192x4096-clouds.jpg"),
            Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", "..", "8192x4096-clouds-alpha.png")
            ),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!string.IsNullOrEmpty(candidates[i]) && File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    public static void EnsureLoaded()
    {
        if (IsAlive(cloudMap) || loadAttempted)
        {
            return;
        }

        loadAttempted = true;
        string path = ResolveMapPath();
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError(
                "NasaCloudMapResources: missing " + MapPath +
                ". Put a matteason cloud map there as " + PlanetSurfaceConstants.CloudsPngFileName +
                " (alpha PNG or greyscale JPG)."
            );
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!loaded.LoadImage(bytes, false))
        {
            Object.Destroy(loaded);
            Debug.LogError("NasaCloudMapResources: LoadImage failed for " + path + ".");
            return;
        }

        int gpuCap = SystemInfo.maxTextureSize;
        if (gpuCap > 0 && loaded.width > gpuCap)
        {
            loaded = Downscale(loaded, gpuCap);
        }

        bool coverageInAlpha = DetectVaryingAlpha(loaded);
        BakeCoverageIntoRgb(loaded, coverageInAlpha);

        loaded.name = TextureName;
        loaded.wrapModeU = TextureWrapMode.Repeat;
        loaded.wrapModeV = TextureWrapMode.Clamp;
        loaded.filterMode = FilterMode.Bilinear;
        loaded.anisoLevel = 8;
        loaded.Apply(false, true);
        cloudMap = loaded;
        Debug.Log(
            "NasaCloudMapResources: loaded " + loaded.width + "x" + loaded.height +
            " coverage=" + (coverageInAlpha ? "alpha" : "luma") + " from " + path + "."
        );
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (cloudMap != null)
        {
            Object.Destroy(cloudMap);
        }

        cloudMap = null;
        loadAttempted = false;
    }

    static bool IsAlive(Texture2D texture)
    {
        return texture != null;
    }

    static bool DetectVaryingAlpha(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;
        int stepX = Mathf.Max(1, width / 8);
        int stepY = Mathf.Max(1, height / 8);
        for (int y = 0; y < height; y += stepY)
        {
            for (int x = 0; x < width; x += stepX)
            {
                if (texture.GetPixel(x, y).a < 0.97f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static void BakeCoverageIntoRgb(Texture2D texture, bool coverageInAlpha)
    {
        Color32[] pixels = texture.GetPixels32();
        if (coverageInAlpha)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                byte coverage = pixels[i].a;
                pixels[i] = new Color32(coverage, coverage, coverage, coverage);
            }
        }
        else
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                int luma = (pixel.r * 299 + pixel.g * 587 + pixel.b * 114 + 500) / 1000;
                byte coverage = (byte)Mathf.Clamp(luma, 0, 255);
                pixels[i] = new Color32(coverage, coverage, coverage, coverage);
            }
        }

        texture.SetPixels32(pixels);
    }

    static Texture2D Downscale(Texture2D source, int maxWidth)
    {
        int newHeight = Mathf.Max(
            1,
            Mathf.RoundToInt(source.height * (maxWidth / (float)source.width))
        );
        var scaled = new Texture2D(maxWidth, newHeight, TextureFormat.RGBA32, false)
        {
            name = TextureName + "Scaled",
        };
        RenderTexture renderTarget = RenderTexture.GetTemporary(
            maxWidth,
            newHeight,
            0,
            RenderTextureFormat.ARGB32
        );
        Graphics.Blit(source, renderTarget);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTarget;
        scaled.ReadPixels(new Rect(0, 0, maxWidth, newHeight), 0, 0);
        scaled.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTarget);
        Object.Destroy(source);
        return scaled;
    }
}
