using UnityEngine;

/// <summary>
/// Altitude-driven LOD policy expressed as fractions of planet radius so it scales with body size.
/// </summary>
public static class PlanetLodAltitudePolicy
{
    /// <summary>Below this fraction, ground max LOD is allowed (~6 km at 1:10 Earth).</summary>
    public const float CoarsenStartAltitudeFraction = 0.012f;

    /// <summary>End of gradual coarsen band (~48 km at 1:10 Earth).</summary>
    public const float CoarsenEndAltitudeFraction = 0.075f;

    /// <summary>Above this fraction, quadtree collapses to root faces (~51 km).</summary>
    public const float OrbitalCoarseAltitudeFraction = 0.08f;

    /// <summary>PNG map shell replaces GEBCO chunks (~76 km at 1:10 Earth). No overlap.</summary>
    public const float MapShellBlendStartAltitudeFraction = 0.12f;

    public const float MapShellFullAltitudeFraction = 0.12f;

    /// <summary>Hysteresis band below blend start — prevents shell/chunk flutter.</summary>
    public const float MapShellHysteresisFraction = 0.015f;

    /// <summary>Same as blend start: chunks hide when the PNG shell appears.</summary>
    public const float MapShellChunkHideFraction = MapShellBlendStartAltitudeFraction;

    /// <summary>Bring GEBCO chunks back on descent (~60 km).</summary>
    public const float MapShellChunkReshowFraction = 0.10f;

    public struct AltitudeShellState
    {
        public bool ShellVisible;
        public bool ChunksHidden;
        public float SmoothedAltitudeMeters;
        public bool ShellMeshReady;
    }

    public const int CoarseMaxLodLevel = 2;
    public const int OrbitalMaxLodLevel = 0;

    public static float GetAltitudeFraction(float altitudeMeters, float planetRadius)
    {
        if (planetRadius <= 1f)
        {
            return 0f;
        }

        return Mathf.Max(0f, altitudeMeters) / planetRadius;
    }

    /// <summary>
    /// Maximum quadtree depth allowed at the current altitude. Low altitude → fine; orbit → coarse.
    /// </summary>
    public static int GetMaxLodLevelForAltitude(
        float altitudeMeters,
        float planetRadius,
        int groundMaxLodLevel,
        int absoluteMaxLodLevel
    )
    {
        int groundCap = Mathf.Clamp(groundMaxLodLevel, 0, absoluteMaxLodLevel);
        float fraction = GetAltitudeFraction(altitudeMeters, planetRadius);

        if (fraction <= CoarsenStartAltitudeFraction)
        {
            return groundCap;
        }

        if (fraction >= OrbitalCoarseAltitudeFraction)
        {
            return OrbitalMaxLodLevel;
        }

        if (fraction >= CoarsenEndAltitudeFraction)
        {
            float orbitalBlend = SmoothStep01(
                CoarsenEndAltitudeFraction,
                OrbitalCoarseAltitudeFraction,
                fraction
            );
            return RoundLodLevel(
                Mathf.Lerp(CoarseMaxLodLevel, OrbitalMaxLodLevel, orbitalBlend),
                groundCap
            );
        }

        float coarsenBlend = SmoothStep01(
            CoarsenStartAltitudeFraction,
            CoarsenEndAltitudeFraction,
            fraction
        );
        return RoundLodLevel(
            Mathf.Lerp(groundCap, CoarseMaxLodLevel, coarsenBlend),
            groundCap
        );
    }

    /// <summary>
    /// Slow coarsen while climbing; snap finer on descent. Slider ceiling is applied
    /// immediately: raising Max LOD must not wait for a 250 m descent.
    /// </summary>
    public static int ApplyMaxLodHysteresis(
        int rawLevel,
        int cachedLevel,
        int maxLodCeiling,
        int previousCeiling,
        bool viewerDescending
    )
    {
        maxLodCeiling = Mathf.Max(0, maxLodCeiling);
        rawLevel = Mathf.Clamp(rawLevel, 0, maxLodCeiling);

        if (cachedLevel < 0)
        {
            return rawLevel;
        }

        int capped = cachedLevel;

        if (previousCeiling >= 0 && maxLodCeiling > previousCeiling)
        {
            capped = rawLevel;
        }
        else if (rawLevel < capped)
        {
            capped = Mathf.Max(rawLevel, capped - 1);
        }
        else if (viewerDescending)
        {
            capped = rawLevel;
        }

        return Mathf.Clamp(capped, 0, maxLodCeiling);
    }

    static float SmoothStep01(float start, float end, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));
    }

    static int RoundLodLevel(float level, int groundCap)
    {
        return Mathf.Clamp(Mathf.RoundToInt(level), OrbitalMaxLodLevel, groundCap);
    }

    /// <summary>Multiplies split target span — larger values merge sooner at altitude.</summary>
    public static float GetSplitSpanAltitudeMultiplier(float altitudeMeters, float planetRadius)
    {
        float fraction = GetAltitudeFraction(altitudeMeters, planetRadius);

        if (fraction <= CoarsenStartAltitudeFraction)
        {
            return 1f;
        }

        float blend = SmoothStep01(
            CoarsenStartAltitudeFraction,
            OrbitalCoarseAltitudeFraction,
            fraction
        );
        float multiplier = Mathf.Lerp(1f, 10f, blend);
        float cap = Mathf.Lerp(1.35f, 6f, blend);
        return Mathf.Min(multiplier, cap);
    }

    public static bool ShouldAllowNeighborBalance(float altitudeMeters, float planetRadius)
    {
        return GetAltitudeFraction(altitudeMeters, planetRadius) <= CoarsenEndAltitudeFraction;
    }

    public static float GetMapShellOpacity(float altitudeMeters, float planetRadius)
    {
        float fraction = GetAltitudeFraction(altitudeMeters, planetRadius);

        if (fraction < MapShellBlendStartAltitudeFraction)
        {
            return 0f;
        }

        if (fraction >= MapShellFullAltitudeFraction)
        {
            return 1f;
        }

        return Mathf.InverseLerp(
            MapShellBlendStartAltitudeFraction,
            MapShellFullAltitudeFraction,
            fraction
        );
    }

    public static bool ShouldCollapseQuadtreeForAltitude(AltitudeShellState state)
    {
        return state.ChunksHidden;
    }

    public static void UpdateShellState(
        ref AltitudeShellState state,
        float altitudeMeters,
        float planetRadius,
        float deltaTime
    )
    {
        if (state.SmoothedAltitudeMeters <= 0f)
        {
            state.SmoothedAltitudeMeters = altitudeMeters;
        }
        else
        {
            state.SmoothedAltitudeMeters = Mathf.Lerp(
                state.SmoothedAltitudeMeters,
                altitudeMeters,
                Mathf.Clamp01(deltaTime * 1.5f)
            );
        }

        float actualFraction = GetAltitudeFraction(altitudeMeters, planetRadius);
        if (
            state.SmoothedAltitudeMeters > altitudeMeters + 250f &&
            actualFraction <= MapShellChunkReshowFraction
        )
        {
            state.SmoothedAltitudeMeters = altitudeMeters;
        }

        float fraction = GetAltitudeFraction(state.SmoothedAltitudeMeters, planetRadius);
        float shellOffFraction = MapShellBlendStartAltitudeFraction -
            MapShellHysteresisFraction;

        if (fraction >= MapShellBlendStartAltitudeFraction)
        {
            state.ShellVisible = true;
        }
        else if (fraction <= shellOffFraction)
        {
            state.ShellVisible = false;
            state.ShellMeshReady = false;
        }

        state.ChunksHidden = state.ShellVisible;
    }

    public static float GetMapShellOpacityForState(AltitudeShellState state, float planetRadius)
    {
        if (!state.ShellVisible)
        {
            return 0f;
        }

        return GetMapShellOpacity(state.SmoothedAltitudeMeters, planetRadius);
    }

    public static bool ShouldHideTerrainChunks(AltitudeShellState state)
    {
        return state.ChunksHidden;
    }
}
