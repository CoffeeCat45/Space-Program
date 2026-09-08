using System;
using NUnit.Framework;

public sealed class OrbitConicPathTests
{
    const double SmallBodyMu = 4.891e10;
    const double SmallBodyRadius = 173700.0;
    const double LargeBodyMu = 3.986004418e12;
    const double LargeBodyRadius = 637100.0;

    [Test]
    public void RadialAscent_OnSmallBody_DrawsPastTheSurface()
    {
        SimVector3[] path = OrbitalMechanics.BuildOrbitPath(
            SimVector3.Zero(),
            SmallBodyMu,
            SmallBodyRadius,
            new SimVector3(SmallBodyRadius + 250.0, 0.0, 0.0),
            new SimVector3(90.0, 0.0, 0.0),
            48
        );

        Assert.GreaterOrEqual(path.Length, 2);
        Assert.IsTrue(PathReachesAboveSurface(path, SmallBodyRadius, 400.0));
    }

    [Test]
    public void RadialAscent_OnLargeBody_DrawsPastTheSurface()
    {
        SimVector3[] path = OrbitalMechanics.BuildOrbitPath(
            SimVector3.Zero(),
            LargeBodyMu,
            LargeBodyRadius,
            new SimVector3(LargeBodyRadius + 400.0, 0.0, 0.0),
            new SimVector3(120.0, 0.0, 0.0),
            48
        );

        Assert.GreaterOrEqual(path.Length, 2);
        Assert.IsTrue(PathReachesAboveSurface(path, LargeBodyRadius, 400.0));
    }

    [Test]
    public void TinyTangentialLeftover_DoesNotYieldEmptyPath()
    {
        SimVector3[] path = OrbitalMechanics.BuildOrbitPath(
            SimVector3.Zero(),
            SmallBodyMu,
            SmallBodyRadius,
            new SimVector3(SmallBodyRadius + 80.0, 0.0, 0.0),
            new SimVector3(40.0, 0.002, 0.0),
            32
        );

        Assert.GreaterOrEqual(path.Length, 2);
    }

    [Test]
    public void CircularOrbit_StaysNearOrbitRadius()
    {
        double orbitRadius = SmallBodyRadius + 80000.0;
        double circularSpeed = Math.Sqrt(SmallBodyMu / orbitRadius);
        SimVector3[] path = OrbitalMechanics.BuildOrbitPath(
            SimVector3.Zero(),
            SmallBodyMu,
            SmallBodyRadius,
            new SimVector3(orbitRadius, 0.0, 0.0),
            new SimVector3(0.0, circularSpeed, 0.0),
            64
        );

        Assert.GreaterOrEqual(path.Length, 16);

        int nearCount = 0;
        for (int i = 0; i < path.Length; i++)
        {
            if (Math.Abs(path[i].Length() - orbitRadius) < orbitRadius * 0.05)
            {
                nearCount++;
            }
        }

        Assert.Greater(nearCount, path.Length / 2);
    }

    [Test]
    public void CircularOrbit_StillHasPeriapsisAndApoapsisPins()
    {
        double orbitRadius = SmallBodyRadius + 80000.0;
        double circularSpeed = Math.Sqrt(SmallBodyMu / orbitRadius);
        bool found = OrbitalMechanics.TryGetOrbitApsides(
            SimVector3.Zero(),
            SmallBodyMu,
            SmallBodyRadius,
            new SimVector3(orbitRadius, 0.0, 0.0),
            new SimVector3(0.0, circularSpeed, 0.0),
            out _,
            out double periapsisAltitude,
            out bool hasPeriapsis,
            out _,
            out double apoapsisAltitude,
            out bool hasApoapsis
        );

        Assert.IsTrue(found);
        Assert.IsTrue(hasPeriapsis);
        Assert.IsTrue(hasApoapsis);
        Assert.AreEqual(80000.0, periapsisAltitude, 200.0);
        Assert.AreEqual(80000.0, apoapsisAltitude, 200.0);
    }

    [Test]
    public void BallisticAscent_HasPeriapsisAndApoapsisPins()
    {
        bool found = OrbitalMechanics.TryGetOrbitApsides(
            SimVector3.Zero(),
            SmallBodyMu,
            SmallBodyRadius,
            new SimVector3(SmallBodyRadius + 300.0, 0.0, 0.0),
            new SimVector3(70.0, 0.0, 0.0),
            out _,
            out double periapsisAltitude,
            out bool hasPeriapsis,
            out _,
            out double apoapsisAltitude,
            out bool hasApoapsis
        );

        Assert.IsTrue(found);
        Assert.IsTrue(hasPeriapsis);
        Assert.IsTrue(hasApoapsis);
        Assert.Less(periapsisAltitude, 0.0);
        Assert.Greater(apoapsisAltitude, 0.0);
    }

    [Test]
    public void PitchOverAscent_DrawsCurvedBallisticArc()
    {
        double altitude = 8000.0;
        SimVector3[] path = OrbitalMechanics.BuildOrbitPath(
            SimVector3.Zero(),
            LargeBodyMu,
            LargeBodyRadius,
            new SimVector3(LargeBodyRadius + altitude, 0.0, 0.0),
            new SimVector3(220.0, 180.0, 0.0),
            64
        );

        Assert.GreaterOrEqual(path.Length, 8);

        double maxSideOffset = 0.0;
        for (int i = 0; i < path.Length; i++)
        {
            maxSideOffset = Math.Max(maxSideOffset, Math.Abs(path[i].y));
        }

        Assert.Greater(maxSideOffset, 2000.0);
    }

    [Test]
    public void ShallowPitchOver_DoesNotCollapseToRadialNeedle()
    {
        SimVector3[] path = OrbitalMechanics.BuildOrbitPath(
            SimVector3.Zero(),
            LargeBodyMu,
            LargeBodyRadius,
            new SimVector3(LargeBodyRadius + 10000.0, 0.0, 0.0),
            new SimVector3(900.0, 80.0, 0.0),
            64
        );

        Assert.GreaterOrEqual(path.Length, 8);

        double maxSideOffset = 0.0;
        for (int i = 0; i < path.Length; i++)
        {
            maxSideOffset = Math.Max(maxSideOffset, Math.Abs(path[i].y));
        }

        Assert.Greater(maxSideOffset, 500.0);
    }

    [Test]
    public void BoundSuborbital_DoesNotReachEarthSoi()
    {
        double soiRadius = LargeBodyRadius * 14.0;
        bool reaches = OrbitalMechanics.OrbitReachesRadius(
            SimVector3.Zero(),
            LargeBodyMu,
            new SimVector3(LargeBodyRadius + 28000.0, 0.0, 0.0),
            new SimVector3(0.0, 1300.0, 0.0),
            soiRadius
        );

        Assert.IsFalse(reaches);
        Assert.IsFalse(
            OrbitalMechanics.TryGetOutgoingRadiusCrossing(
                SimVector3.Zero(),
                LargeBodyMu,
                new SimVector3(LargeBodyRadius + 28000.0, 0.0, 0.0),
                new SimVector3(0.0, 1300.0, 0.0),
                soiRadius,
                out _,
                out _
            )
        );
    }

    [Test]
    public void HyperbolicEscape_CrossesEarthSoi()
    {
        double soiRadius = LargeBodyRadius * 14.0;
        SimVector3 position = new SimVector3(LargeBodyRadius + 60000.0, 0.0, 0.0);
        SimVector3 velocity = new SimVector3(0.0, 4200.0, 0.0);

        Assert.IsTrue(
            OrbitalMechanics.OrbitReachesRadius(
                SimVector3.Zero(),
                LargeBodyMu,
                position,
                velocity,
                soiRadius
            )
        );

        bool found = OrbitalMechanics.TryGetOutgoingRadiusCrossing(
            SimVector3.Zero(),
            LargeBodyMu,
            position,
            velocity,
            soiRadius,
            out SimVector3 crossing,
            out double time
        );

        Assert.IsTrue(found);
        Assert.Greater(crossing.Length(), soiRadius * 0.99);
        Assert.Less(crossing.Length(), soiRadius * 1.01);
        Assert.Greater(time, 0.0);
    }

    [Test]
    public void HyperbolicPath_ExtendsPastLegacyCutoff()
    {
        double radius = LargeBodyRadius + 80000.0;
        SimVector3[] path = OrbitalMechanics.BuildOrbitPath(
            SimVector3.Zero(),
            LargeBodyMu,
            LargeBodyRadius,
            new SimVector3(radius, 0.0, 0.0),
            new SimVector3(0.0, 4500.0, 0.0),
            64
        );

        Assert.GreaterOrEqual(path.Length, 8);

        double maxRadius = 0.0;
        for (int i = 0; i < path.Length; i++)
        {
            maxRadius = Math.Max(maxRadius, path[i].Length());
        }

        double angularMomentum = radius * 4500.0;
        double semiLatusRectum = angularMomentum * angularMomentum / LargeBodyMu;
        double eccentricity = Math.Sqrt(
            1.0 +
            (2.0 * (0.5 * 4500.0 * 4500.0 - LargeBodyMu / radius) *
                angularMomentum * angularMomentum) /
            (LargeBodyMu * LargeBodyMu)
        );
        double asymptote = Math.Acos(Math.Max(-1.0, Math.Min(1.0, -1.0 / eccentricity)));
        double legacyRadius = semiLatusRectum /
            (1.0 + eccentricity * Math.Cos(asymptote * 0.92));

        Assert.Greater(maxRadius, legacyRadius * 1.15);
    }

    [Test]
    public void HyperbolicPath_WithSoi_StopsNearSoiRadius()
    {
        double radius = SmallBodyRadius + 60000.0;
        double soiRadius = 800000.0;
        SimVector3[] path = OrbitalMechanics.BuildOrbitPath(
            SimVector3.Zero(),
            SmallBodyMu,
            SmallBodyRadius,
            new SimVector3(radius, 0.0, 0.0),
            new SimVector3(0.0, 2500.0, 0.0),
            64,
            soiRadius
        );

        Assert.GreaterOrEqual(path.Length, 8);

        double maxRadius = 0.0;
        for (int i = 0; i < path.Length; i++)
        {
            maxRadius = Math.Max(maxRadius, path[i].Length());
        }

        Assert.Greater(maxRadius, soiRadius * 0.85);
        Assert.Less(maxRadius, soiRadius * 1.15);
    }

    [Test]
    public void RadialEscape_CrossesSmallBodySoi()
    {
        double soiRadius = 800000.0;
        SimVector3 position = new SimVector3(SmallBodyRadius + 250.0, 0.0, 0.0);
        SimVector3 velocity = new SimVector3(2500.0, 0.0, 0.0);

        Assert.IsTrue(
            OrbitalMechanics.OrbitReachesRadius(
                SimVector3.Zero(),
                SmallBodyMu,
                position,
                velocity,
                soiRadius
            )
        );

        bool found = OrbitalMechanics.TryGetOutgoingRadiusCrossing(
            SimVector3.Zero(),
            SmallBodyMu,
            position,
            velocity,
            soiRadius,
            out SimVector3 crossing,
            out SimVector3 crossingVelocity,
            out double time
        );

        Assert.IsTrue(found);
        Assert.Greater(time, 0.0);
        Assert.Greater(crossing.Length(), soiRadius * 0.99);
        Assert.Less(crossing.Length(), soiRadius * 1.01);
        Assert.Greater(SimVector3.Dot(crossing, crossingVelocity), 0.0);
    }

    [Test]
    public void OutgoingSoiCrossing_VelocityPointsOutward()
    {
        double soiRadius = LargeBodyRadius * 14.0;
        SimVector3 position = new SimVector3(LargeBodyRadius + 60000.0, 0.0, 0.0);
        SimVector3 velocity = new SimVector3(0.0, 4200.0, 0.0);

        bool found = OrbitalMechanics.TryGetOutgoingRadiusCrossing(
            SimVector3.Zero(),
            LargeBodyMu,
            position,
            velocity,
            soiRadius,
            out SimVector3 crossing,
            out SimVector3 crossingVelocity,
            out double time
        );

        Assert.IsTrue(found);
        Assert.Greater(time, 0.0);
        Assert.Greater(SimVector3.Dot(crossing, crossingVelocity), 0.0);
        Assert.Greater(crossing.Length(), soiRadius * 0.99);
        Assert.Less(crossing.Length(), soiRadius * 1.01);
    }

    [Test]
    public void CircularHeliocentricOrbit_HasFinitePeriod()
    {
        const double sunMu = 5.98e13;
        const double planetOrbitRadius = 44300000.0;
        double circularSpeed = Math.Sqrt(sunMu / planetOrbitRadius);
        SimVector3 position = new SimVector3(planetOrbitRadius, 0.0, 0.0);
        SimVector3 velocity = new SimVector3(0.0, circularSpeed, 0.0);

        double period = OrbitalMechanics.GetOrbitalPeriod(position, velocity, sunMu);
        double eccentricity = OrbitalMechanics.GetOrbitalEccentricity(
            position,
            velocity,
            sunMu
        );

        Assert.Less(eccentricity, 0.01);
        Assert.IsFalse(double.IsInfinity(period));
        Assert.IsFalse(double.IsNaN(period));
        Assert.Greater(period, 1.0);
    }

    static bool PathReachesAboveSurface(
        SimVector3[] path,
        double bodyRadius,
        double extraAltitude
    )
    {
        double threshold = bodyRadius + extraAltitude;

        for (int i = 0; i < path.Length; i++)
        {
            if (path[i].Length() >= threshold)
            {
                return true;
            }
        }

        return false;
    }
}
