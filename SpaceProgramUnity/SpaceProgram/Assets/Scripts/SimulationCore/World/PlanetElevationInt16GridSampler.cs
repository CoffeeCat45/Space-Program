using System;

/// <summary>
/// Bilinear elevation sampling from a fully loaded Int16 grid in RAM.
/// </summary>
public static class PlanetElevationInt16GridSampler
{
    public static double SampleBilinearMeters(
        short[] samples,
        int width,
        int height,
        double latitudeRadians,
        double longitudeRadians
    )
    {
        if (samples == null)
        {
            throw new ArgumentNullException(nameof(samples));
        }

        PlanetElevationRawCoords.LatitudeLongitudeRadiansToContinuousPixel(
            latitudeRadians,
            longitudeRadians,
            width,
            height,
            out double pixelX,
            out double pixelY
        );
        return SampleBilinearMetersAtPixel(samples, width, height, pixelX, pixelY);
    }

    public static double SampleBilinearMetersAtPixel(
        short[] samples,
        int width,
        int height,
        double pixelX,
        double pixelY
    )
    {
        if (samples == null)
        {
            throw new ArgumentNullException(nameof(samples));
        }

        if (width <= 0 || height <= 0)
        {
            return 0.0;
        }

        if (width == 1 && height == 1)
        {
            return samples[0];
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

        double h00 = samples[(y0 * width) + x0];
        double h10 = samples[(y0 * width) + x1];
        double h01 = samples[(y1 * width) + x0];
        double h11 = samples[(y1 * width) + x1];

        double hx0 = h00 + ((h10 - h00) * tx);
        double hx1 = h01 + ((h11 - h01) * tx);
        return hx0 + ((hx1 - hx0) * ty);
    }

    public static short SampleNearestMeters(
        short[] samples,
        int width,
        int height,
        double latitudeRadians,
        double longitudeRadians
    )
    {
        PlanetElevationRawCoords.LatitudeLongitudeRadiansToContinuousPixel(
            latitudeRadians,
            longitudeRadians,
            width,
            height,
            out double pixelX,
            out double pixelY
        );

        int x = WrapIndex((int)Math.Round(pixelX), width);
        int y = ClampIndex((int)Math.Round(pixelY), height);
        return samples[(y * width) + x];
    }

    static double WrapPixelX(double pixelX, int width)
    {
        double maxX = width - 1;

        if (maxX <= 0.0)
        {
            return 0.0;
        }

        double wrapped = pixelX % width;

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
