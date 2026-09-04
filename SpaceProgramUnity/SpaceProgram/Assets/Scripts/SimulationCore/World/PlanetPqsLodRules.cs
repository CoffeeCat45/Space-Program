/// <summary>
/// LOD neighbor rules for quadtree crack prevention (terrain_plan §1.3).
/// </summary>
public static class PlanetPqsLodRules
{
    /// <summary>
    /// Finer chunk edge must be stitched when the neighbor across that edge is coarser.
    /// Higher LOD level number = finer detail.
    /// </summary>
    public static bool RequiresEdgeStitching(int localLodLevel, int neighborLodLevel)
    {
        return localLodLevel > neighborLodLevel;
    }

    /// <summary>
    /// Restricted quadtree: split only when every face-adjacent neighbor is at least
    /// this coarse (same level or finer). A finer leaf closes the gap by splitting the
    /// coarser neighbor; merge is blocked while a neighbor is more than one level finer,
    /// so forced catch-up does not flap.
    /// </summary>
    public static bool CanSplitWithoutCrack(
        int localLodLevel,
        int neighborLodLevelMinU,
        int neighborLodLevelMaxU,
        int neighborLodLevelMinV,
        int neighborLodLevelMaxV
    )
    {
        return neighborLodLevelMinU >= localLodLevel &&
            neighborLodLevelMaxU >= localLodLevel &&
            neighborLodLevelMinV >= localLodLevel &&
            neighborLodLevelMaxV >= localLodLevel;
    }

    /// <summary>
    /// Merge is allowed only when no neighbor is more than one LOD level finer.
    /// </summary>
    public static bool CanMergeWithoutCrack(
        int localLodLevel,
        int neighborLodLevelMinU,
        int neighborLodLevelMaxU,
        int neighborLodLevelMinV,
        int neighborLodLevelMaxV
    )
    {
        int maxAllowedNeighborLevel = localLodLevel + PlanetPqsConstants.MaxNeighborLodGapForMerge;

        return neighborLodLevelMinU <= maxAllowedNeighborLevel &&
            neighborLodLevelMaxU <= maxAllowedNeighborLevel &&
            neighborLodLevelMinV <= maxAllowedNeighborLevel &&
            neighborLodLevelMaxV <= maxAllowedNeighborLevel;
    }

    /// <summary>
    /// Merge SSE check uses this node's span, not its children's (half-size) span.
    /// </summary>
    public static bool NodeSpanWantsMerge(double nodeSpanMeters, double targetSpanMeters)
    {
        return nodeSpanMeters <= targetSpanMeters * PlanetPqsConstants.SplitMergeHysteresis;
    }

    /// <summary>
    /// Restricted quadtree: a leaf sibling of the viewer path is too coarse when the
    /// viewer is more than <see cref="PlanetPqsConstants.MaxNeighborLodGapForMerge"/>
    /// levels finer. The pad used to drill one quadrant to 64 m and freeze the other three.
    /// </summary>
    public static bool ViewerPathSiblingIsTooCoarse(int viewerLevel, int siblingLeafLevel)
    {
        return viewerLevel > siblingLeafLevel + PlanetPqsConstants.MaxNeighborLodGapForMerge;
    }

    /// <summary>
    /// Face-center pads sit on a 2×2 corner. Ancestry then has L2 leaves 20° away
    /// that are past the pad horizon — they are not a neighborhood gap and must not
    /// steal split budget from the ring under the ship.
    /// </summary>
    public static bool ViewerPathSiblingCountsAsNeighborhoodGap(
        int viewerLevel,
        int siblingLeafLevel,
        bool siblingIsInHorizonDisk
    )
    {
        return siblingIsInHorizonDisk &&
            ViewerPathSiblingIsTooCoarse(viewerLevel, siblingLeafLevel);
    }

    /// <summary>
    /// Finest 2×2 under a parent at <see cref="PlanetPqsConstants.MinViewerQuadParentLevel"/>
    /// or deeper. Not the four L1 face quadrants.
    /// </summary>
    public static bool ViewerQuadParentIsPadDisk(int parentLevel)
    {
        return parentLevel >= PlanetPqsConstants.MinViewerQuadParentLevel;
    }

    /// <summary>
    /// One more Split() rents 4 child chunk objects. Compare live GO count, not
    /// pending async meshes — otherwise several splits in one tick all pass and
    /// overshoot the cap (<c>chunks=642/640</c>).
    /// </summary>
    public static bool CanAllocateSplit(int activeChunkCount, int maxActiveChunks)
    {
        return activeChunkCount + 4 <= maxActiveChunks;
    }

    public static bool ChunkCountExceedsCap(int activeChunkCount, int maxActiveChunks)
    {
        return activeChunkCount > maxActiveChunks;
    }

    /// <summary>
    /// 75% of the chunk cap. Below this, all four face-corner L1s may still ring.
    /// At/above it, pad 2×2 completion beats extra-quadrant finest tiles.
    /// </summary>
    public static bool ChunkCapIsUnderPressure(int activeChunkCount, int maxActiveChunks)
    {
        if (maxActiveChunks <= 0)
        {
            return true;
        }

        return activeChunkCount >= (maxActiveChunks * 3) / 4 ||
            ChunkCountExceedsCap(activeChunkCount, maxActiveChunks);
    }

    /// <summary>
    /// When the 640 cap is filling, keep the finest ring around the ship instead of
    /// letting hitch-order / one L1 quadrant paint a line of L8 while the pad stays L7.
    /// </summary>
    public static bool ShouldClampLodToViewerRing(
        bool chunkCapUnderPressure,
        bool canAllocateMoreChunks
    )
    {
        return chunkCapUnderPressure || !canAllocateMoreChunks;
    }

    /// <summary>
    /// How far from the ship (slant) to keep tiles at the viewer's finest level
    /// when the cap is under pressure. Farther same-level 2×2s merge into the next ring.
    /// </summary>
    public const float ViewerFinestRingSlantMultiplier = 2.5f;

    public static bool ChunkIsOutsideViewerFinestRing(
        double chunkSlantMeters,
        double viewerSlantMeters
    )
    {
        if (viewerSlantMeters <= 0.0)
        {
            return false;
        }

        return chunkSlantMeters >
            viewerSlantMeters * ViewerFinestRingSlantMultiplier;
    }

    /// <summary>
    /// Far 2×2 whose children are at least as fine as the ship. Not the pad 2×2.
    /// Protecting the whole viewer L1 (87 km on the Moon) left a strip of L7/L8
    /// in that quadrant while the rest of the disk stayed coarser.
    /// </summary>
    public static bool AllowPressureMergeOfFinestOutsideRing(
        int viewerLeafLevel,
        int parentLevel,
        bool outsideViewerFinestRing,
        bool isPadDiskViewerSibling
    )
    {
        if (isPadDiskViewerSibling || viewerLeafLevel < 0 || !outsideViewerFinestRing)
        {
            return false;
        }

        return parentLevel + 1 >= viewerLeafLevel;
    }

    /// <summary>
    /// How many finest-tile widths this chunk sits from nadir. Ring 0 = under the ship.
    /// </summary>
    public static int AngularRingIndex(
        double nearestAngleDegrees,
        double viewerLeafAngularSpanDegrees
    )
    {
        if (nearestAngleDegrees <= 0.0 || viewerLeafAngularSpanDegrees <= 1e-9)
        {
            return 0;
        }

        return (int)(nearestAngleDegrees / viewerLeafAngularSpanDegrees);
    }

    /// <summary>
    /// Restricted-quadtree rings: finest under the ship, each ring one LOD coarser.
    /// Catch-up to the <em>same</em> level painted a blob / strip of maxL instead.
    /// </summary>
    public static int DesiredLodForAngularRing(int viewerLeafLevel, int ringIndex)
    {
        if (viewerLeafLevel < 0)
        {
            return 0;
        }

        if (ringIndex <= 0)
        {
            return viewerLeafLevel;
        }

        int desired = viewerLeafLevel - ringIndex;
        return desired < 0 ? 0 : desired;
    }

    /// <summary>
    /// Tiles that do not contain the ship start one ring out. Nearest-corner angle
    /// of an adjacent cell is often &lt; one leaf span (ring 0), which would keep
    /// cousins at the same LOD as the rocket.
    /// </summary>
    public static int DesiredLodOutsideFinestQuad(int viewerLeafLevel, int ringIndex)
    {
        int ring = ringIndex <= 0 ? 1 : ringIndex;
        return DesiredLodForAngularRing(viewerLeafLevel, ring);
    }

    /// <summary>
    /// Ring cap must use <paramref name="activeMaxLod"/> (slider + altitude), not the
    /// current viewer leaf. Passing a stuck L2 leaf made neighbors <c>RING cap=1</c>
    /// and the pad could not grow (<c>L2 V CRACK</c>, <c>chunks=9</c>).
    /// </summary>
    public static int AngularRingLodCap(
        int activeMaxLod,
        int ringIndex,
        bool containsViewer
    )
    {
        if (activeMaxLod < 0)
        {
            return 0;
        }

        if (containsViewer)
        {
            return activeMaxLod;
        }

        return DesiredLodOutsideFinestQuad(activeMaxLod, ringIndex);
    }

    /// <summary>
    /// Catch-up only closes a gap &gt; 1. Same-level neighbors are the next ring, not a defect.
    /// </summary>
    public static int MinNeighborLevelToCloseCrack(int finerLeafLevel)
    {
        return finerLeafLevel - PlanetPqsConstants.MaxNeighborLodGapForMerge;
    }

    /// <summary>
    /// RING must not freeze the neighbour that the viewer needs in order to split
    /// (<c>L2 V CRACK V-nL1</c> + <c>RING cap=1</c> → 17 chunks). Catch-up splits
    /// that leaf; RING merge must not collapse it on the next tick.
    /// </summary>
    public static bool AngularRingCapBlocksSplit(
        int level,
        int ringCap,
        bool isCoarserEdgeNeighborOfViewer
    )
    {
        if (isCoarserEdgeNeighborOfViewer)
        {
            return false;
        }

        return level >= ringCap;
    }

    /// <summary>
    /// While the viewer is CRACK-blocked below maxL, RING merge undoes catch-up
    /// (split L1→L2, merge back, <c>chunks=17</c>). Far leftover after maxL still merges.
    /// </summary>
    public static bool AllowAngularRingMerge(bool viewerLeafBlockedByCrack)
    {
        return !viewerLeafBlockedByCrack;
    }

    /// <summary>
    /// While the viewer tile still needs to split, catch-up may ignore RING.
    /// After that, RING may lower the target <em>only</em> when the neighbour is
    /// already within one LOD (otherwise <c>U+nL5</c> next to L8 never closes:
    /// cap=5, required=7, clamp, merge undoes the split).
    /// </summary>
    public static int ClampCatchUpToRingCap(
        int requiredLevel,
        int neighborRingCap,
        bool viewerStillNeedsSplit,
        int neighborLevel
    )
    {
        int required = requiredLevel < 0 ? 0 : requiredLevel;
        if (viewerStillNeedsSplit || neighborLevel < required)
        {
            return required;
        }

        if (neighborRingCap < required)
        {
            required = neighborRingCap;
        }

        return required < 0 ? 0 : required;
    }

    /// <summary>
    /// Stop extra-quadrant finest drills so the pad can finish when there is
    /// no room left. Do not starve just because a pad 2×2 is mixed while
    /// <c>chunks=483/640</c> — that blocked catch-up (<c>L7 CRACK</c> to L6)
    /// and left square tiles at 1 km. Max LOD 8 on the pad stays under 75%.
    /// </summary>
    public static bool StarveNonPadFinestSplits(
        bool chunkCapUnderPressure,
        bool canAllocateMoreChunks,
        bool viewerLeafNeedsSplit
    )
    {
        if (!canAllocateMoreChunks)
        {
            return true;
        }

        return chunkCapUnderPressure && viewerLeafNeedsSplit;
    }

    /// <summary>
    /// Touch-corner force-split (four L1s on a face-center pad) is for even rings
    /// while there is budget. Under starve, only the tile that Contains the ship.
    /// </summary>
    public static bool AllowTouchCornerForceSplit(
        bool starveNonPadFinest,
        bool nodeContainsViewer
    )
    {
        return !starveNonPadFinest || nodeContainsViewer;
    }

    /// <summary>
    /// In-disk 2×2 sibling-balance. Pad 2×2 (parent contains the ship) always;
    /// other in-disk 2×2s only when not starving the pad.
    /// </summary>
    public static bool AllowInDiskSiblingBalance(
        bool starveNonPadFinest,
        bool isPadDiskViewerSibling
    )
    {
        return isPadDiskViewerSibling || !starveNonPadFinest;
    }

    /// <summary>
    /// Parent of a pad 2×2 has both leaves and branches — mixed LOD under/near
    /// the ship (L8 <c>BUDGET</c> next to L9). Not L0/L1 face quads.
    /// </summary>
    public static bool ViewerPadTwoByTwoIncomplete(
        int parentLevel,
        bool anyChildIsLeaf,
        bool anyChildIsBranch
    )
    {
        return ViewerQuadParentIsPadDisk(parentLevel) && anyChildIsLeaf && anyChildIsBranch;
    }

    /// <summary>
    /// Node is on the pad 2×2: it contains the ship, or its parent does and that
    /// parent is not a cube-face root. Flight sibling-balance and mesh allocate
    /// use this so face-center pads still complete the 2×2 past the geometric limb.
    /// </summary>
    public static bool ViewerQuadIgnoresHorizon(
        bool nodeContainsViewer,
        bool parentContainsViewer,
        int parentLevel
    )
    {
        if (nodeContainsViewer)
        {
            return true;
        }

        return parentContainsViewer && ViewerQuadParentIsPadDisk(parentLevel);
    }

    /// <summary>
    /// Shared corner/edge of a 2×2: ClampUv on the node equals the viewer UV.
    /// Face-center pads sit on four L1 tiles; only one Contains, the other three Touch.
    /// </summary>
    public static bool ClampedUvIsViewerPoint(
        double clampedU,
        double clampedV,
        double viewerU,
        double viewerV
    )
    {
        double du = clampedU - viewerU;
        double dv = clampedV - viewerV;
        return du * du + dv * dv <= 1e-12;
    }

    /// <summary>
    /// Closest UV sits on a <em>vertex</em> of the node, not an edge interior.
    /// Face-center pads: the other three L1s only share that corner with the ship.
    /// Treating that corner as slant=altitude SSE-drilled three L7 spikes
    /// ("random dark squares") instead of rings.
    /// </summary>
    public static bool ClosestUvIsNodeCorner(
        double clampedU,
        double clampedV,
        double minU,
        double maxU,
        double minV,
        double maxV
    )
    {
        const double eps = 1e-9;
        bool onU = System.Math.Abs(clampedU - minU) <= eps ||
            System.Math.Abs(clampedU - maxU) <= eps;
        bool onV = System.Math.Abs(clampedV - minV) <= eps ||
            System.Math.Abs(clampedV - maxV) <= eps;
        return onU && onV;
    }

    /// <summary>
    /// UV radius around the cube-face centre where four L1s meet.
    /// 1/32 ≈ one L5 on the face. Cape Canaveral is ~0.11 from centre and must not fire.
    /// </summary>
    public const double FaceCenterDiagonalCatchUpUv = 1.0 / 32.0;

    /// <summary>
    /// Face-center pad: four L1s meet at UV 0.5. Four-edge catch-up never samples
    /// the opposite L1 (SE when V is in NW) → <c>L1q 8/6/8/8</c>.
    /// Fire when the ship is near that centre, not when it sits in a 5% corner of
    /// the finest leaf (pad spawn is often ~8% of an L8 tile from the seam).
    /// </summary>
    public static bool TryDiagonalCatchUpProbe(
        double viewerU,
        double viewerV,
        double minU,
        double maxU,
        double minV,
        double maxV,
        out double probeU,
        out double probeV
    )
    {
        probeU = 0.0;
        probeV = 0.0;
        const double faceCenter = 0.5;
        double du = viewerU - faceCenter;
        double dv = viewerV - faceCenter;
        if (System.Math.Abs(du) > FaceCenterDiagonalCatchUpUv ||
            System.Math.Abs(dv) > FaceCenterDiagonalCatchUpUv)
        {
            return false;
        }

        double uSpan = maxU - minU;
        double vSpan = maxV - minV;
        if (uSpan <= 1e-12 || vSpan <= 1e-12)
        {
            return false;
        }

        // Half-open UV: 0.5 belongs to the + quadrant. Opposite L1 from the ship.
        probeU = du < 0.0
            ? faceCenter + uSpan * 0.25
            : faceCenter - uSpan * 0.25;
        probeV = dv < 0.0
            ? faceCenter + vSpan * 0.25
            : faceCenter - vSpan * 0.25;
        return true;
    }

    /// <summary>
    /// Closest UV on a node rectangle. Same-face rings must use the shared edge,
    /// not a corner: a neighbour's nearest corner can sit past the pad horizon
    /// while the shared edge is a few metres from the ship.
    /// </summary>
    public static void ClampUvToNode(
        double u,
        double v,
        double minU,
        double maxU,
        double minV,
        double maxV,
        out double clampedU,
        out double clampedV
    )
    {
        clampedU = u < minU ? minU : (u > maxU ? maxU : u);
        clampedV = v < minV ? minV : (v > maxV ? maxV : v);
    }

    /// <summary>
    /// Quadtree child ownership: half-open on the max UV so a point on a split
    /// (u = mid, v = mid) belongs to exactly one child — the same rule as
    /// <c>FindLeafNodeAt</c> (<c>u &gt;= midU</c>, <c>v &gt;= midV</c>). Inclusive
    /// max on every child put the ship in two right (or two left) quarters at once.
    /// Face edge u=1 / v=1 stays inclusive so the last cell still owns the pole/seam.
    /// </summary>
    public static bool UvInsideLodNode(
        double u,
        double v,
        double minU,
        double maxU,
        double minV,
        double maxV
    )
    {
        return UvInsideLodInterval(u, minU, maxU) && UvInsideLodInterval(v, minV, maxV);
    }

    static bool UvInsideLodInterval(double t, double min, double max)
    {
        if (t < min)
        {
            return false;
        }

        if (max >= 1.0 - 1e-9)
        {
            return t <= max;
        }

        return t < max;
    }
}
