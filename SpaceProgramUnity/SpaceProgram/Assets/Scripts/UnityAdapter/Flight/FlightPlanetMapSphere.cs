using System;
using UnityEngine;

/// <summary>
/// Low-poly monolithic planet for map mode (ScaledSpace). Uses a sphere collider for picking.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlightPlanetMapSphere : MonoBehaviour
{
    public const int DefaultIcosphereSubdivisions = 3;
    const float MaxLatitudeDegrees = 85f;

    [SerializeField] int icosphereSubdivisions = DefaultIcosphereSubdivisions;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    SphereCollider sphereCollider;
    Mesh runtimeMesh;

    public void Build(Material surfaceMaterial, float radius)
    {
        Build(surfaceMaterial, radius, icosphereSubdivisions);
    }

    public void Build(Material surfaceMaterial, float radius, int subdivisions)
    {
        Build(surfaceMaterial, radius, subdivisions, applyScaledSpaceLayer: true);
    }

    public void Build(
        Material surfaceMaterial,
        float radius,
        int subdivisions,
        bool applyScaledSpaceLayer
    )
    {
        icosphereSubdivisions = Mathf.Clamp(subdivisions, 1, 5);
        EnsureComponents();
        ResolveLatLonSegments(icosphereSubdivisions, out int latitudeSegments, out int longitudeSegments);

        if (runtimeMesh != null)
        {
            Destroy(runtimeMesh);
            runtimeMesh = null;
        }

        runtimeMesh = BuildLatLonSphereMesh(latitudeSegments, longitudeSegments, radius);
        runtimeMesh.name = "PlanetMapSphere";
        meshFilter.sharedMesh = runtimeMesh;
        meshRenderer.sharedMaterial = surfaceMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.allowOcclusionWhenDynamic = false;
        EnsurePickCollider(radius);
        if (applyScaledSpaceLayer)
        {
            FlightRenderLayers.ApplyScaledSpaceLayer(gameObject);
        }
    }

    public bool HasBuiltMesh => runtimeMesh != null;

    public static Mesh BuildIcosphereMeshPublic(int subdivisions, float radius)
    {
        return BuildIcosphereMesh(subdivisions, radius);
    }

    static void ResolveLatLonSegments(
        int icosphereSubdivisions,
        out int latitudeSegments,
        out int longitudeSegments
    )
    {
        int clamped = Mathf.Clamp(icosphereSubdivisions, 1, 5);
        longitudeSegments = Mathf.Clamp(16 << clamped, 32, 128);
        latitudeSegments = Mathf.Max(16, longitudeSegments / 2);
    }

    void EnsureComponents()
    {
        meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        sphereCollider = GetComponent<SphereCollider>();

        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
        }

        RemoveLegacyMeshCollider();
    }

    void EnsurePickCollider(float radius)
    {
        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
        }

        sphereCollider.radius = radius;
        sphereCollider.center = Vector3.zero;
        RemoveLegacyMeshCollider();
    }

    void RemoveLegacyMeshCollider()
    {
        MeshCollider legacyMeshCollider = GetComponent<MeshCollider>();

        if (legacyMeshCollider != null)
        {
            Destroy(legacyMeshCollider);
        }
    }

    void OnDestroy()
    {
        if (runtimeMesh != null)
        {
            Destroy(runtimeMesh);
        }
    }

    /// <summary>
    /// Equirectangular-friendly lat/lon shell with duplicated seam vertices (U=1 at lon=0).
    /// </summary>
    static Mesh BuildLatLonSphereMesh(int latitudeSegments, int longitudeSegments, float radius)
    {
        int ringVerts = longitudeSegments;
        int gridVertCount = (latitudeSegments + 1) * ringVerts;
        int seamDuplicateCount = latitudeSegments + 1;
        int seamDuplicateStart = gridVertCount;
        int vertCount = gridVertCount + seamDuplicateCount + 2;
        var vertices = new Vector3[vertCount];
        var geographicUvs = new Vector2[vertCount];

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
                    vertIndex,
                    latitudeDegrees,
                    LonDegreesForRingIndex(lon, ringVerts),
                    radius
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
        }

        int southPoleIndex = gridVertCount + seamDuplicateCount;
        int northPoleIndex = southPoleIndex + 1;
        AddPoleVertex(vertices, geographicUvs, southPoleIndex, -1f, radius);
        AddPoleVertex(vertices, geographicUvs, northPoleIndex, 1f, radius);

        int bodyTriCount = latitudeSegments * longitudeSegments * 6;
        int capTriCount = longitudeSegments * 6;
        int triCount = bodyTriCount + capTriCount * 2;
        var triangles = new int[triCount];
        int triangleIndex = 0;

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

        WriteSouthCapTriangles(
            triangles,
            bodyTriCount,
            ringVerts,
            southPoleIndex,
            seamDuplicateStart
        );
        WriteNorthCapTriangles(
            triangles,
            bodyTriCount + capTriCount,
            ringVerts,
            latitudeSegments,
            northPoleIndex,
            seamDuplicateStart
        );

        PlanetMeshWindingUtility.EnsureRenderableOutwardNormals(vertices, triangles);
        FlipCapTrianglesToMatchBody(triangles, bodyTriCount, capTriCount);
        FlipCapTrianglesToMatchBody(triangles, bodyTriCount + capTriCount, capTriCount);

        Mesh mesh = new Mesh
        {
            name = "PlanetMapLatLonSphere",
            indexFormat = vertices.Length > 65535 ?
                UnityEngine.Rendering.IndexFormat.UInt32 :
                UnityEngine.Rendering.IndexFormat.UInt16,
        };
        mesh.vertices = vertices;
        mesh.uv = geographicUvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void WriteLatitudeRingVertex(
        Vector3[] vertices,
        Vector2[] geographicUvs,
        int vertIndex,
        float latitudeDegrees,
        float longitudeDegrees,
        float radius
    )
    {
        Vector3 meshUnit = EarthSandboxGlobePaint.MeshDisplayUnitFromLatitudeLongitudeDegrees(
            latitudeDegrees,
            longitudeDegrees
        );
        vertices[vertIndex] = meshUnit * radius;

        double latitudeRadians = latitudeDegrees * (Math.PI / 180.0);
        double longitudeRadians = longitudeDegrees * (Math.PI / 180.0);
        double geographicU = PlanetGeographicCoords.LongitudeRadiansToGeographicU(longitudeRadians);
        double geographicV = PlanetGeographicCoords.LatitudeRadiansToGeographicV(latitudeRadians);
        geographicUvs[vertIndex] = new Vector2((float)geographicU, (float)geographicV);
    }

    static void AddPoleVertex(
        Vector3[] vertices,
        Vector2[] geographicUvs,
        int index,
        float poleY,
        float radius
    )
    {
        Vector3 unit = new Vector3(0f, poleY, 0f);
        vertices[index] = PlanetCubeSphere.SimDirectionToMeshFrame(unit) * radius;
        geographicUvs[index] = new Vector2(
            0.5f,
            poleY < 0f ? 0f : 1f
        );
    }

    static float LonDegreesForRingIndex(int lonIndex, int ringVerts)
    {
        return -180f + (360f * lonIndex / ringVerts);
    }

    static void WriteSouthCapTriangles(
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

    static void WriteNorthCapTriangles(
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

    static void FlipCapTrianglesToMatchBody(int[] triangles, int startIndex, int triangleCount)
    {
        int endIndex = startIndex + triangleCount;

        for (int triangleIndex = startIndex; triangleIndex < endIndex; triangleIndex += 3)
        {
            int indexB = triangles[triangleIndex + 1];
            triangles[triangleIndex + 1] = triangles[triangleIndex + 2];
            triangles[triangleIndex + 2] = indexB;
        }
    }

    static Mesh BuildIcosphereMesh(int subdivisions, float radius)
    {
        var vertices = new System.Collections.Generic.List<Vector3>();
        var geographicUvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();

        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        AddUnitVertex(vertices, geographicUvs, new Vector3(-1f, t, 0f), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(1f, t, 0f), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(-1f, -t, 0f), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(1f, -t, 0f), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(0f, -1f, t), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(0f, 1f, t), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(0f, -1f, -t), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(0f, 1f, -t), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(t, 0f, -1f), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(t, 0f, 1f), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(-t, 0f, -1f), radius);
        AddUnitVertex(vertices, geographicUvs, new Vector3(-t, 0f, 1f), radius);

        int[] seedTriangles =
        {
            0, 11, 5,
            0, 5, 1,
            0, 1, 7,
            0, 7, 10,
            0, 10, 11,
            1, 5, 9,
            5, 11, 4,
            11, 10, 2,
            10, 7, 6,
            7, 1, 8,
            3, 9, 4,
            3, 4, 2,
            3, 2, 6,
            3, 6, 8,
            3, 8, 9,
            4, 9, 5,
            2, 4, 11,
            6, 2, 10,
            8, 6, 7,
            9, 8, 1,
        };
        triangles.AddRange(seedTriangles);

        var midpointCache = new System.Collections.Generic.Dictionary<long, int>();

        for (int i = 0; i < subdivisions; i++)
        {
            var nextTriangles = new System.Collections.Generic.List<int>(triangles.Count * 4);

            for (int tIndex = 0; tIndex < triangles.Count; tIndex += 3)
            {
                int v1 = triangles[tIndex];
                int v2 = triangles[tIndex + 1];
                int v3 = triangles[tIndex + 2];
                int a = GetMidpoint(v1, v2, vertices, geographicUvs, midpointCache, radius);
                int b = GetMidpoint(v2, v3, vertices, geographicUvs, midpointCache, radius);
                int c = GetMidpoint(v3, v1, vertices, geographicUvs, midpointCache, radius);

                nextTriangles.Add(v1);
                nextTriangles.Add(a);
                nextTriangles.Add(c);
                nextTriangles.Add(v2);
                nextTriangles.Add(b);
                nextTriangles.Add(a);
                nextTriangles.Add(v3);
                nextTriangles.Add(c);
                nextTriangles.Add(b);
                nextTriangles.Add(a);
                nextTriangles.Add(b);
                nextTriangles.Add(c);
            }

            triangles = nextTriangles;
        }

        Mesh mesh = new Mesh
        {
            indexFormat = vertices.Count > 65535 ?
                UnityEngine.Rendering.IndexFormat.UInt32 :
                UnityEngine.Rendering.IndexFormat.UInt16,
        };
        var vertexArray = new Vector3[vertices.Count];
        vertices.CopyTo(vertexArray);
        int[] triangleArray = triangles.ToArray();
        PlanetMeshWindingUtility.EnsureRenderableOutwardNormals(vertexArray, triangleArray);

        mesh.SetVertices(vertexArray);
        mesh.SetUVs(0, geographicUvs);
        mesh.SetTriangles(triangleArray, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddUnitVertex(
        System.Collections.Generic.List<Vector3> vertices,
        System.Collections.Generic.List<Vector2> geographicUvs,
        Vector3 simUnitDirection,
        float radius
    )
    {
        Vector3 simUnit = simUnitDirection.normalized;
        vertices.Add(PlanetCubeSphere.FlipMeshVertexPositionX(simUnit * radius));
        geographicUvs.Add(PlanetCubeSphere.DirectionToGeographicUv(simUnit));
    }

    static int GetMidpoint(
        int indexA,
        int indexB,
        System.Collections.Generic.List<Vector3> vertices,
        System.Collections.Generic.List<Vector2> geographicUvs,
        System.Collections.Generic.Dictionary<long, int> cache,
        float radius
    )
    {
        int smallIndex = indexA < indexB ? indexA : indexB;
        int largeIndex = indexA < indexB ? indexB : indexA;
        long key = ((long)smallIndex << 32) + largeIndex;

        if (cache.TryGetValue(key, out int cachedIndex))
        {
            return cachedIndex;
        }

        Vector3 simUnitA = PlanetCubeSphere.FlipMeshVertexPositionX(vertices[indexA] / radius);
        Vector3 simUnitB = PlanetCubeSphere.FlipMeshVertexPositionX(vertices[indexB] / radius);
        Vector3 simMidUnit = (simUnitA + simUnitB).normalized;
        AddUnitVertex(vertices, geographicUvs, simMidUnit, radius);
        int newIndex = vertices.Count - 1;
        cache[key] = newIndex;
        return newIndex;
    }
}
