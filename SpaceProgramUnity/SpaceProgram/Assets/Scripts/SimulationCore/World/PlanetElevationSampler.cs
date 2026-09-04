using System;

/// <summary>
/// Deterministic CPU bilinear sampling for lockstep multiplayer.
/// Uses double precision; U wraps, V clamps at poles.
/// </summary>
public static class PlanetElevationSampler
{
    public static double SampleBilinearMeters(
        float[] elevationsMeters,
        int width,
        int height,
        double geographicU,
        double geographicV
    )
    {
        if (elevationsMeters == null || width <= 0 || height <= 0)
        {
            return 0.0;
        }

        double u = PlanetGeographicCoords.WrapGeographicU(geographicU);
        double v = PlanetGeographicCoords.ClampGeographicV(geographicV);

        if (width == 1 && height == 1)
        {
            return elevationsMeters[0];
        }

        double maxX = width - 1;
        double maxY = height - 1;
        double sampleX = u * maxX;
        double sampleY = v * maxY;

        int x0 = (int)Math.Floor(sampleX);
        int y0 = (int)Math.Floor(sampleY);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        double tx = sampleX - x0;
        double ty = sampleY - y0;

        x0 = WrapIndex(x0, width);
        x1 = WrapIndex(x1, width);
        y0 = ClampIndex(y0, height);
        y1 = ClampIndex(y1, height);

        double h00 = elevationsMeters[(y0 * width) + x0];
        double h10 = elevationsMeters[(y0 * width) + x1];
        double h01 = elevationsMeters[(y1 * width) + x0];
        double h11 = elevationsMeters[(y1 * width) + x1];

        double hx0 = h00 + ((h10 - h00) * tx);
        double hx1 = h01 + ((h11 - h01) * tx);
        return hx0 + ((hx1 - hx0) * ty);
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
