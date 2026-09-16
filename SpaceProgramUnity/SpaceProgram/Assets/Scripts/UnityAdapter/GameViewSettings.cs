public enum FlightCameraRigMode
{
    Horizon = 0,
    Vessel = 1
}

/// <summary>
/// Persistent in-flight view toggles (<c>settings.json</c>).
/// </summary>
public static class GameViewSettings
{
    public static bool ShowTargetDirectionLine { get; private set; } = true;
    public static bool ShowLodLoadingScreen { get; private set; }
    public static FlightCameraRigMode FlightCameraRig { get; private set; } =
        FlightCameraRigMode.Horizon;

    public static void Load()
    {
        GameSettingsStore.EnsureLoaded();
        ShowTargetDirectionLine = GameSettingsStore.Data.play.showTargetDirectionLine;
        ShowLodLoadingScreen = GameSettingsStore.Data.play.showLodLoadingScreen;
        FlightCameraRig = ParseFlightCameraRig(GameSettingsStore.Data.play.flightCameraRig);
        FlightKeyBindings.Reload();
    }

    public static void SetShowTargetDirectionLine(bool enabled)
    {
        ShowTargetDirectionLine = enabled;
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.play.showTargetDirectionLine = enabled;
        GameSettingsStore.SaveToDisk();
    }

    public static void SetShowLodLoadingScreen(bool enabled)
    {
        ShowLodLoadingScreen = enabled;
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.play.showLodLoadingScreen = enabled;
        GameSettingsStore.SaveToDisk();
    }

    public static void SetFlightCameraRig(FlightCameraRigMode mode)
    {
        FlightCameraRig = mode;
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.play.flightCameraRig = (int)mode;
        GameSettingsStore.SaveToDisk();
    }

    public static FlightCameraRigMode CycleFlightCameraRig()
    {
        FlightCameraRigMode next = FlightCameraRig == FlightCameraRigMode.Horizon ?
            FlightCameraRigMode.Vessel :
            FlightCameraRigMode.Horizon;
        SetFlightCameraRig(next);
        return next;
    }

    static FlightCameraRigMode ParseFlightCameraRig(int stored)
    {
        return stored == (int)FlightCameraRigMode.Vessel ?
            FlightCameraRigMode.Vessel :
            FlightCameraRigMode.Horizon;
    }
}
