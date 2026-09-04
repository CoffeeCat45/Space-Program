using System.Collections.Generic;
using UnityEngine;

public class DebrisView : MonoBehaviour
{
    public int orbitPointCount = 360;
    public float labelOffsetY = 0.32f;
    public float labelCharacterSize = 0.05f;

    static readonly Color DebrisOrbitColor = new Color(0.62f, 0.62f, 0.62f, 0.85f);
    static readonly Color DebrisLabelColor = new Color(0.82f, 0.82f, 0.82f, 1.0f);
    const string BlocksContainerName = "Blocks";

    Simulation simulation;
    SimulationRunner runner;
    readonly Dictionary<string, DebrisVisual> visuals =
        new Dictionary<string, DebrisVisual>();

    internal void SetSimulation(Simulation newSimulation)
    {
        simulation = newSimulation;
    }

    internal void SetRunner(SimulationRunner newRunner)
    {
        runner = newRunner;
    }

    void LateUpdate()
    {
        if (simulation == null || runner == null)
        {
            return;
        }

        DebrisState[] debrisStates = simulation.GetDebrisStates();
        HashSet<string> activeIds = new HashSet<string>();

        for (int i = 0; i < debrisStates.Length; i++)
        {
            DebrisState debrisState = debrisStates[i];
            activeIds.Add(debrisState.id);
            bool isNewVisual;
            DebrisVisual visual = GetOrCreateVisual(debrisState.id, out isNewVisual);
            UpdateVisual(visual, debrisState, i, isNewVisual);
        }

        List<string> staleIds = new List<string>();

        foreach (KeyValuePair<string, DebrisVisual> entry in visuals)
        {
            if (!activeIds.Contains(entry.Key))
            {
                staleIds.Add(entry.Key);
            }
        }

        for (int i = 0; i < staleIds.Count; i++)
        {
            DestroyVisual(staleIds[i]);
        }
    }

    DebrisVisual GetOrCreateVisual(string debrisId, out bool isNewVisual)
    {
        if (visuals.TryGetValue(debrisId, out DebrisVisual existingVisual))
        {
            isNewVisual = false;
            return existingVisual;
        }

        isNewVisual = true;
        GameObject rootObject = new GameObject("Debris " + debrisId);
        rootObject.transform.SetParent(transform);

        GameObject blocksContainer = new GameObject(BlocksContainerName);
        blocksContainer.transform.SetParent(rootObject.transform);
        blocksContainer.transform.localPosition = Vector3.zero;
        blocksContainer.transform.localRotation = Quaternion.identity;
        blocksContainer.transform.localScale = Vector3.one;

        GameObject orbitObject = new GameObject("Orbit");
        orbitObject.transform.SetParent(rootObject.transform);
        LineRenderer orbitLine = orbitObject.AddComponent<LineRenderer>();
        orbitLine.useWorldSpace = true;
        orbitLine.positionCount = 0;
        orbitLine.startWidth = 0.03f;
        orbitLine.endWidth = 0.03f;
        orbitLine.material = RuntimeMaterialFactory.CreateLineMaterial();
        orbitLine.startColor = DebrisOrbitColor;
        orbitLine.endColor = DebrisOrbitColor;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(rootObject.transform);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.fontSize = 32;
        label.characterSize = labelCharacterSize;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = DebrisLabelColor;

        DebrisVisual visual = new DebrisVisual
        {
            rootObject = rootObject,
            blocksContainer = blocksContainer.transform,
            orbitLine = orbitLine,
            label = label,
            blockCount = -1,
        };
        visuals[debrisId] = visual;
        return visual;
    }

    void UpdateVisual(
        DebrisVisual visual,
        DebrisState debrisState,
        int debrisIndex,
        bool playSeparationBurst
    )
    {
        Vector3 worldPosition = runner.ToWorldPosition(debrisState.position);
        visual.rootObject.transform.position = worldPosition;
        visual.rootObject.transform.rotation = BodyView.OrientationToUnityRotation(
            SimQuaternion.FromYawRadians(debrisState.headingRadians)
        );

        if (playSeparationBurst)
        {
            ExplosionEffects.PlayStageSeparation(
                worldPosition,
                runner.GetVisualPositionScale()
            );
        }

        int blockCount = debrisState.blocks == null ? 0 : debrisState.blocks.Length;

        if (blockCount != visual.blockCount)
        {
            RebuildDebrisBlocks(visual, debrisState);
            visual.blockCount = blockCount;
        }
        else
        {
            UpdateDebrisBlockTransforms(visual, debrisState);
        }

        visual.label.transform.position =
            worldPosition + new Vector3(0.0f, labelOffsetY, 0.0f);
        visual.label.text =
            Localization.Translate("Stage jettisoned") +
            ": " +
            debrisState.stageName;

        SimVector3[] orbitPoints =
            simulation.GetDebrisOrbitPathRelativeToActivePrimary(
                debrisIndex,
                orbitPointCount
            );
        Vector3[] worldOrbitPoints = new Vector3[orbitPoints.Length];

        for (int i = 0; i < orbitPoints.Length; i++)
        {
            worldOrbitPoints[i] = runner.ToWorldPosition(orbitPoints[i]);
        }

        visual.orbitLine.positionCount = worldOrbitPoints.Length;
        visual.orbitLine.SetPositions(worldOrbitPoints);
        visual.rootObject.SetActive(true);
    }

    void RebuildDebrisBlocks(DebrisVisual visual, DebrisState debrisState)
    {
        for (int i = visual.blocksContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(visual.blocksContainer.GetChild(i).gameObject);
        }

        ShipBlockView[] blocks = debrisState.blocks;

        if (blocks == null)
        {
            return;
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].blockType == ShipBlockType.RcsThruster)
            {
                continue;
            }

            GameObject blockObject = BlockMeshFactory.CreateBlock(blocks[i]);
            blockObject.transform.SetParent(visual.blocksContainer, false);
            BlockMeshFactory.ApplyBlockPositionFromCom(
                blockObject.transform,
                blocks[i],
                debrisState.centerOfMassLocal
            );
            blockObject.transform.localScale = BlockMeshFactory.ToUnityLocalScale(
                blocks[i].dimensions
            );
        }
    }

    void UpdateDebrisBlockTransforms(DebrisVisual visual, DebrisState debrisState)
    {
        if (debrisState.blocks == null)
        {
            return;
        }

        for (int i = 0; i < debrisState.blocks.Length; i++)
        {
            string blockName = BlockMeshFactory.GetBlockObjectName(debrisState.blocks[i]);
            Transform blockTransform = visual.blocksContainer.Find(blockName);

            if (blockTransform == null)
            {
                continue;
            }

            SimVector3 offsetFromCom = debrisState.blocks[i].localOffset.Subtract(
                debrisState.centerOfMassLocal
            );
            blockTransform.localPosition = BlockMeshFactory.ToUnityLocalPosition(
                offsetFromCom
            );
            blockTransform.localScale = BlockMeshFactory.ToUnityLocalScale(
                debrisState.blocks[i].dimensions
            );
        }
    }

    void DestroyVisual(string debrisId)
    {
        if (!visuals.TryGetValue(debrisId, out DebrisVisual visual))
        {
            return;
        }

        if (visual.rootObject != null)
        {
            Destroy(visual.rootObject);
        }

        visuals.Remove(debrisId);
    }

    class DebrisVisual
    {
        public GameObject rootObject;
        public Transform blocksContainer;
        public LineRenderer orbitLine;
        public TextMesh label;
        public int blockCount;
    }
}
