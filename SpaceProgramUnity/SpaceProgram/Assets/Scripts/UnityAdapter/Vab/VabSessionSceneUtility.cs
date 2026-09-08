using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-name helpers for VAB entry. Empty host <c>VabScene</c> plus additive hangar
/// <c>VABlvl3</c> (KSP interior). Loading only the host leaves the editor in empty space.
/// </summary>
public static class VabSessionSceneUtility
{
    static readonly string[] VabHostSceneLoadOrder =
    {
        "VabScene",
        "VABScene",
    };

    static readonly string[] VabHangarSceneLoadOrder =
    {
        "VABlvl3",
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

    public static bool IsVabHostSceneName(string sceneName)
    {
        return MatchesNameList(sceneName, VabHostSceneLoadOrder);
    }

    public static bool IsVabHangarSceneName(string sceneName)
    {
        return MatchesNameList(sceneName, VabHangarSceneLoadOrder) ||
            MatchesVabEnvironmentSceneName(sceneName);
    }

    public static bool IsVabSceneName(string sceneName)
    {
        return IsVabHostSceneName(sceneName) || IsVabHangarSceneName(sceneName);
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
        return CanLoadAnyScene(VabHostSceneLoadOrder) ||
            CanLoadAnyScene(VabHangarSceneLoadOrder);
    }

    public static bool TryLoadVabScene(bool logErrorIfMissing)
    {
        bool hostPresent = IsAnyListedSceneLoaded(VabHostSceneLoadOrder);
        bool hangarPresent = IsHangarContentPresent();

        if (!hostPresent && !hangarPresent)
        {
            if (!TryLoadSingleScene(VabHostSceneLoadOrder))
            {
                if (TryLoadSingleScene(VabHangarSceneLoadOrder))
                {
                    return true;
                }

                if (logErrorIfMissing)
                {
                    Debug.LogError(
                        "VAB: cannot load editor scenes. Add VabScene and VABlvl3 to Build Settings."
                    );
                }

                return false;
            }
        }

        if (hangarPresent || TryLoadHangarAdditive())
        {
            return true;
        }

        if (logErrorIfMissing)
        {
            Debug.LogError(
                "VAB: editor host loaded without hangar. Add VABlvl3 to Build Settings " +
                "(Assets/Scenery/KSC/Interiors3D/VABlvl3.unity) and rebuild."
            );
        }

        return true;
    }

    static bool TryLoadHangarAdditive()
    {
        if (IsHangarContentPresent())
        {
            TrySetActiveHangarScene();
            return true;
        }

        for (int i = 0; i < VabHangarSceneLoadOrder.Length; i++)
        {
            string sceneName = VabHangarSceneLoadOrder[i];

            if (!Application.CanStreamedLevelBeLoaded(sceneName) || IsSceneLoaded(sceneName))
            {
                continue;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            TrySetActiveHangarScene();
            return IsHangarContentPresent() || IsSceneLoaded(sceneName);
        }

        return false;
    }

    static bool IsHangarContentPresent()
    {
        if (UnityEngine.Object.FindFirstObjectByType<VabSceneEnvironment>(
                FindObjectsInactive.Include) != null)
        {
            return true;
        }

        return IsAnyListedSceneLoaded(VabHangarSceneLoadOrder);
    }

    static bool TryLoadSingleScene(string[] sceneNames)
    {
        for (int i = 0; i < sceneNames.Length; i++)
        {
            string sceneName = sceneNames[i];

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                continue;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return true;
        }

        return false;
    }

    static bool CanLoadAnyScene(string[] sceneNames)
    {
        for (int i = 0; i < sceneNames.Length; i++)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneNames[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool MatchesNameList(string sceneName, string[] names)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || names == null)
        {
            return false;
        }

        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(sceneName, names[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsAnyListedSceneLoaded(string[] sceneNames)
    {
        for (int i = 0; i < sceneNames.Length; i++)
        {
            if (IsSceneLoaded(sceneNames[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsSceneLoaded(string sceneName)
    {
        return FindLoadedScene(sceneName).IsValid();
    }

    static Scene FindLoadedScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return default;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase)
            )
            {
                return scene;
            }
        }

        return default;
    }

    static void TrySetActiveHangarScene()
    {
        for (int i = 0; i < VabHangarSceneLoadOrder.Length; i++)
        {
            Scene hangar = FindLoadedScene(VabHangarSceneLoadOrder[i]);

            if (!hangar.IsValid() || !hangar.isLoaded)
            {
                continue;
            }

            SceneManager.SetActiveScene(hangar);
            return;
        }
    }
}
