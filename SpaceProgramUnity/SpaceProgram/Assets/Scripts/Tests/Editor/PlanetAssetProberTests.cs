using NUnit.Framework;
using UnityEngine;

public sealed class PlanetAssetProberTests
{
    [Test]
    public void ResolvePixels_Cape_CanonicalAndMirroredXAreComplements()
    {
        PlanetAssetProber.ResolvePixels(
            28.57,
            -80.65,
            PlanetElevationRawFormat.Width,
            PlanetElevationRawFormat.Height,
            out double geographicU,
            out _,
            out int canonicalX,
            out int mirroredX,
            out _
        );

        Assert.AreEqual(0.276, geographicU, 0.01);
        Assert.AreNotEqual(canonicalX, mirroredX);
        Assert.AreEqual(
            PlanetElevationRawFormat.Width - 1,
            canonicalX + mirroredX,
            2,
            "Canonical and mirrored Cape pixel X should sum to width-1."
        );
    }

    [Test]
    public void InferHorizontalLayout_CanonicalMajority_ReturnsCanonical()
    {
        Assert.AreEqual(
            PlanetAssetProber.HorizontalLayout.Canonical,
            PlanetAssetProber.InferHorizontalLayout(6, 0, 6)
        );
    }

    [Test]
    public void InferHorizontalLayout_MirroredMajority_ReturnsMirrored()
    {
        Assert.AreEqual(
            PlanetAssetProber.HorizontalLayout.Mirrored,
            PlanetAssetProber.InferHorizontalLayout(1, 5, 6)
        );
    }

    [Test]
    public void InferHorizontalLayout_Tie_ReturnsAmbiguous()
    {
        Assert.AreEqual(
            PlanetAssetProber.HorizontalLayout.Ambiguous,
            PlanetAssetProber.InferHorizontalLayout(3, 3, 6)
        );
    }

    [Test]
    public void ElevationMatchesSite_CapeLand_AcceptsLowCoastalElevation()
    {
        var cape = PlanetAssetProber.StandardSites[0];

        Assert.IsTrue(PlanetAssetProber.ElevationMatchesSite(cape, 7.0));
        Assert.IsFalse(PlanetAssetProber.ElevationMatchesSite(cape, -120.0));
    }

    [Test]
    public void ColorMatchesSite_OceanAndLand_Classification()
    {
        var pacific = PlanetAssetProber.StandardSites[5];

        Assert.IsTrue(
            PlanetAssetProber.ColorMatchesSite(pacific, 8, 30, 80),
            "Blue pixel should match ocean site."
        );
        Assert.IsFalse(
            PlanetAssetProber.ColorMatchesSite(pacific, 180, 160, 70),
            "Green/brown pixel should not match ocean site."
        );
    }

    [Test]
    public void MapLayoutPolicy_CleanCanon_PassesThroughU()
    {
        PlanetMapLayoutPolicy policy = PlanetMapLayoutPolicy.CleanCanon;

        Assert.AreEqual(0.276f, policy.MapColorGeographicU(0.276f), 1e-4f);
        Assert.AreEqual(0.276, policy.MapElevationGeographicU(0.276), 1e-4f);
    }

    [Test]
    public void MapLayoutPolicy_MirroredColor_InvertsU()
    {
        var policy = new PlanetMapLayoutPolicy
        {
            ColorMapRequiresHorizontalFlip = true,
        };

        Assert.AreEqual(0.724f, policy.MapColorGeographicU(0.276f), 1e-3f);
    }

    [Test]
    public void MapLayoutPolicy_NasaGeographicPng_IsCleanCanon()
    {
        PlanetMapLayoutPolicy policy = PlanetMapLayoutPolicy.NasaGeographicPng;

        Assert.AreEqual(0.276f, policy.MapColorGeographicU(0.276f), 1e-4f);
        Assert.AreEqual(0.659f, policy.MapColorGeographicV(0.659f), 1e-4f);
    }

    [Test]
    public void InferColorRequiresVerticalFlip_BakedEarthGeographic_ReturnsFalse()
    {
        byte[] bytes = System.IO.File.ReadAllBytes(
            System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                "MapMode",
                "earth_geographic.png"
            )
        );
        var texture = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(bytes, "ProbeVTest");

        Assert.IsNotNull(texture);

        try
        {
            bool needsFlip = PlanetAssetProber.InferColorRequiresVerticalFlip(
                (float u, float v, out byte r, out byte g, out byte b) =>
                {
                    UnityEngine.Color sample = texture.GetPixelBilinear(u, v);
                    r = (byte)UnityEngine.Mathf.RoundToInt(sample.r * 255f);
                    g = (byte)UnityEngine.Mathf.RoundToInt(sample.g * 255f);
                    b = (byte)UnityEngine.Mathf.RoundToInt(sample.b * 255f);
                    return true;
                },
                PlanetAssetProber.StandardSites
            );

            Assert.IsFalse(
                needsFlip,
                "Baked earth_geographic.png is canonical V after GeotiffRgbReader + LoadImage."
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void InferColorRequiresHorizontalFlipGebco_MirroredSyntheticTexture_ReturnsTrue()
    {
        if (!TryEnsureGebcoLoadedForProbeTests())
        {
            Assert.Ignore("GEBCO raw not available for horizontal flip probe test.");
        }

        Texture2D mirroredTexture = CreateGebcoAlignedSyntheticTexture(mirrorLongitude: true);

        try
        {
            bool needsFlip = PlanetAssetProber.InferColorRequiresHorizontalFlipGebco(
                CreateColorSampler(mirroredTexture),
                PlanetAssetProber.StandardSites
            );

            Assert.IsTrue(
                needsFlip,
                "E/W mirrored color map should request colorFlipU=true after GEBCO tie-break."
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mirroredTexture);
        }
    }

    [Test]
    public void InferColorRequiresHorizontalFlipGebco_CanonicalSyntheticTexture_ReturnsFalse()
    {
        if (!TryEnsureGebcoLoadedForProbeTests())
        {
            Assert.Ignore("GEBCO raw not available for horizontal flip probe test.");
        }

        Texture2D canonicalTexture = CreateGebcoAlignedSyntheticTexture(mirrorLongitude: false);

        try
        {
            bool needsFlip = PlanetAssetProber.InferColorRequiresHorizontalFlipGebco(
                CreateColorSampler(canonicalTexture),
                PlanetAssetProber.StandardSites
            );

            Assert.IsFalse(
                needsFlip,
                "Canonically aligned synthetic color map should keep colorFlipU=false."
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canonicalTexture);
        }
    }

    [Test]
    public void InferColorRequiresHorizontalFlipGebco_BakedEarthGeographic_ReturnsFalse()
    {
        if (!TryEnsureGebcoLoadedForProbeTests())
        {
            Assert.Ignore("GEBCO raw not available for horizontal flip probe test.");
        }

        string pngPath = System.IO.Path.Combine(
            Application.streamingAssetsPath,
            "MapMode",
            "earth_geographic.png"
        );

        if (!System.IO.File.Exists(pngPath))
        {
            Assert.Ignore("earth_geographic.png not present.");
        }

        byte[] bytes = System.IO.File.ReadAllBytes(pngPath);
        Texture2D texture = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
            bytes,
            "ProbeEarthGeographicHorizontal"
        );

        Assert.IsNotNull(texture);

        try
        {
            int canonicalScore = PlanetAssetProber.ScoreThreePointTruthAgainstGebco(
                CreateColorSampler(texture),
                colorFlipU: false
            );
            int mirroredScore = PlanetAssetProber.ScoreThreePointTruthAgainstGebco(
                CreateColorSampler(texture),
                colorFlipU: true
            );

            Assert.GreaterOrEqual(
                canonicalScore,
                mirroredScore,
                "Canonical PNG should score at least as well as runtime flip on truth probes."
            );

            bool needsFlip = PlanetAssetProber.InferColorRequiresHorizontalFlipGebco(
                CreateColorSampler(texture),
                PlanetAssetProber.StandardSites
            );

            Assert.IsFalse(
                needsFlip,
                "Baked earth_geographic.png must not request runtime colorFlipU."
            );
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    static bool TryEnsureGebcoLoadedForProbeTests()
    {
        if (PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded)
        {
            return true;
        }

        string rawPath = System.IO.Path.Combine(
            UnityEngine.Application.dataPath,
            "StreamingAssets",
            "Planet",
            PlanetSurfaceConstants.ElevationRawFileName
        );

        if (!System.IO.File.Exists(rawPath))
        {
            return false;
        }

        PlanetElevationCatalog.LoadHighResFromFile(rawPath);
        PlanetElevationCatalog.UseHighResElevation = true;
        return PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded;
    }

    static PlanetAssetProber.ColorSampleDelegate CreateColorSampler(Texture2D texture)
    {
        return (float u, float v, out byte r, out byte g, out byte b) =>
        {
            UnityEngine.Color sample = texture.GetPixelBilinear(u, v);
            r = (byte)UnityEngine.Mathf.RoundToInt(sample.r * 255f);
            g = (byte)UnityEngine.Mathf.RoundToInt(sample.g * 255f);
            b = (byte)UnityEngine.Mathf.RoundToInt(sample.b * 255f);
            return true;
        };
    }

    static Texture2D CreateGebcoAlignedSyntheticTexture(bool mirrorLongitude)
    {
        const int width = 360;
        const int height = 180;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        var ocean = new UnityEngine.Color(0.03f, 0.12f, 0.45f);
        var land = new UnityEngine.Color(0.18f, 0.42f, 0.12f);
        var pixels = new UnityEngine.Color[width * height];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = ocean;
        }

        for (int siteIndex = 0; siteIndex < PlanetAssetProber.StandardSites.Length; siteIndex++)
        {
            PlanetAssetProber.GeographicProbeSite site = PlanetAssetProber.StandardSites[siteIndex];
            double latRadians = site.LatitudeDegrees * (System.Math.PI / 180.0);
            double lonRadians = site.LongitudeDegrees * (System.Math.PI / 180.0);
            var direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
                latRadians,
                lonRadians
            );
            PlanetGeographicCoords.DirectionToGeographicUv(
                direction,
                out double geographicU,
                out double geographicV
            );

            float sampleU = mirrorLongitude ? (float)(1.0 - geographicU) : (float)geographicU;
            int x = UnityEngine.Mathf.Clamp(
                UnityEngine.Mathf.RoundToInt(sampleU * (width - 1)),
                0,
                width - 1
            );
            int y = UnityEngine.Mathf.Clamp(
                UnityEngine.Mathf.RoundToInt((float)geographicV * (height - 1)),
                0,
                height - 1
            );
            pixels[(y * width) + x] = site.ExpectLand ? land : ocean;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
