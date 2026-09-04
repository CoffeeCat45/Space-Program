using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Node-to-node snap using spawned <see cref="PartVisualAdapter"/> attachment transforms.
/// </summary>
static class VabNodeSnapResolver
{
    public struct SnapTarget
    {
        public string blockId;
        public Transform transform;
        public PartVisualAdapter adapter;
    }

    public struct NodeSnapMatch
    {
        public string parentBlockId;
        public int parentNodeIndex;
        public int childNodeIndex;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public float distanceSquared;
    }

    public static bool TryFindNodeSnapWithHysteresis(
        PartVisualAdapter ghostAdapter,
        Transform ghostRoot,
        IReadOnlyList<SnapTarget> placedTargets,
        Func<SnapTarget, int, bool> isNodeFree,
        Vector3 cursorWorld,
        float cursorApproachMeters,
        float snapAcquireMeters,
        float releaseDistanceMeters,
        ref NodeSnapMatch stickyMatch,
        ref bool hasStickyMatch,
        out NodeSnapMatch bestMatch,
        string preferredParentBlockId = null,
        Vector3? meshHitPoint = null
    )
    {
        if (TryFindBestNodeSnap(
                ghostAdapter,
                ghostRoot,
                placedTargets,
                isNodeFree,
                cursorWorld,
                cursorApproachMeters,
                snapAcquireMeters,
                out bestMatch,
                preferredParentBlockId,
                meshHitPoint
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
        float stickyDistanceSquared = ComputeStickyAlignmentDistanceSquared(
            ghostAdapter,
            placedTargets,
            stickyMatch
        );

        if (stickyDistanceSquared < releaseDistanceSquared)
        {
            if (TryRecomputeAlignedMatch(
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

    public static bool TryFindBestNodeSnap(
        PartVisualAdapter ghostAdapter,
        Transform ghostRoot,
        IReadOnlyList<SnapTarget> placedTargets,
        Func<SnapTarget, int, bool> isNodeFree,
        Vector3 cursorWorld,
        float cursorApproachMeters,
        float snapAcquireMeters,
        out NodeSnapMatch bestMatch,
        string preferredParentBlockId = null,
        Vector3? meshHitPoint = null
    )
    {
        bestMatch = default;

        if (
            ghostAdapter == null ||
            ghostRoot == null ||
            placedTargets == null ||
            placedTargets.Count == 0 ||
            ghostAdapter.NodeCount == 0
        )
        {
            return false;
        }

        float snapAcquireSquared = snapAcquireMeters * snapAcquireMeters;
        float bestMatchScore = float.MaxValue;
        bool found = false;
        Vector3 originalPosition = ghostRoot.position;
        Quaternion originalRotation = ghostRoot.rotation;

        for (int partIndex = 0; partIndex < placedTargets.Count; partIndex++)
        {
            SnapTarget parentTarget = placedTargets[partIndex];
            PartVisualAdapter parentAdapter = parentTarget.adapter;

            if (parentAdapter == null || parentAdapter.NodeCount == 0)
            {
                continue;
            }

            bool isPreferredParent =
                !string.IsNullOrEmpty(preferredParentBlockId) &&
                parentTarget.blockId == preferredParentBlockId;

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

                if (!PassesCursorStackApproachGate(cursorWorld, parentNodeWorld, cursorApproachMeters))
                {
                    continue;
                }

                if (
                    !TryFindComplementaryChildNodeIndex(
                        ghostAdapter,
                        parentNodeDirection,
                        out int childNodeIndex,
                        out Vector3 childNodeDirection
                    )
                )
                {
                    continue;
                }

                ghostRoot.SetPositionAndRotation(originalPosition, originalRotation);

                Vector3 probePosition = originalPosition;
                probePosition.x = cursorWorld.x;
                probePosition.z = cursorWorld.z;
                ghostRoot.position = probePosition;

                if (
                    !ghostAdapter.TryGetNodeWorldPosition(childNodeIndex, out Vector3 childNodeWorld) ||
                    (childNodeWorld - parentNodeWorld).sqrMagnitude > snapAcquireSquared
                )
                {
                    continue;
                }

                ghostRoot.SetPositionAndRotation(originalPosition, originalRotation);
                ghostRoot.position = probePosition;

                if (!TryComputeAlignedPose(
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

                if (
                    !ghostAdapter.TryGetNodeWorldPosition(childNodeIndex, out Vector3 alignedChildWorld) ||
                    !parentAdapter.TryGetNodeWorldPosition(parentNodeIndex, out Vector3 alignedParentWorld)
                )
                {
                    continue;
                }

                float alignedDistanceSquared = (alignedChildWorld - alignedParentWorld).sqrMagnitude;

                if (alignedDistanceSquared > snapAcquireSquared)
                {
                    continue;
                }

                float matchScore = ScoreCursorStackMatch(
                    cursorWorld,
                    parentNodeWorld,
                    meshHitPoint,
                    alignedDistanceSquared
                );

                if (isPreferredParent)
                {
                    matchScore *= 0.35f;
                }

                if (meshHitPoint.HasValue)
                {
                    Vector3 hitDelta = meshHitPoint.Value - parentNodeWorld;
                    matchScore += hitDelta.sqrMagnitude * 0.08f;
                }

                if (matchScore >= bestMatchScore)
                {
                    continue;
                }

                bestMatchScore = matchScore;
                bestMatch = new NodeSnapMatch
                {
                    parentBlockId = parentTarget.blockId,
                    parentNodeIndex = parentNodeIndex,
                    childNodeIndex = childNodeIndex,
                    worldPosition = alignedPosition,
                    worldRotation = alignedRotation,
                    distanceSquared = alignedDistanceSquared,
                };
                found = true;
            }
        }

        ghostRoot.SetPositionAndRotation(originalPosition, originalRotation);
        return found;
    }

    public static bool TryFindComplementaryChildNodeIndex(
        PartVisualAdapter ghostAdapter,
        Vector3 parentNodeDirection,
        out int childNodeIndex,
        out Vector3 childNodeDirection
    )
    {
        childNodeIndex = -1;
        childNodeDirection = default;

        if (ghostAdapter == null || ghostAdapter.NodeCount == 0)
        {
            return false;
        }

        bool parentBottom = parentNodeDirection.y < -0.85f;
        bool parentTop = parentNodeDirection.y > 0.85f;

        if (!parentBottom && !parentTop)
        {
            return false;
        }

        for (int nodeIndex = 0; nodeIndex < ghostAdapter.NodeCount; nodeIndex++)
        {
            if (!ghostAdapter.TryGetNodeWorldDirection(nodeIndex, out Vector3 candidateDirection))
            {
                continue;
            }

            bool childTop = candidateDirection.y > 0.85f;
            bool childBottom = candidateDirection.y < -0.85f;

            if (parentBottom && childTop)
            {
                childNodeIndex = nodeIndex;
                childNodeDirection = candidateDirection;
                return true;
            }

            if (parentTop && childBottom)
            {
                childNodeIndex = nodeIndex;
                childNodeDirection = candidateDirection;
                return true;
            }
        }

        return false;
    }

    public static bool PassesCursorStackApproachGate(
        Vector3 cursorWorld,
        Vector3 parentNodeWorld,
        float approachHorizontalMeters
    )
    {
        Vector3 flatDelta = cursorWorld - parentNodeWorld;
        flatDelta.y = 0f;
        float horizontalSquared = approachHorizontalMeters * approachHorizontalMeters;

        if (flatDelta.sqrMagnitude > horizontalSquared)
        {
            return false;
        }

        return Mathf.Abs(cursorWorld.y - parentNodeWorld.y) <= MaxVerticalStackApproachMeters;
    }

    public static float ScoreCursorStackMatch(
        Vector3 cursorWorld,
        Vector3 parentNodeWorld,
        Vector3? meshHitPoint,
        float alignedDistanceSquared
    )
    {
        Vector3 referencePoint = meshHitPoint ?? cursorWorld;
        Vector3 flatDelta = referencePoint - parentNodeWorld;
        flatDelta.y = 0f;
        float score = flatDelta.sqrMagnitude + alignedDistanceSquared * 0.01f;
        score += Mathf.Abs(referencePoint.y - parentNodeWorld.y) * 0.15f;
        return score;
    }

    public static float ComputeStickyAlignmentDistanceSquared(
        PartVisualAdapter ghostAdapter,
        IReadOnlyList<SnapTarget> placedTargets,
        NodeSnapMatch stickyMatch
    )
    {
        if (
            ghostAdapter == null ||
            !ghostAdapter.TryGetNodeWorldPosition(stickyMatch.childNodeIndex, out Vector3 childWorld)
        )
        {
            return float.MaxValue;
        }

        for (int i = 0; i < placedTargets.Count; i++)
        {
            SnapTarget target = placedTargets[i];

            if (target.blockId != stickyMatch.parentBlockId)
            {
                continue;
            }

            if (
                target.adapter != null &&
                target.adapter.TryGetNodeWorldPosition(stickyMatch.parentNodeIndex, out Vector3 parentWorld)
            )
            {
                return (childWorld - parentWorld).sqrMagnitude;
            }
        }

        return float.MaxValue;
    }

    public static bool TryRecomputeAlignedMatch(
        PartVisualAdapter ghostAdapter,
        Transform ghostRoot,
        IReadOnlyList<SnapTarget> placedTargets,
        NodeSnapMatch stickyMatch,
        out NodeSnapMatch recomputedMatch
    )
    {
        recomputedMatch = default;

        for (int i = 0; i < placedTargets.Count; i++)
        {
            SnapTarget parentTarget = placedTargets[i];

            if (parentTarget.blockId != stickyMatch.parentBlockId)
            {
                continue;
            }

            PartVisualAdapter parentAdapter = parentTarget.adapter;

            if (parentAdapter == null)
            {
                return false;
            }

            if (!parentAdapter.TryGetNodeWorldPosition(stickyMatch.parentNodeIndex, out Vector3 parentNodeWorld))
            {
                return false;
            }

            if (!parentAdapter.TryGetNodeWorldDirection(stickyMatch.parentNodeIndex, out Vector3 parentNodeDirection))
            {
                return false;
            }

            if (!ghostAdapter.TryGetNodeWorldDirection(stickyMatch.childNodeIndex, out Vector3 childNodeDirection))
            {
                return false;
            }

            if (!TryComputeAlignedPose(
                    ghostRoot,
                    ghostAdapter,
                    stickyMatch.childNodeIndex,
                    childNodeDirection,
                    parentNodeWorld,
                    parentNodeDirection,
                    out Vector3 alignedPosition,
                    out Quaternion alignedRotation
                ))
            {
                return false;
            }

            ghostAdapter.TryGetNodeWorldPosition(stickyMatch.childNodeIndex, out Vector3 alignedChildWorld);
            parentAdapter.TryGetNodeWorldPosition(stickyMatch.parentNodeIndex, out Vector3 alignedParentWorld);

            recomputedMatch = new NodeSnapMatch
            {
                parentBlockId = stickyMatch.parentBlockId,
                parentNodeIndex = stickyMatch.parentNodeIndex,
                childNodeIndex = stickyMatch.childNodeIndex,
                worldPosition = alignedPosition,
                worldRotation = alignedRotation,
                distanceSquared = (alignedChildWorld - alignedParentWorld).sqrMagnitude,
            };
            return true;
        }

        return false;
    }

    const float MaxVerticalStackApproachMeters = 32f;

    static bool PassesNodeProximityGate(
        Vector3 childNodeWorld,
        Vector3 parentNodeWorld,
        Vector3 parentNodeDirection,
        Vector3 childNodeDirection,
        float acquireDistanceMeters
    )
    {
        if (!AreNodesStackCompatible(parentNodeDirection, childNodeDirection))
        {
            return false;
        }

        float acquireDistanceSquared = acquireDistanceMeters * acquireDistanceMeters;

        if (AreNodesVertical(parentNodeDirection) && AreNodesVertical(childNodeDirection))
        {
            Vector3 flatDelta = childNodeWorld - parentNodeWorld;
            flatDelta.y = 0f;

            if (flatDelta.sqrMagnitude > acquireDistanceSquared)
            {
                return false;
            }

            float verticalSeparation = Mathf.Abs(childNodeWorld.y - parentNodeWorld.y);
            return verticalSeparation <= MaxVerticalStackApproachMeters;
        }

        return (childNodeWorld - parentNodeWorld).sqrMagnitude <= acquireDistanceSquared;
    }

    static bool AreNodesStackCompatible(Vector3 parentNodeDirection, Vector3 childNodeDirection)
    {
        return Vector3.Dot(parentNodeDirection.normalized, childNodeDirection.normalized) < -0.85f;
    }

    static bool AreNodesVertical(Vector3 nodeDirection)
    {
        return Mathf.Abs(Vector3.Dot(nodeDirection.normalized, Vector3.up)) > 0.85f;
    }

    public static float ScoreSnapMatchPublic(
        Vector3 ghostRootWorld,
        Vector3 parentRootWorld,
        Vector3 cursorWorld,
        int parentNodeIndex,
        int childNodeIndex,
        Vector3 parentNodeDirection,
        Vector3 childNodeDirection,
        float alignedDistanceSquared
    )
    {
        return ScoreStackPair(
            ghostRootWorld,
            parentRootWorld,
            cursorWorld,
            parentNodeIndex,
            childNodeIndex,
            parentNodeDirection,
            childNodeDirection,
            alignedDistanceSquared
        );
    }

    static float ScoreSnapMatch(
        Vector3 ghostRootWorld,
        Vector3 parentRootWorld,
        Vector3 cursorWorld,
        int parentNodeIndex,
        int childNodeIndex,
        Vector3 parentNodeDirection,
        Vector3 childNodeDirection,
        float alignedDistanceSquared
    )
    {
        return ScoreStackPair(
            ghostRootWorld,
            parentRootWorld,
            cursorWorld,
            parentNodeIndex,
            childNodeIndex,
            parentNodeDirection,
            childNodeDirection,
            alignedDistanceSquared
        );
    }

    public static float ScoreStackPair(
        Vector3 alignedGhostRootWorld,
        Vector3 parentRootWorld,
        Vector3 cursorWorld,
        int parentNodeIndex,
        int childNodeIndex,
        Vector3 parentNodeDirection,
        Vector3 childNodeDirection,
        float alignedDistanceSquared
    )
    {
        float score = alignedDistanceSquared;

        if (!IsPreferredVerticalStackPair(
                alignedGhostRootWorld,
                parentRootWorld,
                parentNodeIndex,
                childNodeIndex,
                parentNodeDirection,
                childNodeDirection
            ))
        {
            score += 4f;
            return score;
        }

        bool underStack =
            childNodeDirection.y > 0.85f &&
            parentNodeDirection.y < -0.85f;
        bool overStack =
            childNodeDirection.y < -0.85f &&
            parentNodeDirection.y > 0.85f;

        if (underStack && alignedGhostRootWorld.y > parentRootWorld.y + 0.05f)
        {
            score += 16f;
        }

        if (overStack && alignedGhostRootWorld.y < parentRootWorld.y - 0.05f)
        {
            score += 16f;
        }

        score += Mathf.Abs(alignedGhostRootWorld.y - cursorWorld.y) * 0.35f;

        return score;
    }

    internal static bool IsPreferredVerticalStackPair(
        Vector3 ghostRootWorld,
        Vector3 parentRootWorld,
        int parentNodeIndex,
        int childNodeIndex,
        Vector3 parentNodeDirection,
        Vector3 childNodeDirection
    )
    {
        bool childTop = childNodeDirection.y > 0.85f;
        bool childBottom = childNodeDirection.y < -0.85f;
        bool parentTop = parentNodeDirection.y > 0.85f;
        bool parentBottom = parentNodeDirection.y < -0.85f;

        // Vertical stack only: opposing top/bottom nodes (ignore root Y — identical parts share center height).
        return (childBottom && parentTop) || (childTop && parentBottom);
    }

    public static bool TryComputeAlignedPose(
        Transform ghostRoot,
        PartVisualAdapter ghostAdapter,
        int childNodeIndex,
        Vector3 childNodeDirectionWorld,
        Vector3 parentNodeWorld,
        Vector3 parentNodeDirectionWorld,
        out Vector3 alignedPosition,
        out Quaternion alignedRotation
    )
    {
        alignedPosition = ghostRoot.position;
        alignedRotation = ghostRoot.rotation;

        Vector3 targetChildDirection = -parentNodeDirectionWorld.normalized;
        Vector3 sourceChildDirection = childNodeDirectionWorld.normalized;

        if (targetChildDirection.sqrMagnitude < 1e-8f || sourceChildDirection.sqrMagnitude < 1e-8f)
        {
            return false;
        }

        Quaternion rotationDelta = Quaternion.FromToRotation(
            sourceChildDirection,
            targetChildDirection
        );
        alignedRotation = rotationDelta * ghostRoot.rotation;
        ghostRoot.SetPositionAndRotation(ghostRoot.position, alignedRotation);

        if (!ghostAdapter.TryGetNodeWorldPosition(childNodeIndex, out Vector3 childNodeWorldAfterRotate))
        {
            return false;
        }

        alignedPosition = ghostRoot.position + (parentNodeWorld - childNodeWorldAfterRotate);
        alignedRotation = ghostRoot.rotation;
        return true;
    }
}
