using NUnit.Framework;
using UnityEngine;

public sealed class PlanetLodViewPolicyTests
{
    const float PlanetRadius = 637100f;

    [Test]
    public void GetGeocentricNearestAngle_AllowsCoarseSiblingToSplit()
    {
        Vector3 nadir = Vector3.forward;
        Vector3 siblingCenter = Quaternion.Euler(33f, 0f, 0f) * nadir;
        float spanMeters = PlanetRadius * Mathf.PI * 0.5f;

        float nearest = PlanetLodViewPolicy.GetGeocentricNearestAngleDegrees(
            nadir,
            siblingCenter,
            spanMeters,
            PlanetRadius
        );
        float horizon = PlanetLodViewPolicy.GetHorizonHalfAngleDegrees(PlanetRadius, 9f);

        Assert.Less(nearest, horizon);
        Assert.Greater(nearest, 0f);
    }

    [Test]
    public void GetGeocentricNearestAngle_RejectsChunkWhollyBeyondHorizon()
    {
        Vector3 nadir = Vector3.forward;
        Vector3 farCenter = Quaternion.Euler(45f, 0f, 0f) * nadir;
        float spanMeters = PlanetRadius * 0.05f;

        float nearest = PlanetLodViewPolicy.GetGeocentricNearestAngleDegrees(
            nadir,
            farCenter,
            spanMeters,
            PlanetRadius
        );
        float horizon = PlanetLodViewPolicy.GetHorizonHalfAngleDegrees(PlanetRadius, 9f);

        Assert.Greater(nearest, horizon);
        Assert.IsTrue(PlanetLodViewPolicy.IsEntireCapBeyondHorizon(nearest, horizon));
    }

    [Test]
    public void OverlappingHorizonCap_IsNotEntirelyBeyond()
    {
        const float moonRadius = 173700f;
        const float altitude = 3.7f;
        float horizon = PlanetLodViewPolicy.GetHorizonHalfAngleDegrees(moonRadius, altitude);
        const float nearest = 1.27f;
        const float spanMeters = 5428f;
        Vector3 nadir = Vector3.forward;
        Vector3 center = Quaternion.Euler(nearest + 0.9f, 0f, 0f) * nadir;

        float farthest = PlanetLodViewPolicy.GetGeocentricFarthestAngleDegrees(
            nadir,
            center,
            spanMeters,
            moonRadius
        );

        Assert.Less(nearest, horizon);
        Assert.Greater(farthest, horizon);
        Assert.IsFalse(PlanetLodViewPolicy.IsEntireCapBeyondHorizon(nearest, horizon));
    }

    [Test]
    public void GetCameraConeFarthestAngle_IsLargerThanNearest()
    {
        var frame = new PlanetLodViewPolicy.ViewerFrame
        {
            HasCamera = true,
            CameraWorldPosition = new Vector3(0f, 0f, -100f),
            CameraForward = Vector3.forward,
        };
        Vector3 chunkCenter = new Vector3(0f, 0f, PlanetRadius);
        float radius = PlanetRadius * 0.1f;

        float nearest = PlanetLodViewPolicy.GetCameraConeNearestAngleDegrees(
            frame,
            chunkCenter,
            radius
        );
        float farthest = PlanetLodViewPolicy.GetCameraConeFarthestAngleDegrees(
            frame,
            chunkCenter,
            radius
        );

        Assert.Less(nearest, farthest);
    }

    [Test]
    public void ClassifyZone_UsesCameraConeNotGeocentricAngle()
    {
        PlanetLodViewPolicy.ViewZone zone = PlanetLodViewPolicy.ClassifyZone(
            35f,
            40f
        );

        Assert.AreEqual(PlanetLodViewPolicy.ViewZone.Active, zone);
    }

    [Test]
    public void ScreenSpaceTargetSpan_GrowsWithDistanceAndStaysFineNearShip()
    {
        const float tanHalfFov = 0.5773503f;
        const float height = 1080f;
        const float pixelError = 96f;

        float near = PlanetLodViewPolicy.ComputeScreenSpaceTargetSpanMeters(
            50f,
            tanHalfFov,
            height,
            pixelError
        );
        float horizon = PlanetLodViewPolicy.ComputeScreenSpaceTargetSpanMeters(
            8000f,
            tanHalfFov,
            height,
            pixelError
        );

        Assert.Less(near, 12f);
        Assert.Greater(horizon, 600f);
        Assert.Less(horizon, 1200f);
        Assert.Greater(horizon, near * 50f);
    }
}
