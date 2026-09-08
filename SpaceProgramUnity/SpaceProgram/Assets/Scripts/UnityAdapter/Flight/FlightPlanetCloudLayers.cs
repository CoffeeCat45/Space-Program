using UnityEngine;

/// <summary>
/// Two cloud shells parented to the planet, same spawn path as atmosphere:
/// baked-radius icosphere, localScale 1. Flight and map share one noise field + sim time.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlightPlanetCloudLayers : MonoBehaviour
{
    const float LowAltitudeMeters = 1800f;
    const float HighAltitudeMeters = 7500f;
    const float LowNoiseScale = 38f;
    const float LowWeatherScale = 4.6f;
    const float HighNoiseScale = 54f;
    const float HighWeatherScale = 6.6f;
    const float LowCoverageBase = 0.44f;
    const float HighCoverageBase = 0.48f;
    const float LowDensityBase = 1.02f;
    const float HighDensityBase = 0.68f;
    const float FlightWindRadiansPerSecond = 0.000238f;
    const float HighWindRadiansPerSecond = 0.0024f;
    const float ExperimentalLowCoverage = 0.36f;
    const float ExperimentalHighCoverage = 0.40f;
    const float ExperimentalLowDensity = 0.98f;
    const float ExperimentalHighDensity = 0.64f;
    const float ExperimentalLowNoiseScale = 52f;
    const float ExperimentalHighNoiseScale = 70f;
    const float ExperimentalLowWeatherScale = 5.2f;
    const float ExperimentalHighWeatherScale = 6.8f;
    const float NasaEnhancedLowWindRadiansPerSecond = 0.00025f;
    const float NasaEnhancedHighWindRadiansPerSecond = 0.00065f;
    const float NasaEnhancedHighDensity = 0.26f;
    const float NasaEnhancedLowWindPhaseRadians = 2.4f;
    static readonly int CloudTimeShaderId = Shader.PropertyToID("_CloudTime");
    static readonly int CloudDetailShaderId = Shader.PropertyToID("_CloudDetail");
    static readonly int CoverageShaderId = Shader.PropertyToID("_Coverage");
    static readonly int DensityShaderId = Shader.PropertyToID("_Density");
    static readonly int NoiseScaleShaderId = Shader.PropertyToID("_NoiseScale");
    static readonly int WeatherScaleShaderId = Shader.PropertyToID("_WeatherScale");
    static readonly int ClusterShaderId = Shader.PropertyToID("_Cluster");
    static readonly int VolumeStrengthShaderId = Shader.PropertyToID("_VolumeStrength");
    static readonly int WrapAmountShaderId = Shader.PropertyToID("_WrapAmount");
    static readonly int StormIntensityShaderId = Shader.PropertyToID("_StormIntensity");
    static readonly int RimFadeEndShaderId = Shader.PropertyToID("_RimFadeEnd");
    static readonly int CullShaderId = Shader.PropertyToID("_Cull");
    static readonly int FlowAmountShaderId = Shader.PropertyToID("_FlowAmount");
    static readonly int VortexAmountShaderId = Shader.PropertyToID("_VortexAmount");
    static readonly int CellularAmountShaderId = Shader.PropertyToID("_CellularAmount");
    static readonly int LayerKindShaderId = Shader.PropertyToID("_LayerKind");
    static readonly int OrbitViewShaderId = Shader.PropertyToID("_OrbitView");
    const string ThinLayerKeyword = "_CLOUD_THIN";
    const int CloudRenderQueue = PlanetDistantVisualPolicy.CloudRenderQueue;

    Transform lowLayer;
    Transform highLayer;
    Mesh lowMesh;
    Mesh highMesh;
    Material lowMaterial;
    Material highMaterial;
    Shader simpleShader;
    Shader experimentalShader;
    Shader nasaShader;
    Shader nasaEnhancedShader;
    static readonly int CloudMapShaderId = Shader.PropertyToID("_CloudMap");
    float planetRadiusMeters;
    int activeSubdivisions = -1;
    bool ready;
    bool mapModeActive;
    int lastLowCull = -1;
    int lastHighCull = -1;

    float shellAltitudeScale = 1f;
    bool distantGlobeLook;
    bool distantShellHeightApplied;

    public void Build(float planetRadius)
    {
        Build(planetRadius, 1f);
    }

    public void Build(float planetRadius, float altitudeScale)
    {
        planetRadiusMeters = planetRadius;
        shellAltitudeScale = Mathf.Max(1e-8f, altitudeScale);
        distantGlobeLook = false;
        distantShellHeightApplied = false;
        simpleShader = SpaceProgramShaders.Find("SpaceProgram/PlanetClouds");
        experimentalShader = SpaceProgramShaders.Find("SpaceProgram/PlanetCloudsExperimental");
        nasaShader = SpaceProgramShaders.Find("SpaceProgram/PlanetCloudsNasa");
        nasaEnhancedShader = SpaceProgramShaders.Find("SpaceProgram/PlanetCloudsNasaEnhanced");
        Shader shader = ResolveActiveShader();

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            ready = false;
            gameObject.SetActive(false);
            Debug.LogError("FlightPlanetCloudLayers: no cloud shader found.");
            return;
        }

        ClearLegacyChildren();
        lowLayer = EnsureLayer("Clouds_Low");
        highLayer = EnsureLayer("Clouds_High");
        lowMaterial = RecreateMaterial(
            lowMaterial,
            shader,
            "PlanetCloudsLow",
            new Color(0.94f, 0.96f, 0.99f, 1f),
            noiseScale: LowNoiseScale,
            weatherScale: LowWeatherScale,
            coverage: LowCoverageBase,
            softness: 0.20f,
            density: LowDensityBase,
            windLon: FlightWindRadiansPerSecond,
            windLat: 0.0f
        );
        highMaterial = RecreateMaterial(
            highMaterial,
            shader,
            "PlanetCloudsHigh",
            new Color(0.96f, 0.97f, 0.99f, 1f),
            noiseScale: HighNoiseScale,
            weatherScale: HighWeatherScale,
            coverage: HighCoverageBase,
            softness: 0.22f,
            density: HighDensityBase,
            windLon: HighWindRadiansPerSecond,
            windLat: 0.7f
        );
        RebuildMeshesForDetail(PlanetLodSettings.CloudDetail);
        ApplyLayer(lowLayer, lowMesh, lowMaterial);
        ApplyLayer(highLayer, highMesh, highMaterial);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        ready = true;
        gameObject.SetActive(true);
        ApplyDetailLevel(PlanetLodSettings.CloudDetail);
        Shader active = ResolveActiveShader();
        Debug.Log(
            "FlightPlanetCloudLayers: built with shader " +
            (active != null ? active.name : "none") +
            "."
        );
    }

    public void UpdateCameraFacing(Vector3 cameraWorldPosition)
    {
        if (!ready)
        {
            return;
        }

        float scale = Mathf.Max(transform.lossyScale.x, 1e-6f);
        float cameraRadius = Vector3.Distance(cameraWorldPosition, transform.position);
        SetLayerCull(
            lowMaterial,
            ref lastLowCull,
            cameraRadius < (planetRadiusMeters + ScaledLowAltitudeMeters()) * scale - 40f * scale
        );
        SetLayerCull(
            highMaterial,
            ref lastHighCull,
            cameraRadius < (planetRadiusMeters + ScaledHighAltitudeMeters()) * scale - 40f * scale
        );
    }

    static void SetLayerCull(Material material, ref int lastCull, bool cameraInside)
    {
        if (material == null)
        {
            return;
        }

        int cull = cameraInside
            ? (int)UnityEngine.Rendering.CullMode.Front
            : (int)UnityEngine.Rendering.CullMode.Back;

        if (cull == lastCull)
        {
            return;
        }

        lastCull = cull;
        material.SetFloat(CullShaderId, cull);
    }

    public void ApplyDetailLevel(float detail01)
    {
        if (!ready)
        {
            return;
        }

        detail01 = Mathf.Clamp01(detail01);
        ApplyActiveShaderToMaterials();
        RebuildMeshesForDetail(detail01);
        if (PlanetLodSettings.UsesSimpleCloudLook)
        {
            float coverage = PlanetLodSettings.CloudCoverage;
            float frequency = PlanetLodSettings.CloudFrequency;
            float cluster = PlanetLodSettings.CloudCluster;
            ApplyLookToMaterial(lowMaterial, detail01, coverage, frequency, cluster, 1.0f, false);
            ApplyLookToMaterial(highMaterial, detail01, coverage, frequency, cluster, 0.88f, true);
        }
        else if (PlanetLodSettings.UsesNasaEnhancedCloudLook)
        {
            ApplyNasaEnhancedLookToMaterial(lowMaterial, detail01, false);
            ApplyNasaEnhancedLookToMaterial(highMaterial, detail01, true);
        }
        else if (PlanetLodSettings.UsesNasaCloudLook)
        {
            ApplyNasaLookToMaterial(lowMaterial, detail01);
            ApplyNasaLookToMaterial(highMaterial, detail01);
        }
        else
        {
            ApplyExperimentalLookToMaterial(lowMaterial, detail01, false);
            ApplyExperimentalLookToMaterial(highMaterial, detail01, true);
        }

        if (highLayer != null)
        {
            highLayer.gameObject.SetActive(!HideHighCloudLayer());
        }

        ApplyOrbitViewToMaterials();
        ForceCloudRenderQueue();
    }

    public void ApplyRenderLayer(bool mapMode)
    {
        if (!ready || DevSettings.CloudVisualsDisabled)
        {
            gameObject.SetActive(false);
            return;
        }

        mapModeActive = mapMode;
        ApplyOrbitViewToMaterials();

        if (mapMode)
        {
            FlightRenderLayers.ApplyScaledSpaceLayer(gameObject);
        }
        else
        {
            FlightRenderLayers.ApplyLocalPhysicsLayer(gameObject);
        }

        if (lowLayer != null)
        {
            lowLayer.gameObject.SetActive(true);
        }

        if (highLayer != null)
        {
            highLayer.gameObject.SetActive(!HideHighCloudLayer());
        }

        gameObject.SetActive(true);
        ForceCloudRenderQueue();
    }

    public void SyncAnimation(double simulationTimeSeconds)
    {
        if (!ready)
        {
            return;
        }

        float cloudTime = (float)simulationTimeSeconds;

        if (lowMaterial != null)
        {
            lowMaterial.SetFloat(CloudTimeShaderId, cloudTime);
        }

        if (highMaterial != null)
        {
            highMaterial.SetFloat(CloudTimeShaderId, cloudTime);
        }
    }

    /// <summary>
    /// Distant Earth keeps the player's cloud mode (including NASA PNG+).
    /// Assigning a shader resets renderQueue to the shader tag (3010), which
    /// draws under the globe (3050) and the disk covers the weather.
    /// </summary>
    public void PreferNasaMapForDistantGlobe()
    {
        if (!ready)
        {
            return;
        }

        distantGlobeLook = true;
        ApplyDetailLevel(PlanetLodSettings.CloudDetail);
        ForceCloudRenderQueue();

        if (!distantShellHeightApplied)
        {
            distantShellHeightApplied = true;
            activeSubdivisions = -1;
            RebuildMeshesForDetail(PlanetLodSettings.CloudDetail);
            ForceCloudRenderQueue();
        }

        if (highLayer != null)
        {
            highLayer.gameObject.SetActive(!HideHighCloudLayer());
        }
    }

    bool HideHighCloudLayer()
    {
        return PlanetLodSettings.UsesNasaStaticCloudLook;
    }

    void ForceCloudRenderQueue()
    {
        if (lowMaterial != null)
        {
            lowMaterial.renderQueue = CloudRenderQueue;
        }

        if (highMaterial != null)
        {
            highMaterial.renderQueue = CloudRenderQueue;
        }
    }

    public void SetPlanetSpinLongitudeRadians(float spinLongitudeRadians)
    {
        if (!ready)
        {
            return;
        }

        PlanetVisualSpinProperties.Apply(lowMaterial, spinLongitudeRadians);
        PlanetVisualSpinProperties.Apply(highMaterial, spinLongitudeRadians);
    }

    float ScaledLowAltitudeMeters()
    {
        float meters = distantGlobeLook ?
            PlanetDistantVisualPolicy.DistantCloudHeightMeters :
            LowAltitudeMeters;
        return meters * shellAltitudeScale;
    }

    float ScaledHighAltitudeMeters()
    {
        return HighAltitudeMeters * shellAltitudeScale;
    }

    void RebuildMeshesForDetail(float detail01)
    {
        if (planetRadiusMeters <= 0.0f)
        {
            return;
        }

        int subdivisions = ResolveSubdivisions(detail01);

        if (subdivisions == activeSubdivisions && lowMesh != null && highMesh != null)
        {
            return;
        }

        activeSubdivisions = subdivisions;
        RebuildMesh(ref lowMesh, planetRadiusMeters + ScaledLowAltitudeMeters(), subdivisions, "PlanetCloudsLowMesh");
        RebuildMesh(ref highMesh, planetRadiusMeters + ScaledHighAltitudeMeters(), subdivisions, "PlanetCloudsHighMesh");

        if (lowLayer != null && lowMesh != null)
        {
            MeshFilter meshFilter = lowLayer.GetComponent<MeshFilter>();

            if (meshFilter != null)
            {
                meshFilter.sharedMesh = lowMesh;
            }
        }

        if (highLayer != null && highMesh != null)
        {
            MeshFilter meshFilter = highLayer.GetComponent<MeshFilter>();

            if (meshFilter != null)
            {
                meshFilter.sharedMesh = highMesh;
            }
        }
    }

    static int ResolveSubdivisions(float detail01)
    {
        if (detail01 < 0.34f && PlanetLodSettings.UsesSimpleCloudLook)
        {
            return 3;
        }

        return 4;
    }

    static void ApplyLookToMaterial(
        Material material,
        float detail01,
        float coverage,
        float frequency,
        float cluster,
        float layerScale,
        bool highLayer
    )
    {
        if (material == null)
        {
            return;
        }

        float coverageScale = coverage / FlightPlanetTerrainTuning.CloudCoverageDefault;
        float layerCoverage = (highLayer ? HighCoverageBase : LowCoverageBase) * coverageScale;
        float densityScale = Mathf.Clamp(coverageScale, 0.55f, 1.35f);
        float layerDensity = (highLayer ? HighDensityBase : LowDensityBase) * densityScale;
        float frequencyScale = Mathf.Clamp(
            frequency,
            FlightPlanetTerrainTuning.CloudFrequencyMin,
            FlightPlanetTerrainTuning.CloudFrequencyMax
        );

        material.SetFloat(CloudDetailShaderId, detail01);
        material.SetFloat(VolumeStrengthShaderId, Mathf.Lerp(0.1f, 1.35f, detail01) * layerScale);
        material.SetFloat(WrapAmountShaderId, Mathf.Lerp(0.1f, 0.32f, detail01));
        material.SetFloat(StormIntensityShaderId, Mathf.Lerp(0.04f, 0.14f, detail01) * layerScale);
        material.SetFloat(RimFadeEndShaderId, Mathf.Lerp(0.22f, 0.14f, detail01));
        material.SetFloat(CoverageShaderId, Mathf.Clamp(layerCoverage, 0.05f, 0.92f));
        material.SetFloat(DensityShaderId, Mathf.Clamp(layerDensity, 0.20f, 1.80f));
        material.SetFloat(
            NoiseScaleShaderId,
            (highLayer ? HighNoiseScale : LowNoiseScale) * frequencyScale
        );
        material.SetFloat(
            WeatherScaleShaderId,
            (highLayer ? HighWeatherScale : LowWeatherScale) * frequencyScale
        );
        material.SetFloat(
            ClusterShaderId,
            Mathf.Clamp(
                cluster,
                FlightPlanetTerrainTuning.CloudClusterMin,
                FlightPlanetTerrainTuning.CloudClusterMax
            )
        );
        material.SetFloat("_WindLon", highLayer ? HighWindRadiansPerSecond : FlightWindRadiansPerSecond);
        material.SetFloat("_WindLat", highLayer ? 0.7f : 0.0f);
    }

    void ApplyExperimentalLookToMaterial(Material material, float detail01, bool highLayer)
    {
        if (material == null)
        {
            return;
        }

        float layerScale = highLayer ? 0.88f : 1.0f;
        material.SetFloat(CloudDetailShaderId, detail01);
        material.SetFloat(VolumeStrengthShaderId, Mathf.Lerp(0.35f, 1.55f, detail01) * layerScale);
        material.SetFloat(WrapAmountShaderId, Mathf.Lerp(0.12f, 0.30f, detail01));
        material.SetFloat(StormIntensityShaderId, 0.0f);
        material.SetFloat(RimFadeEndShaderId, Mathf.Lerp(0.20f, 0.13f, detail01));
        float massScale = Mathf.Clamp(
            PlanetLodSettings.ExpCloudMassScale,
            FlightPlanetTerrainTuning.ExpCloudMassScaleMin,
            FlightPlanetTerrainTuning.ExpCloudMassScaleMax
        );
        material.SetFloat(
            CoverageShaderId,
            PlanetLodSettings.ExpCloudCoverage
        );
        material.SetFloat(
            DensityShaderId,
            highLayer ? ExperimentalHighDensity : ExperimentalLowDensity
        );
        material.SetFloat(
            NoiseScaleShaderId,
            highLayer ? ExperimentalHighNoiseScale : ExperimentalLowNoiseScale
        );
        material.SetFloat(
            WeatherScaleShaderId,
            (highLayer ? ExperimentalHighWeatherScale : ExperimentalLowWeatherScale) * massScale
        );
        material.SetFloat(ClusterShaderId, 0f);
        material.SetFloat(FlowAmountShaderId, PlanetLodSettings.ExpCloudFlow);
        material.SetFloat(VortexAmountShaderId, PlanetLodSettings.ExpCloudVortex);
        material.SetFloat(CellularAmountShaderId, PlanetLodSettings.ExpCloudGrain);
        material.SetFloat(LayerKindShaderId, highLayer ? 1f : 0f);
        material.SetFloat(
            "_Softness",
            highLayer
                ? Mathf.Clamp(PlanetLodSettings.ExpCloudSoftness + 0.03f, 0.03f, 0.45f)
                : PlanetLodSettings.ExpCloudSoftness
        );
        material.SetFloat("_WindLon", highLayer ? HighWindRadiansPerSecond : FlightWindRadiansPerSecond);
        material.SetFloat("_WindLat", highLayer ? 0.7f : 0.0f);
    }

    void ApplyNasaLookToMaterial(Material material, float detail01)
    {
        if (material == null)
        {
            return;
        }

        NasaCloudMapResources.EnsureLoaded();
        Texture2D map = NasaCloudMapResources.Map;
        if (map != null)
        {
            material.SetTexture(CloudMapShaderId, map);
        }

        material.SetFloat(CloudDetailShaderId, detail01);
        material.SetFloat(CoverageShaderId, 1f);
        material.SetFloat(DensityShaderId, 1.0f);
        material.SetFloat(VolumeStrengthShaderId, Mathf.Lerp(0.6f, 1.35f, detail01));
        material.SetFloat(WrapAmountShaderId, Mathf.Lerp(0.12f, 0.30f, detail01));
        material.SetFloat("_Softness", 0.08f);
        material.SetFloat("_WindLon", 0f);
        material.SetFloat("_WindLat", 0f);
        material.SetFloat(LayerKindShaderId, 0f);
    }

    void ApplyNasaEnhancedLookToMaterial(Material material, float detail01, bool highLayer)
    {
        if (material == null)
        {
            return;
        }

        NasaCloudMapResources.EnsureLoaded();
        Texture2D map = NasaCloudMapResources.Map;
        if (map != null)
        {
            material.SetTexture(CloudMapShaderId, map);
        }

        material.SetFloat(CloudDetailShaderId, detail01);
        material.SetFloat(CoverageShaderId, 1f);
        material.SetFloat(DensityShaderId, highLayer ? NasaEnhancedHighDensity : 1.0f);
        material.SetFloat(VolumeStrengthShaderId, Mathf.Lerp(0.6f, 1.35f, detail01));
        material.SetFloat(WrapAmountShaderId, Mathf.Lerp(0.12f, 0.30f, detail01));
        material.SetFloat("_Softness", highLayer ? 0.12f : 0.08f);
        material.SetFloat(
            "_WindLon",
            highLayer ? NasaEnhancedHighWindRadiansPerSecond : NasaEnhancedLowWindRadiansPerSecond
        );
        material.SetFloat("_WindLat", highLayer ? 0f : NasaEnhancedLowWindPhaseRadians);
        material.SetFloat(LayerKindShaderId, highLayer ? 1f : 0f);
    }

    void ApplyActiveShaderToMaterials()
    {
        Shader shader = ResolveActiveShader();
        if (shader == null)
        {
            return;
        }

        bool swapped = false;
        if (lowMaterial != null && lowMaterial.shader != shader)
        {
            lowMaterial.shader = shader;
            swapped = true;
        }

        if (highMaterial != null && highMaterial.shader != shader)
        {
            highMaterial.shader = shader;
            swapped = true;
        }

        if (swapped)
        {
            lastLowCull = -1;
            lastHighCull = -1;
        }

        ForceCloudRenderQueue();
    }

    void ApplyOrbitViewToMaterials()
    {
        float orbitView = mapModeActive ? 1f : 0f;
        if (lowMaterial != null)
        {
            lowMaterial.SetFloat(OrbitViewShaderId, orbitView);
        }

        if (highMaterial != null)
        {
            highMaterial.SetFloat(OrbitViewShaderId, orbitView);
        }
    }

    Shader ResolveActiveShader()
    {
        if (PlanetLodSettings.UsesNasaCloudLook)
        {
            NasaCloudMapResources.EnsureLoaded();
            if (NasaCloudMapResources.Map != null)
            {
                if (PlanetLodSettings.UsesNasaEnhancedCloudLook && nasaEnhancedShader != null)
                {
                    return nasaEnhancedShader;
                }

                if (nasaShader != null)
                {
                    return nasaShader;
                }
            }

            Debug.LogWarning(
                "FlightPlanetCloudLayers: NASA cloud PNG missing — falling back to simple shells."
            );
        }

        if (PlanetLodSettings.UsesExperimentalCloudLook && experimentalShader != null)
        {
            return experimentalShader;
        }

        return simpleShader;
    }

    void ClearLegacyChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (child.name == "Clouds_Low" || child.name == "Clouds_High")
            {
                continue;
            }

            DestroyImmediateSafe(child.gameObject);
        }

        if (lowLayer != null)
        {
            DestroyLegacyProxies(lowLayer);
        }

        if (highLayer != null)
        {
            DestroyLegacyProxies(highLayer);
        }
    }

    static void DestroyLegacyProxies(Transform layer)
    {
        DestroyNamed(layer, "Inner");
        DestroyNamed(layer, "Outer");
    }

    static void DestroyNamed(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);

        if (child != null)
        {
            DestroyImmediateSafe(child.gameObject);
        }
    }

    static void DestroyImmediateSafe(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    Transform EnsureLayer(string layerName)
    {
        Transform existing = transform.Find(layerName);

        if (existing == null)
        {
            GameObject child = new GameObject(layerName);
            child.transform.SetParent(transform, false);
            existing = child.transform;
        }

        existing.localPosition = Vector3.zero;
        existing.localRotation = Quaternion.identity;
        existing.localScale = Vector3.one;
        existing.gameObject.SetActive(true);
        return existing;
    }

    static void RebuildMesh(ref Mesh mesh, float radius, int subdivisions, string meshName)
    {
        if (mesh != null)
        {
            Destroy(mesh);
            mesh = null;
        }

        mesh = FlightPlanetMapSphere.BuildIcosphereMeshPublic(subdivisions, radius);
        mesh.name = meshName;
    }

    static Material RecreateMaterial(
        Material existing,
        Shader shader,
        string materialName,
        Color color,
        float noiseScale,
        float weatherScale,
        float coverage,
        float softness,
        float density,
        float windLon,
        float windLat
    )
    {
        if (existing != null)
        {
            Destroy(existing);
        }

        Material material = new Material(shader)
        {
            name = materialName,
        };
        material.renderQueue = CloudRenderQueue;
        material.SetColor("_CloudColor", color);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_NoiseScale", noiseScale);
        material.SetFloat("_WeatherScale", weatherScale);
        material.SetFloat("_Coverage", coverage);
        material.SetFloat("_Cluster", PlanetLodSettings.CloudCluster);
        material.SetFloat("_Softness", softness);
        material.SetFloat("_Density", density);
        material.SetFloat("_WindLon", windLon);
        material.SetFloat("_WindLat", windLat);
        material.SetFloat("_CloudTime", 0f);
        material.SetFloat("_TerminatorSoftness", 0.22f);
        material.SetFloat("_NightBrightness", 0.45f);
        material.SetFloat("_TwilightStrength", 1.0f);
        material.SetColor("_TwilightColor", new Color(0.95f, 0.42f, 0.18f, 1f));
        material.SetColor("_NightCloudColor", new Color(0.055f, 0.058f, 0.068f, 1f));
        material.SetFloat(CullShaderId, (float)UnityEngine.Rendering.CullMode.Back);
        material.DisableKeyword(ThinLayerKeyword);

        return material;
    }

    static void ApplyLayer(Transform layer, Mesh mesh, Material material)
    {
        MeshFilter meshFilter = layer.GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            meshFilter = layer.gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = layer.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            meshRenderer = layer.gameObject.AddComponent<MeshRenderer>();
        }

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        meshRenderer.enabled = true;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        layer.localScale = Vector3.one;
        layer.gameObject.SetActive(true);
        FlightRenderLayers.ApplyLocalPhysicsLayer(layer.gameObject);
    }

    void OnDestroy()
    {
        if (lowMesh != null)
        {
            Destroy(lowMesh);
            lowMesh = null;
        }

        if (highMesh != null)
        {
            Destroy(highMesh);
            highMesh = null;
        }

        if (lowMaterial != null)
        {
            Destroy(lowMaterial);
            lowMaterial = null;
        }

        if (highMaterial != null)
        {
            Destroy(highMaterial);
            highMaterial = null;
        }
    }
}
