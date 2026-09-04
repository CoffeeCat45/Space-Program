using System;
using System.Collections.Generic;

/// <summary>
/// Composes VAB parent-relative attach poses into vessel-local simulation poses.
/// </summary>
static class VabAttachPoseComposer
{
    public static bool TryComposeSimPose(
        IReadOnlyList<BlockSnapshot> blocks,
        BlockSnapshot snapshot,
        Dictionary<string, (SimVector3 position, SimQuaternion rotation)> cache,
        out SimVector3 simPosition,
        out SimQuaternion simRotation
    )
    {
        simPosition = SimVector3.Zero();
        simRotation = SimQuaternion.Identity();

        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.blockId))
        {
            return false;
        }

        if (cache.TryGetValue(snapshot.blockId, out (SimVector3 position, SimQuaternion rotation) cachedPose))
        {
            simPosition = VabBlueprintExporter.MapVabPositionToSim(cachedPose.position);
            simRotation = MapVabRotationToSim(cachedPose.rotation);
            return true;
        }

        SimVector3 vabWorldPosition;
        SimQuaternion vabWorldRotation;

        if (string.IsNullOrWhiteSpace(snapshot.parentBlockId))
        {
            vabWorldPosition = snapshot.localPosition;
            vabWorldRotation = snapshot.localRotation.Normalize();
        }
        else
        {
            BlockSnapshot parentSnapshot = FindSnapshot(blocks, snapshot.parentBlockId);

            if (
                parentSnapshot == null ||
                !TryComposeSimPose(blocks, parentSnapshot, cache, out _, out _)
            )
            {
                return false;
            }

            if (!cache.TryGetValue(parentSnapshot.blockId, out (SimVector3 position, SimQuaternion rotation) parentVabPose))
            {
                return false;
            }

            vabWorldPosition = parentVabPose.position.Add(
                parentVabPose.rotation.Rotate(snapshot.localPosition)
            );
            vabWorldRotation = SimQuaternion
                .Multiply(parentVabPose.rotation, snapshot.localRotation)
                .Normalize();
        }

        cache[snapshot.blockId] = (vabWorldPosition, vabWorldRotation);
        simPosition = VabBlueprintExporter.MapVabPositionToSim(vabWorldPosition);
        simRotation = MapVabRotationToSim(vabWorldRotation);
        return true;
    }

    static BlockSnapshot FindSnapshot(IReadOnlyList<BlockSnapshot> blocks, string blockId)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            BlockSnapshot snapshot = blocks[i];

            if (
                snapshot != null &&
                string.Equals(snapshot.blockId, blockId, StringComparison.Ordinal)
            )
            {
                return snapshot;
            }
        }

        return null;
    }

    static SimQuaternion MapVabRotationToSim(SimQuaternion vabRotation)
    {
        return vabRotation.Normalize();
    }
}
