#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ReStock part definitions loaded from <c>Pipeline/Parts/*.partimport.json</c> + Blender manifest.
/// </summary>
static class ReStockBatchParts
{
    internal const string MeshesAssetRoot = "Assets/Models/Parts/Meshes";

    internal enum MeshRole
    {
        Primary,
        Attachment,
        Skin,
    }

    [Serializable]
    internal class ManifestMeshEntry
    {
        public string name;
        public string role;
        public bool defaultEnabled;
    }

    [Serializable]
    internal class PartManifest
    {
        public string partId;
        public string category;
        public bool splitSubmeshes;
        public ManifestMeshEntry[] meshes;
    }

    internal sealed class PartDefinition
    {
        public string PartId;
        public string Category;
        public bool SplitSubmeshes;
        public string[] SubmeshNames = Array.Empty<string>();
        public HashSet<string> KeepMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PrimaryMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AttachmentMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> EnabledMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool LockEnabledMeshes;
        public string PrimaryMaterialOverride;
        public string ConfigFilePath;
        public Dictionary<string, string> MaterialOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SlotMaterialOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    static PartDefinition[] cachedDefinitions;

    internal static PartDefinition[] Definitions
    {
        get
        {
            if (cachedDefinitions == null)
            {
                RefreshDefinitions();
            }

            return cachedDefinitions;
        }
    }

    internal static void RefreshDefinitions()
    {
        cachedDefinitions = ReStockPartImportConfig.LoadAllDefinitions().ToArray();
    }

    internal static void InvalidateCache()
    {
        cachedDefinitions = null;
    }

    internal static PartDefinition FindDefinition(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
        {
            return null;
        }

        PartDefinition[] definitions = Definitions;

        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].PartId.Equals(partId, StringComparison.OrdinalIgnoreCase))
            {
                return definitions[i];
            }
        }

        ReStockPartImportConfig.PartImportFile config =
            ReStockPartImportConfig.LoadConfigForPart(partId);

        if (config != null)
        {
            return ReStockPartImportConfig.ToDefinition(config);
        }

        return TryCreateDefinitionFromManifest(partId);
    }

    static PartDefinition TryCreateDefinitionFromManifest(string partId)
    {
        string[] categories =
        {
            PartDatabase.CategoryCommand,
            PartDatabase.CategoryFuelTank,
            PartDatabase.CategoryEngine,
        };

        for (int i = 0; i < categories.Length; i++)
        {
            string manifestPath = ReStockPartPaths.GetManifestAssetPath(categories[i], partId);
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), manifestPath);

            if (!File.Exists(fullPath))
            {
                continue;
            }

            PartDefinition definition = new PartDefinition
            {
                PartId = partId,
                Category = categories[i],
            };
            MergeManifest(definition);

            if (definition.SubmeshNames.Length > 0)
            {
                return definition;
            }
        }

        return null;
    }

    internal static void MergeManifest(PartDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        string manifestPath = ResolveManifestAssetPath(definition);

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return;
        }

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), manifestPath);

        if (!File.Exists(fullPath))
        {
            return;
        }

        string json = File.ReadAllText(fullPath);
        PartManifest manifest = JsonUtility.FromJson<PartManifest>(json);

        if (manifest == null || manifest.meshes == null || manifest.meshes.Length == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(manifest.category))
        {
            definition.Category = manifest.category;
        }

        definition.SplitSubmeshes = manifest.splitSubmeshes;

        List<string> submeshNames = new List<string>();
        HashSet<string> primaryMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> attachmentMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> enabledMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < manifest.meshes.Length; i++)
        {
            ManifestMeshEntry entry = manifest.meshes[i];

            if (string.IsNullOrWhiteSpace(entry.name))
            {
                continue;
            }

            submeshNames.Add(entry.name);

            if (entry.defaultEnabled)
            {
                enabledMeshes.Add(entry.name);
            }

            MeshRole role = ParseRole(entry.role);

            if (role == MeshRole.Attachment || role == MeshRole.Skin)
            {
                attachmentMeshes.Add(entry.name);
            }
            else
            {
                primaryMeshes.Add(entry.name);
            }
        }

        if (submeshNames.Count > 0)
        {
            definition.SubmeshNames = submeshNames.ToArray();
        }

        if (primaryMeshes.Count > 0)
        {
            definition.PrimaryMeshes = primaryMeshes;
        }

        if (attachmentMeshes.Count > 0)
        {
            definition.AttachmentMeshes = attachmentMeshes;
        }

        if (enabledMeshes.Count > 0 && definition.SplitSubmeshes && !definition.LockEnabledMeshes)
        {
            definition.EnabledMeshes = enabledMeshes;
        }
    }

    internal static string GetPartFolderAssetPath(PartDefinition definition)
    {
        return ReStockPartPaths.GetMeshFolderAssetPath(definition.Category, definition.PartId);
    }

    internal static string GetSplitModelFolderAssetPath(PartDefinition definition)
    {
        return GetPartFolderAssetPath(definition);
    }

    internal static string ResolveManifestAssetPath(PartDefinition definition)
    {
        if (definition == null)
        {
            return null;
        }

        return GetManifestAssetPath(definition);
    }

    internal static string GetSingleModelAssetPath(PartDefinition definition)
    {
        return ReStockPartPaths.GetSingleModelAssetPath(definition.Category, definition.PartId);
    }

    internal static string GetManifestAssetPath(PartDefinition definition)
    {
        return ReStockPartPaths.GetManifestAssetPath(definition.Category, definition.PartId);
    }

    internal static string GetManifestAssetPath(string partId, string category, bool splitSubmeshes)
    {
        return ReStockPartPaths.GetManifestAssetPath(category, partId);
    }

    internal static string GetSubmeshAssetPath(PartDefinition definition, string meshName)
    {
        return ReStockPartPaths.GetSubmeshAssetPath(definition.Category, definition.PartId, meshName);
    }

    internal static bool IsBatchPartMeshAsset(string normalizedPath)
    {
        PartDefinition[] definitions = Definitions;

        for (int i = 0; i < definitions.Length; i++)
        {
            PartDefinition definition = definitions[i];
            string folder = GetPartFolderAssetPath(definition) + "/";

            if (
                normalizedPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase) &&
                normalizedPath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return IsKnownMeshFolderAsset(normalizedPath);
    }

    static bool IsKnownMeshFolderAsset(string normalizedPath)
    {
        if (
            !normalizedPath.StartsWith(MeshesAssetRoot + "/", StringComparison.OrdinalIgnoreCase) ||
            !normalizedPath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        string relative = normalizedPath.Substring(MeshesAssetRoot.Length + 1);
        string[] segments = relative.Split('/');

        return segments.Length >= 3;
    }

    internal static bool IsSplitSubmeshAsset(string normalizedPath)
    {
        PartDefinition[] definitions = Definitions;

        for (int i = 0; i < definitions.Length; i++)
        {
            PartDefinition definition = definitions[i];

            if (!definition.SplitSubmeshes)
            {
                continue;
            }

            string folder = GetPartFolderAssetPath(definition) + "/";

            if (
                normalizedPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase) &&
                normalizedPath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) &&
                !normalizedPath.Equals(GetSingleModelAssetPath(definition), StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsBatchSingleModelAsset(string normalizedPath)
    {
        PartDefinition[] definitions = Definitions;

        for (int i = 0; i < definitions.Length; i++)
        {
            PartDefinition definition = definitions[i];

            if (definition.SplitSubmeshes)
            {
                continue;
            }

            if (normalizedPath.Equals(GetSingleModelAssetPath(definition), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static MeshRole ParseRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return MeshRole.Primary;
        }

        if (role.Equals("attachment", StringComparison.OrdinalIgnoreCase))
        {
            return MeshRole.Attachment;
        }

        if (role.Equals("skin", StringComparison.OrdinalIgnoreCase))
        {
            return MeshRole.Skin;
        }

        return MeshRole.Primary;
    }
}
#endif
