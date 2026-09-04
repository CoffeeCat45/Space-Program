using System;
using UnityEngine;

/// <summary>
/// Sandbox shim over <see cref="PlanetFlightChunkMeshPipeline"/> (Phase A shared extract).
/// </summary>
public static class EarthSandboxFlightChunkMeshPipeline
{
    public readonly struct ChunkMeshBuildRequest
    {
        public readonly int FaceIndex;
        public readonly float MinU;
        public readonly float MaxU;
        public readonly float MinV;
        public readonly float MaxV;
        public readonly int Lod;
        public readonly int Resolution;
        public readonly float PlanetRadiusMeters;

        public ChunkMeshBuildRequest(
            int faceIndex,
            float minU,
            float maxU,
            float minV,
            float maxV,
            int lod,
            int resolution,
            float planetRadiusMeters
        )
        {
            FaceIndex = faceIndex;
            MinU = minU;
            MaxU = maxU;
            MinV = minV;
            MaxV = maxV;
            Lod = lod;
            Resolution = resolution;
            PlanetRadiusMeters = planetRadiusMeters;
        }

        internal PlanetFlightChunkMeshPipeline.ChunkMeshBuildRequest ToShared()
        {
            return new PlanetFlightChunkMeshPipeline.ChunkMeshBuildRequest(
                FaceIndex,
                MinU,
                MaxU,
                MinV,
                MaxV,
                Lod,
                Resolution,
                PlanetRadiusMeters
            );
        }
    }

    public class FlightChunkMeshCpuData : PlanetFlightChunkMeshPipeline.ChunkMeshCpuData
    {
    }

    public static bool TryGetCachedMesh(
        ChunkMeshBuildRequest request,
        out Mesh mesh
    )
    {
        return PlanetFlightChunkMeshPipeline.TryGetCachedMesh(
            request.ToShared(),
            PlanetFlightChunkMeshCache.BuildStyle.SandboxLandOcean,
            out mesh
        );
    }

    public static void StartBuild(
        ChunkMeshBuildRequest request,
        PlanetElevationMmapField elevationField,
        int generation,
        Func<bool> isStillValid,
        Action<Mesh> onComplete,
        Action onAbandoned = null
    )
    {
        PlanetFlightChunkMeshPipeline.StartBuild(
            request.ToShared(),
            PlanetFlightChunkMeshCache.BuildStyle.SandboxLandOcean,
            elevationField,
            flightParams: null,
            generation,
            isStillValid,
            onComplete,
            onAbandoned
        );
    }

    public static void DispatchCompletedMeshes(ref int completionBudgetRemaining)
    {
        PlanetFlightChunkMeshPipeline.DispatchCompletedMeshes(ref completionBudgetRemaining);
    }

    public static void DispatchCompletedMeshes()
    {
        PlanetFlightChunkMeshPipeline.DispatchCompletedMeshes();
    }

    public static void CancelAll()
    {
        PlanetFlightChunkMeshPipeline.CancelAll();
    }

    public static int PendingCount => PlanetFlightChunkMeshPipeline.PendingCount;

    public static FlightChunkMeshCpuData ComputeMeshCpuDataPublic(
        ChunkMeshBuildRequest request,
        PlanetElevationMmapField elevationFieldFallback
    )
    {
        PlanetFlightChunkMeshPipeline.ChunkMeshCpuData built = PlanetFlightChunkCpuBuilder.Build(
            request.ToShared(),
            elevationFieldFallback,
            PlanetFlightChunkMeshCache.BuildStyle.SandboxLandOcean
        );
        return new FlightChunkMeshCpuData
        {
            Vertices = built.Vertices,
            Triangles = built.Triangles,
            Colors = built.Colors,
            GeographicUvs = built.GeographicUvs,
            DetailUvs = built.DetailUvs,
        };
    }
}
