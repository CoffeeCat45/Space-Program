using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cheap IBL for URP Lit parts: two 32px cubemaps (space / daytime sky), blended by altitude.
/// No scene capture. Off by default — does not change hangar or current look until enabled.
/// </summary>
public static class PartEnvironmentReflections
{
    const int CubeSize = 32;
    const float BlendEpsilon = 0.04f;

    static bool wantEnabled;
    static bool applied;
    static bool cubemapsReady;
    static float lastSpaceBlend = -1f;
    static Cubemap spaceCube;
    static Cubemap groundCube;
    static Cubemap blendedCube;
    static GameObject probeObject;
    static ReflectionProbe probe;
    static DefaultReflectionMode savedMode;
    static Texture savedCustomTexture;
    static bool savedStateCaptured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Restore();
        DestroyCubemap(ref spaceCube);
        DestroyCubemap(ref groundCube);
        DestroyCubemap(ref blendedCube);
        wantEnabled = false;
        cubemapsReady = false;
        savedCustomTexture = null;
        savedStateCaptured = false;
    }

    public static bool WantEnabled => wantEnabled;

    public static void SetEnabled(bool enabled)
    {
        wantEnabled = enabled;

        if (!enabled)
        {
            Restore();
        }
    }

    public static void TickFlight(double altitudeMeters, bool useSpaceCubemap)
    {
        if (!wantEnabled)
        {
            Restore();
            return;
        }

        EnsureCubemaps();
        float spaceBlend = ComputeSpaceBlend(altitudeMeters, useSpaceCubemap);

        if (applied && Mathf.Abs(spaceBlend - lastSpaceBlend) < BlendEpsilon)
        {
            FollowProbe();
            return;
        }

        FillBlendedCubemap(spaceBlend);
        Apply(spaceBlend);
    }

    public static void NotifyLeftFlight()
    {
        Restore();
    }

    public static float ComputeSpaceBlend(double altitudeMeters, bool useSpaceCubemap)
    {
        if (useSpaceCubemap)
        {
            return 1f;
        }

        float t = Mathf.InverseLerp(
            FlightSkyVisibilitySettings.BlackZenithStartAltitudeMeters,
            FlightSkyVisibilitySettings.AtmosphereShellFadeEndAltitudeMeters,
            (float)altitudeMeters
        );
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    public static Color EvaluateSpaceColor(Vector3 direction)
    {
        Vector3 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.up;
        float star = StarAmount(dir);
        return new Color(
            0.012f + star,
            0.014f + star,
            0.022f + star,
            1f
        );
    }

    public static Color EvaluateGroundColor(Vector3 direction)
    {
        Vector3 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.up;
        float y = dir.y;
        Color zenith = new Color(0.22f, 0.47f, 0.86f, 1f);
        Color horizon = new Color(0.74f, 0.84f, 0.94f, 1f);
        Color ground = new Color(0.30f, 0.27f, 0.20f, 1f);

        if (y >= 0f)
        {
            return Color.Lerp(horizon, zenith, y * y);
        }

        return Color.Lerp(horizon, ground, Mathf.Clamp01(-y * 1.85f));
    }

    static void EnsureCubemaps()
    {
        if (cubemapsReady && spaceCube != null && groundCube != null && blendedCube != null)
        {
            return;
        }

        spaceCube = CreateCube("PartEnvSpace");
        groundCube = CreateCube("PartEnvGround");
        blendedCube = CreateCube("PartEnvBlended");
        FillCubemap(spaceCube, EvaluateSpaceColor);
        FillCubemap(groundCube, EvaluateGroundColor);
        cubemapsReady = true;
    }

    static Cubemap CreateCube(string name)
    {
        Cubemap cube = new Cubemap(CubeSize, TextureFormat.RGBA32, true)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        return cube;
    }

    static void FillCubemap(Cubemap cube, Func<Vector3, Color> evaluate)
    {
        Color[] face = new Color[CubeSize * CubeSize];

        for (int i = 0; i < 6; i++)
        {
            CubemapFace cubemapFace = (CubemapFace)i;

            for (int y = 0; y < CubeSize; y++)
            {
                for (int x = 0; x < CubeSize; x++)
                {
                    face[y * CubeSize + x] = evaluate(FaceDirection(cubemapFace, x, y));
                }
            }

            cube.SetPixels(face, cubemapFace);
        }

        cube.Apply(updateMipmaps: true, makeNoLongerReadable: false);
    }

    static void FillBlendedCubemap(float spaceBlend)
    {
        float groundBlend = 1f - spaceBlend;

        for (int i = 0; i < 6; i++)
        {
            CubemapFace cubemapFace = (CubemapFace)i;
            Color[] spaceFace = spaceCube.GetPixels(cubemapFace);
            Color[] groundFace = groundCube.GetPixels(cubemapFace);
            var blendFace = new Color[spaceFace.Length];

            for (int p = 0; p < blendFace.Length; p++)
            {
                blendFace[p] = spaceFace[p] * spaceBlend + groundFace[p] * groundBlend;
            }

            blendedCube.SetPixels(blendFace, cubemapFace);
        }

        blendedCube.Apply(updateMipmaps: true, makeNoLongerReadable: false);
    }

    static Vector3 FaceDirection(CubemapFace face, int x, int y)
    {
        float u = (x + 0.5f) / CubeSize * 2f - 1f;
        float v = (y + 0.5f) / CubeSize * 2f - 1f;

        switch (face)
        {
            case CubemapFace.PositiveX:
                return new Vector3(1f, -v, -u).normalized;
            case CubemapFace.NegativeX:
                return new Vector3(-1f, -v, u).normalized;
            case CubemapFace.PositiveY:
                return new Vector3(u, 1f, v).normalized;
            case CubemapFace.NegativeY:
                return new Vector3(u, -1f, -v).normalized;
            case CubemapFace.PositiveZ:
                return new Vector3(u, -v, 1f).normalized;
            default:
                return new Vector3(-u, -v, -1f).normalized;
        }
    }

    static float StarAmount(Vector3 dir)
    {
        float n = Frac(
            Mathf.Sin(Vector3.Dot(dir, new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f
        );

        if (n < 0.991f)
        {
            return 0f;
        }

        float sparkle = (n - 0.991f) / 0.009f;
        return Mathf.Pow(sparkle, 8f) * 1.35f;
    }

    static float Frac(float value)
    {
        return value - Mathf.Floor(value);
    }

    static void Apply(float spaceBlend)
    {
        CaptureSavedStateOnce();
        EnsureProbe();
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = blendedCube;

        if (probe != null)
        {
            probe.customBakedTexture = blendedCube;
            probe.enabled = true;
        }

        FollowProbe();
        lastSpaceBlend = spaceBlend;
        applied = true;
    }

    static void Restore()
    {
        if (!applied && probeObject == null)
        {
            lastSpaceBlend = -1f;
            return;
        }

        if (savedStateCaptured)
        {
            RenderSettings.defaultReflectionMode = savedMode;
            RenderSettings.customReflectionTexture = savedCustomTexture;
        }

        if (probe != null)
        {
            probe.enabled = false;
            probe.customBakedTexture = null;
        }

        if (probeObject != null)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(probeObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(probeObject);
            }

            probeObject = null;
            probe = null;
        }

        applied = false;
        lastSpaceBlend = -1f;
    }

    static void DestroyCubemap(ref Cubemap cube)
    {
        if (cube == null)
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(cube);
        cube = null;
    }

    static void CaptureSavedStateOnce()
    {
        if (savedStateCaptured)
        {
            return;
        }

        savedMode = RenderSettings.defaultReflectionMode;
        savedCustomTexture = RenderSettings.customReflectionTexture;
        savedStateCaptured = true;
    }

    static void EnsureProbe()
    {
        if (probe != null && probeObject != null)
        {
            return;
        }

        probeObject = new GameObject("PartEnvironmentReflectionProbe");
        probeObject.hideFlags = HideFlags.HideAndDontSave;
        probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.importance = 8;
        probe.intensity = 1f;
        probe.boxProjection = false;
        probe.size = Vector3.one * 20000000f;
        probe.nearClipPlane = 0.3f;
        probe.farClipPlane = 10000000f;
        probe.hdr = false;
        probe.customBakedTexture = blendedCube;
    }

    static void FollowProbe()
    {
        if (probeObject == null)
        {
            return;
        }

        Camera camera = Camera.main;
        Vector3 position = camera != null ? camera.transform.position : Vector3.zero;
        probeObject.transform.position = position;
    }
}
