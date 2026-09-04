using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Switches part hierarchy to <see cref="SelectionHighlightLayerName"/> for URP Render Objects outline.
/// Falls back to emission tint when the layer is not configured yet.
/// </summary>
[DisallowMultipleComponent]
public sealed class VabSelectionHighlighter : MonoBehaviour
{
    public const string SelectionHighlightLayerName = "SelectionHighlight";

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    static int selectionHighlightLayer = -1;
    static bool selectionLayerWarningLogged;

    readonly List<Transform> cachedTransforms = new List<Transform>();
    readonly List<int> originalLayers = new List<int>();
    readonly List<Renderer> cachedRenderers = new List<Renderer>();
    bool isSelected;
    bool useLayerHighlight;

    void Awake()
    {
        CacheHierarchyLayers();
        useLayerHighlight = ResolveSelectionLayer() >= 0;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
        {
            return;
        }

        isSelected = selected;

        if (useLayerHighlight)
        {
            int targetLayer = selected ? selectionHighlightLayer : 0;

            for (int i = 0; i < cachedTransforms.Count; i++)
            {
                Transform target = cachedTransforms[i];

                if (target != null)
                {
                    target.gameObject.layer = selected ? targetLayer : originalLayers[i];
                }
            }

            ClearEmissionFallback();
            return;
        }

        ApplyEmissionFallback(selected);
    }

    void CacheHierarchyLayers()
    {
        cachedTransforms.Clear();
        originalLayers.Clear();
        cachedRenderers.Clear();
        CollectTransformsRecursive(transform);
    }

    void CollectTransformsRecursive(Transform root)
    {
        cachedTransforms.Add(root);
        originalLayers.Add(root.gameObject.layer);

        Renderer renderer = root.GetComponent<Renderer>();

        if (renderer != null && renderer.enabled)
        {
            cachedRenderers.Add(renderer);
        }

        for (int i = 0; i < root.childCount; i++)
        {
            CollectTransformsRecursive(root.GetChild(i));
        }
    }

    void ApplyEmissionFallback(bool selected)
    {
        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            Renderer renderer = cachedRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            if (!selected)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, new Color(0.12f, 0.62f, 0.9f, 1f));
            renderer.SetPropertyBlock(block);
        }
    }

    void ClearEmissionFallback()
    {
        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].SetPropertyBlock(null);
            }
        }
    }

    static int ResolveSelectionLayer()
    {
        if (selectionHighlightLayer >= 0)
        {
            return selectionHighlightLayer;
        }

        selectionHighlightLayer = LayerMask.NameToLayer(SelectionHighlightLayerName);

        if (selectionHighlightLayer < 0 && !selectionLayerWarningLogged)
        {
            selectionLayerWarningLogged = true;
            Debug.LogWarning(
                "VAB: layer '" + SelectionHighlightLayerName +
                "' is missing. Using emission fallback. Run Space Program → VAB → Setup Selection Outline Rendering."
            );
        }

        return selectionHighlightLayer;
    }
}
