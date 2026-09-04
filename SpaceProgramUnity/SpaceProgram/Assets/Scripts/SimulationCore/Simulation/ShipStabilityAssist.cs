using System;

static class ShipStabilityAssist
{
    const double DeadzoneRadians = 0.1 * Math.PI / 180.0;
    const double TorqueFraction = 0.4;
    const double MaxHoldErrorRadians = Math.PI;

    public static void NotifyOrientationMode(Vessel vessel, ShipOrientationMode mode)
    {
        if (vessel == null || vessel.stabilityAssistLastMode == mode)
        {
            return;
        }

        vessel.stabilityAssistLastMode = mode;
        vessel.stabilityAssistTargetInitialized = false;
    }

    public static void Apply(
        Vessel vessel,
        double dt,
        bool usePadAxes,
        double manualTurnRateRadiansPerSecond,
        double maxAttitudeTorqueAngularRateRadiansPerSecond,
        double proportionalGain,
        double derivativeGain,
        bool unlimitedAuthority = false,
        double rollTurnRateMultiplier = 1.0
    )
    {
        if (vessel == null || dt <= 0.0)
        {
            return;
        }

        if (vessel.controlState.orientationMode != vessel.stabilityAssistLastMode)
        {
            NotifyOrientationMode(vessel, vessel.controlState.orientationMode);
        }

        if (!vessel.stabilityAssistTargetInitialized)
        {
            vessel.stabilityAssistTargetOrientation = vessel.orientation;
            vessel.stabilityAssistTargetInitialized = true;
            vessel.stabilityAssistPrevPitchInput = 0.0f;
            vessel.stabilityAssistPrevYawInput = 0.0f;
            vessel.stabilityAssistPrevRollInput = 0.0f;
        }

        bool pitchActive = Math.Abs(vessel.controlState.pitchInput) > 1e-6f;
        bool yawActive = Math.Abs(vessel.controlState.yawInput) > 1e-6f;
        bool rollActive = Math.Abs(vessel.controlState.rollInput) > 1e-6f;
        bool hasInput = pitchActive || yawActive || rollActive;
        bool pitchReleased =
            Math.Abs(vessel.stabilityAssistPrevPitchInput) > 1e-6f && !pitchActive;
        bool yawReleased =
            Math.Abs(vessel.stabilityAssistPrevYawInput) > 1e-6f && !yawActive;
        bool rollReleased =
            Math.Abs(vessel.stabilityAssistPrevRollInput) > 1e-6f && !rollActive;

        if (pitchReleased || yawReleased || rollReleased)
        {
            vessel.stabilityAssistTargetOrientation = vessel.orientation;
        }
        else if (hasInput)
        {
            vessel.stabilityAssistTargetOrientation = SlideTargetForActiveAxes(
                vessel.stabilityAssistTargetOrientation,
                vessel.orientation,
                pitchActive,
                yawActive,
                rollActive
            );
        }

        vessel.stabilityAssistPrevPitchInput = vessel.controlState.pitchInput;
        vessel.stabilityAssistPrevYawInput = vessel.controlState.yawInput;
        vessel.stabilityAssistPrevRollInput = vessel.controlState.rollInput;

        if (unlimitedAuthority && !hasInput)
        {
            vessel.orientation = vessel.stabilityAssistTargetOrientation;
            vessel.angularVelocity = SimVector3.Zero();
            return;
        }

        double maxAssistRate = unlimitedAuthority ?
            double.PositiveInfinity :
            maxAttitudeTorqueAngularRateRadiansPerSecond * TorqueFraction;
        SimVector3 holdErrorBody = GetHoldErrorBody(
            vessel.stabilityAssistTargetOrientation,
            vessel.orientation
        );
        MapControlAxes(
            holdErrorBody,
            usePadAxes,
            out double pitchError,
            out double yawError,
            out double rollError
        );
        MapControlAxes(
            vessel.angularVelocity,
            usePadAxes,
            out double pitchRate,
            out double yawRate,
            out double rollRate
        );

        double pitchAccel = pitchActive ?
            0.0 :
            ComputeAxisAngularAcceleration(
                pitchError,
                pitchRate,
                proportionalGain,
                derivativeGain
            );
        double yawAccel = yawActive ?
            0.0 :
            ComputeAxisAngularAcceleration(
                yawError,
                yawRate,
                proportionalGain,
                derivativeGain
            );
        double rollAccel = rollActive ?
            0.0 :
            ComputeAxisAngularAcceleration(
                rollError,
                rollRate,
                proportionalGain,
                derivativeGain
            );
        SimVector3 angularAcceleration = MapControlAngularAccelerationToBody(
            pitchAccel,
            yawAccel,
            rollAccel,
            usePadAxes
        );
        if (!unlimitedAuthority)
        {
            double maxAngularAcceleration = maxAssistRate / Math.Max(dt, 1e-6);
            double accelerationMagnitude = angularAcceleration.Length();

            if (accelerationMagnitude > maxAngularAcceleration)
            {
                angularAcceleration = angularAcceleration.Multiply(
                    maxAngularAcceleration / accelerationMagnitude
                );
            }
        }

        SimVector3 nextAngularVelocity = vessel.angularVelocity.Add(
            angularAcceleration.Multiply(dt)
        );

        if (!unlimitedAuthority)
        {
            double nextRateMagnitude = nextAngularVelocity.Length();

            if (nextRateMagnitude > maxAssistRate)
            {
                nextAngularVelocity = nextAngularVelocity.Multiply(
                    maxAssistRate / nextRateMagnitude
                );
            }
        }

        vessel.angularVelocity = nextAngularVelocity;

        if (hasInput)
        {
            ApplyManualAngularRates(
                vessel,
                manualTurnRateRadiansPerSecond,
                pitchActive,
                yawActive,
                rollActive,
                usePadAxes,
                dt,
                rollTurnRateMultiplier
            );
        }

        if (!unlimitedAuthority)
        {
            vessel.ClampAngularVelocity(maxAssistRate);
        }
    }

    static SimQuaternion SlideTargetForActiveAxes(
        SimQuaternion target,
        SimQuaternion current,
        bool pitchActive,
        bool yawActive,
        bool rollActive
    )
    {
        SimVector3 targetToCurrentError = GetTargetToCurrentErrorBody(target, current);
        SimVector3 inactiveAxisError = targetToCurrentError;

        if (pitchActive)
        {
            inactiveAxisError = new SimVector3(0.0, inactiveAxisError.y, inactiveAxisError.z);
        }

        if (yawActive)
        {
            inactiveAxisError = new SimVector3(
                inactiveAxisError.x,
                0.0,
                inactiveAxisError.z
            );
        }

        if (rollActive)
        {
            inactiveAxisError = new SimVector3(
                inactiveAxisError.x,
                inactiveAxisError.y,
                0.0
            );
        }

        SimQuaternion inactiveRotation = FromRotationVector(inactiveAxisError);
        return SimQuaternion
            .Multiply(current, inactiveRotation.Conjugate())
            .Normalize();
    }

    static double ComputeAxisAngularAcceleration(
        double errorRadians,
        double rateRadiansPerSecond,
        double proportionalGain,
        double derivativeGain
    )
    {
        if (Math.Abs(errorRadians) < DeadzoneRadians)
        {
            return -derivativeGain * rateRadiansPerSecond;
        }

        return proportionalGain * errorRadians - derivativeGain * rateRadiansPerSecond;
    }

    static void ApplyManualAngularRates(
        Vessel vessel,
        double turnRate,
        bool pitchActive,
        bool yawActive,
        bool rollActive,
        bool usePadAxes,
        double dt,
        double rollTurnRateMultiplier
    )
    {
        double pitch = vessel.angularVelocity.x;
        double yaw = usePadAxes ? vessel.angularVelocity.z : vessel.angularVelocity.y;
        double roll = usePadAxes ? vessel.angularVelocity.y : vessel.angularVelocity.z;
        double rollScale = rollTurnRateMultiplier > 1e-6 ? rollTurnRateMultiplier : 1.0;
        double rollMaxRate = turnRate * rollScale;
        SimVector3 targetBodyRates = ShipManualAttitudeRamp.ResolveManualBodyRateTargets(
            vessel.controlState.pitchInput,
            vessel.controlState.yawInput,
            vessel.controlState.rollInput,
            usePadAxes,
            turnRate
        );

        if (pitchActive)
        {
            ShipManualAttitudeRamp.RampAxisTowardTarget(
                ref pitch,
                targetBodyRates.x,
                dt,
                turnRate
            );
        }

        if (yawActive)
        {
            double yawTarget = usePadAxes ? targetBodyRates.z : targetBodyRates.y;
            ShipManualAttitudeRamp.RampAxisTowardTarget(
                ref yaw,
                yawTarget,
                dt,
                turnRate
            );
        }

        if (rollActive)
        {
            double rollTarget = (usePadAxes ? targetBodyRates.y : targetBodyRates.z) * rollScale;
            ShipManualAttitudeRamp.RampAxisTowardTarget(
                ref roll,
                rollTarget,
                dt,
                rollMaxRate
            );
        }

        vessel.angularVelocity = usePadAxes ?
            new SimVector3(pitch, roll, yaw) :
            new SimVector3(pitch, yaw, roll);
    }

    static SimVector3 MapControlAngularAccelerationToBody(
        double pitchAccel,
        double yawAccel,
        double rollAccel,
        bool usePadAxes
    )
    {
        if (usePadAxes)
        {
            return new SimVector3(pitchAccel, rollAccel, yawAccel);
        }

        return new SimVector3(pitchAccel, yawAccel, rollAccel);
    }

    static void MapControlAxes(
        SimVector3 bodyVector,
        bool usePadAxes,
        out double pitch,
        out double yaw,
        out double roll
    )
    {
        pitch = bodyVector.x;
        yaw = bodyVector.y;
        roll = bodyVector.z;

        if (!usePadAxes)
        {
            return;
        }

        yaw = bodyVector.z;
        roll = bodyVector.y;
    }

    static SimVector3 GetHoldErrorBody(SimQuaternion target, SimQuaternion current)
    {
        return GetRotationErrorBody(current, target);
    }

    static SimVector3 GetTargetToCurrentErrorBody(
        SimQuaternion target,
        SimQuaternion current
    )
    {
        return GetRotationErrorBody(target, current);
    }

    static SimVector3 GetRotationErrorBody(
        SimQuaternion fromOrientation,
        SimQuaternion toOrientation
    )
    {
        SimQuaternion delta = SimQuaternion
            .Multiply(fromOrientation.Conjugate(), toOrientation)
            .Normalize();

        if (delta.w < 0.0)
        {
            delta = new SimQuaternion(
                -delta.w,
                -delta.x,
                -delta.y,
                -delta.z
            );
        }

        double sinHalf = Math.Sqrt(
            delta.x * delta.x +
            delta.y * delta.y +
            delta.z * delta.z
        );

        if (sinHalf < 1e-12)
        {
            return SimVector3.Zero();
        }

        double angle = Math.Min(
            2.0 * Math.Atan2(sinHalf, delta.w),
            MaxHoldErrorRadians
        );

        return new SimVector3(
            delta.x / sinHalf,
            delta.y / sinHalf,
            delta.z / sinHalf
        ).Multiply(angle);
    }

    static SimQuaternion FromRotationVector(SimVector3 rotationVector)
    {
        double angle = rotationVector.Length();

        if (angle <= 1e-12)
        {
            return SimQuaternion.Identity();
        }

        return SimQuaternion.FromAxisAngle(
            rotationVector.Multiply(1.0 / angle),
            Math.Min(angle, MaxHoldErrorRadians)
        );
    }
}
