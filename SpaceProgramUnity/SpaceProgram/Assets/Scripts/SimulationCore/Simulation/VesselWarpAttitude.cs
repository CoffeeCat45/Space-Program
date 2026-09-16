using System;

/// <summary>
/// Kepler-coast nose tracking without SAS PID. Public so Editor tests can
/// cover the cap; <see cref="Vessel"/> stays internal.
/// </summary>
public static class VesselWarpAttitude
{
    public static void RotateNoseTowardWorld(
        ref SimQuaternion orientation,
        ref SimVector3 position,
        ref SimVector3 angularVelocity,
        SimVector3 centerOfMassLocal,
        SimVector3 worldDirection,
        double maxRadians
    )
    {
        if (maxRadians <= 0.0 || worldDirection.LengthSquared() <= 1e-18)
        {
            angularVelocity = SimVector3.Zero();
            return;
        }

        SimVector3 nose = orientation.Up();
        if (nose.LengthSquared() <= 1e-18)
        {
            angularVelocity = SimVector3.Zero();
            return;
        }

        nose = nose.Normalize();
        SimVector3 target = worldDirection.Normalize();
        double alignment = Math.Max(-1.0, Math.Min(1.0, SimVector3.Dot(nose, target)));
        double angle = Math.Acos(alignment);
        if (angle <= 1e-12)
        {
            angularVelocity = SimVector3.Zero();
            return;
        }

        double apply = Math.Min(angle, maxRadians);
        SimVector3 axis = SimVector3.Cross(nose, target);
        if (axis.LengthSquared() <= 1e-18)
        {
            axis = SimVector3.Cross(nose, SimVector3.UnitX());
            if (axis.LengthSquared() <= 1e-18)
            {
                axis = SimVector3.Cross(nose, SimVector3.UnitZ());
            }
        }

        if (axis.LengthSquared() <= 1e-18)
        {
            angularVelocity = SimVector3.Zero();
            return;
        }

        SimVector3 comBefore = position.Add(orientation.Rotate(centerOfMassLocal));
        SimQuaternion delta = SimQuaternion.FromAxisAngle(axis.Normalize(), apply);
        orientation = SimQuaternion.Multiply(delta, orientation).Normalize();
        SimVector3 comAfter = position.Add(orientation.Rotate(centerOfMassLocal));
        position = position.Add(comBefore.Subtract(comAfter));
        angularVelocity = SimVector3.Zero();
    }
}
