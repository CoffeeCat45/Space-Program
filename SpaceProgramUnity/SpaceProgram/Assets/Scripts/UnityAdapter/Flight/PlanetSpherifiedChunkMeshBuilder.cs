using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cube-face chunk bent to a sphere with Philip Nowell mapping and GEBCO heights (roadmap 2.2).
/// Vertices are in planet-local space (radiate from origin).
/// </summary>
public static class PlanetSpherifiedChunkMeshBuilder
{
    /// <summary>
    /// Temporary hyper-relief scale for visual calibration. When &gt; 0, replaces
    /// <see cref="PlanetSurfaceConstants.UnityVisualHeightScale"/> for flight mesh heights.
    /// Set to 0 to use only <see cref="PlanetSurfaceConstants.UnityVisualHeightScale"/> (0.4).
    /// </summary>
    public static float TestHeightScale = 1.0f;

    public static float ActiveVisualHeightScale =>
        TestHeightScale > 0f ?
            TestHeightScale :
            PlanetSurfaceConstants.UnityVisualHeightScale;

    public readonly struct BuildParams
    {
        public BuildParams(
            int faceIndex,
            int resolution = PlanetFlatChunkMeshBuilder.DefaultResolution,
            float minU = 0f,
            float maxU = 1f,
            float minV = 0f,
            float maxV = 1f,
            float planetRadiusMeters = PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters,
            Func<Vector3, float> sampleHeightOffsetMeters = null,
            Func<Vector3, Vector3, Vector3> adjustSurfacePoint = null,
            Func<Vector3, Vector2> computeDetailUv = null,
            float maxHeightDeltaMeters = 0f
        )
        {
            FaceIndex = faceIndex;
            Resolution = Mathf.Clamp(resolution, 2, 256);
            MinU = minU;
            MaxU = maxU;
            MinV = minV;
            MaxV = maxV;
            PlanetRadiusMeters = Mathf.Max(1f, planetRadiusMeters);
            SampleHeightOffsetMeters = sampleHeightOffsetMeters;
            AdjustSurfacePoint = adjustSurfacePoint;
            ComputeDetailUv = computeDetailUv;
            MaxHeightDeltaMeters = Mathf.Max(0f, maxHeightDeltaMeters);
        }

        public int FaceIndex { get; }

        public int Resolution { get; }

        public float MinU { get; }

        public float MaxU { get; }

        public float MinV { get; }

        public float MaxV { get; }

        public float PlanetRadiusMeters { get; }

        public Func<Vector3, float> SampleHeightOffsetMeters { get; }

        public Func<Vector3, Vector3, Vector3> AdjustSurfacePoint { get; }

        public Func<Vector3, Vector2> ComputeDetailUv { get; }

        public float MaxHeightDeltaMeters { get; }
    }

    public static Mesh Build(BuildParams buildParams)
    {
        int resolution = buildParams.Resolution;
        int faceIndex = buildParams.FaceIndex;
        int vertexCount = resolution * resolution;
        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        Vector2[] detailUvs = buildParams.ComputeDetailUv != null ?
            new Vector2[vertexCount] :
            null;
        var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        int triangleIndex = 0;
        float planetRadius = buildParams.PlanetRadiusMeters;
        bool useHeightBuffer =
            buildParams.SampleHeightOffsetMeters != null ||
            buildParams.MaxHeightDeltaMeters > 0f ||
            buildParams.AdjustSurfacePoint != null;
        float[] heights = useHeightBuffer ? new float[vertexCount] : null;

        for (int y = 0; y < resolution; y++)
        {
            float gridV = resolution > 1 ? (float)y / (resolution - 1) : 0f;
            float v = Mathf.Lerp(buildParams.MinV, buildParams.MaxV, gridV);

            for (int x = 0; x < resolution; x++)
            {
                float gridU = resolution > 1 ? (float)x / (resolution - 1) : 0f;
                float u = Mathf.Lerp(buildParams.MinU, buildParams.MaxU, gridU);
                int vertexIndex = x + (y * resolution);
                Vector3 unitDirection = PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v);

                if (heights != null)
                {
                    float heightOffset = buildParams.SampleHeightOffsetMeters != null ?
                        buildParams.SampleHeightOffsetMeters(unitDirection) :
                        SampleMeshHeightAtUnitDirection(unitDirection);

                    if (!float.IsFinite(heightOffset))
                    {
                        heightOffset = 0f;
                    }

                    heights[vertexIndex] = heightOffset;
                }

                uvs[vertexIndex] = PlanetCubeSphere.DirectionToGeographicUv(unitDirection);

                if (detailUvs != null)
                {
                    detailUvs[vertexIndex] = buildParams.ComputeDetailUv(unitDirection);
                }
            }
        }

        if (heights != null && buildParams.MaxHeightDeltaMeters > 0f)
        {
            ClampMeshHeightSlopes(heights, resolution, buildParams.MaxHeightDeltaMeters);
        }

        for (int y = 0; y < resolution; y++)
        {
            float gridV = resolution > 1 ? (float)y / (resolution - 1) : 0f;
            float v = Mathf.Lerp(buildParams.MinV, buildParams.MaxV, gridV);

            for (int x = 0; x < resolution; x++)
            {
                float gridU = resolution > 1 ? (float)x / (resolution - 1) : 0f;
                float u = Mathf.Lerp(buildParams.MinU, buildParams.MaxU, gridU);
                int vertexIndex = x + (y * resolution);
                Vector3 unitDirection = PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v);
                Vector3 surfacePoint;

                if (heights != null)
                {
                    surfacePoint = unitDirection * (planetRadius + heights[vertexIndex]);
                }
                else
                {
                    float heightOffset = SampleMeshHeightAtUnitDirection(unitDirection);
                    surfacePoint = unitDirection * (planetRadius + heightOffset);
                }

                if (buildParams.AdjustSurfacePoint != null)
                {
                    surfacePoint = buildParams.AdjustSurfacePoint(unitDirection, surfacePoint);
                }

                if (!IsFiniteVector(surfacePoint))
                {
                    surfacePoint = unitDirection * planetRadius;
                }

                vertices[vertexIndex] = PlanetCubeSphere.FlipMeshVertexPositionX(surfacePoint);

                if (x >= resolution - 1 || y >= resolution - 1)
                {
                    continue;
                }

                int i = vertexIndex;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + resolution + 1;
                triangles[triangleIndex++] = i + resolution;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + 1;
                triangles[triangleIndex++] = i + resolution + 1;
            }
        }

        PlanetMeshWindingUtility.EnsureRenderableOutwardNormals(vertices, triangles);

        var mesh = new Mesh
        {
            name = "PlanetSpherifiedChunk_Face" + faceIndex,
            indexFormat = vertexCount > 65535 ?
                IndexFormat.UInt32 :
                IndexFormat.UInt16,
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);

        if (detailUvs != null)
        {
            mesh.SetUVs(1, detailUvs);
        }

        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static float SampleMeshHeightAtUnitDirection(Vector3 unitDirection)
    {
        if (unitDirection.sqrMagnitude <= 1e-12f)
        {
            return 0f;
        }

        unitDirection.Normalize();
        SimVector3 direction = new SimVector3(
            unitDirection.x,
            unitDirection.y,
            unitDirection.z
        ).Normalize();

        double elevationMeters = PlanetFlightElevationSampling.SampleElevationMetersAtDirection(
            direction
        );
        return (float)(elevationMeters * ActiveVisualHeightScale);
    }

    static void ClampMeshHeightSlopes(float[] heights, int resolution, float maxHeightDeltaMeters)
    {
        if (heights == null || resolution < 2 || maxHeightDeltaMeters <= 0f)
        {
            return;
        }

        int last = resolution - 1;
        var raw = new float[heights.Length];
        Array.Copy(heights, raw, heights.Length);

        ClampGridEdge1D(heights, raw, resolution, maxHeightDeltaMeters, row: 0);
        ClampGridEdge1D(heights, raw, resolution, maxHeightDeltaMeters, row: last);
        ClampGridEdge1D(heights, raw, resolution, maxHeightDeltaMeters, column: 0);
        ClampGridEdge1D(heights, raw, resolution, maxHeightDeltaMeters, column: last);
        heights[0] = raw[0];
        heights[last] = raw[last];
        heights[last * resolution] = raw[last * resolution];
        heights[(last * resolution) + last] = raw[(last * resolution) + last];

        const int iterations = 2;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int y = 1; y < last; y++)
            {
                for (int x = 1; x < last; x++)
                {
                    int index = x + (y * resolution);
                    float height = heights[index];
                    height = Mathf.Min(height, heights[index - 1] + maxHeightDeltaMeters);
                    height = Mathf.Max(height, heights[index - 1] - maxHeightDeltaMeters);
                    height = Mathf.Min(height, heights[index - resolution] + maxHeightDeltaMeters);
                    height = Mathf.Max(height, heights[index - resolution] - maxHeightDeltaMeters);
                    heights[index] = height;
                }
            }
        }
    }

    static void ClampGridEdge1D(
        float[] heights,
        float[] raw,
        int resolution,
        float maxHeightDeltaMeters,
        int row = -1,
        int column = -1
    )
    {
        bool alongRow = row >= 0;
        int last = resolution - 1;

        for (int i = 0; i < resolution; i++)
        {
            int index = alongRow ? i + (row * resolution) : column + (i * resolution);
            heights[index] = raw[index];
        }

        const int iterations = 2;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int i = 1; i < resolution; i++)
            {
                int index = alongRow ? i + (row * resolution) : column + (i * resolution);
                int prev = alongRow ? (i - 1) + (row * resolution) : column + ((i - 1) * resolution);
                float height = heights[index];
                height = Mathf.Min(height, heights[prev] + maxHeightDeltaMeters);
                height = Mathf.Max(height, heights[prev] - maxHeightDeltaMeters);
                heights[index] = height;
            }

            for (int i = last - 1; i >= 0; i--)
            {
                int index = alongRow ? i + (row * resolution) : column + (i * resolution);
                int next = alongRow ? (i + 1) + (row * resolution) : column + ((i + 1) * resolution);
                float height = heights[index];
                height = Mathf.Min(height, heights[next] + maxHeightDeltaMeters);
                height = Mathf.Max(height, heights[next] - maxHeightDeltaMeters);
                heights[index] = height;
            }
        }
    }

    static bool IsFiniteVector(Vector3 value)
    {
        return float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

}
