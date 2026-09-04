#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import selected ReStock parts: optional Blender export, Unity reimport, prefab build.
/// </summary>
class ReStockPartImporterWindow : EditorWindow
{
    readonly List<PartRow> rows = new List<PartRow>();
    Vector2 scroll;
    string blenderPath = "blender";
    bool preserveAuthoredMarkers = true;
    bool runBlenderBeforeImport;
    string statusMessage = string.Empty;

    sealed class PartRow
    {
        public ReStockPartImportConfig.PartImportFile Config;
        public bool Selected;
        public bool HasMeshes;
        public bool HasContainer;
    }

    [MenuItem("Space Program/ReStock/Part Importer")]
    static void OpenWindow()
    {
        ReStockPartImporterWindow window = GetWindow<ReStockPartImporterWindow>("ReStock Parts");
        window.minSize = new Vector2(520f, 420f);
        window.Show();
    }

    void OnEnable()
    {
        blenderPath = ReStockBlenderRunner.GetBlenderPath();
        ReloadRows();
    }

    void ReloadRows()
    {
        rows.Clear();
        List<ReStockPartImportConfig.PartImportFile> configs = ReStockPartImportConfig.LoadAllConfigs();

        for (int i = 0; i < configs.Count; i++)
        {
            ReStockPartImportConfig.PartImportFile config = configs[i];
            string meshFolder = ReStockPartPaths.GetMeshFolderAssetPath(config.category, config.partId);
            string containerPath = ReStockPartPaths.GetContainerPrefabAssetPath(config.category, config.partId);

            rows.Add(
                new PartRow
                {
                    Config = config,
                    Selected = config.enabledMeshes == null || config.enabledMeshes.Length == 0,
                    HasMeshes = AssetDatabase.IsValidFolder(meshFolder),
                    HasContainer = AssetDatabase.LoadAssetAtPath<GameObject>(containerPath) != null,
                }
            );
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("ReStock Part Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Быстрый путь: Import New .mu — выбрал файл → Blender сам создаёт конфиг, " +
            "экспортирует OBJ и собирает prefab.\n\n" +
            "Ручной JSON не нужен. Если mesh не те — открой Pipeline/Parts/*.partimport.json " +
            "и поправь enabledMeshes (или пересканируй).",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import New .mu…", GUILayout.Height(32f)))
        {
            EditorApplication.delayCall += QuickImportNewMu;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4f);

        EditorGUILayout.BeginHorizontal();
        blenderPath = EditorGUILayout.TextField("Blender", blenderPath);

        if (GUILayout.Button("Save", GUILayout.Width(60f)))
        {
            ReStockBlenderRunner.SetBlenderPath(blenderPath);
        }

        EditorGUILayout.EndHorizontal();

        preserveAuthoredMarkers = EditorGUILayout.ToggleLeft(
            "Preserve existing nodes / thrust positions on reimport",
            preserveAuthoredMarkers
        );
        runBlenderBeforeImport = EditorGUILayout.ToggleLeft(
            "Run Blender export before Unity import (selected parts)",
            runBlenderBeforeImport
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh List"))
        {
            ReStockBatchParts.InvalidateCache();
            ReloadRows();
        }

        if (GUILayout.Button("Select All"))
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Selected = true;
            }
        }

        if (GUILayout.Button("Select None"))
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Selected = false;
            }
        }

        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        for (int i = 0; i < rows.Count; i++)
        {
            DrawRow(rows[i]);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Export Selected in Blender", GUILayout.Height(28f)))
        {
            ExportSelectedInBlender();
        }

        if (GUILayout.Button("Import Selected", GUILayout.Height(28f)))
        {
            EditorApplication.delayCall += ImportSelected;
        }

        if (GUILayout.Button("Import All", GUILayout.Height(28f)))
        {
            ImportAll();
        }

        if (GUILayout.Button("Open Config Folder", GUILayout.Height(28f)))
        {
            EditorUtility.RevealInFinder(ReStockPartPaths.GetPartImportConfigDirectory());
        }

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }
    }

    static void DrawRow(PartRow row)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(20f));

        string label = row.Config.partId + "  [" + row.Config.category + "]";
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        if (row.HasMeshes)
        {
            EditorGUILayout.LabelField("OBJ", GUILayout.Width(36f));
        }

        if (row.HasContainer)
        {
            EditorGUILayout.LabelField("Prefab", GUILayout.Width(52f));
        }

        EditorGUILayout.EndHorizontal();

        if (row.Config.enabledMeshes != null && row.Config.enabledMeshes.Length > 0)
        {
            EditorGUILayout.LabelField(
                "    enabled: " + string.Join(", ", row.Config.enabledMeshes),
                EditorStyles.miniLabel
            );
        }
    }

    void QuickImportNewMu()
    {
        if (!ReStockMuPicker.TryPickMuFile(out string sourceMu))
        {
            return;
        }

        ReStockBlenderRunner.SetBlenderPath(blenderPath);

        if (!ReStockBlenderRunner.TryRunScanMu(sourceMu, out string scanLog))
        {
            statusMessage = "Scan failed:\n" + scanLog;
            Debug.LogError(statusMessage);
            return;
        }

        Debug.Log(scanLog);
        string partId = ReStockMuPicker.GetPartIdFromSourceMu(sourceMu);

        if (!ReStockBlenderRunner.TryRunExport(partId, out string exportLog))
        {
            statusMessage = "Export failed:\n" + exportLog;
            Debug.LogError(statusMessage);
            return;
        }

        Debug.Log(exportLog);
        ReStockBatchParts.InvalidateCache();
        ReloadRows();

        ReStockPartImportConfig.PartImportFile config = ReStockPartImportConfig.LoadConfigForPart(partId);

        if (config == null)
        {
            statusMessage = "Config was not created for " + partId;
            return;
        }

        ReStockPartImporter.ImportDefinition(
            ReStockPartImportConfig.ToDefinition(config),
            preserveAuthoredMarkers: false
        );

        statusMessage =
            "Imported " + partId + " from .mu.\n" +
            "Prefab: Assets/Prefabs/Parts/" + config.category + "/" + partId + "/" + partId + ".prefab\n" +
            "Пододвинь Node_Top / Node_Bottom / thrustTransform при необходимости.";
        ReloadRows();
    }

    void ExportSelectedInBlender()
    {
        int exported = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            PartRow row = rows[i];

            if (!row.Selected)
            {
                continue;
            }

            if (!ReStockBlenderRunner.TryRunExport(row.Config.partId, out string log))
            {
                statusMessage = "Blender failed for " + row.Config.partId + ":\n" + log;
                Debug.LogError(statusMessage);
                return;
            }

            exported++;
            Debug.Log(log);
        }

        statusMessage = "Blender export OK for " + exported + " part(s).";
        ReloadRows();
    }

    void ImportSelected()
    {
        if (runBlenderBeforeImport)
        {
            ExportSelectedInBlender();
        }

        List<ReStockBatchParts.PartDefinition> definitions = new List<ReStockBatchParts.PartDefinition>();

        for (int i = 0; i < rows.Count; i++)
        {
            PartRow row = rows[i];

            if (!row.Selected)
            {
                continue;
            }

            ReStockPartImportConfig.PartImportFile config =
                ReStockPartImportConfig.LoadConfigForPart(row.Config.partId) ?? row.Config;
            definitions.Add(ReStockPartImportConfig.ToDefinition(config));
        }

        ReStockBatchParts.InvalidateCache();
        ReStockPartImporter.ImportDefinitions(definitions, preserveAuthoredMarkers);
        statusMessage = "Imported " + definitions.Count + " selected part(s).";
        ReloadRows();
    }

    void ImportAll()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Selected = true;
        }

        if (runBlenderBeforeImport)
        {
            if (!ReStockBlenderRunner.TryRunExport(null, out string log))
            {
                statusMessage = "Blender export all failed:\n" + log;
                return;
            }

            Debug.Log(log);
        }

        ReStockBatchParts.RefreshDefinitions();
        ReStockPartImporter.ImportDefinitions(ReStockBatchParts.Definitions, preserveAuthoredMarkers);
        statusMessage = "Imported all configured parts.";
        ReloadRows();
    }
}
#endif
