#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes broken MonoBehaviour slots left on imported KSP VAB meshes
/// (kerbal props, hangar helpers, etc.).
/// </summary>
static class VabSceneCleanup
{
    const string MenuRoot = "Space Program/VAB/";

    [MenuItem(MenuRoot + "Remove Missing Scripts (Active Scene)", false, 0)]
    static void CleanActiveSceneMenu()
    {
        int removed = CleanActiveScene();
        Debug.Log("VAB cleanup: removed " + removed + " missing script slots in active scene.");
    }

    [MenuItem(MenuRoot + "Remove Missing Scripts (Selected Roots)", false, 1)]
    static void CleanSelectionMenu()
    {
        int removed = CleanSelection(applyToSourcePrefabs: false);
        Debug.Log("VAB cleanup: removed " + removed + " missing script slots under selection.");
    }

    [MenuItem(MenuRoot + "Remove Missing Scripts (Selected + Apply to Prefabs)", false, 2)]
    static void CleanSelectionAndApplyPrefabsMenu()
    {
        int removed = CleanSelection(applyToSourcePrefabs: true);
        Debug.Log(
            "VAB cleanup: removed " + removed +
            " missing script slots under selection and applied changes to source prefabs."
        );
    }

    [MenuItem(MenuRoot + "Remove Missing Scripts (Selected Prefab Asset)", false, 3)]
    static void CleanSelectedPrefabAssetMenu()
    {
        int removed = CleanSelectedPrefabAssets();
        Debug.Log(
            "VAB cleanup: removed " + removed +
            " missing script slots from selected prefab asset(s)."
        );
    }

    const string CoveLaunchSitePrefabPath = "Assets/GameObject/Cove_Launch_Site.prefab";

    [MenuItem("Space Program/Environments/Clean Cove Launch Site Prefab", false, 0)]
    static void CleanCoveLaunchSitePrefabMenu()
    {
        int removed = CleanPrefabAssetAtPath(CoveLaunchSitePrefabPath);
        Debug.Log(
            "Environment cleanup: removed " + removed +
            " missing script slots from Cove_Launch_Site.prefab."
        );
    }

    [MenuItem(MenuRoot + "Rebuild Part Pick Colliders (Active Scene)", false, 30)]
    static void RebuildPartPickCollidersMenu()
    {
        int rebuilt = RebuildPartPickCollidersInActiveScene();
        Debug.Log("VAB: rebuilt pick colliders on " + rebuilt + " part root(s) in active scene.");
    }

    [MenuItem(MenuRoot + "Regenerate Pilot Part Nodes", false, 31)]
    static void RegeneratePilotPartNodesMenu()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PartCatalog.PrefabsAssetRoot });
        int updated = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                continue;
            }

            string partId = System.IO.Path.GetFileNameWithoutExtension(prefabPath);

            if (!PartStackProfileLibrary.TryGetProfile(partId, out _))
            {
                continue;
            }

            string category = InferCategoryFromPrefabPath(prefabPath);
            VabPartDefinitionWriter.WriteForPrefab(prefabPath, category);
            updated++;
        }

        PartCatalog.Refresh();
        Debug.Log("VAB: regenerated pilot attachment nodes for " + updated + " partdef file(s).");
    }

    static string InferCategoryFromPrefabPath(string prefabPath)
    {
        string normalized = prefabPath.Replace('\\', '/');
        string[] knownCategories =
        {
            PartDatabase.CategoryCommand,
            PartDatabase.CategoryFuelTank,
            PartDatabase.CategoryEngine,
        };

        for (int i = 0; i < knownCategories.Length; i++)
        {
            string token = "/" + knownCategories[i] + "/";

            if (normalized.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return knownCategories[i];
            }
        }

        return PartDatabase.CategoryMisc;
    }

    static int RebuildPartPickCollidersInActiveScene()
    {
        PartVisualAdapter[] adapters = UnityEngine.Object.FindObjectsByType<PartVisualAdapter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        int rebuilt = 0;

        for (int i = 0; i < adapters.Length; i++)
        {
            if (adapters[i] == null)
            {
                continue;
            }

            VabColliderUtility.EnsurePickColliders(adapters[i].gameObject, rebuild: true);
            rebuilt++;
        }

        return rebuilt;
    }

    [MenuItem(MenuRoot + "Remove Missing Scripts (All Prefab Assets)", false, 20)]
    static void CleanAllPrefabAssetsMenu()
    {
        int removed = CleanAllPrefabAssets();
        Debug.Log(
            "VAB cleanup: removed " + removed +
            " missing script slots from prefab assets under Assets/."
        );
    }

    [MenuItem(MenuRoot + "Remove Missing Scripts (Active Scene)", true)]
    [MenuItem(MenuRoot + "Remove Missing Scripts (Selected Roots)", true)]
    [MenuItem(MenuRoot + "Remove Missing Scripts (Selected + Apply to Prefabs)", true)]
    [MenuItem(MenuRoot + "Remove Missing Scripts (All Prefab Assets)", true)]
    static bool ValidateCleanupMenus()
    {
        return !EditorApplication.isPlaying;
    }

    [MenuItem(MenuRoot + "Remove Missing Scripts (Selected Prefab Asset)", true)]
    static bool ValidateSelectedPrefabAssetMenu()
    {
        if (EditorApplication.isPlaying)
        {
            return false;
        }

        return Selection.objects.Length > 0 &&
            TryGetPrefabAssetPath(Selection.activeObject, out _);
    }

    [MenuItem("Space Program/Environments/Clean Cove Launch Site Prefab", true)]
    static bool ValidateCoveLaunchSitePrefabMenu()
    {
        return !EditorApplication.isPlaying &&
            AssetDatabase.LoadAssetAtPath<GameObject>(CoveLaunchSitePrefabPath) != null;
    }

    static int CleanSelection(bool applyToSourcePrefabs)
    {
        Transform[] selection = Selection.transforms;
        int removed = 0;
        HashSet<GameObject> prefabRootsToApply = new HashSet<GameObject>();

        for (int i = 0; i < selection.Length; i++)
        {
            Transform selected = selection[i];

            if (selected == null)
            {
                continue;
            }

            removed += CleanHierarchy(selected.gameObject);

            if (applyToSourcePrefabs)
            {
                CollectPrefabInstanceRoots(selected.gameObject, prefabRootsToApply);
            }
        }

        if (applyToSourcePrefabs)
        {
            ApplyPrefabInstanceRoots(prefabRootsToApply);
        }

        MarkActiveSceneDirtyIfNeeded(removed);
        return removed;
    }

    static void CollectPrefabInstanceRoots(GameObject root, HashSet<GameObject> prefabRootsToApply)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];

            if (transform == null || !PrefabUtility.IsPartOfPrefabInstance(transform.gameObject))
            {
                continue;
            }

            GameObject outermostRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject);

            if (outermostRoot != null)
            {
                prefabRootsToApply.Add(outermostRoot);
            }
        }
    }

    static void ApplyPrefabInstanceRoots(HashSet<GameObject> prefabRootsToApply)
    {
        foreach (GameObject prefabRoot in prefabRootsToApply)
        {
            if (prefabRoot == null || !PrefabUtility.IsPartOfPrefabInstance(prefabRoot))
            {
                continue;
            }

            PrefabUtility.ApplyPrefabInstance(
                prefabRoot,
                InteractionMode.UserAction
            );
        }
    }

    static int CleanAllPrefabAssets()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int removed = 0;
        int modifiedPrefabs = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                int removedInPrefab = CleanPrefabAssetAtPath(assetPath);

                if (removedInPrefab > 0)
                {
                    removed += removedInPrefab;
                    modifiedPrefabs++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        if (modifiedPrefabs > 0)
        {
            Debug.Log("VAB cleanup: modified " + modifiedPrefabs + " prefab assets.");
        }

        return removed;
    }

    static int CleanSelectedPrefabAssets()
    {
        UnityEngine.Object[] selectedObjects = Selection.objects;
        int removed = 0;
        int modifiedPrefabs = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                if (!TryGetPrefabAssetPath(selectedObjects[i], out string assetPath))
                {
                    continue;
                }

                int removedInPrefab = CleanPrefabAssetAtPath(assetPath);

                if (removedInPrefab > 0)
                {
                    removed += removedInPrefab;
                    modifiedPrefabs++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        if (modifiedPrefabs > 0)
        {
            Debug.Log("VAB cleanup: modified " + modifiedPrefabs + " selected prefab asset(s).");
        }

        return removed;
    }

    static int CleanPrefabAssetAtPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
        int removedInPrefab = CleanHierarchy(prefabRoot);

        if (removedInPrefab > 0)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
        }

        PrefabUtility.UnloadPrefabContents(prefabRoot);
        return removedInPrefab;
    }

    static bool TryGetPrefabAssetPath(UnityEngine.Object selectedObject, out string assetPath)
    {
        assetPath = AssetDatabase.GetAssetPath(selectedObject);

        if (string.IsNullOrEmpty(assetPath) ||
            !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            assetPath = null;
            return false;
        }

        return true;
    }

    internal static int CleanActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        int removed = 0;

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
            {
                removed += CleanHierarchy(roots[i]);
            }
        }

        MarkSceneDirtyIfNeeded(scene, removed);
        return removed;
    }

    internal static int CleanVabEnvironment()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        int removed = 0;

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && !IsSessionHost(roots[i]))
            {
                removed += CleanHierarchy(roots[i]);
            }
        }

        if (!EditorApplication.isPlayingOrWillChangePlaymode && removed > 0)
        {
            MarkSceneDirtyIfNeeded(scene, removed);
        }

        return removed;
    }

    static bool IsSessionHost(GameObject candidate)
    {
        return candidate.GetComponent<SimulationRunner>() != null ||
            candidate.GetComponent<GameSessionBootstrap>() != null;
    }

    static int CleanHierarchy(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int removed = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                    transforms[i].gameObject
                );
            }
        }

        return removed;
    }

    static void MarkActiveSceneDirtyIfNeeded(int removedCount)
    {
        if (removedCount <= 0)
        {
            return;
        }

        MarkSceneDirtyIfNeeded(SceneManager.GetActiveScene(), removedCount);
    }

    static void MarkSceneDirtyIfNeeded(Scene scene, int removedCount)
    {
        if (removedCount <= 0 || !scene.IsValid())
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
