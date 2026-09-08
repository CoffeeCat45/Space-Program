using UnityEngine;

static class WindowedPlayerStartup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySavedDisplayMode()
    {
        GameSettingsStore.EnsureLoaded();
        VabGraphicsManager.ApplyTargetFrameRate(GameSettingsStore.Data.graphics.targetFrameRate);
#if UNITY_STANDALONE && !UNITY_EDITOR
        VabGraphicsManager.ApplyDisplayMode(GameSettingsStore.Data.graphics.displayMode);
#endif
    }
}
