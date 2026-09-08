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
    /// Legacy near-sky pull (Earth-from-Moon). Do not use: 120 km sits on the lunar
    /// horizon distance and the disk wins ZTest against the Moon.
    /// </summary>
    public const float DistantSecondaryTargetCenterMeters = 120_000f;

    public const float AtmosphereShellHeightMeters = 115000f;

    /// <summary>
    /// Flight distant Moon/Earth globe: after local atmosphere (3000), before clouds (3200).
    /// Terrain still occludes via ZTest. Do not use 120 km pull (disk in front of Earth).
    /// </summary>
    public const int FlightDistantGlobeRenderQueue = 3050;

    /// <summary>
    /// Thin distant Earth limb after the globe (3050), before clouds (3200).
    /// Not the 115 km flight shell — that reads as a bubble from the Moon.
    /// </summary>
    public const int FlightDistantAtmosphereRenderQueue = 3070;

    /// <summary>NASA / noise cloud shells. Must stay after the Earth color globe.</summary>
    public const int CloudRenderQueue = 3200;

    /// <summary>
    /// Visible Chapman rim from the Moon (~2% of 1:10 Earth radius). Interior
    /// flight shell stays <see cref="AtmosphereShellHeightMeters"/>.
    /// </summary>
    public const float DistantLimbHeightMeters = 12_000f;

    /// <summary>
    /// Cloud shell on a distant Earth. 1.8 km is below far-clip depth precision
    /// at lunar distance; 8 km still reads as weather on the disk.
    /// </summary>
    public const float DistantCloudHeightMeters = 8_000f;

    /// <summary>Flight Chapman, slightly stronger than pad limb — not map 0.014.</summary>
    public const float DistantLimbStrength = 0.00008f;

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

    /// <summary>
    /// LOD Earth at this altitude is a PNG disk (SoI exit, Earth-from-Moon).
    /// Clouds need the distant 8 km shell; flight PNG must drop ZWrite or weather loses ZTest.
    /// </summary>
    public static bool UsesDistantEarthWeather(float altitudeMeters)
    {
        return altitudeMeters >= Color2KStartMeters;
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
    /// Clip-only shrink for a globe that is not the LOD body. Angular size is
    /// unchanged. Never pull to <see cref="DistantSecondaryTargetCenterMeters"/>:
    /// that puts Earth on the lunar horizon and the disk draws through the Moon.
    /// Moon-from-Earth and Earth-from-Moon share this path.
    /// </summary>
    public static float GetDistantSecondaryScale(
        float centerDistance,
        float visualRadius,
        bool pullToNearSky = false,
        float atmospherePaddingMeters = 0f
    )
    {
        if (centerDistance <= 1f || !float.IsFinite(centerDistance))
        {
            return 1f;
        }

        float farLimb = centerDistance +
            Mathf.Max(1f, visualRadius) +
            Mathf.Max(0f, atmospherePaddingMeters);
        float clipBudget =
            WorldVisualBridge.FlightCameraFarClip * DistantSecondaryFarClipFill;
        float scaleByClip = farLimb > clipBudget && farLimb > 1f ?
            clipBudget / farLimb :
            1f;
        scaleByClip = Mathf.Clamp(scaleByClip, 0.002f, 1f);

        if (!pullToNearSky)
        {
            return scaleByClip;
        }

        float scaleByDistance = DistantSecondaryTargetCenterMeters / centerDistance;
        return Mathf.Clamp(Mathf.Min(scaleByDistance, scaleByClip), 0.002f, 1f);
    }

    /// <summary>
    /// Map PNG must stay opaque with ZWrite so the color map is visible.
    /// Flight distant Earth (from the Moon) turns ZWrite off so the 8 km cloud
    /// shell wins ZTest; lunar terrain still occludes the disk.
    /// Do not put a second depth mesh on Earth — a failed/black pass covers the PNG.
    /// </summary>
    public static void ApplyEarthColorGlobeCompositing(Material material, bool mapMode)
    {
        if (material == null)
        {
            return;
        }

        if (mapMode)
        {
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            material.SetFloat("_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            return;
        }

        material.renderQueue = FlightDistantGlobeRenderQueue;
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
    }

    public static void ApplyMoonGlobeCompositing(Material material, bool mapMode)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = mapMode ?
            (int)UnityEngine.Rendering.RenderQueue.Geometry :
            FlightDistantGlobeRenderQueue;
        material.SetFloat("_ZWrite", 1f);
        material.SetOverrideTag("RenderType", "Opaque");
    }
}
