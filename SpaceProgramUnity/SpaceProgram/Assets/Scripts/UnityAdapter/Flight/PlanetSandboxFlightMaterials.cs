using UnityEngine;

/// <summary>
/// Sandbox-style flight chunk material (GEBCO height + optional vertex paint).
/// Used when <see cref="PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline"/> is on.
/// </summary>
public static class PlanetSandboxFlightMaterials
{
    public static Material CreateFlightChunkMaterial()
    {
        return CreateFlightChunkMaterial(useVertexPaint: true);
    }

    public static Material CreateFlightChunkMaterial(bool useVertexPaint)
    {
        Material material = SpaceProgramShaders.CreateMaterial("SpaceProgram/SandboxEarthGlobe");

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return RuntimeMaterialFactory.CreateLitMaterial(Color.white);
            }

            material = new Material(shader);
        }

        material.name = "PlanetSandboxFlightChunk";
        material.SetColor("_BaseColor", Color.white);

        Shader usedShader = material.shader;
        if (usedShader != null && usedShader.name.Contains("Universal Render Pipeline/Lit"))
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        }

        material.DisableKeyword("_SANDBOX_VERTEX_PAINT");
        return material;
    }

    public static void BindGeographicColorMap(Material material, Texture2D colorMap)
    {
        if (material == null || colorMap == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", colorMap);
        }

        if (material.HasProperty("_ColorMap"))
        {
            material.SetTexture("_ColorMap", colorMap);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", colorMap);
        }
    }
}
