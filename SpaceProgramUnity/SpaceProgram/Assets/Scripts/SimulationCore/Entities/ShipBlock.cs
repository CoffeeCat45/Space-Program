using System;

enum ShipBlockType
{
    FuelTank,
    Engine,
    Adapter,
    RcsThruster,
    DockingPort
}

class ShipBlock
{
    public int blockId;
    public string id;
    public string partTemplateId;
    public string parentBlockId;
    public bool hasVabAttach;
    public SimVector3 attachLocalPosition;
    public SimQuaternion attachLocalRotation;
    public SimQuaternion localRotation;
    public ShipBlockType blockType;
    public SimVector3 localOffset;
    public SimVector3 dimensions;
    public double dryMass;
    public double fuelMass;
    public double fuelCapacity;
    public bool isFuelTank;
    public bool isEngine;
    public bool isDockingPort;
    public bool isDestroyed;
    public double structuralStrength;
    public SimVector3 thrustDirection;
    /// <summary>Thrust application point in part-local space (from prefab thrustTransform).</summary>
    public SimVector3 thrustLocalOffset;
    /// <summary>Vacuum max thrust for this engine block in newtons.</summary>
    public double maxThrustNewtons;
    /// <summary>Vacuum specific impulse in seconds (engines only).</summary>
    public double ispSeconds;
    public double rcsThrust;

    public ShipBlock()
    {
        blockId = 0;
        id = string.Empty;
        partTemplateId = string.Empty;
        parentBlockId = string.Empty;
        hasVabAttach = false;
        attachLocalPosition = SimVector3.Zero();
        attachLocalRotation = SimQuaternion.Identity();
        localRotation = SimQuaternion.Identity();
        blockType = ShipBlockType.FuelTank;
        localOffset = SimVector3.Zero();
        dimensions = SimVector3.Zero();
        thrustDirection = new SimVector3(0.0, 0.0, -1.0);
        structuralStrength = GetDefaultStructuralStrength(ShipBlockType.FuelTank);
    }

    public static double GetDefaultStructuralStrength(ShipBlockType blockType)
    {
        switch (blockType)
        {
            case ShipBlockType.FuelTank:
                return 14.0;
            case ShipBlockType.Engine:
                return 10.0;
            case ShipBlockType.Adapter:
                return 12.0;
            case ShipBlockType.RcsThruster:
                return 6.0;
            case ShipBlockType.DockingPort:
                return 10.0;
            default:
                return 12.0;
        }
    }

    public double GetMass()
    {
        double mass = dryMass;

        if (isFuelTank)
        {
            mass += fuelMass;
        }

        return mass;
    }

    public ShipBlock Copy()
    {
        return new ShipBlock
        {
            blockId = blockId,
            id = id,
            partTemplateId = partTemplateId,
            parentBlockId = parentBlockId,
            hasVabAttach = hasVabAttach,
            attachLocalPosition = attachLocalPosition,
            attachLocalRotation = attachLocalRotation,
            localRotation = localRotation,
            blockType = blockType,
            localOffset = localOffset,
            dimensions = dimensions,
            dryMass = dryMass,
            fuelMass = fuelMass,
            fuelCapacity = fuelCapacity,
            isFuelTank = isFuelTank,
            isEngine = isEngine,
            isDockingPort = isDockingPort,
            isDestroyed = isDestroyed,
            structuralStrength = structuralStrength,
            thrustDirection = thrustDirection,
            thrustLocalOffset = thrustLocalOffset,
            maxThrustNewtons = maxThrustNewtons,
            ispSeconds = ispSeconds,
            rcsThrust = rcsThrust
        };
    }

    public bool IsEngineBlock()
    {
        return isEngine || blockType == ShipBlockType.Engine;
    }
}

struct ShipBlockView
{
    public int blockId;
    public string id;
    public string partTemplateId;
    public string parentBlockId;
    public bool hasVabAttach;
    public SimVector3 attachLocalPosition;
    public SimQuaternion attachLocalRotation;
    public SimQuaternion localRotation;
    public ShipBlockType blockType;
    public SimVector3 localOffset;
    public SimVector3 dimensions;
    public SimVector3 thrustLocalOffset;
    public int stageIndex;
    public double mass;

    public ShipBlockView(
        int blockId,
        string id,
        string partTemplateId,
        string parentBlockId,
        bool hasVabAttach,
        SimVector3 attachLocalPosition,
        SimQuaternion attachLocalRotation,
        SimQuaternion localRotation,
        ShipBlockType blockType,
        SimVector3 localOffset,
        SimVector3 dimensions,
        SimVector3 thrustLocalOffset,
        int stageIndex,
        double mass
    )
    {
        this.blockId = blockId;
        this.id = id;
        this.partTemplateId = partTemplateId ?? string.Empty;
        this.parentBlockId = parentBlockId ?? string.Empty;
        this.hasVabAttach = hasVabAttach;
        this.attachLocalPosition = attachLocalPosition;
        this.attachLocalRotation = attachLocalRotation;
        this.localRotation = localRotation;
        this.blockType = blockType;
        this.localOffset = localOffset;
        this.dimensions = dimensions;
        this.thrustLocalOffset = thrustLocalOffset;
        this.stageIndex = stageIndex;
        this.mass = mass;
    }
}
