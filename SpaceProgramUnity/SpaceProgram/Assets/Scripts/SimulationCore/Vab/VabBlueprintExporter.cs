using System;
using System.Collections.Generic;

/// <summary>
/// Converts a VAB blueprint into simulation <see cref="StageData"/> for pad launch (C21.3 MVP).
/// Single-stage export: all connected blocks in one stage.
/// </summary>
public static class VabBlueprintExporter
{
    public delegate bool PartStatsLookup(string partTemplateId, out VabPartPhysicsStats stats);

    public delegate bool ThrustFrameLookup(
        string partTemplateId,
        out SimVector3 thrustOffsetPartLocal,
        out SimVector3 thrustForceDirectionPartLocal
    );

    const double TonnesToKg = 1000.0;
    const double KilonewtonsToNewtons = 1000.0;

    internal static List<StageData> ExportToStages(
        VesselBlueprint blueprint,
        PartStatsLookup lookup,
        ThrustFrameLookup thrustLookup = null
    )
    {
        List<StageData> stages = new List<StageData>();

        if (blueprint?.blocks == null || lookup == null)
        {
            return stages;
        }

        StageData stage = new StageData
        {
            name = string.IsNullOrWhiteSpace(blueprint.vesselName) ?
                "VAB Stack" :
                blueprint.vesselName.Trim(),
            blocks = new List<ShipBlock>(),
        };

        int nextBlockId = 1;
        var composedPoseCache =
            new Dictionary<string, (SimVector3 position, SimQuaternion rotation)>();

        for (int i = 0; i < blueprint.blocks.Count; i++)
        {
            BlockSnapshot snapshot = blueprint.blocks[i];

            if (snapshot == null || snapshot.isDisjointed)
            {
                continue;
            }

            if (!lookup(snapshot.partTemplateId, out VabPartPhysicsStats stats))
            {
                continue;
            }

            if (
                !VabAttachPoseComposer.TryComposeSimPose(
                    blueprint.blocks,
                    snapshot,
                    composedPoseCache,
                    out SimVector3 simPosition,
                    out SimQuaternion simRotation
                )
            )
            {
                continue;
            }

            ShipBlock block = CreateShipBlock(
                snapshot,
                stats,
                simPosition,
                simRotation,
                thrustLookup,
                ref nextBlockId
            );
            stage.blocks.Add(block);

            stage.dryMass += block.dryMass;

            if (block.isFuelTank)
            {
                stage.fuelMass += block.fuelMass;
                stage.maxFuelMass += block.fuelCapacity;
            }

            if (block.isEngine && stats.thrustNewtons > 0.0)
            {
                stage.thrust += stats.thrustNewtons;

                if (stats.ispSeconds > stage.isp)
                {
                    stage.isp = stats.ispSeconds;
                }
            }
        }

        if (stage.blocks.Count == 0)
        {
            return stages;
        }

        if (stage.maxFuelMass <= 0.0)
        {
            stage.maxFuelMass = stage.fuelMass;
        }

        if (stage.isp <= 0.0 && stage.thrust > 0.0)
        {
            stage.isp = 300.0;
        }

        stage.dragCoefficient = 0.25;
        stages.Add(stage);
        StageData.PrepareStages(stages);
        return stages;
    }

    static ShipBlock CreateShipBlock(
        BlockSnapshot snapshot,
        VabPartPhysicsStats stats,
        SimVector3 simPosition,
        SimQuaternion simRotation,
        ThrustFrameLookup thrustLookup,
        ref int nextBlockId
    )
    {
        SimVector3 dimensions = EstimateDimensions(stats.category);
        ShipBlockType blockType = MapBlockType(stats.category);
        bool isEngine = stats.isEngine || blockType == ShipBlockType.Engine;
        bool isFuelTank = stats.isFuelTank || blockType == ShipBlockType.FuelTank;
        SimVector3 thrustOffsetPartLocal = SimVector3.Zero();
        SimVector3 thrustForceDirectionPartLocal = SimVector3.UnitY();

        if (
            isEngine &&
            thrustLookup != null &&
            thrustLookup(
                snapshot.partTemplateId,
                out thrustOffsetPartLocal,
                out thrustForceDirectionPartLocal
            )
        )
        {
        }
        else if (isEngine)
        {
            thrustForceDirectionPartLocal = new SimVector3(0.0, 1.0, 0.0);
        }

        SimVector3 thrustDirection = isEngine ?
            simRotation.Rotate(
                VabBlueprintExporter.MapVabDirectionToSim(thrustForceDirectionPartLocal)
            ).Normalize() :
            SimVector3.UnitZ();
        SimVector3 thrustLocalOffset = isEngine ?
            simRotation.Rotate(
                VabBlueprintExporter.MapVabPositionToSim(thrustOffsetPartLocal)
            ) :
            SimVector3.Zero();

        return new ShipBlock
        {
            blockId = nextBlockId++,
            id = snapshot.blockId ?? string.Empty,
            partTemplateId = snapshot.partTemplateId ?? string.Empty,
            parentBlockId = snapshot.parentBlockId ?? string.Empty,
            hasVabAttach = true,
            attachLocalPosition = snapshot.localPosition,
            attachLocalRotation = snapshot.localRotation.Normalize(),
            localRotation = simRotation,
            blockType = blockType,
            localOffset = simPosition,
            dimensions = dimensions,
            dryMass = stats.dryMassKg,
            fuelMass = isFuelTank ? stats.fuelMassKg : 0.0,
            fuelCapacity = isFuelTank ? stats.fuelMassKg : 0.0,
            isFuelTank = isFuelTank,
            isEngine = isEngine,
            isDockingPort = false,
            thrustDirection = thrustDirection,
            thrustLocalOffset = thrustLocalOffset,
            maxThrustNewtons = isEngine ? stats.thrustNewtons : 0.0,
            ispSeconds = isEngine ? stats.ispSeconds : 0.0,
            rcsThrust = 0.0,
            structuralStrength = ShipBlock.GetDefaultStructuralStrength(blockType),
        };
    }

    static ShipBlockType MapBlockType(string category)
    {
        if (string.Equals(category, PartDatabase.CategoryEngine, StringComparison.Ordinal))
        {
            return ShipBlockType.Engine;
        }

        if (string.Equals(category, PartDatabase.CategoryFuelTank, StringComparison.Ordinal))
        {
            return ShipBlockType.FuelTank;
        }

        if (string.Equals(category, PartDatabase.CategoryCommand, StringComparison.Ordinal))
        {
            return ShipBlockType.Adapter;
        }

        return ShipBlockType.Adapter;
    }

    static SimVector3 EstimateDimensions(string category)
    {
        if (string.Equals(category, PartDatabase.CategoryEngine, StringComparison.Ordinal))
        {
            return new SimVector3(1.25, 1.25, 0.9);
        }

        if (string.Equals(category, PartDatabase.CategoryFuelTank, StringComparison.Ordinal))
        {
            return new SimVector3(1.25, 1.25, 2.0);
        }

        if (string.Equals(category, PartDatabase.CategoryCommand, StringComparison.Ordinal))
        {
            return new SimVector3(1.25, 1.25, 1.0);
        }

        return new SimVector3(1.0, 1.0, 1.0);
    }

    /// <summary>
    /// VAB uses Y-up stacking; pad flight keeps the same Y-up body frame in Core.
    /// </summary>
    public static SimVector3 MapVabPositionToSim(SimVector3 vabLocal)
    {
        return vabLocal;
    }

    public static SimVector3 MapVabDirectionToSim(SimVector3 vabDirection)
    {
        return vabDirection;
    }

    public static VabPartPhysicsStats StatsFromPartTemplate(PartTemplate template)
    {
        if (template == null)
        {
            return default;
        }

        bool isEngine =
            string.Equals(template.category, PartDatabase.CategoryEngine, StringComparison.OrdinalIgnoreCase) ||
            template.maxThrustVac > 0f;
        bool isFuelTank =
            string.Equals(template.category, PartDatabase.CategoryFuelTank, StringComparison.OrdinalIgnoreCase) ||
            (!isEngine && template.fuelCapacity > 0f);

        return new VabPartPhysicsStats
        {
            dryMassKg = template.dryMass * TonnesToKg,
            fuelMassKg = template.fuelCapacity,
            thrustNewtons = template.maxThrustVac * KilonewtonsToNewtons,
            ispSeconds = template.ispVacuum,
            isEngine = isEngine,
            isFuelTank = isFuelTank,
            category = template.category,
        };
    }
}
