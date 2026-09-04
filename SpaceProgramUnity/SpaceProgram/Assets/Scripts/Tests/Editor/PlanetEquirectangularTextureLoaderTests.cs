using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class PlanetEquirectangularTextureLoaderTests
{
    const int Width = 360;
    const int Height = 180;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if (PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded)
        {
            return;
        }

        string rawPath = Path.Combine(
            Application.dataPath,
            "StreamingAssets",
            "Planet",
            PlanetSurfaceConstants.ElevationRawFileName
        );

        if (!File.Exists(rawPath))
        {
            return;
        }

        PlanetElevationCatalog.LoadHighResFromFile(rawPath);
        PlanetElevationCatalog.UseHighResElevation = true;
    }

    static bool HasGebcoForOrientation =>
        PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded;

    [Test]
    public void LoadRgbFromImageBytes_PreservesCanonicalPixels()
    {
        Texture2D source = CreateSyntheticLongitudeTexture();
        Color sourceAtCape = SampleRawGeographic(source, 28.57, -80.65);
        byte[] bytes = source.EncodeToPNG();
        Object.DestroyImmediate(source);

        Texture2D loaded = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
            bytes,
            "SyntheticEarthLongitudeLoaded"
        );

        Assert.IsNotNull(loaded);
        Color loadedAtCape = PlanetEquirectangularTextureLoader.SampleColorAtGeographic(
            loaded,
            28.57,
            -80.65
        );

        Assert.AreEqual(sourceAtCape.r, loadedAtCape.r, 1e-4f);
        Assert.AreEqual(sourceAtCape.g, loadedAtCape.g, 1e-4f);
        Assert.AreEqual(sourceAtCape.b, loadedAtCape.b, 1e-4f);

        Object.DestroyImmediate(loaded);
    }

    [Test]
    public void SampleColorAtGeographic_UsesCanonicalGeographicU()
    {
        Texture2D texture = CreateSyntheticLongitudeTexture();

        Assert.Greater(
            LandScore(
                PlanetEquirectangularTextureLoader.SampleColorAtGeographic(
                    texture,
                    28.57,
                    -80.65
                )
            ),
            LandScore(
                PlanetEquirectangularTextureLoader.SampleColorAtGeographic(
                    texture,
                    28.57,
                    -160.0
                )
            ),
            "Cape Florida should read as land at canonical geographicU."
        );

        Object.DestroyImmediate(texture);
    }

    [Test]
    public void LoadRgbFromImageBytes_MatchesGebcoAtCapeWhenGebcoAvailable()
    {
        if (!HasGebcoForOrientation)
        {
            Assert.Ignore("GEBCO raw not available for load-path validation.");
        }

        Texture2D source = CreateSyntheticLongitudeTexture();
        byte[] bytes = source.EncodeToPNG();
        Object.DestroyImmediate(source);

        Texture2D loaded = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
            bytes,
            "SyntheticEarthLongitudeGebco"
        );

        Assert.IsTrue(SiteMatchesGebco(loaded, 28.57, -80.65));
        Assert.IsTrue(SiteMatchesGebco(loaded, 0.0, -80.0));
        Assert.IsTrue(SiteMatchesGebco(loaded, 0.0, 60.0));

        Object.DestroyImmediate(loaded);
    }

    static Texture2D CreateSyntheticLongitudeTexture()
    {
        var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false)
        {
            name = "SyntheticEarthLongitude",
        };

        Color[] pixels = new Color[Width * Height];
        Color land = new Color(0.72f, 0.62f, 0.28f);
        Color ocean = new Color(0.03f, 0.10f, 0.30f);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float geographicU = x / (float)(Width - 1);
                bool isWesternHemisphere = geographicU < 0.5f;
                pixels[(y * Width) + x] = isWesternHemisphere ? land : ocean;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    static Color SampleRawGeographic(
        Texture2D texture,
        double latitudeDegrees,
        double longitudeDegrees
    )
    {
        double latitudeRadians = latitudeDegrees * (System.Math.PI / 180.0);
        double longitudeRadians = longitudeDegrees * (System.Math.PI / 180.0);
        var direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            latitudeRadians,
            longitudeRadians
        );
        PlanetGeographicCoords.DirectionToGeographicUv(
            direction,
            out double geographicU,
            out double geographicV
        );
        return texture.GetPixelBilinear((float)geographicU, (float)geographicV);
    }

    static float LandScore(Color color)
    {
        return color.r + color.g - (color.b * 2f);
    }

    static bool SiteMatchesGebco(
        Texture2D texture,
        double latitudeDegrees,
        double longitudeDegrees
    )
    {
        double elevationMeters = PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(
            latitudeDegrees,
            longitudeDegrees
        );
        bool gebcoLand = elevationMeters > 0.0;
        Color color = PlanetEquirectangularTextureLoader.SampleColorAtGeographic(
            texture,
            latitudeDegrees,
            longitudeDegrees
        );
        bool colorLand = LandScore(color) > 0.08f;
        return gebcoLand == colorLand;
    }
}
