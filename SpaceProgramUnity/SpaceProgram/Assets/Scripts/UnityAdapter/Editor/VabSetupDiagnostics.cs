#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

static class VabSetupDiagnostics
{
    const string MenuPath = "Space Program/VAB/Diagnose VAB Setup";

    [MenuItem(MenuPath, false, 40)]
    static void DiagnoseMenu()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("=== VAB Setup Diagnostics ===");

        Scene activeScene = SceneManager.GetActiveScene();
        report.AppendLine("Active scene: " + activeScene.name + " (loaded=" + activeScene.isLoaded + ")");

        VabSceneEnvironment environment = Object.FindFirstObjectByType<VabSceneEnvironment>();
        report.AppendLine(
            "VabSceneEnvironment: " + (environment != null ? environment.gameObject.name : "MISSING")
        );

        VabEditorController controller = Object.FindFirstObjectByType<VabEditorController>();
        report.AppendLine(
            "VabEditorController: " + (controller != null ? "found" : "MISSING (created at runtime)")
        );

        SimulationRunner runner = Object.FindFirstObjectByType<SimulationRunner>();
        report.AppendLine("SimulationRunner: " + (runner != null ? runner.SessionMode.ToString() : "MISSING"));

        int partCount = PartCatalog.Refresh();
        report.AppendLine("PartCatalog.Refresh(): " + partCount + " prefab(s)");

        string[] loadCandidates =
        {
            "VabScene",
            "VABScene",
            "vablvl3",
            "vablvl",
        };

        report.AppendLine("Build Settings scenes:");

        for (int i = 0; i < loadCandidates.Length; i++)
        {
            string sceneName = loadCandidates[i];
            bool canLoad = Application.CanStreamedLevelBeLoaded(sceneName);
            report.AppendLine("  " + sceneName + ": " + (canLoad ? "OK" : "NOT in build"));
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PartCatalog.PrefabsAssetRoot });
        report.AppendLine("Prefabs under " + PartCatalog.PrefabsAssetRoot + ": " + prefabGuids.Length);

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            int nodeCount = prefab != null ? PartPrefabBuilder.ReadAuthoredNodes(prefab).Count : 0;
            report.AppendLine("  " + path + " nodes=" + nodeCount);
        }

        Debug.Log(report.ToString());
    }
}
#endif
