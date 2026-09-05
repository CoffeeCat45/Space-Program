using System;
using UnityEngine;

/// <summary>
/// Persistent planet terrain graphics tuning (<c>settings.json</c>). Applied to <see cref="FlightPlanetSphere"/>.
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

    public static event Action Changed;

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

    public static void Load()
    {
        GameSettingsStore.EnsureLoaded();
        GameSettingsGraphics graphics = GameSettingsStore.Data.graphics;
        LodDistanceFactor = Mathf.Clamp(
            graphics.lodDistanceFactor,
            FlightPlanetTerrainTuning.LodDistanceFactorMin,
            FlightPlanetTerrainTuning.LodDistanceFactorMax
        );
        LoadSpeedPerFrame = Mathf.Clamp(
            graphics.loadSpeedPerFrame,
            FlightPlanetTerrainTuning.LoadSpeedMin,
            FlightPlanetTerrainTuning.LoadSpeedMax
        );
        MaxLod = Mathf.Clamp(
            graphics.maxLod,
            FlightPlanetTerrainTuning.MaxLodMin,
            FlightPlanetSphere.DefaultMaxLodLevel
        );
        ViewConeHalfAngleDegrees = Mathf.Clamp(
            graphics.viewConeHalfAngleDegrees,
            FlightPlanetTerrainTuning.ViewConeHalfAngleMin,
            FlightPlanetTerrainTuning.ViewConeHalfAngleMax
        );
        CloudDetail = Mathf.Clamp(
            graphics.cloudDetail,
            FlightPlanetTerrainTuning.CloudDetailMin,
            FlightPlanetTerrainTuning.CloudDetailMax
        );
        CloudCoverage = Mathf.Clamp(
            graphics.cloudCoverage,
            FlightPlanetTerrainTuning.CloudCoverageMin,
            FlightPlanetTerrainTuning.CloudCoverageMax
        );
        CloudFrequency = Mathf.Clamp(
            graphics.cloudFrequency,
            FlightPlanetTerrainTuning.CloudFrequencyMin,
            FlightPlanetTerrainTuning.CloudFrequencyMax
        );
        CloudCluster = Mathf.Clamp(
            graphics.cloudCluster,
            FlightPlanetTerrainTuning.CloudClusterMin,
            FlightPlanetTerrainTuning.CloudClusterMax
        );
        CloudRenderer = GameSettingsStore.ReadCloudRenderer();
        ExpCloudCoverage = Mathf.Clamp(
            graphics.expCloudCoverage,
            FlightPlanetTerrainTuning.ExpCloudCoverageMin,
            FlightPlanetTerrainTuning.ExpCloudCoverageMax
        );
        ExpCloudFlow = Mathf.Clamp(
            graphics.expCloudFlow,
            FlightPlanetTerrainTuning.ExpCloudFlowMin,
            FlightPlanetTerrainTuning.ExpCloudFlowMax
        );
        ExpCloudVortex = Mathf.Clamp(
            graphics.expCloudVortex,
            FlightPlanetTerrainTuning.ExpCloudVortexMin,
            FlightPlanetTerrainTuning.ExpCloudVortexMax
        );
        ExpCloudGrain = Mathf.Clamp(
            graphics.expCloudGrain,
            FlightPlanetTerrainTuning.ExpCloudGrainMin,
            FlightPlanetTerrainTuning.ExpCloudGrainMax
        );
        ExpCloudMassScale = Mathf.Clamp(
            graphics.expCloudMassScale,
            FlightPlanetTerrainTuning.ExpCloudMassScaleMin,
            FlightPlanetTerrainTuning.ExpCloudMassScaleMax
        );
        ExpCloudSoftness = Mathf.Clamp(
            graphics.expCloudSoftness,
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
        GameSettingsStore.WriteCloudRenderer(CloudRenderer);
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
        GameSettingsStore.EnsureLoaded();
        GameSettingsGraphics graphics = GameSettingsStore.Data.graphics;
        graphics.lodDistanceFactor = LodDistanceFactor;
        graphics.loadSpeedPerFrame = LoadSpeedPerFrame;
        graphics.maxLod = MaxLod;
        graphics.viewConeHalfAngleDegrees = ViewConeHalfAngleDegrees;
        graphics.cloudDetail = CloudDetail;
        graphics.cloudCoverage = CloudCoverage;
        graphics.cloudFrequency = CloudFrequency;
        graphics.cloudCluster = CloudCluster;
        GameSettingsStore.WriteCloudRenderer(CloudRenderer);
        graphics.expCloudCoverage = ExpCloudCoverage;
        graphics.expCloudFlow = ExpCloudFlow;
        graphics.expCloudVortex = ExpCloudVortex;
        graphics.expCloudGrain = ExpCloudGrain;
        graphics.expCloudMassScale = ExpCloudMassScale;
        graphics.expCloudSoftness = ExpCloudSoftness;
        GameSettingsStore.SaveToDisk();
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
