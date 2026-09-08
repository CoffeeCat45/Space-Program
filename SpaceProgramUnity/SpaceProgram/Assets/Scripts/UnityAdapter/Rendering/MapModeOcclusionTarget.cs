using UnityEngine;

/// <summary>
/// Caches renderers for a map-mode UI marker and applies/restores planet-occlusion hiding.
/// </summary>
[DisallowMultipleComponent]
sealed class MapModeOcclusionTarget : MonoBehaviour
{
    const string VesselMapMarkerName = "MapModeMarker";

    Renderer[] renderers = System.Array.Empty<Renderer>();
    CanvasGroup[] canvasGroups = System.Array.Empty<CanvasGroup>();
    bool[] rendererEnabledSnapshot = System.Array.Empty<bool>();
    float[] canvasAlphaSnapshot = System.Array.Empty<float>();
    Transform visualTransform;
    bool renderersCached;
    bool visualTransformCached;
    bool occlusionApplied;
    bool usesScreenSpaceBillboardPresentation;
    bool presentationModeCached;

    public void ApplyScreenSpacePresentation(
        Camera camera,
        float referenceDistance,
        float baseScale,
        float minScale,
        float maxScale
    )
    {
        if (!isActiveAndEnabled || camera == null)
        {
            return;
        }

        if (UsesScreenSpaceBillboardPresentation())
        {
            EnsureBillboardScalerEnabled();

            if (GetComponent<TextMesh>() != null)
            {
                transform.localScale = Vector3.one;
            }

            return;
        }

        Transform targetVisualTransform = ResolveVisualTransform();

        if (targetVisualTransform == null)
        {
            return;
        }

        targetVisualTransform.rotation = camera.transform.rotation;

        float distance = Vector3.Distance(
            camera.transform.position,
            targetVisualTransform.position
        );
        float calculatedScale = (distance / Mathf.Max(referenceDistance, 1e-3f)) * baseScale;
        float finalScale = Mathf.Clamp(calculatedScale, minScale, maxScale);
        targetVisualTransform.localScale = new Vector3(
            finalScale,
            finalScale,
            finalScale
        );
    }

    public void ApplyOcclusion(
        Camera camera,
        Vector3[] occluderCenters,
        float[] occluderRadii,
        int occluderCount
    )
    {
        if (!isActiveAndEnabled)
        {
            ClearOcclusion();
            return;
        }

        EnsureRendererCache();
        ResolveVisualTransform();

        Vector3 occlusionSamplePosition = ResolveOcclusionSamplePosition();
        bool isVesselMarker = IsVesselMapMarker();

        if (isVesselMarker && camera != null && occluderCount > 0)
        {
            Transform markerRoot = transform.parent != null ? transform.parent : transform;
            occlusionSamplePosition = LiftVesselMarkerOffGlobes(
                camera,
                markerRoot.position,
                occluderCenters,
                occluderRadii,
                occluderCount
            );
        }

        bool visible = true;

        for (int i = 0; i < occluderCount; i++)
        {
            if (
                isVesselMarker &&
                MapMarkerPlacement.IsOnNearHemisphere(
                    camera.transform.position,
                    occluderCenters[i],
                    occlusionSamplePosition
                )
            )
            {
                Vector3 offset = occlusionSamplePosition - occluderCenters[i];
                if (offset.sqrMagnitude <= occluderRadii[i] * occluderRadii[i])
                {
                    continue;
                }
            }

            if (
                !WorldVisualBridge.IsMapMarkerVisibleFromCamera(
                    camera,
                    occlusionSamplePosition,
                    occluderCenters[i],
                    occluderRadii[i],
                    hasPlanetSphere: true
                )
            )
            {
                visible = false;
                break;
            }
        }

        if (visible)
        {
            ClearOcclusion();
            EnsureRenderersEnabledForActiveMarker();
            return;
        }

        ApplyOcclusionHide();
    }

    Vector3 LiftVesselMarkerOffGlobes(
        Camera camera,
        Vector3 worldPosition,
        Vector3[] occluderCenters,
        float[] occluderRadii,
        int occluderCount
    )
    {
        Vector3 lifted = worldPosition;

        for (int i = 0; i < occluderCount; i++)
        {
            if (
                !MapMarkerPlacement.IsOnNearHemisphere(
                    camera.transform.position,
                    occluderCenters[i],
                    lifted
                )
            )
            {
                continue;
            }

            lifted = MapMarkerPlacement.LiftOutOfGlobe(
                lifted,
                occluderCenters[i],
                occluderRadii[i],
                extraPadding: 0.35f
            );
        }

        if (lifted != worldPosition && visualTransform != null)
        {
            visualTransform.position = lifted;
        }

        return lifted;
    }

    public void AbandonOcclusion()
    {
        occlusionApplied = false;
        renderersCached = false;
        visualTransformCached = false;
        presentationModeCached = false;
        visualTransform = null;
    }

    public void ClearOcclusion()
    {
        if (!occlusionApplied)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = rendererEnabledSnapshot[i];
            }
        }

        for (int i = 0; i < canvasGroups.Length; i++)
        {
            if (canvasGroups[i] != null)
            {
                canvasGroups[i].alpha = canvasAlphaSnapshot[i];
            }
        }

        occlusionApplied = false;
    }

    public bool SkipsCentralPresentation()
    {
        return UsesScreenSpaceBillboardPresentation();
    }

    bool IsVesselMapMarker()
    {
        return gameObject.name == VesselMapMarkerName;
    }

    bool UsesScreenSpaceBillboardPresentation()
    {
        if (!presentationModeCached)
        {
            usesScreenSpaceBillboardPresentation =
                IsVesselMapMarker() ||
                GetComponent<TextMesh>() != null ||
                GetComponent<ScreenSpaceBillboardScaler>() != null;
            presentationModeCached = true;
        }

        return usesScreenSpaceBillboardPresentation;
    }

    Vector3 ResolveOcclusionSamplePosition()
    {
        Transform sampleTransform = ResolveVisualTransform();
        return sampleTransform != null ?
            sampleTransform.position :
            transform.position;
    }

    Transform ResolveVisualTransform()
    {
        if (visualTransformCached)
        {
            return visualTransform;
        }

        visualTransform = FindVisualTransform(gameObject.transform);
        visualTransformCached = true;
        return visualTransform;
    }

    static Transform FindVisualTransform(Transform markerRoot)
    {
        if (markerRoot == null)
        {
            return null;
        }

        TextMesh textMesh = markerRoot.GetComponent<TextMesh>();

        if (textMesh != null)
        {
            return textMesh.transform;
        }

        Canvas canvas = markerRoot.GetComponentInChildren<Canvas>(true);

        if (canvas != null)
        {
            return canvas.transform;
        }

        SpriteRenderer spriteRenderer = markerRoot.GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer != null)
        {
            return spriteRenderer.transform;
        }

        MeshRenderer meshRenderer = markerRoot.GetComponent<MeshRenderer>();

        if (meshRenderer != null)
        {
            return meshRenderer.transform;
        }

        Renderer childRenderer = markerRoot.GetComponentInChildren<Renderer>(true);

        if (childRenderer != null)
        {
            return childRenderer.transform;
        }

        return markerRoot;
    }

    void ApplyOcclusionHide()
    {
        EnsureRendererCache();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            if (!occlusionApplied)
            {
                rendererEnabledSnapshot[i] = renderer.enabled;
            }

            renderer.enabled = false;
        }

        for (int i = 0; i < canvasGroups.Length; i++)
        {
            CanvasGroup canvasGroup = canvasGroups[i];

            if (canvasGroup == null)
            {
                continue;
            }

            if (!occlusionApplied)
            {
                canvasAlphaSnapshot[i] = canvasGroup.alpha;
            }

            canvasGroup.alpha = 0f;
        }

        occlusionApplied = true;
    }

    void EnsureRenderersEnabledForActiveMarker()
    {
        EnsureRendererCache();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }

        for (int i = 0; i < canvasGroups.Length; i++)
        {
            if (canvasGroups[i] != null)
            {
                canvasGroups[i].alpha = 1f;
            }
        }
    }

    void EnsureRendererCache()
    {
        if (renderersCached)
        {
            return;
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        canvasGroups = GetComponentsInChildren<CanvasGroup>(true);
        rendererEnabledSnapshot = new bool[renderers.Length];
        canvasAlphaSnapshot = new float[canvasGroups.Length];
        renderersCached = true;
    }

    void EnsureBillboardScalerEnabled()
    {
        ScreenSpaceBillboardScaler scaler = GetComponent<ScreenSpaceBillboardScaler>();

        if (scaler != null)
        {
            scaler.enabled = true;
        }
    }

    void OnDisable()
    {
        ClearOcclusion();
        renderersCached = false;
        visualTransformCached = false;
        presentationModeCached = false;
        visualTransform = null;
    }
}
