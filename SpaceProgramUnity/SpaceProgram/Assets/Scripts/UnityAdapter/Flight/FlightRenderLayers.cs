using UnityEngine;

/// <summary>
/// KSP-style render layers: LocalPhysics (flight terrain) vs ScaledSpace (map globe).
/// </summary>
public static class FlightRenderLayers
{
    public const string ScaledSpaceLayerName = "ScaledSpace";
    public const string LocalPhysicsLayerName = "LocalPhysics";

    static int cachedScaledSpaceLayer = int.MinValue;
    static int cachedLocalPhysicsLayer = int.MinValue;
    static bool layersResolved;

    public static bool HasDedicatedLayers()
    {
        ResolveLayers();
        return cachedScaledSpaceLayer >= 0 && cachedLocalPhysicsLayer >= 0;
    }

    public static int ResolveScaledSpaceLayer()
    {
        ResolveLayers();
        return cachedScaledSpaceLayer;
    }

    public static int ResolveLocalPhysicsLayer()
    {
        ResolveLayers();
        return cachedLocalPhysicsLayer;
    }

    public static int ScaledSpaceMask
    {
        get
        {
            int layer = ResolveScaledSpaceLayer();
            return layer >= 0 ? 1 << layer : 0;
        }
    }

    public static int LocalPhysicsMask
    {
        get
        {
            int layer = ResolveLocalPhysicsLayer();
            return layer >= 0 ? 1 << layer : 0;
        }
    }

    public static void ApplyScaledSpaceLayer(GameObject root)
    {
        ApplyLayerRecursive(root, ResolveScaledSpaceLayer());
    }

    public static void ApplyLocalPhysicsLayer(GameObject root)
    {
        ApplyLayerRecursive(root, ResolveLocalPhysicsLayer());
    }

    public static void ApplyLayerRecursive(GameObject root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        root.layer = layer;

        Transform transform = root.transform;

        for (int i = 0; i < transform.childCount; i++)
        {
            ApplyLayerRecursive(transform.GetChild(i).gameObject, layer);
        }
    }

    static void ResolveLayers()
    {
        if (layersResolved)
        {
            return;
        }

        layersResolved = true;
        cachedScaledSpaceLayer = LayerMask.NameToLayer(ScaledSpaceLayerName);
        cachedLocalPhysicsLayer = LayerMask.NameToLayer(LocalPhysicsLayerName);
    }
}
