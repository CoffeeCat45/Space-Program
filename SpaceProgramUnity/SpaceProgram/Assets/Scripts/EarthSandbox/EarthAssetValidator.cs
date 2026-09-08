using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Earth sandbox — roadmap stages 2–4: validation, MATCH report, visual globe + Florida marker.
/// </summary>
public sealed class EarthAssetValidator : MonoBehaviour
{
    private const int RawWidth = 86400;
    private const int RawHeight = 43200;

    private const float FloridaLatitudeDegrees = 28.57f;
    private const float FloridaLongitudeDegrees = -80.65f;

    private const float MaltaLatitudeDegrees = 35.9f;
    private const float MaltaLongitudeDegrees = 14.4f;

    [Header("Assets")]
    public Texture2D geographicTexture;

    public string elevationRawPath = "";

    [Header("Stage 4 — visual globe")]
    public bool buildVisualGlobe = true;

    public float globeRadius = 100f;

    [Range(8, 256)]
    public int latitudeSegments = 64;

    [Range(8, 512)]
    public int longitudeSegments = 128;

    public float markerCubeScale = 3f;

    [Tooltip("Scale of the Malta test marker cube.")]
    public float maltaMarkerCubeScale = 1.5f;

    [Tooltip("0 = auto (×10 on mini-globe r≈100). 1 = honest 1:10 proportion.")]
    public float reliefExaggeration = 0f;

    [Tooltip("Paint globe from GetPixel vertex colors (bypass GPU texture). Debug only.")]
    public bool debugVertexPaintGlobe = false;

    [Tooltip("Build globe even when stage 3 is not full MATCH (debug only).")]
    public bool buildGlobeEvenIfStage3Fails = false;

    private Transform _globeTransform;
    private Transform _markerTransform;
    private EarthSandboxGeographicRaster _geographicRaster;
    private Mesh _globeMesh;

    private void Start()
    {
        if (!EarthSandboxScene.IsActive())
        {
            return;
        }

        ResolveAssetPaths();
        LoadGeographicRasterIfNeeded();

        Stage3Summary stage3 = RunStage3Analysis();

        if (!buildVisualGlobe || _geographicRaster == null)
        {
            return;
        }

        if (!stage3.CanProceedToStage4 && !buildGlobeEvenIfStage3Fails)
        {
            Debug.LogWarning(
                "[EarthSandbox] Stage 4 skipped — stage 3 not MATCH. " +
                "Fix assets or enable buildGlobeEvenIfStage3Fails."
            );
            return;
        }

        BuildVisualGlobe();
        EnsureQuadtreeManager();
        CreateFloridaMarker();
        CreateMaltaMarker();
        SetupProjectCamera();
        Debug.Log(
            "Stage 4 active — sandbox stage-4 globe (canonical u/v, explicit outward normals). Orbit: RMB + scroll."
        );
        LogGlobePaintVerification();
    }

    private void Update()
    {
        if (_markerTransform == null || _globeTransform == null)
        {
            return;
        }

        UpdateFloridaMarkerPosition();
    }

    private Stage3Summary RunStage3Analysis()
    {
        var sites = BuildProbeSites();
        var elevationResults = new List<SiteProbeResult>(sites.Count);
        var colorResults = new List<SiteProbeResult>(sites.Count);

        for (int i = 0; i < sites.Count; i++)
        {
            ProbeSite site = sites[i];
            elevationResults.Add(ProbeElevation(site));
            colorResults.Add(ProbeColor(site));
            LogSiteDetail(site, elevationResults[i], colorResults[i]);
        }

        string report = BuildStage3Report(sites, elevationResults, colorResults, out Stage3Summary summary);
        Debug.Log(report);
        return summary;
    }

    private static List<ProbeSite> BuildProbeSites()
    {
        return new List<ProbeSite>
        {
            new ProbeSite(
                "Cape Canaveral",
                28.57,
                -80.65,
                expectLand: true,
                minElevationMeters: -20.0,
                maxElevationMeters: 200.0,
                minHeightForMountains: 0.0
            ),
            new ProbeSite(
                "Tibet",
                29.97,
                91.11,
                expectLand: true,
                minElevationMeters: 4000.0,
                maxElevationMeters: 9000.0,
                minHeightForMountains: 4000.0
            ),
            new ProbeSite(
                "Indian Ocean",
                -10.0,
                60.0,
                expectLand: false,
                minElevationMeters: -11000.0,
                maxElevationMeters: 0.0,
                minHeightForMountains: double.NegativeInfinity
            ),
        };
    }

    private void LogSiteDetail(ProbeSite site, SiteProbeResult elevation, SiteProbeResult color)
    {
        Debug.Log(
            $"[EarthSandbox][{site.Label}] lat={site.LatitudeDegrees:F2} lon={site.LongitudeDegrees:F2} " +
            $"U={elevation.GeographicU:F4} V={elevation.GeographicV:F4} | " +
            $"ELEV {elevation.Verdict}: canon={elevation.CanonicalValue:F0}m mirrorU={elevation.MirroredValue:F0}m | " +
            $"COLOR {color.Verdict} (GetPixel): canon={color.CanonicalClass} mirrorU={color.MirroredClass}"
        );
    }

    private string BuildStage3Report(
        List<ProbeSite> sites,
        List<SiteProbeResult> elevationResults,
        List<SiteProbeResult> colorResults,
        out Stage3Summary summary
    )
    {
        int elevCanonMatch = 0;
        int elevMirrorMatch = 0;
        int colorCanonMatch = 0;
        int colorMirrorMatch = 0;

        for (int i = 0; i < sites.Count; i++)
        {
            if (elevationResults[i].CanonicalMatchesExpectation)
            {
                elevCanonMatch++;
            }

            if (elevationResults[i].MirroredMatchesExpectation)
            {
                elevMirrorMatch++;
            }

            if (colorResults[i].CanonicalMatchesExpectation)
            {
                colorCanonMatch++;
            }

            if (colorResults[i].MirroredMatchesExpectation)
            {
                colorMirrorMatch++;
            }
        }

        string elevLayout = InferLayout(elevCanonMatch, elevMirrorMatch, sites.Count);
        string colorLayout = InferLayout(colorCanonMatch, colorMirrorMatch, sites.Count);

        var builder = new StringBuilder();
        builder.AppendLine("=== EarthSandbox Stage 3 Report (pixels + bytes only) ===");
        builder.AppendLine();

        AppendAssetSection(builder, "earth_elevation.raw", sites, elevationResults, elevLayout, elevCanonMatch, elevMirrorMatch);
        builder.AppendLine();
        AppendAssetSection(builder, "earth_geographic color (GetPixel = roadmap stage 2)", sites, colorResults, colorLayout, colorCanonMatch, colorMirrorMatch);
        builder.AppendLine();

        bool elevOk = elevLayout == "Canonical";
        bool colorOk = colorLayout == "Canonical";
        bool colorMirrorOk = colorLayout == "Mirrored";

        if (elevOk && colorOk)
        {
            builder.AppendLine("OVERALL: MATCH — assets + canon u/v OK. Bug is in main game mesh/shader/UI, not on disk.");
            builder.AppendLine("→ Proceed to roadmap stage 4 (visual sphere + Florida marker).");
        }
        else if (elevOk && colorMirrorOk)
        {
            builder.AppendLine("OVERALL: ELEVATION MATCH, COLOR MIRRORED (1-U).");
            builder.AppendLine("→ Fix color once in sandbox: textureMirrorU=true OR horizontal flip PNG — then stage 4.");
        }
        else if (!elevOk && elevLayout == "Mirrored")
        {
            builder.AppendLine("OVERALL: ELEVATION MIRRORED on disk — fix raw read (flip U) before stage 4.");
        }
        else
        {
            builder.AppendLine("OVERALL: PARTIAL / FAIL — see per-site rows. Do not start stage 4 until resolved.");
        }

        builder.AppendLine();
        builder.AppendLine(
            "Recommended for stage 4: elevationFlipU=false, colorFlipU=" +
            (colorMirrorOk && !colorOk ? "true" : "false")
        );

        summary = new Stage3Summary(
            elevOk,
            colorOk,
            colorMirrorOk && !colorOk
        );

        return builder.ToString();
    }

    private static void AppendAssetSection(
        StringBuilder builder,
        string assetLabel,
        List<ProbeSite> sites,
        List<SiteProbeResult> results,
        string layout,
        int canonMatches,
        int mirrorMatches
    )
    {
        builder.AppendLine($"[{assetLabel}] layout={layout} canonicalMatches={canonMatches}/{sites.Count} mirroredMatches={mirrorMatches}/{sites.Count}");

        for (int i = 0; i < sites.Count; i++)
        {
            ProbeSite site = sites[i];
            SiteProbeResult r = results[i];
            builder.AppendLine(
                "  " + site.Label +
                " lat=" + site.LatitudeDegrees.ToString("F2") +
                " lon=" + site.LongitudeDegrees.ToString("F2") +
                " geoU=" + r.GeographicU.ToString("F4") +
                " geoV=" + r.GeographicV.ToString("F4") +
                " | canon=" + r.CanonicalDetail +
                " expect=" + site.ExpectationLabel +
                " verdict=" + r.Verdict
            );

            if (r.MirroredDetail.Length > 0)
            {
                builder.AppendLine("    mirrorU: " + r.MirroredDetail);
            }
        }
    }

    private SiteProbeResult ProbeElevation(ProbeSite site)
    {
        double u = GeographicUFromDegrees(site.LongitudeDegrees);
        double v = GeographicVFromDegrees(site.LatitudeDegrees);

        if (string.IsNullOrWhiteSpace(elevationRawPath) || !File.Exists(elevationRawPath))
        {
            return SiteProbeResult.Missing(u, v);
        }

        int colCanon = ColFromU(u);
        int colMirror = ColFromU(1.0 - u);
        int rowNorthAtTop = RowFromLatitude(site.LatitudeDegrees);
        int rowRoadmapV = Mathf.Clamp((int)(v * RawHeight), 0, RawHeight - 1);

        short heightCanon = ReadElevationSample(rowNorthAtTop, colCanon);
        short heightMirrorU = ReadElevationSample(rowNorthAtTop, colMirror);
        short heightRoadmapRow = ReadElevationSample(rowRoadmapV, colCanon);

        bool canonOk = site.MatchesElevation(heightCanon);
        bool mirrorOk = site.MatchesElevation(heightMirrorU);

        string canonDetail = $"row={rowNorthAtTop} col={colCanon} h={heightCanon}m";
        string mirrorDetail = $"col={colMirror} h={heightMirrorU}m";
        string verdict = canonOk ? "MATCH" : mirrorOk ? "MATCH_MIRROR_U" : "FAIL";

        if (!canonOk && heightRoadmapRow != heightCanon)
        {
            mirrorDetail += $" (roadmap v-row={rowRoadmapV} h={heightRoadmapRow}m — wrong row formula)";
        }

        return new SiteProbeResult(
            u,
            v,
            heightCanon,
            heightMirrorU,
            canonOk,
            mirrorOk,
            verdict,
            canonDetail,
            mirrorDetail,
            string.Empty,
            string.Empty
        );
    }

    private SiteProbeResult ProbeColor(ProbeSite site)
    {
        double u = GeographicUFromDegrees(site.LongitudeDegrees);
        double v = GeographicVFromDegrees(site.LatitudeDegrees);

        if (_geographicRaster == null)
        {
            return SiteProbeResult.Missing(u, v);
        }

        Color canonBilinear = SampleColorBilinear((float)u, (float)v);
        Color mirrorBilinear = SampleColorBilinear((float)(1.0 - u), (float)v);
        Color canonPixel = SampleColorIntegerPixel(u, v);
        Color mirrorPixel = SampleColorIntegerPixel(1.0 - u, v);

        string canonBilinearClass = ClassifyLandOcean(canonBilinear);
        string mirrorBilinearClass = ClassifyLandOcean(mirrorBilinear);
        string canonPixelClass = ClassifyLandOcean(canonPixel);
        string mirrorPixelClass = ClassifyLandOcean(mirrorPixel);

        bool canonOk = site.MatchesColorClass(canonPixelClass);
        bool mirrorOk = site.MatchesColorClass(mirrorPixelClass);

        string verdict = canonOk ? "MATCH" : mirrorOk ? "MATCH_MIRROR_U" : "FAIL";

        string canonDetail =
            $"GetPixel class={canonPixelClass} xy=({ColorPixelXFromU(u)},{ColorPixelYFromV(v)}); " +
            $"bilinear class={canonBilinearClass} RGB=({canonBilinear.r:F2},{canonBilinear.g:F2},{canonBilinear.b:F2})";

        string mirrorDetail =
            $"GetPixel class={mirrorPixelClass}; bilinear class={mirrorBilinearClass} " +
            $"RGB=({mirrorBilinear.r:F2},{mirrorBilinear.g:F2},{mirrorBilinear.b:F2})";

        return new SiteProbeResult(
            u,
            v,
            0,
            0,
            canonOk,
            mirrorOk,
            verdict,
            canonDetail,
            mirrorDetail,
            canonPixelClass,
            mirrorPixelClass
        );
    }

    public void ValidatePoint(double latDegrees, double lonDegrees, string name)
    {
        ProbeSite site = new ProbeSite(
            name,
            latDegrees,
            lonDegrees,
            expectLand: latDegrees > 0 && lonDegrees < 0,
            minElevationMeters: -500.0,
            maxElevationMeters: 9000.0,
            minHeightForMountains: 0.0
        );
        SiteProbeResult elev = ProbeElevation(site);
        SiteProbeResult color = ProbeColor(site);
        LogSiteDetail(site, elev, color);
    }

    private static double GeographicUFromDegrees(double lonDegrees)
    {
        return PlanetGeographicCoords.LongitudeRadiansToGeographicU(
            lonDegrees * (Math.PI / 180.0)
        );
    }

    private static double GeographicVFromDegrees(double latDegrees)
    {
        return PlanetGeographicCoords.LatitudeRadiansToGeographicV(
            latDegrees * (Math.PI / 180.0)
        );
    }

    private static int ColFromU(double u)
    {
        return Mathf.Clamp((int)Math.Round(u * (RawWidth - 1)), 0, RawWidth - 1);
    }

    private static int ColorPixelXFromU(double u, Texture2D texture)
    {
        return Mathf.Clamp((int)(u * texture.width), 0, texture.width - 1);
    }

    private static int ColorPixelYFromV(double v, Texture2D texture)
    {
        return Mathf.Clamp((int)(v * texture.height), 0, texture.height - 1);
    }

    private int ColorPixelXFromU(double u)
    {
        return ColorPixelXFromU(u, _geographicRaster.GpuTexture);
    }

    private int ColorPixelYFromV(double v)
    {
        return ColorPixelYFromV(v, _geographicRaster.GpuTexture);
    }

    private Color SampleColorBilinear(float u, float v)
    {
        return _geographicRaster.SampleCanonicalBilinear(u, v);
    }

    private Color SampleColorIntegerPixel(double u, double v)
    {
        return _geographicRaster.GpuTexture.GetPixel(
            ColorPixelXFromU(u, _geographicRaster.GpuTexture),
            ColorPixelYFromV(v, _geographicRaster.GpuTexture)
        );
    }

    private static int RowFromLatitude(double latDegrees)
    {
        return Mathf.Clamp(
            (int)Math.Round(((90.0 - latDegrees) / 180.0) * (RawHeight - 1)),
            0,
            RawHeight - 1
        );
    }

    private static string ClassifyLandOcean(Color color)
    {
        bool looksLikeOcean = color.b > color.g + 0.04f && color.b > 0.12f;
        bool looksLikeLand = color.g > 0.12f && color.b < color.g * 0.85f;

        if (looksLikeOcean)
        {
            return "OCEAN";
        }

        if (looksLikeLand)
        {
            return "LAND";
        }

        return "MIXED";
    }

    private short ReadElevationSample(int row, int col)
    {
        long offset = ((long)row * RawWidth + col) * 2L;

        try
        {
            using FileStream stream = new FileStream(
                elevationRawPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            if (offset < 0 || offset + 1 >= stream.Length)
            {
                return short.MinValue;
            }

            stream.Seek(offset, SeekOrigin.Begin);
            byte lo = (byte)stream.ReadByte();
            byte hi = (byte)stream.ReadByte();
            return (short)(lo | (hi << 8));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EarthSandbox] elevation read failed at row={row} col={col}: {ex.Message}");
            return short.MinValue;
        }
    }

    private void ResolveAssetPaths()
    {
        if (string.IsNullOrWhiteSpace(elevationRawPath))
        {
            elevationRawPath = Path.Combine(
                Application.streamingAssetsPath,
                "Planet",
                "earth_elevation.raw"
            );
        }
    }

    private void LoadGeographicRasterIfNeeded()
    {
        if (_geographicRaster != null)
        {
            _geographicRaster.Release();
            _geographicRaster = null;
        }

        if (EarthSandboxGeographicRaster.TryLoadFromStreamingAssets(out EarthSandboxGeographicRaster loaded))
        {
            _geographicRaster = loaded;
            geographicTexture = _geographicRaster.GpuTexture;
            Debug.Log(
                "[EarthSandbox] Geographic raster: " + _geographicRaster.SourcePath +
                " (" + _geographicRaster.Width + "x" + _geographicRaster.Height + "). " +
                "pixels[] → SetPixels32 → GPU; mesh UV = CPU canon."
            );
            return;
        }

        if (geographicTexture != null)
        {
            string tempPath = Path.Combine(Application.temporaryCachePath, "EarthSandbox_geographic_fallback.png");
            try
            {
                byte[] encoded = geographicTexture.EncodeToPNG();
                if (encoded != null && encoded.Length > 0)
                {
                    File.WriteAllBytes(tempPath, encoded);
                    _geographicRaster = EarthSandboxGeographicRaster.LoadFromFile(tempPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[EarthSandbox] Inspector geographicTexture fallback failed: " + exception.Message
                );
            }

            if (_geographicRaster != null)
            {
                Debug.LogWarning(
                    "[EarthSandbox] Using Inspector geographicTexture via encode fallback — " +
                    "prefer StreamingAssets/MapMode/earth_geographic.png."
                );
                return;
            }
        }

        Debug.LogWarning("[EarthSandbox] earth_geographic not found — color section skipped.");
    }

    private void LogGlobePaintVerification()
    {
        if (_geographicRaster == null)
        {
            return;
        }

        Vector3 floridaUnitCanon = DirectionFromLatitudeLongitudeDegrees(
            FloridaLatitudeDegrees,
            FloridaLongitudeDegrees
        );
        Vector3 floridaUnitMesh = EarthSandboxGlobePaint.MeshDisplayUnitFromLatitudeLongitudeDegrees(
            FloridaLatitudeDegrees,
            FloridaLongitudeDegrees
        );
        Vector2 floridaUv = _geographicRaster.CanonicalUvFromDegrees(
            FloridaLatitudeDegrees,
            FloridaLongitudeDegrees
        );
        Color32 floridaPixel = _geographicRaster.SampleCanonicalPixel(
            FloridaLatitudeDegrees,
            FloridaLongitudeDegrees
        );

        Debug.Log(
            "[EarthSandbox] Globe paint check — canon unit=(" +
            floridaUnitCanon.x.ToString("F3") + "," + floridaUnitCanon.y.ToString("F3") + "," + floridaUnitCanon.z.ToString("F3") +
            ") mesh unit=(" +
            floridaUnitMesh.x.ToString("F3") + "," + floridaUnitMesh.y.ToString("F3") + "," + floridaUnitMesh.z.ToString("F3") +
            ") | UV=(" +
            floridaUv.x.ToString("F4") + "," + floridaUv.y.ToString("F4") +
            ") | GetPixel class=" + ClassifyLandOcean(floridaPixel)
        );
    }

    private void EnsureQuadtreeManager()
    {
        EarthSandboxQuadtreeManager quadtree = GetComponent<EarthSandboxQuadtreeManager>();

        if (quadtree == null)
        {
            quadtree = gameObject.AddComponent<EarthSandboxQuadtreeManager>();
        }

        quadtree.SyncVisualGizmoRadius(globeRadius);
        EnsureTerrainGraphicsDebugPanel();
    }

    private void EnsureTerrainGraphicsDebugPanel()
    {
        if (GetComponent<TerrainGraphicsDebugPanel>() == null)
        {
            gameObject.AddComponent<TerrainGraphicsDebugPanel>();
        }
    }

    private void BuildVisualGlobe()
    {
        _globeMesh = EarthSandboxGlobePaint.BuildCanonicalUvSphere(
            globeRadius,
            latitudeSegments,
            longitudeSegments,
            _geographicRaster,
            reliefExaggeration: reliefExaggeration
        );

        float appliedExaggeration = EarthSandboxGlobePaint.ResolveReliefExaggeration(
            globeRadius,
            reliefExaggeration
        );
        Debug.Log(
            "[EarthSandbox] Globe relief: exaggeration=" + appliedExaggeration.ToString("F0") +
            " (Tibet ~" + (4361f * PlanetSurfaceConstants.UnityVisualHeightScale *
                (globeRadius / PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters) *
                appliedExaggeration).ToString("F1") +
            " units on r=" + globeRadius.ToString("F0") + ")."
        );

        GameObject globeObject = new GameObject("EarthGlobe");
        globeObject.transform.SetParent(transform, false);
        globeObject.transform.localPosition = Vector3.zero;
        globeObject.transform.localRotation = Quaternion.identity;
        globeObject.transform.localScale = Vector3.one;
        _globeTransform = globeObject.transform;

        var filter = globeObject.AddComponent<MeshFilter>();
        filter.sharedMesh = _globeMesh;

        var renderer = globeObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = EarthSandboxGlobePaint.CreateGlobeMaterial(
            _geographicRaster,
            debugVertexPaintGlobe
        );
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Vector3 DirectionFromLatitudeLongitudeDegrees(float latDegrees, float lonDegrees)
    {
        SimVector3 direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            latDegrees * (Math.PI / 180.0),
            lonDegrees * (Math.PI / 180.0)
        );

        return new Vector3((float)direction.x, (float)direction.y, (float)direction.z);
    }

    private void CreateFloridaMarker()
    {
        _markerTransform = CreateGeographicMarkerCube(
            "FloridaMarker",
            FloridaLatitudeDegrees,
            FloridaLongitudeDegrees,
            new Color(1f, 0.12f, 0.08f),
            markerCubeScale
        );
    }

    private void CreateMaltaMarker()
    {
        CreateGeographicMarkerCube(
            "MaltaMarker",
            MaltaLatitudeDegrees,
            MaltaLongitudeDegrees,
            new Color(0.15f, 0.45f, 0.95f),
            maltaMarkerCubeScale
        );
    }

    private Transform CreateGeographicMarkerCube(
        string markerName,
        float latitudeDegrees,
        float longitudeDegrees,
        Color color,
        float scale
    )
    {
        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        markerObject.name = markerName;
        markerObject.transform.SetParent(_globeTransform, false);

        Collider collider = markerObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = markerObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        Transform markerTransform = markerObject.transform;
        markerTransform.localScale = Vector3.one * scale;
        UpdateGeographicMarkerPosition(markerTransform, latitudeDegrees, longitudeDegrees, scale);
        return markerTransform;
    }

    private void UpdateFloridaMarkerPosition()
    {
        if (_markerTransform == null || _globeTransform == null)
        {
            return;
        }

        UpdateGeographicMarkerPosition(
            _markerTransform,
            FloridaLatitudeDegrees,
            FloridaLongitudeDegrees,
            markerCubeScale
        );
    }

    private void UpdateGeographicMarkerPosition(
        Transform markerTransform,
        float latitudeDegrees,
        float longitudeDegrees,
        float scale
    )
    {
        float halfExtent = scale * 0.5f;
        markerTransform.localPosition = EarthSandboxGlobePaint.MeshDisplayPositionFromLatitudeLongitudeDegrees(
            latitudeDegrees,
            longitudeDegrees,
            globeRadius,
            halfExtent,
            reliefExaggeration: reliefExaggeration
        );
    }

    private void SetupProjectCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        CameraController controller = mainCamera.GetComponent<CameraController>();
        if (controller == null)
        {
            controller = mainCamera.gameObject.AddComponent<CameraController>();
        }

        float minOrbit = globeRadius + EarthSandboxScene.CameraOrbitUnityMinPadding;
        float maxOrbit = EarthSandboxScene.CameraOrbitUnityMax;
        controller.SetNormalOrbitUnityLimits(minOrbit, maxOrbit);

        float orbitDistance = Mathf.Max(
            globeRadius * 1.55f,
            globeRadius + markerCubeScale + 20f
        );
        orbitDistance = Mathf.Clamp(orbitDistance, minOrbit, maxOrbit);

        controller.SetTarget(transform);
        controller.SetMapModeActive(false, 1f);
        controller.SetNormalOrbitDistanceUnity(orbitDistance);
        controller.SetOrbitAngles(20f, 24f);

        EarthSandboxQuadtreeManager quadtree = GetComponent<EarthSandboxQuadtreeManager>();
        if (quadtree != null)
        {
            quadtree.ConfigureSandboxOrbit(minOrbit, maxOrbit);
        }

        Vector3 pivot = transform.position;
        Quaternion orbitRotation = Quaternion.Euler(24f, 20f, 0f);
        Vector3 cameraPosition = pivot + orbitRotation * new Vector3(0f, 0f, -orbitDistance);
        mainCamera.transform.position = cameraPosition;
        mainCamera.transform.rotation = Quaternion.LookRotation(
            pivot - cameraPosition,
            Vector3.up
        );
    }

    private void OnDestroy()
    {
        if (_geographicRaster != null)
        {
            _geographicRaster.Release();
            _geographicRaster = null;
        }

        if (_globeMesh != null)
        {
            Destroy(_globeMesh);
        }

        EarthSandboxGlobePaint.ReleaseSharedElevation();
    }

    private static string InferLayout(int canonMatches, int mirrorMatches, int total)
    {
        if (total <= 0)
        {
            return "Missing";
        }

        if (canonMatches > mirrorMatches && canonMatches >= total - 1)
        {
            return "Canonical";
        }

        if (mirrorMatches > canonMatches && mirrorMatches >= total - 1)
        {
            return "Mirrored";
        }

        return "Ambiguous";
    }

    private readonly struct Stage3Summary
    {
        public Stage3Summary(bool elevationCanonical, bool colorCanonical, bool recommendedColorFlipU)
        {
            ElevationCanonical = elevationCanonical;
            ColorCanonical = colorCanonical;
            RecommendedColorFlipU = recommendedColorFlipU;
        }

        public bool ElevationCanonical { get; }

        public bool ColorCanonical { get; }

        public bool RecommendedColorFlipU { get; }

        public bool CanProceedToStage4 => ElevationCanonical && ColorCanonical;
    }

    private readonly struct ProbeSite
    {
        public ProbeSite(
            string label,
            double latitudeDegrees,
            double longitudeDegrees,
            bool expectLand,
            double minElevationMeters,
            double maxElevationMeters,
            double minHeightForMountains
        )
        {
            Label = label;
            LatitudeDegrees = latitudeDegrees;
            LongitudeDegrees = longitudeDegrees;
            ExpectLand = expectLand;
            MinElevationMeters = minElevationMeters;
            MaxElevationMeters = maxElevationMeters;
            MinHeightForMountains = minHeightForMountains;
        }

        public string Label { get; }

        public double LatitudeDegrees { get; }

        public double LongitudeDegrees { get; }

        public bool ExpectLand { get; }

        public double MinElevationMeters { get; }

        public double MaxElevationMeters { get; }

        public double MinHeightForMountains { get; }

        public string ExpectationLabel => ExpectLand ? "LAND" : "OCEAN";

        public bool MatchesElevation(short heightMeters)
        {
            if (heightMeters == short.MinValue)
            {
                return false;
            }

            return heightMeters >= MinElevationMeters && heightMeters <= MaxElevationMeters;
        }

        public bool MatchesColorClass(string colorClass)
        {
            if (ExpectLand)
            {
                return colorClass == "LAND";
            }

            return colorClass == "OCEAN";
        }
    }

    private readonly struct SiteProbeResult
    {
        public SiteProbeResult(
            double geographicU,
            double geographicV,
            double canonicalValue,
            double mirroredValue,
            bool canonicalMatchesExpectation,
            bool mirroredMatchesExpectation,
            string verdict,
            string canonicalDetail,
            string mirroredDetail,
            string canonicalClass,
            string mirroredClass
        )
        {
            GeographicU = geographicU;
            GeographicV = geographicV;
            CanonicalValue = canonicalValue;
            MirroredValue = mirroredValue;
            CanonicalMatchesExpectation = canonicalMatchesExpectation;
            MirroredMatchesExpectation = mirroredMatchesExpectation;
            Verdict = verdict;
            CanonicalDetail = canonicalDetail;
            MirroredDetail = mirroredDetail;
            CanonicalClass = canonicalClass;
            MirroredClass = mirroredClass;
        }

        public double GeographicU { get; }

        public double GeographicV { get; }

        public double CanonicalValue { get; }

        public double MirroredValue { get; }

        public bool CanonicalMatchesExpectation { get; }

        public bool MirroredMatchesExpectation { get; }

        public string Verdict { get; }

        public string CanonicalDetail { get; }

        public string MirroredDetail { get; }

        public string CanonicalClass { get; }

        public string MirroredClass { get; }

        public static SiteProbeResult Missing(double u, double v)
        {
            return new SiteProbeResult(
                u,
                v,
                0,
                0,
                false,
                false,
                "MISSING",
                "file not found",
                string.Empty,
                string.Empty,
                string.Empty
            );
        }
    }

#if UNITY_EDITOR
    [MenuItem("Space Program/Earth Sandbox/Create Earth_Sandbox_Test Scene")]
    private static void CreateSandboxScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.transform.position = new Vector3(0f, 40f, 160f);
        cameraObject.transform.LookAt(Vector3.zero);

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject proberObject = new GameObject("EarthProber");
        EarthAssetValidator validator = proberObject.AddComponent<EarthAssetValidator>();
        validator.globeRadius = 100f;
        validator.buildVisualGlobe = true;

        EarthSandboxQuadtreeManager quadtree = proberObject.AddComponent<EarthSandboxQuadtreeManager>();
        quadtree.SyncVisualGizmoRadius(validator.globeRadius);
        proberObject.AddComponent<TerrainGraphicsDebugPanel>();

        cameraObject.AddComponent<CameraController>();

        string scenesFolder = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(scenesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        string scenePath = scenesFolder + "/" + EarthSandboxScene.SceneName + ".unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        RestoreGameAndSandboxBuildSettings();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[EarthSandbox] Scene saved: {scenePath}. " +
            "Run Space Program → Restore Game Build Settings if menu/VAB scenes are missing."
        );
    }

    [MenuItem("Space Program/Restore Game Build Settings")]
    private static void RestoreGameBuildSettingsMenu()
    {
        if (!RestoreGameAndSandboxBuildSettings())
        {
            Debug.LogError(
                "[EarthSandbox] Restore failed — no scenes found. " +
                "Use Create Empty Menu Bootstrap Scene or restore VabScene from project."
            );
            return;
        }

        Debug.Log(
            "[EarthSandbox] Build Settings restored. First scene: " +
            EditorBuildSettings.scenes[0].path
        );
    }

    [MenuItem("Space Program/Earth Sandbox/Create Empty Menu Bootstrap Scene")]
    private static void CreateEmptyMenuBootstrapScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        cameraObject.AddComponent<AudioListener>();

        string scenesFolder = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(scenesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        string scenePath = scenesFolder + "/MenuBootstrap.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        RestoreGameAndSandboxBuildSettings();
        AssetDatabase.Refresh();

        Debug.Log(
            "[EarthSandbox] Empty menu scene saved: " + scenePath +
            ". Set as Play Mode start scene or press Play on it for main menu only."
        );
    }

    /// <summary>
    /// Re-add bootstrap + VAB + sandbox scenes without removing unrelated build entries.
    /// </summary>
    static bool RestoreGameAndSandboxBuildSettings()
    {
        var pathsInOrder = new List<string>();

        for (int i = 0; i < BootstrapSceneNamePriority.Length; i++)
        {
            string path = FindSceneAssetPathByName(BootstrapSceneNamePriority[i]);
            AddScenePathIfPresent(pathsInOrder, path);
        }

        string sandboxPath = FindSceneAssetPathByName(EarthSandboxScene.SceneName) ??
            EarthSandboxScene.DefaultSceneAssetPath;
        AddScenePathIfPresent(pathsInOrder, sandboxPath);

        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            AddScenePathIfPresent(pathsInOrder, existing.path);
        }

        if (pathsInOrder.Count == 0)
        {
            return false;
        }

        var buildScenes = new List<EditorBuildSettingsScene>();

        for (int i = 0; i < pathsInOrder.Count; i++)
        {
            buildScenes.Add(new EditorBuildSettingsScene(pathsInOrder[i], true));
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        return true;
    }

    static void AddScenePathIfPresent(List<string> pathsInOrder, string sceneAssetPath)
    {
        if (string.IsNullOrEmpty(sceneAssetPath))
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneAssetPath) == null)
        {
            return;
        }

        for (int i = 0; i < pathsInOrder.Count; i++)
        {
            if (string.Equals(pathsInOrder[i], sceneAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        pathsInOrder.Add(sceneAssetPath);
    }

    [MenuItem("Space Program/Earth Sandbox/Add To Build Settings")]
    private static void AddSandboxSceneToBuildSettingsMenu()
    {
        if (!RestoreGameAndSandboxBuildSettings())
        {
            string sandboxPath = FindSceneAssetPathByName(EarthSandboxScene.SceneName) ??
                EarthSandboxScene.DefaultSceneAssetPath;

            if (!EnsureSceneInBuildSettings(sandboxPath, enable: true, preferAsFirstScene: false))
            {
                Debug.LogError(
                    "[EarthSandbox] Scene \"" + EarthSandboxScene.SceneName + "\" not found. " +
                    "Run Create Earth_Sandbox_Test Scene first.\n" +
                    "Scenes in project:\n  " + DescribeAllSceneAssetsInProject()
                );
                return;
            }
        }

        Debug.Log(
            "[EarthSandbox] Build Settings updated (bootstrap + sandbox). " +
            "Use Restore Game Build Settings if VAB scene is still missing."
        );
    }

    [MenuItem("Space Program/Earth Sandbox/Put Bootstrap Scene First In Build Settings")]
    private static void PutBootstrapSceneFirstInBuildSettings()
    {
        if (!RestoreGameAndSandboxBuildSettings())
        {
            Debug.LogError(
                "[EarthSandbox] No scenes to register. Create Menu Bootstrap or restore VabScene.\n" +
                "Scenes in project:\n  " + DescribeAllSceneAssetsInProject()
            );
            return;
        }

        string firstPath = EditorBuildSettings.scenes[0].path;
        string bootstrapPath = FindBootstrapSceneAssetPath() ?? FindSceneAssetPathByName("MenuBootstrap");

        if (bootstrapPath != null)
        {
            EnsureSceneInBuildSettings(bootstrapPath, enable: true, preferAsFirstScene: true);
            firstPath = bootstrapPath;
        }

        Debug.Log("[EarthSandbox] Build Settings: first scene is " + firstPath);
    }

    static readonly string[] BootstrapSceneNamePriority =
    {
        "MenuBootstrap",
        "MenuScene",
        "PlaybackScene",
        "VabScene",
        "VABScene",
        "VABlvl3",
        "vablvl3",
        "vablvl",
    };

    static string FindBootstrapSceneAssetPath()
    {
        for (int i = 0; i < BootstrapSceneNamePriority.Length; i++)
        {
            string path = FindSceneAssetPathByName(BootstrapSceneNamePriority[i]);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }
        }

        return null;
    }

    static string FindSceneAssetPathByName(string sceneName)
    {
        string[] guids = AssetDatabase.FindAssets(sceneName + " t:Scene");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(fileName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    static string DescribeAllSceneAssetsInProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene");
        var lines = new List<string>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            lines.Add(Path.GetFileNameWithoutExtension(path) + " → " + path);
        }

        if (lines.Count == 0)
        {
            return "(no .unity scenes in Assets)";
        }

        return string.Join("\n  ", lines);
    }

    static bool EnsureSceneInBuildSettings(string sceneAssetPath, bool enable, bool preferAsFirstScene)
    {
        string resolvedPath = sceneAssetPath;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(resolvedPath) == null)
        {
            string fileName = Path.GetFileNameWithoutExtension(sceneAssetPath);
            resolvedPath = FindSceneAssetPathByName(fileName);
        }

        if (resolvedPath == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(resolvedPath) == null)
        {
            return false;
        }

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int existingIndex = -1;

        for (int i = 0; i < scenes.Count; i++)
        {
            if (string.Equals(scenes[i].path, resolvedPath, StringComparison.OrdinalIgnoreCase))
            {
                existingIndex = i;
                break;
            }
        }

        var entry = new EditorBuildSettingsScene(resolvedPath, enable);

        if (existingIndex >= 0)
        {
            scenes[existingIndex] = entry;
        }
        else
        {
            scenes.Add(entry);
        }

        if (preferAsFirstScene && scenes.Count > 1)
        {
            int index = existingIndex >= 0 ? existingIndex : scenes.Count - 1;
            EditorBuildSettingsScene picked = scenes[index];
            scenes.RemoveAt(index);
            scenes.Insert(0, picked);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        return true;
    }
#endif
}

/// <summary>Identifies the isolated Earth calibration scene so global bootstraps stay out.</summary>
public static class EarthSandboxScene
{
    public const string SceneName = "Earth_Sandbox_Test";

    public const string DefaultSceneAssetPath = "Assets/Scenes/Earth_Sandbox_Test.unity";

    /// <summary>Sandbox orbit distance from globe center (Unity units; globe r≈100).</summary>
    public const float CameraOrbitUnityMax = 480f;

    public const float CameraOrbitUnityMinPadding = 5f;

    static bool enteredFromMainMenu;

    /// <summary>True when the player opened the sandbox from the main menu (not cold start).</summary>
    public static bool EnteredFromMainMenu => enteredFromMainMenu;

    public static bool IsActive()
    {
        return string.Equals(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            SceneName,
            StringComparison.OrdinalIgnoreCase
        );
    }

    public static bool CanLoadScene()
    {
        return Application.CanStreamedLevelBeLoaded(SceneName);
    }

    public static void MarkEnteredFromMainMenu()
    {
        enteredFromMainMenu = true;
    }

    public static void ResetSandboxSessionFlags()
    {
        enteredFromMainMenu = false;
    }

    /// <summary>Cold start in sandbox — load bootstrap scene if listed in Build Settings.</summary>
    public static bool TryRedirectStartupToMainMenu()
    {
        return FlightSessionSceneUtility.TryLoadMainMenuBootstrapScene();
    }

    public static bool TryLoadScene()
    {
        if (!CanLoadScene())
        {
            Debug.LogError(
                "[EarthSandbox] Cannot load scene \"" + SceneName + "\". " +
                "Create it via Space Program → Earth Sandbox → Create Earth_Sandbox_Test Scene, " +
                "then Space Program → Earth Sandbox → Add To Build Settings."
            );
            return false;
        }

        MarkEnteredFromMainMenu();
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneName);
        return true;
    }

    /// <summary>Reload sandbox after ESC — empty scene, main menu stays on SimulationRunner.</summary>
    public static bool TryReloadSceneWithoutSandboxSession()
    {
        ResetSandboxSessionFlags();

        if (!CanLoadScene())
        {
            return false;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneName);
        return true;
    }
}
