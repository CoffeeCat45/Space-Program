using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pick colliders for VAB parts. Heavy ReStock meshes use box colliders instead of convex MeshCollider.
/// Held parts move to the HeldPart layer so placement raycasts skip them.
/// </summary>
public static class VabColliderUtility
{
    public const string HeldPartLayerName = "HeldPart";

    const int MaxConvexMeshVertices = 255;

    static readonly Dictionary<int, int> savedLayers = new Dictionary<int, int>();

    public static void EnsurePickColliders(GameObject root, bool rebuild = false)
    {
        if (root == null)
        {
            return;
        }

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        bool addedCollider = false;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Renderer renderer = meshFilter.GetComponent<Renderer>();

            if (renderer != null && !renderer.enabled)
            {
                continue;
            }

            if (rebuild)
            {
                RemoveColliders(meshFilter.gameObject);
            }
            else if (meshFilter.GetComponent<Collider>() != null)
            {
                addedCollider = true;
                continue;
            }

            if (TryAddMeshCollider(meshFilter))
            {
                addedCollider = true;
                continue;
            }

            AddBoxCollider(meshFilter);
            addedCollider = true;
        }

        if (addedCollider || root.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(root);
        BoxCollider rootBox = root.AddComponent<BoxCollider>();
        rootBox.center = bounds.center;
        rootBox.size = bounds.size;
    }

    static bool IsAttachmentNodeObject(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return false;
        }

        string objectName = targetObject.name;

        return targetObject.GetComponent<AttachmentNodeMarker>() != null ||
            objectName == PartPrefabBuilder.NodeTopName ||
            objectName == PartPrefabBuilder.NodeBottomName ||
            objectName.StartsWith(PartPrefabBuilder.NodeIndexPrefix, StringComparison.Ordinal);
    }

    public static void SetCollidersEnabled(GameObject root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        int heldLayer = LayerMask.NameToLayer(HeldPartLayerName);
        bool useHeldLayer = !enabled && heldLayer >= 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform targetTransform = transforms[i];

            if (targetTransform == null)
            {
                continue;
            }

            GameObject targetObject = targetTransform.gameObject;
            int instanceId = targetObject.GetInstanceID();
            bool isAttachmentNode = IsAttachmentNodeObject(targetObject);

            if (useHeldLayer && !isAttachmentNode)
            {
                if (!savedLayers.ContainsKey(instanceId))
                {
                    savedLayers[instanceId] = targetObject.layer;
                }

                targetObject.layer = heldLayer;
            }
            else if (!useHeldLayer && savedLayers.TryGetValue(instanceId, out int originalLayer))
            {
                targetObject.layer = originalLayer;
                savedLayers.Remove(instanceId);
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
            {
                continue;
            }

            if (!enabled && IsAttachmentNodeObject(collider.gameObject))
            {
                collider.enabled = true;
                continue;
            }

            collider.enabled = enabled;
        }
    }

    public static void ClearSavedLayers(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
            {
                savedLayers.Remove(transforms[i].gameObject.GetInstanceID());
            }
        }
    }

    public static bool IsUnderTransform(Transform candidate, Transform ancestor)
    {
        if (candidate == null || ancestor == null)
        {
            return false;
        }

        Transform current = candidate;

        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    static bool TryAddMeshCollider(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.sharedMesh;

        if (mesh.vertexCount > MaxConvexMeshVertices)
        {
            return false;
        }

        MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
        return true;
    }

    static void AddBoxCollider(MeshFilter meshFilter)
    {
        Bounds bounds = meshFilter.sharedMesh.bounds;
        BoxCollider boxCollider = meshFilter.gameObject.AddComponent<BoxCollider>();
        boxCollider.center = bounds.center;
        boxCollider.size = bounds.size;
    }

    static void RemoveColliders(GameObject target)
    {
        Collider[] colliders = target.GetComponents<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(colliders[i]);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(colliders[i]);
            }
        }
    }
}
