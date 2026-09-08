using NUnit.Framework;
using UnityEngine;

public sealed class MapCelestialScaleTests
{
    [Test]
    public void MapHeliocentricLayout_KeepsSunAtPhysicsPosition()
    {
        Assert.AreEqual(1.0, WorldVisualBridge.MapHeliocentricVisualScale, 0.0);
    }

    [Test]
    public void DistantGlobe_UsesUnitDiameterMapScale_EvenIfLodSphereRemains()
    {
        GameObject bodyObject = new GameObject("Moon");

        try
        {
            Assert.IsFalse(DistantCelestialGlobe.IsAttached(bodyObject));

            bodyObject.AddComponent<DistantCelestialGlobe>();
            Assert.IsTrue(DistantCelestialGlobe.IsAttached(bodyObject));
        }
        finally
        {
            Object.DestroyImmediate(bodyObject);
        }
    }

    [Test]
    public void OcclusionSphere_IgnoresHibernatedLodMapSphereAfterDistantGlobeScale()
    {
        GameObject moon = new GameObject("Moon");
        Mesh leftoverMesh = null;
        Mesh globeMesh = null;

        try
        {
            float mapDiameter = WorldVisualBridge.GetCelestialMeshDiameter(
                1_737_400.0,
                WorldVisualBridge.MapPositionScale
            );
            moon.transform.localScale = Vector3.one * mapDiameter;

            globeMesh = CreateRadiusMesh(DistantCelestialGlobe.UnitDiameterMeshRadius);
            MeshFilter globeFilter = moon.AddComponent<MeshFilter>();
            globeFilter.sharedMesh = globeMesh;
            moon.AddComponent<MeshRenderer>();

            GameObject leftover = new GameObject("PlanetMapSphere");
            leftover.transform.SetParent(moon.transform, false);
            leftoverMesh = CreateRadiusMesh(1_737_400f);
            leftover.AddComponent<MeshFilter>().sharedMesh = leftoverMesh;
            leftover.AddComponent<MeshRenderer>();
            leftover.SetActive(false);

            Assert.IsTrue(
                WorldVisualBridge.TryGetCelestialOcclusionSphere(
                    moon.transform,
                    out _,
                    out float radius
                )
            );

            float expected = DistantCelestialGlobe.UnitDiameterMeshRadius * mapDiameter;
            Assert.AreEqual(expected, radius, expected * 0.05f);
            Assert.Less(radius, mapDiameter);
        }
        finally
        {
            Object.DestroyImmediate(moon);
            if (leftoverMesh != null)
            {
                Object.DestroyImmediate(leftoverMesh);
            }

            if (globeMesh != null)
            {
                Object.DestroyImmediate(globeMesh);
            }
        }
    }

    [Test]
    public void OcclusionSphere_UsesActiveLodMapSphere()
    {
        GameObject planet = new GameObject("Planet");
        Mesh mapMesh = null;

        try
        {
            planet.transform.localScale = Vector3.one * WorldVisualBridge.MapPositionScale;
            GameObject mapSphere = new GameObject("PlanetMapSphere");
            mapSphere.transform.SetParent(planet.transform, false);
            mapMesh = CreateRadiusMesh(6_371_000f);
            mapSphere.AddComponent<MeshFilter>().sharedMesh = mapMesh;
            mapSphere.AddComponent<MeshRenderer>();

            Assert.IsTrue(
                WorldVisualBridge.TryGetCelestialOcclusionSphere(
                    planet.transform,
                    out _,
                    out float radius
                )
            );
            Assert.AreEqual(
                6_371_000f * WorldVisualBridge.MapPositionScale,
                radius,
                1f
            );
        }
        finally
        {
            Object.DestroyImmediate(planet);
            if (mapMesh != null)
            {
                Object.DestroyImmediate(mapMesh);
            }
        }
    }

    static Mesh CreateRadiusMesh(float radius)
    {
        Mesh mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(radius, 0f, 0f),
                new Vector3(-radius, 0f, 0f),
                new Vector3(0f, radius, 0f),
                new Vector3(0f, -radius, 0f),
                new Vector3(0f, 0f, radius),
                new Vector3(0f, 0f, -radius),
            },
        };
        mesh.RecalculateBounds();
        return mesh;
    }
}
