using UnityEngine;

/// <summary>
/// Runtime LOD sliders for <see cref="FlightPlanetSphere"/> (main game).
/// </summary>
public sealed class FlightPlanetTerrainTuning : ITerrainGraphicsTuning
{
    public const float LodDistanceFactorMin = 0.25f;
    public const float LodDistanceFactorMax = 10f;
    public const float LodDistanceFactorDefault = 2.5f;
    public const int LoadSpeedMin = 1;
    public const int LoadSpeedMax = 12;
    public const int MaxLodMin = 1;
    public const float ViewConeHalfAngleMin = 40f;
    public const float ViewConeHalfAngleMax = 110f;
    public const float CloudDetailMin = 0f;
    public const float CloudDetailMax = 1f;
    public const float CloudDetailDefault = 0.72f;
    public const float CloudCoverageMin = 0.08f;
    public const float CloudCoverageMax = 0.82f;
    public const float CloudCoverageDefault = 0.44f;
    public const float CloudFrequencyMin = 0.40f;
    public const float CloudFrequencyMax = 2.20f;
    public const float CloudFrequencyDefault = 1.00f;
    public const float CloudClusterMin = 0f;
    public const float CloudClusterMax = 1f;
    public const float CloudClusterDefault = 0f;
    public const float ExpCloudCoverageMin = 0.10f;
    public const float ExpCloudCoverageMax = 0.72f;
    public const float ExpCloudCoverageDefault = 0.36f;
    public const float ExpCloudFlowMin = 0f;
    public const float ExpCloudFlowMax = 1f;
    public const float ExpCloudFlowDefault = 0.22f;
    public const float ExpCloudVortexMin = 0f;
    public const float ExpCloudVortexMax = 1.4f;
    public const float ExpCloudVortexDefault = 0.95f;
    public const float ExpCloudGrainMin = 0f;
    public const float ExpCloudGrainMax = 1f;
    public const float ExpCloudGrainDefault = 0.40f;
    public const float ExpCloudMassScaleMin = 0.45f;
    public const float ExpCloudMassScaleMax = 1.80f;
    public const float ExpCloudMassScaleDefault = 1.00f;
    public const float ExpCloudSoftnessMin = 0.03f;
    public const float ExpCloudSoftnessMax = 0.40f;
    public const float ExpCloudSoftnessDefault = 0.10f;
    public const float SplitSpanFactorPerLodDistanceUnit = 0.14f;

    public static readonly FlightPlanetTerrainTuning Instance = new FlightPlanetTerrainTuning();

    FlightPlanetTerrainTuning()
    {
    }

    FlightPlanetSphere Planet => FlightPlanetSphere.ActiveInstance;

    public bool IsAvailable => Planet != null && Planet.HasActiveQuadtree;

    public float LodDistanceFactor
    {
        get => PlanetLodSettings.LodDistanceFactor;
        set => PlanetLodSettings.SetLodDistanceFactor(value);
    }

    public int LoadSpeedPerFrame
    {
        get => PlanetLodSettings.LoadSpeedPerFrame;
        set => PlanetLodSettings.SetLoadSpeedPerFrame(value);
    }

    public int MaxLod
    {
        get => PlanetLodSettings.MaxLod;
        set => PlanetLodSettings.SetMaxLod(value);
    }

    public float ViewConeHalfAngleDegrees
    {
        get => PlanetLodSettings.ViewConeHalfAngleDegrees;
        set => PlanetLodSettings.SetViewConeHalfAngleDegrees(value);
    }

    public float CloudDetail
    {
        get => PlanetLodSettings.CloudDetail;
        set => PlanetLodSettings.SetCloudDetail(value);
    }

    public float CloudCoverage
    {
        get => PlanetLodSettings.CloudCoverage;
        set => PlanetLodSettings.SetCloudCoverage(value);
    }

    public float CloudFrequency
    {
        get => PlanetLodSettings.CloudFrequency;
        set => PlanetLodSettings.SetCloudFrequency(value);
    }

    public float CloudCluster
    {
        get => PlanetLodSettings.CloudCluster;
        set => PlanetLodSettings.SetCloudCluster(value);
    }

    public CloudRendererMode CloudRenderer
    {
        get => PlanetLodSettings.CloudRenderer;
        set => PlanetLodSettings.SetCloudRenderer(value);
    }

    public float ViewConeUnloadHalfAngleDegrees =>
        Planet != null ?
            Planet.RuntimeViewConeUnloadHalfAngleDegrees :
            PlanetPqsAsyncLodSettings.DefaultViewConeHalfAngleDegrees +
            PlanetPqsAsyncLodSettings.ViewConeUnloadHysteresisDegrees;

    public int ActiveChunkCount => FlightPlanetSphere.GetActiveChunkCount();
}
