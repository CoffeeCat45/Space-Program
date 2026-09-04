using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Shared async flight chunk mesh pipeline (sandbox + main game).
/// CPU build on thread pool; Unity Mesh upload on main thread only.
/// </summary>
public static class PlanetFlightChunkMeshPipeline
{
    public const int DefaultMaxConcurrentBuilds = 12;

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
        public readonly bool UseLandOceanVertexPaint;

        public ChunkMeshBuildRequest(
            int faceIndex,
            float minU,
            float maxU,
            float minV,
            float maxV,
            int lod,
            int resolution,
            float planetRadiusMeters,
            bool useLandOceanVertexPaint = false
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
            UseLandOceanVertexPaint = useLandOceanVertexPaint;
        }
    }

    public class ChunkMeshCpuData
    {
        public Vector3[] Vertices;
        public int[] Triangles;
        public Color[] Colors;
        public Vector2[] GeographicUvs;
        public Vector2[] DetailUvs;
    }

    struct BuildWaiter
    {
        public Func<bool> IsStillValid;
        public Action<Mesh> OnComplete;
        public Action OnAbandoned;
    }

    sealed class PendingBuild
    {
        public ChunkMeshBuildRequest Request;
        public PlanetFlightChunkMeshCache.ChunkMeshKey CacheKey;
        public string MeshName;
        public Task<ChunkMeshCpuData> Task;
        public readonly List<BuildWaiter> Waiters = new List<BuildWaiter>(4);
    }

    static readonly List<PendingBuild> PendingBuilds = new List<PendingBuild>(64);
    static readonly Dictionary<PlanetFlightChunkMeshCache.ChunkMeshKey, PendingBuild> ActiveBuildsByKey =
        new Dictionary<PlanetFlightChunkMeshCache.ChunkMeshKey, PendingBuild>(64);

    static int maxConcurrentBuilds = DefaultMaxConcurrentBuilds;

    public static int MaxConcurrentBuilds
    {
        get => maxConcurrentBuilds;
        set => maxConcurrentBuilds = Mathf.Clamp(value, 4, 32);
    }

    public static bool TryGetCachedMesh(
        ChunkMeshBuildRequest request,
        PlanetFlightChunkMeshCache.BuildStyle style,
        out Mesh mesh
    )
    {
        PlanetFlightChunkMeshCache.ChunkMeshKey cacheKey = PlanetFlightChunkMeshCache.CreateKey(
            request.FaceIndex,
            request.MinU,
            request.MaxU,
            request.MinV,
            request.MaxV,
            request.Lod,
            request.Resolution,
            style,
            request.UseLandOceanVertexPaint
        );
        return PlanetFlightChunkMeshCache.TryGetCached(cacheKey, out mesh);
    }

    public static bool HasActiveBuild(PlanetFlightChunkMeshCache.ChunkMeshKey cacheKey)
    {
        return ActiveBuildsByKey.ContainsKey(cacheKey);
    }

    public static bool CanAcceptNewBuild(bool priority)
    {
        return priority || ActiveBuildsByKey.Count < maxConcurrentBuilds;
    }

    public static void StartBuild(
        ChunkMeshBuildRequest request,
        PlanetFlightChunkMeshCache.BuildStyle style,
        PlanetElevationMmapField elevationField,
        PlanetFlightChunkCpuBuilder.FlightBiomeBuildParams? flightParams,
        int generation,
        Func<bool> isStillValid,
        Action<Mesh> onComplete,
        Action onAbandoned = null,
        bool priority = false
    )
    {
        PlanetFlightChunkMeshCache.ChunkMeshKey cacheKey = PlanetFlightChunkMeshCache.CreateKey(
            request.FaceIndex,
            request.MinU,
            request.MaxU,
            request.MinV,
            request.MaxV,
            request.Lod,
            request.Resolution,
            style,
            request.UseLandOceanVertexPaint
        );

        if (PlanetFlightChunkMeshCache.TryGetCached(cacheKey, out Mesh cachedMesh) &&
            cachedMesh != null)
        {
            onComplete?.Invoke(cachedMesh);
            return;
        }

        BuildWaiter waiter = new BuildWaiter
        {
            IsStillValid = isStillValid,
            OnComplete = onComplete,
            OnAbandoned = onAbandoned,
        };

        if (ActiveBuildsByKey.TryGetValue(cacheKey, out PendingBuild existing))
        {
            existing.Waiters.Clear();
            existing.Waiters.Add(waiter);
            return;
        }

        if (ActiveBuildsByKey.Count >= maxConcurrentBuilds && !priority)
        {
            onAbandoned?.Invoke();
            return;
        }

        if (ActiveBuildsByKey.Count >= maxConcurrentBuilds * 2)
        {
            onAbandoned?.Invoke();
            return;
        }

        PendingBuild pending = new PendingBuild
        {
            Request = request,
            CacheKey = cacheKey,
            MeshName = BuildMeshName(request, style),
            Task = Task.Run(() =>
                PlanetFlightChunkCpuBuilder.Build(
                    request,
                    elevationField,
                    style,
                    flightParams
                )
            ),
        };
        pending.Waiters.Add(waiter);
        PendingBuilds.Add(pending);
        ActiveBuildsByKey[cacheKey] = pending;
    }

    public static void DispatchCompletedMeshes(ref int completionBudgetRemaining)
    {
        if (completionBudgetRemaining <= 0)
        {
            return;
        }

        for (int i = PendingBuilds.Count - 1; i >= 0; i--)
        {
            if (completionBudgetRemaining <= 0)
            {
                return;
            }

            PendingBuild pending = PendingBuilds[i];
            if (!pending.Task.IsCompleted)
            {
                continue;
            }

            PendingBuilds.RemoveAt(i);
            ActiveBuildsByKey.Remove(pending.CacheKey);

            ChunkMeshCpuData cpuData;

            try
            {
                cpuData = pending.Task.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PlanetFlightChunkMeshPipeline] Async build failed: " + exception.Message
                );
                NotifyWaiters(pending, null);
                continue;
            }

            if (cpuData == null)
            {
                NotifyWaiters(pending, null);
                continue;
            }

            Mesh mesh = PlanetFlightChunkMeshCache.FinalizeFromCpuData(
                cpuData,
                pending.MeshName,
                pending.CacheKey
            );
            NotifyWaiters(pending, mesh);
            completionBudgetRemaining--;
        }
    }

    static void NotifyWaiters(PendingBuild pending, Mesh mesh)
    {
        for (int i = 0; i < pending.Waiters.Count; i++)
        {
            BuildWaiter waiter = pending.Waiters[i];

            if (mesh == null)
            {
                waiter.OnAbandoned?.Invoke();
                continue;
            }

            if (waiter.IsStillValid != null && !waiter.IsStillValid())
            {
                waiter.OnAbandoned?.Invoke();
                continue;
            }

            waiter.OnComplete?.Invoke(mesh);
        }
    }

    public static void DispatchCompletedMeshes()
    {
        int unlimited = int.MaxValue;
        DispatchCompletedMeshes(ref unlimited);
    }

    public static void CancelAll()
    {
        for (int i = PendingBuilds.Count - 1; i >= 0; i--)
        {
            NotifyWaiters(PendingBuilds[i], null);
        }

        PendingBuilds.Clear();
        ActiveBuildsByKey.Clear();
    }

    public static int PendingCount => ActiveBuildsByKey.Count;

    public static int PendingWaiterCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < PendingBuilds.Count; i++)
            {
                count += PendingBuilds[i].Waiters.Count;
            }

            return count;
        }
    }

    public static int CollectPendingBuildLabels(System.Collections.Generic.List<string> sink, int maxItems)
    {
        if (sink == null || maxItems <= 0)
        {
            return 0;
        }

        int added = 0;

        for (int i = 0; i < PendingBuilds.Count && added < maxItems; i++)
        {
            PendingBuild pending = PendingBuilds[i];
            string label = pending.MeshName;

            if (pending.Waiters.Count > 1)
            {
                label += " x" + pending.Waiters.Count;
            }

            if (pending.Task.IsCompleted)
            {
                label += " (ready)";
            }
            else if (!pending.Task.IsFaulted)
            {
                label += " (cpu)";
            }

            sink.Add(label);
            added++;
        }

        return added;
    }

    static string BuildMeshName(
        ChunkMeshBuildRequest request,
        PlanetFlightChunkMeshCache.BuildStyle style
    )
    {
        string prefix = style == PlanetFlightChunkMeshCache.BuildStyle.SandboxLandOcean ?
            "SandboxFlightChunk" :
            "FlightChunk";
        return prefix + "_F" + request.FaceIndex + "_L" + request.Lod;
    }
}
