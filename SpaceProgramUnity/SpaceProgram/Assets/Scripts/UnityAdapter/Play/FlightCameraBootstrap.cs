using UnityEngine;

/// <summary>
/// Flight uses exactly one camera owned by <see cref="SimulationRunner"/> (FlightCamera).
/// VAB / menu / sandbox scene cameras are deactivated so they never compete with Camera.main.
/// </summary>
static class FlightCameraBootstrap
{
    const string FlightCameraName = "FlightCamera";

    public static Camera EnsureMainCamera(Transform sessionRoot)
    {
        Camera primary = ResolvePrimaryFlightCamera(sessionRoot);

        if (primary == null)
        {
            primary = CreateFlightCamera(sessionRoot);
        }

        PromoteToMainCamera(primary);
        ConsolidateDuplicateCameras(primary, sessionRoot);
        PreparePrimaryForFlight(primary);
        VabGraphicsManager.ApplySavedCameraQuality();
        return primary;
    }

    /// <summary>
    /// Menu overlay needs a camera even when the hangar is hidden and the player
    /// never entered flight (no FlightCamera yet). Owns a session camera and
    /// demotes VAB / leftover scene cameras so Display 1 is never empty.
    /// </summary>
    public static Camera EnsureMenuCamera(Transform sessionRoot)
    {
        Camera primary = ResolvePrimaryFlightCamera(sessionRoot);

        if (primary == null)
        {
            primary = CreateFlightCamera(sessionRoot);
        }

        PromoteToMainCamera(primary);
        ConsolidateDuplicateCameras(primary, sessionRoot);
        PreparePrimaryForMenu(primary);
        VabGraphicsManager.ApplySavedCameraQuality();
        return primary;
    }

    /// <summary>Re-enable the hangar scene camera when returning to the VAB editor.</summary>
    public static void RestoreVabEnvironmentCamera(Transform sessionRoot)
    {
        DeactivateSessionFlightCamera(sessionRoot);

        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera == null || !IsVabEnvironmentCamera(camera))
            {
                continue;
            }

            if (!camera.gameObject.activeSelf)
            {
                camera.gameObject.SetActive(true);
            }

            camera.enabled = true;
            camera.tag = "MainCamera";

            VabCameraController vabController = camera.GetComponent<VabCameraController>();

            if (vabController != null)
            {
                vabController.enabled = true;
            }

            CameraController flightController = camera.GetComponent<CameraController>();

            if (flightController != null)
            {
                flightController.enabled = false;
            }

            VabGraphicsManager.ApplySavedCameraQuality();
            return;
        }
    }

    static Camera ResolvePrimaryFlightCamera(Transform sessionRoot)
    {
        if (sessionRoot == null)
        {
            return null;
        }

        Camera namedFlightCamera = FindNamedSessionFlightCamera(sessionRoot);

        if (namedFlightCamera != null)
        {
            return namedFlightCamera;
        }

        CameraController sessionController =
            sessionRoot.GetComponentInChildren<CameraController>(true);

        if (sessionController != null)
        {
            Camera controllerCamera = sessionController.GetComponent<Camera>();

            if (controllerCamera != null && IsSessionOwnedCamera(controllerCamera, sessionRoot))
            {
                return controllerCamera;
            }
        }

        return FindSessionCamera(sessionRoot);
    }

    static Camera FindNamedSessionFlightCamera(Transform sessionRoot)
    {
        if (sessionRoot == null)
        {
            return null;
        }

        Transform existing = sessionRoot.Find(FlightCameraName);

        if (existing == null)
        {
            return null;
        }

        Camera camera = existing.GetComponent<Camera>();

        return camera != null && IsUsableFlightCamera(camera) ? camera : null;
    }

    static bool IsSessionOwnedCamera(Camera camera, Transform sessionRoot)
    {
        if (camera == null || sessionRoot == null)
        {
            return false;
        }

        Transform cameraTransform = camera.transform;

        return
            cameraTransform == sessionRoot ||
            cameraTransform.IsChildOf(sessionRoot);
    }

    static bool IsForeignFlightCamera(Camera camera, Transform sessionRoot)
    {
        if (camera == null)
        {
            return true;
        }

        if (IsSessionOwnedCamera(camera, sessionRoot))
        {
            return false;
        }

        if (IsVabEnvironmentCamera(camera))
        {
            return true;
        }

        if (IsEarthSandboxCamera(camera))
        {
            return true;
        }

        return !camera.gameObject.scene.IsValid() ||
            camera.gameObject.scene.buildIndex < 0;
    }

    static bool IsVabEnvironmentCamera(Camera camera)
    {
        if (camera == null || !camera.gameObject.scene.IsValid())
        {
            return false;
        }

        return VabSessionSceneUtility.MatchesVabEnvironmentSceneName(
            camera.gameObject.scene.name
        );
    }

    static bool IsEarthSandboxCamera(Camera camera)
    {
        if (camera == null || !camera.gameObject.scene.IsValid())
        {
            return false;
        }

        string sceneName = camera.gameObject.scene.name;

        return sceneName.IndexOf("Sandbox", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            sceneName.IndexOf("Earth_Sandbox", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsUsableFlightCamera(Camera camera)
    {
        if (camera == null)
        {
            return false;
        }

        string objectName = camera.gameObject.name;

        if (
            objectName.IndexOf("Thumbnail", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Preview", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Overlay", System.StringComparison.OrdinalIgnoreCase) >= 0
        )
        {
            return false;
        }

        return true;
    }

    static Camera CreateFlightCamera(Transform sessionRoot)
    {
        Transform parent = sessionRoot != null ? sessionRoot : null;
        GameObject cameraObject = new GameObject(FlightCameraName);
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.localPosition = new Vector3(0f, 4f, -12f);
        cameraObject.transform.localRotation = Quaternion.identity;

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = true;
        camera.orthographic = false;
        camera.fieldOfView = 55f;
        camera.nearClipPlane = WorldVisualBridge.FlightCameraNearClip;
        camera.farClipPlane = WorldVisualBridge.FlightCameraFarClip;
        camera.clearFlags = CameraClearFlags.Skybox;
        return camera;
    }

    static Camera FindSessionCamera(Transform sessionRoot)
    {
        if (sessionRoot == null)
        {
            return null;
        }

        Camera[] cameras = sessionRoot.GetComponentsInChildren<Camera>(true);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera != null && IsUsableFlightCamera(camera))
            {
                return camera;
            }
        }

        return null;
    }

    static void ConsolidateDuplicateCameras(Camera primary, Transform sessionRoot)
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera == null || camera == primary || !IsUsableFlightCamera(camera))
            {
                continue;
            }

            if (IsChildCameraOf(primary, camera))
            {
                continue;
            }

            DemoteDuplicateCamera(camera, sessionRoot);
        }
    }

    static bool IsChildCameraOf(Camera primary, Camera candidate)
    {
        if (primary == null || candidate == null)
        {
            return false;
        }

        Transform candidateTransform = candidate.transform;
        Transform primaryTransform = primary.transform;

        return
            candidateTransform == primaryTransform ||
            candidateTransform.IsChildOf(primaryTransform);
    }

    static void DemoteDuplicateCamera(Camera camera, Transform sessionRoot)
    {
        if (camera.CompareTag("MainCamera"))
        {
            camera.tag = "Untagged";
        }

        VabCameraController vabController = camera.GetComponent<VabCameraController>();

        if (vabController != null)
        {
            vabController.enabled = false;
        }

        CameraController flightController = camera.GetComponent<CameraController>();

        if (flightController != null)
        {
            flightController.enabled = false;
        }

        camera.enabled = false;

        if (IsForeignFlightCamera(camera, sessionRoot))
        {
            camera.gameObject.SetActive(false);
        }
    }

    static void DeactivateSessionFlightCamera(Transform sessionRoot)
    {
        Camera sessionCamera = FindNamedSessionFlightCamera(sessionRoot);

        if (sessionCamera == null)
        {
            sessionCamera = FindSessionCamera(sessionRoot);
        }

        if (sessionCamera == null)
        {
            return;
        }

        if (sessionCamera.CompareTag("MainCamera"))
        {
            sessionCamera.tag = "Untagged";
        }

        sessionCamera.enabled = false;
        sessionCamera.gameObject.SetActive(false);
    }

    static void PreparePrimaryForFlight(Camera primary)
    {
        DisableVabCameraController(primary);

        CameraController flightController = primary.GetComponent<CameraController>();

        if (flightController != null)
        {
            flightController.enabled = true;
        }
    }

    static void PreparePrimaryForMenu(Camera primary)
    {
        if (primary == null)
        {
            return;
        }

        DisableVabCameraController(primary);

        CameraController flightController = primary.GetComponent<CameraController>();

        if (flightController != null)
        {
            flightController.enabled = false;
        }

        primary.clearFlags = CameraClearFlags.SolidColor;
        primary.backgroundColor = new Color(0.02f, 0.03f, 0.08f, 1.0f);
    }

    static void DisableVabCameraController(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        VabCameraController vabController = camera.GetComponent<VabCameraController>();

        if (vabController != null)
        {
            vabController.enabled = false;
        }
    }

    static void PromoteToMainCamera(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        if (!camera.gameObject.activeSelf)
        {
            camera.gameObject.SetActive(true);
        }

        camera.enabled = true;
        camera.tag = "MainCamera";
    }
}
