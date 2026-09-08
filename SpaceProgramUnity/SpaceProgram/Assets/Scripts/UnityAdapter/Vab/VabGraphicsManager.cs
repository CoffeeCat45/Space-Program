using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Runtime URP graphics controls for the VAB scene.
/// UnityAdapter only — no SimulationCore dependency.
/// </summary>
public static class VabGraphicsManager
{
    public const int MsaaDisabled = 0;
    public const int Msaa2x = 2;
    public const int Msaa4x = 4;
    public const int Msaa8x = 8;

    public const int PostAaModeNone = 0;
    public const int PostAaModeFxaa = 1;
    public const int PostAaModeSmaa = 2;

    public const int PostAaQualityLow = 0;
    public const int PostAaQualityMedium = 1;
    public const int PostAaQualityHigh = 2;

    public const int DisplayModeWindowed = 0;
    public const int DisplayModeExclusive = 1;
    public const int DisplayModeBorderless = 2;

    public const int WindowedPlayerWidth = 1600;
    public const int WindowedPlayerHeight = 900;

    const string OrbitOverlayCameraObjectName = "Orbit Overlay Camera";

    /// <summary>
    /// Changes MSAA on the active URP asset.
    /// Accepts 0 (Disabled), 2, 4, or 8.
    /// </summary>
    public static void SetMsaaLevel(int index)
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset))
        {
            Debug.LogWarning("VabGraphicsManager: Active render pipeline is not URP.");
            return;
        }

        MsaaQuality sampleCount = index switch
        {
            Msaa2x => MsaaQuality._2x,
            Msaa4x => MsaaQuality._4x,
            Msaa8x => MsaaQuality._8x,
            _ => MsaaQuality.Disabled,
        };

        urpAsset.msaaSampleCount = (int)sampleCount;
    }

    /// <summary>
    /// Configures post anti-aliasing on game cameras. Overlay stack cameras stay without post.
    /// </summary>
    public static void SetPostAntiAliasing(int modeIndex, int qualityIndex)
    {
        Camera[] cameras = FindSceneCameras();
        if (cameras.Length == 0)
        {
            Debug.LogWarning("VabGraphicsManager: No cameras found for post anti-aliasing.");
            return;
        }

        AntialiasingMode mode = modeIndex switch
        {
            PostAaModeFxaa => AntialiasingMode.FastApproximateAntialiasing,
            PostAaModeSmaa => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            _ => AntialiasingMode.None,
        };

        AntialiasingQuality quality = qualityIndex switch
        {
            PostAaQualityHigh => AntialiasingQuality.High,
            PostAaQualityMedium => AntialiasingQuality.Medium,
            _ => AntialiasingQuality.Low,
        };

        for (int i = 0; i < cameras.Length; i++)
        {
            ApplyPostAntiAliasingToCamera(cameras[i], mode, quality);
        }
    }

    static void ApplyPostAntiAliasingToCamera(
        Camera camera,
        AntialiasingMode mode,
        AntialiasingQuality quality
    )
    {
        if (camera == null)
        {
            return;
        }

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        if (cameraData == null)
        {
            return;
        }

        if (!IsPostAntiAliasingTarget(camera))
        {
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderPostProcessing = false;
            return;
        }

        camera.allowMSAA = true;
        cameraData.antialiasing = mode;
        cameraData.renderPostProcessing = mode != AntialiasingMode.None;

        if (mode == AntialiasingMode.None)
        {
            return;
        }

        cameraData.antialiasingQuality = quality;
    }

    public static bool IsPostAntiAliasingTarget(Camera camera)
    {
        if (camera == null || !camera.gameObject.scene.IsValid())
        {
            return false;
        }

        if (camera.name == OrbitOverlayCameraObjectName)
        {
            return false;
        }

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        return cameraData == null || cameraData.renderType != CameraRenderType.Overlay;
    }

    static Camera[] FindSceneCameras()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (cameras.Length == 0)
        {
            return cameras;
        }

        int writeIndex = 0;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.gameObject.scene.IsValid())
            {
                continue;
            }

            cameras[writeIndex] = camera;
            writeIndex++;
        }

        if (writeIndex == cameras.Length)
        {
            return cameras;
        }

        Camera[] trimmed = new Camera[writeIndex];
        for (int i = 0; i < writeIndex; i++)
        {
            trimmed[i] = cameras[i];
        }

        return trimmed;
    }

    static Camera GetPrimaryCamera()
    {
        Camera[] cameras = FindSceneCameras();
        Camera fallback = null;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (!IsPostAntiAliasingTarget(camera))
            {
                continue;
            }

            if (camera.CompareTag("MainCamera"))
            {
                return camera;
            }

            if (fallback == null)
            {
                fallback = camera;
            }
        }

        return fallback;
    }

    public static int GetMsaaLevel()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset))
        {
            return MsaaDisabled;
        }

        return urpAsset.msaaSampleCount switch
        {
            (int)MsaaQuality._2x => Msaa2x,
            (int)MsaaQuality._4x => Msaa4x,
            (int)MsaaQuality._8x => Msaa8x,
            _ => MsaaDisabled,
        };
    }

    public static int GetPostAntiAliasingMode()
    {
        Camera camera = GetPrimaryCamera();
        if (camera == null)
        {
            return PostAaModeNone;
        }

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        if (cameraData == null)
        {
            return PostAaModeNone;
        }

        return cameraData.antialiasing switch
        {
            AntialiasingMode.FastApproximateAntialiasing => PostAaModeFxaa,
            AntialiasingMode.SubpixelMorphologicalAntiAliasing => PostAaModeSmaa,
            _ => PostAaModeNone,
        };
    }

    public static int GetPostAntiAliasingQuality()
    {
        Camera camera = GetPrimaryCamera();
        if (camera == null)
        {
            return PostAaQualityLow;
        }

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        if (cameraData == null || cameraData.antialiasing == AntialiasingMode.None)
        {
            return PostAaQualityLow;
        }

        return cameraData.antialiasingQuality switch
        {
            AntialiasingQuality.High => PostAaQualityHigh,
            AntialiasingQuality.Medium => PostAaQualityMedium,
            _ => PostAaQualityLow,
        };
    }

    public static int ClampDisplayMode(int displayMode)
    {
        if (displayMode == DisplayModeExclusive || displayMode == DisplayModeBorderless)
        {
            return displayMode;
        }

        return DisplayModeWindowed;
    }

    public static FullScreenMode ToUnityFullScreenMode(int displayMode)
    {
        return ClampDisplayMode(displayMode) switch
        {
            DisplayModeExclusive => FullScreenMode.ExclusiveFullScreen,
            DisplayModeBorderless => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed,
        };
    }

    public static int FromUnityFullScreenMode(FullScreenMode mode)
    {
        return mode switch
        {
            FullScreenMode.ExclusiveFullScreen => DisplayModeExclusive,
            FullScreenMode.FullScreenWindow => DisplayModeBorderless,
            FullScreenMode.MaximizedWindow => DisplayModeBorderless,
            _ => DisplayModeWindowed,
        };
    }

    public static void ApplyDisplayMode(int displayMode)
    {
#if UNITY_EDITOR
        ClampDisplayMode(displayMode);
#else
        FullScreenMode mode = ToUnityFullScreenMode(displayMode);
        if (mode == FullScreenMode.Windowed)
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Screen.SetResolution(WindowedPlayerWidth, WindowedPlayerHeight, FullScreenMode.Windowed);
            }

            return;
        }

        Resolution native = Screen.currentResolution;
        int width = native.width > 0 ? native.width : WindowedPlayerWidth;
        int height = native.height > 0 ? native.height : WindowedPlayerHeight;
        Screen.SetResolution(width, height, mode);
#endif
    }

    public static int GetDisplayMode()
    {
        if (TryLoadSnapshot(out GraphicsSettingsSnapshot snapshot))
        {
            return ClampDisplayMode(snapshot.displayMode);
        }

        return ClampDisplayMode(GameSettingsStore.Data.graphics.displayMode);
    }

    public struct GraphicsSettingsSnapshot
    {
        public bool msaaEnabled;
        public int msaaLevel;
        public bool postAaEnabled;
        public int postAaMode;
        public int postAaQuality;
        public int displayMode;
        public int targetFrameRate;
    }

    public static int ClampTargetFrameRate(int framesPerSecond)
    {
        if (framesPerSecond <= 0 || framesPerSecond > 400)
        {
            return 0;
        }

        return framesPerSecond;
    }

    public static void ApplyTargetFrameRate(int framesPerSecond)
    {
        int clamped = ClampTargetFrameRate(framesPerSecond);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = clamped <= 0 ? -1 : clamped;
    }

    public static int GetTargetFrameRate()
    {
        if (TryLoadSnapshot(out GraphicsSettingsSnapshot snapshot))
        {
            return ClampTargetFrameRate(snapshot.targetFrameRate);
        }

        return ClampTargetFrameRate(GameSettingsStore.Data.graphics.targetFrameRate);
    }

    public static void ApplySnapshot(GraphicsSettingsSnapshot snapshot)
    {
        SetMsaaLevel(snapshot.msaaEnabled ? snapshot.msaaLevel : MsaaDisabled);
        SetPostAntiAliasing(
            snapshot.postAaEnabled ? snapshot.postAaMode : PostAaModeNone,
            snapshot.postAaQuality
        );
        ApplyDisplayMode(snapshot.displayMode);
        ApplyTargetFrameRate(snapshot.targetFrameRate);
    }

    public static void ApplySavedCameraQuality()
    {
        if (!TryLoadSnapshot(out GraphicsSettingsSnapshot snapshot))
        {
            return;
        }

        SetMsaaLevel(snapshot.msaaEnabled ? snapshot.msaaLevel : MsaaDisabled);
        SetPostAntiAliasing(
            snapshot.postAaEnabled ? snapshot.postAaMode : PostAaModeNone,
            snapshot.postAaQuality
        );
    }

    public static GraphicsSettingsSnapshot ReadCurrentSnapshot()
    {
        int msaaLevel = GetMsaaLevel();
        int postMode = GetPostAntiAliasingMode();

        return new GraphicsSettingsSnapshot
        {
            msaaEnabled = msaaLevel != MsaaDisabled,
            msaaLevel = msaaLevel == MsaaDisabled ? Msaa4x : msaaLevel,
            postAaEnabled = postMode != PostAaModeNone,
            postAaMode = postMode == PostAaModeNone ? PostAaModeFxaa : postMode,
            postAaQuality = GetPostAntiAliasingQuality(),
            displayMode = GetDisplayMode(),
            targetFrameRate = GetTargetFrameRate(),
        };
    }

    public static void SaveSnapshot(GraphicsSettingsSnapshot snapshot)
    {
        snapshot.displayMode = ClampDisplayMode(snapshot.displayMode);
        snapshot.targetFrameRate = ClampTargetFrameRate(snapshot.targetFrameRate);
        GameSettingsStore.SetGraphicsQuality(snapshot);
    }

    public static bool TryLoadSnapshot(out GraphicsSettingsSnapshot snapshot)
    {
        return GameSettingsStore.TryGetGraphicsQuality(out snapshot);
    }
}
