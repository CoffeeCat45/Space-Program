#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds visual staging prefabs and VAB container prefabs with auto stack nodes and engine thrust.
/// </summary>
static class ReStockPartContainerBuilder
{
    internal const string VisualChildName = "Visual";

    struct MarkerSnapshot
    {
        public string Name;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public int NodeIndex;
    }

    internal static bool ImportPart(ReStockBatchParts.PartDefinition definition, bool preserveAuthoredMarkers)
    {
        if (definition == null)
        {
            return false;
        }

        ReStockBatchParts.MergeManifest(definition);
        ReimportMeshes(definition);

        if (!BuildVisualPrefab(definition))
        {
            return false;
        }

        return BuildOrUpdateContainer(definition, preserveAuthoredMarkers);
    }

    internal static void ReimportMeshes(ReStockBatchParts.PartDefinition definition)
    {
        if (definition.SplitSubmeshes)
        {
            for (int i = 0; i < definition.SubmeshNames.Length; i++)
            {
                ReStockImportPipeline.ImportModelAssetIfPresent(
                    ReStockBatchParts.GetSubmeshAssetPath(definition, definition.SubmeshNames[i])
                );
            }
        }
        else
        {
            ReStockImportPipeline.ImportModelAssetIfPresent(
                ReStockBatchParts.GetSingleModelAssetPath(definition)
            );
        }
    }

    internal static bool BuildVisualPrefab(ReStockBatchParts.PartDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        ReStockBatchParts.MergeManifest(definition);
        ReStockPartPaths.EnsurePartPrefabFolder(definition.Category, definition.PartId);

        string visualPath = ReStockPartPaths.GetVisualPrefabAssetPath(definition.Category, definition.PartId);
        string logicalModelPath = definition.SplitSubmeshes ?
            ReStockBatchParts.GetSplitModelFolderAssetPath(definition) :
            ReStockBatchParts.GetSingleModelAssetPath(definition);

        GameObject visualRoot = new GameObject(VisualChildName);
        bool built;

        if (definition.SplitSubmeshes)
        {
            built = AttachSplitMeshes(definition, visualRoot);
        }
        else
        {
            built = AttachSingleMesh(definition, visualRoot);
        }

        if (!built)
        {
            UnityEngine.Object.DestroyImmediate(visualRoot);
            return false;
        }

        ReStockImportPipeline.ApplySimpleVabPartSetup(visualRoot, logicalModelPath, definition);
        PrefabUtility.SaveAsPrefabAsset(visualRoot, visualPath);
        UnityEngine.Object.DestroyImmediate(visualRoot);
        return true;
    }

    static bool AttachSingleMesh(ReStockBatchParts.PartDefinition definition, GameObject visualRoot)
    {
        string modelAssetPath = ReStockBatchParts.GetSingleModelAssetPath(definition);
        GameObject modelRoot = ReStockImportPipeline.LoadImportedModelRoot(modelAssetPath);

        if (modelRoot == null)
        {
            Debug.LogWarning("ReStock: model not found for visual prefab: " + modelAssetPath);
            return false;
        }

        GameObject instance = UnityEngine.Object.Instantiate(modelRoot);
        instance.transform.SetParent(visualRoot.transform, false);
        instance.name = definition.PartId;
        return true;
    }

    static bool AttachSplitMeshes(ReStockBatchParts.PartDefinition definition, GameObject visualRoot)
    {
        int attached = 0;

        for (int i = 0; i < definition.SubmeshNames.Length; i++)
        {
            string meshName = definition.SubmeshNames[i];
            string submeshAssetPath = ReStockBatchParts.GetSubmeshAssetPath(definition, meshName);

            if (!ReStockImportPipeline.TryCreateEnginePieceFromObj(submeshAssetPath, meshName, out GameObject piece))
            {
                Debug.LogWarning("ReStock: missing submesh OBJ: " + submeshAssetPath);
                continue;
            }

            piece.transform.SetParent(visualRoot.transform, false);
            attached++;
        }

        return attached > 0;
    }

    internal static bool BuildOrUpdateContainer(
        ReStockBatchParts.PartDefinition definition,
        bool preserveAuthoredMarkers
    )
    {
        if (definition == null)
        {
            return false;
        }

        string containerPath = ReStockPartPaths.GetContainerPrefabAssetPath(definition.Category, definition.PartId);
        string visualPath = ReStockPartPaths.GetVisualPrefabAssetPath(definition.Category, definition.PartId);
        string logicalModelPath = definition.SplitSubmeshes ?
            ReStockBatchParts.GetSplitModelFolderAssetPath(definition) :
            ReStockBatchParts.GetSingleModelAssetPath(definition);

        ReStockPartPaths.EnsurePartPrefabFolder(definition.Category, definition.PartId);

        GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);

        if (visualPrefab == null)
        {
            Debug.LogWarning("ReStock: visual prefab missing, build visual first: " + visualPath);
            return false;
        }

        bool containerExists = AssetDatabase.LoadAssetAtPath<GameObject>(containerPath) != null;
        bool loadedFromPrefabContents = false;
        List<MarkerSnapshot> savedMarkers = new List<MarkerSnapshot>();
        Vector3? savedThrustLocalPosition = null;
        Quaternion? savedThrustLocalRotation = null;

        GameObject containerRoot;

        if (containerExists && preserveAuthoredMarkers)
        {
            containerRoot = PrefabUtility.LoadPrefabContents(containerPath);
            loadedFromPrefabContents = true;
            CaptureMarkerSnapshots(containerRoot, savedMarkers);
            CaptureThrustSnapshot(containerRoot, out savedThrustLocalPosition, out savedThrustLocalRotation);
            ReplaceVisualChild(containerRoot, visualPrefab);
        }
        else if (containerExists)
        {
            containerRoot = PrefabUtility.LoadPrefabContents(containerPath);
            loadedFromPrefabContents = true;
            ReplaceVisualChild(containerRoot, visualPrefab);
        }
        else
        {
            containerRoot = new GameObject(definition.PartId);
            GameObject visualInstance = PrefabUtility.InstantiatePrefab(visualPrefab) as GameObject;

            if (visualInstance != null)
            {
                visualInstance.name = VisualChildName;
                visualInstance.transform.SetParent(containerRoot.transform, false);
            }
        }

        EnsurePartVisualAdapter(containerRoot, definition.PartId);
        ReStockImportPipeline.ApplyContainerPrefabMaterials(containerRoot, logicalModelPath, definition);

        bool hasMarkers = PartPrefabBuilder.HasAuthoredNodes(containerRoot);

        if (!hasMarkers || !preserveAuthoredMarkers)
        {
            RemoveExistingMarkers(containerRoot);
            SpawnDefaultMarkers(containerRoot, definition);
        }
        else
        {
            RestoreMarkerSnapshots(containerRoot, savedMarkers);
        }

        bool hasThrust = PartPrefabBuilder.FindThrustTransform(containerRoot.transform) != null;

        if (preserveAuthoredMarkers && savedThrustLocalPosition.HasValue)
        {
            RestoreThrustSnapshot(
                containerRoot,
                savedThrustLocalPosition.Value,
                savedThrustLocalRotation ?? Quaternion.identity
            );
        }
        else if (!hasThrust || !preserveAuthoredMarkers)
        {
            EnsureEngineThrustTransform(containerRoot, definition);
        }

        PrefabUtility.SaveAsPrefabAsset(containerRoot, containerPath);

        if (loadedFromPrefabContents)
        {
            PrefabUtility.UnloadPrefabContents(containerRoot);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(containerRoot);
        }

        VabPartDefinitionWriter.WriteForPrefab(containerPath, definition.Category);
        Debug.Log("ReStock: container prefab -> " + containerPath);
        return true;
    }

    static void EnsurePartVisualAdapter(GameObject root, string partId)
    {
        PartVisualAdapter adapter = root.GetComponent<PartVisualAdapter>();

        if (adapter == null)
        {
            adapter = root.AddComponent<PartVisualAdapter>();
        }

        adapter.PartTemplateId = partId;
    }

    static void ReplaceVisualChild(GameObject containerRoot, GameObject visualPrefab)
    {
        Transform existingVisual = containerRoot.transform.Find(VisualChildName);

        if (existingVisual != null)
        {
            UnityEngine.Object.DestroyImmediate(existingVisual.gameObject);
        }

        GameObject visualInstance = PrefabUtility.InstantiatePrefab(visualPrefab) as GameObject;

        if (visualInstance == null)
        {
            return;
        }

        visualInstance.name = VisualChildName;
        visualInstance.transform.SetParent(containerRoot.transform, false);
    }

    static void CaptureMarkerSnapshots(GameObject root, List<MarkerSnapshot> snapshots)
    {
        AttachmentNodeMarker[] markers = root.GetComponentsInChildren<AttachmentNodeMarker>(true);

        for (int i = 0; i < markers.Length; i++)
        {
            AttachmentNodeMarker marker = markers[i];

            if (marker == null || !PartPrefabBuilder.IsMarkerOwnedByPartRoot(marker, root))
            {
                continue;
            }

            snapshots.Add(
                new MarkerSnapshot
                {
                    Name = marker.gameObject.name,
                    LocalPosition = marker.transform.localPosition,
                    LocalRotation = marker.transform.localRotation,
                    NodeIndex = marker.NodeIndex,
                }
            );
        }
    }

    static void RestoreMarkerSnapshots(GameObject root, List<MarkerSnapshot> snapshots)
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            MarkerSnapshot snapshot = snapshots[i];
            Transform existing = root.transform.Find(snapshot.Name);

            if (existing == null)
            {
                continue;
            }

            existing.localPosition = snapshot.LocalPosition;
            existing.localRotation = snapshot.LocalRotation;
        }
    }

    static void CaptureThrustSnapshot(
        GameObject root,
        out Vector3? localPosition,
        out Quaternion? localRotation
    )
    {
        Transform thrust = PartPrefabBuilder.FindThrustTransform(root.transform);
        localPosition = null;
        localRotation = null;

        if (thrust == null)
        {
            return;
        }

        localPosition = thrust.localPosition;
        localRotation = thrust.localRotation;
    }

    static void RestoreThrustSnapshot(GameObject root, Vector3 localPosition, Quaternion localRotation)
    {
        Transform thrust = EnsureThrustTransformObject(root);

        if (thrust == null)
        {
            return;
        }

        thrust.localPosition = localPosition;
        thrust.localRotation = localRotation;
    }

    static void RemoveExistingMarkers(GameObject root)
    {
        AttachmentNodeMarker[] markers = root.GetComponentsInChildren<AttachmentNodeMarker>(true);

        for (int i = 0; i < markers.Length; i++)
        {
            AttachmentNodeMarker marker = markers[i];

            if (marker == null || !PartPrefabBuilder.IsMarkerOwnedByPartRoot(marker, root))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(marker.gameObject);
        }
    }

    static void SpawnDefaultMarkers(GameObject root, ReStockBatchParts.PartDefinition definition)
    {
        Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(root);
        List<AttachmentNode> nodes = ResolveDefaultNodes(definition, bounds);

        for (int i = 0; i < nodes.Count; i++)
        {
            AttachmentNode node = nodes[i];
            string objectName = i == 0 ?
                PartPrefabBuilder.NodeTopName :
                i == 1 ? PartPrefabBuilder.NodeBottomName : PartPrefabBuilder.NodeIndexPrefix + i;

            CreateMarker(
                root,
                objectName,
                i,
                new Vector3(
                    (float)node.localPosition.x,
                    (float)node.localPosition.y,
                    (float)node.localPosition.z
                ),
                DirectionToQuaternion(node.direction)
            );
        }
    }

    static List<AttachmentNode> ResolveDefaultNodes(
        ReStockBatchParts.PartDefinition definition,
        Bounds bounds
    )
    {
        if (
            PartStackProfileLibrary.TryGetProfile(definition.PartId, out PartStackProfileLibrary.Profile profile)
        )
        {
            if (profile.HasExplicitFaces)
            {
                return PartStackProfileLibrary.CreateStackNodes(
                    profile.TopY ?? bounds.max.y,
                    profile.BottomY ?? bounds.min.y
                );
            }

            if (profile.HasSymmetricStack)
            {
                double half = profile.StackHalfHeightMeters ?? bounds.extents.y;
                return PartStackProfileLibrary.CreateStackNodes(half, -half);
            }
        }

        return PartCatalogHelpers.InferNodes(definition.PartId, bounds, definition.Category);
    }

    static void EnsureEngineThrustTransform(GameObject root, ReStockBatchParts.PartDefinition definition)
    {
        if (!string.Equals(definition.Category, PartDatabase.CategoryEngine, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(root);
        Transform thrust = EnsureThrustTransformObject(root);
        Vector3 centerXZ = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        thrust.localPosition = centerXZ;
        thrust.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.down);
    }

    static Transform EnsureThrustTransformObject(GameObject root)
    {
        Transform existing = PartPrefabBuilder.FindThrustTransform(root.transform);

        if (existing != null)
        {
            return existing;
        }

        GameObject thrustObject = new GameObject(PartPrefabBuilder.ThrustTransformName);
        thrustObject.transform.SetParent(root.transform, false);
        return thrustObject.transform;
    }

    static GameObject CreateMarker(
        GameObject root,
        string objectName,
        int nodeIndex,
        Vector3 localPosition,
        Quaternion localRotation
    )
    {
        GameObject nodeObject = new GameObject(objectName);
        nodeObject.transform.SetParent(root.transform, false);
        nodeObject.transform.localPosition = localPosition;
        nodeObject.transform.localRotation = localRotation;

        AttachmentNodeMarker marker = nodeObject.AddComponent<AttachmentNodeMarker>();
        marker.Configure(string.Empty, nodeIndex);
        ConfigureMarkerTrigger(nodeObject);
        return nodeObject;
    }

    static void ConfigureMarkerTrigger(GameObject nodeObject)
    {
        SphereCollider trigger = nodeObject.GetComponent<SphereCollider>();

        if (trigger == null)
        {
            trigger = nodeObject.AddComponent<SphereCollider>();
        }

        trigger.isTrigger = true;
        trigger.radius = VabAttachmentNodeLayers.TriggerRadiusMeters;
        trigger.center = Vector3.zero;

        int attachmentLayer = LayerMask.NameToLayer(VabAttachmentNodeLayers.LayerName);

        if (attachmentLayer >= 0)
        {
            nodeObject.layer = attachmentLayer;
        }
    }

    static Quaternion DirectionToQuaternion(SimVector3 direction)
    {
        Vector3 up = new Vector3(
            (float)direction.x,
            (float)direction.y,
            (float)direction.z
        ).normalized;

        if (up.sqrMagnitude < 1e-6f)
        {
            up = Vector3.up;
        }

        return Quaternion.FromToRotation(Vector3.up, up);
    }
}
#endif
