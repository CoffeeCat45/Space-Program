using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds a flat PQS chunk mesh on a regular grid (terrain_plan roadmap 2.1).
/// Heights are zero; spherification and GEBCO displacement come in step 2.2.
/// </summary>
public static class PlanetFlatChunkMeshBuilder
{
    public const int DefaultResolution = 33;

    public readonly struct BuildParams
    {
        public BuildParams(
            int resolution = DefaultResolution,
            float spanMeters = 1000f,
            float minU = 0f,
            float maxU = 1f,
            float minV = 0f,
            float maxV = 1f
        )
        {
            Resolution = Mathf.Clamp(resolution, 2, 256);
            SpanMeters = Mathf.Max(1f, spanMeters);
            MinU = minU;
            MaxU = maxU;
            MinV = minV;
            MaxV = maxV;
        }

        public int Resolution { get; }

        public float SpanMeters { get; }

        public float MinU { get; }

        public float MaxU { get; }

        public float MinV { get; }

        public float MaxV { get; }
    }

    public static Mesh Build(BuildParams buildParams)
    {
        int resolution = buildParams.Resolution;
        int vertexCount = resolution * resolution;
        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        int triangleIndex = 0;
        float halfSpan = buildParams.SpanMeters * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            float gridV = resolution > 1 ? (float)y / (resolution - 1) : 0f;
            float v = Mathf.Lerp(buildParams.MinV, buildParams.MaxV, gridV);
            float localZ = Mathf.Lerp(-halfSpan, halfSpan, gridV);

            for (int x = 0; x < resolution; x++)
            {
                float gridU = resolution > 1 ? (float)x / (resolution - 1) : 0f;
                float u = Mathf.Lerp(buildParams.MinU, buildParams.MaxU, gridU);
                float localX = Mathf.Lerp(-halfSpan, halfSpan, gridU);
                int vertexIndex = x + (y * resolution);

                vertices[vertexIndex] = new Vector3(-localX, 0f, localZ);
                uvs[vertexIndex] = new Vector2(u, v);

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

        var mesh = new Mesh
        {
            name = "PlanetFlatChunk",
            indexFormat = vertexCount > 65535 ?
                IndexFormat.UInt32 :
                IndexFormat.UInt16,
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh Build(int resolution = DefaultResolution, float spanMeters = 1000f)
    {
        return Build(new BuildParams(resolution, spanMeters));
    }
}
