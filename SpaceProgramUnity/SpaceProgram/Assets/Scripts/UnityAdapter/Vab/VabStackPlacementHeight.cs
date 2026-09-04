using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSP-style vertical placement policy: free drag is XZ-only; Y changes only via node snap.
/// </summary>
static class VabStackPlacementHeight
{
    public static float ResolvePlacementPlaneY(
        bool hasStickyMatch,
        float stickyMatchWorldY,
        bool hasPlacementLatch,
        float placementLatchPlaneY,
        int placedPartCount,
        float workCenterY,
        float? emptyVesselHorizontalFaceY,
        float? focusPlaneY
    )
    {
        if (hasStickyMatch)
        {
            return stickyMatchWorldY;
        }

        if (hasPlacementLatch)
        {
            return placementLatchPlaneY;
        }

        if (focusPlaneY.HasValue)
        {
            return focusPlaneY.Value;
        }

        if (placedPartCount == 0 && emptyVesselHorizontalFaceY.HasValue)
        {
            return emptyVesselHorizontalFaceY.Value;
        }

        return workCenterY;
    }

    /// <summary>
    /// When a vessel already exists, lift the ghost to the nearest free stack node height
    /// so trigger overlap can engage without hunting Y on mesh faces.
    /// </summary>
    public static void RefineSpawnHeight(
        PartVisualAdapter ghostAdapter,
        Transform ghostTransform,
        Vector3 cursorPoint,
        IReadOnlyList<VabNodeSnapResolver.SnapTarget> snapTargets,
        Func<VabNodeSnapResolver.SnapTarget, int, bool> isNodeFree
    )
    {
        if (
            ghostAdapter == null ||
            ghostTransform == null ||
            snapTargets == null ||
            snapTargets.Count == 0 ||
            ghostAdapter.NodeCount == 0
        )
        {
            return;
        }

        float approachRadius = VabPartStackMetrics.ResolveStackApproachHorizontalMeters(
            ghostAdapter,
            ResolvePlacementTemplate(ghostAdapter)
        );
        float bestScore = float.MaxValue;
        Vector3 bestPosition = ghostTransform.position;
        Quaternion bestRotation = ghostTransform.rotation;
        bool found = false;

        Vector3 originalPosition = ghostTransform.position;
        Quaternion originalRotation = ghostTransform.rotation;

        for (int partIndex = 0; partIndex < snapTargets.Count; partIndex++)
        {
            VabNodeSnapResolver.SnapTarget parentTarget = snapTargets[partIndex];
            PartVisualAdapter parentAdapter = parentTarget.adapter;

            if (parentAdapter == null || parentAdapter.NodeCount == 0)
            {
                continue;
            }

            for (int parentNodeIndex = 0; parentNodeIndex < parentAdapter.NodeCount; parentNodeIndex++)
            {
                if (isNodeFree != null && !isNodeFree(parentTarget, parentNodeIndex))
                {
                    continue;
                }

                if (
                    !parentAdapter.TryGetNodeWorldPosition(parentNodeIndex, out Vector3 parentNodeWorld) ||
                    !parentAdapter.TryGetNodeWorldDirection(parentNodeIndex, out Vector3 parentNodeDirection)
                )
                {
                    continue;
                }

                if (!VabNodeSnapResolver.PassesCursorStackApproachGate(
                        cursorPoint,
                        parentNodeWorld,
                        approachRadius
                    ))
                {
                    continue;
                }

                if (
                    !VabNodeSnapResolver.TryFindComplementaryChildNodeIndex(
                        ghostAdapter,
                        parentNodeDirection,
                        out int childNodeIndex,
                        out Vector3 childNodeDirection
                    )
                )
                {
                    continue;
                }

                ghostTransform.SetPositionAndRotation(originalPosition, originalRotation);

                Vector3 probePosition = originalPosition;
                probePosition.x = cursorPoint.x;
                probePosition.z = cursorPoint.z;
                ghostTransform.position = probePosition;

                if (!ghostAdapter.TryGetNodeWorldPosition(childNodeIndex, out Vector3 childNodeWorld))
                {
                    continue;
                }

                float yDelta = parentNodeWorld.y - childNodeWorld.y;
                Vector3 hintPosition = ghostTransform.position;
                hintPosition.y += yDelta;

                float alignedDistanceSquared = (childNodeWorld - parentNodeWorld).sqrMagnitude;

                float score = VabNodeSnapResolver.ScoreCursorStackMatch(
                    cursorPoint,
                    parentNodeWorld,
                    null,
                    alignedDistanceSquared
                );

                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestPosition = hintPosition;
                bestRotation = originalRotation;
                found = true;
            }
        }

        ghostTransform.SetPositionAndRotation(originalPosition, originalRotation);

        if (found)
        {
            ghostTransform.SetPositionAndRotation(bestPosition, bestRotation);
        }
    }

    static PartTemplate ResolvePlacementTemplate(PartVisualAdapter ghostAdapter)
    {
        if (ghostAdapter == null || string.IsNullOrEmpty(ghostAdapter.PartTemplateId))
        {
            return null;
        }

        return PartDatabase.GetById(ghostAdapter.PartTemplateId);
    }
}
