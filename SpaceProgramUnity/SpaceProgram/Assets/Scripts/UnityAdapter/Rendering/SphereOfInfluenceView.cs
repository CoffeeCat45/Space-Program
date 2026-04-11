using UnityEngine;

public class SphereOfInfluenceView : MonoBehaviour
{
    public int pointCount = 180;

    Simulation simulation;
    float worldScale;
    LineRenderer lineRenderer;

    internal void SetSimulation(Simulation newSimulation)
    {
        simulation = newSimulation;
    }

    internal void SetWorldScale(float newWorldScale)
    {
        worldScale = newWorldScale;
    }

    void Awake()
    {
        GameObject lineObject = new GameObject("Moon SoI");
        lineObject.transform.SetParent(transform);

        lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(1.0f, 1.0f, 1.0f, 0.35f);
        lineRenderer.endColor = new Color(1.0f, 1.0f, 1.0f, 0.35f);
    }

    void LateUpdate()
    {
        if (simulation == null || lineRenderer == null)
        {
            return;
        }

        BodyState[] bodyStates = simulation.GetBodyStates();
        BodyState moonState = default;
        bool foundMoon = false;

        for (int i = 0; i < bodyStates.Length; i++)
        {
            if (bodyStates[i].name == "Moon")
            {
                moonState = bodyStates[i];
                foundMoon = true;
                break;
            }
        }

        if (!foundMoon)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        float radius = (float)(simulation.moonSphereOfInfluenceRadius * worldScale);
        Vector3 center = new Vector3(
            (float)(moonState.position.x * worldScale),
            (float)(moonState.position.y * worldScale),
            0
        );
        Vector3[] points = new Vector3[pointCount + 1];

        for (int i = 0; i <= pointCount; i++)
        {
            float angle = Mathf.PI * 2.0f * i / pointCount;
            points[i] = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0
            );
        }

        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
    }
}
