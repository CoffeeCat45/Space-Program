using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Loads and writes <c>settings.json</c> next to the Unity project / Windows player.
/// First launch copies current PlayerPrefs, then that file is the source of truth.
/// Does not store F5 world saves or VAB ship JSON.
/// </summary>
public static class GameSettingsStore
{
    public const string FileName = "settings.json";
    const int CurrentVersion = 1;
    const string MaxLodFloorMigratedKey = "SpaceProgram.PlanetTerrain.MaxLodFloorV2";

    static GameSettingsData data;
    static bool loaded;

    public static GameSettingsData Data
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    public static string FilePath =>
        Path.Combine(VabFileSystem.GameDirectory, FileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        data = null;
        loaded = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad()
    {
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (loaded && data != null)
        {
            return;
        }

        data = CreateDefaults();
        string path = FilePath;

        if (File.Exists(path))
        {
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path, new UTF8Encoding(false)), data);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "GameSettingsStore: could not read " + path + " — " + exception.Message +
                    ". Using defaults."
                );
                data = CreateDefaults();
            }

            data.version = CurrentVersion;
            EnsureNestedObjects();
            loaded = true;
            ApplyLanguage();
            return;
        }

        MigrateFromPlayerPrefs();
        loaded = true;
        ApplyLanguage();
        SaveToDisk();
        Debug.Log("GameSettingsStore: wrote " + path + " from current PlayerPrefs.");
    }

    public static void SaveToDisk()
    {
        EnsureLoaded();
        data.version = CurrentVersion;
        EnsureNestedObjects();

        string path = FilePath;
        string directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static void SetLanguage(Localization.Language language)
    {
        Localization.currentLanguage = language;
        EnsureLoaded();
        data.hud.language = language == Localization.Language.English ? "English" : "Russian";
        SaveToDisk();
    }

    public static bool TryGetGraphicsQuality(
        out VabGraphicsManager.GraphicsSettingsSnapshot snapshot
    )
    {
        EnsureLoaded();
        snapshot = default;

        if (!data.graphics.graphicsQualitySaved)
        {
            return false;
        }

        snapshot = new VabGraphicsManager.GraphicsSettingsSnapshot
        {
            msaaEnabled = data.graphics.msaaEnabled,
            msaaLevel = data.graphics.msaaLevel,
            postAaEnabled = data.graphics.postAaEnabled,
            postAaMode = data.graphics.postAaMode,
            postAaQuality = data.graphics.postAaQuality,
        };
        return true;
    }

    public static void SetGraphicsQuality(VabGraphicsManager.GraphicsSettingsSnapshot snapshot)
    {
        EnsureLoaded();
        data.graphics.graphicsQualitySaved = true;
        data.graphics.msaaEnabled = snapshot.msaaEnabled;
        data.graphics.msaaLevel = snapshot.msaaLevel;
        data.graphics.postAaEnabled = snapshot.postAaEnabled;
        data.graphics.postAaMode = snapshot.postAaMode;
        data.graphics.postAaQuality = snapshot.postAaQuality;
        SaveToDisk();
    }

    public static CloudRendererMode ReadCloudRenderer()
    {
        EnsureLoaded();
        return ParseCloudRenderer(data.graphics.cloudRenderer);
    }

    public static void WriteCloudRenderer(CloudRendererMode mode)
    {
        data.graphics.cloudRenderer = mode.ToString();
    }

    public static FlightHudBackground ReadHudBackground()
    {
        EnsureLoaded();
        return ParseHudBackground(data.hud.background);
    }

    public static void WriteHudBackground(FlightHudBackground background)
    {
        data.hud.background = background.ToString();
    }

    public static bool[] ReadHudVisibility()
    {
        EnsureLoaded();
        FlightHudField[] fields = (FlightHudField[])Enum.GetValues(typeof(FlightHudField));
        bool[] visible = new bool[fields.Length];

        for (int i = 0; i < visible.Length; i++)
        {
            visible[i] = true;
        }

        string[] hidden = data.hud.hiddenFields;

        if (hidden == null)
        {
            return visible;
        }

        for (int i = 0; i < hidden.Length; i++)
        {
            if (
                Enum.TryParse(hidden[i], out FlightHudField field) &&
                (int)field >= 0 &&
                (int)field < visible.Length
            )
            {
                visible[(int)field] = false;
            }
        }

        return visible;
    }

    public static void WriteHudVisibility(bool[] visible)
    {
        EnsureLoaded();
        FlightHudField[] fields = (FlightHudField[])Enum.GetValues(typeof(FlightHudField));
        List<string> hidden = new List<string>();

        for (int i = 0; i < fields.Length && i < visible.Length; i++)
        {
            if (!visible[i])
            {
                hidden.Add(fields[i].ToString());
            }
        }

        data.hud.hiddenFields = hidden.ToArray();
    }

    static GameSettingsData CreateDefaults()
    {
        GameSettingsData created = new GameSettingsData
        {
            version = CurrentVersion,
        };
        created.graphics.lodDistanceFactor = FlightPlanetTerrainTuning.LodDistanceFactorDefault;
        created.graphics.loadSpeedPerFrame = FlightPlanetSphere.DefaultMaxLodSplitsPerFrame;
        created.graphics.maxLod = FlightPlanetSphere.DefaultMaxLodLevel;
        created.graphics.viewConeHalfAngleDegrees =
            PlanetPqsAsyncLodSettings.DefaultViewConeHalfAngleDegrees;
        created.graphics.cloudDetail = FlightPlanetTerrainTuning.CloudDetailDefault;
        created.graphics.cloudCoverage = FlightPlanetTerrainTuning.CloudCoverageDefault;
        created.graphics.cloudFrequency = FlightPlanetTerrainTuning.CloudFrequencyDefault;
        created.graphics.cloudCluster = FlightPlanetTerrainTuning.CloudClusterDefault;
        created.graphics.cloudRenderer = CloudRendererMode.SimpleShells.ToString();
        created.graphics.expCloudCoverage = FlightPlanetTerrainTuning.ExpCloudCoverageDefault;
        created.graphics.expCloudFlow = FlightPlanetTerrainTuning.ExpCloudFlowDefault;
        created.graphics.expCloudVortex = FlightPlanetTerrainTuning.ExpCloudVortexDefault;
        created.graphics.expCloudGrain = FlightPlanetTerrainTuning.ExpCloudGrainDefault;
        created.graphics.expCloudMassScale = FlightPlanetTerrainTuning.ExpCloudMassScaleDefault;
        created.graphics.expCloudSoftness = FlightPlanetTerrainTuning.ExpCloudSoftnessDefault;
        created.graphics.gebcoOnlyPipeline = true;
        created.graphics.useHighResElevation = true;
        created.developer.thrustMultiplier = (float)DevSettings.DefaultThrustMultiplier;
        created.developer.shipTrailEnabled = true;
        created.developer.atmosphereOpacity = 1f;
        created.play.showTargetDirectionLine = true;
        created.play.lastScenario = (int)SimulationScenario.PlanetSurfaceStart;
        created.hud.language = "Russian";
        created.hud.background = FlightHudBackground.Transparent.ToString();
        created.hud.hiddenFields = Array.Empty<string>();
        return created;
    }

    static void EnsureNestedObjects()
    {
        if (data.graphics == null)
        {
            data.graphics = new GameSettingsGraphics();
        }

        if (data.hud == null)
        {
            data.hud = new GameSettingsHud();
        }

        if (data.developer == null)
        {
            data.developer = new GameSettingsDeveloper();
        }

        if (data.play == null)
        {
            data.play = new GameSettingsPlay();
        }

        if (data.hud.hiddenFields == null)
        {
            data.hud.hiddenFields = Array.Empty<string>();
        }
    }

    static void ApplyLanguage()
    {
        Localization.currentLanguage =
            string.Equals(data.hud.language, "English", StringComparison.OrdinalIgnoreCase) ?
                Localization.Language.English :
                Localization.Language.Russian;
    }

    static void MigrateFromPlayerPrefs()
    {
        GameSettingsGraphics graphics = data.graphics;
        graphics.lodDistanceFactor = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.LodDistanceFactor",
            FlightPlanetTerrainTuning.LodDistanceFactorDefault
        );
        graphics.loadSpeedPerFrame = PlayerPrefs.GetInt(
            "SpaceProgram.PlanetTerrain.LoadSpeedPerFrame",
            FlightPlanetSphere.DefaultMaxLodSplitsPerFrame
        );
        graphics.maxLod = PlayerPrefs.GetInt(
            "SpaceProgram.PlanetTerrain.MaxLod",
            FlightPlanetSphere.DefaultMaxLodLevel
        );

        if (PlayerPrefs.GetInt(MaxLodFloorMigratedKey, 0) == 1)
        {
            if (graphics.maxLod == FlightPlanetSphere.DefaultMaxLodLevel)
            {
                graphics.maxLod = 8;
            }
        }

        graphics.viewConeHalfAngleDegrees = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.ViewConeHalfAngle",
            PlanetPqsAsyncLodSettings.DefaultViewConeHalfAngleDegrees
        );
        graphics.cloudDetail = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.CloudDetail",
            FlightPlanetTerrainTuning.CloudDetailDefault
        );
        graphics.cloudCoverage = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.CloudCoverage",
            FlightPlanetTerrainTuning.CloudCoverageDefault
        );
        graphics.cloudFrequency = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.CloudFrequency",
            FlightPlanetTerrainTuning.CloudFrequencyDefault
        );
        graphics.cloudCluster = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.CloudCluster",
            FlightPlanetTerrainTuning.CloudClusterDefault
        );
        graphics.cloudRenderer = ParseCloudRenderer(
            PlayerPrefs.GetInt(
                "SpaceProgram.PlanetTerrain.CloudRenderer",
                (int)CloudRendererMode.SimpleShells
            )
        ).ToString();
        graphics.expCloudCoverage = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.ExpCloudCoverage",
            FlightPlanetTerrainTuning.ExpCloudCoverageDefault
        );
        graphics.expCloudFlow = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.ExpCloudFlow",
            FlightPlanetTerrainTuning.ExpCloudFlowDefault
        );
        graphics.expCloudVortex = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.ExpCloudVortex",
            FlightPlanetTerrainTuning.ExpCloudVortexDefault
        );
        graphics.expCloudGrain = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.ExpCloudGrain",
            FlightPlanetTerrainTuning.ExpCloudGrainDefault
        );
        graphics.expCloudMassScale = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.ExpCloudMassScale",
            FlightPlanetTerrainTuning.ExpCloudMassScaleDefault
        );
        graphics.expCloudSoftness = PlayerPrefs.GetFloat(
            "SpaceProgram.PlanetTerrain.ExpCloudSoftness",
            FlightPlanetTerrainTuning.ExpCloudSoftnessDefault
        );
        graphics.gebcoOnlyPipeline =
            PlayerPrefs.GetInt("SpaceProgram.PlanetTerrain.GebcoOnlyPipeline", 1) == 1;
        graphics.useHighResElevation = !PlayerPrefs.HasKey("SpaceProgram.PlanetElevation.UseHighResRaw") ||
            PlayerPrefs.GetInt("SpaceProgram.PlanetElevation.UseHighResRaw", 1) == 1;

        if (PlayerPrefs.HasKey("SpaceProgram.GraphicsSettings.v1"))
        {
            string payload = PlayerPrefs.GetString("SpaceProgram.GraphicsSettings.v1", string.Empty);
            string[] parts = payload.Split('|');

            if (parts.Length >= 5)
            {
                graphics.graphicsQualitySaved = true;
                graphics.msaaEnabled = parts[0] == "1";
                graphics.msaaLevel = ParseInt(parts[1], VabGraphicsManager.Msaa4x);
                graphics.postAaEnabled = parts[2] == "1";
                graphics.postAaMode = ParseInt(parts[3], VabGraphicsManager.PostAaModeFxaa);
                graphics.postAaQuality = ParseInt(parts[4], VabGraphicsManager.PostAaQualityMedium);
            }
        }

        GameSettingsDeveloper developer = data.developer;
        developer.infiniteFuel = PlayerPrefs.GetInt("SpaceProgram.Dev.InfiniteFuel", 0) == 1;
        developer.thrustMultiplier = PlayerPrefs.GetFloat(
            "SpaceProgram.Dev.ThrustMultiplier",
            (float)DevSettings.DefaultThrustMultiplier
        );
        developer.cloudVisualsDisabled =
            PlayerPrefs.GetInt("SpaceProgram.Dev.CloudVisualsDisabled", 0) == 1;
        developer.atmosphereVisualsDisabled =
            PlayerPrefs.GetInt("SpaceProgram.Dev.AtmosphereVisualsDisabled", 0) == 1;
        developer.atmosphereOpacity = PlayerPrefs.GetFloat(
            "SpaceProgram.Dev.AtmosphereOpacity",
            1f
        );
        developer.planetSpinDisabled =
            PlayerPrefs.GetInt("SpaceProgram.Dev.PlanetSpinDisabled", 0) == 1;
        developer.shipTrailEnabled =
            PlayerPrefs.GetInt("SpaceProgram.Dev.ShipTrailEnabled", 1) == 1;
        developer.soiSphereEnabled =
            PlayerPrefs.GetInt("SpaceProgram.Dev.SoiSphereEnabled", 0) == 1;
        developer.earthGeographicMapHigh8K =
            PlayerPrefs.GetInt("SpaceProgram.Dev.EarthGeographicMapHigh8K", 0) == 1;
        developer.moonGeographicMapHigh8K =
            PlayerPrefs.GetInt("SpaceProgram.Dev.MoonGeographicMapHigh8K", 0) == 1;

        data.play.showTargetDirectionLine =
            PlayerPrefs.GetInt("SpaceProgram.View.ShowTargetDirectionLine", 1) == 1;
        data.play.lastScenario = PlayerPrefs.GetInt(
            "SpaceProgram.LastScenario",
            (int)SimulationScenario.PlanetSurfaceStart
        );

        FlightHudField[] fields = (FlightHudField[])Enum.GetValues(typeof(FlightHudField));
        List<string> hidden = new List<string>();

        for (int i = 0; i < fields.Length; i++)
        {
            if (PlayerPrefs.GetInt("SpaceProgram.Hud.Show." + fields[i], 1) == 0)
            {
                hidden.Add(fields[i].ToString());
            }
        }

        data.hud.hiddenFields = hidden.ToArray();
        data.hud.background = ParseHudBackground(
            PlayerPrefs.GetInt("SpaceProgram.Hud.Background", 0)
        ).ToString();
        data.hud.language = "Russian";
    }

    static CloudRendererMode ParseCloudRenderer(string value)
    {
        if (Enum.TryParse(value, true, out CloudRendererMode parsed))
        {
            return parsed;
        }

        return CloudRendererMode.SimpleShells;
    }

    static CloudRendererMode ParseCloudRenderer(int value)
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

    static FlightHudBackground ParseHudBackground(string value)
    {
        if (Enum.TryParse(value, true, out FlightHudBackground parsed))
        {
            return parsed;
        }

        return FlightHudBackground.Transparent;
    }

    static FlightHudBackground ParseHudBackground(int value)
    {
        if (value == (int)FlightHudBackground.Dim)
        {
            return FlightHudBackground.Dim;
        }

        if (value == (int)FlightHudBackground.Dark)
        {
            return FlightHudBackground.Dark;
        }

        return FlightHudBackground.Transparent;
    }

    static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ?
            parsed :
            fallback;
    }
}
