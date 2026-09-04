using System;

/// <summary>
/// Canonical equirectangular geographic coordinates for NASA/GEBCO assets.
/// Y = north pole, +longitude east, Greenwich at texture U = 0.5.
/// V = 0 south pole (bottom of image), V = 1 north pole (top of image).
/// </summary>
public static class PlanetGeographicCoords
{
    public const int SimulationElevationWidth = 5400;
    public const int SimulationElevationHeight = 2700;

    public static SimVector3 DirectionFromLatitudeLongitude(
        double latitudeRadians,
        double longitudeRadians
    )
    {
        double cosLat = Math.Cos(latitudeRadians);
        double sinLat = Math.Sin(latitudeRadians);
        double sinLon = Math.Sin(longitudeRadians);
        double cosLon = Math.Cos(longitudeRadians);

        // lon = atan2(x, z) on the unit sphere; +X is east.
        return new SimVector3(
            cosLat * sinLon,
            sinLat,
            cosLat * cosLon
        ).Normalize();
    }

    public static void DirectionToLatitudeLongitude(
        SimVector3 direction,
        out double latitudeRadians,
        out double longitudeRadians
    )
    {
        direction = direction.Normalize();
        latitudeRadians = Math.Asin(ClampUnit(direction.y));
        longitudeRadians = Math.Atan2(direction.x, direction.z);
    }

    /// <summary>
    /// Canonical equirectangular UV for simulation, GEBCO, NASA map, and flight terrain.
    /// </summary>
    public static void DirectionToGeographicUv(
        SimVector3 direction,
        out double u,
        out double v
    )
    {
        DirectionToSimulationGeographicUv(direction, out u, out v);
    }

    /// <summary>Alias kept for call sites that name simulation explicitly.</summary>
    public static void DirectionToSimulationGeographicUv(
        SimVector3 direction,
        out double u,
        out double v
    )
    {
        DirectionToLatitudeLongitude(direction, out double latitudeRadians, out double longitudeRadians);
        u = LongitudeRadiansToGeographicU(longitudeRadians);
        v = LatitudeRadiansToGeographicV(latitudeRadians);
    }

    public static double LongitudeRadiansToGeographicU(double longitudeRadians)
    {
        double u = longitudeRadians / (2.0 * Math.PI) + 0.5;

        if (u < 0.0)
        {
            u += 1.0;
        }
        else if (u >= 1.0)
        {
            u -= 1.0;
        }

        return u;
    }

    /// <summary>South pole = 0, north pole = 1 (image row order, north at top).</summary>
    public static double LatitudeRadiansToGeographicV(double latitudeRadians)
    {
        return latitudeRadians / Math.PI + 0.5;
    }

    /// <summary>
    /// RSS/KSP EarthColor/EarthHeight after row-flip at load: north pole V=0.
    /// Use only for legacy RSS DDS until NASA color replaces them.
    /// </summary>
    public static double MapGeographicVToRssEarthTextureV(double geographicV)
    {
        return 1.0 - geographicV;
    }

    /// <summary>
    /// KSP PQS horizontal flip + ¼-width west shift (baked into DDS columns at load).
    /// </summary>
    public static double MapGeographicUToRssEarthTextureU(double geographicU)
    {
        return WrapGeographicU(0.75 - geographicU);
    }

    public static void GeographicUvToLatitudeLongitude(
        double geographicU,
        double geographicV,
        out double latitudeRadians,
        out double longitudeRadians
    )
    {
        SimulationGeographicUvToLatitudeLongitude(
            geographicU,
            geographicV,
            out latitudeRadians,
            out longitudeRadians
        );
    }

    public static void SimulationGeographicUvToLatitudeLongitude(
        double geographicU,
        double geographicV,
        out double latitudeRadians,
        out double longitudeRadians
    )
    {
        longitudeRadians = (WrapGeographicU(geographicU) - 0.5) * (2.0 * Math.PI);
        latitudeRadians = (ClampGeographicV(geographicV) - 0.5) * Math.PI;
    }

    public static double WrapGeographicU(double geographicU)
    {
        double u = geographicU % 1.0;

        if (u < 0.0)
        {
            u += 1.0;
        }

        return u;
    }

    public static double ClampGeographicV(double geographicV)
    {
        if (geographicV < 0.0)
        {
            return 0.0;
        }

        if (geographicV > 1.0)
        {
            return 1.0;
        }

        return geographicV;
    }

    static double ClampUnit(double value)
    {
        if (value < -1.0)
        {
            return -1.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}
