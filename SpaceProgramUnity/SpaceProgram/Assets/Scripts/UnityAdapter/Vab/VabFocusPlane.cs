using UnityEngine;

/// <summary>
/// Virtual assembly depth plane at the camera focus distance (VAB roadmap 1.2).
/// The plane passes through the focus point and faces the camera.
/// </summary>
public static class VabFocusPlane
{
    public const float MinDepthMeters = 0.5f;

    public static float GetFocusDepth(Camera camera, Vector3 focusPoint)
    {
        if (camera == null)
        {
            return 12f;
        }

        float depth = Vector3.Dot(
            focusPoint - camera.transform.position,
            camera.transform.forward
        );
        return Mathf.Max(depth, MinDepthMeters);
    }

    public static Plane BuildPlane(Camera camera, Vector3 focusPoint)
    {
        Vector3 normal = camera != null ? camera.transform.forward : Vector3.forward;
        float depth = GetFocusDepth(camera, focusPoint);
        Vector3 center = camera.transform.position + normal * depth;
        return new Plane(normal, center);
    }

    public static bool TryRaycast(
        Camera camera,
        Vector3 focusPoint,
        Ray ray,
        out Vector3 hitPoint
    )
    {
        hitPoint = default;

        if (camera == null)
        {
            return false;
        }

        Plane plane = BuildPlane(camera, focusPoint);

        if (!plane.Raycast(ray, out float distance) || distance < 0f)
        {
            return false;
        }

        hitPoint = ray.GetPoint(distance);
        return true;
    }
}
