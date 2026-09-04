#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity-side import: reimport OBJ/PNG, rebuild materials, build visual + container prefabs.
/// </summary>
static class ReStockPartImporter
{
    internal static void ImportDefinitions(
        IEnumerable<ReStockBatchParts.PartDefinition> definitions,
        bool preserveAuthoredMarkers
    )
    {
        if (definitions == null)
        {
            return;
        }

        List<ReStockBatchParts.PartDefinition> definitionList = new List<ReStockBatchParts.PartDefinition>();

        foreach (ReStockBatchParts.PartDefinition definition in definitions)
        {
            if (definition != null)
            {
                definitionList.Add(definition);
            }
        }

        if (definitionList.Count == 0)
        {
            return;
        }

        AssetDatabase.Refresh();

        for (int i = 0; i < definitionList.Count; i++)
        {
            ReimportMeshes(definitionList[i]);
        }

        ReimportAllTextures();
        AssetDatabase.Refresh();
        ReStockImportPipeline.RebuildPartMaterials();

        int imported = 0;
        int failed = 0;

        for (int i = 0; i < definitionList.Count; i++)
        {
            if (BuildPrefabs(definitionList[i], preserveAuthoredMarkers))
            {
                imported++;
            }
            else
            {
                failed++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PartCatalog.Refresh();
        VabPartThumbnailGenerator.GenerateMissingForDefinitions(definitionList);

        Debug.Log(
            "ReStock import: done imported=" + imported +
            " failed=" + failed +
            ". Container prefabs: Assets/Prefabs/Parts/{Category}/{partId}/{partId}.prefab"
        );
    }

    internal static bool ImportDefinition(
        ReStockBatchParts.PartDefinition definition,
        bool preserveAuthoredMarkers
    )
    {
        if (definition == null)
        {
            return false;
        }

        ReimportMeshes(definition);
        ReimportAllTextures();
        AssetDatabase.Refresh();
        ReStockImportPipeline.RebuildPartMaterials();
        return BuildPrefabs(definition, preserveAuthoredMarkers);
    }

    static void ReimportMeshes(ReStockBatchParts.PartDefinition definition)
    {
        ReStockBatchParts.MergeManifest(definition);
        ReStockPartContainerBuilder.ReimportMeshes(definition);
    }

    static bool BuildPrefabs(ReStockBatchParts.PartDefinition definition, bool preserveAuthoredMarkers)
    {
        if (!ReStockPartContainerBuilder.BuildVisualPrefab(definition))
        {
            Debug.LogWarning("ReStock: visual prefab build failed for " + definition.PartId);
            return false;
        }

        if (!ReStockPartContainerBuilder.BuildOrUpdateContainer(definition, preserveAuthoredMarkers))
        {
            Debug.LogWarning("ReStock: container prefab build failed for " + definition.PartId);
            return false;
        }

        return true;
    }

    static void ReimportAllTextures()
    {
        string[] textureGuids = AssetDatabase.FindAssets(
            "t:Texture2D",
            new[] { "Assets/Models/Parts/Textures" }
        );

        for (int i = 0; i < textureGuids.Length; i++)
        {
            ReStockImportPipeline.ImportModelAssetIfPresent(
                AssetDatabase.GUIDToAssetPath(textureGuids[i])
            );
        }
    }
}
#endif
