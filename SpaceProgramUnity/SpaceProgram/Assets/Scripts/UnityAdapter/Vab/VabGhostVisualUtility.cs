using System;
using UnityEngine;

/// <summary>
/// Visual states for VAB parts. Uses MaterialPropertyBlock only — never tints shared materials.
/// </summary>
static class VabGhostVisualUtility
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static int heldPartLayer = -1;

    public static void ApplyHeldGhostVisual(GameObject root, bool snapped)
    {
        if (root == null)
        {
            return;
        }

        EnsureLayersResolved();
        SetHierarchyLayer(root.transform, heldPartLayer >= 0 ? heldPartLayer : root.layer);

        if (snapped)
        {
            ApplyTint(root, new Color(0.25f, 0.95f, 0.3f, 0.72f));
        }
        else
        {
            ApplyTint(root, new Color(1f, 1f, 1f, 0.55f));
        }
    }

    public static void ApplyDisjointedVisual(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        VabColliderUtility.ClearSavedLayers(root);
        SetHierarchyLayer(root.transform, 0);
        ApplyTint(root, new Color(0.25f, 0.25f, 0.25f, 0.55f));
    }

    public static void ApplyPlacedVisual(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        VabColliderUtility.ClearSavedLayers(root);
        SetHierarchyLayer(root.transform, 0);
        ClearTint(root);
    }

    static void ApplyTint(GameObject root, Color color)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || IsUtilityChild(renderer.transform))
            {
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }

    static void ClearTint(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && !IsUtilityChild(renderers[i].transform))
            {
                renderers[i].SetPropertyBlock(null);
            }
        }
    }

    static bool IsAttachmentNodeTransform(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        if (transform.GetComponent<AttachmentNodeMarker>() != null)
        {
            return true;
        }

        string name = transform.name;

        return name == PartPrefabBuilder.NodeTopName ||
            name == PartPrefabBuilder.NodeBottomName ||
            name.StartsWith(PartPrefabBuilder.NodeIndexPrefix, StringComparison.Ordinal);
    }

    static bool IsUtilityChild(Transform transform)
    {
        while (transform != null)
        {
            if (IsAttachmentNodeTransform(transform))
            {
                return true;
            }

            string name = transform.name;

            if (name == "thrustTransform")
            {
                return true;
            }

            transform = transform.parent;
        }

        return false;
    }

    static void SetHierarchyLayer(Transform root, int layer)
    {
        if (!IsUtilityChild(root) || root.name == root.root.name)
        {
            root.gameObject.layer = layer;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (IsAttachmentNodeTransform(child))
            {
                continue;
            }

            SetHierarchyLayer(child, layer);
        }
    }

    static void EnsureLayersResolved()
    {
        if (heldPartLayer < 0)
        {
            heldPartLayer = LayerMask.NameToLayer(VabColliderUtility.HeldPartLayerName);
        }
    }
}
