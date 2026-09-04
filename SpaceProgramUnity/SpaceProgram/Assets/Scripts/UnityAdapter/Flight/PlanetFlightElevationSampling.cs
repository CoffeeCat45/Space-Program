using UnityEngine;

/// <summary>
/// Flight mesh / launch-site height sampling. Prefers high-res GEBCO mmap, then legacy SPEL .bin, then RSS.
/// Mesh offset uses <see cref="PlanetSurfaceConstants.UnityVisualHeightScale"/> on real meters.
/// </summary>
static class PlanetFlightElevationSampling
{
    /// <summary>
    /// PQS vertex pipeline: unit sphere direction → lat/lon → GEBCO meters → visual offset (×0.4).
    /// </summary>
    public static float SampleMeshHeightAtUnitDirection(Vector3 unitDirectionUnity)
    {
        if (unitDirectionUnity.sqrMagnitude <= 1e-12f)
        {
            return 0f;
        }

        unitDirectionUnity.Normalize();
        SimVector3 direction = new SimVector3(
            unitDirectionUnity.x,
            unitDirectionUnity.y,
            unitDirectionUnity.z
        ).Normalize();

        double elevationMeters = SampleElevationMetersAtDirection(direction);
        return (float)(elevationMeters * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale);
    }

    public static float SampleMeshHeightOffsetMeters(Vector3 unitDirectionUnity)
    {
        return SampleMeshHeightAtUnitDirection(unitDirectionUnity);
    }

    public static float SampleMeshHeightOffsetMeters(SimVector3 unitDirectionSim)
    {
        if (unitDirectionSim.LengthSquared() <= 1e-12)
        {
            return 0f;
        }

        double elevationMeters = SampleElevationMetersAtDirection(unitDirectionSim.Normalize());
        return (float)(elevationMeters * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale);
    }

    /// <summary>
    /// Real GEBCO meters at direction. High-res mmap when loaded; legacy SPEL; RSS; else 0 stub.
    /// </summary>
    public static double SampleElevationMetersAtDirection(SimVector3 direction)
    {
        direction = direction.Normalize();

        if (PlanetElevationCatalog.IsHighResLoaded &&
            (PlanetElevationCatalog.UseHighResElevation || !PlanetElevationCatalog.IsLoaded))
        {
            return PlanetElevationCatalog.GetHeightBilinearNew(direction);
        }

        if (PlanetElevationCatalog.IsLoaded)
        {
            return PlanetElevationCatalog.GetHeightBilinearLegacy(direction);
        }

        if (PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline)
        {
            return 0.0;
        }

        Vector3 unitDirectionUnity = new Vector3(
            (float)direction.x,
            (float)direction.y,
            (float)direction.z
        );

        if (PlanetTerrainTextureResources.EarthHeightMap != null)
        {
            float visualOffsetMeters = SampleLegacyRssHeightOffsetMeters(unitDirectionUnity);
            return visualOffsetMeters / PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale;
        }

        return 0.0;
    }

    public static double SampleRealElevationMeters(double latitudeDegrees, double longitudeDegrees)
    {
        if (PlanetElevationCatalog.IsHighResLoaded &&
            (PlanetElevationCatalog.UseHighResElevation || !PlanetElevationCatalog.IsLoaded))
        {
            double latRad = latitudeDegrees * (System.Math.PI / 180.0);
            double lonRad = longitudeDegrees * (System.Math.PI / 180.0);
            return PlanetElevationCatalog.GetHeightBilinearNew(latRad, lonRad);
        }

        if (PlanetElevationCatalog.IsLoaded)
        {
            return PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(
                latitudeDegrees,
                longitudeDegrees
            );
        }

        if (PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline)
        {
            return 0.0;
        }

        if (!PlanetTerrainTextureResources.TrySampleLegacyHeightMetersAtSite(
                latitudeDegrees,
                longitudeDegrees,
                out float legacyMeters
            ))
        {
            return 0.0;
        }

        return legacyMeters;
    }

    static float SampleLegacyRssHeightOffsetMeters(Vector3 unitDirectionUnity)
    {
        Texture2D heightmap = PlanetTerrainTextureResources.EarthHeightMap;

        if (heightmap == null)
        {
            return 0f;
        }

        Vector2 uv = PlanetCubeSphere.DirectionToRssTextureUv(unitDirectionUnity);
        using PlanetHeightSampler sampler = new PlanetHeightSampler(heightmap);

        if (sampler.IsValid)
        {
            return sampler.SampleHeightOffsetMeters(uv);
        }

        if (!heightmap.isReadable)
        {
            return 0f;
        }

        Color heightSample = heightmap.GetPixelBilinear(uv.x, uv.y);
        return (float)PlanetSurfaceMaps.SampleEarthHeightMeters(heightSample.r);
    }
}
