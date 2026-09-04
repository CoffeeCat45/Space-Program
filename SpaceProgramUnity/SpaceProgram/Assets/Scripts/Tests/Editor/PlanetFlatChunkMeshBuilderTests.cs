using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PlanetFlatChunkMeshBuilderTests
{
    readonly List<Mesh> createdMeshes = new List<Mesh>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < createdMeshes.Count; i++)
        {
            if (createdMeshes[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdMeshes[i]);
            }
        }

        createdMeshes.Clear();
    }

    Mesh BuildMesh(PlanetFlatChunkMeshBuilder.BuildParams buildParams)
    {
        Mesh mesh = PlanetFlatChunkMeshBuilder.Build(buildParams);
        createdMeshes.Add(mesh);
        return mesh;
    }

    Mesh BuildMesh(int resolution, float spanMeters)
    {
        Mesh mesh = PlanetFlatChunkMeshBuilder.Build(resolution, spanMeters);
        createdMeshes.Add(mesh);
        return mesh;
    }

    Mesh BuildMesh()
    {
        Mesh mesh = PlanetFlatChunkMeshBuilder.Build();
        createdMeshes.Add(mesh);
        return mesh;
    }
    [Test]
    public void DefaultResolution_IsThirtyThree()
    {
        Assert.AreEqual(33, PlanetFlatChunkMeshBuilder.DefaultResolution);
    }

    [Test]
    public void Build_DefaultGrid_HasExpectedTopology()
    {
        Mesh mesh = BuildMesh();

        Assert.AreEqual(33 * 33, mesh.vertexCount);
        Assert.AreEqual((33 - 1) * (33 - 1) * 6, mesh.triangles.Length);
    }

    [Test]
    public void Build_FlatChunk_AllVerticesOnGroundPlane()
    {
        Mesh mesh = BuildMesh(17, 500f);

        foreach (Vector3 vertex in mesh.vertices)
        {
            Assert.AreEqual(0f, vertex.y, 1e-5f);
        }
    }

    [Test]
    public void Build_FaceUvSubset_StoresCanonicalUvs()
    {
        var buildParams = new PlanetFlatChunkMeshBuilder.BuildParams(
            resolution: 3,
            spanMeters: 100f,
            minU: 0.25f,
            maxU: 0.75f,
            minV: 0.1f,
            maxV: 0.9f
        );
        Mesh mesh = BuildMesh(buildParams);
        Vector2[] uvs = mesh.uv;

        Assert.AreEqual(new Vector2(0.25f, 0.1f), uvs[0]);
        Assert.AreEqual(new Vector2(0.75f, 0.9f), uvs[uvs.Length - 1]);
    }

    [Test]
    public void Build_SpanMeters_SetsHorizontalExtents()
    {
        Mesh mesh = BuildMesh(2, 200f);
        Vector3[] vertices = mesh.vertices;

        Assert.AreEqual(new Vector3(-100f, 0f, -100f), vertices[0]);
        Assert.AreEqual(new Vector3(100f, 0f, 100f), vertices[3]);
    }
}
