/// <summary>
/// Persistent in-flight view toggles (<c>settings.json</c>).
/// </summary>
public static class GameViewSettings
{
    public static bool ShowTargetDirectionLine { get; private set; } = true;

    public static void Load()
    {
        GameSettingsStore.EnsureLoaded();
        ShowTargetDirectionLine = GameSettingsStore.Data.play.showTargetDirectionLine;
    }

    public static void SetShowTargetDirectionLine(bool enabled)
    {
        ShowTargetDirectionLine = enabled;
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.play.showTargetDirectionLine = enabled;
        GameSettingsStore.SaveToDisk();
    }
}
