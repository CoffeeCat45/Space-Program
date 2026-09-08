using NUnit.Framework;
using UnityEngine;

public sealed class OrbitLineScreenSpaceWidthTests
{
    [Test]
    public void PerspectiveWidth_GrowsWithDistance()
    {
        GameObject cameraObject = new GameObject("WidthCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = false;
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.3f;

        try
        {
            float nearWidth = OrbitLineScreenSpaceWidth.ConvertPixelWidthToWorld(
                camera,
                1000f,
                2f
            );
            float farWidth = OrbitLineScreenSpaceWidth.ConvertPixelWidthToWorld(
                camera,
                1e9f,
                2f
            );
            Assert.Greater(farWidth, nearWidth * 1000f);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void LineWidthReference_UsesClosestInFrontVertex_NotMidpoint()
    {
        GameObject cameraObject = new GameObject("WidthCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        camera.nearClipPlane = 0.3f;

        GameObject lineObject = new GameObject("OrbitLine");
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, new Vector3(0f, 0f, 50f));
        lineRenderer.SetPosition(1, new Vector3(0f, 0f, 5_000_000f));
        lineRenderer.SetPosition(2, new Vector3(0f, 0f, 9_000_000f));

        try
        {
            float distance = OrbitLineScreenSpaceWidth.GetLineWidthReferenceDistance(
                camera,
                lineRenderer
            );
            Assert.AreEqual(50f, distance, 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lineObject);
        }
    }

    [Test]
    public void LineWidthReference_CapsFarVerticesToCameraPivot()
    {
        GameObject cameraObject = new GameObject("WidthCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(
            new Vector3(0f, 0f, -10_000f),
            Quaternion.identity
        );
        camera.nearClipPlane = 0.3f;

        GameObject lineObject = new GameObject("OrbitLine");
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, new Vector3(0f, 0f, 1e9f));
        lineRenderer.SetPosition(1, new Vector3(0f, 0f, 2e9f));
        lineRenderer.SetPosition(2, new Vector3(0f, 0f, 3e9f));

        try
        {
            float distance = OrbitLineScreenSpaceWidth.GetLineWidthReferenceDistance(
                camera,
                lineRenderer
            );
            Assert.LessOrEqual(distance, 30_000f + 1f);
            Assert.Greater(distance, 1_000f);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lineObject);
        }
    }
}
