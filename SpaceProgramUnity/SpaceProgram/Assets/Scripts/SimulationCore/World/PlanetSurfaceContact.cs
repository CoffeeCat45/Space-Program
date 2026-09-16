using System;

/// <summary>
/// Collidable surface height above the reference sphere. Pad decks are authored
/// separately; this is GEBCO/LDEM (Earth ocean floor → sea level).
/// </summary>
static class PlanetSurfaceContact
{
    public static SimVector3 ToBodyFixedRadial(Body body, SimVector3 worldRadialOut)
    {
        if (body == null || worldRadialOut.LengthSquared() <= 1e-24)
        {
            return SimVector3.UnitY();
        }

        return body
            .GetSpinRotation()
            .Conjugate()
            .Rotate(worldRadialOut.Normalize())
            .Normalize();
    }

    public static bool TreatNegativeElevationAsSeaLevel(Body body)
    {
        return body != null && body.name == CelestialBody.Earth.Name;
    }

    /// <summary>
    /// Height of the land/water contact plane above the sphere. Missing map → 0.
    /// </summary>
    public static double GetHeightAboveSphereMeters(Body body, SimVector3 worldRadialOut)
    {
        if (!PlanetElevationCatalog.TryGetElevationAtDirection(
            ToBodyFixedRadial(body, worldRadialOut),
            out double sampledMeters
        ))
        {
            return 0.0;
        }

        if (TreatNegativeElevationAsSeaLevel(body))
        {
            return BiomeEvaluator.GetPhysicalSurfaceElevationMeters(sampledMeters);
        }

        return sampledMeters;
    }

    public static bool TrySampleMapAt(
        Body body,
        SimVector3 worldRadialOut,
        out double sampledMeters
    )
    {
        return PlanetElevationCatalog.TryGetElevationAtDirection(
            ToBodyFixedRadial(body, worldRadialOut),
            out sampledMeters
        );
    }
}
