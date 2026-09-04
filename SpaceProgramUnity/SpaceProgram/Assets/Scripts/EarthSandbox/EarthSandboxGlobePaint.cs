using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Stage-4 UV sphere (work_terrain.md): Mesh Frame positions, canonical geographic UV.
/// Winding/normals match the sandbox that passed Stage 3 + visual calibration.
/// Stage A: GEBCO Int16 relief via mmap, displaced along mesh-frame normals (× UnityVisualHeightScale).
/// </summary>
public static class EarthSandboxGlobePaint
{
    private const float MaxLatitudeDegrees = 85f;

    private static ElevationField _sharedElevation;
    private static bool _sharedElevationAttempted;

    /// <summary>
    /// At sandbox mini-globe radius (~100), honest 1:10 relief is sub-pixel. Multiply only for visual QA.
    /// Flight mesh at r=637100 uses exaggeration 1 automatically.
    /// </summary>
    public const float DefaultMiniGlobeReliefExaggeration = 10f;

    private static float _sharedReliefExaggeration = 1f;

    /// <summary>
    /// GEBCO meters → radial mesh offset. Matches flight mesh at
    /// <see cref="PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters"/>; scales down for mini-globes.
    /// </summary>
    public static float ElevationMetersToVisualOffsetMeters(
        double elevationMeters,
        float globeRadiusMeters,
        float reliefExaggeration = 1f
    )
    {
        float radiusScale = globeRadiusMeters / PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters;
        return (float)(
            elevationMeters *
            PlanetSurfaceConstants.UnityVisualHeightScale *
            radiusScale *
            reliefExaggeration
        );
    }

    /// <summary>1 at flight radius; <see cref="DefaultMiniGlobeReliefExaggeration"/> for sandbox mini-globes.</summary>
    public static float ResolveReliefExaggeration(float globeRadiusMeters, float reliefExaggeration)
    {
        if (reliefExaggeration > 0f)
        {
            return reliefExaggeration;
        }

        if (globeRadiusMeters < PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters * 0.01f)
        {
            return DefaultMiniGlobeReliefExaggeration;
        }

        return 1f;
    }

    /// <summary>Mmap-backed GEBCO sampler for sandbox globe relief (clean canon u/v, no runtime flips).</summary>
    public sealed class ElevationField : IDisposable
    {
        private PlanetElevationMmapField _mmapField;
        private bool _disposed;

        public string SourcePath { get; private set; }

        public bool IsOpen => _mmapField != null && !_disposed;

        public static bool TryOpenDefault(out ElevationField field)
        {
            string path = Path.Combine(
                Application.streamingAssetsPath,
                "Planet",
                PlanetElevationRawFormat.DefaultFileName
            );
            return TryOpenFile(path, out field);
        }

        public static bool TryOpenFile(string absolutePath, out ElevationField field)
        {
            field = null;

            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return false;
            }

            try
            {
                field = new ElevationField
                {
                    _mmapField = PlanetElevationMmapField.OpenFile(absolutePath),
                    SourcePath = absolutePath,
                };
                PlanetElevationCatalog.LoadHighResFromFile(absolutePath);
                Debug.Log(
                    "[EarthSandbox] Elevation mmap: " + absolutePath +
                    " | LOD coarse RAM=" + (PlanetElevationCatalog.IsCoarseLodLoaded ? "yes" : "no") +
                    " mid=" + (PlanetElevationCatalog.IsMidLodLoaded ? "yes" : "lazy") +
                    " highMid=" + (PlanetElevationCatalog.IsHighMidLodLoaded ? "yes" : "lazy")
                );
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[EarthSandbox] Elevation mmap open failed: " + absolutePath + " — " + exception.Message
                );
                field = null;
                return false;
            }
        }

        public double SampleElevationMetersAtDegrees(float latitudeDegrees, float longitudeDegrees)
        {
            if (!IsOpen)
            {
                return 0.0;
            }

            double latitudeRadians = latitudeDegrees * (Math.PI / 180.0);
            double longitudeRadians = longitudeDegrees * (Math.PI / 180.0);
            return PlanetElevationHighResSampler.GetHeightBilinearNew(
                _mmapField,
                latitudeRadians,
                longitudeRadians
            );
        }

        public double SampleElevationMetersAtGeographicUv(double geographicU, double geographicV)
        {
            PlanetGeographicCoords.GeographicUvToLatitudeLongitude(
                geographicU,
                geographicV,
                out double latitudeRadians,
                out double longitudeRadians
            );
            return PlanetElevationHighResSampler.GetHeightBilinearNew(
                _mmapField,
                latitudeRadians,
                longitudeRadians
            );
        }

        public float SampleVisualOffsetMetersAtDegrees(
            float latitudeDegrees,
            float longitudeDegrees,
            float globeRadiusMeters,
            float reliefExaggeration
        )
        {
            return ElevationMetersToVisualOffsetMeters(
                SampleElevationMetersAtDegrees(latitudeDegrees, longitudeDegrees),
                globeRadiusMeters,
                reliefExaggeration
            );
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _mmapField?.Dispose();
            _mmapField = null;
            SourcePath = null;
        }

        public PlanetElevationMmapField GetMmapFieldForAsyncSampling()
        {
            return IsOpen ? _mmapField : null;
        }
    }

    public static Vector3 MeshUnitFromLatitudeLongitudeDegrees(float latitudeDegrees, float longitudeDegrees)
    {
        SimVector3 direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            latitudeDegrees * (Math.PI / 180.0),
            longitudeDegrees * (Math.PI / 180.0)
        );

        return new Vector3((float)direction.x, (float)direction.y, (float)direction.z);
    }

    public static Vector3 MeshDisplayUnitFromLatitudeLongitudeDegrees(
        float latitudeDegrees,
        float longitudeDegrees
    )
    {
        Vector3 simUnit = MeshUnitFromLatitudeLongitudeDegrees(
            latitudeDegrees,
            longitudeDegrees
        );
        return PlanetCubeSphere.SimDirectionToMeshFrame(simUnit);
    }

    /// <summary>
    /// Mesh-frame surface point for a geographic marker, including GEBCO relief (× UnityVisualHeightScale).
    /// </summary>
    public static Vector3 MeshDisplayPositionFromLatitudeLongitudeDegrees(
        float latitudeDegrees,
        float longitudeDegrees,
        float radius,
        float markerHalfExtent,
        ElevationField elevation = null,
        float reliefExaggeration = 0f
    )
    {
        Vector3 displayUnit = MeshDisplayUnitFromLatitudeLongitudeDegrees(
            latitudeDegrees,
            longitudeDegrees
        );
        elevation = ResolveElevation(elevation);
        float exaggeration = reliefExaggeration > 0f ?
            reliefExaggeration :
            _sharedReliefExaggeration;
        float visualOffsetMeters = elevation != null ?
            elevation.SampleVisualOffsetMetersAtDegrees(
                latitudeDegrees,
                longitudeDegrees,
                radius,
                exaggeration
            ) :
            0f;
        return displayUnit * (radius + visualOffsetMeters + markerHalfExtent);
    }

    public static void ReleaseSharedElevation()
    {
        _sharedElevation?.Dispose();
        _sharedElevation = null;
        _sharedElevationAttempted = false;
    }

    static ElevationField ResolveElevation(ElevationField elevation)
    {
        if (elevation != null)
        {
            return elevation;
        }

        if (!_sharedElevationAttempted)
        {
            _sharedElevationAttempted = true;
            if (!ElevationField.TryOpenDefault(out _sharedElevation))
            {
                _sharedElevation = null;
            }
        }

        return _sharedElevation;
    }

    public static Mesh BuildCanonicalUvSphere(
        float radius,
        int latitudeSegments,
        int longitudeSegments,
        EarthSandboxGeographicRaster raster = null,
        ElevationField elevation = null,
        float reliefExaggeration = 0f
    )
    {
        elevation = ResolveElevation(elevation);
        reliefExaggeration = ResolveReliefExaggeration(radius, reliefExaggeration);
        _sharedReliefExaggeration = reliefExaggeration;
        int ringVerts = longitudeSegments;
        int gridVertCount = (latitudeSegments + 1) * ringVerts;
        // Duplicate lon=0 vertices with U=1 so seam quads never interpolate 0.99→0 across the texture.
        int seamDuplicateCount = latitudeSegments + 1;
        int seamDuplicateStart = gridVertCount;
        int vertCount = gridVertCount + seamDuplicateCount + 2;
        var vertices = new Vector3[vertCount];
        var geographicUvs = new Vector2[vertCount];
        var vertexColors = new Color[vertCount];

        int vertIndex = 0;
        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float lat01 = lat / (float)latitudeSegments;
            float latitudeDegrees = Mathf.Lerp(-MaxLatitudeDegrees, MaxLatitudeDegrees, lat01);

            for (int lon = 0; lon < ringVerts; lon++)
            {
                WriteLatitudeRingVertex(
                    vertices,
                    geographicUvs,
                    vertexColors,
                    vertIndex,
                    latitudeDegrees,
                    LonDegreesForRingIndex(lon, ringVerts),
                    radius,
                    raster,
                    elevation,
                    reliefExaggeration
                );
                vertIndex++;
            }
        }

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            int westMeridianIndex = lat * ringVerts;
            int seamIndex = seamDuplicateStart + lat;
            vertices[seamIndex] = vertices[westMeridianIndex];
            geographicUvs[seamIndex] = new Vector2(1f, geographicUvs[westMeridianIndex].y);
            vertexColors[seamIndex] = vertexColors[westMeridianIndex];
        }

        int southPoleIndex = gridVertCount + seamDuplicateCount;
        int northPoleIndex = southPoleIndex + 1;
        AddPoleVertex(
            vertices,
            geographicUvs,
            vertexColors,
            southPoleIndex,
            -1f,
            radius,
            raster,
            elevation,
            reliefExaggeration
        );
        AddPoleVertex(
            vertices,
            geographicUvs,
            vertexColors,
            northPoleIndex,
            1f,
            radius,
            raster,
            elevation,
            reliefExaggeration
        );

        int bodyTriCount = latitudeSegments * longitudeSegments * 6;
        int capTriCount = longitudeSegments * 6;
        int triCount = bodyTriCount + capTriCount * 2;
        var triangles = new int[triCount];
        int triangleIndex = 0;

        // Lat grows north (not default Unity sphere tutorials).
        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            for (int lon = 0; lon < ringVerts; lon++)
            {
                int current = lat * ringVerts + lon;
                int next = current + ringVerts;
                int east = lon + 1 < ringVerts ?
                    current + 1 :
                    seamDuplicateStart + lat;
                int eastNext = lon + 1 < ringVerts ?
                    next + 1 :
                    seamDuplicateStart + lat + 1;

                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = east;
                triangles[triangleIndex++] = next;

                triangles[triangleIndex++] = east;
                triangles[triangleIndex++] = eastNext;
                triangles[triangleIndex++] = next;
            }
        }

        var allTriangles = triangles;
        WriteSouthCapTriangles(
            allTriangles,
            bodyTriCount,
            ringVerts,
            southPoleIndex,
            seamDuplicateStart
        );
        WriteNorthCapTriangles(
            allTriangles,
            bodyTriCount + capTriCount,
            ringVerts,
            latitudeSegments,
            northPoleIndex,
            seamDuplicateStart
        );

        // Mesh Frame X-flip: fix whole shell, then flip pole caps once (caps need opposite chirality at seam).
        PlanetMeshWindingUtility.EnsureRenderableOutwardNormals(vertices, allTriangles);
        FlipCapTrianglesToMatchBody(allTriangles, bodyTriCount, capTriCount);
        FlipCapTrianglesToMatchBody(allTriangles, bodyTriCount + capTriCount, capTriCount);

        var mesh = new Mesh
        {
            name = "EarthSandboxCanonicalUvSphere",
        };
        mesh.vertices = vertices;
        mesh.uv = geographicUvs;
        mesh.colors = vertexColors;
        mesh.triangles = allTriangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float LonDegreesForRingIndex(int lonIndex, int ringVerts)
    {
        return -180f + (360f * lonIndex / ringVerts);
    }

    private static float SampleVisualOffsetMeters(
        ElevationField elevation,
        double geographicU,
        double geographicV,
        float globeRadiusMeters,
        float reliefExaggeration
    )
    {
        if (elevation == null || !elevation.IsOpen)
        {
            return 0f;
        }

        double elevationMeters = elevation.SampleElevationMetersAtGeographicUv(geographicU, geographicV);
        return ElevationMetersToVisualOffsetMeters(
            elevationMeters,
            globeRadiusMeters,
            reliefExaggeration
        );
    }

    private static void WriteLatitudeRingVertex(
        Vector3[] vertices,
        Vector2[] geographicUvs,
        Color[] vertexColors,
        int vertIndex,
        float latitudeDegrees,
        float longitudeDegrees,
        float radius,
        EarthSandboxGeographicRaster raster,
        ElevationField elevation,
        float reliefExaggeration
    )
    {
        double latitudeRadians = latitudeDegrees * (Math.PI / 180.0);
        double longitudeRadians = longitudeDegrees * (Math.PI / 180.0);

        Vector3 simUnit = MeshUnitFromLatitudeLongitudeDegrees(
            latitudeDegrees,
            longitudeDegrees
        );
        Vector3 meshUnit = PlanetCubeSphere.SimDirectionToMeshFrame(simUnit);

        double geographicU = PlanetGeographicCoords.LongitudeRadiansToGeographicU(longitudeRadians);
        double geographicV = PlanetGeographicCoords.LatitudeRadiansToGeographicV(latitudeRadians);
        float visualOffsetMeters = SampleVisualOffsetMeters(
            elevation,
            geographicU,
            geographicV,
            radius,
            reliefExaggeration
        );
        vertices[vertIndex] = meshUnit * (radius + visualOffsetMeters);

        geographicUvs[vertIndex] = new Vector2((float)geographicU, (float)geographicV);

        if (raster != null)
        {
            Color32 pixel = raster.SampleCanonicalPixel(latitudeDegrees, longitudeDegrees);
            vertexColors[vertIndex] = new Color(
                pixel.r / 255f,
                pixel.g / 255f,
                pixel.b / 255f,
                1f
            );
        }
        else
        {
            vertexColors[vertIndex] = Color.gray;
        }
    }

    private static void AddPoleVertex(
        Vector3[] vertices,
        Vector2[] geographicUvs,
        Color[] vertexColors,
        int index,
        float poleY,
        float radius,
        EarthSandboxGeographicRaster raster,
        ElevationField elevation,
        float reliefExaggeration
    )
    {
        float latitudeDegrees = poleY < 0f ? -90f : 90f;
        Vector3 unit = new Vector3(0f, poleY, 0f);
        double latitudeRadians = latitudeDegrees * (Math.PI / 180.0);
        double geographicV = PlanetGeographicCoords.LatitudeRadiansToGeographicV(latitudeRadians);
        float visualOffsetMeters = SampleVisualOffsetMeters(
            elevation,
            0.5,
            geographicV,
            radius,
            reliefExaggeration
        );
        vertices[index] = unit * (radius + visualOffsetMeters);

        geographicUvs[index] = new Vector2(
            0.5f,
            (float)geographicV
        );

        if (raster != null)
        {
            Color32 pixel = raster.SampleCanonicalPixel(latitudeDegrees, 0f);
            vertexColors[index] = new Color(
                pixel.r / 255f,
                pixel.g / 255f,
                pixel.b / 255f,
                1f
            );
        }
        else
        {
            vertexColors[index] = Color.gray;
        }
    }

    private static void WriteSouthCapTriangles(
        int[] triangles,
        int writeIndex,
        int ringVerts,
        int southPoleIndex,
        int seamDuplicateStart
    )
    {
        for (int lon = 0; lon < ringVerts; lon++)
        {
            int ring0 = lon;
            int ring1 = lon + 1 < ringVerts ?
                lon + 1 :
                seamDuplicateStart;
            triangles[writeIndex++] = southPoleIndex;
            triangles[writeIndex++] = ring0;
            triangles[writeIndex++] = ring1;
        }
    }

    private static void WriteNorthCapTriangles(
        int[] triangles,
        int writeIndex,
        int ringVerts,
        int latitudeSegments,
        int northPoleIndex,
        int seamDuplicateStart
    )
    {
        int northRingStart = latitudeSegments * ringVerts;

        for (int lon = 0; lon < ringVerts; lon++)
        {
            int ring0 = northRingStart + lon;
            int ring1 = lon + 1 < ringVerts ?
                northRingStart + lon + 1 :
                seamDuplicateStart + latitudeSegments;
            triangles[writeIndex++] = northPoleIndex;
            triangles[writeIndex++] = ring1;
            triangles[writeIndex++] = ring0;
        }
    }

    private static void FlipCapTrianglesToMatchBody(int[] triangles, int startIndex, int triangleCount)
    {
        int endIndex = startIndex + triangleCount;

        for (int triangleIndex = startIndex; triangleIndex < endIndex; triangleIndex += 3)
        {
            int indexB = triangles[triangleIndex + 1];
            triangles[triangleIndex + 1] = triangles[triangleIndex + 2];
            triangles[triangleIndex + 2] = indexB;
        }
    }

    public static Material CreateGlobeMaterial(
        EarthSandboxGeographicRaster raster,
        bool useVertexPaintDebug = false
    )
    {
        Shader shader = SpaceProgramShaders.Find("SpaceProgram/SandboxEarthGlobe");
        if (shader == null)
        {
            Debug.LogError(
                "[EarthSandbox] Shader SpaceProgram/SandboxEarthGlobe not found. " +
                "Globe texture mapping will be wrong until the shader compiles."
            );
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        var material = new Material(shader);
        Texture2D gpuTexture = raster?.GpuTexture;

        if (gpuTexture != null && shader.name.Contains("SandboxEarthGlobe"))
        {
            material.SetTexture("_BaseMap", gpuTexture);
            material.SetColor("_BaseColor", Color.white);
        }
        else if (gpuTexture != null && shader.name.Contains("Universal Render Pipeline"))
        {
            material.SetTexture("_BaseMap", gpuTexture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        }
        else if (gpuTexture != null)
        {
            material.mainTexture = gpuTexture;
        }

        if (useVertexPaintDebug)
        {
            material.EnableKeyword("_SANDBOX_VERTEX_PAINT");
        }
        else
        {
            material.DisableKeyword("_SANDBOX_VERTEX_PAINT");
        }

        return material;
    }

    public const int FlightChunkGridResolution = PlanetFlightChunkMeshCache.DefaultGridResolution;

    public const int FlightChunkCoarseGridResolution = PlanetFlightChunkMeshCache.CoarseGridResolution;

    public const int FlightChunkFineGridResolution = PlanetFlightChunkMeshCache.FineGridResolution;

    public const int FlightChunkUltraGridResolution = PlanetFlightChunkMeshCache.UltraGridResolution;

    public static int FlightChunkMeshCacheCount => PlanetFlightChunkMeshCache.Count;

    public readonly struct FlightChunkMeshKey
    {
        readonly PlanetFlightChunkMeshCache.ChunkMeshKey _key;

        FlightChunkMeshKey(PlanetFlightChunkMeshCache.ChunkMeshKey key)
        {
            _key = key;
        }

        public static FlightChunkMeshKey Create(
            int faceIndex,
            float minU,
            float maxU,
            float minV,
            float maxV,
            int lod,
            int resolution
        )
        {
            return new FlightChunkMeshKey(
                PlanetFlightChunkMeshCache.CreateKey(
                    faceIndex,
                    minU,
                    maxU,
                    minV,
                    maxV,
                    lod,
                    resolution,
                    PlanetFlightChunkMeshCache.BuildStyle.SandboxLandOcean
                )
            );
        }

        internal PlanetFlightChunkMeshCache.ChunkMeshKey ToSharedKey()
        {
            return _key;
        }
    }

    public static int FlightChunkGridResolutionForLod(int lod)
    {
        return PlanetFlightChunkMeshCache.GridResolutionForLod(lod);
    }

    public static void ClearFlightChunkMeshCache()
    {
        PlanetFlightChunkMeshCache.Clear();
        PlanetMeshPool.Clear();
    }

    public static FlightChunkMeshKey CreateFlightChunkMeshKey(
        int faceIndex,
        float minU,
        float maxU,
        float minV,
        float maxV,
        int lod,
        int resolution
    )
    {
        return FlightChunkMeshKey.Create(faceIndex, minU, maxU, minV, maxV, lod, resolution);
    }

    public static bool TryGetCachedFlightChunkMesh(FlightChunkMeshKey cacheKey, out Mesh mesh)
    {
        return PlanetFlightChunkMeshCache.TryGetCached(cacheKey.ToSharedKey(), out mesh);
    }

    /// <summary>Main-thread only: winding fix + Mesh upload + cache insert.</summary>
    public static Mesh FinalizeFlightChunkMeshFromCpuData(
        EarthSandboxFlightChunkMeshPipeline.FlightChunkMeshCpuData data,
        string meshName,
        FlightChunkMeshKey cacheKey
    )
    {
        return PlanetFlightChunkMeshCache.FinalizeFromCpuData(data, meshName, cacheKey.ToSharedKey());
    }

    private static Transform _mapGlobeRoot;

    public static ElevationField GetSharedElevationField()
    {
        ResolveElevation(null);
        return _sharedElevation;
    }

    public static void RegisterMapGlobeRoot(Transform globeRoot)
    {
        _mapGlobeRoot = globeRoot;
    }

    public static void SetMapGlobeVisible(bool visible)
    {
        if (_mapGlobeRoot != null)
        {
            _mapGlobeRoot.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Cube-face quadtree leaf mesh at flight scale (r=637100, × UnityVisualHeightScale relief, Mesh Frame X-flip).
    /// </summary>
    public static Mesh BuildFlightChunkMesh(
        int faceIndex,
        float minU,
        float maxU,
        float minV,
        float maxV,
        float planetRadiusMeters = PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters,
        ElevationField elevation = null,
        int gridResolution = FlightChunkGridResolution,
        int lod = 0
    )
    {
        int resolution = Mathf.Clamp(gridResolution, 2, 256);
        FlightChunkMeshKey cacheKey = FlightChunkMeshKey.Create(
            faceIndex,
            minU,
            maxU,
            minV,
            maxV,
            lod,
            resolution
        );

        if (TryGetCachedFlightChunkMesh(cacheKey, out Mesh cachedMesh) && cachedMesh != null)
        {
            return cachedMesh;
        }

        var request = new EarthSandboxFlightChunkMeshPipeline.ChunkMeshBuildRequest(
            faceIndex,
            minU,
            maxU,
            minV,
            maxV,
            lod,
            resolution,
            planetRadiusMeters
        );
        PlanetElevationMmapField mmapField = elevation != null && elevation.IsOpen ?
            elevation.GetMmapFieldForAsyncSampling() :
            null;
        EarthSandboxFlightChunkMeshPipeline.FlightChunkMeshCpuData cpuData =
            EarthSandboxFlightChunkMeshPipeline.ComputeMeshCpuDataPublic(request, mmapField);

        return FinalizeFlightChunkMeshFromCpuData(
            cpuData,
            "EarthSandboxFlightChunk_F" + faceIndex,
            cacheKey
        );
    }

    /// <summary>URP lit material with vertex-color land/ocean (SandboxEarthGlobe vertex-paint path).</summary>
    public static Material CreateFlightChunkMaterial()
    {
        Material material = PlanetSandboxFlightMaterials.CreateFlightChunkMaterial();
        material.name = "EarthSandboxFlightChunk";
        return material;
    }
}
