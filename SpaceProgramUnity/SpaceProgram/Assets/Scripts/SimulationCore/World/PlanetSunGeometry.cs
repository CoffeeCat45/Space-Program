using System;

/// <summary>
/// Sun direction, noon spin, and planet umbra in simulation meters.
/// Visual code converts the resulting sim vectors through the flight frame.
/// </summary>
static class PlanetSunGeometry
{
    public static SimVector3 DirectionTowardSun(
        SimVector3 sunPosition,
        SimVector3 fromPosition
    )
    {
        SimVector3 toSun = sunPosition.Subtract(fromPosition);

        if (toSun.LengthSquared() <= 1e-24)
        {
            return SimVector3.Zero();
        }

        return toSun.Normalize();
    }

    /// <summary>
    /// Extra yaw after Cape morning alignment so the sun sits in the default
    /// Florida pad camera (side view, ~10° pitch) instead of near zenith.
    /// Other pads keep this same Earth spin; they are not forced into daylight.
    /// </summary>
    public const double LaunchMorningAzimuthOffsetRadians = 50.0 * Math.PI / 180.0;
    public static double ComputeYawAboutYToAlignAzimuth(
        SimVector3 bodyFixedDirection,
        SimVector3 worldTargetDirection
    )
    {
        double siteHorizontal =
            bodyFixedDirection.x * bodyFixedDirection.x +
            bodyFixedDirection.z * bodyFixedDirection.z;
        double targetHorizontal =
            worldTargetDirection.x * worldTargetDirection.x +
            worldTargetDirection.z * worldTargetDirection.z;

        if (siteHorizontal <= 1e-12 || targetHorizontal <= 1e-12)
        {
            return 0.0;
        }

        double siteAzimuth = Math.Atan2(bodyFixedDirection.x, bodyFixedDirection.z);
        double targetAzimuth = Math.Atan2(worldTargetDirection.x, worldTargetDirection.z);
        return targetAzimuth - siteAzimuth;
    }

    /// <summary>
    /// 1 = full sunlight, 0 = planet fully blocks the sun (umbra).
    /// A thin penumbra avoids a hard pop at the terminator.
    /// </summary>
    public static double DirectSunVisibility(
        SimVector3 point,
        SimVector3 sunPosition,
        SimVector3 occluderCenter,
        double occluderRadius
    )
    {
        if (occluderRadius <= 0.0)
        {
            return 1.0;
        }

        SimVector3 toSun = sunPosition.Subtract(point);
        double sunDistanceSquared = toSun.LengthSquared();

        if (sunDistanceSquared <= 1e-24)
        {
            return 1.0;
        }

        double sunDistance = Math.Sqrt(sunDistanceSquared);
        SimVector3 sunDirection = toSun.Multiply(1.0 / sunDistance);
        SimVector3 toOccluder = occluderCenter.Subtract(point);
        double along = SimVector3.Dot(toOccluder, sunDirection);

        if (along <= 0.0 || along >= sunDistance)
        {
            return 1.0;
        }

        double perpSquared = Math.Max(0.0, toOccluder.LengthSquared() - along * along);
        double perp = Math.Sqrt(perpSquared);
        double penumbraWidth = Math.Max(500.0, occluderRadius * 0.02);
        double umbraEdge = occluderRadius - penumbraWidth;
        double sunlitEdge = occluderRadius + penumbraWidth;

        if (perp <= umbraEdge)
        {
            return 0.0;
        }

        if (perp >= sunlitEdge)
        {
            return 1.0;
        }

        return (perp - umbraEdge) / (sunlitEdge - umbraEdge);
    }
}
