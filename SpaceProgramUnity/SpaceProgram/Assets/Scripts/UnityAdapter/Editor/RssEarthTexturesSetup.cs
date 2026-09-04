#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configures RSS Earth DDS import settings and wires FlightPlanetSphere color/normal slots.
/// EarthHeight.dds (R16) is decoded at runtime — Unity cannot import it as Texture2D.
/// </summary>
static class RssEarthTexturesSetup
{
    [MenuItem("Space Program/Environment/Configure RSS Earth Textures")]
    public static void ConfigureFromMenu()
    {
        if (!TryConfigure(logSuccess: true))
        {
            Debug.LogError(
                "RSS Earth Textures: place EarthColor.dds and Earth_NRM.dds in " +
                PlanetSurfaceMaps.RssTexturesAssetFolder +
                ". EarthHeight.dds must live in PluginData/ and is loaded at runtime."
            );
        }
    }

    [InitializeOnLoadMethod]
    static void AutoConfigureOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            string colorPath = Path.Combine(
                Application.dataPath,
                "Content",
                "Environment",
                "RSS-Textures",
                "EarthColor.dds"
            );

            if (!File.Exists(colorPath))
            {
                return;
            }

            TryConfigure(logSuccess: false);
        };
    }

    static bool TryConfigure(bool logSuccess)
    {
        Texture2D colorMap = LoadTexture(PlanetSurfaceMaps.EarthColorAssetPath);
        Texture2D normalMap = LoadTexture(PlanetSurfaceMaps.EarthNormalAssetPath);

        if (colorMap == null || normalMap == null)
        {
            return false;
        }

        ConfigureColorMap(colorMap);
        ConfigureNormalMap(normalMap);
        AssetDatabase.SaveAssets();
        AssignFlightPlanetSphereTextures(colorMap, normalMap);

        if (logSuccess)
        {
            Debug.Log(
                "RSS Earth Textures: color/normal import settings applied. " +
                "EarthHeight.dds is loaded at runtime from PluginData/ (R16 is not importable)."
            );
        }

        return true;
    }

    static Texture2D LoadTexture(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    static void ConfigureColorMap(Texture2D texture)
    {
        ConfigureImporter(
            texture,
            TextureImporterType.Default,
            sRgb: true,
            readable: false,
            wrapMode: TextureWrapMode.Clamp
        );
    }

    static void ConfigureNormalMap(Texture2D texture)
    {
        ConfigureImporter(
            texture,
            TextureImporterType.NormalMap,
            sRgb: false,
            readable: false,
            wrapMode: TextureWrapMode.Clamp
        );
    }

    static void ConfigureImporter(
        Texture2D texture,
        TextureImporterType textureType,
        bool sRgb,
        bool readable,
        TextureWrapMode wrapMode
    )
    {
        if (texture == null)
        {
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        bool changed = false;

        if (importer.textureType != textureType)
        {
            importer.textureType = textureType;
            changed = true;
        }

        if (importer.sRGBTexture != sRgb)
        {
            importer.sRGBTexture = sRgb;
            changed = true;
        }

        if (importer.isReadable != readable)
        {
            importer.isReadable = readable;
            changed = true;
        }

        if (importer.wrapMode != wrapMode)
        {
            importer.wrapMode = wrapMode;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Trilinear)
        {
            importer.filterMode = FilterMode.Trilinear;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    static void AssignFlightPlanetSphereTextures(
        Texture2D colorMap,
        Texture2D normalMap
    )
    {
        FlightPlanetSphere[] planetSpheres = Object.FindObjectsByType<FlightPlanetSphere>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < planetSpheres.Length; i++)
        {
            FlightPlanetSphere planetSphere = planetSpheres[i];

            if (planetSphere == null)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(planetSphere);
            AssignIfEmpty(serializedObject, "earthColorMap", colorMap);
            AssignIfEmpty(serializedObject, "earthNormalMap", normalMap);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(planetSphere);
        }
    }

    static void AssignIfEmpty(
        SerializedObject serializedObject,
        string propertyName,
        Texture2D texture
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue != null || texture == null)
        {
            return;
        }

        property.objectReferenceValue = texture;
    }
}
#endif
