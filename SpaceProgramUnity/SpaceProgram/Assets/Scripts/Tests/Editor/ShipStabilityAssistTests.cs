using System;
using NUnit.Framework;

public sealed class ShipStabilityAssistTests
{
    const double StepDt = 0.02;
    const double ManualTurnRate = 55.0 * Math.PI / 180.0;
    const double AutoTurnRate = 120.0 * Math.PI / 180.0;

    [Test]
    public void DevHold_SteeringDoesNotSnapBackToEnablePose()
    {
        Vessel vessel = CreateDevHoldVessel();
        SimQuaternion enablePose = vessel.orientation;

        StepDevHold(vessel, pitchInput: 0.0f, usePadAxes: true);
        enablePose = vessel.orientation;

        for (int i = 0; i < 60; i++)
        {
            StepDevHold(vessel, pitchInput: 1.0f, usePadAxes: true);
        }

        double steeredAngle = AngleBetween(enablePose, vessel.orientation);
        Assert.Greater(steeredAngle, 8.0 * Math.PI / 180.0);

        SimQuaternion releasedPose = vessel.orientation;

        for (int i = 0; i < 30; i++)
        {
            StepDevHold(vessel, pitchInput: 0.0f, usePadAxes: true);
        }

        Assert.Greater(AngleBetween(enablePose, vessel.orientation), 8.0 * Math.PI / 180.0);
        Assert.Less(AngleBetween(releasedPose, vessel.orientation), 2.0 * Math.PI / 180.0);
    }

    [Test]
    public void DevHold_IdleKeepsEnablePose()
    {
        Vessel vessel = CreateDevHoldVessel();
        StepDevHold(vessel, pitchInput: 0.0f, usePadAxes: true);
        SimQuaternion enablePose = vessel.orientation;

        for (int i = 0; i < 20; i++)
        {
            StepDevHold(vessel, pitchInput: 0.0f, usePadAxes: true);
        }

        Assert.Less(AngleBetween(enablePose, vessel.orientation), 0.1 * Math.PI / 180.0);
    }

    static Vessel CreateDevHoldVessel()
    {
        Vessel vessel = new Vessel("Ship", SimVector3.Zero(), SimVector3.Zero());
        vessel.orientation = SimQuaternion.Identity();
        vessel.angularVelocity = SimVector3.Zero();
        vessel.controlState.orientationMode = ShipOrientationMode.DevStabilityAssist;
        ShipStabilityAssist.NotifyOrientationMode(
            vessel,
            ShipOrientationMode.DevStabilityAssist
        );
        return vessel;
    }

    static void StepDevHold(Vessel vessel, float pitchInput, bool usePadAxes)
    {
        vessel.controlState.pitchInput = pitchInput;
        vessel.controlState.yawInput = 0.0f;
        vessel.controlState.rollInput = 0.0f;
        ShipStabilityAssist.Apply(
            vessel,
            StepDt,
            usePadAxes,
            ManualTurnRate,
            AutoTurnRate,
            800.0,
            80.0,
            unlimitedAuthority: true,
            rollTurnRateMultiplier: 4.0
        );
        vessel.IntegrateOrientation(StepDt);
        ShipStabilityAssist.FollowHoldAfterSteeringIntegrate(vessel);
    }

    static double AngleBetween(SimQuaternion a, SimQuaternion b)
    {
        SimQuaternion na = a.Normalize();
        SimQuaternion nb = b.Normalize();
        double dot = Math.Abs(na.w * nb.w + na.x * nb.x + na.y * nb.y + na.z * nb.z);
        dot = Math.Min(1.0, dot);
        return 2.0 * Math.Acos(dot);
    }
}
