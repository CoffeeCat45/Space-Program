using UnityEngine;

/// <summary>
/// Altitude-driven LOD policy expressed as fractions of planet radius so it scales with body size.
/// Earth pad-start split/merge rings stay in the quadtree 🔒. These bands only cap
/// max LOD / PNG swap while climbing. Moon: coarsen ~2 km, L2 ~48 km, L0 72 km,
/// PNG ~75 km. L2 at 21 km made a 23 km nadir view a handful of 87 km tiles
/// (billiard ball) and then RING dumped the rest at once. Shell on/off uses
/// actual altitude.
/// </summary>
public static class PlanetLodAltitudePolicy
{
    /// <summary>Below this fraction, ground max LOD is allowed (~7.6 km at 1:10 Earth / ~2.1 km Moon).</summary>
    public const float CoarsenStartAltitudeFraction = 0.012f;

    /// <summary>End of gradual coarsen band (~48 km at 1:10 Earth).</summary>
    public const float CoarsenEndAltitudeFraction = 0.075f;

    /// <summary>Above this fraction, quadtree collapses to root faces (~51 km Earth).</summary>
    public const float OrbitalCoarseAltitudeFraction = 0.08f;

    /// <summary>PNG map shell replaces GEBCO chunks (~76 km at 1:10 Earth). No overlap.</summary>
    public const float MapShellBlendStartAltitudeFraction = 0.12f;

    public const float MapShellFullAltitudeFraction = 0.12f;

    /// <summary>Hysteresis band below blend start — prevents shell/chunk flutter.</summary>
    public const float MapShellHysteresisFraction = 0.015f;

    /// <summary>Same as blend start: chunks hide when the PNG shell appears.</summary>
    public const float MapShellChunkHideFraction = MapShellBlendStartAltitudeFraction;

    /// <summary>Bring GEBCO chunks back on descent (~64 km Earth).</summary>
    public const float MapShellChunkReshowFraction = 0.10f;

    /// <summary>
    /// Earth: do not merge the viewer/pad subtree below this altitude.
    /// Moon uses the L2 band (~48 km) via GetViewerSubtreeProtectAltitudeMeters.
    /// </summary>
    public const float EarthViewerSubtreeProtectAltitudeMeters = 30000f;

    /// <summary>Moon: first coarsen (~2.1 km). Same fraction as Earth so unload follows height.</summary>
    public const float MoonCoarsenStartAltitudeFraction = CoarsenStartAltitudeFraction;

    /// <summary>Moon: second coarsen (toward L2, ~48 km). Same meters as Earth L2.</summary>
    public const float MoonCoarsenEndAltitudeFraction = 48000f / 173700f;

    /// <summary>Moon: collapse to L0 at 72 km, just before PNG at 75 km.</summary>
    public const float MoonOrbitalCoarseAltitudeFraction = 72000f / 173700f;

    /// <summary>
    /// Moon PNG after L0. 75000/72000 → ~75 km. A long L0 window is a 6-face ball.
    /// </summary>
    public const float MoonMapShellAfterLodZeroScale = 75000f / 72000f;

    public const float MoonMapShellBlendStartAltitudeFraction =
        MoonOrbitalCoarseAltitudeFraction * MoonMapShellAfterLodZeroScale;

    public const float MoonMapShellFullAltitudeFraction = MoonMapShellBlendStartAltitudeFraction;

    /// <summary>
    /// Moon: show chunks again on descent (~62 km). Same hide/reshow ratio as Earth
    /// (0.10 / 0.12). A 35 km reshow left 45 km hops stuck on PNG with 4 faces.
    /// </summary>
    public const float MoonMapShellChunkReshowFraction =
        MoonMapShellBlendStartAltitudeFraction *
        (MapShellChunkReshowFraction / MapShellBlendStartAltitudeFraction);

    /// <summary>Moon 173.7 km vs Earth 637.1 km at 1:10. Do not use for pad RING logic.</summary>
    const float SmallBodyRadiusMeters = 400_000f;

    public struct AltitudeShellState
    {
        public bool ShellVisible;
        public bool ChunksHidden;
        public float SmoothedAltitudeMeters;
        public bool ShellMeshReady;
    }

    public const int CoarseMaxLodLevel = 2;
    public const int OrbitalMaxLodLevel = 0;

    public static bool UsesMoonAscentBands(float planetRadius)
    {
        return planetRadius > 1f && planetRadius < SmallBodyRadiusMeters;
    }

    public static float GetCoarsenStartAltitudeFraction(float planetRadius)
    {
        return UsesMoonAscentBands(planetRadius) ?
            MoonCoarsenStartAltitudeFraction :
            CoarsenStartAltitudeFraction;
    }

    public static float GetCoarsenEndAltitudeFraction(float planetRadius)
    {
        return UsesMoonAscentBands(planetRadius) ?
            MoonCoarsenEndAltitudeFraction :
            CoarsenEndAltitudeFraction;
    }

    /// <summary>
    /// Below this altitude the viewer/pad subtree is not merged for span-vs-target.
    /// Altitude-cap merges still run. Earth 30 km; Moon = L2 band (~48 km) so
    /// screen-space error cannot dump the disk under the ship before the cap.
    /// Pad 64 m force-split uses <see cref="GetPadForceSplitAltitudeMeters"/> — not this.
    /// </summary>
    public static float GetViewerSubtreeProtectAltitudeMeters(float planetRadius)
    {
        if (UsesMoonAscentBands(planetRadius))
        {
            return GetCoarsenEndAltitudeFraction(planetRadius) * Mathf.Max(1f, planetRadius);
        }

        return EarthViewerSubtreeProtectAltitudeMeters;
    }

    /// <summary>
    /// Stop forcing 64 m cells under the ship. Moon: coarsen start (~2 km). Earth: 30 km.
    /// Must not share the SSE-protect altitude — that kept moon force-split on until 48 km
    /// and fought the altitude cap (1–3 tiles popping, not a 2×2).
    /// </summary>
    public static float GetPadForceSplitAltitudeMeters(float planetRadius)
    {
        if (UsesMoonAscentBands(planetRadius))
        {
            return GetCoarsenStartAltitudeFraction(planetRadius) * Mathf.Max(1f, planetRadius);
        }

        return EarthViewerSubtreeProtectAltitudeMeters;
    }

    public static float GetOrbitalCoarseAltitudeFraction(float planetRadius)
    {
        return UsesMoonAscentBands(planetRadius) ?
            MoonOrbitalCoarseAltitudeFraction :
            OrbitalCoarseAltitudeFraction;
    }

    public static float GetMapShellBlendStartAltitudeFraction(float planetRadius)
    {
        return UsesMoonAscentBands(planetRadius) ?
            MoonMapShellBlendStartAltitudeFraction :
            MapShellBlendStartAltitudeFraction;
    }

    public static float GetMapShellFullAltitudeFraction(float planetRadius)
    {
        return UsesMoonAscentBands(planetRadius) ?
            MoonMapShellFullAltitudeFraction :
            MapShellFullAltitudeFraction;
    }

    public static float GetMapShellChunkHideFraction(float planetRadius)
    {
        return GetMapShellBlendStartAltitudeFraction(planetRadius);
    }

    public static float GetMapShellChunkReshowFraction(float planetRadius)
    {
        return UsesMoonAscentBands(planetRadius) ?
            MoonMapShellChunkReshowFraction :
            MapShellChunkReshowFraction;
    }

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
        float coarsenStart = GetCoarsenStartAltitudeFraction(planetRadius);
        float coarsenEnd = GetCoarsenEndAltitudeFraction(planetRadius);
        float orbitalCoarse = GetOrbitalCoarseAltitudeFraction(planetRadius);

        if (fraction <= coarsenStart)
        {
            return groundCap;
        }

        if (fraction >= orbitalCoarse)
        {
            return OrbitalMaxLodLevel;
        }

        if (fraction >= coarsenEnd)
        {
            float orbitalBlend = SmoothStep01(
                coarsenEnd,
                orbitalCoarse,
                fraction
            );
            return RoundLodLevel(
                Mathf.Lerp(CoarseMaxLodLevel, OrbitalMaxLodLevel, orbitalBlend),
                groundCap
            );
        }

        float coarsenBlend = SmoothStep01(
            coarsenStart,
            coarsenEnd,
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

        if (fraction <= GetCoarsenStartAltitudeFraction(planetRadius))
        {
            return 1f;
        }

        float blend = SmoothStep01(
            GetCoarsenStartAltitudeFraction(planetRadius),
            GetOrbitalCoarseAltitudeFraction(planetRadius),
            fraction
        );
        float multiplier = Mathf.Lerp(1f, 10f, blend);
        float cap = Mathf.Lerp(1.35f, 6f, blend);
        return Mathf.Min(multiplier, cap);
    }

    public static bool ShouldAllowNeighborBalance(float altitudeMeters, float planetRadius)
    {
        return GetAltitudeFraction(altitudeMeters, planetRadius) <=
            GetCoarsenEndAltitudeFraction(planetRadius);
    }

    public static float GetMapShellOpacity(float altitudeMeters, float planetRadius)
    {
        float fraction = GetAltitudeFraction(altitudeMeters, planetRadius);
        float shellStart = GetMapShellBlendStartAltitudeFraction(planetRadius);
        float shellFull = GetMapShellFullAltitudeFraction(planetRadius);

        if (fraction < shellStart)
        {
            return 0f;
        }

        if (fraction >= shellFull)
        {
            return 1f;
        }

        return Mathf.InverseLerp(shellStart, shellFull, fraction);
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
        float shellStart = GetMapShellBlendStartAltitudeFraction(planetRadius);
        float shellOffFraction = Mathf.Max(
            GetMapShellChunkReshowFraction(planetRadius),
            shellStart - MapShellHysteresisFraction
        );

        if (actualFraction >= shellStart)
        {
            state.ShellVisible = true;
        }
        else if (actualFraction <= shellOffFraction)
        {
            state.ShellVisible = false;
            state.ShellMeshReady = false;
            state.SmoothedAltitudeMeters = altitudeMeters;
        }
        else if (
            state.SmoothedAltitudeMeters > altitudeMeters + 250f &&
            actualFraction <= shellStart
        )
        {
            state.SmoothedAltitudeMeters = altitudeMeters;
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
