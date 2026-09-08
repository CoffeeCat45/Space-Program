using System;
using UnityEngine;

/// <summary>
/// Map-mode marker anchor: planet center in render world for surface-pin billboards.
/// </summary>
public static class MapMarkerPlacement
{
    static Func<Vector3> planetCenterWorldResolver;

    public static void SetPlanetCenterWorldResolver(Func<Vector3> resolver)
    {
        planetCenterWorldResolver = resolver;
    }

    public static Vector3 ResolvePlanetCenterWorld()
    {
        return planetCenterWorldResolver != null ?
            planetCenterWorldResolver() :
            Vector3.zero;
    }

    public static Vector3 ComputeSurfacePinPosition(
        Vector3 anchorWorldPosition,
        float markerWorldDiameter
    )
    {
        Vector3 planetCenter = ResolvePlanetCenterWorld();
        Vector3 surfaceOutward = anchorWorldPosition - planetCenter;
        float radialDistance = surfaceOutward.magnitude;

        if (radialDistance <= 1e-4f)
        {
            surfaceOutward = Vector3.up;
        }
        else
        {
            surfaceOutward /= radialDistance;
        }

        float lift = markerWorldDiameter * 0.62f;
        return anchorWorldPosition + surfaceOutward * lift;
    }

    public static Vector3 LiftOutOfGlobe(
        Vector3 worldPosition,
        Vector3 globeCenter,
        float globeRadius,
        float extraPadding
    )
    {
        if (globeRadius <= 0f)
        {
            return worldPosition;
        }

        Vector3 offset = worldPosition - globeCenter;
        float distance = offset.magnitude;
        float minDistance = globeRadius * 1.02f + Mathf.Max(0f, extraPadding);

        if (distance >= minDistance)
        {
            return worldPosition;
        }

        if (distance <= 1e-4f)
        {
            offset = Vector3.up;
        }
        else
        {
            offset /= distance;
        }

        return globeCenter + offset * minDistance;
    }

    public static bool IsOnNearHemisphere(
        Vector3 cameraPosition,
        Vector3 globeCenter,
        Vector3 worldPosition
    )
    {
        return Vector3.Dot(worldPosition - globeCenter, cameraPosition - globeCenter) > 0f;
    }
}
