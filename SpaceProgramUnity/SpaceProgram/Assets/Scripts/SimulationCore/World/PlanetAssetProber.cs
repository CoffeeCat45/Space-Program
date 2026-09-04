using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Probes on-disk <c>earth_elevation.raw</c> and color maps at canonical geographic sites.
/// Determines whether each asset is stored canonical or E/W mirrored — without Play Mode guessing.
/// </summary>
public static class PlanetAssetProber
{
    public enum HorizontalLayout
    {
        Missing,
        Ambiguous,
        Canonical,
        Mirrored,
    }

    public readonly struct GeographicProbeSite
    {
        public GeographicProbeSite(
            string label,
            double latitudeDegrees,
            double longitudeDegrees,
            bool expectLand,
            double minElevationMeters = double.NegativeInfinity,
            double maxElevationMeters = double.PositiveInfinity
        )
        {
            Label = label;
            LatitudeDegrees = latitudeDegrees;
            LongitudeDegrees = longitudeDegrees;
            ExpectLand = expectLand;
            MinElevationMeters = minElevationMeters;
            MaxElevationMeters = maxElevationMeters;
        }

        public string Label { get; }

        public double LatitudeDegrees { get; }

        public double LongitudeDegrees { get; }

        public bool ExpectLand { get; }

        public double MinElevationMeters { get; }

        public double MaxElevationMeters { get; }
    }

    public readonly struct SiteSample
    {
        public SiteSample(
            GeographicProbeSite site,
            double geographicU,
            double geographicV,
            int canonicalPixelX,
            int mirroredPixelX,
            int pixelY,
            double canonicalElevationMeters,
            double mirroredElevationMeters,
            byte canonicalColorR,
            byte canonicalColorG,
            byte canonicalColorB,
            byte mirroredColorR,
            byte mirroredColorG,
            byte mirroredColorB,
            bool hasElevation,
            bool hasColor
        )
        {
            Site = site;
            GeographicU = geographicU;
            GeographicV = geographicV;
            CanonicalPixelX = canonicalPixelX;
            MirroredPixelX = mirroredPixelX;
            PixelY = pixelY;
            CanonicalElevationMeters = canonicalElevationMeters;
            MirroredElevationMeters = mirroredElevationMeters;
            CanonicalColorR = canonicalColorR;
            CanonicalColorG = canonicalColorG;
            CanonicalColorB = canonicalColorB;
            MirroredColorR = mirroredColorR;
            MirroredColorG = mirroredColorG;
            MirroredColorB = mirroredColorB;
            HasElevation = hasElevation;
            HasColor = hasColor;
        }

        public GeographicProbeSite Site { get; }

        public double GeographicU { get; }

        public double GeographicV { get; }

        public int CanonicalPixelX { get; }

        public int MirroredPixelX { get; }

        public int PixelY { get; }

        public double CanonicalElevationMeters { get; }

        public double MirroredElevationMeters { get; }

        public byte CanonicalColorR { get; }

        public byte CanonicalColorG { get; }

        public byte CanonicalColorB { get; }

        public byte MirroredColorR { get; }

        public byte MirroredColorG { get; }

        public byte MirroredColorB { get; }

        public bool HasElevation { get; }

        public bool HasColor { get; }
    }

    public sealed class AssetProbeReport
    {
        public string AssetPath { get; set; }

        public HorizontalLayout Layout { get; set; }

        public int CanonicalMatches { get; set; }

        public int MirroredMatches { get; set; }

        public int SitesProbed { get; set; }

        public List<SiteSample> Samples { get; } = new List<SiteSample>();

        public string Summary { get; set; }
    }

    public sealed class CombinedProbeReport
    {
        public AssetProbeReport Elevation { get; set; }

        public AssetProbeReport Color { get; set; }

        public PlanetMapLayoutPolicy RecommendedPolicy { get; set; }

        public string BuildFullText()
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("=== PlanetAssetProber (canonical geographicU law) ===");
            builder.AppendLine();

            AppendAssetSection(builder, "earth_elevation.raw", Elevation, isElevation: true);
            builder.AppendLine();
            AppendAssetSection(builder, "earth_geographic color", Color, isElevation: false);
            builder.AppendLine();
            builder.AppendLine("Recommended " + RecommendedPolicy);
            builder.AppendLine(
                "Apply flip flags only via PlanetMapLayoutPolicy in ONE sampler per asset — never split CPU/GPU."
            );
            return builder.ToString();
        }

        static void AppendAssetSection(
            StringBuilder builder,
            string title,
            AssetProbeReport report,
            bool isElevation
        )
        {
            if (report == null)
            {
                builder.AppendLine("[" + title + "] MISSING");
                return;
            }

            builder.AppendLine(
                "[" + title + "] path=" + (report.AssetPath ?? "n/a") +
                " layout=" + report.Layout +
                " canonicalMatches=" + report.CanonicalMatches + "/" + report.SitesProbed +
                " mirroredMatches=" + report.MirroredMatches + "/" + report.SitesProbed
            );
            builder.AppendLine(report.Summary ?? string.Empty);

            for (int i = 0; i < report.Samples.Count; i++)
            {
                SiteSample sample = report.Samples[i];
                builder.Append("  ").Append(sample.Site.Label);
                builder.Append(" lat=").Append(sample.Site.LatitudeDegrees.ToString("F2"));
                builder.Append(" lon=").Append(sample.Site.LongitudeDegrees.ToString("F2"));
                builder.Append(" geoU=").Append(sample.GeographicU.ToString("F4"));
                builder.Append(" geoV=").Append(sample.GeographicV.ToString("F4"));

                if (sample.HasElevation)
                {
                    builder.Append(" | elev@canonicalX=").Append(sample.CanonicalPixelX);
                    builder.Append("=").Append(sample.CanonicalElevationMeters.ToString("F0")).Append("m");
                    builder.Append(" elev@mirrorX=").Append(sample.MirroredPixelX);
                    builder.Append("=").Append(sample.MirroredElevationMeters.ToString("F0")).Append("m");
                    builder.Append(" expect=").Append(sample.Site.ExpectLand ? "LAND" : "OCEAN");
                }

                if (sample.HasColor)
                {
                    builder.Append(" | rgb@U,V=").Append(FormatRgb(
                        sample.CanonicalColorR,
                        sample.CanonicalColorG,
                        sample.CanonicalColorB
                    ));
                    builder.Append(" class=").Append(ClassifyColorLandOcean(
                        sample.CanonicalColorR,
                        sample.CanonicalColorG,
                        sample.CanonicalColorB
                    ));
                    builder.Append(" rgb@1-U,V=").Append(FormatRgb(
                        sample.MirroredColorR,
                        sample.MirroredColorG,
                        sample.MirroredColorB
                    ));
                    builder.Append(" class=").Append(ClassifyColorLandOcean(
                        sample.MirroredColorR,
                        sample.MirroredColorG,
                        sample.MirroredColorB
                    ));
                }

                builder.AppendLine();
            }
        }
    }

    public static readonly GeographicProbeSite[] StandardSites =
    {
        new GeographicProbeSite("Cape_Canaveral", 28.57, -80.65, expectLand: true, maxElevationMeters: 80.0),
        new GeographicProbeSite("Morocco", 31.63, -7.99, expectLand: true, maxElevationMeters: 2500.0),
        new GeographicProbeSite("Tibet", 31.48, 91.10, expectLand: true, minElevationMeters: 2500.0),
        new GeographicProbeSite("Equator_80W_Andes", 0.0, -80.0, expectLand: true, minElevationMeters: 200.0),
        new GeographicProbeSite("Equator_60E_Ocean", 0.0, 60.0, expectLand: false, maxElevationMeters: -50.0),
        new GeographicProbeSite("Pacific_160W", 0.0, -160.0, expectLand: false, maxElevationMeters: -50.0),
    };

    public delegate bool ColorSampleDelegate(
        float geographicU,
        float geographicV,
        out byte r,
        out byte g,
        out byte b
    );

    public static CombinedProbeReport ProbeStandardAssets(
        string elevationRawPath,
        string colorImagePath,
        ColorSampleDelegate colorSampler
    )
    {
        var combined = new CombinedProbeReport
        {
            Elevation = ProbeElevationRawFile(elevationRawPath, StandardSites),
            Color = ProbeColorMap(colorImagePath, colorSampler, StandardSites),
        };

        combined.RecommendedPolicy = BuildRecommendedPolicy(
            combined.Elevation,
            combined.Color,
            colorSampler,
            StandardSites
        );

        return combined;
    }

    static PlanetMapLayoutPolicy BuildRecommendedPolicy(
        AssetProbeReport elevation,
        AssetProbeReport color,
        ColorSampleDelegate colorSampler,
        IReadOnlyList<GeographicProbeSite> sites
    )
    {
        var policy = PlanetMapLayoutPolicy.CleanCanon;

        if (elevation.Layout == HorizontalLayout.Mirrored)
        {
            policy.ElevationMapRequiresHorizontalFlip = true;
        }

        if (color.Layout == HorizontalLayout.Mirrored)
        {
            policy.ColorMapRequiresHorizontalFlip = true;
        }
        else if (
            color.Layout == HorizontalLayout.Ambiguous &&
            colorSampler != null &&
            InferColorRequiresHorizontalFlipGebco(colorSampler, sites)
        )
        {
            policy.ColorMapRequiresHorizontalFlip = true;
        }

        if (colorSampler != null && InferColorRequiresVerticalFlip(colorSampler, sites))
        {
            policy.ColorMapRequiresVerticalFlip = true;
        }

        return policy;
    }

    public static bool InferColorRequiresVerticalFlip(
        ColorSampleDelegate sampler,
        IReadOnlyList<GeographicProbeSite> sites
    )
    {
        if (sampler == null)
        {
            return false;
        }

        int canonicalVMatches = 0;
        int flippedVMatches = 0;
        int sitesChecked = 0;

        for (int i = 0; i < sites.Count; i++)
        {
            GeographicProbeSite site = sites[i];

            if (System.Math.Abs(site.LatitudeDegrees) < 10.0)
            {
                continue;
            }

            double latRadians = site.LatitudeDegrees * (System.Math.PI / 180.0);
            double lonRadians = site.LongitudeDegrees * (System.Math.PI / 180.0);
            var direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRadians, lonRadians);
            PlanetGeographicCoords.DirectionToGeographicUv(
                direction,
                out double geographicU,
                out double geographicV
            );

            float u = (float)geographicU;
            float v = (float)geographicV;
            float flippedV = 1f - v;

            sampler(u, v, out byte canonicalR, out byte canonicalG, out byte canonicalB);
            sampler(u, flippedV, out byte flippedR, out byte flippedG, out byte flippedB);

            sitesChecked++;

            if (ColorMatchesSite(site, canonicalR, canonicalG, canonicalB))
            {
                canonicalVMatches++;
            }

            if (ColorMatchesSite(site, flippedR, flippedG, flippedB))
            {
                flippedVMatches++;
            }
        }

        if (sitesChecked == 0)
        {
            return false;
        }

        // Require a clear majority — ties keep Clean Canon (baked earth_geographic.png).
        return flippedVMatches > canonicalVMatches + 1;
    }

    /// <summary>
    /// When color layout is ambiguous (4/4), break the tie using GEBCO land/ocean vs RGB at U and 1-U.
    /// On a 4/4 six-site tie, score the three asymmetric truth probes (Andes / Indian Ocean / Cape);
    /// prefer <c>colorFlipU=false</c> when scores tie (equator anchors beat coastal false positives).
    /// </summary>
    public static bool InferColorRequiresHorizontalFlipGebco(
        ColorSampleDelegate sampler,
        IReadOnlyList<GeographicProbeSite> sites
    )
    {
        if (sampler == null)
        {
            return false;
        }

        if (!PlanetElevationCatalog.IsLoaded && !PlanetElevationCatalog.IsHighResLoaded)
        {
            return false;
        }

        int canonicalGebcoAgree = 0;
        int mirroredGebcoAgree = 0;
        int sitesChecked = 0;

        for (int i = 0; i < sites.Count; i++)
        {
            GeographicProbeSite site = sites[i];
            double gebcoMeters = PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(
                site.LatitudeDegrees,
                site.LongitudeDegrees
            );
            bool gebcoLand = gebcoMeters > 0.0;

            double latRadians = site.LatitudeDegrees * (System.Math.PI / 180.0);
            double lonRadians = site.LongitudeDegrees * (System.Math.PI / 180.0);
            var direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
                latRadians,
                lonRadians
            );
            PlanetGeographicCoords.DirectionToGeographicUv(
                direction,
                out double geographicU,
                out double geographicV
            );

            float u = (float)geographicU;
            float v = (float)geographicV;
            float mirroredU = 1f - u;

            sampler(u, v, out byte canonicalR, out byte canonicalG, out byte canonicalB);
            sampler(mirroredU, v, out byte mirroredR, out byte mirroredG, out byte mirroredB);

            sitesChecked++;

            if (ColorAgreesWithGebcoLandOcean(gebcoLand, canonicalR, canonicalG, canonicalB))
            {
                canonicalGebcoAgree++;
            }

            if (ColorAgreesWithGebcoLandOcean(gebcoLand, mirroredR, mirroredG, mirroredB))
            {
                mirroredGebcoAgree++;
            }
        }

        if (sitesChecked == 0)
        {
            return false;
        }

        if (mirroredGebcoAgree > canonicalGebcoAgree + 1)
        {
            return true;
        }

        if (canonicalGebcoAgree > mirroredGebcoAgree + 1)
        {
            return false;
        }

        int canonicalThreePointScore = ScoreThreePointTruthAgainstGebco(sampler, colorFlipU: false);
        int mirroredThreePointScore = ScoreThreePointTruthAgainstGebco(sampler, colorFlipU: true);
        return mirroredThreePointScore > canonicalThreePointScore;
    }

    public readonly struct ThreePointTruthSite
    {
        public ThreePointTruthSite(string label, double latitudeDegrees, double longitudeDegrees)
        {
            Label = label;
            LatitudeDegrees = latitudeDegrees;
            LongitudeDegrees = longitudeDegrees;
        }

        public string Label { get; }

        public double LatitudeDegrees { get; }

        public double LongitudeDegrees { get; }
    }

    public static readonly ThreePointTruthSite[] ThreePointTruthSites =
    {
        new ThreePointTruthSite("Andes_80W", 0.0, -80.0),
        new ThreePointTruthSite("IndianOcean_60E", 0.0, 60.0),
        new ThreePointTruthSite("Cape_80W", 28.4, -80.6),
    };

    public static int ScoreThreePointTruthAgainstGebco(
        ColorSampleDelegate sampler,
        bool colorFlipU
    )
    {
        if (sampler == null)
        {
            return 0;
        }

        if (!PlanetElevationCatalog.IsLoaded && !PlanetElevationCatalog.IsHighResLoaded)
        {
            return 0;
        }

        var policy = new PlanetMapLayoutPolicy
        {
            ColorMapRequiresHorizontalFlip = colorFlipU,
        };
        int score = 0;

        for (int i = 0; i < ThreePointTruthSites.Length; i++)
        {
            ThreePointTruthSite site = ThreePointTruthSites[i];
            double gebcoMeters = PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(
                site.LatitudeDegrees,
                site.LongitudeDegrees
            );
            bool gebcoLand = gebcoMeters > 0.0;

            double latRadians = site.LatitudeDegrees * (System.Math.PI / 180.0);
            double lonRadians = site.LongitudeDegrees * (System.Math.PI / 180.0);
            var direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
                latRadians,
                lonRadians
            );
            PlanetGeographicCoords.DirectionToGeographicUv(
                direction,
                out double geographicU,
                out double geographicV
            );

            float sampleU = policy.MapColorGeographicU((float)geographicU);
            float sampleV = (float)geographicV;
            sampler(sampleU, sampleV, out byte r, out byte g, out byte b);

            if (ColorAgreesWithGebcoLandOcean(gebcoLand, r, g, b))
            {
                score++;
            }
        }

        return score;
    }

    static bool ColorAgreesWithGebcoLandOcean(bool gebcoLand, byte r, byte g, byte b)
    {
        string colorClass = ClassifyColorLandOcean(r, g, b);

        if (gebcoLand)
        {
            return colorClass == "LAND";
        }

        return colorClass == "OCEAN";
    }

    public static AssetProbeReport ProbeElevationRawFile(
        string absolutePath,
        IReadOnlyList<GeographicProbeSite> sites
    )
    {
        var report = new AssetProbeReport
        {
            AssetPath = absolutePath,
            Layout = HorizontalLayout.Missing,
        };

        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            report.Summary = "File not found.";
            return report;
        }

        int width = PlanetElevationRawFormat.Width;
        int height = PlanetElevationRawFormat.Height;
        int canonicalMatches = 0;
        int mirroredMatches = 0;

        for (int i = 0; i < sites.Count; i++)
        {
            GeographicProbeSite site = sites[i];
            ResolvePixels(site.LatitudeDegrees, site.LongitudeDegrees, width, height,
                out double geographicU,
                out double geographicV,
                out int canonicalX,
                out int mirroredX,
                out int pixelY
            );

            double canonicalMeters = ReadRawMetersAtPixel(absolutePath, width, height, canonicalX, pixelY);
            double mirroredMeters = ReadRawMetersAtPixel(absolutePath, width, height, mirroredX, pixelY);

            report.Samples.Add(new SiteSample(
                site,
                geographicU,
                geographicV,
                canonicalX,
                mirroredX,
                pixelY,
                canonicalMeters,
                mirroredMeters,
                0,
                0,
                0,
                0,
                0,
                0,
                hasElevation: true,
                hasColor: false
            ));

            if (ElevationMatchesSite(site, canonicalMeters))
            {
                canonicalMatches++;
            }

            if (ElevationMatchesSite(site, mirroredMeters))
            {
                mirroredMatches++;
            }
        }

        report.SitesProbed = sites.Count;
        report.CanonicalMatches = canonicalMatches;
        report.MirroredMatches = mirroredMatches;
        report.Layout = ResolveLayout(canonicalMatches, mirroredMatches, sites.Count);
        report.Summary = BuildElevationSummary(report.Layout, canonicalMatches, mirroredMatches, sites.Count);
        return report;
    }

    public static AssetProbeReport ProbeColorMap(
        string absolutePath,
        ColorSampleDelegate sampler,
        IReadOnlyList<GeographicProbeSite> sites
    )
    {
        var report = new AssetProbeReport
        {
            AssetPath = absolutePath,
            Layout = HorizontalLayout.Missing,
        };

        if (sampler == null)
        {
            report.Summary = "Color sampler not available.";
            return report;
        }

        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            report.Summary = "File not found.";
            return report;
        }

        int canonicalMatches = 0;
        int mirroredMatches = 0;

        for (int i = 0; i < sites.Count; i++)
        {
            GeographicProbeSite site = sites[i];
            double latRadians = site.LatitudeDegrees * (Math.PI / 180.0);
            double lonRadians = site.LongitudeDegrees * (Math.PI / 180.0);
            var direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRadians, lonRadians);
            PlanetGeographicCoords.DirectionToGeographicUv(
                direction,
                out double geographicU,
                out double geographicV
            );

            float u = (float)geographicU;
            float v = (float)geographicV;
            float mirroredU = 1f - u;

            sampler(u, v, out byte canonicalR, out byte canonicalG, out byte canonicalB);
            sampler(mirroredU, v, out byte mirroredR, out byte mirroredG, out byte mirroredB);

            ResolvePixels(site.LatitudeDegrees, site.LongitudeDegrees,
                PlanetElevationRawFormat.Width,
                PlanetElevationRawFormat.Height,
                out _,
                out _,
                out int canonicalX,
                out int mirroredX,
                out int pixelY
            );

            report.Samples.Add(new SiteSample(
                site,
                geographicU,
                geographicV,
                canonicalX,
                mirroredX,
                pixelY,
                0.0,
                0.0,
                canonicalR,
                canonicalG,
                canonicalB,
                mirroredR,
                mirroredG,
                mirroredB,
                hasElevation: false,
                hasColor: true
            ));

            if (ColorMatchesSite(site, canonicalR, canonicalG, canonicalB))
            {
                canonicalMatches++;
            }

            if (ColorMatchesSite(site, mirroredR, mirroredG, mirroredB))
            {
                mirroredMatches++;
            }
        }

        report.SitesProbed = sites.Count;
        report.CanonicalMatches = canonicalMatches;
        report.MirroredMatches = mirroredMatches;
        report.Layout = ResolveLayout(canonicalMatches, mirroredMatches, sites.Count);
        report.Summary = BuildColorSummary(report.Layout, canonicalMatches, mirroredMatches, sites.Count);
        return report;
    }

    public static void ResolvePixels(
        double latitudeDegrees,
        double longitudeDegrees,
        int width,
        int height,
        out double geographicU,
        out double geographicV,
        out int canonicalPixelX,
        out int mirroredPixelX,
        out int pixelY
    )
    {
        PlanetElevationRawCoords.LatitudeLongitudeDegreesToContinuousPixel(
            latitudeDegrees,
            longitudeDegrees,
            out double continuousCanonicalX,
            out double continuousY
        );

        double latRadians = latitudeDegrees * (Math.PI / 180.0);
        double lonRadians = longitudeDegrees * (Math.PI / 180.0);
        var direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRadians, lonRadians);
        PlanetGeographicCoords.DirectionToGeographicUv(
            direction,
            out geographicU,
            out geographicV
        );

        double maxX = width - 1;
        double continuousMirroredX = (1.0 - geographicU) * maxX;

        canonicalPixelX = ClampPixelIndex((int)Math.Round(continuousCanonicalX), width);
        mirroredPixelX = ClampPixelIndex((int)Math.Round(continuousMirroredX), width);
        pixelY = ClampPixelIndex((int)Math.Round(continuousY), height);
    }

    public static double ReadRawMetersAtPixel(
        string absolutePath,
        int width,
        int height,
        int pixelX,
        int pixelY
    )
    {
        pixelX = ClampPixelIndex(pixelX, width);
        pixelY = ClampPixelIndex(pixelY, height);
        long offset = ((long)pixelY * width + pixelX) * PlanetElevationRawFormat.BytesPerSample;

        using var stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );

        if (stream.Length < offset + PlanetElevationRawFormat.BytesPerSample)
        {
            return double.NaN;
        }

        stream.Seek(offset, SeekOrigin.Begin);
        byte[] buffer = new byte[PlanetElevationRawFormat.BytesPerSample];

        if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
        {
            return double.NaN;
        }

        return BitConverter.ToInt16(buffer);
    }

    public static bool ElevationMatchesSite(GeographicProbeSite site, double elevationMeters)
    {
        if (!double.IsFinite(elevationMeters))
        {
            return false;
        }

        bool isLand = elevationMeters > 0.0;

        if (site.ExpectLand != isLand)
        {
            return false;
        }

        if (elevationMeters < site.MinElevationMeters)
        {
            return false;
        }

        if (elevationMeters > site.MaxElevationMeters)
        {
            return false;
        }

        return true;
    }

    public static bool ColorMatchesSite(GeographicProbeSite site, byte r, byte g, byte b)
    {
        string colorClass = ClassifyColorLandOcean(r, g, b);

        if (site.ExpectLand)
        {
            return colorClass == "LAND";
        }

        return colorClass == "OCEAN";
    }

    public static string ClassifyColorLandOcean(byte r, byte g, byte b)
    {
        float rf = r / 255f;
        float gf = g / 255f;
        float bf = b / 255f;
        bool looksLikeOcean = bf > gf + 0.04f && bf > 0.12f;
        bool looksLikeLand = gf > 0.12f && bf < gf * 0.85f;

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

    public static HorizontalLayout InferHorizontalLayout(
        int canonicalMatches,
        int mirroredMatches,
        int totalSites
    )
    {
        return ResolveLayout(canonicalMatches, mirroredMatches, totalSites);
    }

    static HorizontalLayout ResolveLayout(int canonicalMatches, int mirroredMatches, int totalSites)
    {
        if (totalSites <= 0)
        {
            return HorizontalLayout.Missing;
        }

        if (canonicalMatches > mirroredMatches && canonicalMatches >= totalSites - 1)
        {
            return HorizontalLayout.Canonical;
        }

        if (mirroredMatches > canonicalMatches && mirroredMatches >= totalSites - 1)
        {
            return HorizontalLayout.Mirrored;
        }

        if (canonicalMatches == mirroredMatches && canonicalMatches == totalSites)
        {
            return HorizontalLayout.Ambiguous;
        }

        return HorizontalLayout.Ambiguous;
    }

    static string BuildElevationSummary(
        HorizontalLayout layout,
        int canonicalMatches,
        int mirroredMatches,
        int totalSites
    )
    {
        return
            "Elevation layout=" + layout +
            " (canonical " + canonicalMatches + "/" + totalSites +
            ", mirrored " + mirroredMatches + "/" + totalSites + "). " +
            LayoutHint(layout, "elevation");
    }

    static string BuildColorSummary(
        HorizontalLayout layout,
        int canonicalMatches,
        int mirroredMatches,
        int totalSites
    )
    {
        return
            "Color layout=" + layout +
            " (canonical U " + canonicalMatches + "/" + totalSites +
            ", mirrored 1-U " + mirroredMatches + "/" + totalSites + "). " +
            LayoutHint(layout, "color");
    }

    static string LayoutHint(HorizontalLayout layout, string assetKind)
    {
        switch (layout)
        {
            case HorizontalLayout.Canonical:
                return "On-disk " + assetKind + " matches Clean Canon — keep flip=false.";
            case HorizontalLayout.Mirrored:
                return "On-disk " + assetKind + " is E/W mirrored — set flip=true in PlanetMapLayoutPolicy OR re-bake file.";
            case HorizontalLayout.Ambiguous:
                return "Inconclusive — inspect per-site rows or re-bake " + assetKind + " asset.";
            default:
                return "Asset missing.";
        }
    }

    static int ClampPixelIndex(int index, int size)
    {
        if (index < 0)
        {
            return 0;
        }

        if (index >= size)
        {
            return size - 1;
        }

        return index;
    }

    static string FormatRgb(byte r, byte g, byte b)
    {
        return "(" +
            (r / 255f).ToString("F2") + "," +
            (g / 255f).ToString("F2") + "," +
            (b / 255f).ToString("F2") + ")";
    }
}
