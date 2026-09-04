using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class PlanetSpherifiedChunkMeshBuilderTests
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
        PlanetElevationCatalog.UnloadAll();
    }

    Mesh BuildMesh(PlanetSpherifiedChunkMeshBuilder.BuildParams buildParams)
    {
        Mesh mesh = PlanetSpherifiedChunkMeshBuilder.Build(buildParams);
        createdMeshes.Add(mesh);
        return mesh;
    }

    [Test]
    public void Build_FaceCenter_PlacesVertexOnPositiveXAxis()
    {
        var buildParams = new PlanetSpherifiedChunkMeshBuilder.BuildParams(
            faceIndex: 3,
            resolution: 3,
            minU: 0f,
            maxU: 1f,
            minV: 0f,
            maxV: 1f,
            planetRadiusMeters: 1000f
        );
        Mesh mesh = BuildMesh(buildParams);
        Vector3 centerVertex = mesh.vertices[4];

        Assert.AreEqual(1000f, centerVertex.x, 1f);
        Assert.AreEqual(0f, centerVertex.y, 1f);
        Assert.AreEqual(0f, centerVertex.z, 1f);
    }

    [Test]
    public void Build_AllVerticesSitOnSphereShell()
    {
        const float radius = 637100f;
        var buildParams = new PlanetSpherifiedChunkMeshBuilder.BuildParams(
            faceIndex: 4,
            resolution: 5,
            minU: 0.2f,
            maxU: 0.8f,
            minV: 0.2f,
            maxV: 0.8f,
            planetRadiusMeters: radius
        );
        Mesh mesh = BuildMesh(buildParams);

        foreach (Vector3 vertex in mesh.vertices)
        {
            float distance = vertex.magnitude;
            Assert.GreaterOrEqual(distance, radius - 5000f);
            Assert.LessOrEqual(distance, radius + 5000f);
        }
    }

    [Test]
    public void Build_WithUniformElevation_OffsetsVerticesByScaledHeight()
    {
        LoadUniformElevationField(250f);

        const float radius = 1000f;
        var buildParams = new PlanetSpherifiedChunkMeshBuilder.BuildParams(
            faceIndex: 3,
            resolution: 3,
            planetRadiusMeters: radius
        );
        Mesh mesh = BuildMesh(buildParams);
        float expectedRadius = radius + (250f * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale);

        foreach (Vector3 vertex in mesh.vertices)
        {
            Assert.AreEqual(expectedRadius, vertex.magnitude, 0.5f);
        }
    }

    [Test]
    public void FaceUvToUnitDirection_UsesNowell_NotLegacyNormalize()
    {
        const int faceIndex = 3;
        const double u = 0.1;
        const double v = 0.1;

        SimVector3 cubePoint = PlanetCubeSphereCoords.FaceUvToCubePoint(faceIndex, u, v);
        SimVector3 legacyNormalize = cubePoint.Normalize();
        SimVector3 nowell = PlanetCubeSphereCoords.FaceUvToUnitDirection(faceIndex, u, v);

        Assert.AreEqual(1.0, nowell.Length(), 1e-4);
        Assert.That(
            Math.Abs(nowell.x - legacyNormalize.x),
            Is.GreaterThan(1e-2),
            "Face UV mapping should use Nowell spherification, not cube-point normalize."
        );
        Assert.Greater(nowell.x, legacyNormalize.x);
    }

    [Test]
    public void Build_RecalculatedNormalsPointAwayFromOriginOnAllFaces()
    {
        const float radius = 637100f;
        int normalsAway = 0;
        int normalCount = 0;

        for (int faceIndex = 0; faceIndex < 6; faceIndex++)
        {
            var buildParams = new PlanetSpherifiedChunkMeshBuilder.BuildParams(
                faceIndex: faceIndex,
                resolution: 8,
                planetRadiusMeters: radius
            );
            Mesh mesh = BuildMesh(buildParams);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int faceAway = PlanetMeshWindingUtility.CountNormalsPointingAwayFromOrigin(
                vertices,
                normals
            );
            normalsAway += faceAway;
            normalCount += vertices.Length;
        }

        Assert.GreaterOrEqual(
            normalsAway,
            (normalCount * 3) / 4,
            "Exterior shell normals should point away from the planet center."
        );
    }

    static void LoadUniformElevationField(float elevationMeters)
    {
        const int width = 2;
        const int height = 1;
        var elevations = new float[width * height];

        for (int i = 0; i < elevations.Length; i++)
        {
            elevations[i] = elevationMeters;
        }

        using var stream = new MemoryStream();
        PlanetElevationBinaryFormat.Write(stream, elevations, width, height);
        stream.Position = 0;
        PlanetElevationCatalog.LoadFromStream(stream);
    }
}
