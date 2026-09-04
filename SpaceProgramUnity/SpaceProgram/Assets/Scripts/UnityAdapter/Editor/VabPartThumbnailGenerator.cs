#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders VAB part prefabs to foreground PNG icons under
/// <see cref="PartThumbnailPaths.IconsAssetRoot"/>.
/// The KSP background is drawn in UI by <see cref="PartThumbnailUi"/>.
/// </summary>
static class VabPartThumbnailGenerator
{
    const string MenuRoot = "Space Program/VAB/";
    const int ThumbnailSize = 256;
    const int ThumbnailPipelineVersion = 8;
    const string ThumbnailPipelineVersionPrefKey = "SpaceProgram.Vab.ThumbnailPipelineVersion";

    [MenuItem(MenuRoot + "Generate Part Thumbnails (Missing)", false, 40)]
    static void GenerateMissingMenu()
    {
        if (!TryBeginThumbnailGeneration())
        {
            return;
        }

        int generated = GenerateMissingForAllContainerPrefabs();
        Debug.Log("VAB thumbnails: generated " + generated + " missing icon(s).");
    }

    [MenuItem(MenuRoot + "Regenerate All Part Thumbnails", false, 41)]
    static void RegenerateAllMenu()
    {
        if (!TryBeginThumbnailGeneration())
        {
            return;
        }

        int generated = GenerateAllContainerPrefabs(force: true);
        EditorPrefs.SetInt(ThumbnailPipelineVersionPrefKey, ThumbnailPipelineVersion);
        Debug.Log(
            "VAB thumbnails: regenerated " + generated +
            " foreground icon(s). Enter Play mode to view them in the catalog."
        );
    }

    [MenuItem(MenuRoot + "Repair Broken Part Thumbnails", false, 42)]
    static void RepairBrokenMenu()
    {
        if (!TryBeginThumbnailGeneration())
        {
            return;
        }

        int repaired = EnsureCatalogIconsValid();
        Debug.Log("VAB thumbnails: repaired " + repaired + " broken icon(s).");
    }

    static bool TryBeginThumbnailGeneration()
    {
        if (CanRunThumbnailGeneration())
        {
            return true;
        }

        const string message =
            "Thumbnail generation only works in Edit mode.\n\n" +
            "Stop Play mode, run the menu item again, then enter Play mode to see updated icons.";

        EditorUtility.DisplayDialog("VAB Thumbnails", message, "OK");
        Debug.LogWarning("VAB thumbnails: " + message.Replace("\n\n", " "));
        return false;
    }

    internal static void GenerateMissingForDefinitions(
        IEnumerable<ReStockBatchParts.PartDefinition> definitions
    )
    {
        if (definitions == null)
        {
            return;
        }

        int generated = 0;

        foreach (ReStockBatchParts.PartDefinition definition in definitions)
        {
            if (definition == null)
            {
                continue;
            }

            string prefabPath = ReStockPartPaths.GetContainerPrefabAssetPath(
                definition.Category,
                definition.PartId
            );

            if (TryGenerateThumbnail(prefabPath, force: false))
            {
                generated++;
            }
        }

        if (generated > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("VAB thumbnails: generated " + generated + " icon(s) after ReStock import.");
        }
    }

    internal static int GenerateMissingForAllContainerPrefabs()
    {
        return GenerateAllContainerPrefabs(force: false);
    }

    internal static int EnsureCatalogIconsValid()
    {
        if (!CanRunThumbnailGeneration())
        {
            return 0;
        }

        EnsureIconsFolder();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PartCatalog.PrefabsAssetRoot });
        int regenerated = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

            if (
                string.IsNullOrWhiteSpace(prefabPath) ||
                !PartCatalog.IsContainerPrefabPath(prefabPath)
            )
            {
                continue;
            }

            string partId = Path.GetFileNameWithoutExtension(prefabPath);
            string iconAssetPath = PartThumbnailPaths.GetIconAssetPath(partId);
            string iconFullPath = GetProjectFullPath(iconAssetPath);
            bool missing = !File.Exists(iconFullPath);
            bool needsRegeneration = !missing && NeedsForegroundRegeneration(iconAssetPath);

            if (!missing && !needsRegeneration)
            {
                continue;
            }

            if (TryGenerateThumbnail(prefabPath, force: true))
            {
                regenerated++;
            }
        }

        if (regenerated > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PartThumbnailLoader.ClearCache();
        }

        return regenerated;
    }

    static bool CanRunThumbnailGeneration()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
    }

    static bool NeedsForegroundRegeneration(string iconAssetPath)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconAssetPath);

        if (texture == null)
        {
            return true;
        }

        Texture2D readable = PartThumbnailCatalogBackground.MakeReadableCopy(texture);

        try
        {
            int width = readable.width;
            int height = readable.height;
            Color topLeft = readable.GetPixel(4, height - 5);
            Color bottomLeft = readable.GetPixel(4, 4);

            // Foreground-only icons should have transparent corners.
            return topLeft.a > 0.25f || bottomLeft.a > 0.25f;
        }
        finally
        {
            if (readable != texture)
            {
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }
    }

    static int GenerateAllContainerPrefabs(bool force)
    {
        if (!CanRunThumbnailGeneration())
        {
            Debug.LogWarning("VAB thumbnails: skipped generation while play mode or compilation is active.");
            return 0;
        }

        EnsureIconsFolder();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PartCatalog.PrefabsAssetRoot });
        int generated = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

                if (
                    string.IsNullOrWhiteSpace(prefabPath) ||
                    !PartCatalog.IsContainerPrefabPath(prefabPath)
                )
                {
                    continue;
                }

                string partId = Path.GetFileNameWithoutExtension(prefabPath);
                EditorUtility.DisplayProgressBar(
                    "VAB Part Thumbnails",
                    partId,
                    prefabGuids.Length <= 1 ? 1f : (float)i / prefabGuids.Length
                );

                if (TryGenerateThumbnail(prefabPath, force))
                {
                    generated++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PartThumbnailLoader.ClearCache();
        return generated;
    }

    static bool TryGenerateThumbnail(string containerPrefabPath, bool force)
    {
        if (
            string.IsNullOrWhiteSpace(containerPrefabPath) ||
            !PartCatalog.IsContainerPrefabPath(containerPrefabPath)
        )
        {
            return false;
        }

        string partId = Path.GetFileNameWithoutExtension(containerPrefabPath);
        string iconAssetPath = PartThumbnailPaths.GetIconAssetPath(partId);
        string iconFullPath = GetProjectFullPath(iconAssetPath);

        if (!force && File.Exists(iconFullPath) && !IsSourceNewerThanIcon(containerPrefabPath, iconFullPath))
        {
            return false;
        }

        GameObject renderPrefab = ResolveRenderPrefab(containerPrefabPath);

        if (renderPrefab == null)
        {
            Debug.LogWarning("VAB thumbnails: prefab not found for '" + containerPrefabPath + "'.");
            return false;
        }

        EnsureIconsFolder();

        if (!TryRenderPrefabToPng(renderPrefab, iconFullPath))
        {
            Debug.LogWarning("VAB thumbnails: render failed for '" + partId + "'.");
            return false;
        }

        AssetDatabase.ImportAsset(iconAssetPath, ImportAssetOptions.ForceUpdate);
        ConfigureImportedIcon(iconAssetPath);
        return true;
    }

    static GameObject ResolveRenderPrefab(string containerPrefabPath)
    {
        if (
            ReStockPartPaths.TryParseContainerPrefabPath(
                containerPrefabPath,
                out string category,
                out string partId
            )
        )
        {
            string visualPath = ReStockPartPaths.GetVisualPrefabAssetPath(category, partId);
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);

            if (visualPrefab != null)
            {
                return visualPrefab;
            }
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(containerPrefabPath);
    }

    static bool IsSourceNewerThanIcon(string containerPrefabPath, string iconFullPath)
    {
        DateTime iconTime = File.GetLastWriteTimeUtc(iconFullPath);
        DateTime newestSource = File.GetLastWriteTimeUtc(GetProjectFullPath(containerPrefabPath));

        if (
            ReStockPartPaths.TryParseContainerPrefabPath(
                containerPrefabPath,
                out string category,
                out string partId
            )
        )
        {
            string visualPath = ReStockPartPaths.GetVisualPrefabAssetPath(category, partId);
            string visualFullPath = GetProjectFullPath(visualPath);

            if (File.Exists(visualFullPath))
            {
                newestSource = MaxUtc(newestSource, File.GetLastWriteTimeUtc(visualFullPath));
            }
        }

        return newestSource > iconTime;
    }

    static DateTime MaxUtc(DateTime left, DateTime right)
    {
        return left >= right ? left : right;
    }

    static bool TryRenderPrefabToPng(GameObject prefab, string outputFullPath)
    {
        return VabPartThumbnailStudio.TryCapturePrefab(prefab, outputFullPath, ThumbnailSize);
    }

    static void EnsureIconsFolder()
    {
        const string modelsRoot = "Assets/Models/Parts";
        const string iconsRoot = "Assets/Models/Parts/Icons";

        if (!AssetDatabase.IsValidFolder(modelsRoot))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(iconsRoot))
        {
            AssetDatabase.CreateFolder(modelsRoot, "Icons");
        }

        if (!AssetDatabase.IsValidFolder(PartThumbnailPaths.IconsAssetRoot))
        {
            AssetDatabase.CreateFolder(iconsRoot, "@thumbs");
        }
    }

    static void ConfigureImportedIcon(string iconAssetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(iconAssetPath) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.isReadable = false;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = ThumbnailSize;
        importer.SaveAndReimport();
    }

    static string GetProjectFullPath(string assetPath)
    {
        string normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));
    }
}
#endif
