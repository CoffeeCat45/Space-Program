using UnityEngine;

/// <summary>
/// Adapts <see cref="EarthSandboxQuadtreeManager"/> to terrain graphics sliders.
/// </summary>
public sealed class EarthSandboxTerrainTuning : ITerrainGraphicsTuning
{
    readonly EarthSandboxQuadtreeManager manager;

    public EarthSandboxTerrainTuning(EarthSandboxQuadtreeManager manager)
    {
        this.manager = manager;
    }

    public bool IsAvailable => manager != null;

    public float LodDistanceFactor
    {
        get => manager.LodFactor;
        set => manager.LodFactor = value;
    }

    public int LoadSpeedPerFrame
    {
        get => manager.LoadSpeedPerFrame;
        set => manager.LoadSpeedPerFrame = value;
    }

    public int MaxLod
    {
        get => manager.FlightMaxLod;
        set => manager.FlightMaxLod = value;
    }

    public float ViewConeHalfAngleDegrees
    {
        get => manager.ViewConeHalfAngleDegrees;
        set => manager.ViewConeHalfAngleDegrees = value;
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
        manager.ViewConeUnloadHalfAngleDegrees;

    public int ActiveChunkCount =>
        manager != null ?
            EarthSandboxGlobePaint.FlightChunkMeshCacheCount :
            0;
}
