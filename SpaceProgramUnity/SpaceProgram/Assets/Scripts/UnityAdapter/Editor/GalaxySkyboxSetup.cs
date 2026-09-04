#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports galaxy cubemap faces, builds mat_KspSkybox, and assigns RenderSettings.skybox.
/// </summary>
static class GalaxySkyboxSetup
{
    const string SkyboxFolder = "Assets/Content/Environment/Skybox";
    const string ResourcesFolder = "Assets/Resources/Environment";
    const string MaterialAssetPath = SkyboxFolder + "/mat_KspSkybox.mat";
    const string ResourcesMaterialPath = ResourcesFolder + "/mat_KspSkybox.mat";

    static readonly string[] TextureSearchFolders =
    {
        SkyboxFolder,
        "Assets/Texture2D",
    };

    [MenuItem("Space Program/Environment/Setup Galaxy Skybox")]
    public static void SetupFromMenu()
    {
        if (!TrySetup(logSuccess: true))
        {
            Debug.LogError(
                "Galaxy Skybox: place GalaxyTex_Positive/Negative X/Y/Z .png files in " +
                SkyboxFolder +
                " or Assets/Texture2D, then run this menu again."
            );
        }
    }

    internal static void EnsurePlayerSkyboxMaterial()
    {
        EnsureFolder(ResourcesFolder);
        Material source = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);

        if (source == null)
        {
            TrySetup(logSuccess: false);
            source = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
        }

        if (source == null)
        {
            source = AssetDatabase.LoadAssetAtPath<Material>(ResourcesMaterialPath);
        }

        if (source == null)
        {
            Debug.LogWarning(
                "Galaxy Skybox: no mat_KspSkybox to copy into Resources for the player."
            );
            return;
        }

        CopyMaterialToResources(source);
    }

    [InitializeOnLoadMethod]
    static void AutoSetupOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath) != null)
            {
                return;
            }

            TrySetup(logSuccess: false);
        };
    }

    static bool TrySetup(bool logSuccess)
    {
        EnsureFolder(SkyboxFolder);
        EnsureFolder(ResourcesFolder);

        if (!TryResolveFaceTextures(out Texture2D front, out Texture2D back,
                out Texture2D left, out Texture2D right, out Texture2D up, out Texture2D down))
        {
            return false;
        }

        ConfigureImportSettings(front);
        ConfigureImportSettings(back);
        ConfigureImportSettings(left);
        ConfigureImportSettings(right);
        ConfigureImportSettings(up);
        ConfigureImportSettings(down);
        AssetDatabase.SaveAssets();

        Shader skyboxShader = Shader.Find("Skybox/6 Sided");

        if (skyboxShader == null)
        {
            Debug.LogError("Galaxy Skybox: built-in shader Skybox/6 Sided was not found.");
            return false;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);

        if (material == null)
        {
            material = new Material(skyboxShader)
            {
                name = "mat_KspSkybox",
            };
            AssetDatabase.CreateAsset(material, MaterialAssetPath);
        }
        else
        {
            material.shader = skyboxShader;
        }

        material.SetTexture("_FrontTex", front);
        material.SetTexture("_BackTex", back);
        material.SetTexture("_LeftTex", left);
        material.SetTexture("_RightTex", right);
        material.SetTexture("_UpTex", up);
        material.SetTexture("_DownTex", down);
        material.SetFloat("_Exposure", 1.0f);
        material.SetColor("_Tint", Color.white);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        CopyMaterialToResources(material);
        RenderSettings.skybox = material;
        GalaxySkyboxEnvironment.ClearCache();

        if (logSuccess)
        {
            Debug.Log(
                "Galaxy Skybox: mat_KspSkybox created and assigned to Lighting > Environment > Skybox Material."
            );
        }

        return true;
    }

    static bool TryResolveFaceTextures(
        out Texture2D front,
        out Texture2D back,
        out Texture2D left,
        out Texture2D right,
        out Texture2D up,
        out Texture2D down
    )
    {
        front = FindTexture("GalaxyTex_PositiveZ");
        back = FindTexture("GalaxyTex_NegativeZ");
        left = FindTexture("GalaxyTex_PositiveX");
        right = FindTexture("GalaxyTex_NegativeX");
        up = FindTexture("GalaxyTex_PositiveY");
        down = FindTexture("GalaxyTex_NegativeY");

        return
            front != null &&
            back != null &&
            left != null &&
            right != null &&
            up != null &&
            down != null;
    }

    static Texture2D FindTexture(string textureName)
    {
        for (int folderIndex = 0; folderIndex < TextureSearchFolders.Length; folderIndex++)
        {
            string folder = TextureSearchFolders[folderIndex];
            string assetPath = folder + "/" + textureName + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (texture != null)
            {
                return texture;
            }
        }

        string[] guids = AssetDatabase.FindAssets(textureName + " t:Texture2D");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (Path.GetFileNameWithoutExtension(path) == textureName)
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        return null;
    }

    static void ConfigureImportSettings(Texture2D texture)
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

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    static void CopyMaterialToResources(Material sourceMaterial)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(ResourcesMaterialPath);

        if (existing == null)
        {
            Material copy = Object.Instantiate(sourceMaterial);
            copy.name = "mat_KspSkybox";
            AssetDatabase.CreateAsset(copy, ResourcesMaterialPath);
        }
        else
        {
            existing.shader = sourceMaterial.shader;
            existing.CopyPropertiesFromMaterial(sourceMaterial);
            EditorUtility.SetDirty(existing);
        }
    }

    static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
        string leaf = Path.GetFileName(assetFolder);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
