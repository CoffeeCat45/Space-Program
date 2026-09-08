/// <summary>
/// Persistent in-flight view toggles (<c>settings.json</c>).
/// </summary>
public static class GameViewSettings
{
    public static bool ShowTargetDirectionLine { get; private set; } = true;
    public static bool ShowLodLoadingScreen { get; private set; }

    public static void Load()
    {
        GameSettingsStore.EnsureLoaded();
        ShowTargetDirectionLine = GameSettingsStore.Data.play.showTargetDirectionLine;
        ShowLodLoadingScreen = GameSettingsStore.Data.play.showLodLoadingScreen;
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
}
