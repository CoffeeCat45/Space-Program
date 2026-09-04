using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSP-style ghost hold and stack snap: pivot follows the nearest stack node;
/// snapped pose ignores cursor XZ and uses trigger overlap + node alignment only.
/// </summary>
static class VabGhostMovementController
{
    public sealed class SnapAcquireSuppressState
    {
        public bool suppressed;
        public Vector3 origin;
    }

    const int MaintainMissGraceFrames = 3;
    const float VerticalNodeDotThreshold = 0.85f;
    const float VerticalIntentHysteresisMeters = 0.35f;
    const float VerticalFaceDotThreshold = 0.85f;

    static int maintainMissFrames;

    public struct Context
    {
        public Transform ghostRoot;
        public PartVisualAdapter ghostAdapter;
        public PartTemplate ghostTemplate;
        public Vector3 cursorWorld;
        public Vector3 verticalIntentWorld;
        public float verticalIntentReferenceY;
        public Vector3 vesselAxisWorld;
        public float stackReferenceY;
        public IReadOnlyList<VabNodeSnapResolver.SnapTarget> snapTargets;
        public LayerMask attachmentNodeMask;
        public Func<VabNodeSnapResolver.SnapTarget, int, bool> isNodeFree;
        public string heldBlockId;
        public Func<string, bool> isPlacedBlock;
        public float acquireMeters;
        public float previewMeters;
        public float releaseMeters;
        public float releaseHorizontalMeters;
        public float releaseVerticalMeters;
        public int lastActiveGhostNodeIndex;
        public bool hasVesselRaycastHit;
        public RaycastHit vesselHit;
        public int symmetryCount;
        public bool stabilizeVerticalHold;
        public float holdVerticalAnchorY;
        public float holdIntentAnchorY;
        public VabPlacementAttachMode attachMode;
        public bool strictNodeAttach;
        public float stackApproachMeters;
        public string preferredStackParentBlockId;
        public Func<VabNodeSnapResolver.NodeSnapMatch, IReadOnlyList<PlacedPart>> resolveSymmetricStackParents;
        public bool forceUnsnap;
        public SnapAcquireSuppressState snapAcquireSuppress;
    }

    public struct Result
    {
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public bool snapped;
        public bool isSurfaceAttach;
        public int activeGhostNodeIndex;
        public VabNodeSnapResolver.NodeSnapMatch match;
        public string surfaceParentBlockId;
    }

    public struct TriggerQueryResult
    {
        public bool found;
        public VabNodeSnapResolver.NodeSnapMatch match;
        public int ghostNodeIndex;
        public int parentNodeIndex;
        public string parentBlockId;
        public PartVisualAdapter parentAdapter;
        public Vector3 parentNodeWorld;
        public Vector3 parentNodeDirection;
    }

    public static Result HandleGhostMovement(
        Context context,
        ref VabNodeSnapResolver.NodeSnapMatch stickyMatch,
        ref bool hasStickyMatch,
        Vector3 snapLatchCursor,
        bool hasSnapLatchCursor
    )
    {
        Result result = default;

        if (context.ghostRoot == null || context.ghostAdapter == null)
        {
            result.worldPosition = context.ghostRoot != null ? context.ghostRoot.position : Vector3.zero;
            result.worldRotation = context.ghostRoot != null ? context.ghostRoot.rotation : Quaternion.identity;
            return result;
        }

        if (context.forceUnsnap && hasStickyMatch)
        {
            ArmSnapAcquireSuppress(
                context,
                hasSnapLatchCursor ? snapLatchCursor : context.cursorWorld
            );
            hasStickyMatch = false;
            maintainMissFrames = 0;
            context.ghostAdapter.ClearSnapTarget();
        }

        if (
            hasStickyMatch &&
            TryMaintainSnappedPose(
                context,
                stickyMatch,
                snapLatchCursor,
                hasSnapLatchCursor,
                out VabNodeSnapResolver.NodeSnapMatch latchedMatch
            )
        )
        {
            maintainMissFrames = 0;
            stickyMatch = latchedMatch;
            hasStickyMatch = true;
            ApplySnapTarget(context.ghostAdapter, latchedMatch);
            TryRefreshStackSnapSymmetry(context, latchedMatch, latchedMatch.childNodeIndex);
            result.worldPosition = latchedMatch.worldPosition;
            result.worldRotation = latchedMatch.worldRotation;
            result.snapped = true;
            result.match = latchedMatch;
            result.activeGhostNodeIndex = latchedMatch.childNodeIndex;
            return result;
        }

        if (hasStickyMatch)
        {
            maintainMissFrames++;

            if (
                maintainMissFrames < MaintainMissGraceFrames &&
                hasSnapLatchCursor &&
                !HasExceededSnapRelease(
                    context.cursorWorld,
                    context.verticalIntentWorld,
                    snapLatchCursor,
                    context.releaseHorizontalMeters,
                    context.releaseVerticalMeters
                ) &&
                VabNodeSnapResolver.TryRecomputeAlignedMatch(
                    context.ghostAdapter,
                    context.ghostRoot,
                    context.snapTargets,
                    stickyMatch,
                    out VabNodeSnapResolver.NodeSnapMatch graceMatch
                )
            )
            {
                stickyMatch = graceMatch;
                ApplySnapTarget(context.ghostAdapter, graceMatch);
                result.worldPosition = graceMatch.worldPosition;
                result.worldRotation = graceMatch.worldRotation;
                result.snapped = true;
                result.match = graceMatch;
                result.activeGhostNodeIndex = graceMatch.childNodeIndex;
                return result;
            }

            ArmSnapAcquireSuppress(
                context,
                hasSnapLatchCursor ? snapLatchCursor : context.cursorWorld
            );

            maintainMissFrames = 0;
            hasStickyMatch = false;
            context.ghostAdapter.ClearSnapTarget();
        }

        int activeGhostNodeIndex = ResolveActiveGhostNodeIndex(
            context.ghostAdapter,
            context.verticalIntentWorld,
            context.verticalIntentReferenceY,
            context.lastActiveGhostNodeIndex,
            out int nextActiveGhostNodeIndex
        );

        string preferredStackParentBlockId = string.Empty;

        if (TryResolveMeshHoverHoldNodeIndex(context, out int hoverHoldNodeIndex, out string hoverParentBlockId))
        {
            activeGhostNodeIndex = hoverHoldNodeIndex;
            nextActiveGhostNodeIndex = hoverHoldNodeIndex;
            preferredStackParentBlockId = hoverParentBlockId;
        }

        ApplyActiveNodeHold(
            context.ghostRoot,
            context.ghostAdapter,
            activeGhostNodeIndex,
            context.cursorWorld,
            context.verticalIntentWorld,
            context.stabilizeVerticalHold,
            context.holdVerticalAnchorY,
            context.holdIntentAnchorY
        );

        bool allowStackAttach = AllowsStackAttach(context);
        bool allowSurfaceAttach = AllowsSurfaceAttach(context);

        if (allowStackAttach)
        {
            VabStackPlacementHeight.RefineSpawnHeight(
                context.ghostAdapter,
                context.ghostRoot,
                context.cursorWorld,
                context.snapTargets,
                context.isNodeFree
            );
        }

        bool snapAcquireSuppressed = IsSnapAcquireSuppressed(context);

        if (
            allowStackAttach &&
            !snapAcquireSuppressed &&
            QueryAttachmentNodeTriggers(
                context,
                activeGhostNodeIndex,
                preferredStackParentBlockId,
                out TriggerQueryResult triggerQuery
            )
        )
        {
            ApplyStackSnapResult(context, triggerQuery.match, triggerQuery.ghostNodeIndex, ref stickyMatch, ref hasStickyMatch, ref result);
            return result;
        }

        float previewMeters = context.previewMeters > 0f ?
            context.previewMeters :
            VabSnapSolverAdapter.SnapPreviewMeters;

        if (
            allowStackAttach &&
            !snapAcquireSuppressed &&
            previewMeters > context.acquireMeters &&
            TryFindStackSnapCandidate(
                context,
                activeGhostNodeIndex,
                preferredStackParentBlockId,
                context.acquireMeters,
                previewMeters,
                out TriggerQueryResult softQuery
            )
        )
        {
            VabSnapSolverAdapter.ApplySoftSnapTowardAlignedPose(
                context.ghostRoot,
                softQuery.match.worldPosition,
                softQuery.match.worldRotation
            );
            ApplySnapTarget(context.ghostAdapter, softQuery.match);
            TryRefreshStackSnapSymmetry(context, softQuery.match, softQuery.ghostNodeIndex);

            result.worldPosition = context.ghostRoot.position;
            result.worldRotation = context.ghostRoot.rotation;
            result.snapped = false;
            result.activeGhostNodeIndex = softQuery.ghostNodeIndex;
            return result;
        }

        VabSnapSolverAdapter.ClearSymmetryGhosts();

        if (allowSurfaceAttach && TryResolveSurfaceAttach(context, out string surfaceParentBlockId))
        {
            stickyMatch = default;
            hasStickyMatch = false;
            result.worldPosition = context.ghostRoot.position;
            result.worldRotation = context.ghostRoot.rotation;
            result.snapped = true;
            result.isSurfaceAttach = true;
            result.surfaceParentBlockId = surfaceParentBlockId;
            result.activeGhostNodeIndex = activeGhostNodeIndex;
            return result;
        }

        context.ghostAdapter.ClearSnapTarget();
        result.worldPosition = context.ghostRoot.position;
        result.worldRotation = context.ghostRoot.rotation;
        result.snapped = false;
        result.activeGhostNodeIndex = nextActiveGhostNodeIndex;
        return result;
    }

    public static bool QueryAttachmentNodeTriggers(
        Context context,
        int preferredGhostNodeIndex,
        string preferredStackParentBlockId,
        out TriggerQueryResult result
    )
    {
        return TryFindStackSnapCandidate(
            context,
            preferredGhostNodeIndex,
            preferredStackParentBlockId,
            0f,
            context.acquireMeters,
            out result
        );
    }

    static bool AllowsStackAttach(Context context)
    {
        if (context.strictNodeAttach)
        {
            return true;
        }

        return context.attachMode != VabPlacementAttachMode.SurfaceOnly;
    }

    static bool AllowsSurfaceAttach(Context context)
    {
        if (context.strictNodeAttach)
        {
            return false;
        }

        return context.attachMode != VabPlacementAttachMode.StackOnly;
    }

    static float ResolveStackApproachMeters(Context context)
    {
        if (context.stackApproachMeters > 0f)
        {
            return context.stackApproachMeters;
        }

        return VabPartStackMetrics.ResolveStackApproachHorizontalMeters(
            context.ghostAdapter,
            context.ghostTemplate
        );
    }

    static bool TryFindStackSnapCandidate(
        Context context,
        int preferredGhostNodeIndex,
        string preferredStackParentBlockId,
        float minDistanceExclusive,
        float maxDistanceInclusive,
        out TriggerQueryResult result
    )
    {
        result = default;

        if (
            context.ghostAdapter == null ||
            context.ghostRoot == null ||
            context.snapTargets == null ||
            context.snapTargets.Count == 0 ||
            context.ghostAdapter.NodeCount == 0 ||
            maxDistanceInclusive <= 0f
        )
        {
            return false;
        }

        bool wantAttachUnder = ResolveWantAttachUnderForSnap(
            context,
            preferredGhostNodeIndex
        );

        float stackApproachMeters = ResolveStackApproachMeters(context);
        string preferredParentBlockId = !string.IsNullOrEmpty(preferredStackParentBlockId) ?
            preferredStackParentBlockId :
            context.preferredStackParentBlockId;
        Vector3? meshHitPoint = context.hasVesselRaycastHit ?
            context.vesselHit.point :
            (Vector3?)null;

        float bestScore = float.MaxValue;
        bool found = false;

        Vector3 originalPosition = context.ghostRoot.position;
        Quaternion originalRotation = context.ghostRoot.rotation;

        for (int scanOrder = 0; scanOrder < context.ghostAdapter.NodeCount + 1; scanOrder++)
        {
            int childNodeIndex = scanOrder == 0 ?
                preferredGhostNodeIndex :
                scanOrder - 1;

            if (scanOrder > 0 && childNodeIndex == preferredGhostNodeIndex)
            {
                continue;
            }

            if (childNodeIndex < 0 || childNodeIndex >= context.ghostAdapter.NodeCount)
            {
                continue;
            }

            if (
                !TryGetStackNodeKind(context.ghostAdapter, childNodeIndex, out bool childIsTop, out bool childIsBottom) ||
                (!childIsTop && !childIsBottom)
            )
            {
                continue;
            }

            if (wantAttachUnder && !childIsTop)
            {
                continue;
            }

            if (!wantAttachUnder && !childIsBottom)
            {
                continue;
            }

            if (
                !context.ghostAdapter.TryGetNodeWorldDirection(childNodeIndex, out Vector3 childNodeDirection)
            )
            {
                continue;
            }

            for (int targetIndex = 0; targetIndex < context.snapTargets.Count; targetIndex++)
            {
                VabNodeSnapResolver.SnapTarget parentTarget = context.snapTargets[targetIndex];
                PartVisualAdapter parentAdapter = parentTarget.adapter;

                if (
                    parentAdapter == null ||
                    parentAdapter.NodeCount == 0 ||
                    string.IsNullOrEmpty(parentTarget.blockId) ||
                    parentTarget.blockId == context.heldBlockId ||
                    (context.isPlacedBlock != null && !context.isPlacedBlock(parentTarget.blockId))
                )
                {
                    continue;
                }

                for (int parentNodeIndex = 0; parentNodeIndex < parentAdapter.NodeCount; parentNodeIndex++)
                {
                    if (
                        !parentAdapter.TryGetNodeWorldPosition(parentNodeIndex, out Vector3 parentNodeWorld) ||
                        !parentAdapter.TryGetNodeWorldDirection(parentNodeIndex, out Vector3 parentNodeDirection)
                    )
                    {
                        continue;
                    }

                    if (!AreNodesStackCompatible(parentNodeDirection, childNodeDirection))
                    {
                        continue;
                    }

                    if (
                        !TryGetStackNodeKind(parentAdapter, parentNodeIndex, out bool parentIsTop, out bool parentIsBottom) ||
                        (!parentIsTop && !parentIsBottom)
                    )
                    {
                        continue;
                    }

                    if (wantAttachUnder && !parentIsBottom)
                    {
                        continue;
                    }

                    if (!wantAttachUnder && !parentIsTop)
                    {
                        continue;
                    }

                    if (context.isNodeFree != null && !context.isNodeFree(parentTarget, parentNodeIndex))
                    {
                        continue;
                    }

                    if (
                        !VabNodeSnapResolver.PassesCursorStackApproachGate(
                            context.cursorWorld,
                            parentNodeWorld,
                            stackApproachMeters
                        )
                    )
                    {
                        continue;
                    }

                    context.ghostRoot.SetPositionAndRotation(originalPosition, originalRotation);

                    Vector3 probePosition = originalPosition;
                    probePosition.x = context.cursorWorld.x;
                    probePosition.z = context.cursorWorld.z;
                    context.ghostRoot.position = probePosition;

                    if (context.ghostAdapter.TryGetNodeWorldPosition(childNodeIndex, out Vector3 childAtProbe))
                    {
                        probePosition.y += parentNodeWorld.y - childAtProbe.y;
                        context.ghostRoot.position = probePosition;
                    }

                    if (
                        !VabNodeSnapResolver.TryComputeAlignedPose(
                            context.ghostRoot,
                            context.ghostAdapter,
                            childNodeIndex,
                            childNodeDirection,
                            parentNodeWorld,
                            parentNodeDirection,
                            out Vector3 alignedPosition,
                            out Quaternion alignedRotation
                        )
                    )
                    {
                        continue;
                    }

                    context.ghostRoot.SetPositionAndRotation(alignedPosition, alignedRotation);

                    if (
                        !context.ghostAdapter.TryGetNodeWorldPosition(childNodeIndex, out Vector3 alignedChildWorld) ||
                        !parentAdapter.TryGetNodeWorldPosition(parentNodeIndex, out Vector3 alignedParentWorld)
                    )
                    {
                        continue;
                    }

                    float alignedDistanceSquared = (alignedChildWorld - alignedParentWorld).sqrMagnitude;

                    if (!IsNodeDistanceInSnapBand(alignedDistanceSquared, minDistanceExclusive, maxDistanceInclusive))
                    {
                        continue;
                    }

                    float matchScore = VabNodeSnapResolver.ScoreCursorStackMatch(
                        context.cursorWorld,
                        parentNodeWorld,
                        meshHitPoint,
                        alignedDistanceSquared
                    );

                    if (childNodeIndex == preferredGhostNodeIndex)
                    {
                        matchScore *= 0.5f;
                    }

                    if (
                        !string.IsNullOrEmpty(preferredParentBlockId) &&
                        parentTarget.blockId == preferredParentBlockId
                    )
                    {
                        matchScore *= 0.25f;
                    }

                    if (matchScore >= bestScore)
                    {
                        continue;
                    }

                    bestScore = matchScore;
                    found = true;
                    result.found = true;
                    result.ghostNodeIndex = childNodeIndex;
                    result.parentNodeIndex = parentNodeIndex;
                    result.parentBlockId = parentTarget.blockId;
                    result.parentAdapter = parentAdapter;
                    result.parentNodeWorld = parentNodeWorld;
                    result.parentNodeDirection = parentNodeDirection;
                    result.match = new VabNodeSnapResolver.NodeSnapMatch
                    {
                        parentBlockId = parentTarget.blockId,
                        parentNodeIndex = parentNodeIndex,
                        childNodeIndex = childNodeIndex,
                        worldPosition = alignedPosition,
                        worldRotation = alignedRotation,
                        distanceSquared = alignedDistanceSquared,
                    };
                }
            }
        }

        if (!found)
        {
            context.ghostRoot.SetPositionAndRotation(originalPosition, originalRotation);
        }

        return found;
    }

    static bool IsNodeDistanceInSnapBand(
        float distanceSquared,
        float minDistanceExclusive,
        float maxDistanceInclusive
    )
    {
        float maxDistanceSquared = maxDistanceInclusive * maxDistanceInclusive;

        if (minDistanceExclusive <= 0f)
        {
            return distanceSquared <= maxDistanceSquared;
        }

        float minDistanceSquared = minDistanceExclusive * minDistanceExclusive;
        return distanceSquared > minDistanceSquared && distanceSquared <= maxDistanceSquared;
    }

    static bool ResolveWantAttachUnderForSnap(Context context, int preferredGhostNodeIndex)
    {
        if (
            TryGetStackNodeKind(context.ghostAdapter, preferredGhostNodeIndex, out bool preferredIsTop, out _) &&
            preferredIsTop
        )
        {
            return true;
        }

        if (
            TryGetStackNodeKind(context.ghostAdapter, preferredGhostNodeIndex, out _, out bool preferredIsBottom) &&
            preferredIsBottom
        )
        {
            return false;
        }

        return ResolveWantAttachUnder(
            context.verticalIntentWorld.y,
            context.verticalIntentReferenceY,
            context.lastActiveGhostNodeIndex,
            context.ghostAdapter
        );
    }

    public static bool ResolvePlacementCursorPoint(
        Ray mouseRay,
        Camera camera,
        Vector3 focusPoint,
        Vector3 vesselAxisWorld,
        float stablePlaneY,
        bool rayHitsVessel,
        RaycastHit vesselHit,
        bool useVesselAxisFocusPlane,
        out Vector3 cursorPoint
    )
    {
        cursorPoint = default;

        if (rayHitsVessel)
        {
            cursorPoint = vesselHit.point;
            return true;
        }

        if (useVesselAxisFocusPlane && camera != null)
        {
            Vector3 planeAnchor = vesselAxisWorld;
            planeAnchor.y = focusPoint.y;
            Plane slicePlane = new Plane(camera.transform.forward, planeAnchor);

            if (slicePlane.Raycast(mouseRay, out float distance) && distance >= 0f)
            {
                cursorPoint = mouseRay.GetPoint(distance);
                return true;
            }
        }

        if (camera != null && VabFocusPlane.TryRaycast(camera, focusPoint, mouseRay, out Vector3 focusHit))
        {
            cursorPoint = focusHit;
            return true;
        }

        return VabPlacementPlane.TryRaycastHorizontal(mouseRay, stablePlaneY, out cursorPoint);
    }

    public static Vector3 ResolveCursorPointWithVesselAxisFallback(
        Ray mouseRay,
        Camera camera,
        Vector3 focusPoint,
        float planeY,
        Vector3 vesselAxisWorld,
        bool useVesselAxisFallback
    )
    {
        if (VabPlacementPlane.TryRaycastHorizontal(mouseRay, planeY, out Vector3 point))
        {
            return point;
        }

        if (camera != null && VabFocusPlane.TryRaycast(camera, focusPoint, mouseRay, out Vector3 focusHit))
        {
            if (useVesselAxisFallback)
            {
                focusHit.x = vesselAxisWorld.x;
                focusHit.z = vesselAxisWorld.z;
            }

            return focusHit;
        }

        Vector3 fallback = focusPoint;
        fallback.y = planeY;

        if (useVesselAxisFallback)
        {
            fallback.x = vesselAxisWorld.x;
            fallback.z = vesselAxisWorld.z;
        }

        return fallback;
    }

    public static bool HasExceededVerticalRelease(
        Vector3 cursorWorld,
        Vector3 referencePoint,
        float releaseVerticalMeters
    )
    {
        return Mathf.Abs(cursorWorld.y - referencePoint.y) > releaseVerticalMeters;
    }

    public static bool HasExceededSnapRelease(
        Vector3 cursorWorld,
        Vector3 referencePoint,
        float releaseHorizontalMeters,
        float releaseVerticalMeters
    )
    {
        return VabStackSnapController.HasExceededSnapLatchRelease(
            cursorWorld,
            referencePoint,
            releaseHorizontalMeters,
            releaseVerticalMeters
        );
    }

    public static bool HasExceededSnapRelease(
        Vector3 cursorWorld,
        Vector3 verticalIntentWorld,
        Vector3 referencePoint,
        float releaseHorizontalMeters,
        float releaseVerticalMeters
    )
    {
        return HasExceededSnapRelease(
                   cursorWorld,
                   referencePoint,
                   releaseHorizontalMeters,
                   releaseVerticalMeters
               ) ||
               HasExceededSnapRelease(
                   verticalIntentWorld,
                   referencePoint,
                   releaseHorizontalMeters,
                   releaseVerticalMeters
               );
    }

    public static void UpdateSnapAcquireSuppress(
        SnapAcquireSuppressState state,
        Vector3 cursorWorld,
        Vector3 verticalIntentWorld,
        float releaseMeters
    )
    {
        if (state == null || !state.suppressed)
        {
            return;
        }

        if (
            HasExceededSnapRelease(
                cursorWorld,
                verticalIntentWorld,
                state.origin,
                releaseMeters,
                releaseMeters
            )
        )
        {
            state.suppressed = false;
        }
    }

    static bool IsSnapAcquireSuppressed(Context context)
    {
        return context.snapAcquireSuppress != null && context.snapAcquireSuppress.suppressed;
    }

    static void ArmSnapAcquireSuppress(Context context, Vector3 origin)
    {
        if (context.snapAcquireSuppress == null)
        {
            return;
        }

        context.snapAcquireSuppress.suppressed = true;
        context.snapAcquireSuppress.origin = origin;
    }

    public static float ResolveVerticalIntentReferenceY(
        Vector3 cursorWorld,
        IReadOnlyList<VabNodeSnapResolver.SnapTarget> snapTargets,
        float fallbackY,
        Vector3? meshHitPoint = null
    )
    {
        if (meshHitPoint.HasValue)
        {
            return meshHitPoint.Value.y;
        }

        if (snapTargets == null || snapTargets.Count == 0)
        {
            return fallbackY;
        }

        float bestHorizontalSquared = float.MaxValue;
        float bestY = fallbackY;
        bool found = false;

        for (int partIndex = 0; partIndex < snapTargets.Count; partIndex++)
        {
            VabNodeSnapResolver.SnapTarget parentTarget = snapTargets[partIndex];
            PartVisualAdapter parentAdapter = parentTarget.adapter;

            if (parentAdapter == null)
            {
                continue;
            }

            for (int nodeIndex = 0; nodeIndex < parentAdapter.NodeCount; nodeIndex++)
            {
                if (
                    !parentAdapter.TryGetNodeWorldPosition(nodeIndex, out Vector3 nodeWorld) ||
                    !TryGetStackNodeKind(parentAdapter, nodeIndex, out bool isTop, out bool isBottom) ||
                    (!isTop && !isBottom)
                )
                {
                    continue;
                }

                Vector3 flatDelta = cursorWorld - nodeWorld;
                flatDelta.y = 0f;
                float horizontalSquared = flatDelta.sqrMagnitude;

                if (horizontalSquared >= bestHorizontalSquared)
                {
                    continue;
                }

                bestHorizontalSquared = horizontalSquared;
                bestY = nodeWorld.y;
                found = true;
            }
        }

        return found ? bestY : fallbackY;
    }

    static bool TryMaintainSnappedPose(
        Context context,
        VabNodeSnapResolver.NodeSnapMatch stickyMatch,
        Vector3 snapLatchCursor,
        bool hasSnapLatchCursor,
        out VabNodeSnapResolver.NodeSnapMatch latchedMatch
    )
    {
        latchedMatch = default;

        if (
            hasSnapLatchCursor &&
            HasExceededSnapRelease(
                context.cursorWorld,
                context.verticalIntentWorld,
                snapLatchCursor,
                context.releaseHorizontalMeters,
                context.releaseVerticalMeters
            )
        )
        {
            return false;
        }

        if (
            !VabNodeSnapResolver.TryRecomputeAlignedMatch(
                context.ghostAdapter,
                context.ghostRoot,
                context.snapTargets,
                stickyMatch,
                out latchedMatch
            )
        )
        {
            return false;
        }

        context.ghostRoot.SetPositionAndRotation(latchedMatch.worldPosition, latchedMatch.worldRotation);
        LockSnappedPoseToParentAxis(context, ref latchedMatch);

        float releaseDistanceSquared = context.releaseMeters * context.releaseMeters;
        float stickyDistanceSquared = VabNodeSnapResolver.ComputeStickyAlignmentDistanceSquared(
            context.ghostAdapter,
            context.snapTargets,
            latchedMatch
        );

        return stickyDistanceSquared <= releaseDistanceSquared;
    }

    static void ApplySnapTarget(
        PartVisualAdapter ghostAdapter,
        VabNodeSnapResolver.NodeSnapMatch match
    )
    {
        ghostAdapter.TargetParentBlockId = match.parentBlockId;
        ghostAdapter.TargetParentNodeIndex = match.parentNodeIndex;
        ghostAdapter.TargetChildNodeIndex = match.childNodeIndex;
    }

    static int ResolveActiveGhostNodeIndex(
        PartVisualAdapter ghostAdapter,
        Vector3 verticalIntentWorld,
        float verticalIntentReferenceY,
        int lastActiveGhostNodeIndex,
        out int resolvedActiveGhostNodeIndex
    )
    {
        resolvedActiveGhostNodeIndex = lastActiveGhostNodeIndex;

        if (ghostAdapter == null || ghostAdapter.NodeCount == 0)
        {
            resolvedActiveGhostNodeIndex = 0;
            return 0;
        }

        if (!TryFindStackTopBottomIndices(ghostAdapter, out int topIndex, out int bottomIndex))
        {
            resolvedActiveGhostNodeIndex = ResolveClosestNodeIndex(ghostAdapter, verticalIntentWorld);
            return resolvedActiveGhostNodeIndex;
        }

        bool wantAttachUnder = ResolveWantAttachUnder(
            verticalIntentWorld.y,
            verticalIntentReferenceY,
            lastActiveGhostNodeIndex,
            ghostAdapter,
            topIndex,
            bottomIndex
        );

        resolvedActiveGhostNodeIndex = wantAttachUnder ? topIndex : bottomIndex;
        return resolvedActiveGhostNodeIndex;
    }

    static bool ResolveWantAttachUnder(
        float intentY,
        float referenceY,
        int lastActiveGhostNodeIndex,
        PartVisualAdapter ghostAdapter,
        int topIndex = -1,
        int bottomIndex = -1
    )
    {
        if (topIndex < 0 || bottomIndex < 0)
        {
            if (!TryFindStackTopBottomIndices(ghostAdapter, out topIndex, out bottomIndex))
            {
                return intentY < referenceY;
            }
        }

        if (lastActiveGhostNodeIndex == topIndex &&
            intentY <= referenceY + VerticalIntentHysteresisMeters)
        {
            return true;
        }

        if (lastActiveGhostNodeIndex == bottomIndex &&
            intentY > referenceY - VerticalIntentHysteresisMeters)
        {
            return false;
        }

        return intentY <= referenceY;
    }

    static int ResolveClosestNodeIndex(PartVisualAdapter ghostAdapter, Vector3 cursorWorld)
    {
        int bestIndex = 0;
        float bestDistanceSquared = float.MaxValue;

        for (int nodeIndex = 0; nodeIndex < ghostAdapter.NodeCount; nodeIndex++)
        {
            if (!ghostAdapter.TryGetNodeWorldPosition(nodeIndex, out Vector3 nodeWorld))
            {
                continue;
            }

            float distanceSquared = (nodeWorld - cursorWorld).sqrMagnitude;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestIndex = nodeIndex;
            }
        }

        return bestIndex;
    }

    static void ApplyActiveNodeHold(
        Transform ghostRoot,
        PartVisualAdapter ghostAdapter,
        int activeNodeIndex,
        Vector3 cursorWorld,
        Vector3 verticalIntentWorld,
        bool stabilizeVerticalHold,
        float holdVerticalAnchorY,
        float holdIntentAnchorY
    )
    {
        if (ghostRoot == null)
        {
            return;
        }

        float holdY = ResolveHoldTargetY(
            verticalIntentWorld.y,
            stabilizeVerticalHold,
            holdVerticalAnchorY,
            holdIntentAnchorY
        );

        if (
            ghostAdapter == null ||
            !ghostAdapter.TryGetNodeLocalPosition(activeNodeIndex, out Vector3 activeNodeLocal)
        )
        {
            Vector3 freePosition = ghostRoot.position;
            freePosition.x = cursorWorld.x;
            freePosition.z = cursorWorld.z;
            freePosition.y = holdY;
            ghostRoot.position = freePosition;
            return;
        }

        Vector3 activeNodeOffsetWorld = ghostRoot.rotation * activeNodeLocal;
        Vector3 holdTarget = cursorWorld;
        holdTarget.y = holdY;

        ghostRoot.position = holdTarget - activeNodeOffsetWorld;
    }

    static float ResolveHoldTargetY(
        float verticalIntentY,
        bool stabilizeVerticalHold,
        float holdVerticalAnchorY,
        float holdIntentAnchorY
    )
    {
        if (!stabilizeVerticalHold)
        {
            return verticalIntentY;
        }

        return holdVerticalAnchorY + (verticalIntentY - holdIntentAnchorY);
    }

    static void ApplyStackSnapResult(
        Context context,
        VabNodeSnapResolver.NodeSnapMatch match,
        int activeGhostNodeIndex,
        ref VabNodeSnapResolver.NodeSnapMatch stickyMatch,
        ref bool hasStickyMatch,
        ref Result result
    )
    {
        LockSnappedPoseToParentAxis(context, ref match);
        context.ghostRoot.SetPositionAndRotation(match.worldPosition, match.worldRotation);

        stickyMatch = match;
        hasStickyMatch = true;
        maintainMissFrames = 0;
        ApplySnapTarget(context.ghostAdapter, match);
        TryRefreshStackSnapSymmetry(context, match, activeGhostNodeIndex);

        result.worldPosition = match.worldPosition;
        result.worldRotation = match.worldRotation;
        result.snapped = true;
        result.isSurfaceAttach = false;
        result.match = match;
        result.activeGhostNodeIndex = activeGhostNodeIndex;
    }

    static void TryRefreshStackSnapSymmetry(
        Context context,
        VabNodeSnapResolver.NodeSnapMatch match,
        int childNodeIndex
    )
    {
        if (
            context.symmetryCount <= 1 ||
            context.resolveSymmetricStackParents == null ||
            context.ghostRoot == null ||
            context.ghostAdapter == null
        )
        {
            return;
        }

        IReadOnlyList<PlacedPart> symmetricParents = context.resolveSymmetricStackParents(match);

        if (symmetricParents == null || symmetricParents.Count <= 1)
        {
            VabSnapSolverAdapter.ClearSymmetryGhosts();
            return;
        }

        VabSnapSolverAdapter.GenerateStackSnapSymmetry(
            context.ghostRoot.gameObject,
            context.ghostAdapter,
            match,
            childNodeIndex,
            symmetricParents,
            context.symmetryCount
        );
    }

    static void LockSnappedPoseToParentAxis(Context context, ref VabNodeSnapResolver.NodeSnapMatch match)
    {
        if (context.snapTargets == null)
        {
            return;
        }

        for (int i = 0; i < context.snapTargets.Count; i++)
        {
            VabNodeSnapResolver.SnapTarget parentTarget = context.snapTargets[i];

            if (parentTarget.blockId != match.parentBlockId || parentTarget.adapter == null)
            {
                continue;
            }

            if (!parentTarget.adapter.TryGetNodeWorldPosition(match.parentNodeIndex, out Vector3 parentNodeWorld))
            {
                return;
            }

            if (
                !VabNodeSnapResolver.TryRecomputeAlignedMatch(
                    context.ghostAdapter,
                    context.ghostRoot,
                    context.snapTargets,
                    match,
                    out VabNodeSnapResolver.NodeSnapMatch realignedMatch
                )
            )
            {
                return;
            }

            context.ghostRoot.SetPositionAndRotation(realignedMatch.worldPosition, realignedMatch.worldRotation);

            if (!context.ghostAdapter.TryGetNodeWorldPosition(match.childNodeIndex, out Vector3 childWorld))
            {
                return;
            }

            Vector3 lockedPosition = context.ghostRoot.position + (parentNodeWorld - childWorld);
            lockedPosition.x = parentNodeWorld.x;
            lockedPosition.z = parentNodeWorld.z;
            context.ghostRoot.SetPositionAndRotation(lockedPosition, realignedMatch.worldRotation);

            match = realignedMatch;
            match.worldPosition = lockedPosition;
            match.worldRotation = realignedMatch.worldRotation;
            return;
        }
    }

    static bool TryResolveSurfaceAttach(Context context, out string parentBlockId)
    {
        parentBlockId = string.Empty;

        if (!context.hasVesselRaycastHit)
        {
            return false;
        }

        RaycastHit hit = context.vesselHit;

        if (Mathf.Abs(Vector3.Dot(hit.normal.normalized, Vector3.up)) > VerticalFaceDotThreshold)
        {
            return false;
        }

        return VabSnapSolverAdapter.TryApplySurfaceSnap(
            context.ghostRoot.gameObject,
            context.ghostAdapter,
            context.heldBlockId,
            hit,
            context.isPlacedBlock,
            context.symmetryCount,
            out parentBlockId
        );
    }

    static bool TryResolveMeshHoverHoldNodeIndex(
        Context context,
        out int holdNodeIndex,
        out string parentBlockId
    )
    {
        holdNodeIndex = -1;
        parentBlockId = string.Empty;

        if (
            !context.hasVesselRaycastHit ||
            context.vesselHit.collider == null ||
            context.ghostAdapter == null ||
            context.isPlacedBlock == null
        )
        {
            return false;
        }

        PartVisualAdapter parentAdapter =
            context.vesselHit.collider.GetComponentInParent<PartVisualAdapter>();

        if (
            parentAdapter == null ||
            string.IsNullOrEmpty(parentAdapter.BlockId) ||
            parentAdapter.BlockId == context.heldBlockId ||
            !context.isPlacedBlock(parentAdapter.BlockId)
        )
        {
            return false;
        }

        if (!TryFindStackTopBottomIndices(context.ghostAdapter, out int topIndex, out int bottomIndex))
        {
            return false;
        }

        Vector3 localHit = parentAdapter.transform.InverseTransformPoint(context.vesselHit.point);
        holdNodeIndex = localHit.y < 0f ? topIndex : bottomIndex;
        parentBlockId = parentAdapter.BlockId;
        return true;
    }

    public static bool TryFindStackTopBottomIndices(
        PartVisualAdapter adapter,
        out int topIndex,
        out int bottomIndex
    )
    {
        topIndex = -1;
        bottomIndex = -1;

        if (adapter == null)
        {
            return false;
        }

        for (int nodeIndex = 0; nodeIndex < adapter.NodeCount; nodeIndex++)
        {
            if (!adapter.TryGetNodeTransform(nodeIndex, out Transform nodeTransform))
            {
                continue;
            }

            string nodeName = nodeTransform.name;

            if (nodeName == PartPrefabBuilder.NodeTopName)
            {
                topIndex = nodeIndex;
            }
            else if (nodeName == PartPrefabBuilder.NodeBottomName)
            {
                bottomIndex = nodeIndex;
            }
        }

        if (topIndex >= 0 && bottomIndex >= 0)
        {
            return true;
        }

        for (int nodeIndex = 0; nodeIndex < adapter.NodeCount; nodeIndex++)
        {
            if (!adapter.TryGetNodeWorldDirection(nodeIndex, out Vector3 direction))
            {
                continue;
            }

            if (direction.y > VerticalNodeDotThreshold)
            {
                topIndex = nodeIndex;
            }
            else if (direction.y < -VerticalNodeDotThreshold)
            {
                bottomIndex = nodeIndex;
            }
        }

        return topIndex >= 0 && bottomIndex >= 0;
    }

    static bool TryGetStackNodeKind(
        PartVisualAdapter adapter,
        int nodeIndex,
        out bool isTop,
        out bool isBottom
    )
    {
        isTop = false;
        isBottom = false;

        if (adapter == null || !adapter.TryGetNodeTransform(nodeIndex, out Transform nodeTransform))
        {
            return false;
        }

        if (nodeTransform.name == PartPrefabBuilder.NodeTopName)
        {
            isTop = true;
            return true;
        }

        if (nodeTransform.name == PartPrefabBuilder.NodeBottomName)
        {
            isBottom = true;
            return true;
        }

        if (!adapter.TryGetNodeWorldDirection(nodeIndex, out Vector3 direction))
        {
            return false;
        }

        isTop = direction.y > VerticalNodeDotThreshold;
        isBottom = direction.y < -VerticalNodeDotThreshold;
        return isTop || isBottom;
    }

    static bool AreNodesStackCompatible(Vector3 parentNodeDirection, Vector3 childNodeDirection)
    {
        return Vector3.Dot(parentNodeDirection.normalized, childNodeDirection.normalized) < -VerticalNodeDotThreshold;
    }
}
