using UnityEngine;

/// <summary>
/// Keeps a LineRenderer at a constant on-screen thickness by converting
/// pixel width to world width each frame (for Unity versions without
/// LineRenderer.widthMode).
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class OrbitLineScreenSpaceWidth : MonoBehaviour
{
    [SerializeField]
    float pixelWidth = 2.5f;

    LineRenderer lineRenderer;

    public float PixelWidth
    {
        get => pixelWidth;
        set => pixelWidth = value;
    }

    void LateUpdate()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null || !lineRenderer.enabled || lineRenderer.positionCount < 2)
        {
            return;
        }

        Camera camera = Camera.main;

        if (camera == null)
        {
            return;
        }

        float distance = GetLineWidthReferenceDistance(camera, lineRenderer);
        float worldWidth = ConvertPixelWidthToWorld(camera, distance, pixelWidth);
        lineRenderer.startWidth = worldWidth;
        lineRenderer.endWidth = worldWidth;
    }

    public static float GetLineWidthReferenceDistance(
        Camera camera,
        LineRenderer lineRenderer
    )
    {
        if (camera == null || lineRenderer == null || lineRenderer.positionCount < 1)
        {
            return 1.0f;
        }

        float nearClip = Mathf.Max(camera.nearClipPlane, 1e-4f);
        Vector3 cameraPosition = camera.transform.position;
        Vector3 cameraForward = camera.transform.forward;
        float pivotDepth = Mathf.Max(
            Vector3.Dot(-cameraPosition, cameraForward),
            nearClip
        );
        float bestDepth = float.PositiveInfinity;
        int count = lineRenderer.positionCount;

        for (int i = 0; i < count; i++)
        {
            Vector3 worldPosition = lineRenderer.useWorldSpace ?
                lineRenderer.GetPosition(i) :
                lineRenderer.transform.TransformPoint(lineRenderer.GetPosition(i));
            float depth = Vector3.Dot(worldPosition - cameraPosition, cameraForward);

            if (depth < nearClip)
            {
                continue;
            }

            if (depth < bestDepth)
            {
                bestDepth = depth;
            }
        }

        if (float.IsPositiveInfinity(bestDepth))
        {
            Vector3 fallbackPosition = GetLineCenterWorld(lineRenderer);
            float fallbackDepth = Vector3.Dot(
                fallbackPosition - cameraPosition,
                cameraForward
            );
            bestDepth = Mathf.Max(Mathf.Abs(fallbackDepth), nearClip);
        }

        if (pivotDepth > nearClip * 2.0f)
        {
            bestDepth = Mathf.Clamp(
                bestDepth,
                pivotDepth * 0.35f,
                pivotDepth * 3.0f
            );
        }

        return bestDepth;
    }

    static Vector3 GetLineCenterWorld(LineRenderer lineRenderer)
    {
        int index = lineRenderer.positionCount == 1 ?
            0 :
            lineRenderer.positionCount / 2;
        Vector3 localPosition = lineRenderer.GetPosition(index);

        return lineRenderer.useWorldSpace ?
            localPosition :
            lineRenderer.transform.TransformPoint(localPosition);
    }

    public static float ConvertPixelWidthToWorld(
        Camera camera,
        float distance,
        float pixelWidth
    )
    {
        float worldHeight;

        if (camera.orthographic)
        {
            worldHeight = 2.0f * camera.orthographicSize;
        }
        else
        {
            float safeDistance = Mathf.Max(distance, camera.nearClipPlane);
            worldHeight =
                2.0f * safeDistance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        }

        float worldPerPixel = worldHeight / Mathf.Max(Screen.height, 1f);
        return Mathf.Max(worldPerPixel * pixelWidth, 1e-6f);
    }
}
