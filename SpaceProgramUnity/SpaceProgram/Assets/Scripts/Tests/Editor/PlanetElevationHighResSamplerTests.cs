using System;
using System.Reflection;
using NUnit.Framework;

public sealed class PlanetElevationHighResSamplerTests
{
    [TearDown]
    public void TearDown()
    {
        PlanetElevationCatalog.UnloadAll();
    }

    [Test]
    public void RawCoords_GreenwichEquator_MapsToGridCenter()
    {
        PlanetElevationRawCoords.LatitudeLongitudeDegreesToContinuousPixel(
            0.0,
            0.0,
            out double pixelX,
            out double pixelY
        );

        Assert.AreEqual(PlanetElevationRawFormat.GreenwichCenterColumn, pixelX, 0.5);
        Assert.AreEqual((PlanetElevationRawFormat.Height - 1) * 0.5, pixelY, 0.5);
    }

    [Test]
    public void RawCoords_NorthPole_MapsToTopRow()
    {
        PlanetElevationRawCoords.LatitudeLongitudeDegreesToContinuousPixel(
            90.0,
            0.0,
            out _,
            out double pixelY
        );

        Assert.AreEqual(0.0, pixelY, 1e-6);
    }

    [Test]
    public void RawCoords_SouthPole_MapsToBottomRow()
    {
        PlanetElevationRawCoords.LatitudeLongitudeDegreesToContinuousPixel(
            -90.0,
            0.0,
            out _,
            out double pixelY
        );

        Assert.AreEqual(PlanetElevationRawFormat.Height - 1, pixelY, 0.5);
    }

    [Test]
    public void RawCoords_WestLongitude_MapsToCanonicalPixelX()
    {
        PlanetElevationRawCoords.LatitudeLongitudeDegreesToContinuousPixel(
            0.0,
            -80.0,
            out double pixelX,
            out _
        );

        double geographicU = (-80.0 + 180.0) / 360.0;
        double expectedX = geographicU * PlanetElevationRawFormat.Width;

        Assert.AreEqual(expectedX, pixelX, 1.0);
    }

    [Test]
    public void GetHeightBilinearNew_CanonicalRawFile_ReadsLongitude()
    {
        const int width = PlanetElevationRawFormat.Width;
        const int height = 4;
        var elevations = new short[width * height];

        for (int i = 0; i < elevations.Length; i++)
        {
            elevations[i] = -120;
        }

        PlanetElevationRawCoords.LatitudeLongitudeDegreesToContinuousPixel(
            0.0,
            -80.0,
            out double capePixelX,
            out double equatorPixelY
        );
        int capeX = (int)Math.Round(capePixelX);
        int equatorY = (int)Math.Round(equatorPixelY);
        elevations[(equatorY * width) + capeX] = 420;

        using PlanetElevationMmapField field =
            PlanetElevationMmapField.OpenFromInt16Grid(elevations, width, height);

        double sample = PlanetElevationHighResSampler.GetHeightBilinearNew(
            field,
            0.0,
            -80.0 * (Math.PI / 180.0)
        );

        Assert.AreEqual(420.0, sample, 1.0);
    }

    [Test]
    public void GetHeightBilinearNew_UniformGrid_ReturnsElevation()
    {
        const int width = 4;
        const int height = 2;
        var elevations = new short[width * height];

        for (int i = 0; i < elevations.Length; i++)
        {
            elevations[i] = -120;
        }

        using PlanetElevationMmapField field =
            PlanetElevationMmapField.OpenFromInt16Grid(elevations, width, height);

        double sample = PlanetElevationHighResSampler.GetHeightBilinearNew(
            field,
            0.0,
            0.0
        );

        Assert.AreEqual(-120.0, sample, 0.5);
    }

    [Test]
    public void SampleForLod_CoarseGrid_UsesRamBuffer()
    {
        const int width = PlanetElevationLodRawFormat.CoarseWidth;
        const int height = 4;
        var elevations = new short[width * height];

        for (int i = 0; i < elevations.Length; i++)
        {
            elevations[i] = -50;
        }

        PlanetElevationRawCoords.LatitudeLongitudeDegreesToContinuousPixel(
            0.0,
            -80.0,
            width,
            height,
            out double capePixelX,
            out double equatorPixelY
        );
        int capeX = (int)Math.Round(capePixelX);
        int equatorY = (int)Math.Round(equatorPixelY);
        elevations[(equatorY * width) + capeX] = 512;

        PlanetElevationCatalog.UnloadAll();
        typeof(PlanetElevationCatalog)
            .GetField("coarseBuffer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, elevations);
        typeof(PlanetElevationCatalog)
            .GetField("coarseLoadAttempted", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, true);

        double sample = PlanetElevationCatalog.SampleForLod(
            0.0,
            -80.0 * (Math.PI / 180.0),
            lod: 1
        );

        Assert.AreEqual(512.0, sample, 1.0);
    }

    [Test]
    public void SampleForLod_NoSources_ReturnsZero()
    {
        PlanetElevationCatalog.UnloadAll();

        double sample = PlanetElevationCatalog.SampleForLod(
            0.0,
            0.0,
            lod: 2
        );

        Assert.AreEqual(0.0, sample, 1e-6);
    }

    [Test]
    public void SampleForLod_LaunchSiteParityZone_UsesFullMmapDespiteCoarseLod()
    {
        LaunchSiteConfig.Site previousSite = LaunchSiteConfig.Active;
        LaunchSiteConfig.SetActive(LaunchSiteConfig.CapeCanaveral);

        try
        {
            const int coarseWidth = PlanetElevationLodRawFormat.CoarseWidth;
            const int coarseHeight = 4;
            var coarseElevations = new short[coarseWidth * coarseHeight];

            for (int i = 0; i < coarseElevations.Length; i++)
            {
                coarseElevations[i] = -50;
            }

            const int highWidth = PlanetElevationRawFormat.Width;
            const int highHeight = 4;
            var highElevations = new short[highWidth * highHeight];

            for (int i = 0; i < highElevations.Length; i++)
            {
                highElevations[i] = -99;
            }

            double capeLatRad = LaunchSiteConfig.CapeCanaveral.LatitudeRadians;
            double capeLonRad = LaunchSiteConfig.CapeCanaveral.LongitudeRadians;
            PlanetElevationRawCoords.LatitudeLongitudeRadiansToContinuousPixel(
                capeLatRad,
                capeLonRad,
                highWidth,
                highHeight,
                out double capePixelX,
                out double capePixelY
            );
            int capeX = (int)Math.Round(capePixelX);
            int capeY = (int)Math.Round(capePixelY);
            highElevations[(capeY * highWidth) + capeX] = 7;

            PlanetElevationCatalog.UnloadAll();
            SetPrivateStaticField("coarseBuffer", coarseElevations);
            SetPrivateStaticField("coarseLoadAttempted", true);
            SetPrivateStaticField(
                "highResField",
                PlanetElevationMmapField.OpenFromInt16Grid(
                    highElevations,
                    highWidth,
                    highHeight
                )
            );

            double padSample = PlanetElevationCatalog.SampleForLod(
                capeLatRad,
                capeLonRad,
                lod: 1
            );
            Assert.AreEqual(7.0, padSample, 1.0);

            double farSample = PlanetElevationCatalog.SampleForLod(
                31.0 * (Math.PI / 180.0),
                91.0 * (Math.PI / 180.0),
                lod: 1
            );
            Assert.AreEqual(-50.0, farSample, 1.0);
        }
        finally
        {
            LaunchSiteConfig.SetActive(previousSite);
            PlanetElevationCatalog.UnloadAll();
        }
    }

    static void SetPrivateStaticField(string fieldName, object value)
    {
        typeof(PlanetElevationCatalog)
            .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, value);
    }
}
