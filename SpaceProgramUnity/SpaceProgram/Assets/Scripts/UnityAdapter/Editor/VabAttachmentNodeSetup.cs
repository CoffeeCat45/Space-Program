#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures the AttachmentNode physics layer exists for trigger-based stack snap.
/// </summary>
static class VabAttachmentNodeSetup
{
    [MenuItem("Space Program/VAB/Setup Attachment Node Layer")]
    public static void SetupAttachmentNodeLayerMenu()
    {
        EnsureLayer();
        Debug.Log("VAB: AttachmentNode layer ready.");
    }

    public static void EnsureLayer()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
        );
        SerializedProperty layers = tagManager.FindProperty("layers");
        string layerName = VabAttachmentNodeLayers.LayerName;

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

        Debug.LogError("VAB: no free User Layer slot for '" + layerName + "'.");
    }
}

[InitializeOnLoad]
static class VabAttachmentNodeAutoSetup
{
    static VabAttachmentNodeAutoSetup()
    {
        EditorApplication.delayCall += VabAttachmentNodeSetup.EnsureLayer;
    }
}
#endif
