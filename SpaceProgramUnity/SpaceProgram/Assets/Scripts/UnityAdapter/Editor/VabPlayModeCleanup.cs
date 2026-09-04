#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

/// <summary>
/// Strips missing-script slots from imported VAB props when entering Play mode.
/// For a permanent fix, run Space Program → VAB → Remove Missing Scripts and save the scene.
/// </summary>
[InitializeOnLoad]
static class VabPlayModeCleanup
{
    static VabPlayModeCleanup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.name.Equals("VabScene", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        int removed = VabSceneCleanup.CleanVabEnvironment();

        if (removed > 0)
        {
            Debug.Log(
                "VAB cleanup: removed " + removed +
                " missing script slots from imported hangar props. " +
                "Save the scene after Space Program → VAB → Remove Missing Scripts."
            );
        }
    }
}
#endif
