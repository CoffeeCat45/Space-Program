using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Optional secondary camera for orbit overlay lines when the OrbitOverlay layer exists.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrbitOverlayCamera : MonoBehaviour
{
    Camera sourceCamera;
    Camera overlayCamera;
    int orbitLayerMask;
    bool installed;
    bool overlayEnabled;

    public static void Install(Camera mainCamera)
    {
        if (mainCamera == null || !OrbitLineVisualUtility.HasDedicatedOverlayLayer())
        {
            return;
        }

        OrbitOverlayCamera overlay = mainCamera.GetComponent<OrbitOverlayCamera>();

        if (overlay == null)
        {
            overlay = mainCamera.gameObject.AddComponent<OrbitOverlayCamera>();
        }

        overlay.EnsureInstalled();
    }

    public static void SetOverlayEnabled(Camera mainCamera, bool enabled)
    {
        if (mainCamera == null)
        {
            return;
        }

        OrbitOverlayCamera overlay = mainCamera.GetComponent<OrbitOverlayCamera>();

        if (overlay == null)
        {
            return;
        }

        overlay.SetOverlayEnabled(enabled);
    }

    public static void SetMapModeActive(Camera mainCamera, bool mapModeActive)
    {
        SetOverlayEnabled(mainCamera, mapModeActive);
    }

    void EnsureInstalled()
    {
        if (installed)
        {
            return;
        }

        sourceCamera = GetComponent<Camera>();

        if (sourceCamera == null || !OrbitLineVisualUtility.HasDedicatedOverlayLayer())
        {
            enabled = false;
            return;
        }

        int orbitLayer = OrbitLineVisualUtility.ResolveOrbitOverlayLayer();
        orbitLayerMask = 1 << orbitLayer;

        GameObject overlayObject = new GameObject("Orbit Overlay Camera");
        overlayObject.transform.SetParent(transform, false);
        overlayObject.hideFlags = HideFlags.HideInHierarchy;

        overlayCamera = overlayObject.AddComponent<Camera>();
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.cullingMask = orbitLayerMask;
        overlayCamera.depth = sourceCamera.depth + 1f;
        overlayCamera.allowHDR = sourceCamera.allowHDR;
        overlayCamera.allowMSAA = false;
        overlayCamera.useOcclusionCulling = false;
        overlayCamera.enabled = false;

        UniversalAdditionalCameraData sourceData =
            sourceCamera.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData overlayData =
            overlayCamera.GetUniversalAdditionalCameraData();

        if (sourceData != null && sourceData.renderType != CameraRenderType.Base)
        {
            sourceData.renderType = CameraRenderType.Base;
        }

        if (overlayData != null)
        {
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = false;
            overlayData.antialiasing = AntialiasingMode.None;
            overlayData.dithering = false;
        }

        if (sourceData != null)
        {
            sourceData.cameraStack.Remove(overlayCamera);
            sourceData.cameraStack.Add(overlayCamera);
        }

        sourceCamera.cullingMask &= ~orbitLayerMask;
        installed = true;
        SyncFromSource();
    }

    void OnDestroy()
    {
        if (!installed || sourceCamera == null || overlayCamera == null)
        {
            return;
        }

        UniversalAdditionalCameraData sourceData =
            sourceCamera.GetUniversalAdditionalCameraData();

        if (sourceData != null)
        {
            sourceData.cameraStack.Remove(overlayCamera);
        }

        sourceCamera.cullingMask |= orbitLayerMask;
    }

    void ApplyMainCameraOrbitLayerMask()
    {
        if (!installed || sourceCamera == null)
        {
            return;
        }

        sourceCamera.cullingMask &= ~orbitLayerMask;
    }

    void LateUpdate()
    {
        SyncFromSource();
    }

    void SetOverlayEnabled(bool enabled)
    {
        overlayEnabled = enabled;
        EnsureInstalled();

        ApplyMainCameraOrbitLayerMask();

        if (overlayCamera != null)
        {
            overlayCamera.enabled = enabled;
        }
    }

    void SyncFromSource()
    {
        if (!installed || sourceCamera == null || overlayCamera == null)
        {
            return;
        }

        Transform sourceTransform = sourceCamera.transform;
        Transform overlayTransform = overlayCamera.transform;
        overlayTransform.SetPositionAndRotation(
            sourceTransform.position,
            sourceTransform.rotation
        );

        overlayCamera.orthographic = sourceCamera.orthographic;
        overlayCamera.fieldOfView = sourceCamera.fieldOfView;
        overlayCamera.orthographicSize = sourceCamera.orthographicSize;
        overlayCamera.nearClipPlane = sourceCamera.nearClipPlane;
        overlayCamera.farClipPlane = sourceCamera.farClipPlane;
        overlayCamera.rect = sourceCamera.rect;
    }
}
