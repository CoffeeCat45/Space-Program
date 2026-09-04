using UnityEngine;

static class RuntimeMaterialFactory
{
    static Shader cachedFallbackShader;

    static readonly string[] SolidShaderNames =
    {
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Lit",
        "Unlit/Color",
        "Sprites/Default",
    };

    static readonly string[] LitShaderNames =
    {
        "Universal Render Pipeline/Lit",
        "Standard",
        "Universal Render Pipeline/Unlit",
        "Unlit/Color",
    };

    static readonly string[] LineShaderNames =
    {
        "Sprites/Default",
        "Universal Render Pipeline/Unlit",
        "Unlit/Color",
    };

    static readonly string[] ParticleShaderNames =
    {
        "Universal Render Pipeline/Particles/Unlit",
        "Particles/Standard Unlit",
        "Sprites/Default",
        "Universal Render Pipeline/Unlit",
    };

    public static Shader ResolveShader(params string[] shaderNames)
    {
        if (shaderNames != null)
        {
            for (int i = 0; i < shaderNames.Length; i++)
            {
                if (string.IsNullOrEmpty(shaderNames[i]))
                {
                    continue;
                }

                Shader shader = Shader.Find(shaderNames[i]);

                if (shader != null)
                {
                    return shader;
                }
            }
        }

        return ResolveFallbackShader();
    }

    static Shader ResolveFallbackShader()
    {
        if (cachedFallbackShader != null)
        {
            return cachedFallbackShader;
        }

        GameObject tempObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Renderer renderer = tempObject.GetComponent<Renderer>();
        cachedFallbackShader = renderer != null ?
            renderer.sharedMaterial.shader :
            null;
        Object.Destroy(tempObject);

        return cachedFallbackShader;
    }

    public static Material CreateMaterial(params string[] shaderNames)
    {
        Shader shader = ResolveShader(shaderNames);

        if (shader == null)
        {
            return null;
        }

        return new Material(shader);
    }

    public static Material CreateColoredMaterial(
        Color color,
        params string[] shaderNames
    )
    {
        string[] resolvedShaderNames = shaderNames != null && shaderNames.Length > 0 ?
            shaderNames :
            SolidShaderNames;
        Material material = CreateMaterial(resolvedShaderNames);

        if (material == null)
        {
            return null;
        }

        ApplyColor(material, color);
        return material;
    }

    public static Material CreateSolidMaterial(Color color)
    {
        return CreateColoredMaterial(color, SolidShaderNames);
    }

    public static Material CreateLitMaterial(Color color)
    {
        return CreateColoredMaterial(color, LitShaderNames);
    }

    public static Material CreateLineMaterial()
    {
        return CreateMaterial(LineShaderNames);
    }

    public static Material CreateOrbitLineMaterial(Color color)
    {
        Material material = CreateMaterial(
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Unlit/Color"
        );

        if (material == null)
        {
            return null;
        }

        ApplyColor(material, color);
        ApplyOrbitOverlayDepthSettings(material);
        return material;
    }

    public static Material CreateOrbitOverlayMarkerMaterial(Color color)
    {
        Material material = CreateColoredMaterial(color, SolidShaderNames);

        if (material == null)
        {
            return null;
        }

        ApplyOrbitOverlayDepthSettings(material);
        return material;
    }

    public static void ApplyOrbitOverlayRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        Material material = renderer.material;

        if (material == null)
        {
            return;
        }

        ApplyOrbitOverlayDepthSettings(material);
    }

    public static void ApplyOrbitOverlayDepthSettings(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt(
            "_DstBlend",
            (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
        );
        material.SetInt("_ZWrite", 0);
        material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        material.renderQueue = 4000;
    }

    public static Material CreateParticleMaterial(Color color)
    {
        return CreateColoredMaterial(color, ParticleShaderNames);
    }

    public static Material CreateTransparentSolidMaterial(Color color)
    {
        Material material = CreateMaterial(SolidShaderNames);

        if (material == null)
        {
            return null;
        }

        ConfigureMaterialTransparency(material);
        ApplyColor(material, color);
        return material;
    }

    static void ConfigureMaterialTransparency(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt(
            "_DstBlend",
            (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
        );
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    public static void ApplyColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    public static void ApplyColor(Renderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        Material material = CreateSolidMaterial(color);

        if (material != null)
        {
            renderer.material = material;
        }
    }
}
