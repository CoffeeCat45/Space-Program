using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Earth / flight planet LOD mesh cache (face + LOD + UV bounds + style).
/// Scene instances use <c>Instantiate</c> copies; templates stay here with TTL eviction.
/// </summary>
public static class PlanetFlightChunkMeshCache
{
    public const int DefaultGridResolution = 33;
    public const int CoarseGridResolution = 17;
    public const int FineGridResolution = 49;
    public const int UltraGridResolution = 65;

    public const double DefaultEntryTtlSeconds = 300.0;
    public const int DefaultMaxCacheEntries = 2048;

    const int CacheQuantization = 100000;

    static readonly Dictionary<ChunkMeshKey, CacheEntry> Cache =
        new Dictionary<ChunkMeshKey, CacheEntry>(DefaultMaxCacheEntries);

    static readonly LinkedList<ChunkMeshKey> LruOrder = new LinkedList<ChunkMeshKey>();

    static readonly Dictionary<ChunkMeshKey, LinkedListNode<ChunkMeshKey>> LruNodes =
        new Dictionary<ChunkMeshKey, LinkedListNode<ChunkMeshKey>>();

    static double lastPurgeUtcSeconds;
    static int maxCacheEntries = DefaultMaxCacheEntries;
    static double entryTtlSeconds = DefaultEntryTtlSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Cache.Clear();
        LruOrder.Clear();
        LruNodes.Clear();
        lastPurgeUtcSeconds = 0.0;
    }

    public static int Count => Cache.Count;

    public static int MaxCacheEntries
    {
        get => maxCacheEntries;
        set => maxCacheEntries = Mathf.Max(64, value);
    }

    public static double EntryTtlSeconds
    {
        get => entryTtlSeconds;
        set => entryTtlSeconds = Math.Max(30.0, value);
    }

    public enum BuildStyle : byte
    {
        SandboxLandOcean = 0,
        FlightBiome = 1,
    }

    sealed class CacheEntry
    {
        public Mesh Mesh;
        public double LastAccessUtcSeconds;
    }

    public readonly struct ChunkMeshKey : IEquatable<ChunkMeshKey>
    {
        public readonly int FaceIndex;
        public readonly int Lod;
        public readonly int Resolution;
        public readonly BuildStyle Style;
        public readonly int MinUi;
        public readonly int MaxUi;
        public readonly int MinVi;
        public readonly int MaxVi;
        public readonly bool LandOceanVertexPaint;
        public readonly int BodyCode;
        public readonly byte FormatVersion;

        public const byte CurrentFormatVersion = 10;

        ChunkMeshKey(
            int faceIndex,
            int lod,
            int resolution,
            BuildStyle style,
            int minUi,
            int maxUi,
            int minVi,
            int maxVi,
            bool landOceanVertexPaint,
            int bodyCode,
            byte formatVersion
        )
        {
            FaceIndex = faceIndex;
            Lod = lod;
            Resolution = resolution;
            Style = style;
            MinUi = minUi;
            MaxUi = maxUi;
            MinVi = minVi;
            MaxVi = maxVi;
            LandOceanVertexPaint = landOceanVertexPaint;
            BodyCode = bodyCode;
            FormatVersion = formatVersion;
        }

        public static ChunkMeshKey Create(
            int faceIndex,
            float minU,
            float maxU,
            float minV,
            float maxV,
            int lod,
            int resolution,
            BuildStyle style,
            bool landOceanVertexPaint = false
        )
        {
            return new ChunkMeshKey(
                faceIndex,
                lod,
                resolution,
                style,
                Mathf.RoundToInt(minU * CacheQuantization),
                Mathf.RoundToInt(maxU * CacheQuantization),
                Mathf.RoundToInt(minV * CacheQuantization),
                Mathf.RoundToInt(maxV * CacheQuantization),
                style == BuildStyle.SandboxLandOcean || landOceanVertexPaint,
                CelestialTerrainRuntime.IsMoon ? 1 : 0,
                CurrentFormatVersion
            );
        }

        public bool Equals(ChunkMeshKey other)
        {
            return FaceIndex == other.FaceIndex &&
                Lod == other.Lod &&
                Resolution == other.Resolution &&
                Style == other.Style &&
                MinUi == other.MinUi &&
                MaxUi == other.MaxUi &&
                MinVi == other.MinVi &&
                MaxVi == other.MaxVi &&
                LandOceanVertexPaint == other.LandOceanVertexPaint &&
                BodyCode == other.BodyCode &&
                FormatVersion == other.FormatVersion;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkMeshKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = FaceIndex;
                hash = (hash * 397) ^ Lod;
                hash = (hash * 397) ^ Resolution;
                hash = (hash * 397) ^ (int)Style;
                hash = (hash * 397) ^ MinUi;
                hash = (hash * 397) ^ MaxUi;
                hash = (hash * 397) ^ MinVi;
                hash = (hash * 397) ^ MaxVi;
                hash = (hash * 397) ^ (LandOceanVertexPaint ? 1 : 0);
                hash = (hash * 397) ^ BodyCode;
                hash = (hash * 397) ^ FormatVersion;
                return hash;
            }
        }
    }

    public static int GridResolutionForLod(int lod)
    {
        if (lod <= 1)
        {
            return CoarseGridResolution;
        }

        if (lod <= 3)
        {
            return DefaultGridResolution;
        }

        if (lod <= 5)
        {
            return FineGridResolution;
        }

        return UltraGridResolution;
    }

    public static ChunkMeshKey CreateKey(
        int faceIndex,
        float minU,
        float maxU,
        float minV,
        float maxV,
        int lod,
        int resolution,
        BuildStyle style,
        bool landOceanVertexPaint = false
    )
    {
        return ChunkMeshKey.Create(
            faceIndex,
            minU,
            maxU,
            minV,
            maxV,
            lod,
            resolution,
            style,
            landOceanVertexPaint
        );
    }

    public static bool TryGetCached(ChunkMeshKey cacheKey, out Mesh mesh)
    {
        if (Cache.TryGetValue(cacheKey, out CacheEntry entry))
        {
            if (entry.Mesh != null)
            {
                Touch(cacheKey, entry);
                mesh = entry.Mesh;
                return true;
            }

            RemoveEntry(cacheKey);
        }

        mesh = null;
        return false;
    }

    public static void Clear()
    {
        foreach (KeyValuePair<ChunkMeshKey, CacheEntry> entry in Cache)
        {
            if (entry.Value.Mesh != null)
            {
                PlanetMeshPool.Release(entry.Value.Mesh);
            }
        }

        Cache.Clear();
        LruOrder.Clear();
        LruNodes.Clear();
        lastPurgeUtcSeconds = 0.0;
    }

    /// <summary>
    /// Drops expired entries and enforces LRU capacity. Call once per LOD tick.
    /// </summary>
    public static void PurgeStaleEntries()
    {
        double now = GetUtcNowSeconds();
        if (now - lastPurgeUtcSeconds < 1.0 && Cache.Count <= maxCacheEntries)
        {
            return;
        }

        lastPurgeUtcSeconds = now;
        List<ChunkMeshKey> expiredKeys = null;

        foreach (KeyValuePair<ChunkMeshKey, CacheEntry> pair in Cache)
        {
            if (now - pair.Value.LastAccessUtcSeconds <= entryTtlSeconds)
            {
                continue;
            }

            expiredKeys ??= new List<ChunkMeshKey>(32);
            expiredKeys.Add(pair.Key);
        }

        if (expiredKeys != null)
        {
            for (int i = 0; i < expiredKeys.Count; i++)
            {
                RemoveEntry(expiredKeys[i]);
            }
        }

        while (Cache.Count > maxCacheEntries)
        {
            EvictOldest();
        }
    }

    public static Mesh FinalizeFromCpuData(
        PlanetFlightChunkMeshPipeline.ChunkMeshCpuData data,
        string meshName,
        ChunkMeshKey cacheKey
    )
    {
        if (TryGetCached(cacheKey, out Mesh cachedMesh))
        {
            return cachedMesh;
        }

        PlanetMeshWindingUtility.EnsureRenderableOutwardNormals(data.Vertices, data.Triangles);
        SanitizeVerticesInPlace(data.Vertices);

        int vertexCount = data.Vertices.Length;
        var mesh = PlanetMeshPool.Rent(meshName, vertexCount);
        mesh.vertices = data.Vertices;
        mesh.uv = data.GeographicUvs;

        if (data.Colors != null)
        {
            mesh.colors = data.Colors;
        }

        if (data.DetailUvs != null)
        {
            mesh.SetUVs(1, data.DetailUvs);
        }

        mesh.triangles = data.Triangles;
        AssignRadialOutwardNormals(mesh, data.Vertices);
        mesh.RecalculateBounds();

        while (Cache.Count >= maxCacheEntries)
        {
            EvictOldest();
        }

        var entry = new CacheEntry
        {
            Mesh = mesh,
            LastAccessUtcSeconds = GetUtcNowSeconds(),
        };
        Cache[cacheKey] = entry;
        Touch(cacheKey, entry);
        return mesh;
    }

    static void Touch(ChunkMeshKey cacheKey, CacheEntry entry)
    {
        entry.LastAccessUtcSeconds = GetUtcNowSeconds();

        if (LruNodes.TryGetValue(cacheKey, out LinkedListNode<ChunkMeshKey> node))
        {
            LruOrder.Remove(node);
        }

        LinkedListNode<ChunkMeshKey> newNode = LruOrder.AddFirst(cacheKey);
        LruNodes[cacheKey] = newNode;
    }

    static void EvictOldest()
    {
        if (LruOrder.Last == null)
        {
            return;
        }

        RemoveEntry(LruOrder.Last.Value);
    }

    static void RemoveEntry(ChunkMeshKey cacheKey)
    {
        if (!Cache.TryGetValue(cacheKey, out CacheEntry entry))
        {
            return;
        }

        Cache.Remove(cacheKey);

        if (LruNodes.TryGetValue(cacheKey, out LinkedListNode<ChunkMeshKey> node))
        {
            LruOrder.Remove(node);
            LruNodes.Remove(cacheKey);
        }

        if (entry.Mesh != null)
        {
            PlanetMeshPool.Release(entry.Mesh);
        }
    }

    static double GetUtcNowSeconds()
    {
        return DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
    }

    /// <summary>
    /// Per-chunk RecalculateNormals creates lighting creases at shared edges.
    /// Radial normals from planet-local vertex positions are continuous across chunks.
    /// </summary>
    internal static void AssignRadialOutwardNormals(Mesh mesh, Vector3[] vertices)
    {
        if (mesh == null || vertices == null || vertices.Length == 0)
        {
            return;
        }

        var normals = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            float magnitude = vertex.magnitude;
            normals[i] = magnitude > 1e-8f ? vertex / magnitude : Vector3.up;
        }

        mesh.normals = normals;
    }

    static void SanitizeVerticesInPlace(Vector3[] vertices)
    {
        if (vertices == null)
        {
            return;
        }

        float fallbackRadius = CelestialTerrainRuntime.LodBodyRadiusUnity;
        if (fallbackRadius <= 1f)
        {
            fallbackRadius = PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];

            if (
                !float.IsNaN(vertex.x) &&
                !float.IsNaN(vertex.y) &&
                !float.IsNaN(vertex.z) &&
                !float.IsInfinity(vertex.x) &&
                !float.IsInfinity(vertex.y) &&
                !float.IsInfinity(vertex.z)
            )
            {
                continue;
            }

            Vector3 direction = vertex.sqrMagnitude > 1e-8f ?
                vertex.normalized :
                Vector3.up;
            vertices[i] = direction * fallbackRadius;
        }
    }
}

/// <summary>Alias for documentation / tooling referring to Earth chunk cache.</summary>
public static class PlanetEarthChunkMeshCache
{
    public static int Count => PlanetFlightChunkMeshCache.Count;

    public static void Clear() => PlanetFlightChunkMeshCache.Clear();

    public static void PurgeStaleEntries() => PlanetFlightChunkMeshCache.PurgeStaleEntries();
}
