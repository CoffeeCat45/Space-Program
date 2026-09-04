using UnityEngine;

/// <summary>
/// Unity left-handed meshes with <c>Cull Back</c>: exterior cameras see front faces when the
/// geometric cross product points <em>toward the origin</em> on a planet shell (dot with
/// centroid &lt; 0). Do not flip per-triangle — only flip the entire shell once if needed.
/// </summary>
public static class PlanetMeshWindingUtility
{
    /// <summary>
    /// Flip every triangle once when <c>RecalculateNormals</c> would point most normals inward.
    /// </summary>
    public static void EnsureRenderableOutwardNormals(Vector3[] vertices, int[] triangles)
    {
        EnsureRenderableOutwardNormals(vertices, triangles, 0, triangles.Length);
    }

    public static void EnsureRenderableOutwardNormals(
        Vector3[] vertices,
        int[] triangles,
        int triangleStartIndex,
        int triangleCount
    )
    {
        if (vertices == null || triangles == null || triangleCount < 3)
        {
            return;
        }

        var scratchNormals = new Vector3[vertices.Length];
        ComputeNormalsFromWinding(
            vertices,
            triangles,
            scratchNormals,
            triangleStartIndex,
            triangleCount
        );
        int awayFromOrigin = CountNormalsPointingAwayFromOrigin(vertices, scratchNormals);

        if (awayFromOrigin < vertices.Length - awayFromOrigin)
        {
            FlipTriangleWindingRange(triangles, triangleStartIndex, triangleCount);
        }
    }

    static void ComputeNormalsFromWinding(
        Vector3[] vertices,
        int[] triangles,
        Vector3[] normals,
        int triangleStartIndex,
        int triangleCount
    )
    {
        for (int index = 0; index < normals.Length; index++)
        {
            normals[index] = Vector3.zero;
        }

        int triangleEndIndex = triangleStartIndex + triangleCount;

        for (int triangleIndex = triangleStartIndex; triangleIndex < triangleEndIndex; triangleIndex += 3)
        {
            int indexA = triangles[triangleIndex];
            int indexB = triangles[triangleIndex + 1];
            int indexC = triangles[triangleIndex + 2];

            if (
                indexA < 0 ||
                indexA >= vertices.Length ||
                indexB < 0 ||
                indexB >= vertices.Length ||
                indexC < 0 ||
                indexC >= vertices.Length
            )
            {
                continue;
            }

            Vector3 vertexA = vertices[indexA];
            Vector3 vertexB = vertices[indexB];
            Vector3 vertexC = vertices[indexC];
            Vector3 faceNormal = Vector3.Cross(vertexB - vertexA, vertexC - vertexA);

            if (faceNormal.sqrMagnitude <= 1e-12f)
            {
                continue;
            }

            faceNormal.Normalize();
            normals[indexA] += faceNormal;
            normals[indexB] += faceNormal;
            normals[indexC] += faceNormal;
        }

        for (int index = 0; index < normals.Length; index++)
        {
            if (normals[index].sqrMagnitude > 1e-12f)
            {
                normals[index].Normalize();
            }
        }
    }

    /// <summary>
    /// Legacy geometric test — do not use for sandbox UV sphere; caps need seam-matched winding.
    /// </summary>
    public static void EnsureUnityExteriorShellWinding(Vector3[] vertices, int[] triangles)
    {
        EnsureRenderableOutwardNormals(vertices, triangles);
    }

  /// <summary>Geometric normal agrees with centroid (dot &gt; 0). Not Unity front-face.</summary>
    public static int CountGeometricOutwardFaces(Vector3[] vertices, int[] triangles)
    {
        if (vertices == null || triangles == null || triangles.Length < 3)
        {
            return 0;
        }

        int outwardFaces = 0;

        for (int triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex += 3)
        {
            int indexA = triangles[triangleIndex];
            int indexB = triangles[triangleIndex + 1];
            int indexC = triangles[triangleIndex + 2];

            if (
                indexA < 0 ||
                indexA >= vertices.Length ||
                indexB < 0 ||
                indexB >= vertices.Length ||
                indexC < 0 ||
                indexC >= vertices.Length
            )
            {
                continue;
            }

            Vector3 vertexA = vertices[indexA];
            Vector3 vertexB = vertices[indexB];
            Vector3 vertexC = vertices[indexC];
            Vector3 edgeAb = vertexB - vertexA;
            Vector3 edgeAc = vertexC - vertexA;
            Vector3 faceNormal = Vector3.Cross(edgeAb, edgeAc);
            Vector3 centroid = (vertexA + vertexB + vertexC) * (1f / 3f);

            if (centroid.sqrMagnitude < 1e-8f)
            {
                continue;
            }

            if (Vector3.Dot(faceNormal, centroid) > 0f)
            {
                outwardFaces++;
            }
        }

        return outwardFaces;
    }

    /// <summary>
    /// After <see cref="EnsureUnityExteriorShellWinding"/> + <c>RecalculateNormals</c>, normals
    /// should radiate away from the origin.
    /// </summary>
    public static int CountNormalsPointingAwayFromOrigin(Vector3[] vertices, Vector3[] normals)
    {
        if (vertices == null || normals == null)
        {
            return 0;
        }

        int length = Mathf.Min(vertices.Length, normals.Length);
        int awayFromOrigin = 0;

        for (int index = 0; index < length; index++)
        {
            if (Vector3.Dot(normals[index], vertices[index]) > 0f)
            {
                awayFromOrigin++;
            }
        }

        return awayFromOrigin;
    }

    static void FlipAllTriangleWinding(int[] triangles)
    {
        FlipTriangleWindingRange(triangles, 0, triangles.Length);
    }

    static void FlipTriangleWindingRange(int[] triangles, int startIndex, int triangleCount)
    {
        int endIndex = startIndex + triangleCount;

        for (int triangleIndex = startIndex; triangleIndex < endIndex; triangleIndex += 3)
        {
            int indexB = triangles[triangleIndex + 1];
            triangles[triangleIndex + 1] = triangles[triangleIndex + 2];
            triangles[triangleIndex + 2] = indexB;
        }
    }
}
