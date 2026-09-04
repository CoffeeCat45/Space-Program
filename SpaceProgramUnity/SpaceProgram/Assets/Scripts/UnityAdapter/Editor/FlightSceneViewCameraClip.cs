#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view uses dynamic clip from the planet AABB (~1270 km). That pushes the near
/// plane hundreds of meters out, so the ground under the Scene camera is clipped and
/// looks transparent. Game view is fine because the flight camera uses a 1 m near plane.
/// Match Scene clip to the flight camera while a planet is in Play mode.
/// </summary>
[InitializeOnLoad]
static class FlightSceneViewCameraClip
{
    const float RecheckIntervalSeconds = 0.5f;
    // Same values as WorldVisualBridge.FlightCameraNearClip / FarClip (internal to runtime).
    const float NearClipMeters = 1.0f;
    const float FarClipMeters = 8_000_000f;

    static double nextCheckTime;
    static bool flightPlanetActive;

    static FlightSceneViewCameraClip()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    static void OnSceneGui(SceneView sceneView)
    {
        if (!Application.isPlaying || sceneView == null)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now >= nextCheckTime)
        {
            nextCheckTime = now + RecheckIntervalSeconds;
            flightPlanetActive = Object.FindFirstObjectByType<FlightPlanetSphere>() != null;
        }

        if (!flightPlanetActive)
        {
            return;
        }

        sceneView.cameraSettings.dynamicClip = false;
        sceneView.cameraSettings.nearClip = NearClipMeters;
        sceneView.cameraSettings.farClip = FarClipMeters;

        Camera camera = sceneView.camera;
        if (camera == null)
        {
            return;
        }

        camera.nearClipPlane = NearClipMeters;
        camera.farClipPlane = FarClipMeters;
    }
}
#endif
