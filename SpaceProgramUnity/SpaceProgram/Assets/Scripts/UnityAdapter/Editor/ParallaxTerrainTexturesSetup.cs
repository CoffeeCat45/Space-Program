#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Copies Kerbin Parallax DDS files into Assets and wires FlightPlanetSphere texture slots.
/// Unity must own rendering textures — runtime DDS decode is only a fallback.
/// </summary>
static class ParallaxTerrainTexturesSetup
{
    static readonly (string propertyName, string textureBaseName, bool isNormal)[] TextureSlots =
    {
        ("splatInfluenceMap", ParallaxTerrainPaths.InfluenceTextureName, false),
        ("lowAlbedoMap", ParallaxTerrainPaths.LowAlbedoTextureName, false),
        ("lowNormalMap", ParallaxTerrainPaths.LowNormalTextureName, true),
        ("midAlbedoMap", ParallaxTerrainPaths.MidAlbedoTextureName, false),
        ("midNormalMap", ParallaxTerrainPaths.MidNormalTextureName, true),
        ("highAlbedoMap", ParallaxTerrainPaths.HighAlbedoTextureName, false),
        ("highNormalMap", ParallaxTerrainPaths.HighNormalTextureName, true),
        ("steepAlbedoMap", ParallaxTerrainPaths.SteepAlbedoTextureName, false),
        ("steepNormalMap", ParallaxTerrainPaths.SteepNormalTextureName, true),
    };

    [MenuItem("Space Program/Environment/Configure Parallax Terrain Textures")]
    public static void ConfigureFromMenu()
    {
        if (!TryConfigure(logSuccess: true))
        {
            Debug.LogError(
                "Parallax Terrain: place Parallax_StockTerrainTextures/Kerbin/PluginData next to " +
                "the Unity project, or copy DDS files into " +
                ParallaxTerrainPaths.ParallaxAssetFolder +
                "."
            );
        }
    }

    [InitializeOnLoadMethod]
    static void AutoConfigureOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            string influenceAssetPath = ParallaxTerrainPaths.ResolveAssetTexturePath(
                ParallaxTerrainPaths.InfluenceTextureName
            );

            if (!File.Exists(GetAbsoluteAssetPath(influenceAssetPath)) &&
                !ParallaxTerrainPaths.TerrainPackExistsOnDisk(
                    ParallaxTerrainPaths.FlightParallaxBodyName,
                    ParallaxTerrainPaths.LowAlbedoTextureName
                ))
            {
                return;
            }

            TryConfigure(logSuccess: false);
        };
    }

    static bool TryConfigure(bool logSuccess)
    {
        if (!EnsureAssetTexturesOnDisk())
        {
            return false;
        }

        AssetDatabase.Refresh();

        Texture2D[] textures = new Texture2D[TextureSlots.Length];
        bool allLoaded = true;

        for (int i = 0; i < TextureSlots.Length; i++)
        {
            string assetPath = ParallaxTerrainPaths.ResolveAssetTexturePath(
                TextureSlots[i].textureBaseName
            );
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (texture == null)
            {
                allLoaded = false;
                continue;
            }

            ConfigureImporter(texture, TextureSlots[i].isNormal, TextureSlots[i].textureBaseName);
            textures[i] = texture;
        }

        if (!allLoaded)
        {
            return false;
        }

        AssetDatabase.SaveAssets();
        AssignFlightPlanetSphereTextures(textures);

        if (logSuccess)
        {
            Debug.Log(
                "Parallax Terrain: Kerbin textures imported from " +
                ParallaxTerrainPaths.ParallaxAssetFolder +
                " and assigned to FlightPlanetSphere."
            );
        }

        return true;
    }

    static bool EnsureAssetTexturesOnDisk()
    {
        string assetDirectory = GetAbsoluteAssetPath(ParallaxTerrainPaths.ParallaxAssetFolder);

        if (!Directory.Exists(assetDirectory))
        {
            Directory.CreateDirectory(assetDirectory);
        }

        bool copiedAny = false;

        for (int i = 0; i < TextureSlots.Length; i++)
        {
            string assetPath = ParallaxTerrainPaths.ResolveAssetTexturePath(
                TextureSlots[i].textureBaseName
            );
            string absoluteAssetPath = GetAbsoluteAssetPath(assetPath);

            if (File.Exists(absoluteAssetPath))
            {
                continue;
            }

            if (!ParallaxTerrainPaths.TryResolveTexturePath(
                    ParallaxTerrainPaths.FlightParallaxBodyName,
                    TextureSlots[i].textureBaseName,
                    out string sourcePath
                ))
            {
                return false;
            }

            File.Copy(sourcePath, absoluteAssetPath, overwrite: false);
            copiedAny = true;
        }

        if (copiedAny)
        {
            AssetDatabase.Refresh();
        }

        return true;
    }

    static void ConfigureImporter(Texture2D texture, bool isNormal, string textureBaseName)
    {
        string assetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        bool changed = false;
        TextureImporterType targetType = isNormal ?
            TextureImporterType.NormalMap :
            TextureImporterType.Default;
        TextureWrapMode targetWrap = string.Equals(
            textureBaseName,
            ParallaxTerrainPaths.InfluenceTextureName,
            System.StringComparison.OrdinalIgnoreCase
        ) ?
            TextureWrapMode.Clamp :
            TextureWrapMode.Repeat;

        if (importer.textureType != targetType)
        {
            importer.textureType = targetType;
            changed = true;
        }

        if (importer.sRGBTexture != !isNormal)
        {
            importer.sRGBTexture = !isNormal;
            changed = true;
        }

        if (importer.wrapMode != targetWrap)
        {
            importer.wrapMode = targetWrap;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Trilinear)
        {
            importer.filterMode = FilterMode.Trilinear;
            changed = true;
        }

        if (importer.mipmapEnabled != true)
        {
            importer.mipmapEnabled = true;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    static void AssignFlightPlanetSphereTextures(Texture2D[] textures)
    {
        FlightPlanetSphere[] planetSpheres = Object.FindObjectsByType<FlightPlanetSphere>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int sphereIndex = 0; sphereIndex < planetSpheres.Length; sphereIndex++)
        {
            FlightPlanetSphere planetSphere = planetSpheres[sphereIndex];

            if (planetSphere == null)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(planetSphere);

            for (int i = 0; i < TextureSlots.Length; i++)
            {
                AssignIfEmpty(serializedObject, TextureSlots[i].propertyName, textures[i]);
            }

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

    static string GetAbsoluteAssetPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
#endif
