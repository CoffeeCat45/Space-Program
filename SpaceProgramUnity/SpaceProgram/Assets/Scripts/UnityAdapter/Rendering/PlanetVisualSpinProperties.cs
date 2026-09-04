using UnityEngine;

/// <summary>
/// Rigid-body spin for flight visuals without rotating the planet transform (LOD stability).
/// Longitude offset is applied in planet surface shaders; clouds use the same angle.
/// </summary>
public static class PlanetVisualSpinProperties
{
    public const string SpinLongitudeRadiansProperty = "_PlanetSpinLongitudeRadians";

    public static readonly int SpinLongitudeRadiansId =
        Shader.PropertyToID(SpinLongitudeRadiansProperty);

    public static void Apply(Material material, float spinLongitudeRadians)
    {
        if (material == null || !material.HasProperty(SpinLongitudeRadiansId))
        {
            return;
        }

        material.SetFloat(SpinLongitudeRadiansId, spinLongitudeRadians);
    }

    public static void Apply(Transform root, float spinLongitudeRadians)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            Material shared = renderer.sharedMaterial;

            if (shared != null)
            {
                Apply(shared, spinLongitudeRadians);
            }
        }
    }
}
