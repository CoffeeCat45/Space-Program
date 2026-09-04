using System;
using UnityEngine;

/// <summary>
/// Map-mode marker anchor: planet center in render world for surface-pin billboards.
/// </summary>
static class MapMarkerPlacement
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
}
