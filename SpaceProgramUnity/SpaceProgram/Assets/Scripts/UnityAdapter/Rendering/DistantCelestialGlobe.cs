using UnityEngine;

/// <summary>
/// Textured lat/lon globe for the non-LOD Earth or Moon (map mode + distant flight).
/// Mesh radius is 0.5 so parent scale = celestial diameter yields the true radius.
/// Distant Earth keeps the same clouds and a thin atmosphere limb as the LOD
/// planet — unit-scale children, not the 115 km flight shell.
/// </summary>
[DisallowMultipleComponent]
public sealed class DistantCelestialGlobe : MonoBehaviour
{
    public const float UnitDiameterMeshRadius = 0.5f;

    public string BodyName { get; private set; }

    Material runtimeMaterial;
    FlightPlanetCloudLayers cloudLayers;
    FlightPlanetAtmosphereSphere atmosphereSphere;

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

        DisableAtmosphere();

        if (isMoon)
        {
            DisableCloudLayers();
        }
        else
        {
            EnsureCloudLayers();
            EnsureAtmosphere();
        }

        ApplyCompositing(mapMode: false);
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

        // Opaque Earth ZWrite at ~4.5 Mm kills the 8 km cloud shell (ZTest).
        // Limb still shows: those pixels miss the disk. Flight Earth globe
        // composites by queue; the Moon's terrain still occludes via ZTest.
        bool writeDepth = mapMode || BodyName == CelestialTerrainRuntime.MoonBodyName;
        runtimeMaterial.SetFloat("_ZWrite", writeDepth ? 1f : 0f);
        runtimeMaterial.SetOverrideTag("RenderType", writeDepth ? "Opaque" : "Transparent");

        if (cloudLayers != null)
        {
            cloudLayers.ApplyRenderLayer(mapMode);
            cloudLayers.PreferNasaMapForDistantGlobe();
        }

        if (atmosphereSphere == null)
        {
            return;
        }

        atmosphereSphere.ApplyRenderLayer(mapMode);

        if (!mapMode)
        {
            atmosphereSphere.ApplyDistantLimbLook();
        }
    }

    public void SyncTransparentShells(Vector3 cameraWorldPosition)
    {
        cloudLayers?.UpdateCameraFacing(cameraWorldPosition);
        atmosphereSphere?.UpdateCameraFacing(cameraWorldPosition);
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
        cloudLayers.ApplyRenderLayer(false);
    }

    void EnsureAtmosphere()
    {
        Transform existing = transform.Find("PlanetAtmosphere");
        GameObject atmosphereObject;

        if (existing != null)
        {
            atmosphereObject = existing.gameObject;
            atmosphereObject.SetActive(true);
        }
        else
        {
            atmosphereObject = new GameObject("PlanetAtmosphere");
            atmosphereObject.transform.SetParent(transform, false);
            atmosphereObject.transform.localPosition = Vector3.zero;
            atmosphereObject.transform.localRotation = Quaternion.identity;
            atmosphereObject.transform.localScale = Vector3.one;
        }

        atmosphereSphere = atmosphereObject.GetComponent<FlightPlanetAtmosphereSphere>();

        if (atmosphereSphere == null)
        {
            atmosphereSphere = atmosphereObject.AddComponent<FlightPlanetAtmosphereSphere>();
        }

        float realRadius = Mathf.Max(1f, (float)CelestialBody.Earth.Radius);
        float altitudeScale = UnitDiameterMeshRadius / realRadius;
        atmosphereSphere.Build(
            UnitDiameterMeshRadius,
            altitudeScale,
            PlanetDistantVisualPolicy.DistantLimbHeightMeters
        );
        atmosphereSphere.ApplyRenderLayer(false);
        atmosphereSphere.ApplyDistantLimbLook();
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

    void DisableAtmosphere()
    {
        atmosphereSphere = null;
        Transform existing = transform.Find("PlanetAtmosphere");

        if (existing != null)
        {
            existing.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            return;
        }

        SyncTransparentShells(camera.transform.position);
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
        material.SetFloat("_ZWrite", 1f);
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
