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
            moonRadius * PlanetLodAltitudePolicy.MoonOrbitalCoarseAltitudeFraction,
            moonRadius,
            12,
            12
        );

        Assert.AreEqual(12, ground);
        Assert.AreEqual(PlanetLodAltitudePolicy.OrbitalMaxLodLevel, hop);
    }

    [Test]
    public void GetMaxLodLevelForAltitude_MoonCoarsensWithAltitudeInsteadOfHoldingUntilOrbit()
    {
        const float moonRadius = 173700f;
        int nearPad = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            1000f,
            moonRadius,
            8,
            8
        );
        int midClimb = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            18000f,
            moonRadius,
            8,
            8
        );
        int atTwentySevenKm = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            27000f,
            moonRadius,
            8,
            8
        );

        Assert.AreEqual(8, nearPad);
        Assert.Less(midClimb, nearPad);
        Assert.Less(atTwentySevenKm, midClimb);
        Assert.Greater(atTwentySevenKm, PlanetLodAltitudePolicy.CoarseMaxLodLevel);
        Assert.AreEqual(
            48000f,
            PlanetLodAltitudePolicy.GetViewerSubtreeProtectAltitudeMeters(moonRadius),
            1f
        );
        Assert.AreEqual(
            moonRadius * PlanetLodAltitudePolicy.MoonCoarsenStartAltitudeFraction,
            PlanetLodAltitudePolicy.GetPadForceSplitAltitudeMeters(moonRadius),
            1f
        );
        Assert.Less(
            PlanetLodAltitudePolicy.GetPadForceSplitAltitudeMeters(moonRadius),
            PlanetLodAltitudePolicy.GetViewerSubtreeProtectAltitudeMeters(moonRadius)
        );
    }

    [Test]
    public void GetMaxLodLevelForAltitude_MoonIsLodZeroBySeventyTwoKm()
    {
        const float moonRadius = 173700f;
        int atFortyFourKm = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            44479f,
            moonRadius,
            8,
            8
        );
        int atSixtyKm = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            60000f,
            moonRadius,
            8,
            8
        );
        int atL0 = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            72000f,
            moonRadius,
            8,
            8
        );

        Assert.Greater(atFortyFourKm, PlanetLodAltitudePolicy.OrbitalMaxLodLevel);
        Assert.Greater(atSixtyKm, PlanetLodAltitudePolicy.OrbitalMaxLodLevel);
        Assert.AreEqual(PlanetLodAltitudePolicy.OrbitalMaxLodLevel, atL0);
    }

    [Test]
    public void GetSplitSpanAltitudeMultiplier_MoonIsNotSqueezedVsEarth()
    {
        const float moonRadius = 173700f;
        float moonLow = PlanetLodAltitudePolicy.GetSplitSpanAltitudeMultiplier(1000f, moonRadius);
        float moonHigh = PlanetLodAltitudePolicy.GetSplitSpanAltitudeMultiplier(
            50000f,
            moonRadius
        );

        Assert.AreEqual(1f, moonLow, 0.01f);
        Assert.Greater(moonHigh, 3f);
    }

    [Test]
    public void GetMaxLodLevelForAltitude_MoonSecondAndThirdUnloadAreSpreadOut()
    {
        const float moonRadius = 173700f;
        int atPad = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            50f,
            moonRadius,
            12,
            12
        );
        int afterCoarsenStart = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            moonRadius * PlanetLodAltitudePolicy.MoonCoarsenStartAltitudeFraction * 1.15f,
            moonRadius,
            12,
            12
        );
        int atL2Band = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            moonRadius * PlanetLodAltitudePolicy.MoonCoarsenEndAltitudeFraction,
            moonRadius,
            12,
            12
        );
        int atMoonL0 = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            moonRadius * PlanetLodAltitudePolicy.MoonOrbitalCoarseAltitudeFraction,
            moonRadius,
            12,
            12
        );

        Assert.AreEqual(12, atPad);
        Assert.Less(afterCoarsenStart, 12);
        Assert.AreEqual(PlanetLodAltitudePolicy.CoarseMaxLodLevel, atL2Band);
        Assert.AreEqual(
            72000f,
            moonRadius * PlanetLodAltitudePolicy.MoonOrbitalCoarseAltitudeFraction,
            1f
        );
        Assert.AreEqual(PlanetLodAltitudePolicy.OrbitalMaxLodLevel, atMoonL0);
    }

    [Test]
    public void UpdateShellState_MoonPngSwapIsPastLodZero()
    {
        const float moonRadius = 173700f;
        var early = new PlanetLodAltitudePolicy.AltitudeShellState();
        PlanetLodAltitudePolicy.UpdateShellState(
            ref early,
            moonRadius * PlanetLodAltitudePolicy.MoonOrbitalCoarseAltitudeFraction,
            moonRadius,
            0.1f
        );
        Assert.IsFalse(early.ShellVisible);

        var atSeventyThreeKm = new PlanetLodAltitudePolicy.AltitudeShellState();
        PlanetLodAltitudePolicy.UpdateShellState(
            ref atSeventyThreeKm,
            73000f,
            moonRadius,
            0.1f
        );
        Assert.IsFalse(atSeventyThreeKm.ShellVisible);

        var late = new PlanetLodAltitudePolicy.AltitudeShellState();
        PlanetLodAltitudePolicy.UpdateShellState(
            ref late,
            moonRadius * PlanetLodAltitudePolicy.MoonMapShellBlendStartAltitudeFraction,
            moonRadius,
            0.1f
        );
        Assert.IsTrue(late.ShellVisible);
        Assert.IsTrue(late.ChunksHidden);
        Assert.AreEqual(
            PlanetLodAltitudePolicy.MoonOrbitalCoarseAltitudeFraction *
                PlanetLodAltitudePolicy.MoonMapShellAfterLodZeroScale,
            PlanetLodAltitudePolicy.MoonMapShellBlendStartAltitudeFraction,
            1e-6f
        );
        Assert.AreEqual(
            75000f,
            moonRadius * PlanetLodAltitudePolicy.MoonMapShellBlendStartAltitudeFraction,
            1f
        );
    }

    [Test]
    public void UpdateShellState_MoonDescentFromPngReshowsChunksBeforeFortyFiveKm()
    {
        const float moonRadius = 173700f;
        var state = new PlanetLodAltitudePolicy.AltitudeShellState();
        PlanetLodAltitudePolicy.UpdateShellState(
            ref state,
            moonRadius * PlanetLodAltitudePolicy.MoonMapShellBlendStartAltitudeFraction,
            moonRadius,
            0.1f
        );
        Assert.IsTrue(state.ChunksHidden);

        PlanetLodAltitudePolicy.UpdateShellState(
            ref state,
            44479f,
            moonRadius,
            0.02f
        );
        Assert.IsFalse(state.ShellVisible);
        Assert.IsFalse(state.ChunksHidden);
    }

    [Test]
    public void UpdateShellState_EarthBandsUnchanged()
    {
        const float earthRadius = 637100f;
        var state = new PlanetLodAltitudePolicy.AltitudeShellState();
        PlanetLodAltitudePolicy.UpdateShellState(
            ref state,
            earthRadius * 0.13f,
            earthRadius,
            0.1f
        );

        Assert.IsTrue(state.ShellVisible);
        Assert.IsFalse(PlanetLodAltitudePolicy.UsesMoonAscentBands(earthRadius));
        Assert.IsTrue(PlanetLodAltitudePolicy.UsesMoonAscentBands(173700f));
        Assert.AreEqual(
            PlanetLodAltitudePolicy.EarthViewerSubtreeProtectAltitudeMeters,
            PlanetLodAltitudePolicy.GetPadForceSplitAltitudeMeters(earthRadius),
            0.1f
        );
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
