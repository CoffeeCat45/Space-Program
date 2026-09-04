using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sandbox cube-sphere quadtree: Map mode (textured globe) vs Flight mode (LOD chunk meshes).
/// </summary>
[DisallowMultipleComponent]
public sealed class EarthSandboxQuadtreeManager : MonoBehaviour
{
    public const int MaxLod = 6;

    public const float DefaultSandboxLodFactor = 2.5f;

    public const float LodFactorMin = 0.25f;

    public const float LodFactorMax = 10f;

    public const int DefaultSandboxLoadSpeedPerFrame = 4;

    public const int LoadSpeedMin = 1;

    public const int LoadSpeedMax = 32;

    public const int DefaultSandboxFlightMaxLod = 5;

    public const float DefaultSandboxViewConeHalfAngleDegrees = 75f;

    public const float ViewConeHalfAngleMin = 40f;

    public const float ViewConeHalfAngleMax = 110f;

    const float ViewConeUnloadHysteresisDegrees = 8f;

    [SerializeField]
    float planetRadiusMeters = PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters;

    [SerializeField]
    float visualGizmoRadius = 100f;

    float lodFactor = DefaultSandboxLodFactor;

    [Tooltip("Split when node span exceeds camera distance × this factor (matches main-game PQS).")]
    [SerializeField]
    float splitTargetSpanFactor = 0.35f;

    [Tooltip("Do not subdivide chunks farther than this × visual globe radius from the camera.")]
    [SerializeField]
    float splitDistanceCutoffFraction = 0.85f;

    [Tooltip("Chunks outside this half-angle from the view axis stay coarse.")]
    [SerializeField]
    float peripheralViewHalfAngleDegrees = 36f;

    [Tooltip(
        "Also subdivide chunks on other cube faces when their closest surface point is within " +
        "this angle of the viewer (hides cube-face seams)."
    )]
    [SerializeField]
    float seamDetailHalfAngleDegrees = 52f;

    [Tooltip("Merge only when child distance exceeds split cutoff × this (prevents split/merge flicker).")]
    [SerializeField]
    float mergeDistanceHysteresis = 1.5f;

    [SerializeField]
    int maxMergesPerFrame = 2;

    [Tooltip("Span multiplier for the chunk directly under the camera (<1 = finer).")]
    [SerializeField]
    float viewerSplitSpanScale = 0.62f;

    [Tooltip("Span multiplier for peripheral visible chunks (>1 = coarser).")]
    [SerializeField]
    float peripheralSplitSpanScale = 1.45f;

    [SerializeField]
    bool drawLeafOutlines = true;

    [SerializeField]
    bool drawLodGizmosInFlightMode = true;

    [SerializeField]
    int edgeSubdivisions = 8;

    [SerializeField]
    bool isMapMode = true;

    [Tooltip(
        "Subdivide flight chunks when camera altitude is below this (meters above globe). " +
        "0 = auto (~55% of visual globe radius, matching sandbox orbit range)."
    )]
    [SerializeField]
    float flightSplitMaxAltitudeMeters;

    int flightMaxLod = DefaultSandboxFlightMaxLod;

    [Tooltip("LOD 5 (finest mesh) only when altitude is at or below this (meters above globe).")]
    [SerializeField]
    float lod5MaxAltitudeMeters = 30f;

    [Tooltip("LOD 4 only when altitude is at or below this.")]
    [SerializeField]
    float lod4MaxAltitudeMeters = 65f;

    [Tooltip("LOD 3 only when altitude is at or below this.")]
    [SerializeField]
    float lod3MaxAltitudeMeters = 105f;

    [Tooltip("Higher LOD levels require a closer camera to subdivide further (>1 = finer only when nearer).")]
    [SerializeField]
    float detailLodSplitTightening = 0.18f;

    [Tooltip("Split earlier on cube-face edges and parent-branch boundaries (× lodFactor).")]
    [SerializeField]
    float boundaryLodFactorBoost = 1.25f;

    const float CubeFaceEdgeEpsilon = 0.001f;

    const float NeighborProbeSpanFraction = 0.25f;

    static readonly float[] EdgeNeighborSampleFractions = { 0.25f, 0.5f, 0.75f };

    enum CubeEdgeDirection : byte
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3,
    }

    int maxSplitsPerFrame = DefaultSandboxLoadSpeedPerFrame;

    int maxMeshesPerFrame = DefaultSandboxLoadSpeedPerFrame;

    [Tooltip("Async mesh swaps applied per frame (cheap — main-thread Mesh upload only).")]
    [SerializeField]
    int maxMeshCompletionsPerFrame = 8;

    [Tooltip("Root cube-face shells built per frame when entering flight mode.")]
    [SerializeField]
    int rootShellMeshesPerFrame = 6;

    [Tooltip("Cross-chunk LOD parity splits per frame (prevents split storms at depth 3+).")]
    [SerializeField]
    int maxParitySplitsPerFrame = 4;

    [Tooltip("Do not build new meshes on the hemisphere facing away from the camera.")]
    [SerializeField]
    bool skipBacksideMeshBuilds = true;

    [Tooltip("Half-angle of the view cone around camera look direction (degrees). Driven by debug UI.")]
    float viewConeHalfAngleDegrees = DefaultSandboxViewConeHalfAngleDegrees;

    float meshAllocationHalfAngleDegrees = DefaultSandboxViewConeHalfAngleDegrees;

    float visibleHemisphereHalfAngleDegrees = DefaultSandboxViewConeHalfAngleDegrees;

    float horizonUnloadHalfAngleDegrees =
        DefaultSandboxViewConeHalfAngleDegrees + ViewConeUnloadHysteresisDegrees;

    [SerializeField]
    int maxUnloadMergesPerFrame = 4;

    [SerializeField]
    int maxMeshDestroysPerFrame = 32;

    [Tooltip("Pause non-viewer splits while this many leaves lack meshes.")]
    [SerializeField]
    int maxLeavesWithoutMeshBeforeSplitHold = 64;

    [Tooltip("Pause non-viewer splits while async pipeline backlog exceeds this.")]
    [SerializeField]
    int maxPendingAsyncBeforeSplitHold = 256;

    [Tooltip("LOD levels built synchronously on main thread (avoids async backlog near camera).")]
    [SerializeField]
    int syncFlightMeshBuildMaxLod = 2;

    [Tooltip("LOD 2+ builds on thread pool; only root shell is sync on main thread.")]
    [SerializeField]
    bool useAsyncFlightMeshBuild = true;

    [SerializeField]
    bool showFlightDebugHud = true;

    [SerializeField]
    bool showTextureMemoryHud = true;

    float sandboxMinOrbitDistanceUnity = 105f;
    float sandboxMaxOrbitDistanceUnity = EarthSandboxScene.CameraOrbitUnityMax;

    QuadtreeNode[] _roots;
    Transform _flightChunksRoot;
    Material _chunkMaterial;
    EarthSandboxGlobePaint.ElevationField _elevation;
    readonly List<QuadtreeNode> _leafScratch = new List<QuadtreeNode>(256);
    readonly List<QuadtreeNode> _pendingMeshScratch = new List<QuadtreeNode>(256);
    readonly List<QuadtreeNode> _edgeNeighborScratch = new List<QuadtreeNode>(16);
    Vector3 _lastCameraLocal;
    Vector3 _lastCameraWorldPosition;
    Vector3 _viewerFocusDirectionSim;
    Vector3 _viewerFocusDirectionVisual;
    bool _allowFlightMeshes;
    bool _allowFlightSplits;
    bool _deferMeshBuild;
    bool _buildRootShellOnlyPass;
    bool _flightRootShellReady;
    int _splitBudgetRemaining;
    int _meshBudgetRemaining;
    int _mergeBudgetRemaining;
    int _paritySplitBudgetRemaining;
    int _unloadMergeBudgetRemaining;
    int _meshCullBudgetRemaining;
    int _meshDestroysThisFrame;
    int _meshCompletionBudgetRemaining;
    int _mergesThisFrame;
    int _leavesWithMesh;
    int _leavesWithoutMesh;
    int _leavesWithVisibleMesh;
    int _inFlightLeafBuilds;
    int _internalNodesWithoutMesh;
    int _leavesWithoutMeshNearViewer;
    int _leavesWithoutMeshBlockedBackside;
    int _leavesWithBrokenMesh;
    int _lastPendingMeshQueueCount;
    float _diagnosticLogTimer;
    float _hudFps;

    public float PlanetRadiusMeters => planetRadiusMeters;

    public bool IsMapMode => isMapMode;

    public Transform FlightChunksRoot => _flightChunksRoot;

    public Material ChunkMaterial => _chunkMaterial;

    public EarthSandboxGlobePaint.ElevationField Elevation => _elevation;

    public float VisualGizmoRadius
    {
        get => visualGizmoRadius;
        set => visualGizmoRadius = Mathf.Max(1f, value);
    }

    public int ActiveLeafCount { get; private set; }

    public int LeavesWithMesh => _leavesWithMesh;

    public int LeavesWithVisibleMesh => _leavesWithVisibleMesh;

    public int LeavesWithoutMesh => _leavesWithoutMesh;

    public int PendingAsyncBuilds =>
        EarthSandboxFlightChunkMeshPipeline.PendingCount + _inFlightLeafBuilds;

    public int MergesLastFrame => _mergesThisFrame;

    public float LodFactor
    {
        get => lodFactor;
        set => lodFactor = Mathf.Clamp(value, LodFactorMin, LodFactorMax);
    }

    public int LoadSpeedPerFrame
    {
        get => maxSplitsPerFrame;
        set
        {
            int clamped = Mathf.Clamp(value, LoadSpeedMin, LoadSpeedMax);
            maxSplitsPerFrame = clamped;
            maxMeshesPerFrame = clamped;
        }
    }

    public int FlightMaxLod
    {
        get => flightMaxLod;
        set => flightMaxLod = Mathf.Clamp(value, 1, MaxLod);
    }

    public float ViewConeHalfAngleDegrees
    {
        get => viewConeHalfAngleDegrees;
        set => ApplyViewConeHalfAngleDegrees(value);
    }

    public float ViewConeUnloadHalfAngleDegrees => horizonUnloadHalfAngleDegrees;

    public void ApplySandboxTerrainGraphicsSettings(
        float lodFactorValue,
        int loadSpeedPerFrame,
        int maxLod,
        float viewConeHalfAngle
    )
    {
        LodFactor = lodFactorValue;
        LoadSpeedPerFrame = loadSpeedPerFrame;
        FlightMaxLod = maxLod;
        ViewConeHalfAngleDegrees = viewConeHalfAngle;
    }

    void ApplyViewConeHalfAngleDegrees(float degrees)
    {
        viewConeHalfAngleDegrees = Mathf.Clamp(
            degrees,
            ViewConeHalfAngleMin,
            ViewConeHalfAngleMax
        );
        meshAllocationHalfAngleDegrees = viewConeHalfAngleDegrees;
        visibleHemisphereHalfAngleDegrees = viewConeHalfAngleDegrees;
        horizonUnloadHalfAngleDegrees = viewConeHalfAngleDegrees + ViewConeUnloadHysteresisDegrees;
    }

    public void ConfigureSandboxOrbit(float minOrbitDistanceUnity, float maxOrbitDistanceUnity)
    {
        sandboxMinOrbitDistanceUnity = Mathf.Max(visualGizmoRadius + 1f, minOrbitDistanceUnity);
        sandboxMaxOrbitDistanceUnity = Mathf.Max(
            sandboxMinOrbitDistanceUnity + 1f,
            maxOrbitDistanceUnity
        );
    }

    void OnEnable()
    {
        EnsureInfrastructure();
        EnsureRoots();
    }

    void Start()
    {
        if (!EarthSandboxScene.IsActive())
        {
            return;
        }

        BindMapGlobeRoot();
        ConfigureSandboxOrbit(
            visualGizmoRadius + EarthSandboxScene.CameraOrbitUnityMinPadding,
            EarthSandboxScene.CameraOrbitUnityMax
        );
        ApplyViewConeHalfAngleDegrees(viewConeHalfAngleDegrees);
        ApplyModeState();
    }

    void OnDestroy()
    {
        EarthSandboxFlightChunkMeshPipeline.CancelAll();
        DestroyAllFlightMeshes();
        EarthSandboxGlobePaint.ClearFlightChunkMeshCache();

        if (_chunkMaterial != null)
        {
            Destroy(_chunkMaterial);
            _chunkMaterial = null;
        }
    }

    void Update()
    {
        if (!EarthSandboxScene.IsActive())
        {
            return;
        }

        BindMapGlobeRoot();

        Keyboard keyboard = Keyboard.current;
        if (
            keyboard != null &&
            keyboard.mKey != null &&
            keyboard.mKey.wasPressedThisFrame
        )
        {
            SetMapMode(!isMapMode);
            LogModeState(cameraWorldPosition: GetCameraWorldPosition());
        }

        Vector3 cameraWorldPosition = GetCameraWorldPosition();
        _lastCameraWorldPosition = cameraWorldPosition;
        UpdateModeVisibility();
        _meshCompletionBudgetRemaining = maxMeshCompletionsPerFrame;
        EarthSandboxFlightChunkMeshPipeline.DispatchCompletedMeshes(
            ref _meshCompletionBudgetRemaining
        );

        if (isMapMode)
        {
            return;
        }

        _hudFps = Mathf.Lerp(_hudFps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-5f), 0.12f);

        UpdateFlightLod(cameraWorldPosition);
        _meshCompletionBudgetRemaining = maxMeshCompletionsPerFrame;
        EarthSandboxFlightChunkMeshPipeline.DispatchCompletedMeshes(
            ref _meshCompletionBudgetRemaining
        );

        _diagnosticLogTimer += Time.unscaledDeltaTime;
        if (_diagnosticLogTimer >= 1f)
        {
            _diagnosticLogTimer = 0f;
            Debug.Log(
                "PQS: ActiveLeaves=" + ActiveLeafCount +
                ", WithMesh=" + _leavesWithMesh +
                ", Visible=" + _leavesWithVisibleMesh +
                ", WithoutMesh=" + _leavesWithoutMesh +
                " (near=" + _leavesWithoutMeshNearViewer +
                ", blocked=" + _leavesWithoutMeshBlockedBackside + ")" +
                ", InternalNoMesh=" + _internalNodesWithoutMesh +
                ", MeshQueue=" + _lastPendingMeshQueueCount +
                ", BrokenMesh=" + _leavesWithBrokenMesh +
                ", MeshCache=" + EarthSandboxGlobePaint.FlightChunkMeshCacheCount +
                ", PendingAsync=" + PendingAsyncBuilds +
                ", MergesThisFrame=" + _mergesThisFrame +
                ", MeshCull=" + _meshDestroysThisFrame +
                ", ViewCone=" + viewConeHalfAngleDegrees.ToString("F0") + "°" +
                ", Backpressure=" + (IsSplitBackpressureActive() ? "ON" : "off")
            );
        }
    }

    void OnGUI()
    {
        if (!EarthSandboxScene.IsActive())
        {
            return;
        }

        const int lineHeight = 22;
        int y = 10;

        if (showTextureMemoryHud)
        {
            RuntimeTextureMemoryStats.Snapshot textureMemory =
                RuntimeTextureMemoryStats.GetCachedSnapshot();
            GUI.Label(
                new Rect(10f, y, 720f, lineHeight),
                RuntimeTextureMemoryStats.FormatHudLine(textureMemory)
            );
            y += lineHeight;
        }

        if (!showFlightDebugHud || isMapMode)
        {
            return;
        }

        GUI.Label(
            new Rect(10f, y, 640f, lineHeight),
            "FPS: " + _hudFps.ToString("F0") +
            " | Leaves: " + ActiveLeafCount +
            " | Mesh vis/with/tot: " + _leavesWithVisibleMesh + "/" + _leavesWithMesh + "/" + ActiveLeafCount +
            " | Async: " + PendingAsyncBuilds +
            " | Queue: " + _lastPendingMeshQueueCount +
            (IsSplitBackpressureActive() ? " | HOLD" : string.Empty)
        );
        y += lineHeight;
        GUI.Label(
            new Rect(10f, y, 640f, lineHeight),
            "NoMesh: " + _leavesWithoutMesh +
            " (near=" + _leavesWithoutMeshNearViewer +
            ", blocked=" + _leavesWithoutMeshBlockedBackside + ")" +
            " | IntNoMesh: " + _internalNodesWithoutMesh +
            " | Broken: " + _leavesWithBrokenMesh +
            " | Cache: " + EarthSandboxGlobePaint.FlightChunkMeshCacheCount +
            " | Merges: " + _mergesThisFrame
        );
        y += lineHeight;
        GUI.Label(
            new Rect(10f, y, 520f, lineHeight),
            "Altitude: " + GetCameraAltitudeAboveGlobe(_lastCameraWorldPosition).ToString("F1") + "m" +
            " | MaxLOD: " + GetEffectiveFlightMaxLod() +
            " | View: " + viewConeHalfAngleDegrees.ToString("F0") + "°" +
            " | Mode: FLIGHT"
        );
    }

    void LogModeState(Vector3 cameraWorldPosition)
    {
        float altitude = GetCameraAltitudeAboveGlobe(cameraWorldPosition);
        string modeLabel = isMapMode ?
            "MAP — NASA PNG globe (orbital)" :
            "FLIGHT — white land/ocean chunks (altitude=" + altitude.ToString("F1") + "m, split below " +
            GetEffectiveFlightSplitMaxAltitudeMeters().ToString("F0") + "m)";
        Debug.Log("[EarthSandbox] " + modeLabel);
    }

    void UpdateFlightLod(Vector3 cameraWorldPosition)
    {
        bool wantRootShellOnly = !CanSplitFlightChunksAtCurrentAltitude();

        if (wantRootShellOnly)
        {
            _flightRootShellReady = false;
            EarthSandboxFlightChunkMeshPipeline.CancelAll();

            if (!AllRootsAreLeaves())
            {
                CollapseToRoots();
            }
            else
            {
                ResetAllRootMeshBuildState();
            }

            RefreshLodTree(
                cameraWorldPosition,
                gizmosOnly: false,
                buildRootShellOnly: true
            );
            EnsureCompleteRootShell();
            _flightRootShellReady = HasCompleteRootShell();
            return;
        }

        if (!_flightRootShellReady)
        {
            EarthSandboxFlightChunkMeshPipeline.CancelAll();

            if (!AllRootsAreLeaves())
            {
                CollapseToRoots();
            }
            else
            {
                ResetAllRootMeshBuildState();
            }

            RefreshLodTree(
                cameraWorldPosition,
                gizmosOnly: false,
                buildRootShellOnly: true
            );
            EnsureCompleteRootShell();
            _flightRootShellReady = HasCompleteRootShell();

            if (_flightRootShellReady)
            {
                Debug.Log(
                    "[EarthSandbox] Flight root shell ready — 6 cube faces, altitude=" +
                    GetCameraAltitudeAboveGlobe(cameraWorldPosition).ToString("F1") + "m"
                );
            }

            return;
        }

        RefreshLodTree(
            cameraWorldPosition,
            gizmosOnly: false,
            buildRootShellOnly: false
        );
    }

    void OnDrawGizmos()
    {
        if (!drawLeafOutlines || !isActiveAndEnabled)
        {
            return;
        }

        if (isMapMode == false && !drawLodGizmosInFlightMode)
        {
            return;
        }

        if (!EarthSandboxScene.IsActive() && !Application.isPlaying)
        {
            return;
        }

        EnsureRoots();
        DrawLeafOutlines();
    }

    public void SyncVisualGizmoRadius(float globeRadius)
    {
        visualGizmoRadius = Mathf.Max(1f, globeRadius);
        UpdateFlightChunksScale();
    }

    public void SetMapMode(bool mapMode)
    {
        if (isMapMode == mapMode)
        {
            return;
        }

        isMapMode = mapMode;
        _flightRootShellReady = false;

        if (isMapMode)
        {
            ApplyMapModeState();
        }
        else
        {
            HardResetFlightMode();
        }

        LogModeState(cameraWorldPosition: GetCameraWorldPosition());
    }

    void ApplyMapModeState()
    {
        UpdateModeVisibility();
        EarthSandboxFlightChunkMeshPipeline.CancelAll();
        DestroyAllFlightMeshes();
        CollapseToRoots();
    }

    void HardResetFlightMode()
    {
        UpdateModeVisibility();
        EarthSandboxFlightChunkMeshPipeline.CancelAll();
        DestroyAllFlightMeshes();
        CollapseToRoots();
        EnsureCompleteRootShell();
        _flightRootShellReady = HasCompleteRootShell();

        if (_flightRootShellReady)
        {
            Debug.Log(
                "[EarthSandbox] Flight root shell ready — 6 cube faces, altitude=" +
                GetCameraAltitudeAboveGlobe(GetCameraWorldPosition()).ToString("F1") + "m"
            );
        }
        else
        {
            Debug.LogWarning(
                "[EarthSandbox] Flight root shell incomplete after reset — " +
                ActiveLeafCount + " leaves"
            );
        }
    }

    void ApplyModeState()
    {
        if (isMapMode)
        {
            ApplyMapModeState();
        }
        else
        {
            HardResetFlightMode();
        }
    }

    void UpdateModeVisibility()
    {
        BindMapGlobeRoot();
        EarthSandboxGlobePaint.SetMapGlobeVisible(isMapMode);

        if (_flightChunksRoot != null)
        {
            _flightChunksRoot.gameObject.SetActive(!isMapMode);
        }
    }

    bool IsSplitBackpressureActive()
    {
        return _leavesWithoutMesh > maxLeavesWithoutMeshBeforeSplitHold ||
            PendingAsyncBuilds > maxPendingAsyncBeforeSplitHold;
    }

    bool CanSplitFlightChunksAtCurrentAltitude()
    {
        return GetCameraAltitudeAboveGlobe(_lastCameraWorldPosition) <=
            GetEffectiveFlightSplitMaxAltitudeMeters();
    }

    float GetEffectiveFlightSplitMaxAltitudeMeters()
    {
        if (flightSplitMaxAltitudeMeters > 0f)
        {
            return flightSplitMaxAltitudeMeters;
        }

        return GetSandboxMaxOrbitAltitude() * 0.4f;
    }

    float GetEffectiveFlightMaxLod()
    {
        float altitude = GetCameraAltitudeAboveGlobe(_lastCameraWorldPosition);
        int cap = flightMaxLod;

        if (altitude > lod5MaxAltitudeMeters)
        {
            cap = Mathf.Min(cap, 4);
        }

        if (altitude > lod4MaxAltitudeMeters)
        {
            cap = Mathf.Min(cap, 3);
        }

        if (altitude > lod3MaxAltitudeMeters)
        {
            cap = Mathf.Min(cap, 2);
        }

        return cap;
    }

    float DetailLodSplitTightening(int lod)
    {
        return 1f + (Mathf.Max(0, lod) * detailLodSplitTightening);
    }

    void PropagateAdjacentLodParity(Vector3 cameraLocal)
    {
        _leafScratch.Clear();

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].CollectLeaves(_leafScratch);
        }

        for (int i = 0; i < _leafScratch.Count; i++)
        {
            QuadtreeNode leaf = _leafScratch[i];

            if (!leaf.IsLeaf || leaf.Lod >= GetEffectiveFlightMaxLod())
            {
                continue;
            }

            if (!ExceedsAllowedNeighborLodGap(leaf))
            {
                continue;
            }

            if (!IsChunkNearViewer(cameraLocal, leaf))
            {
                continue;
            }

            if (_paritySplitBudgetRemaining <= 0)
            {
                return;
            }

            if (!leaf.ShouldSplitForLodParity(cameraLocal))
            {
                continue;
            }

            leaf.RequestHighPriorityMeshBuild();

            if (leaf.TryForceParitySplit(cameraLocal))
            {
                _paritySplitBudgetRemaining--;
            }
        }
    }

    bool ExceedsAllowedNeighborLodGap(QuadtreeNode leaf)
    {
        return GetMaxAdjacentLeafLod(leaf) > leaf.Lod + PlanetPqsConstants.MaxNeighborLodGapForMerge;
    }

    static bool IsBoundaryNode(QuadtreeNode node)
    {
        if (TouchesCubeFaceEdge(node))
        {
            return true;
        }

        if (node.Parent == null)
        {
            return true;
        }

        return Mathf.Approximately(node.MinU, node.Parent.MinU) ||
            Mathf.Approximately(node.MaxU, node.Parent.MaxU) ||
            Mathf.Approximately(node.MinV, node.Parent.MinV) ||
            Mathf.Approximately(node.MaxV, node.Parent.MaxV);
    }

    static bool TouchesCubeFaceEdge(QuadtreeNode node)
    {
        return node.MinU <= CubeFaceEdgeEpsilon ||
            node.MaxU >= 1f - CubeFaceEdgeEpsilon ||
            node.MinV <= CubeFaceEdgeEpsilon ||
            node.MaxV >= 1f - CubeFaceEdgeEpsilon;
    }

    int GetMaxAdjacentLeafLod(QuadtreeNode leaf)
    {
        if (_roots == null || leaf == null)
        {
            return leaf != null ? leaf.Lod : 0;
        }

        int maxNeighborLod = leaf.Lod;
        float midU = (leaf.MinU + leaf.MaxU) * 0.5f;
        float midV = (leaf.MinV + leaf.MaxV) * 0.5f;
        float uSpan = Mathf.Max(leaf.MaxU - leaf.MinU, 1e-5f);
        float vSpan = Mathf.Max(leaf.MaxV - leaf.MinV, 1e-5f);

        ConsiderNeighborLeafLod(leaf, midU, leaf.MaxV + (vSpan * NeighborProbeSpanFraction), ref maxNeighborLod);
        ConsiderNeighborLeafLod(leaf, midU, leaf.MinV - (vSpan * NeighborProbeSpanFraction), ref maxNeighborLod);
        ConsiderNeighborLeafLod(leaf, leaf.MaxU + (uSpan * NeighborProbeSpanFraction), midV, ref maxNeighborLod);
        ConsiderNeighborLeafLod(leaf, leaf.MinU - (uSpan * NeighborProbeSpanFraction), midV, ref maxNeighborLod);

        if (TouchesCubeFaceEdge(leaf))
        {
            if (leaf.MinU <= CubeFaceEdgeEpsilon)
            {
                ConsiderCrossFaceNeighborLod(leaf, leaf.MinU, midV, ref maxNeighborLod);
            }

            if (leaf.MaxU >= 1f - CubeFaceEdgeEpsilon)
            {
                ConsiderCrossFaceNeighborLod(leaf, leaf.MaxU, midV, ref maxNeighborLod);
            }

            if (leaf.MinV <= CubeFaceEdgeEpsilon)
            {
                ConsiderCrossFaceNeighborLod(leaf, midU, leaf.MinV, ref maxNeighborLod);
            }

            if (leaf.MaxV >= 1f - CubeFaceEdgeEpsilon)
            {
                ConsiderCrossFaceNeighborLod(leaf, midU, leaf.MaxV, ref maxNeighborLod);
            }
        }

        return maxNeighborLod;
    }

    void ConsiderNeighborLeafLod(
        QuadtreeNode self,
        float probeU,
        float probeV,
        ref int maxNeighborLod
    )
    {
        if (probeU >= 0f && probeU <= 1f && probeV >= 0f && probeV <= 1f)
        {
            QuadtreeNode neighbor = _roots[self.FaceIndex].FindLeafAt(probeU, probeV);

            if (neighbor != null && neighbor != self && neighbor.Lod > maxNeighborLod)
            {
                maxNeighborLod = neighbor.Lod;
            }

            return;
        }

        ConsiderCrossFaceNeighborLod(self, probeU, probeV, ref maxNeighborLod);
    }

    void ConsiderCrossFaceNeighborLod(
        QuadtreeNode self,
        float edgeU,
        float edgeV,
        ref int maxNeighborLod
    )
    {
        float clampedU = Mathf.Clamp01(edgeU);
        float clampedV = Mathf.Clamp01(edgeV);
        Vector3 edgeDirection = PlanetCubeSphere.FaceUvToUnitDirection(
            self.FaceIndex,
            clampedU,
            clampedV
        );
        Vector3 inwardDirection = PlanetCubeSphere.FaceUvToUnitDirection(
            self.FaceIndex,
            Mathf.Clamp(clampedU + ((self.MaxU - self.MinU) * 0.1f), 0f, 1f),
            Mathf.Clamp(clampedV + ((self.MaxV - self.MinV) * 0.1f), 0f, 1f)
        );
        Vector3 tangent = inwardDirection - edgeDirection;

        if (tangent.sqrMagnitude < 1e-8f)
        {
            tangent = Vector3.Cross(edgeDirection, Vector3.up);

            if (tangent.sqrMagnitude < 1e-8f)
            {
                tangent = Vector3.Cross(edgeDirection, Vector3.right);
            }
        }

        Vector3 probeDirection = (edgeDirection + tangent.normalized * 0.02f).normalized;

        if (!PlanetCubeSphere.TryDirectionToFaceUv(
            probeDirection,
            out int neighborFace,
            out float neighborU,
            out float neighborV))
        {
            return;
        }

        QuadtreeNode neighbor = _roots[neighborFace].FindLeafAt(neighborU, neighborV);

        if (neighbor != null && neighbor != self && neighbor.Lod > maxNeighborLod)
        {
            maxNeighborLod = neighbor.Lod;
        }
    }

    bool WouldViolateRestrictedQuadtreeAfterMerge(QuadtreeNode parentNode)
    {
        if (parentNode == null)
        {
            return true;
        }

        return GetMaxAdjacentLeafLod(parentNode) >
            parentNode.Lod + PlanetPqsConstants.MaxNeighborLodGapForMerge;
    }

    bool TryPrepareRestrictedSplit(QuadtreeNode leaf, Vector3 cameraLocal)
    {
        if (leaf == null || !leaf.IsLeaf)
        {
            return false;
        }

        int childLod = leaf.Lod + 1;
        int minRequiredNeighborLod = childLod - PlanetPqsConstants.MaxNeighborLodGapForMerge;
        bool blocked = false;

        for (int edgeIndex = 0; edgeIndex < 4; edgeIndex++)
        {
            CollectDistinctEdgeNeighborLeaves(leaf, (CubeEdgeDirection)edgeIndex, _edgeNeighborScratch);

            for (int i = 0; i < _edgeNeighborScratch.Count; i++)
            {
                QuadtreeNode neighbor = _edgeNeighborScratch[i];

                if (neighbor.Lod >= minRequiredNeighborLod)
                {
                    continue;
                }

                blocked = true;
                neighbor.RequestHighPriorityMeshBuild();
                neighbor.TryForcedRestrictedNeighborSplit(cameraLocal);
            }
        }

        return !blocked;
    }

    void CollectDistinctEdgeNeighborLeaves(
        QuadtreeNode self,
        CubeEdgeDirection edge,
        List<QuadtreeNode> neighbors
    )
    {
        neighbors.Clear();

        float uSpan = Mathf.Max(self.MaxU - self.MinU, 1e-5f);
        float vSpan = Mathf.Max(self.MaxV - self.MinV, 1e-5f);
        float uEpsilon = uSpan * NeighborProbeSpanFraction;
        float vEpsilon = vSpan * NeighborProbeSpanFraction;

        for (int sampleIndex = 0; sampleIndex < EdgeNeighborSampleFractions.Length; sampleIndex++)
        {
            float t = EdgeNeighborSampleFractions[sampleIndex];
            float probeU;
            float probeV;

            switch (edge)
            {
                case CubeEdgeDirection.North:
                    probeU = self.MinU + (uSpan * t);
                    probeV = self.MaxV + vEpsilon;
                    break;
                case CubeEdgeDirection.South:
                    probeU = self.MinU + (uSpan * t);
                    probeV = self.MinV - vEpsilon;
                    break;
                case CubeEdgeDirection.East:
                    probeU = self.MaxU + uEpsilon;
                    probeV = self.MinV + (vSpan * t);
                    break;
                default:
                    probeU = self.MinU - uEpsilon;
                    probeV = self.MinV + (vSpan * t);
                    break;
            }

            if (!TryResolveNeighborLeaf(self, probeU, probeV, out QuadtreeNode neighbor))
            {
                continue;
            }

            if (!neighbors.Contains(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }
    }

    bool TryResolveNeighborLeaf(
        QuadtreeNode self,
        float probeU,
        float probeV,
        out QuadtreeNode neighbor
    )
    {
        neighbor = null;

        if (_roots == null || self == null)
        {
            return false;
        }

        if (probeU >= 0f && probeU <= 1f && probeV >= 0f && probeV <= 1f)
        {
            neighbor = _roots[self.FaceIndex].FindLeafAt(probeU, probeV);

            if (neighbor != null && neighbor != self)
            {
                return true;
            }

            return false;
        }

        float clampedU = Mathf.Clamp01(probeU);
        float clampedV = Mathf.Clamp01(probeV);
        Vector3 edgeDirection = PlanetCubeSphere.FaceUvToUnitDirection(
            self.FaceIndex,
            clampedU,
            clampedV
        );
        Vector3 inwardDirection = PlanetCubeSphere.FaceUvToUnitDirection(
            self.FaceIndex,
            Mathf.Clamp(clampedU + ((self.MaxU - self.MinU) * 0.1f), 0f, 1f),
            Mathf.Clamp(clampedV + ((self.MaxV - self.MinV) * 0.1f), 0f, 1f)
        );
        Vector3 tangent = inwardDirection - edgeDirection;

        if (tangent.sqrMagnitude < 1e-8f)
        {
            tangent = Vector3.Cross(edgeDirection, Vector3.up);

            if (tangent.sqrMagnitude < 1e-8f)
            {
                tangent = Vector3.Cross(edgeDirection, Vector3.right);
            }
        }

        Vector3 probeDirection = (edgeDirection + tangent.normalized * 0.02f).normalized;

        if (!PlanetCubeSphere.TryDirectionToFaceUv(
            probeDirection,
            out int neighborFace,
            out float neighborU,
            out float neighborV))
        {
            return false;
        }

        neighbor = _roots[neighborFace].FindLeafAt(neighborU, neighborV);
        return neighbor != null && neighbor != self;
    }

    float GetSandboxMaxOrbitAltitude()
    {
        return Mathf.Max(0f, sandboxMaxOrbitDistanceUnity - visualGizmoRadius);
    }

    bool HasCompleteRootShell()
    {
        if (_roots == null)
        {
            return false;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            if (!_roots[i].IsLeaf || !_roots[i].HasFlightMesh)
            {
                return false;
            }
        }

        return true;
    }

    bool AllRootsAreLeaves()
    {
        if (_roots == null)
        {
            return true;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            if (!_roots[i].IsLeaf)
            {
                return false;
            }
        }

        return true;
    }

    float GetCameraAltitudeAboveGlobe(Vector3 cameraWorldPosition)
    {
        Vector3 cameraLocal = transform.InverseTransformPoint(cameraWorldPosition);
        return Mathf.Max(0f, cameraLocal.magnitude - visualGizmoRadius);
    }

    void CollapseToRoots()
    {
        if (_roots == null)
        {
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].CollapseToRoot();
        }
    }

    void CollapseStructureToRoots()
    {
        if (_roots == null)
        {
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].CollapseStructureToRoot();
        }
    }

    void ResetAllRootMeshBuildState()
    {
        if (_roots == null)
        {
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].ResetMeshBuildState();
        }
    }

    void EnsureCompleteRootShell()
    {
        EnsureInfrastructure();
        EnsureRoots();

        bool previousRootShellPass = _buildRootShellOnlyPass;
        bool previousAllowMeshes = _allowFlightMeshes;
        _buildRootShellOnlyPass = true;
        _allowFlightMeshes = !isMapMode;

        for (int i = 0; i < _roots.Length; i++)
        {
            if (_roots[i].IsLeaf && !_roots[i].HasFlightMesh)
            {
                _roots[i].SyncFlightMeshImmediate();
            }
        }

        _buildRootShellOnlyPass = previousRootShellPass;
        _allowFlightMeshes = previousAllowMeshes;
    }

    void BindMapGlobeRoot()
    {
        Transform globeRoot = transform.Find("EarthGlobe");

        if (globeRoot != null)
        {
            EarthSandboxGlobePaint.RegisterMapGlobeRoot(globeRoot);
        }
    }

    void EnsureInfrastructure()
    {
        if (_chunkMaterial == null)
        {
            _chunkMaterial = EarthSandboxGlobePaint.CreateFlightChunkMaterial();
        }

        if (_elevation == null)
        {
            _elevation = EarthSandboxGlobePaint.GetSharedElevationField();
        }

        if (_flightChunksRoot == null)
        {
            var flightRootObject = new GameObject("FlightChunks");
            _flightChunksRoot = flightRootObject.transform;
            _flightChunksRoot.SetParent(transform, false);
            UpdateFlightChunksScale();
        }
    }

    void UpdateFlightChunksScale()
    {
        if (_flightChunksRoot == null)
        {
            return;
        }

        float scale = visualGizmoRadius / planetRadiusMeters;
        _flightChunksRoot.localScale = new Vector3(scale, scale, scale);
        _flightChunksRoot.localPosition = Vector3.zero;
        _flightChunksRoot.localRotation = Quaternion.identity;
    }

    void EnsureRoots()
    {
        if (_roots != null && _roots.Length == PlanetCubeSphereCoords.FaceCount)
        {
            return;
        }

        _roots = new QuadtreeNode[PlanetCubeSphereCoords.FaceCount];

        for (int faceIndex = 0; faceIndex < _roots.Length; faceIndex++)
        {
            _roots[faceIndex] = QuadtreeNode.CreateRoot(this, faceIndex);
        }
    }

    void RefreshLodTree(Vector3 cameraWorldPosition, bool gizmosOnly, bool buildRootShellOnly = false)
    {
        EnsureInfrastructure();
        EnsureRoots();
        _lastCameraWorldPosition = cameraWorldPosition;
        Vector3 cameraLocal = transform.InverseTransformPoint(cameraWorldPosition);
        _lastCameraLocal = cameraLocal;
        SyncViewerFocusDirections(cameraLocal);

        bool flightLodActive = !gizmosOnly && !isMapMode;
        _allowFlightMeshes = flightLodActive;
        _allowFlightSplits = (flightLodActive || gizmosOnly) &&
            !buildRootShellOnly &&
            (gizmosOnly || _flightRootShellReady) &&
            CanSplitFlightChunksAtCurrentAltitude();
        _deferMeshBuild = flightLodActive && !buildRootShellOnly;
        _buildRootShellOnlyPass = buildRootShellOnly;
        _splitBudgetRemaining = gizmosOnly ? int.MaxValue :
            buildRootShellOnly ? 0 :
            maxSplitsPerFrame;
        _mergeBudgetRemaining = gizmosOnly ? 0 :
            buildRootShellOnly ? 0 :
            maxMergesPerFrame;
        _meshBudgetRemaining = _allowFlightMeshes ?
            (buildRootShellOnly ? rootShellMeshesPerFrame : maxMeshesPerFrame) :
            0;
        _paritySplitBudgetRemaining = buildRootShellOnly ? 0 : maxParitySplitsPerFrame;
        _mergesThisFrame = 0;
        _meshDestroysThisFrame = 0;
        _unloadMergeBudgetRemaining = buildRootShellOnly || !CanSplitFlightChunksAtCurrentAltitude() ?
            0 :
            maxUnloadMergesPerFrame;
        _meshCullBudgetRemaining = buildRootShellOnly || !CanSplitFlightChunksAtCurrentAltitude() ?
            0 :
            maxMeshDestroysPerFrame;

        // Phase 1: camera basis + distance-driven split/merge traversal.
        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].RefreshLod(cameraLocal);
        }

        // Phase 2: mesh builds sorted by priority and surface distance.
        if (_deferMeshBuild)
        {
            BuildMeshesForNearestLeaves(cameraLocal);
        }

        if (_allowFlightSplits && !buildRootShellOnly)
        {
            // Phase 3: cross-face neighbor LOD parity.
            PropagateAdjacentLodParity(cameraLocal);
            // Phase 5: horizon / view-cone cleanup.
            CullMeshesOutsideViewCone(cameraLocal);
            CullDistantSubtrees(cameraLocal);
        }

        CollectLeaves();
        CollectLeafMeshStats();
        CollectInternalMeshStats();

        // Phase 4: parent coverage swap — hide parent only when all children are ready.
        UpdateAllCoverageMeshStates();
    }

    void UpdateAllCoverageMeshStates()
    {
        if (_roots == null || isMapMode)
        {
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].UpdateCoverageMeshState();
        }
    }

    void CullMeshesOutsideViewCone(Vector3 cameraLocal)
    {
        if (_roots == null || !skipBacksideMeshBuilds || !CanSplitFlightChunksAtCurrentAltitude())
        {
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].CullFlightMeshOutsideViewCone(cameraLocal);
        }
    }

    void CullDistantSubtrees(Vector3 cameraLocal)
    {
        if (_roots == null || !CanSplitFlightChunksAtCurrentAltitude())
        {
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].TryCullDistantSubtree(cameraLocal);
        }
    }

    void CollectLeafMeshStats()
    {
        _leavesWithMesh = 0;
        _leavesWithoutMesh = 0;
        _leavesWithVisibleMesh = 0;
        _inFlightLeafBuilds = 0;
        _leavesWithoutMeshNearViewer = 0;
        _leavesWithoutMeshBlockedBackside = 0;
        _leavesWithBrokenMesh = 0;
        Vector3 cameraLocal = _lastCameraLocal;

        for (int i = 0; i < _leafScratch.Count; i++)
        {
            QuadtreeNode leaf = _leafScratch[i];

            if (leaf.HasBrokenFlightMeshReference())
            {
                _leavesWithBrokenMesh++;
                leaf.RepairBrokenFlightMesh();
            }

            if (leaf.HasFlightMesh)
            {
                _leavesWithMesh++;
                if (leaf.HasRenderableFlightMesh)
                {
                    _leavesWithVisibleMesh++;
                }
            }
            else
            {
                _leavesWithoutMesh++;
                if (IsChunkNearViewer(cameraLocal, leaf))
                {
                    _leavesWithoutMeshNearViewer++;
                }
                else if (!ShouldAllocateMeshForChunk(cameraLocal, leaf))
                {
                    _leavesWithoutMeshBlockedBackside++;
                }
            }

            if (leaf.IsMeshBuildInFlight)
            {
                _inFlightLeafBuilds++;
            }
        }
    }

    void CollectInternalMeshStats()
    {
        _internalNodesWithoutMesh = 0;

        if (_roots == null)
        {
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].CountInternalNodesWithoutMesh(ref _internalNodesWithoutMesh);
        }
    }

    void BuildMeshesForNearestLeaves(Vector3 cameraLocal)
    {
        _pendingMeshScratch.Clear();

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].CollectLeavesNeedingMeshes(_pendingMeshScratch, cameraLocal);
        }

        _lastPendingMeshQueueCount = _pendingMeshScratch.Count;

        _pendingMeshScratch.Sort(
            (left, right) =>
            {
                int priorityCompare = right.MeshBuildPriority.CompareTo(left.MeshBuildPriority);

                if (priorityCompare != 0)
                {
                    return priorityCompare;
                }

                bool leftNear = IsChunkNearViewer(cameraLocal, left);
                bool rightNear = IsChunkNearViewer(cameraLocal, right);

                if (leftNear != rightNear)
                {
                    return leftNear ? -1 : 1;
                }

                int lodCompare = leftNear ?
                    right.Lod.CompareTo(left.Lod) :
                    left.Lod.CompareTo(right.Lod);
                if (lodCompare != 0)
                {
                    return lodCompare;
                }

                int angularCompare = left.AngularDistanceFromViewerDegrees(cameraLocal)
                    .CompareTo(right.AngularDistanceFromViewerDegrees(cameraLocal));
                if (angularCompare != 0)
                {
                    return angularCompare;
                }

                return left.EstimateDistanceToCamera(cameraLocal)
                    .CompareTo(right.EstimateDistanceToCamera(cameraLocal));
            }
        );

        for (int i = 0; i < _pendingMeshScratch.Count && _meshBudgetRemaining > 0; i++)
        {
            _pendingMeshScratch[i].SyncFlightMesh();
        }
    }

    void DestroyAllFlightMeshes()
    {
        if (_roots == null)
        {
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].DestroyFlightMeshRecursive();
        }
    }

    void CollectLeaves()
    {
        _leafScratch.Clear();

        if (_roots == null)
        {
            ActiveLeafCount = 0;
            return;
        }

        for (int i = 0; i < _roots.Length; i++)
        {
            _roots[i].CollectLeaves(_leafScratch);
        }

        ActiveLeafCount = _leafScratch.Count;
    }

    void DrawLeafOutlines()
    {
        CollectLeaves();

        for (int i = 0; i < _leafScratch.Count; i++)
        {
            QuadtreeNode leaf = _leafScratch[i];
            Color color = LodColor(leaf.Lod);
            DrawFaceUvQuad(leaf.FaceIndex, leaf.MinU, leaf.MaxU, leaf.MinV, leaf.MaxV, color);
        }
    }

    void DrawFaceUvQuad(
        int faceIndex,
        float minU,
        float maxU,
        float minV,
        float maxV,
        Color color
    )
    {
        Gizmos.color = color;
        DrawProjectedEdge(faceIndex, minU, minV, maxU, minV);
        DrawProjectedEdge(faceIndex, maxU, minV, maxU, maxV);
        DrawProjectedEdge(faceIndex, maxU, maxV, minU, maxV);
        DrawProjectedEdge(faceIndex, minU, maxV, minU, minV);
    }

    void DrawProjectedEdge(int faceIndex, float u0, float v0, float u1, float v1)
    {
        int segments = Mathf.Max(1, edgeSubdivisions);
        Vector3 previous = transform.TransformPoint(
            FaceUvToLocalMeshPosition(faceIndex, u0, v0)
        );

        for (int segment = 1; segment <= segments; segment++)
        {
            float t = segment / (float)segments;
            Vector3 current = transform.TransformPoint(
                FaceUvToLocalMeshPosition(
                    faceIndex,
                    Mathf.Lerp(u0, u1, t),
                    Mathf.Lerp(v0, v1, t)
                )
            );
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }

    Vector3 FaceUvToLocalMeshPosition(int faceIndex, float u, float v)
    {
        Vector3 simUnit = PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v);
        return PlanetCubeSphere.FlipMeshVertexPositionX(simUnit * visualGizmoRadius);
    }

    Vector3 GetCameraWorldPosition()
    {
        if (Application.isPlaying)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                return mainCamera.transform.position;
            }
        }

#if UNITY_EDITOR
        if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
        {
            return SceneView.lastActiveSceneView.camera.transform.position;
        }
#endif

        return transform.position + new Vector3(0f, 0f, -(visualGizmoRadius * 1.6f));
    }

    float WorldScale => visualGizmoRadius / planetRadiusMeters;

    internal float NodeSizeMeters(int lod)
    {
        int level = Mathf.Clamp(lod, 0, MaxLod);
        return planetRadiusMeters * 2f / (1 << level);
    }

    float ScaledNodeSize(int lod)
    {
        return NodeSizeMeters(lod) * WorldScale;
    }

    float MergeDistanceThreshold(Vector3 cameraLocal, QuadtreeNode node)
    {
        bool viewerChunk = node.ContainsViewerDirection(cameraLocal);
        float splitCutoff = GetSplitDistanceCutoff(cameraLocal, viewerChunk);
        float hysteresis = IsChunkNearViewer(cameraLocal, node) ?
            mergeDistanceHysteresis :
            mergeDistanceHysteresis * 1.1f;
        return splitCutoff * hysteresis;
    }

    float GetSplitDistanceCutoff()
    {
        return visualGizmoRadius * splitDistanceCutoffFraction;
    }

    float GetSplitDistanceCutoff(Vector3 cameraLocal, bool viewerChunk)
    {
        if (viewerChunk)
        {
            return GetSplitDistanceCutoff() * 1.15f;
        }

        return GetSplitDistanceCutoff() * 0.82f;
    }

    float MinChunkSpan()
    {
        return visualGizmoRadius * 0.045f;
    }

    float SplitTargetSpan(
        float distanceToCamera,
        bool viewerChunk,
        bool peripheralVisible,
        QuadtreeNode node
    )
    {
        float effectiveLodFactor = lodFactor;

        if (node != null && IsBoundaryNode(node))
        {
            effectiveLodFactor *= boundaryLodFactorBoost;
        }

        float spanFactor = splitTargetSpanFactor * effectiveLodFactor;

        if (viewerChunk)
        {
            spanFactor *= viewerSplitSpanScale;
        }
        else if (peripheralVisible)
        {
            spanFactor *= peripheralSplitSpanScale;
        }
        else
        {
            spanFactor *= peripheralSplitSpanScale * 1.25f;
        }

        return Mathf.Max(distanceToCamera * spanFactor, MinChunkSpan());
    }

    float SplitTargetSpan(float distanceToCamera, bool viewerChunk, bool peripheralVisible)
    {
        return SplitTargetSpan(distanceToCamera, viewerChunk, peripheralVisible, null);
    }

    bool IsWithinPeripheralViewCone(Vector3 cameraLocal, Vector3 chunkCenterLocal)
    {
        Vector3 viewAxis = GetViewAxisDirectionSim();
        Vector3 chunkDirection = MeshDirectionToSim(chunkCenterLocal);
        float angle = Vector3.Angle(viewAxis, chunkDirection);
        return angle <= peripheralViewHalfAngleDegrees;
    }

    bool IsChunkNearViewer(Vector3 cameraLocal, QuadtreeNode node)
    {
        if (node.ContainsViewerDirection(cameraLocal))
        {
            return true;
        }

        if (IsWithinPeripheralViewCone(cameraLocal, node.GetCenterMeshDirectionLocal()))
        {
            return true;
        }

        return GetClosestAngularDistanceToChunkDegrees(
            cameraLocal,
            node.FaceIndex,
            node.MinU,
            node.MaxU,
            node.MinV,
            node.MaxV
        ) <= seamDetailHalfAngleDegrees;
    }

    bool ShouldBlockSplitForVisibility(Vector3 cameraLocal, QuadtreeNode node)
    {
        Vector3 viewAxis = GetViewAxisDirectionSim();
        Vector3 chunkDirection = node.GetCenterUnitDirectionSim();
        float angle = Vector3.Angle(viewAxis, chunkDirection);

        if (angle > 90f)
        {
            return true;
        }

        return angle > viewConeHalfAngleDegrees + ViewConeUnloadHysteresisDegrees;
    }

    float ComputeViewWeight(Vector3 cameraLocal, QuadtreeNode node)
    {
        Vector3 viewAxis = GetViewAxisDirectionSim();
        Vector3 chunkDirection = node.GetCenterUnitDirectionSim();
        float angle = Vector3.Angle(viewAxis, chunkDirection);
        float focusHalfAngle = viewConeHalfAngleDegrees * 0.5f;

        if (angle <= focusHalfAngle)
        {
            return 1f;
        }

        if (angle >= viewConeHalfAngleDegrees)
        {
            return 2.5f;
        }

        float blend = (angle - focusHalfAngle) / Mathf.Max(1e-3f, viewConeHalfAngleDegrees - focusHalfAngle);
        return Mathf.Lerp(1f, 2.5f, blend);
    }

    float GetSplitDistanceThreshold(QuadtreeNode node, Vector3 cameraLocal)
    {
        float nodeSize = ScaledNodeSize(node.Lod);
        float viewWeight = ComputeViewWeight(cameraLocal, node);
        return nodeSize * lodFactor * viewWeight;
    }

    bool ShouldAllocateMeshForChunk(Vector3 cameraLocal, QuadtreeNode node)
    {
        if (!skipBacksideMeshBuilds || !CanSplitFlightChunksAtCurrentAltitude())
        {
            return true;
        }

        if (IsChunkNearViewer(cameraLocal, node))
        {
            return true;
        }

        return GetClosestAngularDistanceToChunkDegrees(
            cameraLocal,
            node.FaceIndex,
            node.MinU,
            node.MaxU,
            node.MinV,
            node.MaxV
        ) <= meshAllocationHalfAngleDegrees;
    }

    bool IsChunkFacingCamera(Vector3 cameraLocal, QuadtreeNode node)
    {
        return GetClosestAngularDistanceToChunkDegrees(
            cameraLocal,
            node.FaceIndex,
            node.MinU,
            node.MaxU,
            node.MinV,
            node.MaxV
        ) <= visibleHemisphereHalfAngleDegrees;
    }

    bool IsChunkBehindHorizon(Vector3 cameraLocal, QuadtreeNode node)
    {
        if (!CanSplitFlightChunksAtCurrentAltitude() || node.IsRootShellNode())
        {
            return false;
        }

        return GetClosestAngularDistanceToChunkDegrees(
            cameraLocal,
            node.FaceIndex,
            node.MinU,
            node.MaxU,
            node.MinV,
            node.MaxV
        ) > horizonUnloadHalfAngleDegrees;
    }

    float GetClosestAngularDistanceToChunkDegrees(
        Vector3 cameraLocal,
        int faceIndex,
        float minU,
        float maxU,
        float minV,
        float maxV
    )
    {
        Vector3 viewAxis = GetViewAxisDirectionSim();
        float closestAngle = float.MaxValue;
        AccumulateClosestAngleSample(ref closestAngle, viewAxis, faceIndex, minU, minV);
        AccumulateClosestAngleSample(ref closestAngle, viewAxis, faceIndex, maxU, minV);
        AccumulateClosestAngleSample(ref closestAngle, viewAxis, faceIndex, maxU, maxV);
        AccumulateClosestAngleSample(ref closestAngle, viewAxis, faceIndex, minU, maxV);
        AccumulateClosestAngleSample(
            ref closestAngle,
            viewAxis,
            faceIndex,
            (minU + maxU) * 0.5f,
            (minV + maxV) * 0.5f
        );
        AccumulateClosestAngleSample(
            ref closestAngle,
            viewAxis,
            faceIndex,
            (minU + maxU) * 0.5f,
            minV
        );
        AccumulateClosestAngleSample(
            ref closestAngle,
            viewAxis,
            faceIndex,
            (minU + maxU) * 0.5f,
            maxV
        );
        AccumulateClosestAngleSample(
            ref closestAngle,
            viewAxis,
            faceIndex,
            minU,
            (minV + maxV) * 0.5f
        );
        AccumulateClosestAngleSample(
            ref closestAngle,
            viewAxis,
            faceIndex,
            maxU,
            (minV + maxV) * 0.5f
        );
        return closestAngle;
    }

    public void SetViewerFocusDirectionSim(Vector3 viewerFocusDirectionSim)
    {
        _viewerFocusDirectionSim = viewerFocusDirectionSim.sqrMagnitude > 1e-8f ?
            viewerFocusDirectionSim.normalized :
            Vector3.zero;
    }

    void SyncViewerFocusDirections(Vector3 cameraLocal)
    {
        Vector3 focusSim = _viewerFocusDirectionSim;

        if (focusSim.sqrMagnitude < 0.5f)
        {
            focusSim = GetViewerSurfaceDirectionSim(cameraLocal);
        }
        else
        {
            focusSim = focusSim.normalized;
        }

        _viewerFocusDirectionSim = focusSim;
        _viewerFocusDirectionVisual = PlanetCubeSphere.SimDirectionToMeshFrame(focusSim);
    }

    internal Vector3 GetViewerFocusDirectionSimForChunkLookup()
    {
        return _viewerFocusDirectionSim.sqrMagnitude > 0.5f ?
            _viewerFocusDirectionSim :
            GetViewerSurfaceDirectionSim(_lastCameraLocal);
    }

    internal Vector3 GetViewerFocusDirectionVisual()
    {
        return _viewerFocusDirectionVisual.sqrMagnitude > 0.5f ?
            _viewerFocusDirectionVisual :
            Vector3.forward;
    }

    internal Vector3 GetViewAxisDirectionSim()
    {
        if (Application.isPlaying)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null &&
                PlanetLodViewPolicy.TryGetCameraViewSurfaceUnitMesh(
                    transform,
                    mainCamera,
                    visualGizmoRadius,
                    out Vector3 outwardMesh))
            {
                return MeshDirectionToSim(outwardMesh);
            }
        }

        return GetViewerSurfaceDirectionSim(_lastCameraLocal);
    }

    static void AccumulateClosestAngleSample(
        ref float closestAngle,
        Vector3 viewerDirection,
        int faceIndex,
        float u,
        float v
    )
    {
        Vector3 chunkDirection = PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v);
        float angle = Vector3.Angle(viewerDirection, chunkDirection);
        closestAngle = Mathf.Min(closestAngle, angle);
    }

    internal Vector3 GetViewerSurfaceDirectionSim(Vector3 cameraLocal)
    {
        if (cameraLocal.sqrMagnitude < 1e-6f)
        {
            return Vector3.forward;
        }

        return MeshDirectionToSim(cameraLocal);
    }

    internal static Vector3 MeshDirectionToSim(Vector3 meshDirection)
    {
        if (meshDirection.sqrMagnitude < 1e-6f)
        {
            return Vector3.forward;
        }

        return PlanetCubeSphere.FlipMeshVertexPositionX(meshDirection.normalized);
    }

    bool TryConsumeMergeBudget()
    {
        if (_mergeBudgetRemaining <= 0)
        {
            return false;
        }

        _mergeBudgetRemaining--;
        _mergesThisFrame++;
        return true;
    }

    bool TryConsumeMeshCullBudget()
    {
        if (_meshCullBudgetRemaining <= 0)
        {
            return false;
        }

        _meshCullBudgetRemaining--;
        _meshDestroysThisFrame++;
        return true;
    }

    bool TryConsumeUnloadMergeBudget()
    {
        if (_unloadMergeBudgetRemaining <= 0)
        {
            return false;
        }

        _unloadMergeBudgetRemaining--;
        _mergesThisFrame++;
        return true;
    }

    bool TryConsumeSplitBudget()
    {
        if (_splitBudgetRemaining <= 0)
        {
            return false;
        }

        _splitBudgetRemaining--;
        return true;
    }

    bool TryConsumeMeshBudget()
    {
        if (_meshBudgetRemaining <= 0)
        {
            return false;
        }

        _meshBudgetRemaining--;
        return true;
    }

    static float DistancePointToBounds(Vector3 point, Bounds bounds)
    {
        Vector3 closest = bounds.ClosestPoint(point);
        return Vector3.Distance(point, closest);
    }

    internal float EstimateArcDistanceToChunk(
        Vector3 viewerDirection,
        int faceIndex,
        float minU,
        float maxU,
        float minV,
        float maxV
    )
    {
        float closestArcDistance = float.MaxValue;
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            (minU + maxU) * 0.5f,
            (minV + maxV) * 0.5f
        );
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            minU,
            minV
        );
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            maxU,
            minV
        );
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            maxU,
            maxV
        );
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            minU,
            maxV
        );
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            (minU + maxU) * 0.5f,
            minV
        );
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            (minU + maxU) * 0.5f,
            maxV
        );
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            minU,
            (minV + maxV) * 0.5f
        );
        AccumulateClosestArcSample(
            ref closestArcDistance,
            viewerDirection,
            faceIndex,
            visualGizmoRadius,
            maxU,
            (minV + maxV) * 0.5f
        );
        return closestArcDistance;
    }

    static void AccumulateClosestArcSample(
        ref float closestArcDistance,
        Vector3 viewerDirection,
        int faceIndex,
        float planetRadius,
        float u,
        float v
    )
    {
        Vector3 unitDirection = PlanetCubeSphere.FaceUvToUnitDirection(faceIndex, u, v);
        float arcDistance = PlanetCubeSphere.SurfaceDistanceMeters(
            viewerDirection,
            unitDirection,
            planetRadius
        );
        closestArcDistance = Mathf.Min(closestArcDistance, arcDistance);
    }

    internal float EstimateDistanceToCamera(
        Vector3 cameraLocal,
        int faceIndex,
        float minU,
        float maxU,
        float minV,
        float maxV
    )
    {
        float altitude = Mathf.Max(cameraLocal.magnitude - visualGizmoRadius, 1f);
        Vector3 viewerDirection = GetViewerSurfaceDirectionSim(cameraLocal);
        float arcDistance = EstimateArcDistanceToChunk(
            viewerDirection,
            faceIndex,
            minU,
            maxU,
            minV,
            maxV
        );
        return Mathf.Sqrt((altitude * altitude) + (arcDistance * arcDistance));
    }

    static Color LodColor(int lod)
    {
        switch (lod)
        {
            case 0:
                return Color.white;
            case 1:
                return Color.yellow;
            case 2:
                return Color.red;
            case 3:
                return new Color(1f, 0.5f, 0f);
            case 4:
                return Color.magenta;
            case 5:
                return Color.cyan;
            default:
                return Color.green;
        }
    }

    sealed class QuadtreeNode
    {
        public readonly int FaceIndex;
        public readonly int Lod;
        public readonly float MinU;
        public readonly float MaxU;
        public readonly float MinV;
        public readonly float MaxV;
        public readonly Bounds BoundsLocal;
        public readonly Vector3 CenterLocal;

        public QuadtreeNode Parent;
        public QuadtreeNode NorthWest;
        public QuadtreeNode NorthEast;
        public QuadtreeNode SouthWest;
        public QuadtreeNode SouthEast;

        GameObject _chunkObject;
        Mesh _chunkMesh;

        bool _meshBuildInFlight;
        int _meshBuildGeneration;
        int _meshBuildPriority;

        readonly EarthSandboxQuadtreeManager _manager;

        public int MeshBuildPriority => _meshBuildPriority;

        public void RequestHighPriorityMeshBuild()
        {
            _meshBuildPriority = 1;
        }

        public Vector3 GetCenterUnitDirectionSim()
        {
            float centerU = (MinU + MaxU) * 0.5f;
            float centerV = (MinV + MaxV) * 0.5f;
            Vector3 centerUnitDirection = PlanetCubeSphere.FaceUvToUnitDirection(
                FaceIndex,
                centerU,
                centerV
            );

            if (centerUnitDirection.sqrMagnitude <= 1e-12f)
            {
                return Vector3.forward;
            }

            return centerUnitDirection.normalized;
        }

        public Vector3 GetCenterMeshDirectionLocal()
        {
            return PlanetCubeSphere.FlipMeshVertexPositionX(GetCenterUnitDirectionSim());
        }

        public bool IsMeshBuildInFlight => _meshBuildInFlight;

        public bool IsLeaf =>
            NorthWest == null &&
            NorthEast == null &&
            SouthWest == null &&
            SouthEast == null;

        QuadtreeNode(
            EarthSandboxQuadtreeManager manager,
            int faceIndex,
            int lod,
            float minU,
            float maxU,
            float minV,
            float maxV,
            QuadtreeNode parent
        )
        {
            _manager = manager;
            FaceIndex = faceIndex;
            Lod = lod;
            MinU = minU;
            MaxU = maxU;
            MinV = minV;
            MaxV = maxV;
            Parent = parent;

            Vector3 corner00 = manager.FaceUvToLocalMeshPosition(faceIndex, minU, minV);
            Vector3 corner10 = manager.FaceUvToLocalMeshPosition(faceIndex, maxU, minV);
            Vector3 corner11 = manager.FaceUvToLocalMeshPosition(faceIndex, maxU, maxV);
            Vector3 corner01 = manager.FaceUvToLocalMeshPosition(faceIndex, minU, maxV);
            Bounds bounds = new Bounds(corner00, Vector3.zero);
            bounds.Encapsulate(corner10);
            bounds.Encapsulate(corner11);
            bounds.Encapsulate(corner01);
            BoundsLocal = bounds;
            CenterLocal = bounds.center;
        }

        public static QuadtreeNode CreateRoot(EarthSandboxQuadtreeManager manager, int faceIndex)
        {
            return new QuadtreeNode(manager, faceIndex, 0, 0f, 1f, 0f, 1f, null);
        }

        public void CollapseToRoot()
        {
            CollapseStructureToRoot();
            DestroyFlightMesh();
        }

        public void CollapseStructureToRoot()
        {
            if (!IsLeaf)
            {
                SouthWest?.DestroyFlightMeshRecursive();
                SouthEast?.DestroyFlightMeshRecursive();
                NorthWest?.DestroyFlightMeshRecursive();
                NorthEast?.DestroyFlightMeshRecursive();
                SouthWest = null;
                SouthEast = null;
                NorthWest = null;
                NorthEast = null;
            }
        }

        public bool HasFlightMesh => HasValidFlightMesh;

        public bool HasValidFlightMesh =>
            _chunkObject != null &&
            _chunkMesh != null &&
            _chunkObject.TryGetComponent(out MeshFilter meshFilter) &&
            meshFilter.sharedMesh != null;

        public bool HasRenderableFlightMesh =>
            HasValidFlightMesh && _chunkObject.activeSelf;

        public bool HasBrokenFlightMeshReference() =>
            _chunkObject != null && !HasValidFlightMesh;

        public void RepairBrokenFlightMesh()
        {
            DestroyFlightMesh();
        }

        public bool IsRootShellNode()
        {
            return Parent == null && Lod == 0;
        }

        public QuadtreeNode FindLeafAt(float u, float v)
        {
            if (IsLeaf)
            {
                return this;
            }

            float midU = (MinU + MaxU) * 0.5f;
            float midV = (MinV + MaxV) * 0.5f;

            if (v < midV)
            {
                return u < midU ?
                    SouthWest.FindLeafAt(u, v) :
                    SouthEast.FindLeafAt(u, v);
            }

            return u < midU ?
                NorthWest.FindLeafAt(u, v) :
                NorthEast.FindLeafAt(u, v);
        }

        public void RefreshLod(Vector3 cameraLocal)
        {
            if (!IsLeaf)
            {
                SouthWest?.RefreshLod(cameraLocal);
                SouthEast?.RefreshLod(cameraLocal);
                NorthWest?.RefreshLod(cameraLocal);
                NorthEast?.RefreshLod(cameraLocal);

                if (_manager._allowFlightSplits && ShouldMerge(cameraLocal))
                {
                    if (_manager.TryConsumeMergeBudget())
                    {
                        Merge();
                    }
                }

                return;
            }

            if (_manager._allowFlightSplits && ShouldSplit(cameraLocal))
            {
                if (_manager.IsSplitBackpressureActive())
                {
                    TrySyncFlightMesh();
                    return;
                }

                if (!TryEnsureMeshBeforeSplit(cameraLocal))
                {
                    return;
                }

                bool priority = ContainsViewerDirection(cameraLocal);

                if (!priority && !_manager.TryConsumeSplitBudget())
                {
                    return;
                }

                if (!TrySplitWithRestrictedQuadtree(cameraLocal))
                {
                    return;
                }

                return;
            }

            TrySyncFlightMesh();
        }

        bool TryEnsureMeshBeforeSplit(Vector3 cameraLocal)
        {
            if (HasFlightMesh)
            {
                return true;
            }

            if (_meshBuildInFlight)
            {
                return false;
            }

            if (!_manager.ShouldAllocateMeshForChunk(cameraLocal, this))
            {
                return true;
            }

            BuildFlightMeshSyncNow();
            return HasFlightMesh;
        }

        bool TrySplitWithRestrictedQuadtree(Vector3 cameraLocal)
        {
            if (!_manager.TryPrepareRestrictedSplit(this, cameraLocal))
            {
                return false;
            }

            Split();
            SouthWest?.RefreshLod(cameraLocal);
            SouthEast?.RefreshLod(cameraLocal);
            NorthWest?.RefreshLod(cameraLocal);
            NorthEast?.RefreshLod(cameraLocal);
            return true;
        }

        public bool TryForcedRestrictedNeighborSplit(Vector3 cameraLocal)
        {
            if (!_manager._allowFlightSplits || !IsLeaf)
            {
                return false;
            }

            if (Lod >= _manager.GetEffectiveFlightMaxLod())
            {
                return false;
            }

            RequestHighPriorityMeshBuild();

            if (!TryEnsureMeshBeforeSplit(cameraLocal))
            {
                return false;
            }

            if (!ContainsViewerDirection(cameraLocal) &&
                !_manager.TryConsumeSplitBudget())
            {
                return false;
            }

            return TrySplitWithRestrictedQuadtree(cameraLocal);
        }

        public void UpdateCoverageMeshState()
        {
            if (IsLeaf)
            {
                return;
            }

            SouthWest?.UpdateCoverageMeshState();
            SouthEast?.UpdateCoverageMeshState();
            NorthWest?.UpdateCoverageMeshState();
            NorthEast?.UpdateCoverageMeshState();

            // Keep parent coverage visible until Merge() destroys children. Hiding parents
            // when children exist caused large black holes — leaf tiles never fully tile a quad.
            EnsureParentCoverageActive();
        }

        public void TryCullDistantSubtree(Vector3 cameraLocal)
        {
            if (!IsLeaf)
            {
                SouthWest?.TryCullDistantSubtree(cameraLocal);
                SouthEast?.TryCullDistantSubtree(cameraLocal);
                NorthWest?.TryCullDistantSubtree(cameraLocal);
                NorthEast?.TryCullDistantSubtree(cameraLocal);
                TryHorizonMerge(cameraLocal);
            }
        }

        public void CullFlightMeshOutsideViewCone(Vector3 cameraLocal)
        {
            if (!IsLeaf)
            {
                SouthWest?.CullFlightMeshOutsideViewCone(cameraLocal);
                SouthEast?.CullFlightMeshOutsideViewCone(cameraLocal);
                NorthWest?.CullFlightMeshOutsideViewCone(cameraLocal);
                NorthEast?.CullFlightMeshOutsideViewCone(cameraLocal);
            }

            if (IsRootShellNode())
            {
                return;
            }

            if (!_manager.IsChunkBehindHorizon(cameraLocal, this))
            {
                return;
            }

            if (_meshBuildInFlight)
            {
                if (_manager.TryConsumeMeshCullBudget())
                {
                    CancelFlightMeshBuild();
                }

                return;
            }

            if (!HasFlightMesh)
            {
                return;
            }

            if (_manager.TryConsumeMeshCullBudget())
            {
                DestroyFlightMesh();
            }
        }

        void EnsureParentCoverageActive()
        {
            if (HasBrokenFlightMeshReference())
            {
                RepairBrokenFlightMesh();
            }

            if (_manager.skipBacksideMeshBuilds &&
                _manager.CanSplitFlightChunksAtCurrentAltitude() &&
                _manager.IsChunkBehindHorizon(_manager._lastCameraLocal, this))
            {
                if (HasFlightMesh || _meshBuildInFlight)
                {
                    DestroyFlightMesh();
                }

                return;
            }

            if (AllChildrenAreLeaves() && AllChildrenHaveMeshes())
            {
                SetFlightMeshVisible(false);
                return;
            }

            SetFlightMeshVisible(true);

            if (!HasFlightMesh && !_meshBuildInFlight)
            {
                BuildCoverageMeshImmediate();
            }
        }

        void SetFlightMeshVisible(bool visible)
        {
            if (_chunkObject != null)
            {
                _chunkObject.SetActive(visible);
            }
        }

        void BuildCoverageMeshImmediate()
        {
            if (!_manager._allowFlightMeshes || _chunkObject != null || _meshBuildInFlight)
            {
                return;
            }

            if (_manager.skipBacksideMeshBuilds &&
                _manager.CanSplitFlightChunksAtCurrentAltitude() &&
                _manager.IsChunkBehindHorizon(_manager._lastCameraLocal, this))
            {
                return;
            }

            if (_manager.IsChunkNearViewer(_manager._lastCameraLocal, this))
            {
                BuildFlightMeshSyncNow();
                return;
            }

            BuildFlightMeshNow();
        }

        public void CountInternalNodesWithoutMesh(ref int count)
        {
            if (IsLeaf)
            {
                return;
            }

            if (!HasFlightMesh && !_meshBuildInFlight)
            {
                count++;
            }

            SouthWest?.CountInternalNodesWithoutMesh(ref count);
            SouthEast?.CountInternalNodesWithoutMesh(ref count);
            NorthWest?.CountInternalNodesWithoutMesh(ref count);
            NorthEast?.CountInternalNodesWithoutMesh(ref count);
        }

        bool TryHorizonMerge(Vector3 cameraLocal)
        {
            if (IsLeaf || IsRootShellNode())
            {
                return false;
            }

            if (!_manager.IsChunkBehindHorizon(cameraLocal, this))
            {
                return false;
            }

            if (!AllChildrenAreLeaves())
            {
                return false;
            }

            if (!AllChildrenBehindHorizon(cameraLocal))
            {
                return false;
            }

            if (HasChildMeshBuildInFlight())
            {
                return false;
            }

            if (!_manager.TryConsumeUnloadMergeBudget())
            {
                return false;
            }

            Merge();
            QuadtreeNode ancestor = Parent;
            while (ancestor != null && ancestor.TryHorizonMerge(cameraLocal))
            {
                ancestor = ancestor.Parent;
            }

            return true;
        }

        bool AllChildrenBehindHorizon(Vector3 cameraLocal)
        {
            return SouthWest != null &&
                _manager.IsChunkBehindHorizon(cameraLocal, SouthWest) &&
                SouthEast != null &&
                _manager.IsChunkBehindHorizon(cameraLocal, SouthEast) &&
                NorthWest != null &&
                _manager.IsChunkBehindHorizon(cameraLocal, NorthWest) &&
                NorthEast != null &&
                _manager.IsChunkBehindHorizon(cameraLocal, NorthEast);
        }

        public void CollectLeavesNeedingMeshes(List<QuadtreeNode> leaves, Vector3 cameraLocal)
        {
            if (IsLeaf)
            {
                if (_chunkObject == null &&
                    !_meshBuildInFlight &&
                    _manager.ShouldAllocateMeshForChunk(cameraLocal, this))
                {
                    leaves.Add(this);
                }

                return;
            }

            SouthWest?.CollectLeavesNeedingMeshes(leaves, cameraLocal);
            SouthEast?.CollectLeavesNeedingMeshes(leaves, cameraLocal);
            NorthWest?.CollectLeavesNeedingMeshes(leaves, cameraLocal);
            NorthEast?.CollectLeavesNeedingMeshes(leaves, cameraLocal);
        }

        public float EstimateDistanceToCamera(Vector3 cameraLocal)
        {
            Vector3 targetSurfaceLocal = GetTargetSurfacePositionLocal();
            return Vector3.Distance(cameraLocal, targetSurfaceLocal);
        }

        Vector3 GetTargetSurfacePositionLocal()
        {
            Vector3 centerUnitDirection = GetCenterUnitDirectionSim();
            double centerHeightMeters = SampleCenterElevationMeters(centerUnitDirection);
            float heightScale = PlanetSurfaceConstants.UnityVisualHeightScale;
            float planetRadius = _manager.planetRadiusMeters;
            Vector3 targetSurfacePos = centerUnitDirection * (
                planetRadius + (float)(centerHeightMeters * heightScale)
            );
            targetSurfacePos = PlanetCubeSphere.FlipMeshVertexPositionX(targetSurfacePos);

            float localScale = _manager.visualGizmoRadius / planetRadius;
            return targetSurfacePos * localScale;
        }

        double SampleCenterElevationMeters(Vector3 simUnitDirection)
        {
            EarthSandboxGlobePaint.ElevationField elevation = _manager.Elevation;

            if (elevation != null && elevation.IsOpen)
            {
                Vector2 geographicUv = PlanetCubeSphere.DirectionToGeographicUv(simUnitDirection);
                return elevation.SampleElevationMetersAtGeographicUv(geographicUv.x, geographicUv.y);
            }

            SimVector3 direction = new SimVector3(
                simUnitDirection.x,
                simUnitDirection.y,
                simUnitDirection.z
            ).Normalize();

            return PlanetFlightElevationSampling.SampleElevationMetersAtDirection(direction);
        }

        public bool ContainsViewerDirection(Vector3 cameraLocal)
        {
            return PlanetCubeSphere.IsDirectionInsideFaceUvBounds(
                _manager.GetViewerFocusDirectionSimForChunkLookup(),
                FaceIndex,
                MinU,
                MaxU,
                MinV,
                MaxV
            );
        }

        public bool IsNearViewerChunk(Vector3 cameraLocal)
        {
            if (_manager.IsChunkNearViewer(cameraLocal, this))
            {
                return true;
            }

            if (Parent != null && Parent.HasSubdividedSibling(this))
            {
                return true;
            }

            if (ShouldSplitForAdjacentLodGap(cameraLocal))
            {
                return true;
            }

            return false;
        }

        public float AngularDistanceFromViewerDegrees(Vector3 cameraLocal)
        {
            return _manager.GetClosestAngularDistanceToChunkDegrees(
                cameraLocal,
                FaceIndex,
                MinU,
                MaxU,
                MinV,
                MaxV
            );
        }

        public bool HasFlightMeshInTree()
        {
            if (HasFlightMesh)
            {
                return true;
            }

            return (SouthWest != null && SouthWest.HasFlightMeshInTree()) ||
                (SouthEast != null && SouthEast.HasFlightMeshInTree()) ||
                (NorthWest != null && NorthWest.HasFlightMeshInTree()) ||
                (NorthEast != null && NorthEast.HasFlightMeshInTree());
        }

        bool IsPriorityForceSplit(Vector3 cameraLocal)
        {
            if (Lod >= _manager.GetEffectiveFlightMaxLod())
            {
                return false;
            }

            if (!ContainsViewerDirection(cameraLocal))
            {
                return false;
            }

            if (!_manager.CanSplitFlightChunksAtCurrentAltitude())
            {
                return false;
            }

            return _manager.ScaledNodeSize(Lod) > _manager.MinChunkSpan() * 0.85f;
        }

        void TrySyncFlightMesh()
        {
            if (!_manager._deferMeshBuild)
            {
                SyncFlightMesh();
            }
        }

        public void CollectLeaves(List<QuadtreeNode> leaves)
        {
            if (IsLeaf)
            {
                leaves.Add(this);
                return;
            }

            SouthWest?.CollectLeaves(leaves);
            SouthEast?.CollectLeaves(leaves);
            NorthWest?.CollectLeaves(leaves);
            NorthEast?.CollectLeaves(leaves);
        }

        public void DestroyFlightMeshRecursive()
        {
            DestroyFlightMesh();
            SouthWest?.DestroyFlightMeshRecursive();
            SouthEast?.DestroyFlightMeshRecursive();
            NorthWest?.DestroyFlightMeshRecursive();
            NorthEast?.DestroyFlightMeshRecursive();
        }

        bool ShouldSplit(Vector3 cameraLocal)
        {
            if (_manager.ShouldBlockSplitForVisibility(cameraLocal, this))
            {
                return false;
            }

            if (_manager.skipBacksideMeshBuilds &&
                _manager.CanSplitFlightChunksAtCurrentAltitude() &&
                !_manager.ShouldAllocateMeshForChunk(cameraLocal, this) &&
                !ContainsViewerDirection(cameraLocal))
            {
                return false;
            }

            if (IsPriorityForceSplit(cameraLocal))
            {
                return true;
            }

            if (ShouldSplitForSiblingBalance(cameraLocal))
            {
                return true;
            }

            if (ShouldSplitForLodParity(cameraLocal))
            {
                return true;
            }

            if (Lod >= _manager.GetEffectiveFlightMaxLod())
            {
                return false;
            }

            if (!_manager.CanSplitFlightChunksAtCurrentAltitude())
            {
                return false;
            }

            float distanceToCamera = EstimateDistanceToCamera(cameraLocal);
            float splitThreshold = _manager.GetSplitDistanceThreshold(this, cameraLocal);
            return distanceToCamera < splitThreshold;
        }

        public bool ShouldSplitForLodParity(Vector3 cameraLocal)
        {
            if (!IsLeaf)
            {
                return false;
            }

            if (Lod >= _manager.GetEffectiveFlightMaxLod())
            {
                return false;
            }

            if (!_manager.ExceedsAllowedNeighborLodGap(this))
            {
                return false;
            }

            return _manager.IsChunkNearViewer(cameraLocal, this);
        }

        public bool TryForceParitySplit(Vector3 cameraLocal)
        {
            if (!_manager._allowFlightSplits || !IsLeaf)
            {
                return false;
            }

            if (!ShouldSplitForLodParity(cameraLocal) &&
                !ShouldSplitForSiblingBalance(cameraLocal))
            {
                return false;
            }

            RequestHighPriorityMeshBuild();

            if (!TryEnsureMeshBeforeSplit(cameraLocal))
            {
                return false;
            }

            if (!ContainsViewerDirection(cameraLocal) &&
                !_manager.TryConsumeSplitBudget())
            {
                return false;
            }

            return TrySplitWithRestrictedQuadtree(cameraLocal);
        }

        bool ShouldSplitForSiblingBalance(Vector3 cameraLocal)
        {
            if (!IsLeaf || Parent == null)
            {
                return false;
            }

            if (Lod >= _manager.GetEffectiveFlightMaxLod())
            {
                return false;
            }

            if (!_manager.IsChunkNearViewer(cameraLocal, this))
            {
                return false;
            }

            return Parent.HasSubdividedSibling(this);
        }

        bool ShouldSplitForAdjacentLodGap(Vector3 cameraLocal)
        {
            return ShouldSplitForLodParity(cameraLocal);
        }

        bool HasSubdividedSibling(QuadtreeNode self)
        {
            return (SouthWest != null && SouthWest != self && !SouthWest.IsLeaf) ||
                (SouthEast != null && SouthEast != self && !SouthEast.IsLeaf) ||
                (NorthWest != null && NorthWest != self && !NorthWest.IsLeaf) ||
                (NorthEast != null && NorthEast != self && !NorthEast.IsLeaf);
        }

        bool ShouldMerge(Vector3 cameraLocal)
        {
            if (IsLeaf)
            {
                return false;
            }

            if (!AllChildrenAreLeaves())
            {
                return false;
            }

            if (!AllChildrenHaveMeshes())
            {
                return false;
            }

            if (HasChildMeshBuildInFlight())
            {
                return false;
            }

            if (_manager.WouldViolateRestrictedQuadtreeAfterMerge(this))
            {
                return false;
            }

            if (_manager.IsChunkNearViewer(cameraLocal, this))
            {
                return false;
            }

            if (SouthWest != null && _manager.IsChunkNearViewer(cameraLocal, SouthWest))
            {
                return false;
            }

            if (SouthEast != null && _manager.IsChunkNearViewer(cameraLocal, SouthEast))
            {
                return false;
            }

            if (NorthWest != null && _manager.IsChunkNearViewer(cameraLocal, NorthWest))
            {
                return false;
            }

            if (NorthEast != null && _manager.IsChunkNearViewer(cameraLocal, NorthEast))
            {
                return false;
            }

            if (SouthWest != null &&
                _manager.ExceedsAllowedNeighborLodGap(SouthWest))
            {
                return false;
            }

            if (SouthEast != null &&
                _manager.ExceedsAllowedNeighborLodGap(SouthEast))
            {
                return false;
            }

            if (NorthWest != null &&
                _manager.ExceedsAllowedNeighborLodGap(NorthWest))
            {
                return false;
            }

            if (NorthEast != null &&
                _manager.ExceedsAllowedNeighborLodGap(NorthEast))
            {
                return false;
            }

            if (SouthWest != null &&
                _manager.GetMaxAdjacentLeafLod(SouthWest) > Lod)
            {
                return false;
            }

            if (SouthEast != null &&
                _manager.GetMaxAdjacentLeafLod(SouthEast) > Lod)
            {
                return false;
            }

            if (NorthWest != null &&
                _manager.GetMaxAdjacentLeafLod(NorthWest) > Lod)
            {
                return false;
            }

            if (NorthEast != null &&
                _manager.GetMaxAdjacentLeafLod(NorthEast) > Lod)
            {
                return false;
            }

            if (ContainsViewerDirection(cameraLocal))
            {
                return false;
            }

            if (SouthWest != null &&
                SouthWest.EstimateDistanceToCamera(cameraLocal) <=
                _manager.MergeDistanceThreshold(cameraLocal, SouthWest))
            {
                return false;
            }

            if (SouthEast != null &&
                SouthEast.EstimateDistanceToCamera(cameraLocal) <=
                _manager.MergeDistanceThreshold(cameraLocal, SouthEast))
            {
                return false;
            }

            if (NorthWest != null &&
                NorthWest.EstimateDistanceToCamera(cameraLocal) <=
                _manager.MergeDistanceThreshold(cameraLocal, NorthWest))
            {
                return false;
            }

            if (NorthEast != null &&
                NorthEast.EstimateDistanceToCamera(cameraLocal) <=
                _manager.MergeDistanceThreshold(cameraLocal, NorthEast))
            {
                return false;
            }

            return true;
        }

        bool AllChildrenAreLeaves()
        {
            return SouthWest != null && SouthWest.IsLeaf &&
                SouthEast != null && SouthEast.IsLeaf &&
                NorthWest != null && NorthWest.IsLeaf &&
                NorthEast != null && NorthEast.IsLeaf;
        }

        bool AllChildrenHaveMeshes()
        {
            return SouthWest != null && SouthWest.HasFlightMesh &&
                SouthEast != null && SouthEast.HasFlightMesh &&
                NorthWest != null && NorthWest.HasFlightMesh &&
                NorthEast != null && NorthEast.HasFlightMesh;
        }

        bool AnyChildHasMesh()
        {
            return (SouthWest != null && SouthWest.HasFlightMesh) ||
                (SouthEast != null && SouthEast.HasFlightMesh) ||
                (NorthWest != null && NorthWest.HasFlightMesh) ||
                (NorthEast != null && NorthEast.HasFlightMesh);
        }

        bool HasChildMeshBuildInFlight()
        {
            return (SouthWest != null && SouthWest.IsMeshBuildInFlight) ||
                (SouthEast != null && SouthEast.IsMeshBuildInFlight) ||
                (NorthWest != null && NorthWest.IsMeshBuildInFlight) ||
                (NorthEast != null && NorthEast.IsMeshBuildInFlight);
        }

        void Split()
        {
            if (!IsLeaf || Lod >= _manager.GetEffectiveFlightMaxLod())
            {
                return;
            }

            float midU = (MinU + MaxU) * 0.5f;
            float midV = (MinV + MaxV) * 0.5f;
            int childLod = Lod + 1;

            SouthWest = new QuadtreeNode(_manager, FaceIndex, childLod, MinU, midU, MinV, midV, this);
            SouthEast = new QuadtreeNode(_manager, FaceIndex, childLod, midU, MaxU, MinV, midV, this);
            NorthWest = new QuadtreeNode(_manager, FaceIndex, childLod, MinU, midU, midV, MaxV, this);
            NorthEast = new QuadtreeNode(_manager, FaceIndex, childLod, midU, MaxU, midV, MaxV, this);
            SetFlightMeshVisible(true);
        }

        void Merge()
        {
            SouthWest?.DestroyFlightMesh();
            SouthEast?.DestroyFlightMesh();
            NorthWest?.DestroyFlightMesh();
            NorthEast?.DestroyFlightMesh();

            SouthWest = null;
            SouthEast = null;
            NorthWest = null;
            NorthEast = null;

            SyncFlightMeshImmediate();
            SetFlightMeshVisible(true);
        }

        public void ResetMeshBuildState()
        {
            CancelFlightMeshBuild();
        }

        public void SyncFlightMeshImmediate()
        {
            if (!_manager._allowFlightMeshes)
            {
                CancelFlightMeshBuild();
                return;
            }

            if (!IsLeaf && !HasFlightMesh)
            {
                return;
            }

            if (!IsLeaf && HasFlightMesh)
            {
                return;
            }

            if (_chunkObject != null || _meshBuildInFlight)
            {
                return;
            }

            BuildFlightMeshNow();
        }

        public void SyncFlightMesh()
        {
            if (!_manager._allowFlightMeshes)
            {
                CancelFlightMeshBuild();
                return;
            }

            if (HasBrokenFlightMeshReference())
            {
                RepairBrokenFlightMesh();
            }

            if (!IsLeaf && !HasFlightMesh)
            {
                return;
            }

            if (!IsLeaf && HasFlightMesh)
            {
                return;
            }

            if (_chunkObject != null || _meshBuildInFlight)
            {
                return;
            }

            if (!_manager.TryConsumeMeshBudget())
            {
                return;
            }

            BuildFlightMeshNow();
        }

        void BuildFlightMeshNow()
        {
            if (HasBrokenFlightMeshReference())
            {
                RepairBrokenFlightMesh();
            }

            if (_chunkObject != null || _meshBuildInFlight)
            {
                return;
            }

            int gridResolution = EarthSandboxGlobePaint.FlightChunkGridResolutionForLod(Lod);
            var request = new EarthSandboxFlightChunkMeshPipeline.ChunkMeshBuildRequest(
                FaceIndex,
                MinU,
                MaxU,
                MinV,
                MaxV,
                Lod,
                gridResolution,
                _manager.PlanetRadiusMeters
            );

            if (EarthSandboxFlightChunkMeshPipeline.TryGetCachedMesh(request, out Mesh cachedMesh))
            {
                AttachFlightMesh(cachedMesh);
                return;
            }

            if (_manager._buildRootShellOnlyPass ||
                !_manager.useAsyncFlightMeshBuild ||
                Lod <= _manager.syncFlightMeshBuildMaxLod)
            {
                BuildFlightMeshSyncNow();
                return;
            }

            _meshBuildInFlight = true;
            int generation = ++_meshBuildGeneration;
            PlanetElevationMmapField elevationField = _manager.Elevation != null ?
                _manager.Elevation.GetMmapFieldForAsyncSampling() :
                null;

            EarthSandboxFlightChunkMeshPipeline.StartBuild(
                request,
                elevationField,
                generation,
                () => IsMeshBuildStillValid(generation),
                mesh =>
                {
                    if (!IsMeshBuildStillValid(generation))
                    {
                        _meshBuildInFlight = false;
                        return;
                    }

                    _meshBuildInFlight = false;
                    AttachFlightMesh(mesh);
                },
                () =>
                {
                    if (generation == _meshBuildGeneration)
                    {
                        _meshBuildInFlight = false;
                    }
                }
            );
        }

        void BuildFlightMeshSyncNow()
        {
            if (HasBrokenFlightMeshReference())
            {
                RepairBrokenFlightMesh();
            }

            if (_chunkObject != null || _meshBuildInFlight)
            {
                return;
            }

            int gridResolution = EarthSandboxGlobePaint.FlightChunkGridResolutionForLod(Lod);
            var request = new EarthSandboxFlightChunkMeshPipeline.ChunkMeshBuildRequest(
                FaceIndex,
                MinU,
                MaxU,
                MinV,
                MaxV,
                Lod,
                gridResolution,
                _manager.PlanetRadiusMeters
            );

            if (EarthSandboxFlightChunkMeshPipeline.TryGetCachedMesh(request, out Mesh cachedMesh))
            {
                AttachFlightMesh(cachedMesh);
                return;
            }

            Mesh syncMesh = EarthSandboxGlobePaint.BuildFlightChunkMesh(
                FaceIndex,
                MinU,
                MaxU,
                MinV,
                MaxV,
                _manager.PlanetRadiusMeters,
                _manager.Elevation,
                gridResolution,
                Lod
            );
            AttachFlightMesh(syncMesh);
        }

        bool IsMeshBuildStillValid(int generation)
        {
            return generation == _meshBuildGeneration &&
                _manager._allowFlightMeshes &&
                IsLeaf &&
                _chunkObject == null;
        }

        void AttachFlightMesh(Mesh sourceMesh)
        {
            if (sourceMesh == null || _chunkObject != null)
            {
                return;
            }

            _chunkMesh = Object.Instantiate(sourceMesh);
            _chunkMesh.name = sourceMesh.name + "_Live";
            _chunkObject = new GameObject(BuildChunkObjectName());
            _chunkObject.transform.SetParent(_manager.FlightChunksRoot, false);
            _chunkObject.transform.localPosition = Vector3.zero;
            _chunkObject.transform.localRotation = Quaternion.identity;
            _chunkObject.transform.localScale = Vector3.one;
            _chunkObject.SetActive(true);

            MeshFilter meshFilter = _chunkObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = _chunkObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = _chunkMesh;
            meshRenderer.sharedMaterial = _manager.ChunkMaterial;
            _meshBuildPriority = 0;
            NotifyParentChildMeshReady();
        }

        void NotifyParentChildMeshReady()
        {
            if (Parent == null || !Parent.AllChildrenAreLeaves())
            {
                return;
            }

            if (Parent.AllChildrenHaveMeshes())
            {
                Parent.SetFlightMeshVisible(false);
            }
        }

        void CancelFlightMeshBuild()
        {
            _meshBuildGeneration++;
            _meshBuildInFlight = false;
        }

        void DestroyFlightMesh()
        {
            CancelFlightMeshBuild();

            if (_chunkObject != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_chunkObject);
                }
                else
                {
                    Object.DestroyImmediate(_chunkObject);
                }

                _chunkObject = null;
            }

            if (_chunkMesh != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_chunkMesh);
                }
                else
                {
                    Object.DestroyImmediate(_chunkMesh);
                }

                _chunkMesh = null;
            }
        }

        string BuildChunkObjectName()
        {
            return "FlightChunk_F" + FaceIndex + "_L" + Lod;
        }
    }
}
