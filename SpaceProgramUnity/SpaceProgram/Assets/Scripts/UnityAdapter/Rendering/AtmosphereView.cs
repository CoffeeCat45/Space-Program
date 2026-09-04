using System.Collections.Generic;
using UnityEngine;

public class AtmosphereView : MonoBehaviour
{
    public int pointCount = 180;

    Simulation simulation;
    float worldScale;
    readonly Dictionary<string, LineRenderer> lineRenderers =
        new Dictionary<string, LineRenderer>();

    internal void SetSimulation(Simulation newSimulation)
    {
        simulation = newSimulation;
    }

    internal void SetWorldScale(float newWorldScale)
    {
        worldScale = newWorldScale;
    }

    void LateUpdate()
    {
        if (
            simulation == null ||
            simulation.planet == null ||
            !IsMapScaleActive() ||
            DevSettings.AtmosphereVisualsDisabled
        )
        {
            HideSphere("Planet");
            return;
        }

        AtmosphereProfile profile = simulation.planet.atmosphereProfile;

        if (!profile.HasAtmosphere)
        {
            HideSphere("Planet");
            return;
        }

        double topRadius = profile.GetTopRadius(simulation.planet.radius);
        UpdateSphere(
            "Planet",
            topRadius,
            new Color(0.45f, 0.75f, 1.0f, 0.22f)
        );
    }

    void UpdateSphere(string bodyName, double radius, Color color)
    {
        BodyState bodyState = default;
        bool foundBody = false;
        BodyState[] bodyStates = simulation.GetBodyStates();

        for (int i = 0; i < bodyStates.Length; i++)
        {
            if (bodyStates[i].name == bodyName)
            {
                bodyState = bodyStates[i];
                foundBody = true;
                break;
            }
        }

        if (!foundBody)
        {
            HideSphere(bodyName);
            return;
        }

        LineRenderer lineRenderer = GetOrCreateLineRenderer(bodyName, color);
        float worldRadius = (float)(radius * worldScale);
        global::SimVector3 renderOrigin = GetRenderOrigin();
        global::SimVector3 localCenter = bodyState.position.Subtract(renderOrigin);
        Vector3 center = new Vector3(
            (float)(localCenter.x * worldScale),
            (float)(localCenter.y * worldScale),
            (float)(localCenter.z * worldScale)
        );
        Vector3[] points = new Vector3[pointCount + 1];

        for (int i = 0; i <= pointCount; i++)
        {
            float angle = Mathf.PI * 2.0f * i / pointCount;
            points[i] = center + new Vector3(
                Mathf.Cos(angle) * worldRadius,
                Mathf.Sin(angle) * worldRadius,
                0
            );
        }

        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
    }

    LineRenderer GetOrCreateLineRenderer(string bodyName, Color color)
    {
        if (lineRenderers.TryGetValue(bodyName, out LineRenderer existingRenderer))
        {
            return existingRenderer;
        }

        GameObject lineObject = new GameObject(bodyName + " Atmosphere");
        lineObject.transform.SetParent(transform);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.03f;
        lineRenderer.endWidth = 0.03f;
        lineRenderer.material = RuntimeMaterialFactory.CreateLineMaterial();
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderers[bodyName] = lineRenderer;
        return lineRenderer;
    }

    void HideSphere(string bodyName)
    {
        if (
            lineRenderers.TryGetValue(bodyName, out LineRenderer lineRenderer) &&
            lineRenderer != null
        )
        {
            lineRenderer.positionCount = 0;
        }
    }

    bool IsMapScaleActive()
    {
        return Mathf.Abs(worldScale - WorldVisualBridge.MapPositionScale) < 1e-5f;
    }

    global::SimVector3 GetRenderOrigin()
    {
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
}
