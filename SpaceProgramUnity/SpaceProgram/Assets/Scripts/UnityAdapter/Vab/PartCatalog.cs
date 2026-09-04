using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Discovers VAB parts from <c>Assets/Prefabs/Parts/</c> and fills <see cref="PartDatabase"/>.
/// New prefab (+ optional <c>.partdef.json</c>) in that folder appears in the editor UI automatically.
/// </summary>
public static class PartCatalog
{
    public const string PrefabsAssetRoot = "Assets/Prefabs/Parts";
    public const string MeshesAssetRoot = "Assets/Models/Parts/Meshes";

    [Serializable]
    sealed class ManifestStub
    {
        public string partId;
        public string category;
    }

    public static int Refresh()
    {
#if UNITY_EDITOR
        List<PartTemplate> discovered = DiscoverTemplatesEditor();
        PartDatabase.ReplaceTemplates(discovered);
        Debug.Log("VAB PartCatalog: discovered " + discovered.Count + " parts in " + PrefabsAssetRoot);
        return discovered.Count;
#else
        RuntimePartCatalog catalog = RuntimePartCatalog.Load();

        if (catalog == null)
        {
            Debug.LogError(
                "VAB PartCatalog: RuntimePartCatalog is missing from Resources. " +
                "Rebuild the Windows player from Unity so the catalog is baked."
            );
            PartDatabase.ReplaceTemplates(new List<PartTemplate>());
            return 0;
        }

        List<PartTemplate> discovered = catalog.ToTemplates();
        PartDatabase.ReplaceTemplates(discovered);
        Debug.Log(
            "VAB PartCatalog: loaded " + discovered.Count +
            " parts from RuntimePartCatalog."
        );
        return discovered.Count;
#endif
    }

#if UNITY_EDITOR
    public static List<PartTemplate> DiscoverTemplatesForPlayerBake()
    {
        return DiscoverTemplatesEditor();
    }

    static List<PartTemplate> DiscoverTemplatesEditor()
    {
        Dictionary<string, string> manifestCategories = IndexManifestCategories();
        Dictionary<string, PartTemplate> templatesById =
            new Dictionary<string, PartTemplate>(StringComparer.OrdinalIgnoreCase);
        List<PartTemplate> templates = new List<PartTemplate>();

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsAssetRoot });

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

            if (
                string.IsNullOrWhiteSpace(prefabPath) ||
                !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                !IsContainerPrefabPath(prefabPath)
            )
            {
                continue;
            }

            PartTemplate template = BuildTemplateForPrefab(
                prefabPath,
                manifestCategories
            );

            if (template == null)
            {
                continue;
            }

            if (
                templatesById.TryGetValue(template.id, out PartTemplate existing) &&
                !ShouldPreferPrefabPath(prefabPath, existing.prefabPath)
            )
            {
                continue;
            }

            templatesById[template.id] = template;
        }

        templates.AddRange(templatesById.Values);
        templates.Sort(CompareTemplates);
        return templates;
    }

    /// <summary>
    /// Container prefabs: <c>.../{Category}/{partId}/{partId}.prefab</c>. Visual staging uses <c>.visual.prefab</c>.
    /// </summary>
    public static bool IsContainerPrefabPath(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            return false;
        }

        string normalized = prefabPath.Replace('\\', '/');

        if (normalized.IndexOf(".visual.", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        string[] categoryTokens =
        {
            "/" + PartDatabase.CategoryCommand + "/",
            "/" + PartDatabase.CategoryFuelTank + "/",
            "/" + PartDatabase.CategoryEngine + "/",
        };

        for (int i = 0; i < categoryTokens.Length; i++)
        {
            if (normalized.IndexOf(categoryTokens[i], StringComparison.OrdinalIgnoreCase) < 0)
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

    static bool ShouldPreferPrefabPath(string candidatePath, string existingPath)
    {
        bool candidateInCategory = IsContainerPrefabPath(candidatePath);
        bool existingInCategory = IsContainerPrefabPath(existingPath);

        if (candidateInCategory != existingInCategory)
        {
            return candidateInCategory;
        }

        return string.Compare(candidatePath, existingPath, StringComparison.OrdinalIgnoreCase) < 0;
    }

    static PartTemplate BuildTemplateForPrefab(
        string prefabPath,
        Dictionary<string, string> manifestCategories
    )
    {
        string partId = Path.GetFileNameWithoutExtension(prefabPath);
        string partDefPath = PartDefinitionFile.GetSidecarPathForPrefab(prefabPath);

        if (PartDefinitionFile.TryLoad(partDefPath, out PartDefinitionData partDef))
        {
            PartTemplate fromFile = PartDefinitionFile.ToTemplate(partDef, prefabPath);
            fromFile.nodes = ResolveNodes(partId, prefabPath, fromFile.category, fromFile.nodes);
            AssignIconPath(fromFile);
            return fromFile;
        }

        string category = ResolveCategory(prefabPath, partId, manifestCategories);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogWarning("VAB PartCatalog: failed to load prefab '" + prefabPath + "'.");
            return null;
        }

        List<AttachmentNode> authoredNodes = PartPrefabBuilder.ReadAuthoredNodes(prefab);

        PartDefinitionData generated = new PartDefinitionData
        {
            id = partId,
            displayName = PartCatalogHelpers.HumanizePartId(partId),
            category = category,
            dryMass = PartCatalogHelpers.GuessDryMass(partId, category),
            fuelCapacity = PartCatalogHelpers.GuessFuelCapacity(partId, category),
            maxThrustVac = PartCatalogHelpers.GuessMaxThrustVac(partId, category),
            ispVacuum = PartCatalogHelpers.GuessIspVacuum(partId, category),
            nodes = authoredNodes.Count > 0 ?
                authoredNodes :
                PartCatalogHelpers.InferNodes(partId, PartCatalogHelpers.CalculateLocalBounds(prefab), category),
        };

        PartDefinitionFile.Save(partDefPath, generated);
        Debug.Log("VAB PartCatalog: auto-generated " + partDefPath);

        PartTemplate template = PartDefinitionFile.ToTemplate(generated, prefabPath);
        template.nodes = ResolveNodes(partId, prefabPath, category, template.nodes);
        AssignIconPath(template);
        return template;
    }

    static void AssignIconPath(PartTemplate template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.id))
        {
            return;
        }

        string iconPath = PartThumbnailPaths.GetIconAssetPath(template.id);
        template.iconPath = File.Exists(ResolveProjectFullPath(iconPath)) ? iconPath : string.Empty;
    }

    static string ResolveProjectFullPath(string assetPath)
    {
        string normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));
    }

    static List<AttachmentNode> ResolveNodes(
        string partId,
        string prefabPath,
        string category,
        List<AttachmentNode> existingNodes
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab != null)
        {
            List<AttachmentNode> authoredNodes = PartPrefabBuilder.ReadAuthoredNodes(prefab);

            if (authoredNodes.Count > 0)
            {
                return authoredNodes;
            }
        }

        if (existingNodes != null && existingNodes.Count > 0)
        {
            return existingNodes;
        }

        return InferNodesFromPrefab(partId, prefabPath, category);
    }

    static List<AttachmentNode> InferNodesFromPrefab(
        string partId,
        string prefabPath,
        string category
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            return new List<AttachmentNode>();
        }

        Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(prefab);

        return PartCatalogHelpers.InferNodes(partId, bounds, category);
    }

    static Dictionary<string, string> IndexManifestCategories()
    {
        Dictionary<string, string> categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] manifestGuids = AssetDatabase.FindAssets(string.Empty, new[] { MeshesAssetRoot });

        for (int i = 0; i < manifestGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(manifestGuids[i]);

            if (
                string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.EndsWith(".manifest.json", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            string json = File.ReadAllText(assetPath);
            ManifestStub manifest = JsonUtility.FromJson<ManifestStub>(json);

            if (
                manifest == null ||
                string.IsNullOrWhiteSpace(manifest.partId) ||
                string.IsNullOrWhiteSpace(manifest.category)
            )
            {
                continue;
            }

            categories[manifest.partId] = manifest.category;
        }

        return categories;
    }

    static string ResolveCategory(
        string prefabPath,
        string partId,
        Dictionary<string, string> manifestCategories
    )
    {
        if (manifestCategories.TryGetValue(partId, out string manifestCategory))
        {
            return manifestCategory;
        }

        string folderCategory = InferCategoryFromPrefabPath(prefabPath);

        if (!string.IsNullOrWhiteSpace(folderCategory))
        {
            return folderCategory;
        }

        return PartDatabase.CategoryMisc;
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

        return string.Empty;
    }

    static int CompareTemplates(PartTemplate left, PartTemplate right)
    {
        int categoryCompare = string.Compare(
            left.category,
            right.category,
            StringComparison.OrdinalIgnoreCase
        );

        if (categoryCompare != 0)
        {
            return categoryCompare;
        }

        return string.Compare(
            left.displayName,
            right.displayName,
            StringComparison.OrdinalIgnoreCase
        );
    }
#endif
}
