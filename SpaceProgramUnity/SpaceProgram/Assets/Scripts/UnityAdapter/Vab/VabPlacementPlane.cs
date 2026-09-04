using UnityEngine;

/// <summary>
/// Horizontal assembly plane for ghost cursor (KSP-style).
/// Camera-facing focus planes change Y every frame and cause snap jitter.
/// </summary>
public static class VabPlacementPlane
{
    public static bool TryRaycastHorizontal(Ray ray, float planeYMeters, out Vector3 hitPoint)
    {
        hitPoint = default;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeYMeters, 0f));

        if (!plane.Raycast(ray, out float distance) || distance < 0f)
        {
            return false;
        }

        hitPoint = ray.GetPoint(distance);
        return true;
    }
}
