#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-time Editor setup: SelectionHighlight layer + URP Render Objects outline pass.
/// </summary>
static class VabSelectionOutlineSetup
{
    const string LayerName = VabSelectionHighlighter.SelectionHighlightLayerName;
    const string HeldPartLayerName = VabColliderUtility.HeldPartLayerName;
    const string MaterialAssetPath = "Assets/Materials/VabSelectionOutline.mat";
    const string FeatureName = "SelectionOutline";

    [MenuItem("Space Program/VAB/Setup Selection Outline Rendering")]
    public static void SetupSelectionOutlineRendering()
    {
        EnsureLayer(LayerName);
        EnsureLayer(HeldPartLayerName);
        Material outlineMaterial = EnsureOutlineMaterial();
        int configuredRenderers = ConfigureUniversalRenderers(outlineMaterial);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "VAB: selection outline setup complete. Layer='" + LayerName +
            "', material='" + MaterialAssetPath +
            "', renderer features updated=" + configuredRenderers + "."
        );
    }

    public static void EnsureLayer()
    {
        EnsureLayer(LayerName);
        EnsureLayer(HeldPartLayerName);
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

        Debug.LogError("VAB: no free User Layer slot for '" + layerName + "'.");
    }

    static Material EnsureOutlineMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);

        if (existing != null)
        {
            return existing;
        }

        string folder = System.IO.Path.GetDirectoryName(MaterialAssetPath);

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color");

        Material material = new Material(shader)
        {
            color = new Color(0.2f, 0.88f, 1f, 0.85f),
        };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.renderQueue = (int)RenderQueue.Transparent + 10;

        AssetDatabase.CreateAsset(material, MaterialAssetPath);
        return material;
    }

    static int ConfigureUniversalRenderers(Material outlineMaterial)
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");
        int configured = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);

            if (rendererData == null || HasSelectionOutlineFeature(rendererData))
            {
                continue;
            }

            RenderObjects feature = ScriptableObject.CreateInstance<RenderObjects>();
            feature.name = FeatureName;
            feature.settings.passTag = FeatureName;
            feature.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            feature.settings.filterSettings.LayerMask = 1 << LayerMask.NameToLayer(LayerName);
            feature.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            feature.settings.overrideMaterial = outlineMaterial;
            feature.settings.overrideMaterialPassIndex = 0;

            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(rendererData);
            configured++;
        }

        return configured;
    }

    static bool HasSelectionOutlineFeature(UniversalRendererData rendererData)
    {
        List<ScriptableRendererFeature> features = rendererData.rendererFeatures;

        for (int i = 0; i < features.Count; i++)
        {
            if (features[i] != null && features[i].name == FeatureName)
            {
                return true;
            }
        }

        return false;
    }
}
#endif

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
static class VabSelectionOutlineAutoSetup
{
    static VabSelectionOutlineAutoSetup()
    {
        UnityEditor.EditorApplication.delayCall += VabSelectionOutlineSetup.EnsureLayer;
    }
}
#endif
