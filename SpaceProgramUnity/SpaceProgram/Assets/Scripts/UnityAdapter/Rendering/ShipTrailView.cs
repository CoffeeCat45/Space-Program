using System.Collections.Generic;
using UnityEngine;

public class ShipTrailView : MonoBehaviour
{
    public int maxPointCount = 600;
    public float minPointDistance = 0.05f;

    LineRenderer lineRenderer;
    List<Vector3> points = new List<Vector3>();

    void Awake()
    {
        GameObject trailObject = new GameObject("Trail");
        trailObject.transform.SetParent(transform);

        lineRenderer = trailObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.025f;
        lineRenderer.endWidth = 0.025f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(0.2f, 0.8f, 1.0f, 0.75f);
        lineRenderer.endColor = new Color(0.2f, 0.8f, 1.0f, 0.75f);
    }

    public void AddPoint(Vector3 position)
    {
        if (
            points.Count > 0 &&
            Vector3.Distance(points[points.Count - 1], position) < minPointDistance
        )
        {
            return;
        }

        points.Add(position);

        while (points.Count > maxPointCount)
        {
            points.RemoveAt(0);
        }

        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
    }
}
