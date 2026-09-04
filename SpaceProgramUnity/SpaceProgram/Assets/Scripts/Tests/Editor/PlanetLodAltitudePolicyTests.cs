using NUnit.Framework;

public sealed class PlanetLodAltitudePolicyTests
{
    const float EarthRadius = 637100f;

    [Test]
    public void GetMaxLodLevelForAltitude_DecreasesWithHeight()
    {
        int ground = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            500f,
            EarthRadius,
            15,
            15
        );
        int high = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            50000f,
            EarthRadius,
            15,
            15
        );

        Assert.Greater(ground, high);
        Assert.LessOrEqual(high, PlanetLodAltitudePolicy.CoarseMaxLodLevel);
    }

    [Test]
    public void GetMaxLodLevelForAltitude_IsNotInvertedAtThirtyKm()
    {
        int atThirtyKm = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            30000f,
            EarthRadius,
            15,
            15
        );

        Assert.Less(atThirtyKm, 10);
    }

    [Test]
    public void ApplyMaxLodHysteresis_RaisingSliderUnsticksWithoutDescent()
    {
        int afterDrop = PlanetLodAltitudePolicy.ApplyMaxLodHysteresis(
            rawLevel: 1,
            cachedLevel: 8,
            maxLodCeiling: 1,
            previousCeiling: 8,
            viewerDescending: false
        );
        Assert.AreEqual(1, afterDrop);

        int afterRaise = PlanetLodAltitudePolicy.ApplyMaxLodHysteresis(
            rawLevel: 8,
            cachedLevel: afterDrop,
            maxLodCeiling: 8,
            previousCeiling: 1,
            viewerDescending: false
        );
        Assert.AreEqual(8, afterRaise);
    }

    [Test]
    public void ApplyMaxLodHysteresis_AscentStillCoarsensOneLevelPerStep()
    {
        int next = PlanetLodAltitudePolicy.ApplyMaxLodHysteresis(
            rawLevel: 2,
            cachedLevel: 8,
            maxLodCeiling: 15,
            previousCeiling: 15,
            viewerDescending: false
        );
        Assert.AreEqual(7, next);
    }

    [Test]
    public void GetSplitSpanAltitudeMultiplier_GrowsWithAltitude()
    {
        float low = PlanetLodAltitudePolicy.GetSplitSpanAltitudeMultiplier(1000f, EarthRadius);
        float high = PlanetLodAltitudePolicy.GetSplitSpanAltitudeMultiplier(
            60000f,
            EarthRadius
        );

        Assert.Greater(high, low);
    }

    [Test]
    public void UpdateShellState_HidesChunksWhenShellAppears()
    {
        var state = new PlanetLodAltitudePolicy.AltitudeShellState();
        float altitude = EarthRadius * 0.13f;

        PlanetLodAltitudePolicy.UpdateShellState(
            ref state,
            altitude,
            EarthRadius,
            0.1f
        );

        Assert.IsTrue(state.ShellVisible);
        Assert.IsTrue(state.ChunksHidden);
    }

    [Test]
    public void UpdateShellState_SnapsDownWhenLandedBelowReshowBand()
    {
        var state = new PlanetLodAltitudePolicy.AltitudeShellState
        {
            SmoothedAltitudeMeters = EarthRadius * 0.2f,
            ShellVisible = true,
            ChunksHidden = true
        };

        PlanetLodAltitudePolicy.UpdateShellState(
            ref state,
            80f,
            EarthRadius,
            0.02f
        );

        Assert.IsFalse(state.ShellVisible);
        Assert.IsFalse(state.ChunksHidden);
        Assert.AreEqual(80f, state.SmoothedAltitudeMeters, 0.1f);
    }

    [Test]
    public void GetMaxLodLevelForAltitude_MoonPadAllowsGroundDepthAfterHop()
    {
        const float moonRadius = 173700f;
        int ground = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            50f,
            moonRadius,
            12,
            12
        );
        int hop = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            30000f,
            moonRadius,
            12,
            12
        );

        Assert.AreEqual(12, ground);
        Assert.AreEqual(PlanetLodAltitudePolicy.OrbitalMaxLodLevel, hop);
    }

    [Test]
    public void ApplyMaxLodHysteresis_DescentFromOrbitUnsticksGroundLod()
    {
        int afterLanding = PlanetLodAltitudePolicy.ApplyMaxLodHysteresis(
            rawLevel: 12,
            cachedLevel: 0,
            maxLodCeiling: 12,
            previousCeiling: 12,
            viewerDescending: true
        );

        Assert.AreEqual(12, afterLanding);
    }
}
