#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

static class FlightRenderLayersSetup
{
    [MenuItem("Space Program/Flight/Setup Scaled Space Layers")]
    public static void SetupLayersMenu()
    {
        EnsureLayers();
        Debug.Log("Flight: ScaledSpace and LocalPhysics layers ready.");
    }

    public static void EnsureLayers()
    {
        EnsureLayer(FlightRenderLayers.ScaledSpaceLayerName);
        EnsureLayer(FlightRenderLayers.LocalPhysicsLayerName);
    }

    static void EnsureLayer(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
        );
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);

            if (layer.stringValue == layerName)
            {
                return;
            }
        }

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);

            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return;
            }
        }

        Debug.LogError("Flight: no free User Layer slot for '" + layerName + "'.");
    }
}

[InitializeOnLoad]
static class FlightRenderLayersAutoSetup
{
    static FlightRenderLayersAutoSetup()
    {
        EditorApplication.delayCall += FlightRenderLayersSetup.EnsureLayers;
    }
}
#endif
