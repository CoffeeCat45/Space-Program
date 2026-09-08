using NUnit.Framework;
using UnityEngine;

public sealed class MapMarkerPlacementTests
{
    [Test]
    public void LiftOutOfGlobe_MovesInsidePointOntoSurface()
    {
        Vector3 lifted = MapMarkerPlacement.LiftOutOfGlobe(
            new Vector3(1f, 0f, 0f),
            Vector3.zero,
            10f,
            0.5f
        );

        Assert.GreaterOrEqual(lifted.magnitude, 10f * 1.02f + 0.5f - 0.001f);
        Assert.Greater(lifted.x, 0f);
    }

    [Test]
    public void LiftOutOfGlobe_LeavesOutsidePointUnchanged()
    {
        Vector3 outside = new Vector3(20f, 0f, 0f);
        Vector3 lifted = MapMarkerPlacement.LiftOutOfGlobe(
            outside,
            Vector3.zero,
            10f,
            0.5f
        );

        Assert.AreEqual(outside.x, lifted.x, 0.0001f);
        Assert.AreEqual(outside.y, lifted.y, 0.0001f);
        Assert.AreEqual(outside.z, lifted.z, 0.0001f);
    }

    [Test]
    public void IsOnNearHemisphere_DetectsCameraFacingSide()
    {
        Vector3 center = Vector3.zero;
        Vector3 camera = new Vector3(0f, 0f, -20f);
        Assert.IsTrue(
            MapMarkerPlacement.IsOnNearHemisphere(
                camera,
                center,
                new Vector3(0f, 0f, -8f)
            )
        );
        Assert.IsFalse(
            MapMarkerPlacement.IsOnNearHemisphere(
                camera,
                center,
                new Vector3(0f, 0f, 8f)
            )
        );
    }

    [Test]
    public void FarSidePoint_IsOccludedByGlobe()
    {
        Vector3 camera = new Vector3(0f, 0f, -20f);
        Vector3 globe = Vector3.zero;
        const float radius = 10f;

        Assert.IsTrue(
            WorldVisualBridge.IsWorldPointOccludedBySphere(
                camera,
                new Vector3(0f, 0f, 8f),
                globe,
                radius
            )
        );
        Assert.IsFalse(
            WorldVisualBridge.IsWorldPointOccludedBySphere(
                camera,
                new Vector3(0f, 0f, -12f),
                globe,
                radius
            )
        );
    }

    [Test]
    public void MapMarker_BehindGlobe_IsHiddenFromCamera()
    {
        GameObject cameraObject = new GameObject("OcclusionCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(
            new Vector3(0f, 0f, -20f),
            Quaternion.identity
        );

        try
        {
            Assert.IsFalse(
                WorldVisualBridge.IsMapMarkerVisibleFromCamera(
                    camera,
                    new Vector3(0f, 0f, 8f),
                    Vector3.zero,
                    10f,
                    hasPlanetSphere: true
                )
            );
            Assert.IsTrue(
                WorldVisualBridge.IsMapMarkerVisibleFromCamera(
                    camera,
                    new Vector3(0f, 0f, -12f),
                    Vector3.zero,
                    10f,
                    hasPlanetSphere: true
                )
            );
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }
}
