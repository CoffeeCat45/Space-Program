using UnityEngine;

/// <summary>
/// Asymmetric GEBCO vs NASA color probes at three longitudes (west Andes, east Indian Ocean,
/// west Florida). Isolates whether earth_geographic.png, UV math, or spawn/map navigation is wrong.
/// </summary>
static class PlanetThreePointsOfTruthDiagnostic
{
    static bool fixedProbesLogged;

    public readonly struct Probe
    {
        public Probe(
            string label,
            double latitudeDegrees,
            double longitudeDegrees,
            string expectedGebco,
            string expectedVisual
        )
        {
            Label = label;
            LatitudeDegrees = latitudeDegrees;
            LongitudeDegrees = longitudeDegrees;
            ExpectedGebco = expectedGebco;
            ExpectedVisual = expectedVisual;
        }

        public string Label { get; }

        public double LatitudeDegrees { get; }

        public double LongitudeDegrees { get; }

        public string ExpectedGebco { get; }

        public string ExpectedVisual { get; }

        public LaunchSiteConfig.Site ToLaunchSite()
        {
            return new LaunchSiteConfig.Site(
                "ThreePoints " + Label,
                LatitudeDegrees,
                LongitudeDegrees
            );
        }
    }

    public static readonly Probe[] Probes =
    {
        new Probe(
            "Point1_Andes_80W",
            0.0,
            -80.0,
            "land > 2000m (Andes)",
            "green/brown land"
        ),
        new Probe(
            "Point2_IndianOcean_60E",
            0.0,
            60.0,
            "ocean < -3000m",
            "blue ocean"
        ),
        new Probe(
            "Point3_CapeCanaveral_80W",
            28.4,
            -80.6,
            "coast ~0-20m",
            "green/brown coast"
        ),
    };

    public static Probe GetProbe(int index)
    {
        if (index < 0 || index >= Probes.Length)
        {
            return Probes[0];
        }

        return Probes[index];
    }

    /// <summary>Runs once when earth color loads — no spawn required.</summary>
    public static void RunFixedProbesOnce()
    {
        if (fixedProbesLogged)
        {
            return;
        }

        fixedProbesLogged = true;
        RunFixedProbes();
    }

    /// <summary>Runs once when earth color loads — no spawn required.</summary>
    public static void RunFixedProbes()
    {
        if (!PlanetElevationCatalog.IsHighResLoaded && !PlanetElevationCatalog.IsLoaded)
        {
            Debug.LogWarning(
                "ThreePointsOfTruth: GEBCO elevation not loaded — skipping fixed probes."
            );
            return;
        }

        if (PlanetTerrainTextureResources.EarthColorMap == null)
        {
            Debug.LogWarning(
                "ThreePointsOfTruth: earth_geographic color map not loaded — skipping fixed probes."
            );
            return;
        }

        Debug.Log(
            "ThreePointsOfTruth: === fixed probes (GEBCO physics vs NASA texture at canonical U) ==="
        );

        int matchCount = 0;

        for (int i = 0; i < Probes.Length; i++)
        {
            if (LogProbe(Probes[i], out bool physicsVisualAgree))
            {
                if (physicsVisualAgree)
                {
                    matchCount++;
                }
            }
        }

        if (matchCount == Probes.Length)
        {
            Debug.Log(
                "ThreePointsOfTruth: SUMMARY " + matchCount + "/" + Probes.Length +
                " physics+visual AGREE → earth_geographic.png matches GEBCO at probe sites."
            );
        }
        else if (matchCount >= Probes.Length - 1)
        {
            Debug.Log(
                "ThreePointsOfTruth: SUMMARY " + matchCount + "/" + Probes.Length +
                " physics+visual AGREE → texture mostly OK (coastal sites can false-mismatch). " +
                "If map marker is on wrong continent with uvDelta≈0, check map shader UV — not spawn."
            );
        }
        else
        {
            Debug.LogWarning(
                "ThreePointsOfTruth: SUMMARY " + matchCount + "/" + Probes.Length +
                " physics+visual AGREE → run Space Program / Fix Earth Color Map Longitude (.png) " +
                "once in Editor, then restart Play (do not runtime-flip)."
            );
        }
    }

    /// <summary>After pad spawn — compares sim position, GEBCO, and texture at ship.</summary>
    public static void LogActiveSpawnTruth(Simulation simulation)
    {
        if (simulation == null)
        {
            return;
        }

        global::SimVector3 shipRadial = simulation.GetShipRadialOutDirectionRelativeToActivePrimary();

        if (shipRadial.LengthSquared() <= 1e-12)
        {
            return;
        }

        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            shipRadial,
            out double shipLatitudeRadians,
            out double shipLongitudeRadians
        );
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            shipRadial,
            out double shipU,
            out double shipV
        );

        double shipLatDeg = shipLatitudeRadians * 180.0 / System.Math.PI;
        double shipLonDeg = shipLongitudeRadians * 180.0 / System.Math.PI;

        LaunchSiteConfig.Site config = LaunchSiteConfig.Active;
        double configLonU = PlanetGeographicCoords.LongitudeRadiansToGeographicU(config.LongitudeRadians);

        SampleAtDegrees(shipLatDeg, shipLonDeg, out _, out _, out double gebcoMeters, out Color earthColor);
        Classify(gebcoMeters, earthColor, out string gebcoClass, out string colorClass, out bool agree);

        double lonDeltaDeg = shipLonDeg - config.LongitudeDegrees;

        if (lonDeltaDeg > 180.0)
        {
            lonDeltaDeg -= 360.0;
        }
        else if (lonDeltaDeg < -180.0)
        {
            lonDeltaDeg += 360.0;
        }

        Debug.Log(
            "ThreePointsOfTruth: SPAWN " +
            Localization.Translate(config.LocalizationKey) +
            " | ship " + shipLatDeg.ToString("F2") + "°N, " + shipLonDeg.ToString("F2") + "°E" +
            " | config " + config.LatitudeDegrees.ToString("F2") + "°N, " +
            config.LongitudeDegrees.ToString("F2") + "°E" +
            " | lonDelta=" + lonDeltaDeg.ToString("F3") + "°" +
            " | uv=(" + shipU.ToString("F3") + ", " + shipV.ToString("F3") + ")" +
            " | configCanonicalU=" + configLonU.ToString("F3") +
            " | GEBCO=" + gebcoMeters.ToString("F0") + "m (" + gebcoClass + ")" +
            " | NASA rgb=(" + earthColor.r.ToString("F2") + ", " + earthColor.g.ToString("F2") + ", " +
            earthColor.b.ToString("F2") + ") (" + colorClass + ")" +
            " | physicsVisualAtShip=" + (agree ? "AGREE" : "DISAGREE") +
            ". Press M: if uvDelta≈0 but marker on wrong continent → variant B (navigation/UI)."
        );
    }

    public static void LogProbeAfterSpawn(int probeIndex)
    {
        if (probeIndex < 0 || probeIndex >= Probes.Length)
        {
            return;
        }

        Debug.Log(
            "ThreePointsOfTruth: === spawn probe " + (probeIndex + 1) + "/3 (" +
            Probes[probeIndex].Label + ") ==="
        );
        LogProbe(Probes[probeIndex], out _);
        LogActiveSpawnTruth(FindActiveSimulation());
    }

    static Simulation FindActiveSimulation()
    {
        SimulationRunner runner = Object.FindFirstObjectByType<SimulationRunner>();

        return runner != null ? runner.Simulation : null;
    }

    static bool LogProbe(Probe probe, out bool physicsVisualAgree)
    {
        physicsVisualAgree = false;

        SampleAtDegrees(
            probe.LatitudeDegrees,
            probe.LongitudeDegrees,
            out double geographicU,
            out double geographicV,
            out double gebcoMeters,
            out Color earthColor
        );

        Classify(
            gebcoMeters,
            earthColor,
            out string gebcoClass,
            out string colorClass,
            out physicsVisualAgree
        );

        double canonicalUFromLon =
            PlanetGeographicCoords.LongitudeRadiansToGeographicU(
                probe.LongitudeDegrees * System.Math.PI / 180.0
            );

        Debug.Log(
            "ThreePointsOfTruth: [" + probe.Label + "] " +
            probe.LatitudeDegrees.ToString("F1") + "°N, " +
            probe.LongitudeDegrees.ToString("F1") + "°E" +
            " | canonicalU(lon)=" + canonicalUFromLon.ToString("F3") +
            " geoUv=(" + geographicU.ToString("F3") + ", " + geographicV.ToString("F3") + ")" +
            " | expect GEBCO: " + probe.ExpectedGebco +
            " | expect visual: " + probe.ExpectedVisual +
            " | GEBCO=" + gebcoMeters.ToString("F0") + "m (" + gebcoClass + ")" +
            " | NASA rgb=(" + earthColor.r.ToString("F2") + ", " + earthColor.g.ToString("F2") + ", " +
            earthColor.b.ToString("F2") + ") (" + colorClass + ")" +
            " | " + (physicsVisualAgree ? "MATCH" : "MISMATCH")
        );

        return true;
    }

    static void SampleAtDegrees(
        double latitudeDegrees,
        double longitudeDegrees,
        out double geographicU,
        out double geographicV,
        out double gebcoMeters,
        out Color earthColor
    )
    {
        double latRadians = latitudeDegrees * System.Math.PI / 180.0;
        double lonRadians = longitudeDegrees * System.Math.PI / 180.0;
        PlanetGeographicCoords.DirectionToSimulationGeographicUv(
            PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRadians, lonRadians),
            out geographicU,
            out geographicV
        );

        gebcoMeters = PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(
            latitudeDegrees,
            longitudeDegrees
        );
        earthColor = PlanetTerrainTextureResources.SampleEarthColorAtGeographicUv(
            geographicU,
            geographicV
        );
    }

    static void Classify(
        double gebcoMeters,
        Color earthColor,
        out string gebcoClass,
        out string colorClass,
        out bool agree
    )
    {
        bool gebcoLand = gebcoMeters > 0.0;
        gebcoClass = gebcoLand ? "LAND" : "OCEAN";

        bool colorLooksLikeOcean =
            earthColor.b > earthColor.g + 0.04f && earthColor.b > 0.12f;
        bool colorLooksLikeLand =
            earthColor.g > 0.12f && earthColor.b < earthColor.g * 0.85f;

        if (colorLooksLikeOcean)
        {
            colorClass = "OCEAN";
        }
        else if (colorLooksLikeLand)
        {
            colorClass = "LAND";
        }
        else
        {
            colorClass = "MIXED";
        }

        agree = (gebcoLand && colorLooksLikeLand) || (!gebcoLand && colorLooksLikeOcean);
    }
}
