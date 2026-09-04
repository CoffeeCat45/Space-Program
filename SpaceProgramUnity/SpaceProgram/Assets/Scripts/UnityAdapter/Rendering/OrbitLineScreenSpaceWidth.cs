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

        float distance = Vector3.Distance(
            camera.transform.position,
            GetLineCenterWorld()
        );
        float worldWidth = ConvertPixelWidthToWorld(camera, distance, pixelWidth);
        lineRenderer.startWidth = worldWidth;
        lineRenderer.endWidth = worldWidth;
    }

    Vector3 GetLineCenterWorld()
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
        float safeDistance = Mathf.Max(distance, camera.nearClipPlane);
        float worldHeight =
            2.0f * safeDistance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float worldPerPixel = worldHeight / Mathf.Max(Screen.height, 1f);
        return Mathf.Max(worldPerPixel * pixelWidth, 1e-6f);
    }
}
