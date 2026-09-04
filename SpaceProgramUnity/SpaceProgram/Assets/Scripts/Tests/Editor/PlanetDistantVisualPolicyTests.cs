using NUnit.Framework;

public sealed class PlanetDistantVisualPolicyTests
{
    const float EarthRadius = 637100f;

    [Test]
    public void ResolveGlobeQuality_StaysFullBelowBand()
    {
        PlanetDistantVisualPolicy.GlobeQuality quality =
            PlanetDistantVisualPolicy.ResolveGlobeQuality(
                1_200_000f,
                PlanetDistantVisualPolicy.GlobeQuality.Full
            );

        Assert.AreEqual(PlanetDistantVisualPolicy.GlobeQuality.Full, quality);
    }

    [Test]
    public void ResolveGlobeQuality_Uses2KAtOnePointFiveMillion()
    {
        PlanetDistantVisualPolicy.GlobeQuality quality =
            PlanetDistantVisualPolicy.ResolveGlobeQuality(
                1_600_000f,
                PlanetDistantVisualPolicy.GlobeQuality.Full
            );

        Assert.AreEqual(PlanetDistantVisualPolicy.GlobeQuality.Color2K, quality);
        Assert.IsTrue(PlanetDistantVisualPolicy.UsesDistantColor2K(quality));
        Assert.AreEqual(
            PlanetDistantVisualPolicy.DefaultShellSubdivisions,
            PlanetDistantVisualPolicy.GetShellSubdivisions(quality, mapMode: false)
        );
    }

    [Test]
    public void ResolveGlobeQuality_UsesCoarseAtFiveMillion()
    {
        PlanetDistantVisualPolicy.GlobeQuality quality =
            PlanetDistantVisualPolicy.ResolveGlobeQuality(
                5_100_000f,
                PlanetDistantVisualPolicy.GlobeQuality.Color2K
            );

        Assert.AreEqual(PlanetDistantVisualPolicy.GlobeQuality.Coarse, quality);
        Assert.AreEqual(
            PlanetDistantVisualPolicy.CoarseShellSubdivisions,
            PlanetDistantVisualPolicy.GetShellSubdivisions(quality, mapMode: false)
        );
        Assert.AreEqual(
            PlanetDistantVisualPolicy.DefaultShellSubdivisions,
            PlanetDistantVisualPolicy.GetShellSubdivisions(quality, mapMode: true)
        );
    }

    [Test]
    public void ResolveGlobeQuality_HysteresisKeeps2KOnDescent()
    {
        PlanetDistantVisualPolicy.GlobeQuality quality =
            PlanetDistantVisualPolicy.ResolveGlobeQuality(
                1_400_000f,
                PlanetDistantVisualPolicy.GlobeQuality.Color2K
            );

        Assert.AreEqual(PlanetDistantVisualPolicy.GlobeQuality.Color2K, quality);
    }

    [Test]
    public void GetVisualScale_IsOneOnThePad()
    {
        float scale = PlanetDistantVisualPolicy.GetVisualScale(0.0, 8.0, 5.0);
        Assert.AreEqual(1f, scale, 1e-4f);
    }

    [Test]
    public void GetVisualScale_ShrinksWhenFarLimbExceedsClip()
    {
        float atMillion = PlanetDistantVisualPolicy.GetVisualScale(
            1_000_000.0,
            0.0,
            0.0
        );
        float atEightMillion = PlanetDistantVisualPolicy.GetVisualScale(
            8_000_000.0,
            0.0,
            0.0
        );

        Assert.AreEqual(1f, atMillion, 1e-4f);
        Assert.Less(atEightMillion, 1f);

        float center = EarthRadius + 8_000_000f;
        float farLimb = center + EarthRadius +
            PlanetDistantVisualPolicy.AtmosphereShellHeightMeters;
        float expected =
            (WorldVisualBridge.FlightCameraFarClip * PlanetDistantVisualPolicy.FarClipFill) /
            farLimb;
        Assert.AreEqual(expected, atEightMillion, 1e-4f);
    }

    [Test]
    public void GetDistantSecondaryScale_MoonFromEarth_DoesNotPullToNearSky()
    {
        const float moonCenter = 4_500_000f;
        const float moonRadius = 173_700f;
        float clipOnly = PlanetDistantVisualPolicy.GetDistantSecondaryScale(
            moonCenter,
            moonRadius,
            pullToNearSky: false
        );
        float pulled = PlanetDistantVisualPolicy.GetDistantSecondaryScale(
            moonCenter,
            moonRadius,
            pullToNearSky: true
        );

        Assert.AreEqual(1f, clipOnly, 1e-4f);
        Assert.Greater(clipOnly, pulled);
        Assert.Greater(moonCenter * clipOnly, PlanetDistantVisualPolicy.DistantSecondaryTargetCenterMeters);
    }

    [Test]
    public void FlightDistantGlobeRenderQueue_IsBetweenAtmosphereAndClouds()
    {
        Assert.Greater(PlanetDistantVisualPolicy.FlightDistantGlobeRenderQueue, 3000);
        Assert.Less(PlanetDistantVisualPolicy.FlightDistantGlobeRenderQueue, 3200);
    }
}
