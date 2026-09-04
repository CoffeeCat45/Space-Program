using UnityEngine;

/// <summary>
/// Persistent developer toggles (PlayerPrefs).
/// </summary>
public static class DevSettings
{
    const string InfiniteFuelKey = "SpaceProgram.Dev.InfiniteFuel";
    const string ThrustMultiplierKey = "SpaceProgram.Dev.ThrustMultiplier";
    const string CloudVisualsDisabledKey = "SpaceProgram.Dev.CloudVisualsDisabled";
    const string AtmosphereVisualsDisabledKey = "SpaceProgram.Dev.AtmosphereVisualsDisabled";
    const string AtmosphereOpacityKey = "SpaceProgram.Dev.AtmosphereOpacity";
    const string PlanetSpinDisabledKey = "SpaceProgram.Dev.PlanetSpinDisabled";
    const string ShipTrailEnabledKey = "SpaceProgram.Dev.ShipTrailEnabled";
    const string SoiSphereEnabledKey = "SpaceProgram.Dev.SoiSphereEnabled";
    const string EarthGeographicMapHigh8KKey = "SpaceProgram.Dev.EarthGeographicMapHigh8K";
    const string MoonGeographicMapHigh8KKey = "SpaceProgram.Dev.MoonGeographicMapHigh8K";

    public const double DefaultThrustMultiplier = 1.0;
    public const double MinThrustMultiplier = 0.01;
    public const double MaxThrustMultiplier = 100.0;

    public static bool InfiniteFuel { get; private set; }

    public static double ThrustMultiplier { get; private set; } = DefaultThrustMultiplier;

    public static bool CloudVisualsDisabled { get; private set; }

    public static bool AtmosphereVisualsDisabled { get; private set; }

    public static float AtmosphereOpacity { get; private set; } = 1f;

    public static bool PlanetSpinDisabled { get; private set; }

    public static bool ShipTrailEnabled { get; private set; } = true;

    public static bool SoiSphereEnabled { get; private set; }

    public static bool EarthGeographicMapHigh8K { get; private set; }

    public static bool MoonGeographicMapHigh8K { get; private set; }

    public static void Load()
    {
        InfiniteFuel = PlayerPrefs.GetInt(InfiniteFuelKey, 0) == 1;
        SimulationCheatFlags.InfiniteFuel = InfiniteFuel;
        CloudVisualsDisabled = PlayerPrefs.GetInt(CloudVisualsDisabledKey, 0) == 1;
        AtmosphereVisualsDisabled = PlayerPrefs.GetInt(AtmosphereVisualsDisabledKey, 0) == 1;
        AtmosphereOpacity = Mathf.Clamp01(
            PlayerPrefs.GetFloat(AtmosphereOpacityKey, 1f)
        );
        PlanetSpinDisabled = PlayerPrefs.GetInt(PlanetSpinDisabledKey, 0) == 1;
        SimulationCheatFlags.PlanetSpinDisabled = PlanetSpinDisabled;
        ShipTrailEnabled = PlayerPrefs.GetInt(ShipTrailEnabledKey, 1) == 1;
        SoiSphereEnabled = PlayerPrefs.GetInt(SoiSphereEnabledKey, 0) == 1;
        EarthGeographicMapHigh8K = PlayerPrefs.GetInt(EarthGeographicMapHigh8KKey, 0) == 1;
        MoonGeographicMapHigh8K = PlayerPrefs.GetInt(MoonGeographicMapHigh8KKey, 0) == 1;

        float savedMultiplier = PlayerPrefs.GetFloat(
            ThrustMultiplierKey,
            (float)DefaultThrustMultiplier
        );
        SetThrustMultiplier(savedMultiplier, persist: false);
    }

    public static void SetInfiniteFuel(bool enabled)
    {
        InfiniteFuel = enabled;
        SimulationCheatFlags.InfiniteFuel = enabled;
        PlayerPrefs.SetInt(InfiniteFuelKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool TrySetThrustMultiplierFromInput(string input, out double appliedMultiplier)
    {
        appliedMultiplier = DefaultThrustMultiplier;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string normalizedInput = input.Trim().Replace(',', '.');

        if (!double.TryParse(
            normalizedInput,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double parsedMultiplier
        ))
        {
            return false;
        }

        SetThrustMultiplier(parsedMultiplier, persist: true);
        appliedMultiplier = ThrustMultiplier;
        return true;
    }

    public static void SetThrustMultiplier(double multiplier, bool persist)
    {
        ThrustMultiplier = System.Math.Clamp(
            multiplier,
            MinThrustMultiplier,
            MaxThrustMultiplier
        );
        SimulationCheatFlags.ThrustMultiplier = ThrustMultiplier;

        if (persist)
        {
            PlayerPrefs.SetFloat(ThrustMultiplierKey, (float)ThrustMultiplier);
            PlayerPrefs.Save();
        }
    }

    public static void SetCloudVisualsDisabled(bool disabled)
    {
        CloudVisualsDisabled = disabled;
        PlayerPrefs.SetInt(CloudVisualsDisabledKey, disabled ? 1 : 0);
        PlayerPrefs.Save();
        FlightPlanetSphere.ApplyRuntimeAtmosphereCloudVisuals();
    }

    public static void SetAtmosphereVisualsDisabled(bool disabled)
    {
        AtmosphereVisualsDisabled = disabled;
        PlayerPrefs.SetInt(AtmosphereVisualsDisabledKey, disabled ? 1 : 0);
        PlayerPrefs.Save();
        FlightPlanetSphere.ApplyRuntimeAtmosphereCloudVisuals();
    }

    public static void SetAtmosphereOpacity(float opacity)
    {
        AtmosphereOpacity = Mathf.Clamp01(opacity);
        PlayerPrefs.SetFloat(AtmosphereOpacityKey, AtmosphereOpacity);
        PlayerPrefs.Save();
        FlightPlanetSphere.ApplyRuntimeAtmosphereOpacity();
    }

    public static void SetPlanetSpinDisabled(bool disabled)
    {
        PlanetSpinDisabled = disabled;
        SimulationCheatFlags.PlanetSpinDisabled = disabled;
        PlayerPrefs.SetInt(PlanetSpinDisabledKey, disabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetShipTrailEnabled(bool enabled)
    {
        ShipTrailEnabled = enabled;
        PlayerPrefs.SetInt(ShipTrailEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ShipTrailView.ApplyEnabledStateToAll();
    }

    public static void SetSoiSphereEnabled(bool enabled)
    {
        SoiSphereEnabled = enabled;
        PlayerPrefs.SetInt(SoiSphereEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        SphereOfInfluenceView.ApplyEnabledStateToAll();
    }

    public static void SetEarthGeographicMapHigh8K(bool high8K)
    {
        EarthGeographicMapHigh8K = high8K;
        PlayerPrefs.SetInt(EarthGeographicMapHigh8KKey, high8K ? 1 : 0);
        PlayerPrefs.Save();
        FlightPlanetSphere.ApplyRuntimeEarthGeographicMap();
    }

    public static void SetMoonGeographicMapHigh8K(bool high8K)
    {
        MoonGeographicMapHigh8K = high8K;
        PlayerPrefs.SetInt(MoonGeographicMapHigh8KKey, high8K ? 1 : 0);
        PlayerPrefs.Save();
        FlightPlanetSphere.ApplyRuntimeMoonGeographicMap();
    }
}
