using System;
using UnityEngine;

/// <summary>
/// KSP-style distant Earth in flight: keep the globe inside the camera far clip
/// (same angular size), then drop texture/mesh detail. Not a spin/frame handoff.
/// </summary>
public static class PlanetDistantVisualPolicy
{
    public enum GlobeQuality
    {
        Full = 0,
        Color2K = 1,
        Coarse = 2
    }

    /// <summary>Force the 2K NASA PNG on the flight shell.</summary>
    public const float Color2KStartMeters = 1_500_000f;

    /// <summary>Hysteresis: stay on 2K until descent goes below this.</summary>
    public const float Color2KEndMeters = 1_350_000f;

    /// <summary>Coarser lat/lon shell, still 2K color.</summary>
    public const float CoarseStartMeters = 5_000_000f;

    public const float CoarseEndMeters = 4_500_000f;

    public const int DefaultShellSubdivisions = FlightPlanetMapSphere.DefaultIcosphereSubdivisions;
    public const int CoarseShellSubdivisions = 1;

    /// <summary>
    /// Fraction of the flight far clip reserved for the far limb (atmosphere included).
    /// Below 1 the globe is pulled closer like Scaled Space.
    /// </summary>
    public const float FarClipFill = 0.82f;

    /// <summary>
    /// Tighter budget for a second globe in the sky (Earth from the Moon). The
    /// untextured icosphere used diameter-scale with a radius-1 mesh, so the far
    /// limb sat on the clip plane and popped with camera angle.
    /// </summary>
    public const float DistantSecondaryFarClipFill = 0.62f;

    /// <summary>
    /// Place a secondary globe (Earth from the Moon) this close so the far limb
    /// stays well inside the flight far clip and the depth buffer can resolve the disk.
    /// Angular size is preserved by the same scale on radius.
    /// </summary>
    public const float DistantSecondaryTargetCenterMeters = 120_000f;

    public const float AtmosphereShellHeightMeters = 115000f;

    /// <summary>
    /// Flight distant Moon/Earth globe: after atmosphere (3000), before clouds (3200).
    /// Day sky is ~98% opaque and ZWrite Off; Geometry-queue Moon sits behind it.
    /// Terrain still occludes via ZTest. Do not use 120 km pull (disk in front of Earth).
    /// </summary>
    public const int FlightDistantGlobeRenderQueue = 3050;

    public static GlobeQuality ResolveGlobeQuality(float altitudeMeters, GlobeQuality current)
    {
        float altitude = Mathf.Max(0f, altitudeMeters);

        if (altitude >= CoarseStartMeters)
        {
            return GlobeQuality.Coarse;
        }

        if (current == GlobeQuality.Coarse && altitude >= CoarseEndMeters)
        {
            return GlobeQuality.Coarse;
        }

        if (altitude >= Color2KStartMeters)
        {
            return GlobeQuality.Color2K;
        }

        if (
            (current == GlobeQuality.Color2K || current == GlobeQuality.Coarse) &&
            altitude >= Color2KEndMeters
        )
        {
            return GlobeQuality.Color2K;
        }

        return GlobeQuality.Full;
    }

    public static bool UsesDistantColor2K(GlobeQuality quality)
    {
        return quality != GlobeQuality.Full;
    }

    public static int GetShellSubdivisions(GlobeQuality quality, bool mapMode)
    {
        if (mapMode)
        {
            return DefaultShellSubdivisions;
        }

        return quality == GlobeQuality.Coarse ?
            CoarseShellSubdivisions :
            DefaultShellSubdivisions;
    }

    /// <summary>
    /// Shrink and pull the globe toward the ship so the far limb stays inside the
    /// flight far clip. Angular size is unchanged. Scale is 1 on the pad.
    /// </summary>
    public static float GetVisualScale(
        double altitudeAbovePlanetMeters,
        double bottomExtentMeters,
        double surfaceHeightAboveSphereMeters
    )
    {
        if (double.IsNaN(altitudeAbovePlanetMeters) || double.IsInfinity(altitudeAbovePlanetMeters))
        {
            altitudeAbovePlanetMeters = 0.0;
        }

        if (double.IsNaN(bottomExtentMeters) || double.IsInfinity(bottomExtentMeters))
        {
            bottomExtentMeters = 0.0;
        }

        if (double.IsNaN(surfaceHeightAboveSphereMeters) ||
            double.IsInfinity(surfaceHeightAboveSphereMeters))
        {
            surfaceHeightAboveSphereMeters = 0.0;
        }

        double comAltitude = Math.Max(
            0.0,
            altitudeAbovePlanetMeters + bottomExtentMeters
        );
        double planetRadius = CelestialTerrainRuntime.LodBodyRadiusUnity;
        double centerDistance = planetRadius +
            comAltitude +
            surfaceHeightAboveSphereMeters;
        double farLimb = centerDistance + planetRadius + AtmosphereShellHeightMeters;
        double clipBudget = WorldVisualBridge.FlightCameraFarClip * FarClipFill;

        if (farLimb <= clipBudget || farLimb <= 1.0)
        {
            return 1f;
        }

        return (float)(clipBudget / farLimb);
    }

    /// <summary>
    /// Scaled-space shrink for a globe that is not the LOD body.
    /// <paramref name="pullToNearSky"/> is for Earth-from-Moon (large disk in the sky).
    /// Moon-from-Earth stays at true range so flight, map, and SoI share one geometry.
    /// </summary>
    public static float GetDistantSecondaryScale(
        float centerDistance,
        float visualRadius,
        bool pullToNearSky = true
    )
    {
        if (centerDistance <= 1f || !float.IsFinite(centerDistance))
        {
            return 1f;
        }

        float farLimb = centerDistance + Mathf.Max(1f, visualRadius);
        float clipBudget =
            WorldVisualBridge.FlightCameraFarClip * DistantSecondaryFarClipFill;
        float scaleByClip = farLimb > clipBudget && farLimb > 1f ?
            clipBudget / farLimb :
            1f;

        if (!pullToNearSky)
        {
            return 1f;
        }

        float scaleByDistance = DistantSecondaryTargetCenterMeters / centerDistance;
        return Mathf.Clamp(Mathf.Min(scaleByDistance, scaleByClip), 0.002f, 1f);
    }
}
