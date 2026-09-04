using System;

/// <summary>
/// Canonical 1:10 scaled solar-system bodies in simulation meters.
/// g = mu / r² is preserved at the surface for honest Keplerian physics.
/// </summary>
static class CelestialBody
{
    public static class Earth
    {
        public const string Name = "Planet";
        public const double Radius = 637100.0;
        public const double Mu = 3.986004418e12;
        public const double AtmosphereTopAltitude = 60000.0;
        public const double SeaLevelDensity = 1.225;
        public const double AtmosphereScaleHeight = 5000.0;
        public const double SiderealPeriodSeconds = 21600.0;
        public const double LowOrbitAltitude = 70000.0;
        public const double HudOrbitSpeedModeAltitude = 40000.0;
        /// <summary>
        /// Unity visual handoff surface-locked → inertial transform (1:10 Earth, ~20 km real).
        /// Core spin angle is always global; only client presentation switches here.
        /// </summary>
        public const double SurfaceReferenceFrameHandoffAltitudeMeters = 20000.0;
        /// <summary>Spin moves from shaders to transform over this band (no snap at handoff start).</summary>
        public const double SurfaceReferenceFrameHandoffBlendEndAltitudeMeters = 28000.0;
        /// <summary>Playable Earth SoI: 1400% of equatorial radius (14×). Target ~140× later.</summary>
        public const double SphereOfInfluenceRadiusMultiplier = 14.0;

        public static double SurfaceGravity => Mu / (Radius * Radius);

        public static double GetSphereOfInfluenceRadius()
        {
            return Radius * SphereOfInfluenceRadiusMultiplier;
        }

        public static double GetSpinRadiansPerSecond()
        {
            return 2.0 * Math.PI / SiderealPeriodSeconds;
        }

        public static double GetCircularOrbitSpeedAtAltitude(double altitudeAboveSurface)
        {
            double orbitRadius = Radius + altitudeAboveSurface;

            if (orbitRadius <= 0.0)
            {
                return 0.0;
            }

            return Math.Sqrt(Mu / orbitRadius);
        }

        public static double GetLowOrbitRadius()
        {
            return Radius + LowOrbitAltitude;
        }

        public static double GetLowOrbitCircularSpeed()
        {
            return GetCircularOrbitSpeedAtAltitude(LowOrbitAltitude);
        }

        public static AtmosphereProfile CreateAtmosphere()
        {
            return new AtmosphereProfile(
                SeaLevelDensity,
                AtmosphereScaleHeight,
                AtmosphereTopAltitude
            );
        }
    }

    public static class Moon
    {
        public const string Name = "Moon";
        public const double Radius = 173700.0;
        public const double Mu = 4.891e10;
        /// <summary>
        /// Toy SMA so the Moon is visible from Earth and sits inside the 14×R Earth SoI.
        /// Later 1:10: <see cref="OrbitSemiMajorAxisRealOneToTen"/>.
        /// </summary>
        public const double OrbitSemiMajorAxis = 4_500_000.0;
        /// <summary>Real Moon SMA at 1:10 (384 400 km / 10).</summary>
        public const double OrbitSemiMajorAxisRealOneToTen = 38_440_000.0;

        public static double SurfaceGravity => Mu / (Radius * Radius);

        public static double GetOrbitalPeriodSeconds()
        {
            double semiMajorAxis = OrbitSemiMajorAxis;

            if (semiMajorAxis <= 0.0 || Earth.Mu <= 0.0)
            {
                return 0.0;
            }

            return 2.0 * Math.PI * Math.Sqrt(
                (semiMajorAxis * semiMajorAxis * semiMajorAxis) / Earth.Mu
            );
        }

        public static double GetSpinRadiansPerSecond()
        {
            double period = GetOrbitalPeriodSeconds();

            if (period <= 1e-6)
            {
                return 0.0;
            }

            return 2.0 * Math.PI / period;
        }
    }

    public static class Sun
    {
        public const string Name = "Sun";
        /// <summary>
        /// Heliocentric root (KSP/RSS): no finite SOI bubble; patched conics never leave Sun as top parent.
        /// </summary>
        public const bool IsRootBody = true;
        public const double Mu = 5.98e13;
        /// <summary>
        /// Toy photosphere: Earth at <see cref="PlanetOrbitRadius"/> must sit outside the sun.
        /// Real 1:10 radius (6.96e7 m) does not fit this compressed orbit.
        /// </summary>
        public const double Radius = 4_000_000.0;
        public const double PlanetOrbitRadius = 44300000.0;
    }
}
