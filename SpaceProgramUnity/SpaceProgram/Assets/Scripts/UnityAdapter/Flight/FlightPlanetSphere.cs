using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Cube-sphere planet with per-face spherical quadtree LOD.
/// Flight LOD anchor is world origin (vessel frame zero). Split/merge uses screen-space
/// error on a disk around the ship; the camera only frustum-culls drawing.
/// </summary>
[DisallowMultipleComponent]
    [DefaultExecutionOrder(250)]
public sealed class FlightPlanetSphere : MonoBehaviour
{
    // Flatten only the pad yard. Wider radii hid real dunes and were easy to confuse
    // with pad visibility (now 100 m in LaunchPadConstants).
    public const float DefaultFlattenRadiusMeters = 100f;
    public const float DefaultHardFlattenRadiusMeters = 40f;
    public const int DefaultChunkVertexResolution = 24;
    public const float DefaultSplitDistanceMeters = 4000f;
    public const int DefaultMaxLodLevel = 15;
    public const int DefaultMinColliderLodLevel = 14;
    /// <summary>
    /// Ceiling on allocated chunk objects. The view cone plus the horizon already bound the
    /// working set, so this only exists to stop a runaway tree; keeping it near a hundred
    /// starved the LOD fan long before the visible area was covered.
    /// </summary>
    public const int DefaultMaxActiveChunks = 640;
    public const float DefaultLodDebugLineWidth = 1.5f;
    public const float LodRefreshIntervalSeconds = 0.05f;
    public const float DefaultLodUpdateIntervalSeconds = 0.18f;
    /// <summary>Skip the LOD walk when the ship has moved less than this since the last tick.</summary>
    public const float IdleLodShipMoveMeters = 2.5f;
    public const float IdleLodAltitudeDeltaMeters = 3f;
    /// <summary>
    /// Play start used to dump 12–24 splits every frame until the pad was 64 m, plus a full-disk
    /// tick the moment that finished. Keep the first seconds on a small budget.
    /// </summary>
    public const int BootstrapMaxSplitsPerFrame = 4;
    public const int BootstrapMaxMeshesPerFrame = 4;
    public const int WarmupMaxSplitsPerFrame = 4;
    public const int WarmupMaxMeshesPerFrame = 4;
    public const float LodWarmupSeconds = 4f;
    public const int BootstrapMaxFrames = 180;
    /// <summary>
    /// After warmup, never dump more than this per tick even if Load Speed is 12.
    /// Load Speed still feeds the async queue; hitch budget stays small for 1% lows.
    /// </summary>
    public const int HitchMaxSplitsPerFrame = 4;
    public const int HitchMaxMeshesPerFrame = 2;
    /// <summary>
    /// Restricted-quadtree catch-up used to split every in-disk neighbor in one LateUpdate
    /// until the 640 cap (takeoff hitch, pad stuck at BUDGET). Cap it per tick.
    /// </summary>
    public const int HitchMaxNeighborSplitsPerFrame = 2;
    /// <summary>
    /// After pad warmup, stop the LOD walk once this much real time has elapsed.
    /// Split/merge rules stay the same; leftover work continues next tick.
    /// Bootstrap/warmup keep the count budget only so pad rings still fill.
    /// </summary>
    public const float HitchMaxLodTickMilliseconds = 6f;
    public const float DefaultViewerLookAheadSeconds = 2.5f;
    public const float MergeDistanceMultiplier = 1.5f;
    public const int DefaultMaxLodSplitsPerFrame = 8;
    public const int DefaultMaxMeshBuildsPerFrame = 4;
    public const float DefaultColliderMaxSurfaceDistanceMeters = 400f;
    public const float DefaultMinChunkSpanMeters = 24f;
    public const float DefaultLaunchSiteMaxChunkSpanMeters = 64f;

    [SerializeField] float planetRadius = WorldVisualBridge.FlightPlanetRadiusUnity;
    [SerializeField] int chunkVertexResolution = DefaultChunkVertexResolution;
    [SerializeField] float splitDistanceMeters = DefaultSplitDistanceMeters;
    [SerializeField] int maxLodLevel = DefaultMaxLodLevel;
    [SerializeField] int minColliderLodLevel = DefaultMinColliderLodLevel;
    [SerializeField] float flattenRadiusMeters = DefaultFlattenRadiusMeters;
    [SerializeField] float hardFlattenRadiusMeters = DefaultHardFlattenRadiusMeters;
    [SerializeField] float lodDebugLineWidth = DefaultLodDebugLineWidth;
    [SerializeField] int maxLodSplitsPerFrame = DefaultMaxLodSplitsPerFrame;
    [SerializeField] int maxMeshBuildsPerFrame = DefaultMaxMeshBuildsPerFrame;
    [SerializeField] float colliderMaxSurfaceDistanceMeters = DefaultColliderMaxSurfaceDistanceMeters;
    [SerializeField] float viewerLookAheadSeconds = DefaultViewerLookAheadSeconds;
    [SerializeField] float lodUpdateIntervalSeconds = DefaultLodUpdateIntervalSeconds;
    [SerializeField] bool showLodDebug;

    [Header("RSS Scaled Space (Assets/Content/Environment/RSS-Textures)")]
    [SerializeField] Texture2D earthColorMap;
    [SerializeField] Texture2D earthNormalMap;

    [Header("RSS Local Height (PluginData/EarthHeight.dds)")]
    [SerializeField] Texture2D earthHeightMap;

    [Header("Terrain Detail (RSS-Terrain Earth, fallback Kerbin Parallax)")]
    [SerializeField] Texture2D splatInfluenceMap;
    [SerializeField] Texture2D lowAlbedoMap;
    [SerializeField] Texture2D lowNormalMap;
    [SerializeField] Texture2D midAlbedoMap;
    [SerializeField] Texture2D midNormalMap;
    [SerializeField] Texture2D highAlbedoMap;
    [SerializeField] Texture2D highNormalMap;
    [SerializeField] Texture2D steepAlbedoMap;
    [SerializeField] Texture2D steepNormalMap;

    static FlightPlanetSphere activeInstance;

    readonly List<Texture2D> runtimeTextures = new List<Texture2D>();

    Transform chunksRoot;
    Transform mapSphereRoot;
    Transform oceanSphereRoot;
    FlightPlanetCloudLayers cloudLayers;
    FlightPlanetAtmosphereSphere atmosphereSphere;
    FlightPlanetMapSphere mapSphere;
    Material scaledSpaceMaterial;
    Material localChunksMaterial;
    PlanetQuadtreeLod quadtree;
    MeshFilter legacyMeshFilter;
    MeshRenderer legacyMeshRenderer;
    float nextLodDebugRefreshTime;
    float nextLodUpdateTime;
    bool lodUpdatesEnabled = true;
    bool pendingViewerLodBootstrap;
    int viewerLodBootstrapFramesRemaining;
    bool mapModeActive;
    bool chunksWereVisibleLastFrame;
    PlanetLodAltitudePolicy.AltitudeShellState altitudeShellState;
    PlanetDistantVisualPolicy.GlobeQuality appliedGlobeQuality;
    int appliedGlobeSubdivisions = PlanetDistantVisualPolicy.DefaultShellSubdivisions;
    float viewerAltitudeMeters;
    Vector3 viewerVelocityWorld;
    Vector3 viewerWorldPosition;
    Vector3 lastIdleLodFocusSim;
    float lastIdleLodAltitudeMeters;
    bool hasIdleLodBaseline;
    bool wasSkippingIdleLod;
    float lodWarmupEndTime = -1f;
    SimulationRunner cachedSimulationRunner;

    public float PlanetRadius => planetRadius;

    public static FlightPlanetSphere ActiveInstance => activeInstance;

    public bool HasActiveQuadtree => quadtree != null;

    public float RuntimeLodDistanceFactor
    {
        get => runtimeLodDistanceFactor;
        set
        {
            runtimeLodDistanceFactor = Mathf.Clamp(
                value,
                FlightPlanetTerrainTuning.LodDistanceFactorMin,
                FlightPlanetTerrainTuning.LodDistanceFactorMax
            );
            ApplyRuntimeTuning();
        }
    }

    public int RuntimeLoadSpeedPerFrame
    {
        get => runtimeLoadSpeedPerFrame;
        set
        {
            runtimeLoadSpeedPerFrame = Mathf.Clamp(
                value,
                FlightPlanetTerrainTuning.LoadSpeedMin,
                FlightPlanetTerrainTuning.LoadSpeedMax
            );
            ApplyRuntimeTuning();
        }
    }

    public int RuntimeMaxLodCap
    {
        get => runtimeMaxLodCap;
        set
        {
            runtimeMaxLodCap = Mathf.Clamp(
                value,
                FlightPlanetTerrainTuning.MaxLodMin,
                maxLodLevel
            );
            ApplyRuntimeTuning();
        }
    }

    public float RuntimeViewConeHalfAngleDegrees
    {
        get => runtimeViewConeHalfAngleDegrees;
        set
        {
            runtimeViewConeHalfAngleDegrees = Mathf.Clamp(
                value,
                FlightPlanetTerrainTuning.ViewConeHalfAngleMin,
                FlightPlanetTerrainTuning.ViewConeHalfAngleMax
            );
            ApplyRuntimeTuning();
        }
    }

    public float RuntimeViewConeUnloadHalfAngleDegrees =>
        quadtree != null ?
            quadtree.AsyncLodSettings.HorizonUnloadHalfAngleDegrees :
            runtimeViewConeHalfAngleDegrees +
            PlanetPqsAsyncLodSettings.ViewConeUnloadHysteresisDegrees;

    float runtimeLodDistanceFactor = FlightPlanetTerrainTuning.LodDistanceFactorDefault;
    int runtimeLoadSpeedPerFrame = DefaultMaxLodSplitsPerFrame;
    int runtimeMaxLodCap = DefaultMaxLodLevel;
    float runtimeViewConeHalfAngleDegrees =
        PlanetPqsAsyncLodSettings.DefaultViewConeHalfAngleDegrees;

    public static bool ShowLodDebug => activeInstance != null && activeInstance.showLodDebug;

    public bool IsMapModeActive => mapModeActive;

    public string CaptureLodDiagnosticsReport()
    {
        if (quadtree == null)
        {
            return "LOD DIAG  F8=показать+лог  Shift+F8=скрыть\nquadtree=null";
        }

        return quadtree.CaptureDiagnosticsReport(BuildDiagPhaseLine());
    }

    string BuildDiagPhaseLine()
    {
        GetLodWorkBudgets(out int splitBudget, out int meshBudget);
        string phase;
        if (IsLodBootstrapping())
        {
            phase = "BOOT " + viewerLodBootstrapFramesRemaining + "/" + BootstrapMaxFrames;
        }
        else if (IsLodWarmupActive())
        {
            float left = Mathf.Max(0f, lodWarmupEndTime - Time.unscaledTime);
            phase = "WARM " + left.ToString("F1") + "s";
        }
        else if (ShouldSkipIdleLodTick())
        {
            phase = "IDLE-SKIP";
        }
        else
        {
            phase = "TICK";
        }

        return "LOD DIAG  F8=показать+лог  Shift+F8=скрыть  " + phase +
            "  splitBud=" + splitBudget +
            "  meshBud=" + meshBudget +
            "  skipIdle=" + (ShouldSkipIdleLodTick() ? "1" : "0") +
            "  refine=" + (NeedsViewerLodRefinement() ? "1" : "0");
    }

    public static int GetActiveChunkCount()
    {
        return activeInstance?.quadtree?.GetActiveChunkCount() ?? 0;
    }

    public static bool TryCaptureLodLoadSnapshot(out PlanetLodLoadSnapshot snapshot)
    {
        if (activeInstance?.quadtree == null)
        {
            snapshot = default;
            return false;
        }

        snapshot = activeInstance.quadtree.CaptureLodLoadSnapshot();
        return true;
    }

    /// <summary>
    /// Loading cover: Max LOD under the rocket, pad span fine, bootstrap/warmup done.
    /// Does not wait for in-disk unfinished (horizon keeps splitting).
    /// </summary>
    public static bool TryGetLodLoadingReadiness(
        out bool hasQuadtree,
        out bool hasViewerMesh,
        out int viewerLeafLevel,
        out int activeMaxLodLevel,
        out bool padStillCoarse,
        out bool bootstrapping,
        out bool warmupActive
    )
    {
        if (activeInstance == null)
        {
            hasQuadtree = false;
            hasViewerMesh = false;
            viewerLeafLevel = -1;
            activeMaxLodLevel = 0;
            padStillCoarse = false;
            bootstrapping = false;
            warmupActive = false;
            return false;
        }

        hasQuadtree = activeInstance.quadtree != null;
        hasViewerMesh = hasQuadtree && activeInstance.quadtree.ViewerAreaHasRenderableMesh();
        viewerLeafLevel = hasQuadtree ? activeInstance.quadtree.GetViewerLeafLevel() : -1;
        activeMaxLodLevel = hasQuadtree ? activeInstance.quadtree.GetActiveMaxLodLevel() : 0;
        padStillCoarse = activeInstance.NeedsViewerPadLodBootstrap();
        bootstrapping = activeInstance.IsLodBootstrapping();
        warmupActive = activeInstance.IsLodWarmupActive();
        return true;
    }

    public static void RefreshActiveEarthColorShaderProperties()
    {
        activeInstance?.RefreshEarthColorShaderProperties();
    }

    internal void RefreshEarthColorShaderProperties()
    {
        if (CelestialTerrainRuntime.IsMoon)
        {
            if (localChunksMaterial != null)
            {
                PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(localChunksMaterial);
            }

            if (scaledSpaceMaterial != null)
            {
                PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(scaledSpaceMaterial);
            }

            if (LocalChunksUseCloseupSplat())
            {
                BindLodBodyMacroColorMap();
            }
            else
            {
                BindSandboxChunkAlbedo();
            }

            return;
        }

        if (localChunksMaterial != null)
        {
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(localChunksMaterial);
        }

        if (scaledSpaceMaterial != null)
        {
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(scaledSpaceMaterial);
        }
    }

    public void SetPlanetSpinLongitudeRadians(float spinLongitudeRadians)
    {
        ApplyPlanetSpinLongitudeRadians(spinLongitudeRadians, allowWhileMapMode: false);
    }

    void ApplyPlanetSpinLongitudeRadians(
        float spinLongitudeRadians,
        bool allowWhileMapMode
    )
    {
        if (mapModeActive && !allowWhileMapMode)
        {
            return;
        }

        PlanetVisualSpinProperties.Apply(localChunksMaterial, 0f);

        float shellSpinRadians = ResolveShellShaderSpinRadians(spinLongitudeRadians);
        PlanetVisualSpinProperties.Apply(scaledSpaceMaterial, shellSpinRadians);
        cloudLayers?.SetPlanetSpinLongitudeRadians(shellSpinRadians);
    }

    /// <summary>
    /// Sidereal spin is on the planet transform (canon §3.1). Chunks never sample shader spin.
    /// </summary>
    float ResolveShellShaderSpinRadians(float spinLongitudeRadians)
    {
        return 0f;
    }

    public static FlightPlanetSphere AttachTo(GameObject planetObject)
    {
        FlightPlanetSphere component = planetObject.GetComponent<FlightPlanetSphere>();

        if (component == null)
        {
            component = planetObject.AddComponent<FlightPlanetSphere>();
        }

        component.Build();
        return component;
    }

    public void HibernateForDistantGlobe()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }

        enabled = false;
        SetOwnedLodVisualsActive(false);
        DestroyNamedChild(transform, "PlanetMapSphere");
        mapSphereRoot = null;
        mapSphere = null;
    }

    public void ResumeFromHibernate()
    {
        activeInstance = this;
        enabled = true;
        EnsureComponents();
        DisableLegacyPlanetMesh();
        altitudeShellState = default;
        RefreshOwnedShellsAfterLodSwap();
        SetOwnedLodVisualsActive(true);
        RequestViewerLodBootstrap();
    }

    /// <summary>
    /// After Moon↔Earth SoI: show the PNG disk on the flight camera immediately
    /// (not ScaledSpace) and restore cloud/atmosphere layers.
    /// </summary>
    public void ApplyPresentationAfterSoISwap(bool mapMode)
    {
        if (mapMode)
        {
            if (!mapModeActive)
            {
                SetMapModeActive(true);
            }
            else
            {
                ApplyMapModePresentation(true);
            }

            return;
        }

        if (mapModeActive)
        {
            SetMapModeActive(false);
        }

        UpdateAltitudeShellPresentation();
        ApplyAtmosphereCloudVisuals();
    }

    void RefreshOwnedShellsAfterLodSwap()
    {
        if (CelestialTerrainRuntime.IsMoon)
        {
            DisableCloudLayers();
            DisableAtmosphereSphere();
            return;
        }

        EnsureCloudLayers();
        EnsureAtmosphereSphere();
    }

    void SetOwnedLodVisualsActive(bool visible)
    {
        if (chunksRoot != null)
        {
            chunksRoot.gameObject.SetActive(visible);
        }

        if (mapSphereRoot != null)
        {
            mapSphereRoot.gameObject.SetActive(
                visible && (mapModeActive || altitudeShellState.ShellVisible)
            );
        }

        if (cloudLayers != null)
        {
            cloudLayers.gameObject.SetActive(visible && !CelestialTerrainRuntime.IsMoon);
        }

        if (atmosphereSphere != null)
        {
            atmosphereSphere.gameObject.SetActive(visible && !CelestialTerrainRuntime.IsMoon);
        }
    }

    /// <summary>
    /// Tear down quadtree + atmosphere/clouds so this object can become a distant globe.
    /// </summary>
    public static void StripLodHierarchy(GameObject planetObject)
    {
        if (planetObject == null)
        {
            return;
        }

        FlightPlanetSphere sphere = planetObject.GetComponent<FlightPlanetSphere>();

        if (sphere != null)
        {
            Object.DestroyImmediate(sphere);
        }

        DestroyNamedChild(planetObject.transform, "PlanetChunks");
        DestroyNamedChild(planetObject.transform, "PlanetMapSphere");
        DestroyNamedChild(planetObject.transform, "PlanetClouds");
        DestroyNamedChild(planetObject.transform, "PlanetAtmosphere");
        DestroyNamedChild(planetObject.transform, "PlanetGlobeDepth");
    }

    static void DestroyNamedChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);

        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    public static void SetLodDebugVisible(bool visible)
    {
        if (activeInstance == null)
        {
            return;
        }

        activeInstance.showLodDebug = visible;
        activeInstance.quadtree?.SetLodDebugVisible(visible);

        if (visible)
        {
            activeInstance.nextLodDebugRefreshTime = 0f;
            activeInstance.quadtree?.RefreshDebugOutlines();
        }
    }

    Vector3 viewerVelocitySim;
    float runtimeLookAheadSeconds = DefaultViewerLookAheadSeconds;

    public void SetViewerMotion(
        float altitudeAboveSurfaceMeters,
        Vector3 velocityWorldMetersPerSecond,
        Vector3 viewerWorldPosition,
        Vector3 viewerFocusDirectionSim,
        Vector3 velocitySimMetersPerSecond
    )
    {
        viewerAltitudeMeters = Mathf.Max(0f, altitudeAboveSurfaceMeters);
        viewerVelocityWorld = velocityWorldMetersPerSecond;
        viewerVelocitySim = velocitySimMetersPerSecond;
        this.viewerWorldPosition = viewerWorldPosition;

        if (quadtree != null)
        {
            quadtree.ViewerAltitudeMeters = viewerAltitudeMeters;
            quadtree.ViewerWorldPosition = viewerWorldPosition;
            quadtree.HasViewerWorldAnchor = true;
            quadtree.ViewerVelocityWorld = viewerVelocityWorld;
            quadtree.ViewerVelocitySim = viewerVelocitySim;

            if (viewerFocusDirectionSim.sqrMagnitude > 0.25f)
            {
                quadtree.ViewerFocusDirectionSim = viewerFocusDirectionSim.normalized;
            }

            float speed = velocitySimMetersPerSecond.magnitude;
            runtimeLookAheadSeconds = speed > 80f ?
                Mathf.Clamp(28000f / speed, 2.5f, 12f) :
                viewerLookAheadSeconds;
            quadtree.ViewerLookAheadSeconds = runtimeLookAheadSeconds;
        }

        UpdateOceanVisibility();

        if (!mapModeActive)
        {
            UpdateAltitudeShellPresentation();
        }
    }

    public void SetViewerMotion(
        float altitudeAboveSurfaceMeters,
        Vector3 velocityWorldMetersPerSecond,
        Vector3 viewerWorldPosition,
        Vector3 viewerFocusDirectionSim
    )
    {
        SetViewerMotion(
            altitudeAboveSurfaceMeters,
            velocityWorldMetersPerSecond,
            viewerWorldPosition,
            viewerFocusDirectionSim,
            velocityWorldMetersPerSecond
        );
    }

    public void SetViewerMotion(
        float altitudeAboveSurfaceMeters,
        Vector3 velocityWorldMetersPerSecond,
        Vector3 viewerWorldPosition
    )
    {
        SetViewerMotion(
            altitudeAboveSurfaceMeters,
            velocityWorldMetersPerSecond,
            viewerWorldPosition,
            Vector3.zero,
            viewerVelocitySim.sqrMagnitude > 1e-6f ? viewerVelocitySim : velocityWorldMetersPerSecond
        );
    }

    public void SetViewerAltitude(float altitudeAboveSurfaceMeters)
    {
        SetViewerMotion(altitudeAboveSurfaceMeters, viewerVelocityWorld, viewerWorldPosition);
    }

    public void SetMapModeActive(bool active)
    {
        if (mapModeActive == active)
        {
            return;
        }

        if (active)
        {
            ApplyPlanetSpinLongitudeRadians(0f, allowWhileMapMode: true);
        }

        mapModeActive = active;
        lodUpdatesEnabled = !active;

        if (active)
        {
            EnsureScaledSpaceMaterialTextures();
            EnsureMapSphere();
            RefreshMapSphere();
            SyncMapSphereWorldFrame();
        }
        else
        {
            nextLodDebugRefreshTime = 0f;
            RestoreFlightTerrainAfterMapExit();
        }

        ApplyMapModePresentation(active);
    }

    void RestoreFlightTerrainAfterMapExit()
    {
        if (!Application.isPlaying || quadtree == null)
        {
            return;
        }

        SimulationRunner runner = FindFirstObjectByType<SimulationRunner>();

        if (runner != null)
        {
            SetPlanetSpinLongitudeRadians(runner.GetPlanetVisualSpinLongitudeRadians());
        }

        if (mapSphereRoot != null)
        {
            mapSphereRoot.gameObject.SetActive(false);
        }

        if (chunksRoot != null)
        {
            chunksRoot.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Same first LOD tick as map → flight: pose already applied, then look-ahead + mesh feed.
    /// Play start used to Build() before the planet transform existed, then skip the main
    /// LateUpdate path for 45 frames — one quadrant froze until M / scenario / GFX rebuild.
    /// </summary>
    public void PrimeFlightTerrainLod()
    {
        if (!Application.isPlaying || quadtree == null || mapModeActive)
        {
            return;
        }

        chunksWereVisibleLastFrame = true;
        RequestViewerLodBootstrap();
        SyncViewerLodFocusFromSimulation();
        if (quadtree != null)
        {
            quadtree.SyncInDiskMeshes = true;
        }

        int splitBudget = BootstrapMaxSplitsPerFrame;
        int meshBudget = BootstrapMaxMeshesPerFrame;
        quadtree.RefreshLodLookAhead(splitBudget, meshBudget, allowNeighborSync: true);
        quadtree.DispatchCompletedMeshes();
        quadtree.FeedPendingMeshQueue(meshBudget);
        quadtree.DispatchCompletedMeshes();
        InvalidateLaunchSiteMeshes();
    }

    /// <summary>
    /// Map icosphere UV is object-space (body-fixed Mesh Frame). Keep the sphere
    /// local-identity under the planet. Planet rotation is the Mesh-Frame conjugate
    /// of sim spin so inertial map markers sit on geography — do not force identity.
    /// </summary>
    void SyncMapSphereWorldFrame()
    {
        if (mapSphereRoot == null)
        {
            return;
        }

        mapSphereRoot.localRotation = Quaternion.identity;
    }

    void RefreshMapSphere()
    {
        if (mapSphere == null || scaledSpaceMaterial == null)
        {
            return;
        }

        EnsureScaledSpaceMaterialTextures(mapModeActive);
        int subdivisions = PlanetDistantVisualPolicy.GetShellSubdivisions(
            appliedGlobeQuality,
            mapModeActive
        );

        if (
            mapSphere.HasBuiltMesh &&
            appliedGlobeSubdivisions == subdivisions
        )
        {
            if (mapModeActive)
            {
                FlightRenderLayers.ApplyScaledSpaceLayer(mapSphereRoot.gameObject);
            }
            else if (altitudeShellState.ShellVisible)
            {
                FlightRenderLayers.ApplyLocalPhysicsLayer(mapSphereRoot.gameObject);
            }

            return;
        }

        mapSphere.Build(
            scaledSpaceMaterial,
            planetRadius,
            subdivisions,
            applyScaledSpaceLayer: mapModeActive
        );
        appliedGlobeSubdivisions = subdivisions;

        if (mapSphereRoot != null)
        {
            if (mapModeActive)
            {
                FlightRenderLayers.ApplyScaledSpaceLayer(mapSphereRoot.gameObject);
            }
            else if (altitudeShellState.ShellVisible)
            {
                FlightRenderLayers.ApplyLocalPhysicsLayer(mapSphereRoot.gameObject);
            }
        }
    }

    void EnsureScaledSpaceMaterialTextures(bool force = false)
    {
        if (scaledSpaceMaterial == null || (!mapModeActive && !force))
        {
            return;
        }

        Texture2D mapModeColor = CelestialTerrainRuntime.IsMoon ?
            PlanetTerrainTextureResources.GetMoonColorMap() :
            MapModeEarthResources.GetColorMap();

        if (mapModeColor != null)
        {
            PlanetParallaxTextures.ConfigureTextureSampling(mapModeColor);
            scaledSpaceMaterial.SetTexture("_ColorMap", mapModeColor);
            if (CelestialTerrainRuntime.IsMoon)
            {
                PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(scaledSpaceMaterial);
            }
            else
            {
                PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(scaledSpaceMaterial);
            }
            Debug.Log(
                "FlightPlanetSphere: map mode color from " +
                (CelestialTerrainRuntime.IsMoon ?
                    "moon_geographic" :
                    MapModeEarthResources.SourceLabel) +
                "."
            );
        }
    }

    void ResolveLaunchSiteFlattenRadii(
        out float activeFlattenRadiusMeters,
        out float activeHardFlattenRadiusMeters
    )
    {
        if (!CelestialTerrainRuntime.ApplyLaunchSiteFlatten)
        {
            activeFlattenRadiusMeters = 0f;
            activeHardFlattenRadiusMeters = 0f;
            return;
        }

        float padHeightMeters = (float)LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters();
        float heightScale = Mathf.Clamp(padHeightMeters / 120f, 1f, 2.5f);
        activeFlattenRadiusMeters = flattenRadiusMeters * heightScale;
        activeHardFlattenRadiusMeters = hardFlattenRadiusMeters * heightScale;
    }

    void ApplyMapModePresentation(bool mapMode)
    {
        if (mapMode)
        {
            EnsureScaledSpaceMaterialTextures();

            if (chunksRoot != null)
            {
                chunksRoot.gameObject.SetActive(false);
            }

            if (mapSphereRoot != null)
            {
                mapSphereRoot.gameObject.SetActive(true);
            }

            ApplyPngGlobeCompositing();
            ApplyAtmosphereCloudVisuals();
        }
        else
        {
            UpdateAltitudeShellPresentation();
            ApplyAtmosphereCloudVisuals();
        }

        DisableLegacyOceanSphere();
    }

    void UpdateAltitudeShellPresentation()
    {
        if (mapModeActive)
        {
            return;
        }

        PlanetLodAltitudePolicy.UpdateShellState(
            ref altitudeShellState,
            viewerAltitudeMeters,
            planetRadius,
            Time.unscaledDeltaTime
        );

        bool showShell = altitudeShellState.ShellVisible;
        bool hideChunks = PlanetLodAltitudePolicy.ShouldHideTerrainChunks(altitudeShellState);

        if (hideChunks && chunksWereVisibleLastFrame)
        {
            CollapseLodTree();
        }
        else if (!hideChunks && !chunksWereVisibleLastFrame)
        {
            PrimeFlightTerrainLod();
        }

        chunksWereVisibleLastFrame = !hideChunks;

        if (showShell)
        {
            appliedGlobeQuality = PlanetDistantVisualPolicy.ResolveGlobeQuality(
                viewerAltitudeMeters,
                appliedGlobeQuality
            );
            appliedGlobeSubdivisions = PlanetDistantVisualPolicy.GetShellSubdivisions(
                appliedGlobeQuality,
                mapMode: false
            );
            EnsureMapSphere();

            if (!altitudeShellState.ShellMeshReady)
            {
                EnsureScaledSpaceMaterialTextures(force: true);
                RefreshMapSphere();
                altitudeShellState.ShellMeshReady = true;
            }

            if (mapSphereRoot != null)
            {
                // Flight camera culls ScaledSpace; altitude shell must use LocalPhysics.
                FlightRenderLayers.ApplyLocalPhysicsLayer(mapSphereRoot.gameObject);
                mapSphereRoot.gameObject.SetActive(true);
            }

            ApplyMapShellTint();
            ApplyPngGlobeCompositing();
            SyncDistantShellPresentation();
            if (ShouldUseDistantEarthWeather())
            {
                ApplyAtmosphereCloudVisuals();
            }
        }
        else if (mapSphereRoot != null)
        {
            mapSphereRoot.gameObject.SetActive(false);
            DestroyNamedChild(mapSphereRoot, "PlanetGlobeDepth");
        }

        if (chunksRoot != null)
        {
            chunksRoot.gameObject.SetActive(!hideChunks);
        }
    }

    void ApplyPngGlobeCompositing()
    {
        if (scaledSpaceMaterial == null)
        {
            return;
        }

        if (CelestialTerrainRuntime.IsMoon)
        {
            PlanetDistantVisualPolicy.ApplyMoonGlobeCompositing(scaledSpaceMaterial, mapModeActive);
            return;
        }

        // LOD / map PNG is the geographic color globe. Keep it opaque with ZWrite
        // so Cape/Moon starts still show earth_geographic. Distant flight Earth
        // (other GameObject, or this LOD shell past ~1.5 Mm) turns ZWrite off so
        // the 8 km cloud shell wins ZTest.
        if (!mapModeActive && ShouldUseDistantEarthWeather())
        {
            PlanetDistantVisualPolicy.ApplyEarthColorGlobeCompositing(
                scaledSpaceMaterial,
                mapMode: false
            );
            return;
        }

        scaledSpaceMaterial.renderQueue = mapModeActive ?
            (int)UnityEngine.Rendering.RenderQueue.Geometry :
            PlanetDistantVisualPolicy.FlightDistantGlobeRenderQueue;
        scaledSpaceMaterial.SetFloat("_ZWrite", 1f);
        scaledSpaceMaterial.SetOverrideTag("RenderType", "Opaque");
    }

    bool ShouldUseDistantEarthWeather()
    {
        return !CelestialTerrainRuntime.IsMoon &&
            PlanetDistantVisualPolicy.UsesDistantEarthWeather(viewerAltitudeMeters);
    }

    void ApplyMapShellTint()
    {
        if (scaledSpaceMaterial == null)
        {
            return;
        }

        scaledSpaceMaterial.SetColor("_ColorTint", new Color(1.1f, 1.1f, 1.1f, 1f));
    }

    void SyncDistantShellPresentation()
    {
        PlanetDistantVisualPolicy.GlobeQuality quality =
            PlanetDistantVisualPolicy.ResolveGlobeQuality(
                viewerAltitudeMeters,
                appliedGlobeQuality
            );
        bool prefer2K = PlanetDistantVisualPolicy.UsesDistantColor2K(quality);
        int subdivisions = PlanetDistantVisualPolicy.GetShellSubdivisions(
            quality,
            mapMode: false
        );

        if (subdivisions != appliedGlobeSubdivisions)
        {
            appliedGlobeQuality = quality;
            appliedGlobeSubdivisions = subdivisions;
            RefreshMapSphere();
        }
        else
        {
            appliedGlobeQuality = quality;
        }

        BindShellColorMap(prefer2K);
    }

    void BindShellColorMap(bool preferDistant2K)
    {
        if (scaledSpaceMaterial == null)
        {
            return;
        }

        Texture2D color;
        if (CelestialTerrainRuntime.IsMoon)
        {
            color = PlanetTerrainTextureResources.GetMoonColorMap();
        }
        else
        {
            color = preferDistant2K ?
                PlanetTerrainTextureResources.GetFlightDistantColorMap() :
                MapModeEarthResources.GetColorMap();

            if (color == null)
            {
                color = PlanetTerrainTextureResources.EarthColorMap;
            }
        }

        if (color == null)
        {
            return;
        }

        PlanetParallaxTextures.ConfigureTextureSampling(color);
        scaledSpaceMaterial.SetTexture("_ColorMap", color);
        if (CelestialTerrainRuntime.IsMoon)
        {
            PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(scaledSpaceMaterial);
        }
        else
        {
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(scaledSpaceMaterial);
        }
    }

    void UpdateOceanVisibility()
    {
        DisableLegacyOceanSphere();
    }

    public void CollapseLodTree()
    {
        quadtree?.CollapseToRoots();
    }

    public static void ApplyRuntimeLodSettings()
    {
        if (activeInstance == null)
        {
            return;
        }

        activeInstance.ApplyLodSettingsFromPrefs();
        activeInstance.RequestViewerLodRebuild();
    }

    public static void ApplyRuntimeAtmosphereCloudVisuals()
    {
        activeInstance?.ApplyAtmosphereCloudVisuals();
    }

    public static void ApplyRuntimeAtmosphereOpacity()
    {
        activeInstance?.atmosphereSphere?.ApplyOpacity(DevSettings.AtmosphereOpacity);
    }

    public static void ApplyRuntimeCloudSettings()
    {
        if (activeInstance == null)
        {
            return;
        }

        PlanetLodSettings.Load();
        activeInstance.cloudLayers?.ApplyDetailLevel(PlanetLodSettings.CloudDetail);
    }

    public static void ApplyRuntimeMoonGeographicMap()
    {
        PlanetTerrainTextureResources.ForceReloadMoonColorMapOnly();

        if (activeInstance != null)
        {
            PlanetTerrainTextureResources.ApplyTo(activeInstance);
            activeInstance.RefreshEarthGeographicMapBindings();
            activeInstance.RequestViewerLodBootstrap();

            if (activeInstance.mapModeActive)
            {
                activeInstance.ApplyMapModePresentation(true);
            }
        }

        DistantCelestialGlobe.RebuildAllForBody(CelestialTerrainRuntime.MoonBodyName);
    }

    public static void ApplyRuntimeEarthGeographicMap()
    {
        PlanetTerrainTextureResources.ForceReloadEarthColorMapOnly();

        if (activeInstance == null)
        {
            return;
        }

        PlanetTerrainTextureResources.ApplyTo(activeInstance);
        activeInstance.RefreshEarthGeographicMapBindings();
        activeInstance.RequestViewerLodBootstrap();

        if (activeInstance.mapModeActive)
        {
            activeInstance.ApplyMapModePresentation(true);
        }
    }

    void RefreshEarthGeographicMapBindings()
    {
        RefreshLocalChunkMaterialTextures();

        if (CelestialTerrainRuntime.IsMoon)
        {
            BindShellColorMap(preferDistant2K: false);
            EnsureScaledSpaceMaterialTextures(force: true);
        }

        RefreshShellMapTexturesIfVisible();

        if (!mapModeActive)
        {
            UpdateAltitudeShellPresentation();
        }
    }

    void RefreshLocalChunkMaterialTextures()
    {
        if (localChunksMaterial == null)
        {
            return;
        }

        if (LocalChunksUseCloseupSplat())
        {
            PlanetParallaxTerrainSet terrainSet = PlanetTerrainTextureResources.TerrainSet;

            if (
                terrainSet == null ||
                !PlanetBiomeTerrainMaterialBuilder.HasPartialTerrainSet(terrainSet)
            )
            {
                terrainSet = BuildTerrainSetFromInstanceSlots();
            }

            if (PlanetBiomeTerrainMaterialBuilder.HasPartialTerrainSet(terrainSet))
            {
                PlanetParallaxTextures.ApplyTerrainSetToMaterial(localChunksMaterial, terrainSet);
            }

            BindLodBodyMacroColorMap();
            return;
        }

        BindSandboxChunkAlbedo();
    }

    void RefreshShellMapTexturesIfVisible()
    {
        if (!mapModeActive && !altitudeShellState.ShellVisible)
        {
            return;
        }

        EnsureScaledSpaceMaterialTextures(force: true);

        if (mapSphere != null && scaledSpaceMaterial != null)
        {
            RefreshMapSphere();
        }

        if (!mapModeActive && altitudeShellState.ShellVisible)
        {
            SyncDistantShellPresentation();
        }
    }

    public void RequestViewerLodBootstrap()
    {
        pendingViewerLodBootstrap = Application.isPlaying;
        viewerLodBootstrapFramesRemaining = Application.isPlaying ? BootstrapMaxFrames : 0;
        nextLodUpdateTime = 0f;
        ClearIdleLodBaseline();
        quadtree?.ResetAltitudeLodTracking();
    }

    void ApplyAtmosphereCloudVisuals()
    {
        if (ShouldUseDistantEarthWeather() && cloudLayers != null)
        {
            cloudLayers.PreferNasaMapForDistantGlobe();
        }

        cloudLayers?.ApplyRenderLayer(mapModeActive);
        atmosphereSphere?.ApplyRenderLayer(mapModeActive);

        if (!mapModeActive && ShouldUseDistantEarthWeather())
        {
            atmosphereSphere?.ApplyDistantLimbLook();
        }
    }

    void ApplyLodSettingsFromPrefs()
    {
        PlanetLodSettings.Load();
        PlanetLodSettings.ResolvedRuntime settings = PlanetLodSettings.ResolveBaselineRuntime();
        maxLodSplitsPerFrame = Mathf.Clamp(settings.MaxLodSplitsPerFrame, 1, 24);
        maxMeshBuildsPerFrame = Mathf.Clamp(settings.MaxMeshBuildsPerFrame, 1, 24);
        lodUpdateIntervalSeconds = Mathf.Max(0.04f, settings.LodUpdateIntervalSeconds);
        splitDistanceMeters = Mathf.Clamp(settings.SplitDistanceMeters, 500f, 24000f);
        runtimeLodDistanceFactor = PlanetLodSettings.LodDistanceFactor;
        runtimeLoadSpeedPerFrame = PlanetLodSettings.LoadSpeedPerFrame;
        runtimeMaxLodCap = PlanetLodSettings.MaxLod;
        runtimeViewConeHalfAngleDegrees = PlanetLodSettings.ViewConeHalfAngleDegrees;
        ApplyLodSettingsToQuadtree(settings);
        ApplyRuntimeTuning();
        nextLodUpdateTime = 0f;
        ClearIdleLodBaseline();
    }

    void ApplyLodSettingsToQuadtree(PlanetLodSettings.ResolvedRuntime settings)
    {
        if (quadtree == null)
        {
            return;
        }

        quadtree.SplitDistanceMeters = settings.SplitDistanceMeters;
        quadtree.MinChunkSpanMeters = settings.MinChunkSpanMeters;
        quadtree.LaunchSiteMaxChunkSpanMeters = settings.LaunchSiteMaxChunkSpanMeters;
        quadtree.SplitTargetSpanFactor = settings.SplitTargetSpanFactor;
        quadtree.MaxActiveChunks = Mathf.Max(64, settings.MaxActiveChunks);
    }

    void ApplyRuntimeTuning()
    {
        if (quadtree == null)
        {
            return;
        }

        quadtree.SplitTargetSpanFactor =
            FlightPlanetTerrainTuning.SplitSpanFactorPerLodDistanceUnit *
            runtimeLodDistanceFactor;
        quadtree.RuntimeMaxLodCap = runtimeMaxLodCap;
        quadtree.AsyncLodSettings.ApplyViewConeHalfAngleDegrees(
            runtimeViewConeHalfAngleDegrees
        );
        maxLodSplitsPerFrame = Mathf.Clamp(runtimeLoadSpeedPerFrame, 1, 24);
        maxMeshBuildsPerFrame = Mathf.Clamp(runtimeLoadSpeedPerFrame, 1, 24);
        quadtree.AsyncLodSettings.MaxMeshCompletionsPerFrame = Mathf.Clamp(
            runtimeLoadSpeedPerFrame,
            1,
            3
        );
        PlanetFlightChunkMeshPipeline.MaxConcurrentBuilds = Mathf.Clamp(
            runtimeLoadSpeedPerFrame,
            4,
            8
        );
        quadtree.AsyncLodSettings.ViewerSyncMeshBuildMaxLod = Mathf.Clamp(
            runtimeMaxLodCap - 2,
            PlanetPqsAsyncLodSettings.DefaultSyncMeshBuildMaxLod + 1,
            4
        );
    }

    public void RequestViewerLodRebuild()
    {
        pendingViewerLodBootstrap = Application.isPlaying;
        viewerLodBootstrapFramesRemaining = Application.isPlaying ? BootstrapMaxFrames : 0;
        nextLodUpdateTime = 0f;
        ClearIdleLodBaseline();
        CollapseLodTree();
    }

    public void InvalidateLaunchSiteMeshes()
    {
        quadtree?.InvalidateLaunchSiteMeshes();
    }

    public void Build()
    {
        activeInstance = this;
        altitudeShellState = default;
        appliedGlobeQuality = PlanetDistantVisualPolicy.GlobeQuality.Full;
        appliedGlobeSubdivisions = PlanetDistantVisualPolicy.DefaultShellSubdivisions;
        planetRadius = CelestialTerrainRuntime.LodBodyRadiusUnity;
        ApplyInspectorPerformanceCaps();
        DisposeQuadtree();
        earthColorMap = null;
        earthNormalMap = null;
        earthHeightMap = null;
        PlanetTerrainTextureResources.PrepareForPlanetRebuild();
        PlanetTerrainTextureResources.ApplyTo(this);
        AssignTexturesFromAssetPathsIfMissing();
        EnsureComponents();
        DisableLegacyPlanetMesh();
        EnsureChunksRoot();
        EnsureMapSphere();
        if (CelestialTerrainRuntime.IsMoon)
        {
            DisableCloudLayers();
            DisableAtmosphereSphere();
        }
        else
        {
            EnsureCloudLayers();
            EnsureAtmosphereSphere();
        }
        DisableLegacyOceanSphere();
        ConfigureSpaceLighting();

        LaunchSiteTerrain.Refresh(earthHeightMap);

        if (earthHeightMap == null &&
            !PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline &&
            !CelestialTerrainRuntime.IsMoon)
        {
            Debug.LogWarning(
                "FlightPlanetSphere: EarthHeight.dds not found at " +
                PlanetSurfaceMaps.EarthHeightAssetPath +
                " — building smooth sphere chunks."
            );
        }

        scaledSpaceMaterial = CreateScaledSpaceMaterial();
        localChunksMaterial = CreateLocalChunksMaterial();

        if (localChunksMaterial == null)
        {
            Debug.LogError(
                "FlightPlanetSphere: local chunk material failed; using URP Lit fallback so LOD still runs."
            );
            localChunksMaterial = RuntimeMaterialFactory.CreateLitMaterial(
                new Color(0.28f, 0.48f, 0.22f, 1f)
            );
        }

        if (localChunksMaterial == null)
        {
            Debug.LogError(
                "FlightPlanetSphere: no fallback shader either; quadtree will be disabled."
            );
            return;
        }

        ResolveLaunchSiteFlattenRadii(
            out float activeFlattenRadiusMeters,
            out float activeHardFlattenRadiusMeters
        );

        PlanetLodSettings.Load();
        PlanetLodSettings.ResolvedRuntime lodRuntime = PlanetLodSettings.ResolveBaselineRuntime();
        maxLodSplitsPerFrame = Mathf.Clamp(lodRuntime.MaxLodSplitsPerFrame, 1, 24);
        maxMeshBuildsPerFrame = Mathf.Clamp(lodRuntime.MaxMeshBuildsPerFrame, 1, 12);
        lodUpdateIntervalSeconds = Mathf.Max(0.04f, lodRuntime.LodUpdateIntervalSeconds);
        splitDistanceMeters = Mathf.Clamp(lodRuntime.SplitDistanceMeters, 500f, 24000f);

        quadtree = new PlanetQuadtreeLod(
            transform,
            chunksRoot,
            localChunksMaterial,
            earthHeightMap,
            planetRadius,
            splitDistanceMeters,
            maxLodLevel,
            minColliderLodLevel,
            chunkVertexResolution,
            activeFlattenRadiusMeters,
            activeHardFlattenRadiusMeters,
            lodDebugLineWidth,
            colliderMaxSurfaceDistanceMeters,
            mergeDistanceMultiplier: MergeDistanceMultiplier,
            minChunkSpanMeters: lodRuntime.MinChunkSpanMeters,
            launchSiteMaxChunkSpanMeters: lodRuntime.LaunchSiteMaxChunkSpanMeters,
            maxActiveChunks: lodRuntime.MaxActiveChunks
        );
        runtimeLodDistanceFactor = PlanetLodSettings.LodDistanceFactor;
        runtimeLoadSpeedPerFrame = PlanetLodSettings.LoadSpeedPerFrame;
        runtimeMaxLodCap = PlanetLodSettings.MaxLod;
        runtimeViewConeHalfAngleDegrees = PlanetLodSettings.ViewConeHalfAngleDegrees;
        ApplyRuntimeTuning();
        quadtree.ViewerAltitudeMeters = viewerAltitudeMeters;
        EnsureLodDiagOverlay();
        quadtree.ViewerVelocityWorld = viewerVelocityWorld;
        quadtree.ViewerVelocitySim = viewerVelocitySim;
        quadtree.ViewerLookAheadSeconds = runtimeLookAheadSeconds;
        quadtree.ResetAltitudeLodTracking();
        showLodDebug = false;
        quadtree.SetLodDebugVisible(false);
        mapSphere?.Build(
            scaledSpaceMaterial,
            planetRadius,
            PlanetDistantVisualPolicy.DefaultShellSubdivisions,
            applyScaledSpaceLayer: mapModeActive
        );
        DisableLegacyOceanSphere();
        RefreshEarthColorShaderProperties();
        if (mapModeActive)
        {
            EnsureScaledSpaceMaterialTextures();
        }
        ApplyMapModePresentation(mapModeActive);
        UpdateOceanVisibility();
        nextLodDebugRefreshTime = 0f;
        nextLodUpdateTime = 0f;
        chunksWereVisibleLastFrame = !mapModeActive;
        ClearIdleLodBaseline();
        pendingViewerLodBootstrap = Application.isPlaying;
        viewerLodBootstrapFramesRemaining = Application.isPlaying ? BootstrapMaxFrames : 0;
    }

    bool NeedsViewerPadLodBootstrap()
    {
        if (quadtree == null ||
            PlanetLodAltitudePolicy.ShouldHideTerrainChunks(altitudeShellState))
        {
            return false;
        }

        if (!quadtree.ViewerAreaHasRenderableMesh())
        {
            return true;
        }

        float viewerSpan = quadtree.GetViewerLeafSpanMeters();
        float finestSpan = quadtree.GetFinestPossibleViewerSpanMeters();
        float padTarget = Mathf.Max(
            quadtree.LaunchSiteMaxChunkSpanMeters,
            finestSpan * 1.05f
        );
        return viewerSpan > padTarget;
    }

    bool NeedsViewerLodRefinement()
    {
        return NeedsViewerPadLodBootstrap() ||
            (quadtree != null &&
                (quadtree.ViewerNeighborhoodHasLodGap() ||
                    quadtree.DiskHasUnfinishedLod() ||
                    quadtree.FaceCenterL1sNeedViewerLod()));
    }

    bool IsLodBootstrapping()
    {
        return pendingViewerLodBootstrap || viewerLodBootstrapFramesRemaining > 0;
    }

    bool IsLodWarmupActive()
    {
        return lodWarmupEndTime > 0f && Time.unscaledTime < lodWarmupEndTime;
    }

    void BeginLodWarmup()
    {
        if (lodWarmupEndTime < 0f)
        {
            lodWarmupEndTime = Time.unscaledTime + LodWarmupSeconds;
        }
    }

    void GetLodWorkBudgets(out int splitBudget, out int meshBudget)
    {
        if (IsLodBootstrapping())
        {
            splitBudget = BootstrapMaxSplitsPerFrame;
            meshBudget = BootstrapMaxMeshesPerFrame;
            return;
        }

        if (IsLodWarmupActive())
        {
            splitBudget = Mathf.Min(maxLodSplitsPerFrame, WarmupMaxSplitsPerFrame);
            meshBudget = Mathf.Min(maxMeshBuildsPerFrame, WarmupMaxMeshesPerFrame);
            return;
        }

        splitBudget = Mathf.Min(Mathf.Max(1, maxLodSplitsPerFrame), HitchMaxSplitsPerFrame);
        meshBudget = Mathf.Min(Mathf.Max(1, maxMeshBuildsPerFrame), HitchMaxMeshesPerFrame);
    }

    bool ShouldSkipIdleLodTick()
    {
        if (IsLodBootstrapping() || IsLodWarmupActive())
        {
            return false;
        }

        if (NeedsViewerLodRefinement())
        {
            return false;
        }

        if (quadtree == null ||
            quadtree.HasPendingLodWork() ||
            quadtree.LastLodRefreshHadPendingWork ||
            quadtree.ViewerAncestryHasIncompleteCoverage() ||
            !hasIdleLodBaseline)
        {
            return false;
        }

        if (quadtree.GetActiveChunkCount() >= quadtree.MaxActiveChunks)
        {
            return false;
        }

        Vector3 focus = quadtree.GetShipFocusDirectionSimRaw();
        float moveMeters = Vector3.Angle(lastIdleLodFocusSim, focus) *
            Mathf.Deg2Rad *
            planetRadius;
        float altitudeDelta = Mathf.Abs(viewerAltitudeMeters - lastIdleLodAltitudeMeters);
        return moveMeters < IdleLodShipMoveMeters &&
            altitudeDelta < IdleLodAltitudeDeltaMeters;
    }

    void ClearIdleLodBaseline()
    {
        hasIdleLodBaseline = false;
        wasSkippingIdleLod = false;
        lodWarmupEndTime = -1f;
    }

    bool IsSimulationPaused()
    {
        if (cachedSimulationRunner == null)
        {
            cachedSimulationRunner = FindFirstObjectByType<SimulationRunner>();
        }

        return cachedSimulationRunner != null && cachedSimulationRunner.IsPaused;
    }

    bool ShouldSkipLodUpdatesForTimeWarp()
    {
        if (cachedSimulationRunner == null)
        {
            cachedSimulationRunner = FindFirstObjectByType<SimulationRunner>();
        }

        return cachedSimulationRunner != null &&
            cachedSimulationRunner.GetEffectiveTimeScale() > 4.0;
    }

    void RememberIdleLodBaseline()
    {
        if (quadtree == null)
        {
            return;
        }

        lastIdleLodFocusSim = quadtree.GetShipFocusDirectionSimRaw();
        lastIdleLodAltitudeMeters = viewerAltitudeMeters;
        hasIdleLodBaseline = true;
    }

    void TickViewerTerrainLod(int splitBudget, int meshBudget)
    {
        if (quadtree == null)
        {
            return;
        }

        bool capTickTime = !IsLodBootstrapping() && !IsLodWarmupActive();
        if (capTickTime)
        {
            quadtree.BeginLodTickTimeBudget(HitchMaxLodTickMilliseconds);
        }

        Profiler.BeginSample("FlightLod.Tick");
        quadtree.RefreshLodLookAhead(splitBudget, meshBudget, allowNeighborSync: true);
        Profiler.EndSample();
        if (capTickTime)
        {
            quadtree.ClearLodTickTimeBudget();
        }

        Profiler.BeginSample("FlightLod.FeedMeshes");
        quadtree.FeedPendingMeshQueue(meshBudget);
        Profiler.EndSample();
        RememberIdleLodBaseline();
    }

    void LogLaunchSiteGeography()
    {
        global::SimVector3 launchNormal = LandingConfig.GetSurfaceLaunchPadNormal();
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            launchNormal,
            out double simU,
            out double simV
        );

        LaunchSiteConfig.Site activeSite = LaunchSiteConfig.Active;
        Vector2 expectedGeoUv = new Vector2(
            (float)PlanetGeographicCoords.LongitudeRadiansToGeographicU(activeSite.LongitudeRadians),
            (float)PlanetGeographicCoords.LatitudeRadiansToGeographicV(activeSite.LatitudeRadians)
        );
        double gebcoMeters = PlanetFlightElevationSampling.SampleRealElevationMeters(
            activeSite.LatitudeDegrees,
            activeSite.LongitudeDegrees
        );
        double latitudeDegrees = activeSite.LatitudeDegrees;
        double longitudeDegrees = activeSite.LongitudeDegrees;

        Debug.Log(
            "FlightPlanetSphere: launch site " +
            Localization.Translate(activeSite.LocalizationKey) + " " +
            FormatLatitudeDegrees(latitudeDegrees) + ", " +
            FormatLongitudeDegrees(longitudeDegrees) + ", " +
            "geoUv=(" + simU.ToString("F3") + ", " + simV.ToString("F3") + "), " +
            "expectedGeoUv=(" + expectedGeoUv.x.ToString("F3") + ", " +
            expectedGeoUv.y.ToString("F3") + "), " +
            "GEBCO=" + gebcoMeters.ToString("F0") + "m."
        );
    }

    void UpdateTransparentShellCulling()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            return;
        }

        Vector3 cameraWorldPosition = camera.transform.position;
        cloudLayers?.UpdateCameraFacing(cameraWorldPosition);
        atmosphereSphere?.UpdateCameraFacing(cameraWorldPosition);
    }

    static string FormatLatitudeDegrees(double latitudeDegrees)
    {
        return System.Math.Abs(latitudeDegrees).ToString("F2") + "°" +
            (latitudeDegrees >= 0.0 ? "N" : "S");
    }

    static string FormatLongitudeDegrees(double longitudeDegrees)
    {
        return System.Math.Abs(longitudeDegrees).ToString("F2") + "°" +
            (longitudeDegrees >= 0.0 ? "E" : "W");
    }

    void LateUpdate()
    {
        UpdateTransparentShellCulling();

        if (mapModeActive)
        {
            SyncMapSphereWorldFrame();
            return;
        }

        if (quadtree != null)
        {
            quadtree.ViewerAltitudeMeters = viewerAltitudeMeters;
            quadtree.ViewerVelocityWorld = viewerVelocityWorld;
            quadtree.ViewerVelocitySim = viewerVelocitySim;
            quadtree.ViewerLookAheadSeconds = runtimeLookAheadSeconds;
        }

        UpdateAltitudeShellPresentation();

        if (quadtree == null || !lodUpdatesEnabled)
        {
            return;
        }

        Profiler.BeginSample("FlightLod.DispatchMeshes");
        quadtree.DispatchCompletedMeshes();
        Profiler.EndSample();
        SyncViewerLodFocusFromSimulation();

        if (PlanetLodAltitudePolicy.ShouldHideTerrainChunks(altitudeShellState))
        {
            return;
        }

        bool bootstrapping = IsLodBootstrapping();
        if (quadtree != null)
        {
            quadtree.SyncInDiskMeshes = bootstrapping;
        }

        if (IsSimulationPaused() &&
            !bootstrapping &&
            !IsLodWarmupActive() &&
            !NeedsViewerLodRefinement())
        {
            Profiler.BeginSample("FlightLod.SyncCoverage");
            quadtree.SyncDrawnCoverage();
            Profiler.EndSample();
            return;
        }

        if (ShouldSkipLodUpdatesForTimeWarp() && !bootstrapping)
        {
            Profiler.BeginSample("FlightLod.Tick");
            quadtree.CoarsenForAltitudeOnly();
            Profiler.EndSample();
            RememberIdleLodBaseline();
            return;
        }

        GetLodWorkBudgets(out int splitBudget, out int meshBudget);

        int meshFeedBudget = bootstrapping || IsLodWarmupActive() ?
            meshBudget :
            Mathf.Max(1, meshBudget / 2);
        Profiler.BeginSample("FlightLod.FeedMeshes");
        quadtree.FeedPendingMeshQueue(meshFeedBudget);
        Profiler.EndSample();

        if (ShouldSkipIdleLodTick())
        {
            wasSkippingIdleLod = true;
        }
        else
        {
            if (wasSkippingIdleLod)
            {
                nextLodUpdateTime = 0f;
                wasSkippingIdleLod = false;
            }

            bool tickDue = bootstrapping || Time.unscaledTime >= nextLodUpdateTime;
            if (tickDue)
            {
                if (!bootstrapping)
                {
                    nextLodUpdateTime =
                        Time.unscaledTime + Mathf.Max(0.05f, lodUpdateIntervalSeconds);
                }

                TickViewerTerrainLod(splitBudget, meshBudget);
            }
        }

        if (bootstrapping)
        {
            if (viewerLodBootstrapFramesRemaining > 0)
            {
                viewerLodBootstrapFramesRemaining--;
            }

            bool diskStillNeedsWork = NeedsViewerLodRefinement();
            bool padStillCoarse = NeedsViewerPadLodBootstrap();
            if (!diskStillNeedsWork ||
                (viewerLodBootstrapFramesRemaining <= 0 && !padStillCoarse))
            {
                pendingViewerLodBootstrap = false;
                viewerLodBootstrapFramesRemaining = 0;
                nextLodUpdateTime = 0f;
                BeginLodWarmup();
            }
        }
        else
        {
            BeginLodWarmup();
        }

        Profiler.BeginSample("FlightLod.SyncCoverage");
        quadtree.SyncDrawnCoverage();
        Profiler.EndSample();

        if (!showLodDebug)
        {
            return;
        }

        if (Time.unscaledTime < nextLodDebugRefreshTime)
        {
            return;
        }

        nextLodDebugRefreshTime = Time.unscaledTime + LodRefreshIntervalSeconds;
        quadtree.RefreshDebugOutlines();
    }

    void SyncViewerLodFocusFromSimulation()
    {
        if (quadtree == null)
        {
            return;
        }

        Vector3 shipInPlanetMesh = transform.InverseTransformPoint(Vector3.zero);
        if (shipInPlanetMesh.sqrMagnitude > 1e-6f)
        {
            quadtree.ViewerFocusDirectionSim = PlanetCubeSphere.MeshFrameUnitToSimUnit(
                shipInPlanetMesh.normalized
            );
            quadtree.SyncViewerFocusDirections();
            return;
        }

        SimulationRunner runner = FindFirstObjectByType<SimulationRunner>();

        if (runner == null || !runner.IsInitialized || runner.Simulation == null)
        {
            return;
        }

        quadtree.ViewerFocusDirectionSim = WorldVisualBridge.SimNormalToUnity(
            runner.GetInterpolatedBodyFixedUnderShip()
        );
        quadtree.SyncViewerFocusDirections();
    }

    internal void ApplySharedTerrainResources(
        Texture2D sharedEarthColorMap,
        Texture2D sharedEarthNormalMap,
        Texture2D sharedEarthHeightMap,
        PlanetParallaxTerrainSet sharedTerrainSet
    )
    {
        earthColorMap = sharedEarthColorMap;
        earthNormalMap = sharedEarthNormalMap;
        earthHeightMap = sharedEarthHeightMap;

        splatInfluenceMap = sharedTerrainSet?.InfluenceMap;
        lowAlbedoMap = sharedTerrainSet?.LowAlbedo;
        lowNormalMap = sharedTerrainSet?.LowNormal;
        midAlbedoMap = sharedTerrainSet?.MidAlbedo;
        midNormalMap = sharedTerrainSet?.MidNormal;
        highAlbedoMap = sharedTerrainSet?.HighAlbedo;
        highNormalMap = sharedTerrainSet?.HighNormal;
        steepAlbedoMap = sharedTerrainSet?.SteepAlbedo;
        steepNormalMap = sharedTerrainSet?.SteepNormal;
    }

    void AssignTexturesFromAssetPathsIfMissing()
    {
        bool changed = false;

#if UNITY_EDITOR
        // earthColorMap/earthNormalMap are intentionally NOT re-filled from
        // AssetDatabase here. PlanetTerrainTextureResources already loaded them through
        // DdsTextureLoader.LoadWorldColorMap/LoadWorldNormalMap, which flips these
        // equirectangular maps to the north-pole-at-row-0 convention every lat/long
        // sampler in this game expects. A plain AssetDatabase-imported copy has no such
        // flip and would silently read every location from its mirrored latitude.

        if (!PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline &&
            !CelestialTerrainRuntime.IsMoon &&
            earthHeightMap == null)
        {
            Texture2D importedHeight = LoadEditorTexture(PlanetSurfaceMaps.EarthHeightAssetPath);

            if (IsUsableHeightMap(importedHeight))
            {
                earthHeightMap = importedHeight;
                changed = true;
            }
            else if (importedHeight == null)
            {
                importedHeight = LoadEditorTexture(PlanetSurfaceMaps.EarthHeightAssetPathFlat);

                if (IsUsableHeightMap(importedHeight))
                {
                    earthHeightMap = importedHeight;
                    changed = true;
                }
            }
        }

        changed |= AssignTerrainTexturesFromAssetsIfMissing();

        if (changed)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#else
        AssignTerrainTexturesFromAssetsIfMissing();
#endif
    }

    bool AssignTerrainTexturesFromAssetsIfMissing()
    {
        RssTerrainSplatPack pack = RssTerrainSplatPack.ForLodBody();
        bool changed = false;
        changed |= AssignTerrainTextureIfMissing(
            ref splatInfluenceMap,
            pack,
            pack.InfluenceFileName,
            ParallaxTerrainPaths.InfluenceTextureName
        );
        changed |= AssignTerrainTextureIfMissing(
            ref lowAlbedoMap,
            pack,
            pack.LowColorFileName,
            ParallaxTerrainPaths.LowAlbedoTextureName
        );
        changed |= AssignTerrainTextureIfMissing(
            ref lowNormalMap,
            pack,
            pack.LowNormalFileName,
            ParallaxTerrainPaths.LowNormalTextureName
        );
        changed |= AssignTerrainTextureIfMissing(
            ref midAlbedoMap,
            pack,
            pack.MidColorFileName,
            ParallaxTerrainPaths.MidAlbedoTextureName
        );
        changed |= AssignTerrainTextureIfMissing(
            ref midNormalMap,
            pack,
            pack.MidNormalFileName,
            ParallaxTerrainPaths.MidNormalTextureName
        );
        changed |= AssignTerrainTextureIfMissing(
            ref highAlbedoMap,
            pack,
            pack.HighColorFileName,
            ParallaxTerrainPaths.HighAlbedoTextureName
        );
        changed |= AssignTerrainTextureIfMissing(
            ref highNormalMap,
            pack,
            pack.HighNormalFileName,
            ParallaxTerrainPaths.HighNormalTextureName
        );
        changed |= AssignTerrainTextureIfMissing(
            ref steepAlbedoMap,
            pack,
            pack.SteepColorFileName,
            ParallaxTerrainPaths.SteepAlbedoTextureName
        );
        changed |= AssignTerrainTextureIfMissing(
            ref steepNormalMap,
            pack,
            pack.SteepNormalFileName,
            ParallaxTerrainPaths.SteepNormalTextureName
        );

        return changed;
    }

    bool AssignTerrainTextureIfMissing(
        ref Texture2D slot,
        RssTerrainSplatPack pack,
        string rssTextureBaseName,
        string parallaxTextureBaseName
    )
    {
        if (slot != null)
        {
            return false;
        }

#if UNITY_EDITOR
        Texture2D loaded = LoadEditorTexture(
            RssTerrainPaths.ResolveAssetTexturePath(pack, rssTextureBaseName)
        );
        if (loaded == null && !CelestialTerrainRuntime.IsMoon)
        {
            loaded = LoadEditorTexture(
                ParallaxTerrainPaths.ResolveAssetTexturePath(parallaxTextureBaseName)
            );
        }
#else
        Texture2D loaded = null;
#endif

        if (loaded == null)
        {
            return false;
        }

        slot = loaded;
        return true;
    }

#if UNITY_EDITOR
    static Texture2D LoadEditorTexture(string assetPath)
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }
#endif

    static bool IsUsableHeightMap(Texture2D texture)
    {
        return texture != null &&
            texture.format == TextureFormat.R16 &&
            texture.isReadable;
    }

    void ApplyInspectorPerformanceCaps()
    {
        chunkVertexResolution = Mathf.Clamp(chunkVertexResolution, 12, 32);
        maxLodLevel = Mathf.Clamp(maxLodLevel, 10, DefaultMaxLodLevel);
        minColliderLodLevel = Mathf.Clamp(
            minColliderLodLevel,
            DefaultMinColliderLodLevel,
            DefaultMaxLodLevel
        );
        maxLodSplitsPerFrame = Mathf.Clamp(
            maxLodSplitsPerFrame,
            1,
            DefaultMaxLodSplitsPerFrame
        );
        maxMeshBuildsPerFrame = Mathf.Clamp(
            maxMeshBuildsPerFrame,
            1,
            DefaultMaxMeshBuildsPerFrame
        );
        splitDistanceMeters = Mathf.Clamp(
            splitDistanceMeters,
            500f,
            DefaultSplitDistanceMeters
        );
        lodUpdateIntervalSeconds = Mathf.Max(
            lodUpdateIntervalSeconds,
            DefaultLodUpdateIntervalSeconds
        );
    }

    void ReleaseRuntimeTextures()
    {
        for (int i = 0; i < runtimeTextures.Count; i++)
        {
            if (runtimeTextures[i] != null)
            {
                Destroy(runtimeTextures[i]);
            }
        }

        runtimeTextures.Clear();
        ClearRuntimeOwnedTextureRefs();
    }

    void ClearRuntimeOwnedTextureRefs()
    {
        if (IsRuntimeOwned(earthColorMap))
        {
            earthColorMap = null;
        }

        if (IsRuntimeOwned(earthNormalMap))
        {
            earthNormalMap = null;
        }

        if (IsRuntimeOwned(earthHeightMap))
        {
            earthHeightMap = null;
        }
    }

    bool IsRuntimeOwned(Texture2D texture)
    {
        return texture != null && runtimeTextures.Contains(texture);
    }

    void ConfigureSpaceLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        // Moon has no atmosphere fill; keep a slightly higher floor so LROC albedo reads.
        RenderSettings.ambientLight = CelestialTerrainRuntime.IsMoon ?
            new Color(0.08f, 0.08f, 0.09f, 1f) :
            new Color(0.035f, 0.042f, 0.06f, 1f);
        RenderSettings.ambientIntensity = 1f;
    }

    PlanetParallaxTerrainSet BuildTerrainSetFromInstanceSlots()
    {
        return new PlanetParallaxTerrainSet
        {
            InfluenceMap = splatInfluenceMap,
            LowAlbedo = lowAlbedoMap,
            LowNormal = lowNormalMap,
            MidAlbedo = midAlbedoMap,
            MidNormal = midNormalMap,
            HighAlbedo = highAlbedoMap,
            HighNormal = highNormalMap,
            SteepAlbedo = steepAlbedoMap,
            SteepNormal = steepNormalMap,
        };
    }

    Material CreateLocalChunksMaterial()
    {
        if (localChunksMaterial != null)
        {
            Destroy(localChunksMaterial);
            localChunksMaterial = null;
        }

        PlanetTerrainTextureResources.EnsureLoaded();

        if (ShouldUseCloseupSplatMaterial())
        {
            PlanetParallaxTerrainSet terrainSet = PlanetTerrainTextureResources.TerrainSet;

            if (!PlanetBiomeTerrainMaterialBuilder.HasPartialTerrainSet(terrainSet))
            {
                terrainSet = BuildTerrainSetFromInstanceSlots();
            }

            if (PlanetBiomeTerrainMaterialBuilder.HasUsableDetailSet(terrainSet) ||
                PlanetBiomeTerrainMaterialBuilder.HasPartialTerrainSet(terrainSet))
            {
                localChunksMaterial = PlanetBiomeTerrainMaterialBuilder.CreateChunkMaterial(
                    earthColorMap,
                    terrainSet
                );
                if (localChunksMaterial != null)
                {
                    Debug.Log(
                        CelestialTerrainRuntime.IsMoon ?
                            "FlightPlanetSphere: Moon close-up splat = RSS-Terrain Low/Mid/High/Steep + LROC macro (LDEM heights)." :
                            "FlightPlanetSphere: Earth biome splat chunks."
                    );
                    return localChunksMaterial;
                }

                Debug.LogWarning(
                    "FlightPlanetSphere: splat pack present but EarthPlanetLocalChunks shader missing."
                );
            }

            if (CelestialTerrainRuntime.IsMoon)
            {
                Debug.LogWarning(
                    "FlightPlanetSphere: Moon RSS-Terrain pack missing — chunks fall back to moon_geographic PNG."
                );
            }
        }

        if (PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline || CelestialTerrainRuntime.IsMoon)
        {
            Texture2D colorMap = ResolveLodBodyColorMap();
            localChunksMaterial = PlanetBiomeTerrainMaterialBuilder.CreateChunkMaterial(
                colorMap,
                null
            );
            if (localChunksMaterial != null)
            {
                Debug.Log(
                    CelestialTerrainRuntime.IsMoon ?
                        "FlightPlanetSphere: Moon chunks = EarthPlanetLocalChunks + moon_geographic (LDEM heights)." :
                        "FlightPlanetSphere: GEBCO-only Earth chunks = EarthPlanetLocalChunks + earth_geographic (GEBCO heights)."
                );
                return localChunksMaterial;
            }

            localChunksMaterial = PlanetSandboxFlightMaterials.CreateFlightChunkMaterial(
                useVertexPaint: false
            );
            BindSandboxChunkAlbedo();
            Debug.LogWarning(
                CelestialTerrainRuntime.IsMoon ?
                    "FlightPlanetSphere: LocalChunks shader missing — Moon fallback sandbox + moon_geographic." :
                    "FlightPlanetSphere: LocalChunks shader missing — Earth fallback sandbox + earth_geographic."
            );
            return localChunksMaterial;
        }

        PlanetParallaxTerrainSet earthSet = PlanetTerrainTextureResources.TerrainSet;
        if (!PlanetBiomeTerrainMaterialBuilder.HasPartialTerrainSet(earthSet))
        {
            earthSet = BuildTerrainSetFromInstanceSlots();
        }

        localChunksMaterial = PlanetBiomeTerrainMaterialBuilder.CreateChunkMaterial(
            earthColorMap,
            earthSet
        );

        if (localChunksMaterial == null)
        {
            localChunksMaterial = PlanetSandboxFlightMaterials.CreateFlightChunkMaterial(
                useVertexPaint: false
            );
            BindSandboxChunkAlbedo();
            Debug.LogWarning(
                "FlightPlanetSphere: splat shader missing — falling back to sandbox chunks."
            );
        }

        return localChunksMaterial;
    }

    bool ShouldUseCloseupSplatMaterial()
    {
        return true;
    }

    Texture2D ResolveLodBodyColorMap()
    {
        if (CelestialTerrainRuntime.IsMoon)
        {
            return PlanetTerrainTextureResources.GetMoonColorMap();
        }

        if (earthColorMap != null)
        {
            return earthColorMap;
        }

        return PlanetTerrainTextureResources.EarthColorMap;
    }

    bool LocalChunksUseCloseupSplat()
    {
        return localChunksMaterial != null &&
            localChunksMaterial.HasProperty("_LowAlbedo") &&
            localChunksMaterial.HasProperty("_PlanetColorMap");
    }

    void BindLodBodyMacroColorMap()
    {
        if (localChunksMaterial == null || !localChunksMaterial.HasProperty("_PlanetColorMap"))
        {
            return;
        }

        Texture2D colorMap = ResolveLodBodyColorMap();

        if (colorMap == null)
        {
            return;
        }

        PlanetParallaxTextures.ConfigureTextureSampling(colorMap, clampWrap: true);
        localChunksMaterial.SetTexture("_PlanetColorMap", colorMap);
        if (CelestialTerrainRuntime.IsMoon)
        {
            PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(localChunksMaterial);
        }
        else
        {
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(localChunksMaterial);
        }
    }

    void BindSandboxChunkAlbedo()
    {
        if (localChunksMaterial == null)
        {
            return;
        }

        Texture2D colorMap = ResolveLodBodyColorMap();
        PlanetSandboxFlightMaterials.BindGeographicColorMap(localChunksMaterial, colorMap);
        if (CelestialTerrainRuntime.IsMoon)
        {
            PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(localChunksMaterial);
            Debug.Log(
                colorMap != null ?
                    "FlightPlanetSphere: flight chunks _BaseMap = moon_geographic " +
                    colorMap.width + "x" + colorMap.height + "." :
                    "FlightPlanetSphere: moon_geographic missing — chunks have no albedo map."
            );
        }
        else
        {
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(localChunksMaterial);
        }
    }

    void LogTerrainTextureSet(PlanetParallaxTerrainSet terrainSet)
    {
        if (terrainSet == null)
        {
            Debug.LogWarning("FlightPlanetSphere: terrain set is null.");
            return;
        }

        LogTerrainTexture("Influence", terrainSet.InfluenceMap);
        LogTerrainTexture("LowAlbedo", terrainSet.LowAlbedo);
        LogTerrainTexture("MidAlbedo", terrainSet.MidAlbedo);
        LogTerrainTexture("HighAlbedo", terrainSet.HighAlbedo);
        LogTerrainTexture("SteepAlbedo", terrainSet.SteepAlbedo);
        LogTerrainTexture("EarthColor", earthColorMap);
    }

    void LogTerrainTexture(string label, Texture2D texture)
    {
        if (texture == null)
        {
            Debug.LogWarning("FlightPlanetSphere: missing texture slot " + label + ".");
            return;
        }

        bool isRuntimeOwned = runtimeTextures.Contains(texture);
        bool isSharedRuntimeTexture =
            texture == earthColorMap ||
            texture == earthNormalMap ||
            texture == earthHeightMap;
        string sourceLabel = isRuntimeOwned || isSharedRuntimeTexture ?
            " (runtime DDS)" :
            " (asset)";
        Debug.Log(
            "FlightPlanetSphere: " +
            label +
            " " +
            texture.width +
            "x" +
            texture.height +
            " " +
            texture.format +
            sourceLabel
        );
    }

    void EnsureLodDiagOverlay()
    {
        PlanetLodDiagOverlay overlay = GetComponent<PlanetLodDiagOverlay>();
        if (overlay == null)
        {
            overlay = gameObject.AddComponent<PlanetLodDiagOverlay>();
        }

        overlay.Bind(this);
    }

    void EnsureComponents()
    {
        legacyMeshFilter = GetComponent<MeshFilter>();
        legacyMeshRenderer = GetComponent<MeshRenderer>();
    }

    void DisableLegacyPlanetMesh()
    {
        if (legacyMeshFilter != null)
        {
            legacyMeshFilter.sharedMesh = null;
        }

        if (legacyMeshRenderer != null)
        {
            legacyMeshRenderer.enabled = false;
        }
    }

    void EnsureChunksRoot()
    {
        Transform existingRoot = transform.Find("PlanetChunks");

        if (existingRoot != null)
        {
            chunksRoot = existingRoot;
            return;
        }

        GameObject rootObject = new GameObject("PlanetChunks");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;
        chunksRoot = rootObject.transform;
        FlightRenderLayers.ApplyLocalPhysicsLayer(chunksRoot.gameObject);
    }

    void EnsureMapSphere()
    {
        Transform existingMapSphere = transform.Find("PlanetMapSphere");

        if (existingMapSphere != null)
        {
            mapSphereRoot = existingMapSphere;
            mapSphere = existingMapSphere.GetComponent<FlightPlanetMapSphere>();

            if (mapSphere == null)
            {
                mapSphere = existingMapSphere.gameObject.AddComponent<FlightPlanetMapSphere>();
            }

            existingMapSphere.gameObject.SetActive(false);
            return;
        }

        GameObject mapObject = new GameObject("PlanetMapSphere");
        mapObject.transform.SetParent(transform, false);
        mapObject.transform.localPosition = Vector3.zero;
        mapObject.transform.localRotation = Quaternion.identity;
        mapObject.transform.localScale = Vector3.one;
        mapObject.SetActive(false);
        mapSphereRoot = mapObject.transform;
        mapSphere = mapObject.AddComponent<FlightPlanetMapSphere>();
    }

    void EnsureCloudLayers()
    {
        Transform existing = transform.Find("PlanetClouds");
        GameObject cloudsObject;

        if (existing != null)
        {
            cloudsObject = existing.gameObject;
            cloudsObject.SetActive(true);
        }
        else
        {
            cloudsObject = new GameObject("PlanetClouds");
            cloudsObject.transform.SetParent(transform, false);
            cloudsObject.transform.localPosition = Vector3.zero;
            cloudsObject.transform.localRotation = Quaternion.identity;
            cloudsObject.transform.localScale = Vector3.one;
        }

        cloudLayers = cloudsObject.GetComponent<FlightPlanetCloudLayers>();

        if (cloudLayers == null)
        {
            cloudLayers = cloudsObject.AddComponent<FlightPlanetCloudLayers>();
        }

        cloudLayers.Build(planetRadius);
        cloudLayers.ApplyRenderLayer(mapModeActive);
    }

    void DisableCloudLayers()
    {
        cloudLayers = null;
        Transform existing = transform.Find("PlanetClouds");

        if (existing != null)
        {
            existing.gameObject.SetActive(false);
        }
    }

    public void SyncCloudAnimation(double simulationTimeSeconds)
    {
        cloudLayers?.SyncAnimation(simulationTimeSeconds);
    }

    void EnsureAtmosphereSphere()
    {
        Transform existing = transform.Find("PlanetAtmosphere");
        GameObject atmosphereObject;

        if (existing != null)
        {
            atmosphereObject = existing.gameObject;
            atmosphereObject.SetActive(true);
        }
        else
        {
            atmosphereObject = new GameObject("PlanetAtmosphere");
            atmosphereObject.transform.SetParent(transform, false);
            atmosphereObject.transform.localPosition = Vector3.zero;
            atmosphereObject.transform.localRotation = Quaternion.identity;
            atmosphereObject.transform.localScale = Vector3.one;
        }

        atmosphereSphere = atmosphereObject.GetComponent<FlightPlanetAtmosphereSphere>();

        if (atmosphereSphere == null)
        {
            atmosphereSphere = atmosphereObject.AddComponent<FlightPlanetAtmosphereSphere>();
        }

        atmosphereSphere.Build(planetRadius);
        atmosphereSphere.ApplyRenderLayer(mapModeActive);
    }

    void DisableAtmosphereSphere()
    {
        atmosphereSphere = null;
        Transform existing = transform.Find("PlanetAtmosphere");

        if (existing != null)
        {
            existing.gameObject.SetActive(false);
        }
    }

    void DisableLegacyOceanSphere()
    {
        Transform existingOcean = oceanSphereRoot != null ?
            oceanSphereRoot :
            transform.Find("Planet_Ocean");

        if (existingOcean == null)
        {
            oceanSphereRoot = null;
            return;
        }

        oceanSphereRoot = existingOcean;
        existingOcean.gameObject.SetActive(false);
    }

    void DisposeQuadtree()
    {
        quadtree?.Dispose();
        quadtree = null;

        if (chunksRoot == null)
        {
            return;
        }

        for (int i = chunksRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(chunksRoot.GetChild(i).gameObject);
        }
    }

    Material CreateScaledSpaceMaterial()
    {
        if (scaledSpaceMaterial != null)
        {
            Destroy(scaledSpaceMaterial);
        }

        Shader shader = SpaceProgramShaders.Find("SpaceProgram/MapModeEarthSurface");

        if (shader == null)
        {
            shader = SpaceProgramShaders.Find("SpaceProgram/EarthPlanetSurface");
        }

        scaledSpaceMaterial = shader != null ?
            new Material(shader) :
            RuntimeMaterialFactory.CreateLitMaterial(new Color(0.2f, 0.6f, 1f));

        if (scaledSpaceMaterial == null)
        {
            return null;
        }

        scaledSpaceMaterial.name = "PlanetScaledSpace";
        scaledSpaceMaterial.SetTexture("_ColorMap", Texture2D.whiteTexture);
        scaledSpaceMaterial.SetColor("_ColorTint", new Color(1.1f, 1.1f, 1.1f, 1f));
        scaledSpaceMaterial.SetColor(
            "_FallbackColor",
            CelestialTerrainRuntime.IsMoon ?
                new Color(0.55f, 0.55f, 0.52f, 1f) :
                new Color(0.1f, 0.2f, 0.45f, 1f)
        );
        if (CelestialTerrainRuntime.IsMoon)
        {
            Texture2D moonColor = PlanetTerrainTextureResources.GetMoonColorMap();
            if (moonColor != null)
            {
                PlanetParallaxTextures.ConfigureTextureSampling(moonColor);
                scaledSpaceMaterial.SetTexture("_ColorMap", moonColor);
            }

            PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(scaledSpaceMaterial);
        }
        else
        {
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(scaledSpaceMaterial);
        }

        ApplyPngGlobeCompositing();
        return scaledSpaceMaterial;
    }

    void OnDestroy()
    {
        DisposeQuadtree();
        ReleaseRuntimeTextures();

        if (scaledSpaceMaterial != null)
        {
            Destroy(scaledSpaceMaterial);
        }

        if (localChunksMaterial != null)
        {
            Destroy(localChunksMaterial);
        }

        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }
}
