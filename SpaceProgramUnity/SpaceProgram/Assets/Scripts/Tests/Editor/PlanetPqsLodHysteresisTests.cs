using NUnit.Framework;

public sealed class PlanetPqsLodHysteresisTests
{
    [Test]
    public void SplitMergeHysteresis_StaysBelowSplitThreshold()
    {
        Assert.Greater(PlanetPqsConstants.SplitMergeHysteresis, 0.5);
        Assert.Less(PlanetPqsConstants.SplitMergeHysteresis, 1.0);
    }

    [Test]
    public void NodeJustAboveSplitTarget_DoesNotMerge()
    {
        const double targetSpan = 1000.0;
        double parentSpan = targetSpan * 1.05;

        Assert.IsFalse(PlanetPqsLodRules.NodeSpanWantsMerge(parentSpan, targetSpan));
    }

    [Test]
    public void ChildSpanTimesHysteresis_WouldFlapTheSplitRing()
    {
        const double targetSpan = 1000.0;
        double parentSpan = targetSpan * 1.05;
        double childSpan = parentSpan * 0.5;

        Assert.LessOrEqual(childSpan, targetSpan * PlanetPqsConstants.SplitMergeHysteresis);
        Assert.IsFalse(PlanetPqsLodRules.NodeSpanWantsMerge(parentSpan, targetSpan));
    }

    [Test]
    public void NodeWellBelowTarget_WantsMerge()
    {
        const double targetSpan = 1000.0;
        Assert.IsTrue(PlanetPqsLodRules.NodeSpanWantsMerge(targetSpan * 0.5, targetSpan));
    }

    [Test]
    public void ViewerPathSiblingIsTooCoarse_OnlyWhenGapExceedsOneLevel()
    {
        Assert.IsFalse(PlanetPqsLodRules.ViewerPathSiblingIsTooCoarse(5, 5));
        Assert.IsFalse(PlanetPqsLodRules.ViewerPathSiblingIsTooCoarse(5, 4));
        Assert.IsTrue(PlanetPqsLodRules.ViewerPathSiblingIsTooCoarse(5, 3));
        Assert.IsTrue(PlanetPqsLodRules.ViewerPathSiblingIsTooCoarse(12, 1));
    }

    [Test]
    public void ViewerPathSiblingCountsAsNeighborhoodGap_IgnoresHorizonLeaves()
    {
        Assert.IsFalse(
            PlanetPqsLodRules.ViewerPathSiblingCountsAsNeighborhoodGap(9, 2, false));
        Assert.IsTrue(
            PlanetPqsLodRules.ViewerPathSiblingCountsAsNeighborhoodGap(9, 2, true));
        Assert.IsFalse(
            PlanetPqsLodRules.ViewerPathSiblingCountsAsNeighborhoodGap(6, 6, true));
    }

    [Test]
    public void ClampUvToNode_AdjacentSiblingUsesSharedEdgeNotCorner()
    {
        PlanetPqsLodRules.ClampUvToNode(
            0.25,
            0.25,
            0.5,
            1.0,
            0.0,
            0.5,
            out double u,
            out double v
        );
        Assert.AreEqual(0.5, u, 1e-9);
        Assert.AreEqual(0.25, v, 1e-9);
    }

    [Test]
    public void ClampUvToNode_InteriorPointStaysPut()
    {
        PlanetPqsLodRules.ClampUvToNode(
            0.25,
            0.4,
            0.0,
            0.5,
            0.0,
            0.5,
            out double u,
            out double v
        );
        Assert.AreEqual(0.25, u, 1e-9);
        Assert.AreEqual(0.4, v, 1e-9);
    }

    [Test]
    public void ViewerQuadParentIsPadDisk_RejectsCubeFaceLevels()
    {
        Assert.IsFalse(PlanetPqsLodRules.ViewerQuadParentIsPadDisk(0));
        Assert.IsFalse(PlanetPqsLodRules.ViewerQuadParentIsPadDisk(1));
        Assert.IsTrue(PlanetPqsLodRules.ViewerQuadParentIsPadDisk(2));
        Assert.IsTrue(PlanetPqsLodRules.ViewerQuadParentIsPadDisk(
            PlanetPqsConstants.MinViewerQuadParentLevel));
    }

    [Test]
    public void ViewerQuadIgnoresHorizon_PadTwoByTwoNotCubeFace()
    {
        Assert.IsTrue(PlanetPqsLodRules.ViewerQuadIgnoresHorizon(true, false, 0));
        Assert.IsTrue(PlanetPqsLodRules.ViewerQuadIgnoresHorizon(false, true, 2));
        Assert.IsTrue(PlanetPqsLodRules.ViewerQuadIgnoresHorizon(false, true, 3));
        Assert.IsTrue(PlanetPqsLodRules.ViewerQuadIgnoresHorizon(false, true, 4));
        Assert.IsTrue(PlanetPqsLodRules.ViewerQuadIgnoresHorizon(false, true, 5));
        Assert.IsFalse(PlanetPqsLodRules.ViewerQuadIgnoresHorizon(false, true, 0));
        Assert.IsFalse(PlanetPqsLodRules.ViewerQuadIgnoresHorizon(false, true, 1));
        Assert.IsFalse(PlanetPqsLodRules.ViewerQuadIgnoresHorizon(false, false, 4));
    }

    [Test]
    public void ClampedUvIsViewerPoint_FaceCenterSharedCorner()
    {
        Assert.IsTrue(PlanetPqsLodRules.ClampedUvIsViewerPoint(0.5, 0.5, 0.5, 0.5));
        Assert.IsFalse(PlanetPqsLodRules.ClampedUvIsViewerPoint(0.25, 0.5, 0.5, 0.5));
        Assert.IsFalse(PlanetPqsLodRules.ClampedUvIsViewerPoint(0.5, 0.0, 0.390, 0.159));
    }

    [Test]
    public void ClosestUvIsNodeCorner_VertexNotEdge()
    {
        Assert.IsTrue(
            PlanetPqsLodRules.ClosestUvIsNodeCorner(0.5, 0.5, 0.0, 0.5, 0.0, 0.5));
        Assert.IsFalse(
            PlanetPqsLodRules.ClosestUvIsNodeCorner(0.25, 0.5, 0.0, 0.5, 0.0, 0.5));
        Assert.IsFalse(
            PlanetPqsLodRules.ClosestUvIsNodeCorner(0.25, 0.25, 0.0, 0.5, 0.0, 0.5));
    }

    [Test]
    public void TryDiagonalCatchUpProbe_FaceCenterNwLeafHitsSe()
    {
        const double l8 = 1.0 / 256.0;
        Assert.IsTrue(
            PlanetPqsLodRules.TryDiagonalCatchUpProbe(
                0.4999, 0.5001,
                0.5 - l8, 0.5,
                0.5, 0.5 + l8,
                out double probeU,
                out double probeV));
        Assert.IsTrue(probeU > 0.5);
        Assert.IsTrue(probeV < 0.5);
        // Pad spawn is often several % of an L8 tile from the seam, not a 5% corner.
        Assert.IsTrue(
            PlanetPqsLodRules.TryDiagonalCatchUpProbe(
                0.498, 0.502,
                0.5 - l8, 0.5,
                0.5, 0.5 + l8,
                out probeU,
                out probeV));
        Assert.IsTrue(probeU > 0.5);
        Assert.IsTrue(probeV < 0.5);
        Assert.IsTrue(
            PlanetPqsLodRules.TryDiagonalCatchUpProbe(
                0.5, 0.5,
                0.5, 0.5 + l8,
                0.5, 0.5 + l8,
                out probeU,
                out probeV));
        Assert.IsTrue(probeU < 0.5);
        Assert.IsTrue(probeV < 0.5);
        Assert.IsFalse(
            PlanetPqsLodRules.TryDiagonalCatchUpProbe(
                0.39, 0.16,
                0.38, 0.40,
                0.15, 0.17,
                out _,
                out _));
    }

    [Test]
    public void UvInsideLodNode_SplitMidpoint_BelongsToExactlyOneChild()
    {
        Assert.IsFalse(PlanetPqsLodRules.UvInsideLodNode(0.5, 0.25, 0.0, 0.5, 0.0, 0.5));
        Assert.IsTrue(PlanetPqsLodRules.UvInsideLodNode(0.5, 0.25, 0.5, 1.0, 0.0, 0.5));
        Assert.IsFalse(PlanetPqsLodRules.UvInsideLodNode(0.25, 0.5, 0.0, 0.5, 0.0, 0.5));
        Assert.IsTrue(PlanetPqsLodRules.UvInsideLodNode(0.25, 0.5, 0.0, 0.5, 0.5, 1.0));
        Assert.IsTrue(PlanetPqsLodRules.UvInsideLodNode(0.5, 0.5, 0.5, 1.0, 0.5, 1.0));
        Assert.IsFalse(PlanetPqsLodRules.UvInsideLodNode(0.5, 0.5, 0.0, 0.5, 0.0, 0.5));
        Assert.IsFalse(PlanetPqsLodRules.UvInsideLodNode(0.5, 0.5, 0.5, 1.0, 0.0, 0.5));
        Assert.IsFalse(PlanetPqsLodRules.UvInsideLodNode(0.5, 0.5, 0.0, 0.5, 0.5, 1.0));
    }

    [Test]
    public void UvInsideLodNode_FaceMaxEdge_StaysInclusive()
    {
        Assert.IsTrue(PlanetPqsLodRules.UvInsideLodNode(1.0, 1.0, 0.5, 1.0, 0.5, 1.0));
        Assert.IsTrue(PlanetPqsLodRules.UvInsideLodNode(0.0, 0.0, 0.0, 0.5, 0.0, 0.5));
    }

    [Test]
    public void CanAllocateSplit_RequiresFourFreeSlots()
    {
        Assert.IsTrue(PlanetPqsLodRules.CanAllocateSplit(636, 640));
        Assert.IsFalse(PlanetPqsLodRules.CanAllocateSplit(637, 640));
        Assert.IsFalse(PlanetPqsLodRules.CanAllocateSplit(642, 640));
        Assert.IsTrue(PlanetPqsLodRules.ChunkCountExceedsCap(642, 640));
        Assert.IsFalse(PlanetPqsLodRules.ChunkCountExceedsCap(640, 640));
    }

    [Test]
    public void ChunkCapIsUnderPressure_AtThreeQuarters()
    {
        Assert.IsFalse(PlanetPqsLodRules.ChunkCapIsUnderPressure(355, 640));
        Assert.IsTrue(PlanetPqsLodRules.ChunkCapIsUnderPressure(480, 640));
        Assert.IsTrue(PlanetPqsLodRules.ChunkCapIsUnderPressure(642, 640));
    }

    [Test]
    public void StarveNonPadFinest_OnlyWhenNoSlotsOrViewerStillSplitting()
    {
        Assert.IsFalse(
            PlanetPqsLodRules.StarveNonPadFinestSplits(
                chunkCapUnderPressure: false,
                canAllocateMoreChunks: true,
                viewerLeafNeedsSplit: true));
        Assert.IsFalse(
            PlanetPqsLodRules.StarveNonPadFinestSplits(
                true, true, false));
        Assert.IsTrue(
            PlanetPqsLodRules.StarveNonPadFinestSplits(
                true, false, false));
        Assert.IsTrue(
            PlanetPqsLodRules.StarveNonPadFinestSplits(
                true, true, true));
        Assert.IsTrue(
            PlanetPqsLodRules.StarveNonPadFinestSplits(
                false, false, false));
    }

    [Test]
    public void ShouldClampLodToViewerRing_AtPressureOrNoSlots()
    {
        Assert.IsFalse(PlanetPqsLodRules.ShouldClampLodToViewerRing(false, true));
        Assert.IsTrue(PlanetPqsLodRules.ShouldClampLodToViewerRing(true, true));
        Assert.IsTrue(PlanetPqsLodRules.ShouldClampLodToViewerRing(false, false));
    }

    [Test]
    public void ChunkIsOutsideViewerFinestRing_UsesSlantMultiplier()
    {
        Assert.IsFalse(
            PlanetPqsLodRules.ChunkIsOutsideViewerFinestRing(4000.0, 4682.0));
        Assert.IsTrue(
            PlanetPqsLodRules.ChunkIsOutsideViewerFinestRing(
                4682.0 * 3.0,
                4682.0));
        Assert.IsFalse(
            PlanetPqsLodRules.ChunkIsOutsideViewerFinestRing(1000.0, 0.0));
    }

    [Test]
    public void AngularRingIndex_ZeroUnderNadir()
    {
        Assert.AreEqual(0, PlanetPqsLodRules.AngularRingIndex(0.0, 0.90));
        Assert.AreEqual(0, PlanetPqsLodRules.AngularRingIndex(0.63, 0.90));
        Assert.AreEqual(1, PlanetPqsLodRules.AngularRingIndex(1.27, 0.90));
        Assert.AreEqual(5, PlanetPqsLodRules.AngularRingIndex(2.53, 0.45));
        Assert.AreEqual(0, PlanetPqsLodRules.AngularRingIndex(2.53, 3.58));
    }

    [Test]
    public void DesiredLodForAngularRing_DropsOneLevelPerRing()
    {
        Assert.AreEqual(7, PlanetPqsLodRules.DesiredLodForAngularRing(7, 0));
        Assert.AreEqual(6, PlanetPqsLodRules.DesiredLodForAngularRing(7, 1));
        Assert.AreEqual(5, PlanetPqsLodRules.DesiredLodForAngularRing(7, 2));
        Assert.AreEqual(0, PlanetPqsLodRules.DesiredLodForAngularRing(7, 20));
        Assert.AreEqual(6, PlanetPqsLodRules.MinNeighborLevelToCloseCrack(7));
    }

    [Test]
    public void DesiredLodOutsideFinestQuad_TreatsRingZeroAsFirstRing()
    {
        Assert.AreEqual(6, PlanetPqsLodRules.DesiredLodOutsideFinestQuad(7, 0));
        Assert.AreEqual(6, PlanetPqsLodRules.DesiredLodOutsideFinestQuad(7, 1));
        Assert.AreEqual(5, PlanetPqsLodRules.DesiredLodOutsideFinestQuad(7, 2));
        Assert.AreEqual(0, PlanetPqsLodRules.DesiredLodOutsideFinestQuad(7, 20));
    }

    [Test]
    public void AngularRingLodCap_UsesMaxLodNotStuckViewerLeaf()
    {
        Assert.AreEqual(8, PlanetPqsLodRules.AngularRingLodCap(8, 0, true));
        Assert.AreEqual(7, PlanetPqsLodRules.AngularRingLodCap(8, 0, false));
        Assert.AreEqual(7, PlanetPqsLodRules.AngularRingLodCap(8, 1, false));
        Assert.AreEqual(6, PlanetPqsLodRules.AngularRingLodCap(8, 2, false));
        Assert.AreEqual(1, PlanetPqsLodRules.DesiredLodOutsideFinestQuad(2, 0));
    }

    [Test]
    public void ClampCatchUpToRingCap_StopsSameLevelPaintAfterViewerSettles()
    {
        Assert.AreEqual(14, PlanetPqsLodRules.ClampCatchUpToRingCap(14, 7, true, 7));
        Assert.AreEqual(7, PlanetPqsLodRules.ClampCatchUpToRingCap(7, 5, false, 5));
        Assert.AreEqual(5, PlanetPqsLodRules.ClampCatchUpToRingCap(7, 5, false, 7));
        Assert.AreEqual(13, PlanetPqsLodRules.ClampCatchUpToRingCap(13, 13, false, 13));
        Assert.AreEqual(6, PlanetPqsLodRules.MinNeighborLevelToCloseCrack(7));
        Assert.IsFalse(PlanetPqsLodRules.AngularRingCapBlocksSplit(1, 1, true));
        Assert.IsTrue(PlanetPqsLodRules.AngularRingCapBlocksSplit(1, 1, false));
        Assert.IsFalse(PlanetPqsLodRules.AllowAngularRingMerge(true));
        Assert.IsTrue(PlanetPqsLodRules.AllowAngularRingMerge(false));
    }

    [Test]
    public void AllowPressureMergeOfFinestOutsideRing_SkipsPadAndNearTiles()
    {
        Assert.IsFalse(
            PlanetPqsLodRules.AllowPressureMergeOfFinestOutsideRing(
                7, 6, true, true));
        Assert.IsFalse(
            PlanetPqsLodRules.AllowPressureMergeOfFinestOutsideRing(
                7, 6, false, false));
        Assert.IsTrue(
            PlanetPqsLodRules.AllowPressureMergeOfFinestOutsideRing(
                7, 6, true, false));
        Assert.IsFalse(
            PlanetPqsLodRules.AllowPressureMergeOfFinestOutsideRing(
                7, 4, true, false));
    }

    [Test]
    public void TouchForceAndInDiskBalance_YieldToPadWhenStarving()
    {
        Assert.IsTrue(PlanetPqsLodRules.AllowTouchCornerForceSplit(false, false));
        Assert.IsFalse(PlanetPqsLodRules.AllowTouchCornerForceSplit(true, false));
        Assert.IsTrue(PlanetPqsLodRules.AllowTouchCornerForceSplit(true, true));
        Assert.IsTrue(PlanetPqsLodRules.AllowInDiskSiblingBalance(true, true));
        Assert.IsFalse(PlanetPqsLodRules.AllowInDiskSiblingBalance(true, false));
        Assert.IsTrue(PlanetPqsLodRules.AllowInDiskSiblingBalance(false, false));
    }

    [Test]
    public void ViewerPadTwoByTwoIncomplete_IgnoresFaceQuads()
    {
        Assert.IsFalse(PlanetPqsLodRules.ViewerPadTwoByTwoIncomplete(1, true, true));
        Assert.IsTrue(PlanetPqsLodRules.ViewerPadTwoByTwoIncomplete(7, true, true));
        Assert.IsFalse(PlanetPqsLodRules.ViewerPadTwoByTwoIncomplete(7, true, false));
        Assert.IsFalse(PlanetPqsLodRules.ViewerPadTwoByTwoIncomplete(8, false, true));
    }
}
