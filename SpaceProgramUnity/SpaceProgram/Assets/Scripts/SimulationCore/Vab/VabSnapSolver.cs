using System.Collections.Generic;

/// <summary>
/// Fixed-point snap math for the VAB editor. Unity-free.
/// </summary>
public static class VabSnapSolver
{
    public const double DefaultSnapDistanceMeters = 0.25;
    public const double DefaultSnapReleaseDistanceMeters = 0.4;

    public struct SnapCandidate
    {
        public string parentBlockId;
        public int parentNodeIndex;
        public int childNodeIndex;
        public SimVector3 worldPosition;
        public SimQuaternion worldRotation;
        public double distanceSquared;
    }

    public struct PlacedBlockPose
    {
        public string blockId;
        public PartTemplate template;
        public SimVector3 worldPosition;
        public SimQuaternion worldRotation;
    }

    public static bool TryFindSnapWithHysteresis(
        PartTemplate childTemplate,
        SimVector3 childWorldPosition,
        SimQuaternion childWorldRotation,
        IReadOnlyList<PlacedBlockPose> placedBlocks,
        double acquireDistanceMeters,
        double releaseDistanceMeters,
        ref SnapCandidate stickyCandidate,
        ref bool hasStickyCandidate,
        out SnapCandidate bestSnap
    )
    {
        if (TryFindBestSnap(
                childTemplate,
                childWorldPosition,
                childWorldRotation,
                placedBlocks,
                acquireDistanceMeters,
                out bestSnap
            ))
        {
            stickyCandidate = bestSnap;
            hasStickyCandidate = true;
            return true;
        }

        if (!hasStickyCandidate)
        {
            bestSnap = default;
            return false;
        }

        double releaseDistanceSquared = releaseDistanceMeters * releaseDistanceMeters;
        double alignmentDistanceSquared = ComputeNodeAlignmentDistanceSquared(
            childTemplate,
            childWorldPosition,
            childWorldRotation,
            stickyCandidate,
            placedBlocks
        );

        if (alignmentDistanceSquared < releaseDistanceSquared)
        {
            bestSnap = stickyCandidate;
            return true;
        }

        hasStickyCandidate = false;
        bestSnap = default;
        return false;
    }

    static double ComputeNodeAlignmentDistanceSquared(
        PartTemplate childTemplate,
        SimVector3 childWorldPosition,
        SimQuaternion childWorldRotation,
        SnapCandidate stickyCandidate,
        IReadOnlyList<PlacedBlockPose> placedBlocks
    )
    {
        if (
            childTemplate == null ||
            childTemplate.nodes == null ||
            stickyCandidate.childNodeIndex < 0 ||
            stickyCandidate.childNodeIndex >= childTemplate.nodes.Count
        )
        {
            return double.MaxValue;
        }

        PlacedBlockPose? parentPose = null;

        for (int i = 0; i < placedBlocks.Count; i++)
        {
            if (placedBlocks[i].blockId == stickyCandidate.parentBlockId)
            {
                parentPose = placedBlocks[i];
                break;
            }
        }

        if (
            !parentPose.HasValue ||
            parentPose.Value.template == null ||
            parentPose.Value.template.nodes == null ||
            stickyCandidate.parentNodeIndex < 0 ||
            stickyCandidate.parentNodeIndex >= parentPose.Value.template.nodes.Count
        )
        {
            return double.MaxValue;
        }

        AttachmentNode parentNode = parentPose.Value.template.nodes[stickyCandidate.parentNodeIndex];
        AttachmentNode childNode = childTemplate.nodes[stickyCandidate.childNodeIndex];

        SimVector3 parentNodeWorld = parentPose.Value.worldPosition.Add(
            parentPose.Value.worldRotation.Rotate(parentNode.localPosition)
        );
        SimVector3 childNodeWorld = childWorldPosition.Add(
            childWorldRotation.Rotate(childNode.localPosition)
        );

        return childNodeWorld.Subtract(parentNodeWorld).LengthSquared();
    }

    public static bool TryFindBestSnap(
        PartTemplate childTemplate,
        SimVector3 childWorldPosition,
        SimQuaternion childWorldRotation,
        IReadOnlyList<PlacedBlockPose> placedBlocks,
        double snapDistanceMeters,
        out SnapCandidate bestSnap
    )
    {
        bestSnap = default;

        if (placedBlocks == null || placedBlocks.Count == 0)
        {
            return false;
        }

        double bestDistanceSquared = snapDistanceMeters * snapDistanceMeters;
        bool found = false;

        if (childTemplate == null || childTemplate.nodes == null)
        {
            return false;
        }

        for (int partIndex = 0; partIndex < placedBlocks.Count; partIndex++)
        {
            PlacedBlockPose parentPose = placedBlocks[partIndex];
            PartTemplate parentTemplate = parentPose.template;

            if (parentTemplate == null || parentTemplate.nodes == null)
            {
                continue;
            }

            for (int parentNodeIndex = 0; parentNodeIndex < parentTemplate.nodes.Count; parentNodeIndex++)
            {
                AttachmentNode parentNode = parentTemplate.nodes[parentNodeIndex];
                SimVector3 parentNodeWorld = parentPose.worldPosition.Add(
                    parentPose.worldRotation.Rotate(parentNode.localPosition)
                );
                SimVector3 parentNodeDirection = parentPose.worldRotation
                    .Rotate(parentNode.direction)
                    .Normalize();

                for (int childNodeIndex = 0; childNodeIndex < childTemplate.nodes.Count; childNodeIndex++)
                {
                    AttachmentNode childNode = childTemplate.nodes[childNodeIndex];
                    SimVector3 childNodeWorld = childWorldPosition.Add(
                        childWorldRotation.Rotate(childNode.localPosition)
                    );

                    double distanceSquared = childNodeWorld
                        .Subtract(parentNodeWorld)
                        .LengthSquared();

                    if (distanceSquared >= bestDistanceSquared)
                    {
                        continue;
                    }

                    SimVector3 parentUp = parentPose.worldRotation.Up();
                    SimQuaternion snappedRotation = ComputeSnapWorldRotation(
                        parentNodeDirection,
                        parentUp,
                        childNode.direction
                    );
                    SimVector3 snappedPosition = parentNodeWorld.Subtract(
                        snappedRotation.Rotate(childNode.localPosition)
                    );

                    bestDistanceSquared = distanceSquared;
                    bestSnap = new SnapCandidate
                    {
                        parentBlockId = parentPose.blockId,
                        parentNodeIndex = parentNodeIndex,
                        childNodeIndex = childNodeIndex,
                        worldPosition = snappedPosition,
                        worldRotation = snappedRotation,
                        distanceSquared = distanceSquared,
                    };
                    found = true;
                }
            }
        }

        return found;
    }

    public static SimQuaternion ComputeSnapWorldRotation(
        SimVector3 parentNodeWorldDirection,
        SimVector3 parentWorldUp,
        SimVector3 childNodeLocalDirection
    )
    {
        SimVector3 targetDirection = parentNodeWorldDirection.Normalize().Multiply(-1.0);
        SimVector3 childDirection = childNodeLocalDirection.Normalize();
        SimVector3 upReference = parentWorldUp.LengthSquared() > 1e-12 ?
            parentWorldUp.Normalize() :
            SimVector3.UnitY();

        SimQuaternion childBasis = SimQuaternion.FromForwardAndUp(
            childDirection,
            SimVector3.UnitY()
        );
        SimQuaternion targetBasis = SimQuaternion.FromForwardAndUp(
            targetDirection,
            upReference
        );

        return SimQuaternion.Multiply(targetBasis, childBasis.Conjugate());
    }
}
