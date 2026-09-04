using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes VAB/editor scene artifacts when entering flight so only the active session remains.
/// </summary>
static class FlightSceneIsolation
{
    static readonly string[] DestroyNameTokens =
    {
        "VAB",
        "MainSidebarUi",
        "SceneLogic",
    };

    static readonly string[] SessionDestroyNameTokens =
    {
        "MainSidebarUi",
        "SceneLogic",
    };

    public static void PurgeEditorArtifacts(SimulationRunner sessionRunner)
    {
        if (sessionRunner == null)
        {
            return;
        }

        Transform sessionRoot = sessionRunner.transform;
        DetachSessionFromVabHierarchy(sessionRoot);
        TryUnloadExtraVabScenes();
        DestroyMatchingRoots(sessionRoot, DestroyNameTokens);
        DestroyOrphanedNameMatches(sessionRoot, DestroyNameTokens);
        PurgeVabWorkspaceParts(sessionRunner);
        FlightCameraBootstrap.EnsureMainCamera(sessionRoot);
    }

    /// <summary>
    /// Removes editor UI/session objects but keeps the imported hangar geometry so VAB can be reopened.
    /// </summary>
    public static void PurgeEditorSessionArtifacts(SimulationRunner sessionRunner)
    {
        if (sessionRunner == null)
        {
            return;
        }

        Transform sessionRoot = sessionRunner.transform;
        DetachSessionFromVabHierarchy(sessionRoot);
        DestroyMatchingRoots(sessionRoot, SessionDestroyNameTokens);
        DestroyOrphanedNameMatches(sessionRoot, SessionDestroyNameTokens);
        PurgeVabWorkspaceParts(sessionRunner);
        FlightCameraBootstrap.EnsureMainCamera(sessionRoot);
    }

    static void DetachSessionFromVabHierarchy(Transform sessionRoot)
    {
        if (sessionRoot == null || sessionRoot.parent == null)
        {
            return;
        }

        Transform parent = sessionRoot.parent;

        if (!ShouldDestroyByName(parent.name, DestroyNameTokens))
        {
            return;
        }

        sessionRoot.SetParent(null, true);
    }

    static void TryUnloadExtraVabScenes()
    {
        if (SceneManager.sceneCount <= 1)
        {
            return;
        }

        for (int sceneIndex = SceneManager.sceneCount - 1; sceneIndex >= 0; sceneIndex--)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.isLoaded || !scene.IsValid())
            {
                continue;
            }

            if (!VabSessionSceneUtility.IsVabSceneName(scene.name))
            {
                continue;
            }

            if (CountOtherLoadedScenes(scene) < 1)
            {
                continue;
            }

            SceneManager.UnloadSceneAsync(scene);
        }
    }

    static int CountOtherLoadedScenes(Scene excludedScene)
    {
        int count = 0;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.isLoaded || !scene.IsValid())
            {
                continue;
            }

            if (scene.handle == excludedScene.handle)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    static void PurgeVabWorkspaceParts(SimulationRunner sessionRunner)
    {
        Transform activeShip = sessionRunner != null ?
            sessionRunner.GetShipVisualTransform() :
            null;

        PartVisualAdapter[] adapters = UnityEngine.Object.FindObjectsByType<PartVisualAdapter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < adapters.Length; i++)
        {
            PartVisualAdapter adapter = adapters[i];

            if (adapter == null)
            {
                continue;
            }

            Transform adapterTransform = adapter.transform;

            if (
                activeShip != null &&
                (adapterTransform == activeShip || adapterTransform.IsChildOf(activeShip))
            )
            {
                continue;
            }

            if (IsProtected(adapterTransform, sessionRunner.transform))
            {
                continue;
            }

            UnityEngine.Object.Destroy(adapter.gameObject);
        }
    }

    static void DestroyMatchingRoots(Transform sessionRoot, string[] nameTokens)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                TryDestroyEditorRoot(roots[i], sessionRoot, nameTokens);
            }
        }
    }

    static void DestroyOrphanedNameMatches(Transform sessionRoot, string[] nameTokens)
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];

            if (candidate == null || IsProtected(candidate.transform, sessionRoot))
            {
                continue;
            }

            if (IsVabEnvironmentRoot(candidate))
            {
                continue;
            }

            if (!ShouldDestroyByName(candidate.name, nameTokens))
            {
                continue;
            }

            if (candidate.transform.parent != null)
            {
                continue;
            }

            UnityEngine.Object.Destroy(candidate);
        }
    }

    static void TryDestroyEditorRoot(
        GameObject root,
        Transform sessionRoot,
        string[] nameTokens
    )
    {
        if (
            root == null ||
            IsProtected(root.transform, sessionRoot) ||
            IsVabEnvironmentRoot(root)
        )
        {
            return;
        }

        if (!ShouldDestroyByName(root.name, nameTokens))
        {
            return;
        }

        UnityEngine.Object.Destroy(root);
    }

    static bool IsVabEnvironmentRoot(GameObject root)
    {
        return root != null && root.GetComponent<VabSceneEnvironment>() != null;
    }

    static bool ShouldDestroyByName(string objectName, string[] nameTokens)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        for (int i = 0; i < nameTokens.Length; i++)
        {
            if (objectName.IndexOf(nameTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        if (nameTokens == SessionDestroyNameTokens)
        {
            return false;
        }

        return objectName.IndexOf("VabEnvironment", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("VabRoot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("vablvl", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(objectName, "VABlvl3", StringComparison.OrdinalIgnoreCase);
    }

    public static void PurgeStrayVesselBlocks(SimulationRunner sessionRunner)
    {
        if (sessionRunner == null)
        {
            return;
        }

        Transform activeShip = sessionRunner.GetShipVisualTransform();

        if (activeShip == null)
        {
            return;
        }

        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];

            if (
                candidate == null ||
                candidate == activeShip.gameObject ||
                candidate.transform.IsChildOf(activeShip)
            )
            {
                continue;
            }

            if (
                !candidate.name.StartsWith("Block_", StringComparison.Ordinal) &&
                !candidate.name.StartsWith("block_", StringComparison.Ordinal)
            )
            {
                continue;
            }

            if (IsProtected(candidate.transform, sessionRunner.transform))
            {
                continue;
            }

            UnityEngine.Object.Destroy(candidate);
        }
    }

    static bool IsProtected(Transform candidate, Transform sessionRoot)
    {
        if (candidate == null)
        {
            return true;
        }

        if (sessionRoot != null && (candidate == sessionRoot || candidate.IsChildOf(sessionRoot)))
        {
            return true;
        }

        if (candidate.GetComponentInParent<SimulationRunner>(true) != null)
        {
            return true;
        }

        if (candidate.GetComponentInParent<GameSessionBootstrap>(true) != null)
        {
            return true;
        }

        return false;
    }

}
