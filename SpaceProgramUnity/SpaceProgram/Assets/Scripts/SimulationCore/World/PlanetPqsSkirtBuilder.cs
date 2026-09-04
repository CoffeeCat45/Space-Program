using System;

/// <summary>
/// Skirt geometry helpers for Unity PQS meshes (terrain_plan §1.3 visual path).
/// SimulationCore owns the math; Unity applies it when building chunk meshes (roadmap 2.4).
/// </summary>
public static class PlanetPqsSkirtBuilder
{
    /// <summary>
    /// Skirt depth in meters. Fraction of span, then limited to a few grid cells and
    /// <see cref="PlanetPqsConstants.MaxSkirtDepthMeters"/>. L0 face shells return 0.
    /// </summary>
    public static double ComputeSkirtDepthMeters(
        double chunkSpanMeters,
        double depthFraction = PlanetPqsConstants.DefaultSkirtDepthFraction,
        int vertexResolution = 0,
        int lodLevel = int.MaxValue
    )
    {
        if (lodLevel < PlanetPqsConstants.MinLodLevelForSkirts)
        {
            return 0.0;
        }

        if (chunkSpanMeters < 0.0)
        {
            chunkSpanMeters = 0.0;
        }

        double clampedFraction = ClampFraction(depthFraction);
        double depth = chunkSpanMeters * clampedFraction;

        if (vertexResolution >= 2)
        {
            double cellMeters = chunkSpanMeters / (vertexResolution - 1);
            depth = Math.Min(
                depth,
                cellMeters * PlanetPqsConstants.SkirtDepthGridCellMultiple
            );
        }

        if (depth > PlanetPqsConstants.MaxSkirtDepthMeters)
        {
            return PlanetPqsConstants.MaxSkirtDepthMeters;
        }

        return depth;
    }

    /// <summary>
    /// Moves a surface point inward toward the planet center by skirt depth.
    /// </summary>
    public static SimVector3 OffsetTowardPlanetCenter(
        SimVector3 surfacePosition,
        SimVector3 planetCenter,
        double skirtDepthMeters
    )
    {
        if (skirtDepthMeters <= 0.0)
        {
            return surfacePosition;
        }

        SimVector3 toCenter = planetCenter.Subtract(surfacePosition);

        if (toCenter.LengthSquared() <= 1e-24)
        {
            return surfacePosition;
        }

        SimVector3 inward = toCenter.Normalize();
        return surfacePosition.Add(inward.Multiply(skirtDepthMeters));
    }

    static double ClampFraction(double depthFraction)
    {
        if (depthFraction < PlanetPqsConstants.MinSkirtDepthFraction)
        {
            return PlanetPqsConstants.MinSkirtDepthFraction;
        }

        if (depthFraction > PlanetPqsConstants.MaxSkirtDepthFraction)
        {
            return PlanetPqsConstants.MaxSkirtDepthFraction;
        }

        return depthFraction;
    }
}
