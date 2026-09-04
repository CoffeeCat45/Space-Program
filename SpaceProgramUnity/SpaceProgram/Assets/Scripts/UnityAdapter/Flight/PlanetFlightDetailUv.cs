using System;
using UnityEngine;

/// <summary>
/// Close-up splat UV in surface meters, wrapped so float32 keeps sub-cm precision.
/// Wrap is centered on 0: lat/lon origin must not sit on the wrap seam (Moon pad is 0°N 0°E).
/// </summary>
public static class PlanetFlightDetailUv
{
    public const double WrapMeters = 16384.0;

    public static Vector2 ComputeWrappedDetailMetersUv(
        SimVector3 simUnit,
        float planetRadiusMeters
    )
    {
        double lat = Math.Asin(Math.Clamp(simUnit.y, -1.0, 1.0));
        double lon = Math.Atan2(simUnit.x, simUnit.z);
        double cosLat = Math.Max(Math.Abs(Math.Cos(lat)), 0.01);
        double surfaceMetersX = lon * planetRadiusMeters * cosLat;
        double surfaceMetersY = lat * planetRadiusMeters;
        return new Vector2(
            (float)WrapCentered(surfaceMetersX, WrapMeters),
            (float)WrapCentered(surfaceMetersY, WrapMeters)
        );
    }

    public static double WrapCentered(double value, double modulus)
    {
        double half = modulus * 0.5;
        double wrapped = (value + half) % modulus;
        if (wrapped < 0.0)
        {
            wrapped += modulus;
        }

        return wrapped - half;
    }
}
