using UnityEngine;

/// <summary>
/// Textured lat/lon globe for the non-LOD Earth or Moon (map mode + distant flight).
/// Mesh radius is 0.5 so parent scale = celestial diameter yields the true radius.
/// </summary>
[DisallowMultipleComponent]
public sealed class DistantCelestialGlobe : MonoBehaviour
{
    public const float UnitDiameterMeshRadius = 0.5f;

    public string BodyName { get; private set; }

    Material runtimeMaterial;
    FlightPlanetCloudLayers cloudLayers;

    public static DistantCelestialGlobe Attach(GameObject bodyObject, string bodyName)
    {
        DistantCelestialGlobe globe = bodyObject.GetComponent<DistantCelestialGlobe>();

        if (globe == null)
        {
            globe = bodyObject.AddComponent<DistantCelestialGlobe>();
        }

        globe.BodyName = bodyName;
        globe.Rebuild();
        return globe;
    }

    public static void RebuildAllForBody(string bodyName)
    {
        DistantCelestialGlobe[] globes = Object.FindObjectsByType<DistantCelestialGlobe>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < globes.Length; i++)
        {
            DistantCelestialGlobe globe = globes[i];

            if (globe != null && globe.BodyName == bodyName)
            {
                globe.Rebuild();
            }
        }
    }

    public void Rebuild()
    {
        bool isMoon = BodyName == CelestialTerrainRuntime.MoonBodyName;
        Texture2D colorMap = isMoon ?
            PlanetTerrainTextureResources.GetMoonColorMap() :
            MapModeEarthResources.GetColorMap();
        Color fallback = isMoon ?
            new Color(0.55f, 0.55f, 0.52f, 1f) :
            new Color(0.2f, 0.6f, 1f);

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        runtimeMaterial = CreateGlobeMaterial(colorMap, fallback, isMoon);

        FlightPlanetMapSphere mapSphere = GetComponent<FlightPlanetMapSphere>();

        if (mapSphere == null)
        {
            mapSphere = gameObject.AddComponent<FlightPlanetMapSphere>();
        }

        mapSphere.Build(
            runtimeMaterial,
            UnitDiameterMeshRadius,
            FlightPlanetMapSphere.DefaultIcosphereSubdivisions,
            applyScaledSpaceLayer: false
        );
        ApplyCompositing(mapMode: false);

        if (isMoon)
        {
            DisableCloudLayers();
        }
        else
        {
            EnsureCloudLayers();
        }
    }

    public void SyncCloudAnimation(double simulationTimeSeconds)
    {
        cloudLayers?.SyncAnimation(simulationTimeSeconds);
    }

    public void ApplyCompositing(bool mapMode)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.renderQueue = mapMode ?
            (int)UnityEngine.Rendering.RenderQueue.Geometry :
            PlanetDistantVisualPolicy.FlightDistantGlobeRenderQueue;
    }

    void EnsureCloudLayers()
    {
        Transform existing = transform.Find("PlanetClouds");
        GameObject cloudsObject;

        if (existing != null)
        {
            cloudsObject = existing.gameObject;
            cloudsObject.SetActive(true);
        }
        else
        {
            cloudsObject = new GameObject("PlanetClouds");
            cloudsObject.transform.SetParent(transform, false);
            cloudsObject.transform.localPosition = Vector3.zero;
            cloudsObject.transform.localRotation = Quaternion.identity;
            cloudsObject.transform.localScale = Vector3.one;
        }

        cloudLayers = cloudsObject.GetComponent<FlightPlanetCloudLayers>();

        if (cloudLayers == null)
        {
            cloudLayers = cloudsObject.AddComponent<FlightPlanetCloudLayers>();
        }

        float realRadius = Mathf.Max(1f, (float)CelestialBody.Earth.Radius);
        float altitudeScale = UnitDiameterMeshRadius / realRadius;
        cloudLayers.Build(UnitDiameterMeshRadius, altitudeScale);
        cloudLayers.PreferNasaMapForDistantGlobe();
    }

    void DisableCloudLayers()
    {
        cloudLayers = null;
        Transform existing = transform.Find("PlanetClouds");

        if (existing != null)
        {
            existing.gameObject.SetActive(false);
        }
    }

    static Material CreateGlobeMaterial(Texture2D colorMap, Color fallback, bool isMoon)
    {
        Shader shader = SpaceProgramShaders.Find("SpaceProgram/MapModeEarthSurface");

        if (shader == null)
        {
            shader = SpaceProgramShaders.Find("SpaceProgram/EarthPlanetSurface");
        }

        Material material = shader != null ?
            new Material(shader) :
            RuntimeMaterialFactory.CreateLitMaterial(fallback);

        material.name = isMoon ? "DistantMoonGlobe" : "DistantEarthGlobe";
        material.SetColor("_ColorTint", new Color(1.1f, 1.1f, 1.1f, 1f));
        material.SetColor("_FallbackColor", fallback);

        if (colorMap != null)
        {
            PlanetParallaxTextures.ConfigureTextureSampling(colorMap);
            if (material.HasProperty("_ColorMap"))
            {
                material.SetTexture("_ColorMap", colorMap);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", colorMap);
            }
        }

        if (isMoon)
        {
            PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(material);
        }
        else
        {
            PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(material);
        }

        return material;
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
