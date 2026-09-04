using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds in-flight vessel visuals from VAB parent-child attach data (KSP-style tree spawn).
/// </summary>
static class FlightVesselSpawner
{
    public const string PartTreeName = "PartTree";

    public static void EnsureFlightSceneReady(SimulationRunner sessionRunner)
    {
        if (FlightSessionSceneUtility.CanLoadBootstrapScene())
        {
            FlightSceneIsolation.PurgeEditorArtifacts(sessionRunner);
        }
        else
        {
            FlightSceneIsolation.PurgeEditorSessionArtifacts(sessionRunner);
        }

        FlightSceneIsolation.PurgeStrayVesselBlocks(sessionRunner);
    }

    public static bool HasVabHierarchy(ShipBlockView[] blocks)
    {
        if (blocks == null)
        {
            return false;
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].hasVabAttach && !string.IsNullOrWhiteSpace(blocks[i].partTemplateId))
            {
                return true;
            }
        }

        return false;
    }

    public static Transform RebuildPartTree(Transform blocksContainer, ShipBlockView[] blocks)
    {
        if (blocksContainer == null)
        {
            return null;
        }

        Transform existingTree = blocksContainer.Find(PartTreeName);

        if (existingTree != null)
        {
            Object.Destroy(existingTree.gameObject);
        }

        if (!HasVabHierarchy(blocks))
        {
            return null;
        }

        GameObject partTreeObject = new GameObject(PartTreeName);
        partTreeObject.transform.SetParent(blocksContainer, false);
        partTreeObject.transform.localPosition = Vector3.zero;
        partTreeObject.transform.localRotation = Quaternion.identity;
        partTreeObject.transform.localScale = Vector3.one;

        Dictionary<string, GameObject> lookup = new Dictionary<string, GameObject>();
        bool anySpawned = false;

        for (int pass = 0; pass < blocks.Length; pass++)
        {
            bool spawnedThisPass = false;

            for (int i = 0; i < blocks.Length; i++)
            {
                ShipBlockView block = blocks[i];

                if (
                    !block.hasVabAttach ||
                    string.IsNullOrWhiteSpace(block.partTemplateId) ||
                    block.blockType == ShipBlockType.DockingPort ||
                    (!string.IsNullOrWhiteSpace(block.id) && lookup.ContainsKey(block.id))
                )
                {
                    continue;
                }

                bool hasParent = !string.IsNullOrWhiteSpace(block.parentBlockId);

                if (hasParent && !lookup.ContainsKey(block.parentBlockId))
                {
                    continue;
                }

                if (!FlightPartVisualFactory.TryCreatePartVisual(block.partTemplateId, out GameObject partObject))
                {
                    continue;
                }

                Transform parentTransform = hasParent ?
                    lookup[block.parentBlockId].transform :
                    partTreeObject.transform;

                partObject.name = string.IsNullOrWhiteSpace(block.id) ?
                    BlockMeshFactory.GetBlockObjectName(block) :
                    block.id;
                partObject.transform.SetParent(parentTransform, false);
                partObject.transform.localPosition = ToVabUnityPosition(block.attachLocalPosition);
                partObject.transform.localRotation = ToVabUnityRotation(block.attachLocalRotation);
                partObject.transform.localScale = Vector3.one;

                if (!string.IsNullOrWhiteSpace(block.id))
                {
                    lookup[block.id] = partObject;
                }

                spawnedThisPass = true;
                anySpawned = true;
            }

            if (!spawnedThisPass)
            {
                break;
            }
        }

        if (!anySpawned)
        {
            Object.Destroy(partTreeObject);
            return null;
        }

        return partTreeObject.transform;
    }

    public static void ApplyVisualGroundingOffset(Transform blocksContainer)
    {
        if (blocksContainer == null)
        {
            return;
        }

        if (!TryGetLowestRendererLocalY(blocksContainer, out float lowestY))
        {
            return;
        }

        Vector3 localPosition = blocksContainer.localPosition;
        localPosition.y -= lowestY;
        blocksContainer.localPosition = localPosition;
    }

    public static bool TryGetLowestRendererLocalY(Transform root, out float lowestY)
    {
        lowestY = float.PositiveInfinity;
        bool found = false;
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z),
            };

            for (int c = 0; c < corners.Length; c++)
            {
                float localY = root.InverseTransformPoint(corners[c]).y;

                if (localY < lowestY)
                {
                    lowestY = localY;
                    found = true;
                }
            }
        }

        return found;
    }

    public static bool IsUnderPartTree(Transform blockTransform, Transform partTree)
    {
        if (blockTransform == null || partTree == null)
        {
            return false;
        }

        return blockTransform == partTree || blockTransform.IsChildOf(partTree);
    }

    /// <summary>
    /// Lowest <see cref="PartPrefabBuilder.NodeBottomName"/> along vessel up (KSP-style feet).
    /// </summary>
    public static bool TryGetLowestBottomAttachNodeWorldPosition(
        Transform blocksContainer,
        out Vector3 bottomNodeWorldPosition
    )
    {
        bottomNodeWorldPosition = Vector3.zero;

        if (blocksContainer == null)
        {
            return false;
        }

        Transform partTree = blocksContainer.Find(PartTreeName);
        Transform searchRoot = partTree != null ? partTree : blocksContainer;
        Transform shipRoot = blocksContainer.parent != null ?
            blocksContainer.parent :
            blocksContainer;
        Vector3 up = shipRoot.up;
        Vector3 origin = shipRoot.position;
        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        Transform bestNode = null;
        float lowestAlongUp = float.PositiveInfinity;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform nodeTransform = transforms[i];

            if (
                nodeTransform == null ||
                nodeTransform.name != PartPrefabBuilder.NodeBottomName
            )
            {
                continue;
            }

            float alongUp = Vector3.Dot(nodeTransform.position - origin, up);

            if (alongUp < lowestAlongUp)
            {
                lowestAlongUp = alongUp;
                bestNode = nodeTransform;
            }
        }

        if (bestNode == null)
        {
            return false;
        }

        bottomNodeWorldPosition = bestNode.position;
        return true;
    }

    public static bool TryGetLowestRendererWorldPositionAlongAxis(
        Transform blocksContainer,
        Vector3 upAxis,
        Vector3 axisOrigin,
        out Vector3 lowestWorldPosition
    )
    {
        lowestWorldPosition = Vector3.zero;

        if (blocksContainer == null)
        {
            return false;
        }

        Renderer[] renderers = blocksContainer.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return false;
        }

        Vector3 normalizedUp = upAxis.sqrMagnitude > 1e-8f ?
            upAxis.normalized :
            Vector3.up;
        bool found = false;
        float lowestAlongUp = float.PositiveInfinity;
        Vector3 bestWorld = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z),
            };

            for (int c = 0; c < corners.Length; c++)
            {
                float alongUp = Vector3.Dot(corners[c] - axisOrigin, normalizedUp);

                if (alongUp < lowestAlongUp)
                {
                    lowestAlongUp = alongUp;
                    bestWorld = corners[c];
                    found = true;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        lowestWorldPosition = bestWorld;
        return true;
    }

    static Vector3 ToVabUnityPosition(SimVector3 value)
    {
        return new Vector3((float)value.x, (float)value.y, (float)value.z);
    }

    static Quaternion ToVabUnityRotation(SimQuaternion value)
    {
        return new Quaternion(
            (float)value.x,
            (float)value.y,
            (float)value.z,
            (float)value.w
        );
    }
}
