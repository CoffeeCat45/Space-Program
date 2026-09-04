#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu entry points for the ReStock pilot pipeline (separate file to force Editor recompile).
/// </summary>
static class ReStockPilotEditorMenus
{
    const string MenuRoot = "Space Program/ReStock/";

    [MenuItem(MenuRoot + "9. Force Reimport Batch + Rebuild Prefabs")]
    static void ForceReimportPilotAndRebuildMenu()
    {
        Debug.Log("ReStock: batch rebuild via Part Importer pipeline (per-part folders + auto nodes).");
        ReStockImportPipeline.ForceReimportPilotAndRebuild();
    }

    [MenuItem(MenuRoot + "10. Build Split-Mesh Prefabs Only")]
    static void BuildSplitMeshPrefabsMenu()
    {
        Debug.Log("ReStock: rebuilding split-mesh parts via Part Importer");
        ReStockBatchParts.RefreshDefinitions();
        int built = 0;

        for (int i = 0; i < ReStockBatchParts.Definitions.Length; i++)
        {
            ReStockBatchParts.PartDefinition definition = ReStockBatchParts.Definitions[i];

            if (!definition.SplitSubmeshes)
            {
                continue;
            }

            if (ReStockPartImporter.ImportDefinition(definition, true))
            {
                built++;
            }
        }

        if (built > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ReStock: rebuilt " + built + " split-mesh part(s).");
        }
        else
        {
            Debug.LogWarning(
                "ReStock: no split-mesh parts rebuilt. Run Blender export_part.py or Part Importer first."
            );
        }
    }
}

#endif
