#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports RSS-Terrain Earth DDS files into Assets and wires FlightPlanetSphere slots.
/// Scatters (.mu meshes) are KSP-only and are not imported here.
/// </summary>
static class RssTerrainTexturesSetup
{
    static readonly (string propertyName, string textureBaseName, bool isNormal)[] TextureSlots =
    {
        ("splatInfluenceMap", RssTerrainPaths.InfluenceFileName, false),
        ("lowAlbedoMap", RssTerrainPaths.LowColorFileName, false),
        ("lowNormalMap", RssTerrainPaths.LowNormalFileName, true),
        ("midAlbedoMap", RssTerrainPaths.MidColorFileName, false),
        ("midNormalMap", RssTerrainPaths.MidNormalFileName, true),
        ("highAlbedoMap", RssTerrainPaths.HighColorFileName, false),
        ("highNormalMap", RssTerrainPaths.HighNormalFileName, true),
        ("steepAlbedoMap", RssTerrainPaths.SteepColorFileName, false),
        ("steepNormalMap", RssTerrainPaths.SteepNormalFileName, true),
    };

    [MenuItem("Space Program/Environment/Configure RSS-Terrain Earth Textures")]
    public static void ConfigureFromMenu()
    {
        if (!EnsureImported(logSuccess: true))
        {
            Debug.LogError(
                "RSS-Terrain: place DDS files in " +
                RssTerrainPaths.ResolveEarthPluginDataDirectory() +
                " or copy them into " +
                RssTerrainPaths.EarthAssetFolder +
                "."
            );
        }
    }

    public static bool EnsureImported(bool logSuccess = false)
    {
#if UNITY_EDITOR
        return TryConfigure(logSuccess);
#else
        return false;
#endif
    }

    [InitializeOnLoadMethod]
    static void AutoConfigureOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!RssTerrainPaths.EarthTerrainPackExistsOnDisk())
            {
                return;
            }

            TryConfigure(logSuccess: false);
        };
    }

    [InitializeOnEnterPlayMode]
    static void AutoConfigureBeforePlay()
    {
        if (!RssTerrainPaths.EarthTerrainPackExistsOnDisk())
        {
            return;
        }

        TryConfigure(logSuccess: false);
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
            string assetPath = RssTerrainPaths.ResolveAssetTexturePath(
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
                "RSS-Terrain Earth: textures imported to " +
                RssTerrainPaths.EarthAssetFolder +
                " and assigned to FlightPlanetSphere."
            );
        }

        return true;
    }

    static bool EnsureAssetTexturesOnDisk()
    {
        string assetDirectory = GetAbsoluteAssetPath(RssTerrainPaths.EarthAssetFolder);

        if (!Directory.Exists(assetDirectory))
        {
            Directory.CreateDirectory(assetDirectory);
        }

        if (!RssTerrainPaths.TryResolveEarthPluginDataDirectory(out string sourceDirectory))
        {
            return Directory.GetFiles(assetDirectory, "*.dds").Length > 0;
        }

        bool copiedAny = false;

        for (int i = 0; i < TextureSlots.Length; i++)
        {
            string assetPath = RssTerrainPaths.ResolveAssetTexturePath(
                TextureSlots[i].textureBaseName
            );
            string absoluteAssetPath = GetAbsoluteAssetPath(assetPath);

            if (File.Exists(absoluteAssetPath))
            {
                continue;
            }

            if (!RssTerrainPaths.TryResolveTexturePath(
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
            RssTerrainPaths.InfluenceFileName,
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
                AssignTexture(serializedObject, TextureSlots[i].propertyName, textures[i]);
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(planetSphere);
        }
    }

    static void AssignTexture(
        SerializedObject serializedObject,
        string propertyName,
        Texture2D texture
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || texture == null)
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
