using UnityEngine;

/// <summary>
/// Static sea-level ocean shell. Land chunks pierce through; NASA-blue clip hides the
/// sphere over continents. Inland lakes at other elevations are painted on terrain chunks.
/// Geometry stays a sphere — waves are shader-only.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlightPlanetOceanSphere : MonoBehaviour
{
    public const int DefaultIcosphereSubdivisions = 4;

    [SerializeField] int icosphereSubdivisions = DefaultIcosphereSubdivisions;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh runtimeMesh;
    Material oceanMaterial;

    public void Build(float seaLevelRadius, Texture2D earthColorMap)
    {
        EnsureComponents();
        runtimeMesh = FlightPlanetMapSphere.BuildIcosphereMeshPublic(
            Mathf.Clamp(icosphereSubdivisions, 1, 5),
            seaLevelRadius
        );
        runtimeMesh.name = "PlanetOceanSphere";
        meshFilter.sharedMesh = runtimeMesh;
        meshRenderer.sharedMaterial = EnsureOceanMaterial(earthColorMap);
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        FlightRenderLayers.ApplyLocalPhysicsLayer(gameObject);
    }

    public void Build(float seaLevelRadius)
    {
        Build(seaLevelRadius, PlanetTerrainTextureResources.EarthColorMap);
    }

    Material EnsureOceanMaterial(Texture2D earthColorMap)
    {
        if (oceanMaterial == null)
        {
            Shader shader = SpaceProgramShaders.Find("SpaceProgram/EarthPlanetOcean");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            oceanMaterial = new Material(shader)
            {
                name = "PlanetOcean",
            };
        }

        if (oceanMaterial.HasProperty("_PlanetColorMap") && earthColorMap != null)
        {
            oceanMaterial.SetTexture("_PlanetColorMap", earthColorMap);
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(oceanMaterial);
        }

        oceanMaterial.renderQueue = 3000;
        return oceanMaterial;
    }

    public void ApplyEarthColor(Texture2D earthColorMap)
    {
        if (oceanMaterial == null)
        {
            return;
        }

        if (oceanMaterial.HasProperty("_PlanetColorMap") && earthColorMap != null)
        {
            oceanMaterial.SetTexture("_PlanetColorMap", earthColorMap);
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(oceanMaterial);
        }
    }

    void EnsureComponents()
    {
        meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
    }

    void OnDestroy()
    {
        if (runtimeMesh != null)
        {
            Destroy(runtimeMesh);
        }

        if (oceanMaterial != null)
        {
            Destroy(oceanMaterial);
        }
    }
}
