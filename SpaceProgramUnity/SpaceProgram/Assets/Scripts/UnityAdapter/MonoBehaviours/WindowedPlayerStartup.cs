using UnityEngine;

static class WindowedPlayerStartup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureWindowedMode()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        if (Screen.fullScreenMode != FullScreenMode.Windowed)
        {
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
        }
#endif
    }
}
