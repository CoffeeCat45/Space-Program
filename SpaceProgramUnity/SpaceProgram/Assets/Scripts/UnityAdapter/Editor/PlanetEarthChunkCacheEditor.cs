#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EarthSandboxQuadtreeManager))]
public sealed class EarthSandboxQuadtreeManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Planet mesh cache", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Cached templates: " + PlanetFlightChunkMeshCache.Count +
            " | Mesh pool: " + PlanetMeshPool.Count
        );

        if (GUILayout.Button("Clear Planet Mesh Cache"))
        {
        PlanetEarthChunkMeshCache.Clear();
        PlanetMeshPool.Clear();
        PlanetFlightChunkMeshPipeline.CancelAll();
        Debug.Log("PlanetEarthChunkMeshCache: cleared templates, mesh pool, and pending async builds.");
        }
    }
}

static class PlanetEarthChunkCacheMenu
{
    const string MenuPath = "Space Program/Planet Terrain/Clear Mesh Cache";

    [MenuItem(MenuPath)]
    public static void ClearMeshCache()
    {
        int clearedTemplates = PlanetFlightChunkMeshCache.Count;
        PlanetEarthChunkMeshCache.Clear();
        PlanetMeshPool.Clear();
        PlanetFlightChunkMeshPipeline.CancelAll();
        Debug.Log(
            "PlanetEarthChunkMeshCache: cleared " + clearedTemplates +
            " cached templates, mesh pool, and pending async builds."
        );
    }
}
#endif
