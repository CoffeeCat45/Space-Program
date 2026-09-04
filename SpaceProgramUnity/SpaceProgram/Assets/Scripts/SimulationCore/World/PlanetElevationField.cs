using System;
using System.IO;

/// <summary>
/// Read-only GEBCO elevation grid (5400×2700, meters above sea level).
/// Loaded once from baked <see cref="PlanetElevationBinaryFormat"/> data.
/// </summary>
public sealed class PlanetElevationField
{
    readonly float[] elevationsMeters;
    readonly int width;
    readonly int height;

    PlanetElevationField(float[] elevationsMeters, int width, int height)
    {
        this.elevationsMeters = elevationsMeters;
        this.width = width;
        this.height = height;
    }

    public int Width => width;

    public int Height => height;

    public float[] ElevationsMeters => elevationsMeters;

    public static PlanetElevationField LoadFromStream(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        PlanetElevationBinaryFormat.Header header = PlanetElevationBinaryFormat.ReadHeader(stream);
        float[] payload = PlanetElevationBinaryFormat.ReadPayload(stream, header);
        return new PlanetElevationField(payload, header.Width, header.Height);
    }

    public static PlanetElevationField LoadFromFile(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new ArgumentException("Path is required.", nameof(absolutePath));
        }

        using FileStream stream = File.OpenRead(absolutePath);
        return LoadFromStream(stream);
    }

    /// <summary>
    /// Real GEBCO meters at the unit-sphere direction (bilinear, deterministic).
    /// Uses simulation longitude (atan2(x, z)), not texture UV longitude.
    /// </summary>
    public double GetElevationAtDirection(SimVector3 direction)
    {
        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            direction,
            out double latitudeRadians,
            out double longitudeRadians
        );
        return SampleLatitudeLongitudeRadians(latitudeRadians, longitudeRadians);
    }

    public double SampleGeographicUv(double geographicU, double geographicV)
    {
        return PlanetElevationSampler.SampleBilinearMeters(
            elevationsMeters,
            width,
            height,
            geographicU,
            geographicV
        );
    }

    public double SampleLatitudeLongitudeRadians(double latitudeRadians, double longitudeRadians)
    {
        double u = PlanetGeographicCoords.LongitudeRadiansToGeographicU(longitudeRadians);
        double v = PlanetGeographicCoords.LatitudeRadiansToGeographicV(latitudeRadians);
        return SampleGeographicUv(u, v);
    }
}
