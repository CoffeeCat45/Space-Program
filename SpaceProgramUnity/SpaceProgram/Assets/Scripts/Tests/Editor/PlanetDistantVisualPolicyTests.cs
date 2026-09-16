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
        Assert.GreaterOrEqual(atEightMillion, 0.002f);

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
    public void GetDistantSecondaryScale_EarthFromMoon_DoesNotPullToNearSky()
    {
        const float earthCenter = 4_500_000f;
        float clipOnly = PlanetDistantVisualPolicy.GetDistantSecondaryScale(
            earthCenter,
            EarthRadius,
            pullToNearSky: false,
            PlanetDistantVisualPolicy.DistantLimbHeightMeters
        );
        float pulled = PlanetDistantVisualPolicy.GetDistantSecondaryScale(
            earthCenter,
            EarthRadius,
            pullToNearSky: true,
            PlanetDistantVisualPolicy.DistantLimbHeightMeters
        );

        float farLimb = earthCenter + EarthRadius +
            PlanetDistantVisualPolicy.DistantLimbHeightMeters;
        float clipBudget = WorldVisualBridge.FlightCameraFarClip *
            PlanetDistantVisualPolicy.DistantSecondaryFarClipFill;
        float expectedClip = farLimb > clipBudget ? clipBudget / farLimb : 1f;

        Assert.AreEqual(expectedClip, clipOnly, 1e-4f);
        Assert.Greater(clipOnly, pulled);
        Assert.Greater(
            earthCenter * clipOnly,
            PlanetDistantVisualPolicy.DistantSecondaryTargetCenterMeters * 10f
        );
    }

    [Test]
    public void FlightDistantGlobeRenderQueue_IsBetweenAtmosphereAndClouds()
    {
        Assert.Greater(PlanetDistantVisualPolicy.FlightDistantGlobeRenderQueue, 3000);
        Assert.Greater(
            PlanetDistantVisualPolicy.FlightDistantAtmosphereRenderQueue,
            PlanetDistantVisualPolicy.FlightDistantGlobeRenderQueue
        );
        Assert.Less(PlanetDistantVisualPolicy.FlightDistantAtmosphereRenderQueue, 3200);
        Assert.Less(
            PlanetDistantVisualPolicy.DistantLimbHeightMeters,
            PlanetDistantVisualPolicy.AtmosphereShellHeightMeters
        );
        Assert.Less(PlanetDistantVisualPolicy.DistantLimbHeightMeters / EarthRadius, 0.03f);
        Assert.Less(
            PlanetDistantVisualPolicy.DistantLimbStrength,
            0.001f
        );
        Assert.AreEqual(3200, PlanetDistantVisualPolicy.CloudRenderQueue);
        Assert.Less(
            PlanetDistantVisualPolicy.FlightDistantGlobeRenderQueue,
            PlanetDistantVisualPolicy.CloudRenderQueue
        );
    }

    [Test]
    public void UsesDistantEarthWeather_AtSunSoiAltitude()
    {
        Assert.IsFalse(PlanetDistantVisualPolicy.UsesDistantEarthWeather(80_000f));
        Assert.IsFalse(
            PlanetDistantVisualPolicy.UsesDistantEarthWeather(
                PlanetDistantVisualPolicy.Color2KStartMeters - 1f
            )
        );
        Assert.IsTrue(
            PlanetDistantVisualPolicy.UsesDistantEarthWeather(
                PlanetDistantVisualPolicy.Color2KStartMeters
            )
        );
        Assert.IsTrue(PlanetDistantVisualPolicy.UsesDistantEarthWeather(8_920_000f));
    }
}
