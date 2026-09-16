using UnityEngine;

/// <summary>
/// Persistent developer toggles (<c>settings.json</c>).
/// </summary>
public static class DevSettings
{
    public const double DefaultThrustMultiplier = 1.0;
    public const double MinThrustMultiplier = 0.01;
    public const double MaxThrustMultiplier = 100.0;

    public static bool InfiniteFuel { get; private set; }

    public static double ThrustMultiplier { get; private set; } = DefaultThrustMultiplier;

    public static bool CloudVisualsDisabled { get; private set; }

    public static bool AtmosphereVisualsDisabled { get; private set; }

    public static float AtmosphereOpacity { get; private set; } = 1f;

    public static bool PlanetSpinDisabled { get; private set; }

    public static bool DisableCrashDamage { get; private set; }

    public static bool ShipTrailEnabled { get; private set; } = true;

    public static bool SoiSphereEnabled { get; private set; }

    public static bool EarthGeographicMapHigh8K { get; private set; }

    public static bool MoonGeographicMapHigh8K { get; private set; }

    public static void Load()
    {
        GameSettingsStore.EnsureLoaded();
        GameSettingsDeveloper developer = GameSettingsStore.Data.developer;
        InfiniteFuel = developer.infiniteFuel;
        SimulationCheatFlags.InfiniteFuel = InfiniteFuel;
        CloudVisualsDisabled = developer.cloudVisualsDisabled;
        AtmosphereVisualsDisabled = developer.atmosphereVisualsDisabled;
        AtmosphereOpacity = Mathf.Clamp01(developer.atmosphereOpacity);
        PlanetSpinDisabled = developer.planetSpinDisabled;
        SimulationCheatFlags.PlanetSpinDisabled = PlanetSpinDisabled;
        DisableCrashDamage = developer.disableCrashDamage;
        SimulationCheatFlags.DisableCrashDamage = DisableCrashDamage;
        ShipTrailEnabled = developer.shipTrailEnabled;
        SoiSphereEnabled = developer.soiSphereEnabled;
        EarthGeographicMapHigh8K = developer.earthGeographicMapHigh8K;
        MoonGeographicMapHigh8K = developer.moonGeographicMapHigh8K;
        SetThrustMultiplier(developer.thrustMultiplier, persist: false);
    }

    public static void SetInfiniteFuel(bool enabled)
    {
        InfiniteFuel = enabled;
        SimulationCheatFlags.InfiniteFuel = enabled;
        Persist();
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
            Persist();
        }
    }

    public static void SetCloudVisualsDisabled(bool disabled)
    {
        CloudVisualsDisabled = disabled;
        Persist();
        FlightPlanetSphere.ApplyRuntimeAtmosphereCloudVisuals();
    }

    public static void SetAtmosphereVisualsDisabled(bool disabled)
    {
        AtmosphereVisualsDisabled = disabled;
        Persist();
        FlightPlanetSphere.ApplyRuntimeAtmosphereCloudVisuals();
    }

    public static void SetAtmosphereOpacity(float opacity)
    {
        AtmosphereOpacity = Mathf.Clamp01(opacity);
        Persist();
        FlightPlanetSphere.ApplyRuntimeAtmosphereOpacity();
    }

    public static void SetPlanetSpinDisabled(bool disabled)
    {
        PlanetSpinDisabled = disabled;
        SimulationCheatFlags.PlanetSpinDisabled = disabled;
        Persist();
    }

    public static void SetDisableCrashDamage(bool disabled)
    {
        DisableCrashDamage = disabled;
        SimulationCheatFlags.DisableCrashDamage = disabled;
        Persist();
    }

    public static void SetShipTrailEnabled(bool enabled)
    {
        ShipTrailEnabled = enabled;
        Persist();
        ShipTrailView.ApplyEnabledStateToAll();
    }

    public static void SetSoiSphereEnabled(bool enabled)
    {
        SoiSphereEnabled = enabled;
        Persist();
        SphereOfInfluenceView.ApplyEnabledStateToAll();
    }

    public static void SetEarthGeographicMapHigh8K(bool high8K)
    {
        EarthGeographicMapHigh8K = high8K;
        Persist();
        FlightPlanetSphere.ApplyRuntimeEarthGeographicMap();
    }

    public static void SetMoonGeographicMapHigh8K(bool high8K)
    {
        MoonGeographicMapHigh8K = high8K;
        Persist();
        FlightPlanetSphere.ApplyRuntimeMoonGeographicMap();
    }

    static void Persist()
    {
        GameSettingsStore.EnsureLoaded();
        GameSettingsDeveloper developer = GameSettingsStore.Data.developer;
        developer.infiniteFuel = InfiniteFuel;
        developer.thrustMultiplier = (float)ThrustMultiplier;
        developer.cloudVisualsDisabled = CloudVisualsDisabled;
        developer.atmosphereVisualsDisabled = AtmosphereVisualsDisabled;
        developer.atmosphereOpacity = AtmosphereOpacity;
        developer.planetSpinDisabled = PlanetSpinDisabled;
        developer.disableCrashDamage = DisableCrashDamage;
        developer.shipTrailEnabled = ShipTrailEnabled;
        developer.soiSphereEnabled = SoiSphereEnabled;
        developer.earthGeographicMapHigh8K = EarthGeographicMapHigh8K;
        developer.moonGeographicMapHigh8K = MoonGeographicMapHigh8K;
        GameSettingsStore.SaveToDisk();
    }
}
