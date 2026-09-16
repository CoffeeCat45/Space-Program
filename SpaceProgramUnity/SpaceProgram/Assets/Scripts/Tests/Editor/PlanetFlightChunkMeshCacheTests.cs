using NUnit.Framework;
using UnityEngine;

public sealed class PlanetFlightChunkMeshCacheTests
{
    [TearDown]
    public void TearDown()
    {
        PlanetFlightChunkMeshCache.Clear();
    }

    [Test]
    public void GetSurfaceVertices_ReturnsSameArrayOnSecondCall()
    {
        var mesh = new Mesh();
        mesh.vertices = new[]
        {
            Vector3.zero,
            Vector3.one,
            Vector3.up
        };
        mesh.triangles = new[] { 0, 1, 2 };

        try
        {
            Vector3[] first = PlanetFlightChunkMeshCache.GetSurfaceVertices(mesh);
            Vector3[] second = PlanetFlightChunkMeshCache.GetSurfaceVertices(mesh);

            Assert.AreSame(first, second);
            Assert.AreEqual(3, first.Length);
            Assert.AreEqual(Vector3.up, first[2]);
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void HitchMaxLodTick_IsUnderOneFrameAtSixtyFps()
    {
        Assert.Greater(FlightPlanetSphere.HitchMaxLodTickMilliseconds, 0f);
        Assert.Less(FlightPlanetSphere.HitchMaxLodTickMilliseconds, 16f);
        Assert.AreEqual(4, FlightPlanetSphere.HitchMaxSplitsPerFrame);
        Assert.AreEqual(2, FlightPlanetSphere.HitchMaxMeshesPerFrame);
    }
}
