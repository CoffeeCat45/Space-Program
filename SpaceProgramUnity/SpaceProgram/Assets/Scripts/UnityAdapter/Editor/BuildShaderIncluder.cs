#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class BuildShaderIncluder
{
    internal static readonly string[] RequiredShaderNames =
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Particles/Unlit",
        "Sprites/Default",
        "Unlit/Color",
        "Particles/Standard Unlit",
        "Skybox/6 Sided",
        "SpaceProgram/EarthPlanetLocalChunks",
        "SpaceProgram/MapModeEarthSurface",
        "SpaceProgram/SandboxEarthGlobe",
        "SpaceProgram/PlanetCloudsNasaEnhanced",
        "SpaceProgram/PlanetCloudsNasa",
        "SpaceProgram/PlanetCloudsExperimental",
        "SpaceProgram/PlanetClouds",
        "SpaceProgram/EarthAtmosphere",
        "SpaceProgram/EarthPlanetOcean",
        "SpaceProgram/EarthPlanetSurface",
        "SpaceProgram/OrbitLineOverlay",
        "SpaceProgram/OrbitMarkerOverlay",
    };

    static BuildShaderIncluder()
    {
        EditorApplication.delayCall += EnsureRegistered;
    }

    [MenuItem("Space Program/Register Build Shaders")]
    static void RegisterRequiredShadersMenuItem()
    {
        EnsureRegistered();
        Debug.Log("Space Program: registered shaders for player builds.");
    }

    internal static void EnsureRegistered()
    {
        RegisterRequiredShaders();
    }

    static void RegisterRequiredShaders()
    {
        Object graphicsSettings = Unsupported.GetSerializedAssetInterfaceSingleton(
            "GraphicsSettings"
        );

        if (graphicsSettings == null)
        {
            return;
        }

        SerializedObject serializedGraphicsSettings = new SerializedObject(graphicsSettings);
        SerializedProperty includedShadersProperty = serializedGraphicsSettings.FindProperty(
            "m_AlwaysIncludedShaders"
        );

        if (includedShadersProperty == null || !includedShadersProperty.isArray)
        {
            return;
        }

        List<Shader> shadersToInclude = new List<Shader>();

        for (int i = 0; i < includedShadersProperty.arraySize; i++)
        {
            Shader existingShader = includedShadersProperty
                .GetArrayElementAtIndex(i)
                .objectReferenceValue as Shader;

            if (existingShader != null)
            {
                shadersToInclude.Add(existingShader);
            }
        }

        bool changed = false;

        for (int i = 0; i < RequiredShaderNames.Length; i++)
        {
            Shader shader = Shader.Find(RequiredShaderNames[i]);

            if (shader == null || shadersToInclude.Contains(shader))
            {
                continue;
            }

            shadersToInclude.Add(shader);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        includedShadersProperty.arraySize = shadersToInclude.Count;

        for (int i = 0; i < shadersToInclude.Count; i++)
        {
            includedShadersProperty
                .GetArrayElementAtIndex(i)
                .objectReferenceValue = shadersToInclude[i];
        }

        serializedGraphicsSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(graphicsSettings);
        AssetDatabase.SaveAssets();
        Debug.Log(
            "Space Program: Always Included shaders now " + shadersToInclude.Count + " entries."
        );
    }
}
#endif
