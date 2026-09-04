#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders a part prefab inside a temporary empty scene so no VAB scene objects leak in.
/// </summary>
static class VabPartThumbnailStudio
{
    const int ThumbnailLayer = 31;
    const float FramePadding = 1.25f;
    static readonly Quaternion PartRotation = Quaternion.identity;
    static readonly Quaternion CameraRotation = Quaternion.Euler(22f, -38f, 0f);
    static readonly Color ChromaKeyColor = new Color(1f, 0f, 1f, 1f);
    static readonly Color AmbientColor = new Color(0.72f, 0.74f, 0.78f, 1f);

    public static bool TryCapturePrefab(GameObject prefab, string outputFullPath, int size)
    {
        if (prefab == null)
        {
            return false;
        }

        Scene studioScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        AmbientMode previousAmbientMode = RenderSettings.ambientMode;
        Color previousAmbientLight = RenderSettings.ambientLight;
        RenderTexture renderTarget = null;
        Texture2D colorCapture = null;
        Texture2D foreground = null;

        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, studioScene) as GameObject;

            if (instance == null)
            {
                return false;
            }

            PrepareInstance(instance);
            Bounds bounds = CenterInstance(instance);

            renderTarget = RenderTexture.GetTemporary(
                size,
                size,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );

            GameObject cameraObject = new GameObject("VabThumbnailCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, studioScene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            ConfigureCamera(camera, instance, bounds, renderTarget);

            GameObject keyLightObject = CreateDirectionalLight(
                studioScene,
                "VabThumbnailKeyLight",
                Quaternion.Euler(34f, 38f, 0f),
                new Color(1f, 0.98f, 0.94f, 1f),
                2.2f
            );
            GameObject fillLightObject = CreateDirectionalLight(
                studioScene,
                "VabThumbnailFillLight",
                Quaternion.Euler(300f, 210f, 0f),
                new Color(0.92f, 0.95f, 1f, 1f),
                1.25f
            );

            RenderTexture previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = renderTarget;
                GL.Clear(true, true, ChromaKeyColor);
                RenderTexture.active = null;

                camera.Render();

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
            byte[] png = foreground.EncodeToPNG();
            File.WriteAllBytes(outputFullPath, png);
            return png.Length > 0;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "VAB thumbnail studio: capture failed for '" + prefab.name + "': " + exception.Message
            );
            return false;
        }
        finally
        {
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;

            if (foreground != null)
            {
                UnityEngine.Object.DestroyImmediate(foreground);
            }

            if (colorCapture != null)
            {
                UnityEngine.Object.DestroyImmediate(colorCapture);
            }

            if (renderTarget != null)
            {
                RenderTexture.ReleaseTemporary(renderTarget);
            }

            EditorSceneManager.CloseScene(studioScene, true);
        }
    }

    static void PrepareInstance(GameObject root)
    {
        SetLayerRecursively(root, ThumbnailLayer);
        HideThumbnailHelpers(root);
        root.transform.rotation = PartRotation;
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
        camera.orthographic = true;
        camera.transform.rotation = CameraRotation;
        camera.cullingMask = 1 << ThumbnailLayer;
        camera.targetTexture = target;
        camera.aspect = 1f;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = ChromaKeyColor;
        camera.allowHDR = false;
        camera.allowMSAA = false;

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

            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

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

    static GameObject CreateDirectionalLight(
        Scene scene,
        string name,
        Quaternion rotation,
        Color color,
        float intensity
    )
    {
        GameObject lightObject = new GameObject(name, typeof(Light));
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = rotation;
        light.color = color;
        light.intensity = intensity;
        light.cullingMask = 1 << ThumbnailLayer;
        return lightObject;
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

        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        result.SetPixels(output);
        result.Apply();
        return result;
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
            objectName == PartPrefabBuilder.ThrustTransformName;
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
#endif
