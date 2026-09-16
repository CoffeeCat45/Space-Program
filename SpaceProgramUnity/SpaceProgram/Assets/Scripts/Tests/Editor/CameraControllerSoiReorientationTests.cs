using NUnit.Framework;
using UnityEngine;

public sealed class CameraControllerSoiReorientationTests
{
    [Test]
    public void PreserveOrbitThroughWorldReorientation_KeepsShipLocalOffset()
    {
        GameObject cameraObject = new GameObject("SoiChaseCamera");
        CameraController camera = cameraObject.AddComponent<CameraController>();

        try
        {
            camera.SetOrbitAngles(90.0f, 10.0f);
            Vector3 oldOffset = camera.GetOrbitOffsetDirection();

            global::SimVector3 earthRadial = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
                28.5728 * System.Math.PI / 180.0,
                -80.6480 * System.Math.PI / 180.0
            );
            global::SimVector3 moonRadial = new global::SimVector3(0.0, 0.0, 1.0);
            global::SimVector3 east = global::SimVector3.Cross(
                global::SimVector3.UnitY(),
                earthRadial
            );
            east = east.LengthSquared() > 1e-12 ?
                east.Normalize() :
                global::SimVector3.UnitZ().Multiply(-1.0);
            global::SimQuaternion orientation = global::SimQuaternion.FromForwardAndUp(
                east,
                earthRadial
            );
            Quaternion oldVisual = WorldVisualBridge.SimOrientationToFlightVisualRotation(
                orientation,
                WorldVisualBridge.SimNormalToUnity(earthRadial)
            );
            Quaternion newVisual = WorldVisualBridge.SimOrientationToFlightVisualRotation(
                orientation,
                WorldVisualBridge.SimNormalToUnity(moonRadial)
            );
            Quaternion delta = WorldVisualBridge.GetFlightVisualReorientationDelta(
                orientation,
                earthRadial,
                moonRadial
            );

            camera.PreserveOrbitThroughWorldReorientation(delta, applyToLiveOrbit: true);
            Vector3 newOffset = camera.GetOrbitOffsetDirection();

            Vector3 oldShipLocal = Quaternion.Inverse(oldVisual) * oldOffset;
            Vector3 newShipLocal = Quaternion.Inverse(newVisual) * newOffset;
            Assert.Greater(
                Vector3.Dot(oldShipLocal.normalized, newShipLocal.normalized),
                0.99f,
                "Live chase yaw/pitch must follow the ship visual jump, not stay in old world axes."
            );

            Vector3 oldLook = Quaternion.Inverse(oldVisual) * (-oldOffset);
            Vector3 newLook = Quaternion.Inverse(newVisual) * (-newOffset);
            Assert.Greater(
                Vector3.Dot(oldLook.normalized, newLook.normalized),
                0.99f,
                "SoI reorientation must not invert ship-relative forward."
            );
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void PreserveOrbitThroughWorldReorientation_MapModeLeavesLiveOrbit()
    {
        GameObject cameraObject = new GameObject("SoiMapCamera");
        CameraController camera = cameraObject.AddComponent<CameraController>();

        try
        {
            camera.SetMapModeActive(true, WorldVisualBridge.MapPositionScale);
            camera.SetOrbitAngles(45.0f, 12.0f);
            Quaternion yawDelta = Quaternion.Euler(0.0f, 90.0f, 0.0f);
            camera.PreserveOrbitThroughWorldReorientation(yawDelta, applyToLiveOrbit: false);
            camera.GetOrbitAngles(out float yaw, out float pitch);
            Assert.AreEqual(45.0f, yaw, 0.05f);
            Assert.AreEqual(12.0f, pitch, 0.05f);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void UpdateShipFollow_UsesExplicitShipPivot()
    {
        GameObject cameraObject = new GameObject("ShipFollowCamera");
        GameObject planet = new GameObject("Planet");
        planet.transform.position = new Vector3(0.0f, -1000.0f, 0.0f);
        CameraController camera = cameraObject.AddComponent<CameraController>();

        try
        {
            camera.SetTarget(planet.transform, updateDefault: true);
            Vector3 shipPivot = new Vector3(1.0f, 2.0f, 3.0f);
            camera.UpdateShipFollow(shipPivot, 0.0f, 2.0, WorldVisualBridge.FlightPositionScale);

            Assert.AreEqual(
                shipPivot,
                camera.GetFollowPivotWorldPosition(),
                "Flight chase must orbit the ship pivot, not the planet target transform."
            );
            Assert.Greater(
                (camera.GetFollowPivotWorldPosition() - planet.transform.position).sqrMagnitude,
                1.0f
            );
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(planet);
        }
    }

    [Test]
    public void SetOrbitFrameKeepingWorldView_DoesNotJumpOffset()
    {
        GameObject cameraObject = new GameObject("FrameSwitchCamera");
        CameraController camera = cameraObject.AddComponent<CameraController>();

        try
        {
            camera.SetOrbitAngles(40.0f, 15.0f);
            Vector3 before = camera.GetOrbitOffsetDirection();
            camera.SetOrbitFrameKeepingWorldView(Quaternion.Euler(0.0f, 90.0f, 0.0f));
            Vector3 after = camera.GetOrbitOffsetDirection();
            Assert.Greater(
                Vector3.Dot(before.normalized, after.normalized),
                0.99f,
                "Switching Horizon/Vessel must keep the current Game view."
            );
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void PreserveOrbitThroughWorldReorientation_KeepsSignedRoll()
    {
        GameObject cameraObject = new GameObject("SoiRollCamera");
        CameraController camera = cameraObject.AddComponent<CameraController>();

        try
        {
            camera.SetOrbitAngles(0.0f, 0.0f);
            camera.transform.rotation = Quaternion.LookRotation(
                Vector3.forward,
                Quaternion.Euler(0.0f, 0.0f, 25.0f) * Vector3.up
            );
            camera.SetOrbitFrameKeepingWorldView(Quaternion.identity);
            Assert.AreEqual(25.0f, camera.GetOrbitRollDegrees(), 1.5f);

            camera.PreserveOrbitThroughWorldReorientation(
                Quaternion.Euler(0.0f, 90.0f, 0.0f),
                applyToLiveOrbit: true
            );
            Assert.AreEqual(
                25.0f,
                camera.GetOrbitRollDegrees(),
                2.0f,
                "SoI yaw/pitch remap must keep chase roll so a side view does not become nadir."
            );
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void MapModeRoundTrip_RestoresVesselOrbitFrame()
    {
        GameObject cameraObject = new GameObject("MapRoundTripCamera");
        CameraController camera = cameraObject.AddComponent<CameraController>();

        try
        {
            Quaternion vesselFrame = Quaternion.Euler(12.0f, 40.0f, 8.0f);
            camera.SetOrbitAngles(22.0f, 11.0f);
            camera.SetOrbitFrameRotation(vesselFrame);
            Vector3 before = camera.GetOrbitOffsetDirection();

            camera.SetMapModeActive(true, WorldVisualBridge.MapPositionScale);
            camera.SetOrbitAngles(5.0f, 3.0f);
            camera.SetMapModeActive(false, WorldVisualBridge.FlightPositionScale);

            Vector3 after = camera.GetOrbitOffsetDirection();
            Assert.Greater(
                Vector3.Dot(before.normalized, after.normalized),
                0.99f,
                "Leaving map must restore the flight chase frame, not treat ship-local yaw as horizon."
            );
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }
}
