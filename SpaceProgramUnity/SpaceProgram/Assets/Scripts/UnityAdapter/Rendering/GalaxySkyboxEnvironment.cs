using UnityEngine;

/// <summary>
/// Applies the 6-sided galaxy skybox to flight cameras.
/// </summary>
public static class GalaxySkyboxEnvironment
{
    public const string SkyboxMaterialResourcePath = "Environment/mat_KspSkybox";
    public const string SkyboxMaterialAssetPath =
        "Assets/Content/Environment/Skybox/mat_KspSkybox.mat";

    static Material cachedMaterial;
    static Material runtimeSkyboxMaterial;
    static Quaternion lastSkyRotation = Quaternion.identity;

    public static bool TryApply(Camera camera)
    {
        Material sourceMaterial = ResolveMaterial();

        if (sourceMaterial == null)
        {
            return false;
        }

        EnsureRuntimeSkyboxMaterial(sourceMaterial);
        RenderSettings.skybox = runtimeSkyboxMaterial;

        // Ambient lighting (RenderSettings.ambientMode is Skybox by default, and our
        // ground shader samples it via SampleSH) comes from a cached "ambient probe",
        // not the skybox material directly. Unity only refreshes that probe on a bake
        // or an explicit DynamicGI call — assigning RenderSettings.skybox alone does
        // NOT trigger a refresh. Without this call the probe stays whatever it was for
        // the scene's default (bright) skybox until something else happens to poke it,
        // which is exactly why the first load looks washed-out white and a menu restart
        // (which re-triggers this path again after the probe already got nudged once)
        // looks correctly dark.
        DynamicGI.UpdateEnvironment();

        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        return true;
    }

    public static void ApplyAltitudeVisibility(double altitudeMeters)
    {
        if (runtimeSkyboxMaterial == null)
        {
            Material sourceMaterial = ResolveMaterial();

            if (sourceMaterial == null)
            {
                return;
            }

            EnsureRuntimeSkyboxMaterial(sourceMaterial);
        }

        if (runtimeSkyboxMaterial == null)
        {
            return;
        }

        float altitude = Mathf.Max(0f, (float)altitudeMeters);
        float starFade = Smooth01(Mathf.InverseLerp(
            FlightSkyVisibilitySettings.SkyboxStarsExposureStartAltitudeMeters,
            FlightSkyVisibilitySettings.SkyboxStarsExposureEndAltitudeMeters,
            altitude
        ));
        float galaxyFade = Smooth01(Mathf.InverseLerp(
            FlightSkyVisibilitySettings.SkyboxGalaxyExposureStartAltitudeMeters,
            FlightSkyVisibilitySettings.SkyboxGalaxyExposureEndAltitudeMeters,
            altitude
        ));

        float starsExposure = starFade * FlightSkyVisibilitySettings.StarsOnlySkyboxExposure;
        float galaxyExposure = galaxyFade * (
            FlightSkyVisibilitySettings.FullGalaxySkyboxExposure -
            FlightSkyVisibilitySettings.StarsOnlySkyboxExposure
        );
        runtimeSkyboxMaterial.SetFloat("_Exposure", starsExposure + galaxyExposure);

        float tint = Mathf.Lerp(0.12f, 1f, Mathf.Max(starFade, galaxyFade * 0.35f));
        runtimeSkyboxMaterial.SetColor("_Tint", new Color(tint, tint, tint, 1f));
    }

    public static void ApplyMapModeVisibility()
    {
        if (runtimeSkyboxMaterial == null)
        {
            Material sourceMaterial = ResolveMaterial();

            if (sourceMaterial == null)
            {
                return;
            }

            EnsureRuntimeSkyboxMaterial(sourceMaterial);
        }

        if (runtimeSkyboxMaterial == null)
        {
            return;
        }

        runtimeSkyboxMaterial.SetFloat(
            "_Exposure",
            FlightSkyVisibilitySettings.MapModeSkyboxExposure
        );
        runtimeSkyboxMaterial.SetColor("_Tint", Color.white);
        ApplySkyboxRotation(Quaternion.identity);
    }

    /// <summary>
    /// Rotates the galaxy cubemap in Unity. Identity = cubemap axes = world.
    /// Flight passes inertial-to-flight so stars stay in space, not glued to the pad compass.
    /// </summary>
    public static void ApplySkyboxRotation(Quaternion worldFromCubemap)
    {
        if (runtimeSkyboxMaterial == null || !runtimeSkyboxMaterial.HasProperty("_SkyRotation"))
        {
            return;
        }

        Quaternion q = worldFromCubemap;
        if (q.w < 0f)
        {
            q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
        }

        if (Quaternion.Dot(lastSkyRotation, q) < 0f)
        {
            q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
        }

        lastSkyRotation = q;
        runtimeSkyboxMaterial.SetVector(
            "_SkyRotation",
            new Vector4(q.x, q.y, q.z, q.w)
        );
    }

    static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static void EnsureRuntimeSkyboxMaterial(Material sourceMaterial)
    {
        Shader galaxyShader = Shader.Find("SpaceProgram/GalaxySkybox");
        Shader targetShader = galaxyShader != null ? galaxyShader : sourceMaterial.shader;

        if (
            runtimeSkyboxMaterial != null &&
            runtimeSkyboxMaterial.shader == targetShader
        )
        {
            return;
        }

        if (runtimeSkyboxMaterial != null)
        {
            Object.Destroy(runtimeSkyboxMaterial);
        }

        runtimeSkyboxMaterial = new Material(sourceMaterial)
        {
            name = sourceMaterial.name + " (Runtime)",
        };

        if (galaxyShader != null)
        {
            runtimeSkyboxMaterial.shader = galaxyShader;
        }

        runtimeSkyboxMaterial.SetFloat("_Exposure", 0f);
        runtimeSkyboxMaterial.SetColor("_Tint", new Color(0.12f, 0.12f, 0.12f, 1f));
        lastSkyRotation = Quaternion.identity;
        ApplySkyboxRotation(Quaternion.identity);
    }

    public static void ClearCache()
    {
        cachedMaterial = null;

        if (runtimeSkyboxMaterial != null)
        {
            Object.Destroy(runtimeSkyboxMaterial);
            runtimeSkyboxMaterial = null;
            lastSkyRotation = Quaternion.identity;
        }
    }

    static Material ResolveMaterial()
    {
        if (cachedMaterial != null)
        {
            return cachedMaterial;
        }

        cachedMaterial = Resources.Load<Material>(SkyboxMaterialResourcePath);

#if UNITY_EDITOR
        if (cachedMaterial == null)
        {
            cachedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                SkyboxMaterialAssetPath
            );
        }
#endif

        return cachedMaterial;
    }
}
