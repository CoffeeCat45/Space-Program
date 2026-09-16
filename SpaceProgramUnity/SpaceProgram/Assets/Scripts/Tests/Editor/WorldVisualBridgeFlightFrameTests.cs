using System;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldVisualBridgeFlightFrameTests
{
    static global::SimVector3 CapeSim()
    {
        return PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            28.5728 * Math.PI / 180.0,
            -80.6480 * Math.PI / 180.0
        );
    }

    static global::SimVector3 EastTangent(global::SimVector3 up)
    {
        global::SimVector3 east = global::SimVector3.Cross(global::SimVector3.UnitY(), up);
        return east.LengthSquared() > 1e-12 ? east.Normalize() : global::SimVector3.UnitZ().Multiply(-1.0);
    }

    [Test]
    public void FlightVectors_MatchPlanetGroundMotionEastAndNorthAtCape()
    {
        global::SimVector3 cape = CapeSim();
        global::SimVector3 east = EastTangent(cape);
        global::SimVector3 north = global::SimVector3.Cross(east, cape).Normalize();
        Vector3 capeUnity = WorldVisualBridge.SimNormalToUnity(cape);
        Quaternion planetRotation = WorldVisualBridge.GetFlightPlanetRotationFromBodyFixed(cape);

        Vector3 groundEast = planetRotation * WorldVisualBridge.SimNormalToMeshFrameUnity(east);
        Vector3 groundNorth = planetRotation * WorldVisualBridge.SimNormalToMeshFrameUnity(north);
        Vector3 shipEast = WorldVisualBridge.SimVelocityToUnityFlight(east, capeUnity);
        Vector3 shipNorth = WorldVisualBridge.SimVelocityToUnityFlight(north, capeUnity);

        Assert.Greater(
            Vector3.Dot(groundEast.normalized, shipEast.normalized),
            0.99f,
            "East: ship visual must match terrain stream (Atlantic to the right)."
        );
        Assert.Greater(
            Vector3.Dot(groundNorth.normalized, shipNorth.normalized),
            0.99f,
            "North: Mesh Frame used to invert ground vs ship; visual Z must follow flipX(simNorth)."
        );
    }

    [Test]
    public void PadOrientation_NoseStaysZenithAndPlusZStaysEast()
    {
        global::SimVector3 cape = CapeSim();
        global::SimVector3 east = EastTangent(cape);
        global::SimQuaternion pad = global::SimQuaternion.FromForwardAndUp(east, cape);
        Vector3 capeUnity = WorldVisualBridge.SimNormalToUnity(cape);
        Quaternion visual = WorldVisualBridge.SimOrientationToFlightVisualRotation(
            pad,
            capeUnity
        );

        Vector3 nose = visual * Vector3.up;
        Vector3 padForward = visual * Vector3.forward;

        Assert.Greater(Vector3.Dot(nose.normalized, Vector3.up), 0.99f);
        Assert.Greater(Vector3.Dot(padForward.normalized, Vector3.right), 0.99f);
    }

    [Test]
    public void PadPositivePitchRate_TipsNoseEastTowardAtlantic()
    {
        Vector3 nose = TipPadNose(global::SimVector3.UnitX(), 15.0);
        Assert.Greater(
            nose.x,
            0.1f,
            "Flight W maps to +body X after Core −pitchInput: nose toward +X (east)."
        );
    }

    [Test]
    public void PadPositiveYawRate_TipsNoseSouth()
    {
        Vector3 nose = TipPadNose(global::SimVector3.UnitZ(), 15.0);
        Assert.Greater(
            nose.z,
            0.1f,
            "Flight D maps to +body Z after Core −yawInput: nose toward +Z (south / right from behind)."
        );
    }

    static Vector3 TipPadNose(global::SimVector3 bodyAxis, double degrees)
    {
        global::SimVector3 cape = CapeSim();
        global::SimVector3 east = EastTangent(cape);
        global::SimQuaternion pad = global::SimQuaternion.FromForwardAndUp(east, cape);
        global::SimQuaternion delta = global::SimQuaternion.FromAxisAngle(
            bodyAxis,
            degrees * Math.PI / 180.0
        );
        global::SimQuaternion tipped = global::SimQuaternion.Multiply(pad, delta);
        Quaternion visual = WorldVisualBridge.SimOrientationToFlightVisualRotation(
            tipped,
            WorldVisualBridge.SimNormalToUnity(cape)
        );
        return visual * Vector3.up;
    }

    [Test]
    public void GetFlightDistantBodyLocalPosition_UndoesUniverseAltitudeSlide()
    {
        Vector3 moonInShipFrame = new Vector3(0f, 1_840_000f, 0f);
        Vector3 universeSlide = new Vector3(0f, -2_020_000f, 0f);
        Vector3 local = WorldVisualBridge.GetFlightDistantBodyLocalPosition(
            moonInShipFrame,
            universeSlide
        );

        Assert.AreEqual(new Vector3(0f, 3_860_000f, 0f), local);
        Assert.AreEqual(moonInShipFrame, universeSlide + local);
    }

    [Test]
    public void InertialOffsetToFlight_KeepsNadirUpWhenLodSpinIsOn()
    {
        global::SimVector3 cape = CapeSim();
        global::SimQuaternion lodSpin = global::SimQuaternion.FromAxisAngle(
            global::SimVector3.UnitY(),
            Math.PI / 3.0
        );
        global::SimVector3 inertialRadial = lodSpin.Rotate(cape);
        Quaternion lodFlight = WorldVisualBridge.GetFlightPlanetRotationFromBodyFixed(cape);
        Quaternion inertialToFlight = WorldVisualBridge.GetInertialMeshToFlightRotation(
            lodFlight,
            lodSpin
        );
        Vector3 flight = WorldVisualBridge.InertialOffsetToFlight(
            inertialRadial,
            inertialToFlight
        );

        Assert.Greater(
            Vector3.Dot(flight.normalized, Vector3.up),
            0.99f,
            "Ship-radial inertial offset must stay zenith after InverseSpin."
        );

        Vector3 capeUnity = WorldVisualBridge.SimNormalToUnity(inertialRadial);
        Quaternion oldEnu = WorldVisualBridge.GetSimToFlightFrameRotation(capeUnity);
        Assert.Greater(
            Quaternion.Angle(inertialToFlight, oldEnu),
            5f,
            "Distant-body frame must not ignore LOD spin (that desyncs map vs flight faces)."
        );
    }

    [Test]
    public void GetInertialMeshToFlightRotation_MovesWhenSpinAdvancesUnderFrozenFlight()
    {
        global::SimVector3 cape = CapeSim();
        Quaternion frozenFlight = WorldVisualBridge.GetFlightPlanetRotationFromBodyFixed(cape);
        Quaternion atLaunch = WorldVisualBridge.GetInertialMeshToFlightRotation(
            frozenFlight,
            global::SimQuaternion.Identity()
        );
        Quaternion later = WorldVisualBridge.GetInertialMeshToFlightRotation(
            frozenFlight,
            global::SimQuaternion.FromAxisAngle(global::SimVector3.UnitY(), Math.PI / 6.0)
        );

        Assert.Greater(
            Quaternion.Angle(atLaunch, later),
            20f,
            "Pad-locked planet mesh with live Core spin must rotate the inertial skybox."
        );
    }

    [Test]
    public void FlightVisualInertialFrame_MatchesPlanetLocalRotationAndInterpolatedSpin()
    {
        global::SimVector3 cape = CapeSim();
        Quaternion planetLocal = WorldVisualBridge.GetFlightPlanetRotationFromBodyFixed(cape);
        global::SimQuaternion spin = global::SimQuaternion.FromAxisAngle(
            global::SimVector3.UnitY(),
            Math.PI / 5.0
        );
        WorldVisualBridge.BeginFlightVisualInertialFrame(planetLocal, spin);

        Assert.IsTrue(
            WorldVisualBridge.TryGetFlightVisualInertialToFlight(out Quaternion fromFrame)
        );
        Quaternion expected = WorldVisualBridge.GetInertialMeshToFlightRotation(
            planetLocal,
            spin
        );
        Assert.Less(Quaternion.Angle(fromFrame, expected), 0.01f);

        WorldVisualBridge.EndFlightVisualInertialFrame();
        Assert.IsFalse(
            WorldVisualBridge.TryGetFlightVisualInertialToFlight(out _)
        );
    }

    [Test]
    public void FlightCameraUsesInertialOrbitFrame_NeverForShipFollow()
    {
        Assert.IsFalse(
            WorldVisualBridge.FlightCameraUsesInertialOrbitFrame(false, true),
            "Pad ship-follow stays identity (horizon-locked)."
        );
        Assert.IsFalse(
            WorldVisualBridge.FlightCameraUsesInertialOrbitFrame(false, false),
            "Flying ship-follow stays identity like the vessel / Scene."
        );
        Assert.IsFalse(
            WorldVisualBridge.FlightCameraUsesInertialOrbitFrame(true, false),
            "Map ship-follow stays identity; F restores ship."
        );
    }

    [Test]
    public void FlightCameraOrbitFrame_PadAndFlyingStayIdentity()
    {
        Assert.AreEqual(
            WorldVisualBridge.FlightCameraUsesInertialOrbitFrame(false, true),
            WorldVisualBridge.FlightCameraUsesInertialOrbitFrame(false, false),
            "Liftoff must not switch the chase camera into inertial Mesh-to-flight."
        );
    }

    [Test]
    public void SignedOrbitRollDegrees_MeasuresTwistAroundLook()
    {
        Vector3 look = Vector3.forward;
        Vector3 frameUp = Vector3.up;
        Vector3 cameraUp = Quaternion.AngleAxis(30.0f, look) * frameUp;
        Assert.AreEqual(
            30.0f,
            WorldVisualBridge.SignedOrbitRollDegrees(look, frameUp, cameraUp),
            0.05f
        );
    }

    [Test]
    public void MapShipOrbitFrame_LocalUpFollowsMeshRadialAtCape()
    {
        // Math helper for Mesh-Frame outward. Live flying camera stays identity,
        // not this SoI-radial frame and not inertial Mesh-to-flight.
        global::SimVector3 cape = CapeSim();
        Quaternion frame = WorldVisualBridge.GetMapShipOrbitFrameRotation(cape);
        Vector3 localUp = frame * Vector3.up;
        Vector3 meshRadial = WorldVisualBridge.SimNormalToMeshFrameUnity(cape);

        Assert.Greater(
            Vector3.Dot(localUp.normalized, meshRadial.normalized),
            0.99f,
            "SoI-radial helper +Y is Mesh-Frame outward (not applied as live camera frame)."
        );
    }

    [Test]
    public void GetFlightVisualReorientationDelta_SameRadial_IsIdentity()
    {
        global::SimVector3 cape = CapeSim();
        global::SimVector3 east = EastTangent(cape);
        global::SimQuaternion orientation = global::SimQuaternion.FromForwardAndUp(east, cape);
        Quaternion delta = WorldVisualBridge.GetFlightVisualReorientationDelta(
            orientation,
            cape,
            cape
        );

        Assert.Less(
            Quaternion.Angle(Quaternion.identity, delta),
            0.05f,
            "No SoI change must not rotate the chase offset."
        );
    }

    [Test]
    public void GetFlightVisualReorientationDelta_EarthToMoonRadial_PreservesShipLocalCameraOffset()
    {
        global::SimVector3 earthRadial = CapeSim();
        global::SimVector3 moonRadial = new global::SimVector3(0.0, 0.0, 1.0);
        global::SimVector3 east = EastTangent(earthRadial);
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

        Assert.Greater(
            Quaternion.Angle(Quaternion.identity, delta),
            20f,
            "Earth↔Moon radial swap must produce a real flight-frame jump."
        );

        Vector3 oldOffset = Quaternion.Euler(10.0f, 90.0f, 0.0f) * Vector3.back;
        Vector3 newOffset = delta * oldOffset;
        Vector3 oldShipLocal = Quaternion.Inverse(oldVisual) * oldOffset;
        Vector3 newShipLocal = Quaternion.Inverse(newVisual) * newOffset;

        Assert.Greater(
            Vector3.Dot(oldShipLocal.normalized, newShipLocal.normalized),
            0.99f,
            "Applying the SoI delta must keep the chase offset in ship space."
        );

        Vector3 oldLookShipLocal = Quaternion.Inverse(oldVisual) * (-oldOffset);
        Vector3 newLookShipLocal = Quaternion.Inverse(newVisual) * (-newOffset);
        Assert.Greater(
            Vector3.Dot(oldLookShipLocal.normalized, newLookShipLocal.normalized),
            0.99f,
            "SoI change must not invert the ship-relative camera forward."
        );
    }

    [Test]
    public void FlightOrbitOffsetToYawPitch_RoundTripsIdentityChase()
    {
        Vector3 offset = Quaternion.Euler(10.0f, 90.0f, 0.0f) * Vector3.back;
        WorldVisualBridge.FlightOrbitOffsetToYawPitch(
            offset,
            out float yaw,
            out float pitch
        );
        Vector3 roundTrip = Quaternion.Euler(pitch, yaw, 0.0f) * Vector3.back;

        Assert.Greater(
            Vector3.Dot(offset.normalized, roundTrip.normalized),
            0.99f
        );
        Assert.AreEqual(90.0f, yaw, 0.05f);
        Assert.AreEqual(10.0f, pitch, 0.05f);
    }
}
