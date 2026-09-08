using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// KSP-style orbit lines: constant screen-pixel width, unlit overlay material,
/// optional isolated render layer for the overlay camera.
/// </summary>
static class OrbitLineVisualUtility
{
    public const float CurrentOrbitPixelWidth = 2.8f;
    public const float CelestialOrbitBackgroundPixelWidth = 1.55f;
    public const float CelestialOrbitFocusedPixelWidth = 2.0f;
    public const float CelestialOrbitPixelWidth = CelestialOrbitFocusedPixelWidth;
    public const float PlannedOrbitPixelWidth = 2.0f;
    public const float SoiEntryPixelWidth = 2.5f;
    public const float SoiSpherePixelWidth = 0.65f;
    public const float MoonSoiOrbitPixelWidth = 2.35f;
    public const float ClosestApproachPixelWidth = 2.2f;
    public const float BurnPreviewPixelWidth = 2.0f;
    public const float DescentPreviewPixelWidth = 2.1f;
    public const float ManeuverHandlePixelWidth = 2.4f;

    const string OrbitOverlayLayerName = "OrbitOverlay";

    static int cachedOverlayLayer = int.MinValue;
    static bool overlayLayerResolved;

    public static bool HasDedicatedOverlayLayer()
    {
        return ResolveOrbitOverlayLayer() >= 0;
    }

    public static int ResolveOrbitOverlayLayer()
    {
        if (overlayLayerResolved)
        {
            return cachedOverlayLayer;
        }

        overlayLayerResolved = true;
        cachedOverlayLayer = LayerMask.NameToLayer(OrbitOverlayLayerName);
        return cachedOverlayLayer;
    }

    public static void ConfigureOrbitLine(
        LineRenderer lineRenderer,
        Color color,
        float pixelWidth
    )
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        lineRenderer.lightProbeUsage = LightProbeUsage.Off;
        lineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.material = RuntimeMaterialFactory.CreateOrbitLineMaterial(
            new Color(color.r, color.g, color.b, 1.0f)
        );
        lineRenderer.enabled = false;

        int overlayLayer = ResolveOrbitOverlayLayer();

        if (overlayLayer >= 0)
        {
            lineRenderer.gameObject.layer = overlayLayer;
        }

        OrbitLineScreenSpaceWidth screenSpaceWidth =
            lineRenderer.GetComponent<OrbitLineScreenSpaceWidth>();

        if (screenSpaceWidth == null)
        {
            screenSpaceWidth = lineRenderer.gameObject.AddComponent<OrbitLineScreenSpaceWidth>();
        }

        screenSpaceWidth.PixelWidth = pixelWidth;
        lineRenderer.startWidth = pixelWidth * 0.001f;
        lineRenderer.endWidth = pixelWidth * 0.001f;
    }

    public static void ApplyOverlayLayerRecursive(Transform root)
    {
        if (root == null || !HasDedicatedOverlayLayer())
        {
            return;
        }

        int layer = ResolveOrbitOverlayLayer();
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
        {
            ApplyOverlayLayerRecursive(root.GetChild(i));
        }
    }

    public static void ApplyOrbitOverlayLayer(GameObject rootObject)
    {
        if (rootObject == null)
        {
            return;
        }

        ApplyOverlayLayerRecursive(rootObject.transform);
    }
}
