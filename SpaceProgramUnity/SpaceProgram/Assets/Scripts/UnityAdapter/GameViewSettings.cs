using UnityEngine;

/// <summary>
/// Persistent in-flight view toggles (PlayerPrefs).
/// </summary>
public static class GameViewSettings
{
    const string ShowTargetDirectionLineKey = "SpaceProgram.View.ShowTargetDirectionLine";

    public static bool ShowTargetDirectionLine { get; private set; } = true;

    public static void Load()
    {
        ShowTargetDirectionLine =
            PlayerPrefs.GetInt(ShowTargetDirectionLineKey, 1) == 1;
    }

    public static void SetShowTargetDirectionLine(bool enabled)
    {
        ShowTargetDirectionLine = enabled;
        PlayerPrefs.SetInt(ShowTargetDirectionLineKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
