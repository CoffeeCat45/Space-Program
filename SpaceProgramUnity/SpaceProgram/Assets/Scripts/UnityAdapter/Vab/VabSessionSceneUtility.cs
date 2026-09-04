using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-name helpers for VAB entry (VabScene, VABScene, vablvl3, …).
/// </summary>
static class VabSessionSceneUtility
{
    static readonly string[] VabSceneLoadOrder =
    {
        "VabScene",
        "VABScene",
        "vablvl3",
        "vablvl",
    };

    static readonly string[] VabEnvironmentSceneHints =
    {
        "VAB",
        "Hangar",
        "SpaceCenter",
        "vablvl",
    };

    public static bool IsVabSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        for (int i = 0; i < VabSceneLoadOrder.Length; i++)
        {
            if (string.Equals(sceneName, VabSceneLoadOrder[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return MatchesVabEnvironmentSceneName(sceneName);
    }

    public static bool MatchesVabEnvironmentSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        for (int i = 0; i < VabEnvironmentSceneHints.Length; i++)
        {
            if (sceneName.IndexOf(VabEnvironmentSceneHints[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsActiveSceneVab()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (IsVabSceneName(activeScene.name))
        {
            return true;
        }

        return UnityEngine.Object.FindFirstObjectByType<VabSceneEnvironment>() != null;
    }

    public static bool TryLoadVabScene()
    {
        return TryLoadVabScene(logErrorIfMissing: true);
    }

    public static bool CanLoadAnyVabScene()
    {
        for (int i = 0; i < VabSceneLoadOrder.Length; i++)
        {
            if (Application.CanStreamedLevelBeLoaded(VabSceneLoadOrder[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryLoadVabScene(bool logErrorIfMissing)
    {
        for (int i = 0; i < VabSceneLoadOrder.Length; i++)
        {
            string sceneName = VabSceneLoadOrder[i];

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                continue;
            }

            SceneManager.LoadScene(sceneName);
            return true;
        }

        if (logErrorIfMissing)
        {
            Debug.LogError(
                "VAB: cannot load hangar scene. Add one of these scenes to Build Settings: " +
                string.Join(", ", VabSceneLoadOrder)
            );
        }

        return false;
    }
}
