using System;
using UnityEngine;

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
    public double sphereOfInfluenceLabelMargin = 2.0;

    Simulation simulation;
    SimulationRunner runner;
    float worldScale;
    LineRenderer lineRenderer;
    LineRenderer plannedLineRenderer;
    LineRenderer sphereOfInfluenceEntryLineRenderer;
    TextMesh periapsisMarker;
    TextMesh apoapsisMarker;
    TextMesh plannedPeriapsisMarker;
    TextMesh plannedApoapsisMarker;
    TextMesh closestApproachMarker;
    GameObject closestApproachPoint;
    GameObject sphereOfInfluenceEntryPoint;
    Renderer closestApproachPointRenderer;
    Renderer sphereOfInfluenceEntryPointRenderer;
    Vector3 closestApproachMarkerPosition;
    Vector3 closestApproachPointPosition;
    int frameCounter;
    bool hasClosestApproachMarkerPosition;
    bool hasClosestApproachPointPosition;
    bool closestApproachWasInsideSphereOfInfluence;

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

        periapsisMarker = CreateOrbitMarker("Current Pe Marker", "Pe", new Color(0.0f, 1.0f, 1.0f));
        apoapsisMarker = CreateOrbitMarker("Current Ap Marker", "Ap", new Color(0.0f, 1.0f, 1.0f));
        plannedPeriapsisMarker = CreateOrbitMarker("Planned Pe Marker", "Pe", new Color(1.0f, 0.85f, 0.0f));
        plannedApoapsisMarker = CreateOrbitMarker("Planned Ap Marker", "Ap", new Color(1.0f, 0.85f, 0.0f));
        closestApproachMarker = CreateOrbitMarker("Closest Approach Marker", "CA", new Color(1.0f, 1.0f, 1.0f));
        CreateClosestApproachPoint();
        CreateSphereOfInfluenceEntryPoint();
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
    }

    void UpdatePreview()
    {
        if (simulation == null || lineRenderer == null)
        {
            return;
        }

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

        if (runner == null)
        {
            HideMarkers(plannedPeriapsisMarker, plannedApoapsisMarker);
            closestApproachMarker.gameObject.SetActive(false);
            closestApproachPoint.SetActive(false);
            HideSphereOfInfluenceEntryPreview();
            return;
        }

        global::Vector2 plannedDeltaV = runner.GetPlannedDeltaV();
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

        global::Vector2[] plannedOrbitPoints =
            simulation.GetPreviewShipOrbitPathRelativeToActivePrimary(
                plannedDeltaV,
                previewPointCount
            );
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

        string markerText = GetStableClosestApproachMarkerText(closestApproach) ?
            "SoI" :
            "CA";

        closestApproachMarker.gameObject.SetActive(true);
        closestApproachMarker.text = markerText;
        closestApproachMarker.transform.position =
            GetSmoothedClosestApproachMarkerPosition(closestApproach.shipPosition);
    }

    SphereOfInfluenceEntryPreview UpdateSphereOfInfluenceEntryPreview()
    {
        if (!showSphereOfInfluenceEntryPreview)
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
        sphereOfInfluenceEntryPoint.SetActive(true);
        sphereOfInfluenceEntryPoint.transform.position =
            ToWorldPosition(preview.entryPosition);

        return preview;
    }

    void HideSphereOfInfluenceEntryPreview()
    {
        sphereOfInfluenceEntryLineRenderer.positionCount = 0;
        sphereOfInfluenceEntryPoint.SetActive(false);
    }

    void UpdateClosestApproachPoint(
        bool hideWhenSphereOfInfluenceEntryExists
    )
    {
        if (!showClosestApproachPoint || hideWhenSphereOfInfluenceEntryExists)
        {
            closestApproachPoint.SetActive(false);
            return;
        }

        ClosestApproachInfo closestApproach =
            runner.GetCachedClosestApproach();

        if (!closestApproach.hasApproach)
        {
            closestApproachPoint.SetActive(false);
            return;
        }

        Vector3 targetPosition = ToWorldPosition(closestApproach.shipPosition);

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
    }

    bool GetStableClosestApproachMarkerText(ClosestApproachInfo closestApproach)
    {
        double sphereOfInfluenceRadius = simulation.moonSphereOfInfluenceRadius;

        if (closestApproach.distance <= sphereOfInfluenceRadius - sphereOfInfluenceLabelMargin)
        {
            closestApproachWasInsideSphereOfInfluence = true;
        }
        else if (closestApproach.distance >= sphereOfInfluenceRadius + sphereOfInfluenceLabelMargin)
        {
            closestApproachWasInsideSphereOfInfluence = false;
        }

        return closestApproachWasInsideSphereOfInfluence;
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

    Vector3 ToWorldPosition(global::Vector2 simulationPosition)
    {
        return new Vector3(
            (float)(simulationPosition.x * worldScale),
            (float)(simulationPosition.y * worldScale),
            -0.1f
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
}
