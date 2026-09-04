using System;
using UnityEngine;

public enum FlightHudField
{
    Language,
    SpeedAccel,
    MassFuel,
    Stage,
    Status,
    VerticalSurfaceGlide,
    Orientation,
    OrbitBodyAlt,
    OrbitType,
    OrbitPeAp,
    OrbitTimeToPeAp,
    OrbitCircularize,
    TargetBodyDist,
    TargetRelVel,
    TargetPortGap,
    TargetClosestApproach,
    TargetDockingMessage,
    TargetSoiEncounter,
    EventsNext,
    EventsLastStaging,
    EventsLastFlight,
    MissionHint,
    ControlThrustThrottle,
    ControlTwr,
    ControlAutoBurn,
    AtmosphereDensityDrag,
    AtmosphereQDragAccel,
    AtmosphereAoALift,
    AtmosphereArea,
    AtmosphereHeating,
    AtmospherePreview,
    DescentDrift,
    ManeuverPlannedDv,
    ManeuverBurn,
    ManeuverNodeTime,
    ManeuverWarnings,
    DetailsAltitude,
    DetailsMoonDistance,
    DetailsClosestApproach,
    DetailsTimeToCa,
    DetailsRelSpeedCa,
    DetailsDeltaVStep,
    DetailsPreviewOrbit,
    DetailsBurnDrift,
    DetailsVesselMarkers,
    Keys,
    DebugGraphicsMode,
    DebugGraphicsShip,
    DebugGraphicsPlanet,
    DebugGraphicsUnityDistance,
    DebugGraphicsSimDistance
}

public enum FlightHudBackground
{
    Transparent = 0,
    Dim = 1,
    Dark = 2
}

/// <summary>
/// Which flight HUD lines are shown. Persisted in PlayerPrefs. Default: all on.
/// </summary>
public static class FlightHudSettings
{
    public readonly struct Item
    {
        public readonly FlightHudField Field;
        public readonly string SectionKey;
        public readonly string NameKey;

        public Item(FlightHudField field, string sectionKey, string nameKey)
        {
            Field = field;
            SectionKey = sectionKey;
            NameKey = nameKey;
        }
    }

    const string PrefsPrefix = "SpaceProgram.Hud.Show.";
    const string BackgroundKey = "SpaceProgram.Hud.Background";

    static readonly Color BackgroundTransparent = new Color(0f, 0f, 0f, 0f);
    static readonly Color BackgroundDim = new Color(0f, 0f, 0f, 0.38f);
    static readonly Color BackgroundDark = new Color(0f, 0f, 0f, 0.78f);

    static readonly FlightHudField[] AllFields =
        (FlightHudField[])Enum.GetValues(typeof(FlightHudField));

    static readonly bool[] Visible = new bool[AllFields.Length];
    static bool loaded;

    public static FlightHudBackground Background { get; private set; } =
        FlightHudBackground.Transparent;

    public static readonly Item[] Items =
    {
        new Item(FlightHudField.Language, "HUD", "Language"),
        new Item(FlightHudField.SpeedAccel, "Flight", "Speed / Accel"),
        new Item(FlightHudField.MassFuel, "Flight", "Mass / Fuel"),
        new Item(FlightHudField.Stage, "Flight", "Stage"),
        new Item(FlightHudField.Status, "Flight", "Status"),
        new Item(FlightHudField.VerticalSurfaceGlide, "Flight", "Vertical / surface / glide"),
        new Item(FlightHudField.Orientation, "Flight", "Orientation"),
        new Item(FlightHudField.OrbitBodyAlt, "Orbit", "Body / Alt"),
        new Item(FlightHudField.OrbitType, "Orbit", "Type"),
        new Item(FlightHudField.OrbitPeAp, "Orbit", "Pe / Ap"),
        new Item(FlightHudField.OrbitTimeToPeAp, "Orbit", "Time to Pe / Ap"),
        new Item(FlightHudField.OrbitCircularize, "Orbit", "Circularize"),
        new Item(FlightHudField.TargetBodyDist, "Target", "Target body / Dist"),
        new Item(FlightHudField.TargetRelVel, "Target", "Rel vel to target"),
        new Item(FlightHudField.TargetPortGap, "Target", "Port gap"),
        new Item(FlightHudField.TargetClosestApproach, "Target", "Target CA"),
        new Item(FlightHudField.TargetDockingMessage, "Target", "Docking message"),
        new Item(FlightHudField.TargetSoiEncounter, "Target", "SoI encounter"),
        new Item(FlightHudField.EventsNext, "Events", "Next"),
        new Item(FlightHudField.EventsLastStaging, "Events", "Last staging"),
        new Item(FlightHudField.EventsLastFlight, "Events", "Last flight event"),
        new Item(FlightHudField.MissionHint, "Mission", "Mission"),
        new Item(FlightHudField.ControlThrustThrottle, "Control", "Thrust accel / Throttle"),
        new Item(FlightHudField.ControlTwr, "Control", "TWR"),
        new Item(FlightHudField.ControlAutoBurn, "Control", "Auto-burn"),
        new Item(FlightHudField.AtmosphereDensityDrag, "Atmosphere", "Density / Drag"),
        new Item(FlightHudField.AtmosphereQDragAccel, "Atmosphere", "Dynamic pressure"),
        new Item(FlightHudField.AtmosphereAoALift, "Atmosphere", "Angle of attack / Lift"),
        new Item(FlightHudField.AtmosphereArea, "Atmosphere", "Effective area"),
        new Item(FlightHudField.AtmosphereHeating, "Atmosphere", "Heating warnings"),
        new Item(FlightHudField.AtmospherePreview, "Atmosphere", "Atmospheric preview"),
        new Item(FlightHudField.DescentDrift, "Atmosphere", "Descent drift"),
        new Item(FlightHudField.ManeuverPlannedDv, "Maneuver", "Planned delta-v"),
        new Item(FlightHudField.ManeuverBurn, "Maneuver", "Burn duration"),
        new Item(FlightHudField.ManeuverNodeTime, "Maneuver", "Node time"),
        new Item(FlightHudField.ManeuverWarnings, "Maneuver", "Maneuver warnings"),
        new Item(FlightHudField.DetailsAltitude, "Details", "Altitude over planet"),
        new Item(FlightHudField.DetailsMoonDistance, "Details", "Moon distance"),
        new Item(FlightHudField.DetailsClosestApproach, "Details", "Closest approach"),
        new Item(FlightHudField.DetailsTimeToCa, "Details", "Time to CA"),
        new Item(FlightHudField.DetailsRelSpeedCa, "Details", "Relative speed at CA"),
        new Item(FlightHudField.DetailsDeltaVStep, "Details", "Delta-v step"),
        new Item(FlightHudField.DetailsPreviewOrbit, "Details", "Preview orbit"),
        new Item(FlightHudField.DetailsBurnDrift, "Details", "Burn drift"),
        new Item(FlightHudField.DetailsVesselMarkers, "View", "Vessel target markers"),
        new Item(FlightHudField.Keys, "Keys", "Keys"),
        new Item(FlightHudField.DebugGraphicsMode, "Developer settings", "Graphics bridge mode"),
        new Item(FlightHudField.DebugGraphicsShip, "Developer settings", "Graphics bridge ship"),
        new Item(FlightHudField.DebugGraphicsPlanet, "Developer settings", "Graphics bridge planet"),
        new Item(FlightHudField.DebugGraphicsUnityDistance, "Developer settings", "Graphics bridge unity distance"),
        new Item(FlightHudField.DebugGraphicsSimDistance, "Developer settings", "Graphics bridge sim distance")
    };

    public static void Load()
    {
        for (int i = 0; i < AllFields.Length; i++)
        {
            Visible[i] = PlayerPrefs.GetInt(PrefsKey(AllFields[i]), 1) == 1;
        }

        Background = ClampBackground(PlayerPrefs.GetInt(BackgroundKey, 0));
        loaded = true;
    }

    public static bool IsVisible(FlightHudField field)
    {
        if (!loaded)
        {
            Load();
        }

        int index = (int)field;
        if (index < 0 || index >= Visible.Length)
        {
            return true;
        }

        return Visible[index];
    }

    public static bool AnyVisible(params FlightHudField[] fields)
    {
        if (fields == null || fields.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < fields.Length; i++)
        {
            if (IsVisible(fields[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static void SetVisible(FlightHudField field, bool visible)
    {
        if (!loaded)
        {
            Load();
        }

        int index = (int)field;
        if (index < 0 || index >= Visible.Length)
        {
            return;
        }

        if (Visible[index] == visible)
        {
            return;
        }

        Visible[index] = visible;
        PlayerPrefs.SetInt(PrefsKey(field), visible ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetAllVisible(bool visible)
    {
        if (!loaded)
        {
            Load();
        }

        for (int i = 0; i < AllFields.Length; i++)
        {
            Visible[i] = visible;
            PlayerPrefs.SetInt(PrefsKey(AllFields[i]), visible ? 1 : 0);
        }

        PlayerPrefs.Save();
    }

    public static void SetBackground(FlightHudBackground background)
    {
        if (!loaded)
        {
            Load();
        }

        Background = ClampBackground((int)background);
        PlayerPrefs.SetInt(BackgroundKey, (int)Background);
        PlayerPrefs.Save();
    }

    public static Color GetBackgroundColor()
    {
        if (!loaded)
        {
            Load();
        }

        switch (Background)
        {
            case FlightHudBackground.Dim:
                return BackgroundDim;
            case FlightHudBackground.Dark:
                return BackgroundDark;
            default:
                return BackgroundTransparent;
        }
    }

    static FlightHudBackground ClampBackground(int value)
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

    static string PrefsKey(FlightHudField field)
    {
        return PrefsPrefix + field;
    }
}
