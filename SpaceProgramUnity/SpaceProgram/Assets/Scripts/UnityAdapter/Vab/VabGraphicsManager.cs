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
    /// Configures post anti-aliasing on all active cameras.
    /// </summary>
    public static void SetPostAntiAliasing(int modeIndex, int qualityIndex)
    {
        Camera[] cameras = FindTargetCameras();
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

        camera.allowMSAA = true;
        cameraData.antialiasing = mode;
        cameraData.renderPostProcessing = mode != AntialiasingMode.None;

        if (mode == AntialiasingMode.None)
        {
            return;
        }

        cameraData.antialiasingQuality = quality;
    }

    static Camera[] FindTargetCameras()
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
        Camera[] cameras = FindTargetCameras();
        if (cameras.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].CompareTag("MainCamera"))
            {
                return cameras[i];
            }
        }

        return cameras[0];
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

    public struct GraphicsSettingsSnapshot
    {
        public bool msaaEnabled;
        public int msaaLevel;
        public bool postAaEnabled;
        public int postAaMode;
        public int postAaQuality;
    }

    public static void ApplySnapshot(GraphicsSettingsSnapshot snapshot)
    {
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
        };
    }

    public static void SaveSnapshot(GraphicsSettingsSnapshot snapshot)
    {
        GameSettingsStore.SetGraphicsQuality(snapshot);
    }

    public static bool TryLoadSnapshot(out GraphicsSettingsSnapshot snapshot)
    {
        return GameSettingsStore.TryGetGraphicsQuality(out snapshot);
    }
}
