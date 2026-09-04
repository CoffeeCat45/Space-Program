using System.Collections.Generic;
using UnityEngine;

public class ShipTrailView : MonoBehaviour
{
    public int maxPointCount = 600;
    public float minPointDistance = 0.05f;
    public float lineWidth = 0.065f;

    LineRenderer lineRenderer;
    List<Vector3> points = new List<Vector3>();
    bool trailVisible;

    void Awake()
    {
        GameObject trailObject = new GameObject("Trail");
        trailObject.transform.SetParent(transform);

        lineRenderer = trailObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        ApplyLineWidth();
        lineRenderer.material = RuntimeMaterialFactory.CreateLineMaterial();
        lineRenderer.startColor = new Color(0.45f, 0.72f, 1.0f, 0.88f);
        lineRenderer.endColor = new Color(0.45f, 0.72f, 1.0f, 0.88f);
        trailVisible = DevSettings.ShipTrailEnabled;
        lineRenderer.enabled = trailVisible;
    }

    public void SetTrailVisible(bool visible)
    {
        trailVisible = visible;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = visible;
        }

        if (!visible)
        {
            Clear();
        }
    }

    public void ApplyLineWidth()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
    }

    public void AddPoint(Vector3 position)
    {
        if (!trailVisible || !DevSettings.ShipTrailEnabled)
        {
            return;
        }

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

    public void Clear()
    {
        points.Clear();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
    }

    public static void ApplyEnabledStateToAll()
    {
        ShipTrailView[] trailViews = FindObjectsByType<ShipTrailView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < trailViews.Length; i++)
        {
            ShipTrailView trailView = trailViews[i];

            if (trailView == null)
            {
                continue;
            }

            trailView.SetTrailVisible(DevSettings.ShipTrailEnabled);
        }
    }
}
