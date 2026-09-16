using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// One node in a cube-face quadtree. Leaf nodes own render/collision meshes.
/// </summary>
sealed class PlanetQuadtreeNode
{
    readonly PlanetQuadtreeLod lod;
    readonly int faceIndex;
    readonly int level;
    readonly float minU;
    readonly float maxU;
    readonly float minV;
    readonly float maxV;

    internal int FaceIndex => faceIndex;
    internal int Level => level;
    internal bool IsLeaf => children == null;
    internal float MinU => minU;
    internal float MaxU => maxU;
    internal float MinV => minV;
    internal float MaxV => maxV;
    internal PlanetQuadtreeNode DiagParent => parent;
    internal PlanetQuadtreeNode[] DiagChildren => children;

    PlanetQuadtreeNode[] children;
    PlanetQuadtreeNode parent;
    GameObject chunkObject;
    Mesh chunkMesh;
    Mesh sharedCacheMesh;
    Mesh stitchedMesh;
    Vector3[] cachedSurfaceVertices;
    Vector3[] cachedDrawnVertices;
    MeshCollider meshCollider;
    PlanetChunkDebugOutline debugOutline;
    static readonly PlanetChunkEdgeSide[] NeighborEdgeSides =
    {
        PlanetChunkEdgeSide.MinU,
        PlanetChunkEdgeSide.MaxU,
        PlanetChunkEdgeSide.MinV,
        PlanetChunkEdgeSide.MaxV
    };
    static int[] stitchEdgeIndexScratchA;
    static int[] stitchEdgeIndexScratchB;
    bool meshBuildInFlight;
    int meshBuildGeneration;

    internal bool IsMeshBuildInFlight => meshBuildInFlight;

    internal bool HasValidMesh => chunkMesh != null && chunkObject != null;

    internal bool HasRenderableMesh => HasValidMesh;

    /// <summary>
    /// Tile overlaps the geometric disk around the ship (nearest surface point),
    /// or is a sibling in the pad 2×2 (parent contains the ship, parent is not L0/L1).
    /// Face-center pads sit on every 2×2 corner: three siblings are past the geometric
    /// horizon even though the camera still sees them.
    /// </summary>
    internal bool IsInsideViewerSplitQuad()
    {
        if (ContainsViewerSurfaceDirection())
        {
            return true;
        }

        if (!lod.IsChunkBeyondHorizon(this))
        {
            return true;
        }

        return IsPadDiskViewerSibling();
    }

    bool IsPadDiskViewerSibling()
    {
        if (parent == null)
        {
            return false;
        }

        return PlanetPqsLodRules.ViewerQuadIgnoresHorizon(
            ChunkContainsViewerSurface(),
            parent.ContainsViewerSurfaceDirection(),
            parent.Level
        );
    }

    bool HasSubdividedSibling()
    {
        if (parent == null || parent.children == null)
        {
            return false;
        }

        PlanetQuadtreeNode[] siblings = parent.children;
        for (int i = 0; i < siblings.Length; i++)
        {
            if (siblings[i] != this && siblings[i].children != null)
            {
                return true;
            }
        }

        return false;
    }

    internal bool IsViewerFinestQuadMember()
    {
        return PlanetPqsLodRules.NodeIsViewerFinestQuadMember(
            ChunkContainsViewerSurface(),
            parent != null && parent.ContainsViewerSurfaceDirection());
    }

    bool WantsSiblingBalanceSplit()
    {
        if (!IsLeaf || parent == null || level >= lod.GetActiveMaxLodLevel())
        {
            return false;
        }

        if (!HasSubdividedSibling())
        {
            return false;
        }

        // Only leftover coarse siblings under the same parent as the viewer leaf.
        // Completing every in-disk 2×2 pulled cousins to the same LOD as the rocket.
        PlanetQuadtreeNode viewerLeaf = lod.FindAnyViewerLeaf();
        if (viewerLeaf == null || viewerLeaf.parent != parent)
        {
            return false;
        }

        return level < viewerLeaf.Level;
    }

    public PlanetQuadtreeNode(
        PlanetQuadtreeLod owner,
        int face,
        int lodLevel,
        float uMin,
        float uMax,
        float vMin,
        float vMax
    )
    {
        lod = owner;
        faceIndex = face;
        level = lodLevel;
        minU = uMin;
        maxU = uMax;
        minV = vMin;
        maxV = vMax;
    }

    public void Dispose()
    {
        DestroyChildren();
        DestroyLeafResources();
    }

    public void CollapseToRoot()
    {
        if (children != null)
        {
            for (int i = 0; i < children.Length; i++)
            {
                children[i].Dispose();
            }

            children = null;
        }

        DestroyLeafResources();
    }

    public void RefreshLod()
    {
        RefreshLodBudgeted(int.MaxValue, int.MaxValue);
    }

    public bool RefreshLodBudgeted(int splitBudget, int meshBuildBudget)
    {
        int splitsUsed = 0;
        int meshesUsed = 0;
        return RefreshLodBudgeted(ref splitsUsed, ref meshesUsed, splitBudget, meshBuildBudget);
    }

    internal bool RefreshLodBudgeted(
        ref int splitsUsed,
        ref int meshesUsed,
        int splitBudget,
        int meshBuildBudget
    )
    {
        bool pending = false;

        if (children != null)
        {
            pending |= RefreshChildrenViewerFirst(
                ref splitsUsed,
                ref meshesUsed,
                splitBudget,
                meshBuildBudget
            );

            if (ShouldMerge())
            {
                if (!HasValidMesh)
                {
                    if (!meshBuildInFlight)
                    {
                        StartLeafMeshBuild();
                    }

                    EnsureParentCoverageActive();
                    return true;
                }

                bool skipHitchMerge =
                    ShouldMergeForAltitudeCap() || ShouldMergeForAngularRing();
                if (skipHitchMerge || lod.AsyncLodSettings.TryConsumeUnloadMergeBudget())
                {
                    Merge();
                }
                else
                {
                    EnsureParentCoverageActive();
                }
            }
            else
            {
                EnsureParentCoverageActive();
            }

            return pending;
        }

        bool viewerOnChunk = ChunkContainsViewerSurface();

        if (viewerOnChunk &&
            !HasValidMesh &&
            !meshBuildInFlight &&
            !lod.IsLodSuspendedForAltitudeShell())
        {
            if (meshesUsed >= meshBuildBudget)
            {
                return true;
            }

            if (lod.ShouldAllocateMeshForChunk(this))
            {
                StartLeafMeshBuild();
                meshesUsed++;
            }

            if (!ShouldSplit())
            {
                return meshBuildInFlight;
            }
        }

        if (ShouldSplit())
        {
            if (lod.AsyncLodSettings.IsSplitBackpressureActive() && !viewerOnChunk)
            {
                if (!HasValidMesh && !meshBuildInFlight && lod.ShouldAllocateMeshForChunk(this))
                {
                    StartLeafMeshBuild();
                }

                return meshBuildInFlight;
            }

            if (!TryEnsureMeshBeforeSplit())
            {
                return meshBuildInFlight;
            }

            if (splitsUsed >= splitBudget || lod.IsLodTickTimeBudgetExhausted())
            {
                return true;
            }

            Split();
            splitsUsed++;
            EnsureParentCoverageActive();
            return true;
        }

        if (HasValidMesh)
        {
            return false;
        }

        if (!lod.ShouldAllocateMeshForChunk(this))
        {
            return false;
        }

        if (meshesUsed >= meshBuildBudget)
        {
            return true;
        }

        StartLeafMeshBuild();
        meshesUsed++;
        return meshBuildInFlight;
    }

    bool RefreshChildrenViewerFirst(
        ref int splitsUsed,
        ref int meshesUsed,
        int splitBudget,
        int meshBuildBudget
    )
    {
        if (children == null)
        {
            return false;
        }

        int viewerIndex = -1;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].ChunkContainsViewerSurface())
            {
                viewerIndex = i;
                break;
            }
        }

        bool catchUpSiblings = false;
        if (viewerIndex >= 0)
        {
            PlanetQuadtreeNode viewerLeaf = children[viewerIndex].FindViewerLeaf();
            int viewerLevel = viewerLeaf != null ? viewerLeaf.Level : children[viewerIndex].Level;
            for (int i = 0; i < children.Length; i++)
            {
                if (i == viewerIndex)
                {
                    continue;
                }

                if (children[i].IsLeaf &&
                    PlanetPqsLodRules.ViewerPathSiblingCountsAsNeighborhoodGap(
                        viewerLevel,
                        children[i].Level,
                        children[i].IntersectsHorizonDisk()
                    ) &&
                    children[i].Level < lod.GetAngularRingLodCap(children[i]))
                {
                    catchUpSiblings = true;
                    break;
                }
            }
        }

        bool pending = false;
        if (catchUpSiblings)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (i == viewerIndex)
                {
                    continue;
                }

                pending |= children[i].RefreshLodBudgeted(
                    ref splitsUsed,
                    ref meshesUsed,
                    splitBudget,
                    meshBuildBudget
                );
            }

            pending |= children[viewerIndex].RefreshLodBudgeted(
                ref splitsUsed,
                ref meshesUsed,
                splitBudget,
                meshBuildBudget
            );
            return pending;
        }

        if (viewerIndex >= 0)
        {
            pending |= children[viewerIndex].RefreshLodBudgeted(
                ref splitsUsed,
                ref meshesUsed,
                splitBudget,
                meshBuildBudget
            );
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (i == viewerIndex)
            {
                continue;
            }

            pending |= children[i].RefreshLodBudgeted(
                ref splitsUsed,
                ref meshesUsed,
                splitBudget,
                meshBuildBudget
            );
        }

        return pending;
    }

    public void RefreshLodCascade()
    {
        RefreshLodBudgeted(int.MaxValue, int.MaxValue);
    }

    public void SetLodDebugVisible(bool visible)
    {
        if (debugOutline != null)
        {
            debugOutline.SetVisible(visible);
        }

        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].SetLodDebugVisible(visible);
        }
    }

    public void RefreshDebugOutlines()
    {
        if (chunkObject != null)
        {
            UpdateDebugOutline();
            debugOutline?.SetVisible(lod.ShowLodDebug);
        }

        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].RefreshDebugOutlines();
        }
    }

    /// <summary>
    /// Pad tile still wants a 64 m cell under the ship. Restricted quadtree forbids
    /// bypassing <see cref="CanSplitWithoutLodCrack"/> — neighbors catch up first.
    /// </summary>
    bool IsPriorityForceSplit()
    {
        if (level >= lod.GetActiveMaxLodLevel())
        {
            return false;
        }

        if (lod.ViewerAltitudeMeters >=
            PlanetLodAltitudePolicy.GetPadForceSplitAltitudeMeters(lod.PlanetRadius))
        {
            return false;
        }

        float nodeSpan = EstimateNodeWorldSpanMeters();

        if (nodeSpan <= lod.MinChunkSpanMeters)
        {
            return false;
        }

        if (!TouchesViewerSurface())
        {
            return false;
        }

        if (!ChunkContainsViewerSurface() && lod.NodeClosestUvIsCorner(this))
        {
            return false;
        }

        if (PlanetPqsLodRules.AngularRingCapBlocksSplit(
                level,
                lod.GetAngularRingLodCap(this),
                IsCoarserEdgeNeighborOfViewer(),
                lod.ViewerIsBelowMaxLod()))
        {
            return false;
        }

        if (!PlanetPqsLodRules.AllowTouchCornerForceSplit(
                lod.ShouldStarveNonPadFinestSplits(),
                ChunkContainsViewerSurface()))
        {
            return false;
        }

        if (nodeSpan > lod.LaunchSiteMaxChunkSpanMeters)
        {
            return true;
        }

        return nodeSpan > lod.MinChunkSpanMeters * 1.5f &&
            GetViewerUvOffsetFromCenter() > 0.28f;
    }

    float GetViewerUvOffsetFromCenter()
    {
        Vector3 focusDirection = lod.GetShipFocusDirectionSimRaw();

        if (!PlanetCubeSphere.TryDirectionToFaceUv(
                focusDirection,
                out int focusFace,
                out float focusU,
                out float focusV))
        {
            return 0f;
        }

        if (focusFace != faceIndex)
        {
            return 0.5f;
        }

        float midU = (minU + maxU) * 0.5f;
        float midV = (minV + maxV) * 0.5f;
        float uSpan = Mathf.Max(maxU - minU, 1e-6f);
        float vSpan = Mathf.Max(maxV - minV, 1e-6f);
        float offsetU = Mathf.Abs(focusU - midU) / (uSpan * 0.5f);
        float offsetV = Mathf.Abs(focusV - midV) / (vSpan * 0.5f);
        return Mathf.Clamp01(Mathf.Max(offsetU, offsetV));
    }

    internal float EstimateArcDistanceToChunkCenterMeters()
    {
        Vector3 viewerDirection = lod.GetLodFocusDirectionSim();
        Vector3 centerDirection = GetCenterUnitDirectionSim();
        return PlanetCubeSphere.SurfaceDistanceMeters(
            viewerDirection,
            centerDirection,
            lod.PlanetRadius
        );
    }

    bool WantsSplit()
    {
        if (level >= lod.GetActiveMaxLodLevel())
        {
            return false;
        }

        if (lod.ShouldBlockSplitForVisibility(this))
        {
            return false;
        }

        if (lod.IsHardBackpressureActive() && !ChunkContainsViewerSurface())
        {
            return false;
        }

        if (lod.ShouldDeferPeripheralSplit(this))
        {
            return false;
        }

        if (PlanetPqsLodRules.AngularRingCapBlocksSplit(
                level,
                lod.GetAngularRingLodCap(this),
                IsCoarserEdgeNeighborOfViewer(),
                lod.ViewerIsBelowMaxLod()))
        {
            return false;
        }

        float nodeSpan = EstimateNodeWorldSpanMeters();

        if (nodeSpan <= lod.MinChunkSpanMeters)
        {
            return false;
        }

        float targetSpan = lod.ComputeSplitTargetSpanMeters(this);
        return nodeSpan > targetSpan;
    }

    bool ShouldSplit()
    {
        if (!lod.CanAllocateMoreChunks())
        {
            return false;
        }

        if (lod.HoldNonViewerSplits &&
            !ChunkContainsViewerSurface() &&
            !WantsSiblingBalanceSplit() &&
            !IsCoarserEdgeNeighborOfViewer())
        {
            return false;
        }

        if (lod.AsyncLodSettings.SkipBacksideMeshBuilds &&
            !lod.ShouldAllocateMeshForChunk(this) &&
            !ChunkContainsViewerSurface())
        {
            return false;
        }

        if (lod.IsHardBackpressureActive() &&
            !ChunkContainsViewerSurface() &&
            !WantsSiblingBalanceSplit())
        {
            return false;
        }

        if (!WantsSplit() && !IsPriorityForceSplit() && !WantsSiblingBalanceSplit())
        {
            return false;
        }

        return CanSplitWithoutLodCrack();
    }

    internal bool IsSplitBlockedByCrack()
    {
        return IsLeaf && !CanSplitWithoutLodCrack();
    }

    internal bool IsCoarserEdgeNeighborOfViewer()
    {
        PlanetQuadtreeNode viewer = lod.FindAnyViewerLeaf();
        if (viewer == null || !IsViewerCatchUpNeighbor())
        {
            return false;
        }

        return level < viewer.Level;
    }

    /// <summary>
    /// Leaf hit by the viewer's edge or face-center diagonal catch-up probe.
    /// RING merge used to collapse the diagonal L1 back to L7 after catch-up
    /// reached L8 (<c>L1q 8/7/8/8</c>) because same-level is not "coarser".
    /// </summary>
    internal bool IsViewerCatchUpNeighbor()
    {
        PlanetQuadtreeNode viewer = lod.FindAnyViewerLeaf();
        if (viewer == null || viewer == this || !viewer.IsLeaf || !IsLeaf)
        {
            return false;
        }

        return lod.ViewerLeafEdgeTouches(viewer, this) ||
            lod.ViewerLeafDiagonalTouches(viewer, this);
    }

    bool CanSplitWithoutLodCrack()
    {
        const float edgeEpsilon = 1e-5f;
        float midU = (minU + maxU) * 0.5f;
        float midV = (minV + maxV) * 0.5f;
        float uSpan = Mathf.Max(maxU - minU, edgeEpsilon);
        float vSpan = Mathf.Max(maxV - minV, edgeEpsilon);

        if (lod.FindAdjacentLeafLevel(faceIndex, minU - (uSpan * 0.25f), midV) < level &&
            !IsCrackNeighborExempt(minU - (uSpan * 0.25f), midV))
        {
            return false;
        }

        if (lod.FindAdjacentLeafLevel(faceIndex, maxU + (uSpan * 0.25f), midV) < level &&
            !IsCrackNeighborExempt(maxU + (uSpan * 0.25f), midV))
        {
            return false;
        }

        if (lod.FindAdjacentLeafLevel(faceIndex, midU, minV - (vSpan * 0.25f)) < level &&
            !IsCrackNeighborExempt(midU, minV - (vSpan * 0.25f)))
        {
            return false;
        }

        if (lod.FindAdjacentLeafLevel(faceIndex, midU, maxV + (vSpan * 0.25f)) < level &&
            !IsCrackNeighborExempt(midU, maxV + (vSpan * 0.25f)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// A coarse neighbor over the geometric horizon cannot split anyway, so blocking this
    /// chunk would create a permanent T-junction at the disk edge. Cube-face neighbors are
    /// real neighbors — do not treat UV outside [0, 1] as "no neighbor".
    /// </summary>
    bool IsCrackNeighborExempt(float probeU, float probeV)
    {
        PlanetQuadtreeNode neighbor = lod.FindNeighborLeaf(faceIndex, probeU, probeV);

        if (neighbor == null || neighbor == this)
        {
            return true;
        }

        return lod.IsChunkBeyondHorizon(neighbor);
    }

    bool CanMergeWithoutLodCrack()
    {
        const float edgeEpsilon = 1e-5f;
        float midU = (minU + maxU) * 0.5f;
        float midV = (minV + maxV) * 0.5f;
        int maxAllowedNeighborLevel = level + PlanetPqsConstants.MaxNeighborLodGapForMerge;

        if (lod.FindAdjacentLeafLevel(faceIndex, minU - edgeEpsilon, midV) > maxAllowedNeighborLevel)
        {
            return false;
        }

        if (lod.FindAdjacentLeafLevel(faceIndex, maxU + edgeEpsilon, midV) > maxAllowedNeighborLevel)
        {
            return false;
        }

        if (lod.FindAdjacentLeafLevel(faceIndex, midU, minV - edgeEpsilon) > maxAllowedNeighborLevel)
        {
            return false;
        }

        if (lod.FindAdjacentLeafLevel(faceIndex, midU, maxV + edgeEpsilon) > maxAllowedNeighborLevel)
        {
            return false;
        }

        return true;
    }

    bool ShouldMerge()
    {
        if (children == null)
        {
            return false;
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].children != null)
            {
                return false;
            }
        }

        if (
            AllChildrenBehindHorizon() &&
            !ContainsViewerSurfaceSubtree() &&
            !IsInsideViewerSplitQuad() &&
            CanMergeWithoutLodCrack()
        )
        {
            return true;
        }

        if (!CanMergeWithoutLodCrack())
        {
            return false;
        }

        if (ShouldMergeForAltitudeCap())
        {
            return true;
        }

        if (lod.IsFaceCenterCatchUpProtected(this))
        {
            return false;
        }

        if (ShouldMergeForAngularRing())
        {
            return true;
        }

        if (
            ContainsViewerSurfaceSubtree() &&
            lod.ViewerAltitudeMeters <
                PlanetLodAltitudePolicy.GetViewerSubtreeProtectAltitudeMeters(lod.PlanetRadius)
        )
        {
            return false;
        }

        // Same node as WantsSplit: span vs target. Comparing *child* span (half)
        // to target × 0.72 overlapped the split band and flapped the LOD ring.
        return PlanetPqsLodRules.NodeSpanWantsMerge(
            GetWorldSpanMeters(),
            lod.ComputeSplitTargetSpanMeters(this)
        );
    }

    bool ShouldMergeForAltitudeCap()
    {
        if (children == null || level == 0 || !AllChildrenAreLeaves())
        {
            return false;
        }

        return level >= lod.GetActiveMaxLodLevel();
    }

    bool ShouldMergeForAngularRing()
    {
        if (children == null || !AllChildrenAreLeaves())
        {
            return false;
        }

        if (!PlanetPqsLodRules.AngularRingMayMergeParent(
                ContainsViewerSurfaceSubtree() || lod.NodeContainsCachedViewerUv(this)))
        {
            return false;
        }

        if (lod.IsFaceCenterCatchUpProtected(this))
        {
            return false;
        }

        if (!PlanetPqsLodRules.AllowAngularRingMerge(lod.ViewerLeafBlockedByCrack()))
        {
            return false;
        }

        bool anyOverCap = false;
        bool anyAtOrUnderCap = false;
        for (int i = 0; i < children.Length; i++)
        {
            PlanetQuadtreeNode child = children[i];
            if (child.IsViewerFinestQuadMember())
            {
                continue;
            }

            if (child.IsViewerCatchUpNeighbor())
            {
                return false;
            }

            int cap = lod.GetAngularRingLodCap(child);
            int viewerLevel = lod.GetViewerLeafLevel();
            if (cap <= 0 &&
                viewerLevel >= 0 &&
                child.level <= viewerLevel &&
                lod.ViewerLeafNeedsSplit())
            {
                continue;
            }

            if (child.level > cap)
            {
                anyOverCap = true;
            }
            else
            {
                anyAtOrUnderCap = true;
            }
        }

        return PlanetPqsLodRules.AngularRingParentShouldMerge(anyOverCap, anyAtOrUnderCap);
    }

    /// <summary>
    /// Collapse leftover same-LOD blobs so rings are +1 around the ship, not a
    /// hitch-painted strip. Deepest first; bypasses hitch merge budget.
    /// </summary>
    internal void MergeOverAngularRingRecursive()
    {
        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].MergeOverAngularRingRecursive();
        }

        if (!ShouldMergeForAngularRing() || !CanMergeWithoutLodCrack())
        {
            return;
        }

        if (!HasValidMesh)
        {
            BuildLeafMeshSyncNow();
        }

        if (HasValidMesh)
        {
            Merge();
        }
    }

    /// <summary>
    /// Children leftover from a higher Max LOD / lower altitude. Hitch merge budget
    /// used to leave L8 next to L7 (<c>gate=MAX</c>) — black holes from the camera.
    /// Deepest first so L8 collapses before its L7 parent.
    /// </summary>
    internal void MergeOverMaxLodRecursive()
    {
        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].MergeOverMaxLodRecursive();
        }

        if (!ShouldMergeForAltitudeCap() || !CanMergeWithoutLodCrack())
        {
            return;
        }

        if (!HasValidMesh)
        {
            BuildLeafMeshSyncNow();
        }

        if (HasValidMesh)
        {
            Merge();
        }
    }

    bool ContainsViewerSurfaceSubtree()
    {
        if (ChunkContainsViewerSurface())
        {
            return true;
        }

        if (children == null)
        {
            return false;
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].ContainsViewerSurfaceSubtree())
            {
                return true;
            }
        }

        return false;
    }

    void Split()
    {
        if (!lod.CanAllocateMoreChunks())
        {
            return;
        }

        float midU = (minU + maxU) * 0.5f;
        float midV = (minV + maxV) * 0.5f;
        children = new PlanetQuadtreeNode[4];
        children[0] = new PlanetQuadtreeNode(lod, faceIndex, level + 1, minU, midU, minV, midV);
        children[1] = new PlanetQuadtreeNode(lod, faceIndex, level + 1, midU, maxU, minV, midV);
        children[2] = new PlanetQuadtreeNode(lod, faceIndex, level + 1, minU, midU, midV, maxV);
        children[3] = new PlanetQuadtreeNode(lod, faceIndex, level + 1, midU, maxU, midV, maxV);

        if (meshCollider != null)
        {
            meshCollider.enabled = false;
            meshCollider.sharedMesh = null;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].parent = this;
            // Rent now, not when the async mesh attaches. Several Split()s in one
            // tick used to all pass CanAllocate and land at chunks=642/640.
            children[i].EnsureChunkObject();
        }

        EnsureParentCoverageActive();
        parent?.NotifyChildSubtreeChanged();
        NotifyAdjacentLeavesToRestitch();
        lod.RecordLodFlap("split", this);
    }

    internal void NotifyChildSubtreeChanged()
    {
        EnsureParentCoverageActive();
    }

    /// <summary>
    /// Interior nodes keep a coverage mesh until direct children can draw.
    /// Children stay hidden until that swap so coarse and fine never draw together.
    /// </summary>
    bool ShouldBuildMeshSynchronously()
    {
        if (lod.SyncInDiskMeshes && lod.ShouldAllocateMeshForChunk(this))
        {
            return true;
        }

        if (level <= lod.AsyncLodSettings.SyncMeshBuildMaxLod)
        {
            return true;
        }

        if (ChunkContainsViewerSurface() &&
            level <= lod.AsyncLodSettings.ViewerSyncMeshBuildMaxLod)
        {
            return true;
        }

        if (ChunkContainsLaunchSiteDirection() &&
            lod.ViewerAltitudeMeters < 250f &&
            level <= lod.AsyncLodSettings.ViewerSyncMeshBuildMaxLod)
        {
            return true;
        }

        return false;
    }

    void ReleaseCoverageMeshForSplit()
    {
        CancelMeshBuild();
        UnbindChunkMesh();

        if (debugOutline != null)
        {
            Object.Destroy(debugOutline);
            debugOutline = null;
        }

        if (chunkObject != null)
        {
            Transform chunkTransform = chunkObject.transform;
            chunkTransform.localPosition = Vector3.zero;
            chunkTransform.localRotation = Quaternion.identity;
            chunkTransform.localScale = Vector3.one;
            chunkObject.SetActive(false);
        }
    }

    void Merge()
    {
        if (children == null || !HasValidMesh)
        {
            return;
        }

        HideDirectChildMeshes();
        SetChunkObjectVisible(true);

        for (int i = 0; i < children.Length; i++)
        {
            children[i].Dispose();
        }

        children = null;
        SetChunkObjectVisible(true);
        NotifyAdjacentLeavesToRestitch();
        ApplyNeighborEdgeStitch();
        lod.RecordLodFlap("merge", this);
    }

    bool TryEnsureMeshBeforeSplit()
    {
        if (HasValidMesh)
        {
            return true;
        }

        if (parent != null && parent.HasValidMesh)
        {
            return true;
        }

        if (meshBuildInFlight)
        {
            return false;
        }

        if (ShouldBuildMeshSynchronously())
        {
            BuildLeafMeshSyncNow();
            return HasValidMesh;
        }

        StartLeafMeshBuild();
        return HasValidMesh;
    }

    void EnsureChunkObject()
    {
        if (chunkObject != null)
        {
            return;
        }

        chunkObject = lod.RentChunkObject(BuildChunkName());
    }

    PlanetFlightChunkMeshPipeline.ChunkMeshBuildRequest CreateChunkMeshBuildRequest(
        int resolution
    )
    {
        return new PlanetFlightChunkMeshPipeline.ChunkMeshBuildRequest(
            faceIndex,
            minU,
            maxU,
            minV,
            maxV,
            level,
            resolution,
            lod.PlanetRadius,
            useLandOceanVertexPaint: CelestialTerrainRuntime.UseLandOceanVertexPaint
        );
    }

    void StartLeafMeshBuild()
    {
        if (HasValidMesh || meshBuildInFlight)
        {
            return;
        }

        int resolution = ResolveChunkResolution();
        PlanetFlightChunkMeshPipeline.ChunkMeshBuildRequest request =
            CreateChunkMeshBuildRequest(resolution);

        if (PlanetFlightChunkMeshPipeline.TryGetCachedMesh(
                request,
                PlanetFlightChunkMeshCache.BuildStyle.FlightBiome,
                out Mesh cachedMesh))
        {
            AttachChunkMesh(cachedMesh);
            return;
        }

        if (!lod.AsyncLodSettings.UseAsyncMeshBuild ||
            ShouldBuildMeshSynchronously())
        {
            BuildLeafMeshSyncNow();
            return;
        }

        PlanetFlightChunkMeshCache.ChunkMeshKey cacheKey =
            PlanetFlightChunkMeshCache.CreateKey(
                faceIndex,
                minU,
                maxU,
                minV,
                maxV,
                level,
                resolution,
                PlanetFlightChunkMeshCache.BuildStyle.FlightBiome,
                request.UseLandOceanVertexPaint
            );

        bool alreadyQueued = PlanetFlightChunkMeshPipeline.HasActiveBuild(cacheKey);

        if (!alreadyQueued &&
            !PlanetFlightChunkMeshPipeline.CanAcceptNewBuild(HasHighPriorityMeshBuild()))
        {
            return;
        }

        meshBuildInFlight = true;
        int generation = alreadyQueued ? meshBuildGeneration : ++meshBuildGeneration;
        PlanetFlightChunkCpuBuilder.FlightBiomeBuildParams flightParams =
            lod.CreateFlightBiomeBuildParams(EstimateNodeWorldSpanMeters(), resolution);

        PlanetFlightChunkMeshPipeline.StartBuild(
            request,
            PlanetFlightChunkMeshCache.BuildStyle.FlightBiome,
            PlanetElevationCatalog.IsHighResLoaded ?
                PlanetElevationCatalog.HighResField :
                null,
            flightParams,
            generation,
            () => IsMeshBuildStillValid(generation),
            mesh =>
            {
                if (!IsMeshBuildStillValid(generation))
                {
                    meshBuildInFlight = false;
                    return;
                }

                meshBuildInFlight = false;
                AttachChunkMesh(mesh);
            },
            () =>
            {
                if (generation == meshBuildGeneration)
                {
                    meshBuildInFlight = false;
                }
            },
            HasHighPriorityMeshBuild()
        );
    }

    bool HasHighPriorityMeshBuild()
    {
        return ContainsViewerSurfaceSubtree();
    }

    void BuildLeafMeshSyncNow()
    {
        if (HasValidMesh || meshBuildInFlight)
        {
            return;
        }

        int resolution = ResolveChunkResolution();
        PlanetFlightChunkMeshPipeline.ChunkMeshBuildRequest request =
            CreateChunkMeshBuildRequest(resolution);

        if (PlanetFlightChunkMeshPipeline.TryGetCachedMesh(
                request,
                PlanetFlightChunkMeshCache.BuildStyle.FlightBiome,
                out Mesh cachedMesh))
        {
            AttachChunkMesh(cachedMesh);
            return;
        }

        PlanetFlightChunkCpuBuilder.FlightBiomeBuildParams flightParams =
            lod.CreateFlightBiomeBuildParams(EstimateNodeWorldSpanMeters(), resolution);
        PlanetFlightChunkMeshPipeline.ChunkMeshCpuData cpuData =
            PlanetFlightChunkCpuBuilder.Build(
                request,
                PlanetElevationCatalog.IsHighResLoaded ?
                    PlanetElevationCatalog.HighResField :
                    null,
                PlanetFlightChunkMeshCache.BuildStyle.FlightBiome,
                flightParams
            );
        PlanetFlightChunkMeshCache.ChunkMeshKey cacheKey =
            PlanetFlightChunkMeshCache.CreateKey(
                faceIndex,
                minU,
                maxU,
                minV,
                maxV,
                level,
                resolution,
                PlanetFlightChunkMeshCache.BuildStyle.FlightBiome,
                request.UseLandOceanVertexPaint
            );
        Mesh mesh = PlanetFlightChunkMeshCache.FinalizeFromCpuData(
            cpuData,
            BuildChunkName() + "_Mesh",
            cacheKey
        );
        AttachChunkMesh(mesh);
    }

    int ResolveChunkResolution()
    {
        int lodGrid = PlanetFlightChunkMeshCache.GridResolutionForLod(level);
        return Mathf.Max(lodGrid, lod.ChunkVertexResolution);
    }

    bool IsMeshBuildStillValid(int generation)
    {
        return generation == meshBuildGeneration && !HasValidMesh;
    }

    void AttachChunkMesh(Mesh sourceMesh)
    {
        if (sourceMesh == null || HasValidMesh)
        {
            return;
        }

        if (!IsMeshBoundsFinite(sourceMesh))
        {
            Debug.LogWarning(
                "PlanetQuadtreeNode: rejected mesh with invalid bounds for " + BuildChunkName()
            );
            return;
        }

        EnsureChunkObject();
        sharedCacheMesh = sourceMesh;
        chunkMesh = sourceMesh;
        cachedSurfaceVertices = PlanetFlightChunkMeshCache.GetSurfaceVertices(sourceMesh);
        cachedDrawnVertices = cachedSurfaceVertices;

        MeshFilter meshFilter = chunkObject.GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            meshFilter = chunkObject.AddComponent<MeshFilter>();
        }

        meshFilter.sharedMesh = chunkMesh;

        MeshRenderer meshRenderer = chunkObject.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        }

        meshRenderer.sharedMaterial = lod.SurfaceMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;

        if (ShouldUseMeshCollider())
        {
            EnsureMeshCollider();
        }
        else if (meshCollider != null)
        {
            meshCollider.enabled = false;
            meshCollider.sharedMesh = null;
        }

        UpdateDebugOutline();
        debugOutline?.SetVisible(lod.ShowLodDebug);
        ApplyCoverageVisibilityAfterMeshAttach();
        ApplyNeighborEdgeStitch();
        NotifyAdjacentLeavesToRestitch();
    }

    void ApplyCoverageVisibilityAfterMeshAttach()
    {
        if (IsLeaf)
        {
            SetChunkObjectVisible(false);
            if (parent != null)
            {
                parent.EnsureParentCoverageActive();
                return;
            }

            SetChunkObjectVisible(HasValidMesh);
            return;
        }

        EnsureParentCoverageActive();
        parent?.EnsureParentCoverageActive();
    }

    internal void ApplyNeighborEdgeStitch()
    {
        if (!IsLeaf || sharedCacheMesh == null || chunkObject == null)
        {
            return;
        }

        int resolution = ResolveChunkResolution();
        Vector3[] cacheVerts = GetCachedSurfaceVertices();
        if (cacheVerts == null || cacheVerts.Length < resolution * resolution)
        {
            BindDrawnMesh(sharedCacheMesh);
            return;
        }

        bool anyStitch = false;
        Vector3[] working = null;

        TryStitchOneEdge(
            PlanetChunkEdgeSide.MinU,
            minU - 1e-5f,
            (minV + maxV) * 0.5f,
            resolution,
            cacheVerts,
            ref working,
            ref anyStitch
        );
        TryStitchOneEdge(
            PlanetChunkEdgeSide.MaxU,
            maxU + 1e-5f,
            (minV + maxV) * 0.5f,
            resolution,
            cacheVerts,
            ref working,
            ref anyStitch
        );
        TryStitchOneEdge(
            PlanetChunkEdgeSide.MinV,
            (minU + maxU) * 0.5f,
            minV - 1e-5f,
            resolution,
            cacheVerts,
            ref working,
            ref anyStitch
        );
        TryStitchOneEdge(
            PlanetChunkEdgeSide.MaxV,
            (minU + maxU) * 0.5f,
            maxV + 1e-5f,
            resolution,
            cacheVerts,
            ref working,
            ref anyStitch
        );

        if (!anyStitch)
        {
            DestroyStitchedClone();
            cachedDrawnVertices = cachedSurfaceVertices;
            BindDrawnMesh(sharedCacheMesh);
            return;
        }

        if (stitchedMesh == null)
        {
            stitchedMesh = Object.Instantiate(sharedCacheMesh);
            stitchedMesh.name = sharedCacheMesh.name + "_stitch";
        }

        UpdateSkirtVerticesFromSurface(working, resolution);
        stitchedMesh.vertices = working;
        PlanetFlightChunkMeshCache.AssignRadialOutwardNormals(stitchedMesh, working);
        stitchedMesh.RecalculateBounds();
        chunkMesh = stitchedMesh;
        cachedDrawnVertices = working;
        BindDrawnMesh(stitchedMesh);
    }

    void TryStitchOneEdge(
        PlanetChunkEdgeSide ourEdge,
        float probeU,
        float probeV,
        int resolution,
        Vector3[] cacheVerts,
        ref Vector3[] working,
        ref bool anyStitch
    )
    {
        PlanetQuadtreeNode neighbor = lod.FindNeighborLeaf(faceIndex, probeU, probeV);
        if (neighbor == null ||
            neighbor == this ||
            !neighbor.HasValidMesh ||
            neighbor.Level >= level)
        {
            return;
        }

        Vector3[] neighborVerts = neighbor.GetCachedDrawnVertices();
        int neighborRes = neighbor.ResolveChunkResolution();
        if (neighborVerts == null || neighborVerts.Length < neighborRes * neighborRes)
        {
            return;
        }

        int[] ourIndices = EnsureEdgeIndexScratch(ref stitchEdgeIndexScratchA, resolution);
        FillEdgeIndices(ourIndices, resolution, ourEdge);
        Vector3 ourMid = cacheVerts[ourIndices[resolution / 2]];
        PlanetChunkEdgeSide neighborEdge = PickClosestNeighborEdge(
            neighborVerts,
            neighborRes,
            ourMid
        );
        Vector3 snapMid = ClosestPointOnGridEdge(
            neighborVerts,
            neighborRes,
            neighborEdge,
            ourMid
        );
        float maxSnap = GetWorldSpanMeters() * 0.5f;
        if ((snapMid - ourMid).sqrMagnitude > maxSnap * maxSnap)
        {
            return;
        }

        if (working == null)
        {
            working = (Vector3[])cacheVerts.Clone();
        }

        for (int i = 0; i < resolution; i++)
        {
            int index = ourIndices[i];
            working[index] = ClosestPointOnGridEdge(
                neighborVerts,
                neighborRes,
                neighborEdge,
                working[index]
            );
        }

        anyStitch = true;
    }

    void NotifyAdjacentLeavesToRestitch()
    {
        const float edgeEpsilon = 1e-5f;
        float midU = (minU + maxU) * 0.5f;
        float midV = (minV + maxV) * 0.5f;
        RestitchLeafAt(minU - edgeEpsilon, midV);
        RestitchLeafAt(maxU + edgeEpsilon, midV);
        RestitchLeafAt(midU, minV - edgeEpsilon);
        RestitchLeafAt(midU, maxV + edgeEpsilon);
    }

    void RestitchLeafAt(float probeU, float probeV)
    {
        PlanetQuadtreeNode neighbor = lod.FindNeighborLeaf(faceIndex, probeU, probeV);
        if (neighbor != null && neighbor != this && neighbor.IsLeaf)
        {
            neighbor.ApplyNeighborEdgeStitch();
        }
    }

    void BindDrawnMesh(Mesh mesh)
    {
        if (chunkObject == null || mesh == null)
        {
            return;
        }

        chunkMesh = mesh;
        if (mesh == sharedCacheMesh)
        {
            cachedDrawnVertices = cachedSurfaceVertices;
        }

        MeshFilter meshFilter = chunkObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = mesh;
        }

        if (meshCollider != null && meshCollider.enabled)
        {
            meshCollider.sharedMesh = mesh;
        }
    }

    void DestroyStitchedClone()
    {
        if (stitchedMesh == null)
        {
            return;
        }

        if (chunkMesh == stitchedMesh)
        {
            chunkMesh = sharedCacheMesh;
        }

        if (chunkObject != null)
        {
            MeshFilter meshFilter = chunkObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == stitchedMesh)
            {
                meshFilter.sharedMesh = sharedCacheMesh;
            }
        }

        if (meshCollider != null && meshCollider.sharedMesh == stitchedMesh)
        {
            meshCollider.sharedMesh = sharedCacheMesh;
        }

        Object.Destroy(stitchedMesh);
        stitchedMesh = null;
        cachedDrawnVertices = cachedSurfaceVertices;
    }

    Vector3[] GetCachedSurfaceVertices()
    {
        if (cachedSurfaceVertices != null)
        {
            return cachedSurfaceVertices;
        }

        cachedSurfaceVertices = PlanetFlightChunkMeshCache.GetSurfaceVertices(sharedCacheMesh);
        if (cachedDrawnVertices == null)
        {
            cachedDrawnVertices = cachedSurfaceVertices;
        }

        return cachedSurfaceVertices;
    }

    Vector3[] GetCachedDrawnVertices()
    {
        if (cachedDrawnVertices != null)
        {
            return cachedDrawnVertices;
        }

        return GetCachedSurfaceVertices();
    }

    static int[] EnsureEdgeIndexScratch(ref int[] scratch, int resolution)
    {
        if (scratch == null || scratch.Length < resolution)
        {
            scratch = new int[resolution];
        }

        return scratch;
    }

    static void FillEdgeIndices(int[] indices, int resolution, PlanetChunkEdgeSide edge)
    {
        int last = resolution - 1;
        for (int i = 0; i < resolution; i++)
        {
            switch (edge)
            {
                case PlanetChunkEdgeSide.MinU:
                    indices[i] = i * resolution;
                    break;
                case PlanetChunkEdgeSide.MaxU:
                    indices[i] = (i * resolution) + last;
                    break;
                case PlanetChunkEdgeSide.MinV:
                    indices[i] = i;
                    break;
                default:
                    indices[i] = (last * resolution) + i;
                    break;
            }
        }
    }

    static PlanetChunkEdgeSide PickClosestNeighborEdge(
        Vector3[] neighborVerts,
        int neighborRes,
        Vector3 ourMid
    )
    {
        PlanetChunkEdgeSide best = PlanetChunkEdgeSide.MinU;
        float bestDist = float.MaxValue;
        for (int s = 0; s < NeighborEdgeSides.Length; s++)
        {
            Vector3 point = ClosestPointOnGridEdge(
                neighborVerts,
                neighborRes,
                NeighborEdgeSides[s],
                ourMid
            );
            float dist = (point - ourMid).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = NeighborEdgeSides[s];
            }
        }

        return best;
    }

    static Vector3 ClosestPointOnGridEdge(
        Vector3[] verts,
        int resolution,
        PlanetChunkEdgeSide edge,
        Vector3 point
    )
    {
        int[] indices = EnsureEdgeIndexScratch(ref stitchEdgeIndexScratchB, resolution);
        FillEdgeIndices(indices, resolution, edge);
        Vector3 best = verts[indices[0]];
        float bestDist = (best - point).sqrMagnitude;
        for (int i = 1; i < resolution; i++)
        {
            Vector3 a = verts[indices[i - 1]];
            Vector3 b = verts[indices[i]];
            Vector3 ab = b - a;
            float denom = Vector3.Dot(ab, ab);
            Vector3 candidate = a;
            if (denom > 1e-12f)
            {
                float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / denom);
                candidate = a + (ab * t);
            }

            float dist = (candidate - point).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        return best;
    }

    void UpdateSkirtVerticesFromSurface(Vector3[] verts, int resolution)
    {
        int grid = resolution * resolution;
        if (verts.Length < grid + (resolution * 4))
        {
            return;
        }

        float depth = (float)PlanetPqsSkirtBuilder.ComputeSkirtDepthMeters(
            EstimateNodeWorldSpanMeters(),
            PlanetPqsConstants.DefaultSkirtDepthFraction,
            resolution,
            level
        );
        if (depth <= 0f)
        {
            return;
        }

        int minVSkirt = grid;
        int maxVSkirt = grid + resolution;
        int minUSkirt = grid + (resolution * 2);
        int maxUSkirt = grid + (resolution * 3);
        int last = resolution - 1;
        for (int i = 0; i < resolution; i++)
        {
            verts[minVSkirt + i] = OffsetVertexTowardOrigin(verts[i], depth);
            verts[maxVSkirt + i] = OffsetVertexTowardOrigin(verts[(last * resolution) + i], depth);
            verts[minUSkirt + i] = OffsetVertexTowardOrigin(verts[i * resolution], depth);
            verts[maxUSkirt + i] = OffsetVertexTowardOrigin(verts[(i * resolution) + last], depth);
        }
    }

    static Vector3 OffsetVertexTowardOrigin(Vector3 surface, float depthMeters)
    {
        float mag = surface.magnitude;
        if (mag <= depthMeters + 1e-3f)
        {
            return surface;
        }

        return surface * ((mag - depthMeters) / mag);
    }

    /// <summary>
    /// Interior nodes keep a coverage mesh until their direct children can draw.
    /// Swap is atomic: either the parent draws, or all ready children, never both.
    /// </summary>
    internal void UpdateCoverageMeshState()
    {
        if (!IsLeaf && children != null)
        {
            for (int i = 0; i < children.Length; i++)
            {
                children[i].UpdateCoverageMeshState();
            }
        }

        EnsureParentCoverageActive();
    }

    /// <summary>
    /// Direct children can draw this node's area: each child is a leaf with a mesh, or an
    /// interior that still has its own coverage mesh, or an interior whose children can.
    /// Used instead of "any descendant" (holes) and "every leaf in the subtree" (coarse
    /// overlay until L10).
    /// </summary>
    bool DirectChildrenCanCover()
    {
        if (children == null)
        {
            return false;
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (!children[i].SubtreeCanRender())
            {
                return false;
            }
        }

        return true;
    }

    bool SubtreeCanRender()
    {
        if (IsLeaf)
        {
            return HasValidMesh;
        }

        if (HasValidMesh)
        {
            return true;
        }

        return DirectChildrenCanCover();
    }

    void EnsureParentCoverageActive()
    {
        if (IsLeaf || children == null)
        {
            return;
        }

        if (
            lod.AsyncLodSettings.SkipBacksideMeshBuilds &&
            lod.IsChunkFullyOutsideView(this) &&
            !ContainsViewerSurfaceSubtree()
        )
        {
            SetChunkObjectVisible(false);
            return;
        }

        if (DirectChildrenCanCover())
        {
            // Four descendants already cover this quad — never draw the parent on top.
            SetChunkObjectVisible(false);
            RevealDirectChildren();
            return;
        }

        if (!HasValidMesh)
        {
            SetChunkObjectVisible(false);
            if (!meshBuildInFlight)
            {
                if (ShouldBuildMeshSynchronously())
                {
                    BuildLeafMeshSyncNow();
                }
                else
                {
                    StartLeafMeshBuild();
                }
            }

            return;
        }

        HideDirectChildMeshes();
        SetChunkObjectVisible(true);
    }

    void RevealDirectChildren()
    {
        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            PlanetQuadtreeNode child = children[i];
            if (child.IsLeaf)
            {
                if (child.HasValidMesh)
                {
                    child.SetChunkObjectVisible(true);
                }

                continue;
            }

            child.EnsureParentCoverageActive();
        }
    }

    void HideDirectChildMeshes()
    {
        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].HideSubtreeMeshes();
        }
    }

    void HideSubtreeMeshes()
    {
        SetChunkObjectVisible(false);
        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].HideSubtreeMeshes();
        }
    }

    void SetChunkObjectVisible(bool visible)
    {
        if (chunkObject == null)
        {
            return;
        }

        MeshRenderer meshRenderer = chunkObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }

        if (chunkObject.activeSelf != visible)
        {
            chunkObject.SetActive(visible);
        }
    }

    internal void QueueLeafMeshBuild()
    {
        StartLeafMeshBuild();
    }

    void EnsureLeafMesh()
    {
        if (children != null)
        {
            return;
        }

        if (!HasValidMesh && !meshBuildInFlight)
        {
            StartLeafMeshBuild();
        }
        else if (HasValidMesh)
        {
            if (ShouldUseMeshCollider())
            {
                EnsureMeshCollider();
            }
            else if (meshCollider != null)
            {
                meshCollider.enabled = false;
                meshCollider.sharedMesh = null;
            }

            UpdateDebugOutline();
            debugOutline?.SetVisible(lod.ShowLodDebug);
        }
    }

    void DestroyChildren()
    {
        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].Dispose();
        }

        children = null;
    }

    void EnsureMeshCollider()
    {
        if (chunkMesh == null)
        {
            return;
        }

        if (meshCollider == null)
        {
            meshCollider = chunkObject.GetComponent<MeshCollider>();

            if (meshCollider == null)
            {
                meshCollider = chunkObject.AddComponent<MeshCollider>();
            }

            meshCollider.convex = false;
        }

        meshCollider.sharedMesh = chunkMesh;
        meshCollider.enabled = true;
    }

    void UnbindChunkMesh()
    {
        if (chunkObject != null)
        {
            MeshFilter meshFilter = chunkObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = null;
            }
        }

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.enabled = false;
        }

        chunkMesh = null;
    }

    void DestroyLeafResources()
    {
        CancelMeshBuild();
        meshBuildGeneration++;
        DestroyStitchedClone();
        sharedCacheMesh = null;
        cachedSurfaceVertices = null;
        cachedDrawnVertices = null;
        UnbindChunkMesh();

        if (debugOutline != null)
        {
            Object.Destroy(debugOutline);
            debugOutline = null;
        }

        if (chunkObject != null)
        {
            lod.ReleaseChunkObject(chunkObject);
            chunkObject = null;
        }

        meshCollider = null;
    }

    void CancelMeshBuild()
    {
        meshBuildGeneration++;
        meshBuildInFlight = false;
    }

    internal string DebugChunkName => BuildChunkName();

    string BuildChunkName()
    {
        return
            "Face" + faceIndex +
            "_L" + level +
            "_U" + Mathf.RoundToInt(minU * 1000f) +
            "_V" + Mathf.RoundToInt(minV * 1000f);
    }

    static bool IsFiniteVector(Vector3 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y) &&
            !float.IsInfinity(value.z);
    }

    static bool IsMeshBoundsFinite(Mesh mesh)
    {
        Bounds bounds = mesh.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        return
            IsFiniteVector(center) &&
            IsFiniteVector(extents) &&
            extents.sqrMagnitude > 1e-6f;
    }

    bool ShouldUseMeshCollider()
    {
        if (level < lod.MinColliderLodLevel)
        {
            return false;
        }

        float spanMeters = EstimateNodeWorldSpanMeters();
        if (spanMeters > 120f)
        {
            return false;
        }

        return EstimateDistanceToCameraMeters() <= lod.ColliderMaxSurfaceDistanceMeters;
    }

    float EstimateNodeWorldSpanMeters()
    {
        return lod.PlanetRadius * 2f / (1 << level);
    }

    internal float GetWorldSpanMeters()
    {
        return EstimateNodeWorldSpanMeters();
    }

    /// <summary>
    /// Euclidean distance from the viewer to this chunk's geographic center on the surface.
    /// </summary>
    internal float EstimateDistanceToCameraMeters()
    {
        float altitude = Mathf.Max(lod.ViewerAltitudeMeters, 1f);

        if (ChunkContainsViewerSurface())
        {
            return altitude;
        }

        Vector3 viewerDirection = lod.GetLodFocusDirectionSim();
        float arcDistance = EstimateArcDistanceToChunk(viewerDirection);
        return Mathf.Sqrt((altitude * altitude) + (arcDistance * arcDistance));
    }

    float EstimateArcDistanceToChunk(Vector3 viewerDirection)
    {
        if (lod.TryGetClosestUvOnNode(this, out float closestU, out float closestV))
        {
            float closestArcDistance = float.MaxValue;
            AccumulateClosestArcSample(ref closestArcDistance, viewerDirection, closestU, closestV);
            return closestArcDistance;
        }

        float fallbackArc = float.MaxValue;
        AccumulateClosestArcSample(ref fallbackArc, viewerDirection, minU, minV);
        AccumulateClosestArcSample(ref fallbackArc, viewerDirection, maxU, minV);
        AccumulateClosestArcSample(ref fallbackArc, viewerDirection, maxU, maxV);
        AccumulateClosestArcSample(ref fallbackArc, viewerDirection, minU, maxV);
        AccumulateClosestArcSample(
            ref fallbackArc,
            viewerDirection,
            (minU + maxU) * 0.5f,
            (minV + maxV) * 0.5f
        );
        AccumulateClosestArcSample(ref fallbackArc, viewerDirection, (minU + maxU) * 0.5f, minV);
        AccumulateClosestArcSample(ref fallbackArc, viewerDirection, (minU + maxU) * 0.5f, maxV);
        AccumulateClosestArcSample(ref fallbackArc, viewerDirection, minU, (minV + maxV) * 0.5f);
        AccumulateClosestArcSample(ref fallbackArc, viewerDirection, maxU, (minV + maxV) * 0.5f);
        return fallbackArc;
    }

    Vector3 GetChunkSurfaceLocalMeshPosition()
    {
        Vector3 centerUnitDirection = GetCenterUnitDirectionSim();
        float heightOffset = lod.SampleHeightOffset(centerUnitDirection);
        return PlanetCubeSphere.FlipMeshVertexPositionX(
            centerUnitDirection * (lod.PlanetRadius + heightOffset)
        );
    }

    Vector3 GetChunkSurfaceWorldPosition()
    {
        return lod.PlanetTransform.TransformPoint(GetChunkSurfaceLocalMeshPosition());
    }

    internal Vector3 GetCenterUnitDirectionSim()
    {
        return PlanetCubeSphere.FaceUvToUnitDirection(
            faceIndex,
            (minU + maxU) * 0.5f,
            (minV + maxV) * 0.5f
        ).normalized;
    }

    /// <summary>
    /// Chunk centre in world space on the reference sphere. Skips the elevation sample that
    /// <see cref="GetChunkSurfaceWorldPosition"/> does — visibility tests run for every chunk
    /// every tick, and the bounding radius already covers the terrain offset.
    /// </summary>
    internal Vector3 GetCenterWorldPositionApprox()
    {
        Vector3 meshLocal = PlanetCubeSphere.FlipMeshVertexPositionX(
            GetCenterUnitDirectionSim() * lod.PlanetRadius
        );
        return lod.PlanetTransform.TransformPoint(meshLocal);
    }

    internal Vector3 GetClosestSurfaceWorldPosition()
    {
        Vector3 closestDirection = GetClosestUnitDirectionSim();
        float heightOffset = lod.SampleHeightOffset(closestDirection);
        Vector3 meshLocal = PlanetCubeSphere.FlipMeshVertexPositionX(
            closestDirection * (lod.PlanetRadius + heightOffset)
        );
        return lod.PlanetTransform.TransformPoint(meshLocal);
    }

    internal Vector3 GetClosestUnitDirectionSim()
    {
        if (lod.TryGetClosestUvOnNode(this, out float u, out float v))
        {
            return PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v).normalized;
        }

        return GetCenterUnitDirectionSim();
    }

    void AccumulateClosestArcSample(
        ref float closestArcDistance,
        Vector3 viewerDirection,
        float u,
        float v
    )
    {
        Vector3 unitDirection = PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v);
        float arcDistance = PlanetCubeSphere.SurfaceDistanceMeters(
            viewerDirection,
            unitDirection,
            lod.PlanetRadius
        );
        closestArcDistance = Mathf.Min(closestArcDistance, arcDistance);
    }

    float GetDistanceToViewerWorldPosition(Vector3 worldPosition)
    {
        return (worldPosition - lod.ViewerLookAheadWorldPosition).magnitude;
    }

    bool ChunkContainsViewerSurface()
    {
        if (!PlanetCubeSphere.TryDirectionToFaceUv(
                lod.GetShipFocusDirectionSimRaw(),
                out int directionFace,
                out float u,
                out float v))
        {
            return false;
        }

        if (directionFace != faceIndex)
        {
            return false;
        }

        return PlanetPqsLodRules.UvInsideLodNode(u, v, minU, maxU, minV, maxV);
    }

    /// <summary>
    /// Ship UV is inside this node, or on a shared corner/edge (Clamp equals viewer).
    /// Face-center pads own one L1 by containment and three others by the corner.
    /// </summary>
    internal bool TouchesViewerSurface()
    {
        if (ChunkContainsViewerSurface())
        {
            return true;
        }

        if (!lod.TryGetClosestUvOnNode(this, out float closestU, out float closestV))
        {
            return false;
        }

        return lod.ClampedUvMatchesViewer(faceIndex, closestU, closestV);
    }

    internal int CollectFinestLeafLevel()
    {
        if (IsLeaf)
        {
            return level;
        }

        int finest = level;
        for (int i = 0; i < children.Length; i++)
        {
            finest = Mathf.Max(finest, children[i].CollectFinestLeafLevel());
        }

        return finest;
    }

    internal int CollectCoarsestLeafLevel()
    {
        if (IsLeaf)
        {
            return level;
        }

        int coarsest = int.MaxValue;
        for (int i = 0; i < children.Length; i++)
        {
            coarsest = Mathf.Min(coarsest, children[i].CollectCoarsestLeafLevel());
        }

        return coarsest == int.MaxValue ? level : coarsest;
    }

    internal bool ContainsViewerSurfaceDirection()
    {
        return ChunkContainsViewerSurface();
    }

    internal bool IntersectsViewCone()
    {
        return IntersectsHorizonDisk();
    }

    /// <summary>
    /// True when any part of this node can sit on the geometric horizon disk around the ship.
    /// Camera look does not count: the disk stays resident while you look around.
    /// </summary>
    internal bool IntersectsHorizonDisk()
    {
        if (ChunkContainsViewerSurface())
        {
            return true;
        }

        return !lod.IsChunkBeyondHorizon(this);
    }

    internal bool ContainsLaunchSiteDirection()
    {
        return ChunkContainsLaunchSiteDirection();
    }

    internal PlanetQuadtreeNode FindViewerLeaf()
    {
        if (!ChunkContainsViewerSurface())
        {
            return null;
        }

        if (children == null)
        {
            return this;
        }

        for (int i = 0; i < children.Length; i++)
        {
            PlanetQuadtreeNode leaf = children[i].FindViewerLeaf();
            if (leaf != null)
            {
                return leaf;
            }
        }

        return this;
    }

    internal bool AncestryHasIncompleteDirectChildren()
    {
        PlanetQuadtreeNode node = parent;
        while (node != null)
        {
            if (node.children != null && !node.DirectChildrenCanCover())
            {
                return true;
            }

            node = node.parent;
        }

        return false;
    }

    internal bool AncestryHasCoarseSiblings()
    {
        int viewerLevel = level;
        PlanetQuadtreeNode node = this;
        while (node.parent != null)
        {
            PlanetQuadtreeNode[] siblings = node.parent.children;
            if (siblings != null)
            {
                for (int i = 0; i < siblings.Length; i++)
                {
                    if (siblings[i] == node || !siblings[i].IsLeaf)
                    {
                        continue;
                    }

                    if (PlanetPqsLodRules.ViewerPathSiblingCountsAsNeighborhoodGap(
                            viewerLevel,
                            siblings[i].Level,
                            siblings[i].IntersectsHorizonDisk()
                        ) &&
                        siblings[i].Level < lod.GetAngularRingLodCap(siblings[i]))
                    {
                        return true;
                    }
                }
            }

            node = node.parent;
        }

        return false;
    }

    /// <summary>
    /// A pad 2×2 (parent ≥ L2) has both leaves and branches — mixed LOD under
    /// the ship. Stops at L0/L1 so a coarse face-corner L1 is not this.
    /// </summary>
    internal bool AncestryHasMixedPadTwoByTwo()
    {
        PlanetQuadtreeNode node = this;
        while (node.parent != null)
        {
            if (!PlanetPqsLodRules.ViewerQuadParentIsPadDisk(node.parent.Level))
            {
                break;
            }

            PlanetQuadtreeNode[] kids = node.parent.children;
            if (kids == null)
            {
                break;
            }

            bool anyLeaf = false;
            bool anyBranch = false;
            for (int i = 0; i < kids.Length; i++)
            {
                if (kids[i].IsLeaf)
                {
                    anyLeaf = true;
                }
                else
                {
                    anyBranch = true;
                }
            }

            if (PlanetPqsLodRules.ViewerPadTwoByTwoIncomplete(
                    node.parent.Level,
                    anyLeaf,
                    anyBranch))
            {
                return true;
            }

            node = node.parent;
        }

        return false;
    }

    internal bool FinestTwoByTwoIsMixed()
    {
        if (parent == null ||
            !PlanetPqsLodRules.ViewerQuadParentIsPadDisk(parent.Level) ||
            parent.children == null)
        {
            return false;
        }

        bool anyLeaf = false;
        bool anyBranch = false;
        PlanetQuadtreeNode[] kids = parent.children;
        for (int i = 0; i < kids.Length; i++)
        {
            if (kids[i].IsLeaf)
            {
                anyLeaf = true;
            }
            else
            {
                anyBranch = true;
            }
        }

        return PlanetPqsLodRules.ViewerPadTwoByTwoIncomplete(
            parent.Level,
            anyLeaf,
            anyBranch
        );
    }

    internal int FinestTwoByTwoParentLevel()
    {
        return parent != null ? parent.Level : -1;
    }

    /// <summary>
    /// In-disk leaf still wants a mesh or a split. Idle-skip used to freeze that after
    /// the viewer tile hit 64 m, so three siblings stayed coarse until a scenario reload
    /// rebuilt into a warm mesh cache.
    /// </summary>
    internal bool HasUnfinishedDiskLod()
    {
        if (children != null)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].HasUnfinishedDiskLod())
                {
                    return true;
                }
            }

            return !DirectChildrenCanCover();
        }

        if (!HasValidMesh && !meshBuildInFlight && lod.ShouldAllocateMeshForChunk(this))
        {
            return true;
        }

        if (!lod.CanAllocateMoreChunks())
        {
            return false;
        }

        return WantsSplit() || WantsSiblingBalanceSplit() || IsPriorityForceSplit();
    }

    internal bool LeafWantsRefinement()
    {
        if (!IsLeaf || level >= lod.GetActiveMaxLodLevel())
        {
            return false;
        }

        return WantsSplit() || IsPriorityForceSplit() || WantsSiblingBalanceSplit();
    }

    bool ChunkContainsLaunchSiteDirection()
    {
        return PlanetCubeSphere.IsDirectionInsideFaceUvBounds(
            LaunchSiteDirection,
            faceIndex,
            minU,
            maxU,
            minV,
            maxV
        );
    }

    static Vector3 LaunchSiteDirection
    {
        get
        {
            global::SimVector3 normal = LandingConfig.GetSurfaceLaunchPadNormal();
            return new Vector3(
                (float)normal.x,
                (float)normal.y,
                (float)normal.z
            );
        }
    }

    void InvalidateCachedMesh()
    {
        if (!HasValidMesh && !meshBuildInFlight)
        {
            return;
        }

        DestroyLeafResources();
        StartLeafMeshBuild();
    }

    internal void CollectBudgetPressureMergeCandidates(List<PlanetQuadtreeNode> list)
    {
        if (children == null)
        {
            return;
        }

        bool allLeaves = true;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].children != null)
            {
                allLeaves = false;
                children[i].CollectBudgetPressureMergeCandidates(list);
            }
        }

        if (allLeaves && ShouldBudgetPressureMerge())
        {
            list.Add(this);
        }
    }

    bool ShouldBudgetPressureMerge()
    {
        if (level == 0 || children == null)
        {
            return false;
        }

        int viewerLevel = lod.GetViewerLeafLevel();
        bool finerThanViewer = viewerLevel >= 0 && (level + 1) > viewerLevel;
        // Pad 2×2 only — the whole viewer L1 is tens of km and ate 640 as a
        // strip of finest tiles while other quadrants stayed coarse.
        if (IsPadDiskViewerSibling() && !finerThanViewer)
        {
            return false;
        }

        if (!HasValidMesh || HasChildMeshBuildInFlight())
        {
            return false;
        }

        if (!AllChildrenAreLeaves() || !CanMergeWithoutLodCrack())
        {
            return false;
        }

        if (finerThanViewer)
        {
            return true;
        }

        if (lod.ShouldClampLodToViewerRingThisTick() &&
            PlanetPqsLodRules.AllowPressureMergeOfFinestOutsideRing(
                viewerLevel,
                level,
                lod.IsNodeOutsideViewerFinestRing(this),
                IsPadDiskViewerSibling()))
        {
            return true;
        }

        return !lod.CanAllocateMoreChunks();
    }

    internal bool TryBudgetPressureMerge()
    {
        if (!ShouldBudgetPressureMerge())
        {
            return false;
        }

        Merge();
        return children == null;
    }

    internal void CollectLeavesNeedingMeshes(System.Collections.Generic.List<PlanetQuadtreeNode> leaves)
    {
        if (IsLeaf)
        {
            if (!HasValidMesh &&
                !meshBuildInFlight &&
                lod.ShouldAllocateMeshForChunk(this))
            {
                leaves.Add(this);
            }

            return;
        }

        if (children == null)
        {
            return;
        }

        if (
            !DirectChildrenCanCover() &&
            !HasValidMesh &&
            !meshBuildInFlight &&
            lod.ShouldAllocateMeshForChunk(this)
        )
        {
            leaves.Add(this);
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].CollectLeavesNeedingMeshes(leaves);
        }
    }

    internal void CollectLeavesWithInFlightBuilds(System.Collections.Generic.List<PlanetQuadtreeNode> leaves)
    {
        if (meshBuildInFlight)
        {
            leaves.Add(this);
        }

        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].CollectLeavesWithInFlightBuilds(leaves);
        }
    }

    internal void CullChunkMeshOutsideViewCone()
    {
        if (!IsLeaf)
        {
            if (children == null)
            {
                return;
            }

            for (int i = 0; i < children.Length; i++)
            {
                children[i].CullChunkMeshOutsideViewCone();
            }

            TryHorizonMerge();
        }
    }

    void TryHorizonMerge()
    {
        if (IsLeaf || level == 0)
        {
            return;
        }

        if (!lod.IsChunkBehindHorizon(this))
        {
            return;
        }

        if (ContainsViewerSurfaceSubtree() || IsInsideViewerSplitQuad())
        {
            return;
        }

        if (!AllChildrenAreLeaves() || !AllChildrenBehindHorizon())
        {
            return;
        }

        if (!CanMergeWithoutLodCrack())
        {
            return;
        }

        if (HasChildMeshBuildInFlight())
        {
            return;
        }

        if (!lod.AsyncLodSettings.TryConsumeUnloadMergeBudget())
        {
            return;
        }

        Merge();
    }

    bool AllChildrenAreLeaves()
    {
        if (children == null)
        {
            return false;
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (!children[i].IsLeaf)
            {
                return false;
            }
        }

        return true;
    }

    bool AllChildrenBehindHorizon()
    {
        if (children == null)
        {
            return false;
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (!lod.IsChunkBehindHorizon(children[i]))
            {
                return false;
            }
        }

        return true;
    }

    bool HasChildMeshBuildInFlight()
    {
        if (children == null)
        {
            return false;
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].meshBuildInFlight)
            {
                return true;
            }
        }

        return false;
    }

    internal void CollectLeafMeshStats(ref int withMesh, ref int withoutMesh, ref int inFlight)
    {
        if (meshBuildInFlight)
        {
            inFlight++;
        }

        if (IsLeaf)
        {
            if (HasValidMesh)
            {
                withMesh++;
            }
            else if (lod.ShouldAllocateMeshForChunk(this))
            {
                withoutMesh++;
            }

            return;
        }

        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].CollectLeafMeshStats(ref withMesh, ref withoutMesh, ref inFlight);
        }
    }

    internal void InvalidateLaunchSiteMeshesRecursive()
    {
        if (ChunkContainsLaunchSiteDirection())
        {
            InvalidateCachedMesh();
        }

        if (children == null)
        {
            return;
        }

        for (int i = 0; i < children.Length; i++)
        {
            children[i].InvalidateLaunchSiteMeshesRecursive();
        }
    }

    Vector3 GetNodeWorldPosition()
    {
        Vector3 centerDirection = PlanetCubeSphere.FaceUvToUnitDirection(
            faceIndex,
            (minU + maxU) * 0.5f,
            (minV + maxV) * 0.5f
        );
        float heightOffset = lod.SampleHeightOffset(centerDirection);
        Vector3 centerLocal = PlanetCubeSphere.FlipMeshVertexPositionX(
            centerDirection * (lod.PlanetRadius + heightOffset)
        );
        return lod.PlanetTransform.TransformPoint(centerLocal);
    }

    void UpdateDebugOutline()
    {
        if (chunkObject == null)
        {
            return;
        }

        if (debugOutline == null)
        {
            debugOutline = chunkObject.GetComponent<PlanetChunkDebugOutline>();

            if (debugOutline == null)
            {
                debugOutline = chunkObject.AddComponent<PlanetChunkDebugOutline>();
            }

            debugOutline.Configure(level, lod.DebugLineWidth);
        }

        Vector3[] corners = new Vector3[]
        {
            CornerLocalPoint(minU, minV),
            CornerLocalPoint(maxU, minV),
            CornerLocalPoint(maxU, maxV),
            CornerLocalPoint(minU, maxV)
        };

        if (!IsFiniteVector(corners[0]) ||
            !IsFiniteVector(corners[1]) ||
            !IsFiniteVector(corners[2]) ||
            !IsFiniteVector(corners[3]))
        {
            debugOutline.SetVisible(false);
            return;
        }

        debugOutline.SetCorners(corners);
    }

    Vector3 CornerLocalPoint(float u, float v)
    {
        Vector3 unitDirection = PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v);
        float heightOffset = lod.SampleHeightOffset(unitDirection);
        Vector3 surfacePoint = unitDirection * (lod.PlanetRadius + heightOffset);
        surfacePoint = lod.ApplyLaunchSiteFlatten(surfacePoint, unitDirection);
        return PlanetCubeSphere.FlipMeshVertexPositionX(surfacePoint);
    }

    internal int FindLeafLevelAt(float u, float v)
    {
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);
        PlanetQuadtreeNode node = this;

        while (node.children != null)
        {
            float midU = (node.minU + node.maxU) * 0.5f;
            float midV = (node.minV + node.maxV) * 0.5f;
            int childIndex = 0;

            if (u >= midU)
            {
                childIndex |= 1;
            }

            if (v >= midV)
            {
                childIndex |= 2;
            }

            node = node.children[childIndex];
        }

        return node.level;
    }

    internal PlanetQuadtreeNode FindLeafNodeAt(float u, float v)
    {
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);
        PlanetQuadtreeNode node = this;

        while (node.children != null)
        {
            float midU = (node.minU + node.maxU) * 0.5f;
            float midV = (node.minV + node.maxV) * 0.5f;
            int childIndex = 0;

            if (u >= midU)
            {
                childIndex |= 1;
            }

            if (v >= midV)
            {
                childIndex |= 2;
            }

            node = node.children[childIndex];
        }

        return node;
    }

    /// <summary>
    /// Restricted quadtree: a finer leaf may split a coarser in-disk neighbor one level
    /// even if SSE would not refine that neighbor. Merge stays blocked by
    /// <see cref="CanMergeWithoutLodCrack"/> (neighbor too fine) so this does not flap.
    /// </summary>
    internal bool TrySplitToCloseLodGap()
    {
        if (children != null || level >= lod.GetActiveMaxLodLevel())
        {
            return false;
        }

        if (!lod.CanAllocateMoreChunks())
        {
            return false;
        }

        if (lod.ShouldAllocateMeshForChunk(this) && !TryEnsureMeshBeforeSplit())
        {
            return false;
        }

        Split();
        return true;
    }

    internal bool SynchronizeNeighborLevelsRecursive()
    {
        if (children != null)
        {
            bool changed = false;

            for (int i = 0; i < children.Length; i++)
            {
                changed |= children[i].SynchronizeNeighborLevelsRecursive();
            }

            return changed;
        }

        return lod.SynchronizeNeighborLevelsForLeaf(this);
    }

    internal void AppendDiagnostics(StringBuilder sb, int childIndex)
    {
        sb.Append(" [");
        sb.Append(childIndex);
        sb.Append("] L");
        sb.Append(level);
        if (children != null)
        {
            sb.Append('+');
        }

        if (ChunkContainsViewerSurface())
        {
            sb.Append(" V");
        }

        sb.Append(HasValidMesh ? " M" : (meshBuildInFlight ? " m" : " -"));
        sb.Append(" span=");
        sb.Append(Mathf.RoundToInt(GetWorldSpanMeters()));
        sb.Append(" n=");
        sb.Append(lod.GetGeocentricAngleFromViewerDegrees(this).ToString("F2"));
        sb.Append("° c=");
        sb.Append(lod.GetChunkCenterAngleFromViewerDegrees(this).ToString("F2"));
        sb.Append(lod.NodeClosestUvIsCorner(this) ? " k=C" : " k=E");
        sb.Append(" cap=");
        sb.Append(lod.GetAngularRingLodCap(this));
        sb.Append(' ');
        if (children != null)
        {
            sb.Append(DirectChildrenCanCover() ? "cover=1" : "cover=0");
            sb.Append(" gate=");
            sb.Append(DiagnoseShouldSplit());
            return;
        }

        sb.Append("gate=");
        sb.Append(DiagnoseShouldSplit());
    }

    internal string DiagnoseShouldSplit()
    {
        if (!IsLeaf)
        {
            return "branch";
        }

        if (level >= lod.GetActiveMaxLodLevel())
        {
            return "MAX";
        }

        float nearest = lod.GetGeocentricAngleFromViewerDegrees(this);
        float horizon = lod.GetHorizonHalfAngleDegrees();
        if (lod.ShouldBlockSplitForVisibility(this) && !WantsSiblingBalanceSplit())
        {
            return "HORIZON n=" + nearest.ToString("F2") + ">h=" + horizon.ToString("F2");
        }

        int ringCap = lod.GetAngularRingLodCap(this);
        if (PlanetPqsLodRules.AngularRingCapBlocksSplit(
                level,
                ringCap,
                IsCoarserEdgeNeighborOfViewer(),
                lod.ViewerIsBelowMaxLod()))
        {
            return "RING cap=" + ringCap;
        }

        if (lod.ShouldDeferPeripheralSplit(this) && !WantsSiblingBalanceSplit())
        {
            return "PERIPH";
        }

        bool wants = WantsSplit();
        bool priority = IsPriorityForceSplit();
        bool sibling = WantsSiblingBalanceSplit();
        if (!wants && !priority && !sibling)
        {
            float span = EstimateNodeWorldSpanMeters();
            float target = lod.ComputeSplitTargetSpanMeters(this);
            if (span <= lod.MinChunkSpanMeters)
            {
                return "MINSPAN";
            }

            return "SSE span=" + Mathf.RoundToInt(span) + ">tgt=" + Mathf.RoundToInt(target) +
                " want=0";
        }

        if (!lod.CanAllocateMoreChunks())
        {
            return "BUDGET";
        }

        if (lod.LastTickHeldNonViewerSplits &&
            !ChunkContainsViewerSurface() &&
            !sibling &&
            !IsCoarserEdgeNeighborOfViewer())
        {
            return "HOLD-V";
        }

        if (lod.AsyncLodSettings.SkipBacksideMeshBuilds &&
            !lod.ShouldAllocateMeshForChunk(this) &&
            !ChunkContainsViewerSurface())
        {
            return "BACKSIDE";
        }

        if (lod.IsHardBackpressureActive() &&
            !ChunkContainsViewerSurface() &&
            !WantsSiblingBalanceSplit())
        {
            return "HARDBP";
        }

        if (!CanSplitWithoutLodCrack())
        {
            return DiagnoseCrack();
        }

        if (!HasValidMesh && meshBuildInFlight)
        {
            return "WAIT-MESH " + (priority ? "pri" : sibling ? "sib" : "sse");
        }

        if (!HasValidMesh && (parent == null || !parent.HasValidMesh))
        {
            return "NEED-MESH " + (priority ? "pri" : sibling ? "sib" : "sse");
        }

        return "SPLIT-OK " + (priority ? "pri" : sibling ? "sib" : "sse");
    }

    string DiagnoseCrack()
    {
        const float edgeEpsilon = 1e-5f;
        float midU = (minU + maxU) * 0.5f;
        float midV = (minV + maxV) * 0.5f;
        float uSpan = Mathf.Max(maxU - minU, edgeEpsilon);
        float vSpan = Mathf.Max(maxV - minV, edgeEpsilon);
        StringBuilder crack = new StringBuilder("CRACK");
        AppendCrackEdge(crack, "U-", minU - (uSpan * 0.25f), midV);
        AppendCrackEdge(crack, "U+", maxU + (uSpan * 0.25f), midV);
        AppendCrackEdge(crack, "V-", midU, minV - (vSpan * 0.25f));
        AppendCrackEdge(crack, "V+", midU, maxV + (vSpan * 0.25f));
        return crack.ToString();
    }

    void AppendCrackEdge(StringBuilder sb, string label, float probeU, float probeV)
    {
        PlanetQuadtreeNode neighbor = lod.FindNeighborLeaf(faceIndex, probeU, probeV);
        int neighborLevel = neighbor != null ? neighbor.Level : 0;
        bool blocked = neighborLevel < level && !IsCrackNeighborExempt(probeU, probeV);
        if (!blocked)
        {
            return;
        }

        sb.Append(' ');
        sb.Append(label);
        sb.Append("nL");
        sb.Append(neighborLevel);
        sb.Append("<L");
        sb.Append(level);
        if (neighbor != null && lod.IsChunkBeyondHorizon(neighbor))
        {
            sb.Append("H");
        }
    }
}

/// <summary>
/// Shared state and sampling helpers for planet quadtree chunks.
/// </summary>
sealed class PlanetQuadtreeLod
{
    Vector3 smoothedCameraForward;
    PlanetLodViewPolicy.ViewerFrame viewerFrame;
    int activeChunkObjectCount;
    float smoothedLodAltitudeMeters = -1f;
    int altitudeCappedMaxLod = -1;
    int lastMaxLodCeiling = -1;
    int activeMaxLodFrame = -1;
    int leavesNeedingMeshCount;
    float lodTickDeadlineUnscaled;
    int neighborCatchUpSplitsUsed;
    int neighborCatchUpSplitBudget;
    int tickSplitCount;
    int tickMergeCount;
    int lastTickSplitCount;
    int lastTickMergeCount;
    string lastFlapSummary = "";
    const int MaxFlapLogEntries = 8;
    readonly string[] flapLog = new string[MaxFlapLogEntries];
    int flapLogCount;
    int flapLogWrite;
    internal bool HoldNonViewerSplits;
    internal bool LastTickHeldNonViewerSplits;
    bool starveNonPadFinestThisTick;
    bool clampLodToViewerRingThisTick;
    readonly Stack<GameObject> chunkObjectPool = new Stack<GameObject>(256);
    readonly List<PlanetQuadtreeNode> budgetMergeScratch = new List<PlanetQuadtreeNode>(64);

    static Vector3 LaunchSiteDirection
    {
        get
        {
            global::SimVector3 normal = LandingConfig.GetSurfaceLaunchPadNormal();
            return new Vector3(
                (float)normal.x,
                (float)normal.y,
                (float)normal.z
            );
        }
    }

    public Transform PlanetTransform { get; }
    public Transform ChunksRoot { get; }
    public Material SurfaceMaterial { get; }
    public Texture2D Heightmap { get; }
    public float PlanetRadius { get; }
    public float SplitDistanceMeters { get; set; }
    public int MaxLodLevel { get; }
    public int MaxActiveChunks { get; set; }
    public int MinColliderLodLevel { get; }
    public int ChunkVertexResolution { get; }
    public float FlattenRadiusMeters { get; }
    public float HardFlattenRadiusMeters { get; }
    public float DebugLineWidth { get; }
    public float MergeDistanceMultiplier { get; }
    public float MinChunkSpanMeters { get; set; }
    public float LaunchSiteMaxChunkSpanMeters { get; set; }
    public float SplitTargetSpanFactor { get; set; } = 0.35f;
    public float ColliderMaxSurfaceDistanceMeters { get; }
    public float ViewerAltitudeMeters { get; set; }
    public Vector3 ViewerWorldPosition { get; set; }
    /// <summary>Flight frame-zero anchor was set (may be world origin).</summary>
    public bool HasViewerWorldAnchor { get; set; }
    /// <summary>Sim-unit radial from planet center through the ship (LOD focus on pad / low altitude).</summary>
    public Vector3 ViewerFocusDirectionSim { get; set; }
    public Vector3 ViewerFocusDirectionVisual { get; private set; }
    int cachedViewerFace;
    float cachedViewerU;
    float cachedViewerV;
    bool cachedViewerUvValid;
    public Vector3 ViewerVelocityWorld { get; set; }
    public Vector3 ViewerVelocitySim { get; set; }
    public float ViewerLookAheadSeconds { get; set; } =
        FlightPlanetSphere.DefaultViewerLookAheadSeconds;
    public Vector3 ViewerLookAheadWorldPosition =>
        ViewerWorldPosition + (ViewerVelocityWorld * ViewerLookAheadSeconds);

    internal Vector3 GetViewerWorldPositionForLod()
    {
        if (HasViewerWorldAnchor)
        {
            return ViewerWorldPosition;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            return mainCamera.transform.position;
        }

        return ViewerLookAheadWorldPosition;
    }

    internal Vector3 GetViewerAnchorLocalMeshPosition()
    {
        if (PlanetTransform == null)
        {
            return Vector3.zero;
        }

        return PlanetTransform.InverseTransformPoint(GetViewerWorldPositionForLod());
    }

    internal float ComputeViewWeight(PlanetQuadtreeNode node)
    {
        if (node.ContainsViewerSurfaceDirection())
        {
            return 1f;
        }

        return PlanetLodViewPolicy.ComputeViewWeight(
            GetCameraConeAngleDegrees(node),
            viewerFrame.ConeHalfAngleDegrees
        );
    }

    /// <summary>
    /// Desired world span at this chunk's distance from the ship, from screen-space error.
    /// Higher <see cref="SplitTargetSpanFactor"/> (LOD Distance slider) → coarser rings.
    /// </summary>
    internal float ComputeSplitTargetSpanMeters(PlanetQuadtreeNode node)
    {
        float slantRange = GetSlantRangeToChunkMeters(node);
        float altitudeMultiplier = PlanetLodAltitudePolicy.GetSplitSpanAltitudeMultiplier(
            ViewerAltitudeMeters,
            PlanetRadius
        );
        float lodUnits = SplitTargetSpanFactor /
            Mathf.Max(1e-4f, FlightPlanetTerrainTuning.SplitSpanFactorPerLodDistanceUnit);
        float pixelError = Mathf.Clamp(
            PlanetLodViewPolicy.ScreenSpacePixelErrorBase * lodUnits,
            PlanetLodViewPolicy.ScreenSpacePixelErrorMin,
            PlanetLodViewPolicy.ScreenSpacePixelErrorMax
        );
        float targetSpan = Mathf.Max(
            MinChunkSpanMeters,
            PlanetLodViewPolicy.ComputeScreenSpaceTargetSpanMeters(
                slantRange,
                viewerFrame.TanHalfVerticalFov,
                viewerFrame.ScreenHeightPixels,
                pixelError
            ) * altitudeMultiplier
        );

        if (node.ContainsViewerSurfaceDirection())
        {
            targetSpan = Mathf.Min(targetSpan, LaunchSiteMaxChunkSpanMeters);
        }
        else if (slantRange <= LaunchSiteMaxChunkSpanMeters * 3f)
        {
            targetSpan = Mathf.Min(targetSpan, LaunchSiteMaxChunkSpanMeters);
        }

        return targetSpan;
    }

    internal void UpdateSmoothedLodAltitude()
    {
        if (smoothedLodAltitudeMeters < 0f)
        {
            smoothedLodAltitudeMeters = ViewerAltitudeMeters;
            return;
        }

        if (smoothedLodAltitudeMeters > ViewerAltitudeMeters + 250f)
        {
            ResetAltitudeLodTracking();
            return;
        }

        smoothedLodAltitudeMeters = Mathf.Lerp(
            smoothedLodAltitudeMeters,
            ViewerAltitudeMeters,
            Mathf.Clamp01(Time.unscaledDeltaTime * 1.25f)
        );
    }

    /// <summary>
    /// After a high hop / PNG-shell collapse, stale orbital altitude would keep max LOD at 0
    /// and skip mesh builds. Snap to the real ship height so descent can refine again.
    /// </summary>
    public void ResetAltitudeLodTracking()
    {
        smoothedLodAltitudeMeters = ViewerAltitudeMeters;
        altitudeCappedMaxLod = -1;
        lastMaxLodCeiling = -1;
        activeMaxLodFrame = -1;
    }

    internal float GetLodAltitudeMeters()
    {
        return smoothedLodAltitudeMeters < 0f ?
            ViewerAltitudeMeters :
            smoothedLodAltitudeMeters;
    }

    internal bool IsLodSuspendedForAltitudeShell()
    {
        return PlanetLodAltitudePolicy.GetAltitudeFraction(
                ViewerAltitudeMeters,
                PlanetRadius
            ) >= PlanetLodAltitudePolicy.GetMapShellChunkHideFraction(PlanetRadius);
    }

    internal float ComputeMergeDistanceMeters(PlanetQuadtreeNode node)
    {
        float slantRange = GetSlantRangeToChunkMeters(node);
        float viewWeight = ComputeViewWeight(node);
        float childSpan = node.GetWorldSpanMeters() * 0.5f;
        return Mathf.Max(
            childSpan * viewWeight * MergeDistanceMultiplier * 2.5f,
            slantRange * SplitTargetSpanFactor * 2.5f
        );
    }

    /// <summary>
    /// 3D distance from the ship to the chunk. The tile that contains the ship uses
    /// altitude. Edge neighbours use the nearest surface point. Corner-only tiles
    /// (the other three L1s on a face-center pad) use the chunk centre — nearest
    /// is the shared vertex at the rocket, so slant≈altitude drilled L7 spikes.
    /// </summary>
    internal float GetSlantRangeToChunkMeters(PlanetQuadtreeNode node)
    {
        if (node.ContainsViewerSurfaceDirection())
        {
            return Mathf.Max(1f, Mathf.Abs(ViewerAltitudeMeters));
        }

        if (NodeClosestUvIsCorner(node))
        {
            return Mathf.Max(
                1f,
                Vector3.Distance(
                    GetViewerWorldPositionForLod(),
                    node.GetCenterWorldPositionApprox()
                )
            );
        }

        return Mathf.Max(
            1f,
            Vector3.Distance(
                GetViewerWorldPositionForLod(),
                node.GetClosestSurfaceWorldPosition()
            )
        );
    }

    internal bool NodeClosestUvIsCorner(PlanetQuadtreeNode node)
    {
        if (node == null || !TryGetClosestUvOnNode(node, out float cu, out float cv))
        {
            return false;
        }

        return PlanetPqsLodRules.ClosestUvIsNodeCorner(
            cu,
            cv,
            node.MinU,
            node.MaxU,
            node.MinV,
            node.MaxV
        );
    }

    internal void UpdateViewerFrame()
    {
        CacheViewerFaceUv();
        viewerFrame = PlanetLodViewPolicy.BuildViewerFrame(
            Camera.main,
            PlanetRadius,
            ViewerAltitudeMeters,
            AsyncLodSettings.ViewConeHalfAngleDegrees
        );

        if (!viewerFrame.HasCamera)
        {
            return;
        }

        // Smooth the look axis so a fast camera sweep does not flap chunks between zones.
        if (smoothedCameraForward.sqrMagnitude <= 0.5f)
        {
            smoothedCameraForward = viewerFrame.CameraForward;
        }
        else
        {
            float blend = Mathf.Clamp01(Time.unscaledDeltaTime * 10f);
            smoothedCameraForward = Vector3.Slerp(
                smoothedCameraForward,
                viewerFrame.CameraForward,
                blend
            ).normalized;
        }

        viewerFrame.CameraForward = smoothedCameraForward;
    }

    /// <summary>Angle between the camera forward axis and the chunk's bounding sphere (nearest).</summary>
    internal float GetCameraConeAngleDegrees(PlanetQuadtreeNode node)
    {
        if (!viewerFrame.HasCamera)
        {
            return 0f;
        }

        return PlanetLodViewPolicy.GetCameraConeNearestAngleDegrees(
            viewerFrame,
            node.GetCenterWorldPositionApprox(),
            node.GetWorldSpanMeters() * 0.75f
        );
    }

    internal float GetCameraConeFarthestAngleDegrees(PlanetQuadtreeNode node)
    {
        if (!viewerFrame.HasCamera)
        {
            return 180f;
        }

        return PlanetLodViewPolicy.GetCameraConeFarthestAngleDegrees(
            viewerFrame,
            node.GetCenterWorldPositionApprox(),
            node.GetWorldSpanMeters() * 0.75f
        );
    }

    /// <summary>
    /// Geocentric angle measured from the point directly under the vessel. The horizon is a
    /// property of where the eye is, not of where it happens to be pointing, so this must not
    /// use the camera look axis.
    /// </summary>
    internal float GetGeocentricAngleFromViewerDegrees(PlanetQuadtreeNode node)
    {
        Vector3 nadir = GetShipFocusDirectionSimRaw();
        if (TryGetClosestUvOnNode(node, out float closestU, out float closestV))
        {
            Vector3 closestDirection = PlanetCubeSphere.FaceUvToUnitDirection(
                node.FaceIndex,
                closestU,
                closestV
            );
            return Vector3.Angle(nadir, closestDirection);
        }

        float closestAngle = float.MaxValue;
        AccumulateClosestAngleSample(ref closestAngle, nadir, node.FaceIndex, node.MinU, node.MinV);
        AccumulateClosestAngleSample(ref closestAngle, nadir, node.FaceIndex, node.MaxU, node.MinV);
        AccumulateClosestAngleSample(ref closestAngle, nadir, node.FaceIndex, node.MaxU, node.MaxV);
        AccumulateClosestAngleSample(ref closestAngle, nadir, node.FaceIndex, node.MinU, node.MaxV);
        AccumulateClosestAngleSample(
            ref closestAngle,
            nadir,
            node.FaceIndex,
            (node.MinU + node.MaxU) * 0.5f,
            (node.MinV + node.MaxV) * 0.5f
        );
        AccumulateClosestAngleSample(
            ref closestAngle,
            nadir,
            node.FaceIndex,
            (node.MinU + node.MaxU) * 0.5f,
            node.MinV
        );
        AccumulateClosestAngleSample(
            ref closestAngle,
            nadir,
            node.FaceIndex,
            (node.MinU + node.MaxU) * 0.5f,
            node.MaxV
        );
        AccumulateClosestAngleSample(
            ref closestAngle,
            nadir,
            node.FaceIndex,
            node.MinU,
            (node.MinV + node.MaxV) * 0.5f
        );
        AccumulateClosestAngleSample(
            ref closestAngle,
            nadir,
            node.FaceIndex,
            node.MaxU,
            (node.MinV + node.MaxV) * 0.5f
        );
        return closestAngle;
    }

    internal bool IsChunkBeyondHorizon(PlanetQuadtreeNode node)
    {
        return GetGeocentricAngleFromViewerDegrees(node) >
            viewerFrame.HorizonHalfAngleDegrees;
    }

    /// <summary>
    /// True when the entire chunk cap is over the geometric horizon. Camera look is ignored
    /// so turning around does not unload the disk.
    /// </summary>
    internal bool IsChunkFullyOutsideView(PlanetQuadtreeNode node)
    {
        if (node.Level == 0)
        {
            return false;
        }

        if (node.IsInsideViewerSplitQuad())
        {
            return false;
        }

        return IsChunkFullyBeyondHorizon(node);
    }

    internal bool IsChunkFullyBeyondHorizon(PlanetQuadtreeNode node)
    {
        if (node.ContainsViewerSurfaceDirection())
        {
            return false;
        }

        return PlanetLodViewPolicy.IsEntireCapBeyondHorizon(
            GetGeocentricAngleFromViewerDegrees(node),
            viewerFrame.HorizonHalfAngleDegrees
        );
    }

    /// <summary>
    /// Debug classification (camera cone + horizon). Split/merge/allocate use the ship disk
    /// and screen-space error, not this zone.
    /// </summary>
    internal PlanetLodViewPolicy.ViewZone GetViewZone(PlanetQuadtreeNode node)
    {
        // Over the curve of the planet nothing can be on screen, whatever the cone says.
        if (IsChunkBeyondHorizon(node))
        {
            return PlanetLodViewPolicy.ViewZone.Dead;
        }

        return PlanetLodViewPolicy.ClassifyZone(
            GetCameraConeAngleDegrees(node),
            viewerFrame.ConeHalfAngleDegrees
        );
    }

    internal bool IsHardBackpressureActive()
    {
        return PlanetFlightChunkMeshPipeline.PendingCount +
            AsyncLodSettings.InFlightLeafBuilds >=
            PlanetLodViewPolicy.HardPendingAsyncSplitHold;
    }

    internal bool ShouldBlockSplitForVisibility(PlanetQuadtreeNode node)
    {
        return IsChunkBeyondHorizon(node);
    }
    public bool ShowLodDebug { get; set; }
    /// <summary>
    /// During Play bootstrap/warmup, in-disk leaves build meshes on the main thread so
    /// siblings can split. Async-only from L5 left three quarters waiting; a later
    /// scenario load hit the mesh cache and looked "fixed".
    /// </summary>
    public bool SyncInDiskMeshes { get; set; }

    internal float GetHorizonHalfAngleDegrees()
    {
        return viewerFrame.HorizonHalfAngleDegrees;
    }

    public string CaptureDiagnosticsReport(string phaseLine)
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine(phaseLine);
        sb.Append("chunks=");
        sb.Append(GetActiveChunkCount());
        sb.Append('/');
        sb.Append(MaxActiveChunks);
        sb.Append(" pendingWork=");
        sb.Append(HasPendingLodWork() ? "1" : "0");
        sb.Append(" lastTickPending=");
        sb.Append(LastLodRefreshHadPendingWork ? "1" : "0");
        sb.Append(" unfinished=");
        sb.Append(DiskHasUnfinishedLod() ? "1" : "0");
        sb.Append(" gap=");
        sb.Append(ViewerNeighborhoodHasLodGap() ? "1" : "0");
        string gapWhere = FormatViewerGapEdges();
        if (!string.IsNullOrEmpty(gapWhere))
        {
            sb.Append(gapWhere);
        }

        sb.Append(" padMix=");
        sb.Append(ViewerPadTwoByTwoIsIncomplete() ? "1" : "0");
        sb.Append(" starve=");
        sb.Append(starveNonPadFinestThisTick ? "1" : "0");
        sb.Append(" clamp=");
        sb.Append(
            PlanetPqsLodRules.ShouldClampLodToViewerRing(
                PlanetPqsLodRules.ChunkCapIsUnderPressure(
                    GetActiveChunkCount(),
                    MaxActiveChunks),
                CanAllocateMoreChunks())
                ? "1"
                : "0");
        sb.Append(" incomplete=");
        sb.Append(ViewerAncestryHasIncompleteCoverage() ? "1" : "0");
        sb.Append(" syncDisk=");
        sb.Append(SyncInDiskMeshes ? "1" : "0");
        sb.Append(" hardBP=");
        sb.Append(IsHardBackpressureActive() ? "1" : "0");
        sb.Append(" flap s=");
        sb.Append(lastTickSplitCount);
        sb.Append(" m=");
        sb.Append(lastTickMergeCount);
        sb.AppendLine();
        if (!string.IsNullOrEmpty(lastFlapSummary))
        {
            sb.Append("lastFlap ");
            sb.AppendLine(lastFlapSummary);
        }
        sb.Append("focus F");
        sb.Append(cachedViewerUvValid ? cachedViewerFace.ToString() : "?");
        if (cachedViewerUvValid)
        {
            sb.Append(" uv=");
            sb.Append(cachedViewerU.ToString("F3"));
            sb.Append(",");
            sb.Append(cachedViewerV.ToString("F3"));
        }

        sb.Append(" alt=");
        sb.Append(ViewerAltitudeMeters.ToString("F1"));
        sb.Append(" horizon=");
        sb.Append(viewerFrame.HorizonHalfAngleDegrees.ToString("F2"));
        sb.Append("° maxL=");
        sb.Append(GetActiveMaxLodLevel());
        sb.Append(" capL=");
        sb.Append(GetMaxLodCeiling());
        sb.Append(" needL=");
        sb.Append(GetPadTargetLodLevel());
        sb.Append(" viewerSpan=");
        sb.Append(Mathf.RoundToInt(GetViewerLeafSpanMeters()));
        sb.Append("m");
        sb.AppendLine();
        AppendFaceQuadrantFinestLine(sb);
        sb.Append("async=");
        sb.Append(PlanetFlightChunkMeshPipeline.PendingCount);
        sb.Append(" inFlight=");
        sb.Append(AsyncLodSettings.InFlightLeafBuilds);
        sb.Append(" noMesh=");
        sb.Append(AsyncLodSettings.LeavesWithoutMesh);
        sb.AppendLine();
        sb.AppendLine("V=viewer M=mesh n=nearest c=center k=C corner/E edge cap=RING max");
        AppendViewerAncestryDiagnostics(sb);
        return sb.ToString();
    }

    void AppendFaceQuadrantFinestLine(StringBuilder sb)
    {
        PlanetQuadtreeNode faceRoot = null;
        if (cachedViewerUvValid &&
            faceRoots != null &&
            cachedViewerFace >= 0 &&
            cachedViewerFace < faceRoots.Length)
        {
            faceRoot = faceRoots[cachedViewerFace];
        }

        if (faceRoot == null || faceRoot.DiagChildren == null)
        {
            return;
        }

        sb.Append("F");
        sb.Append(cachedViewerFace);
        sb.Append(" L1q maxL=");
        PlanetQuadtreeNode[] quads = faceRoot.DiagChildren;
        for (int i = 0; i < quads.Length; i++)
        {
            if (i > 0)
            {
                sb.Append('/');
            }

            sb.Append(quads[i].CollectFinestLeafLevel());
        }

        sb.Append(" minL=");
        for (int i = 0; i < quads.Length; i++)
        {
            if (i > 0)
            {
                sb.Append('/');
            }

            sb.Append(quads[i].CollectCoarsestLeafLevel());
        }

        sb.Append(" (SW/SE/NW/NE; even max=min = rings)");
        sb.AppendLine();
    }

    void AppendViewerAncestryDiagnostics(StringBuilder sb)
    {
        if (faceRoots == null)
        {
            sb.AppendLine("tree=null");
            return;
        }

        PlanetQuadtreeNode viewerLeaf = null;
        for (int i = 0; i < faceRoots.Length; i++)
        {
            viewerLeaf = faceRoots[i].FindViewerLeaf();
            if (viewerLeaf != null)
            {
                break;
            }
        }

        if (viewerLeaf == null)
        {
            sb.AppendLine("viewerLeaf=NONE (focus not on any face tile)");
            return;
        }

        PlanetQuadtreeNode[] stack = new PlanetQuadtreeNode[16];
        int depth = 0;
        PlanetQuadtreeNode walk = viewerLeaf;
        while (walk != null && depth < stack.Length)
        {
            stack[depth] = walk;
            depth++;
            walk = walk.DiagParent;
        }

        int printed = 0;
        for (int i = depth - 1; i >= 0; i--)
        {
            PlanetQuadtreeNode node = stack[i];
            PlanetQuadtreeNode parent = node.DiagParent;
            if (parent == null || parent.DiagChildren == null)
            {
                continue;
            }

            sb.Append("F");
            sb.Append(parent.FaceIndex);
            sb.Append(" parentL");
            sb.Append(parent.Level);
            sb.Append(" 2x2:");
            sb.AppendLine();
            PlanetQuadtreeNode[] kids = parent.DiagChildren;
            for (int c = 0; c < kids.Length; c++)
            {
                kids[c].AppendDiagnostics(sb, c);
                sb.AppendLine();
            }

            printed++;
        }

        if (printed == 0)
        {
            sb.AppendLine("viewer is unsplit face leaf:");
            viewerLeaf.AppendDiagnostics(sb, 0);
            sb.AppendLine();
        }
    }

    public PlanetPqsAsyncLodSettings AsyncLodSettings { get; } = new PlanetPqsAsyncLodSettings();

    PlanetQuadtreeNode[] faceRoots;
    readonly PlanetHeightSampler heightSampler;
    readonly List<PlanetQuadtreeNode> pendingMeshScratch = new List<PlanetQuadtreeNode>(256);
    readonly List<PlanetQuadtreeNode> inFlightMeshScratch = new List<PlanetQuadtreeNode>(64);
    readonly List<string> meshLabelScratch = new List<string>(8);

    public PlanetQuadtreeLod(
        Transform planetTransform,
        Transform chunksRoot,
        Material surfaceMaterial,
        Texture2D heightmap,
        float planetRadius,
        float splitDistanceMeters,
        int maxLodLevel,
        int minColliderLodLevel,
        int chunkVertexResolution,
        float flattenRadiusMeters,
        float hardFlattenRadiusMeters,
        float debugLineWidth,
        float colliderMaxSurfaceDistanceMeters,
        float mergeDistanceMultiplier = FlightPlanetSphere.MergeDistanceMultiplier,
        float minChunkSpanMeters = FlightPlanetSphere.DefaultMinChunkSpanMeters,
        float launchSiteMaxChunkSpanMeters = FlightPlanetSphere.DefaultLaunchSiteMaxChunkSpanMeters,
        int maxActiveChunks = FlightPlanetSphere.DefaultMaxActiveChunks
    )
    {
        PlanetTransform = planetTransform;
        ChunksRoot = chunksRoot;
        SurfaceMaterial = surfaceMaterial;
        Heightmap = heightmap;
        heightSampler = new PlanetHeightSampler(heightmap);
        if (heightmap != null && !heightSampler.IsValid)
        {
            Debug.LogWarning(
                "PlanetQuadtreeLod: height sampler is invalid for " +
                heightmap.width +
                "x" +
                heightmap.height +
                " " +
                heightmap.format +
                " (readable=" +
                heightmap.isReadable +
                "). Falling back to GetPixelBilinear height sampling."
            );
        }
        PlanetRadius = planetRadius;
        SplitDistanceMeters = splitDistanceMeters;
        MaxLodLevel = maxLodLevel;
        MaxActiveChunks = Mathf.Max(64, maxActiveChunks);
        MinColliderLodLevel = Mathf.Max(0, minColliderLodLevel);
        ChunkVertexResolution = Mathf.Clamp(chunkVertexResolution, 4, 128);
        FlattenRadiusMeters = flattenRadiusMeters;
        HardFlattenRadiusMeters = Mathf.Clamp(hardFlattenRadiusMeters, 0f, flattenRadiusMeters);
        DebugLineWidth = debugLineWidth;
        ColliderMaxSurfaceDistanceMeters = Mathf.Max(0f, colliderMaxSurfaceDistanceMeters);
        MergeDistanceMultiplier = mergeDistanceMultiplier;
        MinChunkSpanMeters = Mathf.Max(4f, minChunkSpanMeters);
        LaunchSiteMaxChunkSpanMeters = Mathf.Max(8f, launchSiteMaxChunkSpanMeters);
        SplitTargetSpanFactor = 0.35f;

        CreateFaceRoots();
    }

    public void RefreshLod()
    {
        RefreshLodBudgeted(int.MaxValue, int.MaxValue);
        FeedPendingMeshQueue(int.MaxValue);
    }

    /// <summary>
    /// Time warp skips splits/mesh (those hitch at x1). Still coarsen so 2×2 merges
    /// follow altitude during warp instead of dumping when the scale returns to 1.
    /// </summary>
    public void CoarsenForAltitudeOnly()
    {
        UpdateViewerFrame();
        UpdateSmoothedLodAltitude();
        SyncViewerFocusDirections();
        AsyncLodSettings.MeshCullBudgetRemaining = AsyncLodSettings.MaxMeshDestroysPerFrame;
        AsyncLodSettings.UnloadMergeBudgetRemaining = Mathf.Max(
            8,
            AsyncLodSettings.MaxUnloadMergesPerFrame * 4
        );
        ApplyLodCapSplitPolicy();
        RunViewConeMaintenance();
        SyncDrawnCoverage();
        UpdateLeafMeshStats();
    }

    public void RefreshLodBudgeted(int splitBudget, int meshBuildBudget)
    {
        RefreshLodLookAhead(splitBudget, meshBuildBudget, allowNeighborSync: true);
    }

    internal void BeginLodTickTimeBudget(float maxMilliseconds)
    {
        lodTickDeadlineUnscaled = Time.realtimeSinceStartup + Mathf.Max(0.001f, maxMilliseconds) * 0.001f;
    }

    internal void ClearLodTickTimeBudget()
    {
        lodTickDeadlineUnscaled = 0f;
    }

    internal bool IsLodTickTimeBudgetExhausted()
    {
        return lodTickDeadlineUnscaled > 0f &&
            Time.realtimeSinceStartup >= lodTickDeadlineUnscaled;
    }

    public void RefreshLodLookAhead(
        int peripheralSplitBudget,
        int peripheralMeshBudget,
        bool allowNeighborSync = true
    )
    {
        UpdateViewerFrame();
        UpdateSmoothedLodAltitude();
        SyncViewerFocusDirections();
        tickSplitCount = 0;
        tickMergeCount = 0;
        flapLogCount = 0;
        flapLogWrite = 0;

        int splitBudget = Mathf.Max(1, peripheralSplitBudget);
        int meshBudget = Mathf.Max(1, peripheralMeshBudget);

        AsyncLodSettings.MeshCullBudgetRemaining = AsyncLodSettings.MaxMeshDestroysPerFrame;
        AsyncLodSettings.UnloadMergeBudgetRemaining = AsyncLodSettings.MaxUnloadMergesPerFrame;

        ApplyLodCapSplitPolicy();

        bool closeGap = ViewerNeighborhoodHasLodGap();
        bool viewerCrack = ViewerLeafBlockedByCrack();
        int catchUpReserve = 0;
        bool needCatchUp = allowNeighborSync && (closeGap || viewerCrack);
        if (needCatchUp)
        {
            catchUpReserve = FlightPlanetSphere.HitchMaxNeighborSplitsPerFrame;
            SynchronizeNeighborLevels(catchUpReserve);
        }

        int viewerSplitBudget = Mathf.Max(1, splitBudget - catchUpReserve);
        int splitsUsed = 0;
        int meshesUsed = 0;
        bool pending = RefreshFaceRootsViewerFirst(
            ref splitsUsed,
            ref meshesUsed,
            viewerSplitBudget,
            meshBudget
        );

        if (allowNeighborSync)
        {
            int leftover = Mathf.Max(0, splitBudget - splitsUsed);
            int neighborBudget;
            if (needCatchUp)
            {
                neighborBudget = Mathf.Max(catchUpReserve, leftover);
            }
            else
            {
                neighborBudget = leftover;
            }

            neighborBudget = Mathf.Min(
                neighborBudget,
                FlightPlanetSphere.HitchMaxNeighborSplitsPerFrame
            );
            SynchronizeNeighborLevels(neighborBudget);
        }

        RunViewConeMaintenance();
        UpdateLeafMeshStats();
        HoldNonViewerSplits = false;
        LastLodRefreshHadPendingWork =
            pending ||
            HasPendingLodWork() ||
            neighborCatchUpSplitsUsed > 0 ||
            IsLodTickTimeBudgetExhausted();
        lastTickSplitCount = tickSplitCount;
        lastTickMergeCount = tickMergeCount;
        lastFlapSummary = FormatFlapLog();
        tickSplitCount = 0;
        tickMergeCount = 0;
    }

    bool RefreshFaceRootsViewerFirst(
        ref int splitsUsed,
        ref int meshesUsed,
        int splitBudget,
        int meshBudget
    )
    {
        if (faceRoots == null)
        {
            return false;
        }

        int viewerFace = -1;
        for (int i = 0; i < faceRoots.Length; i++)
        {
            if (faceRoots[i].ContainsViewerSurfaceDirection())
            {
                viewerFace = i;
                break;
            }
        }

        bool pending = false;
        if (viewerFace >= 0)
        {
            pending |= RefreshOneFaceBudgeted(
                faceRoots[viewerFace],
                ref splitsUsed,
                ref meshesUsed,
                splitBudget,
                meshBudget
            );
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            if (i == viewerFace)
            {
                continue;
            }

            pending |= RefreshOneFaceBudgeted(
                faceRoots[i],
                ref splitsUsed,
                ref meshesUsed,
                splitBudget,
                meshBudget
            );
        }

        return pending;
    }

    bool RefreshOneFaceBudgeted(
        PlanetQuadtreeNode faceRoot,
        ref int splitsUsed,
        ref int meshesUsed,
        int splitBudget,
        int meshBudget
    )
    {
        bool inView = faceRoot.IntersectsHorizonDisk();
        return faceRoot.RefreshLodBudgeted(
            ref splitsUsed,
            ref meshesUsed,
            inView ? splitBudget : Mathf.Max(1, splitBudget / 2),
            inView ? meshBudget : Mathf.Max(1, meshBudget / 2)
        );
    }

    /// <summary>
    /// Parent mesh is never drawn when all four children can already draw their quads.
    /// Runs every LateUpdate, not only on a LOD tick — idle skip used to leave both on.
    /// </summary>
    public void SyncDrawnCoverage()
    {
        if (faceRoots == null)
        {
            return;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].UpdateCoverageMeshState();
        }
    }

    public void DispatchCompletedMeshes()
    {
        PlanetFlightChunkMeshCache.PurgeStaleEntries();
        int budget = AsyncLodSettings.MaxMeshCompletionsPerFrame;
        PlanetFlightChunkMeshPipeline.DispatchCompletedMeshes(ref budget);
    }

    public void FeedPendingMeshQueue(int meshBudget)
    {
        if (meshBudget <= 0)
        {
            return;
        }

        UpdateViewerFrame();
        BuildMeshesForNearestLeaves(meshBudget);
    }

    public PlanetLodLoadSnapshot CaptureLodLoadSnapshot()
    {
        UpdateLeafMeshStats();

        pendingMeshScratch.Clear();
        inFlightMeshScratch.Clear();
        meshLabelScratch.Clear();

        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].CollectLeavesNeedingMeshes(pendingMeshScratch);
            faceRoots[i].CollectLeavesWithInFlightBuilds(inFlightMeshScratch);
        }

        int sampleBudget = 5;

        for (int i = 0; i < inFlightMeshScratch.Count && meshLabelScratch.Count < sampleBudget; i++)
        {
            meshLabelScratch.Add(inFlightMeshScratch[i].DebugChunkName + " (build)");
        }

        PlanetFlightChunkMeshPipeline.CollectPendingBuildLabels(meshLabelScratch, sampleBudget);

        for (int i = 0; i < pendingMeshScratch.Count && meshLabelScratch.Count < sampleBudget; i++)
        {
            meshLabelScratch.Add(pendingMeshScratch[i].DebugChunkName + " (queue)");
        }

        string summary =
            "LOD: чанки " + GetActiveChunkCount() +
            " | без меша " + AsyncLodSettings.LeavesWithoutMesh +
            " | очередь " + pendingMeshScratch.Count +
            " | async " + PlanetFlightChunkMeshPipeline.PendingCount +
            " | waiters " + PlanetFlightChunkMeshPipeline.PendingWaiterCount +
            " | строится " + AsyncLodSettings.InFlightLeafBuilds +
            (AsyncLodSettings.IsSplitBackpressureActive() ? " | HOLD" : string.Empty);

        string samples = meshLabelScratch.Count > 0 ?
            string.Join(", ", meshLabelScratch) :
            "(нет очереди)";

        return new PlanetLodLoadSnapshot(
            AsyncLodSettings.LeavesWithoutMesh,
            AsyncLodSettings.InFlightLeafBuilds,
            PlanetFlightChunkMeshPipeline.PendingCount,
            pendingMeshScratch.Count,
            AsyncLodSettings.IsSplitBackpressureActive(),
            IsHardBackpressureActive(),
            summary,
            samples
        );
    }

    void BuildMeshesForNearestLeaves(int meshBudget)
    {
        pendingMeshScratch.Clear();

        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].CollectLeavesNeedingMeshes(pendingMeshScratch);
        }

        leavesNeedingMeshCount = pendingMeshScratch.Count;

        pendingMeshScratch.Sort(
            CompareLeavesForMeshBuild
        );

        int meshesUsed = 0;

        for (int i = 0; i < pendingMeshScratch.Count && meshesUsed < meshBudget; i++)
        {
            pendingMeshScratch[i].QueueLeafMeshBuild();
            meshesUsed++;
        }

        leavesNeedingMeshCount = pendingMeshScratch.Count - meshesUsed;
    }

    int CompareLeavesForMeshBuild(PlanetQuadtreeNode left, PlanetQuadtreeNode right)
    {
        bool leftShip = left.ContainsViewerSurfaceDirection();
        bool rightShip = right.ContainsViewerSurfaceDirection();

        if (leftShip != rightShip)
        {
            return leftShip ? -1 : 1;
        }

        float leftRange = GetSlantRangeToChunkMeters(left);
        float rightRange = GetSlantRangeToChunkMeters(right);

        if (Mathf.Abs(leftRange - rightRange) > 0.5f)
        {
            return leftRange.CompareTo(rightRange);
        }

        int lodCompare = right.Level.CompareTo(left.Level);
        if (lodCompare != 0)
        {
            return lodCompare;
        }

        return left.EstimateDistanceToCameraMeters()
            .CompareTo(right.EstimateDistanceToCameraMeters());
    }

    void RunViewConeMaintenance()
    {
        if (!AsyncLodSettings.SkipBacksideMeshBuilds)
        {
            return;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].CullChunkMeshOutsideViewCone();
        }
    }

    void UpdateLeafMeshStats()
    {
        int withMesh = 0;
        int withoutMesh = 0;
        int inFlight = 0;

        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].CollectLeafMeshStats(ref withMesh, ref withoutMesh, ref inFlight);
        }

        AsyncLodSettings.LeavesWithoutMesh = withoutMesh;
        AsyncLodSettings.InFlightLeafBuilds = inFlight;
    }

    internal bool ShouldAllocateMeshForChunk(PlanetQuadtreeNode node)
    {
        if (node.IsInsideViewerSplitQuad())
        {
            return true;
        }

        PlanetQuadtreeNode parentNode = node.DiagParent;
        if (parentNode != null && parentNode.IsInsideViewerSplitQuad())
        {
            return true;
        }

        return !IsChunkBeyondHorizon(node);
    }

    /// <summary>Entire chunk is over the geometric horizon (merge/unload).</summary>
    internal bool IsChunkBehindHorizon(PlanetQuadtreeNode node)
    {
        return IsChunkFullyBeyondHorizon(node);
    }

    internal bool IsChunkNearViewer(PlanetQuadtreeNode node)
    {
        return node.ContainsViewerSurfaceDirection();
    }

    public bool HasPendingLodWork()
    {
        return PlanetFlightChunkMeshPipeline.PendingCount > 0 ||
            leavesNeedingMeshCount > 0 ||
            AsyncLodSettings.InFlightLeafBuilds > 0;
    }

    internal Vector3 GetLodFocusDirectionSim()
    {
        Vector3 focus = ViewerFocusDirectionSim.sqrMagnitude > 0.5f ?
            ViewerFocusDirectionSim.normalized :
            WorldVisualBridge.GetLaunchSiteNormalUnity();

        Vector3 lookAheadVelocity = ViewerVelocitySim.sqrMagnitude > 0.25f ?
            ViewerVelocitySim :
            ViewerVelocityWorld;

        return PlanetLodViewPolicy.ApplyVelocityLookAheadOnSphere(
            focus,
            lookAheadVelocity,
            PlanetRadius,
            ViewerLookAheadSeconds
        );
    }

    internal Vector3 GetShipFocusDirectionSimRaw()
    {
        if (ViewerFocusDirectionSim.sqrMagnitude > 0.5f)
        {
            return ViewerFocusDirectionSim.normalized;
        }

        return WorldVisualBridge.GetLaunchSiteNormalUnity();
    }

    internal bool NodeContainsCachedViewerUv(PlanetQuadtreeNode node)
    {
        if (!cachedViewerUvValid || node == null || cachedViewerFace != node.FaceIndex)
        {
            return false;
        }

        return PlanetPqsLodRules.UvInsideLodNode(
            cachedViewerU,
            cachedViewerV,
            node.MinU,
            node.MaxU,
            node.MinV,
            node.MaxV
        );
    }

    internal bool IsFaceCenterCatchUpProtected(PlanetQuadtreeNode node)
    {
        if (node == null || !cachedViewerUvValid)
        {
            return false;
        }

        return PlanetPqsLodRules.FaceCenterCatchUpTileMustNotMerge(
            PlanetPqsLodRules.FaceCenterPadNeedsCrossL1CatchUp(
                cachedViewerU,
                cachedViewerV),
            PlanetPqsLodRules.NodeUvTouchesFaceCenter(
                node.MinU,
                node.MaxU,
                node.MinV,
                node.MaxV),
            node.Level,
            GetViewerLeafLevel()
        );
    }

    internal bool FaceCenterL1sNeedViewerLod()
    {
        if (!cachedViewerUvValid ||
            faceRoots == null ||
            cachedViewerFace < 0 ||
            cachedViewerFace >= faceRoots.Length ||
            !PlanetPqsLodRules.FaceCenterPadNeedsCrossL1CatchUp(
                cachedViewerU,
                cachedViewerV))
        {
            return false;
        }

        int viewerLevel = GetViewerLeafLevel();
        if (viewerLevel < 0)
        {
            return false;
        }

        PlanetQuadtreeNode faceRoot = faceRoots[cachedViewerFace];
        if (faceRoot == null || faceRoot.DiagChildren == null)
        {
            return false;
        }

        PlanetQuadtreeNode[] quads = faceRoot.DiagChildren;
        for (int i = 0; i < quads.Length; i++)
        {
            if (quads[i].CollectFinestLeafLevel() < viewerLevel)
            {
                return true;
            }
        }

        return false;
    }

    internal void CacheViewerFaceUv()
    {
        cachedViewerUvValid = PlanetCubeSphere.TryDirectionToFaceUv(
            GetShipFocusDirectionSimRaw(),
            out cachedViewerFace,
            out cachedViewerU,
            out cachedViewerV
        );
    }

    internal bool ClampedUvMatchesViewer(int faceIndex, float clampedU, float clampedV)
    {
        return cachedViewerUvValid &&
            cachedViewerFace == faceIndex &&
            PlanetPqsLodRules.ClampedUvIsViewerPoint(
                clampedU,
                clampedV,
                cachedViewerU,
                cachedViewerV
            );
    }

    internal bool TryGetClosestUvOnNode(
        PlanetQuadtreeNode node,
        out float u,
        out float v
    )
    {
        if (cachedViewerUvValid && cachedViewerFace == node.FaceIndex)
        {
            PlanetPqsLodRules.ClampUvToNode(
                cachedViewerU,
                cachedViewerV,
                node.MinU,
                node.MaxU,
                node.MinV,
                node.MaxV,
                out double clampedU,
                out double clampedV
            );
            u = (float)clampedU;
            v = (float)clampedV;
            return true;
        }

        u = (node.MinU + node.MaxU) * 0.5f;
        v = (node.MinV + node.MaxV) * 0.5f;
        return false;
    }

    internal void SyncViewerFocusDirections()
    {
        CacheViewerFaceUv();
        Vector3 ledFocus = GetLodFocusDirectionSim();
        ViewerFocusDirectionVisual = PlanetCubeSphere.SimDirectionToMeshFrame(ledFocus);
    }

    internal Vector3 GetLodFocusDirectionVisual()
    {
        if (ViewerFocusDirectionVisual.sqrMagnitude > 0.5f)
        {
            return ViewerFocusDirectionVisual.normalized;
        }

        SyncViewerFocusDirections();
        return ViewerFocusDirectionVisual;
    }

    internal Vector3 GetObserverLocalMeshPosition()
    {
        if (HasViewerWorldAnchor && PlanetTransform != null)
        {
            return GetViewerAnchorLocalMeshPosition();
        }

        Vector3 focusSim = GetLodFocusDirectionSim();
        Vector3 meshDirection = PlanetCubeSphere.SimDirectionToMeshFrame(focusSim);

        if (meshDirection.sqrMagnitude <= 1e-8f)
        {
            meshDirection = Vector3.up;
        }

        meshDirection.Normalize();
        float heightOffset = SampleHeightOffset(focusSim);
        float radius = PlanetRadius + heightOffset + ViewerAltitudeMeters;
        return meshDirection * radius;
    }

    static void AccumulateClosestAngleSample(
        ref float closestAngle,
        Vector3 viewerDirection,
        int faceIndex,
        float u,
        float v
    )
    {
        Vector3 chunkDirection = PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v);
        float angle = Vector3.Angle(viewerDirection, chunkDirection);
        closestAngle = Mathf.Min(closestAngle, angle);
    }

    internal PlanetFlightChunkCpuBuilder.FlightBiomeBuildParams CreateFlightBiomeBuildParams(
        float chunkSpanMeters,
        int resolution
    )
    {
        float horizontalStepMeters = chunkSpanMeters / Mathf.Max(1, resolution - 1);
        float maxHeightDeltaMeters = horizontalStepMeters * 0.45f;
        return new PlanetFlightChunkCpuBuilder.FlightBiomeBuildParams(
            FlattenRadiusMeters,
            HardFlattenRadiusMeters,
            (float)LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters(),
            LaunchSiteDirection,
            maxHeightDeltaMeters
        );
    }

    void TryAltitudeCapMerges()
    {
        if (faceRoots == null)
        {
            return;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].MergeOverMaxLodRecursive();
            faceRoots[i].MergeOverAngularRingRecursive();
        }
    }

    void TryBudgetPressureMerges(bool forceForViewer)
    {
        if (faceRoots == null)
        {
            return;
        }

        if (!forceForViewer && GetActiveChunkCount() + 4 <= MaxActiveChunks)
        {
            return;
        }

        budgetMergeScratch.Clear();
        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].CollectBudgetPressureMergeCandidates(budgetMergeScratch);
        }

        budgetMergeScratch.Sort(CompareBudgetPressureMerge);
        int merges = 0;
        bool overCap = PlanetPqsLodRules.ChunkCountExceedsCap(
            GetActiveChunkCount(),
            MaxActiveChunks);
        int mergeLimit = forceForViewer ? 8 : AsyncLodSettings.MaxUnloadMergesPerFrame;
        if (overCap)
        {
            mergeLimit = Mathf.Max(mergeLimit, 16);
        }

        for (int i = 0; i < budgetMergeScratch.Count; i++)
        {
            bool needSlots = GetActiveChunkCount() + 4 > MaxActiveChunks;
            bool compactRing = clampLodToViewerRingThisTick &&
                IsNodeOutsideViewerFinestRing(budgetMergeScratch[i]);
            if (!needSlots && !compactRing)
            {
                break;
            }

            if (!forceForViewer && !AsyncLodSettings.TryConsumeUnloadMergeBudget())
            {
                break;
            }

            if (forceForViewer && merges >= mergeLimit)
            {
                break;
            }

            if (budgetMergeScratch[i].TryBudgetPressureMerge())
            {
                merges++;
            }
        }
    }

    int CompareBudgetPressureMerge(PlanetQuadtreeNode left, PlanetQuadtreeNode right)
    {
        int levelCompare = right.Level.CompareTo(left.Level);
        if (levelCompare != 0)
        {
            return levelCompare;
        }

        return GetSlantRangeToChunkMeters(right).CompareTo(GetSlantRangeToChunkMeters(left));
    }

    void SynchronizeNeighborLevels(int splitBudget)
    {
        if (splitBudget <= 0 || !CanAllocateMoreChunks() || IsLodTickTimeBudgetExhausted())
        {
            return;
        }

        neighborCatchUpSplitBudget = splitBudget;
        neighborCatchUpSplitsUsed = 0;
        int maxIterations = 2;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            if (neighborCatchUpSplitsUsed >= neighborCatchUpSplitBudget ||
                !CanAllocateMoreChunks() ||
                IsLodTickTimeBudgetExhausted())
            {
                break;
            }

            bool changed = false;

            for (int i = 0; i < faceRoots.Length; i++)
            {
                if (!faceRoots[i].IntersectsHorizonDisk())
                {
                    continue;
                }

                changed |= faceRoots[i].SynchronizeNeighborLevelsRecursive();
            }

            if (!changed)
            {
                break;
            }
        }
    }

    internal bool SynchronizeNeighborLevelsForLeaf(PlanetQuadtreeNode leaf)
    {
        if (leaf == null || !leaf.IsLeaf)
        {
            return false;
        }

        if (IsChunkBeyondHorizon(leaf) && !leaf.IsInsideViewerSplitQuad())
        {
            return false;
        }

        if (!PlanetPqsLodRules.AllowCatchUpFromLeaf(
                leaf.ContainsViewerSurfaceDirection(),
                ViewerLeafNeedsSplit()))
        {
            return false;
        }

        const float edgeEpsilon = 1e-5f;
        float midU = (leaf.MinU + leaf.MaxU) * 0.5f;
        float midV = (leaf.MinV + leaf.MaxV) * 0.5f;
        float uSpan = Mathf.Max(leaf.MaxU - leaf.MinU, edgeEpsilon);
        float vSpan = Mathf.Max(leaf.MaxV - leaf.MinV, edgeEpsilon);
        int minNeighborLevel = PlanetPqsLodRules.MinNeighborLevelToCloseCrack(leaf.Level);
        bool viewerClimb =
            leaf.ContainsViewerSurfaceDirection() &&
            leaf.IsSplitBlockedByCrack() &&
            ViewerLeafNeedsSplit();
        if (viewerClimb)
        {
            minNeighborLevel = leaf.Level;
        }

        bool changed = false;

        if (leaf.ContainsViewerSurfaceDirection() &&
            TryGetDiagonalCatchUpProbe(leaf, out float diagU, out float diagV))
        {
            changed |= EnsureNeighborLevelAt(leaf, diagU, diagV, leaf.Level);
        }

        changed |= EnsureNeighborLevelAt(
            leaf,
            leaf.MinU - uSpan * 0.25f,
            midV,
            FaceCenterEdgeCatchUpRequired(
                leaf,
                minNeighborLevel,
                leaf.MinU - uSpan * 0.25f,
                midV));
        changed |= EnsureNeighborLevelAt(
            leaf,
            leaf.MaxU + uSpan * 0.25f,
            midV,
            FaceCenterEdgeCatchUpRequired(
                leaf,
                minNeighborLevel,
                leaf.MaxU + uSpan * 0.25f,
                midV));
        changed |= EnsureNeighborLevelAt(
            leaf,
            midU,
            leaf.MinV - vSpan * 0.25f,
            FaceCenterEdgeCatchUpRequired(
                leaf,
                minNeighborLevel,
                midU,
                leaf.MinV - vSpan * 0.25f));
        changed |= EnsureNeighborLevelAt(
            leaf,
            midU,
            leaf.MaxV + vSpan * 0.25f,
            FaceCenterEdgeCatchUpRequired(
                leaf,
                minNeighborLevel,
                midU,
                leaf.MaxV + vSpan * 0.25f));

        return changed;
    }

    bool TryGetDiagonalCatchUpProbe(
        PlanetQuadtreeNode leaf,
        out float probeU,
        out float probeV
    )
    {
        probeU = 0f;
        probeV = 0f;
        if (!cachedViewerUvValid || cachedViewerFace != leaf.FaceIndex)
        {
            return false;
        }

        if (!PlanetPqsLodRules.TryDiagonalCatchUpProbe(
                cachedViewerU,
                cachedViewerV,
                leaf.MinU,
                leaf.MaxU,
                leaf.MinV,
                leaf.MaxV,
                out double du,
                out double dv))
        {
            return false;
        }

        probeU = (float)du;
        probeV = (float)dv;
        return true;
    }

    int FaceCenterEdgeCatchUpRequired(
        PlanetQuadtreeNode leaf,
        int defaultRequired,
        float probeU,
        float probeV
    )
    {
        if (leaf == null ||
            !leaf.ContainsViewerSurfaceDirection() ||
            !cachedViewerUvValid)
        {
            return defaultRequired;
        }

        return PlanetPqsLodRules.FaceCenterCrossL1CatchUpRequired(
            defaultRequired,
            leaf.Level,
            cachedViewerU,
            cachedViewerV,
            probeU,
            probeV
        );
    }

    bool EnsureNeighborLevelAt(
        PlanetQuadtreeNode leaf,
        float probeU,
        float probeV,
        int requiredLevel
    )
    {
        PlanetQuadtreeNode neighbor = FindNeighborLeaf(leaf.FaceIndex, probeU, probeV);

        if (neighbor == null || neighbor == leaf || neighbor.Level >= requiredLevel)
        {
            return false;
        }

        if (IsChunkBeyondHorizon(neighbor) && !neighbor.IsInsideViewerSplitQuad())
        {
            return false;
        }

        bool allowExceedingRing =
            leaf.ContainsViewerSurfaceDirection() && ViewerLeafNeedsSplit();
        int required = PlanetPqsLodRules.ClampCatchUpToRingCap(
            requiredLevel,
            GetAngularRingLodCap(neighbor),
            allowExceedingRing,
            neighbor.Level
        );
        if (neighbor.Level >= required)
        {
            return false;
        }

        if (neighborCatchUpSplitsUsed >= neighborCatchUpSplitBudget)
        {
            return false;
        }

        if (!neighbor.TrySplitToCloseLodGap())
        {
            return false;
        }

        neighborCatchUpSplitsUsed++;
        return true;
    }

    public bool CanAllocateMoreChunks()
    {
        return PlanetPqsLodRules.CanAllocateSplit(GetActiveChunkCount(), MaxActiveChunks);
    }

    internal bool ShouldStarveNonPadFinestSplits()
    {
        return starveNonPadFinestThisTick || clampLodToViewerRingThisTick;
    }

    internal bool ShouldClampLodToViewerRingThisTick()
    {
        return clampLodToViewerRingThisTick;
    }

    internal bool IsNodeOutsideViewerFinestRing(PlanetQuadtreeNode node)
    {
        PlanetQuadtreeNode viewerLeaf = FindAnyViewerLeaf();
        if (viewerLeaf == null || node == null)
        {
            return false;
        }

        return PlanetPqsLodRules.ChunkIsOutsideViewerFinestRing(
            GetSlantRangeToChunkMeters(node),
            GetSlantRangeToChunkMeters(viewerLeaf)
        );
    }

    bool ComputeStarveNonPadFinestSplits()
    {
        return PlanetPqsLodRules.StarveNonPadFinestSplits(
            PlanetPqsLodRules.ChunkCapIsUnderPressure(GetActiveChunkCount(), MaxActiveChunks),
            CanAllocateMoreChunks(),
            ViewerLeafNeedsSplit()
        );
    }

    /// <summary>
    /// Finest pad 2×2 (parent ≥ L2) has both leaves and branches — L8 next to L9
    /// under the ship. Coarse L1 quadrants on a 4-corner pad are not this.
    /// </summary>
    public bool ViewerPadTwoByTwoIsIncomplete()
    {
        if (faceRoots == null)
        {
            return false;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            PlanetQuadtreeNode viewerLeaf = faceRoots[i].FindViewerLeaf();
            if (viewerLeaf != null &&
                viewerLeaf.IsLeaf &&
                viewerLeaf.FinestTwoByTwoIsMixed())
            {
                return true;
            }
        }

        return false;
    }

    void ApplyLodCapSplitPolicy()
    {
        TryAltitudeCapMerges();
        bool overCap = PlanetPqsLodRules.ChunkCountExceedsCap(
            GetActiveChunkCount(),
            MaxActiveChunks);
        starveNonPadFinestThisTick = ComputeStarveNonPadFinestSplits();
        clampLodToViewerRingThisTick = PlanetPqsLodRules.ShouldClampLodToViewerRing(
            PlanetPqsLodRules.ChunkCapIsUnderPressure(GetActiveChunkCount(), MaxActiveChunks),
            CanAllocateMoreChunks());
        TryBudgetPressureMerges(
            forceForViewer: overCap ||
                starveNonPadFinestThisTick ||
                clampLodToViewerRingThisTick);
        starveNonPadFinestThisTick = ComputeStarveNonPadFinestSplits();
        clampLodToViewerRingThisTick = PlanetPqsLodRules.ShouldClampLodToViewerRing(
            PlanetPqsLodRules.ChunkCapIsUnderPressure(GetActiveChunkCount(), MaxActiveChunks),
            CanAllocateMoreChunks());
        HoldNonViewerSplits = starveNonPadFinestThisTick || clampLodToViewerRingThisTick;
        LastTickHeldNonViewerSplits = HoldNonViewerSplits;
    }

    /// <summary>
    /// When the chunk cap is mostly full, stop SSE near the limb so the pad
    /// can keep splitting. Inner ~85% of the horizon disk still rings (0.45×
    /// left one face-quadrant coarse at a 4-corner pad). Camera look is not used.
    /// </summary>
    internal bool ShouldDeferPeripheralSplit(PlanetQuadtreeNode node)
    {
        if (node.ContainsViewerSurfaceDirection() || node.TouchesViewerSurface())
        {
            return false;
        }

        if (GetActiveChunkCount() < (MaxActiveChunks * 3) / 4)
        {
            return false;
        }

        float nearest = GetGeocentricAngleFromViewerDegrees(node);
        return nearest > GetHorizonHalfAngleDegrees() * 0.85f;
    }

    /// <summary>
    /// Non-viewer tiles may not exceed this LOD. Ring width is the <em>viewer</em>
    /// leaf span (one finest tile). Using this node's span + slider maxL let an L8
    /// kilometres away keep <c>cap=13</c> when the slider was 14 — SSE then drilled
    /// a mountain tongue (<c>L1q 14/13/14/14</c>, 8 FPS). Height follows the current
    /// viewer leaf so bootstrap cannot paint L13 while the ship is still L3.
    /// Adjacent tiles use nearest angle (often &lt; one span → ring 0 → cap −1), not
    /// the node centre, so pad-adjacent L5s do not get <c>RING cap=0</c>.
    /// </summary>
    internal int GetAngularRingLodCap(PlanetQuadtreeNode node)
    {
        if (node == null)
        {
            return 0;
        }

        int maxLod = GetActiveMaxLodLevel();
        if (node.ContainsViewerSurfaceDirection())
        {
            return maxLod;
        }

        int viewerLevel = GetViewerLeafLevel();
        int capHeight = viewerLevel < 0 ? maxLod : Mathf.Min(maxLod, viewerLevel);

        float ringSpanMeters = GetViewerLeafSpanMeters();
        if (ringSpanMeters >= 1e12f)
        {
            return capHeight;
        }

        float angularSpanDegrees =
            ringSpanMeters / Mathf.Max(PlanetRadius, 1f) * Mathf.Rad2Deg;
        int ring = PlanetPqsLodRules.AngularRingIndex(
            GetGeocentricAngleFromViewerDegrees(node),
            angularSpanDegrees
        );
        return PlanetPqsLodRules.AngularRingLodCap(capHeight, ring, false);
    }

    internal float GetChunkCenterAngleFromViewerDegrees(PlanetQuadtreeNode node)
    {
        return Vector3.Angle(
            GetShipFocusDirectionSimRaw(),
            node.GetCenterUnitDirectionSim()
        );
    }

    internal int GetViewerLeafLevel()
    {
        if (faceRoots == null)
        {
            return -1;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            PlanetQuadtreeNode leaf = faceRoots[i].FindViewerLeaf();
            if (leaf != null && leaf.IsLeaf)
            {
                return leaf.Level;
            }
        }

        return -1;
    }

    internal bool ViewerIsBelowMaxLod()
    {
        int viewerLevel = GetViewerLeafLevel();
        return viewerLevel >= 0 && viewerLevel < GetActiveMaxLodLevel();
    }

    internal void RecordLodFlap(string verb, PlanetQuadtreeNode node)
    {
        if (node == null)
        {
            return;
        }

        if (verb == "split")
        {
            tickSplitCount++;
        }
        else
        {
            tickMergeCount++;
        }

        flapLog[flapLogWrite] =
            verb +
            " F" +
            node.FaceIndex +
            " L" +
            node.Level +
            " " +
            node.MinU.ToString("F2") +
            "," +
            node.MinV.ToString("F2");
        flapLogWrite = (flapLogWrite + 1) % MaxFlapLogEntries;
        if (flapLogCount < MaxFlapLogEntries)
        {
            flapLogCount++;
        }
    }

    string FormatFlapLog()
    {
        if (flapLogCount <= 0)
        {
            return "";
        }

        int start = flapLogCount < MaxFlapLogEntries ? 0 : flapLogWrite;
        var sb = new StringBuilder(128);
        for (int i = 0; i < flapLogCount; i++)
        {
            if (i > 0)
            {
                sb.Append(" | ");
            }

            sb.Append(flapLog[(start + i) % MaxFlapLogEntries]);
        }

        return sb.ToString();
    }

    internal bool ViewerLeafNeedsSplit()
    {
        if (faceRoots == null)
        {
            return false;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            PlanetQuadtreeNode leaf = faceRoots[i].FindViewerLeaf();
            if (leaf != null && leaf.IsLeaf && leaf.LeafWantsRefinement())
            {
                return true;
            }
        }

        return false;
    }

    internal PlanetQuadtreeNode FindAnyViewerLeaf()
    {
        if (faceRoots == null)
        {
            return null;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            PlanetQuadtreeNode leaf = faceRoots[i].FindViewerLeaf();
            if (leaf == null)
            {
                continue;
            }

            if (leaf.IsLeaf)
            {
                return leaf;
            }

            if (cachedViewerUvValid && cachedViewerFace == leaf.FaceIndex)
            {
                return leaf.FindLeafNodeAt(cachedViewerU, cachedViewerV);
            }

            return leaf;
        }

        return null;
    }

    internal bool ViewerLeafBlockedByCrack()
    {
        PlanetQuadtreeNode leaf = FindAnyViewerLeaf();
        return leaf != null &&
            leaf.IsLeaf &&
            leaf.Level < GetActiveMaxLodLevel() &&
            leaf.IsSplitBlockedByCrack();
    }

    internal bool ViewerLeafEdgeTouches(
        PlanetQuadtreeNode viewerLeaf,
        PlanetQuadtreeNode other
    )
    {
        if (viewerLeaf == null || other == null)
        {
            return false;
        }

        const float edgeEpsilon = 1e-5f;
        float midU = (viewerLeaf.MinU + viewerLeaf.MaxU) * 0.5f;
        float midV = (viewerLeaf.MinV + viewerLeaf.MaxV) * 0.5f;
        float uSpan = Mathf.Max(viewerLeaf.MaxU - viewerLeaf.MinU, edgeEpsilon);
        float vSpan = Mathf.Max(viewerLeaf.MaxV - viewerLeaf.MinV, edgeEpsilon);
        return FindNeighborLeaf(
                viewerLeaf.FaceIndex,
                viewerLeaf.MinU - uSpan * 0.25f,
                midV) == other ||
            FindNeighborLeaf(
                viewerLeaf.FaceIndex,
                viewerLeaf.MaxU + uSpan * 0.25f,
                midV) == other ||
            FindNeighborLeaf(
                viewerLeaf.FaceIndex,
                midU,
                viewerLeaf.MinV - vSpan * 0.25f) == other ||
            FindNeighborLeaf(
                viewerLeaf.FaceIndex,
                midU,
                viewerLeaf.MaxV + vSpan * 0.25f) == other;
    }

    internal bool ViewerLeafDiagonalTouches(
        PlanetQuadtreeNode viewerLeaf,
        PlanetQuadtreeNode other
    )
    {
        if (viewerLeaf == null || other == null)
        {
            return false;
        }

        if (!TryGetDiagonalCatchUpProbe(viewerLeaf, out float diagU, out float diagV))
        {
            return false;
        }

        return FindNeighborLeaf(viewerLeaf.FaceIndex, diagU, diagV) == other;
    }

    public PlanetQuadtreeNode FindLeafNodeAt(int faceIndex, float u, float v)
    {
        if (faceRoots == null || faceIndex < 0 || faceIndex >= faceRoots.Length)
        {
            return null;
        }

        return faceRoots[faceIndex].FindLeafNodeAt(u, v);
    }

    internal PlanetQuadtreeNode FindNeighborLeaf(int faceIndex, float probeU, float probeV)
    {
        if (!PlanetCubeSphereCoords.TryResolveUvProbe(
            faceIndex,
            probeU,
            probeV,
            out int resolvedFace,
            out double resolvedU,
            out double resolvedV))
        {
            return null;
        }

        return FindLeafNodeAt(resolvedFace, (float)resolvedU, (float)resolvedV);
    }

    public int GetActiveMaxLodLevel()
    {
        int frame = Time.frameCount;
        if (activeMaxLodFrame == frame && altitudeCappedMaxLod >= 0)
        {
            return altitudeCappedMaxLod;
        }

        int maxLodCeiling = GetMaxLodCeiling();
        int unboundedGround = GetPadTargetLodLevel();
        int minGround = Mathf.Min(10, maxLodCeiling);
        int groundLevel = Mathf.Clamp(unboundedGround, minGround, maxLodCeiling);

        int raw = PlanetLodAltitudePolicy.GetMaxLodLevelForAltitude(
            ViewerAltitudeMeters,
            PlanetRadius,
            groundLevel,
            maxLodCeiling
        );

        bool descending = ViewerAltitudeMeters < smoothedLodAltitudeMeters - 250f;
        altitudeCappedMaxLod = PlanetLodAltitudePolicy.ApplyMaxLodHysteresis(
            raw,
            altitudeCappedMaxLod,
            maxLodCeiling,
            lastMaxLodCeiling,
            descending
        );
        lastMaxLodCeiling = maxLodCeiling;
        activeMaxLodFrame = frame;
        return altitudeCappedMaxLod;
    }

    /// <summary>
    /// LOD needed for <see cref="LaunchSiteMaxChunkSpanMeters"/> on this planet, ignoring
    /// the Terrain Graphics slider. Overlay <c>needL</c> vs <c>maxL</c> / <c>capL</c>.
    /// </summary>
    public int GetPadTargetLodLevel()
    {
        float targetSpan = Mathf.Max(LaunchSiteMaxChunkSpanMeters, 32f);
        return Mathf.CeilToInt(
            Mathf.Log(Mathf.Max(PlanetRadius * 2f / targetSpan, 2f), 2f)
        );
    }

    /// <summary>
    /// World span of a leaf at the current max LOD. Pad bootstrap must not wait for 64 m
    /// when the slider/altitude cap cannot reach that (Moon L8 ≈ 1.3 km).
    /// </summary>
    public float GetFinestPossibleViewerSpanMeters()
    {
        int maxLod = Mathf.Clamp(GetActiveMaxLodLevel(), 0, 30);
        return PlanetRadius * 2f / (1 << maxLod);
    }

    public int RuntimeMaxLodCap { get; set; }

    /// <summary>
    /// Last <see cref="RefreshLodLookAhead"/> / viewer-face bootstrap still wanted splits
    /// or meshes. Pad idle-skip must not freeze while three quadrants are catching up.
    /// </summary>
    public bool LastLodRefreshHadPendingWork { get; private set; }

    int GetMaxLodCeiling()
    {
        return RuntimeMaxLodCap > 0 ?
            Mathf.Min(RuntimeMaxLodCap, MaxLodLevel) :
            MaxLodLevel;
    }

    public void RefreshLodImmediate()
    {
        RefreshLodBudgeted(int.MaxValue, int.MaxValue);
        FeedPendingMeshQueue(int.MaxValue);
    }

    public void RefreshViewerFaceLodLookAhead(
        int splitBudget,
        int meshBuildBudget,
        bool allowNeighborSync = true
    )
    {
        UpdateViewerFrame();
        UpdateSmoothedLodAltitude();
        AsyncLodSettings.MeshCullBudgetRemaining = AsyncLodSettings.MaxMeshDestroysPerFrame;
        AsyncLodSettings.UnloadMergeBudgetRemaining = AsyncLodSettings.MaxUnloadMergesPerFrame;

        ApplyLodCapSplitPolicy();

        int splitsUsed = 0;
        int meshesUsed = 0;
        bool pending = false;

        for (int i = 0; i < faceRoots.Length; i++)
        {
            if (!faceRoots[i].ContainsViewerSurfaceDirection())
            {
                continue;
            }

            pending = faceRoots[i].RefreshLodBudgeted(
                ref splitsUsed,
                ref meshesUsed,
                splitBudget,
                meshBuildBudget
            );

            if (allowNeighborSync)
            {
                int neighborBudget = Mathf.Min(
                    FlightPlanetSphere.HitchMaxNeighborSplitsPerFrame,
                    Mathf.Max(0, splitBudget - splitsUsed)
                );
                SynchronizeNeighborLevels(neighborBudget);
            }

            BuildMeshesForNearestLeaves(meshBuildBudget);
            RunViewConeMaintenance();
            UpdateLeafMeshStats();
            HoldNonViewerSplits = false;
            LastLodRefreshHadPendingWork = pending || HasPendingLodWork();
            return;
        }

        RefreshLodLookAhead(splitBudget, meshBuildBudget, allowNeighborSync);
    }

    public bool ViewerAreaHasRenderableMesh()
    {
        if (faceRoots == null)
        {
            return false;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            if (!faceRoots[i].ContainsViewerSurfaceDirection())
            {
                continue;
            }

            PlanetQuadtreeNode viewerLeaf = faceRoots[i].FindViewerLeaf();

            if (viewerLeaf != null && viewerLeaf.HasRenderableMesh)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Any in-disk leaf still missing a mesh or still wanting split. Do not idle-skip.
    /// </summary>
    public bool DiskHasUnfinishedLod()
    {
        if (faceRoots == null)
        {
            return false;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            if (faceRoots[i].HasUnfinishedDiskLod())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when an ancestor of the viewer still lacks all four drawable children.
    /// Idle-skip would freeze that face as a single quadrant / black hole.
    /// </summary>
    public bool ViewerAncestryHasIncompleteCoverage()
    {
        if (faceRoots == null)
        {
            return false;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            if (!faceRoots[i].ContainsViewerSurfaceDirection())
            {
                continue;
            }

            PlanetQuadtreeNode viewerLeaf = faceRoots[i].FindViewerLeaf();
            if (viewerLeaf != null && viewerLeaf.AncestryHasIncompleteDirectChildren())
            {
                return true;
            }
        }

        return false;
    }

    string FormatViewerGapEdges()
    {
        PlanetQuadtreeNode leaf = FindAnyViewerLeaf();
        if (leaf == null || !leaf.IsLeaf)
        {
            return "";
        }

        const float edgeEpsilon = 1e-5f;
        float midU = (leaf.MinU + leaf.MaxU) * 0.5f;
        float midV = (leaf.MinV + leaf.MaxV) * 0.5f;
        float uSpan = Mathf.Max(leaf.MaxU - leaf.MinU, edgeEpsilon);
        float vSpan = Mathf.Max(leaf.MaxV - leaf.MinV, edgeEpsilon);
        int minAllowed = leaf.Level - PlanetPqsConstants.MaxNeighborLodGapForMerge;
        StringBuilder edges = new StringBuilder();
        AppendGapEdge(edges, leaf, "U-", leaf.MinU - (uSpan * 0.25f), midV, minAllowed);
        AppendGapEdge(edges, leaf, "U+", leaf.MaxU + (uSpan * 0.25f), midV, minAllowed);
        AppendGapEdge(edges, leaf, "V-", midU, leaf.MinV - (vSpan * 0.25f), minAllowed);
        AppendGapEdge(edges, leaf, "V+", midU, leaf.MaxV + (vSpan * 0.25f), minAllowed);
        if (TryGetDiagonalCatchUpProbe(leaf, out float diagU, out float diagV))
        {
            AppendGapEdge(edges, leaf, "D", diagU, diagV, leaf.Level);
        }

        if (leaf.AncestryHasCoarseSiblings())
        {
            edges.Append(" sib");
        }

        return edges.ToString();
    }

    void AppendGapEdge(
        StringBuilder edges,
        PlanetQuadtreeNode leaf,
        string label,
        float probeU,
        float probeV,
        int minAllowed
    )
    {
        if (!NeighborLevelIsTooCoarse(leaf, probeU, probeV, minAllowed))
        {
            return;
        }

        PlanetQuadtreeNode neighbor = FindNeighborLeaf(leaf.FaceIndex, probeU, probeV);
        edges.Append(' ');
        edges.Append(label);
        edges.Append("nL");
        edges.Append(neighbor != null ? neighbor.Level : 0);
    }

    /// <summary>
    /// Viewer tile is more than one LOD finer than an edge neighbor. Pad idle-skip
    /// used to freeze that: one quadrant under the ship, three coarse siblings.
    /// </summary>
    public bool ViewerNeighborhoodHasLodGap()
    {
        if (faceRoots == null)
        {
            return false;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            if (!faceRoots[i].ContainsViewerSurfaceDirection())
            {
                continue;
            }

            PlanetQuadtreeNode viewerLeaf = faceRoots[i].FindViewerLeaf();
            if (viewerLeaf == null || !viewerLeaf.IsLeaf)
            {
                continue;
            }

            if (viewerLeaf.AncestryHasCoarseSiblings() || ViewerLeafHasCoarserNeighbor(viewerLeaf))
            {
                return true;
            }
        }

        return false;
    }

    bool ViewerLeafHasCoarserNeighbor(PlanetQuadtreeNode leaf)
    {
        const float edgeEpsilon = 1e-5f;
        float midU = (leaf.MinU + leaf.MaxU) * 0.5f;
        float midV = (leaf.MinV + leaf.MaxV) * 0.5f;
        float uSpan = Mathf.Max(leaf.MaxU - leaf.MinU, edgeEpsilon);
        float vSpan = Mathf.Max(leaf.MaxV - leaf.MinV, edgeEpsilon);
        int minAllowedNeighborLevel = leaf.Level - PlanetPqsConstants.MaxNeighborLodGapForMerge;

        return NeighborLevelIsTooCoarse(leaf, leaf.MinU - (uSpan * 0.25f), midV, minAllowedNeighborLevel) ||
            NeighborLevelIsTooCoarse(leaf, leaf.MaxU + (uSpan * 0.25f), midV, minAllowedNeighborLevel) ||
            NeighborLevelIsTooCoarse(leaf, midU, leaf.MinV - (vSpan * 0.25f), minAllowedNeighborLevel) ||
            NeighborLevelIsTooCoarse(leaf, midU, leaf.MaxV + (vSpan * 0.25f), minAllowedNeighborLevel) ||
            DiagonalNeighborIsTooCoarse(leaf);
    }

    bool DiagonalNeighborIsTooCoarse(PlanetQuadtreeNode leaf)
    {
        if (!TryGetDiagonalCatchUpProbe(leaf, out float diagU, out float diagV))
        {
            return false;
        }

        return NeighborLevelIsTooCoarse(leaf, diagU, diagV, leaf.Level);
    }

    bool NeighborLevelIsTooCoarse(
        PlanetQuadtreeNode leaf,
        float probeU,
        float probeV,
        int minAllowedNeighborLevel
    )
    {
        PlanetQuadtreeNode neighbor = FindNeighborLeaf(leaf.FaceIndex, probeU, probeV);
        if (neighbor == null || neighbor == leaf)
        {
            return false;
        }

        if (IsChunkBeyondHorizon(neighbor) && !neighbor.IsInsideViewerSplitQuad())
        {
            return false;
        }

        return neighbor.Level < minAllowedNeighborLevel;
    }

    public float GetViewerLeafSpanMeters()
    {
        if (faceRoots == null)
        {
            return float.MaxValue;
        }

        float span = float.MaxValue;

        for (int i = 0; i < faceRoots.Length; i++)
        {
            if (!faceRoots[i].ContainsViewerSurfaceDirection())
            {
                continue;
            }

            PlanetQuadtreeNode viewerLeaf = faceRoots[i].FindViewerLeaf();

            if (viewerLeaf == null)
            {
                continue;
            }

            span = Mathf.Min(span, viewerLeaf.GetWorldSpanMeters());
        }

        return span;
    }

    public void RefreshDebugOutlines()
    {
        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].RefreshDebugOutlines();
        }
    }

    public void SetLodDebugVisible(bool visible)
    {
        ShowLodDebug = visible;

        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].SetLodDebugVisible(visible);
            faceRoots[i].RefreshDebugOutlines();
        }
    }

    /// <summary>
    /// Live count of allocated chunk objects. ShouldSplit consults the budget for every node on
    /// every tick, so walking the tree to answer it was quadratic in the chunk count.
    /// </summary>
    public int GetActiveChunkCount()
    {
        return activeChunkObjectCount;
    }

    internal void NotifyChunkObjectCreated()
    {
        activeChunkObjectCount++;
    }

    internal void NotifyChunkObjectDestroyed()
    {
        activeChunkObjectCount = Mathf.Max(0, activeChunkObjectCount - 1);
    }

    public int FindLeafLevelAt(int faceIndex, float u, float v)
    {
        if (faceRoots == null || faceIndex < 0 || faceIndex >= faceRoots.Length)
        {
            return 0;
        }

        return faceRoots[faceIndex].FindLeafLevelAt(u, v);
    }

    internal int FindAdjacentLeafLevel(int faceIndex, float u, float v)
    {
        PlanetQuadtreeNode neighbor = FindNeighborLeaf(faceIndex, u, v);
        return neighbor != null ? neighbor.Level : 0;
    }

    public void InvalidateLaunchSiteMeshes()
    {
        if (faceRoots == null)
        {
            return;
        }

        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].InvalidateLaunchSiteMeshesRecursive();
        }
    }

    public void CollapseToRoots()
    {
        for (int i = 0; i < faceRoots.Length; i++)
        {
            faceRoots[i].CollapseToRoot();
        }
    }

    public void Dispose()
    {
        PlanetFlightChunkMeshPipeline.CancelAll();

        if (faceRoots != null)
        {
            for (int i = 0; i < faceRoots.Length; i++)
            {
                faceRoots[i].Dispose();
            }

            faceRoots = null;
        }

        heightSampler.Dispose();
        ClearChunkObjectPool();
    }

    internal GameObject RentChunkObject(string chunkName)
    {
        GameObject chunkObject = null;

        while (chunkObjectPool.Count > 0 && chunkObject == null)
        {
            chunkObject = chunkObjectPool.Pop();
        }

        if (chunkObject == null)
        {
            chunkObject = new GameObject(chunkName);
            chunkObject.transform.SetParent(ChunksRoot, false);
            FlightRenderLayers.ApplyLocalPhysicsLayer(chunkObject);
        }
        else
        {
            chunkObject.name = chunkName;
        }

        MeshRenderer pooledRenderer = chunkObject.GetComponent<MeshRenderer>();
        if (pooledRenderer != null)
        {
            pooledRenderer.enabled = false;
        }

        chunkObject.SetActive(false);

        Transform chunkTransform = chunkObject.transform;
        chunkTransform.SetParent(ChunksRoot, false);
        chunkTransform.localPosition = Vector3.zero;
        chunkTransform.localRotation = Quaternion.identity;
        chunkTransform.localScale = Vector3.one;
        NotifyChunkObjectCreated();
        return chunkObject;
    }

    internal void ReleaseChunkObject(GameObject chunkObject)
    {
        if (chunkObject == null)
        {
            return;
        }

        MeshFilter meshFilter = chunkObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = null;
        }

        MeshCollider collider = chunkObject.GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.sharedMesh = null;
            collider.enabled = false;
        }

        MeshRenderer meshRenderer = chunkObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        chunkObject.SetActive(false);
        chunkObjectPool.Push(chunkObject);
        NotifyChunkObjectDestroyed();
    }

    void ClearChunkObjectPool()
    {
        chunkObjectPool.Clear();
    }

    void CreateFaceRoots()
    {
        faceRoots = new PlanetQuadtreeNode[PlanetCubeSphere.FaceNormals.Length];

        for (int faceIndex = 0; faceIndex < faceRoots.Length; faceIndex++)
        {
            faceRoots[faceIndex] = new PlanetQuadtreeNode(
                this,
                faceIndex,
                0,
                0f,
                1f,
                0f,
                1f
            );
        }
    }

    public float SampleHeightOffset(Vector3 unitDirection)
    {
        Vector3 sampleDirection = PlanetCubeSphere.SanitizeUnitDirectionForHeightSampling(
            unitDirection
        );
        float heightOffset = PlanetFlightElevationSampling.SampleMeshHeightOffsetMeters(
            sampleDirection
        );
        if (!float.IsFinite(heightOffset))
        {
            heightOffset = 0f;
        }

        heightOffset = ClampHeightOffsetMeters(heightOffset, sampleDirection);
        return ApplyLaunchSiteHeightFlatten(heightOffset, unitDirection);
    }

    float SampleHeightOffsetSlow(Vector2 uv)
    {
        if (Heightmap == null || !Heightmap.isReadable)
        {
            return 0f;
        }

        Color heightSample = Heightmap.GetPixelBilinear(uv.x, uv.y);
        return (float)PlanetSurfaceMaps.SampleEarthHeightMeters(heightSample.r);
    }

    static float ClampHeightOffsetMeters(float heightOffset, Vector3 unitDirection)
    {
        float maxAbsHeightMeters = PlanetElevationCatalog.IsLoaded ?
            12000f * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale :
            12000f;
        float minHeightMeters = PlanetElevationCatalog.IsLoaded ?
            -11000f * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale :
            -2500f;
        float clamped = Mathf.Clamp(heightOffset, minHeightMeters, maxAbsHeightMeters);
        float poleBlend = Mathf.InverseLerp(0.985f, 0.9995f, Mathf.Abs(unitDirection.y));

        if (poleBlend <= 0f)
        {
            return clamped;
        }

        return Mathf.Lerp(clamped, 0f, poleBlend);
    }

    float ApplyLaunchSiteHeightFlatten(float heightOffset, Vector3 unitDirection)
    {
        float targetHeight = (float)LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters();
        float surfaceDistance = PlanetCubeSphere.SurfaceDistanceMeters(
            unitDirection,
            LaunchSiteDirection,
            PlanetRadius
        );

        if (surfaceDistance <= HardFlattenRadiusMeters)
        {
            return targetHeight;
        }

        if (surfaceDistance >= FlattenRadiusMeters)
        {
            return heightOffset;
        }

        float flattenBlend = Mathf.SmoothStep(
            FlattenRadiusMeters,
            HardFlattenRadiusMeters,
            surfaceDistance
        );
        return Mathf.Lerp(heightOffset, targetHeight, flattenBlend);
    }

    public Vector3 ApplyLaunchSiteFlatten(Vector3 surfacePoint, Vector3 unitDirection)
    {
        float surfaceDistance = PlanetCubeSphere.SurfaceDistanceMeters(
            unitDirection,
            LaunchSiteDirection,
            PlanetRadius
        );
        float targetRadius = PlanetRadius +
            (float)LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters();
        Vector3 flatPoint = unitDirection * targetRadius;

        if (surfaceDistance <= HardFlattenRadiusMeters)
        {
            return flatPoint;
        }

        if (surfaceDistance >= FlattenRadiusMeters)
        {
            return surfacePoint;
        }

        float flattenBlend = Mathf.SmoothStep(
            FlattenRadiusMeters,
            HardFlattenRadiusMeters,
            surfaceDistance
        );
        return Vector3.Lerp(surfacePoint, flatPoint, flattenBlend);
    }
}
