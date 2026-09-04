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
    }

    static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static void EnsureRuntimeSkyboxMaterial(Material sourceMaterial)
    {
        if (
            runtimeSkyboxMaterial != null &&
            runtimeSkyboxMaterial.shader == sourceMaterial.shader
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
        runtimeSkyboxMaterial.SetFloat("_Exposure", 0f);
        runtimeSkyboxMaterial.SetColor("_Tint", new Color(0.12f, 0.12f, 0.12f, 1f));
    }

    public static void ClearCache()
    {
        cachedMaterial = null;

        if (runtimeSkyboxMaterial != null)
        {
            Object.Destroy(runtimeSkyboxMaterial);
            runtimeSkyboxMaterial = null;
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
