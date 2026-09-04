using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-name helpers for returning from the VAB hangar to a flight bootstrap scene.
/// When no separate bootstrap scene exists in Build Settings, flight runs in the same scene
/// and the hangar is hidden instead of unloaded.
/// </summary>
static class FlightSessionSceneUtility
{
    static readonly string[] MainMenuSceneLoadOrder =
    {
        "MenuBootstrap",
        "MenuScene",
        "PlaybackScene",
    };

    static readonly string[] FlightSceneLoadOrder =
    {
        "PlaybackScene",
        "MenuScene",
        "MenuBootstrap",
    };

    public static bool IsFlightBootstrapSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        for (int i = 0; i < FlightSceneLoadOrder.Length; i++)
        {
            if (string.Equals(sceneName, FlightSceneLoadOrder[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsActiveSceneFlightBootstrap()
    {
        return IsFlightBootstrapSceneName(SceneManager.GetActiveScene().name);
    }

    public static bool CanLoadBootstrapScene()
    {
        return CanLoadSceneFromOrder(FlightSceneLoadOrder);
    }

    public static bool CanLoadMainMenuBootstrapScene()
    {
        return CanLoadSceneFromOrder(MainMenuSceneLoadOrder);
    }

    static bool CanLoadSceneFromOrder(string[] sceneLoadOrder)
    {
        for (int i = 0; i < sceneLoadOrder.Length; i++)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneLoadOrder[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryLoadFlightScene()
    {
        return TryLoadSceneFromOrder(FlightSceneLoadOrder);
    }

    /// <summary>Empty menu / bootstrap scene for cold start and return-to-menu.</summary>
    public static bool TryLoadMainMenuBootstrapScene()
    {
        return TryLoadSceneFromOrder(MainMenuSceneLoadOrder);
    }

    static bool TryLoadSceneFromOrder(string[] sceneLoadOrder)
    {
        for (int i = 0; i < sceneLoadOrder.Length; i++)
        {
            string sceneName = sceneLoadOrder[i];

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                continue;
            }

            SceneManager.LoadScene(sceneName);
            return true;
        }

        return false;
    }
}
