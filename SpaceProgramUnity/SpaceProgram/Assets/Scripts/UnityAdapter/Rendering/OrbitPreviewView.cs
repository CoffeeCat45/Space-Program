using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitPreviewView : MonoBehaviour
{
    public int previewPointCount = 360;
    public int updateEveryFrames = 10;
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

    Simulation simulation;
    SimulationRunner runner;
    float worldScale;
    LineRenderer lineRenderer;
    LineRenderer plannedLineRenderer;
    LineRenderer sphereOfInfluenceEntryLineRenderer;
    LineRenderer moonSphereOfInfluenceOrbitLineRenderer;
    LineRenderer closestApproachLineRenderer;
    LineRenderer maneuverProgradeLineRenderer;
    LineRenderer maneuverRetrogradeLineRenderer;
    LineRenderer maneuverRadialOutLineRenderer;
    LineRenderer maneuverRadialInLineRenderer;
    TextMesh periapsisMarker;
    TextMesh apoapsisMarker;
    TextMesh plannedPeriapsisMarker;
    TextMesh plannedApoapsisMarker;
    TextMesh closestApproachMarker;
    TextMesh closestApproachDistanceMarker;
    GameObject closestApproachPoint;
    GameObject closestApproachTargetPoint;
    GameObject sphereOfInfluenceEntryPoint;
    GameObject orbitHoverPoint;
    GameObject maneuverNodePoint;
    Renderer closestApproachPointRenderer;
    Renderer closestApproachTargetPointRenderer;
    Renderer sphereOfInfluenceEntryPointRenderer;
    Renderer orbitHoverPointRenderer;
    Renderer maneuverNodePointRenderer;
    Vector3 closestApproachMarkerPosition;
    Vector3 closestApproachPointPosition;
    Vector3[] hoverPredictionWorldPoints = Array.Empty<Vector3>();
    double[] hoverPredictionTimes = Array.Empty<double>();
    Vector3 hoveredOrbitPointPosition;
    double hoveredOrbitPointTime;
    Vector3 maneuverNodeWorldPosition;
    UnityEngine.Vector2 hoverGuiPosition;
    UnityEngine.Vector2 warpMenuGuiPosition;
    double warpMenuTime;
    int frameCounter;
    bool hasClosestApproachMarkerPosition;
    bool hasClosestApproachPointPosition;
    bool hasHoveredOrbitPoint;
    bool showWarpContextMenu;
    bool isDraggingManeuverNode;

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

    void Awake()
    {
        GameObject lineObject = new GameObject("Ship Orbit Preview");
        lineObject.transform.SetParent(transform);

        lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.035f;
        lineRenderer.endWidth = 0.035f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(0.0f, 1.0f, 1.0f, 0.75f);
        lineRenderer.endColor = new Color(0.0f, 1.0f, 1.0f, 0.75f);

        GameObject plannedLineObject = new GameObject("Planned Orbit Preview");
        plannedLineObject.transform.SetParent(transform);

        plannedLineRenderer = plannedLineObject.AddComponent<LineRenderer>();
        plannedLineRenderer.positionCount = 0;
        plannedLineRenderer.useWorldSpace = true;
        plannedLineRenderer.startWidth = 0.035f;
        plannedLineRenderer.endWidth = 0.035f;
        plannedLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        plannedLineRenderer.startColor = new Color(1.0f, 0.85f, 0.0f, 0.85f);
        plannedLineRenderer.endColor = new Color(1.0f, 0.85f, 0.0f, 0.85f);

        GameObject sphereOfInfluenceEntryLineObject =
            new GameObject("Moon SoI Entry Preview");
        sphereOfInfluenceEntryLineObject.transform.SetParent(transform);

        sphereOfInfluenceEntryLineRenderer =
            sphereOfInfluenceEntryLineObject.AddComponent<LineRenderer>();
        sphereOfInfluenceEntryLineRenderer.positionCount = 0;
        sphereOfInfluenceEntryLineRenderer.useWorldSpace = true;
        sphereOfInfluenceEntryLineRenderer.startWidth = 0.05f;
        sphereOfInfluenceEntryLineRenderer.endWidth = 0.05f;
        sphereOfInfluenceEntryLineRenderer.material =
            new Material(Shader.Find("Sprites/Default"));
        sphereOfInfluenceEntryLineRenderer.startColor =
            new Color(1.0f, 0.85f, 0.0f, 0.95f);
        sphereOfInfluenceEntryLineRenderer.endColor =
            new Color(1.0f, 0.85f, 0.0f, 0.95f);

        GameObject moonSphereOfInfluenceOrbitLineObject =
            new GameObject("Moon SoI Orbit Preview");
        moonSphereOfInfluenceOrbitLineObject.transform.SetParent(transform);

        moonSphereOfInfluenceOrbitLineRenderer =
            moonSphereOfInfluenceOrbitLineObject.AddComponent<LineRenderer>();
        moonSphereOfInfluenceOrbitLineRenderer.positionCount = 0;
        moonSphereOfInfluenceOrbitLineRenderer.useWorldSpace = true;
        moonSphereOfInfluenceOrbitLineRenderer.startWidth = 0.045f;
        moonSphereOfInfluenceOrbitLineRenderer.endWidth = 0.045f;
        moonSphereOfInfluenceOrbitLineRenderer.material =
            new Material(Shader.Find("Sprites/Default"));
        moonSphereOfInfluenceOrbitLineRenderer.startColor =
            new Color(0.4f, 1.0f, 0.55f, 0.95f);
        moonSphereOfInfluenceOrbitLineRenderer.endColor =
            new Color(0.4f, 1.0f, 0.55f, 0.95f);

        periapsisMarker = CreateOrbitMarker("Current Pe Marker", "Pe", new Color(0.0f, 1.0f, 1.0f));
        apoapsisMarker = CreateOrbitMarker("Current Ap Marker", "Ap", new Color(0.0f, 1.0f, 1.0f));
        plannedPeriapsisMarker = CreateOrbitMarker("Planned Pe Marker", "Pe", new Color(1.0f, 0.85f, 0.0f));
        plannedApoapsisMarker = CreateOrbitMarker("Planned Ap Marker", "Ap", new Color(1.0f, 0.85f, 0.0f));
        closestApproachMarker = CreateOrbitMarker("Closest Approach Marker", "CA", new Color(1.0f, 1.0f, 1.0f));
        closestApproachDistanceMarker = CreateOrbitMarker("Closest Approach Distance Marker", "CA", new Color(1.0f, 0.75f, 0.95f));
        closestApproachLineRenderer = CreateClosestApproachLine();
        CreateClosestApproachPoint();
        CreateClosestApproachTargetPoint();
        CreateSphereOfInfluenceEntryPoint();
        CreateOrbitHoverPoint();
        CreateManeuverNodePoint();
        CreateManeuverHandleLines();
    }

    void LateUpdate()
    {
        frameCounter++;

        if (frameCounter < updateEveryFrames)
        {
            return;
        }

        frameCounter = 0;
        UpdatePreview();
        UpdateOrbitHover();
    }

    void Update()
    {
        UpdateOrbitHover();
    }

    void UpdatePreview()
    {
        if (simulation == null || lineRenderer == null)
        {
            return;
        }

        if (runner == null)
        {
            global::Vector2[] currentOrbitPoints =
                simulation.GetShipOrbitPathRelativeToActivePrimary(previewPointCount);
            DrawOrbit(lineRenderer, currentOrbitPoints);
            UpdateOrbitMarkers(
                periapsisMarker,
                apoapsisMarker,
                currentOrbitPoints,
                simulation.GetActivePrimaryPosition(),
                simulation.GetActivePrimaryRadius(),
                simulation.GetShipOrbitTypeRelativeToActivePrimary() == "Ellipse"
            );
            HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);
            closestApproachMarker.gameObject.SetActive(false);
            closestApproachPoint.SetActive(false);
            HideClosestApproachRendezvousView();
            maneuverNodePoint.SetActive(false);
            HideSphereOfInfluenceEntryPreview();
            return;
        }

        global::Vector2 plannedDeltaV = runner.GetPlannedDeltaV();
        bool hasManeuverNode = runner.HasManeuverNode();
        global::Vector2[] orbitPoints =
            simulation.GetShipOrbitPathRelativeToActivePrimary(previewPointCount);
        DrawOrbit(lineRenderer, orbitPoints);
        UpdateOrbitMarkers(
            periapsisMarker,
            apoapsisMarker,
            orbitPoints,
            simulation.GetActivePrimaryPosition(),
            simulation.GetActivePrimaryRadius(),
            simulation.GetShipOrbitTypeRelativeToActivePrimary() == "Ellipse"
        );
        UpdateHoverPrediction(
            hasManeuverNode ? global::Vector2.Zero() : plannedDeltaV
        );
        UpdateManeuverNodePoint();

        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview =
            UpdateSphereOfInfluenceEntryPreview();
        UpdateClosestApproachMarker();
        UpdateClosestApproachPoint(
            sphereOfInfluenceEntryPreview.entersSphereOfInfluence
        );

        if (plannedDeltaV.LengthSquared() == 0)
        {
            plannedLineRenderer.positionCount = 0;
            HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);
            return;
        }

        if (sphereOfInfluenceEntryPreview.entersSphereOfInfluence)
        {
            plannedLineRenderer.positionCount = 0;
            HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);
            return;
        }

        global::Vector2[] plannedOrbitPoints =
            runner.GetPlannedOrbitPath(previewPointCount);
        DrawOrbit(plannedLineRenderer, plannedOrbitPoints);
        UpdateOrbitMarkers(
            plannedPeriapsisMarker,
            plannedApoapsisMarker,
            plannedOrbitPoints,
            simulation.GetActivePrimaryPosition(),
            simulation.GetActivePrimaryRadius(),
            simulation.GetPreviewShipOrbitTypeRelativeToActivePrimary(plannedDeltaV) == "Ellipse"
        );
    }

    void UpdateHoverPrediction(global::Vector2 plannedDeltaV)
    {
        hoverPredictionWorldPoints = BuildHoverPrediction(
            plannedDeltaV,
            out hoverPredictionTimes
        );
    }

    Vector3[] BuildHoverPrediction(
        global::Vector2 plannedDeltaV,
        out double[] predictionTimes
    )
    {
        if (hoverPredictionStepCount <= 0 || hoverPredictionDt <= 0)
        {
            predictionTimes = Array.Empty<double>();
            return Array.Empty<Vector3>();
        }

        Simulation preview = simulation.CreateCopy();
        preview.ship.thrust = global::Vector2.Zero();
        preview.ApplyShipDeltaV(plannedDeltaV);

        double orbitPeriod =
            plannedDeltaV.LengthSquared() > 0 ?
            simulation.GetPreviewShipOrbitalPeriodRelativeToActivePrimary(plannedDeltaV) :
            simulation.GetShipOrbitalPeriodRelativeToActivePrimary();
        bool isClosedOrbit =
            !double.IsInfinity(orbitPeriod) &&
            orbitPeriod > hoverPredictionDt;

        if (isClosedOrbit)
        {
            global::Vector2[] orbitPoints =
                plannedDeltaV.LengthSquared() > 0 ?
                simulation.GetPreviewShipOrbitPathRelativeToActivePrimary(
                    plannedDeltaV,
                    previewPointCount
                ) :
                simulation.GetShipOrbitPathRelativeToActivePrimary(
                    previewPointCount
                );
            return BuildHoverPredictionFromOrbitPoints(
                orbitPoints,
                orbitPeriod,
                preview.ship.position,
                out predictionTimes
            );
        }

        int sampleCount = hoverPredictionStepCount;
        double sampleDt = hoverPredictionDt;
        Vector3[] predictionWorldPoints = new Vector3[sampleCount + 1];
        predictionTimes = new double[sampleCount + 1];

        for (int i = 0; i <= sampleCount; i++)
        {
            predictionWorldPoints[i] = ToWorldPosition(preview.ship.position);
            predictionTimes[i] = i * sampleDt;
            preview.Update(sampleDt);
        }

        return predictionWorldPoints;
    }

    Vector3[] BuildHoverPredictionFromOrbitPoints(
        global::Vector2[] orbitPoints,
        double orbitPeriod,
        global::Vector2 currentShipPosition,
        out double[] predictionTimes
    )
    {
        int orbitPointCount = GetUniqueClosedOrbitPointCount(orbitPoints);

        if (orbitPointCount < 2)
        {
            predictionTimes = Array.Empty<double>();
            return Array.Empty<Vector3>();
        }

        int startIndex = FindClosestOrbitPointIndex(
            orbitPoints,
            orbitPointCount,
            currentShipPosition
        );
        Vector3[] predictionWorldPoints = new Vector3[orbitPointCount + 1];
        predictionTimes = new double[orbitPointCount + 1];

        for (int i = 0; i <= orbitPointCount; i++)
        {
            int orbitPointIndex = (startIndex + i) % orbitPointCount;
            predictionWorldPoints[i] = ToWorldPosition(orbitPoints[orbitPointIndex]);
            predictionTimes[i] = orbitPeriod * i / orbitPointCount;
        }

        return predictionWorldPoints;
    }

    int GetUniqueClosedOrbitPointCount(global::Vector2[] orbitPoints)
    {
        if (orbitPoints.Length < 2)
        {
            return orbitPoints.Length;
        }

        global::Vector2 firstToLast =
            orbitPoints[0].Subtract(orbitPoints[orbitPoints.Length - 1]);

        if (firstToLast.LengthSquared() < 0.000001)
        {
            return orbitPoints.Length - 1;
        }

        return orbitPoints.Length;
    }

    int FindClosestOrbitPointIndex(
        global::Vector2[] orbitPoints,
        int orbitPointCount,
        global::Vector2 currentShipPosition
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
                    out Vector3 dragWorldPosition,
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
                out Vector3 closestWorldPosition,
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
            warpMenuGuiPosition = hoverGuiPosition;
        }

    }

    bool TryFindClosestPredictionPoint(
        UnityEngine.Vector2 mousePosition,
        float pickRadiusPixels,
        out Vector3 closestWorldPosition,
        out double closestTime
    )
    {
        bool foundPoint = false;
        float closestDistance = pickRadiusPixels;
        closestWorldPosition = Vector3.zero;
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
                closestWorldPosition = Vector3.Lerp(
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
        Vector3 worldPosition,
        float pickRadiusPixels
    )
    {
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

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

            HideManeuverHandleLines();
            return;
        }

        maneuverNodeWorldPosition = ToWorldPosition(
            runner.GetManeuverNodePosition()
        );
        maneuverNodePoint.SetActive(true);
        maneuverNodePoint.transform.position = maneuverNodeWorldPosition;
        UpdateManeuverHandleLines();
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
        global::Vector2 direction,
        Color color
    )
    {
        Vector3 endPosition = maneuverNodeWorldPosition + new Vector3(
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

    Vector3 GetWorldPositionAtPredictionTime(double predictionTime)
    {
        if (hoverPredictionWorldPoints.Length == 0)
        {
            return Vector3.zero;
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

                return Vector3.Lerp(
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
        if (!hasHoveredOrbitPoint || runner == null)
        {
            return;
        }

        UnityEngine.Vector2 guiPosition =
            showWarpContextMenu ? warpMenuGuiPosition : hoverGuiPosition;
        double displayTime =
            showWarpContextMenu ? warpMenuTime : hoveredOrbitPointTime;
        GUILayout.BeginArea(new Rect(guiPosition.x, guiPosition.y, 210, 260));
        GUILayout.Label("T+" + FormatTime(displayTime));

        if (showWarpContextMenu && GUILayout.Button("Warp here"))
        {
            runner.WarpBy(warpMenuTime);
            showWarpContextMenu = false;
            HideOrbitHoverPoint();
        }

        if (
            showWarpContextMenu &&
            runner.HasManeuverNode() &&
            GUILayout.Button("Warp to maneuver start")
        )
        {
            runner.WarpToManeuverStart();
            showWarpContextMenu = false;
            HideOrbitHoverPoint();
        }

        if (
            showWarpContextMenu &&
            runner.HasManeuverNode() &&
            GUILayout.Button("Auto execute maneuver")
        )
        {
            runner.StartAutoBurn();
            showWarpContextMenu = false;
            HideOrbitHoverPoint();
        }

        if (
            showWarpContextMenu &&
            runner.HasManeuverNode() &&
            GUILayout.Button("Remove maneuver")
        )
        {
            runner.ClearManeuverNode();
            showWarpContextMenu = false;
            HideOrbitHoverPoint();
        }

        if (showWarpContextMenu && GUILayout.Button("Add maneuver"))
        {
            runner.SetManeuverNode(
                warpMenuTime,
                ToSimulationPosition(hoveredOrbitPointPosition)
            );
            showWarpContextMenu = false;
            HideOrbitHoverPoint();
        }

        if (showWarpContextMenu && GUILayout.Button("Add maneuver at Pe"))
        {
            runner.SetManeuverNodeAtPeriapsis();
            showWarpContextMenu = false;
            HideOrbitHoverPoint();
        }

        if (showWarpContextMenu && GUILayout.Button("Add maneuver at Ap"))
        {
            runner.SetManeuverNodeAtApoapsis();
            showWarpContextMenu = false;
            HideOrbitHoverPoint();
        }

        GUILayout.EndArea();
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
        if (!showSphereOfInfluenceEntryPreview || simulation.activePrimaryBodyName == "Moon")
        {
            HideSphereOfInfluenceEntryPreview();
            return SphereOfInfluenceEntryPreview.None();
        }

        SphereOfInfluenceEntryPreview preview =
            runner.GetCachedSphereOfInfluenceEntryPreview();

        if (!preview.entersSphereOfInfluence)
        {
            HideSphereOfInfluenceEntryPreview();
            return preview;
        }

        DrawOrbit(sphereOfInfluenceEntryLineRenderer, preview.pathPoints);
        DrawOrbit(
            moonSphereOfInfluenceOrbitLineRenderer,
            preview.moonOrbitPathPoints
        );
        sphereOfInfluenceEntryPoint.SetActive(true);
        sphereOfInfluenceEntryPoint.transform.position =
            ToWorldPosition(preview.entryPosition);

        return preview;
    }

    void HideSphereOfInfluenceEntryPreview()
    {
        sphereOfInfluenceEntryLineRenderer.positionCount = 0;
        moonSphereOfInfluenceOrbitLineRenderer.positionCount = 0;
        sphereOfInfluenceEntryPoint.SetActive(false);
    }

    void UpdateClosestApproachPoint(
        bool hideWhenSphereOfInfluenceEntryExists
    )
    {
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

        Vector3 targetPosition = ToWorldPosition(closestApproach.shipPosition);
        Vector3 targetBodyPosition = ToWorldPosition(closestApproach.targetPosition);

        if (
            !hasClosestApproachPointPosition ||
            Vector3.Distance(closestApproachPointPosition, targetPosition) >=
                closestApproachPointMoveThreshold
        )
        {
            closestApproachPointPosition = targetPosition;
            hasClosestApproachPointPosition = true;
        }

        closestApproachPoint.SetActive(true);
        closestApproachPoint.transform.position = closestApproachPointPosition;
        closestApproachPointRenderer.material.color =
            closestApproach.entersMoonSphereOfInfluence ?
            new Color(1.0f, 0.85f, 0.0f) :
            Color.white;

        closestApproachTargetPoint.SetActive(true);
        closestApproachTargetPoint.transform.position = targetBodyPosition;
        closestApproachLineRenderer.positionCount = 2;
        closestApproachLineRenderer.SetPosition(0, closestApproachPointPosition);
        closestApproachLineRenderer.SetPosition(1, targetBodyPosition);
        ShowMarker(
            closestApproachDistanceMarker,
            (closestApproachPointPosition + targetBodyPosition) * 0.5f,
            "CA",
            closestApproach.distance
        );
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
    }

    Vector3 GetSmoothedClosestApproachMarkerPosition(global::Vector2 simulationPosition)
    {
        Vector3 targetPosition = ToWorldPosition(simulationPosition);

        if (
            !hasClosestApproachMarkerPosition ||
            Vector3.Distance(closestApproachMarkerPosition, targetPosition) > 3.0f
        )
        {
            closestApproachMarkerPosition = targetPosition;
            hasClosestApproachMarkerPosition = true;
            return closestApproachMarkerPosition;
        }

        closestApproachMarkerPosition = Vector3.Lerp(
            closestApproachMarkerPosition,
            targetPosition,
            closestApproachMarkerSmoothing
        );

        return closestApproachMarkerPosition;
    }

    void DrawOrbit(LineRenderer targetLineRenderer, global::Vector2[] orbitPoints)
    {
        Vector3[] points = new Vector3[orbitPoints.Length];

        for (int i = 0; i < orbitPoints.Length; i++)
        {
            points[i] = ToWorldPosition(orbitPoints[i]);
        }

        targetLineRenderer.positionCount = points.Length;
        targetLineRenderer.SetPositions(points);
    }

    void DrawWorldOrbit(LineRenderer targetLineRenderer, Vector3[] orbitPoints)
    {
        targetLineRenderer.positionCount = orbitPoints.Length;
        targetLineRenderer.SetPositions(orbitPoints);
    }

    TextMesh CreateOrbitMarker(string markerName, string markerText, Color color)
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

        markerObject.SetActive(false);

        return textMesh;
    }

    void CreateClosestApproachPoint()
    {
        closestApproachPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        closestApproachPoint.name = "Closest Approach Point";
        closestApproachPoint.transform.SetParent(transform);
        closestApproachPoint.transform.localScale =
            Vector3.one * closestApproachPointSize;

        Collider pointCollider = closestApproachPoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        closestApproachPointRenderer =
            closestApproachPoint.GetComponent<Renderer>();
        closestApproachPointRenderer.material.color = Color.white;
        closestApproachPoint.SetActive(false);
    }

    void CreateClosestApproachTargetPoint()
    {
        closestApproachTargetPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        closestApproachTargetPoint.name = "Closest Approach Target Point";
        closestApproachTargetPoint.transform.SetParent(transform);
        closestApproachTargetPoint.transform.localScale =
            Vector3.one * closestApproachPointSize * 0.85f;

        Collider pointCollider =
            closestApproachTargetPoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        closestApproachTargetPointRenderer =
            closestApproachTargetPoint.GetComponent<Renderer>();
        closestApproachTargetPointRenderer.material.color =
            new Color(1.0f, 0.45f, 0.9f);
        closestApproachTargetPoint.SetActive(false);
    }

    LineRenderer CreateClosestApproachLine()
    {
        GameObject lineObject = new GameObject("Closest Approach Distance Line");
        lineObject.transform.SetParent(transform);

        LineRenderer distanceLineRenderer = lineObject.AddComponent<LineRenderer>();
        distanceLineRenderer.positionCount = 0;
        distanceLineRenderer.useWorldSpace = true;
        distanceLineRenderer.startWidth = 0.035f;
        distanceLineRenderer.endWidth = 0.035f;
        distanceLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        distanceLineRenderer.startColor = new Color(1.0f, 0.75f, 0.95f, 0.9f);
        distanceLineRenderer.endColor = new Color(1.0f, 0.75f, 0.95f, 0.9f);

        return distanceLineRenderer;
    }

    void CreateSphereOfInfluenceEntryPoint()
    {
        sphereOfInfluenceEntryPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereOfInfluenceEntryPoint.name = "Moon SoI Entry Point";
        sphereOfInfluenceEntryPoint.transform.SetParent(transform);
        sphereOfInfluenceEntryPoint.transform.localScale =
            Vector3.one * sphereOfInfluenceEntryPointSize;

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
        sphereOfInfluenceEntryPoint.SetActive(false);
    }

    void CreateOrbitHoverPoint()
    {
        orbitHoverPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbitHoverPoint.name = "Orbit Hover Point";
        orbitHoverPoint.transform.SetParent(transform);
        orbitHoverPoint.transform.localScale = Vector3.one * hoverPointSize;

        Collider pointCollider = orbitHoverPoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        orbitHoverPointRenderer = orbitHoverPoint.GetComponent<Renderer>();
        orbitHoverPointRenderer.material.color = new Color(1.0f, 1.0f, 0.15f);
        orbitHoverPoint.SetActive(false);
    }

    void CreateManeuverNodePoint()
    {
        maneuverNodePoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        maneuverNodePoint.name = "Maneuver Node Point";
        maneuverNodePoint.transform.SetParent(transform);
        maneuverNodePoint.transform.localScale = Vector3.one * hoverPointSize * 1.35f;

        Collider pointCollider = maneuverNodePoint.GetComponent<Collider>();

        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        maneuverNodePointRenderer = maneuverNodePoint.GetComponent<Renderer>();
        maneuverNodePointRenderer.material.color = new Color(1.0f, 0.85f, 0.0f);
        maneuverNodePoint.SetActive(false);
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
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform);

        LineRenderer handleLineRenderer = lineObject.AddComponent<LineRenderer>();
        handleLineRenderer.positionCount = 0;
        handleLineRenderer.useWorldSpace = true;
        handleLineRenderer.startWidth = maneuverHandleWidth;
        handleLineRenderer.endWidth = maneuverHandleWidth;
        handleLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        handleLineRenderer.startColor = color;
        handleLineRenderer.endColor = color;

        return handleLineRenderer;
    }

    void UpdateOrbitMarkers(
        TextMesh periapsisText,
        TextMesh apoapsisText,
        global::Vector2[] orbitPoints,
        global::Vector2 primaryPosition,
        double primaryRadius,
        bool showApoapsis
    )
    {
        if (orbitPoints.Length == 0)
        {
            HideMarkers(periapsisText, apoapsisText);
            return;
        }

        int periapsisIndex = 0;
        int apoapsisIndex = 0;
        double periapsisDistanceSquared =
            orbitPoints[0].Subtract(primaryPosition).LengthSquared();
        double apoapsisDistanceSquared = periapsisDistanceSquared;

        for (int i = 1; i < orbitPoints.Length; i++)
        {
            double distanceSquared =
                orbitPoints[i].Subtract(primaryPosition).LengthSquared();

            if (distanceSquared < periapsisDistanceSquared)
            {
                periapsisIndex = i;
                periapsisDistanceSquared = distanceSquared;
            }

            if (distanceSquared > apoapsisDistanceSquared)
            {
                apoapsisIndex = i;
                apoapsisDistanceSquared = distanceSquared;
            }
        }

        ShowMarker(
            periapsisText,
            orbitPoints[periapsisIndex],
            "Pe",
            Math.Sqrt(periapsisDistanceSquared) - primaryRadius
        );

        if (showApoapsis)
        {
            ShowMarker(
                apoapsisText,
                orbitPoints[apoapsisIndex],
                "Ap",
                Math.Sqrt(apoapsisDistanceSquared) - primaryRadius
            );
        }
        else
        {
            apoapsisText.gameObject.SetActive(false);
        }
    }

    void UpdateOrbitMarkers(
        TextMesh periapsisText,
        TextMesh apoapsisText,
        Vector3[] orbitPoints,
        global::Vector2 primaryPosition,
        double primaryRadius,
        bool showApoapsis
    )
    {
        if (orbitPoints.Length == 0)
        {
            HideMarkers(periapsisText, apoapsisText);
            return;
        }

        int periapsisIndex = 0;
        int apoapsisIndex = 0;
        Vector3 primaryWorldPosition = ToWorldPosition(primaryPosition);
        double periapsisDistanceSquared =
            (orbitPoints[0] - primaryWorldPosition).sqrMagnitude;
        double apoapsisDistanceSquared = periapsisDistanceSquared;

        for (int i = 1; i < orbitPoints.Length; i++)
        {
            double distanceSquared =
                (orbitPoints[i] - primaryWorldPosition).sqrMagnitude;

            if (distanceSquared < periapsisDistanceSquared)
            {
                periapsisIndex = i;
                periapsisDistanceSquared = distanceSquared;
            }

            if (distanceSquared > apoapsisDistanceSquared)
            {
                apoapsisIndex = i;
                apoapsisDistanceSquared = distanceSquared;
            }
        }

        ShowMarker(
            periapsisText,
            orbitPoints[periapsisIndex],
            "Pe",
            Math.Sqrt(periapsisDistanceSquared) / worldScale - primaryRadius
        );

        if (showApoapsis)
        {
            ShowMarker(
                apoapsisText,
                orbitPoints[apoapsisIndex],
                "Ap",
                Math.Sqrt(apoapsisDistanceSquared) / worldScale - primaryRadius
            );
        }
        else
        {
            apoapsisText.gameObject.SetActive(false);
        }
    }

    void ShowMarker(
        TextMesh marker,
        global::Vector2 simulationPosition,
        string markerName,
        double markerDistance
    )
    {
        marker.gameObject.SetActive(true);
        marker.text = markerName + "\n" + FormatDistance(markerDistance);
        marker.transform.position = ToWorldPosition(simulationPosition);
    }

    void ShowMarker(
        TextMesh marker,
        Vector3 worldPosition,
        string markerName,
        double markerDistance
    )
    {
        marker.gameObject.SetActive(true);
        marker.text = markerName + "\n" + FormatDistance(markerDistance);
        marker.transform.position = worldPosition;
    }

    Vector3 ToWorldPosition(global::Vector2 simulationPosition)
    {
        return new Vector3(
            (float)(simulationPosition.x * worldScale),
            (float)(simulationPosition.y * worldScale),
            -0.1f
        );
    }

    global::Vector2 ToSimulationPosition(Vector3 worldPosition)
    {
        if (worldScale == 0.0f)
        {
            return global::Vector2.Zero();
        }

        return new global::Vector2(
            worldPosition.x / worldScale,
            worldPosition.y / worldScale
        );
    }

    void HideMarkers(TextMesh periapsisText, TextMesh apoapsisText)
    {
        periapsisText.gameObject.SetActive(false);
        apoapsisText.gameObject.SetActive(false);
    }

    string FormatDistance(double distance)
    {
        if (distance >= 1000.0)
        {
            return distance.ToString("0");
        }

        return distance.ToString("0.0");
    }

    string FormatTime(double time)
    {
        return time.ToString("0.0") + "s";
    }
}
