using NUnit.Framework;
using UnityEngine;

public sealed class FlightCameraDiagTests
{
    [Test]
    public void ClassifyKind_NearShipWithZenithUp_IsHorizon()
    {
        Assert.AreEqual(
            FlightCameraFollowKind.Horizon,
            FlightCameraDiag.ClassifyKind(
                mapMode: false,
                chaseMode: false,
                distCameraToShip: 40.0f,
                distCameraToPlanet: 173700.0f,
                distPivotToShip: 0.4f,
                upAngleToUnityY: 3.0f,
                upAngleToShipUp: 4.0f,
                upAngleToInertial: 88.0f
            )
        );
    }

    [Test]
    public void ClassifyKind_CloserToPlanetThanShip_IsPlanetPos()
    {
        Assert.AreEqual(
            FlightCameraFollowKind.PlanetPos,
            FlightCameraDiag.ClassifyKind(
                mapMode: false,
                chaseMode: false,
                distCameraToShip: 1.2e6f,
                distCameraToPlanet: 800.0f,
                distPivotToShip: 1.2e6f,
                upAngleToUnityY: 5.0f,
                upAngleToShipUp: 5.0f,
                upAngleToInertial: 90.0f
            )
        );
    }

    [Test]
    public void ClassifyKind_InertialUp_IsInertial()
    {
        Assert.AreEqual(
            FlightCameraFollowKind.Inertial,
            FlightCameraDiag.ClassifyKind(
                mapMode: false,
                chaseMode: false,
                distCameraToShip: 40.0f,
                distCameraToPlanet: 173700.0f,
                distPivotToShip: 0.4f,
                upAngleToUnityY: 90.0f,
                upAngleToShipUp: 88.0f,
                upAngleToInertial: 4.0f
            )
        );
    }

    [Test]
    public void ClassifyPlanetScreen_PlanetAlongCameraLeft_IsLeft()
    {
        Quaternion camera = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        FlightCameraPlanetScreen screen = FlightCameraDiag.ClassifyPlanetScreen(
            Vector3.zero,
            camera,
            new Vector3(-100.0f, 0.0f, 10.0f)
        );
        Assert.AreEqual(FlightCameraPlanetScreen.Left, screen);
    }

    [Test]
    public void ClassifyPlanetScreen_PlanetBelowCamera_IsDown()
    {
        Quaternion camera = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        FlightCameraPlanetScreen screen = FlightCameraDiag.ClassifyPlanetScreen(
            Vector3.zero,
            camera,
            new Vector3(0.0f, -100.0f, 10.0f)
        );
        Assert.AreEqual(FlightCameraPlanetScreen.Down, screen);
    }

    [Test]
    public void Build_HorizonCompactContainsTokens()
    {
        FlightCameraDiagSnapshot snap = FlightCameraDiag.Build(
            cameraPosition: new Vector3(0.0f, 10.0f, -40.0f),
            cameraRotation: Quaternion.LookRotation(Vector3.forward, Vector3.up),
            pivotPosition: Vector3.zero,
            shipPosition: Vector3.zero,
            planetPosition: new Vector3(0.0f, -173700.0f, 0.0f),
            shipUp: Vector3.up,
            inertialUp: Vector3.right,
            soiName: "Moon",
            focusName: "Ship",
            padLocked: false,
            mapMode: false,
            chaseMode: false,
            yawDegrees: 88.0f,
            pitchDegrees: 12.0f,
            orbitFrameAngleFromIdentity: 0.0f,
            orbitDistance: 40.0f
        );

        Assert.AreEqual(FlightCameraFollowKind.Horizon, snap.Kind);
        Assert.AreEqual(FlightCameraPlanetScreen.Down, snap.PlanetScreen);
        StringAssert.Contains("HORIZON", snap.CompactLine);
        StringAssert.Contains("DOWN", snap.CompactLine);
        StringAssert.Contains("Moon", snap.CompactLine);
    }
}
