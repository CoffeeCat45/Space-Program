using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class OrbitPreviewView : MonoBehaviour
{
    public int previewPointCount = 512;
    public int updateEveryFrames = 20;
    public bool showClosestApproachWorldMarker = false;
    public bool showClosestApproachPoint = true;
    public float closestApproachPointSize = 0.18f;
    public float closestApproachPointMoveThreshold = 2.0f;
    public bool showSphereOfInfluenceEntryPreview = true;
    public float sphereOfInfluenceEntryPointSize = 0.22f;
    public float closestApproachMarkerSmoothing = 0.25f;
    public int hoverPredictionStepCount = 1000;
    public double hoverPredictionDt = 0.2;
    public float hoverPickRadiusPixels = 14.0f;
    public float hoverPointSize = 0.2f;
    public float maneuverHandleLength = 1.6f;
    public float maneuverHandleWidth = 0.045f;
    public float maneuverNodePickRadiusPixels = 20.0f;
    public float maneuverNodeDragRadiusPixels = 180.0f;
    public double nearCircularMarkerEccentricity = 0.001;
    public float highlightedMarkerScale = 1.35f;
    public float eventLabelOffsetY = 0.38f;
    public bool showGhostTarget = true;
    public bool showVesselTargetMarkers = false;
    public bool showOrbitLegend = true;

    static readonly Color EventColorPeAp = new Color(0.0f, 1.0f, 1.0f);
    static readonly Color EventColorPlanned = new Color(1.0f, 0.85f, 0.0f);
    static readonly Color EventColorCA = new Color(1.0f, 0.75f, 0.95f);
    static readonly Color EventColorGhost = new Color(1.0f, 0.45f, 0.9f);
    static readonly Color EventColorSoI = new Color(1.0f, 0.88f, 0.2f);
    static readonly Color EventColorNode = new Color(1.0f, 0.88f, 0.2f);
    static readonly Color EventColorBurn = new Color(1.0f, 0.55f, 0.15f);

    static readonly Color CurrentOrbitLineColor = new Color(0.0f, 0.95f, 1.0f, 0.8f);
    static readonly Color PlannedOrbitLineColor = new Color(1.0f, 0.82f, 0.0f, 0.9f);
    static readonly Color SoiEntryLineColor = new Color(1.0f, 0.85f, 0.0f, 0.95f);
    static readonly Color MoonSoiOrbitLineColor = new Color(0.4f, 1.0f, 0.55f, 0.95f);
    static readonly Color BackgroundCelestialOrbitLineColor =
        new Color(0.58f, 0.68f, 0.78f, 0.42f);
    const float OrbitMarkerScreenHeightFraction = 0.080f;
    const float OrbitMarkerTextMeshSizeMultiplier = 2.75f;

    Simulation simulation;
    SimulationRunner runner;
    float worldScale;
    LineRenderer lineRenderer;
    LineRenderer plannedLineRenderer;
    LineRenderer sphereOfInfluenceEntryLineRenderer;
    LineRenderer moonSphereOfInfluenceOrbitLineRenderer;
    LineRenderer closestApproachLineRenderer;
    LineRenderer burnPreviewPathLineRenderer;
    LineRenderer descentPreviewPathLineRenderer;
    LineRenderer maneuverProgradeLineRenderer;
    LineRenderer maneuverRetrogradeLineRenderer;
    LineRenderer maneuverRadialOutLineRenderer;
    LineRenderer maneuverRadialInLineRenderer;
    readonly System.Collections.Generic.Dictionary<string, LineRenderer> backgroundOrbitLineRenderers =
        new System.Collections.Generic.Dictionary<string, LineRenderer>();
    readonly System.Collections.Generic.Dictionary<string, UnityEngine.Vector3[]> backgroundOrbitWorldBuffers =
        new System.Collections.Generic.Dictionary<string, UnityEngine.Vector3[]>();
    TextMesh periapsisMarker;
    TextMesh apoapsisMarker;
    TextMesh plannedPeriapsisMarker;
    TextMesh plannedApoapsisMarker;
    TextMesh closestApproachMarker;
    TextMesh closestApproachDistanceMarker;
    TextMesh closestApproachTargetMarker;
    TextMesh sphereOfInfluenceEntryMarker;
    TextMesh maneuverNodeMarker;
    TextMesh burnStartMarker;
    TextMesh maneuverProgradeMarker;
    TextMesh maneuverRetrogradeMarker;
    TextMesh maneuverRadialOutMarker;
    TextMesh maneuverRadialInMarker;
    TextMesh descentImpactMarker;
    GameObject closestApproachPoint;
    GameObject closestApproachTargetPoint;
    GameObject sphereOfInfluenceEntryPoint;
    GameObject orbitHoverPoint;
    GameObject maneuverNodePoint;
    GameObject burnStartPoint;
    Renderer closestApproachPointRenderer;
    Renderer closestApproachTargetPointRenderer;
    Renderer sphereOfInfluenceEntryPointRenderer;
    Renderer orbitHoverPointRenderer;
    Renderer maneuverNodePointRenderer;
    Renderer burnStartPointRenderer;
    UnityEngine.Vector3 closestApproachPointPosition;
    UnityEngine.Vector3[] hoverPredictionWorldPoints = Array.Empty<UnityEngine.Vector3>();
    double[] hoverPredictionTimes = Array.Empty<double>();
    UnityEngine.Vector3 hoveredOrbitPointPosition;
    UnityEngine.Vector3 warpMenuWorldPosition;
    double hoveredOrbitPointTime;
    UnityEngine.Vector3 maneuverNodeWorldPosition;
    UnityEngine.Vector2 hoverGuiPosition;
    UnityEngine.Vector2 warpMenuGuiPosition;
    double warpMenuTime;
    int frameCounter;
    bool hasCachedPeriapsisMarker;
    bool hasCachedApoapsisMarker;
    bool hasCachedPlannedPeriapsisMarker;
    bool hasCachedPlannedApoapsisMarker;
    global::SimVector3 cachedPeriapsisSimPosition;
    global::SimVector3 cachedApoapsisSimPosition;
    global::SimVector3 cachedPlannedPeriapsisSimPosition;
    global::SimVector3 cachedPlannedApoapsisSimPosition;
    double cachedPeriapsisAltitude;
    double cachedApoapsisAltitude;
    double cachedPlannedPeriapsisAltitude;
    double cachedPlannedApoapsisAltitude;
    double cachedPeriapsisTime;
    double cachedApoapsisTime;
    bool hasHoveredOrbitPoint;
    bool showWarpContextMenu;
    bool isDraggingManeuverNode;
    bool orbitOverlayClearedForFlight;
    global::SimVector3[] cachedCurrentOrbitSimPath = Array.Empty<global::SimVector3>();
    string cachedCurrentOrbitLayoutBodyName = string.Empty;
    global::SimVector3[] cachedPlannedOrbitSimPath = Array.Empty<global::SimVector3>();
    UnityEngine.Vector3[] currentOrbitWorldBuffer = Array.Empty<UnityEngine.Vector3>();
    readonly System.Collections.Generic.Dictionary<TextMesh, float> markerBaseScreenHeightFractions =
        new System.Collections.Generic.Dictionary<TextMesh, float>();
    UnityEngine.Vector3[] plannedOrbitWorldBuffer = Array.Empty<UnityEngine.Vector3>();
    global::SimVector3 lastOrbitRenderOrigin;
    float lastOrbitWorldScale = float.NaN;
    bool hasLastOrbitRenderOrigin;

    bool ShouldShowOrbitOverlay()
    {
        return runner != null && runner.IsMapModeActive;
    }

    internal void SetMapModeActive(bool mapModeActive)
    {
        if (mapModeActive)
        {
            orbitOverlayClearedForFlight = false;
            frameCounter = updateEveryFrames;
            UpdatePreview();
            return;
        }

        ClearAllOrbitOverlay();
        cachedCurrentOrbitSimPath = Array.Empty<global::SimVector3>();
        cachedCurrentOrbitLayoutBodyName = string.Empty;
        cachedPlannedOrbitSimPath = Array.Empty<global::SimVector3>();
        orbitOverlayClearedForFlight = true;
    }

    internal void SetSimulation(Simulation newSimulation)
    {
        simulation = newSimulation;
    }

    internal void SetRunner(SimulationRunner newRunner)
    {
        runner = newRunner;
    }

    internal void SetWorldScale(float newWorldScale)
    {
        worldScale = newWorldScale;
    }

    internal void ForceRefresh()
    {
        frameCounter = updateEveryFrames;
        UpdatePreview();
    }

    void Awake()
    {
        lineRenderer = CreateOrbitLine(
            "Ship Orbit Preview",
            CurrentOrbitLineColor,
            OrbitLineVisualUtility.CurrentOrbitPixelWidth
        );
        plannedLineRenderer = CreateOrbitLine(
            "Planned Orbit Preview",
            PlannedOrbitLineColor,
            OrbitLineVisualUtility.PlannedOrbitPixelWidth
        );
        sphereOfInfluenceEntryLineRenderer = CreateOrbitLine(
            "Moon SoI Entry Preview",
            SoiEntryLineColor,
            OrbitLineVisualUtility.SoiEntryPixelWidth
        );
        moonSphereOfInfluenceOrbitLineRenderer = CreateOrbitLine(
            "Moon SoI Orbit Preview",
            MoonSoiOrbitLineColor,
            OrbitLineVisualUtility.MoonSoiOrbitPixelWidth
        );

        periapsisMarker = CreateOrbitMarker("Current Pe Marker", "Pe", EventColorPeAp);
        apoapsisMarker = CreateOrbitMarker("Current Ap Marker", "Ap", EventColorPeAp);
        plannedPeriapsisMarker = CreateOrbitMarker("Planned Pe Marker", "Pe", EventColorPlanned);
        plannedApoapsisMarker = CreateOrbitMarker("Planned Ap Marker", "Ap", EventColorPlanned);
        closestApproachMarker = CreateOrbitMarker("Closest Approach Marker", "CA", EventColorCA);
        closestApproachDistanceMarker = CreateOrbitMarker("Closest Approach Distance Marker", "CA", EventColorCA);
        closestApproachTargetMarker = CreateOrbitMarker("Closest Approach Target Marker", "Ghost", EventColorGhost);
        sphereOfInfluenceEntryMarker = CreateOrbitMarker("Moon SoI Entry Marker", "Moon SoI", EventColorSoI);
        maneuverNodeMarker = CreateOrbitMarker("Maneuver Node Marker", "Node", EventColorNode);
        burnStartMarker = CreateOrbitMarker("Burn Start Marker", "Burn start", EventColorBurn);
        maneuverProgradeMarker = CreateOrbitMarker("Maneuver Prograde Marker", "Pg", new Color(0.25f, 1.0f, 0.25f));
        maneuverRetrogradeMarker = CreateOrbitMarker("Maneuver Retrograde Marker", "Rg", new Color(0.08f, 0.55f, 0.08f));
        maneuverRadialOutMarker = CreateOrbitMarker("Maneuver Radial Out Marker", "R+", new Color(0.25f, 0.85f, 1.0f));
        maneuverRadialInMarker = CreateOrbitMarker("Maneuver Radial In Marker", "R-", new Color(0.08f, 0.35f, 0.75f));
        descentImpactMarker = CreateOrbitMarker(
            "Descent Impact Marker",
            "Impact",
            new Color(1.0f, 0.45f, 0.1f)
        );
        closestApproachLineRenderer = CreateClosestApproachLine();
        burnPreviewPathLineRenderer = CreateBurnPreviewPathLine();
        descentPreviewPathLineRenderer = CreateDescentPreviewPathLine();
        CreateClosestApproachPoint();
        CreateClosestApproachTargetPoint();
        CreateSphereOfInfluenceEntryPoint();
        CreateOrbitHoverPoint();
        CreateManeuverNodePoint();
        CreateBurnStartPoint();
        CreateManeuverHandleLines();
        OrbitLineVisualUtility.ApplyOverlayLayerRecursive(transform);
    }

    LineRenderer CreateOrbitLine(string lineName, Color color, float pixelWidth)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform);

        LineRenderer orbitLineRenderer = lineObject.AddComponent<LineRenderer>();
        orbitLineRenderer.positionCount = 0;
        OrbitLineVisualUtility.ConfigureOrbitLine(
            orbitLineRenderer,
            color,
            pixelWidth
        );
        return orbitLineRenderer;
    }

    void LateUpdate()
    {
        if (simulation == null || lineRenderer == null)
        {
            return;
        }

        if (!ShouldShowOrbitOverlay())
        {
            if (!orbitOverlayClearedForFlight)
            {
                ClearAllOrbitOverlay();
                orbitOverlayClearedForFlight = true;
            }

            return;
        }

        orbitOverlayClearedForFlight = false;
        frameCounter++;

        if (frameCounter >= updateEveryFrames)
        {
            frameCounter = 0;
            UpdatePreview();
        }

        bool renderStateChanged =
            runner != null &&
            (
                !hasLastOrbitRenderOrigin ||
                HasOrbitRenderStateChanged(
                    runner.GetRenderOrigin(),
                    runner.GetVisualPositionScale()
                )
            );

        if (renderStateChanged)
        {
            RepositionCachedOrbitMarkers();
            RepositionClosestApproachMarker();
        }

        RefreshOrbitLineWorldPositions();
    }

    void RefreshOrbitLineWorldPositions()
    {
        if (!ShouldShowOrbitOverlay() || runner == null)
        {
            return;
        }

        global::SimVector3 renderOrigin = runner.GetRenderOrigin();
        float positionScale = runner.GetVisualPositionScale();

        if (
            hasLastOrbitRenderOrigin &&
            !HasOrbitRenderStateChanged(renderOrigin, positionScale)
        )
        {
            return;
        }

        lastOrbitRenderOrigin = renderOrigin;
        lastOrbitWorldScale = positionScale;
        hasLastOrbitRenderOrigin = true;
        worldScale = positionScale;

        DrawOrbit(
            lineRenderer,
            cachedCurrentOrbitSimPath,
            ref currentOrbitWorldBuffer,
            cachedCurrentOrbitLayoutBodyName
        );

        if (cachedPlannedOrbitSimPath.Length >= 2)
        {
            DrawOrbit(plannedLineRenderer, cachedPlannedOrbitSimPath, ref plannedOrbitWorldBuffer);
        }
    }

    void MarkOrbitRenderStateCurrent()
    {
        if (runner == null)
        {
            return;
        }

        lastOrbitRenderOrigin = runner.GetRenderOrigin();
        lastOrbitWorldScale = runner.GetVisualPositionScale();
        hasLastOrbitRenderOrigin = true;
        worldScale = lastOrbitWorldScale;
    }

    bool HasOrbitRenderStateChanged(
        global::SimVector3 renderOrigin,
        float positionScale
    )
    {
        if (!Mathf.Approximately(positionScale, lastOrbitWorldScale))
        {
            return true;
        }

        const double epsilonMeters = 0.5;
        return
            Math.Abs(renderOrigin.x - lastOrbitRenderOrigin.x) > epsilonMeters ||
            Math.Abs(renderOrigin.y - lastOrbitRenderOrigin.y) > epsilonMeters ||
            Math.Abs(renderOrigin.z - lastOrbitRenderOrigin.z) > epsilonMeters;
    }

    void Update()
    {
        if (!ShouldShowOrbitOverlay())
        {
            return;
        }

        UpdateOrbitHover();
    }

    void SetOrbitLinePixelWidth(float pixelWidth)
    {
        SetLineRendererPixelWidth(lineRenderer, pixelWidth);
    }

    void SetLineRendererPixelWidth(LineRenderer targetLineRenderer, float pixelWidth)
    {
        if (targetLineRenderer == null)
        {
            return;
        }

        OrbitLineScreenSpaceWidth screenSpaceWidth =
            targetLineRenderer.GetComponent<OrbitLineScreenSpaceWidth>();

        if (screenSpaceWidth != null)
        {
            screenSpaceWidth.PixelWidth = pixelWidth;
        }
    }

    LineRenderer GetBackgroundOrbitLine(string bodyName)
    {
        if (!backgroundOrbitLineRenderers.TryGetValue(bodyName, out LineRenderer backgroundLine))
        {
            backgroundLine = CreateOrbitLine(
                "Background Orbit " + bodyName,
                BackgroundCelestialOrbitLineColor,
                OrbitLineVisualUtility.CelestialOrbitBackgroundPixelWidth
            );
            backgroundOrbitLineRenderers[bodyName] = backgroundLine;
        }

        return backgroundLine;
    }

    void UpdateBackgroundMapOrbits(string focusedBodyName)
    {
        if (simulation == null)
        {
            ClearBackgroundMapOrbits();
            return;
        }

        string[] bodyNames = simulation.GetMapDisplayOrbitBodyNames();
        System.Collections.Generic.HashSet<string> activeBodyNames =
            new System.Collections.Generic.HashSet<string>();

        for (int i = 0; i < bodyNames.Length; i++)
        {
            string bodyName = bodyNames[i];
            activeBodyNames.Add(bodyName);

            if (bodyName == focusedBodyName)
            {
                ClearLineRenderer(GetBackgroundOrbitLine(bodyName));
                continue;
            }

            LineRenderer backgroundLine = GetBackgroundOrbitLine(bodyName);

            if (
                simulation.IsVesselBodyName(bodyName) &&
                !simulation.IsVesselBodyName(focusedBodyName)
            )
            {
                ClearLineRenderer(backgroundLine);
                continue;
            }

            if (
                simulation.IsVesselBodyName(bodyName) &&
                simulation.GetFlightStatus() == FlightStatus.Landed
            )
            {
                ClearLineRenderer(backgroundLine);
                continue;
            }

            if (simulation.TryGetOrbitParentBody(bodyName) == null)
            {
                ClearLineRenderer(backgroundLine);
                continue;
            }

            SetLineRendererPixelWidth(
                backgroundLine,
                OrbitLineVisualUtility.CelestialOrbitBackgroundPixelWidth
            );

            global::SimVector3[] orbitPoints =
                simulation.GetBodyOrbitPathRelativeToParent(bodyName, previewPointCount);

            if (!backgroundOrbitWorldBuffers.TryGetValue(bodyName, out UnityEngine.Vector3[] worldBuffer))
            {
                worldBuffer = Array.Empty<UnityEngine.Vector3>();
            }

            DrawOrbit(backgroundLine, orbitPoints, ref worldBuffer, bodyName);
            backgroundOrbitWorldBuffers[bodyName] = worldBuffer;
        }

        foreach (System.Collections.Generic.KeyValuePair<string, LineRenderer> entry in
            backgroundOrbitLineRenderers)
        {
            if (!activeBodyNames.Contains(entry.Key))
            {
                ClearLineRenderer(entry.Value);
            }
        }
    }

    void ClearBackgroundMapOrbits()
    {
        foreach (System.Collections.Generic.KeyValuePair<string, LineRenderer> entry in
            backgroundOrbitLineRenderers)
        {
            ClearLineRenderer(entry.Value);
        }
    }

    void ClearFocusedOrbitLineAndMarkers()
    {
        cachedCurrentOrbitSimPath = Array.Empty<global::SimVector3>();
        cachedCurrentOrbitLayoutBodyName = string.Empty;
        ClearLineRenderer(lineRenderer);
        HideMarkers(periapsisMarker, apoapsisMarker);
        ClearOrbitMarkerCache(isPlannedMarker: false);
    }

    void UpdateCelestialOrbitFocusOverlay(string orbitFocusBodyName)
    {
        HideShipOrbitOverlayExtras();
        SetLineRendererPixelWidth(
            lineRenderer,
            OrbitLineVisualUtility.CelestialOrbitFocusedPixelWidth
        );

        Body parentBody = simulation.TryGetOrbitParentBody(orbitFocusBodyName);

        if (parentBody == null)
        {
            lineRenderer.positionCount = 0;
            cachedCurrentOrbitSimPath = Array.Empty<global::SimVector3>();
            cachedCurrentOrbitLayoutBodyName = string.Empty;
            HideMarkers(periapsisMarker, apoapsisMarker);
            ClearOrbitMarkerCache(isPlannedMarker: false);
            return;
        }

        global::SimVector3[] orbitPoints =
            simulation.GetBodyOrbitPathRelativeToParent(
                orbitFocusBodyName,
                previewPointCount
            );
        cachedCurrentOrbitSimPath = orbitPoints;
        cachedCurrentOrbitLayoutBodyName = orbitFocusBodyName;
        DrawOrbit(
            lineRenderer,
            orbitPoints,
            ref currentOrbitWorldBuffer,
            orbitFocusBodyName
        );
        MarkOrbitRenderStateCurrent();

        simulation.TryGetBodyPeriapsisAltitudeRelativeToParent(
            orbitFocusBodyName,
            out double periapsisAltitude
        );
        simulation.TryGetBodyApoapsisAltitudeRelativeToParent(
            orbitFocusBodyName,
            out double apoapsisAltitude
        );

        UpdateOrbitMarkers(
            periapsisMarker,
            apoapsisMarker,
            orbitPoints,
            parentBody.position,
            parentBody.radius,
            periapsisAltitude,
            apoapsisAltitude,
            double.NaN,
            double.NaN,
            MapOrbitIsHeliocentric(orbitFocusBodyName)
        );
    }

    void HideShipOrbitOverlayExtras()
    {
        plannedLineRenderer.positionCount = 0;
        cachedPlannedOrbitSimPath = Array.Empty<global::SimVector3>();
        HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);
        ClearOrbitMarkerCache(isPlannedMarker: true);
        closestApproachMarker.gameObject.SetActive(false);
        closestApproachPoint.SetActive(false);
        HideClosestApproachRendezvousView();
        maneuverNodePoint.SetActive(false);
        HideBurnStartMarker();
        HideManeuverNodeLabels();
        HideSphereOfInfluenceEntryPreview();
        ResetEventMarkerHighlights();
        ClearLineRenderer(burnPreviewPathLineRenderer);
        ClearLineRenderer(descentPreviewPathLineRenderer);
        HideMarker(descentImpactMarker);
    }

    void UpdatePreview()
    {
        if (simulation == null || lineRenderer == null)
        {
            return;
        }

        if (!ShouldShowOrbitOverlay())
        {
            ClearAllOrbitOverlay();
            return;
        }

        if (simulation.scenario == SimulationScenario.DockingApproach)
        {
            lineRenderer.positionCount = 0;
            plannedLineRenderer.positionCount = 0;
            HideMarkers(periapsisMarker, apoapsisMarker);
            HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);
            closestApproachMarker.gameObject.SetActive(false);
            closestApproachPoint.SetActive(false);
            HideClosestApproachRendezvousView();
            maneuverNodePoint.SetActive(false);
            HideBurnStartMarker();
            HideManeuverNodeLabels();
            HideSphereOfInfluenceEntryPreview();
            ResetEventMarkerHighlights();
            return;
        }

        if (runner == null)
        {
            ClearAllOrbitOverlay();
            return;
        }

        string orbitFocusBodyName = runner.GetOrbitFocusBodyName();
        bool focusIsShip = simulation.IsVesselBodyName(orbitFocusBodyName);

        UpdateBackgroundMapOrbits(orbitFocusBodyName);

        if (focusIsShip)
        {
            if (simulation.GetFlightStatus() == FlightStatus.Landed)
            {
                ClearFocusedOrbitLineAndMarkers();
                HideShipOrbitOverlayExtras();
                return;
            }
        }
        else if (
            simulation.sun != null &&
            orbitFocusBodyName == simulation.sun.name
        )
        {
            ClearFocusedOrbitLineAndMarkers();
            HideShipOrbitOverlayExtras();
            return;
        }
        else
        {
            UpdateCelestialOrbitFocusOverlay(orbitFocusBodyName);
            return;
        }

        global::SimVector3 plannedDeltaV = runner.GetPlannedDeltaV();
        bool hasManeuverNode = runner.HasManeuverNode();
        SetOrbitLinePixelWidth(OrbitLineVisualUtility.CurrentOrbitPixelWidth);
        global::SimVector3[] orbitPoints =
            simulation.GetShipOrbitPathRelativeToActivePrimary(previewPointCount);
        cachedCurrentOrbitSimPath = orbitPoints;
        cachedCurrentOrbitLayoutBodyName = orbitFocusBodyName;
        DrawOrbit(lineRenderer, orbitPoints, ref currentOrbitWorldBuffer, orbitFocusBodyName);
        MarkOrbitRenderStateCurrent();
        if (plannedDeltaV.LengthSquared() == 0.0)
        {
            UpdateOrbitMarkers(
                periapsisMarker,
                apoapsisMarker,
                orbitPoints,
                simulation.GetActivePrimaryPosition(),
                simulation.GetActivePrimaryRadius(),
                simulation.GetShipPeriapsisAltitudeRelativeToActivePrimary(),
                simulation.GetShipApoapsisAltitudeRelativeToActivePrimary(),
                runner.GetTimeToPeriapsis(),
                runner.GetTimeToApoapsis()
            );
        }
        else
        {
            HideMarkers(periapsisMarker, apoapsisMarker);
            ClearOrbitMarkerCache(isPlannedMarker: false);
        }

        UpdateManeuverNodePoint();
        UpdateBurnStartMarker();

        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview =
            UpdateSphereOfInfluenceEntryPreview();
        UpdateHoverPredictionForVisiblePath(
            hasManeuverNode,
            plannedDeltaV,
            sphereOfInfluenceEntryPreview
        );
        UpdateEventMarkerHighlights(
            runner.GetCachedClosestApproach(),
            sphereOfInfluenceEntryPreview
        );
        UpdateBurnPreviewPathOverlay();
        UpdateDescentPreviewPathOverlay();
        UpdateDescentImpactMarker();
        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreviewForCa =
            runner.GetCachedSphereOfInfluenceEntryPreview();
        UpdateClosestApproachMarker();
        UpdateClosestApproachPoint(
            sphereOfInfluenceEntryPreviewForCa.entersSphereOfInfluence
        );

        if (plannedDeltaV.LengthSquared() == 0)
        {
            plannedLineRenderer.positionCount = 0;
            cachedPlannedOrbitSimPath = Array.Empty<global::SimVector3>();
            HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);
            ClearOrbitMarkerCache(true);
            return;
        }

        if (sphereOfInfluenceEntryPreview.entersSphereOfInfluence)
        {
            plannedLineRenderer.positionCount = 0;
            cachedPlannedOrbitSimPath = Array.Empty<global::SimVector3>();
            HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);
            ClearOrbitMarkerCache(true);
            return;
        }

        global::SimVector3[] plannedOrbitPoints =
            runner.GetPlannedOrbitPath(previewPointCount);
        cachedPlannedOrbitSimPath = plannedOrbitPoints;
        DrawOrbit(plannedLineRenderer, plannedOrbitPoints, ref plannedOrbitWorldBuffer);
        MarkOrbitRenderStateCurrent();
        UpdateOrbitMarkers(
            plannedPeriapsisMarker,
            plannedApoapsisMarker,
            plannedOrbitPoints,
            simulation.GetActivePrimaryPosition(),
            simulation.GetActivePrimaryRadius(),
            simulation.GetPreviewShipPeriapsisAltitudeRelativeToActivePrimary(plannedDeltaV),
            simulation.GetPreviewShipApoapsisAltitudeRelativeToActivePrimary(plannedDeltaV)
        );
    }

    void RepositionClosestApproachMarker()
    {
        if (
            runner == null ||
            !showClosestApproachWorldMarker ||
            closestApproachMarker == null ||
            !closestApproachMarker.gameObject.activeSelf
        )
        {
            return;
        }

        ClosestApproachInfo closestApproach =
            runner.GetCachedClosestApproach();

        if (!closestApproach.hasApproach)
        {
            return;
        }

        closestApproachMarker.transform.position =
            GetSmoothedClosestApproachMarkerPosition(closestApproach.shipPosition);
    }

    void RepositionCachedOrbitMarkers()
    {
        if (simulation == null || runner == null)
        {
            return;
        }

        worldScale = runner.GetVisualPositionScale();
        bool preferPlannedMarkers = ShouldPreferPlannedOrbitMarkers();

        if (hasCachedPeriapsisMarker && !preferPlannedMarkers)
        {
            if (
                !runner.HasManeuverNode() ||
                !IsNearManeuverNode(cachedPeriapsisSimPosition)
            )
            {
                ApplyCachedMarkerWorldPosition(
                    periapsisMarker,
                    cachedPeriapsisSimPosition
                );
            }
        }

        if (hasCachedApoapsisMarker && !preferPlannedMarkers)
        {
            if (
                !runner.HasManeuverNode() ||
                !IsNearManeuverNode(cachedApoapsisSimPosition)
            )
            {
                ApplyCachedMarkerWorldPosition(
                    apoapsisMarker,
                    cachedApoapsisSimPosition
                );
            }
        }

        if (hasCachedPlannedPeriapsisMarker)
        {
            ApplyCachedMarkerWorldPosition(
                plannedPeriapsisMarker,
                cachedPlannedPeriapsisSimPosition
            );
        }

        if (hasCachedPlannedApoapsisMarker)
        {
            ApplyCachedMarkerWorldPosition(
                plannedApoapsisMarker,
                cachedPlannedApoapsisSimPosition
            );
        }
    }

    bool ShouldPreferPlannedOrbitMarkers()
    {
        return runner != null && runner.GetPlannedDeltaV().LengthSquared() > 0.0;
    }

    void ApplyCachedMarkerWorldPosition(
        TextMesh marker,
        global::SimVector3 simulationPosition
    )
    {
        if (marker == null || !marker.gameObject.activeSelf)
        {
            return;
        }

        marker.transform.position =
            ToLayoutWorldPosition(
                simulationPosition,
                MapOrbitIsHeliocentric(cachedCurrentOrbitLayoutBodyName)
            ) +
            new UnityEngine.Vector3(0.0f, eventLabelOffsetY, 0.0f);
    }

    void UpdateHoverPredictionForVisiblePath(
        bool hasManeuverNode,
        global::SimVector3 plannedDeltaV,
        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview
    )
    {
        if (
            plannedDeltaV.LengthSquared() > 0.0 &&
            sphereOfInfluenceEntryPreview.entersSphereOfInfluence
        )
        {
            hoverPredictionWorldPoints =
                ToWorldPositions(sphereOfInfluenceEntryPreview.pathPoints);
            hoverPredictionTimes = BuildLinearPredictionTimes(
                hoverPredictionWorldPoints.Length,
                sphereOfInfluenceEntryPreview.entryTime
            );
            return;
        }

        UpdateHoverPrediction(
            hasManeuverNode ? global::SimVector3.Zero() : plannedDeltaV
        );
    }

    void UpdateHoverPrediction(global::SimVector3 plannedDeltaV)
    {
        hoverPredictionWorldPoints = BuildHoverPrediction(
            plannedDeltaV,
            out hoverPredictionTimes
        );
    }

    UnityEngine.Vector3[] BuildHoverPrediction(
        global::SimVector3 plannedDeltaV,
        out double[] predictionTimes
    )
    {
        if (hoverPredictionStepCount <= 0 || hoverPredictionDt <= 0)
        {
            predictionTimes = Array.Empty<double>();
            return Array.Empty<UnityEngine.Vector3>();
        }

        double orbitPeriod =
            plannedDeltaV.LengthSquared() > 0 ?
            runner.GetPlannedOrbitPeriod() :
            simulation.GetShipOrbitalPeriodRelativeToActivePrimary();
        bool isClosedOrbit =
            !double.IsInfinity(orbitPeriod) &&
            orbitPeriod > hoverPredictionDt;

        if (isClosedOrbit)
        {
            global::SimVector3[] orbitPoints =
                plannedDeltaV.LengthSquared() > 0 ?
                runner.GetPlannedOrbitPath(previewPointCount) :
                simulation.GetShipOrbitPathRelativeToActivePrimary(
                    previewPointCount
                );
            return BuildHoverPredictionFromOrbitPoints(
                orbitPoints,
                orbitPeriod,
                simulation.ship.position,
                out predictionTimes
            );
        }

        Simulation preview = simulation.CreateCopy();
        preview.ship.thrust = global::SimVector3.Zero();
        int sampleCount = hoverPredictionStepCount;
        double sampleDt = hoverPredictionDt;
        UnityEngine.Vector3[] predictionWorldPoints = new UnityEngine.Vector3[sampleCount + 1];
        predictionTimes = new double[sampleCount + 1];

        for (int i = 0; i <= sampleCount; i++)
        {
            predictionWorldPoints[i] = ToWorldPosition(preview.ship.position);
            predictionTimes[i] = i * sampleDt;
            preview.Update(sampleDt);
        }

        return predictionWorldPoints;
    }

    UnityEngine.Vector3[] ToWorldPositions(global::SimVector3[] simulationPoints)
    {
        UnityEngine.Vector3[] worldPoints = new UnityEngine.Vector3[simulationPoints.Length];

        for (int i = 0; i < simulationPoints.Length; i++)
        {
            worldPoints[i] = ToWorldPosition(simulationPoints[i]);
        }

        return worldPoints;
    }

    double[] BuildLinearPredictionTimes(int pointCount, double endTime)
    {
        double[] predictionTimes = new double[pointCount];

        if (pointCount <= 1)
        {
            return predictionTimes;
        }

        for (int i = 0; i < pointCount; i++)
        {
            predictionTimes[i] = endTime * i / (pointCount - 1);
        }

        return predictionTimes;
    }

    UnityEngine.Vector3[] BuildHoverPredictionFromOrbitPoints(
        global::SimVector3[] orbitPoints,
        double orbitPeriod,
        global::SimVector3 currentShipPosition,
        out double[] predictionTimes
    )
    {
        int orbitPointCount = GetUniqueClosedOrbitPointCount(orbitPoints);

        if (orbitPointCount < 2)
        {
            predictionTimes = Array.Empty<double>();
            return Array.Empty<UnityEngine.Vector3>();
        }

        int startIndex = FindClosestOrbitPointIndex(
            orbitPoints,
            orbitPointCount,
            currentShipPosition
        );
        UnityEngine.Vector3[] predictionWorldPoints = new UnityEngine.Vector3[orbitPointCount + 1];
        predictionTimes = new double[orbitPointCount + 1];

        for (int i = 0; i <= orbitPointCount; i++)
        {
            int orbitPointIndex = (startIndex + i) % orbitPointCount;
            predictionWorldPoints[i] = ToWorldPosition(orbitPoints[orbitPointIndex]);
            predictionTimes[i] = orbitPeriod * i / orbitPointCount;
        }

        return predictionWorldPoints;
    }

    int GetUniqueClosedOrbitPointCount(global::SimVector3[] orbitPoints)
    {
        if (orbitPoints.Length < 2)
        {
            return orbitPoints.Length;
        }

        global::SimVector3 firstToLast =
            orbitPoints[0].Subtract(orbitPoints[orbitPoints.Length - 1]);

        if (firstToLast.LengthSquared() < 0.000001)
        {
            return orbitPoints.Length - 1;
        }

        return orbitPoints.Length;
    }

    int FindClosestOrbitPointIndex(
        global::SimVector3[] orbitPoints,
        int orbitPointCount,
        global::SimVector3 currentShipPosition
    )
    {
        int closestIndex = 0;
        double closestDistanceSquared = double.PositiveInfinity;

        for (int i = 0; i < orbitPointCount; i++)
        {
            double distanceSquared = orbitPoints[i]
                .Subtract(currentShipPosition)
                .LengthSquared();

            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    void UpdateOrbitHover()
    {
        if (
            Camera.main == null ||
            Mouse.current == null ||
            orbitHoverPoint == null ||
            hoverPredictionWorldPoints.Length == 0
        )
        {
            HideOrbitHoverPoint();
            return;
        }

        UnityEngine.Vector2 mousePosition = Mouse.current.position.ReadValue();
        bool rightMousePressed =
            Mouse.current.rightButton != null &&
            Mouse.current.rightButton.wasPressedThisFrame;
        bool leftMousePressed =
            Mouse.current.leftButton != null &&
            Mouse.current.leftButton.wasPressedThisFrame;
        bool leftMouseHeld =
            Mouse.current.leftButton != null &&
            Mouse.current.leftButton.isPressed;

        if (showWarpContextMenu && rightMousePressed)
        {
            HideOrbitHoverPoint();
            return;
        }

        if (
            runner != null &&
            runner.HasManeuverNode() &&
            leftMousePressed &&
            IsMouseNearWorldPoint(
                mousePosition,
                maneuverNodeWorldPosition,
                maneuverNodePickRadiusPixels
            )
        )
        {
            isDraggingManeuverNode = true;
            showWarpContextMenu = false;
        }

        if (isDraggingManeuverNode)
        {
            if (!leftMouseHeld)
            {
                isDraggingManeuverNode = false;
            }
            else if (
                TryFindClosestPredictionPoint(
                    mousePosition,
                    maneuverNodeDragRadiusPixels,
                    out UnityEngine.Vector3 dragWorldPosition,
                    out double dragTime
                )
            )
            {
                runner.MoveManeuverNode(
                    dragTime,
                    ToSimulationPosition(dragWorldPosition)
                );
                hoveredOrbitPointPosition = dragWorldPosition;
                hoveredOrbitPointTime = dragTime;
                hasHoveredOrbitPoint = true;
                orbitHoverPoint.SetActive(false);
                return;
            }
        }

        if (
            !TryFindClosestPredictionPoint(
                mousePosition,
                hoverPickRadiusPixels,
                out UnityEngine.Vector3 closestWorldPosition,
                out double closestTime
            )
        )
        {
            if (!showWarpContextMenu)
            {
                HideOrbitHoverPoint();
            }

            return;
        }

        hoveredOrbitPointPosition = closestWorldPosition;
        hoveredOrbitPointTime = closestTime;

        if (!showWarpContextMenu)
        {
            hoverGuiPosition = new UnityEngine.Vector2(
                mousePosition.x + 16.0f,
                Screen.height - mousePosition.y + 16.0f
            );
        }

        hasHoveredOrbitPoint = true;
        orbitHoverPoint.SetActive(true);
        orbitHoverPoint.transform.position = hoveredOrbitPointPosition;

        if (rightMousePressed)
        {
            showWarpContextMenu = true;
            warpMenuTime = hoveredOrbitPointTime;
            warpMenuWorldPosition = hoveredOrbitPointPosition;
            warpMenuGuiPosition = hoverGuiPosition;
        }

    }

    bool TryFindClosestPredictionPoint(
        UnityEngine.Vector2 mousePosition,
        float pickRadiusPixels,
        out UnityEngine.Vector3 closestWorldPosition,
        out double closestTime
    )
    {
        bool foundPoint = false;
        float closestDistance = pickRadiusPixels;
        closestWorldPosition = UnityEngine.Vector3.zero;
        closestTime = 0.0;

        for (int i = 1; i < hoverPredictionWorldPoints.Length; i++)
        {
            Vector3 screenA =
                Camera.main.WorldToScreenPoint(hoverPredictionWorldPoints[i - 1]);
            Vector3 screenB =
                Camera.main.WorldToScreenPoint(hoverPredictionWorldPoints[i]);

            if (screenA.z < 0 || screenB.z < 0)
            {
                continue;
            }

            UnityEngine.Vector2 pointA =
                new UnityEngine.Vector2(screenA.x, screenA.y);
            UnityEngine.Vector2 pointB =
                new UnityEngine.Vector2(screenB.x, screenB.y);
            UnityEngine.Vector2 segment = pointB - pointA;
            float segmentLengthSquared = segment.sqrMagnitude;

            if (segmentLengthSquared <= 0.000001f)
            {
                continue;
            }

            float segmentT = Mathf.Clamp01(
                UnityEngine.Vector2.Dot(mousePosition - pointA, segment) /
                segmentLengthSquared
            );
            UnityEngine.Vector2 closestScreenPoint =
                pointA + segment * segmentT;
            float distance = UnityEngine.Vector2.Distance(
                mousePosition,
                closestScreenPoint
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                foundPoint = true;
                closestWorldPosition = UnityEngine.Vector3.Lerp(
                    hoverPredictionWorldPoints[i - 1],
                    hoverPredictionWorldPoints[i],
                    segmentT
                );
                closestTime = Mathf.Lerp(
                    (float)hoverPredictionTimes[i - 1],
                    (float)hoverPredictionTimes[i],
                    segmentT
                );
            }
        }

        return foundPoint;
    }

    bool IsMouseNearWorldPoint(
        UnityEngine.Vector2 mousePosition,
        UnityEngine.Vector3 worldPosition,
        float pickRadiusPixels
    )
    {
        UnityEngine.Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

        if (screenPosition.z < 0)
        {
            return false;
        }

        UnityEngine.Vector2 screenPoint =
            new UnityEngine.Vector2(screenPosition.x, screenPosition.y);
        return UnityEngine.Vector2.Distance(mousePosition, screenPoint) <=
            pickRadiusPixels;
    }

    void UpdateManeuverNodePoint()
    {
        if (
            runner == null ||
            !runner.HasManeuverNode() ||
            maneuverNodePoint == null ||
            hoverPredictionWorldPoints.Length == 0
        )
        {
            if (maneuverNodePoint != null)
            {
                maneuverNodePoint.SetActive(false);
            }

            HideBurnStartMarker();
            HideManeuverHandleLines();
            HideManeuverNodeLabels();
            return;
        }

        maneuverNodeWorldPosition = ToWorldPosition(
            runner.GetManeuverNodePosition()
        );
        maneuverNodePoint.SetActive(true);
        maneuverNodePoint.transform.position = maneuverNodeWorldPosition;
        UpdateManeuverHandleLines();
        UpdateManeuverNodeLabels();
    }

    void UpdateBurnStartMarker()
    {
        if (
            runner == null ||
            !runner.HasManeuverNode() ||
            runner.GetManeuverNodeDeltaV().LengthSquared() <= 0.0 ||
            burnStartPoint == null ||
            hoverPredictionWorldPoints.Length == 0
        )
        {
            HideBurnStartMarker();
            return;
        }

        double burnStartTime = runner.GetPlannedBurnStartTime();
        if (
            double.IsNaN(burnStartTime) ||
            double.IsInfinity(burnStartTime) ||
            burnStartTime < 0.0
        )
        {
            HideBurnStartMarker();
            return;
        }

        UnityEngine.Vector3 burnStartWorldPosition =
            GetWorldPositionAtPredictionTime(burnStartTime);
        burnStartPoint.SetActive(true);
        burnStartPoint.transform.position = burnStartWorldPosition;

        if (
            UnityEngine.Vector3.Distance(
                burnStartWorldPosition,
                maneuverNodeWorldPosition
            ) < 0.5f
        )
        {
            HideMarker(burnStartMarker);
            return;
        }

        ShowEventMarker(
            burnStartMarker,
            burnStartWorldPosition,
            "Burn start",
            GetAltitudeAboveActivePrimary(
                ToSimulationPosition(burnStartWorldPosition)
            ),
            burnStartTime
        );
    }

    void HideBurnStartMarker()
    {
        if (burnStartPoint != null)
        {
            burnStartPoint.SetActive(false);
        }

        HideMarker(burnStartMarker);
    }

    void UpdateManeuverHandleLines()
    {
        DrawManeuverHandleLine(
            maneuverProgradeLineRenderer,
            runner.GetManeuverNodeProgradeDirection(),
            new Color(0.25f, 1.0f, 0.25f, 0.95f)
        );
        DrawManeuverHandleLine(
            maneuverRetrogradeLineRenderer,
            runner.GetManeuverNodeRetrogradeDirection(),
            new Color(0.08f, 0.55f, 0.08f, 0.95f)
        );
        DrawManeuverHandleLine(
            maneuverRadialOutLineRenderer,
            runner.GetManeuverNodeRadialOutDirection(),
            new Color(0.25f, 0.85f, 1.0f, 0.95f)
        );
        DrawManeuverHandleLine(
            maneuverRadialInLineRenderer,
            runner.GetManeuverNodeRadialInDirection(),
            new Color(0.08f, 0.35f, 0.75f, 0.95f)
        );
    }

    void DrawManeuverHandleLine(
        LineRenderer targetLineRenderer,
        global::SimVector3 direction,
        Color color
    )
    {
        UnityEngine.Vector3 endPosition = maneuverNodeWorldPosition + new UnityEngine.Vector3(
            (float)direction.x,
            (float)direction.y,
            0.0f
        ) * maneuverHandleLength;

        targetLineRenderer.startColor = color;
        targetLineRenderer.endColor = color;
        targetLineRenderer.positionCount = 2;
        targetLineRenderer.SetPosition(0, maneuverNodeWorldPosition);
        targetLineRenderer.SetPosition(1, endPosition);
    }

    void HideManeuverHandleLines()
    {
        maneuverProgradeLineRenderer.positionCount = 0;
        maneuverRetrogradeLineRenderer.positionCount = 0;
        maneuverRadialOutLineRenderer.positionCount = 0;
        maneuverRadialInLineRenderer.positionCount = 0;
    }

    void UpdateManeuverNodeLabels()
    {
        global::SimVector3 nodeDeltaV = runner.GetManeuverNodeDeltaV();
        global::SimVector3 progradeDirection = runner.GetManeuverNodeProgradeDirection();
        global::SimVector3 radialOutDirection = runner.GetManeuverNodeRadialOutDirection();
        double progradeComponent = Dot(nodeDeltaV, progradeDirection);
        double radialComponent = Dot(nodeDeltaV, radialOutDirection);
        double nodeDeltaVMagnitude = nodeDeltaV.Length();

        ShowEventMarker(
            maneuverNodeMarker,
            maneuverNodeWorldPosition,
            "Node",
            GetAltitudeAboveActivePrimary(runner.GetManeuverNodePosition()),
            runner.GetManeuverNodeTime(),
            BuildManeuverNodeExtraLine(nodeDeltaVMagnitude)
        );
        UpdateAxisMarker(
            maneuverProgradeMarker,
            runner.GetManeuverNodeProgradeDirection(),
            "Pg",
            progradeComponent
        );
        UpdateAxisMarker(
            maneuverRetrogradeMarker,
            runner.GetManeuverNodeRetrogradeDirection(),
            "Rg",
            -progradeComponent
        );
        UpdateAxisMarker(
            maneuverRadialOutMarker,
            runner.GetManeuverNodeRadialOutDirection(),
            "R+",
            radialComponent
        );
        UpdateAxisMarker(
            maneuverRadialInMarker,
            runner.GetManeuverNodeRadialInDirection(),
            "R-",
            -radialComponent
        );
    }

    void UpdateAxisMarker(
        TextMesh marker,
        global::SimVector3 direction,
        string axisName,
        double deltaVComponent
    )
    {
        if (deltaVComponent <= 0.01)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        UnityEngine.Vector3 worldDirection = new UnityEngine.Vector3(
            (float)direction.x,
            (float)direction.y,
            0.0f
        );
        UnityEngine.Vector3 markerPosition =
            maneuverNodeWorldPosition + worldDirection * (maneuverHandleLength + 0.28f);
        ShowTextMarker(
            marker,
            markerPosition,
            axisName + "\nΔv " + FormatDeltaV(deltaVComponent) +
            "\nT+" + FormatTime(runner.GetPlannedBurnStartTime())
        );
    }

    void HideManeuverNodeLabels()
    {
        HideMarker(maneuverNodeMarker);
        HideMarker(maneuverProgradeMarker);
        HideMarker(maneuverRetrogradeMarker);
        HideMarker(maneuverRadialOutMarker);
        HideMarker(maneuverRadialInMarker);
    }

    UnityEngine.Vector3 GetWorldPositionAtPredictionTime(double predictionTime)
    {
        if (hoverPredictionWorldPoints.Length == 0)
        {
            return UnityEngine.Vector3.zero;
        }

        for (int i = 1; i < hoverPredictionWorldPoints.Length; i++)
        {
            if (hoverPredictionTimes[i] >= predictionTime)
            {
                double timeA = hoverPredictionTimes[i - 1];
                double timeB = hoverPredictionTimes[i];
                float t = 0.0f;

                if (timeB > timeA)
                {
                    t = (float)((predictionTime - timeA) / (timeB - timeA));
                }

                return UnityEngine.Vector3.Lerp(
                    hoverPredictionWorldPoints[i - 1],
                    hoverPredictionWorldPoints[i],
                    Mathf.Clamp01(t)
                );
            }
        }

        return hoverPredictionWorldPoints[hoverPredictionWorldPoints.Length - 1];
    }

    void HideOrbitHoverPoint()
    {
        hasHoveredOrbitPoint = false;
        showWarpContextMenu = false;
        isDraggingManeuverNode = false;

        if (orbitHoverPoint != null)
        {
            orbitHoverPoint.SetActive(false);
        }
    }

    void OnGUI()
    {
        if (!ShouldShowOrbitOverlay())
        {
            return;
        }

        DrawOrbitLegend();

        if (!hasHoveredOrbitPoint || runner == null)
        {
            return;
        }

        UnityEngine.Vector2 guiPosition =
            showWarpContextMenu ? warpMenuGuiPosition : hoverGuiPosition;
        double displayTime =
            showWarpContextMenu ? warpMenuTime : hoveredOrbitPointTime;
        GUILayout.BeginArea(
            new Rect(guiPosition.x, guiPosition.y, 280, showWarpContextMenu ? 280 : 72)
        );
        GUILayout.Label(BuildHoverTooltip(displayTime));

        if (!showWarpContextMenu)
        {
            GUILayout.EndArea();
            return;
        }

        if (GUILayout.Button(T("Warp here")))
        {
            runner.WarpBy(warpMenuTime);
            showWarpContextMenu = false;
            HideOrbitHoverPoint();
        }

        if (runner.HasManeuverNode())
        {
            if (GUILayout.Button(T("Warp to maneuver start")))
            {
                runner.WarpToManeuverStart();
                showWarpContextMenu = false;
                HideOrbitHoverPoint();
            }

            if (GUILayout.Button(T("Auto execute maneuver")))
            {
                runner.StartAutoBurn();
                showWarpContextMenu = false;
                HideOrbitHoverPoint();
            }

            if (GUILayout.Button(T("Remove maneuver")))
            {
                runner.ClearManeuverNode();
                showWarpContextMenu = false;
                HideOrbitHoverPoint();
            }
        }
        else
        {
            if (GUILayout.Button(T("Add maneuver")))
            {
                runner.SetManeuverNode(
                    warpMenuTime,
                    ToSimulationPosition(warpMenuWorldPosition)
                );
                showWarpContextMenu = false;
                HideOrbitHoverPoint();
            }

            if (GUILayout.Button(T("Add maneuver at Pe")))
            {
                runner.SetManeuverNodeAtPeriapsis();
                showWarpContextMenu = false;
                HideOrbitHoverPoint();
            }

            if (GUILayout.Button(T("Add maneuver at Ap")))
            {
                runner.SetManeuverNodeAtApoapsis();
                showWarpContextMenu = false;
                HideOrbitHoverPoint();
            }
        }

        GUILayout.EndArea();
    }

    string T(string key)
    {
        return Localization.Translate(key);
    }

    void UpdateClosestApproachMarker()
    {
        if (!showClosestApproachWorldMarker)
        {
            closestApproachMarker.gameObject.SetActive(false);
            return;
        }

        ClosestApproachInfo closestApproach =
            runner.GetCachedClosestApproach();

        if (!closestApproach.hasApproach)
        {
            closestApproachMarker.gameObject.SetActive(false);
            return;
        }

        string markerText = closestApproach.entersMoonSphereOfInfluence ?
            "SoI" :
            "CA";

        closestApproachMarker.gameObject.SetActive(true);
        closestApproachMarker.text = markerText;
        closestApproachMarker.transform.position =
            GetSmoothedClosestApproachMarkerPosition(closestApproach.shipPosition);
    }

    SphereOfInfluenceEntryPreview UpdateSphereOfInfluenceEntryPreview()
    {
        SphereOfInfluenceEntryPreview preview =
            runner.GetCachedSphereOfInfluenceEntryPreview();

        if (
            !showSphereOfInfluenceEntryPreview ||
            !preview.entersSphereOfInfluence ||
            simulation.activePrimaryBodyName == preview.targetBodyName
        )
        {
            HideSphereOfInfluenceEntryPreview();
            return SphereOfInfluenceEntryPreview.None();
        }

        DrawOrbit(sphereOfInfluenceEntryLineRenderer, preview.pathPoints);
        DrawOrbit(
            moonSphereOfInfluenceOrbitLineRenderer,
            preview.moonOrbitPathPoints
        );
        sphereOfInfluenceEntryPoint.SetActive(true);
        UnityEngine.Vector3 entryWorldPosition = ToWorldPosition(preview.entryPosition);
        sphereOfInfluenceEntryPoint.transform.position = entryWorldPosition;
        ShowEventMarker(
            sphereOfInfluenceEntryMarker,
            entryWorldPosition,
            GetSphereOfInfluenceEventLabel(preview.targetBodyName),
            GetAltitudeAboveActivePrimary(preview.entryPosition),
            preview.entryTime
        );

        return preview;
    }

    static string GetSphereOfInfluenceEventLabel(string targetBodyName)
    {
        if (targetBodyName == "Sun")
        {
            return "Sun SoI";
        }

        return "Moon SoI";
    }

    void HideSphereOfInfluenceEntryPreview()
    {
        sphereOfInfluenceEntryLineRenderer.positionCount = 0;
        moonSphereOfInfluenceOrbitLineRenderer.positionCount = 0;
        sphereOfInfluenceEntryPoint.SetActive(false);
        HideMarker(sphereOfInfluenceEntryMarker);
    }

    void UpdateClosestApproachPoint(
        bool hideWhenSphereOfInfluenceEntryExists
    )
    {
        if (
            runner == null ||
            simulation == null ||
            runner.Simulation == null ||
            closestApproachPoint == null
        )
        {
            return;
        }

        if (
            !showClosestApproachPoint ||
            hideWhenSphereOfInfluenceEntryExists
        )
        {
            closestApproachPoint.SetActive(false);
            HideClosestApproachRendezvousView();
            return;
        }

        ClosestApproachInfo closestApproach =
            runner.GetCachedClosestApproach();

        if (!closestApproach.hasApproach)
        {
            closestApproachPoint.SetActive(false);
            HideClosestApproachRendezvousView();
            return;
        }

        UnityEngine.Vector3 targetPosition = ToWorldPosition(closestApproach.shipPosition);
        UnityEngine.Vector3 targetBodyPosition = ToWorldPosition(closestApproach.targetPosition);
        closestApproachPointPosition = targetPosition;

        closestApproachPoint.SetActive(true);
        closestApproachPoint.transform.position = closestApproachPointPosition;
        closestApproachPoint.transform.localScale =
            UnityEngine.Vector3.one * closestApproachPointSize;
        closestApproachPointRenderer.material.color =
            closestApproach.entersMoonSphereOfInfluence ?
            new Color(1.0f, 0.85f, 0.0f) :
            Color.white;

        closestApproachTargetPoint.SetActive(false);
        bool showTargetBodyMarker = ShouldShowTargetBodyMarker(
            closestApproach.targetBodyName
        );

        if (showTargetBodyMarker)
        {
            closestApproachTargetPoint.SetActive(true);
            const double closestApproachPreviewRadiusSim = 1500.0;
            BodyView.UpdateGhostTarget(
                closestApproachTargetPoint,
                closestApproach.targetBodyName,
                targetBodyPosition,
                runner.GetTargetBodyVelocity(),
                closestApproachPreviewRadiusSim,
                worldScale
            );
        }

        closestApproachLineRenderer.positionCount = 2;
        closestApproachLineRenderer.SetPosition(0, closestApproachPointPosition);
        closestApproachLineRenderer.SetPosition(1, targetBodyPosition);
        if (showGhostTarget && showTargetBodyMarker)
        {
            ShowEventMarker(
                closestApproachTargetMarker,
                targetBodyPosition,
                T("Ghost") + " " + closestApproach.targetBodyName,
                GetAltitudeAboveActivePrimary(closestApproach.targetPosition),
                closestApproach.time,
                T("Dist") + " " + FormatNumber(closestApproach.distance) +
                "  v " + FormatNumber(closestApproach.relativeSpeed),
                false
            );
        }
        else
        {
            HideMarker(closestApproachTargetMarker);
        }

        ShowEventMarker(
            closestApproachDistanceMarker,
            (closestApproachPointPosition + targetBodyPosition) * 0.5f,
            "CA",
            closestApproach.distance,
            closestApproach.time
        );
    }

    bool ShouldShowTargetBodyMarker(string bodyName)
    {
        if (simulation == null || string.IsNullOrEmpty(bodyName))
        {
            return true;
        }

        if (!simulation.IsVesselBodyName(bodyName))
        {
            return true;
        }

        return showVesselTargetMarkers;
    }

    void HideClosestApproachRendezvousView()
    {
        if (closestApproachTargetPoint != null)
        {
            closestApproachTargetPoint.SetActive(false);
        }

        if (closestApproachLineRenderer != null)
        {
            closestApproachLineRenderer.positionCount = 0;
        }

        if (closestApproachDistanceMarker != null)
        {
            closestApproachDistanceMarker.gameObject.SetActive(false);
        }

        HideMarker(closestApproachTargetMarker);
    }

    UnityEngine.Vector3 GetSmoothedClosestApproachMarkerPosition(global::SimVector3 simulationPosition)
    {
        return ToWorldPosition(simulationPosition);
    }

    void DrawOrbit(
        LineRenderer targetLineRenderer,
        global::SimVector3[] orbitPoints,
        ref UnityEngine.Vector3[] worldBuffer,
        string layoutBodyName = null
    )
    {
        if (targetLineRenderer == null)
        {
            return;
        }

        if (orbitPoints == null || orbitPoints.Length < 2)
        {
            ClearLineRenderer(targetLineRenderer);
            return;
        }

        if (worldBuffer == null || worldBuffer.Length != orbitPoints.Length)
        {
            worldBuffer = new UnityEngine.Vector3[orbitPoints.Length];
        }

        bool stretchHeliocentric = MapOrbitIsHeliocentric(layoutBodyName);

        for (int i = 0; i < orbitPoints.Length; i++)
        {
            worldBuffer[i] = ToLayoutWorldPosition(orbitPoints[i], stretchHeliocentric);
        }

        targetLineRenderer.enabled = true;
        targetLineRenderer.positionCount = worldBuffer.Length;
        targetLineRenderer.SetPositions(worldBuffer);
    }

    void DrawOrbit(LineRenderer targetLineRenderer, global::SimVector3[] orbitPoints)
    {
        UnityEngine.Vector3[] unusedBuffer = null;
        DrawOrbit(targetLineRenderer, orbitPoints, ref unusedBuffer);
    }

    void DrawWorldOrbit(LineRenderer targetLineRenderer, UnityEngine.Vector3[] orbitPoints)
    {
        if (targetLineRenderer == null)
        {
            return;
        }

        if (orbitPoints == null || orbitPoints.Length < 2)
        {
            ClearLineRenderer(targetLineRenderer);
            return;
        }

        targetLineRenderer.enabled = true;
        targetLineRenderer.positionCount = orbitPoints.Length;
        targetLineRenderer.SetPositions(orbitPoints);
    }

    TextMesh CreateOrbitMarker(
        string markerName,
        string markerText,
        Color color,
        float screenHeightFraction = OrbitMarkerScreenHeightFraction
    )
    {
        GameObject markerObject = new GameObject(markerName);
        markerObject.transform.SetParent(transform);

        TextMesh textMesh = markerObject.AddComponent<TextMesh>();
        textMesh.text = markerText;
        textMesh.fontSize = 32;
        textMesh.characterSize = 0.055f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;

        ScreenSpaceBillboardScaler billboardScaler =
            markerObject.AddComponent<ScreenSpaceBillboardScaler>();
        billboardScaler.ScreenHeightFraction = screenHeightFraction;
        markerBaseScreenHeightFractions[textMesh] = screenHeightFraction;
        billboardScaler.TextMeshSizeMultiplier = OrbitMarkerTextMeshSizeMultiplier;
        billboardScaler.DriveTextMeshCharacterSize = true;

        MeshRenderer markerRenderer = markerObject.GetComponent<MeshRenderer>();

        if (markerRenderer != null)
        {
            RuntimeMaterialFactory.ApplyOrbitOverlayRenderer(markerRenderer);
        }

        markerObject.SetActive(false);

        return textMesh;
    }

    void UpdateEventMarkerHighlights(
        ClosestApproachInfo closestApproach,
        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview
    )
    {
        NextOrbitalEventInfo nextEvent = runner.GetNextOrbitalEvent();
        string nextEventName = nextEvent.hasEvent ? nextEvent.name : string.Empty;

        SetMarkerHighlight(
            periapsisMarker,
            nextEventName == "Pe",
            EventColorPeAp
        );
        SetMarkerHighlight(
            apoapsisMarker,
            nextEventName == "Ap",
            EventColorPeAp
        );
        SetMarkerHighlight(
            plannedPeriapsisMarker,
            false,
            EventColorPlanned
        );
        SetMarkerHighlight(
            plannedApoapsisMarker,
            false,
            EventColorPlanned
        );
        SetMarkerHighlight(
            closestApproachDistanceMarker,
            nextEventName == "CA",
            EventColorCA
        );
        SetMarkerHighlight(
            closestApproachTargetMarker,
            nextEventName == "CA",
            EventColorGhost
        );
        SetMarkerHighlight(
            sphereOfInfluenceEntryMarker,
            nextEventName == "Moon SoI" || nextEventName == "Sun SoI",
            EventColorSoI
        );
        SetMarkerHighlight(
            maneuverNodeMarker,
            nextEventName == "Node",
            EventColorNode
        );
        SetMarkerHighlight(
            burnStartMarker,
            nextEventName == "Burn start",
            EventColorBurn
        );

        bool highlightManeuverAxes =
            nextEventName == "Node" || nextEventName == "Burn start";
        SetMarkerHighlight(
            maneuverProgradeMarker,
            highlightManeuverAxes,
            new Color(0.25f, 1.0f, 0.25f)
        );
        SetMarkerHighlight(
            maneuverRetrogradeMarker,
            highlightManeuverAxes,
            new Color(0.08f, 0.55f, 0.08f)
        );
        SetMarkerHighlight(
            maneuverRadialOutMarker,
            highlightManeuverAxes,
            new Color(0.25f, 0.85f, 1.0f)
        );
        SetMarkerHighlight(
            maneuverRadialInMarker,
            highlightManeuverAxes,
            new Color(0.08f, 0.35f, 0.75f)
        );

        SetPointHighlight(
            closestApproachPointRenderer,
            closestApproachPoint != null && closestApproachPoint.activeSelf && nextEventName == "CA",
            closestApproach.entersMoonSphereOfInfluence ?
                new Color(1.0f, 0.85f, 0.0f) :
                Color.white,
            closestApproachPointSize
        );
        SetPointHighlight(
            closestApproachTargetPointRenderer,
            closestApproachTargetPoint != null && closestApproachTargetPoint.activeSelf && nextEventName == "CA",
            new Color(1.0f, 0.45f, 0.9f),
            closestApproachPointSize * 0.85f
        );
        SetPointHighlight(
            sphereOfInfluenceEntryPointRenderer,
            sphereOfInfluenceEntryPoint != null && sphereOfInfluenceEntryPoint.activeSelf &&
                (nextEventName == "Moon SoI" || nextEventName == "Sun SoI"),
            new Color(1.0f, 0.85f, 0.0f),
            sphereOfInfluenceEntryPointSize
        );
        SetPointHighlight(
            maneuverNodePointRenderer,
            maneuverNodePoint != null && maneuverNodePoint.activeSelf && nextEventName == "Node",
            new Color(1.0f, 0.85f, 0.0f),
            hoverPointSize * 1.35f
        );
        SetPointHighlight(
            burnStartPointRenderer,
            burnStartPoint != null && burnStartPoint.activeSelf && nextEventName == "Burn start",
            new Color(1.0f, 0.55f, 0.15f),
            hoverPointSize * 1.15f
        );
    }

    void ResetEventMarkerHighlights()
    {
        SetMarkerHighlight(periapsisMarker, false, EventColorPeAp);
        SetMarkerHighlight(apoapsisMarker, false, EventColorPeAp);
        SetMarkerHighlight(plannedPeriapsisMarker, false, EventColorPlanned);
        SetMarkerHighlight(plannedApoapsisMarker, false, EventColorPlanned);
        SetMarkerHighlight(closestApproachDistanceMarker, false, EventColorCA);
        SetMarkerHighlight(closestApproachTargetMarker, false, EventColorGhost);
        SetMarkerHighlight(sphereOfInfluenceEntryMarker, false, EventColorSoI);
        SetMarkerHighlight(maneuverNodeMarker, false, EventColorNode);
        SetMarkerHighlight(burnStartMarker, false, EventColorBurn);
        SetMarkerHighlight(maneuverProgradeMarker, false, new Color(0.25f, 1.0f, 0.25f));
        SetMarkerHighlight(maneuverRetrogradeMarker, false, new Color(0.08f, 0.55f, 0.08f));
        SetMarkerHighlight(maneuverRadialOutMarker, false, new Color(0.25f, 0.85f, 1.0f));
        SetMarkerHighlight(maneuverRadialInMarker, false, new Color(0.08f, 0.35f, 0.75f));
    }

    void SetMarkerHighlight(TextMesh marker, bool highlighted, Color baseColor)
    {
        if (marker == null)
        {
            return;
        }

        marker.color = highlighted ? Color.white : baseColor;
        marker.fontSize = highlighted ? 38 : 32;

        ScreenSpaceBillboardScaler billboardScaler =
            marker.GetComponent<ScreenSpaceBillboardScaler>();

        if (billboardScaler != null)
        {
            float baseScreenHeightFraction = markerBaseScreenHeightFractions.TryGetValue(
                marker,
                out float storedFraction
            ) ?
                storedFraction :
                OrbitMarkerScreenHeightFraction;
            billboardScaler.ScreenHeightFraction =
                baseScreenHeightFraction *
                (highlighted ? highlightedMarkerScale : 1.0f);
        }
    }

    void SetPointHighlight(
        Renderer pointRenderer,
        bool highlighted,
        Color baseColor,
        float baseSize
    )
    {
        if (pointRenderer == null)
        {
            return;
        }

        pointRenderer.material.color = highlighted ? Color.white : baseColor;
        pointRenderer.transform.localScale =
            UnityEngine.Vector3.one * baseSize * (highlighted ? highlightedMarkerScale : 1.0f);
    }

    void CreateClosestApproachPoint()
    {
        closestApproachPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        closestApproachPoint.name = "Closest Approach Point";
        closestApproachPoint.transform.SetParent(transform);
        closestApproachPoint.transform.localScale =
            UnityEngine.Vector3.one * closestApproachPointSize;

        Collider pointCollider = closestApproachPoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        closestApproachPointRenderer =
            closestApproachPoint.GetComponent<Renderer>();
        closestApproachPointRenderer.material.color = Color.white;
        RuntimeMaterialFactory.ApplyOrbitOverlayRenderer(closestApproachPointRenderer);
        closestApproachPoint.SetActive(false);
    }

    void CreateClosestApproachTargetPoint()
    {
        closestApproachTargetPoint = BodyView.CreateGhostTarget(
            "Moon",
            simulation == null ? 0.0 : simulation.GetBodyRadius("Moon"),
            worldScale
        );
        closestApproachTargetPoint.transform.SetParent(transform);
        closestApproachTargetPointRenderer =
            closestApproachTargetPoint.GetComponent<Renderer>();
        closestApproachTargetPoint.SetActive(false);
    }

    void UpdateBurnPreviewPathOverlay()
    {
        if (
            runner == null ||
            burnPreviewPathLineRenderer == null ||
            !runner.HasBurnDiagnosticOverlay()
        )
        {
            if (burnPreviewPathLineRenderer != null)
            {
                burnPreviewPathLineRenderer.positionCount = 0;
            }

            return;
        }

        global::SimVector3[] previewPath = runner.GetCachedPreviewBurnWorldPath();
        UnityEngine.Vector3[] worldPoints = new UnityEngine.Vector3[previewPath.Length];

        for (int i = 0; i < previewPath.Length; i++)
        {
            worldPoints[i] = ToWorldPosition(previewPath[i]);
        }

        burnPreviewPathLineRenderer.enabled = true;
        burnPreviewPathLineRenderer.positionCount = worldPoints.Length;
        burnPreviewPathLineRenderer.SetPositions(worldPoints);
    }

    void UpdateDescentPreviewPathOverlay()
    {
        if (simulation == null || descentPreviewPathLineRenderer == null)
        {
            return;
        }

        if (!simulation.ShouldShowDescentPreview())
        {
            descentPreviewPathLineRenderer.positionCount = 0;
            return;
        }

        bool useAtmosphericSas =
            runner != null &&
            runner.ShipOrientationMode != ShipOrientationMode.Manual;
        global::SimVector3[] previewPath = simulation.SampleDescentPreviewWorldPath(
            useAtmosphericSas,
            3600.0,
            hoverPredictionDt,
            hoverPredictionStepCount
        );

        if (previewPath.Length < 2)
        {
            descentPreviewPathLineRenderer.positionCount = 0;
            return;
        }

        UnityEngine.Vector3[] worldPoints = new UnityEngine.Vector3[previewPath.Length];

        for (int i = 0; i < previewPath.Length; i++)
        {
            worldPoints[i] = ToWorldPosition(previewPath[i]);
        }

        descentPreviewPathLineRenderer.enabled = true;
        descentPreviewPathLineRenderer.positionCount = worldPoints.Length;
        descentPreviewPathLineRenderer.SetPositions(worldPoints);
    }

    void UpdateDescentImpactMarker()
    {
        if (simulation == null || descentImpactMarker == null)
        {
            return;
        }

        if (
            !simulation.ShouldShowDescentPreview() ||
            !simulation.TryGetDescentImpactPoint(out global::SimVector3 impactPoint)
        )
        {
            HideMarker(descentImpactMarker);
            return;
        }

        Body activePrimary = simulation.GetActivePrimaryBody();
        double impactAltitude = activePrimary == null ?
            double.NaN :
            activePrimary.position
                .Subtract(impactPoint)
                .Length() -
            activePrimary.radius;

        ShowEventMarker(
            descentImpactMarker,
            ToWorldPosition(impactPoint),
            "Impact",
            impactAltitude,
            double.NaN
        );
    }

    LineRenderer CreateBurnPreviewPathLine()
    {
        return CreateOrbitLine(
            "Burn Preview Path",
            new Color(1.0f, 0.85f, 0.15f, 0.9f),
            OrbitLineVisualUtility.BurnPreviewPixelWidth
        );
    }

    LineRenderer CreateDescentPreviewPathLine()
    {
        return CreateOrbitLine(
            "Descent Preview Path",
            new Color(1.0f, 0.45f, 0.1f, 0.85f),
            OrbitLineVisualUtility.DescentPreviewPixelWidth
        );
    }

    LineRenderer CreateClosestApproachLine()
    {
        return CreateOrbitLine(
            "Closest Approach Distance Line",
            new Color(1.0f, 0.75f, 0.95f, 0.9f),
            OrbitLineVisualUtility.ClosestApproachPixelWidth
        );
    }

    void CreateSphereOfInfluenceEntryPoint()
    {
        sphereOfInfluenceEntryPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereOfInfluenceEntryPoint.name = "Moon SoI Entry Point";
        sphereOfInfluenceEntryPoint.transform.SetParent(transform);
        sphereOfInfluenceEntryPoint.transform.localScale =
            UnityEngine.Vector3.one * sphereOfInfluenceEntryPointSize;

        Collider pointCollider =
            sphereOfInfluenceEntryPoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        sphereOfInfluenceEntryPointRenderer =
            sphereOfInfluenceEntryPoint.GetComponent<Renderer>();
        sphereOfInfluenceEntryPointRenderer.material.color =
            new Color(1.0f, 0.85f, 0.0f);
        RuntimeMaterialFactory.ApplyOrbitOverlayRenderer(sphereOfInfluenceEntryPointRenderer);
        sphereOfInfluenceEntryPoint.SetActive(false);
    }

    void CreateOrbitHoverPoint()
    {
        orbitHoverPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbitHoverPoint.name = "Orbit Hover Point";
        orbitHoverPoint.transform.SetParent(transform);
        orbitHoverPoint.transform.localScale = UnityEngine.Vector3.one * hoverPointSize;

        Collider pointCollider = orbitHoverPoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        orbitHoverPointRenderer = orbitHoverPoint.GetComponent<Renderer>();
        orbitHoverPointRenderer.material.color = new Color(1.0f, 1.0f, 0.15f);
        RuntimeMaterialFactory.ApplyOrbitOverlayRenderer(orbitHoverPointRenderer);
        orbitHoverPoint.SetActive(false);
    }

    void CreateManeuverNodePoint()
    {
        maneuverNodePoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        maneuverNodePoint.name = "Maneuver Node Point";
        maneuverNodePoint.transform.SetParent(transform);
        maneuverNodePoint.transform.localScale = UnityEngine.Vector3.one * hoverPointSize * 1.35f;

        Collider pointCollider = maneuverNodePoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        maneuverNodePointRenderer = maneuverNodePoint.GetComponent<Renderer>();
        maneuverNodePointRenderer.material.color = new Color(1.0f, 0.85f, 0.0f);
        maneuverNodePoint.SetActive(false);
    }

    void CreateBurnStartPoint()
    {
        burnStartPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burnStartPoint.name = "Burn Start Point";
        burnStartPoint.transform.SetParent(transform);
        burnStartPoint.transform.localScale = UnityEngine.Vector3.one * hoverPointSize * 1.15f;

        Collider pointCollider = burnStartPoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        burnStartPointRenderer = burnStartPoint.GetComponent<Renderer>();
        burnStartPointRenderer.material.color = new Color(1.0f, 0.55f, 0.15f);
        burnStartPoint.SetActive(false);
    }

    void CreateManeuverHandleLines()
    {
        maneuverProgradeLineRenderer = CreateManeuverHandleLine(
            "Maneuver Prograde Handle",
            new Color(0.25f, 1.0f, 0.25f, 0.95f)
        );
        maneuverRetrogradeLineRenderer = CreateManeuverHandleLine(
            "Maneuver Retrograde Handle",
            new Color(0.08f, 0.55f, 0.08f, 0.95f)
        );
        maneuverRadialOutLineRenderer = CreateManeuverHandleLine(
            "Maneuver Radial Out Handle",
            new Color(0.25f, 0.85f, 1.0f, 0.95f)
        );
        maneuverRadialInLineRenderer = CreateManeuverHandleLine(
            "Maneuver Radial In Handle",
            new Color(0.08f, 0.35f, 0.75f, 0.95f)
        );
    }

    LineRenderer CreateManeuverHandleLine(string lineName, Color color)
    {
        return CreateOrbitLine(
            lineName,
            color,
            OrbitLineVisualUtility.ManeuverHandlePixelWidth
        );
    }

    void UpdateOrbitMarkers(
        TextMesh periapsisText,
        TextMesh apoapsisText,
        global::SimVector3[] orbitPoints,
        global::SimVector3 primaryPosition,
        double primaryRadius,
        double truePeriapsisAltitude,
        double trueApoapsisAltitude,
        double timeToPeriapsis = double.NaN,
        double timeToApoapsis = double.NaN,
        bool stretchHeliocentric = false
    )
    {
        bool isPlannedMarker = periapsisText == plannedPeriapsisMarker;

        if (orbitPoints.Length == 0)
        {
            HideMarkers(periapsisText, apoapsisText);
            ClearOrbitMarkerCache(isPlannedMarker);
            return;
        }

        double periapsisRadius = primaryRadius + truePeriapsisAltitude;
        double apoapsisRadius = primaryRadius + trueApoapsisAltitude;

        if (
            IsValidOrbitMarkerAltitude(truePeriapsisAltitude) &&
            IsValidOrbitMarkerAltitude(trueApoapsisAltitude) &&
            IsNearlyCircular(periapsisRadius, apoapsisRadius)
        )
        {
            HideMarkers(periapsisText, apoapsisText);
            ClearOrbitMarkerCache(isPlannedMarker);
            return;
        }

        UpdateSingleOrbitMarker(
            periapsisText,
            orbitPoints,
            primaryPosition,
            primaryRadius,
            truePeriapsisAltitude,
            "Pe",
            timeToPeriapsis,
            isPlannedMarker,
            isPeriapsis: true,
            stretchHeliocentric
        );
        UpdateSingleOrbitMarker(
            apoapsisText,
            orbitPoints,
            primaryPosition,
            primaryRadius,
            trueApoapsisAltitude,
            "Ap",
            timeToApoapsis,
            isPlannedMarker,
            isPeriapsis: false,
            stretchHeliocentric
        );
    }

    void UpdateSingleOrbitMarker(
        TextMesh marker,
        global::SimVector3[] orbitPoints,
        global::SimVector3 primaryPosition,
        double primaryRadius,
        double trueAltitude,
        string markerLabel,
        double markerTime,
        bool isPlannedMarker,
        bool isPeriapsis,
        bool stretchHeliocentric = false
    )
    {
        if (!IsValidOrbitMarkerAltitude(trueAltitude))
        {
            marker.gameObject.SetActive(false);
            ClearOrbitMarkerCacheEntry(isPlannedMarker, isPeriapsis);
            return;
        }

        int markerIndex = FindOrbitMarkerIndexForAltitude(
            orbitPoints,
            primaryPosition,
            primaryRadius,
            trueAltitude
        );

        if (markerIndex < 0)
        {
            marker.gameObject.SetActive(false);
            ClearOrbitMarkerCacheEntry(isPlannedMarker, isPeriapsis);
            return;
        }

        global::SimVector3 markerSimulationPosition = orbitPoints[markerIndex];

        if (
            runner != null &&
            runner.HasManeuverNode() &&
            IsNearManeuverNode(markerSimulationPosition)
        )
        {
            marker.gameObject.SetActive(false);
            ClearOrbitMarkerCacheEntry(isPlannedMarker, isPeriapsis);
            return;
        }

        if (isPeriapsis)
        {
            CachePeriapsisMarker(
                isPlannedMarker,
                markerSimulationPosition,
                trueAltitude,
                markerTime
            );
        }
        else
        {
            CacheApoapsisMarker(
                isPlannedMarker,
                markerSimulationPosition,
                trueAltitude,
                markerTime
            );
        }

        ShowMarker(
            marker,
            markerSimulationPosition,
            markerLabel,
            trueAltitude,
            markerTime,
            stretchHeliocentric
        );
    }

    bool IsNearManeuverNode(global::SimVector3 simulationPosition)
    {
        if (runner == null || !runner.HasManeuverNode())
        {
            return false;
        }

        global::SimVector3 nodePosition = runner.GetManeuverNodePosition();
        double separationMeters = nodePosition
            .Subtract(simulationPosition)
            .Length();
        double nodeAltitude = GetAltitudeAboveActivePrimary(nodePosition);
        double orbitRadius = simulation != null ?
            simulation.GetActivePrimaryRadius() + Math.Max(nodeAltitude, 0.0) :
            6_371_000.0;
        double epsilonMeters = Math.Max(2_000.0, orbitRadius * 0.0002);

        return separationMeters <= epsilonMeters;
    }

    string BuildManeuverNodeExtraLine(double nodeDeltaVMagnitude)
    {
        if (nodeDeltaVMagnitude <= 0.01)
        {
            return null;
        }

        return T("Burn duration") + " " +
            FormatTime(runner.GetEstimatedManeuverBurnDuration()) +
            "\nΔv " + FormatDeltaV(nodeDeltaVMagnitude);
    }

    void ClearOrbitMarkerCacheEntry(bool isPlannedMarker, bool isPeriapsis)
    {
        if (isPeriapsis)
        {
            if (isPlannedMarker)
            {
                hasCachedPlannedPeriapsisMarker = false;
            }
            else
            {
                hasCachedPeriapsisMarker = false;
            }

            return;
        }

        if (isPlannedMarker)
        {
            hasCachedPlannedApoapsisMarker = false;
        }
        else
        {
            hasCachedApoapsisMarker = false;
        }
    }

    static int FindOrbitMarkerIndexForAltitude(
        global::SimVector3[] orbitPoints,
        global::SimVector3 primaryPosition,
        double primaryRadius,
        double targetAltitude
    )
    {
        double targetRadius = primaryRadius + targetAltitude;
        int bestIndex = -1;
        double bestRadiusError = double.PositiveInfinity;

        for (int i = 0; i < orbitPoints.Length; i++)
        {
            double distance = orbitPoints[i].Subtract(primaryPosition).Length();
            double altitude = distance - primaryRadius;

            if (altitude <= 0.0)
            {
                continue;
            }

            double radiusError = Math.Abs(distance - targetRadius);

            if (radiusError < bestRadiusError)
            {
                bestRadiusError = radiusError;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    static bool IsValidOrbitMarkerAltitude(double altitudeAboveSphereMeters)
    {
        return
            !double.IsNaN(altitudeAboveSphereMeters) &&
            !double.IsInfinity(altitudeAboveSphereMeters) &&
            altitudeAboveSphereMeters > 0.0;
    }

    void CachePeriapsisMarker(
        bool isPlannedMarker,
        global::SimVector3 simulationPosition,
        double altitude,
        double timeToPeriapsis
    )
    {
        if (isPlannedMarker)
        {
            hasCachedPlannedPeriapsisMarker = true;
            cachedPlannedPeriapsisSimPosition = simulationPosition;
            cachedPlannedPeriapsisAltitude = altitude;
            return;
        }

        hasCachedPeriapsisMarker = true;
        cachedPeriapsisSimPosition = simulationPosition;
        cachedPeriapsisAltitude = altitude;
        cachedPeriapsisTime = timeToPeriapsis;
    }

    void CacheApoapsisMarker(
        bool isPlannedMarker,
        global::SimVector3 simulationPosition,
        double altitude,
        double timeToApoapsis
    )
    {
        if (isPlannedMarker)
        {
            hasCachedPlannedApoapsisMarker = true;
            cachedPlannedApoapsisSimPosition = simulationPosition;
            cachedPlannedApoapsisAltitude = altitude;
            return;
        }

        hasCachedApoapsisMarker = true;
        cachedApoapsisSimPosition = simulationPosition;
        cachedApoapsisAltitude = altitude;
        cachedApoapsisTime = timeToApoapsis;
    }

    void ClearOrbitMarkerCache(bool isPlannedMarker)
    {
        if (isPlannedMarker)
        {
            hasCachedPlannedPeriapsisMarker = false;
            hasCachedPlannedApoapsisMarker = false;
            return;
        }

        hasCachedPeriapsisMarker = false;
        hasCachedApoapsisMarker = false;
    }

    bool IsNearlyCircular(double periapsisDistance, double apoapsisDistance)
    {
        double averageDistance = (periapsisDistance + apoapsisDistance) * 0.5;
        double minimumVisibleSpread =
            Math.Max(0.01, averageDistance * nearCircularMarkerEccentricity);
        return Math.Abs(apoapsisDistance - periapsisDistance) <=
            minimumVisibleSpread;
    }

    bool TryShowOrbitMarker(
        TextMesh marker,
        UnityEngine.Vector3 worldPosition,
        string markerName,
        double altitudeAboveSphereMeters,
        double markerTime = double.NaN
    )
    {
        if (marker == null)
        {
            return false;
        }

        if (!IsValidOrbitMarkerAltitude(altitudeAboveSphereMeters))
        {
            marker.gameObject.SetActive(false);
            return false;
        }

        ShowEventMarker(
            marker,
            worldPosition,
            markerName,
            altitudeAboveSphereMeters,
            markerTime
        );
        return true;
    }

    void ShowMarker(
        TextMesh marker,
        global::SimVector3 simulationPosition,
        string markerName,
        double markerDistance,
        double markerTime = double.NaN,
        bool stretchHeliocentric = false
    )
    {
        TryShowOrbitMarker(
            marker,
            ToLayoutWorldPosition(simulationPosition, stretchHeliocentric),
            markerName,
            markerDistance,
            markerTime
        );
    }

    void ShowWorldMarker(
        TextMesh marker,
        UnityEngine.Vector3 worldPosition,
        string markerName,
        double markerDistance,
        double markerTime = double.NaN
    )
    {
        TryShowOrbitMarker(
            marker,
            worldPosition,
            markerName,
            markerDistance,
            markerTime
        );
    }

    void ShowEventMarker(
        TextMesh marker,
        UnityEngine.Vector3 worldPosition,
        string markerTitle,
        double markerValue,
        double markerTime,
        string extraLine = null,
        bool translateTitle = true
    )
    {
        marker.gameObject.SetActive(true);
        string markerText = BuildEventMarkerText(
            markerTitle,
            markerValue,
            markerTime,
            extraLine,
            translateTitle
        );

        if (marker.text != markerText)
        {
            marker.text = markerText;
        }

        marker.transform.position = ApplyMapOrbitMarkerDisplayPosition(
            worldPosition + new UnityEngine.Vector3(0.0f, eventLabelOffsetY, 0.0f)
        );
    }

    UnityEngine.Vector3 ApplyMapOrbitMarkerDisplayPosition(UnityEngine.Vector3 worldPosition)
    {
        Camera facingCamera = Camera.main;

        if (facingCamera == null)
        {
            return worldPosition;
        }

        UnityEngine.Vector3 toCamera = facingCamera.transform.position - worldPosition;
        float distanceToCamera = toCamera.magnitude;

        if (distanceToCamera <= 1e-4f)
        {
            return worldPosition;
        }

        float depthBias = OrbitLineScreenSpaceWidth.ConvertPixelWidthToWorld(
            facingCamera,
            distanceToCamera,
            14.0f
        );
        return worldPosition + toCamera.normalized * depthBias;
    }

    void ShowTextMarker(TextMesh marker, UnityEngine.Vector3 worldPosition, string text)
    {
        marker.gameObject.SetActive(true);

        if (marker.text != text)
        {
            marker.text = text;
        }

        marker.transform.position = ApplyMapOrbitMarkerDisplayPosition(worldPosition);
    }

    UnityEngine.Vector3 ToWorldPosition(global::SimVector3 simulationPosition)
    {
        if (runner != null)
        {
            return runner.ToWorldPosition(simulationPosition);
        }

        return new UnityEngine.Vector3(
            (float)(simulationPosition.x * worldScale),
            (float)(simulationPosition.y * worldScale),
            (float)(simulationPosition.z * worldScale)
        );
    }

    UnityEngine.Vector3 ToLayoutWorldPosition(
        global::SimVector3 simulationPosition,
        bool stretchHeliocentric
    )
    {
        if (
            stretchHeliocentric &&
            simulation != null &&
            WorldVisualBridge.ShouldExaggerateMapHeliocentric(
                simulation,
                runner != null ? runner.GetRenderOrigin() : global::SimVector3.Zero()
            )
        )
        {
            simulationPosition = WorldVisualBridge.ExaggerateMapAwayFromPlanet(
                simulationPosition,
                simulation
            );
        }

        return ToWorldPosition(simulationPosition);
    }

    bool MapOrbitIsHeliocentric(string bodyName)
    {
        if (string.IsNullOrEmpty(bodyName) || simulation == null)
        {
            return false;
        }

        Body parentBody = simulation.TryGetOrbitParentBody(bodyName);
        return parentBody != null && parentBody.name == "Sun";
    }

    global::SimVector3 ToSimulationPosition(UnityEngine.Vector3 worldPosition)
    {
        if (worldScale == 0.0f)
        {
            return global::SimVector3.Zero();
        }

        global::SimVector3 localPosition = new global::SimVector3(
            worldPosition.x / worldScale,
            worldPosition.y / worldScale,
            worldPosition.z / worldScale
        );

        if (runner != null)
        {
            return localPosition.Add(runner.GetRenderOrigin());
        }

        return localPosition;
    }

    internal void ClearAllOrbitOverlayPublic()
    {
        ClearAllOrbitOverlay();
        orbitOverlayClearedForFlight = true;
    }

    void ClearAllOrbitOverlay()
    {
        cachedCurrentOrbitSimPath = Array.Empty<global::SimVector3>();
        cachedCurrentOrbitLayoutBodyName = string.Empty;
        cachedPlannedOrbitSimPath = Array.Empty<global::SimVector3>();
        hasLastOrbitRenderOrigin = false;
        ClearLineRenderer(lineRenderer);
        ClearLineRenderer(plannedLineRenderer);
        ClearLineRenderer(sphereOfInfluenceEntryLineRenderer);
        ClearLineRenderer(moonSphereOfInfluenceOrbitLineRenderer);
        ClearLineRenderer(closestApproachLineRenderer);
        ClearLineRenderer(burnPreviewPathLineRenderer);
        ClearLineRenderer(descentPreviewPathLineRenderer);
        ClearLineRenderer(maneuverProgradeLineRenderer);
        ClearLineRenderer(maneuverRetrogradeLineRenderer);
        ClearLineRenderer(maneuverRadialOutLineRenderer);
        ClearLineRenderer(maneuverRadialInLineRenderer);
        ClearBackgroundMapOrbits();

        HideMarkers(periapsisMarker, apoapsisMarker);
        HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);

        if (closestApproachMarker != null)
        {
            closestApproachMarker.gameObject.SetActive(false);
        }

        if (closestApproachPoint != null)
        {
            closestApproachPoint.SetActive(false);
        }

        HideClosestApproachRendezvousView();

        if (maneuverNodePoint != null)
        {
            maneuverNodePoint.SetActive(false);
        }

        HideBurnStartMarker();
        HideManeuverNodeLabels();
        HideSphereOfInfluenceEntryPreview();
        ResetEventMarkerHighlights();
    }

    static void ClearLineRenderer(LineRenderer targetLineRenderer)
    {
        if (targetLineRenderer == null)
        {
            return;
        }

        targetLineRenderer.positionCount = 0;
        targetLineRenderer.enabled = false;
    }

    void ClearMapOnlyPreview()
    {
        ClearAllOrbitOverlay();
    }

    void HideMarkers(TextMesh periapsisText, TextMesh apoapsisText)
    {
        periapsisText.gameObject.SetActive(false);
        apoapsisText.gameObject.SetActive(false);
    }

    void HideMarker(TextMesh marker)
    {
        if (marker != null)
        {
            marker.gameObject.SetActive(false);
        }
    }

    double Dot(global::SimVector3 left, global::SimVector3 right)
    {
        return global::SimVector3.Dot(left, right);
    }

    string FormatDistance(double distance)
    {
        if (distance >= 1000.0)
        {
            return distance.ToString("0");
        }

        return distance.ToString("0.0");
    }

    string FormatDeltaV(double deltaV)
    {
        return deltaV.ToString("0.0");
    }

    string BuildEventMarkerText(
        string markerTitle,
        double markerValue,
        double markerTime,
        string extraLine = null,
        bool translateTitle = true
    )
    {
        string text = translateTitle ? T(markerTitle) : markerTitle;

        if (!double.IsNaN(markerValue) && !double.IsInfinity(markerValue))
        {
            string valueLabel = markerTitle == "CA" ? T("Dist") : T("Alt");
            text += "\n" + valueLabel + " " + FormatDistance(markerValue);
        }

        if (!double.IsNaN(markerTime) && !double.IsInfinity(markerTime))
        {
            text += "\nT+" + FormatTime(markerTime);
        }

        if (!string.IsNullOrEmpty(extraLine))
        {
            text += "\n" + extraLine;
        }

        return text;
    }

    string BuildHoverTooltip(double orbitTime)
    {
        global::SimVector3 hoverPosition =
            ToSimulationPosition(hoveredOrbitPointPosition);
        double hoverAltitude = GetAltitudeAboveActivePrimary(hoverPosition);

        return T("Orbit time T+") + FormatTime(orbitTime) + "\n" +
            T("Body") + ": " + runner.GetOrbitFocusBodyName() + "  " +
            T("Alt") + ": " + FormatDistance(hoverAltitude);
    }

    void DrawOrbitLegend()
    {
        if (!showOrbitLegend || runner == null || !runner.IsMapModeActive)
        {
            return;
        }

        float legendWidth = 250.0f;
        float legendHeight = runner.HasBurnDiagnosticOverlay() ? 108.0f : 84.0f;
        float x = Screen.width - legendWidth - 12.0f;
        float y = Screen.height - legendHeight - 12.0f;

        GUILayout.BeginArea(new Rect(x, y, legendWidth, legendHeight));
        GUILayout.Label(T("Orbit legend current"));
        GUILayout.Label(T("Orbit legend planned"));
        GUILayout.Label(T("Orbit legend descent"));
        GUILayout.Label(T("Orbit legend trail"));

        if (runner.HasBurnDiagnosticOverlay())
        {
            GUILayout.Label(T("Drift legend"));
        }

        GUILayout.EndArea();
    }

    double GetAltitudeAboveActivePrimary(global::SimVector3 simulationPosition)
    {
        if (simulation == null)
        {
            return double.NaN;
        }

        global::SimVector3 primaryPosition = simulation.GetActivePrimaryPosition();
        double primaryRadius = simulation.GetActivePrimaryRadius();
        return simulationPosition
            .Subtract(primaryPosition)
            .Length() - primaryRadius;
    }

    string FormatNumber(double value)
    {
        if (double.IsInfinity(value))
        {
            return "inf";
        }

        return value.ToString("0.00");
    }

    string FormatTime(double time)
    {
        return time.ToString("0.0") + "s";
    }
}
