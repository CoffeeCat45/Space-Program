using System;

/// <summary>
/// KSP-style warp caps: physics warp ×1–×4 on the pad / in thick air / with throttle
/// parked; rails stay unlimited in vacuum.
/// </summary>
public static class TimeWarpLimits
{
    public const double PhysicsWarpMax = 4.0;
    public const double AnalyticWarpWhileThrusting = 1.0;
    public const double UpperAtmosphereMax = 10.0;
    public const double AtmosphereEdgeMax = 50.0;

    public static double GetAtmosphereCap(double depthFraction)
    {
        if (depthFraction >= 0.5)
        {
            return PhysicsWarpMax;
        }

        if (depthFraction >= 0.1)
        {
            return UpperAtmosphereMax;
        }

        return AtmosphereEdgeMax;
    }

    public static double GetMaxTimeScale(
        bool thrustRestricted,
        bool thrustActive,
        bool onKeplerRails,
        bool landedOrCrashed,
        bool inAtmosphere,
        double atmosphereDepthFraction
    )
    {
        double maxScale = double.PositiveInfinity;

        if (thrustRestricted)
        {
            maxScale = thrustActive ?
                AnalyticWarpWhileThrusting :
                PhysicsWarpMax;
        }
        else if (onKeplerRails)
        {
            return double.PositiveInfinity;
        }

        if (landedOrCrashed)
        {
            maxScale = Math.Min(maxScale, PhysicsWarpMax);
        }

        if (inAtmosphere)
        {
            maxScale = Math.Min(
                maxScale,
                GetAtmosphereCap(atmosphereDepthFraction)
            );
        }

        return maxScale;
    }
}
