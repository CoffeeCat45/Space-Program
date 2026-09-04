using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Clean-room geographic color raster: PNG bytes on disk → pixel array → GPU texture.
/// Sampling uses the same canonical u/v law as stage 3 GetPixel probes.
/// </summary>
public sealed class EarthSandboxGeographicRaster
{
    private Color32[] _pixels;
    private Texture2D _gpuTexture;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public string SourcePath { get; private set; }

    public static bool TryLoadFromStreamingAssets(out EarthSandboxGeographicRaster raster)
    {
        string[] candidates = new[]
        {
            Path.Combine(Application.streamingAssetsPath, "MapMode", "earth_geographic.png"),
            Path.Combine(Application.streamingAssetsPath, "MapMode", "earth_geographic.jpg"),
        };

        foreach (string path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            raster = LoadFromFile(path);
            return raster != null;
        }

        raster = null;
        return false;
    }

    public static EarthSandboxGeographicRaster LoadFromFile(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(absolutePath);
        var decodeTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (!decodeTexture.LoadImage(bytes))
        {
            DestroyTexture(decodeTexture);
            return null;
        }

        Color32[] pixels = decodeTexture.GetPixels32();
        int width = decodeTexture.width;
        int height = decodeTexture.height;
        DestroyTexture(decodeTexture);

        var raster = new EarthSandboxGeographicRaster
        {
            _pixels = pixels,
            Width = width,
            Height = height,
            SourcePath = absolutePath,
        };
        raster.BuildGpuTexture();
        return raster;
    }

    private void BuildGpuTexture()
    {
        ReleaseGpuTexture();

        _gpuTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false)
        {
            name = "EarthSandboxGeographicGpu",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        _gpuTexture.SetPixels32(_pixels);
        _gpuTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
    }

    public Texture2D GpuTexture => _gpuTexture;

    public Vector2 CanonicalUvFromDegrees(double latitudeDegrees, double longitudeDegrees)
    {
        double latitudeRadians = latitudeDegrees * (Math.PI / 180.0);
        double longitudeRadians = longitudeDegrees * (Math.PI / 180.0);
        double u = PlanetGeographicCoords.LongitudeRadiansToGeographicU(longitudeRadians);
        double v = PlanetGeographicCoords.LatitudeRadiansToGeographicV(latitudeRadians);
        return new Vector2((float)u, (float)v);
    }

    public Vector2 CanonicalUvFromDirection(Vector3 unitDirection)
    {
        unitDirection.Normalize();
        var simDirection = new SimVector3(unitDirection.x, unitDirection.y, unitDirection.z).Normalize();
        PlanetGeographicCoords.DirectionToGeographicUv(
            simDirection,
            out double u,
            out double v
        );
        return new Vector2((float)u, (float)v);
    }

    public Color32 SampleCanonicalPixel(double latitudeDegrees, double longitudeDegrees)
    {
        Vector2 uv = CanonicalUvFromDegrees(latitudeDegrees, longitudeDegrees);
        return SampleCanonicalUv(uv.x, uv.y);
    }

    public Color32 SampleCanonicalUv(float geographicU, float geographicV)
    {
        if (_pixels == null || Width <= 0 || Height <= 0)
        {
            return new Color32(0, 0, 0, 255);
        }

        int x = Mathf.Clamp((int)(geographicU * Width), 0, Width - 1);
        int y = Mathf.Clamp((int)(geographicV * Height), 0, Height - 1);
        return _pixels[y * Width + x];
    }

    public Color SampleCanonicalBilinear(float geographicU, float geographicV)
    {
        if (_gpuTexture == null)
        {
            return Color.black;
        }

        return _gpuTexture.GetPixelBilinear(geographicU, geographicV);
    }

    public void Release()
    {
        ReleaseGpuTexture();
        _pixels = null;
        Width = 0;
        Height = 0;
        SourcePath = null;
    }

    private void ReleaseGpuTexture()
    {
        if (_gpuTexture != null)
        {
            DestroyTexture(_gpuTexture);
            _gpuTexture = null;
        }
    }

    private static void DestroyTexture(Texture texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(texture);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
