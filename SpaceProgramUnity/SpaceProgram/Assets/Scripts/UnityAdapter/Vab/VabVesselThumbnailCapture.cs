using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Runtime capture of the assembled vessel for ship-browser thumbnails.
/// Uses chroma-key extraction; foreground is composited over KSP background in UI.
/// </summary>
public static class VabVesselThumbnailCapture
{
    const int ThumbnailLayer = 31;
    const int DefaultSize = 256;
    const float FramePadding = 1.18f;
    static readonly Quaternion CameraRotation = Quaternion.Euler(22f, -38f, 0f);
    static readonly Color ChromaKeyColor = new Color(1f, 0f, 1f, 1f);
    static readonly Color AmbientColor = new Color(0.72f, 0.74f, 0.78f, 1f);

    public static IEnumerator CaptureAndSaveCoroutine(
        Transform vesselRoot,
        string vesselName,
        Action onComplete = null
    )
    {
        if (vesselRoot == null || string.IsNullOrWhiteSpace(vesselName))
        {
            onComplete?.Invoke();
            yield break;
        }

        yield return null;
        yield return new WaitForEndOfFrame();

        byte[] pngBytes = CapturePng(vesselRoot, DefaultSize);

        if (pngBytes == null || pngBytes.Length == 0)
        {
            Debug.LogWarning(
                "VAB thumbnail: failed to capture preview for '" + vesselName + "'."
            );
            onComplete?.Invoke();
            yield break;
        }

        VabFileSystem.SaveThumbnail(vesselName, pngBytes);
        onComplete?.Invoke();
    }

    public static byte[] CapturePng(Transform vesselRoot, int size)
    {
        if (vesselRoot == null || !HasRenderableGeometry(vesselRoot))
        {
            return null;
        }

        GameObject studioRoot = new GameObject("VabVesselThumbnailStudio");
        RenderTexture renderTarget = null;
        Texture2D colorCapture = null;
        Texture2D foreground = null;
        AmbientMode previousAmbientMode = RenderSettings.ambientMode;
        Color previousAmbientLight = RenderSettings.ambientLight;

        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;

            GameObject clone = UnityEngine.Object.Instantiate(vesselRoot.gameObject, studioRoot.transform);
            clone.name = "VesselThumbnailClone";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            PrepareInstance(clone);
            Bounds bounds = CenterInstance(clone);

            renderTarget = RenderTexture.GetTemporary(
                size,
                size,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );

            GameObject cameraObject = new GameObject("VabVesselThumbnailCamera");
            cameraObject.transform.SetParent(studioRoot.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            ConfigureCamera(camera, clone, bounds, renderTarget);

            CreateDirectionalLight(
                studioRoot.transform,
                "KeyLight",
                Quaternion.Euler(34f, 38f, 0f),
                new Color(1f, 0.98f, 0.94f, 1f),
                2.2f
            );
            CreateDirectionalLight(
                studioRoot.transform,
                "FillLight",
                Quaternion.Euler(300f, 210f, 0f),
                new Color(0.92f, 0.95f, 1f, 1f),
                1.25f
            );

            if (!RenderToTexture(camera, renderTarget))
            {
                return null;
            }

            RenderTexture previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = renderTarget;
                colorCapture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                colorCapture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                colorCapture.Apply();
            }
            finally
            {
                RenderTexture.active = previousActive;
            }

            foreground = ExtractForeground(colorCapture);
            return foreground.EncodeToPNG();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("VAB thumbnail capture failed: " + exception.Message);
            return null;
        }
        finally
        {
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;

            if (foreground != null)
            {
                UnityEngine.Object.Destroy(foreground);
            }

            if (colorCapture != null)
            {
                UnityEngine.Object.Destroy(colorCapture);
            }

            if (renderTarget != null)
            {
                RenderTexture.ReleaseTemporary(renderTarget);
            }

            if (studioRoot != null)
            {
                UnityEngine.Object.Destroy(studioRoot);
            }
        }
    }

    static bool RenderToTexture(Camera camera, RenderTexture renderTarget)
    {
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = renderTarget;

        RenderTexture previousActive = RenderTexture.active;

        try
        {
            RenderTexture.active = renderTarget;
            GL.Clear(true, true, ChromaKeyColor);
            RenderTexture.active = null;

            if (GraphicsSettings.currentRenderPipeline != null)
            {
                RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest
                {
                    destination = renderTarget,
                };

                RenderPipeline.SubmitRenderRequest(camera, request);
                return true;
            }

            camera.Render();
            return true;
        }
        finally
        {
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
        }
    }

    static bool HasRenderableGeometry(Transform vesselRoot)
    {
        Renderer[] renderers = vesselRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer != null && renderer.enabled && !ShouldIgnoreRenderer(renderer))
            {
                return true;
            }
        }

        return false;
    }

    static void PrepareInstance(GameObject root)
    {
        SetLayerRecursively(root, ThumbnailLayer);
        VabDecorativeSphereUtility.HideDecorativeSpheresUnder(root.transform);
        HideThumbnailHelpers(root);
        DisableColliders(root);
    }

    static void HideThumbnailHelpers(GameObject root)
    {
        AttachmentNodeMarker[] markers = root.GetComponentsInChildren<AttachmentNodeMarker>(true);

        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                markers[i].gameObject.SetActive(false);
            }
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate == null)
            {
                continue;
            }

            string objectName = candidate.name;

            if (
                objectName == "VabHoldPivot" ||
                objectName == PartPrefabBuilder.NodeTopName ||
                objectName == PartPrefabBuilder.NodeBottomName ||
                objectName == PartPrefabBuilder.ThrustTransformName ||
                objectName.StartsWith("Node_Index_", StringComparison.Ordinal)
            )
            {
                candidate.gameObject.SetActive(false);
            }
        }
    }

    static void DisableColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    static Bounds CenterInstance(GameObject root)
    {
        Bounds bounds = CalculateRendererBounds(root);
        root.transform.position -= bounds.center;
        return CalculateRendererBounds(root);
    }

    static void ConfigureCamera(Camera camera, GameObject instance, Bounds bounds, RenderTexture target)
    {
        Vector3 focusPoint = bounds.center;
        camera.transform.rotation = CameraRotation;
        camera.cullingMask = 1 << ThumbnailLayer;
        camera.targetTexture = target;
        camera.aspect = 1f;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = ChromaKeyColor;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.orthographic = true;

        CalculateViewExtents(instance, camera.transform.rotation, focusPoint, out float maxAbsX, out float maxAbsY);
        float halfHeight = Mathf.Max(maxAbsY, maxAbsX, 0.05f) * FramePadding;
        camera.orthographicSize = halfHeight;

        float depth = Mathf.Max(bounds.extents.magnitude * 8f, 5f);
        camera.transform.position = focusPoint + camera.transform.rotation * Vector3.back * depth;
        camera.nearClipPlane = 0.001f;
        camera.farClipPlane = depth + bounds.extents.magnitude * 8f;

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();

        if (cameraData != null)
        {
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.dithering = false;
        }
    }

    static void CalculateViewExtents(
        GameObject root,
        Quaternion cameraRotation,
        Vector3 focusPoint,
        out float maxAbsX,
        out float maxAbsY
    )
    {
        maxAbsX = 0f;
        maxAbsY = 0f;
        Quaternion inverseRotation = Quaternion.Inverse(cameraRotation);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null || !renderer.enabled || ShouldIgnoreRenderer(renderer))
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 center = rendererBounds.center;
            Vector3 extents = rendererBounds.extents;

            IncludeViewPoint(inverseRotation, focusPoint, center + new Vector3(-extents.x, -extents.y, -extents.z), ref maxAbsX, ref maxAbsY);
            IncludeViewPoint(inverseRotation, focusPoint, center + new Vector3(-extents.x, -extents.y, extents.z), ref maxAbsX, ref maxAbsY);
            IncludeViewPoint(inverseRotation, focusPoint, center + new Vector3(-extents.x, extents.y, -extents.z), ref maxAbsX, ref maxAbsY);
            IncludeViewPoint(inverseRotation, focusPoint, center + new Vector3(-extents.x, extents.y, extents.z), ref maxAbsX, ref maxAbsY);
            IncludeViewPoint(inverseRotation, focusPoint, center + new Vector3(extents.x, -extents.y, -extents.z), ref maxAbsX, ref maxAbsY);
            IncludeViewPoint(inverseRotation, focusPoint, center + new Vector3(extents.x, -extents.y, extents.z), ref maxAbsX, ref maxAbsY);
            IncludeViewPoint(inverseRotation, focusPoint, center + new Vector3(extents.x, extents.y, -extents.z), ref maxAbsX, ref maxAbsY);
            IncludeViewPoint(inverseRotation, focusPoint, center + new Vector3(extents.x, extents.y, extents.z), ref maxAbsX, ref maxAbsY);
        }

        if (maxAbsX <= 0f && maxAbsY <= 0f)
        {
            maxAbsX = 0.5f;
            maxAbsY = 0.5f;
        }
    }

    static void IncludeViewPoint(
        Quaternion inverseRotation,
        Vector3 focusPoint,
        Vector3 worldPoint,
        ref float maxAbsX,
        ref float maxAbsY
    )
    {
        Vector3 viewLocal = inverseRotation * (worldPoint - focusPoint);
        maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(viewLocal.x));
        maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(viewLocal.y));
    }

    static void CreateDirectionalLight(
        Transform parent,
        string name,
        Quaternion rotation,
        Color color,
        float intensity
    )
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = rotation;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.cullingMask = 1 << ThumbnailLayer;
    }

    static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || ShouldIgnoreRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    static bool ShouldIgnoreRenderer(Renderer renderer)
    {
        if (renderer is LineRenderer)
        {
            return true;
        }

        string objectName = renderer.gameObject.name;

        return objectName.StartsWith("Node_Index_", StringComparison.Ordinal) ||
            objectName == PartPrefabBuilder.NodeTopName ||
            objectName == PartPrefabBuilder.NodeBottomName ||
            objectName == PartPrefabBuilder.ThrustTransformName ||
            objectName == "VabHoldPivot";
    }

    static Texture2D ExtractForeground(Texture2D source)
    {
        Color[] pixels = source.GetPixels();
        Color[] output = new Color[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            output[i] = IsBackgroundPixel(pixel) ?
                Color.clear :
                new Color(pixel.r, pixel.g, pixel.b, 1f);
        }

        Texture2D foreground = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        foreground.SetPixels(output);
        foreground.Apply();
        return foreground;
    }

    static bool IsBackgroundPixel(Color pixel)
    {
        const float alphaCutoff = 0.05f;

        if (pixel.a < alphaCutoff)
        {
            return true;
        }

        const float chromaTolerance = 0.14f;

        return Mathf.Abs(pixel.r - ChromaKeyColor.r) <= chromaTolerance &&
            Mathf.Abs(pixel.g - ChromaKeyColor.g) <= chromaTolerance &&
            Mathf.Abs(pixel.b - ChromaKeyColor.b) <= chromaTolerance;
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
            {
                transforms[i].gameObject.layer = layer;
            }
        }
    }
}
