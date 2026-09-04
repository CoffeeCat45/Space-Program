using System;

/// <summary>
/// Gradual body-frame attitude rate control (KSP-style: torque ramps up, not instant spin).
/// </summary>
static class ShipManualAttitudeRamp
{
    public const double DefaultResponseTimeSeconds = 0.9;
    const double MinResponseTimeSeconds = 0.05;

    public static SimVector3 ResolveManualBodyRateTargets(
        float pitchInput,
        float yawInput,
        float rollInput,
        bool usePadAxes,
        double turnRate
    )
    {
        double pitchTarget = -pitchInput * turnRate;

        if (usePadAxes)
        {
            // 🔒 Y-up VAB: Q/E roll +Y, A/D yaw +Z with −yawInput.
            // Do not flip this sign to "fix" Mesh Frame heading — that inverts thrust too.
            // WASD vs KSP is the Unity keymap (SimulationPilotInput), not these signs.
            // Visual planet rotation: WorldVisualBridge.GetFlightPlanetLocalRotation
            // (context/systems/work_terrain.md §3.1).
            return new SimVector3(
                pitchTarget,
                rollInput * turnRate,
                -yawInput * turnRate
            );
        }

        return new SimVector3(
            pitchTarget,
            yawInput * turnRate,
            -rollInput * turnRate
        );
    }

    public static void RampTowardBodyRates(
        Vessel vessel,
        double dt,
        double pitchTargetRate,
        double yawTargetRate,
        double rollTargetRate,
        double maxAngularRate,
        double responseTimeSeconds = DefaultResponseTimeSeconds
    )
    {
        if (vessel == null || dt <= 0.0)
        {
            return;
        }

        double responseTime = Math.Max(responseTimeSeconds, MinResponseTimeSeconds);
        double maxAngularAcceleration = maxAngularRate / responseTime;
        double proportionalGain = 1.0 / responseTime;
        double derivativeGain = 2.2 / responseTime;
        SimVector3 current = vessel.angularVelocity;
        SimVector3 target = new SimVector3(
            pitchTargetRate,
            yawTargetRate,
            rollTargetRate
        );
        SimVector3 delta = target.Subtract(current);
        SimVector3 angularAcceleration = new SimVector3(
            ComputeAxisAcceleration(delta.x, current.x, proportionalGain, derivativeGain),
            ComputeAxisAcceleration(delta.y, current.y, proportionalGain, derivativeGain),
            ComputeAxisAcceleration(delta.z, current.z, proportionalGain, derivativeGain)
        );
        angularAcceleration = SimVector3.ClampMagnitude(
            angularAcceleration,
            maxAngularAcceleration
        );
        vessel.angularVelocity = current.Add(angularAcceleration.Multiply(dt));
        vessel.ClampAngularVelocity(maxAngularRate);
    }

    public static void RampAxisTowardTarget(
        ref double currentRate,
        double targetRate,
        double dt,
        double maxAngularRate,
        double responseTimeSeconds = DefaultResponseTimeSeconds
    )
    {
        if (dt <= 0.0)
        {
            return;
        }

        double responseTime = Math.Max(responseTimeSeconds, MinResponseTimeSeconds);
        double maxAngularAcceleration = maxAngularRate / responseTime;
        double proportionalGain = 1.0 / responseTime;
        double derivativeGain = 2.2 / responseTime;
        double delta = targetRate - currentRate;
        double angularAcceleration = ComputeAxisAcceleration(
            delta,
            currentRate,
            proportionalGain,
            derivativeGain
        );
        angularAcceleration = Math.Clamp(
            angularAcceleration,
            -maxAngularAcceleration,
            maxAngularAcceleration
        );
        currentRate += angularAcceleration * dt;

        if (Math.Abs(currentRate) > maxAngularRate)
        {
            currentRate = Math.Sign(currentRate) * maxAngularRate;
        }
    }

    static double ComputeAxisAcceleration(
        double rateError,
        double currentRate,
        double proportionalGain,
        double derivativeGain
    )
    {
        return proportionalGain * rateError - derivativeGain * currentRate;
    }
}
