using UnityEngine;

/// <summary>
/// Samples RSS terrain height at the configured launch site and shares it with SimulationCore.
/// </summary>
static class LaunchSiteTerrain
{
    static bool launchDiagnosticsLogged;

    public static float CachedRawHeightMeters { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPlaySession()
    {
        launchDiagnosticsLogged = false;
    }

    public static void Refresh(Texture2D heightmap)
    {
        LaunchSiteConfig.Site activeSite = LaunchSiteConfig.Active;
        CachedRawHeightMeters = PlanetFlightElevationSampling.SampleMeshHeightOffsetMeters(
            WorldVisualBridge.SimNormalToUnity(
                LandingConfig.DirectionFromPlanetLatitudeLongitude(
                    activeSite.LatitudeRadians,
                    activeSite.LongitudeRadians
                )
            )
        );
        LaunchPadConstants.SetLaunchSiteTerrainHeightAboveSphereMeters(CachedRawHeightMeters);

        if (launchDiagnosticsLogged)
        {
            return;
        }

        launchDiagnosticsLogged = true;
        double gebcoMeters = PlanetFlightElevationSampling.SampleRealElevationMeters(
            activeSite.LatitudeDegrees,
            activeSite.LongitudeDegrees
        );
        Debug.Log(
            "LaunchSiteTerrain: " + Localization.Translate(activeSite.LocalizationKey) +
            " GEBCO=" + gebcoMeters.ToString("F0") + "m, meshOffset=" +
            CachedRawHeightMeters.ToString("F2") + "m, padBase=" +
            LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters().ToString("F2") + "m."
        );
        LogLaunchSiteTextureSample(heightmap);
    }

    static void LogLaunchSiteTextureSample(Texture2D heightmap)
    {
        Vector3 direction = WorldVisualBridge.GetLaunchSiteNormalUnity();
        Vector2 geoUv = PlanetCubeSphere.DirectionToGeographicUv(direction);
        Vector2 expectedGeoUv = ExpectedSimulationUv(LaunchSiteConfig.Active);
        Texture2D colorMap = CelestialTerrainRuntime.IsMoon ?
            PlanetTerrainTextureResources.MoonColorMap :
            PlanetTerrainTextureResources.EarthColorMap;
        string colorText = "missing";

        if (colorMap != null)
        {
            Color colorSample = colorMap.GetPixelBilinear(geoUv.x, geoUv.y);
            colorText =
                "rgb(" + colorSample.r.ToString("F2") + ", " +
                colorSample.g.ToString("F2") + ", " +
                colorSample.b.ToString("F2") + ")";
        }

        Debug.Log(
            "LaunchSiteTerrain: " +
            Localization.Translate(LaunchSiteConfig.Active.LocalizationKey) +
            " geoUv=(" + geoUv.x.ToString("F3") + ", " + geoUv.y.ToString("F3") +
            "), expectedGeoUv=(" + expectedGeoUv.x.ToString("F3") + ", " +
            expectedGeoUv.y.ToString("F3") + ")" +
            ", color=" + colorText +
            ", height=" + SampleHeightAtDirection(heightmap, LandingConfig.GetSurfaceLaunchPadNormal()).ToString("F2") + "m"
        );
    }

    /// <summary>
    /// Logs where the ship actually sits in sim geography (not just LandingConfig constants).
    /// If this disagrees with the configured Cape Canaveral coords, the spawn bug is in Core;
    /// if it matches but the map/terrain looks like another continent, the bug is in rendering.
    /// </summary>
    public static void LogShipGeography(Simulation simulation, Texture2D heightmap)
    {
        if (simulation == null || simulation.planet == null || simulation.ship == null)
        {
            return;
        }

        global::SimVector3 shipOut = simulation.GetShipRadialOutDirectionRelativeToActivePrimary();

        if (shipOut.LengthSquared() <= 1e-12)
        {
            return;
        }

        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            shipOut,
            out double shipLatitudeRadians,
            out double shipLongitudeRadians
        );
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            shipOut,
            out double shipU,
            out double shipV
        );

        global::SimVector3 configOut = LandingConfig.GetSurfaceLaunchPadNormal();
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            configOut,
            out double configU,
            out double configV
        );

        float shipSampleHeight = SampleHeightAtDirection(heightmap, shipOut);
        float configSampleHeight = SampleHeightAtDirection(heightmap, configOut);
        double arcToLaunchSiteMeters =
            simulation.GetShipSurfaceDistanceFromLaunchSiteMeters();

        Debug.Log(
            "LaunchSiteTerrain: ship geography " +
            FormatLatitudeDegrees(shipLatitudeRadians) + ", " +
            FormatLongitudeDegrees(shipLongitudeRadians) +
            ", uv=(" + shipU.ToString("F3") + ", " + shipV.ToString("F3") + ")" +
            ", heightSample=" + shipSampleHeight.ToString("F2") + "m" +
            ", arcToConfig=" + arcToLaunchSiteMeters.ToString("F1") + "m" +
            ". Config uv=(" + configU.ToString("F3") + ", " + configV.ToString("F3") +
            "), configHeightSample=" + configSampleHeight.ToString("F2") + "m."
        );
    }

    /// <summary>
    /// Confirms GEBCO height and Unity placement at rocket spawn (work_terrain.md canon).
    /// </summary>
    public static void LogShipSpawnDiagnostic(Simulation simulation, Transform shipTransform)
    {
        if (simulation == null || simulation.ship == null)
        {
            return;
        }

        global::SimVector3 shipOut = simulation.GetShipRadialOutDirectionRelativeToActivePrimary();

        if (shipOut.LengthSquared() <= 1e-12)
        {
            return;
        }

        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            shipOut,
            out double shipLatitudeRadians,
            out double shipLongitudeRadians
        );
        double gebcoMeters = PlanetFlightElevationSampling.SampleRealElevationMeters(
            shipLatitudeRadians * (180.0 / System.Math.PI),
            shipLongitudeRadians * (180.0 / System.Math.PI)
        );
        Vector3 unityPosition = shipTransform != null ?
            shipTransform.position :
            Vector3.zero;

        Debug.Log(
            "Спавн корабля: Симуляция Lat/Lon = " +
            FormatLatitudeDegrees(shipLatitudeRadians) + "/" +
            FormatLongitudeDegrees(shipLongitudeRadians) +
            ", Физическая высота GEBCO = " + gebcoMeters.ToString("F0") + "м" +
            ", Визуальная позиция Unity = " + unityPosition + "."
        );
    }

    /// <summary>
    /// Logs which geography the flight-frame viewer (+Y) samples on the planet mesh.
    /// If this disagrees with ship geography, terrain under the camera is misaligned.
    /// </summary>
    public static void LogViewerGeography(Transform planetTransform, Texture2D heightmap)
    {
        if (planetTransform == null)
        {
            return;
        }

        Vector3 planetToViewer = Vector3.zero - planetTransform.position;

        if (planetToViewer.sqrMagnitude < 1f)
        {
            return;
        }

        Vector3 localDirection = planetTransform.InverseTransformDirection(
            planetToViewer
        ).normalized;
        Vector3 simDirection = PlanetCubeSphere.MeshFrameUnitToSimUnit(localDirection);
        Vector2 geoUv = PlanetCubeSphere.DirectionToGeographicUv(simDirection);

        float heightSample = SampleHeightAtUv(heightmap, geoUv);

        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            new SimVector3(simDirection.x, simDirection.y, simDirection.z),
            out double latitudeRadians,
            out double longitudeRadians
        );
        Texture2D colorMap = PlanetTerrainTextureResources.EarthColorMap;
        string colorText = "missing";

        if (colorMap != null)
        {
            Color colorSample = PlanetTerrainTextureResources.SampleEarthColorAtGeographicUv(
                geoUv.x,
                geoUv.y
            );
            colorText =
                "rgb(" + colorSample.r.ToString("F2") + ", " +
                colorSample.g.ToString("F2") + ", " +
                colorSample.b.ToString("F2") + ")";
        }

        Debug.Log(
            "LaunchSiteTerrain: viewer geography " +
            FormatLatitudeDegrees(latitudeRadians) + ", " +
            FormatLongitudeDegrees(longitudeRadians) +
            ", geoUv=(" + geoUv.x.ToString("F3") + ", " + geoUv.y.ToString("F3") + ")" +
            ", planetLocalRotEuler=" + planetTransform.localRotation.eulerAngles.ToString("F1") +
            ", EarthColor=" + colorText +
            ", heightSample=" + heightSample.ToString("F2") + "m."
        );
    }

    static Vector2 ExpectedSimulationUv(LaunchSiteConfig.Site site)
    {
        double u = PlanetGeographicCoords.LongitudeRadiansToGeographicU(site.LongitudeRadians);
        double v = PlanetGeographicCoords.LatitudeRadiansToGeographicV(site.LatitudeRadians);
        return new Vector2((float)u, (float)v);
    }

    static float SampleHeightAtUv(Texture2D heightmap, Vector2 uv)
    {
        PlanetGeographicCoords.SimulationGeographicUvToLatitudeLongitude(
            uv.x,
            uv.y,
            out double latitudeRadians,
            out double longitudeRadians
        );
        Vector3 direction = PlanetCubeSphere.DirectionFromLatLongRadians(
            (float)latitudeRadians,
            (float)longitudeRadians
        );
        return PlanetFlightElevationSampling.SampleMeshHeightOffsetMeters(direction);
    }

    static float SampleHeightAtDirection(Texture2D heightmap, global::SimVector3 direction)
    {
        return PlanetFlightElevationSampling.SampleMeshHeightOffsetMeters(direction);
    }

    static string FormatLatitudeDegrees(double latitudeRadians)
    {
        double degrees = latitudeRadians * 180.0 / System.Math.PI;
        return System.Math.Abs(degrees).ToString("F2") + "°" + (degrees >= 0.0 ? "N" : "S");
    }

    static string FormatLongitudeDegrees(double longitudeRadians)
    {
        double degrees = longitudeRadians * 180.0 / System.Math.PI;
        return System.Math.Abs(degrees).ToString("F2") + "°" + (degrees >= 0.0 ? "E" : "W");
    }

    public static float SampleHeightAboveSphereAtLaunchSite(Texture2D heightmap)
    {
        if (heightmap == null)
        {
            return 0f;
        }

        Vector3 direction = WorldVisualBridge.GetLaunchSiteNormalUnity();
        Vector2 uv = PlanetCubeSphere.DirectionToGeographicUv(direction);
        return SampleHeightAtUv(heightmap, uv);
    }
}
