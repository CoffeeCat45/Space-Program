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
}
