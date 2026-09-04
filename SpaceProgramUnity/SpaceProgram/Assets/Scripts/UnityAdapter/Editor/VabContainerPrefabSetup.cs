#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies ReStock URP materials to hand-authored container prefabs under Assets/Prefabs/Parts/.
/// </summary>
static class VabContainerPrefabSetup
{
    const string MenuPath = "Space Program/VAB/Apply Materials to Container Prefabs";

    [MenuItem(MenuPath, false, 32)]
    static void ApplyMaterialsMenu()
    {
        int updated = ApplyMaterialsToAllContainerPrefabs();
        AssetDatabase.SaveAssets();
        Debug.Log("VAB: applied materials to " + updated + " container prefab(s).");
    }

    internal static int ApplyMaterialsToAllContainerPrefabs()
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

            if (
                string.IsNullOrWhiteSpace(prefabPath) ||
                !PartCatalog.IsContainerPrefabPath(prefabPath)
            )
            {
                continue;
            }

            if (ApplyMaterialsToPrefabAsset(prefabPath))
            {
                updated++;
            }
        }

        return updated;
    }

    static bool ApplyMaterialsToPrefabAsset(string prefabAssetPath)
    {
        string partId = System.IO.Path.GetFileNameWithoutExtension(prefabAssetPath);
        string modelAssetPath = ResolveModelAssetPath(partId);

        if (string.IsNullOrWhiteSpace(modelAssetPath))
        {
            Debug.LogWarning("VAB: no model path for prefab '" + prefabAssetPath + "'.");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabAssetPath);

        if (prefabRoot == null)
        {
            return false;
        }

        ReStockImportPipeline.ApplyContainerPrefabMaterials(prefabRoot, modelAssetPath);

        ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition(partId);

        if (definition == null || !definition.SplitSubmeshes)
        {
            TryCopyMaterialsFromLegacyPrefab(partId, prefabRoot);
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabAssetPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        return true;
    }

    static string ResolveModelAssetPath(string partId)
    {
        ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition(partId);

        if (definition != null)
        {
            if (definition.SplitSubmeshes)
            {
                return ReStockBatchParts.GetPartFolderAssetPath(definition);
            }

            return ReStockBatchParts.GetSingleModelAssetPath(definition);
        }

        string[] categoryFolders =
        {
            "Command",
            "FuelTank",
            "Engine",
        };

        string[] extensions = { ".obj", ".fbx", ".glb" };

        for (int i = 0; i < categoryFolders.Length; i++)
        {
            for (int extensionIndex = 0; extensionIndex < extensions.Length; extensionIndex++)
            {
                string candidate =
                    $"{PartCatalog.MeshesAssetRoot}/{categoryFolders[i]}/{partId}/{partId}{extensions[extensionIndex]}";

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidate) != null)
                {
                    return candidate;
                }
            }
        }

        return string.Empty;
    }

    static void TryCopyMaterialsFromLegacyPrefab(string partId, GameObject containerRoot)
    {
        if (containerRoot == null || string.IsNullOrWhiteSpace(partId))
        {
            return;
        }

        string legacyPrefabPath = ReStockBatchParts.FindDefinition(partId) != null ?
            ReStockPartPaths.GetVisualPrefabAssetPath(
                ReStockBatchParts.FindDefinition(partId).Category,
                partId
            ) :
            $"{PartCatalog.PrefabsAssetRoot}/{partId}.prefab";
        GameObject legacyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(legacyPrefabPath);

        if (legacyPrefab == null)
        {
            return;
        }

        Dictionary<string, Material[]> materialsByMeshGuid =
            new Dictionary<string, Material[]>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Material[]> materialsByMeshKey =
            new Dictionary<string, Material[]>(StringComparer.OrdinalIgnoreCase);
        Renderer[] legacyRenderers = legacyPrefab.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < legacyRenderers.Length; i++)
        {
            Renderer legacyRenderer = legacyRenderers[i];

            if (legacyRenderer == null || legacyRenderer.sharedMaterials.Length == 0)
            {
                continue;
            }

            if (
                ReStockImportPipeline.TryGetRendererMeshAssetGuid(legacyRenderer, out string meshGuid) &&
                !materialsByMeshGuid.ContainsKey(meshGuid)
            )
            {
                materialsByMeshGuid[meshGuid] = legacyRenderer.sharedMaterials;
            }

            string meshKey = GetRendererMaterialKey(legacyRenderer, partId);

            if (!string.IsNullOrWhiteSpace(meshKey) && !materialsByMeshKey.ContainsKey(meshKey))
            {
                materialsByMeshKey[meshKey] = legacyRenderer.sharedMaterials;
            }
        }

        if (materialsByMeshGuid.Count == 0 && materialsByMeshKey.Count == 0)
        {
            return;
        }

        Renderer[] containerRenderers = containerRoot.GetComponentsInChildren<Renderer>(true);
        int copied = 0;

        for (int i = 0; i < containerRenderers.Length; i++)
        {
            Renderer containerRenderer = containerRenderers[i];

            if (
                containerRenderer == null ||
                containerRenderer.GetComponent<AttachmentNodeMarker>() != null
            )
            {
                continue;
            }

            Material[] copiedMaterials = null;

            if (
                ReStockImportPipeline.TryGetRendererMeshAssetGuid(containerRenderer, out string meshGuid) &&
                materialsByMeshGuid.TryGetValue(meshGuid, out copiedMaterials)
            )
            {
                containerRenderer.sharedMaterials = copiedMaterials;
                copied++;
                continue;
            }

            string meshKey = GetRendererMaterialKey(containerRenderer, partId);

            if (
                !string.IsNullOrWhiteSpace(meshKey) &&
                materialsByMeshKey.TryGetValue(meshKey, out copiedMaterials)
            )
            {
                containerRenderer.sharedMaterials = copiedMaterials;
                copied++;
            }
        }

        if (copied > 0)
        {
            Debug.Log($"VAB: copied legacy materials to {copied} renderer(s) on {partId}.");
        }
    }

    static string GetRendererMaterialKey(Renderer renderer, string partId)
    {
        if (renderer == null)
        {
            return string.Empty;
        }

        return ReStockImportPipeline.GetRendererPilotMeshKey(renderer, partId);
    }
}
#endif
