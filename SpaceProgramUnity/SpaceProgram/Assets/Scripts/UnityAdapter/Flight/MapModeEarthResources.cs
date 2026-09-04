using System.IO;
using UnityEngine;

/// <summary>
/// Map mode (M key) uses its own geographic texture, separate from RSS flight parallax colors.
/// Loaded lazily on first map-mode entry — never during flight planet rebuild.
/// Priority: earth_geographic.png → NASA TIFF → height-colored runtime bake → RSS EarthColor (remapped).
/// NASA assets load byte-in-byte. Map-mode globe uses the same canonical geographic UV
/// as simulation spawn and GEBCO (<see cref="PlanetCubeSphere.DirectionToGeographicUv"/>).
/// </summary>
static class MapModeEarthResources
{
    const int HeightMapWidth = 2048;
    const int HeightMapHeight = 1024;
    const int MaxRuntimeTiffWidth = 8192;

    static Texture2D mapModeTexture;
    static bool ownsMapModeTexture;
    static string mapModeSourceLabel;

    public static string SourceLabel => mapModeSourceLabel ?? "unset";

    public static Texture2D GetColorMap()
    {
        if (mapModeTexture != null)
        {
            return mapModeTexture;
        }

        PlanetTerrainTextureResources.EnsureLoaded();

        if (
            PlanetTerrainTextureResources.EarthColorMap != null &&
            PlanetTerrainTextureResources.EarthColorUsesCanonicalGeographicUv
        )
        {
            mapModeTexture = PlanetTerrainTextureResources.EarthColorMap;
            ownsMapModeTexture = false;
            mapModeSourceLabel = "shared canonical earth_geographic (flight)";
            ConfigureSampling(mapModeTexture);
            return mapModeTexture;
        }

        if (TryLoadExternalGeographicMap(out mapModeTexture, out string path))
        {
            ownsMapModeTexture = true;
            mapModeSourceLabel = "external: " + path;
            ConfigureSampling(mapModeTexture);
            Debug.Log(
                "MapModeEarth: using external geographic map at " + path +
                " (" + mapModeTexture.width + "x" + mapModeTexture.height + ")."
            );
            return mapModeTexture;
        }

        if (TryLoadNasaColorTiff(out mapModeTexture, out string tiffPath))
        {
            ownsMapModeTexture = true;
            mapModeSourceLabel = "NASA TIFF: " + tiffPath;
            ConfigureSampling(mapModeTexture);
            Debug.Log(
                "MapModeEarth: using NASA color TIFF at " + tiffPath +
                " (" + mapModeTexture.width + "x" + mapModeTexture.height + ")."
            );
            return mapModeTexture;
        }

        PlanetTerrainTextureResources.EnsureLoaded();

        if (BuildHeightColoredMap(out mapModeTexture))
        {
            ownsMapModeTexture = true;
            mapModeSourceLabel = "height-colored geographic (runtime)";
            ConfigureSampling(mapModeTexture);
            Debug.Log(
                "MapModeEarth: built " + HeightMapWidth + "x" + HeightMapHeight +
                " height-colored map for map mode. Bake NASA color via " +
                "'Space Program / Bake Earth Color Map 2K (.png)' for satellite imagery."
            );
            return mapModeTexture;
        }

        Texture2D rssColor = PlanetTerrainTextureResources.EarthColorMap;

        if (rssColor != null && ConvertRssColorMapToCanonical(rssColor, out mapModeTexture))
        {
            ownsMapModeTexture = true;
            mapModeSourceLabel = "RSS EarthColor (remapped to canonical UV)";
            ConfigureSampling(mapModeTexture);
            Debug.LogWarning(
                "MapModeEarth: remapped RSS EarthColor to canonical UV. " +
                "Drop NASA Blue Marble into StreamingAssets/MapMode/ for satellite imagery."
            );
            return mapModeTexture;
        }

        mapModeTexture = rssColor;
        ownsMapModeTexture = false;
        mapModeSourceLabel = "RSS EarthColor fallback (unremapped)";

        if (mapModeTexture != null)
        {
            ConfigureSampling(mapModeTexture);
        }

        Debug.LogWarning(
            "MapModeEarth: falling back to raw RSS EarthColorMap. " +
            "Add Earth21600x10800_geo.tif or earth_geographic.png to StreamingAssets/MapMode/."
        );
        return mapModeTexture;
    }

    public static void Invalidate()
    {
        if (ownsMapModeTexture && mapModeTexture != null)
        {
            Object.Destroy(mapModeTexture);
        }

        mapModeTexture = null;
        ownsMapModeTexture = false;
        mapModeSourceLabel = null;
    }

    /// <summary>Call after replacing StreamingAssets/MapMode/earth_geographic.png.</summary>
    public static void ReloadExternalMap()
    {
        Invalidate();
        GetColorMap();
    }

    static void ConfigureSampling(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.wrapModeU = TextureWrapMode.Repeat;
        texture.wrapModeV = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Trilinear;
    }

    static bool TryLoadNasaColorTiff(out Texture2D texture, out string loadedPath)
    {
        texture = null;
        loadedPath = null;
        string tiffPath = EarthGeographicMapPaths.ActiveSourceTiffPath;

        if (!File.Exists(tiffPath))
        {
            tiffPath = PlanetDataPaths.ColorTiffPath;
        }

        if (!File.Exists(tiffPath))
        {
            tiffPath = PlanetDataPaths.ColorTiffStandardPath;
        }

        if (!File.Exists(tiffPath))
        {
            return false;
        }

        try
        {
            int maxTiffWidth = EarthGeographicMapPaths.UseHighResolutionMap ?
                EarthGeographicMapPaths.HighMaxLoadWidth :
                EarthGeographicMapPaths.StandardMaxLoadWidth;
            GeotiffRgbReader.Raster raster = GeotiffRgbReader.ReadRgb(
                tiffPath,
                maxTiffWidth
            );
            texture = GeotiffRgbReader.ToTexture(raster, "MapModeEarthNasaTiff");
            loadedPath = tiffPath;
            WarnIfAspectRatioUnexpected(texture, tiffPath);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "MapModeEarth: failed to load NASA color TIFF (" + exception.Message + "). " +
                "Use menu 'Space Program / Bake Earth Color Map 2K/8K (.png)' in the Editor."
            );
            return false;
        }
    }

    static bool TryLoadExternalGeographicMap(out Texture2D texture, out string loadedPath)
    {
        texture = null;
        loadedPath = null;

        string[] candidates =
        {
            EarthGeographicMapPaths.ActivePngPath,
            EarthGeographicMapPaths.UseHighResolutionMap ?
                EarthGeographicMapPaths.StandardPngPath :
                EarthGeographicMapPaths.HighPngPath,
            Path.Combine(Application.streamingAssetsPath, "MapMode", "earth.png"),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!File.Exists(candidates[i]))
            {
                continue;
            }

            byte[] bytes = File.ReadAllBytes(candidates[i]);
            Texture2D loaded = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
                bytes,
                "MapModeEarthExternal"
            );

            if (loaded == null)
            {
                continue;
            }

            if (loaded.width > EarthGeographicMapPaths.ActiveMaxLoadWidth)
            {
                loaded = DownscaleTexture(
                    loaded,
                    EarthGeographicMapPaths.ActiveMaxLoadWidth
                );
            }

            WarnIfAspectRatioUnexpected(loaded, candidates[i]);
            texture = loaded;
            loadedPath = candidates[i];
            return true;
        }

        return false;
    }

    static Texture2D LoadRgbTextureFromImageBytes(byte[] imageBytes, string textureName)
    {
        return PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(imageBytes, textureName);
    }

    static Texture2D DownscaleTexture(Texture2D source, int maxWidth)
    {
        int newHeight = Mathf.Max(
            1,
            Mathf.RoundToInt(source.height * (maxWidth / (float)source.width))
        );
        var scaled = new Texture2D(maxWidth, newHeight, TextureFormat.RGB24, false)
        {
            name = "MapModeEarthExternalScaled",
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
        scaled.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTarget);
        Object.Destroy(source);
        return scaled;
    }

    static void WarnIfAspectRatioUnexpected(Texture2D texture, string path)
    {
        float aspect = texture.width / (float)texture.height;

        if (aspect < 1.8f || aspect > 2.2f)
        {
            Debug.LogWarning(
                "MapModeEarth: " + path + " aspect ratio is " + aspect.ToString("F2") +
                " (expected ~2.0 for equirectangular). Map may look stretched on the globe."
            );
        }
    }

    static bool BuildHeightColoredMap(out Texture2D texture)
    {
        texture = null;
        Texture2D heightMap = PlanetTerrainTextureResources.EarthHeightMap;

        if (heightMap == null || !heightMap.isReadable || heightMap.format != TextureFormat.R16)
        {
            return false;
        }

        texture = new Texture2D(HeightMapWidth, HeightMapHeight, TextureFormat.RGB24, false)
        {
            name = "MapModeHeightColoredEarth",
        };

        Color[] pixels = new Color[HeightMapWidth * HeightMapHeight];

        for (int y = 0; y < HeightMapHeight; y++)
        {
            float canonicalV = y / (float)(HeightMapHeight - 1);
            float rssTextureV = (float)PlanetGeographicCoords.MapGeographicVToRssEarthTextureV(canonicalV);

            for (int x = 0; x < HeightMapWidth; x++)
            {
                float geographicU = x / (float)(HeightMapWidth - 1);
                float heightMeters = SampleHeightMeters(heightMap, geographicU, rssTextureV);
                pixels[(y * HeightMapWidth) + x] = HeightMetersToMapColor(heightMeters);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return true;
    }

    static bool ConvertRssColorMapToCanonical(Texture2D rssColorMap, out Texture2D texture)
    {
        texture = null;

        if (rssColorMap == null || !rssColorMap.isReadable)
        {
            return false;
        }

        int width = rssColorMap.width;
        int height = rssColorMap.height;
        texture = new Texture2D(width, height, TextureFormat.RGB24, false)
        {
            name = "MapModeEarthRssCanonical",
        };

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float canonicalV = y / (float)(height - 1);
            float rssTextureV = (float)PlanetGeographicCoords.MapGeographicVToRssEarthTextureV(canonicalV);

            for (int x = 0; x < width; x++)
            {
                float geographicU = x / (float)(width - 1);
                pixels[(y * width) + x] = rssColorMap.GetPixelBilinear(geographicU, rssTextureV);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return true;
    }

    static float SampleHeightMeters(Texture2D heightMap, float geographicU, float rssTextureV)
    {
        Color sample = heightMap.GetPixelBilinear(geographicU, rssTextureV);
        return (float)PlanetSurfaceMaps.SampleEarthHeightMeters(sample.r);
    }

    static Color HeightMetersToMapColor(float heightMeters)
    {
        if (heightMeters < 0f)
        {
            return new Color(0.04f, 0.12f, 0.32f);
        }

        if (heightMeters < 200f)
        {
            float t = heightMeters / 200f;
            return Color.Lerp(
                new Color(0.04f, 0.12f, 0.32f),
                new Color(0.72f, 0.68f, 0.52f),
                t
            );
        }

        if (heightMeters < 800f)
        {
            float t = (heightMeters - 200f) / 600f;
            return Color.Lerp(
                new Color(0.72f, 0.68f, 0.52f),
                new Color(0.28f, 0.48f, 0.22f),
                t
            );
        }

        if (heightMeters < 2500f)
        {
            float t = (heightMeters - 800f) / 1700f;
            return Color.Lerp(
                new Color(0.28f, 0.48f, 0.22f),
                new Color(0.48f, 0.40f, 0.28f),
                t
            );
        }

        float snowT = Mathf.Clamp01((heightMeters - 2500f) / 3500f);
        return Color.Lerp(
            new Color(0.48f, 0.40f, 0.28f),
            new Color(0.88f, 0.90f, 0.94f),
            snowT
        );
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Invalidate();
    }
}
