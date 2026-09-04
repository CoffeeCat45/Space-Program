using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSP-style stack snap: OverlapSphere at ghost node probes AttachmentNode trigger spheres.
/// </summary>
static class VabAttachmentSnapResolver
{
    public static bool TryFindSnapWithHysteresis(
        PartVisualAdapter ghostAdapter,
        Transform ghostRoot,
        IReadOnlyList<VabNodeSnapResolver.SnapTarget> placedTargets,
        LayerMask attachmentNodeMask,
        Vector3 cursorWorld,
        float probeRadiusMeters,
        float releaseDistanceMeters,
        Func<VabNodeSnapResolver.SnapTarget, int, bool> isNodeFree,
        string heldBlockId,
        Func<string, bool> isPlacedBlock,
        ref VabNodeSnapResolver.NodeSnapMatch stickyMatch,
        ref bool hasStickyMatch,
        out VabNodeSnapResolver.NodeSnapMatch bestMatch
    )
    {
        if (TryFindBestSnap(
                ghostAdapter,
                ghostRoot,
                attachmentNodeMask,
                cursorWorld,
                probeRadiusMeters,
                isNodeFree,
                heldBlockId,
                isPlacedBlock,
                out bestMatch
            ))
        {
            stickyMatch = bestMatch;
            hasStickyMatch = true;
            return true;
        }

        if (!hasStickyMatch)
        {
            bestMatch = default;
            return false;
        }

        float releaseDistanceSquared = releaseDistanceMeters * releaseDistanceMeters;
        float stickyDistanceSquared = VabNodeSnapResolver.ComputeStickyAlignmentDistanceSquared(
            ghostAdapter,
            placedTargets,
            stickyMatch
        );

        if (stickyDistanceSquared < releaseDistanceSquared)
        {
            if (VabNodeSnapResolver.TryRecomputeAlignedMatch(
                    ghostAdapter,
                    ghostRoot,
                    placedTargets,
                    stickyMatch,
                    out bestMatch
                ))
            {
                stickyMatch = bestMatch;
                return true;
            }
        }

        hasStickyMatch = false;
        bestMatch = default;
        return false;
    }

    public static bool TryFindBestSnap(
        PartVisualAdapter ghostAdapter,
        Transform ghostRoot,
        LayerMask attachmentNodeMask,
        Vector3 cursorWorld,
        float probeRadiusMeters,
        Func<VabNodeSnapResolver.SnapTarget, int, bool> isNodeFree,
        string heldBlockId,
        Func<string, bool> isPlacedBlock,
        out VabNodeSnapResolver.NodeSnapMatch bestMatch
    )
    {
        bestMatch = default;

        if (
            ghostAdapter == null ||
            ghostRoot == null ||
            attachmentNodeMask.value == 0 ||
            ghostAdapter.NodeCount == 0
        )
        {
            return false;
        }

        float probeRadiusSquared = probeRadiusMeters * probeRadiusMeters;
        float bestMatchScore = probeRadiusSquared;
        bool found = false;
        Vector3 originalPosition = ghostRoot.position;
        Quaternion originalRotation = ghostRoot.rotation;

        for (int childNodeIndex = 0; childNodeIndex < ghostAdapter.NodeCount; childNodeIndex++)
        {
            if (!ghostAdapter.TryGetNodeWorldPosition(childNodeIndex, out Vector3 childProbeCenter))
            {
                continue;
            }

            if (!ghostAdapter.TryGetNodeWorldDirection(childNodeIndex, out Vector3 childNodeDirection))
            {
                continue;
            }

            Collider[] overlaps = Physics.OverlapSphere(
                childProbeCenter,
                probeRadiusMeters,
                attachmentNodeMask,
                QueryTriggerInteraction.Collide
            );

            if (overlaps == null || overlaps.Length == 0)
            {
                continue;
            }

            for (int overlapIndex = 0; overlapIndex < overlaps.Length; overlapIndex++)
            {
                Collider overlapCollider = overlaps[overlapIndex];

                if (overlapCollider == null)
                {
                    continue;
                }

                AttachmentNodeMarker marker = overlapCollider.GetComponent<AttachmentNodeMarker>();

                if (marker == null)
                {
                    continue;
                }

                if (marker.OwnerBlockId == heldBlockId)
                {
                    continue;
                }

                PartVisualAdapter parentAdapter =
                    overlapCollider.GetComponentInParent<PartVisualAdapter>();

                if (
                    parentAdapter == null ||
                    parentAdapter.NodeCount == 0 ||
                    string.IsNullOrEmpty(parentAdapter.BlockId) ||
                    !PartPrefabBuilder.IsMarkerOwnedByPartRoot(marker, parentAdapter.gameObject)
                )
                {
                    continue;
                }

                if (
                    parentAdapter.BlockId == heldBlockId ||
                    !isPlacedBlock(parentAdapter.BlockId)
                )
                {
                    continue;
                }

                int parentNodeIndex = marker.NodeIndex;
                Vector3 parentNodeWorld = marker.transform.position;
                Vector3 parentNodeDirection = marker.transform.up;

                if (!AreNodesStackCompatible(parentNodeDirection, childNodeDirection))
                {
                    continue;
                }

                VabNodeSnapResolver.SnapTarget parentTarget = new VabNodeSnapResolver.SnapTarget
                {
                    blockId = parentAdapter.BlockId,
                    transform = parentAdapter.transform,
                    adapter = parentAdapter,
                };

                if (isNodeFree != null && !isNodeFree(parentTarget, parentNodeIndex))
                {
                    continue;
                }

                if (!VabNodeSnapResolver.IsPreferredVerticalStackPair(
                        originalPosition,
                        parentAdapter.transform.position,
                        parentNodeIndex,
                        childNodeIndex,
                        parentNodeDirection,
                        childNodeDirection
                    ))
                {
                    continue;
                }

                ghostRoot.SetPositionAndRotation(originalPosition, originalRotation);

                if (!VabNodeSnapResolver.TryComputeAlignedPose(
                        ghostRoot,
                        ghostAdapter,
                        childNodeIndex,
                        childNodeDirection,
                        parentNodeWorld,
                        parentNodeDirection,
                        out Vector3 alignedPosition,
                        out Quaternion alignedRotation
                    ))
                {
                    continue;
                }

                ghostRoot.SetPositionAndRotation(alignedPosition, alignedRotation);

                if (!ghostAdapter.TryGetNodeWorldPosition(childNodeIndex, out Vector3 alignedChildWorld))
                {
                    continue;
                }

                float alignedDistanceSquared = (alignedChildWorld - parentNodeWorld).sqrMagnitude;
                float matchScore = VabNodeSnapResolver.ScoreSnapMatchPublic(
                    alignedPosition,
                    parentAdapter.transform.position,
                    cursorWorld,
                    parentNodeIndex,
                    childNodeIndex,
                    parentNodeDirection,
                    childNodeDirection,
                    alignedDistanceSquared
                );

                if (matchScore >= bestMatchScore)
                {
                    continue;
                }

                bestMatchScore = matchScore;
                bestMatch = new VabNodeSnapResolver.NodeSnapMatch
                {
                    parentBlockId = parentAdapter.BlockId,
                    parentNodeIndex = parentNodeIndex,
                    childNodeIndex = childNodeIndex,
                    worldPosition = alignedPosition,
                    worldRotation = alignedRotation,
                    distanceSquared = alignedDistanceSquared,
                };
                found = true;
            }
        }

        if (!found)
        {
            ghostRoot.SetPositionAndRotation(originalPosition, originalRotation);
        }

        return found;
    }

    static bool AreNodesStackCompatible(Vector3 parentNodeDirection, Vector3 childNodeDirection)
    {
        return Vector3.Dot(parentNodeDirection.normalized, childNodeDirection.normalized) < -0.85f;
    }
}
