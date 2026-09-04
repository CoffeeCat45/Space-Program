using System;



static class LandingConfig

{

    // Коллизионная «оболочка» корабля в метрах симуляции.

    public const double ShipCollisionRadius = 12.0;



    // Зазор центра корабля над точкой контакта (метры).

    public const double SurfacePad = 3.0;



    // Коэффициент сухого трения при контакте с поверхностью (μ).

    public const double SurfaceFrictionCoefficient = 0.65;



    // Порог |v_vertical| / v_surface для аварии при посадке (м/с).

    public const double CrashSpeedThreshold = 12.0;



    // Скорость звука у поверхности для расчёта reentryIntensity (м/с).

    public const double SeaLevelSpeedOfSound = 340.0;



    public const double SurfaceLaunchAssistClearanceAltitude = 500.0;

    public const double SurfaceLaunchTargetTwr = 2.35;

    /// <summary>Cape Canaveral / KSC.</summary>
    public const double SurfaceLaunchLatitudeRadians = 28.5728 * Math.PI / 180.0;

    /// <summary>Cape Canaveral / KSC (west = negative).</summary>
    public const double SurfaceLaunchLongitudeRadians = -80.6480 * Math.PI / 180.0;

    // Other US grassland spawn examples (edit lat/lon above):
    // Orlando FL (greener inland):  28.5383, -81.3792
    // Huntsville AL:               34.7304, -86.5861
    // Denver CO:                   39.7392, -104.9903

    public const double SurfaceLaunchBurnSeconds = 50.0;

    public const double SurfaceLaunchBurnoutAltitude = 45000.0;

    public const double SurfaceLaunchMaxAscentTwr = 5.5;

    public const double OrbitalStageTargetTwr = 1.15;

    public const double InclinedOrbitTargetInclinationDegrees = 42.0;



    public static double SurfaceStartPlanetRadius => CelestialBody.Earth.Radius;

    public static double SurfaceStartPlanetMu => CelestialBody.Earth.Mu;

    public static double SurfaceLowOrbitAltitude => CelestialBody.Earth.LowOrbitAltitude;



    public static double GetSurfaceStartPlanetSpinRadiansPerSecond()

    {

        return CelestialBody.Earth.GetSpinRadiansPerSecond();

    }



    public static AtmosphereProfile CreateSurfaceStartAtmosphere()

    {

        return CelestialBody.Earth.CreateAtmosphere();

    }



    public static double GetSurfaceContactDistance(double bodyRadius)

    {

        return bodyRadius + ShipCollisionRadius;

    }



    public static double GetSurfaceRestDistance(double bodyRadius)

    {

        return bodyRadius + ShipCollisionRadius + SurfacePad;

    }



    public static SimVector3 DirectionFromPlanetLatitudeLongitude(
        double latitudeRadians,
        double longitudeRadians
    )
    {
        return PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            latitudeRadians,
            longitudeRadians
        );
    }

    public static SimVector3 GetSurfaceLaunchPadNormal()
    {
        return LaunchSiteConfig.GetActiveSurfaceNormal();
    }

    public static SimVector3 GetSurfaceLaunchPadTangent()
    {
        return GetSurfaceEastTangent(GetSurfaceLaunchPadNormal());
    }

    public static SimVector3 GetSurfaceEastTangent(SimVector3 surfaceNormal)
    {
        SimVector3 east = SimVector3.Cross(SimVector3.UnitY(), surfaceNormal);

        if (east.LengthSquared() <= 1e-12)
        {
            return SimVector3.UnitZ().Multiply(-1.0);
        }

        return east.Normalize();
    }



    public static double GetSurfaceStartGravity()

    {

        return CelestialBody.Earth.SurfaceGravity;

    }



    public static double ComputeSurfaceLaunchThrust(double vehicleMass, double targetTwr)

    {

        return targetTwr * vehicleMass * GetSurfaceStartGravity();

    }



    public static double GetGravityAtAltitude(double altitudeAboveSurface)

    {

        double distance = CelestialBody.Earth.Radius + altitudeAboveSurface;



        if (distance <= 0.0)

        {

            return GetSurfaceStartGravity();

        }



        return CelestialBody.Earth.Mu / (distance * distance);

    }



    public static double ComputeCappedSurfaceBoosterThrust(

        double vehicleMass,

        double boosterFuelMass,

        double targetPadTwr

    )

    {

        double padThrust = ComputeSurfaceLaunchThrust(vehicleMass, targetPadTwr);

        double burnoutMass = vehicleMass - boosterFuelMass;



        if (burnoutMass <= 0.0)

        {

            return padThrust;

        }



        double burnoutGravity = GetGravityAtAltitude(SurfaceLaunchBurnoutAltitude);

        double maxAscentThrust =

            SurfaceLaunchMaxAscentTwr * burnoutMass * burnoutGravity;



        return Math.Min(padThrust, maxAscentThrust);

    }



    public static double ComputeOrbitalStageThrust(

        double stageMass,

        double orbitAltitudeAboveSurface,

        double targetTwr

    )

    {

        if (stageMass <= 0.0 || targetTwr <= 0.0)

        {

            return 0.0;

        }



        double orbitGravity = GetGravityAtAltitude(orbitAltitudeAboveSurface);

        return targetTwr * stageMass * orbitGravity;

    }

}

