using UnityEngine;

/// <summary>
/// Approximate playable VAB hangar volume (meters, world space relative to work center).
/// </summary>
public static class VabWorkspaceBounds
{
    public const float HeightMeters = 82f;
    public const float HalfExtentXMeters = 25f;
    public const float HalfExtentZMeters = 30f;
    public const float FocusPanVerticalAboveMeters = 48f;
    public const float FocusPanVerticalBelowMeters = 12f;

    public static Vector3 ClampHorizontal(Vector3 worldPosition, Vector3 center)
    {
        Vector3 offset = worldPosition - center;
        offset.x = Mathf.Clamp(offset.x, -HalfExtentXMeters, HalfExtentXMeters);
        offset.z = Mathf.Clamp(offset.z, -HalfExtentZMeters, HalfExtentZMeters);
        worldPosition.x = center.x + offset.x;
        worldPosition.z = center.z + offset.z;
        return worldPosition;
    }

    public static Vector3 ClampFocusPoint(Vector3 focusPoint, Vector3 center)
    {
        focusPoint.y = Mathf.Clamp(
            focusPoint.y,
            center.y - FocusPanVerticalBelowMeters,
            center.y + FocusPanVerticalAboveMeters
        );
        return ClampHorizontal(focusPoint, center);
    }
}
