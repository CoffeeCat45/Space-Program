using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public struct NextOrbitalEventInfo
{
    public string name;
    public double time;
    public bool hasEvent;
}

public enum LockstepDiagState
{
    None,
    PauseFromServer,
    WaitingAccumulator,
    BufferLow,
    MissingPacket,
    ExecutingTick,
}

public struct GraphicsBridgeDebugSnapshot
{
    public bool IsMapMode;
    public Vector3 ShipUnityPosition;
    public Vector3 PlanetUnityPosition;
    public Vector3 ShipUnityLocalScale;
    public Vector3 PlanetUnityLocalScale;
    public float UnitySceneDistance;
}

public class SimulationRunner : MonoBehaviour
{
    [Header("Simulation")]
    public SimulationScenario scenario = SimulationScenario.PlanetSurfaceStart;
    public double fixedDt = 0.02;
    public double timeScale = 1.0;

    [Header("Ship Control")]
    public float shipTurnSpeedDegrees = 120.0f;
    public float shipAutoTurnSpeedDegrees = 120.0f;
    public float shipThrustForce = 60.0f;
    public float throttle = 1.0f;
    public float throttleChangeSpeed = 0.75f;

    [Header("Maneuver Planning")]
    public double plannedDeltaVStep = 0.25;
    public int circularizeFiniteBurnIterations = 4;

    [Header("Preview Analysis")]
    public int previewAnalysisStepCount = 1500;
    public double previewAnalysisDt = 0.2;
    public float previewAnalysisUpdateInterval = 1.5f;

    [Header("Warp")]
    public float warpToPointTimeScale = 50.0f;
    public double maneuverWarpLeadTime = 60.0;

    [Header("Target")]
    public string targetBodyName = "Moon";
    public string orbitFocusBodyName = "Ship";
    string lastMapCameraFocusBodyName;
    public double closestApproachWarpLeadTime = 30.0;
    public double closestApproachSlowdownLeadTime = 120.0;
    public float closestApproachSlowdownTimeScale = 1.0f;
    public float closestApproachWarpSlowdownTimeScale = 5.0f;

    [Header("Environment Visuals")]
    public GameObject launchSitePrefab;

    [Header("Diagnostics")]
    public bool enableBurnDiagnostics = true;
    public bool showPerformanceOverlay = true;
    public float perfOverlayRefreshSeconds = 0.5f;
    public bool enableRenderInterpolation = true;
    /// <summary>
    /// Extra physics steps after a hitch make 1% lows worse (sim×2 on the same long frame).
    /// Catch up on later frames instead.
    /// </summary>
    public int maxSimulationStepsPerFrameAtNormalSpeed = 1;

    [Header("Multiplayer")]
    public bool isMultiplayerMode = false;
    public int assignedVesselId;
    public int diagPacketsReceivedInLoop;
    public int diagPacketsDequeued;
    public LockstepDiagState diagLockstepState = LockstepDiagState.None;
    public int diagLockstepBufferCount;
    public long diagLockstepMissingTick;
    public const int LockstepInputBufferThreshold = 1;
    const int MaxLockstepCatchUpStepsPerFrame = 20;

    bool pendingNetworkStaging;
    bool desyncDetected;
    bool lockstepReady;
    global::SimVector3 localNetworkRcsVector;
    readonly Queue<byte[]> pendingJoinSnapshots = new Queue<byte[]>();
    readonly object networkBufferLock = new object();
    NetworkTransportClient networkTransport;

    Simulation simulation;
    double accumulator;
    double lockstepAccumulator;
    ShipOrientationMode shipOrientationMode = ShipOrientationMode.Manual;
    global::SimVector3 plannedDeltaV;
    global::SimVector3 maneuverNodeDeltaV;
    global::SimVector3 maneuverNodePosition;
    global::SimVector3 cachedPreviewPlannedDeltaV;
    global::SimVector3 cachedPreviewManeuverNodeDeltaV;
    ClosestApproachInfo cachedClosestApproach;
    SphereOfInfluenceEntryPreview cachedSphereOfInfluenceEntryPreview;
    bool isPaused;
    bool isPauseMenuOpen;
    bool pendingNetworkPauseRequested;
    bool pendingPauseToggleToSend;
    bool lockstepTickPaused;
    bool joinSnapshotLoaded;
    bool mpHudMenuOpen;
    bool isInitialized;
    GameSessionMode sessionMode = GameSessionMode.MainMenu;
    bool earthSandboxSessionActive;
    bool isMapModeActive;
    bool hasCachedPreviewAnalysis;
    string lastActivePrimaryBodyName;
    string cachedPreviewTargetBodyName;
    string warpLabel = "point";
    bool hasManeuverNode;
    bool cachedPreviewHasManeuverNode;
    bool autoBurnActive;
    bool autoBurnIsBurning;
    bool warpStopAfterCriticalEvent;
    float previewAnalysisTimer;
    double previewAnalysisSimTimeSinceCache;
    bool previewRebuildSoIPending;
    int previewSoIReadyFrame;
    double warpRemainingTime;
    double autoBurnRemainingDeltaV;
    double maneuverNodeTime;
    double cachedPreviewManeuverNodeTime;
    float cachedPreviewThrustForce;
    double autoBurnDuration;
    double autoBurnRemainingTime;
    PreviewExecutionDriftInfo lastBurnDriftInfo;
    PreviewExecutionDriftInfo lastDescentDriftInfo;
    global::SimVector3[] cachedPreviewBurnWorldPath = Array.Empty<global::SimVector3>();
    global::SimVector3[] cachedDescentPreviewWorldPath = Array.Empty<global::SimVector3>();
    List<global::SimVector3> actualBurnWorldPath = new List<global::SimVector3>();
    List<global::SimVector3> actualDescentWorldPath = new List<global::SimVector3>();
    bool burnDiagnosticTrackingActive;
    bool descentDiagnosticTrackingActive;
    bool wasInAtmosphereLastStep;
    global::SimVector3 diagnosticBurnDeltaV;
    double diagnosticBurnStartTime;
    double diagnosticThrustForce;
    double diagnosticBurnDuration;
    double[] timeScaleSteps = new double[]
    {
        0.25,
        0.5,
        1.0,
        5.0,
        10.0,
        50.0,
        100.0,
        1000.0,
        10000.0
    };
    string[] targetBodyNames = new string[] { "Moon", "Planet" };
    Dictionary<string, GameObject> bodyObjects = new Dictionary<string, GameObject>();
    Transform vesselVisualRoot;
    Transform celestialVisualRoot;
    Transform flightUniverseRoot;
    int lastShipLayoutRevision = -1;
    int lastStationLayoutRevision = -1;
    int lastWorldRevision = -1;
    FlightStatus lastRenderedFlightStatus = FlightStatus.Flying;
    string lastRenderedDockingMessage = string.Empty;
    CameraController cameraController;
    GameObject cameraTargetFocusObject = null;
    SimulationHud simulationHud;
    LaunchPadView launchPadView;
    LaunchSiteConfig.Site? pendingLaunchSiteOverride;
    int? pendingTruthProbeLogIndex;
    readonly SimulationPilotInput pilotInput = new SimulationPilotInput();
    readonly SimulationPlaySession playSession = new SimulationPlaySession();
    readonly Queue<NetworkTickPacket> networkInputBuffer = new Queue<NetworkTickPacket>();
    const string QuicksaveFileName = "quicksave.bin";
    string centerHudMessage = string.Empty;
    float centerHudMessageUntil;
    GUIStyle centerHudStyle;
    GUIStyle centerHudBoxStyle;
    float perfSmoothedFps;
    int perfSimStepsThisFrame;
    int perfSimStepsLastFrame;
    double perfAccumulatorMs;
    float perfOverlayNextRefreshTime;
    float perfSampleFpsSum;
    int perfSampleFrameCount;
    float perfSampleMaxFrameMs;
    int perfSampleMaxSimSteps;
    double perfSampleMaxDebtMs;
    float perfDisplayFps;
    float perfDisplayOnePercentLowFps;
    int perfDisplaySimSteps;
    double perfDisplayDebtMs;
    double perfDisplaySimAdvanceMs;
    double perfDisplayMaxFrameMs;
    const int PerfFrameTimeWindow = 300;
    readonly float[] perfFrameTimesMs = new float[PerfFrameTimeWindow];
    readonly float[] perfFrameTimesSortScratch = new float[PerfFrameTimeWindow];
    int perfFrameTimeCount;
    int perfFrameTimeWriteIndex;
    string perfOverlayText = string.Empty;
    string perfTimeScaleOverlayText = string.Empty;
    GUIStyle perfOverlayStyle;
    GUIStyle perfTimeScaleOverlayStyle;
    GUIStyle perfOverlayBoxStyle;
    bool hasShipRenderPrevious;
    global::SimVector3 shipRenderPreviousComWorld;
    global::SimVector3 shipRenderPreviousPosition;
    global::SimVector3 shipRenderPreviousCenterOfMassLocal;
    double shipRenderPreviousHeading;
    global::SimQuaternion shipRenderPreviousOrientation = global::SimQuaternion.Identity();
    global::SimVector3 shipRenderPreviousRadialOut;
    double shipRenderPreviousSpinAngle;
    bool pendingCameraChaseSnap;
    double lastAppliedLaunchTerrainHeight = double.NaN;
    double renderInterpolationT = 1.0;
    GraphicsBridgeDebugSnapshot graphicsBridgeDebug;

    internal Simulation Simulation => simulation;
    internal bool IsMultiplayerMode => isMultiplayerMode;
    internal bool IsLockstepReady => lockstepReady;
    internal ISimulationStateView SimulationState => simulation;
    internal float Throttle
    {
        get => throttle;
        set => throttle = value;
    }
    internal ShipOrientationMode ShipOrientationMode
    {
        get
        {
            Vessel vessel = GetLocalControlledVessel();

            if (vessel != null)
            {
                return vessel.controlState.orientationMode;
            }

            return shipOrientationMode;
        }
        set
        {
            shipOrientationMode = value;
            Vessel vessel = GetLocalControlledVessel();

            if (vessel != null)
            {
                vessel.controlState.orientationMode = value;
            }
        }
    }
    internal float ShipRotationDegrees
    {
        get =>
            simulation == null ?
            0.0f :
            (float)(simulation.GetShipHeadingRadians() * 180.0 / Math.PI);
        set
        {
            if (simulation != null)
            {
                simulation.SetShipHeadingRadians(value * Mathf.Deg2Rad);
            }
        }
    }
    internal float ShipTurnSpeedDegrees => shipTurnSpeedDegrees;
    internal float ShipAutoTurnSpeedDegrees => shipAutoTurnSpeedDegrees;
    internal double ShipTurnSpeedRadiansPerSecond =>
        shipTurnSpeedDegrees * Math.PI / 180.0;
    internal double ShipAutoTurnSpeedRadiansPerSecond =>
        shipAutoTurnSpeedDegrees * Math.PI / 180.0;
    internal float ThrottleChangeSpeed => throttleChangeSpeed;
    internal SimulationPlaySession PlaySession => playSession;
    public bool IsInitialized => isInitialized;

    public bool IsColdStartMenu =>
        sessionMode == GameSessionMode.MainMenu && !isInitialized;
    public bool IsPauseMenuOpen => isPauseMenuOpen;
    public bool IsEarthSandboxSessionActive => earthSandboxSessionActive;

    public bool IsMainMenuVisible =>
        !earthSandboxSessionActive && (IsColdStartMenu || isPauseMenuOpen);
    public bool IsVabEditorActive => sessionMode == GameSessionMode.VabEditor;
    public GameSessionMode SessionMode => sessionMode;
    internal bool IsMapModeActive => isMapModeActive;
    internal GraphicsBridgeDebugSnapshot GetGraphicsBridgeDebug() => graphicsBridgeDebug;

    public void LaunchScenario(SimulationScenario selectedScenario)
    {
        GameSessionBootstrap.PersistRunner(this);
        isPauseMenuOpen = false;
        isPaused = false;
        sessionMode = GameSessionMode.Flight;
        scenario = selectedScenario;
        LaunchSiteConfig.ApplyScenario(scenario);

        playSession.SaveLastScenario(scenario);

        if (VabSessionSceneUtility.IsActiveSceneVab())
        {
            if (FlightSessionSceneUtility.TryLoadFlightScene())
            {
                GameSessionBootstrap.SyncPresentationForRunner(this);
            }
            else
            {
                GameSessionBootstrap.ApplyFlightPresentationInPlace(this);
            }
        }

        ReinitializeSimulation(preserveScenario: true);
    }

    static GameSessionMode startupSessionMode = GameSessionMode.MainMenu;

    public static void SetStartupSessionMode(GameSessionMode mode)
    {
        startupSessionMode = mode;
    }

    public void EnterVabEditor()
    {
        GameSessionBootstrap.PersistRunner(this);
        isPauseMenuOpen = false;
        ShutdownActiveSession();
        SetStartupSessionMode(GameSessionMode.VabEditor);

        if (!VabSessionSceneUtility.TryLoadVabScene())
        {
            SetStartupSessionMode(GameSessionMode.MainMenu);
            return;
        }

        sessionMode = GameSessionMode.VabEditor;
        EnsureVabEditorUi();
        GameSessionBootstrap.SyncPresentationForRunner(this);
    }

    /// <summary>Load isolated Earth asset / globe test scene from the main menu.</summary>
    public void EnterEarthSandbox()
    {
        GameSessionBootstrap.PersistRunner(this);
        isPauseMenuOpen = false;
        ShutdownActiveSession();
        earthSandboxSessionActive = true;

        if (!EarthSandboxScene.TryLoadScene())
        {
            earthSandboxSessionActive = false;
        }
    }

    static void EnsureVabEditorUi()
    {
        VabEditorController[] existingControllers = FindObjectsByType<VabEditorController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (existingControllers.Length > 0)
        {
            return;
        }

        GameObject host = GameObject.Find("SceneLogic");

        if (host == null)
        {
            host = new GameObject("SceneLogic");
        }

        host.AddComponent<VabEditorController>();
    }

    public void ReturnToMainMenu()
    {
        isPauseMenuOpen = false;
        earthSandboxSessionActive = false;
        EarthSandboxScene.ResetSandboxSessionFlags();

        if (sessionMode == GameSessionMode.Flight && isInitialized)
        {
            isPaused = true;
        }

        ShutdownActiveSession();
        sessionMode = GameSessionMode.MainMenu;

        if (VabSessionSceneUtility.IsActiveSceneVab())
        {
            FlightSessionSceneUtility.TryLoadMainMenuBootstrapScene();
        }
        else if (EarthSandboxScene.IsActive())
        {
            if (!FlightSessionSceneUtility.TryLoadMainMenuBootstrapScene())
            {
                EarthSandboxScene.TryReloadSceneWithoutSandboxSession();
            }
        }

        GameSessionBootstrap.SyncPresentationForRunner(this);
    }

    public void TogglePauseMenu()
    {
        if (!isInitialized || sessionMode != GameSessionMode.Flight)
        {
            return;
        }

        if (FlightLodLoadingOverlay.IsBlocking)
        {
            return;
        }

        SetPauseMenuOpen(!isPauseMenuOpen);
    }

    public void ClosePauseMenu()
    {
        SetPauseMenuOpen(false);
    }

    void SetPauseMenuOpen(bool open)
    {
        if (!isInitialized || sessionMode != GameSessionMode.Flight)
        {
            isPauseMenuOpen = false;
            return;
        }

        isPauseMenuOpen = open;
        isPaused = open;

        if (open)
        {
            accumulator = 0.0;
            SnapShipRenderInterpolationToCurrent();
            ApplyFlightUniverseVisualNow();
        }
        else
        {
            SnapShipRenderInterpolationToCurrent();
            ApplyFlightUniverseVisualNow();
        }
    }

    public void LaunchPlanetSurfaceStart()
    {
        LaunchPlanetSurfacePad(SimulationScenario.PlanetSurfaceStart);
    }

    public void LaunchPlanetSurfaceMorocco()
    {
        LaunchPlanetSurfacePad(SimulationScenario.PlanetSurfaceMorocco);
    }

    public void LaunchPlanetSurfaceTibet()
    {
        LaunchPlanetSurfacePad(SimulationScenario.PlanetSurfaceTibet);
    }

    /// <summary>
    /// LOD / mesh-frame diagnostic: 0°N 0°E (sin(lon)=0 → sim.x=0; X-mirror bugs vanish here).
    /// </summary>
    public void LaunchPlanetSurfaceGreenwich()
    {
        LaunchSiteConfig.Site site = LaunchSiteConfig.GreenwichMeridian;
        SimVector3 simNormal = site.GetSurfaceNormal();
        Debug.Log(
            "SimulationRunner: Greenwich LOD test spawn — " +
            site.LatitudeDegrees.ToString("F2") + "°N, " +
            site.LongitudeDegrees.ToString("F2") + "°E. " +
            "simNormal=(" +
            simNormal.x.ToString("F4") + ", " +
            simNormal.y.ToString("F4") + ", " +
            simNormal.z.ToString("F4") + "). " +
            "Expect sim.x≈0; mesh-frame x = -sim.x. " +
            "If max LOD is not under the rocket here, check phase offset (90°/180°), not lon mirror."
        );
        LaunchPlanetSurfacePad(SimulationScenario.PlanetSurfaceGreenwich);
    }

    public void LaunchMoonSurfaceStart()
    {
        Debug.Log("SimulationRunner: Moon near-side pad spawn (0°N 0°E, toy SMA).");
        LaunchPlanetSurfacePad(SimulationScenario.MoonSurfaceStart);
    }

    /// <summary>Diagnostic spawn at ThreePointsOfTruth probe 0..2 (main menu keys 5/6/7).</summary>
    public void LaunchThreePointsTruthProbe(int probeIndex)
    {
        PlanetThreePointsOfTruthDiagnostic.Probe probe =
            PlanetThreePointsOfTruthDiagnostic.GetProbe(probeIndex);
        pendingLaunchSiteOverride = probe.ToLaunchSite();
        pendingTruthProbeLogIndex = probeIndex;
        Debug.Log(
            "SimulationRunner: ThreePointsOfTruth spawn probe " + (probeIndex + 1) + "/3 — " +
            probe.Label + " (" + probe.LatitudeDegrees.ToString("F1") + "°N, " +
            probe.LongitudeDegrees.ToString("F1") + "°E)."
        );
        LaunchPlanetSurfacePad(SimulationScenario.PlanetSurfaceStart);
    }

    void LaunchPlanetSurfacePad(SimulationScenario padScenario)
    {
        LaunchPlanetSurfacePadWithSavedVessel(
            padScenario,
            playSession.GetDefaultPadVesselName()
        );
    }

    public void LaunchPlanetSurfaceStartWithSavedVessel(string vesselName)
    {
        LaunchPlanetSurfacePadWithSavedVessel(
            SimulationScenario.PlanetSurfaceStart,
            vesselName
        );
    }

    void LaunchPlanetSurfacePadWithSavedVessel(
        SimulationScenario padScenario,
        string vesselName
    )
    {
        if (TryLaunchSavedVessel(vesselName, padScenario))
        {
            return;
        }

        Debug.LogWarning(
            "Pad launch: saved vessel '" + vesselName +
            "' was not found. Falling back to default launch ship."
        );
        playSession.ClearOverrideLaunchShipPath();
        LaunchScenario(padScenario);
    }

    bool TryLaunchSavedVessel(string vesselName)
    {
        return TryLaunchSavedVessel(vesselName, SimulationScenario.PlanetSurfaceStart);
    }

    bool TryLaunchSavedVessel(string vesselName, SimulationScenario padScenario)
    {
        if (string.IsNullOrWhiteSpace(vesselName))
        {
            return false;
        }

        try
        {
            int partCount = PartCatalog.Refresh();
            VesselBlueprint blueprint = VabFileSystem.LoadVehicle(vesselName);

            if (blueprint?.blocks == null || blueprint.blocks.Count == 0)
            {
                Debug.LogWarning(
                    "Pad launch: blueprint '" + vesselName + "' has no blocks."
                );
                return false;
            }

            Debug.Log(
                "Pad launch: blueprint '" + vesselName + "' has " +
                blueprint.blocks.Count + " block(s); PartCatalog has " +
                partCount + " template(s)."
            );
            return LaunchVesselFromVab(blueprint, padScenario);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "Pad launch: failed to load vessel '" + vesselName + "': " +
                exception.Message
            );
            return false;
        }
    }

    public void LaunchInclinedOrbit()
    {
        LaunchScenario(SimulationScenario.InclinedOrbit);
    }

    public void LaunchVacuumDocking()
    {
        LaunchScenario(SimulationScenario.DockingApproach);
    }

    public void LaunchVesselFromVab(VesselBlueprint blueprint)
    {
        LaunchVesselFromVab(blueprint, SimulationScenario.PlanetSurfaceStart);
    }

    public bool LaunchVesselFromVab(
        VesselBlueprint blueprint,
        SimulationScenario padScenario
    )
    {
        if (blueprint == null)
        {
            Debug.LogWarning("VAB launch: blueprint is null.");
            return false;
        }

        GameSessionBootstrap.PersistRunner(this);
        PartCatalog.Refresh();

        List<StageData> stages = VabBlueprintExporter.ExportToStages(
            blueprint,
            VabPartStatsBridge.TryLookup,
            VabPartThrustBridge.TryGetEngineThrustFrame
        );

        if (stages == null || stages.Count == 0)
        {
            Debug.LogWarning(
                "VAB launch: vessel '" + blueprint.vesselName +
                "' has no connected parts. Check PartCatalog and blueprint blocks."
            );
            return false;
        }

        string launchPath = playSession.GetVabLaunchShipPath();

        try
        {
            ShipPersistence.SaveShip(launchPath, stages);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("VAB launch: failed to write ship file: " + exception.Message);
            return false;
        }

        playSession.SetOverrideLaunchShipPath(launchPath);
        playSession.SetSessionPadLaunchShipPath(launchPath);
        Debug.Log(
            "Pad launch: loaded vessel '" + blueprint.vesselName +
            "' with " + stages[0].blocks.Count + " block(s) from " + launchPath
        );
        LaunchScenario(padScenario);
        return true;
    }

    void Awake()
    {
#if UNITY_EDITOR
        scenario = playSession.NormalizePlayableScenario(scenario);
#else
        scenario = playSession.GetStartupScenario(scenario);
#endif
        LaunchSiteConfig.ApplyScenario(scenario);
        ApplyStartupSessionModeIfNeeded();
        EnsureHudRunnerBinding();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        scenario = playSession.NormalizePlayableScenario(scenario);

        if (
            Application.isPlaying &&
            isInitialized &&
            simulation != null &&
            simulation.scenario != scenario
        )
        {
            ReinitializeSimulation();
        }
    }
#endif

    internal bool ShouldReleaseGuiTextInputFocus()
    {
        if (simulation == null)
        {
            return false;
        }

        if (isMultiplayerMode)
        {
            if (!lockstepReady)
            {
                return false;
            }

            return !mpHudMenuOpen;
        }

        if (isPaused)
        {
            return false;
        }

        return simulation.GetFlightStatus() == FlightStatus.Flying;
    }

    internal void ToggleNetworkPauseRequest()
    {
        pendingNetworkPauseRequested = !pendingNetworkPauseRequested;
        pendingPauseToggleToSend = true;
    }

    internal bool GetLocalNetworkPauseRequestStatus()
    {
        return pendingNetworkPauseRequested;
    }

    internal bool IsNetworkPauseRequested()
    {
        return pendingNetworkPauseRequested;
    }

    internal void AcknowledgePauseInputSent()
    {
        pendingPauseToggleToSend = false;
    }

    internal bool HasPendingPauseToggleToSend()
    {
        return pendingPauseToggleToSend;
    }

    internal bool IsLockstepTickPaused()
    {
        return isMultiplayerMode && lockstepTickPaused;
    }

    internal void SetAssignedPlayerSlot(int playerSlot)
    {
        assignedVesselId = Math.Clamp(playerSlot, 1, 2);

        if (simulation != null)
        {
            simulation.ClientLocalOrientationVesselId = assignedVesselId;
        }

        TryFinalizeLockstepJoin();
    }

    internal Vessel GetLocalControlledVessel()
    {
        if (simulation == null)
        {
            return null;
        }

        if (assignedVesselId > 0)
        {
            Vessel assignedVessel = simulation.GetVesselById(assignedVesselId);

            if (assignedVessel != null)
            {
                return assignedVessel;
            }
        }

        return simulation.ship;
    }

    internal int GetAssignedPlayerSlot()
    {
        return assignedVesselId;
    }

    void TryFinalizeLockstepJoin()
    {
        if (!isMultiplayerMode || !joinSnapshotLoaded || assignedVesselId <= 0)
        {
            return;
        }

        if (lockstepReady)
        {
            return;
        }

        lockstepReady = true;
        mpHudMenuOpen = false;
        ShowCenterHudMessage(
            "Синхронизация завершена — игрок " + assignedVesselId,
            3.0f
        );
        RebuildVisualWorldAfterLockstepJoin();
    }

    void ResetScenarioThrottle()
    {
        throttle = 0.0f;

        if (simulation == null)
        {
            return;
        }

        simulation.ResetAllVesselPilotControls();
    }

    void RebuildVisualWorldAfterLockstepJoin()
    {
        if (simulation == null)
        {
            return;
        }

        ClearBodyObjects();
        lastWorldRevision = simulation.GetWorldRevision();
        lastShipLayoutRevision = -1;
        lastStationLayoutRevision = -1;
        RefreshAvailableTargets();
        ForceRebuildShipVisual();
        ForceRebuildDockableVisuals();
        RenderBodies();
        ResetScenarioThrottle();
        EnsureDefaultCameraFocusTarget();
        pendingCameraChaseSnap = true;
        SnapShipRenderInterpolationToCurrent();
    }

    internal bool IsSimulationFrozen()
    {
        return !isMultiplayerMode && isPaused;
    }

    internal bool IsMpHudMenuOpen => mpHudMenuOpen;

    void SyncWorldRevision()
    {
        if (simulation == null)
        {
            return;
        }

        int revision = simulation.GetWorldRevision();

        if (revision == lastWorldRevision)
        {
            return;
        }

        ClearBodyObjects();
        lastWorldRevision = revision;
        lastShipLayoutRevision = -1;
        lastStationLayoutRevision = -1;
        SnapShipRenderInterpolationToCurrent();
    }

    void SnapShipRenderInterpolationToCurrent()
    {
        if (simulation == null || simulation.ship == null)
        {
            return;
        }

        shipRenderPreviousComWorld = simulation.ship.GetCenterOfMassWorld();
        shipRenderPreviousPosition = simulation.ship.position;
        shipRenderPreviousCenterOfMassLocal = simulation.ship.centerOfMassLocal;
        shipRenderPreviousHeading = simulation.ship.heading;
        shipRenderPreviousOrientation = simulation.ship.orientation;
        shipRenderPreviousRadialOut = CaptureCurrentShipRadialOutSim();
        shipRenderPreviousSpinAngle = CaptureCurrentPlanetSpinAngle();
        hasShipRenderPrevious = true;
        renderInterpolationT = 1.0;
    }

    internal void ToggleAtmosphericPreview()
    {
        simulation.ToggleAtmosphericPreviewEnabled();
    }

    internal bool IsAtmosphericPreviewEnabled()
    {
        return simulation.IsAtmosphericPreviewEnabled();
    }

    void SyncLocalPlayerControlState()
    {
        Vessel vessel = GetLocalControlledVessel();

        if (vessel == null)
        {
            return;
        }

        vessel.controlState.throttle = GetThrottle();
    }

    void SyncSimulationOrientationInput()
    {
        Vessel localVessel = GetLocalControlledVessel();
        ShipOrientationMode activeMode = localVessel == null ?
            shipOrientationMode :
            localVessel.controlState.orientationMode;
        simulation.orientationInput = new ShipOrientationInput
        {
            mode = activeMode,
            maneuverTargetDirection = GetActivePlannedDeltaV(),
            autoTurnRateRadiansPerSecond = ShipAutoTurnSpeedRadiansPerSecond,
            atmosphericAutoTurnRateMultiplier = 1.5,
        };
        simulation.atmosphericSasEnabled =
            activeMode != ShipOrientationMode.Manual &&
            activeMode != ShipOrientationMode.StabilityAssist &&
            activeMode != ShipOrientationMode.DevStabilityAssist;
    }

    internal void GetNavBallAngles(out double pitch, out double heading, out double roll)
    {
        if (simulation?.ship == null)
        {
            pitch = 0.0;
            heading = 0.0;
            roll = 0.0;
            return;
        }

        simulation.CalculateNavBallAngles(simulation.ship, out pitch, out heading, out roll);
    }
    internal bool IsPaused
    {
        get => isPaused;
        set => isPaused = value;
    }

    static bool startupSessionModeApplied;

    void Start()
    {
        EnsureHudRunnerBinding();
    }

    void ApplyStartupSessionModeIfNeeded()
    {
        if (startupSessionModeApplied)
        {
            return;
        }

        startupSessionModeApplied = true;
        isInitialized = false;

        if (
            VabSessionSceneUtility.IsActiveSceneVab() &&
            startupSessionMode == GameSessionMode.VabEditor
        )
        {
            sessionMode = GameSessionMode.VabEditor;
        }
        else
        {
            sessionMode = GameSessionMode.MainMenu;
        }

        startupSessionMode = GameSessionMode.MainMenu;
    }

    void EnsureHudRunnerBinding()
    {
        simulationHud = GetComponent<SimulationHud>();

        if (simulationHud == null)
        {
            simulationHud = gameObject.AddComponent<SimulationHud>();
        }

        simulationHud.SetRunner(this);
        EnsureTerrainGraphicsDebugPanel();
        EnsureLodLoadingOverlay();
    }

    void EnsureLodLoadingOverlay()
    {
        if (GetComponent<FlightLodLoadingOverlay>() == null)
        {
            gameObject.AddComponent<FlightLodLoadingOverlay>();
        }
    }

    void EnsureTerrainGraphicsDebugPanel()
    {
        if (GetComponent<TerrainGraphicsDebugPanel>() == null)
        {
            gameObject.AddComponent<TerrainGraphicsDebugPanel>();
        }
    }

    void ShutdownActiveSession()
    {
        isInitialized = false;
        isPaused = false;
        isPauseMenuOpen = false;
        isMapModeActive = false;
        simulation = null;
        FlightLodLoadingOverlay.HideActive();

        if (simulationHud != null)
        {
            simulationHud.SetSimulation(null);
        }

        SunVisual sunVisual = Camera.main != null ?
            Camera.main.GetComponent<SunVisual>() :
            null;

        if (sunVisual != null)
        {
            sunVisual.SetSimulation(null, WorldVisualBridge.FlightPositionScale);
        }

        ClearBodyObjects();
        TeardownFlightPresentation();
    }

    internal void TeardownFlightPresentation()
    {
        OrbitPreviewView orbitPreviewView = GetOrbitPreviewView();

        if (orbitPreviewView != null)
        {
            orbitPreviewView.SetMapModeActive(false);
        }

        OrbitOverlayCamera.SetMapModeActive(Camera.main, false);

        if (launchPadView != null)
        {
            launchPadView.Teardown();
        }

        if (flightUniverseRoot != null)
        {
            flightUniverseRoot.gameObject.SetActive(false);
        }

        if (celestialVisualRoot != null)
        {
            celestialVisualRoot.gameObject.SetActive(false);
        }

        if (vesselVisualRoot != null)
        {
            vesselVisualRoot.gameObject.SetActive(false);
        }
    }

    internal void RestoreFlightPresentation()
    {
        Transform universeRoot = GetOrCreateFlightUniverseRoot();

        if (universeRoot != null)
        {
            universeRoot.gameObject.SetActive(true);
        }

        if (celestialVisualRoot != null)
        {
            celestialVisualRoot.gameObject.SetActive(true);
        }

        if (vesselVisualRoot != null)
        {
            vesselVisualRoot.gameObject.SetActive(true);
        }
    }

    void ReinitializeSimulation(bool preserveScenario = false)
    {
        OrbitPreviewView orbitPreviewView = GetOrbitPreviewView();

        if (orbitPreviewView != null)
        {
            orbitPreviewView.SetMapModeActive(false);
        }

        isInitialized = false;
        isMapModeActive = false;
        lastMapCameraFocusBodyName = null;
        orbitFocusBodyName = "Ship";
        hasCachedPreviewAnalysis = false;
        previewAnalysisTimer = 0.0f;
        previewAnalysisSimTimeSinceCache = 0.0;
        previewRebuildSoIPending = false;
        previewSoIReadyFrame = 0;
        warpRemainingTime = 0.0;
        warpStopAfterCriticalEvent = false;
        autoBurnActive = false;
        autoBurnIsBurning = false;
        descentDiagnosticTrackingActive = false;
        wasInAtmosphereLastStep = false;
        cachedDescentPreviewWorldPath = Array.Empty<global::SimVector3>();
        actualDescentWorldPath.Clear();
        lastDescentDriftInfo = new PreviewExecutionDriftInfo();
        accumulator = 0.0;
        lockstepAccumulator = 0.0;
        desyncDetected = false;
        lockstepReady = false;
        joinSnapshotLoaded = false;

        lock (networkBufferLock)
        {
            networkInputBuffer.Clear();
            pendingJoinSnapshots.Clear();
        }
        ClearBodyObjects();
        InitializeSimulation(preserveScenario);
    }

    public void RespawnVessel()
    {
        if (simulation == null)
        {
            return;
        }

        simulation.RespawnActiveVessel(GetLaunchShipPathForRespawn());
        playSession.ResetMissionTracking();
        warpRemainingTime = 0.0;
        warpStopAfterCriticalEvent = false;
        autoBurnActive = false;
        autoBurnIsBurning = false;
        descentDiagnosticTrackingActive = false;
        wasInAtmosphereLastStep = false;
        cachedDescentPreviewWorldPath = Array.Empty<global::SimVector3>();
        actualDescentWorldPath.Clear();
        lastDescentDriftInfo = new PreviewExecutionDriftInfo();
        hasCachedPreviewAnalysis = false;
        throttle = 0.0f;
        ClearShipTrail();
        ForceRebuildShipVisual();
        RebuildPreviewAnalysis();
    }

    string GetLaunchShipPathForRespawn()
    {
        if (!SimulationScenarios.IsAnySurfacePad(scenario))
        {
            return string.Empty;
        }

        return playSession.ResolveLaunchShipPath();
    }

    void ForceRebuildShipVisual()
    {
        if (simulation == null)
        {
            return;
        }

        if (
            !bodyObjects.TryGetValue("Ship", out GameObject shipObject) ||
            shipObject == null
        )
        {
            return;
        }

        RenderVesselBlockVisual(
            shipObject,
            simulation.ship,
            ref lastShipLayoutRevision,
            true
        );
    }

    void ForceRebuildDockableVisuals()
    {
        if (simulation == null)
        {
            return;
        }

        for (int i = 0; i < simulation.dockableVessels.Count; i++)
        {
            Vessel dockable = simulation.dockableVessels[i];

            if (
                !bodyObjects.TryGetValue(dockable.name, out GameObject dockableObject) ||
                dockableObject == null
            )
            {
                continue;
            }

            if (dockable.name == "Station")
            {
                RenderVesselBlockVisual(
                    dockableObject,
                    dockable,
                    ref lastStationLayoutRevision
                );
            }
        }
    }

    void RenderVesselBlockVisual(
        GameObject vesselObject,
        Vessel vessel,
        ref int lastLayoutRevision,
        bool forceRebuild = false,
        global::SimVector3? centerOfMassOverride = null
    )
    {
        BodyView.EnsureVesselVisualAttachments(vesselObject, vessel.name);
        ShipBlockView[] blocks = vessel.GetShipBlockViews();

        if (blocks == null || blocks.Length == 0)
        {
            BodyView.ClearBlockVisuals(vesselObject);
            lastLayoutRevision = vessel.shipLayoutRevision;
            return;
        }

        global::SimVector3 centerOfMassLocal =
            centerOfMassOverride ?? vessel.centerOfMassLocal;

        if (forceRebuild || vessel.shipLayoutRevision != lastLayoutRevision)
        {
            BodyView.RebuildBlocks(
                vesselObject,
                blocks,
                centerOfMassLocal,
                anchorVesselRootAtCom: !isMapModeActive
            );
            lastLayoutRevision = vessel.shipLayoutRevision;
        }

        BodyView.UpdateBlockTransforms(
            vesselObject,
            blocks,
            centerOfMassLocal,
            anchorVesselRootAtCom: !isMapModeActive
        );
    }

    Transform GetOrCreateFlightUniverseRoot()
    {
        FlightUniverseRoot universe = FlightUniverseRoot.Ensure(this);
        flightUniverseRoot = universe != null ? universe.RootTransform : null;
        return flightUniverseRoot;
    }

    internal Transform GetFlightUniverseRoot()
    {
        if (flightUniverseRoot == null)
        {
            return GetOrCreateFlightUniverseRoot();
        }

        return flightUniverseRoot;
    }

    internal void ApplyFlightUniverseVisualNow()
    {
        Transform universeRoot = GetFlightUniverseRoot();

        if (universeRoot == null)
        {
            return;
        }

        FlightUniverseMovement movement = universeRoot.GetComponent<FlightUniverseMovement>();
        movement?.ApplyVisualNow();
    }

    internal void RefreshLaunchPadVisual()
    {
        launchPadView?.RefreshPadTransformForRender();
    }

    internal Transform GetShipVisualTransform()
    {
        if (
            bodyObjects != null &&
            bodyObjects.TryGetValue("Ship", out GameObject shipObject) &&
            shipObject != null
        )
        {
            return shipObject.transform;
        }

        return null;
    }

    internal global::SimVector3 GetShipCenterOfMassWorldForVisuals()
    {
        return GetInterpolatedShipCenterOfMassWorld();
    }

    internal bool TryGetPlanetVisualTransform(out Transform planetTransform)
    {
        planetTransform = null;

        if (bodyObjects == null)
        {
            return false;
        }

        string lodName = CelestialTerrainRuntime.LodBodyName;

        if (
            bodyObjects.TryGetValue(lodName, out GameObject lodObject) &&
            lodObject != null
        )
        {
            planetTransform = lodObject.transform;
            return true;
        }

        if (
            bodyObjects.TryGetValue("Planet", out GameObject planetObject) &&
            planetObject != null
        )
        {
            planetTransform = planetObject.transform;
            return true;
        }

        return false;
    }

    internal bool TryGetBodyVisualTransform(string bodyName, out Transform bodyTransform)
    {
        bodyTransform = null;

        if (string.IsNullOrEmpty(bodyName) || bodyObjects == null)
        {
            return false;
        }

        if (
            bodyObjects.TryGetValue(bodyName, out GameObject bodyObject) &&
            bodyObject != null
        )
        {
            bodyTransform = bodyObject.transform;
            return true;
        }

        return false;
    }

    bool TryGetLodTerrainObject(out GameObject lodObject)
    {
        lodObject = null;

        if (bodyObjects == null)
        {
            return false;
        }

        string lodName = CelestialTerrainRuntime.LodBodyName;

        if (bodyObjects.TryGetValue(lodName, out lodObject) && lodObject != null)
        {
            return true;
        }

        return bodyObjects.TryGetValue("Planet", out lodObject) && lodObject != null;
    }

    internal Transform GetVesselVisualRoot()
    {
        return GetOrCreateVesselVisualRoot();
    }

    internal Transform GetCelestialVisualRoot()
    {
        return GetOrCreateCelestialVisualRoot();
    }

    Transform GetOrCreateCelestialVisualRoot()
    {
        Transform universeRoot = GetOrCreateFlightUniverseRoot();

        if (celestialVisualRoot == null)
        {
            GameObject rootObject = new GameObject("CelestialVisuals");
            rootObject.transform.SetParent(universeRoot != null ? universeRoot : transform, false);
            rootObject.transform.localPosition = UnityEngine.Vector3.zero;
            rootObject.transform.localRotation = UnityEngine.Quaternion.identity;
            rootObject.transform.localScale = UnityEngine.Vector3.one;
            celestialVisualRoot = rootObject.transform;
        }
        else if (universeRoot != null && celestialVisualRoot.parent != universeRoot)
        {
            celestialVisualRoot.SetParent(universeRoot, false);
        }

        celestialVisualRoot.localPosition = UnityEngine.Vector3.zero;
        celestialVisualRoot.localRotation = UnityEngine.Quaternion.identity;
        celestialVisualRoot.localScale = UnityEngine.Vector3.one;
        return celestialVisualRoot;
    }

    void ParentCelestialVisual(GameObject celestialObject)
    {
        if (celestialObject == null)
        {
            return;
        }

        Transform root = GetOrCreateCelestialVisualRoot();

        if (celestialObject.transform.parent != root)
        {
            celestialObject.transform.SetParent(root, false);
        }

        root.localScale = UnityEngine.Vector3.one;
    }

    Transform GetOrCreateVesselVisualRoot()
    {
        if (vesselVisualRoot == null)
        {
            GameObject rootObject = new GameObject("VesselVisuals");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localScale = UnityEngine.Vector3.one;
            vesselVisualRoot = rootObject.transform;
        }

        vesselVisualRoot.localScale = UnityEngine.Vector3.one;
        return vesselVisualRoot;
    }

    void ParentVesselVisual(GameObject vesselObject)
    {
        if (vesselObject == null)
        {
            return;
        }

        Transform root = GetOrCreateVesselVisualRoot();

        if (vesselObject.transform.parent != root)
        {
            vesselObject.transform.SetParent(root, false);
        }

        BodyView.EnsureVesselUnityScale(vesselObject);
    }

    void ClearBodyObjects()
    {
        if (bodyObjects == null)
        {
            return;
        }

        foreach (KeyValuePair<string, GameObject> kvp in bodyObjects)
        {
            if (kvp.Value != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.DestroyImmediate(kvp.Value);
                }
                else
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
        }

        bodyObjects.Clear();

        if (vesselVisualRoot != null)
        {
            for (int i = vesselVisualRoot.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(vesselVisualRoot.GetChild(i).gameObject);
            }
        }

        if (celestialVisualRoot != null)
        {
            for (int i = celestialVisualRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = celestialVisualRoot.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    UnityEngine.Object.DestroyImmediate(child);
                    continue;
                }
#endif
                UnityEngine.Object.Destroy(child);
            }
        }

        lastShipLayoutRevision = -1;
        lastStationLayoutRevision = -1;
        lastRenderedFlightStatus = FlightStatus.Flying;
        lastRenderedDockingMessage = string.Empty;
        cameraTargetFocusObject = null;
    }

    void InitializeSimulation(bool preserveScenario = false)
    {
        isMultiplayerMode = false;
        desyncDetected = false;
        lockstepReady = false;
        joinSnapshotLoaded = false;
        assignedVesselId = 1;
        localNetworkRcsVector = global::SimVector3.Zero();

        lock (networkBufferLock)
        {
            networkInputBuffer.Clear();
        }
        hasShipRenderPrevious = false;

        if (!preserveScenario)
        {
            scenario = playSession.NormalizePlayableScenario(scenario);
        }

        RestoreFlightPresentation();
        FlightVesselSpawner.EnsureFlightSceneReady(this);

        // Simulation(scenario) below immediately computes the vessel's rest position via
        // LaunchPadConstants.GetPadRestComDistance(), which reads the cached
        // launchSiteTerrainHeightAboveSphereMeters static field. That field is only ever
        // refreshed by LaunchSiteTerrain.Refresh(), which normally runs much later, inside
        // RenderBodies() -> FlightPlanetSphere.Build(). On a vessel's very first launch in
        // a session the field is still at its 0.0 default, so the vessel gets pinned to a
        // "sea level" pad height while the terrain mesh (built moments later, using the
        // real ~tens-of-meters elevation at the Cove launch site) renders the pad well
        // above that — the ship visibly spawns sunk below the pad. Refresh it here first
        // so the vessel and the ground agree on the same height from the very first frame.
        PlanetTerrainTextureResources.ResetDiagnosticsSession();
        LaunchSiteConfig.ApplyScenario(scenario);
        if (pendingLaunchSiteOverride.HasValue)
        {
            LaunchSiteConfig.SetActive(pendingLaunchSiteOverride.Value);
            pendingLaunchSiteOverride = null;
        }
        CelestialTerrainRuntime.BindForScenario(scenario);
        LaunchPadConstants.BindForScenario(scenario);
        PlanetTerrainTextureResources.EnsureLoaded();
        PartCatalog.Refresh();
        LaunchSiteTerrain.Refresh(PlanetTerrainTextureResources.EarthHeightMap);
        LaunchSitePadAuthoring.RefreshActive(launchSitePrefab);

        simulation = new Simulation(scenario);
        simulation.ClientLocalOrientationVesselId = assignedVesselId;
        ScenarioVisualDefaults.ApplyToRunner(this, simulation);
        SyncDockingPortOffsets();
        playSession.TryApplyLaunchShip(simulation);
        if (SimulationScenarios.IsAnySurfacePad(scenario))
        {
            simulation.RefreshLaunchPadGrounding();
        }
        LaunchSiteTerrain.LogShipGeography(
            simulation,
            PlanetTerrainTextureResources.EarthHeightMap
        );
        if (
            bodyObjects != null &&
            bodyObjects.TryGetValue("Ship", out GameObject shipObject) &&
            shipObject != null
        )
        {
            LaunchSiteTerrain.LogShipSpawnDiagnostic(simulation, shipObject.transform);
        }
        PlanetThreePointsOfTruthDiagnostic.LogActiveSpawnTruth(simulation);
        if (pendingTruthProbeLogIndex.HasValue)
        {
            PlanetThreePointsOfTruthDiagnostic.LogProbeAfterSpawn(pendingTruthProbeLogIndex.Value);
            pendingTruthProbeLogIndex = null;
        }

        playSession.ResetMissionTracking();
        playSession.SaveLastScenario(scenario);
        RefreshAvailableTargets();
        RenderBodies();
        SetupHud();

        if (
            simulation != null &&
            TryGetPlanetVisualTransform(out Transform planetTransformAfterRender)
        )
        {
            LaunchSiteTerrain.LogViewerGeography(
                planetTransformAfterRender,
                PlanetTerrainTextureResources.EarthHeightMap
            );
        }
        SetupCamera();
        SetupOrbitPreview();
        SetupDebrisView();
        SetupSphereOfInfluenceView();
        SetupAtmosphereView();
        SetupLaunchPadView();
        AlignFlightUniverseZeroFrame();
        lastActivePrimaryBodyName = simulation.activePrimaryBodyName;
        lastWorldRevision = simulation.GetWorldRevision();
        EnsureValidTarget();
        isInitialized = true;
        sessionMode = GameSessionMode.Flight;
        RebuildPreviewAnalysis();
        SnapShipRenderInterpolationToCurrent();
        ForceRebuildShipVisual();
        FlightSceneIsolation.PurgeStrayVesselBlocks(this);
        ForceRebuildDockableVisuals();
        ResetScenarioThrottle();
        pendingCameraChaseSnap = true;
        GameSessionBootstrap.ApplyFlightPresentationInPlace(this);
        ApplyFlightUniverseVisualNow();
        SyncPlanetLodRuntime();
        PrimeLodTerrainAfterVisualReady();
        FlightLodLoadingOverlay.NotifyFlightSessionStarted(this);
    }

    void PrimeLodTerrainAfterVisualReady()
    {
        if (
            !TryGetLodTerrainObject(out GameObject planetObject) ||
            planetObject == null
        )
        {
            return;
        }

        FlightPlanetSphere planetSphere = planetObject.GetComponent<FlightPlanetSphere>();
        planetSphere?.PrimeFlightTerrainLod();
    }

    void SyncDockingPortOffsets()
    {
        if (
            simulation == null ||
            simulation.scenario != SimulationScenario.DockingApproach
        )
        {
            return;
        }

        if (simulation.ship.TryGetPrimaryDockingPortLocal(out _))
        {
            simulation.ship.SyncDockingPortForwardOffset();
        }

        for (int i = 0; i < simulation.dockableVessels.Count; i++)
        {
            Vessel dockable = simulation.dockableVessels[i];

            if (dockable.TryGetPrimaryDockingPortLocal(out _))
            {
                dockable.SyncDockingPortForwardOffset();
            }
        }
    }

    void RefreshAvailableTargets()
    {
        string[] preferredOrder = { "Station", "Moon", "Planet", "Sun" };
        List<string> availableTargets = new List<string>();

        if (simulation != null)
        {
            for (int i = 0; i < simulation.dockableVessels.Count; i++)
            {
                string dockableName = simulation.dockableVessels[i].name;

                if (!availableTargets.Contains(dockableName))
                {
                    availableTargets.Add(dockableName);
                }
            }
        }

        for (int i = 0; i < preferredOrder.Length; i++)
        {
            if (
                simulation != null &&
                simulation.HasBody(preferredOrder[i]) &&
                !availableTargets.Contains(preferredOrder[i])
            )
            {
                availableTargets.Add(preferredOrder[i]);
            }
        }

        if (availableTargets.Count > 0)
        {
            targetBodyNames = availableTargets.ToArray();
        }

        if (
            simulation != null &&
            simulation.scenario == SimulationScenario.DockingApproach &&
            !simulation.ship.IsDocked &&
            FindDockableVessel("Station") != null
        )
        {
            targetBodyName = "Station";
            simulation.targetVessel = FindDockableVessel("Station");
        }

        if (
            simulation != null &&
            simulation.ship.IsDocked &&
            targetBodyName == "Station"
        )
        {
            targetBodyName = string.Empty;
            simulation.targetVessel = null;
        }

        EnsureValidTarget();
    }

    bool IsTargetAvailable(string targetName)
    {
        if (simulation == null || string.IsNullOrEmpty(targetName))
        {
            return false;
        }

        if (simulation.HasBody(targetName))
        {
            return true;
        }

        return FindDockableVessel(targetName) != null;
    }

    void OnDockingSucceeded()
    {
        DestroyVesselBodyView("Station");
        SnapShipRenderInterpolationToCurrent();
        targetBodyName = string.Empty;
        simulation.targetVessel = null;
        InvalidatePreviewAnalysis();
        RefreshAvailableTargets();
        lastShipLayoutRevision = -1;
        lastStationLayoutRevision = -1;
        ForceRebuildShipVisual();
        ClearShipTargetVisuals();

        if (ShipOrientationMode == ShipOrientationMode.TargetAlignment)
        {
            ShipOrientationMode = ShipOrientationMode.Manual;
            simulation.atmosphericSasEnabled = false;
        }

        if (
            assignedVesselId != 1 &&
            simulation.GetVesselById(assignedVesselId) == null
        )
        {
            assignedVesselId = 1;
            ShowCenterHudMessage("Управление переключено на стыковочный комплекс", 3.0f);
        }
    }

    void InvalidatePreviewAnalysis()
    {
        hasCachedPreviewAnalysis = false;
        previewRebuildSoIPending = false;
        previewSoIReadyFrame = 0;
        cachedClosestApproach = ClosestApproachInfo.None();
        cachedSphereOfInfluenceEntryPreview =
            SphereOfInfluenceEntryPreview.None();
    }

    void ClearShipTargetVisuals()
    {
        if (
            !bodyObjects.TryGetValue("Ship", out GameObject shipObject) ||
            shipObject == null
        )
        {
            return;
        }

        Vector3 shipPosition = shipObject.transform.position;
        BodyView.UpdateTargetDirection(shipObject, shipPosition);
        BodyView.UpdateTargetRelativeVelocity(
            shipObject,
            global::SimVector3.Zero()
        );
    }

    void DestroyVesselBodyView(string vesselName)
    {
        if (string.IsNullOrEmpty(vesselName))
        {
            return;
        }

        if (
            bodyObjects.TryGetValue(vesselName, out GameObject vesselObject) &&
            vesselObject != null
        )
        {
            Destroy(vesselObject);
        }

        bodyObjects.Remove(vesselName);
    }

    Vessel FindDockableVessel(string vesselName)
    {
        if (simulation == null)
        {
            return null;
        }

        for (int i = 0; i < simulation.dockableVessels.Count; i++)
        {
            Vessel dockable = simulation.dockableVessels[i];

            if (dockable.name == vesselName)
            {
                return dockable;
            }
        }

        return null;
    }

    internal void EnqueueNetworkTickPacket(NetworkTickPacket packet)
    {
        diagPacketsReceivedInLoop++;

        lock (networkBufferLock)
        {
            networkInputBuffer.Enqueue(packet);
        }
    }

    internal void EnqueueJoinSnapshot(byte[] snapshotData)
    {
        if (snapshotData == null || snapshotData.Length == 0)
        {
            return;
        }

        lock (networkBufferLock)
        {
            pendingJoinSnapshots.Enqueue(snapshotData);
        }
    }

    internal int GetAssignedVesselId()
    {
        return assignedVesselId;
    }

    internal global::SimVector3 GetLocalNetworkRcsVector()
    {
        return localNetworkRcsVector;
    }

    internal void SetLocalNetworkRcsVector(double localForward, double localRight)
    {
        localNetworkRcsVector = new global::SimVector3(localForward, localRight, 0.0);
    }

    public void ConnectMultiplayer(string host, int port, int preferredPlayerSlot = 0)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            ShowCenterHudMessage("Укажите IP сервера", 4.0f);
            return;
        }

        if (!isInitialized || simulation == null)
        {
            LaunchScenario(SimulationScenario.DockingApproach);
        }

        if (preferredPlayerSlot == 1 || preferredPlayerSlot == 2)
        {
            assignedVesselId = preferredPlayerSlot;
        }
        else
        {
            assignedVesselId = 0;
        }
        joinSnapshotLoaded = false;
        isMultiplayerMode = true;
        mpHudMenuOpen = true;
        lockstepReady = false;
        desyncDetected = false;
        diagPacketsReceivedInLoop = 0;
        diagPacketsDequeued = 0;
        diagLockstepState = LockstepDiagState.None;
        diagLockstepBufferCount = 0;
        diagLockstepMissingTick = 0;
        pendingNetworkPauseRequested = true;
        pendingPauseToggleToSend = false;
        accumulator = 0.0;
        lockstepAccumulator = 0.0;
        localNetworkRcsVector = global::SimVector3.Zero();

        if (
            simulation != null &&
            simulation.scenario == SimulationScenario.DockingApproach
        )
        {
            simulation.EnforceMultiplayerSlotVesselIds();
        }

        lock (networkBufferLock)
        {
            networkInputBuffer.Clear();
            pendingJoinSnapshots.Clear();
        }

        networkTransport = GetComponent<NetworkTransportClient>();

        if (networkTransport == null)
        {
            networkTransport = gameObject.AddComponent<NetworkTransportClient>();
        }

        networkTransport.StartTransport(host.Trim(), port);
        networkTransport.enabled = true;
        ShowCenterHudMessage(
            "Подключение к " + host.Trim() + ":" + port + "...",
            4.0f
        );
    }

    void ProcessPendingJoinSnapshots()
    {
        byte[] snapshotData = null;

        lock (networkBufferLock)
        {
            if (pendingJoinSnapshots.Count == 0)
            {
                return;
            }

            snapshotData = pendingJoinSnapshots.Dequeue();
        }

        if (simulation == null || snapshotData == null)
        {
            return;
        }

        try
        {
            WorldSnapshot snapshot = ShipPersistence.DeserializeFromBinary(snapshotData);
            simulation.LoadSnapshot(snapshot);

            if (isMultiplayerMode)
            {
                simulation.EnforceMultiplayerSlotVesselIds();
            }

            scenario = simulation.scenario;
            ScenarioVisualDefaults.ApplyToRunner(this, simulation);
            accumulator = 0.0;
            lockstepAccumulator = 0.0;
            joinSnapshotLoaded = true;

            lock (networkBufferLock)
            {
                networkInputBuffer.Clear();
            }

            TryFinalizeLockstepJoin();
        }
        catch (Exception exception)
        {
            isMultiplayerMode = false;
            lockstepReady = false;
            Debug.LogError("Join snapshot load failed: " + exception.Message);
            ShowCenterHudMessage("Ошибка загрузки снимка мира", 5.0f);
        }
    }

    internal int GetLocalVesselId()
    {
        return assignedVesselId;
    }

    internal void QueuePendingNetworkStaging()
    {
        pendingNetworkStaging = true;
    }

    internal bool ConsumePendingNetworkStaging()
    {
        if (!pendingNetworkStaging)
        {
            return false;
        }

        pendingNetworkStaging = false;
        return true;
    }

    void HandleSessionInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || keyboard.escapeKey == null)
        {
            return;
        }

        if (!keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (FlightLodLoadingOverlay.IsBlocking)
        {
            ReturnToMainMenu();
            return;
        }

        if (
            earthSandboxSessionActive ||
            (EarthSandboxScene.IsActive() && EarthSandboxScene.EnteredFromMainMenu)
        )
        {
            ReturnToMainMenu();
            return;
        }

        if (sessionMode == GameSessionMode.MainMenu)
        {
            FlightHudSettingsUi hudSettingsUi = FlightHudSettingsUi.TryGetInstance();
            if (hudSettingsUi != null && hudSettingsUi.TryHandleEscape())
            {
                return;
            }

            VabOptionsUi optionsUi = VabOptionsUi.TryGetInstance();
            if (optionsUi != null)
            {
                optionsUi.TryHandleEscape();
            }

            return;
        }

        if (sessionMode == GameSessionMode.VabEditor)
        {
            return;
        }

        if (sessionMode == GameSessionMode.Flight && isInitialized)
        {
            FlightHudSettingsUi hudSettingsUi = FlightHudSettingsUi.TryGetInstance();
            if (hudSettingsUi != null && hudSettingsUi.TryHandleEscape())
            {
                return;
            }

            VabOptionsUi optionsUi = VabOptionsUi.TryGetInstance();
            if (optionsUi != null && optionsUi.TryHandleEscape())
            {
                return;
            }

            TogglePauseMenu();
        }
    }

    void Update()
    {
        HandleSessionInput();

        if (!isInitialized || simulation == null)
        {
            return;
        }

        if (!isMultiplayerMode && simulation.scenario != scenario)
        {
            ReinitializeSimulation();
            return;
        }

        UpdateCenterHudMessageState();
        perfSimStepsThisFrame = 0;

        if (!isPauseMenuOpen)
        {
            if (!FlightLodLoadingOverlay.IsBlocking)
            {
                HandleMapModeToggle();
                pilotInput.Update(this);
                HandleQuickSaveLoadInput();
            }

            playSession.UpdateMissionPhase(scenario, simulation);
            ClampTimeScaleForActiveThrust();

            if (isMultiplayerMode)
            {
                ProcessPendingJoinSnapshots();
                SendMultiplayerInputFrame();

                if (lockstepReady)
                {
                    RunLockstepSimulationSteps();
                }
            }
            else
            {
                RunAccumulatedSimulationSteps();
            }

            UpdatePreviewAnalysis();
            UpdateRenderInterpolationState();
            SyncWorldRevision();
        }
        else if (simulation != null && simulation.ship != null)
        {
            // Pause menu blocks sim steps but visuals still need a stable interpolation
            // sample so FlightUniverseMovement does not jump when the menu closes.
            UpdateRenderInterpolationState();
        }

        RenderBodies();
        UpdatePerformanceStats();
    }

    void SendMultiplayerInputFrame()
    {
        if (!isMultiplayerMode || desyncDetected)
        {
            return;
        }

        if (networkTransport == null)
        {
            networkTransport = GetComponent<NetworkTransportClient>();
        }

        if (networkTransport != null)
        {
            networkTransport.BuildAndSendLocalInputFrame();
        }
    }

    void LateUpdate()
    {
        if (!isInitialized || simulation == null)
        {
            return;
        }

        launchPadView?.RefreshPadTransformForRender();
    }

    internal void SyncFlightVisualLate()
    {
        if (!isInitialized || simulation == null)
        {
            return;
        }

        UpdateShipCameraFollow();
        SyncPlanetCloudAnimation();

        if (isMapModeActive)
        {
            return;
        }

        SyncPlanetLodRuntime();
    }

    internal global::SimQuaternion GetInterpolatedPlanetSpinRotation()
    {
        Body lodBody = GetLodTerrainBody();

        if (lodBody == null)
        {
            return global::SimQuaternion.Identity();
        }

        global::SimVector3 axis = lodBody.spinAxis.LengthSquared() > 1e-12 ?
            lodBody.spinAxis.Normalize() :
            global::SimVector3.UnitY();
        return global::SimQuaternion.FromAxisAngle(axis, GetInterpolatedPlanetSpinAngle());
    }

    internal float GetPlanetVisualSpinLongitudeRadians()
    {
        return (float)GetInterpolatedPlanetSpinAngle();
    }

    double CaptureCurrentPlanetSpinAngle()
    {
        Body lodBody = GetLodTerrainBody();

        if (lodBody == null)
        {
            return 0.0;
        }

        return lodBody.spinAngleRadians;
    }

    double GetInterpolatedPlanetSpinAngle()
    {
        Body lodBody = GetLodTerrainBody();

        if (lodBody == null)
        {
            return 0.0;
        }

        double current = lodBody.spinAngleRadians;

        if (
            !enableRenderInterpolation ||
            !hasShipRenderPrevious
        )
        {
            return current;
        }

        return shipRenderPreviousSpinAngle +
            (current - shipRenderPreviousSpinAngle) * renderInterpolationT;
    }

    Body GetLodTerrainBody()
    {
        if (simulation == null)
        {
            return null;
        }

        return simulation.GetVisualTerrainBody() ?? simulation.planet;
    }

    /// <summary>
    /// Geography under the ship in body-fixed axes. Interpolates previous/current
    /// InverseSpin(r) pairs so spin and position stay on the same clock.
    /// InverseSpin(now) * lerp(r) is what made Earth jerk when spin was on.
    /// </summary>
    internal global::SimVector3 GetInterpolatedBodyFixedUnderShip()
    {
        if (simulation == null || GetLodTerrainBody() == null)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        if (WorldVisualBridge.IsFlightPadSurfaceLocked(simulation))
        {
            global::SimVector3 landed = simulation.ship.landedSurfaceDirectionBodyFixed;

            if (landed.LengthSquared() > 1e-12)
            {
                return landed.Normalize();
            }

            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        global::SimVector3 currentRadial = CaptureCurrentShipRadialOutSim();

        if (currentRadial.LengthSquared() <= 1e-12)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        currentRadial = currentRadial.Normalize();
        global::SimQuaternion currentSpin = GetLodTerrainBody().GetSpinRotation();
        global::SimVector3 currentBodyFixed = currentSpin.InverseRotate(currentRadial).Normalize();

        if (
            !enableRenderInterpolation ||
            !hasShipRenderPrevious ||
            shipRenderPreviousRadialOut.LengthSquared() <= 1e-12
        )
        {
            return currentBodyFixed;
        }

        Body lodBody = GetLodTerrainBody();
        global::SimVector3 axis = lodBody != null && lodBody.spinAxis.LengthSquared() > 1e-12 ?
            lodBody.spinAxis.Normalize() :
            global::SimVector3.UnitY();
        global::SimQuaternion previousSpin = global::SimQuaternion.FromAxisAngle(
            axis,
            shipRenderPreviousSpinAngle
        );
        global::SimVector3 previousRadial = shipRenderPreviousRadialOut.Normalize();
        global::SimVector3 previousBodyFixed = previousSpin
            .InverseRotate(previousRadial)
            .Normalize();

        if (previousBodyFixed.LengthSquared() <= 1e-12)
        {
            return currentBodyFixed;
        }

        if (global::SimVector3.Dot(previousBodyFixed, currentBodyFixed) < 0.0)
        {
            return currentBodyFixed;
        }

        global::SimVector3 blended = LerpSimVector3(
            previousBodyFixed,
            currentBodyFixed,
            renderInterpolationT
        );

        if (blended.LengthSquared() <= 1e-12)
        {
            return currentBodyFixed;
        }

        return blended.Normalize();
    }

    internal global::SimVector3 GetInterpolatedShipRadialOutSim()
    {
        if (simulation == null || simulation.planet == null)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        global::SimQuaternion planetSpin = GetInterpolatedPlanetSpinRotation();

        if (WorldVisualBridge.IsFlightPadSurfaceLocked(simulation) ||
            simulation.IsShipOnCoveLaunchPad())
        {
            return WorldVisualBridge.ResolveFlightPlanetUpRadialSim(simulation, planetSpin);
        }

        global::SimVector3 current = CaptureCurrentShipRadialOutSim();

        if (
            !enableRenderInterpolation ||
            !hasShipRenderPrevious ||
            shipRenderPreviousRadialOut.LengthSquared() <= 1e-12
        )
        {
            return current;
        }

        global::SimVector3 blended = LerpSimVector3(
            shipRenderPreviousRadialOut,
            current,
            renderInterpolationT
        );

        if (blended.LengthSquared() <= 1e-12)
        {
            return current;
        }

        return blended.Normalize();
    }

    global::SimVector3 CaptureCurrentShipRadialOutSim()
    {
        if (simulation == null || simulation.planet == null)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        if (WorldVisualBridge.IsFlightPadSurfaceLocked(simulation))
        {
            return WorldVisualBridge.ResolveFlightPlanetUpRadialSim(
                simulation,
                GetInterpolatedPlanetSpinRotation()
            );
        }

        global::SimVector3 surfaceUp = simulation.GetShipSurfaceUp();

        if (surfaceUp.LengthSquared() > 1e-12)
        {
            return surfaceUp.Normalize();
        }

        global::SimVector3 offset = simulation.ship.GetCenterOfMassWorld()
            .Subtract(simulation.planet.position);

        if (offset.LengthSquared() > 1e-12)
        {
            return offset.Normalize();
        }

        return LandingConfig.GetSurfaceLaunchPadNormal();
    }

    void SyncPlanetCloudAnimation()
    {
        if (simulation == null || bodyObjects == null)
        {
            return;
        }

        if (
            TryGetLodTerrainObject(out GameObject lodObject) &&
            lodObject != null
        )
        {
            FlightPlanetSphere planetSphere = lodObject.GetComponent<FlightPlanetSphere>();
            planetSphere?.SyncCloudAnimation(simulation.simulationTime);
        }

        if (
            bodyObjects.TryGetValue("Planet", out GameObject earthObject) &&
            earthObject != null
        )
        {
            DistantCelestialGlobe earthGlobe = earthObject.GetComponent<DistantCelestialGlobe>();
            earthGlobe?.SyncCloudAnimation(simulation.simulationTime);
        }
    }

    internal void RefreshFlightDistantCelestialTransforms()
    {
        if (isMapModeActive || simulation == null || bodyObjects == null)
        {
            return;
        }

        WorldVisualBridge.RenderContext renderContext = GetRenderContext();
        global::SimVector3 renderOrigin = GetRenderOrigin();
        double shipAltitudeAbovePlanet = simulation.GetShipAltitudeAbovePlanet();
        BodyState[] bodyStates = simulation.GetBodyStates();
        string lodBodyName = CelestialTerrainRuntime.LodBodyName;

        for (int i = 0; i < bodyStates.Length; i++)
        {
            BodyState bodyState = bodyStates[i];

            if (
                simulation.IsVesselBodyName(bodyState.name) ||
                bodyState.name == lodBodyName ||
                !WorldVisualBridge.ShouldRenderCelestialBodyInFlight(bodyState.name)
            )
            {
                continue;
            }

            RenderCelestialBodyState(
                bodyState,
                renderOrigin,
                true,
                isMapMode: false,
                shipAltitudeAbovePlanet
            );
        }
    }

    internal void RefreshMapModeCelestialTransforms()
    {
        if (simulation == null)
        {
            return;
        }

        WorldVisualBridge.RenderContext renderContext = GetRenderContext();

        if (!renderContext.IsMapMode)
        {
            return;
        }

        global::SimVector3 renderOrigin = GetRenderOrigin();
        BodyState[] bodyStates = simulation.GetBodyStates();
        double shipAltitudeAbovePlanet = simulation.GetShipAltitudeAbovePlanet();

        for (int i = 0; i < bodyStates.Length; i++)
        {
            BodyState bodyState = bodyStates[i];

            if (simulation.IsVesselBodyName(bodyState.name))
            {
                continue;
            }

            if (
                renderContext.VacuumDocking &&
                bodyState.name == "Planet"
            )
            {
                continue;
            }

            if (!bodyObjects.TryGetValue(bodyState.name, out GameObject bodyObject))
            {
                continue;
            }

            global::SimVector3 planetCenterWorld = bodyState.position;

            if (bodyState.name == "Planet" && simulation.planet != null)
            {
                planetCenterWorld = simulation.planet.position;
            }

            BodyView.UpdateCelestialBody(
                bodyObject,
                bodyState,
                planetCenterWorld,
                renderOrigin,
                renderContext.ShowCelestialMeshes,
                isMapMode: true,
                shipAltitudeAbovePlanet,
                simulation
            );
        }
    }

    void SyncPlanetLodRuntime()
    {
        if (
            bodyObjects == null ||
            !TryGetLodTerrainObject(out GameObject planetObject) ||
            planetObject == null
        )
        {
            return;
        }

        FlightPlanetSphere planetSphere = planetObject.GetComponent<FlightPlanetSphere>();

        if (planetSphere == null)
        {
            return;
        }

        Vector3 viewerAnchorWorld = WorldVisualBridge.FlightFrameViewerAnchorWorld;
        planetSphere.SetViewerMotion(
            (float)simulation.GetShipAltitudeAbovePlanet(),
            GetShipFlightFrameVelocityUnity(),
            viewerAnchorWorld,
            WorldVisualBridge.SimNormalToUnity(GetInterpolatedBodyFixedUnderShip()),
            GetShipSurfaceRelativeVelocitySimUnity()
        );

        if (isMapModeActive)
        {
            return;
        }

        planetSphere.SetPlanetSpinLongitudeRadians(GetPlanetVisualSpinLongitudeRadians());
        TryRefreshLaunchSiteTerrainGrounding();
    }

    Vector3 GetShipFlightFrameVelocityUnity()
    {
        if (simulation == null || simulation.ship == null)
        {
            return Vector3.zero;
        }

        if (WorldVisualBridge.IsFlightPadSurfaceLocked(simulation))
        {
            return Vector3.zero;
        }

        Body primary = simulation.GetActivePrimaryBody() ?? simulation.planet;
        global::SimVector3 surfaceRelative = simulation.ship.velocity;

        if (primary != null)
        {
            surfaceRelative = simulation.ship.velocity.Subtract(
                primary.GetAbsoluteSurfaceVelocityAt(simulation.ship.GetCenterOfMassWorld())
            );
        }

        Vector3 surfaceNormalUnity = WorldVisualBridge.SimNormalToUnity(
            simulation.GetShipSurfaceUp()
        );
        return WorldVisualBridge.SimVelocityToUnityFlight(
            surfaceRelative,
            surfaceNormalUnity
        );
    }

    Vector3 GetShipSurfaceRelativeVelocitySimUnity()
    {
        if (simulation == null || simulation.ship == null)
        {
            return Vector3.zero;
        }

        if (WorldVisualBridge.IsFlightPadSurfaceLocked(simulation))
        {
            return Vector3.zero;
        }

        Body primary = simulation.GetActivePrimaryBody() ?? simulation.planet;
        global::SimVector3 surfaceRelative = simulation.ship.velocity;

        if (primary != null)
        {
            surfaceRelative = simulation.ship.velocity.Subtract(
                primary.GetAbsoluteSurfaceVelocityAt(simulation.ship.GetCenterOfMassWorld())
            );
        }

        global::SimQuaternion planetSpin = GetInterpolatedPlanetSpinRotation();
        global::SimVector3 bodyFixedVelocity = planetSpin.InverseRotate(surfaceRelative);
        return WorldVisualBridge.SimNormalToUnity(bodyFixedVelocity);
    }

    internal bool TryDevTeleportShip(double altitudeMeters, Vector3 velocityUnity)
    {
        if (simulation == null || simulation.ship == null)
        {
            return false;
        }

        global::SimVector3 velocity = new global::SimVector3(
            velocityUnity.x,
            velocityUnity.y,
            velocityUnity.z
        );

        if (
            !simulation.TryDevTeleportShipAboveActivePrimary(
                altitudeMeters,
                velocity,
                out _
            )
        )
        {
            return false;
        }

        global::Vessel ship = simulation.ship;
        shipRenderPreviousComWorld = ship.GetCenterOfMassWorld();
        shipRenderPreviousPosition = ship.position;
        shipRenderPreviousCenterOfMassLocal = ship.centerOfMassLocal;
        shipRenderPreviousHeading = ship.heading;
        shipRenderPreviousOrientation = ship.orientation;
        shipRenderPreviousRadialOut = CaptureCurrentShipRadialOutSim();
        shipRenderPreviousSpinAngle = CaptureCurrentPlanetSpinAngle();

        if (altitudeMeters <= 0.01)
        {
            simulation.RefreshLaunchPadGrounding();
        }

        SyncPlanetLodRuntime();
        SyncFlightVisualLate();

        if (
            TryGetLodTerrainObject(out GameObject planetObject) &&
            planetObject != null
        )
        {
            FlightPlanetSphere planetSphere = planetObject.GetComponent<FlightPlanetSphere>();

            if (planetSphere != null)
            {
                planetSphere.RequestViewerLodRebuild();
            }
        }

        return true;
    }

    void TryRefreshLaunchSiteTerrainGrounding()
    {
        if (simulation == null)
        {
            return;
        }

        double terrainHeight = LaunchPadConstants.GetLaunchSiteTerrainHeightAboveSphereMeters();

        if (
            !double.IsNaN(lastAppliedLaunchTerrainHeight) &&
            Math.Abs(terrainHeight - lastAppliedLaunchTerrainHeight) < 0.01
        )
        {
            return;
        }

        lastAppliedLaunchTerrainHeight = terrainHeight;
        simulation.RefreshLaunchPadGrounding();

        if (
            TryGetLodTerrainObject(out GameObject planetObject) &&
            planetObject != null
        )
        {
            FlightPlanetSphere planetSphere = planetObject.GetComponent<FlightPlanetSphere>();

            if (planetSphere != null)
            {
                planetSphere.InvalidateLaunchSiteMeshes();
            }
        }

    }

    void RunLockstepSimulationSteps()
    {
        if (desyncDetected)
        {
            return;
        }

        if (TryPeekLatestBufferedNetworkPacket(out NetworkTickPacket latestPacket))
        {
            if (latestPacket.IsPauseTick)
            {
                lockstepTickPaused = true;
                lockstepAccumulator = 0.0;
                diagLockstepState = LockstepDiagState.PauseFromServer;
                return;
            }

            lockstepTickPaused = false;
        }

        lockstepAccumulator += Time.deltaTime;

        int bufferedTicks = GetNetworkBufferCount();
        bool catchUpMode = bufferedTicks > 10;
        int maxStepsThisFrame = catchUpMode ? 15 : 1;
        int stepsRun = 0;

        if (!catchUpMode && lockstepAccumulator < fixedDt)
        {
            diagLockstepState = LockstepDiagState.WaitingAccumulator;
            return;
        }

        if (bufferedTicks < LockstepInputBufferThreshold && !catchUpMode)
        {
            diagLockstepState = LockstepDiagState.BufferLow;
            diagLockstepBufferCount = bufferedTicks;
        }

        while (
            stepsRun < maxStepsThisFrame &&
            (catchUpMode || lockstepAccumulator >= fixedDt)
        )
        {
            if (bufferedTicks < LockstepInputBufferThreshold && !catchUpMode)
            {
                diagLockstepState = LockstepDiagState.BufferLow;
                diagLockstepBufferCount = bufferedTicks;
                break;
            }

            lock (networkBufferLock)
            {
                while (networkInputBuffer.Count > 0)
                {
                    NetworkTickPacket head = networkInputBuffer.Peek();

                    if (head.tick < simulation.currentTick)
                    {
                        networkInputBuffer.Dequeue();
                        continue;
                    }

                    if (head.tick > simulation.currentTick)
                    {
                        simulation.currentTick = head.tick;
                        simulation.simulationTime = head.tick * fixedDt;
                    }

                    break;
                }
            }

            if (!TryDequeuePacketForTick(simulation.currentTick, out NetworkTickPacket packet))
            {
                diagLockstepState = LockstepDiagState.MissingPacket;
                diagLockstepMissingTick = simulation.currentTick;
                break;
            }

            if (stepsRun == 0)
            {
                CommitShipRenderPreviousState();
            }

            ApplyNetworkTickPacket(packet);
            lockstepTickPaused = packet.IsPauseTick;

            if (!packet.IsPauseTick)
            {
                RunLockstepSimulationStep(fixedDt);
            }
            else
            {
                simulation.AdvanceLockstepPausedTick();
            }

            ulong localChecksum = simulation.CalculateStateChecksum();

            if (
                packet.tick > 0 &&
                localChecksum != packet.stateChecksum
            )
            {
                ApplyCorrectionStates(packet.correctionStates);
            }

            diagPacketsDequeued++;
            lockstepAccumulator -= fixedDt;
            stepsRun++;
            bufferedTicks = GetNetworkBufferCount();
            catchUpMode = bufferedTicks > 10;
        }

        if (lockstepAccumulator < 0.0)
        {
            lockstepAccumulator = 0.0;
        }

        if (stepsRun > 0)
        {
            diagLockstepState = LockstepDiagState.ExecutingTick;
            RenderBodies();
        }
    }

    internal string GetLockstepDiagMessage()
    {
        switch (diagLockstepState)
        {
            case LockstepDiagState.PauseFromServer:
                return "Pause from server (instant)";
            case LockstepDiagState.WaitingAccumulator:
                return "Waiting for accumulator timer";
            case LockstepDiagState.BufferLow:
                return "Buffer low: " + diagLockstepBufferCount + "/" + LockstepInputBufferThreshold;
            case LockstepDiagState.MissingPacket:
                return "Missing packet for tick " + diagLockstepMissingTick;
            case LockstepDiagState.ExecutingTick:
                return "Executing tick";
            default:
                return "None";
        }
    }

    void ApplyCorrectionStates(List<VesselCompactState> correctionStates)
    {
        if (simulation == null || correctionStates == null)
        {
            return;
        }

        for (int i = 0; i < correctionStates.Count; i++)
        {
            VesselCompactState state = correctionStates[i];
            Vessel vessel = simulation.GetVesselById(state.id);

            if (vessel == null)
            {
                continue;
            }

            vessel.position = state.pos;
            vessel.velocity = state.vel;
        }
    }

    void RunLockstepSimulationStep(double stepDt)
    {
        perfSimStepsThisFrame++;
        simulation.Update(stepDt);
    }

    bool TryPeekLatestBufferedNetworkPacket(out NetworkTickPacket packet)
    {
        packet = default;

        lock (networkBufferLock)
        {
            if (networkInputBuffer.Count == 0)
            {
                return false;
            }

            NetworkTickPacket latest = networkInputBuffer.Peek();

            foreach (NetworkTickPacket candidate in networkInputBuffer)
            {
                latest = candidate;
            }

            packet = latest;
            return true;
        }
    }

    bool TryDequeuePacketForTick(long expectedTick, out NetworkTickPacket packet)
    {
        packet = default;

        lock (networkBufferLock)
        {
            while (networkInputBuffer.Count > 0)
            {
                NetworkTickPacket candidate = networkInputBuffer.Peek();

                if (candidate.tick < expectedTick)
                {
                    networkInputBuffer.Dequeue();
                    continue;
                }

                if (candidate.tick > expectedTick)
                {
                    return false;
                }

                packet = networkInputBuffer.Dequeue();
                return true;
            }
        }

        return false;
    }

    public int GetNetworkBufferCount()
    {
        lock (networkBufferLock)
        {
            return networkInputBuffer.Count;
        }
    }

    void ApplyNetworkTickPacket(NetworkTickPacket packet)
    {
        if (simulation == null)
        {
            return;
        }

        simulation.ApplyNetworkTickPacket(packet);
        ApplyLocalRunnerHudStateFromPlayerSlot(
            NetworkInputFrame.Player1VesselId,
            packet.player1Frame
        );
        ApplyLocalRunnerHudStateFromPlayerSlot(
            NetworkInputFrame.Player2VesselId,
            packet.player2Frame
        );
    }

    void ApplyLocalRunnerHudStateFromPlayerSlot(int playerSlot, NetworkInputFrame frame)
    {
        if (playerSlot != assignedVesselId)
        {
            return;
        }

        throttle = Mathf.Clamp01(frame.throttle);
    }

    void HandleDesync(long tick)
    {
        if (!isMultiplayerMode)
        {
            desyncDetected = true;
            isPaused = true;
        }
        else
        {
            desyncDetected = true;
        }

        try
        {
            WorldSnapshot snapshot = simulation.CaptureSnapshot();
            string fileName = "desync_error_tick_" + tick + ".bin";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(path, ShipPersistence.SerializeToBinary(snapshot));
            ShowCenterHudMessage(
                "DESYNC на тике " + tick + ". Дамп: " + fileName,
                30.0f
            );
            Debug.LogError(
                "Lockstep desync at tick " + tick + ". Dump saved to " + path
            );
        }
        catch (Exception exception)
        {
            ShowCenterHudMessage(
                "DESYNC на тике " + tick + "! Ошибка сохранения дампа",
                30.0f
            );
            Debug.LogError("Desync dump failed: " + exception.Message);
        }
    }

    internal bool IsLocalMainEngineRequested()
    {
        Keyboard keyboard = Keyboard.current;

        return keyboard != null &&
            keyboard.wKey != null &&
            keyboard.wKey.isPressed;
    }

    bool ShouldShowEngineExhaustForVesselName(string vesselName)
    {
        int vesselId = GetNetworkVesselIdForBodyName(vesselName);

        if (vesselId != assignedVesselId)
        {
            return false;
        }

        Vessel vessel = simulation.GetVesselById(vesselId);

        if (vessel == null || vessel.GetActiveStageFuelMass() <= 0.0)
        {
            return false;
        }

        bool engineFiring =
            vessel.thrustAcceleration.Length() > 0.000001 ||
            vessel.thrust.LengthSquared() > 0.000001;

        return engineFiring;
    }

    static int GetNetworkVesselIdForBodyName(string vesselName)
    {
        if (vesselName == "Ship")
        {
            return 1;
        }

        if (vesselName == "Station")
        {
            return 2;
        }

        return 0;
    }

    NetworkTickPacket DequeueNetworkTickPacket()
    {
        lock (networkBufferLock)
        {
            return networkInputBuffer.Dequeue();
        }
    }

    void RunAccumulatedSimulationSteps()
    {
        double scaledDeltaTime =
            Time.deltaTime * Math.Max(0.0, GetEffectiveTimeScale());

        if (IsWarping())
        {
            scaledDeltaTime = System.Math.Min(
                scaledDeltaTime,
                warpRemainingTime
            );
        }

        int maxStepsPerFrame = GetMaxSimulationStepsPerFrame();
        double maxStepDt = GetMaxSimulationStepDt();
        double maxAccumulatorFeed = maxStepDt * maxStepsPerFrame;
        scaledDeltaTime = System.Math.Min(scaledDeltaTime, maxAccumulatorFeed);

        accumulator += scaledDeltaTime;
        CommitShipRenderPreviousState();

        int stepsThisFrame = 0;
        double minStepToRun = ShouldUseLargeCoastWarpSteps() ?
            1e-9 :
            fixedDt;

        while (
            (
                accumulator >= minStepToRun ||
                (IsWarping() && accumulator > 0.0)
            ) &&
            stepsThisFrame < maxStepsPerFrame
        )
        {
            double stepDt = ShouldUseLargeCoastWarpSteps() ?
                System.Math.Min(accumulator, GetMaxSimulationStepDt()) :
                fixedDt;

            if (IsWarping())
            {
                stepDt = System.Math.Min(
                    stepDt,
                    System.Math.Min(accumulator, warpRemainingTime)
                );

                double criticalEventTime =
                    GetSoonestCriticalEventTime(warpRemainingTime);

                if (
                    criticalEventTime > 0.0 &&
                    criticalEventTime <= stepDt
                )
                {
                    stepDt = criticalEventTime;
                    warpStopAfterCriticalEvent = true;
                }
            }
            else if (stepDt > fixedDt * 1.5)
            {
                double criticalEventTime = GetSoonestCriticalEventTime(stepDt);

                if (
                    criticalEventTime > 0.0 &&
                    !double.IsInfinity(criticalEventTime) &&
                    criticalEventTime <= stepDt
                )
                {
                    stepDt = System.Math.Max(criticalEventTime, 1e-6);
                }
            }

            if (
                autoBurnActive &&
                !autoBurnIsBurning &&
                hasManeuverNode &&
                GetTimeToManeuverBurnStart() > 0.0 &&
                GetTimeToManeuverBurnStart() < stepDt
            )
            {
                stepDt = GetTimeToManeuverBurnStart();
            }

            if (stepDt <= 0.0)
            {
                break;
            }

            RunSingleSimulationStep(stepDt);
            accumulator -= stepDt;
            stepsThisFrame++;

            if (IsWarping())
            {
                warpRemainingTime -= stepDt;

                if (
                    warpRemainingTime <= 0.000001 ||
                    warpStopAfterCriticalEvent
                )
                {
                    warpRemainingTime = 0.0;
                    warpStopAfterCriticalEvent = false;
                    accumulator = 0.0;
                    RebuildPreviewAnalysis();
                    break;
                }
            }
        }
    }

    int GetMaxSimulationStepsPerFrame()
    {
        if (isPaused)
        {
            return 0;
        }

        double effectiveTimeScale = GetEffectiveTimeScale();

        if (ShouldUseLargeCoastWarpSteps() && effectiveTimeScale > 1.01)
        {
            return effectiveTimeScale >= 10000.0 ? 6 : 4;
        }

        if (IsWarping() || effectiveTimeScale > 4.0)
        {
            return 12;
        }

        if (effectiveTimeScale > 1.01)
        {
            return 6;
        }

        return Math.Max(1, maxSimulationStepsPerFrameAtNormalSpeed);
    }

    double GetMaxSimulationStepDt()
    {
        if (!ShouldUseLargeCoastWarpSteps())
        {
            return fixedDt;
        }

        double scale = GetEffectiveTimeScale();

        if (scale >= 10000.0)
        {
            return 120.0;
        }

        if (scale >= 1000.0)
        {
            return 20.0;
        }

        if (scale >= 100.0)
        {
            return 4.0;
        }

        if (scale >= 50.0)
        {
            return 1.0;
        }

        if (scale >= 10.0)
        {
            return 0.25;
        }

        if (scale >= 5.0)
        {
            return 0.1;
        }

        return fixedDt;
    }

    bool ShouldUseLargeCoastWarpSteps()
    {
        return simulation != null && simulation.CanUseLargeCoastWarpSteps();
    }

    void RunSingleSimulationStep(double stepDt)
    {
        perfSimStepsThisFrame++;
        SyncLocalPlayerControlState();
        UpdateAutoBurnThrustBeforeStep();
        SyncSimulationOrientationInput();
        simulation.Update(stepDt);
        previewAnalysisSimTimeSinceCache += stepDt;
        UpdateAutoBurnAfterStep(stepDt);
        UpdateManeuverNodeTime(stepDt);
        RecordBurnDiagnosticSample();
        UpdateDescentDiagnostics(stepDt);
        UpdateAutomaticTimeSlowdown();
    }

    void UpdateCenterHudMessageState()
    {
        if (
            !string.IsNullOrEmpty(centerHudMessage) &&
            Time.unscaledTime > centerHudMessageUntil
        )
        {
            centerHudMessage = string.Empty;
        }
    }

    void SnapMapModeCameraToLaunchHemisphere()
    {
        if (
            cameraController == null ||
            !isMapModeActive ||
            !TryGetLodTerrainObject(out GameObject planetObject) ||
            planetObject == null
        )
        {
            return;
        }

        cameraTargetFocusObject = planetObject;
        cameraController.SetTarget(planetObject.transform);
        StabilizeMapCameraOnFocusBody(
            CelestialTerrainRuntime.LodBodyName,
            planetObject.transform,
            snapOrbitFocus: false,
            outwardHint: ResolveMapModeLaunchSiteOutwardMeshFrame()
        );
    }

    void RestoreLastMapCameraFocusOrLaunchHemisphere()
    {
        if (
            !string.IsNullOrEmpty(lastMapCameraFocusBodyName) &&
            bodyObjects != null &&
            bodyObjects.TryGetValue(lastMapCameraFocusBodyName, out GameObject focusObject) &&
            focusObject != null
        )
        {
            StabilizeMapCameraOnFocusBody(
                lastMapCameraFocusBodyName,
                focusObject.transform
            );
            return;
        }

        SnapMapModeCameraToLaunchHemisphere();
    }

    /// <summary>
    /// Double-click in map: render origin + camera orbit lock onto the same body.
    /// </summary>
    internal void StabilizeMapCameraOnFocusBody(
        string bodyName,
        Transform focusTransform,
        bool snapOrbitFocus = true,
        Vector3? outwardHint = null
    )
    {
        if (
            cameraController == null ||
            !isMapModeActive ||
            focusTransform == null ||
            string.IsNullOrEmpty(bodyName)
        )
        {
            return;
        }

        cameraTargetFocusObject = focusTransform.gameObject;
        cameraController.SetTarget(focusTransform);

        if (snapOrbitFocus)
        {
            lastMapCameraFocusBodyName = bodyName;
            SetOrbitFocusBody(bodyName);
        }
        else
        {
            RefreshMapModeCelestialTransforms();
        }

        Vector3 pivot = focusTransform.position;
        Vector3 outward = outwardHint.HasValue && outwardHint.Value.sqrMagnitude > 1e-8f ?
            outwardHint.Value :
            cameraController.transform.position - pivot;
        cameraController.StabilizeMapOrbitOnTarget(
            pivot,
            outward,
            ResolveMapFocusOrbitSimMeters(bodyName)
        );
    }

    double ResolveMapFocusOrbitSimMeters(string bodyName)
    {
        if (bodyName == "Planet")
        {
            return WorldVisualBridge.MapOrbitSimMetersMin;
        }

        if (simulation != null && simulation.TryGetCelestialBodyByName(bodyName, out Body body))
        {
            double visualRadius = body.radius;

            if (bodyName == "Sun")
            {
                visualRadius *= WorldVisualBridge.MapSunVisualRadiusScale;
            }

            return Math.Max(visualRadius * 3.0, 50_000.0);
        }

        if (simulation != null && simulation.IsVesselBodyName(bodyName))
        {
            return Math.Max(simulation.GetShipEnvelopeRadius() * 40.0, 2_000.0);
        }

        return WorldVisualBridge.MapOrbitSimMetersMin;
    }

    Vector3 ResolveMapModeLaunchSiteOutwardMeshFrame()
    {
        if (simulation != null)
        {
            global::SimVector3 shipRadialSim =
                simulation.GetShipRadialOutDirectionRelativeToActivePrimary();

            if (shipRadialSim.LengthSquared() > 1e-12)
            {
                return WorldVisualBridge.SimNormalToMeshFrameUnity(shipRadialSim);
            }
        }

        return WorldVisualBridge.GetLaunchSiteNormalMeshFrameUnity();
    }

    void RunMapModeGlobeDiagnostics()
    {
        if (simulation == null || !isMapModeActive)
        {
            return;
        }

        if (
            !TryGetLodTerrainObject(out GameObject planetObject) ||
            planetObject == null
        )
        {
            return;
        }

        global::SimVector3 shipRadial = simulation.GetShipRadialOutDirectionRelativeToActivePrimary();

        if (shipRadial.LengthSquared() <= 1e-12)
        {
            return;
        }

        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            shipRadial,
            out double shipLatitudeRadians,
            out double shipLongitudeRadians
        );
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            shipRadial,
            out double shipU,
            out double shipV
        );

        Vector3 planetUnityPosition = planetObject.transform.position;
        Vector3 mapOutwardMesh = planetObject.transform.InverseTransformDirection(
            ResolveMapModeLaunchSiteOutwardMeshFrame()
        );

        Vector3 mapOutwardSimUnit = WorldVisualBridge.PlanetLocalDirectionToSimUnit(mapOutwardMesh);
        global::SimVector3 mapOutwardSim = new global::SimVector3(
            mapOutwardSimUnit.x,
            mapOutwardSimUnit.y,
            mapOutwardSimUnit.z
        );
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            mapOutwardSim,
            out double mapU,
            out double mapV
        );
        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            mapOutwardSim,
            out double mapLatitudeRadians,
            out double mapLongitudeRadians
        );

        double uvDelta = Math.Sqrt(
            ((shipU - mapU) * (shipU - mapU)) + ((shipV - mapV) * (shipV - mapV))
        );

        Debug.Log(
            "MapModeGlobe: ship sim " +
            (shipLatitudeRadians * 180.0 / Math.PI).ToString("F2") + "° lat, " +
            (shipLongitudeRadians * 180.0 / Math.PI).ToString("F2") + "° lon, uv=(" +
            shipU.ToString("F3") + ", " + shipV.ToString("F3") + "). " +
            "Map globe outward uv=(" + mapU.ToString("F3") + ", " + mapV.ToString("F3") +
            "), uvDelta=" + uvDelta.ToString("F4") + ", planetUnityPos=" +
            planetUnityPosition.ToString("F1") + "."
        );

        Color earthColorAtShip = PlanetTerrainTextureResources.SampleEarthColorAtGeographicUv(
            shipU,
            shipV
        );
        Color earthColorDirect = Color.black;
        Color earthColorMirroredU = Color.black;
        if (PlanetTerrainTextureResources.EarthColorMap != null)
        {
            earthColorDirect = PlanetTerrainTextureResources.EarthColorMap.GetPixelBilinear(
                PlanetTerrainTextureResources.MapGeographicUToEarthColorTextureU((float)shipU),
                PlanetTerrainTextureResources.MapGeographicVToEarthColorTextureV((float)shipV)
            );
            earthColorMirroredU = PlanetTerrainTextureResources.EarthColorMap.GetPixelBilinear(
                PlanetTerrainTextureResources.MapGeographicUToEarthColorTextureU((float)(1.0 - shipU)),
                PlanetTerrainTextureResources.MapGeographicVToEarthColorTextureV((float)shipV)
            );
        }

        PlanetMapLayoutPolicy colorPolicy = PlanetTerrainTextureResources.EarthColorLayoutPolicy;

        Debug.Log(
            "MapModeGlobe: EarthColor at ship uv rgb=(" +
            earthColorAtShip.r.ToString("F2") + ", " +
            earthColorAtShip.g.ToString("F2") + ", " +
            earthColorAtShip.b.ToString("F2") + "). " +
            "policy=" + colorPolicy + ", texUv=(" +
            PlanetTerrainTextureResources.MapGeographicUToEarthColorTextureU((float)shipU).ToString("F3") + ", " +
            PlanetTerrainTextureResources.MapGeographicVToEarthColorTextureV((float)shipV).ToString("F3") + "), " +
            "rawTexUv rgb=(" +
            earthColorDirect.r.ToString("F2") + ", " +
            earthColorDirect.g.ToString("F2") + ", " +
            earthColorDirect.b.ToString("F2") + "), " +
            "mirrorU rgb=(" +
            earthColorMirroredU.r.ToString("F2") + ", " +
            earthColorMirroredU.g.ToString("F2") + ", " +
            earthColorMirroredU.b.ToString("F2") + "). " +
            "Map shader samples vertex geographicUv + policy; map planet localRotation forced identity."
        );
    }

    void HandleMapModeToggle()
    {
        Keyboard keyboard = Keyboard.current;

        if (
            keyboard == null ||
            keyboard.mKey == null ||
            !keyboard.mKey.wasPressedThisFrame
        )
        {
            return;
        }

        isMapModeActive = !isMapModeActive;

        if (!isMapModeActive)
        {
            ApplyFlightUniverseVisualNow();
        }

        if (
            TryGetLodTerrainObject(out GameObject planetObject) &&
            planetObject != null
        )
        {
            FlightPlanetSphere planetSphere = planetObject.GetComponent<FlightPlanetSphere>();

            if (planetSphere != null)
            {
                planetSphere.SetMapModeActive(isMapModeActive);

                if (!isMapModeActive)
                {
                    planetSphere.InvalidateLaunchSiteMeshes();
                }
            }
        }

        if (!isMapModeActive)
        {
            SyncPlanetLodRuntime();
        }

        if (cameraController != null)
        {
            cameraController.SetMapModeActive(isMapModeActive, GetVisualPositionScale());
            ScenarioVisualDefaults.ApplyToCamera(
                cameraController,
                simulation,
                isMapModeActive
            );

            if (!isMapModeActive)
            {
                pendingCameraChaseSnap = true;
                cameraController.SnapOrbitNextUpdate();
            }
        }

        ApplyDualRenderWorldMode();
        SyncVisualSubsystemScales();
        OrbitOverlayCamera.SetMapModeActive(Camera.main, isMapModeActive);

        if (isMapModeActive)
        {
            GalaxySkyboxEnvironment.ApplyMapModeVisibility();
            FlightCameraLayerMask.ApplyMapMode(Camera.main);
            Transform universeRoot = GetFlightUniverseRoot();

            if (universeRoot != null)
            {
                universeRoot.localPosition = Vector3.zero;
            }

            RefreshMapModeCelestialTransforms();
            RestoreLastMapCameraFocusOrLaunchHemisphere();
            RunMapModeGlobeDiagnostics();
        }
        else
        {
            FlightCameraLayerMask.ApplyFlightMode(Camera.main);
            RestoreShipAsFlightFocus();
            ApplyFlightUniverseVisualNow();
        }

        OrbitPreviewView orbitPreviewView = GetOrbitPreviewView();

        if (orbitPreviewView != null)
        {
            orbitPreviewView.SetMapModeActive(isMapModeActive);
        }
    }

    void ApplyDualRenderWorldMode()
    {
        GetOrCreateCelestialVisualRoot().gameObject.SetActive(true);
        Transform vesselRoot = GetOrCreateVesselVisualRoot();

        // Map mode: heavy vessel meshes are stripped in BodyView.UpdateVisualState;
        // the root stays active so billboard markers remain visible at compressed CoM.
        vesselRoot.gameObject.SetActive(true);
    }

    void HandleQuickSaveLoadInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || simulation == null)
        {
            return;
        }

        if (keyboard.f5Key.wasPressedThisFrame)
        {
            QuickSave();
        }

        if (keyboard.f9Key.wasPressedThisFrame)
        {
            QuickLoad();
        }
    }

    void QuickSave()
    {
        ShowCenterHudMessage("Идёт сохранение...");

        try
        {
            WorldSnapshot snapshot = simulation.CaptureSnapshot();
            string path = Path.Combine(
                Application.persistentDataPath,
                QuicksaveFileName
            );
            File.WriteAllBytes(path, ShipPersistence.SerializeToBinary(snapshot));
        }
        catch (Exception exception)
        {
            Debug.LogError("QuickSave failed: " + exception.Message);
        }
    }

    void QuickLoad()
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            QuicksaveFileName
        );

        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            isPaused = true;
            accumulator = 0.0;
            warpRemainingTime = 0.0;
            warpStopAfterCriticalEvent = false;
            ShowCenterHudMessage("Загрузка сохранения...");

            byte[] data = File.ReadAllBytes(path);
            WorldSnapshot snapshot = ShipPersistence.DeserializeFromBinary(data);
            simulation.LoadSnapshot(snapshot);
            ClearBodyObjects();
            lastWorldRevision = simulation.GetWorldRevision();
            RebuildAllVesselBlocksAfterSnapshotLoad();
            RefreshAvailableTargets();
            hasCachedPreviewAnalysis = false;
            previewAnalysisSimTimeSinceCache = 0.0;
            previewAnalysisTimer = 0.0f;
            RebuildPreviewAnalysis();

            if (simulationHud != null)
            {
                simulationHud.SetSimulation(simulation);
            }

            OrbitPreviewView orbitPreviewView = GetOrbitPreviewView();

            if (orbitPreviewView != null)
            {
                orbitPreviewView.SetSimulation(simulation);
            }

            DebrisView debrisView = GetComponent<DebrisView>();

            if (debrisView != null)
            {
                debrisView.SetSimulation(simulation);
            }

            isPaused = false;
        }
        catch (Exception exception)
        {
            Debug.LogError("QuickLoad failed: " + exception.Message);
        }
    }

    internal bool HasActiveCenterHudMessage()
    {
        return
            !string.IsNullOrEmpty(centerHudMessage) &&
            Time.unscaledTime <= centerHudMessageUntil;
    }

    internal void DrawCenterHudMessageGui()
    {
        if (!HasActiveCenterHudMessage())
        {
            return;
        }

        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (centerHudStyle == null)
        {
            centerHudStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
            centerHudStyle.normal.textColor = Color.white;

            centerHudBoxStyle = new GUIStyle(GUI.skin.box);
            centerHudBoxStyle.normal.background = Texture2D.grayTexture;
            centerHudBoxStyle.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 0.72f);
        }

        const float width = 520.0f;
        const float height = 56.0f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height
        );

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = 1000;
        GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.72f);
        GUI.Box(rect, GUIContent.none, centerHudBoxStyle);
        GUI.color = Color.white;
        GUI.Label(rect, centerHudMessage, centerHudStyle);
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    void UpdatePerformanceStats()
    {
        float deltaSeconds = Time.unscaledDeltaTime;

        if (deltaSeconds > 0.000001f)
        {
            float instantFps = 1.0f / deltaSeconds;
            float instantFrameMs = deltaSeconds * 1000.0f;
            perfSmoothedFps = perfSmoothedFps <= 0.0f ?
                instantFps :
                Mathf.Lerp(perfSmoothedFps, instantFps, 0.12f);
            perfSampleFpsSum += instantFps;
            perfSampleFrameCount++;
            perfSampleMaxFrameMs = Math.Max(perfSampleMaxFrameMs, instantFrameMs);
            perfFrameTimesMs[perfFrameTimeWriteIndex] = instantFrameMs;
            perfFrameTimeWriteIndex++;
            if (perfFrameTimeWriteIndex >= PerfFrameTimeWindow)
            {
                perfFrameTimeWriteIndex = 0;
            }

            if (perfFrameTimeCount < PerfFrameTimeWindow)
            {
                perfFrameTimeCount++;
            }
        }

        perfSimStepsLastFrame = perfSimStepsThisFrame;
        perfAccumulatorMs = accumulator * 1000.0;
        perfSampleMaxSimSteps = Math.Max(perfSampleMaxSimSteps, perfSimStepsLastFrame);
        perfSampleMaxDebtMs = Math.Max(perfSampleMaxDebtMs, perfAccumulatorMs);

        if (Time.unscaledTime < perfOverlayNextRefreshTime)
        {
            return;
        }

        perfDisplayFps = perfSampleFrameCount > 0 ?
            perfSampleFpsSum / perfSampleFrameCount :
            perfSmoothedFps;
        perfDisplaySimSteps = perfSampleMaxSimSteps;
        perfDisplayDebtMs = perfSampleMaxDebtMs;
        perfDisplaySimAdvanceMs = perfDisplaySimSteps * fixedDt * 1000.0;
        perfDisplayMaxFrameMs = perfSampleMaxFrameMs;
        perfDisplayOnePercentLowFps = ComputeOnePercentLowFps();

        float frameMs = perfDisplayFps > 0.0f ?
            1000.0f / perfDisplayFps :
            deltaSeconds * 1000.0f;
        perfOverlayText = string.Format(
            CultureInfo.InvariantCulture,
            "{0:0} FPS · 1% {1:0} · {2:0.0} ms · sim×{3} · debt {4:0.0} ms · adv {5:0} ms/f",
            perfDisplayFps,
            perfDisplayOnePercentLowFps,
            frameMs,
            perfDisplaySimSteps,
            perfDisplayDebtMs,
            perfDisplaySimAdvanceMs
        );

        if (perfDisplayMaxFrameMs >= 25.0f)
        {
            perfOverlayText += string.Format(
                CultureInfo.InvariantCulture,
                " · spike {0:0} ms",
                perfDisplayMaxFrameMs
            );
        }

        if (isPaused)
        {
            perfOverlayText += " · PAUSE";
            perfTimeScaleOverlayText = string.Empty;
        }
        else
        {
            double effectiveTimeScale = GetEffectiveTimeScale();
            perfTimeScaleOverlayText = string.Format(
                CultureInfo.InvariantCulture,
                "×{0:0.##}",
                effectiveTimeScale
            );
        }

        perfSampleFpsSum = 0.0f;
        perfSampleFrameCount = 0;
        perfSampleMaxFrameMs = 0.0f;
        perfSampleMaxSimSteps = 0;
        perfSampleMaxDebtMs = 0.0;
        perfOverlayNextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, perfOverlayRefreshSeconds);
    }

    float ComputeOnePercentLowFps()
    {
        int count = perfFrameTimeCount;
        if (count <= 0)
        {
            return perfDisplayFps;
        }

        Array.Copy(perfFrameTimesMs, perfFrameTimesSortScratch, count);
        Array.Sort(perfFrameTimesSortScratch, 0, count);
        int percentileIndex = Mathf.Clamp(
            Mathf.CeilToInt(count * 0.99f) - 1,
            0,
            count - 1
        );
        float frameMs = perfFrameTimesSortScratch[percentileIndex];
        if (frameMs <= 0.0001f)
        {
            return perfDisplayFps;
        }

        return 1000.0f / frameMs;
    }

    void EnsurePerfOverlayStyles()
    {
        if (perfOverlayStyle == null)
        {
            perfOverlayStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }

        if (perfTimeScaleOverlayStyle == null)
        {
            perfTimeScaleOverlayStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                fontStyle = FontStyle.Bold
            };
        }

        if (perfOverlayBoxStyle == null)
        {
            perfOverlayBoxStyle = new GUIStyle(GUI.skin.box);
            perfOverlayBoxStyle.normal.background = Texture2D.grayTexture;
        }
    }

    internal void DrawPerformanceOverlayGui()
    {
        if (!showPerformanceOverlay || string.IsNullOrEmpty(perfOverlayText))
        {
            return;
        }

        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        EnsurePerfOverlayStyles();

        bool lagging =
            perfDisplayFps < 30.0f ||
            perfDisplayOnePercentLowFps < 28.0f ||
            perfDisplaySimSteps >= 3 ||
            perfDisplayDebtMs > 20.0 ||
            perfDisplaySimAdvanceMs > 60.0;
        bool stressed =
            !lagging &&
            (
                perfDisplayFps < 55.0f ||
                perfDisplayOnePercentLowFps < 45.0f ||
                perfDisplaySimSteps >= 2 ||
                perfDisplayDebtMs > 5.0 ||
                perfDisplaySimAdvanceMs > 40.0
            );

        perfOverlayStyle.normal.textColor = lagging ?
            new Color(1.0f, 0.35f, 0.35f) :
            stressed ?
            new Color(1.0f, 0.92f, 0.35f) :
            new Color(0.55f, 1.0f, 0.65f);

        const float width = 720.0f;
        const float height = 28.0f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            8.0f,
            width,
            height
        );

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = 999;
        GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.55f);
        GUI.Box(rect, GUIContent.none, perfOverlayBoxStyle);
        GUI.color = Color.white;
        GUI.Label(rect, perfOverlayText, perfOverlayStyle);

        if (!string.IsNullOrEmpty(perfTimeScaleOverlayText))
        {
            const float timeScaleHeight = 42.0f;
            Rect timeScaleRect = new Rect(
                (Screen.width - width) * 0.5f,
                rect.yMax + 2.0f,
                width,
                timeScaleHeight
            );

            GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.45f);
            GUI.Box(timeScaleRect, GUIContent.none, perfOverlayBoxStyle);
            GUI.color = new Color(0.95f, 0.88f, 0.45f);
            perfTimeScaleOverlayStyle.normal.textColor = GUI.color;
            GUI.Label(timeScaleRect, perfTimeScaleOverlayText, perfTimeScaleOverlayStyle);
        }

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    void UpdateRenderInterpolationState()
    {
        if (!enableRenderInterpolation || simulation == null || simulation.ship == null)
        {
            renderInterpolationT = 1.0;
            return;
        }

        if (fixedDt <= 1e-12)
        {
            renderInterpolationT = 1.0;
            return;
        }

        double activeAccumulator = isMultiplayerMode ? lockstepAccumulator : accumulator;
        renderInterpolationT = 1.0 - Math.Clamp(activeAccumulator / fixedDt, 0.0, 1.0);
    }

    void CommitShipRenderPreviousState()
    {
        if (simulation == null || simulation.ship == null)
        {
            hasShipRenderPrevious = false;
            return;
        }

        Vessel activeVessel = simulation.activeVessel;

        if (activeVessel == null)
        {
            hasShipRenderPrevious = false;
            return;
        }

        shipRenderPreviousComWorld = activeVessel.GetCenterOfMassWorld();
        shipRenderPreviousPosition = activeVessel.position;
        shipRenderPreviousCenterOfMassLocal = activeVessel.centerOfMassLocal;
        shipRenderPreviousHeading = activeVessel.heading;
        shipRenderPreviousOrientation = activeVessel.orientation;
        shipRenderPreviousRadialOut = CaptureCurrentShipRadialOutSim();
        shipRenderPreviousSpinAngle = CaptureCurrentPlanetSpinAngle();
        hasShipRenderPrevious = true;
    }


    double GetActiveVesselHeadingRadians()
    {
        if (simulation == null)
        {
            return 0.0;
        }

        if (assignedVesselId == 1 || simulation.ship == null)
        {
            return simulation.ship == null ? 0.0 : simulation.ship.heading;
        }

        Vessel activeVessel = simulation.GetVesselById(assignedVesselId);
        return activeVessel == null ? simulation.ship.heading : activeVessel.heading;
    }

    global::SimVector3 GetInterpolatedShipCenterOfMassLocal()
    {
        if (simulation == null || simulation.ship == null)
        {
            return global::SimVector3.Zero();
        }

        if (
            !enableRenderInterpolation ||
            !hasShipRenderPrevious
        )
        {
            return simulation.ship.centerOfMassLocal;
        }

        return LerpSimVector3(
            shipRenderPreviousCenterOfMassLocal,
            simulation.ship.centerOfMassLocal,
            renderInterpolationT
        );
    }

    global::SimVector3 GetInterpolatedShipPosition()
    {
        if (
            !enableRenderInterpolation ||
            !hasShipRenderPrevious ||
            simulation == null ||
            simulation.ship == null
        )
        {
            return simulation == null ?
                global::SimVector3.Zero() :
                simulation.ship.position;
        }

        return LerpSimVector3(
            shipRenderPreviousPosition,
            simulation.ship.position,
            renderInterpolationT
        );
    }

    double GetInterpolatedShipHeadingRadians()
    {
        if (
            !enableRenderInterpolation ||
            !hasShipRenderPrevious ||
            simulation == null ||
            simulation.ship == null
        )
        {
            return simulation == null ? 0.0 : simulation.ship.heading;
        }

        return LerpAngleRadians(
            shipRenderPreviousHeading,
            simulation.ship.heading,
            renderInterpolationT
        );
    }

    global::SimVector3 GetInterpolatedShipCenterOfMassWorld()
    {
        if (simulation == null || simulation.ship == null)
        {
            return global::SimVector3.Zero();
        }

        if (ShouldSnapShipVisualToPhysics())
        {
            return simulation.ship.GetCenterOfMassWorld();
        }

        if (
            !enableRenderInterpolation ||
            !hasShipRenderPrevious
        )
        {
            return simulation.ship.GetCenterOfMassWorld();
        }

        return LerpSimVector3(
            shipRenderPreviousComWorld,
            simulation.ship.GetCenterOfMassWorld(),
            renderInterpolationT
        );
    }

    bool ShouldSnapShipVisualToPhysicsForDocking()
    {
        return
            simulation != null &&
            simulation.ship != null &&
            !simulation.ship.IsDocked &&
            simulation.targetVessel != null &&
            simulation.ScenarioIsVacuumDocking();
    }

    bool ShouldSnapShipVisualToPhysics()
    {
        if (simulation == null || simulation.ship == null)
        {
            return false;
        }

        if (simulation.ship.flightStatus == FlightStatus.Landed)
        {
            return true;
        }

        return ShouldSnapShipVisualToPhysicsForDocking();
    }

    float GetShipVisualRotationInterpolationT()
    {
        return ShouldSnapShipVisualToPhysics() ?
            1.0f :
            (float)renderInterpolationT;
    }

    global::SimVector3 GetShipVisualCenterOfMassLocal()
    {
        if (simulation == null || simulation.ship == null)
        {
            return global::SimVector3.Zero();
        }

        if (ShouldSnapShipVisualToPhysics())
        {
            return simulation.ship.centerOfMassLocal;
        }

        return GetInterpolatedShipCenterOfMassLocal();
    }

    global::SimVector3 GetVesselCenterOfMassWorld(Vessel vessel)
    {
        if (vessel == null)
        {
            return global::SimVector3.Zero();
        }

        return vessel.GetCenterOfMassWorld();
    }

    static global::SimVector3 LerpSimVector3(
        global::SimVector3 from,
        global::SimVector3 to,
        double t
    )
    {
        return new global::SimVector3(
            from.x + (to.x - from.x) * t,
            from.y + (to.y - from.y) * t,
            from.z + (to.z - from.z) * t
        );
    }

    static double LerpAngleRadians(double from, double to, double t)
    {
        double delta = Math.Atan2(Math.Sin(to - from), Math.Cos(to - from));
        return from + delta * t;
    }

    void ShowCenterHudMessage(string message, float durationSeconds = 2.5f)
    {
        centerHudMessage = message;
        centerHudMessageUntil = Time.unscaledTime + durationSeconds;
    }

    void RebuildAllVesselBlocksAfterSnapshotLoad()
    {
        RenderBodies();
        ForceRebuildShipVisual();
        ForceRebuildDockableVisuals();
    }

    internal global::SimVector3 GetPlannedDeltaV()
    {
        return GetActivePlannedDeltaV();
    }

    internal double GetEffectiveTimeScale()
    {
        if (isPaused)
        {
            return 0.0;
        }

        if (IsWarping())
        {
            double warpScale = warpToPointTimeScale;

            if (warpLabel == "CA")
            {
                warpScale = Math.Min(
                    warpScale,
                    GetClosestApproachWarpTimeScale()
                );
            }

            return warpScale;
        }

        return timeScale;
    }

    double GetClosestApproachFlightTimeScale()
    {
        if (!ShouldSlowForClosestApproach())
        {
            return timeScale;
        }

        return closestApproachSlowdownTimeScale;
    }

    double GetClosestApproachWarpTimeScale()
    {
        if (!ShouldSlowForClosestApproach())
        {
            return warpToPointTimeScale;
        }

        return closestApproachWarpSlowdownTimeScale;
    }

    bool ShouldSlowForClosestApproach()
    {
        double timeToClosestApproach = GetTimeToClosestApproach();

        return
            timeToClosestApproach > 0.0 &&
            timeToClosestApproach <= closestApproachSlowdownLeadTime;
    }

    double GetTimeToClosestApproach()
    {
        ClosestApproachInfo closestApproach = GetCachedClosestApproach();

        if (!closestApproach.hasApproach)
        {
            return double.PositiveInfinity;
        }

        return closestApproach.time;
    }

    internal bool IsWarping()
    {
        return warpRemainingTime > 0.0;
    }

    internal string GetWarpLabel()
    {
        return warpLabel;
    }

    internal string GetTargetBodyName()
    {
        EnsureValidTarget();
        return targetBodyName;
    }

    internal double GetTargetDistance()
    {
        EnsureValidTarget();
        return simulation.GetShipDistanceToBody(targetBodyName);
    }

    internal double GetDockingPortSeparationToTarget()
    {
        return simulation.GetDockingPortSeparationToTarget();
    }

    internal global::SimVector3 GetTargetDirection()
    {
        EnsureValidTarget();
        return simulation.GetShipDirectionToBody(targetBodyName);
    }

    internal global::SimVector3 GetTargetRelativeVelocity()
    {
        EnsureValidTarget();
        return simulation.GetShipRelativeVelocityToBody(targetBodyName);
    }

    internal double GetTargetRelativeSpeed()
    {
        EnsureValidTarget();
        return simulation.GetShipRelativeSpeedToBody(targetBodyName);
    }

    internal double GetTargetClosureRate()
    {
        global::SimVector3 relativeVelocity = GetTargetRelativeVelocity();
        global::SimVector3 directionToTarget = GetTargetDirection();
        return relativeVelocity.x * directionToTarget.x +
            relativeVelocity.y * directionToTarget.y;
    }

    internal global::SimVector3 GetTargetBodyVelocity()
    {
        if (simulation == null)
        {
            return global::SimVector3.Zero();
        }

        EnsureValidTarget();
        BodyState[] bodyStates = simulation.GetBodyStates();

        for (int i = 0; i < bodyStates.Length; i++)
        {
            if (bodyStates[i].name == targetBodyName)
            {
                return bodyStates[i].velocity;
            }
        }

        return global::SimVector3.Zero();
    }

    internal double GetBodyRadius(string bodyName)
    {
        if (simulation == null)
        {
            return 0.0;
        }

        return simulation.GetBodyRadius(bodyName);
    }

    internal Vector3 GetTargetWorldPosition()
    {
        EnsureValidTarget();

        if (string.IsNullOrEmpty(targetBodyName))
        {
            return Vector3.zero;
        }

        Vessel dockableTarget = FindDockableVessel(targetBodyName);

        if (dockableTarget != null)
        {
            return ToWorldPosition(dockableTarget.GetCenterOfMassWorld());
        }

        if (
            bodyObjects != null &&
            bodyObjects.ContainsKey(targetBodyName) &&
            IsTargetAvailable(targetBodyName)
        )
        {
            return bodyObjects[targetBodyName].transform.position;
        }

        BodyState[] bodyStates = simulation.GetBodyStates();

        for (int i = 0; i < bodyStates.Length; i++)
        {
            if (bodyStates[i].name == targetBodyName)
            {
                return ToWorldPosition(bodyStates[i].position);
            }
        }

        return Vector3.zero;
    }

    bool HasActiveTarget()
    {
        return IsTargetAvailable(targetBodyName);
    }

    internal global::SimVector3 GetRenderOrigin()
    {
        if (isMapModeActive && simulation != null)
        {
            if (TryGetOrbitFocusSimPosition(out global::SimVector3 focusPosition))
            {
                return focusPosition;
            }

            if (simulation.planet != null)
            {
                return simulation.planet.position;
            }
        }

        if (simulation != null && simulation.ship != null)
        {
            return GetInterpolatedShipCenterOfMassWorld();
        }

        return global::SimVector3.Zero();
    }

    internal string GetOrbitFocusBodyName()
    {
        return string.IsNullOrEmpty(orbitFocusBodyName) ? "Ship" : orbitFocusBodyName;
    }

    internal void SetOrbitFocusBody(string bodyName)
    {
        if (string.IsNullOrEmpty(bodyName) || simulation == null)
        {
            return;
        }

        if (
            bodyName != "Ship" &&
            !simulation.IsVesselBodyName(bodyName) &&
            !simulation.TryGetCelestialBodyByName(bodyName, out _)
        )
        {
            return;
        }

        orbitFocusBodyName = bodyName;

        if (isMapModeActive)
        {
            RefreshMapModeCelestialTransforms();

            OrbitPreviewView orbitPreviewView = GetOrbitPreviewView();

            if (orbitPreviewView != null)
            {
                orbitPreviewView.ForceRefresh();
            }

            SphereOfInfluenceView sphereOfInfluenceView =
                GetComponent<SphereOfInfluenceView>();

            if (sphereOfInfluenceView != null)
            {
                sphereOfInfluenceView.SetMapRenderContext(
                    isMapModeActive,
                    GetVisualPositionScale(),
                    GetRenderOrigin(),
                    GetOrbitFocusBodyName()
                );
            }
        }
    }

    bool TryGetOrbitFocusSimPosition(out global::SimVector3 focusPosition)
    {
        focusPosition = global::SimVector3.Zero();

        if (simulation == null)
        {
            return false;
        }

        string focusBodyName = GetOrbitFocusBodyName();

        if (simulation.IsVesselBodyName(focusBodyName) && simulation.ship != null)
        {
            focusPosition = GetInterpolatedShipCenterOfMassWorld();
            return true;
        }

        if (simulation.TryGetCelestialBodyByName(focusBodyName, out Body focusBody))
        {
            focusPosition = focusBody.position;
            return true;
        }

        return false;
    }

    internal WorldVisualBridge.RenderContext GetRenderContext()
    {
        bool vacuumDocking =
            simulation != null && simulation.ScenarioIsVacuumDocking();
        return WorldVisualBridge.Resolve(isMapModeActive, vacuumDocking);
    }

    internal float GetVisualPositionScale()
    {
        return GetRenderContext().PositionScale;
    }

    internal void SyncVisualSubsystemScales()
    {
        float positionScale = GetVisualPositionScale();
        SunVisual sunVisual = Camera.main != null ?
            Camera.main.GetComponent<SunVisual>() :
            null;

        if (sunVisual != null)
        {
            sunVisual.SetSimulation(simulation, positionScale);
            sunVisual.SetRenderMode(isMapModeActive, positionScale);
            sunVisual.SetMapRenderOrigin(GetRenderOrigin());
        }

        OrbitPreviewView orbitPreviewView = GetComponent<OrbitPreviewView>();

        if (orbitPreviewView != null)
        {
            orbitPreviewView.SetWorldScale(positionScale);
        }

        SphereOfInfluenceView sphereOfInfluenceView =
            GetComponent<SphereOfInfluenceView>();

        if (sphereOfInfluenceView != null)
        {
            sphereOfInfluenceView.SetMapRenderContext(
                isMapModeActive,
                positionScale,
                GetRenderOrigin(),
                GetOrbitFocusBodyName()
            );
        }

        AtmosphereView atmosphereView = GetComponent<AtmosphereView>();

        if (atmosphereView != null)
        {
            atmosphereView.SetWorldScale(positionScale);
        }
    }

    /// <summary>
    /// Map mode draws the globe, the celestial bodies and the vessel in Mesh Frame
    /// (<c>mesh.x = -sim.x</c>, see work_terrain.md), so orbit and preview polylines have to use
    /// the same basis. Feeding them raw sim positions mirrors every track across the prime
    /// meridian while the planet under them is flipped. Flight mode has no mesh-frame vessel,
    /// so it keeps the plain sim conversion.
    /// </summary>
    internal UnityEngine.Vector3 SimToRenderWorld(
        global::SimVector3 simWorld,
        global::SimVector3 renderOrigin
    )
    {
        float positionScale = GetVisualPositionScale();

        return isMapModeActive ?
            WorldVisualBridge.SimToUnityMeshFramePosition(
                simWorld,
                renderOrigin,
                positionScale
            ) :
            WorldVisualBridge.SimToUnityPosition(
                simWorld,
                renderOrigin,
                positionScale
            );
    }

    internal UnityEngine.Vector3 ToWorldPosition(global::SimVector3 simulationPosition)
    {
        return SimToRenderWorld(simulationPosition, GetRenderOrigin());
    }

    internal string GetShipOrientationModeName()
    {
        return ShipOrientationMode.ToString();
    }

    internal float GetThrottle()
    {
        return Mathf.Clamp01(throttle);
    }

    internal void FocusCameraOnShip()
    {
        if (
            bodyObjects.TryGetValue("Ship", out GameObject shipObject) &&
            shipObject != null
        )
        {
            cameraTargetFocusObject = shipObject;
        }

        if (cameraController != null)
        {
            cameraController.FocusOnDefaultTarget();
        }
    }

    internal string GetCameraFocusName()
    {
        if (cameraController == null)
        {
            return "Ship";
        }

        return cameraController.GetFocusTargetName();
    }

    internal bool IsCameraFocusedOnShip()
    {
        return cameraController == null || cameraController.IsFocusedOnDefaultTarget();
    }

    internal double GetTimeToPeriapsis()
    {
        return simulation.GetShipTimeToPeriapsisRelativeToActivePrimary();
    }

    internal double GetTimeToApoapsis()
    {
        return simulation.GetShipTimeToApoapsisRelativeToActivePrimary();
    }

    internal NextOrbitalEventInfo GetNextOrbitalEvent()
    {
        NextOrbitalEventInfo nextEvent = new NextOrbitalEventInfo
        {
            name = string.Empty,
            time = double.PositiveInfinity,
            hasEvent = false
        };

        TrySetNextOrbitalEvent("Pe", GetTimeToPeriapsis(), ref nextEvent);
        TrySetNextOrbitalEvent("Ap", GetTimeToApoapsis(), ref nextEvent);

        ClosestApproachInfo closestApproach = GetCachedClosestApproach();

        if (closestApproach.hasApproach)
        {
            TrySetNextOrbitalEvent("CA", closestApproach.time, ref nextEvent);
        }

        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview =
            GetCachedSphereOfInfluenceEntryPreview();

        if (sphereOfInfluenceEntryPreview.entersSphereOfInfluence)
        {
            string soiEventName = GetSphereOfInfluenceEventName(
                sphereOfInfluenceEntryPreview.targetBodyName
            );
            TrySetNextOrbitalEvent(
                soiEventName,
                sphereOfInfluenceEntryPreview.entryTime,
                ref nextEvent
            );
        }

        if (hasManeuverNode)
        {
            double burnStartTime = GetPlannedBurnStartTime();

            if (burnStartTime > 0.0)
            {
                TrySetNextOrbitalEvent(
                    "Burn start",
                    burnStartTime,
                    ref nextEvent
                );
            }

            TrySetNextOrbitalEvent(
                "Node",
                GetManeuverNodeTime(),
                ref nextEvent
            );
        }

        nextEvent.hasEvent = !double.IsInfinity(nextEvent.time);
        return nextEvent;
    }

    double GetSoonestCriticalEventTime(double withinTime)
    {
        if (withinTime <= 0.0)
        {
            return double.PositiveInfinity;
        }

        NextOrbitalEventInfo nextEvent = GetNextOrbitalEvent();

        if (
            !nextEvent.hasEvent ||
            nextEvent.time <= 0.0 ||
            nextEvent.time > withinTime
        )
        {
            return double.PositiveInfinity;
        }

        return nextEvent.time;
    }

    static void TrySetNextOrbitalEvent(
        string eventName,
        double eventTime,
        ref NextOrbitalEventInfo nextEvent
    )
    {
        if (
            double.IsNaN(eventTime) ||
            double.IsInfinity(eventTime) ||
            eventTime <= 0.0 ||
            eventTime >= nextEvent.time
        )
        {
            return;
        }

        nextEvent.name = eventName;
        nextEvent.time = eventTime;
    }

    static string GetSphereOfInfluenceEventName(string targetBodyName)
    {
        if (targetBodyName == "Sun")
        {
            return "Sun SoI";
        }

        if (targetBodyName == "Moon")
        {
            return "Moon SoI";
        }

        return "Earth SoI";
    }

    internal bool HasManeuverNode()
    {
        return hasManeuverNode;
    }

    internal double GetManeuverNodeTime()
    {
        return maneuverNodeTime;
    }

    internal global::SimVector3 GetManeuverNodeDeltaV()
    {
        return maneuverNodeDeltaV;
    }

    internal global::SimVector3 GetManeuverNodePosition()
    {
        return maneuverNodePosition;
    }

    internal bool IsAutoBurnActive()
    {
        return autoBurnActive;
    }

    internal bool IsAutoBurning()
    {
        return autoBurnIsBurning;
    }

    internal double GetEstimatedManeuverBurnDuration()
    {
        if (!hasManeuverNode || maneuverNodeDeltaV.LengthSquared() <= 0.0)
        {
            return 0.0;
        }

        return GetEstimatedBurnDuration(maneuverNodeDeltaV);
    }

    internal double GetPlannedBurnDuration()
    {
        return GetActiveBurnDuration();
    }

    internal double GetPlannedBurnStartTime()
    {
        return GetActiveBurnStartTime();
    }

    internal double GetPlannedManeuverTime()
    {
        return GetActiveManeuverTime();
    }

    double GetEstimatedBurnDuration(global::SimVector3 deltaV)
    {
        return simulation.EstimateShipBurnDurationForDeltaV(
            deltaV,
            GetCurrentThrustForce()
        );
    }

    internal double GetTimeToManeuverBurnStart()
    {
        if (!hasManeuverNode)
        {
            return double.PositiveInfinity;
        }

        double burnDuration = GetEstimatedManeuverBurnDuration();
        if (
            burnDuration <= 0.0 ||
            double.IsInfinity(burnDuration)
        )
        {
            return maneuverNodeTime;
        }

        return Math.Max(0.0, maneuverNodeTime - burnDuration * 0.5);
    }

    internal global::SimVector3 GetManeuverNodeProgradeDirection()
    {
        if (!hasManeuverNode)
        {
            return global::SimVector3.Zero();
        }

        return simulation.GetShipProgradeDirectionRelativeToActivePrimaryAtTime(
            maneuverNodeTime,
            previewAnalysisDt
        );
    }

    internal global::SimVector3 GetManeuverNodeRetrogradeDirection()
    {
        return GetManeuverNodeProgradeDirection().Multiply(-1);
    }

    internal global::SimVector3 GetManeuverNodeRadialOutDirection()
    {
        if (!hasManeuverNode)
        {
            return global::SimVector3.Zero();
        }

        return simulation.GetShipRadialOutDirectionRelativeToActivePrimaryAtTime(
            maneuverNodeTime,
            previewAnalysisDt
        );
    }

    internal global::SimVector3 GetManeuverNodeRadialInDirection()
    {
        return GetManeuverNodeRadialOutDirection().Multiply(-1);
    }

    internal void SetManeuverNode(double time)
    {
        if (double.IsInfinity(time) || double.IsNaN(time) || time <= fixedDt)
        {
            return;
        }

        maneuverNodeTime = time;
        maneuverNodePosition =
            simulation.GetShipPositionAtTime(time, previewAnalysisDt);
        plannedDeltaV = global::SimVector3.Zero();
        maneuverNodeDeltaV = global::SimVector3.Zero();
        hasManeuverNode = true;
        RebuildPreviewAnalysis();
    }

    internal void SetManeuverNode(double time, global::SimVector3 position)
    {
        SetManeuverNode(time);

        if (hasManeuverNode)
        {
            maneuverNodePosition = position;
        }
    }

    internal void MoveManeuverNode(double time)
    {
        if (double.IsInfinity(time) || double.IsNaN(time) || time <= fixedDt)
        {
            return;
        }

        if (!hasManeuverNode)
        {
            SetManeuverNode(time);
            return;
        }

        maneuverNodeTime = time;
        maneuverNodePosition =
            simulation.GetShipPositionAtTime(time, previewAnalysisDt);
        RebuildPreviewAnalysis();
    }

    internal void MoveManeuverNode(double time, global::SimVector3 position)
    {
        MoveManeuverNode(time);

        if (hasManeuverNode)
        {
            maneuverNodePosition = position;
        }
    }

    internal void SetManeuverNodeAtPeriapsis()
    {
        SetManeuverNode(GetTimeToPeriapsis());
    }

    internal void SetManeuverNodeAtApoapsis()
    {
        SetManeuverNode(GetTimeToApoapsis());
    }

    internal void WarpToManeuverStart()
    {
        if (!hasManeuverNode || maneuverNodeTime <= fixedDt)
        {
            return;
        }

        double burnStartTime = GetTimeToManeuverBurnStart();

        if (double.IsInfinity(burnStartTime))
        {
            return;
        }

        double warpTime = burnStartTime - maneuverWarpLeadTime;

        if (warpTime <= fixedDt)
        {
            return;
        }

        warpRemainingTime = warpTime;
        warpLabel = "maneuver start";
        warpStopAfterCriticalEvent = false;
        isPaused = false;
    }

    internal void StartAutoBurn()
    {
        if (!hasManeuverNode && plannedDeltaV.LengthSquared() > 0.0)
        {
            PromotePlannedDeltaVToImmediateManeuverNode();
        }

        if (!hasManeuverNode || maneuverNodeDeltaV.LengthSquared() <= 0.0)
        {
            return;
        }

        autoBurnDuration = GetEstimatedManeuverBurnDuration();

        if (double.IsInfinity(autoBurnDuration) || autoBurnDuration <= 0.0)
        {
            return;
        }

        autoBurnRemainingTime = autoBurnDuration;
        autoBurnRemainingDeltaV = maneuverNodeDeltaV.Length();
        autoBurnActive = true;
        autoBurnIsBurning = false;
        ShipOrientationMode = ShipOrientationMode.Maneuver;
        isPaused = false;
        BeginBurnDiagnostics();
    }

    void BeginBurnDiagnostics()
    {
        if (!enableBurnDiagnostics || !hasManeuverNode)
        {
            return;
        }

        diagnosticBurnDeltaV = maneuverNodeDeltaV;
        diagnosticBurnStartTime = GetActiveBurnStartTime();
        diagnosticThrustForce = GetCurrentThrustForce();
        diagnosticBurnDuration = GetEstimatedManeuverBurnDuration();
        cachedPreviewBurnWorldPath =
            simulation.SampleFiniteBurnPreviewWorldPath(
                diagnosticBurnDeltaV,
                diagnosticThrustForce,
                diagnosticBurnDuration,
                diagnosticBurnStartTime,
                previewAnalysisDt,
                fixedDt
            );
        actualBurnWorldPath.Clear();
        burnDiagnosticTrackingActive = true;
        lastBurnDriftInfo = new PreviewExecutionDriftInfo();
        ClearShipTrail();
    }

    void RecordBurnDiagnosticSample()
    {
        if (!burnDiagnosticTrackingActive || !autoBurnActive)
        {
            return;
        }

        actualBurnWorldPath.Add(simulation.ship.position);
    }

    void FinalizeBurnDiagnostics()
    {
        if (!burnDiagnosticTrackingActive)
        {
            return;
        }

        simulation.GetFiniteBurnPreviewEndWorldState(
            diagnosticBurnDeltaV,
            diagnosticThrustForce,
            diagnosticBurnDuration,
            diagnosticBurnStartTime,
            previewAnalysisDt,
            fixedDt,
            out global::SimVector3 previewEndPosition,
            out global::SimVector3 previewEndVelocity
        );

        lastBurnDriftInfo = Simulation.CompareBurnExecution(
            cachedPreviewBurnWorldPath,
            actualBurnWorldPath.ToArray(),
            previewEndPosition,
            previewEndVelocity,
            simulation.ship.position,
            simulation.ship.velocity
        );
        lastBurnDriftInfo.previewThrustForce = diagnosticThrustForce;
        lastBurnDriftInfo.actualThrustForce = GetCurrentThrustForce();
        lastBurnDriftInfo.throttle = GetThrottle();
        burnDiagnosticTrackingActive = false;
    }

    internal PreviewExecutionDriftInfo GetLastBurnDriftInfo()
    {
        return lastBurnDriftInfo;
    }

    internal global::SimVector3[] GetCachedPreviewBurnWorldPath()
    {
        return cachedPreviewBurnWorldPath;
    }

    internal bool HasBurnDiagnosticOverlay()
    {
        return cachedPreviewBurnWorldPath.Length > 1;
    }

    internal bool ExpectsDescentDrift()
    {
        return descentDiagnosticTrackingActive && simulation.IsShipInAtmosphere();
    }

    internal PreviewExecutionDriftInfo GetLastDescentDriftInfo()
    {
        return lastDescentDriftInfo;
    }

    internal global::SimVector3[] GetCachedDescentPreviewWorldPath()
    {
        return cachedDescentPreviewWorldPath;
    }

    internal bool HasDescentPreviewOverlay()
    {
        return cachedDescentPreviewWorldPath.Length > 1;
    }

    void UpdateDescentDiagnostics(double stepDt)
    {
        if (simulation == null)
        {
            return;
        }

        bool inAtmosphere = simulation.IsShipInAtmosphere();

        if (inAtmosphere && !wasInAtmosphereLastStep)
        {
            StartDescentDiagnostics();
        }

        if (descentDiagnosticTrackingActive && inAtmosphere)
        {
            actualDescentWorldPath.Add(simulation.ship.position);
        }

        if (!inAtmosphere && wasInAtmosphereLastStep)
        {
            FinalizeDescentDiagnostics();
        }

        wasInAtmosphereLastStep = inAtmosphere;

        if (
            descentDiagnosticTrackingActive &&
            inAtmosphere &&
            simulation.GetFlightStatus() != FlightStatus.Flying
        )
        {
            FinalizeDescentDiagnostics();
        }
    }

    void StartDescentDiagnostics()
    {
        if (!simulation.ShouldShowDescentPreview())
        {
            return;
        }

        bool useAtmosphericSas =
            ShipOrientationMode != ShipOrientationMode.Manual;
        cachedDescentPreviewWorldPath = simulation.SampleDescentPreviewWorldPath(
            useAtmosphericSas,
            3600.0,
            previewAnalysisDt,
            previewAnalysisStepCount
        );
        actualDescentWorldPath.Clear();
        actualDescentWorldPath.Add(simulation.ship.position);
        descentDiagnosticTrackingActive = cachedDescentPreviewWorldPath.Length > 1;
        lastDescentDriftInfo = new PreviewExecutionDriftInfo();
    }

    void FinalizeDescentDiagnostics()
    {
        if (!descentDiagnosticTrackingActive)
        {
            return;
        }

        global::SimVector3 previewEndPosition = cachedDescentPreviewWorldPath[
            cachedDescentPreviewWorldPath.Length - 1
        ];
        global::SimVector3 previewEndVelocity = simulation.ship.velocity;

        lastDescentDriftInfo = Simulation.CompareBurnExecution(
            cachedDescentPreviewWorldPath,
            actualDescentWorldPath.ToArray(),
            previewEndPosition,
            previewEndVelocity,
            simulation.ship.position,
            simulation.ship.velocity
        );
        descentDiagnosticTrackingActive = false;
    }

    internal bool ExpectsBurnDrift()
    {
        global::SimVector3 deltaV = GetActivePlannedDeltaV();

        if (deltaV.LengthSquared() <= 0.0)
        {
            return false;
        }

        double burnDuration = GetActiveBurnDuration();

        if (
            burnDuration <= 0.0 ||
            double.IsInfinity(burnDuration) ||
            double.IsNaN(burnDuration)
        )
        {
            return false;
        }

        double orbitalPeriod =
            simulation.GetShipOrbitalPeriodRelativeToActivePrimary();

        if (
            orbitalPeriod <= 0.0 ||
            double.IsInfinity(orbitalPeriod)
        )
        {
            return false;
        }

        bool significantBurn = burnDuration > orbitalPeriod * 0.04;

        if (!significantBurn)
        {
            return false;
        }

        bool lowThrottle = GetThrottle() < 0.95f;
        bool soiChangeDuringBurn =
            simulation.WillActivePrimaryChangeDuringFiniteBurn(
                deltaV,
                GetCurrentThrustForce(),
                burnDuration,
                GetActiveBurnStartTime(),
                previewAnalysisDt,
                fixedDt
            );

        return lowThrottle || soiChangeDuringBurn;
    }

    void PromotePlannedDeltaVToImmediateManeuverNode()
    {
        maneuverNodeDeltaV = plannedDeltaV;
        maneuverNodePosition = simulation.ship.position;
        maneuverNodeTime = 0.0;
        plannedDeltaV = global::SimVector3.Zero();
        hasManeuverNode = true;
    }

    internal void ClearManeuverNode()
    {
        ClearManeuverPlan();
    }

    internal global::SimVector3[] GetPlannedOrbitPath(int pointCount)
    {
        if (hasManeuverNode)
        {
            return simulation.GetFiniteBurnPreviewShipOrbitPathRelativeToActivePrimary(
                maneuverNodeDeltaV,
                GetCurrentThrustForce(),
                GetEstimatedManeuverBurnDuration(),
                pointCount,
                GetActiveBurnStartTime(),
                previewAnalysisDt,
                fixedDt
            );
        }

        return simulation.GetFiniteBurnPreviewShipOrbitPathRelativeToActivePrimary(
            plannedDeltaV,
            GetCurrentThrustForce(),
            GetEstimatedBurnDuration(plannedDeltaV),
            pointCount,
            0.0,
            previewAnalysisDt,
            fixedDt
        );
    }

    internal double GetPlannedOrbitPeriod()
    {
        return simulation.GetFiniteBurnPreviewShipOrbitalPeriodRelativeToActivePrimary(
            GetActivePlannedDeltaV(),
            GetCurrentThrustForce(),
            GetActiveBurnDuration(),
            GetActiveBurnStartTime(),
            previewAnalysisDt,
            fixedDt
        );
    }

    internal void WarpBy(double seconds)
    {
        if (simulation == null || seconds <= 0)
        {
            return;
        }

        warpRemainingTime = seconds;
        warpLabel = "point";
        warpStopAfterCriticalEvent = false;
        isPaused = false;
    }

    internal void WarpToClosestApproach()
    {
        ClosestApproachInfo closestApproach = GetCachedClosestApproach();

        if (!closestApproach.hasApproach || closestApproach.time <= fixedDt)
        {
            return;
        }

        double warpTime = closestApproach.time - closestApproachWarpLeadTime;

        if (warpTime <= fixedDt)
        {
            warpTime = closestApproach.time;
        }

        warpRemainingTime = warpTime;
        warpLabel = "CA";
        warpStopAfterCriticalEvent = true;
        isPaused = false;
    }

    internal void WarpToPeriapsis()
    {
        WarpToOrbitalEvent(GetTimeToPeriapsis(), "Pe");
    }

    internal void WarpToApoapsis()
    {
        WarpToOrbitalEvent(GetTimeToApoapsis(), "Ap");
    }

    void WarpToOrbitalEvent(double eventTime, string label)
    {
        if (double.IsInfinity(eventTime) || eventTime <= fixedDt)
        {
            return;
        }

        warpRemainingTime = eventTime;
        warpLabel = label;
        warpStopAfterCriticalEvent = false;
        isPaused = false;
    }

    internal ClosestApproachInfo GetCachedClosestApproach()
    {
        EnsurePreviewAnalysis();
        ClosestApproachInfo closestApproach = cachedClosestApproach;
        closestApproach.time = SubtractPreviewAnalysisElapsedTime(
            closestApproach.time
        );
        return closestApproach;
    }

    internal SphereOfInfluenceEntryPreview GetCachedSphereOfInfluenceEntryPreview()
    {
        EnsurePreviewAnalysis();
        SphereOfInfluenceEntryPreview preview =
            cachedSphereOfInfluenceEntryPreview;
        preview.entryTime = SubtractPreviewAnalysisElapsedTime(
            preview.entryTime
        );
        return preview;
    }

    void UpdatePreviewAnalysis()
    {
        if (simulation != null && simulation.scenario == SimulationScenario.DockingApproach)
        {
            return;
        }

        if (TryFinishPendingPreviewSoI())
        {
            return;
        }

        if (isPaused && hasCachedPreviewAnalysis && !PlannedDeltaVChanged())
        {
            return;
        }

        if (Time.unscaledDeltaTime > 0.08f)
        {
            return;
        }

        previewAnalysisTimer += Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (
            !hasCachedPreviewAnalysis ||
            PlannedDeltaVChanged() ||
            previewAnalysisTimer >= previewAnalysisUpdateInterval ||
            previewAnalysisSimTimeSinceCache >= previewAnalysisDt
        )
        {
            BeginPreviewRebuildClosestApproach();
        }
    }

    void EnsurePreviewAnalysis()
    {
        if (simulation != null && simulation.scenario == SimulationScenario.DockingApproach)
        {
            return;
        }

        if (!hasCachedPreviewAnalysis && !previewRebuildSoIPending)
        {
            BeginPreviewRebuildClosestApproach();
            return;
        }

        TryFinishPendingPreviewSoI();
    }

    bool TryFinishPendingPreviewSoI()
    {
        if (!previewRebuildSoIPending || Time.frameCount < previewSoIReadyFrame)
        {
            return false;
        }

        FinishPreviewRebuildSoI();
        return true;
    }

    void BeginPreviewRebuildClosestApproach()
    {
        if (simulation == null)
        {
            return;
        }

        cachedPreviewPlannedDeltaV = plannedDeltaV;
        cachedPreviewManeuverNodeDeltaV = maneuverNodeDeltaV;
        cachedPreviewHasManeuverNode = hasManeuverNode;
        cachedPreviewManeuverNodeTime = maneuverNodeTime;
        cachedPreviewTargetBodyName = targetBodyName;
        cachedPreviewThrustForce = GetCurrentThrustForce();
        cachedClosestApproach = simulation.GetClosestApproachToBody(
            targetBodyName,
            GetActivePlannedDeltaV(),
            previewAnalysisStepCount,
            previewAnalysisDt,
            GetActiveBurnStartTime(),
            GetCurrentThrustForce(),
            GetActiveBurnDuration(),
            fixedDt
        );
        previewRebuildSoIPending = true;
        previewSoIReadyFrame = Time.frameCount + 1;
    }

    void FinishPreviewRebuildSoI()
    {
        if (simulation == null)
        {
            previewRebuildSoIPending = false;
            return;
        }

        cachedSphereOfInfluenceEntryPreview =
            simulation.GetActiveSphereOfInfluenceEntryPreview(
                GetActivePlannedDeltaV(),
                previewAnalysisStepCount,
                previewAnalysisDt,
                GetActiveBurnStartTime(),
                GetCurrentThrustForce(),
                GetActiveBurnDuration(),
                fixedDt
            );
        previewAnalysisTimer = 0.0f;
        previewAnalysisSimTimeSinceCache = 0.0;
        hasCachedPreviewAnalysis = true;
        previewRebuildSoIPending = false;
    }

    void RebuildPreviewAnalysis()
    {
        BeginPreviewRebuildClosestApproach();
        FinishPreviewRebuildSoI();
    }

    double SubtractPreviewAnalysisElapsedTime(double cachedTime)
    {
        double remainingTime = cachedTime - previewAnalysisSimTimeSinceCache;

        if (remainingTime < 0.0)
        {
            return 0.0;
        }

        return remainingTime;
    }

    bool PlannedDeltaVChanged()
    {
        return plannedDeltaV
            .Subtract(cachedPreviewPlannedDeltaV)
            .LengthSquared() > 0.000001 ||
            maneuverNodeDeltaV
                .Subtract(cachedPreviewManeuverNodeDeltaV)
                .LengthSquared() > 0.000001 ||
            cachedPreviewHasManeuverNode != hasManeuverNode ||
            cachedPreviewTargetBodyName != targetBodyName ||
            Mathf.Abs(cachedPreviewThrustForce - GetCurrentThrustForce()) > 0.0001f;
    }

    internal global::SimVector3 GetActivePlannedDeltaV()
    {
        return hasManeuverNode ? maneuverNodeDeltaV : plannedDeltaV;
    }

    internal void AddManeuverDeltaV(global::SimVector3 deltaV)
    {
        if (hasManeuverNode)
        {
            maneuverNodeDeltaV = maneuverNodeDeltaV.Add(deltaV);
            return;
        }

        plannedDeltaV = plannedDeltaV.Add(deltaV);
    }

    double GetActiveManeuverTime()
    {
        return hasManeuverNode ? maneuverNodeTime : 0.0;
    }

    double GetActiveBurnStartTime()
    {
        return hasManeuverNode ? GetTimeToManeuverBurnStart() : 0.0;
    }

    double GetActiveBurnDuration()
    {
        global::SimVector3 activePlannedDeltaV = GetActivePlannedDeltaV();

        if (activePlannedDeltaV.LengthSquared() <= 0.0)
        {
            return 0.0;
        }

        return GetEstimatedBurnDuration(activePlannedDeltaV);
    }

    void UpdateManeuverNodeTime(double dt)
    {
        if (!hasManeuverNode)
        {
            return;
        }

        maneuverNodeTime -= dt;

        if (maneuverNodeTime < 0.0)
        {
            maneuverNodeTime = 0.0;
        }
    }

    internal float GetCurrentThrustForce()
    {
        if (simulation == null)
        {
            return 0.0f;
        }

        return (float)(simulation.GetActiveStageThrust() * GetThrottle());
    }

    internal void TryStage()
    {
        if (simulation == null)
        {
            return;
        }

        simulation.TryJettisonStage();
    }

    public void LoadScenario(SimulationScenario newScenario)
    {
        scenario = playSession.NormalizePlayableScenario(newScenario);
        playSession.SaveLastScenario(scenario);
        ReinitializeSimulation();
    }

    public void RestartCurrentScenario()
    {
        ReinitializeSimulation();
    }

    public SimulationScenario[] GetMenuScenarios()
    {
        return playSession.GetMenuScenarios();
    }

    public string GetScenarioDisplayKey()
    {
        return playSession.GetScenarioDisplayKey(scenario);
    }

    public string GetScenarioDisplayKey(SimulationScenario menuScenario)
    {
        return playSession.GetScenarioDisplayKey(menuScenario);
    }

    internal void ApplyShipConfiguration(
        bool enableStage2,
        bool enableStage3,
        float stage1FuelFraction,
        float stage2FuelFraction,
        float stage3FuelFraction
    )
    {
        if (simulation == null)
        {
            return;
        }

        List<StageData> stages = new List<StageData>
        {
            StageData.CreateBooster(stage1FuelFraction),
        };

        if (enableStage2)
        {
            stages.Add(StageData.CreateOrbiter(stage2FuelFraction));
        }

        if (enableStage3)
        {
            stages.Add(StageData.CreateUpperStage(stage3FuelFraction));
        }

        simulation.ConfigureShipStages(stages);
        simulation.RefreshInitialShipSnapshot();
    }

    internal void SaveCurrentShipConfiguration(string fileName)
    {
        if (simulation == null || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        string path = Path.Combine(Application.persistentDataPath, fileName);
        simulation.SaveShip(path);
    }

    internal void LoadShipConfiguration(string fileName)
    {
        if (simulation == null || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        string path = Path.Combine(Application.persistentDataPath, fileName);
        simulation.ApplyLoadedShip(path);
        simulation.RefreshInitialShipSnapshot();
    }

    internal global::SimVector3 GetPlanningProgradeDirection()
    {
        if (hasManeuverNode)
        {
            return simulation.GetShipProgradeDirectionRelativeToActivePrimaryAtTime(
                maneuverNodeTime,
                previewAnalysisDt
            );
        }

        return simulation.GetShipProgradeDirectionRelativeToActivePrimary();
    }

    internal void SetManeuverDeltaV(global::SimVector3 deltaV)
    {
        if (hasManeuverNode)
        {
            maneuverNodeDeltaV = deltaV;
            return;
        }

        plannedDeltaV = deltaV;
    }

    internal global::SimVector3 GetPlanningRetrogradeDirection()
    {
        if (hasManeuverNode)
        {
            return simulation.GetShipRetrogradeDirectionRelativeToActivePrimaryAtTime(
                maneuverNodeTime,
                previewAnalysisDt
            );
        }

        return simulation.GetShipRetrogradeDirectionRelativeToActivePrimary();
    }

    internal global::SimVector3 GetPlanningRadialOutDirection()
    {
        if (hasManeuverNode)
        {
            return simulation.GetShipRadialOutDirectionRelativeToActivePrimaryAtTime(
                maneuverNodeTime,
                previewAnalysisDt
            );
        }

        return simulation.GetShipRadialOutDirectionRelativeToActivePrimary();
    }

    internal global::SimVector3 GetPlanningRadialInDirection()
    {
        if (hasManeuverNode)
        {
            return simulation.GetShipRadialInDirectionRelativeToActivePrimaryAtTime(
                maneuverNodeTime,
                previewAnalysisDt
            );
        }

        return simulation.GetShipRadialInDirectionRelativeToActivePrimary();
    }

    internal global::SimVector3 GetPlanningCircularizeDeltaV()
    {
        return simulation.GetFiniteBurnCircularizeDeltaVRelativeToActivePrimary(
            GetCurrentThrustForce(),
            GetActiveManeuverTime(),
            GetActiveBurnStartTime(),
            previewAnalysisDt,
            fixedDt,
            circularizeFiniteBurnIterations
        );
    }

    internal void ApplyOrWarpManeuver()
    {
        if (hasManeuverNode)
        {
            if (GetTimeToManeuverBurnStart() > fixedDt * 2.0)
            {
                WarpToManeuverStart();
                return;
            }

            StartAutoBurn();
            return;
        }

        StartAutoBurn();
    }

    internal void ClearManeuverPlan()
    {
        plannedDeltaV = global::SimVector3.Zero();
        maneuverNodeDeltaV = global::SimVector3.Zero();
        maneuverNodePosition = global::SimVector3.Zero();
        maneuverNodeTime = 0.0;
        hasManeuverNode = false;
        autoBurnActive = false;
        autoBurnIsBurning = false;
        autoBurnDuration = 0.0;
        autoBurnRemainingTime = 0.0;
        autoBurnRemainingDeltaV = 0.0;
        simulation.SetShipThrust(global::SimVector3.Zero());
    }

    void UpdateAutoBurnThrustBeforeStep()
    {
        if (!autoBurnActive)
        {
            return;
        }

        if (!hasManeuverNode || maneuverNodeDeltaV.LengthSquared() <= 0.0)
        {
            ClearManeuverPlan();
            return;
        }

        ShipOrientationMode = ShipOrientationMode.Maneuver;

        if (
            autoBurnIsBurning ||
            GetTimeToManeuverBurnStart() <= fixedDt
        )
        {
            autoBurnIsBurning = true;
            float currentThrustForce = GetCurrentThrustForce();

            if (currentThrustForce <= 0.000001f)
            {
                simulation.SetShipThrust(global::SimVector3.Zero());
                return;
            }

            simulation.SetShipThrust(
                maneuverNodeDeltaV.Normalize().Multiply(currentThrustForce)
            );
            return;
        }

        simulation.SetShipThrust(global::SimVector3.Zero());
    }

    void UpdateAutoBurnAfterStep(double dt)
    {
        if (!autoBurnActive || !autoBurnIsBurning)
        {
            return;
        }

        double appliedDeltaV = simulation.GetShipThrustAcceleration() * dt;
        autoBurnRemainingDeltaV = System.Math.Max(
            0.0,
            autoBurnRemainingDeltaV - appliedDeltaV
        );
        autoBurnRemainingTime = System.Math.Max(
            0.0,
            autoBurnRemainingTime - dt * GetThrottle()
        );

        if (autoBurnRemainingDeltaV > 0.05)
        {
            return;
        }

        simulation.SetShipThrust(global::SimVector3.Zero());
        FinalizeBurnDiagnostics();
        ClearManeuverPlan();
        RebuildPreviewAnalysis();
    }

    internal double GetManeuverStep()
    {
        return plannedDeltaVStep;
    }

    internal void CycleTarget()
    {
        if (simulation == null || targetBodyNames.Length == 0)
        {
            return;
        }

        int startIndex = FindTargetIndex();

        for (int offset = 1; offset <= targetBodyNames.Length; offset++)
        {
            int nextIndex = (startIndex + offset) % targetBodyNames.Length;
            string nextTargetBodyName = targetBodyNames[nextIndex];

            if (simulation.HasBody(nextTargetBodyName))
            {
                targetBodyName = nextTargetBodyName;
                SyncTargetVessel();
                RebuildPreviewAnalysis();
                return;
            }

            if (FindDockableVessel(nextTargetBodyName) != null)
            {
                targetBodyName = nextTargetBodyName;
                SyncTargetVessel();
                RebuildPreviewAnalysis();
                return;
            }
        }
    }

    void SyncTargetVessel()
    {
        if (simulation == null)
        {
            return;
        }

        simulation.targetVessel = FindDockableVessel(targetBodyName);
    }

    int FindTargetIndex()
    {
        for (int i = 0; i < targetBodyNames.Length; i++)
        {
            if (targetBodyNames[i] == targetBodyName)
            {
                return i;
            }
        }

        return -1;
    }

    void EnsureValidTarget()
    {
        if (simulation == null)
        {
            return;
        }

        if (IsTargetAvailable(targetBodyName))
        {
            SyncTargetVessel();
            return;
        }

        for (int i = 0; i < targetBodyNames.Length; i++)
        {
            if (IsTargetAvailable(targetBodyNames[i]))
            {
                targetBodyName = targetBodyNames[i];
                SyncTargetVessel();
                return;
            }
        }

        targetBodyName = string.Empty;
        simulation.targetVessel = null;
    }

    internal string GetLastDockingMessage()
    {
        return simulation == null ?
            string.Empty :
            simulation.GetLastDockingMessage();
    }

    internal OrbitPreviewView GetOrbitPreviewView()
    {
        return GetComponent<OrbitPreviewView>();
    }

    internal bool GetShowVesselTargetMarkers()
    {
        OrbitPreviewView view = GetOrbitPreviewView();
        return view != null && view.showVesselTargetMarkers;
    }

    internal void SetShowVesselTargetMarkers(bool enabled)
    {
        OrbitPreviewView view = GetOrbitPreviewView();

        if (view != null)
        {
            view.showVesselTargetMarkers = enabled;
        }
    }

    void UpdateAutomaticTimeSlowdown()
    {
        ClampTimeScaleForAtmosphere();
        ClampTimeScaleForActiveThrust();

        if (simulation.activePrimaryBodyName == lastActivePrimaryBodyName)
        {
            return;
        }

        lastActivePrimaryBodyName = simulation.activePrimaryBodyName;
        timeScale = 1.0;
        CancelWarpForAtmosphere();

        if (autoBurnActive && autoBurnIsBurning)
        {
            RebuildPreviewAnalysis();
        }

        SyncLodBodyWithActivePrimary();
    }

    void SyncLodBodyWithActivePrimary()
    {
        if (simulation == null || bodyObjects == null)
        {
            return;
        }

        Body terrain = simulation.GetVisualTerrainBody();
        string nextName = terrain != null &&
            simulation.moon != null &&
            terrain == simulation.moon ?
            CelestialTerrainRuntime.MoonBodyName :
            CelestialTerrainRuntime.EarthBodyName;

        if (!CelestialTerrainRuntime.TryBindLodBodyForSoI(nextName))
        {
            return;
        }

        Debug.Log("SimulationRunner: flight LOD → " + nextName + " (SoI).");
        BodyView.RebindLodGlobes(bodyObjects);
        ApplyFlightUniverseVisualNow();
        SyncPlanetLodRuntime();

        if (
            TryGetLodTerrainObject(out GameObject planetObject) &&
            planetObject != null
        )
        {
            FlightPlanetSphere planetSphere = planetObject.GetComponent<FlightPlanetSphere>();

            if (planetSphere != null)
            {
                planetSphere.ApplyPresentationAfterSoISwap(isMapModeActive);
                planetSphere.RequestViewerLodBootstrap();
            }
        }
    }

    void ClampTimeScaleForAtmosphere()
    {
        if (simulation == null)
        {
            return;
        }

        double maxScale = simulation.GetRecommendedMaxTimeScale();

        if (double.IsPositiveInfinity(maxScale))
        {
            return;
        }

        if (GetEffectiveTimeScale() <= maxScale)
        {
            return;
        }

        CancelWarpForAtmosphere();
        timeScale = CapTimeScaleToMax(maxScale);
    }

    void ClampTimeScaleForActiveThrust()
    {
        if (simulation == null)
        {
            return;
        }

        simulation.SetShipThrottle(GetThrottle());

        if (!simulation.IsThrustTimeWarpRestricted())
        {
            return;
        }

        double maxScale = simulation.GetRecommendedMaxTimeScale();

        if (double.IsPositiveInfinity(maxScale))
        {
            return;
        }

        if (GetEffectiveTimeScale() <= maxScale)
        {
            return;
        }

        CancelWarpForAtmosphere();
        timeScale = 1.0;
        simulation.lastFlightEventMessage =
            global::Simulation.ThrustTimeWarpBlockedMessage;
    }

    void CancelWarpForAtmosphere()
    {
        if (!IsWarping())
        {
            return;
        }

        warpRemainingTime = 0.0;
        warpStopAfterCriticalEvent = false;
        accumulator = 0.0;
    }

    double CapTimeScaleToMax(double maxScale)
    {
        double cappedScale = timeScaleSteps[0];

        for (int i = 0; i < timeScaleSteps.Length; i++)
        {
            if (timeScaleSteps[i] <= maxScale)
            {
                cappedScale = timeScaleSteps[i];
            }
            else
            {
                break;
            }
        }

        return cappedScale;
    }

    internal void IncreaseTimeScale()
    {
        isPaused = false;
        int currentIndex = FindNearestTimeScaleIndex();
        int nextIndex = Mathf.Min(currentIndex + 1, timeScaleSteps.Length - 1);
        timeScale = timeScaleSteps[nextIndex];
    }

    internal void DecreaseTimeScale()
    {
        isPaused = false;
        int currentIndex = FindNearestTimeScaleIndex();
        int nextIndex = Mathf.Max(currentIndex - 1, 0);
        timeScale = timeScaleSteps[nextIndex];
    }

    int FindNearestTimeScaleIndex()
    {
        int nearestIndex = 0;
        double nearestDistance = Math.Abs(timeScale - timeScaleSteps[0]);

        for (int i = 1; i < timeScaleSteps.Length; i++)
        {
            double distance = Math.Abs(timeScale - timeScaleSteps[i]);

            if (distance < nearestDistance)
            {
                nearestIndex = i;
                nearestDistance = distance;
            }
        }

        return nearestIndex;
    }

    void RenderBodies()
    {
        if (simulation == null)
        {
            return;
        }

        ApplyDualRenderWorldMode();

        WorldVisualBridge.RenderContext renderContext = GetRenderContext();
        global::SimVector3 renderOrigin = GetRenderOrigin();
        float positionScale = renderContext.PositionScale;
        BodyState[] bodyStates = simulation.GetBodyStates();
        double shipAltitudeAbovePlanet = simulation.GetShipAltitudeAbovePlanet();

        SyncVisualSubsystemScales();

        MapMarkerPlacement.SetPlanetCenterWorldResolver(() =>
        {
            if (simulation == null)
            {
                return Vector3.zero;
            }

            Body terrainBody = simulation.GetVisualTerrainBody() ?? simulation.planet;

            if (terrainBody == null)
            {
                return Vector3.zero;
            }

            return SimToRenderWorld(
                terrainBody.position,
                renderOrigin
            );
        });

        for (int i = 0; i < bodyStates.Length; i++)
        {
            BodyState bodyState = bodyStates[i];

            if (simulation.IsVesselBodyName(bodyState.name))
            {
                continue;
            }

            if (
                renderContext.VacuumDocking &&
                bodyState.name == "Planet"
            )
            {
                if (bodyObjects.TryGetValue(bodyState.name, out GameObject planetObject))
                {
                    planetObject.SetActive(false);
                }

                continue;
            }

            bool showCelestial = renderContext.ShowCelestialMeshes;

            if (!renderContext.IsMapMode)
            {
                showCelestial = WorldVisualBridge.ShouldRenderCelestialBodyInFlight(
                    bodyState.name
                );
            }

            RenderCelestialBodyState(
                bodyState,
                renderOrigin,
                showCelestial,
                renderContext.IsMapMode,
                shipAltitudeAbovePlanet
            );
        }

        if (simulation.ship != null)
        {
            RenderVesselBodyState(
                simulation.ship,
                renderOrigin,
                positionScale,
                renderContext
            );
        }

        for (int i = 0; i < simulation.dockableVessels.Count; i++)
        {
            Vessel dockable = simulation.dockableVessels[i];

            if (dockable == simulation.ship)
            {
                continue;
            }

            RenderVesselBodyState(
                dockable,
                renderOrigin,
                positionScale,
                renderContext
            );
        }

        HideRemovedDockableVesselViews(bodyStates);
        UpdateDockedPartnerVisual();
        CaptureGraphicsBridgeDebug();
    }

    void CaptureGraphicsBridgeDebug()
    {
        Vector3 shipPosition = Vector3.zero;
        Vector3 shipScale = Vector3.one;
        Vector3 planetPosition = Vector3.zero;
        Vector3 planetScale = Vector3.one;

        if (bodyObjects.TryGetValue("Ship", out GameObject shipObject) && shipObject != null)
        {
            shipPosition = shipObject.transform.position;
            shipScale = shipObject.transform.localScale;
        }

        if (
            TryGetLodTerrainObject(out GameObject planetObject) &&
            planetObject != null
        )
        {
            planetPosition = planetObject.transform.position;
            planetScale = planetObject.transform.localScale;
        }

        graphicsBridgeDebug = new GraphicsBridgeDebugSnapshot
        {
            IsMapMode = isMapModeActive,
            ShipUnityPosition = shipPosition,
            PlanetUnityPosition = planetPosition,
            ShipUnityLocalScale = shipScale,
            PlanetUnityLocalScale = planetScale,
            UnitySceneDistance = Vector3.Distance(shipPosition, planetPosition),
        };
    }

    void RenderCelestialBodyState(
        BodyState bodyState,
        global::SimVector3 renderOrigin,
        bool visible,
        bool isMapMode,
        double altitudeAbovePlanetMeters
    )
    {
        if (!bodyObjects.ContainsKey(bodyState.name))
        {
            GameObject bodyObject = BodyView.Create(bodyState.name);
            ParentCelestialVisual(bodyObject);
            bodyObjects[bodyState.name] = bodyObject;
        }

        global::SimVector3 planetCenterWorld = bodyState.position;
        global::SimVector3 shipCenterWorld = renderOrigin;

        if (bodyState.name == "Planet" && simulation != null && simulation.planet != null)
        {
            planetCenterWorld = simulation.planet.position;
        }

        BodyView.UpdateCelestialBody(
            bodyObjects[bodyState.name],
            bodyState,
            planetCenterWorld,
            shipCenterWorld,
            visible,
            isMapMode,
            altitudeAbovePlanetMeters,
            simulation
        );
    }

    internal float GetShipBottomExtentUnityForPlanetRender()
    {
        if (simulation == null || simulation.ship == null)
        {
            return 0.0f;
        }

        simulation.ship.EnsureStageBlocks();

        global::SimVector3 outwardDirection;

        if (
            !isMapModeActive &&
            simulation.ship.flightStatus == FlightStatus.Landed &&
            simulation.planet != null
        )
        {
            outwardDirection = simulation.GetShipRadialOutDirectionRelativeToActivePrimary();

            if (outwardDirection.LengthSquared() <= 1e-12)
            {
                outwardDirection = LandingConfig.GetSurfaceLaunchPadNormal();
            }
        }
        else if (simulation.ship.flightStatus != FlightStatus.Landed)
        {
            return 0.0f;
        }
        else
        {
            global::SimVector3 forwardWorld = simulation.ship.orientation.Rotate(
                global::SimVector3.UnitZ()
            );

            if (forwardWorld.LengthSquared() <= 1e-12)
            {
                forwardWorld = global::SimVector3.UnitY();
            }

            outwardDirection = forwardWorld.Normalize();
        }

        double bottomExtentSim = simulation.ship.GetBottomExtentAlongWorldDirection(
            outwardDirection
        );
        bottomExtentSim = LaunchPadConstants.GetEffectiveBottomExtentMeters(bottomExtentSim);

        if (
            !isMapModeActive &&
            simulation.ship.flightStatus == FlightStatus.Landed &&
            bottomExtentSim < LandingConfig.ShipCollisionRadius
        )
        {
            bottomExtentSim = LandingConfig.ShipCollisionRadius;
        }

        return (float)bottomExtentSim;
    }

    void RenderVesselBodyState(
        Vessel vessel,
        global::SimVector3 renderOrigin,
        float positionScale,
        WorldVisualBridge.RenderContext renderContext
    )
    {
        if (vessel == null)
        {
            return;
        }

        if (!bodyObjects.TryGetValue(vessel.name, out GameObject vesselObject))
        {
            vesselObject = BodyView.Create(vessel.name);
            ParentVesselVisual(vesselObject);
            bodyObjects[vessel.name] = vesselObject;
        }

        BodyView.EnforceVesselScaleIsolation(vesselObject);

        global::SimVector3 centerOfMassWorld = vessel == simulation.ship ?
            GetInterpolatedShipCenterOfMassWorld() :
            GetVesselCenterOfMassWorld(vessel);

        RenderBodyState(
            new BodyState(
                vessel.name,
                centerOfMassWorld,
                vessel.velocity,
                vessel.isStatic,
                false,
                vessel.radius
            ),
            renderOrigin,
            positionScale,
            vessel
        );
    }

    void RenderBodyState(
        BodyState bodyState,
        global::SimVector3 renderOrigin,
        float positionScale,
        Vessel dockableVessel = null
    )
    {
        if (!bodyObjects.ContainsKey(bodyState.name))
        {
            GameObject vesselObject = BodyView.Create(bodyState.name);
            ParentVesselVisual(vesselObject);
            bodyObjects[bodyState.name] = vesselObject;
        }

        if (bodyState.name == "Ship" || bodyState.name == "Station")
        {
            BodyView.EnsureVesselVisualAttachments(
                bodyObjects[bodyState.name],
                bodyState.name
            );
        }

        global::SimVector3 centerOfMassWorld = bodyState.position;

        if (bodyState.name == "Ship")
        {
            centerOfMassWorld = GetInterpolatedShipCenterOfMassWorld();
        }
        else if (dockableVessel != null)
        {
            centerOfMassWorld = GetVesselCenterOfMassWorld(dockableVessel);
        }

        BodyView.EnsureVesselUnityScale(bodyObjects[bodyState.name]);
        BodyView.EnforceVesselScaleIsolation(bodyObjects[bodyState.name]);

        if (isMapModeActive || bodyState.name != "Ship")
        {
            BodyView.UpdateVesselBody(
                bodyObjects[bodyState.name],
                bodyState,
                centerOfMassWorld,
                renderOrigin,
                positionScale,
                useMeshFramePosition: isMapModeActive,
                showDirectionVectors: !isMapModeActive
            );
        }
        else
        {
            Transform shipTransform = bodyObjects[bodyState.name].transform;
            shipTransform.localPosition = Vector3.zero;
            shipTransform.localRotation = Quaternion.identity;
        }

        if (bodyState.name == "Ship")
        {
            Vector3 flightSurfaceNormal = WorldVisualBridge.SimNormalToUnity(
                simulation.GetShipSurfaceUp()
            );
            global::SimQuaternion shipOrientation = simulation.ship.orientation;
            global::SimQuaternion previousShipOrientation = hasShipRenderPrevious ?
                shipRenderPreviousOrientation :
                shipOrientation;

            int[] destroyedBlockIds = simulation.ConsumePendingDestroyedBlockIds();

            if (destroyedBlockIds.Length > 0)
            {
                BodyView.ProcessDestroyedBlocks(
                    bodyObjects[bodyState.name],
                    destroyedBlockIds,
                    GetVisualPositionScale()
                );
                lastShipLayoutRevision = -1;
            }

            if (!isMapModeActive)
            {
                RenderVesselBlockVisual(
                    bodyObjects[bodyState.name],
                    simulation.ship,
                    ref lastShipLayoutRevision,
                    simulation.ship.IsDocked,
                    GetShipVisualCenterOfMassLocal()
                );
            }

            if (isMapModeActive)
            {
                BodyView.ApplyVesselOrientation(
                    bodyObjects[bodyState.name].transform,
                    previousShipOrientation,
                    shipOrientation,
                    GetShipVisualRotationInterpolationT()
                );
            }
            else
            {
                BodyView.ApplyFlightVesselFrameZero(
                    bodyObjects[bodyState.name],
                    previousShipOrientation,
                    shipOrientation,
                    GetShipVisualRotationInterpolationT(),
                    flightSurfaceNormal
                );
                RefreshLaunchPadVisual();
            }

            if (!isMapModeActive)
            {
                Transform thrustRotationSource = BodyView.GetVesselOrientationTransform(
                    bodyObjects[bodyState.name]
                );
                float renderedShipRotationDegrees =
                    thrustRotationSource != null ?
                        thrustRotationSource.eulerAngles.y :
                        0.0f;
                BodyView.SyncEngineExhaustToActiveThrust(
                    bodyObjects[bodyState.name],
                    simulation.ship.GetShipBlockViews(),
                    simulation.ship.centerOfMassLocal
                );

                BodyView.UpdateThrustDirection(
                    bodyObjects[bodyState.name],
                    renderedShipRotationDegrees
                );
                BodyView.UpdatePlannedDeltaV(
                    bodyObjects[bodyState.name],
                    GetActivePlannedDeltaV()
                );

                if (HasActiveTarget())
                {
                    BodyView.UpdateTargetDirection(
                        bodyObjects[bodyState.name],
                        GetTargetWorldPosition()
                    );
                    BodyView.UpdateTargetRelativeVelocity(
                        bodyObjects[bodyState.name],
                        GetTargetRelativeVelocity()
                    );
                }
                else
                {
                    Vector3 shipPosition = bodyObjects[bodyState.name].transform.position;
                    BodyView.UpdateTargetDirection(bodyObjects[bodyState.name], shipPosition);
                    BodyView.UpdateTargetRelativeVelocity(
                        bodyObjects[bodyState.name],
                        global::SimVector3.Zero()
                    );
                }
            }

            BodyView.UpdateEngineExhaust(
                bodyObjects[bodyState.name],
                isMapModeActive ? 0.0f : GetThrottle(),
                !isMapModeActive &&
                ShouldShowEngineExhaustForVesselName(bodyState.name)
            );

            BodyView.UpdateVisualState(
                bodyObjects[bodyState.name],
                bodyState.name,
                isMapModeActive
            );
            BodyView.SetComDebugMarker(
                bodyObjects[bodyState.name],
                !isMapModeActive &&
                simulationHud != null &&
                simulationHud.showComDebug
            );
            BodyView.UpdateFocusPickCollider(
                bodyObjects[bodyState.name],
                (float)simulation.ship.envelopeRadius
            );

            FlightStatus flightStatus = simulation.GetFlightStatus();

            if (
                flightStatus == FlightStatus.Crashed &&
                lastRenderedFlightStatus != FlightStatus.Crashed
            )
            {
                ExplosionEffects.PlayCrash(
                    bodyObjects[bodyState.name].transform.position,
                    GetVisualPositionScale()
                );
            }

            lastRenderedFlightStatus = flightStatus;

            string dockingMessage = simulation.GetLastDockingMessage();

            if (
                !string.IsNullOrEmpty(dockingMessage) &&
                dockingMessage != lastRenderedDockingMessage &&
                dockingMessage.Contains("Стыковка успешна")
            )
            {
                ExplosionEffects.PlayDockingCapture(
                    bodyObjects[bodyState.name].transform.position,
                    GetVisualPositionScale()
                );
                OnDockingSucceeded();
            }

            lastRenderedDockingMessage = dockingMessage;
        }
        else if (dockableVessel != null && bodyState.name == dockableVessel.name)
        {
            Vector3 flightSurfaceNormal = WorldVisualBridge.SimNormalToUnity(
                simulation.GetShipSurfaceUp()
            );

            if (!isMapModeActive)
            {
                RenderVesselBlockVisual(
                    bodyObjects[bodyState.name],
                    dockableVessel,
                    ref lastStationLayoutRevision
                );
            }

            if (isMapModeActive)
            {
                BodyView.ApplyVesselOrientation(
                    bodyObjects[bodyState.name].transform,
                    dockableVessel.orientation,
                    dockableVessel.orientation,
                    1.0f
                );
            }
            else
            {
                BodyView.ApplyFlightVesselFrameZero(
                    bodyObjects[bodyState.name],
                    dockableVessel.orientation,
                    dockableVessel.orientation,
                    1.0f,
                    flightSurfaceNormal
                );
            }

            BodyView.SyncEngineExhaustToActiveThrust(
                bodyObjects[bodyState.name],
                dockableVessel.GetShipBlockViews(),
                dockableVessel.centerOfMassLocal
            );

            BodyView.UpdateEngineExhaust(
                bodyObjects[bodyState.name],
                isMapModeActive ? 0.0f : GetThrottle(),
                !isMapModeActive &&
                ShouldShowEngineExhaustForVesselName(bodyState.name)
            );
            BodyView.UpdateFocusPickCollider(
                bodyObjects[bodyState.name],
                (float)dockableVessel.envelopeRadius
            );
            BodyView.UpdateVisualState(
                bodyObjects[bodyState.name],
                bodyState.name,
                isMapModeActive
            );
        }
    }

    void UpdateShipCameraFollow()
    {
        if (cameraController == null || simulation == null)
        {
            return;
        }

        if (cameraTargetFocusObject == null)
        {
            EnsureDefaultCameraFocusTarget();
        }

        if (cameraTargetFocusObject == null)
        {
            return;
        }

        Vessel focusVessel = FindSimulationVesselByName(cameraTargetFocusObject.name);

        if (focusVessel == null)
        {
            focusVessel = simulation.ship;
        }

        if (focusVessel == null)
        {
            return;
        }

        Vector3 chasePivot = GetCameraFocusPivotWorldPosition();
        Transform focusTransform = cameraTargetFocusObject != null ?
            BodyView.GetVesselOrientationTransform(cameraTargetFocusObject) :
            null;

        if (focusTransform == null && cameraTargetFocusObject != null)
        {
            focusTransform = cameraTargetFocusObject.transform;
        }

        if (focusTransform != null && !isMapModeActive)
        {
            cameraController.SetOrbitFrameRotation(focusTransform.rotation);
        }

        cameraController.UpdateShipFollow(
            chasePivot,
            0.0f,
            GetCameraFocusEnvelopeRadius(),
            GetVisualPositionScale()
        );

        if (isMapModeActive)
        {
            cameraController.SetMapOrbitPivotWorld(chasePivot);
        }

        if (pendingCameraChaseSnap)
        {
            Vector3 flightSurfaceNormal = WorldVisualBridge.SimNormalToUnity(
                simulation.GetShipSurfaceUp()
            );
            Quaternion shipRotation = focusTransform != null ?
                focusTransform.rotation :
                WorldVisualBridge.SimOrientationToFlightVisualRotation(
                    focusVessel.orientation,
                    flightSurfaceNormal
                );
            Vector3 chaseUp = shipRotation * Vector3.up;
            Vector3 chaseOffset = shipRotation * new Vector3(
                52.0f,
                14.0f,
                -6.0f
            );

            if (chaseUp.sqrMagnitude <= 1e-8f)
            {
                chaseUp = WorldVisualBridge.FlightFrameUp;
            }

            cameraController.SnapInitialOrbitToChase(
                chasePivot,
                chaseOffset,
                chaseUp,
                shipRotation
            );
            pendingCameraChaseSnap = false;
        }
    }

    Vector3 GetCameraFocusPivotWorldPosition()
    {
        if (!isMapModeActive && simulation != null)
        {
            if (
                cameraTargetFocusObject == null ||
                cameraTargetFocusObject.name == "Planet" ||
                (
                    !simulation.IsVesselBodyName(cameraTargetFocusObject.name) &&
                    cameraTargetFocusObject.name != "Station"
                )
            )
            {
                RestoreShipAsFlightFocus(applyCameraTarget: false);
            }
        }

        if (!isMapModeActive && cameraTargetFocusObject != null && simulation != null)
        {
            if (
                cameraTargetFocusObject.name == "Ship" ||
                cameraTargetFocusObject.name == "Station"
            )
            {
                Vessel focusVessel = FindSimulationVesselByName(cameraTargetFocusObject.name);

                if (focusVessel == null)
                {
                    focusVessel = simulation.ship;
                }

                if (focusVessel != null)
                {
                    global::SimVector3 comLocal = focusVessel == simulation.ship ?
                        GetShipVisualCenterOfMassLocal() :
                        focusVessel.centerOfMassLocal;

                    return BodyView.GetVesselCenterOfMassWorldPosition(
                        cameraTargetFocusObject,
                        comLocal,
                        anchorVesselRootAtCom: true
                    );
                }
            }

            if (
                BodyView.TryGetVesselVisualPivotWorld(
                    cameraTargetFocusObject,
                    out Vector3 visualPivot
                )
            )
            {
                return visualPivot;
            }
        }

        return GetMapModeCameraPivotWorldPosition();
    }

    Vector3 GetMapModeCameraPivotWorldPosition()
    {
        if (!isMapModeActive)
        {
            return cameraTargetFocusObject != null ?
                cameraTargetFocusObject.transform.position :
                Vector3.zero;
        }

        if (cameraTargetFocusObject != null)
        {
            return cameraTargetFocusObject.transform.position;
        }

        string focusName = GetOrbitFocusBodyName();

        if (
            bodyObjects != null &&
            !string.IsNullOrEmpty(focusName) &&
            bodyObjects.TryGetValue(focusName, out GameObject focusObject) &&
            focusObject != null
        )
        {
            return focusObject.transform.position;
        }

        if (TryGetLodTerrainObject(out GameObject planetObject) && planetObject != null)
        {
            return planetObject.transform.position;
        }

        return Vector3.zero;
    }

    void EnsureDefaultCameraFocusTarget()
    {
        if (cameraTargetFocusObject != null)
        {
            return;
        }

        RestoreShipAsFlightFocus(applyCameraTarget: false);
    }

    void RestoreShipAsFlightFocus(bool applyCameraTarget = true)
    {
        orbitFocusBodyName = "Ship";

        if (
            !bodyObjects.TryGetValue("Ship", out GameObject shipObject) ||
            shipObject == null
        )
        {
            return;
        }

        cameraTargetFocusObject = shipObject;

        if (applyCameraTarget && cameraController != null)
        {
            cameraController.SetTarget(shipObject.transform);
        }
    }

    void OnCameraFocusTargetChanged(Transform focusTransform)
    {
        cameraTargetFocusObject =
            focusTransform != null ? focusTransform.gameObject : null;

        if (cameraController != null && focusTransform != null)
        {
            cameraController.SetTarget(focusTransform);
        }

        if (!isMapModeActive || focusTransform == null)
        {
            return;
        }

        string bodyName = ResolveBodyNameFromTransform(focusTransform);

        if (string.IsNullOrEmpty(bodyName))
        {
            return;
        }

        StabilizeMapCameraOnFocusBody(bodyName, focusTransform);
    }

    string ResolveBodyNameFromTransform(Transform focusTransform)
    {
        if (focusTransform == null)
        {
            return string.Empty;
        }

        GameObject resolved = ResolveClickedFocusObject(focusTransform.gameObject);

        if (resolved == null)
        {
            return string.Empty;
        }

        foreach (KeyValuePair<string, GameObject> entry in bodyObjects)
        {
            if (entry.Value == resolved)
            {
                return entry.Key;
            }
        }

        return string.Empty;
    }

    Body FindSimulationBodyByName(string bodyName)
    {
        if (simulation == null || string.IsNullOrEmpty(bodyName))
        {
            return null;
        }

        if (simulation.planet != null && simulation.planet.name == bodyName)
        {
            return simulation.planet;
        }

        if (simulation.moon != null && simulation.moon.name == bodyName)
        {
            return simulation.moon;
        }

        if (simulation.sun != null && simulation.sun.name == bodyName)
        {
            return simulation.sun;
        }

        return null;
    }

    Transform TryPickMapFocusFromScreen(Vector2 mousePosition)
    {
        if (!isMapModeActive || bodyObjects == null)
        {
            return null;
        }

        Camera pickCamera = cameraController != null ?
            cameraController.GetComponent<Camera>() :
            Camera.main;

        if (pickCamera == null)
        {
            return null;
        }

        Transform best = null;
        float bestDiscPixels = float.MaxValue;
        const float minClickPixels = 28f;

        foreach (KeyValuePair<string, GameObject> entry in bodyObjects)
        {
            GameObject bodyObject = entry.Value;

            if (bodyObject == null || !bodyObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 worldPosition = bodyObject.transform.position;
            Vector3 screen = pickCamera.WorldToScreenPoint(worldPosition);

            if (screen.z <= 0.05f)
            {
                continue;
            }

            float discPixels = EstimateMapBodyScreenRadiusPixels(
                pickCamera,
                bodyObject,
                entry.Key,
                worldPosition,
                screen
            );
            float hitPixels = Mathf.Max(discPixels, minClickPixels);
            float pixelDistance = Vector2.Distance(
                mousePosition,
                new Vector2(screen.x, screen.y)
            );

            if (pixelDistance > hitPixels)
            {
                continue;
            }

            if (discPixels > bestDiscPixels)
            {
                continue;
            }

            bestDiscPixels = discPixels;
            best = bodyObject.transform;
        }

        return best;
    }

    static float EstimateMapBodyScreenRadiusPixels(
        Camera pickCamera,
        GameObject bodyObject,
        string bodyName,
        Vector3 worldPosition,
        Vector3 centerScreen
    )
    {
        float worldRadius = EstimateMapBodyWorldRadius(bodyObject, bodyName);
        Vector3 edgeWorld = worldPosition + pickCamera.transform.right * Mathf.Max(worldRadius, 1f);
        Vector3 edgeScreen = pickCamera.WorldToScreenPoint(edgeWorld);

        if (edgeScreen.z <= 0.05f)
        {
            return 28f;
        }

        return Vector2.Distance(
            new Vector2(centerScreen.x, centerScreen.y),
            new Vector2(edgeScreen.x, edgeScreen.y)
        );
    }

    static float EstimateMapBodyWorldRadius(GameObject bodyObject, string bodyName)
    {
        Renderer renderer = bodyObject.GetComponentInChildren<Renderer>();

        if (renderer != null && renderer.bounds.extents.sqrMagnitude > 1e-8f)
        {
            Vector3 extents = renderer.bounds.extents;
            return Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
        }

        if (bodyName == "Planet")
        {
            return WorldVisualBridge.FlightPlanetRadiusUnity * WorldVisualBridge.MapPositionScale;
        }

        return Mathf.Max(bodyObject.transform.lossyScale.x, 1f);
    }

    Transform ResolveCameraFocusRoot(Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return null;
        }

        GameObject resolved = ResolveClickedFocusObject(hitTransform.gameObject);
        return resolved != null ? resolved.transform : hitTransform;
    }

    GameObject ResolveClickedFocusObject(GameObject hitObject)
    {
        if (hitObject == null)
        {
            return null;
        }

        Transform current = hitObject.transform;

        while (current != null)
        {
            foreach (KeyValuePair<string, GameObject> entry in bodyObjects)
            {
                if (entry.Value != null && entry.Value.transform == current)
                {
                    return entry.Value;
                }
            }

            current = current.parent;
        }

        return null;
    }

    double GetCameraFocusEnvelopeRadius()
    {
        if (cameraTargetFocusObject == null || simulation == null)
        {
            return simulation != null ? simulation.GetShipEnvelopeRadius() : 1.0;
        }

        Vessel vessel = FindSimulationVesselByName(cameraTargetFocusObject.name);

        if (vessel != null)
        {
            return vessel.envelopeRadius;
        }

        Body body = FindSimulationBodyByName(cameraTargetFocusObject.name);

        if (body != null)
        {
            return body.radius;
        }

        return simulation.GetShipEnvelopeRadius();
    }

    Vessel FindSimulationVesselByName(string vesselName)
    {
        if (simulation == null || string.IsNullOrEmpty(vesselName))
        {
            return null;
        }

        if (simulation.ship != null && simulation.ship.name == vesselName)
        {
            return simulation.ship;
        }

        for (int i = 0; i < simulation.dockableVessels.Count; i++)
        {
            Vessel dockable = simulation.dockableVessels[i];
            if (dockable != null && dockable.name == vesselName)
            {
                return dockable;
            }
        }

        for (int i = 0; i < simulation.debrisVessels.Count; i++)
        {
            Vessel debris = simulation.debrisVessels[i];
            if (debris != null && debris.name == vesselName)
            {
                return debris;
            }
        }

        return null;
    }

    void UpdateDockedPartnerVisual()
    {
        if (simulation == null || !simulation.ship.hasDockedPartnerVisual)
        {
            return;
        }

        string partnerName = simulation.ship.dockedPartnerName;

        if (string.IsNullOrEmpty(partnerName))
        {
            return;
        }

        if (
            !bodyObjects.TryGetValue("Ship", out GameObject shipObject) ||
            shipObject == null
        )
        {
            return;
        }

        if (!bodyObjects.TryGetValue(partnerName, out GameObject partnerObject))
        {
            partnerObject = BodyView.Create(partnerName);
            ParentVesselVisual(partnerObject);
            bodyObjects[partnerName] = partnerObject;
        }

        if (partnerObject.transform.parent != shipObject.transform)
        {
            partnerObject.transform.SetParent(shipObject.transform, false);
        }

        SimVector3 localOffset = simulation.ship.dockedPartnerLocalOffset;
        partnerObject.transform.localPosition = BodyView.ToUnityVesselLocalOffset(
            localOffset
        );

        float shipWorldEulerZ = ShipRotationDegrees - 90.0f;
        float partnerWorldEulerZ =
            (float)(
                (
                    simulation.ship.heading +
                    simulation.ship.dockedPartnerRelativeHeading
                ) *
                Mathf.Rad2Deg
            ) - 90.0f;
        partnerObject.transform.localRotation = Quaternion.Euler(
            0.0f,
            0.0f,
            partnerWorldEulerZ - shipWorldEulerZ
        );
    }

    void HideRemovedDockableVesselViews(BodyState[] bodyStates)
    {
        if (simulation == null)
        {
            return;
        }

        System.Collections.Generic.HashSet<string> activeNames =
            new System.Collections.Generic.HashSet<string>();

        for (int i = 0; i < bodyStates.Length; i++)
        {
            activeNames.Add(bodyStates[i].name);
        }

        for (int i = 0; i < simulation.dockableVessels.Count; i++)
        {
            activeNames.Add(simulation.dockableVessels[i].name);
        }

        System.Collections.Generic.List<string> staleNames =
            new System.Collections.Generic.List<string>();

        foreach (System.Collections.Generic.KeyValuePair<string, GameObject> entry in bodyObjects)
        {
            if (
                simulation.ship.hasDockedPartnerVisual &&
                entry.Key == simulation.ship.dockedPartnerName
            )
            {
                continue;
            }

            if (
                entry.Key == "Ship" ||
                !simulation.IsVesselBodyName(entry.Key) ||
                activeNames.Contains(entry.Key)
            )
            {
                continue;
            }

            staleNames.Add(entry.Key);
        }

        for (int i = 0; i < staleNames.Count; i++)
        {
            string staleName = staleNames[i];

            if (
                bodyObjects.TryGetValue(staleName, out GameObject staleObject) &&
                staleObject != null
            )
            {
                Destroy(staleObject);
            }

            bodyObjects.Remove(staleName);
        }
    }

    void ClearShipTrail()
    {
        if (!bodyObjects.ContainsKey("Ship"))
        {
            return;
        }

        ShipTrailView shipTrailView =
            bodyObjects["Ship"].GetComponent<ShipTrailView>();

        if (shipTrailView != null)
        {
            shipTrailView.Clear();
        }
    }

    void SetupCamera()
    {
        Camera flightCamera = FlightCameraBootstrap.EnsureMainCamera(transform);

        if (flightCamera == null)
        {
            Debug.LogWarning("SetupCamera: no MainCamera is available after flight bootstrap.");
            return;
        }

        cameraController = flightCamera.GetComponent<CameraController>();

        if (cameraController == null)
        {
            cameraController = flightCamera.gameObject.AddComponent<CameraController>();
        }

        cameraController.ResolveFocusRoot = ResolveCameraFocusRoot;
        cameraController.ResolveMapClick = TryPickMapFocusFromScreen;
        cameraController.FocusTargetChanged -= OnCameraFocusTargetChanged;
        cameraController.FocusTargetChanged += OnCameraFocusTargetChanged;

        EnsureDefaultCameraFocusTarget();

        if (cameraTargetFocusObject != null)
        {
            cameraController.SetTarget(cameraTargetFocusObject.transform);
        }

        cameraController.SetMapModeActive(isMapModeActive, GetVisualPositionScale());

        OrbitOverlayCamera.Install(flightCamera);
        OrbitOverlayCamera.SetMapModeActive(flightCamera, isMapModeActive);

        if (isMapModeActive)
        {
            FlightCameraLayerMask.ApplyMapMode(flightCamera);
        }
        else
        {
            FlightCameraLayerMask.ApplyFlightMode(flightCamera);
        }

        ScenarioVisualDefaults.ApplyToCamera(
            cameraController,
            simulation,
            isMapModeActive
        );

        if (isMapModeActive)
        {
            SnapMapModeCameraToLaunchHemisphere();
        }

        SyncVisualSubsystemScales();

        StarfieldBackground starfieldBackground =
            flightCamera.GetComponent<StarfieldBackground>();

        if (starfieldBackground == null)
        {
            starfieldBackground = flightCamera.gameObject.AddComponent<StarfieldBackground>();
        }

        starfieldBackground.Setup(flightCamera);

        SunVisual sunVisual = flightCamera.GetComponent<SunVisual>();

        if (sunVisual == null)
        {
            sunVisual = flightCamera.gameObject.AddComponent<SunVisual>();
        }

        sunVisual.Setup(flightCamera);
        sunVisual.SetSimulation(simulation, GetVisualPositionScale());

        FlightCameraBootstrap.EnsureMainCamera(transform);
    }

    void SetupHud()
    {
        simulationHud = GetComponent<SimulationHud>();

        if (simulationHud == null)
        {
            simulationHud = gameObject.AddComponent<SimulationHud>();
        }

        simulationHud.SetSimulation(simulation);
        simulationHud.SetRunner(this);

        ShipConfigurationHud shipConfigurationHud =
            GetComponent<ShipConfigurationHud>();

        if (shipConfigurationHud == null)
        {
            shipConfigurationHud = gameObject.AddComponent<ShipConfigurationHud>();
        }

        shipConfigurationHud.SetRunner(this);

        SimulationScenarioMenu scenarioMenu = GetComponent<SimulationScenarioMenu>();

        if (scenarioMenu == null)
        {
            scenarioMenu = gameObject.AddComponent<SimulationScenarioMenu>();
        }

        scenarioMenu.SetRunner(this);

        DevSettingsHud devSettingsHud = GetComponent<DevSettingsHud>();

        if (devSettingsHud == null)
        {
            devSettingsHud = gameObject.AddComponent<DevSettingsHud>();
        }

        devSettingsHud.SetRunner(this);
        SetupMapModeUiOcclusionPass();
    }

    void SetupMapModeUiOcclusionPass()
    {
        MapModeUiOcclusionPass occlusionPass = GetComponent<MapModeUiOcclusionPass>();

        if (occlusionPass == null)
        {
            occlusionPass = gameObject.AddComponent<MapModeUiOcclusionPass>();
        }

        occlusionPass.Bind(this);
    }

    void SetupOrbitPreview()
    {
        OrbitPreviewView orbitPreviewView = GetComponent<OrbitPreviewView>();

        if (orbitPreviewView == null)
        {
            orbitPreviewView = gameObject.AddComponent<OrbitPreviewView>();
        }

        orbitPreviewView.SetSimulation(simulation);
        orbitPreviewView.SetRunner(this);
        orbitPreviewView.SetWorldScale(GetVisualPositionScale());
        orbitPreviewView.SetMapModeActive(isMapModeActive);
    }

    void SetupDebrisView()
    {
        DebrisView debrisView = GetComponent<DebrisView>();

        if (debrisView == null)
        {
            debrisView = gameObject.AddComponent<DebrisView>();
        }

        debrisView.SetSimulation(simulation);
        debrisView.SetRunner(this);
    }

    void SetupSphereOfInfluenceView()
    {
        SphereOfInfluenceView sphereOfInfluenceView =
            GetComponent<SphereOfInfluenceView>();

        if (sphereOfInfluenceView == null)
        {
            sphereOfInfluenceView =
                gameObject.AddComponent<SphereOfInfluenceView>();
        }

        sphereOfInfluenceView.SetSimulation(simulation);
        sphereOfInfluenceView.SetMapRenderContext(
            isMapModeActive,
            GetVisualPositionScale(),
            GetRenderOrigin(),
            GetOrbitFocusBodyName()
        );
    }

    void SetupAtmosphereView()
    {
        AtmosphereView atmosphereView = GetComponent<AtmosphereView>();

        if (atmosphereView == null)
        {
            atmosphereView = gameObject.AddComponent<AtmosphereView>();
        }

        atmosphereView.SetSimulation(simulation);
        atmosphereView.SetWorldScale(GetVisualPositionScale());
    }

    void AlignFlightUniverseZeroFrame()
    {
        FlightUniverseRoot universe = FlightUniverseRoot.Ensure(this);

        if (universe != null)
        {
            universe.AlignZeroFrame();
        }
    }

    void SetupLaunchPadView()
    {
        launchPadView = GetComponent<LaunchPadView>();

        if (launchPadView == null)
        {
            launchPadView = gameObject.AddComponent<LaunchPadView>();
        }

        launchPadView.SetSimulation(simulation);
        launchPadView.SetRunner(this);
        launchPadView.SetLaunchSitePrefab(launchSitePrefab);
    }
}
