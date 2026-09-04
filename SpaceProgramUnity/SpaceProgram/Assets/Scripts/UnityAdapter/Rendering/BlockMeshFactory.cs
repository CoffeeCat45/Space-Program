using UnityEngine;

static class BlockMeshFactory
{
    const string DockingRingChildName = "DockingRing";
    const string DockingStemChildName = "PortStem";

    static readonly Color[] StageHullColors =
    {
        new Color(0.85f, 0.88f, 0.95f),
        new Color(0.72f, 0.78f, 0.88f),
        new Color(0.62f, 0.68f, 0.78f),
        new Color(0.55f, 0.60f, 0.70f),
    };

    static readonly Quaternion FuselageCylinderMeshRotation =
        Quaternion.Euler(90.0f, 0.0f, 0.0f);

    public static bool UsesIntrinsicBlockScale(ShipBlockType blockType)
    {
        return blockType == ShipBlockType.DockingPort;
    }

    public static GameObject CreateBlock(ShipBlockView block)
    {
        GameObject blockObject;
        string objectName = GetBlockObjectName(block);

        switch (block.blockType)
        {
            case ShipBlockType.Adapter:
                blockObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                break;
            case ShipBlockType.DockingPort:
                blockObject = CreateDockingPortModule(block);
                break;
            case ShipBlockType.Engine:
            case ShipBlockType.FuelTank:
            default:
                blockObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                break;
            case ShipBlockType.RcsThruster:
                blockObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                break;
        }

        blockObject.name = objectName;

        if (block.blockType != ShipBlockType.DockingPort)
        {
            ApplyBlockTransform(blockObject, block);
            SetBlockColor(blockObject, block);
        }

        DestroyCollider(blockObject);
        return blockObject;
    }

    public static Vector3 ToUnityLocalScale(SimVector3 simDimensions)
    {
        return new Vector3(
            (float)simDimensions.x,
            (float)(simDimensions.z * 0.5),
            (float)simDimensions.y
        );
    }

    static float CylinderHeightScale(float unityLength)
    {
        return Mathf.Max(0.005f, unityLength * 0.5f);
    }

    static GameObject CreateDockingPortModule(ShipBlockView block)
    {
        GameObject blockObject = new GameObject(GetBlockObjectName(block));
        float diameterScale = Mathf.Max(0.1f, (float)block.dimensions.x);
        float lengthUnity = Mathf.Max(0.1f, (float)block.dimensions.z);
        float ringThicknessUnity = 0.02f;
        float aftZ = -lengthUnity * 0.5f;
        float noseZ = lengthUnity * 0.5f;
        float ringHalfHeight = ringThicknessUnity * 0.5f;
        float bodyTopZ = noseZ - ringThicknessUnity;
        float bodySpanUnity = Mathf.Max(0.01f, bodyTopZ - aftZ);

        GameObject stemObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stemObject.name = DockingStemChildName;
        stemObject.transform.SetParent(blockObject.transform, false);
        stemObject.transform.localScale = new Vector3(
            diameterScale,
            CylinderHeightScale(bodySpanUnity),
            diameterScale
        );
        stemObject.transform.localPosition = new Vector3(
            0.0f,
            0.0f,
            aftZ + bodySpanUnity * 0.5f
        );
        stemObject.transform.localRotation = FuselageCylinderMeshRotation;
        SetRendererColor(
            stemObject,
            Color.Lerp(
                GetStageHullColor(block),
                new Color(0.45f, 0.48f, 0.52f),
                0.35f
            )
        );
        DestroyCollider(stemObject);

        GameObject ringObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringObject.name = DockingRingChildName;
        ringObject.transform.SetParent(blockObject.transform, false);
        ringObject.transform.localScale = new Vector3(
            diameterScale * 1.05f,
            CylinderHeightScale(ringThicknessUnity),
            diameterScale * 1.05f
        );
        ringObject.transform.localPosition = new Vector3(
            0.0f,
            0.0f,
            noseZ - ringHalfHeight
        );
        ringObject.transform.localRotation = FuselageCylinderMeshRotation;
        SetRendererColor(ringObject, new Color(1.0f, 0.88f, 0.2f));
        DestroyCollider(ringObject);

        blockObject.transform.localRotation = Quaternion.identity;
        blockObject.transform.localScale = Vector3.one;
        return blockObject;
    }

    public static string GetBlockObjectName(ShipBlockView block)
    {
        if (block.blockId > 0)
        {
            return "Block_" + block.blockId;
        }

        return "Block_" + block.stageIndex + "_" + block.id;
    }

    public static void ApplyBlockTransform(GameObject blockObject, ShipBlockView block)
    {
        blockObject.transform.localPosition = ToUnityLocalPosition(block.localOffset);
        blockObject.transform.localRotation = GetBlockLocalRotation(block.blockType);
        blockObject.transform.localScale = ToUnityLocalScale(block.dimensions);
    }

    public static void ApplyBlockPositionFromCom(
        Transform blockTransform,
        ShipBlockView block,
        SimVector3 centerOfMassLocal
    )
    {
        SimVector3 offsetFromCom = block.localOffset.Subtract(centerOfMassLocal);
        blockTransform.localPosition = ToUnityLocalPosition(offsetFromCom);
    }

    public static Vector3 ToUnityLocalPosition(SimVector3 simLocalOffset)
    {
        return new Vector3(
            (float)simLocalOffset.x,
            (float)simLocalOffset.y,
            (float)simLocalOffset.z
        );
    }

    static Quaternion GetBlockLocalRotation(ShipBlockType blockType)
    {
        if (
            blockType == ShipBlockType.Adapter ||
            blockType == ShipBlockType.RcsThruster ||
            blockType == ShipBlockType.DockingPort
        )
        {
            return Quaternion.identity;
        }

        return FuselageCylinderMeshRotation;
    }

    static void SetBlockColor(GameObject blockObject, ShipBlockView block)
    {
        Renderer renderer = blockObject.GetComponent<Renderer>();

        if (renderer == null)
        {
            return;
        }

        RuntimeMaterialFactory.ApplyColor(renderer, GetBlockColor(block));
    }

    static void SetRendererColor(GameObject blockObject, Color color)
    {
        Renderer renderer = blockObject.GetComponent<Renderer>();
        RuntimeMaterialFactory.ApplyColor(renderer, color);
    }

    static Color GetStageHullColor(ShipBlockView block)
    {
        return StageHullColors[Mathf.Abs(block.stageIndex) % StageHullColors.Length];
    }

    static Color GetBlockColor(ShipBlockView block)
    {
        Color stageColor = GetStageHullColor(block);

        switch (block.blockType)
        {
            case ShipBlockType.Engine:
                return Color.Lerp(stageColor, new Color(0.35f, 0.38f, 0.42f), 0.55f);
            case ShipBlockType.RcsThruster:
                return new Color(0.25f, 0.85f, 0.95f);
            case ShipBlockType.Adapter:
                return Color.Lerp(stageColor, new Color(0.55f, 0.55f, 0.58f), 0.35f);
            case ShipBlockType.DockingPort:
                return new Color(1.0f, 0.88f, 0.2f);
            case ShipBlockType.FuelTank:
            default:
                return stageColor;
        }
    }

    static void DestroyCollider(GameObject blockObject)
    {
        Collider collider = blockObject.GetComponent<Collider>();

        if (collider != null)
        {
            Object.Destroy(collider);
        }
    }
}
