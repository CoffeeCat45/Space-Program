using System;
using System.IO;

/// <summary>
/// Process-wide elevation provider for SimulationCore and UnityAdapter.
/// Legacy SPEL .bin and high-res Int16 .raw can coexist; switch via <see cref="UseHighResElevation"/>.
/// </summary>
public static class PlanetElevationCatalog
{
    static PlanetElevationField activeField;
    static PlanetElevationMmapField highResField;
    static PlanetElevationMmapField earthHighResField;
    static PlanetElevationMmapField moonHighResField;
    static bool useHighResElevation;
    static bool earthLodPyramidActive;
    static short[] coarseBuffer;
    static short[] midBuffer;
    static short[] highMidBuffer;
    static string lodBuffersDirectory;
    static bool coarseLoadAttempted;
    static bool midLoadAttempted;
    static bool highMidLoadAttempted;
    static readonly object midLoadLock = new object();
    static readonly object highMidLoadLock = new object();

    /// <summary>
    /// Within this surface distance of <see cref="LaunchSiteConfig.Active"/>, LOD mesh builds
    /// always sample full-resolution GEBCO mmap so pad flatten and neighbors share heights.
    /// </summary>
    public const double LaunchSiteHeightParityRadiusMeters = 15000.0;

    public static bool IsLoaded => activeField != null;

    public static bool IsHighResLoaded => highResField != null;

    public static bool IsCoarseLodLoaded => coarseBuffer != null;

    public static bool IsMidLodLoaded => midBuffer != null;

    public static bool IsHighMidLodLoaded => highMidBuffer != null;

    /// <summary>
    /// When true, sampling APIs use mmap Int16 grid (<see cref="GetHeightBilinearNew"/> path).
    /// Legacy .bin remains available when false.
    /// </summary>
    public static bool UseHighResElevation
    {
        get => useHighResElevation;
        set
        {
            if (value && highResField == null)
            {
                throw new InvalidOperationException(
                    "High-res elevation is not loaded. Call LoadHighResFromFile first."
                );
            }

            useHighResElevation = value;
        }
    }

    public static PlanetElevationField ActiveField
    {
        get
        {
            if (activeField == null)
            {
                throw new InvalidOperationException(
                    "Planet elevation is not loaded. Bake and load " +
                    PlanetSurfaceConstants.ElevationBinaryFileName +
                    " before sampling terrain."
                );
            }

            return activeField;
        }
    }

    public static PlanetElevationMmapField HighResField
    {
        get
        {
            if (highResField == null)
            {
                throw new InvalidOperationException(
                    "High-res elevation is not loaded. Load " +
                    PlanetSurfaceConstants.ElevationRawFileName +
                    " before sampling."
                );
            }

            return highResField;
        }
    }

    public static void LoadFromFile(string absolutePath)
    {
        activeField = PlanetElevationField.LoadFromFile(absolutePath);
    }

    public static void LoadFromStream(Stream stream)
    {
        activeField = PlanetElevationField.LoadFromStream(stream);
    }

    public static void LoadHighResFromFile(string absolutePath)
    {
        earthHighResField?.Dispose();
        earthHighResField = PlanetElevationMmapField.OpenFile(absolutePath);
        highResField = earthHighResField;
        earthLodPyramidActive = true;

        string directory = Path.GetDirectoryName(absolutePath);

        if (!string.IsNullOrEmpty(directory))
        {
            TryLoadLodBuffersFromDirectory(directory);
        }
    }

    /// <summary>
    /// Arbitrary Int16 grid (Moon LDEM). Does not load Earth LOD pyramid buffers.
    /// </summary>
    public static void LoadHighResGridFromFile(
        string absolutePath,
        int gridWidth,
        int gridHeight
    )
    {
        moonHighResField?.Dispose();
        moonHighResField = PlanetElevationMmapField.OpenFile(
            absolutePath,
            gridWidth,
            gridHeight
        );
        highResField = moonHighResField;
        earthLodPyramidActive = false;
        useHighResElevation = true;
    }

    public static void TryLoadLodBuffersFromDirectory(string planetDirectoryAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(planetDirectoryAbsolutePath))
        {
            return;
        }

        lodBuffersDirectory = planetDirectoryAbsolutePath;
        TryLoadCoarseBuffer(Path.Combine(
            planetDirectoryAbsolutePath,
            PlanetElevationLodRawFormat.CoarseFileName
        ));
    }

    public static bool TryLoadCoarseBuffer(string absolutePath)
    {
        if (coarseLoadAttempted)
        {
            return coarseBuffer != null;
        }

        coarseLoadAttempted = true;
        coarseBuffer = TryReadInt16GridFile(
            absolutePath,
            PlanetElevationLodRawFormat.CoarseWidth,
            PlanetElevationLodRawFormat.CoarseHeight,
            PlanetElevationLodRawFormat.CoarseByteCount
        );
        return coarseBuffer != null;
    }

    static void EnsureMidBufferLoaded()
    {
        if (midBuffer != null || midLoadAttempted)
        {
            return;
        }

        lock (midLoadLock)
        {
            if (midBuffer != null || midLoadAttempted)
            {
                return;
            }

            midLoadAttempted = true;

            if (string.IsNullOrWhiteSpace(lodBuffersDirectory))
            {
                return;
            }

            midBuffer = TryReadInt16GridFile(
                Path.Combine(lodBuffersDirectory, PlanetElevationLodRawFormat.MidFileName),
                PlanetElevationLodRawFormat.MidWidth,
                PlanetElevationLodRawFormat.MidHeight,
                PlanetElevationLodRawFormat.MidByteCount
            );
        }
    }

    static void EnsureHighMidBufferLoaded()
    {
        if (highMidBuffer != null || highMidLoadAttempted)
        {
            return;
        }

        lock (highMidLoadLock)
        {
            if (highMidBuffer != null || highMidLoadAttempted)
            {
                return;
            }

            highMidLoadAttempted = true;

            if (string.IsNullOrWhiteSpace(lodBuffersDirectory))
            {
                return;
            }

            highMidBuffer = TryReadInt16GridFile(
                Path.Combine(lodBuffersDirectory, PlanetElevationLodRawFormat.HighMidFileName),
                PlanetElevationLodRawFormat.HighMidWidth,
                PlanetElevationLodRawFormat.HighMidHeight,
                PlanetElevationLodRawFormat.HighMidByteCount
            );
        }
    }

    static short[] TryReadInt16GridFile(
        string absolutePath,
        int expectedWidth,
        int expectedHeight,
        long expectedByteCount
    )
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return null;
        }

        long fileLength = new FileInfo(absolutePath).Length;

        if (fileLength != expectedByteCount)
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(absolutePath);
        int sampleCount = expectedWidth * expectedHeight;
        var samples = new short[sampleCount];
        Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
        return samples;
    }

    /// <summary>
    /// LOD-aware elevation sample (bilinear meters). Face UV uses cube-sphere canon.
    /// LOD 0-2: coarse RAM, LOD 3: mid RAM (lazy), LOD 4: high-mid RAM (lazy), LOD 5+: mmap full.
    /// Missing LOD files fall back to the next finer available source, ending at mmap.
    /// </summary>
    public static double SampleForLod(int faceIndex, double faceU, double faceV, int lod)
    {
        SimVector3 direction = PlanetCubeSphereCoords.FaceUvToUnitDirection(faceIndex, faceU, faceV);
        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            direction,
            out double latitudeRadians,
            out double longitudeRadians
        );
        return SampleForLod(latitudeRadians, longitudeRadians, lod);
    }

    /// <summary>
    /// Nearest-neighbor Int16 cell in meters for the active LOD source grid.
    /// </summary>
    public static short SampleForLodNearest(
        int faceIndex,
        double faceU,
        double faceV,
        int lod
    )
    {
        SimVector3 direction = PlanetCubeSphereCoords.FaceUvToUnitDirection(faceIndex, faceU, faceV);
        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            direction,
            out double latitudeRadians,
            out double longitudeRadians
        );
        return SampleForLodNearest(latitudeRadians, longitudeRadians, lod);
    }

    public static double SampleForLod(
        double latitudeRadians,
        double longitudeRadians,
        int lod
    )
    {
        if (TrySampleFullResInLaunchSiteParityZone(
                latitudeRadians,
                longitudeRadians,
                out double parityElevationMeters
            ))
        {
            return parityElevationMeters;
        }

        if (earthLodPyramidActive && lod <= 2 && coarseBuffer != null)
        {
            return PlanetElevationInt16GridSampler.SampleBilinearMeters(
                coarseBuffer,
                PlanetElevationLodRawFormat.CoarseWidth,
                PlanetElevationLodRawFormat.CoarseHeight,
                latitudeRadians,
                longitudeRadians
            );
        }

        if (earthLodPyramidActive && lod == 3)
        {
            EnsureMidBufferLoaded();

            if (midBuffer != null)
            {
                return PlanetElevationInt16GridSampler.SampleBilinearMeters(
                    midBuffer,
                    PlanetElevationLodRawFormat.MidWidth,
                    PlanetElevationLodRawFormat.MidHeight,
                    latitudeRadians,
                    longitudeRadians
                );
            }
        }

        if (earthLodPyramidActive && lod == 4)
        {
            EnsureHighMidBufferLoaded();

            if (highMidBuffer != null)
            {
                return PlanetElevationInt16GridSampler.SampleBilinearMeters(
                    highMidBuffer,
                    PlanetElevationLodRawFormat.HighMidWidth,
                    PlanetElevationLodRawFormat.HighMidHeight,
                    latitudeRadians,
                    longitudeRadians
                );
            }
        }

        if (highResField != null)
        {
            return PlanetElevationHighResSampler.GetHeightBilinearNew(
                highResField,
                latitudeRadians,
                longitudeRadians
            );
        }

        return 0.0;
    }

    public static short SampleForLodNearest(
        double latitudeRadians,
        double longitudeRadians,
        int lod
    )
    {
        if (TrySampleFullResInLaunchSiteParityZone(
                latitudeRadians,
                longitudeRadians,
                out double parityElevationMeters
            ))
        {
            return (short)Math.Round(
                Math.Max(short.MinValue, Math.Min(short.MaxValue, parityElevationMeters))
            );
        }

        if (earthLodPyramidActive && lod <= 2 && coarseBuffer != null)
        {
            return PlanetElevationInt16GridSampler.SampleNearestMeters(
                coarseBuffer,
                PlanetElevationLodRawFormat.CoarseWidth,
                PlanetElevationLodRawFormat.CoarseHeight,
                latitudeRadians,
                longitudeRadians
            );
        }

        if (earthLodPyramidActive && lod == 3)
        {
            EnsureMidBufferLoaded();

            if (midBuffer != null)
            {
                return PlanetElevationInt16GridSampler.SampleNearestMeters(
                    midBuffer,
                    PlanetElevationLodRawFormat.MidWidth,
                    PlanetElevationLodRawFormat.MidHeight,
                    latitudeRadians,
                    longitudeRadians
                );
            }
        }

        if (earthLodPyramidActive && lod == 4)
        {
            EnsureHighMidBufferLoaded();

            if (highMidBuffer != null)
            {
                return PlanetElevationInt16GridSampler.SampleNearestMeters(
                    highMidBuffer,
                    PlanetElevationLodRawFormat.HighMidWidth,
                    PlanetElevationLodRawFormat.HighMidHeight,
                    latitudeRadians,
                    longitudeRadians
                );
            }
        }

        if (highResField != null)
        {
            PlanetElevationRawCoords.LatitudeLongitudeRadiansToContinuousPixel(
                latitudeRadians,
                longitudeRadians,
                out double pixelX,
                out double pixelY
            );
            int x = (int)Math.Round(pixelX);
            int y = (int)Math.Round(pixelY);
            return highResField.ReadSampleMeters(x, y);
        }

        return 0;
    }

    public static void Unload()
    {
        activeField = null;
        useHighResElevation = false;
    }

    public static void UnloadHighRes()
    {
        if (highResField != earthHighResField && highResField != moonHighResField)
        {
            highResField?.Dispose();
        }

        earthHighResField?.Dispose();
        moonHighResField?.Dispose();
        earthHighResField = null;
        moonHighResField = null;
        highResField = null;
        useHighResElevation = false;
        earthLodPyramidActive = false;
    }

    public static bool TryActivateCachedHighRes(bool moon)
    {
        PlanetElevationMmapField cached = moon ? moonHighResField : earthHighResField;

        if (cached == null)
        {
            return false;
        }

        highResField = cached;
        useHighResElevation = true;
        earthLodPyramidActive = !moon;
        return true;
    }

    public static void UnloadAll()
    {
        Unload();
        UnloadHighRes();
        UnloadLodBuffers();
    }

    public static void UnloadLodBuffers()
    {
        coarseBuffer = null;
        midBuffer = null;
        highMidBuffer = null;
        lodBuffersDirectory = null;
        coarseLoadAttempted = false;
        midLoadAttempted = false;
        highMidLoadAttempted = false;
    }

    /// <summary>Real GEBCO meters at the unit-sphere direction.</summary>
    public static double GetElevationAtDirection(SimVector3 direction)
    {
        if (!TryGetElevationAtDirection(direction, out double elevationMeters))
        {
            throw new InvalidOperationException(
                "Planet elevation is not loaded. Bake and load " +
                PlanetSurfaceConstants.ElevationBinaryFileName +
                " or " +
                PlanetSurfaceConstants.ElevationRawFileName +
                " before sampling terrain."
            );
        }

        return elevationMeters;
    }

    public static bool TryGetElevationAtDirection(SimVector3 direction, out double elevationMeters)
    {
        elevationMeters = 0.0;

        if (direction.LengthSquared() <= 1e-24)
        {
            return false;
        }

        SimVector3 unit = direction.Normalize();

        if (highResField != null && (useHighResElevation || activeField == null))
        {
            elevationMeters = GetHeightBilinearNew(unit);
            return true;
        }

        if (activeField != null)
        {
            elevationMeters = activeField.GetElevationAtDirection(unit);
            return true;
        }

        if (highResField != null)
        {
            elevationMeters = GetHeightBilinearNew(unit);
            return true;
        }

        return false;
    }

    /// <summary>
    /// High-res mmap bilinear sample. Available without toggling <see cref="UseHighResElevation"/>.
    /// </summary>
    public static double GetHeightBilinearNew(SimVector3 direction)
    {
        return PlanetElevationHighResSampler.GetHeightBilinearNew(HighResField, direction);
    }

    public static double GetHeightBilinearNew(double latitudeRadians, double longitudeRadians)
    {
        return PlanetElevationHighResSampler.GetHeightBilinearNew(
            HighResField,
            latitudeRadians,
            longitudeRadians
        );
    }

    /// <summary>Legacy SPEL .bin bilinear sample (debug / A-B compare).</summary>
    public static double GetHeightBilinearLegacy(SimVector3 direction)
    {
        return ActiveField.GetElevationAtDirection(direction);
    }

    public static double GetElevationAtLatitudeLongitudeDegrees(
        double latitudeDegrees,
        double longitudeDegrees
    )
    {
        double latitudeRadians = latitudeDegrees * (Math.PI / 180.0);
        double longitudeRadians = longitudeDegrees * (Math.PI / 180.0);

        if (highResField != null && (useHighResElevation || activeField == null))
        {
            return GetHeightBilinearNew(latitudeRadians, longitudeRadians);
        }

        if (activeField != null)
        {
            return activeField.SampleLatitudeLongitudeRadians(latitudeRadians, longitudeRadians);
        }

        if (highResField != null)
        {
            return GetHeightBilinearNew(latitudeRadians, longitudeRadians);
        }

        throw new InvalidOperationException(
            "Planet elevation is not loaded. Bake and load " +
            PlanetSurfaceConstants.ElevationBinaryFileName +
            " or " +
            PlanetSurfaceConstants.ElevationRawFileName +
            " before sampling terrain."
        );
    }

    /// <summary>PQS mesh vertex: face UV → Philip Nowell → GEBCO.</summary>
    public static double GetElevationAtFaceUv(int faceIndex, double u, double v)
    {
        if (useHighResElevation)
        {
            SimVector3 direction = PlanetCubeSphereCoords.FaceUvToUnitDirection(faceIndex, u, v);
            return GetHeightBilinearNew(direction);
        }

        return PlanetCubeSphereCoords.SampleElevationAtFaceUv(ActiveField, faceIndex, u, v);
    }

    /// <summary>
    /// Lat/lon → cube-face assignment + GEBCO at the geographic point.
    /// </summary>
    public static double GetElevationAtLatitudeLongitudeRadiansWithCubeFaceAssignment(
        double latitudeRadians,
        double longitudeRadians,
        out int faceIndex,
        out double faceU,
        out double faceV
    )
    {
        if (useHighResElevation)
        {
            PlanetCubeSphereCoords.LatitudeLongitudeRadiansToFaceUv(
                latitudeRadians,
                longitudeRadians,
                out faceIndex,
                out faceU,
                out faceV
            );
            SimVector3 direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
                latitudeRadians,
                longitudeRadians
            );
            return GetHeightBilinearNew(direction);
        }

        return PlanetCubeSphereCoords.SampleElevationWithCubeFaceAssignment(
            ActiveField,
            latitudeRadians,
            longitudeRadians,
            out faceIndex,
            out faceU,
            out faceV
        );
    }

    public static bool IsWithinLaunchSiteHeightParityZone(
        double latitudeRadians,
        double longitudeRadians
    )
    {
        SimVector3 sampleDirection = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            latitudeRadians,
            longitudeRadians
        );
        SimVector3 siteDirection = LaunchSiteConfig.GetActiveSurfaceNormal();
        double dot = SimVector3.Dot(sampleDirection, siteDirection);

        if (dot > 1.0)
        {
            dot = 1.0;
        }
        else if (dot < -1.0)
        {
            dot = -1.0;
        }

        double angleRadians = Math.Acos(dot);
        double surfaceDistanceMeters = angleRadians * CelestialBody.Earth.Radius;
        return surfaceDistanceMeters <= LaunchSiteHeightParityRadiusMeters;
    }

    static bool TrySampleFullResInLaunchSiteParityZone(
        double latitudeRadians,
        double longitudeRadians,
        out double elevationMeters
    )
    {
        elevationMeters = 0.0;

        if (
            !earthLodPyramidActive ||
            highResField == null ||
            !IsWithinLaunchSiteHeightParityZone(latitudeRadians, longitudeRadians)
        )
        {
            return false;
        }

        elevationMeters = GetHeightBilinearNew(latitudeRadians, longitudeRadians);
        return true;
    }
}
