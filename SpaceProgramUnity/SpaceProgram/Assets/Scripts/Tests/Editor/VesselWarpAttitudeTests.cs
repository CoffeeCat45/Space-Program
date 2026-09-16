using System;
using NUnit.Framework;

public sealed class VesselWarpAttitudeTests
{
    [Test]
    public void RotateNoseTowardWorld_DoesNotOvershootSmallError()
    {
        SimQuaternion orientation = SimQuaternion.Identity();
        SimVector3 position = SimVector3.Zero();
        SimVector3 angularVelocity = new SimVector3(0.5, 0.0, 0.0);
        SimVector3 target = new SimVector3(0.1, 1.0, 0.0).Normalize();
        double error = Math.Acos(
            Math.Max(-1.0, Math.Min(1.0, SimVector3.Dot(orientation.Up(), target)))
        );

        VesselWarpAttitude.RotateNoseTowardWorld(
            ref orientation,
            ref position,
            ref angularVelocity,
            SimVector3.Zero(),
            target,
            error
        );

        SimVector3 nose = orientation.Up().Normalize();
        Assert.Greater(SimVector3.Dot(nose, target), 0.999);
        Assert.Less(angularVelocity.LengthSquared(), 1e-18);
    }

    [Test]
    public void RotateNoseTowardWorld_CapsToMaxRadians()
    {
        SimQuaternion orientation = SimQuaternion.Identity();
        SimVector3 position = SimVector3.Zero();
        SimVector3 angularVelocity = SimVector3.Zero();
        SimVector3 target = SimVector3.UnitX();

        VesselWarpAttitude.RotateNoseTowardWorld(
            ref orientation,
            ref position,
            ref angularVelocity,
            SimVector3.Zero(),
            target,
            5.0 * Math.PI / 180.0
        );

        SimVector3 nose = orientation.Up().Normalize();
        double remaining = Math.Acos(
            Math.Max(-1.0, Math.Min(1.0, SimVector3.Dot(nose, target)))
        );
        Assert.Greater(remaining, 70.0 * Math.PI / 180.0);
        Assert.Less(remaining, 90.0 * Math.PI / 180.0);
    }
}
