using UnityEngine;

/// <summary>
/// Resolves <c>SpaceProgram/*</c> shaders in the player. <see cref="Shader.Find"/>
/// alone is not enough: Always Included can be empty, and <c>shader_feature</c>
/// variants are stripped unless a Resources material references them.
/// </summary>
public static class SpaceProgramShaders
{
    public const string PlayerMaterialsResourceFolder = "PlayerMaterials";

    static PlayerIncludedShaders cachedBundle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cachedBundle = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void WarmupForPlayer()
    {
        EnsureBundle();
        Resources.LoadAll<Material>(PlayerMaterialsResourceFolder);
    }

    public static Shader Find(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName))
        {
            return null;
        }

        Shader found = Shader.Find(shaderName);
        if (found != null)
        {
            return found;
        }

        PlayerIncludedShaders bundle = EnsureBundle();
        if (bundle != null && bundle.shaders != null)
        {
            for (int i = 0; i < bundle.shaders.Length; i++)
            {
                Shader shader = bundle.shaders[i];
                if (shader != null && shader.name == shaderName)
                {
                    return shader;
                }
            }
        }

        Material baked = LoadBakedMaterial(shaderName);
        return baked != null ? baked.shader : null;
    }

    public static Material CreateMaterial(string shaderName)
    {
        Material baked = LoadBakedMaterial(shaderName);
        if (baked != null)
        {
            return new Material(baked);
        }

        Shader shader = Find(shaderName);
        if (shader == null)
        {
            return null;
        }

        return new Material(shader)
        {
            name = shaderName,
        };
    }

    static PlayerIncludedShaders EnsureBundle()
    {
        if (cachedBundle != null)
        {
            return cachedBundle;
        }

        cachedBundle = Resources.Load<PlayerIncludedShaders>(
            PlayerIncludedShaders.ResourcesAssetName
        );
        return cachedBundle;
    }

    static Material LoadBakedMaterial(string shaderName)
    {
        const string prefix = "SpaceProgram/";
        if (!shaderName.StartsWith(prefix))
        {
            return null;
        }

        string resourceName = PlayerMaterialsResourceFolder + "/" +
            shaderName.Substring(prefix.Length);
        return Resources.Load<Material>(resourceName);
    }
}
