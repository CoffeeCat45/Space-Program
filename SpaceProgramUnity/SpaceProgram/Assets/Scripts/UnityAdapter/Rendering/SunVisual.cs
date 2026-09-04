using UnityEngine;

[DefaultExecutionOrder(400)]
public class SunVisual : MonoBehaviour
{
    public Vector3 sunWorldPosition = new Vector3(260.0f, 150.0f, 0.0f);
    public float sunSize = 5.0f;
    public float directionalLightIntensity = 1.35f;
    public float pointLightIntensity = 4.0f;
    public float pointLightRange = 180.0f;
    public Color sunColor = Color.white;
    public Color sunCoreColor = new Color(1.0f, 0.84f, 0.22f);

    const float FlightSunBillboardDistance = 1_800_000f;
    const float FlightSunBillboardDiameter = 120_000f;

    static readonly Vector3 FallbackFlightSunDirection =
        new Vector3(0.28f, 0.72f, -0.42f);

    Simulation simulation;
    Camera followCamera;
    float worldScale = 1.0f;
    bool isMapModeActive;
    bool useSimulationSun;
    global::SimVector3 mapRenderOrigin = global::SimVector3.Zero();
    bool hasMapRenderOrigin;

    GameObject sunObject;
    GameObject sunCoreObject;
    Light directionalLight;
    Light pointLight;
    Material sunCoreMaterial;

    public void Setup(Camera camera)
    {
        followCamera = camera;
        EnsureSunObject();
        DisableDefaultDirectionalLights();
        isMapModeActive = false;
        worldScale = WorldVisualBridge.FlightPositionScale;
        UpdateSunTransform();
    }

    internal void SetWorldScale(float newWorldScale)
    {
        worldScale = newWorldScale;
    }

    internal void SetRenderMode(bool mapModeActive, float positionScale)
    {
        isMapModeActive = mapModeActive;
        worldScale = positionScale;
    }

    internal void SetMapRenderOrigin(global::SimVector3 renderOrigin)
    {
        mapRenderOrigin = renderOrigin;
        hasMapRenderOrigin = true;
    }

    internal void SetSimulation(Simulation newSimulation, float newWorldScale)
    {
        simulation = newSimulation;
        worldScale = newWorldScale;
        RefreshSimulationSunBinding();
    }

    bool CanUseSimulationSun =>
        useSimulationSun &&
        simulation != null &&
        simulation.sun != null;

    void RefreshSimulationSunBinding()
    {
        useSimulationSun =
            simulation != null &&
            simulation.ScenarioHasSun() &&
            simulation.sun != null;
    }

    void LateUpdate()
    {
        if (useSimulationSun && simulation == null)
        {
            useSimulationSun = false;
        }

        UpdateSunTransform();
    }

    void EnsureSunObject()
    {
        if (sunObject == null)
        {
            sunObject = new GameObject("Visual Sun");
        }

        sunCoreObject = EnsureSphereChild(sunObject, sunCoreObject, "Sun Core");
        RemoveOldGlowObject();

        Renderer coreRenderer = sunCoreObject.GetComponent<Renderer>();

        if (sunCoreMaterial == null)
        {
            sunCoreMaterial = CreateSunCoreMaterial();
        }

        if (sunCoreMaterial != null)
        {
            coreRenderer.sharedMaterial = sunCoreMaterial;
        }

        coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coreRenderer.receiveShadows = false;

        if (directionalLight == null)
        {
            directionalLight = sunObject.GetComponent<Light>();

            if (directionalLight == null)
            {
                directionalLight = sunObject.AddComponent<Light>();
            }
        }

        directionalLight.type = LightType.Directional;
        directionalLight.color = sunColor;
        directionalLight.intensity = directionalLightIntensity;
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.shadowStrength = 0.82f;
        directionalLight.shadowBias = 0.05f;
        directionalLight.shadowNormalBias = 1.0f;
        RenderSettings.sun = directionalLight;

        Transform pointLightTransform = sunObject.transform.Find("Sun Point Light");

        if (pointLightTransform == null)
        {
            GameObject pointLightObject = new GameObject("Sun Point Light");
            pointLightObject.transform.SetParent(sunObject.transform);
            pointLightTransform = pointLightObject.transform;
        }

        pointLight = pointLightTransform.GetComponent<Light>();

        if (pointLight == null)
        {
            pointLight = pointLightTransform.gameObject.AddComponent<Light>();
        }

        pointLight.type = LightType.Point;
        pointLight.color = sunColor;
        pointLight.intensity = pointLightIntensity;
        pointLight.range = pointLightRange;
        pointLight.shadows = LightShadows.None;
    }

    GameObject EnsureSphereChild(
        GameObject parentObject,
        GameObject currentObject,
        string childName
    )
    {
        if (currentObject != null)
        {
            return currentObject;
        }

        Transform existingTransform = parentObject.transform.Find(childName);

        if (existingTransform != null)
        {
            return existingTransform.gameObject;
        }

        GameObject childObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        childObject.name = childName;
        childObject.transform.SetParent(parentObject.transform);

        Collider collider = childObject.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        return childObject;
    }

    void DisableDefaultDirectionalLights()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            if (
                lights[i] != null &&
                lights[i] != directionalLight &&
                lights[i].type == LightType.Directional
            )
            {
                lights[i].enabled = false;
            }
        }
    }

    Material CreateSunCoreMaterial()
    {
        Material material = RuntimeMaterialFactory.CreateColoredMaterial(
            sunCoreColor,
            new[]
            {
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                "Sprites/Default",
            }
        );

        if (material == null)
        {
            return null;
        }

        Color emission = sunCoreColor * 5.0f;

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", emission);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", emission);
        }

        material.renderQueue = 4500;

        return material;
    }

    Material CreateEmissiveMaterial(Color color, float emissionStrength)
    {
        Material material = RuntimeMaterialFactory.CreateSolidMaterial(color);

        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emissionStrength);
        }

        return material;
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    void UpdateSunTransform()
    {
        if (sunObject == null || sunCoreObject == null || directionalLight == null)
        {
            return;
        }

        if (isMapModeActive)
        {
            UpdateMapSun();
            return;
        }

        UpdateFlightSun();
    }

    void UpdateFlightSun()
    {
        if (pointLight != null)
        {
            pointLight.enabled = false;
        }

        sunCoreObject.SetActive(true);

        Vector3 sunDirection = ResolveFlightSunDirection();
        float visibility = CanUseSimulationSun ?
            (float)simulation.GetDirectSunVisibilityAtShip() :
            1.0f;

        directionalLight.enabled = true;
        directionalLight.intensity = directionalLightIntensity * Mathf.Lerp(0.04f, 1.0f, visibility);
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
        RenderSettings.sun = directionalLight;

        Vector3 cameraPosition = followCamera != null ?
            followCamera.transform.position :
            Vector3.zero;
        sunObject.transform.position = cameraPosition + sunDirection * FlightSunBillboardDistance;
        sunCoreObject.transform.localPosition = Vector3.zero;
        sunCoreObject.transform.localScale = Vector3.one * FlightSunBillboardDiameter;
    }

    Vector3 ResolveFlightSunDirection()
    {
        if (!CanUseSimulationSun)
        {
            return FallbackFlightSunDirection.normalized;
        }

        if (simulation.TryGetDirectionFromShipToSun(out global::SimVector3 sunDirSim))
        {
            Vector3 shipRadialUnity = WorldVisualBridge.ResolveFlightPlanetUpRadialUnity(
                simulation
            );
            Vector3 flightDirection = WorldVisualBridge.SimDirectionToFlightWorld(
                sunDirSim,
                shipRadialUnity
            );

            if (flightDirection.sqrMagnitude > 1e-8f)
            {
                return flightDirection.normalized;
            }
        }

        if (simulation.TryGetDirectionFromPlanetToSun(out global::SimVector3 planetSunDirSim))
        {
            Vector3 shipRadialUnity = WorldVisualBridge.ResolveFlightPlanetUpRadialUnity(
                simulation
            );
            Vector3 flightDirection = WorldVisualBridge.SimDirectionToFlightWorld(
                planetSunDirSim,
                shipRadialUnity
            );

            if (flightDirection.sqrMagnitude > 1e-8f)
            {
                return flightDirection.normalized;
            }
        }

        return FallbackFlightSunDirection.normalized;
    }

    void UpdateMapSun()
    {
        // Map already draws the celestial Sun via BodyView. This object is light only.
        sunCoreObject.SetActive(false);

        if (pointLight != null)
        {
            pointLight.enabled = false;
        }

        Vector3 targetPosition = sunWorldPosition;

        if (CanUseSimulationSun)
        {
            global::SimVector3 renderOrigin = hasMapRenderOrigin ?
                mapRenderOrigin :
                simulation.ship != null ?
                    simulation.ship.position :
                    global::SimVector3.Zero();
            global::SimVector3 sunLayout = WorldVisualBridge.GetMapCelestialLayoutSimPosition(
                "Sun",
                simulation.sun.position,
                renderOrigin,
                simulation
            );
            targetPosition = WorldVisualBridge.SimToUnityMeshFramePosition(
                sunLayout,
                renderOrigin,
                worldScale
            );
        }
        else
        {
            targetPosition = sunWorldPosition * Mathf.Max(worldScale, 0.001f);
        }

        sunObject.transform.position = targetPosition;

        Vector3 mapLightDirection = (Vector3.zero - targetPosition).normalized;

        if (mapLightDirection.sqrMagnitude <= 0.000001f)
        {
            mapLightDirection = Vector3.left;
        }

        directionalLight.enabled = true;
        directionalLight.intensity = directionalLightIntensity;
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.transform.rotation =
            Quaternion.LookRotation(mapLightDirection, Vector3.up);
        RenderSettings.sun = directionalLight;
    }

    void RemoveOldGlowObject()
    {
        Transform oldGlowTransform = sunObject.transform.Find("Sun Glow");

        if (oldGlowTransform == null)
        {
            return;
        }

        Destroy(oldGlowTransform.gameObject);
    }
}
