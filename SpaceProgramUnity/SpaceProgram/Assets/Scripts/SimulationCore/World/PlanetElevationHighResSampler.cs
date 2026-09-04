using System;

/// <summary>
/// Bilinear elevation sampling for memory-mapped Int16 GEBCO grid (parallel to legacy sampler).
/// </summary>
public static class PlanetElevationHighResSampler
{
    public static double GetHeightBilinearNew(
        PlanetElevationMmapField field,
        double latitudeRadians,
        double longitudeRadians
    )
    {
        return GetHeightBilinearOnGrid(field, latitudeRadians, longitudeRadians);
    }

    public static double GetHeightBilinearOnGrid(
        PlanetElevationMmapField field,
        double latitudeRadians,
        double longitudeRadians
    )
    {
        if (field == null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        PlanetElevationRawCoords.LatitudeLongitudeRadiansToContinuousPixel(
            latitudeRadians,
            longitudeRadians,
            field.Width,
            field.Height,
            out double sampleX,
            out double sampleY
        );
        return SampleBilinearMetersAtPixel(field, sampleX, sampleY);
    }

    public static double GetHeightBilinearNew(
        PlanetElevationMmapField field,
        SimVector3 direction
    )
    {
        if (field == null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            direction,
            out double latitudeRadians,
            out double longitudeRadians
        );
        return GetHeightBilinearNew(field, latitudeRadians, longitudeRadians);
    }

    public static double SampleBilinearMetersAtPixel(
        PlanetElevationMmapField field,
        double pixelX,
        double pixelY
    )
    {
        if (field == null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        int width = field.Width;
        int height = field.Height;

        if (width <= 0 || height <= 0)
        {
            return 0.0;
        }

        if (width == 1 && height == 1)
        {
            return field.ReadSampleMeters(0, 0);
        }

        double maxX = width - 1;
        double maxY = height - 1;
        pixelX = WrapPixelX(pixelX, width);
        pixelY = ClampPixelY(pixelY, height);

        int x0 = (int)Math.Floor(pixelX);
        int y0 = (int)Math.Floor(pixelY);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        double tx = pixelX - x0;
        double ty = pixelY - y0;

        x0 = WrapIndex(x0, width);
        x1 = WrapIndex(x1, width);
        y0 = ClampIndex(y0, height);
        y1 = ClampIndex(y1, height);

        double h00 = field.ReadSampleMeters(x0, y0);
        double h10 = field.ReadSampleMeters(x1, y0);
        double h01 = field.ReadSampleMeters(x0, y1);
        double h11 = field.ReadSampleMeters(x1, y1);

        double hx0 = h00 + ((h10 - h00) * tx);
        double hx1 = h01 + ((h11 - h01) * tx);
        return hx0 + ((hx1 - hx0) * ty);
    }

    static double WrapPixelX(double pixelX, int width)
    {
        double maxX = width - 1;

        if (maxX <= 0.0)
        {
            return 0.0;
        }

        double wrapped = pixelX % (width);

        if (wrapped < 0.0)
        {
            wrapped += width;
        }

        if (wrapped > maxX)
        {
            wrapped = maxX;
        }

        return wrapped;
    }

    static double ClampPixelY(double pixelY, int height)
    {
        double maxY = height - 1;

        if (pixelY < 0.0)
        {
            return 0.0;
        }

        if (pixelY > maxY)
        {
            return maxY;
        }

        return pixelY;
    }

    static int WrapIndex(int index, int size)
    {
        int wrapped = index % size;

        if (wrapped < 0)
        {
            wrapped += size;
        }

        return wrapped;
    }

    static int ClampIndex(int index, int size)
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
}
