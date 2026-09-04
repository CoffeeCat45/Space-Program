#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Asset paths for per-part folders under <c>Assets/Prefabs/Parts/{Category}/{partId}/</c>.
/// </summary>
static class ReStockPartPaths
{
    internal const string PrefabsAssetRoot = "Assets/Prefabs/Parts";
    internal const string MeshesAssetRoot = "Assets/Models/Parts/Meshes";
    internal const string VisualPrefabSuffix = ".visual";

    internal static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
    }

    internal static string GetPartImportConfigDirectory()
    {
        return Path.Combine(GetRepoRoot(), "Pipeline", "Parts");
    }

    internal static string GetPartImportConfigPath(string partId)
    {
        return Path.Combine(GetPartImportConfigDirectory(), partId + ".partimport.json");
    }

    internal static string GetPartPrefabFolderAssetPath(string category, string partId)
    {
        return $"{PrefabsAssetRoot}/{category}/{partId}";
    }

    internal static string GetContainerPrefabAssetPath(string category, string partId)
    {
        return $"{GetPartPrefabFolderAssetPath(category, partId)}/{partId}.prefab";
    }

    internal static string GetVisualPrefabAssetPath(string category, string partId)
    {
        return $"{GetPartPrefabFolderAssetPath(category, partId)}/{partId}{VisualPrefabSuffix}.prefab";
    }

    internal static string GetPartDefAssetPath(string category, string partId)
    {
        return $"{GetPartPrefabFolderAssetPath(category, partId)}/{partId}.partdef.json";
    }

    internal static string GetMeshFolderAssetPath(string category, string partId)
    {
        return $"{MeshesAssetRoot}/{category}/{partId}";
    }

    internal static string GetSingleModelAssetPath(string category, string partId)
    {
        return $"{GetMeshFolderAssetPath(category, partId)}/{partId}.obj";
    }

    internal static string GetManifestAssetPath(string category, string partId)
    {
        return $"{GetMeshFolderAssetPath(category, partId)}/{partId}.manifest.json";
    }

    internal static string GetSubmeshAssetPath(string category, string partId, string meshName)
    {
        string safeName = meshName.Replace(" ", "_");
        return $"{GetMeshFolderAssetPath(category, partId)}/{safeName}.obj";
    }

    internal static bool IsVisualPrefabPath(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            return false;
        }

        string fileName = Path.GetFileNameWithoutExtension(prefabPath);
        return fileName.EndsWith(VisualPrefabSuffix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// VAB container prefab: <c>.../{Category}/{partId}/{partId}.prefab</c> (not .visual).
    /// </summary>
    internal static bool IsContainerPrefabPath(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath) || IsVisualPrefabPath(prefabPath))
        {
            return false;
        }

        string normalized = prefabPath.Replace('\\', '/');
        string[] categories =
        {
            PartDatabase.CategoryCommand,
            PartDatabase.CategoryFuelTank,
            PartDatabase.CategoryEngine,
        };

        for (int i = 0; i < categories.Length; i++)
        {
            string token = "/" + categories[i] + "/";

            if (normalized.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            string partId = Path.GetFileNameWithoutExtension(normalized);
            string parentFolder = Path.GetFileName(Path.GetDirectoryName(normalized));

            if (partId.Equals(parentFolder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryParseContainerPrefabPath(string prefabPath, out string category, out string partId)
    {
        category = string.Empty;
        partId = string.Empty;

        if (!IsContainerPrefabPath(prefabPath))
        {
            return false;
        }

        string normalized = prefabPath.Replace('\\', '/');
        partId = Path.GetFileNameWithoutExtension(normalized);
        string parentFolder = Path.GetFileName(Path.GetDirectoryName(normalized));
        string categoryFolder = Path.GetFileName(Path.GetDirectoryName(parentFolder));
        category = categoryFolder;
        return !string.IsNullOrWhiteSpace(partId) && !string.IsNullOrWhiteSpace(category);
    }

    internal static void EnsurePartPrefabFolder(string category, string partId)
    {
        string folder = GetPartPrefabFolderAssetPath(category, partId);

        if (!AssetDatabase.IsValidFolder(folder))
        {
            string categoryPath = $"{PrefabsAssetRoot}/{category}";

            if (!AssetDatabase.IsValidFolder(categoryPath))
            {
                AssetDatabase.CreateFolder(PrefabsAssetRoot, category);
            }

            AssetDatabase.CreateFolder(categoryPath, partId);
        }
    }
}
#endif
