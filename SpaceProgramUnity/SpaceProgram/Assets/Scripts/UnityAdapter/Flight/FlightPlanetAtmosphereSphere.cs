using UnityEngine;

/// <summary>
/// KSP-style atmosphere shell: PlanetRadius + 70 km, parented to the planet
/// (follows Mesh Frame dual-frame rotation). No LOD, no GEBCO.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlightPlanetAtmosphereSphere : MonoBehaviour
{
    public const float AtmosphereHeightMeters = FlightSkyVisibilitySettings.AtmosphereShellHeightMeters;
    const int IcosphereSubdivisions = 6;
    const float FlightLimbStrength = 0.000028f;
    const float MapLimbStrength = 0.014f;
    const float FlightExteriorAlphaBehindScale = 0.18f;
    const float MapExteriorAlphaBehindScale = 0.82f;
    const float FlightExteriorAlphaBoost = 1f;
    const float MapExteriorAlphaBoost = 2.4f;
    static readonly int LimbStrengthShaderId = Shader.PropertyToID("_LimbStrength");
    static readonly int ExteriorAlphaBehindScaleShaderId =
        Shader.PropertyToID("_ExteriorAlphaBehindScale");
    static readonly int ExteriorAlphaBoostShaderId = Shader.PropertyToID("_ExteriorAlphaBoost");
    static readonly int OpacityShaderId = Shader.PropertyToID("_Opacity");

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh runtimeMesh;
    Material atmosphereMaterial;
    float planetRadiusMeters;
    bool ready;
    bool mapModeActive;
    int lastCull = -1;

    public void Build(float planetRadius)
    {
        planetRadiusMeters = planetRadius;
        Shader shader = SpaceProgramShaders.Find("SpaceProgram/EarthAtmosphere");

        if (shader == null)
        {
            ready = false;
            gameObject.SetActive(false);
            Debug.LogWarning(
                "FlightPlanetAtmosphereSphere: shader SpaceProgram/EarthAtmosphere not found."
            );
            return;
        }

        EnsureComponents();

        if (runtimeMesh != null)
        {
            Destroy(runtimeMesh);
            runtimeMesh = null;
        }

        float outerRadius = planetRadius + AtmosphereHeightMeters;
        runtimeMesh = FlightPlanetMapSphere.BuildIcosphereMeshPublic(
            IcosphereSubdivisions,
            outerRadius
        );
        runtimeMesh.name = "PlanetAtmosphereSphere";
        meshFilter.sharedMesh = runtimeMesh;
        atmosphereMaterial = EnsureMaterial(shader, planetRadius);
        meshRenderer.sharedMaterial = atmosphereMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        ready = true;
        gameObject.SetActive(true);
    }

    public void ApplyRenderLayer(bool mapMode)
    {
        if (!ready || DevSettings.AtmosphereVisualsDisabled)
        {
            gameObject.SetActive(false);
            return;
        }

        if (mapMode)
        {
            FlightRenderLayers.ApplyScaledSpaceLayer(gameObject);
        }
        else
        {
            FlightRenderLayers.ApplyLocalPhysicsLayer(gameObject);
        }

        ApplyMapModePresentation(mapMode);
        ApplyOpacity(DevSettings.AtmosphereOpacity);
        gameObject.SetActive(DevSettings.AtmosphereOpacity > 0.001f);
    }

    public void UpdateCameraFacing(Vector3 cameraWorldPosition)
    {
        if (!ready || atmosphereMaterial == null)
        {
            return;
        }

        bool inside = false;

        if (!mapModeActive)
        {
            float scale = Mathf.Max(transform.lossyScale.x, 1e-6f);
            float cameraRadius = Vector3.Distance(cameraWorldPosition, transform.position);
            inside = cameraRadius < (planetRadiusMeters + AtmosphereHeightMeters) * scale - scale;
        }

        int cull = inside
            ? (int)UnityEngine.Rendering.CullMode.Front
            : (int)UnityEngine.Rendering.CullMode.Back;

        if (cull == lastCull)
        {
            return;
        }

        lastCull = cull;
        atmosphereMaterial.SetFloat("_Cull", cull);
    }

    public void ApplyMapModePresentation(bool mapMode)
    {
        if (atmosphereMaterial == null)
        {
            return;
        }

        mapModeActive = mapMode;
        atmosphereMaterial.SetFloat("_MapMode", mapMode ? 1f : 0f);
        lastCull = -1;

        if (mapMode)
        {
            atmosphereMaterial.SetFloat(LimbStrengthShaderId, MapLimbStrength);
            atmosphereMaterial.SetFloat(
                ExteriorAlphaBehindScaleShaderId,
                MapExteriorAlphaBehindScale
            );
            atmosphereMaterial.SetFloat(ExteriorAlphaBoostShaderId, MapExteriorAlphaBoost);
        }
        else
        {
            atmosphereMaterial.SetFloat(LimbStrengthShaderId, FlightLimbStrength);
            atmosphereMaterial.SetFloat(
                ExteriorAlphaBehindScaleShaderId,
                FlightExteriorAlphaBehindScale
            );
            atmosphereMaterial.SetFloat(ExteriorAlphaBoostShaderId, FlightExteriorAlphaBoost);
        }

        ApplyOpacity(DevSettings.AtmosphereOpacity);
    }

    public void ApplyOpacity(float opacity)
    {
        if (atmosphereMaterial == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(opacity);
        atmosphereMaterial.SetFloat(OpacityShaderId, clamped);

        if (ready && !DevSettings.AtmosphereVisualsDisabled)
        {
            gameObject.SetActive(clamped > 0.001f);
        }
    }

    Material EnsureMaterial(Shader shader, float planetRadius)
    {
        if (atmosphereMaterial == null)
        {
            atmosphereMaterial = new Material(shader)
            {
                name = "PlanetAtmosphere",
            };
        }

        atmosphereMaterial.renderQueue = 3000;
        atmosphereMaterial.SetColor("_DayColor", new Color(0.40f, 0.66f, 0.98f, 1f));
        atmosphereMaterial.SetColor("_HorizonColor", new Color(0.80f, 0.88f, 0.96f, 1f));
        atmosphereMaterial.SetColor("_SunsetColor", new Color(0.95f, 0.42f, 0.18f, 1f));
        atmosphereMaterial.SetFloat("_PlanetRadius", planetRadius);
        atmosphereMaterial.SetFloat("_AtmosphereHeight", AtmosphereHeightMeters);
        atmosphereMaterial.SetFloat("_ScaleHeight", 900f);
        atmosphereMaterial.SetFloat("_LimbHeight", 8000f);
        atmosphereMaterial.SetFloat("_SkyOpacity", 0.985f);
        atmosphereMaterial.SetFloat("_SkyExtinction", 6.4f);
        atmosphereMaterial.SetFloat("_LimbStrength", FlightLimbStrength);
        atmosphereMaterial.SetFloat(
            "_ExteriorAlphaBehindScale",
            FlightExteriorAlphaBehindScale
        );
        atmosphereMaterial.SetFloat("_ExteriorAlphaBoost", FlightExteriorAlphaBoost);
        atmosphereMaterial.SetFloat("_TerminatorSoftness", 0.22f);
        atmosphereMaterial.SetFloat("_MapMode", 0f);
        atmosphereMaterial.SetFloat(OpacityShaderId, DevSettings.AtmosphereOpacity);
        atmosphereMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        lastCull = -1;
        ApplySkyVisibilityConstants(atmosphereMaterial);
        return atmosphereMaterial;
    }

    public void ApplyCameraAltitude(double altitudeMeters)
    {
        if (atmosphereMaterial == null)
        {
            return;
        }

        atmosphereMaterial.SetFloat("_CameraAltitude", Mathf.Max(0f, (float)altitudeMeters));
    }

    static void ApplySkyVisibilityConstants(Material material)
    {
        material.SetFloat(
            "_BlackZenithAltStart",
            FlightSkyVisibilitySettings.BlackZenithStartAltitudeMeters
        );
        material.SetFloat(
            "_BlackZenithAltEnd",
            FlightSkyVisibilitySettings.BlackZenithEndAltitudeMeters
        );
        material.SetFloat(
            "_StarAppearAltStart",
            FlightSkyVisibilitySettings.StarsAppearStartAltitudeMeters
        );
        material.SetFloat(
            "_StarAppearAltEnd",
            FlightSkyVisibilitySettings.StarsAppearEndAltitudeMeters
        );
        material.SetFloat(
            "_ShellFadeStartAlt",
            FlightSkyVisibilitySettings.AtmosphereShellFadeStartAltitudeMeters
        );
        material.SetFloat(
            "_ShellFadeEndAlt",
            FlightSkyVisibilitySettings.AtmosphereShellFadeEndAltitudeMeters
        );
        material.SetFloat("_CameraAltitude", 0f);
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
            runtimeMesh = null;
        }

        if (atmosphereMaterial != null)
        {
            Destroy(atmosphereMaterial);
            atmosphereMaterial = null;
        }
    }
}
