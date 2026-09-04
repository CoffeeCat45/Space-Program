using System;

/// <summary>
/// Rule-based biome classification for Earth terrain (terrain_plan §3.3).
/// Deterministic, CPU-only, shared logic for simulation and future HLSL port.
/// </summary>
public static class BiomeEvaluator
{
    public const double PolarIceMinAbsLatitudeDegrees = 70.0;
    public const double SteepSlopeDegrees = 35.0;
    public const double SandMaxElevationMeters = 100.0;
    public const double SandMaxAbsLatitudeDegrees = 30.0;
    public const double SnowMinElevationMeters = 3500.0;

    static readonly double SteepSlopeDotUpThreshold =
        Math.Cos(SteepSlopeDegrees * (Math.PI / 180.0));

    public static BiomeProperties Evaluate(
        double latitudeRadians,
        double longitudeRadians,
        double elevationMeters,
        SimVector3 surfaceNormal,
        SimVector3 surfaceUp
    )
    {
        double slopeDotUp = ComputeSurfaceSlopeDotUp(surfaceNormal, surfaceUp);
        return Evaluate(latitudeRadians, longitudeRadians, elevationMeters, slopeDotUp);
    }

    public static BiomeProperties Evaluate(
        double latitudeRadians,
        double longitudeRadians,
        double elevationMeters,
        double surfaceSlopeDotUp
    )
    {
        _ = longitudeRadians;

        if (elevationMeters < PlanetSurfaceConstants.SeaLevelMeters)
        {
            return CreateProperties(BiomeType.Ocean);
        }

        double absLatitudeDegrees = Math.Abs(latitudeRadians) * (180.0 / Math.PI);

        if (absLatitudeDegrees > PolarIceMinAbsLatitudeDegrees)
        {
            return CreateProperties(BiomeType.Ice);
        }

        if (surfaceSlopeDotUp < SteepSlopeDotUpThreshold)
        {
            return CreateProperties(BiomeType.Rock);
        }

        if (
            elevationMeters < SandMaxElevationMeters &&
            absLatitudeDegrees < SandMaxAbsLatitudeDegrees
        )
        {
            return CreateProperties(BiomeType.Sand);
        }

        if (elevationMeters >= SnowMinElevationMeters)
        {
            return CreateProperties(BiomeType.Snow);
        }

        return CreateProperties(BiomeType.Grass);
    }

    /// <summary>
    /// Collision / altimeter surface from GEBCO. Negative elevation is seafloor:
    /// the physical surface is sea level, not the trench. Land uses GEBCO as-is.
    /// </summary>
    public static double GetPhysicalSurfaceElevationMeters(double gebcoElevationMeters)
    {
        if (gebcoElevationMeters < PlanetSurfaceConstants.SeaLevelMeters)
        {
            return PlanetSurfaceConstants.SeaLevelMeters;
        }

        return gebcoElevationMeters;
    }

    /// <summary>
    /// Dot(surfaceNormal, surfaceUp). 1 = flat, lower = steeper.
    /// </summary>
    public static double ComputeSurfaceSlopeDotUp(SimVector3 surfaceNormal, SimVector3 surfaceUp)
    {
        double dot = SimVector3.Dot(surfaceNormal.Normalize(), surfaceUp.Normalize());

        if (dot < -1.0)
        {
            return -1.0;
        }

        if (dot > 1.0)
        {
            return 1.0;
        }

        return dot;
    }

    public static BiomeProperties CreateProperties(BiomeType biomeType)
    {
        return new BiomeProperties(biomeType, GetStaticFrictionCoefficient(biomeType));
    }

    public static double GetStaticFrictionCoefficient(BiomeType biomeType)
    {
        switch (biomeType)
        {
            case BiomeType.Ocean:
                return 0.15;
            case BiomeType.Ice:
                return 0.05;
            case BiomeType.Rock:
                return 0.85;
            case BiomeType.Sand:
                return 0.55;
            case BiomeType.Snow:
                return 0.35;
            case BiomeType.Grass:
                return 0.65;
            default:
                return 0.65;
        }
    }
}
