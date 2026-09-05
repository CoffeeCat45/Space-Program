using System;

/// <summary>
/// One JSON blob for graphics / HUD / developer options. Not a world save.
/// </summary>
[Serializable]
public sealed class GameSettingsData
{
    public int version = 1;
    public GameSettingsGraphics graphics = new GameSettingsGraphics();
    public GameSettingsHud hud = new GameSettingsHud();
    public GameSettingsDeveloper developer = new GameSettingsDeveloper();
    public GameSettingsPlay play = new GameSettingsPlay();
}

[Serializable]
public sealed class GameSettingsGraphics
{
    public float lodDistanceFactor = 2.5f;
    public int loadSpeedPerFrame = 8;
    public int maxLod = 15;
    public float viewConeHalfAngleDegrees = 75f;
    public float cloudDetail = 0.72f;
    public float cloudCoverage = 0.44f;
    public float cloudFrequency = 1f;
    public float cloudCluster;
    public string cloudRenderer = "SimpleShells";
    public float expCloudCoverage = 0.36f;
    public float expCloudFlow = 0.22f;
    public float expCloudVortex = 0.95f;
    public float expCloudGrain = 0.40f;
    public float expCloudMassScale = 1.00f;
    public float expCloudSoftness = 0.10f;
    public bool gebcoOnlyPipeline = true;
    public bool useHighResElevation = true;
    public bool graphicsQualitySaved;
    public bool msaaEnabled;
    public int msaaLevel = 4;
    public bool postAaEnabled;
    public int postAaMode = 1;
    public int postAaQuality = 1;
}

[Serializable]
public sealed class GameSettingsHud
{
    public string language = "Russian";
    public string background = "Transparent";
    public string[] hiddenFields = Array.Empty<string>();
}

[Serializable]
public sealed class GameSettingsDeveloper
{
    public bool infiniteFuel;
    public float thrustMultiplier = 1f;
    public bool cloudVisualsDisabled;
    public bool atmosphereVisualsDisabled;
    public float atmosphereOpacity = 1f;
    public bool planetSpinDisabled;
    public bool shipTrailEnabled = true;
    public bool soiSphereEnabled;
    public bool earthGeographicMapHigh8K;
    public bool moonGeographicMapHigh8K;
}

[Serializable]
public sealed class GameSettingsPlay
{
    public bool showTargetDirectionLine = true;
    public int lastScenario = (int)SimulationScenario.PlanetSurfaceStart;
}
