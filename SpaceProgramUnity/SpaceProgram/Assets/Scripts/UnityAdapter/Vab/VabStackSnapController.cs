using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSP-style stack snap latch: when attached, pose comes only from node alignment math.
/// Free placement follows cursor on XZ; Y changes only through snap (no plane fighting).
/// </summary>
static class VabStackSnapController
{
    const int MaintainMissGraceFrames = 3;

    static int maintainMissFrames;

    public struct Result
    {
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public bool snapped;
        public VabNodeSnapResolver.NodeSnapMatch match;
    }

    public static Result ResolveStackPlacement(
        Transform ghostRoot,
        PartVisualAdapter ghostAdapter,
        Vector3 cursorOnPlane,
        IReadOnlyList<VabNodeSnapResolver.SnapTarget> snapTargets,
        LayerMask attachmentNodeMask,
        Func<VabNodeSnapResolver.SnapTarget, int, bool> isNodeFree,
        string heldBlockId,
        RaycastHit surfaceHit,
        bool hasSurfaceHit,
        Func<string, bool> isPlacedBlock,
        float acquireMeters,
        float cursorApproachMeters,
        float releaseMeters,
        float releaseHorizontalMeters,
        float releaseVerticalMeters,
        int symmetryCount,
        ref VabNodeSnapResolver.NodeSnapMatch stickyMatch,
        ref bool hasStickyMatch,
        Vector3 snapLatchCursor,
        bool hasSnapLatchCursor
    )
    {
        Result result = default;

        if (ghostRoot == null || ghostAdapter == null)
        {
            result.worldPosition = ghostRoot != null ? ghostRoot.position : Vector3.zero;
            result.worldRotation = ghostRoot != null ? ghostRoot.rotation : Quaternion.identity;
            return result;
        }

        if (
            hasStickyMatch &&
            TryMaintainLatch(
                ghostRoot,
                ghostAdapter,
                cursorOnPlane,
                snapTargets,
                releaseMeters,
                releaseHorizontalMeters,
                releaseVerticalMeters,
                snapLatchCursor,
                hasSnapLatchCursor,
                stickyMatch,
                out VabNodeSnapResolver.NodeSnapMatch latchedMatch
            )
        )
        {
            maintainMissFrames = 0;
            stickyMatch = latchedMatch;
            hasStickyMatch = true;
            ghostAdapter.TargetParentBlockId = latchedMatch.parentBlockId;
            ghostAdapter.TargetParentNodeIndex = latchedMatch.parentNodeIndex;
            ghostAdapter.TargetChildNodeIndex = latchedMatch.childNodeIndex;

            result.worldPosition = latchedMatch.worldPosition;
            result.worldRotation = latchedMatch.worldRotation;
            result.snapped = true;
            result.match = latchedMatch;
            return result;
        }

        if (hasStickyMatch)
        {
            maintainMissFrames++;

            if (
                maintainMissFrames < MaintainMissGraceFrames &&
                hasSnapLatchCursor &&
                !HasExceededSnapLatchRelease(
                    cursorOnPlane,
                    snapLatchCursor,
                    releaseHorizontalMeters,
                    releaseVerticalMeters
                ) &&
                VabNodeSnapResolver.TryRecomputeAlignedMatch(
                    ghostAdapter,
                    ghostRoot,
                    snapTargets,
                    stickyMatch,
                    out VabNodeSnapResolver.NodeSnapMatch graceMatch
                )
            )
            {
                ghostRoot.SetPositionAndRotation(graceMatch.worldPosition, graceMatch.worldRotation);
                stickyMatch = graceMatch;
                ghostAdapter.TargetParentBlockId = graceMatch.parentBlockId;
                ghostAdapter.TargetParentNodeIndex = graceMatch.parentNodeIndex;
                ghostAdapter.TargetChildNodeIndex = graceMatch.childNodeIndex;

                result.worldPosition = graceMatch.worldPosition;
                result.worldRotation = graceMatch.worldRotation;
                result.snapped = true;
                result.match = graceMatch;
                return result;
            }

            maintainMissFrames = 0;
            hasStickyMatch = false;
        }

        bool allowFreeDrag =
            !hasSnapLatchCursor ||
            HasExceededSnapLatchRelease(
                cursorOnPlane,
                snapLatchCursor,
                releaseHorizontalMeters,
                releaseVerticalMeters
            );

        if (!hasSnapLatchCursor)
        {
            VabStackPlacementHeight.RefineSpawnHeight(
                ghostAdapter,
                ghostRoot,
                cursorOnPlane,
                snapTargets,
                isNodeFree
            );
        }

        if (allowFreeDrag)
        {
            ApplyFreePlacement(ghostRoot, cursorOnPlane);
        }

        bool acquired = VabSnapSolverAdapter.ExecuteSnapResolution(
            ghostRoot,
            ghostAdapter,
            cursorOnPlane,
            snapTargets,
            isNodeFree,
            heldBlockId,
            surfaceHit,
            hasSurfaceHit,
            isPlacedBlock,
            symmetryCount,
            acquireMeters,
            cursorApproachMeters,
            ref stickyMatch,
            ref hasStickyMatch
        );

        if (acquired && hasStickyMatch)
        {
            maintainMissFrames = 0;
            result.worldPosition = ghostRoot.position;
            result.worldRotation = ghostRoot.rotation;
            result.snapped = true;
            result.match = stickyMatch;
            return result;
        }

        VabSnapSolverAdapter.ClearSnapTarget(ghostAdapter);
        result.worldPosition = ghostRoot.position;
        result.worldRotation = ghostRoot.rotation;
        result.snapped = false;
        return result;
    }

    static void ApplyFreePlacement(Transform ghostRoot, Vector3 cursorOnPlane)
    {
        Vector3 freePosition = ghostRoot.position;
        freePosition.x = cursorOnPlane.x;
        freePosition.z = cursorOnPlane.z;
        ghostRoot.position = freePosition;
    }

    static bool TryMaintainLatch(
        Transform ghostRoot,
        PartVisualAdapter ghostAdapter,
        Vector3 cursorOnPlane,
        IReadOnlyList<VabNodeSnapResolver.SnapTarget> snapTargets,
        float releaseMeters,
        float releaseHorizontalMeters,
        float releaseVerticalMeters,
        Vector3 snapLatchCursor,
        bool hasSnapLatchCursor,
        VabNodeSnapResolver.NodeSnapMatch stickyMatch,
        out VabNodeSnapResolver.NodeSnapMatch latchedMatch
    )
    {
        latchedMatch = default;

        if (
            hasSnapLatchCursor &&
            HasExceededSnapLatchRelease(
                cursorOnPlane,
                snapLatchCursor,
                releaseHorizontalMeters,
                releaseVerticalMeters
            )
        )
        {
            return false;
        }

        if (
            !hasSnapLatchCursor &&
            HasExceededSnapLatchRelease(
                cursorOnPlane,
                ghostRoot.position,
                releaseHorizontalMeters,
                releaseVerticalMeters
            )
        )
        {
            return false;
        }

        if (!VabNodeSnapResolver.TryRecomputeAlignedMatch(
                ghostAdapter,
                ghostRoot,
                snapTargets,
                stickyMatch,
                out latchedMatch
            ))
        {
            return false;
        }

        ghostRoot.SetPositionAndRotation(latchedMatch.worldPosition, latchedMatch.worldRotation);

        float releaseDistanceSquared = releaseMeters * releaseMeters;
        float stickyDistanceSquared = VabNodeSnapResolver.ComputeStickyAlignmentDistanceSquared(
            ghostAdapter,
            snapTargets,
            latchedMatch
        );

        return stickyDistanceSquared <= releaseDistanceSquared;
    }

    public static bool HasExceededSnapLatchRelease(
        Vector3 cursorOnPlane,
        Vector3 referencePoint,
        float releaseHorizontalMeters,
        float releaseVerticalMeters
    )
    {
        Vector3 delta = cursorOnPlane - referencePoint;
        float horizontalSquared = delta.x * delta.x + delta.z * delta.z;
        float horizontalReleaseSquared = releaseHorizontalMeters * releaseHorizontalMeters;

        if (horizontalSquared > horizontalReleaseSquared)
        {
            return true;
        }

        return Mathf.Abs(delta.y) > releaseVerticalMeters;
    }
}
