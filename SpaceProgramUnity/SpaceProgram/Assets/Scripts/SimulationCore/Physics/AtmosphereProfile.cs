using System;

class AtmosphereProfile
{
    public double surfaceDensity;
    public double scaleHeight;
    public double topAltitude;

    public AtmosphereProfile(
        double surfaceDensity,
        double scaleHeight,
        double topAltitude
    )
    {
        this.surfaceDensity = surfaceDensity;
        this.scaleHeight = scaleHeight;
        this.topAltitude = topAltitude;
    }

    public bool HasAtmosphere =>
        surfaceDensity > 0.0 &&
        scaleHeight > 0.0 &&
        topAltitude > 0.0;

    public static AtmosphereProfile None =>
        new AtmosphereProfile(0.0, 0.0, 0.0);

    public static AtmosphereProfile CreateForBodyRadius(double bodyRadius)
    {
        return new AtmosphereProfile(
            1.2,
            bodyRadius * 0.25,
            bodyRadius * 3.0
        );
    }

    public double GetTopRadius(double bodyRadius)
    {
        return bodyRadius + topAltitude;
    }
}
