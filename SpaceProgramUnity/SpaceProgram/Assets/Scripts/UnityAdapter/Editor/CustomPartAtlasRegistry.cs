#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
sealed class CustomPartAtlasRecord
{
    public string contentHash = string.Empty;
    public string atlasStem = string.Empty;
    public string materialPath = string.Empty;
    public string sharedFolder = string.Empty;
    public string albedoAssetPath = string.Empty;
    public string metallicAssetPath = string.Empty;
    public string reflectionAssetPath = string.Empty;
    public string maskAssetPath = string.Empty;
    public int reflectionKind;
    public string[] partIds = Array.Empty<string>();
}

[Serializable]
sealed class CustomPartAtlasRegistryData
{
    public CustomPartAtlasRecord[] atlases = Array.Empty<CustomPartAtlasRecord>();
}

/// <summary>
/// Maps identical PBR atlas bytes to one shared URP material used by many custom parts.
/// </summary>
static class CustomPartAtlasRegistry
{
    internal const string AssetPath = "Assets/Materials/Parts/Custom/custom-part-materials.json";

    internal static CustomPartAtlasRegistryData Load()
    {
        string fullPath = ToFullPath(AssetPath);

        if (!File.Exists(fullPath))
        {
            return new CustomPartAtlasRegistryData();
        }

        string json = File.ReadAllText(fullPath);
        CustomPartAtlasRegistryData data = JsonUtility.FromJson<CustomPartAtlasRegistryData>(json);

        if (data == null)
        {
            return new CustomPartAtlasRegistryData();
        }

        if (data.atlases == null)
        {
            data.atlases = Array.Empty<CustomPartAtlasRecord>();
        }

        return data;
    }

    internal static void Save(CustomPartAtlasRegistryData data)
    {
        if (data == null)
        {
            data = new CustomPartAtlasRegistryData();
        }

        if (data.atlases == null)
        {
            data.atlases = Array.Empty<CustomPartAtlasRecord>();
        }

        CustomPartImportPipeline.EnsureAssetFolder(CustomPartImportPipeline.CustomMaterialsAssetRoot);
        string fullPath = ToFullPath(AssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(fullPath, JsonUtility.ToJson(data, true));
        AssetDatabase.ImportAsset(AssetPath);
    }

    internal static CustomPartAtlasRecord FindByHash(CustomPartAtlasRegistryData data, string contentHash)
    {
        if (data?.atlases == null || string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        for (int i = 0; i < data.atlases.Length; i++)
        {
            CustomPartAtlasRecord record = data.atlases[i];

            if (
                record != null &&
                string.Equals(record.contentHash, contentHash, StringComparison.OrdinalIgnoreCase)
            )
            {
                return record;
            }
        }

        return null;
    }

    internal static void Upsert(CustomPartAtlasRegistryData data, CustomPartAtlasRecord record)
    {
        if (data == null || record == null)
        {
            return;
        }

        List<CustomPartAtlasRecord> list = new List<CustomPartAtlasRecord>();

        if (data.atlases != null)
        {
            list.AddRange(data.atlases);
        }

        int existing = -1;

        for (int i = 0; i < list.Count; i++)
        {
            if (
                list[i] != null &&
                string.Equals(list[i].contentHash, record.contentHash, StringComparison.OrdinalIgnoreCase)
            )
            {
                existing = i;
                break;
            }
        }

        if (existing >= 0)
        {
            list[existing] = record;
        }
        else
        {
            list.Add(record);
        }

        data.atlases = list.ToArray();
    }

    internal static void RemovePartFromOtherAtlases(
        CustomPartAtlasRegistryData data,
        string partId,
        string keepHash
    )
    {
        if (data?.atlases == null || string.IsNullOrWhiteSpace(partId))
        {
            return;
        }

        for (int i = 0; i < data.atlases.Length; i++)
        {
            CustomPartAtlasRecord record = data.atlases[i];

            if (
                record == null ||
                string.Equals(record.contentHash, keepHash, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            record.partIds = CustomPartImportPipeline.RemovePartId(record.partIds, partId);
        }
    }

    static string ToFullPath(string assetPath)
    {
        string normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));
    }
}
#endif
