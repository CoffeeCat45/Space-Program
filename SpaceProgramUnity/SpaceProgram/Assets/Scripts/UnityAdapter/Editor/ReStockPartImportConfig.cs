#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads <c>Pipeline/Parts/*.partimport.json</c> and merges Blender manifest data.
/// </summary>
static class ReStockPartImportConfig
{
    [Serializable]
    internal class ModelAttachmentEntry
    {
        public string mu;
        public float[] position;
        public float[] scale;
        public float[] rotation;
        public float[] blenderScale;
        public string[] keepMeshes;
        public bool join;
        public string joinName;
        public string material;
    }

    [Serializable]
    internal class SlotMaterialOverrideEntry
    {
        public string slot;
        public string material;
    }

    [Serializable]
    internal class MeshMaterialOverrideEntry
    {
        public string mesh;
        public string material;
    }

    [Serializable]
    internal class PartImportFile
    {
        public string partId;
        public string category;
        public string sourceMu;
        public bool deletePaintVariants = true;
        public bool splitByMaterial = true;
        public bool splitSubmeshes;
        public bool lockEnabledMeshes;
        public string primaryMaterial;
        public string[] keepMeshes;
        public string[] enabledMeshes;
        public MeshMaterialOverrideEntry[] materialOverrides;
        public SlotMaterialOverrideEntry[] slotMaterialOverrides;
        public ModelAttachmentEntry[] modelAttachments;
    }

    internal static List<PartImportFile> LoadAllConfigs()
    {
        List<PartImportFile> configs = new List<PartImportFile>();
        string configDir = ReStockPartPaths.GetPartImportConfigDirectory();

        if (!Directory.Exists(configDir))
        {
            return configs;
        }

        string[] files = Directory.GetFiles(configDir, "*.partimport.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].EndsWith("_template.partimport.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            PartImportFile config = LoadConfigFile(files[i]);

            if (config != null && !string.IsNullOrWhiteSpace(config.partId))
            {
                configs.Add(config);
            }
        }

        return configs;
    }

    internal static PartImportFile LoadConfigForPart(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
        {
            return null;
        }

        string path = ReStockPartPaths.GetPartImportConfigPath(partId);

        if (!File.Exists(path))
        {
            return null;
        }

        return LoadConfigFile(path);
    }

    internal static PartImportFile LoadConfigFile(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return null;
        }

        string json = File.ReadAllText(fullPath);
        PartImportFile config = JsonUtility.FromJson<PartImportFile>(json);

        if (config == null || string.IsNullOrWhiteSpace(config.partId))
        {
            Debug.LogWarning("ReStock: invalid partimport config at " + fullPath);
            return null;
        }

        return config;
    }

    internal static ReStockBatchParts.PartDefinition ToDefinition(PartImportFile config)
    {
        if (config == null)
        {
            return null;
        }

        ReStockBatchParts.PartDefinition definition = new ReStockBatchParts.PartDefinition
        {
            PartId = config.partId,
            Category = config.category,
            SplitSubmeshes = config.splitSubmeshes,
            LockEnabledMeshes = config.lockEnabledMeshes,
            PrimaryMaterialOverride = config.primaryMaterial,
            ConfigFilePath = ReStockPartPaths.GetPartImportConfigPath(config.partId),
        };

        if (config.keepMeshes != null && config.keepMeshes.Length > 0)
        {
            definition.KeepMeshes = new HashSet<string>(config.keepMeshes, StringComparer.OrdinalIgnoreCase);
        }

        if (config.enabledMeshes != null && config.enabledMeshes.Length > 0)
        {
            definition.EnabledMeshes = new HashSet<string>(config.enabledMeshes, StringComparer.OrdinalIgnoreCase);
        }

        if (config.materialOverrides != null && config.materialOverrides.Length > 0)
        {
            for (int i = 0; i < config.materialOverrides.Length; i++)
            {
                MeshMaterialOverrideEntry entry = config.materialOverrides[i];

                if (
                    string.IsNullOrWhiteSpace(entry.mesh) ||
                    string.IsNullOrWhiteSpace(entry.material)
                )
                {
                    continue;
                }

                definition.MaterialOverrides[entry.mesh] = entry.material;
            }
        }

        if (config.slotMaterialOverrides != null && config.slotMaterialOverrides.Length > 0)
        {
            for (int i = 0; i < config.slotMaterialOverrides.Length; i++)
            {
                SlotMaterialOverrideEntry entry = config.slotMaterialOverrides[i];

                if (
                    string.IsNullOrWhiteSpace(entry.slot) ||
                    string.IsNullOrWhiteSpace(entry.material)
                )
                {
                    continue;
                }

                definition.SlotMaterialOverrides[entry.slot] = entry.material;
            }
        }

        ReStockBatchParts.MergeManifest(definition);
        return definition;
    }

    internal static List<ReStockBatchParts.PartDefinition> LoadAllDefinitions()
    {
        List<PartImportFile> configs = LoadAllConfigs();
        List<ReStockBatchParts.PartDefinition> definitions = new List<ReStockBatchParts.PartDefinition>();

        for (int i = 0; i < configs.Count; i++)
        {
            ReStockBatchParts.PartDefinition definition = ToDefinition(configs[i]);

            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }
}
#endif
