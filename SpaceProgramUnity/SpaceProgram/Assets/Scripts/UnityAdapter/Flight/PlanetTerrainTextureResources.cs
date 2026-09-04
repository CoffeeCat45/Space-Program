using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shared RSS / RSS-Terrain texture references that survive planet object rebuilds.
/// </summary>
public static class PlanetTerrainTextureResources
{
    static Texture2D earthColorMap;
    static Texture2D moonColorMap;
    static Texture2D distantColorMap2k;
    static bool earthColorUsesCanonicalGeographicUv;
    static bool moonColorUsesCanonicalGeographicUv;
    static PlanetMapLayoutPolicy earthColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
    static PlanetMapLayoutPolicy moonColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
    static long earthColorSourceTimestampUtcTicks;
    static string earthColorSourcePath;
    static string moonColorSourcePath;
    static Texture2D earthNormalMap;
    static Texture2D earthHeightMap;
    static PlanetParallaxTerrainSet terrainSet;
    static PlanetParallaxTerrainSet cachedEarthTerrainSet;
    static PlanetParallaxTerrainSet cachedMoonTerrainSet;
    static string terrainSetLodBodyName;
    static readonly List<Texture2D> runtimeTextures = new List<Texture2D>();

    public static Texture2D EarthColorMap => earthColorMap;

    public static Texture2D MoonColorMap => moonColorMap;

    /// <summary>
    /// Always the 2K NASA PNG when present. Distant flight shell uses this so an 8K
    /// load is not sampled across the whole disk at millions of meters.
    /// </summary>
    public static Texture2D GetFlightDistantColorMap()
    {
        if (CelestialTerrainRuntime.IsMoon)
        {
            EnsureMoonColorLoaded();
            return moonColorMap;
        }

        EnsureEarthColorLoaded();

        if (
            IsAlive(earthColorMap) &&
            !string.IsNullOrEmpty(earthColorSourcePath) &&
            string.Equals(
                earthColorSourcePath,
                EarthGeographicMapPaths.StandardPngPath,
                System.StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return earthColorMap;
        }

        if (IsAlive(distantColorMap2k))
        {
            return distantColorMap2k;
        }

        if (TryLoadGeographicPngAtPath(
            EarthGeographicMapPaths.StandardPngPath,
            out distantColorMap2k,
            EarthGeographicMapPaths.StandardMaxLoadWidth
        ))
        {
            return distantColorMap2k;
        }

        return earthColorMap;
    }

    public static bool EarthColorUsesCanonicalGeographicUv =>
        earthColorUsesCanonicalGeographicUv;

    public static bool MoonColorUsesCanonicalGeographicUv =>
        moonColorUsesCanonicalGeographicUv;

    public static PlanetMapLayoutPolicy EarthColorLayoutPolicy => earthColorLayoutPolicy;

    public static Texture2D EarthNormalMap => earthNormalMap;

    public static Texture2D EarthHeightMap => earthHeightMap;

    public static PlanetParallaxTerrainSet TerrainSet => terrainSet;

    public static void EnsureLoaded()
    {
        InvalidateStaleResources();
        EnsureEarthColorLoaded();

        if (CelestialTerrainRuntime.IsMoon)
        {
            EnsureMoonColorLoaded();
            earthHeightMap = null;
            earthNormalMap = null;
            EnsureTerrainSetLoaded();
            return;
        }

        if (PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline)
        {
            earthHeightMap = null;
            earthNormalMap = null;
            EnsureTerrainSetLoaded();
            return;
        }

        EnsureEarthNormalLoaded();
        EnsureEarthHeightLoaded();
        EnsureTerrainSetLoaded();
    }

    public static void InvalidateLodBodySplatSet()
    {
        StashTerrainSetForLodBody();
        terrainSet = null;
        terrainSetLodBodyName = null;
    }

    public static void ResetDiagnosticsSession()
    {
    }

    public static void PrepareForPlanetRebuild()
    {
        // Keep cached earth color across FlightPlanetSphere.Build(); avoid redundant disk reload.
    }

    /// <summary>
    /// Drops cached world-map textures so the next <see cref="EnsureLoaded"/> reloads them
    /// with the current loader/UV convention (avoids stale bake + runtime RSS double remap).
    /// </summary>
    public static void ForceReloadWorldMaps()
    {
        DestroyRuntimeOwnedTextures();
        terrainSet = null;
        terrainSetLodBodyName = null;
        earthColorSourceTimestampUtcTicks = 0;
        earthColorSourcePath = null;
        moonColorSourcePath = null;
        MapModeEarthResources.Invalidate();
    }

    /// <summary>
    /// Reload only moon_geographic (4K/8K toggle).
    /// </summary>
    public static void ForceReloadMoonColorMapOnly()
    {
        InvalidateMoonColorMap();
        EnsureMoonColorLoaded();
        RefreshActiveEarthColorShaderProperties();
    }

    /// <summary>
    /// Reload only earth_geographic (2K/8K toggle). Keeps RSS-Terrain splats and height DDS alive.
    /// </summary>
    public static void ForceReloadEarthColorMapOnly()
    {
        InvalidateEarthColorMap();
        EnsureEarthColorLoaded();
        RefreshActiveEarthColorShaderProperties();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        DestroyRuntimeOwnedTextures();
        terrainSet = null;
        cachedEarthTerrainSet = null;
        cachedMoonTerrainSet = null;
        terrainSetLodBodyName = null;
        earthColorMap = null;
        moonColorMap = null;
        distantColorMap2k = null;
        earthNormalMap = null;
        earthHeightMap = null;
        earthColorUsesCanonicalGeographicUv = false;
        moonColorUsesCanonicalGeographicUv = false;
        earthColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
        moonColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
        earthColorSourceTimestampUtcTicks = 0;
        earthColorSourcePath = null;
        moonColorSourcePath = null;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    static void ResetStaticsOnEnterPlayMode()
    {
        ResetStatics();
    }
#endif

    public static void ApplyTo(FlightPlanetSphere planetSphere)
    {
        if (planetSphere == null)
        {
            return;
        }

        EnsureLoaded();
        planetSphere.ApplySharedTerrainResources(
            CelestialTerrainRuntime.IsMoon ? moonColorMap : earthColorMap,
            earthNormalMap,
            earthHeightMap,
            terrainSet
        );
    }

    public static Texture2D GetMoonColorMap()
    {
        EnsureMoonColorLoaded();
        return moonColorMap;
    }

    static void EnsureMoonColorLoaded()
    {
        if (IsCurrentMoonColorCacheValid())
        {
            return;
        }

        if (IsRuntimeOwned(moonColorMap))
        {
            Object.Destroy(moonColorMap);
            runtimeTextures.Remove(moonColorMap);
        }

        moonColorMap = null;
        moonColorUsesCanonicalGeographicUv = false;
        moonColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
        moonColorSourcePath = null;

        if (!TryLoadMoonGeographicColorMap(out moonColorMap, out string loadedPath))
        {
            Debug.LogWarning(
                "PlanetTerrainTextureResources: " +
                MoonGeographicMapPaths.ActivePngFileName +
                " missing at " + MoonGeographicMapPaths.ActivePngPath +
                ". Moon map/chunks will use a gray fallback."
            );
            return;
        }

        moonColorUsesCanonicalGeographicUv = true;
        moonColorLayoutPolicy = PlanetMapLayoutPolicy.NasaGeographicPng;
        moonColorSourcePath = loadedPath;
        bool looks8K = moonColorMap.width >= 8000;
        Debug.Log(
            "PlanetTerrainTextureResources: moon_geographic loaded " +
            moonColorMap.width + "x" + moonColorMap.height +
            " quality=" + (looks8K ? "8K" : "4K") +
            " path=" + loadedPath + "."
        );

        if (DevSettings.MoonGeographicMapHigh8K && !looks8K)
        {
            Debug.LogWarning(
                "PlanetTerrainTextureResources: Moon 8K requested but loaded " +
                moonColorMap.width + "x" + moonColorMap.height +
                " from " + loadedPath +
                ". Put moon_geographic_8k.png in StreamingAssets/MapMode."
            );
        }
    }

    static bool IsCurrentMoonColorCacheValid()
    {
        if (!IsAlive(moonColorMap) || string.IsNullOrEmpty(moonColorSourcePath))
        {
            return false;
        }

        if (!File.Exists(moonColorSourcePath))
        {
            return false;
        }

        string[] candidates = MoonGeographicMapPaths.ResolveLoadCandidatePaths();

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!File.Exists(candidates[i]))
            {
                continue;
            }

            return string.Equals(
                moonColorSourcePath,
                candidates[i],
                System.StringComparison.OrdinalIgnoreCase
            );
        }

        return false;
    }

    static bool TryLoadMoonGeographicColorMap(out Texture2D texture, out string loadedPath)
    {
        texture = null;
        loadedPath = null;

        string[] candidates = MoonGeographicMapPaths.ResolveLoadCandidatePaths();
        string preferredName = MoonGeographicMapPaths.ActivePngFileName;

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];

            if (!TryLoadGeographicPngAtPath(
                candidate,
                out texture,
                MoonGeographicMapPaths.ActiveMaxLoadWidth
            ))
            {
                continue;
            }

            loadedPath = candidate;

            if (!string.Equals(
                Path.GetFileName(candidate),
                preferredName,
                System.StringComparison.OrdinalIgnoreCase
            ))
            {
                Debug.LogWarning(
                    "PlanetTerrainTextureResources: " + preferredName +
                    " missing — using fallback " + Path.GetFileName(candidate) + "."
                );
            }

            return true;
        }

        return false;
    }

    static void EnsureEarthColorLoaded()
    {
        if (TryGetEarthColorSourceOnDisk(out string sourcePath, out long sourceTimestampUtcTicks))
        {
            if (
                IsAlive(earthColorMap) &&
                (
                    earthColorSourceTimestampUtcTicks != sourceTimestampUtcTicks ||
                    !string.Equals(earthColorSourcePath, sourcePath, System.StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                Debug.Log(
                    "PlanetTerrainTextureResources: earth_geographic changed on disk — reloading color map."
                );
                InvalidateEarthColorMap();
            }
        }

        if (IsAlive(earthColorMap))
        {
            return;
        }

        earthColorMap = null;
        earthColorUsesCanonicalGeographicUv = false;
        earthColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
        earthColorSourceTimestampUtcTicks = 0;
        earthColorSourcePath = null;

        if (TryLoadNasaGeographicColorMap(out earthColorMap, out string loadedPath))
        {
            earthColorUsesCanonicalGeographicUv = true;
            earthColorLayoutPolicy = PlanetMapLayoutPolicy.NasaGeographicPng;
            if (TryGetEarthColorSourceOnDisk(out sourcePath, out sourceTimestampUtcTicks))
            {
                earthColorSourcePath = sourcePath;
                earthColorSourceTimestampUtcTicks = sourceTimestampUtcTicks;
            }
            else
            {
                earthColorSourcePath = loadedPath;
            }

            ValidateWorldMapOrientation();
            ValidateEarthColorOrientation();
            ApplyColorLayoutPolicyFromLoadedTexture();
            PlanetThreePointsOfTruthDiagnostic.RunFixedProbesOnce();
            MapModeEarthResources.Invalidate();
            RefreshActiveEarthColorShaderProperties();
            Debug.Log(
                "PlanetTerrainTextureResources: earth_geographic loaded; policy=" +
                earthColorLayoutPolicy + " path=" + earthColorSourcePath +
                " quality=" + (DevSettings.EarthGeographicMapHigh8K ? "8K" : "2K") +
                " (CPU+GPU via PlanetMapLayoutPolicy)."
            );
            return;
        }

        if (PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline)
        {
            Debug.Log(
                "PlanetTerrainTextureResources: GEBCO-only — skipping RSS EarthColor.dds fallback. " +
                "Map mode uses earth_geographic.png; flight chunks use LocalChunks + GEBCO heights."
            );
            return;
        }

        // RSS fallback: row-flipped + column-remapped at load; mesh still uses canonical UV
        // so macro colors may be wrong until earth_geographic.png is present.
        if (PlanetSurfaceMaps.TryResolveEarthColorPath(out string path))
        {
            earthColorMap = LoadRuntimeTexture(
                path,
                DdsTextureLoader.LoadWorldColorMap,
                "EarthColor"
            );
            earthColorUsesCanonicalGeographicUv = false;
        earthColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
            earthColorSourcePath = path;
            ValidateWorldMapOrientation();
            ValidateEarthColorOrientation();
            ApplyColorLayoutPolicyFromLoadedTexture();
            PlanetThreePointsOfTruthDiagnostic.RunFixedProbesOnce();
            MapModeEarthResources.Invalidate();
            RefreshActiveEarthColorShaderProperties();
        }
    }

    static bool TryLoadNasaGeographicColorMap(out Texture2D texture, out string loadedPath)
    {
        texture = null;
        loadedPath = null;

        string activePath = EarthGeographicMapPaths.ActivePngPath;

        if (TryLoadGeographicPngAtPath(
            activePath,
            out texture,
            EarthGeographicMapPaths.ActiveMaxLoadWidth
        ))
        {
            loadedPath = activePath;
            return true;
        }

        string fallbackPath = EarthGeographicMapPaths.UseHighResolutionMap ?
            EarthGeographicMapPaths.StandardPngPath :
            EarthGeographicMapPaths.HighPngPath;

        if (
            TryLoadGeographicPngAtPath(
                fallbackPath,
                out texture,
                EarthGeographicMapPaths.ActiveMaxLoadWidth
            )
        )
        {
            Debug.LogWarning(
                "PlanetTerrainTextureResources: " + EarthGeographicMapPaths.ActivePngFileName +
                " missing — using fallback " + Path.GetFileName(fallbackPath) + "."
            );
            loadedPath = fallbackPath;
            return true;
        }

        return false;
    }

    static bool TryLoadGeographicPngAtPath(
        string pngPath,
        out Texture2D texture,
        int maxWidth
    )
    {
        texture = null;

        if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
        {
            return false;
        }

        byte[] bytes = File.ReadAllBytes(pngPath);
        Texture2D loaded = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
            bytes,
            "EarthColorNasaGeographic"
        );

        if (loaded == null)
        {
            return false;
        }

        if (maxWidth > 0 && loaded.width > maxWidth)
        {
            loaded = DownscaleGeographicTexture(loaded, maxWidth);
        }

        PlanetParallaxTextures.ConfigureTextureSampling(loaded, clampWrap: true);
        texture = loaded;
        runtimeTextures.Add(texture);
        return true;
    }

    static Texture2D DownscaleGeographicTexture(Texture2D source, int maxWidth)
    {
        int newHeight = Mathf.Max(
            1,
            Mathf.RoundToInt(source.height * (maxWidth / (float)source.width))
        );
        var scaled = new Texture2D(maxWidth, newHeight, TextureFormat.RGB24, false)
        {
            name = "EarthColorNasaGeographicScaled",
        };
        RenderTexture renderTarget = RenderTexture.GetTemporary(
            maxWidth,
            newHeight,
            0,
            RenderTextureFormat.ARGB32
        );
        Graphics.Blit(source, renderTarget);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTarget;
        scaled.ReadPixels(new Rect(0, 0, maxWidth, newHeight), 0, 0);
        scaled.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTarget);
        Object.Destroy(source);
        return scaled;
    }

    static bool TryGetEarthColorSourceOnDisk(out string sourcePath, out long timestampUtcTicks)
    {
        sourcePath = null;
        timestampUtcTicks = 0;
        string[] candidates =
        {
            EarthGeographicMapPaths.ActivePngPath,
            EarthGeographicMapPaths.StandardPngPath,
            EarthGeographicMapPaths.HighPngPath,
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!File.Exists(candidates[i]))
            {
                continue;
            }

            sourcePath = candidates[i];
            timestampUtcTicks = File.GetLastWriteTimeUtc(candidates[i]).Ticks;
            return true;
        }

        return false;
    }

    static void InvalidateEarthColorMap()
    {
        if (IsRuntimeOwned(earthColorMap))
        {
            Object.Destroy(earthColorMap);
            runtimeTextures.Remove(earthColorMap);
        }

        earthColorMap = null;
        earthColorUsesCanonicalGeographicUv = false;
        earthColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
        earthColorSourceTimestampUtcTicks = 0;
        earthColorSourcePath = null;
        MapModeEarthResources.Invalidate();
    }

    static void InvalidateMoonColorMap()
    {
        if (IsRuntimeOwned(moonColorMap))
        {
            Object.Destroy(moonColorMap);
            runtimeTextures.Remove(moonColorMap);
        }

        moonColorMap = null;
        moonColorUsesCanonicalGeographicUv = false;
        moonColorLayoutPolicy = PlanetMapLayoutPolicy.CleanCanon;
        moonColorSourcePath = null;
    }

    static void EnsureEarthNormalLoaded()
    {
        if (IsAlive(earthNormalMap))
        {
            return;
        }

        earthNormalMap = null;

        // See EnsureEarthColorLoaded: same south-pole-at-row-0 flip is required, which the
        // Editor AssetDatabase import path can't know to apply.
        if (PlanetSurfaceMaps.TryResolveEarthNormalPath(out string path))
        {
            earthNormalMap = LoadRuntimeTexture(
                path,
                DdsTextureLoader.LoadWorldNormalMap,
                "Earth_NRM"
            );
        }
    }

    static void EnsureEarthHeightLoaded()
    {
        if (IsUsableHeightMap(earthHeightMap))
        {
            return;
        }

        earthHeightMap = null;

        // Always load through DdsTextureLoader so row-0-south-pole RSS heightmaps are
        // flipped to the north-pole-at-row-0 convention every lat/long sampler expects.
        // The Editor AssetDatabase path skips that flip and silently misplaces coastlines.
        if (PlanetSurfaceMaps.TryResolveEarthHeightPath(out string path))
        {
            try
            {
                earthHeightMap = DdsTextureLoader.LoadUInt16GrayscaleHeightMap(path);
                if (earthHeightMap != null)
                {
                    runtimeTextures.Add(earthHeightMap);
                    ValidateWorldMapOrientation();
                    return;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    "PlanetTerrainTextureResources: failed to load EarthHeight (" +
                    exception.Message +
                    ")."
                );
            }
        }

        if (RssTerrainPaths.TryResolveTexturePath(
                RssTerrainPaths.DisplacementFileName,
                out string displacementPath
            ))
        {
            try
            {
                earthHeightMap = DdsTextureLoader.LoadUInt16GrayscaleHeightMap(displacementPath);
                if (earthHeightMap != null)
                {
                    runtimeTextures.Add(earthHeightMap);
                    Debug.Log(
                        "PlanetTerrainTextureResources: using Earth_Displacement.dds for terrain height from " +
                        displacementPath
                    );
                    ValidateWorldMapOrientation();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    "PlanetTerrainTextureResources: Earth_Displacement.dds is not R16 height (" +
                    exception.Message +
                    "). Mesh will stay smooth until EarthHeight.dds is available."
                );
            }
        }
    }

    static void EnsureTerrainSetLoaded()
    {
        if (IsTerrainSetValid())
        {
            return;
        }

        if (TryRestoreCachedTerrainSet())
        {
            return;
        }

        terrainSet = null;
        terrainSetLodBodyName = null;
        RssTerrainSplatPack pack = RssTerrainSplatPack.ForLodBody();

        if (RssEarthTerrainLoader.TryLoadTerrainSet(pack, runtimeTextures, out PlanetParallaxTerrainSet rssSet))
        {
            terrainSet = rssSet;
            terrainSetLodBodyName = CelestialTerrainRuntime.LodBodyName;
            StashTerrainSetForLodBody();
            return;
        }

#if UNITY_EDITOR
        if (TryLoadTerrainSetFromAssets(pack, out PlanetParallaxTerrainSet assetTerrainSet))
        {
            terrainSet = assetTerrainSet;
            terrainSetLodBodyName = CelestialTerrainRuntime.LodBodyName;
            StashTerrainSetForLodBody();
            Debug.Log(
                "PlanetTerrainTextureResources: RSS-Terrain " +
                pack.ContentBodyFolderName +
                " detail set loaded from Unity assets."
            );
            return;
        }
#endif

        terrainSet = new PlanetParallaxTerrainSet();
        PlanetParallaxTextures.FillMissingFromDisk(
            terrainSet,
            runtimeTextures,
            pack
        );
        terrainSetLodBodyName = CelestialTerrainRuntime.LodBodyName;

        if (!IsTerrainSetValid())
        {
            terrainSetLodBodyName = null;
            LogMissingTerrainSlots(terrainSet, pack);
            Debug.LogError(
                "PlanetTerrainTextureResources: failed to load RSS-Terrain " +
                pack.ContentBodyFolderName +
                " textures. Place " +
                pack.ContentBodyFolderName +
                "_*.dds in " +
                pack.ContentAssetFolder +
                " (preferred) or " +
                pack.ResolveLegacyPluginDataDirectory()
            );
        }
        else
        {
            StashTerrainSetForLodBody();
            Debug.Log(
                "PlanetTerrainTextureResources: RSS-Terrain " +
                pack.ContentBodyFolderName +
                " detail set loaded via fallback disk scan."
            );
        }
    }

    static void StashTerrainSetForLodBody()
    {
        if (terrainSet == null || string.IsNullOrEmpty(terrainSetLodBodyName))
        {
            return;
        }

        if (terrainSetLodBodyName == CelestialTerrainRuntime.MoonBodyName)
        {
            cachedMoonTerrainSet = terrainSet;
            return;
        }

        cachedEarthTerrainSet = terrainSet;
    }

    static bool TryRestoreCachedTerrainSet()
    {
        PlanetParallaxTerrainSet cached = CelestialTerrainRuntime.IsMoon ?
            cachedMoonTerrainSet :
            cachedEarthTerrainSet;

        if (cached == null)
        {
            return false;
        }

        terrainSet = cached;
        terrainSetLodBodyName = CelestialTerrainRuntime.LodBodyName;
        return IsTerrainSetValid();
    }

#if UNITY_EDITOR
    static bool TryLoadTerrainSetFromAssets(
        RssTerrainSplatPack pack,
        out PlanetParallaxTerrainSet assetTerrainSet
    )
    {
        assetTerrainSet = new PlanetParallaxTerrainSet
        {
            InfluenceMap = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.InfluenceFileName)
            ),
            LowAlbedo = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.LowColorFileName)
            ),
            LowNormal = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.LowNormalFileName)
            ),
            MidAlbedo = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.MidColorFileName)
            ),
            MidNormal = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.MidNormalFileName)
            ),
            HighAlbedo = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.HighColorFileName)
            ),
            HighNormal = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.HighNormalFileName)
            ),
            SteepAlbedo = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.SteepColorFileName)
            ),
            SteepNormal = LoadAssetTexture(
                RssTerrainPaths.ResolveAssetTexturePath(pack, pack.SteepNormalFileName)
            ),
        };

        return IsAlive(assetTerrainSet.MidAlbedo) &&
            assetTerrainSet.HasAnyAlbedo &&
            (
                pack.ContentBodyFolderName != RssTerrainSplatPack.Earth.ContentBodyFolderName ||
                (assetTerrainSet.HasInfluenceMap && IsAlive(assetTerrainSet.InfluenceMap))
            );
    }
#endif

    static void DestroyRuntimeOwnedTextures()
    {
        for (int i = runtimeTextures.Count - 1; i >= 0; i--)
        {
            Texture2D texture = runtimeTextures[i];

            if (texture != null)
            {
                Object.Destroy(texture);
            }
        }

        runtimeTextures.Clear();

        if (IsRuntimeOwned(earthColorMap))
        {
            earthColorMap = null;
        }

        if (IsRuntimeOwned(distantColorMap2k))
        {
            distantColorMap2k = null;
        }

        if (IsRuntimeOwned(earthNormalMap))
        {
            earthNormalMap = null;
        }

        if (IsRuntimeOwned(earthHeightMap))
        {
            earthHeightMap = null;
        }

        if (!IsAlive(moonColorMap))
        {
            moonColorMap = null;
            moonColorSourcePath = null;
            moonColorUsesCanonicalGeographicUv = false;
        }
    }

    static bool IsRuntimeOwned(Texture2D texture)
    {
        return texture != null && runtimeTextures.Contains(texture);
    }

    public static void InvalidateStaleResources()
    {
        if (!IsAlive(earthColorMap))
        {
            earthColorMap = null;
        }

        if (!IsAlive(moonColorMap))
        {
            moonColorMap = null;
        }

        if (!IsAlive(earthNormalMap))
        {
            earthNormalMap = null;
        }

        if (!IsUsableHeightMap(earthHeightMap))
        {
            earthHeightMap = null;
        }

        if (!IsTerrainSetValid())
        {
            terrainSet = null;
            terrainSetLodBodyName = null;
        }
    }

    public static bool HasCompleteTerrainDetailSet()
    {
        return IsTerrainSetValid();
    }

#if UNITY_EDITOR
    static Texture2D LoadAssetTexture(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }
#endif

    static Texture2D LoadRuntimeTexture(
        string path,
        System.Func<string, Texture2D> loader,
        string label
    )
    {
        try
        {
            Texture2D texture = loader(path);

            if (texture != null)
            {
                runtimeTextures.Add(texture);
            }

            return texture;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "PlanetTerrainTextureResources: failed to load " +
                label +
                " (" +
                exception.Message +
                ")."
            );
            return null;
        }
    }

    static bool IsTerrainSetValid()
    {
        if (terrainSet == null ||
            !IsAlive(terrainSet.MidAlbedo) ||
            !terrainSet.HasAnyAlbedo)
        {
            return false;
        }

        if (!string.Equals(
            terrainSetLodBodyName,
            CelestialTerrainRuntime.LodBodyName,
            System.StringComparison.Ordinal
        ))
        {
            return false;
        }

        if (!CelestialTerrainRuntime.IsMoon &&
            (!terrainSet.HasInfluenceMap || !IsAlive(terrainSet.InfluenceMap)))
        {
            return false;
        }

        return true;
    }

    static void LogMissingTerrainSlots(PlanetParallaxTerrainSet set, RssTerrainSplatPack pack)
    {
        if (set == null)
        {
            Debug.LogWarning("PlanetTerrainTextureResources: terrain set is null.");
            return;
        }

        Debug.LogWarning(
            "PlanetTerrainTextureResources: missing " +
            pack.ContentBodyFolderName +
            " terrain slots — " +
            "influence=" + IsAlive(set.InfluenceMap) +
            " low=" + IsAlive(set.LowAlbedo) +
            " mid=" + IsAlive(set.MidAlbedo) +
            " high=" + IsAlive(set.HighAlbedo) +
            " steep=" + IsAlive(set.SteepAlbedo) +
            ". Path: " + pack.ResolveContentAbsoluteDirectory()
        );
    }

    static bool IsAlive(Texture2D texture)
    {
        return texture;
    }

    static bool IsUsableHeightMap(Texture2D texture)
    {
        return texture != null &&
            texture.format == TextureFormat.R16 &&
            texture.isReadable;
    }

    static void ValidateWorldMapOrientation()
    {
        if (PlanetElevationCatalog.IsLoaded)
        {
            double capeHeight = PlanetFlightElevationSampling.SampleRealElevationMeters(28.57, -80.65);
            double tibetHeight = PlanetFlightElevationSampling.SampleRealElevationMeters(31.48, 91.10);

            if (capeHeight < -200f || capeHeight > 500f || tibetHeight < 500f || tibetHeight > 12000f)
            {
                Debug.LogWarning(
                    "PlanetTerrainTextureResources: baked elevation sanity check " +
                    "(Cape=" + capeHeight.ToString("F0") + "m, Tibet=" +
                    tibetHeight.ToString("F0") + "m)."
                );
            }

            return;
        }

        if (!IsUsableHeightMap(earthHeightMap))
        {
            return;
        }

        float legacyCapeHeight = SampleHeightMetersAtSite(28.57, -80.65);
        float legacyTibetHeight = SampleHeightMetersAtSite(31.48, 91.10);

        if (legacyCapeHeight < -200f || legacyCapeHeight > 200f || legacyTibetHeight < 200f || legacyTibetHeight > 6000f)
        {
            Debug.LogError(
                "PlanetTerrainTextureResources: world-map orientation looks wrong " +
                "(Cape=" + legacyCapeHeight.ToString("F0") + "m, Tibet=" +
                legacyTibetHeight.ToString("F0") +
                "m). Stop Play Mode and restart — stale DDS cache or double RSS remap."
            );
        }
    }

    public static bool TrySampleLegacyHeightMetersAtSite(
        double latitudeDegrees,
        double longitudeDegrees,
        out float elevationMeters
    )
    {
        elevationMeters = 0f;

        if (earthHeightMap == null)
        {
            return false;
        }

        elevationMeters = SampleLegacyHeightMetersAtSite(latitudeDegrees, longitudeDegrees);
        return true;
    }

    static float SampleLegacyHeightMetersAtSite(double latitudeDegrees, double longitudeDegrees)
    {
        double latRadians = latitudeDegrees * System.Math.PI / 180.0;
        double lonRadians = longitudeDegrees * System.Math.PI / 180.0;
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRadians, lonRadians),
            out double geographicU,
            out double geographicV
        );
        Color sample = earthHeightMap.GetPixelBilinear(
            (float)geographicU,
            (float)PlanetGeographicCoords.MapGeographicVToRssEarthTextureV(geographicV)
        );
        return (float)PlanetSurfaceMaps.SampleEarthHeightMeters(sample.r);
    }

    static void ApplyColorLayoutPolicyFromLoadedTexture()
    {
        if (earthColorMap == null || !earthColorMap.isReadable)
        {
            return;
        }

        if (earthColorUsesCanonicalGeographicUv)
        {
            earthColorLayoutPolicy = PlanetMapLayoutPolicy.NasaGeographicPng;

            if (
                PlanetAssetProber.InferColorRequiresHorizontalFlipGebco(
                    SampleColorBytesFromLoadedMap,
                    PlanetAssetProber.StandardSites
                )
            )
            {
                earthColorLayoutPolicy = new PlanetMapLayoutPolicy
                {
                    ColorMapRequiresHorizontalFlip = true,
                    ColorMapRequiresVerticalFlip = false,
                    ElevationMapRequiresHorizontalFlip = false,
                };
                Debug.LogWarning(
                    "PlanetTerrainTextureResources: GEBCO tie-breaker requests colorFlipU on " +
                    "earth_geographic.png — run Space Program / Fix Earth Color Map Longitude (.png) " +
                    "in Editor instead of relying on runtime flip."
                );
                RefreshActiveEarthColorShaderProperties();
                MapModeEarthResources.Invalidate();
            }

            return;
        }

        PlanetAssetProber.ColorSampleDelegate sampler = SampleColorBytesFromLoadedMap;
        PlanetAssetProber.CombinedProbeReport report = PlanetAssetProber.ProbeStandardAssets(
            PlanetDataPaths.ElevationRawPath,
            earthColorSourcePath,
            sampler
        );

        PlanetMapLayoutPolicy probed = report.RecommendedPolicy;
        bool flipU = probed.ColorMapRequiresHorizontalFlip;
        bool flipV = probed.ColorMapRequiresVerticalFlip;

        if (
            flipU == earthColorLayoutPolicy.ColorMapRequiresHorizontalFlip &&
            flipV == earthColorLayoutPolicy.ColorMapRequiresVerticalFlip
        )
        {
            return;
        }

        earthColorLayoutPolicy = new PlanetMapLayoutPolicy
        {
            ColorMapRequiresHorizontalFlip = flipU,
            ColorMapRequiresVerticalFlip = flipV,
            ElevationMapRequiresHorizontalFlip = false,
        };

        Debug.Log(
            "PlanetTerrainTextureResources: applied color layout from GEBCO/asset probe → " +
            earthColorLayoutPolicy + ". CPU and GPU use the same flip flags."
        );
        RefreshActiveEarthColorShaderProperties();
        MapModeEarthResources.Invalidate();
    }

    static bool SampleColorBytesFromLoadedMap(
        float geographicU,
        float geographicV,
        out byte r,
        out byte g,
        out byte b
    )
    {
        r = 0;
        g = 0;
        b = 0;

        if (earthColorMap == null)
        {
            return false;
        }

        Color sample = earthColorMap.GetPixelBilinear(geographicU, geographicV);
        r = (byte)Mathf.RoundToInt(sample.r * 255f);
        g = (byte)Mathf.RoundToInt(sample.g * 255f);
        b = (byte)Mathf.RoundToInt(sample.b * 255f);
        return true;
    }

    static void ValidateEarthColorOrientation()
    {
        if (earthColorMap == null || !earthColorMap.isReadable)
        {
            return;
        }

        ValidateEarthColorAgainstGebco("Cape", 28.57, -80.65);
        ValidateEarthColorAgainstGebco("Morocco", 31.63, -7.99);
        ValidateEarthColorAgainstGebco("Tibet", 31.48, 91.10);
        ValidateEarthColorAgainstGebco("Pacific", 0.0, -160.0, expectOcean: true);
    }

    static void ValidateEarthColorAgainstGebco(
        string label,
        double latitudeDegrees,
        double longitudeDegrees,
        bool expectOcean = false
    )
    {
        double latRadians = latitudeDegrees * System.Math.PI / 180.0;
        double lonRadians = longitudeDegrees * System.Math.PI / 180.0;
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRadians, lonRadians),
            out double geographicU,
            out double geographicV
        );
        Color geoSample = earthColorMap.GetPixelBilinear(
            MapGeographicUToEarthColorTextureU((float)geographicU),
            SampleEarthColorTextureV(geographicV)
        );

        bool colorLooksLikeOcean = geoSample.b > geoSample.g + 0.04f && geoSample.b > 0.12f;
        bool colorLooksLikeLand = geoSample.g > 0.12f && geoSample.b < geoSample.g * 0.85f;

        if (PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded)
        {
            double gebcoMeters = PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(
                latitudeDegrees,
                longitudeDegrees
            );
            bool gebcoLand = gebcoMeters > 0.0;

            if (gebcoLand && colorLooksLikeOcean)
            {
                Debug.LogWarning(
                    "PlanetTerrainTextureResources: EarthColor disagrees with GEBCO at " +
                    label + " (GEBCO=" + gebcoMeters.ToString("F0") + "m, rgb=" +
                    geoSample.r.ToString("F2") + "," + geoSample.g.ToString("F2") + "," +
                    geoSample.b.ToString("F2") + "). Re-bake earth_geographic.png or restart Play."
                );
            }
            else if (!gebcoLand && colorLooksLikeLand)
            {
                Debug.LogWarning(
                    "PlanetTerrainTextureResources: EarthColor disagrees with GEBCO at " +
                    label + " (GEBCO=" + gebcoMeters.ToString("F0") + "m, rgb=" +
                    geoSample.r.ToString("F2") + "," + geoSample.g.ToString("F2") + "," +
                    geoSample.b.ToString("F2") + "). Re-bake earth_geographic.png or restart Play."
                );
            }

            return;
        }

        if (expectOcean && !colorLooksLikeOcean)
        {
            Debug.LogWarning(
                "PlanetTerrainTextureResources: EarthColor at " + label +
                " geoUv=(" + geographicU.ToString("F3") + ", " + geographicV.ToString("F3") + ")" +
                " — expected open ocean."
            );
            return;
        }

        if (!expectOcean && colorLooksLikeOcean)
        {
            Debug.LogWarning(
                "PlanetTerrainTextureResources: EarthColor at " + label +
                " geoUv=(" + geographicU.ToString("F3") + ", " + geographicV.ToString("F3") + ")" +
                " — expected land."
            );
        }
    }

    static float SampleHeightMetersAtSite(double latitudeDegrees, double longitudeDegrees)
    {
        if (PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded)
        {
            return (float)PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(
                latitudeDegrees,
                longitudeDegrees
            );
        }

        double latRadians = latitudeDegrees * System.Math.PI / 180.0;
        double lonRadians = longitudeDegrees * System.Math.PI / 180.0;
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRadians, lonRadians),
            out double geographicU,
            out double geographicV
        );
        Color sample = earthHeightMap.GetPixelBilinear(
            (float)geographicU,
            (float)PlanetGeographicCoords.MapGeographicVToRssEarthTextureV(geographicV)
        );
        return (float)PlanetSurfaceMaps.SampleEarthHeightMeters(sample.r);
    }

    public static Color SampleEarthColorAtGeographicUv(double geographicU, double geographicV)
    {
        if (earthColorMap == null)
        {
            return Color.black;
        }

        return earthColorMap.GetPixelBilinear(
            MapGeographicUToEarthColorTextureU((float)geographicU),
            SampleEarthColorTextureV(geographicV)
        );
    }

    public static float MapGeographicUToEarthColorTextureU(float geographicU)
    {
        return earthColorLayoutPolicy.MapColorGeographicU(geographicU);
    }

    public static float MapGeographicVToEarthColorTextureV(float geographicV)
    {
        return earthColorLayoutPolicy.MapColorGeographicV(geographicV);
    }

    public static void ApplyEarthColorShaderProperties(Material material)
    {
        ApplyColorMapFlipProperties(material, earthColorLayoutPolicy);
    }

    public static void ApplyMoonColorShaderProperties(Material material)
    {
        ApplyColorMapFlipProperties(material, moonColorLayoutPolicy);
    }

    static void ApplyColorMapFlipProperties(Material material, PlanetMapLayoutPolicy layoutPolicy)
    {
        if (material == null)
        {
            return;
        }

        float flipLongitude = layoutPolicy.ColorMapRequiresHorizontalFlip ? 1f : 0f;
        float flipLatitude = layoutPolicy.ColorMapRequiresVerticalFlip ? 1f : 0f;

        if (material.HasProperty("_ColorMapFlipLongitude"))
        {
            material.SetFloat("_ColorMapFlipLongitude", flipLongitude);
        }

        if (material.HasProperty("_ColorMapFlipLatitude"))
        {
            material.SetFloat("_ColorMapFlipLatitude", flipLatitude);
        }
    }

    public static void RefreshActiveEarthColorShaderProperties()
    {
        FlightPlanetSphere.RefreshActiveEarthColorShaderProperties();
    }

    static float SampleEarthColorTextureV(double geographicV)
    {
        return MapGeographicVToEarthColorTextureV((float)geographicV);
    }
}
