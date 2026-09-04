using System;
using UnityEngine;

/// <summary>
/// Persistent planet terrain graphics tuning (PlayerPrefs). Applied to <see cref="FlightPlanetSphere"/>.
/// </summary>
public static class PlanetLodSettings
{
    public readonly struct ResolvedRuntime
    {
        public readonly int MaxLodSplitsPerFrame;
        public readonly int MaxMeshBuildsPerFrame;
        public readonly float LodUpdateIntervalSeconds;
        public readonly float SplitDistanceMeters;
        public readonly float MinChunkSpanMeters;
        public readonly float LaunchSiteMaxChunkSpanMeters;
        public readonly float SplitTargetSpanFactor;
        public readonly int MaxActiveChunks;

        public ResolvedRuntime(
            int maxLodSplitsPerFrame,
            int maxMeshBuildsPerFrame,
            float lodUpdateIntervalSeconds,
            float splitDistanceMeters,
            float minChunkSpanMeters,
            float launchSiteMaxChunkSpanMeters,
            float splitTargetSpanFactor,
            int maxActiveChunks
        )
        {
            MaxLodSplitsPerFrame = maxLodSplitsPerFrame;
            MaxMeshBuildsPerFrame = maxMeshBuildsPerFrame;
            LodUpdateIntervalSeconds = lodUpdateIntervalSeconds;
            SplitDistanceMeters = splitDistanceMeters;
            MinChunkSpanMeters = minChunkSpanMeters;
            LaunchSiteMaxChunkSpanMeters = launchSiteMaxChunkSpanMeters;
            SplitTargetSpanFactor = splitTargetSpanFactor;
            MaxActiveChunks = maxActiveChunks;
        }
    }

    const string LodDistanceKey = "SpaceProgram.PlanetTerrain.LodDistanceFactor";
    const string LoadSpeedKey = "SpaceProgram.PlanetTerrain.LoadSpeedPerFrame";
    const string MaxLodKey = "SpaceProgram.PlanetTerrain.MaxLod";
    const string MaxLodFloorMigratedKey = "SpaceProgram.PlanetTerrain.MaxLodFloorV2";
    const string ViewConeKey = "SpaceProgram.PlanetTerrain.ViewConeHalfAngle";
    const string CloudDetailKey = "SpaceProgram.PlanetTerrain.CloudDetail";
    const string CloudCoverageKey = "SpaceProgram.PlanetTerrain.CloudCoverage";
    const string CloudFrequencyKey = "SpaceProgram.PlanetTerrain.CloudFrequency";
    const string CloudClusterKey = "SpaceProgram.PlanetTerrain.CloudCluster";
    const string CloudRendererKey = "SpaceProgram.PlanetTerrain.CloudRenderer";
    const string ExpCloudCoverageKey = "SpaceProgram.PlanetTerrain.ExpCloudCoverage";
    const string ExpCloudFlowKey = "SpaceProgram.PlanetTerrain.ExpCloudFlow";
    const string ExpCloudVortexKey = "SpaceProgram.PlanetTerrain.ExpCloudVortex";
    const string ExpCloudGrainKey = "SpaceProgram.PlanetTerrain.ExpCloudGrain";
    const string ExpCloudMassScaleKey = "SpaceProgram.PlanetTerrain.ExpCloudMassScale";
    const string ExpCloudSoftnessKey = "SpaceProgram.PlanetTerrain.ExpCloudSoftness";

    public static float LodDistanceFactor { get; private set; } =
        FlightPlanetTerrainTuning.LodDistanceFactorDefault;

    public static int LoadSpeedPerFrame { get; private set; } =
        FlightPlanetSphere.DefaultMaxLodSplitsPerFrame;

    public static int MaxLod { get; private set; } = FlightPlanetSphere.DefaultMaxLodLevel;

    public static float ViewConeHalfAngleDegrees { get; private set; } =
        PlanetPqsAsyncLodSettings.DefaultViewConeHalfAngleDegrees;

    public static float CloudDetail { get; private set; } =
        FlightPlanetTerrainTuning.CloudDetailDefault;

    public static float CloudCoverage { get; private set; } =
        FlightPlanetTerrainTuning.CloudCoverageDefault;

    public static float CloudFrequency { get; private set; } =
        FlightPlanetTerrainTuning.CloudFrequencyDefault;

    public static float CloudCluster { get; private set; } =
        FlightPlanetTerrainTuning.CloudClusterDefault;

    public static CloudRendererMode CloudRenderer { get; private set; } =
        CloudRendererMode.SimpleShells;

    public static bool UsesSimpleCloudLook =>
        CloudRenderer == CloudRendererMode.SimpleShells;

    public static bool UsesExperimentalCloudLook =>
        CloudRenderer == CloudRendererMode.Experimental;

    public static bool UsesNasaCloudLook =>
        CloudRenderer == CloudRendererMode.NasaPng
        || CloudRenderer == CloudRendererMode.NasaPngEnhanced;

    public static bool UsesNasaEnhancedCloudLook =>
        CloudRenderer == CloudRendererMode.NasaPngEnhanced;

    public static bool UsesNasaStaticCloudLook =>
        CloudRenderer == CloudRendererMode.NasaPng;

    public static float ExpCloudCoverage { get; private set; } =
        FlightPlanetTerrainTuning.ExpCloudCoverageDefault;

    public static float ExpCloudFlow { get; private set; } =
        FlightPlanetTerrainTuning.ExpCloudFlowDefault;

    public static float ExpCloudVortex { get; private set; } =
        FlightPlanetTerrainTuning.ExpCloudVortexDefault;

    public static float ExpCloudGrain { get; private set; } =
        FlightPlanetTerrainTuning.ExpCloudGrainDefault;

    public static float ExpCloudMassScale { get; private set; } =
        FlightPlanetTerrainTuning.ExpCloudMassScaleDefault;

    public static float ExpCloudSoftness { get; private set; } =
        FlightPlanetTerrainTuning.ExpCloudSoftnessDefault;

    public static event Action Changed;

    public static void Load()
    {
        LodDistanceFactor = Mathf.Clamp(
            PlayerPrefs.GetFloat(LodDistanceKey, FlightPlanetTerrainTuning.LodDistanceFactorDefault),
            FlightPlanetTerrainTuning.LodDistanceFactorMin,
            FlightPlanetTerrainTuning.LodDistanceFactorMax
        );
        LoadSpeedPerFrame = Mathf.Clamp(
            PlayerPrefs.GetInt(LoadSpeedKey, FlightPlanetSphere.DefaultMaxLodSplitsPerFrame),
            FlightPlanetTerrainTuning.LoadSpeedMin,
            FlightPlanetTerrainTuning.LoadSpeedMax
        );
        MaxLod = Mathf.Clamp(
            PlayerPrefs.GetInt(MaxLodKey, FlightPlanetSphere.DefaultMaxLodLevel),
            FlightPlanetTerrainTuning.MaxLodMin,
            FlightPlanetSphere.DefaultMaxLodLevel
        );
        // V2 forced MaxLod 15 for exe users stuck at 7. L15 on the pad floods
        // split/mesh work (FPS) and pops GEBCO heights under the ship (Y hitch).
        if (PlayerPrefs.GetInt(MaxLodFloorMigratedKey, 0) == 1)
        {
            if (MaxLod == FlightPlanetSphere.DefaultMaxLodLevel)
            {
                MaxLod = 8;
                PlayerPrefs.SetInt(MaxLodKey, MaxLod);
            }

            PlayerPrefs.SetInt(MaxLodFloorMigratedKey, 2);
            PlayerPrefs.Save();
        }
        ViewConeHalfAngleDegrees = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                ViewConeKey,
                PlanetPqsAsyncLodSettings.DefaultViewConeHalfAngleDegrees
            ),
            FlightPlanetTerrainTuning.ViewConeHalfAngleMin,
            FlightPlanetTerrainTuning.ViewConeHalfAngleMax
        );
        CloudDetail = Mathf.Clamp(
            PlayerPrefs.GetFloat(CloudDetailKey, FlightPlanetTerrainTuning.CloudDetailDefault),
            FlightPlanetTerrainTuning.CloudDetailMin,
            FlightPlanetTerrainTuning.CloudDetailMax
        );
        CloudCoverage = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                CloudCoverageKey,
                FlightPlanetTerrainTuning.CloudCoverageDefault
            ),
            FlightPlanetTerrainTuning.CloudCoverageMin,
            FlightPlanetTerrainTuning.CloudCoverageMax
        );
        CloudFrequency = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                CloudFrequencyKey,
                FlightPlanetTerrainTuning.CloudFrequencyDefault
            ),
            FlightPlanetTerrainTuning.CloudFrequencyMin,
            FlightPlanetTerrainTuning.CloudFrequencyMax
        );
        CloudCluster = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                CloudClusterKey,
                FlightPlanetTerrainTuning.CloudClusterDefault
            ),
            FlightPlanetTerrainTuning.CloudClusterMin,
            FlightPlanetTerrainTuning.CloudClusterMax
        );
        CloudRenderer = ClampCloudRenderer(
            PlayerPrefs.GetInt(CloudRendererKey, (int)CloudRendererMode.SimpleShells)
        );
        ExpCloudCoverage = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                ExpCloudCoverageKey,
                FlightPlanetTerrainTuning.ExpCloudCoverageDefault
            ),
            FlightPlanetTerrainTuning.ExpCloudCoverageMin,
            FlightPlanetTerrainTuning.ExpCloudCoverageMax
        );
        ExpCloudFlow = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                ExpCloudFlowKey,
                FlightPlanetTerrainTuning.ExpCloudFlowDefault
            ),
            FlightPlanetTerrainTuning.ExpCloudFlowMin,
            FlightPlanetTerrainTuning.ExpCloudFlowMax
        );
        ExpCloudVortex = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                ExpCloudVortexKey,
                FlightPlanetTerrainTuning.ExpCloudVortexDefault
            ),
            FlightPlanetTerrainTuning.ExpCloudVortexMin,
            FlightPlanetTerrainTuning.ExpCloudVortexMax
        );
        ExpCloudGrain = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                ExpCloudGrainKey,
                FlightPlanetTerrainTuning.ExpCloudGrainDefault
            ),
            FlightPlanetTerrainTuning.ExpCloudGrainMin,
            FlightPlanetTerrainTuning.ExpCloudGrainMax
        );
        ExpCloudMassScale = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                ExpCloudMassScaleKey,
                FlightPlanetTerrainTuning.ExpCloudMassScaleDefault
            ),
            FlightPlanetTerrainTuning.ExpCloudMassScaleMin,
            FlightPlanetTerrainTuning.ExpCloudMassScaleMax
        );
        ExpCloudSoftness = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                ExpCloudSoftnessKey,
                FlightPlanetTerrainTuning.ExpCloudSoftnessDefault
            ),
            FlightPlanetTerrainTuning.ExpCloudSoftnessMin,
            FlightPlanetTerrainTuning.ExpCloudSoftnessMax
        );
    }

    public static void SetLodDistanceFactor(float value)
    {
        LodDistanceFactor = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.LodDistanceFactorMin,
            FlightPlanetTerrainTuning.LodDistanceFactorMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetLoadSpeedPerFrame(int value)
    {
        LoadSpeedPerFrame = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.LoadSpeedMin,
            FlightPlanetTerrainTuning.LoadSpeedMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetMaxLod(int value)
    {
        MaxLod = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.MaxLodMin,
            FlightPlanetSphere.DefaultMaxLodLevel
        );
        Save();
        NotifyChanged();
    }

    public static void SetViewConeHalfAngleDegrees(float value)
    {
        ViewConeHalfAngleDegrees = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.ViewConeHalfAngleMin,
            FlightPlanetTerrainTuning.ViewConeHalfAngleMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetCloudDetail(float value)
    {
        CloudDetail = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.CloudDetailMin,
            FlightPlanetTerrainTuning.CloudDetailMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetCloudCoverage(float value)
    {
        CloudCoverage = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.CloudCoverageMin,
            FlightPlanetTerrainTuning.CloudCoverageMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetCloudFrequency(float value)
    {
        CloudFrequency = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.CloudFrequencyMin,
            FlightPlanetTerrainTuning.CloudFrequencyMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetCloudCluster(float value)
    {
        CloudCluster = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.CloudClusterMin,
            FlightPlanetTerrainTuning.CloudClusterMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetCloudRenderer(CloudRendererMode mode)
    {
        CloudRenderer = ClampCloudRenderer((int)mode);
        Save();
        NotifyChanged();
    }

    public static void SetExpCloudCoverage(float value)
    {
        ExpCloudCoverage = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.ExpCloudCoverageMin,
            FlightPlanetTerrainTuning.ExpCloudCoverageMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetExpCloudFlow(float value)
    {
        ExpCloudFlow = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.ExpCloudFlowMin,
            FlightPlanetTerrainTuning.ExpCloudFlowMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetExpCloudVortex(float value)
    {
        ExpCloudVortex = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.ExpCloudVortexMin,
            FlightPlanetTerrainTuning.ExpCloudVortexMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetExpCloudGrain(float value)
    {
        ExpCloudGrain = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.ExpCloudGrainMin,
            FlightPlanetTerrainTuning.ExpCloudGrainMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetExpCloudMassScale(float value)
    {
        ExpCloudMassScale = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.ExpCloudMassScaleMin,
            FlightPlanetTerrainTuning.ExpCloudMassScaleMax
        );
        Save();
        NotifyChanged();
    }

    public static void SetExpCloudSoftness(float value)
    {
        ExpCloudSoftness = Mathf.Clamp(
            value,
            FlightPlanetTerrainTuning.ExpCloudSoftnessMin,
            FlightPlanetTerrainTuning.ExpCloudSoftnessMax
        );
        Save();
        NotifyChanged();
    }

    static CloudRendererMode ClampCloudRenderer(int value)
    {
        if (value == (int)CloudRendererMode.Experimental)
        {
            return CloudRendererMode.Experimental;
        }

        if (value == (int)CloudRendererMode.NasaPng)
        {
            return CloudRendererMode.NasaPng;
        }

        if (value == (int)CloudRendererMode.NasaPngEnhanced)
        {
            return CloudRendererMode.NasaPngEnhanced;
        }

        return CloudRendererMode.SimpleShells;
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(LodDistanceKey, LodDistanceFactor);
        PlayerPrefs.SetInt(LoadSpeedKey, LoadSpeedPerFrame);
        PlayerPrefs.SetInt(MaxLodKey, MaxLod);
        PlayerPrefs.SetFloat(ViewConeKey, ViewConeHalfAngleDegrees);
        PlayerPrefs.SetFloat(CloudDetailKey, CloudDetail);
        PlayerPrefs.SetFloat(CloudCoverageKey, CloudCoverage);
        PlayerPrefs.SetFloat(CloudFrequencyKey, CloudFrequency);
        PlayerPrefs.SetFloat(CloudClusterKey, CloudCluster);
        PlayerPrefs.SetInt(CloudRendererKey, (int)CloudRenderer);
        PlayerPrefs.SetFloat(ExpCloudCoverageKey, ExpCloudCoverage);
        PlayerPrefs.SetFloat(ExpCloudFlowKey, ExpCloudFlow);
        PlayerPrefs.SetFloat(ExpCloudVortexKey, ExpCloudVortex);
        PlayerPrefs.SetFloat(ExpCloudGrainKey, ExpCloudGrain);
        PlayerPrefs.SetFloat(ExpCloudMassScaleKey, ExpCloudMassScale);
        PlayerPrefs.SetFloat(ExpCloudSoftnessKey, ExpCloudSoftness);
        PlayerPrefs.Save();
    }

    public static ResolvedRuntime ResolveBaselineRuntime()
    {
        return new ResolvedRuntime(
            LoadSpeedPerFrame,
            LoadSpeedPerFrame,
            FlightPlanetSphere.DefaultLodUpdateIntervalSeconds,
            10000f,
            9f,
            24f,
            FlightPlanetTerrainTuning.SplitSpanFactorPerLodDistanceUnit * LodDistanceFactor,
            FlightPlanetSphere.DefaultMaxActiveChunks
        );
    }

    static void NotifyChanged()
    {
        Changed?.Invoke();
        FlightPlanetSphere.ApplyRuntimeLodSettings();
        FlightPlanetSphere.ApplyRuntimeCloudSettings();
    }
}
