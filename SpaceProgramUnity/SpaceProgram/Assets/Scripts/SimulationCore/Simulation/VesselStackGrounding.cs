using System;

/// <summary>
/// Measures vessel contact height from authored stack-bottom attachment nodes (KSP-style),
/// not renderer AABB corners.
/// </summary>
static class VesselStackGrounding
{
    public static bool TryMeasureBottomExtentAlongWorldDirection(
        Vessel vessel,
        SimVector3 worldDirectionOutward,
        out double bottomExtentMeters
    )
    {
        bottomExtentMeters = 0.0;

        if (vessel == null || worldDirectionOutward.LengthSquared() <= 1e-18)
        {
            return false;
        }

        vessel.EnsureStageBlocks();
        SimVector3 outward = worldDirectionOutward.Normalize();
        double minProjection = double.MaxValue;
        bool found = false;

        if (vessel.stages == null)
        {
            return false;
        }

        for (int stageIndex = 0; stageIndex < vessel.stages.Count; stageIndex++)
        {
            StageData stage = vessel.stages[stageIndex];

            if (stage?.blocks == null)
            {
                continue;
            }

            for (int blockIndex = 0; blockIndex < stage.blocks.Count; blockIndex++)
            {
                ShipBlock block = stage.blocks[blockIndex];

                if (
                    block == null ||
                    !block.hasVabAttach ||
                    string.IsNullOrWhiteSpace(block.partTemplateId) ||
                    block.blockType == ShipBlockType.RcsThruster ||
                    block.rcsThrust > 0.0 ||
                    block.blockType == ShipBlockType.DockingPort ||
                    block.isDockingPort
                )
                {
                    continue;
                }

                if (!TryGetStackBottomNodePartLocal(block.partTemplateId, out SimVector3 nodePartLocal))
                {
                    continue;
                }

                SimVector3 nodeVesselLocal = block.localOffset.Add(
                    block.localRotation.Rotate(nodePartLocal)
                );
                SimVector3 nodeFromCom = nodeVesselLocal.Subtract(vessel.centerOfMassLocal);
                SimVector3 nodeWorld = vessel.orientation.Rotate(nodeFromCom);
                double projection = SimVector3.Dot(nodeWorld, outward);

                if (!found || projection < minProjection)
                {
                    minProjection = projection;
                    found = true;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        bottomExtentMeters = LaunchPadConstants.GetEffectiveBottomExtentMeters(
            Math.Max(-minProjection, 0.0)
        );
        return true;
    }

    public static bool TryGetLowestStackBottomOffsetFromCom(
        Vessel vessel,
        SimVector3 worldDirectionOutward,
        out SimVector3 bottomNodeOffsetFromCom
    )
    {
        bottomNodeOffsetFromCom = SimVector3.Zero();

        if (vessel == null || worldDirectionOutward.LengthSquared() <= 1e-18)
        {
            return false;
        }

        vessel.EnsureStageBlocks();
        SimVector3 outward = worldDirectionOutward.Normalize();

        if (vessel.stages == null)
        {
            return false;
        }

        bool found = false;
        double minProjection = double.MaxValue;
        SimVector3 bestOffsetFromCom = SimVector3.Zero();

        for (int stageIndex = 0; stageIndex < vessel.stages.Count; stageIndex++)
        {
            StageData stage = vessel.stages[stageIndex];

            if (stage?.blocks == null)
            {
                continue;
            }

            for (int blockIndex = 0; blockIndex < stage.blocks.Count; blockIndex++)
            {
                ShipBlock block = stage.blocks[blockIndex];

                if (
                    block == null ||
                    !block.hasVabAttach ||
                    string.IsNullOrWhiteSpace(block.partTemplateId) ||
                    block.blockType == ShipBlockType.RcsThruster ||
                    block.rcsThrust > 0.0 ||
                    block.blockType == ShipBlockType.DockingPort ||
                    block.isDockingPort
                )
                {
                    continue;
                }

                if (!TryGetStackBottomNodePartLocal(block.partTemplateId, out SimVector3 nodePartLocal))
                {
                    continue;
                }

                SimVector3 nodeVesselLocal = block.localOffset.Add(
                    block.localRotation.Rotate(nodePartLocal)
                );
                SimVector3 offsetFromCom = nodeVesselLocal.Subtract(vessel.centerOfMassLocal);
                double projection = SimVector3.Dot(
                    vessel.orientation.Rotate(offsetFromCom),
                    outward
                );

                if (!found || projection < minProjection)
                {
                    minProjection = projection;
                    bestOffsetFromCom = offsetFromCom;
                    found = true;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        bottomNodeOffsetFromCom = bestOffsetFromCom;
        return true;
    }

    static bool TryGetStackBottomNodePartLocal(
        string partTemplateId,
        out SimVector3 nodePartLocal
    )
    {
        nodePartLocal = SimVector3.Zero();
        PartTemplate template = PartDatabase.GetById(partTemplateId);

        if (template?.nodes != null)
        {
            for (int i = 0; i < template.nodes.Count; i++)
            {
                AttachmentNode node = template.nodes[i];

                if (node == null)
                {
                    continue;
                }

                if (node.role == PartAttachmentRoles.StackBottom)
                {
                    nodePartLocal = node.localPosition;
                    return true;
                }
            }

            for (int i = 0; i < template.nodes.Count; i++)
            {
                AttachmentNode node = template.nodes[i];

                if (node == null)
                {
                    continue;
                }

                if (node.direction.y < -0.85)
                {
                    nodePartLocal = node.localPosition;
                    return true;
                }
            }
        }

        if (
            PartStackProfileLibrary.TryGetProfile(partTemplateId, out PartStackProfileLibrary.Profile profile) &&
            profile.BottomY.HasValue
        )
        {
            nodePartLocal = new SimVector3(0.0, profile.BottomY.Value, 0.0);
            return true;
        }

        if (
            PartStackProfileLibrary.TryGetProfile(partTemplateId, out profile) &&
            profile.StackHalfHeightMeters.HasValue
        )
        {
            nodePartLocal = new SimVector3(0.0, -profile.StackHalfHeightMeters.Value, 0.0);
            return true;
        }

        return false;
    }
}
