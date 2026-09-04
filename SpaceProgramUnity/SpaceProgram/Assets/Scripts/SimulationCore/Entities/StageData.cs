using System;
using System.Collections.Generic;

class StageData
{
    public string name;
    public double dryMass;
    public double fuelMass;
    public double maxFuelMass;
    public double thrust;
    public double isp;
    public double dragCoefficient;
    public List<ShipBlock> blocks;
    public SimVector3 stageMountOffset;
    public bool suppressLegacyPrimitiveBlocks;

    const double TankLengthZ = 5.5;
    const double TankDiameterXY = 2.0;
    const double DockingPortLengthZ = 1.0;
    const double DockingPortDiameterXY = 1.2;
    const double DockingPortDryMass = 0.2;

    public StageData()
    {
        name = string.Empty;
        blocks = new List<ShipBlock>();
        stageMountOffset = SimVector3.Zero();
    }

    public StageData(
        string name,
        double dryMass,
        double fuelMass,
        double maxFuelMass,
        double thrust,
        double isp,
        double dragCoefficient = 0.25
    )
    {
        this.name = name;
        this.dryMass = dryMass;
        this.fuelMass = fuelMass;
        this.maxFuelMass = maxFuelMass;
        this.thrust = thrust;
        this.isp = isp;
        this.dragCoefficient = dragCoefficient;
        blocks = new List<ShipBlock>();
        stageMountOffset = SimVector3.Zero();
    }

    public double GetFuelConsumptionPerForceSecond()
    {
        double effectiveIsp = ResolveEffectiveIsp();

        if (effectiveIsp <= 0.0)
        {
            return 0.0;
        }

        return 1.0 / (effectiveIsp * PhysicsConstants.StandardGravity);
    }

    double ResolveEffectiveIsp()
    {
        if (isp > 0.0)
        {
            return isp;
        }

        if (blocks == null)
        {
            return 0.0;
        }

        double bestIsp = 0.0;

        for (int i = 0; i < blocks.Count; i++)
        {
            ShipBlock block = blocks[i];

            if (block == null || block.isDestroyed || !block.IsEngineBlock())
            {
                continue;
            }

            bestIsp = Math.Max(bestIsp, block.ispSeconds);
        }

        return bestIsp;
    }

    public StageData Copy()
    {
        StageData copy = new StageData(
            name,
            dryMass,
            fuelMass,
            maxFuelMass,
            thrust,
            isp,
            dragCoefficient
        );
        copy.stageMountOffset = stageMountOffset;
        copy.suppressLegacyPrimitiveBlocks = suppressLegacyPrimitiveBlocks;

        if (blocks != null)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                copy.blocks.Add(blocks[i].Copy());
            }
        }

        return copy;
    }

    public static void EnsureDockingPortBlock(StageData stage)
    {
        if (stage == null)
        {
            return;
        }

        if (stage.blocks == null)
        {
            stage.blocks = new List<ShipBlock>();
        }

        GetStageHullZExtents(stage, out _, out double maxHullZ);
        double portCenterZ = maxHullZ + DockingPortLengthZ * 0.5;
        SimVector3 portDimensions = new SimVector3(
            DockingPortDiameterXY,
            DockingPortDiameterXY,
            DockingPortLengthZ
        );

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (
                !block.isDockingPort &&
                block.blockType != ShipBlockType.DockingPort
            )
            {
                continue;
            }

            block.isDockingPort = true;
            block.blockType = ShipBlockType.DockingPort;
            block.localOffset = new SimVector3(0.0, 0.0, portCenterZ);
            block.dimensions = portDimensions;
            block.thrustDirection = SimVector3.UnitZ();
            return;
        }

        stage.blocks.Add(
            new ShipBlock
            {
                id = "docking_port",
                blockType = ShipBlockType.DockingPort,
                isDockingPort = true,
                localOffset = new SimVector3(0.0, 0.0, portCenterZ),
                dimensions = portDimensions,
                dryMass = DockingPortDryMass,
                fuelMass = 0.0,
                fuelCapacity = 0.0,
                isFuelTank = false,
                isEngine = false,
                thrustDirection = SimVector3.UnitZ(),
                rcsThrust = 0.0,
                structuralStrength = ShipBlock.GetDefaultStructuralStrength(
                    ShipBlockType.DockingPort
                )
            }
        );
        stage.dryMass += DockingPortDryMass;
    }

    public static void PrepareDockingStage(StageData stage)
    {
        if (stage == null)
        {
            return;
        }

        stage.suppressLegacyPrimitiveBlocks = true;
        EnsureDockingPortBlock(stage);
        AlignStageBlocksBottomToOrigin(stage);
    }

    public static void PrepareStages(List<StageData> stages)
    {
        if (stages == null)
        {
            return;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i].suppressLegacyPrimitiveBlocks)
            {
                AlignStageBlocksBottomToOrigin(stages[i]);
            }
            else
            {
                AlignStageBlocksAftToOrigin(stages[i]);
            }
        }

        ComputeStageMountOffsets(stages);
        AssignBlockMetadata(stages);

        for (int i = 0; i < stages.Count; i++)
        {
            NormalizeEngineBlockMetadata(stages[i]);
            ReconcileScalarsFromBlocks(stages[i]);
        }
    }

    public static void ReconcileScalarsFromBlocks(StageData stage)
    {
        if (stage?.blocks == null || stage.blocks.Count == 0)
        {
            return;
        }

        double dryMass = 0.0;
        double fuelMass = 0.0;
        double maxFuelMass = 0.0;
        double thrust = 0.0;
        double isp = 0.0;

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (block == null || block.isDestroyed)
            {
                continue;
            }

            dryMass += block.dryMass;

            if (block.isFuelTank)
            {
                fuelMass += block.fuelMass;
                maxFuelMass += Math.Max(0.0, block.fuelCapacity);
            }

            if (!block.IsEngineBlock())
            {
                continue;
            }

            thrust += Math.Max(0.0, block.maxThrustNewtons);

            if (block.ispSeconds > isp)
            {
                isp = block.ispSeconds;
            }
        }

        stage.dryMass = dryMass;
        stage.fuelMass = fuelMass;

        if (maxFuelMass > 0.0)
        {
            stage.maxFuelMass = maxFuelMass;
        }
        else if (stage.maxFuelMass <= 0.0)
        {
            stage.maxFuelMass = fuelMass;
        }

        if (thrust > 0.0)
        {
            stage.thrust = thrust;
        }

        if (isp > 0.0)
        {
            stage.isp = isp;
        }
    }

    static void NormalizeEngineBlockMetadata(StageData stage)
    {
        if (stage?.blocks == null || stage.blocks.Count == 0)
        {
            return;
        }

        int engineCount = 0;
        double blockThrustSum = 0.0;

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (block == null || block.isDestroyed || !block.IsEngineBlock())
            {
                continue;
            }

            engineCount++;
            blockThrustSum += Math.Max(0.0, block.maxThrustNewtons);
        }

        if (engineCount == 0)
        {
            return;
        }

        double fallbackThrustPerEngine = 0.0;

        if (blockThrustSum <= 0.0 && stage.thrust > 0.0)
        {
            fallbackThrustPerEngine = stage.thrust / engineCount;
        }

        double fallbackIsp = stage.isp > 0.0 ? stage.isp : 300.0;

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (block == null || block.isDestroyed || !block.IsEngineBlock())
            {
                continue;
            }

            if (block.maxThrustNewtons <= 0.0 && fallbackThrustPerEngine > 0.0)
            {
                block.maxThrustNewtons = fallbackThrustPerEngine;
            }

            if (block.ispSeconds <= 0.0)
            {
                block.ispSeconds = fallbackIsp;
            }

            if (block.thrustDirection.LengthSquared() > 1e-12)
            {
                block.thrustDirection = block.thrustDirection.Normalize();

                if (block.thrustDirection.y < -0.5)
                {
                    block.thrustDirection = block.thrustDirection.Multiply(-1.0);
                }
            }
            else
            {
                block.thrustDirection = SimVector3.UnitY();
            }
        }

        RepairStageThrustAxisIfPerpendicularToUp(stage);
    }

    /// <summary>
    /// Vessel body frame is Y-up (see <c>VabBlueprintExporter</c>): pad orientation points local
    /// +Y at the surface normal, so a stage whose summed engine thrust has no +Y component can
    /// never lift off — the pad liftoff check only sees the vertical component. Ship files saved
    /// under an older X- or Z-stacked convention look exactly like that, so snap them back to
    /// +Y instead of leaving the vessel stuck on the pad with the engines firing sideways.
    /// </summary>
    static void RepairStageThrustAxisIfPerpendicularToUp(StageData stage)
    {
        SimVector3 axisSum = SimVector3.Zero();

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (block == null || block.isDestroyed || !block.IsEngineBlock())
            {
                continue;
            }

            axisSum = axisSum.Add(
                block.thrustDirection.Multiply(Math.Max(0.0, block.maxThrustNewtons))
            );
        }

        if (axisSum.LengthSquared() <= 1e-12)
        {
            return;
        }

        const double MinUsableVerticalThrustFraction = 0.1;

        if (axisSum.Normalize().y >= MinUsableVerticalThrustFraction)
        {
            return;
        }

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (block == null || block.isDestroyed || !block.IsEngineBlock())
            {
                continue;
            }

            block.thrustDirection = SimVector3.UnitY();
        }
    }

    public static void ComputeStageMountOffsets(List<StageData> stages)
    {
        if (stages == null || stages.Count == 0)
        {
            return;
        }

        stages[0].stageMountOffset = SimVector3.Zero();

        for (int i = 1; i < stages.Count; i++)
        {
            GetStageZExtents(stages[i - 1], out _, out double previousMaxZ);
            double mountZ = stages[i - 1].stageMountOffset.z + previousMaxZ;

            stages[i].stageMountOffset = new SimVector3(0.0, 0.0, mountZ);
        }
    }

    public static void AssignBlockMetadata(List<StageData> stages)
    {
        if (stages == null)
        {
            return;
        }

        int nextBlockId = 1;

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (block.blockId <= 0)
                {
                    block.blockId = nextBlockId++;
                }

                if (block.structuralStrength <= 0.0)
                {
                    block.structuralStrength = ShipBlock.GetDefaultStructuralStrength(
                        block.blockType
                    );
                }
            }
        }
    }

    public static void IntegrateDockedPartnerIntoHostStages(
        Vessel hostVessel,
        Vessel partnerVessel,
        StageData partnerStage
    )
    {
        if (
            hostVessel == null ||
            partnerVessel == null ||
            hostVessel.stages == null ||
            hostVessel.stages.Count == 0 ||
            partnerStage == null
        )
        {
            return;
        }

        StageData hostStage = hostVessel.stages[hostVessel.stages.Count - 1];

        if (hostStage.blocks == null)
        {
            hostStage.blocks = new List<ShipBlock>();
        }

        RemoveDockingPortBlocks(hostStage);

        if (partnerStage.blocks == null)
        {
            return;
        }

        for (int i = 0; i < partnerStage.blocks.Count; i++)
        {
            ShipBlock block = partnerStage.blocks[i];

            if (block.isDockingPort || block.blockType == ShipBlockType.DockingPort)
            {
                continue;
            }

            SimVector3 partnerBlockVesselLocal = partnerStage.stageMountOffset.Add(
                block.localOffset
            );
            SimVector3 blockWorld = partnerVessel.WorldFromVesselLocal(
                partnerBlockVesselLocal
            );
            SimVector3 hostBlockVesselLocal = hostVessel.VesselLocalFromWorld(blockWorld);
            ShipBlock mergedBlock = block.Copy();
            mergedBlock.localOffset = hostBlockVesselLocal.Subtract(
                hostStage.stageMountOffset
            );
            mergedBlock.thrustDirection = hostVessel.DirectionLocalFromWorld(
                partnerVessel.DirectionWorldFromLocal(block.thrustDirection)
            );

            if (mergedBlock.thrustDirection.LengthSquared() > 1e-12)
            {
                mergedBlock.thrustDirection = mergedBlock.thrustDirection.Normalize();
            }

            mergedBlock.id = "docked_" + block.id;
            hostStage.blocks.Add(mergedBlock);
            hostStage.dryMass += mergedBlock.dryMass;
        }

        hostStage.fuelMass += partnerStage.fuelMass;
        hostStage.maxFuelMass += partnerStage.maxFuelMass;

        hostVessel.stages.Clear();
        hostVessel.stages.Add(hostStage);
        AssignBlockMetadata(hostVessel.stages);
    }

    static bool TryGetDockingPortCenter(StageData stage, out SimVector3 portCenter)
    {
        if (stage?.blocks == null)
        {
            portCenter = SimVector3.Zero();
            return false;
        }

        return TryGetDockingPortCenterFromBlocks(stage.blocks, out portCenter);
    }

    static bool TryGetDockingPortCenterFromBlocks(
        List<ShipBlock> blocks,
        out SimVector3 portCenter
    )
    {
        portCenter = SimVector3.Zero();

        if (blocks == null)
        {
            return false;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            ShipBlock block = blocks[i];

            if (block.isDockingPort || block.blockType == ShipBlockType.DockingPort)
            {
                portCenter = block.localOffset;
                return true;
            }
        }

        return false;
    }

    static void RemoveDockingPortBlocks(StageData stage)
    {
        if (stage?.blocks == null)
        {
            return;
        }

        for (int i = stage.blocks.Count - 1; i >= 0; i--)
        {
            ShipBlock block = stage.blocks[i];

            if (block.isDockingPort || block.blockType == ShipBlockType.DockingPort)
            {
                stage.dryMass = Math.Max(0.0, stage.dryMass - block.dryMass);
                stage.blocks.RemoveAt(i);
            }
        }
    }

    public static void AlignStageBlocksBottomToOrigin(StageData stage)
    {
        if (stage.blocks == null || stage.blocks.Count == 0)
        {
            return;
        }

        GetStageYExtents(stage, out double minY, out _);

        if (Math.Abs(minY) < 1e-9)
        {
            return;
        }

        SimVector3 shift = new SimVector3(0.0, -minY, 0.0);

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            stage.blocks[i].localOffset = stage.blocks[i].localOffset.Add(shift);
        }
    }

    static void GetStageYExtents(StageData stage, out double minY, out double maxY)
    {
        minY = double.PositiveInfinity;
        maxY = double.NegativeInfinity;

        if (stage.blocks == null || stage.blocks.Count == 0)
        {
            minY = 0.0;
            maxY = 0.0;
            return;
        }

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (
                block.blockType == ShipBlockType.RcsThruster ||
                block.blockType == ShipBlockType.DockingPort ||
                block.isDockingPort
            )
            {
                continue;
            }

            double halfHeight = block.dimensions.y * 0.5;
            double blockMinY = block.localOffset.y - halfHeight;
            double blockMaxY = block.localOffset.y + halfHeight;
            minY = Math.Min(minY, blockMinY);
            maxY = Math.Max(maxY, blockMaxY);
        }

        if (double.IsPositiveInfinity(minY))
        {
            minY = 0.0;
            maxY = 0.0;
        }
    }

    public static void AlignStageBlocksAftToOrigin(StageData stage)
    {
        if (stage.blocks == null || stage.blocks.Count == 0)
        {
            return;
        }

        GetStageZExtents(stage, out double minZ, out _);

        if (Math.Abs(minZ) < 1e-9)
        {
            return;
        }

        SimVector3 shift = new SimVector3(0.0, 0.0, -minZ);

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            stage.blocks[i].localOffset = stage.blocks[i].localOffset.Add(shift);
        }
    }

    static void GetStageZExtents(StageData stage, out double minZ, out double maxZ)
    {
        GetStageZExtents(stage, includeDockingPorts: true, out minZ, out maxZ);
    }

    static bool IsHullStructureBlock(ShipBlock block)
    {
        return
            block.blockType == ShipBlockType.FuelTank ||
            block.blockType == ShipBlockType.Engine ||
            block.blockType == ShipBlockType.Adapter;
    }

    static void GetStageHullZExtents(StageData stage, out double minZ, out double maxZ)
    {
        minZ = -TankLengthZ * 0.5;
        maxZ = TankLengthZ * 0.5;

        if (stage.blocks == null || stage.blocks.Count == 0)
        {
            return;
        }

        bool hasExtent = false;

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (!IsHullStructureBlock(block))
            {
                continue;
            }

            double halfLength = block.dimensions.z * 0.5;
            double blockMinZ = block.localOffset.z - halfLength;
            double blockMaxZ = block.localOffset.z + halfLength;

            if (!hasExtent)
            {
                minZ = blockMinZ;
                maxZ = blockMaxZ;
                hasExtent = true;
            }
            else
            {
                minZ = Math.Min(minZ, blockMinZ);
                maxZ = Math.Max(maxZ, blockMaxZ);
            }
        }
    }

    static void GetStageZExtentsExcludingPorts(
        StageData stage,
        out double minZ,
        out double maxZ
    )
    {
        GetStageZExtents(stage, includeDockingPorts: false, out minZ, out maxZ);
    }

    static void GetStageZExtents(
        StageData stage,
        bool includeDockingPorts,
        out double minZ,
        out double maxZ
    )
    {
        minZ = -TankLengthZ * 0.5;
        maxZ = TankLengthZ * 0.5;

        if (stage.blocks == null || stage.blocks.Count == 0)
        {
            return;
        }

        bool hasExtent = false;

        for (int i = 0; i < stage.blocks.Count; i++)
        {
            ShipBlock block = stage.blocks[i];

            if (
                !includeDockingPorts &&
                (block.isDockingPort || block.blockType == ShipBlockType.DockingPort)
            )
            {
                continue;
            }

            double halfLength = block.dimensions.z * 0.5;
            double blockMinZ = block.localOffset.z - halfLength;
            double blockMaxZ = block.localOffset.z + halfLength;

            if (!hasExtent)
            {
                minZ = blockMinZ;
                maxZ = blockMaxZ;
                hasExtent = true;
            }
            else
            {
                minZ = Math.Min(minZ, blockMinZ);
                maxZ = Math.Max(maxZ, blockMaxZ);
            }
        }
    }

    public static StageData CreateBooster(double fuelFraction = 1.0)
    {
        const double maxFuel = 12.0;
        double clampedFraction = Math.Clamp(fuelFraction, 0.0, 1.0);

        return new StageData(
            "S1 Booster",
            10.0,
            maxFuel * clampedFraction,
            maxFuel,
            80.0,
            250.0,
            0.25
        );
    }

    public static StageData CreateOrbiter(double fuelFraction = 1.0)
    {
        const double maxFuel = 8.0;
        double clampedFraction = Math.Clamp(fuelFraction, 0.0, 1.0);

        return new StageData(
            "S2 Orbiter",
            5.0,
            maxFuel * clampedFraction,
            maxFuel,
            60.0,
            310.0,
            0.22
        );
    }

    public static StageData CreateUpperStage(double fuelFraction = 1.0)
    {
        const double maxFuel = 5.0;
        double clampedFraction = Math.Clamp(fuelFraction, 0.0, 1.0);

        return new StageData(
            "S3 Upper",
            3.0,
            maxFuel * clampedFraction,
            maxFuel,
            40.0,
            320.0,
            0.2
        );
    }

    public static StageData CreateOrbitalUpperStage(
        double dryMass,
        double fuelMass,
        double thrust,
        double isp = 310.0,
        double dragCoefficient = 0.22
    )
    {
        return new StageData(
            "S2 Orbiter",
            dryMass,
            fuelMass,
            fuelMass,
            thrust,
            isp,
            dragCoefficient
        );
    }

    public static StageData CreateLaunchStage(
        string name,
        double dryMass,
        double fuelMass,
        double thrust,
        double massFlowRateKgPerSec,
        double dragCoefficient = 0.25
    )
    {
        double isp = 0.0;

        if (massFlowRateKgPerSec > 0.0)
        {
            isp = thrust / (
                massFlowRateKgPerSec * PhysicsConstants.StandardGravity
            );
        }

        return new StageData(
            name,
            dryMass,
            fuelMass,
            fuelMass,
            thrust,
            isp,
            dragCoefficient
        );
    }

    public static StageData CreateSurfaceBooster(
        double dryMass,
        double fuelMass,
        double thrust,
        double burnSeconds
    )
    {
        double consumption = fuelMass / (thrust * burnSeconds);
        double isp = 1.0 / (consumption * PhysicsConstants.StandardGravity);

        return new StageData(
            "S1 Booster",
            dryMass,
            fuelMass,
            fuelMass,
            thrust,
            isp,
            0.25
        );
    }
}
