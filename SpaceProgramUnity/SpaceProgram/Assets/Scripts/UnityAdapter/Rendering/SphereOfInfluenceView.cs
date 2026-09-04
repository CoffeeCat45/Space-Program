using UnityEngine;

public class SphereOfInfluenceView : MonoBehaviour
{
    public int pointCount = 180;

    const int RingCount = 3;
    static readonly Color SoiSphereColor = new Color(0.35f, 0.85f, 1.0f, 0.55f);

    Simulation simulation;
    float worldScale;
    bool mapModeActive;
    string focusBodyName = "Ship";
    global::SimVector3 mapRenderOrigin = global::SimVector3.Zero();
    bool hasMapRenderOrigin;
    LineRenderer[] ringRenderers;

    internal void SetSimulation(Simulation newSimulation)
    {
        simulation = newSimulation;
    }

    internal void SetMapRenderContext(
        bool mapMode,
        float positionScale,
        global::SimVector3 renderOrigin,
        string orbitFocusBodyName
    )
    {
        mapModeActive = mapMode;
        worldScale = positionScale;
        mapRenderOrigin = renderOrigin;
        hasMapRenderOrigin = true;
        focusBodyName = string.IsNullOrEmpty(orbitFocusBodyName) ?
            "Ship" :
            orbitFocusBodyName;
    }

    void LateUpdate()
    {
        if (
            !DevSettings.SoiSphereEnabled ||
            simulation == null ||
            !mapModeActive
        )
        {
            HideAllRings();
            return;
        }

        if (
            !simulation.TryGetMapFocusSphereOfInfluence(
                focusBodyName,
                out Body soiBody,
                out double soiRadiusMeters
            )
        )
        {
            HideAllRings();
            return;
        }

        UpdateSoiSphere(soiBody.position, soiRadiusMeters);
    }

    void UpdateSoiSphere(global::SimVector3 bodyPosition, double soiRadiusMeters)
    {
        EnsureRingRenderers();
        Vector3 center = SimToMapWorldPosition(bodyPosition);
        float worldRadius = (float)(soiRadiusMeters * worldScale);
        int segmentCount = Mathf.Max(pointCount, 3);
        Vector3[] ringPoints = new Vector3[segmentCount + 1];

        for (int ringIndex = 0; ringIndex < RingCount; ringIndex++)
        {
            FillRingPositions(ringPoints, center, worldRadius, ringIndex);
            LineRenderer ringRenderer = ringRenderers[ringIndex];
            ringRenderer.enabled = true;
            ringRenderer.positionCount = ringPoints.Length;
            ringRenderer.SetPositions(ringPoints);
        }
    }

    static void FillRingPositions(
        Vector3[] points,
        Vector3 center,
        float radius,
        int planeIndex
    )
    {
        int segmentCount = points.Length - 1;

        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = Mathf.PI * 2.0f * i / segmentCount;
            float cosine = Mathf.Cos(angle) * radius;
            float sine = Mathf.Sin(angle) * radius;

            switch (planeIndex)
            {
                case 0:
                    points[i] = center + new Vector3(cosine, sine, 0.0f);
                    break;
                case 1:
                    points[i] = center + new Vector3(cosine, 0.0f, sine);
                    break;
                default:
                    points[i] = center + new Vector3(0.0f, cosine, sine);
                    break;
            }
        }
    }

    void EnsureRingRenderers()
    {
        if (ringRenderers != null && ringRenderers.Length == RingCount)
        {
            return;
        }

        ringRenderers = new LineRenderer[RingCount];

        for (int ringIndex = 0; ringIndex < RingCount; ringIndex++)
        {
            GameObject ringObject = new GameObject("SoI Ring " + ringIndex);
            ringObject.transform.SetParent(transform);
            LineRenderer ringRenderer = ringObject.AddComponent<LineRenderer>();
            OrbitLineVisualUtility.ConfigureOrbitLine(
                ringRenderer,
                SoiSphereColor,
                OrbitLineVisualUtility.SoiSpherePixelWidth
            );
            ringRenderer.enabled = false;
            ringRenderers[ringIndex] = ringRenderer;
        }

        OrbitLineVisualUtility.ApplyOrbitOverlayLayer(gameObject);
    }

    void HideAllRings()
    {
        if (ringRenderers == null)
        {
            return;
        }

        for (int i = 0; i < ringRenderers.Length; i++)
        {
            LineRenderer ringRenderer = ringRenderers[i];

            if (ringRenderer != null)
            {
                ringRenderer.enabled = false;
                ringRenderer.positionCount = 0;
            }
        }
    }

    Vector3 SimToMapWorldPosition(global::SimVector3 simulationPosition)
    {
        global::SimVector3 origin = ResolveLiveMapRenderOrigin();
        float scale = worldScale > 1e-12f ?
            worldScale :
            WorldVisualBridge.MapPositionScale;
        return WorldVisualBridge.SimToUnityMeshFramePosition(
            simulationPosition,
            origin,
            scale
        );
    }

    global::SimVector3 ResolveLiveMapRenderOrigin()
    {
        SimulationRunner runner = GetComponent<SimulationRunner>();

        if (runner != null && runner.IsMapModeActive)
        {
            return runner.GetRenderOrigin();
        }

        if (hasMapRenderOrigin)
        {
            return mapRenderOrigin;
        }

        if (simulation?.planet != null)
        {
            return simulation.planet.position;
        }

        if (simulation?.ship != null)
        {
            return simulation.ship.position;
        }

        return global::SimVector3.Zero();
    }

    public static void ApplyEnabledStateToAll()
    {
        SphereOfInfluenceView[] views = FindObjectsByType<SphereOfInfluenceView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < views.Length; i++)
        {
            SphereOfInfluenceView view = views[i];

            if (view == null)
            {
                continue;
            }

            if (!DevSettings.SoiSphereEnabled)
            {
                view.HideAllRings();
            }
        }
    }
}
