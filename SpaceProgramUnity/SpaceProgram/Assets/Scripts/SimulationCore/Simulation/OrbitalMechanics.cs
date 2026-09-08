using System;
using System.Collections.Generic;

public static class OrbitalMechanics
{
    const double OrbitPathNearCircularEccentricity = 0.001;
    const double HyperbolicPathMinRadiansPerSegment = 0.3 * Math.PI / 180.0;
    const int HyperbolicPathMaxSamples = 2048;
    const double HyperbolicPathLengthScale = 1.35;
    const double DegenerateAngularMomentumFraction = 1.0e-4;
    const double DegenerateSemiLatusRectumFraction = 1.0e-12;
    const double DegenerateRadialFlightPathSine = 1.0e-4;

    public static double GetSpecificOrbitalEnergy(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu
    )
    {
        double radius = relativePosition.Length();

        if (radius == 0)
        {
            return 0;
        }

        double speed = relativeVelocity.Length();
        double kineticPart = 0.5 * speed * speed;
        double potentialPart = -mu / radius;

        return kineticPart + potentialPart;
    }

    public static double GetAngularMomentumMagnitude(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity
    )
    {
        return SimVector3.Cross(relativePosition, relativeVelocity).Length();
    }

    public static double GetSignedAngularMomentum(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity
    )
    {
        return SimVector3.Cross(relativePosition, relativeVelocity).z;
    }

    public static double GetOrbitalEccentricity(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu
    )
    {
        double specificEnergy = GetSpecificOrbitalEnergy(
            relativePosition,
            relativeVelocity,
            mu
        );
        double angularMomentum = GetAngularMomentumMagnitude(
            relativePosition,
            relativeVelocity
        );
        double eccentricitySquared =
            1.0 +
            (
                2.0 *
                specificEnergy *
                angularMomentum *
                angularMomentum
            ) /
            (mu * mu);

        if (eccentricitySquared < 0)
        {
            eccentricitySquared = 0;
        }

        return Math.Sqrt(eccentricitySquared);
    }

    public static double GetSemiMajorAxis(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu
    )
    {
        double specificEnergy = GetSpecificOrbitalEnergy(
            relativePosition,
            relativeVelocity,
            mu
        );

        if (specificEnergy == 0)
        {
            return double.PositiveInfinity;
        }

        return -mu / (2.0 * specificEnergy);
    }

    public static double GetPeriapsisRadius(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu
    )
    {
        double semiMajorAxis = GetSemiMajorAxis(
            relativePosition,
            relativeVelocity,
            mu
        );
        double eccentricity = GetOrbitalEccentricity(
            relativePosition,
            relativeVelocity,
            mu
        );

        if (double.IsInfinity(semiMajorAxis))
        {
            return double.PositiveInfinity;
        }

        return semiMajorAxis * (1.0 - eccentricity);
    }

    public static double GetApoapsisRadius(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu
    )
    {
        double semiMajorAxis = GetSemiMajorAxis(
            relativePosition,
            relativeVelocity,
            mu
        );
        double eccentricity = GetOrbitalEccentricity(
            relativePosition,
            relativeVelocity,
            mu
        );

        if (double.IsInfinity(semiMajorAxis) || eccentricity >= 1.0)
        {
            return double.PositiveInfinity;
        }

        return semiMajorAxis * (1.0 + eccentricity);
    }

    public static bool OrbitReachesRadius(
        SimVector3 primaryPosition,
        double primaryMu,
        SimVector3 orbitingPosition,
        SimVector3 relativeVelocity,
        double targetRadius
    )
    {
        if (targetRadius <= 0.0 || primaryMu <= 0.0)
        {
            return false;
        }

        SimVector3 relativePosition = orbitingPosition.Subtract(primaryPosition);
        double radius = relativePosition.Length();
        if (radius <= 0.0)
        {
            return false;
        }

        if (radius >= targetRadius)
        {
            return true;
        }

        double apoapsisRadius = GetApoapsisRadius(
            relativePosition,
            relativeVelocity,
            primaryMu
        );
        return !double.IsNaN(apoapsisRadius) && apoapsisRadius >= targetRadius;
    }

    public static bool TryGetOutgoingRadiusCrossing(
        SimVector3 primaryPosition,
        double primaryMu,
        SimVector3 orbitingPosition,
        SimVector3 relativeVelocity,
        double targetRadius,
        out SimVector3 crossingPosition,
        out double timeToCrossing
    )
    {
        return TryGetOutgoingRadiusCrossing(
            primaryPosition,
            primaryMu,
            orbitingPosition,
            relativeVelocity,
            targetRadius,
            out crossingPosition,
            out _,
            out timeToCrossing
        );
    }

    public static bool TryGetOutgoingRadiusCrossing(
        SimVector3 primaryPosition,
        double primaryMu,
        SimVector3 orbitingPosition,
        SimVector3 relativeVelocity,
        double targetRadius,
        out SimVector3 crossingPosition,
        out SimVector3 crossingRelativeVelocity,
        out double timeToCrossing
    )
    {
        crossingPosition = orbitingPosition;
        crossingRelativeVelocity = relativeVelocity;
        timeToCrossing = double.NaN;

        SimVector3 relativePosition = orbitingPosition.Subtract(primaryPosition);
        double radius = relativePosition.Length();
        if (
            targetRadius <= 0.0 ||
            primaryMu <= 0.0 ||
            radius <= 0.0 ||
            radius >= targetRadius * 0.999
        )
        {
            return false;
        }

        if (
            !OrbitReachesRadius(
                primaryPosition,
                primaryMu,
                orbitingPosition,
                relativeVelocity,
                targetRadius
            )
        )
        {
            return false;
        }

        double angularMomentum = GetAngularMomentumMagnitude(
            relativePosition,
            relativeVelocity
        );
        if (angularMomentum <= 1.0e-12)
        {
            return TryGetRadialOutgoingRadiusCrossing(
                primaryPosition,
                primaryMu,
                relativePosition,
                relativeVelocity,
                targetRadius,
                out crossingPosition,
                out crossingRelativeVelocity,
                out timeToCrossing
            );
        }

        double semiLatusRectum = angularMomentum * angularMomentum / primaryMu;
        SimVector3 eccentricityVector = GetEccentricityVector(
            relativePosition,
            relativeVelocity,
            primaryMu
        );
        double eccentricity = eccentricityVector.Length();
        if (eccentricity < OrbitPathNearCircularEccentricity)
        {
            return false;
        }

        double cosine = (semiLatusRectum / targetRadius - 1.0) / eccentricity;
        if (cosine < -1.0 || cosine > 1.0)
        {
            return false;
        }

        SimVector3 orbitNormal = GetOrbitNormal(relativePosition, relativeVelocity);
        SimVector3 periapsisDirection = eccentricityVector.Normalize();
        SimVector3 sideDirection = SimVector3.Cross(orbitNormal, periapsisDirection);
        if (sideDirection.LengthSquared() <= 1.0e-20)
        {
            return false;
        }

        sideDirection = sideDirection.Normalize();
        double crossingTrueAnomaly = Math.Acos(cosine);
        double shipTrueAnomaly = Math.Atan2(
            SimVector3.Dot(relativePosition, sideDirection),
            SimVector3.Dot(relativePosition, periapsisDirection)
        );

        if (eccentricity >= 1.0 && shipTrueAnomaly > crossingTrueAnomaly)
        {
            return false;
        }

        crossingPosition = primaryPosition.Add(
            periapsisDirection.Multiply(Math.Cos(crossingTrueAnomaly) * targetRadius).Add(
                sideDirection.Multiply(Math.Sin(crossingTrueAnomaly) * targetRadius)
            )
        );
        crossingRelativeVelocity = GetPerifocalVelocity(
            periapsisDirection,
            sideDirection,
            primaryMu,
            semiLatusRectum,
            eccentricity,
            crossingTrueAnomaly
        );
        timeToCrossing = GetTimeToTrueAnomaly(
            relativePosition,
            relativeVelocity,
            primaryMu,
            crossingTrueAnomaly
        );

        return
            !double.IsNaN(timeToCrossing) &&
            !double.IsInfinity(timeToCrossing) &&
            timeToCrossing >= 0.0;
    }

    static bool TryGetRadialOutgoingRadiusCrossing(
        SimVector3 primaryPosition,
        double primaryMu,
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double targetRadius,
        out SimVector3 crossingPosition,
        out SimVector3 crossingRelativeVelocity,
        out double timeToCrossing
    )
    {
        crossingPosition = primaryPosition.Add(relativePosition);
        crossingRelativeVelocity = relativeVelocity;
        timeToCrossing = double.NaN;

        double radius = relativePosition.Length();
        if (radius <= 0.0 || targetRadius <= radius)
        {
            return false;
        }

        SimVector3 radialDirection = relativePosition.Multiply(1.0 / radius);
        double radialSpeed = SimVector3.Dot(relativeVelocity, radialDirection);
        if (radialSpeed <= 0.0)
        {
            return false;
        }

        double energy = 0.5 * relativeVelocity.LengthSquared() - primaryMu / radius;
        if (energy + primaryMu / targetRadius < 0.0)
        {
            return false;
        }

        timeToCrossing = GetRadialTimeOfFlight(primaryMu, radius, targetRadius, energy);
        if (
            double.IsNaN(timeToCrossing) ||
            double.IsInfinity(timeToCrossing) ||
            timeToCrossing < 0.0
        )
        {
            return false;
        }

        double crossingSpeed = Math.Sqrt(
            Math.Max(0.0, 2.0 * (energy + primaryMu / targetRadius))
        );
        crossingPosition = primaryPosition.Add(radialDirection.Multiply(targetRadius));
        crossingRelativeVelocity = radialDirection.Multiply(crossingSpeed);
        return true;
    }

    static double GetRadialTimeOfFlight(
        double mu,
        double startRadius,
        double endRadius,
        double energy
    )
    {
        if (mu <= 0.0 || startRadius <= 0.0 || endRadius <= startRadius)
        {
            return double.NaN;
        }

        if (energy > 1.0e-12)
        {
            double hyperbolicA = mu / (2.0 * energy);
            return RadialAnomalyTime(mu, hyperbolicA, endRadius, hyperbolic: true) -
                RadialAnomalyTime(mu, hyperbolicA, startRadius, hyperbolic: true);
        }

        if (energy < -1.0e-12)
        {
            double ellipticA = -mu / (2.0 * energy);
            double apoapsisRadius = 2.0 * ellipticA;
            if (endRadius > apoapsisRadius * 0.999)
            {
                return double.NaN;
            }

            return RadialAnomalyTime(mu, ellipticA, endRadius, hyperbolic: false) -
                RadialAnomalyTime(mu, ellipticA, startRadius, hyperbolic: false);
        }

        return (2.0 / 3.0) *
            (Math.Pow(endRadius, 1.5) - Math.Pow(startRadius, 1.5)) /
            Math.Sqrt(2.0 * mu);
    }

    static double RadialAnomalyTime(
        double mu,
        double a,
        double radius,
        bool hyperbolic
    )
    {
        if (a <= 0.0 || radius < 0.0 || mu <= 0.0)
        {
            return double.NaN;
        }

        double meanMotion = Math.Sqrt(mu / (a * a * a));
        if (hyperbolic)
        {
            double coshF = 1.0 + radius / a;
            double sinhF = Math.Sqrt(Math.Max(0.0, coshF * coshF - 1.0));
            double f = Math.Log(coshF + sinhF);
            return (sinhF - f) / meanMotion;
        }

        double cosine = Math.Max(-1.0, Math.Min(1.0, 1.0 - radius / a));
        double eccentricAnomaly = Math.Acos(cosine);
        return (eccentricAnomaly - Math.Sin(eccentricAnomaly)) / meanMotion;
    }

    public static string GetOrbitType(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu
    )
    {
        double specificEnergy = GetSpecificOrbitalEnergy(
            relativePosition,
            relativeVelocity,
            mu
        );
        double epsilon = 0.000001;

        if (specificEnergy < -epsilon)
        {
            return "Ellipse";
        }

        if (specificEnergy > epsilon)
        {
            return "Hyperbola";
        }

        return "Parabola";
    }

    public static double GetEscapeSpeed(double radius, double mu)
    {
        if (radius == 0)
        {
            return 0;
        }

        return Math.Sqrt(2.0 * mu / radius);
    }

    public static double GetOrbitalPeriod(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu
    )
    {
        double eccentricity = GetOrbitalEccentricity(
            relativePosition,
            relativeVelocity,
            mu
        );
        double semiMajorAxis = GetSemiMajorAxis(
            relativePosition,
            relativeVelocity,
            mu
        );

        if (
            eccentricity >= 1.0 ||
            semiMajorAxis <= 0 ||
            double.IsInfinity(semiMajorAxis) ||
            mu <= 0
        )
        {
            return double.PositiveInfinity;
        }

        return Math.PI * 2.0 * Math.Sqrt(
            semiMajorAxis * semiMajorAxis * semiMajorAxis / mu
        );
    }

    public static double GetTimeToOrbitalEvent(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu,
        double targetMeanAnomaly
    )
    {
        double radius = relativePosition.Length();
        double semiMajorAxis = GetSemiMajorAxis(
            relativePosition,
            relativeVelocity,
            mu
        );

        if (
            radius <= 0.0 ||
            mu <= 0.0 ||
            semiMajorAxis <= 0.0 ||
            double.IsInfinity(semiMajorAxis)
        )
        {
            return double.PositiveInfinity;
        }

        double speedSquared = relativeVelocity.LengthSquared();
        double radialVelocity = SimVector3.Dot(relativePosition, relativeVelocity);
        SimVector3 eccentricityVector = relativePosition
            .Multiply((speedSquared - mu / radius) / mu)
            .Subtract(relativeVelocity.Multiply(radialVelocity / mu));
        double eccentricity = eccentricityVector.Length();

        if (eccentricity < 0.01 || eccentricity >= 1.0)
        {
            return double.PositiveInfinity;
        }

        SimVector3 orbitNormal = GetOrbitNormal(relativePosition, relativeVelocity);
        SimVector3 periapsisDirection = eccentricityVector.Normalize();
        SimVector3 sideDirection = SimVector3.Cross(orbitNormal, periapsisDirection).Normalize();

        double trueAnomaly = Math.Atan2(
            SimVector3.Dot(relativePosition, sideDirection),
            SimVector3.Dot(relativePosition, periapsisDirection)
        );
        double eccentricAnomaly = 2.0 * Math.Atan2(
            Math.Sqrt(1.0 - eccentricity) * Math.Sin(trueAnomaly * 0.5),
            Math.Sqrt(1.0 + eccentricity) * Math.Cos(trueAnomaly * 0.5)
        );
        double meanAnomaly = NormalizeAngle(
            eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly)
        );
        double target = NormalizeAngle(targetMeanAnomaly);
        double deltaMeanAnomaly = target - meanAnomaly;

        if (deltaMeanAnomaly < 0.0)
        {
            deltaMeanAnomaly += Math.PI * 2.0;
        }

        double meanMotion = Math.Sqrt(
            mu /
            (semiMajorAxis * semiMajorAxis * semiMajorAxis)
        );

        if (meanMotion <= 0.0)
        {
            return double.PositiveInfinity;
        }

        return deltaMeanAnomaly / meanMotion;
    }

    internal static SimVector3 GetCircularizeDeltaV(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu,
        IGravityModel gravityModel
    )
    {
        double radius = relativePosition.Length();

        if (radius == 0 || mu <= 0)
        {
            return SimVector3.Zero();
        }

        SimVector3 orbitNormal = GetOrbitNormal(relativePosition, relativeVelocity);
        SimVector3 circularDirection = SimVector3.Cross(orbitNormal, relativePosition).Normalize();
        double circularSpeed = gravityModel.CalculateCircularOrbitSpeed(mu, radius);
        SimVector3 targetRelativeVelocity = circularDirection.Multiply(circularSpeed);

        return targetRelativeVelocity.Subtract(relativeVelocity);
    }

    internal static SimVector3[] BuildOrbitPathRelativeToBody(
        Body primaryBody,
        SimVector3 orbitingPosition,
        SimVector3 orbitingVelocity,
        int pointCount
    )
    {
        if (primaryBody == null)
        {
            return Array.Empty<SimVector3>();
        }

        return BuildOrbitPath(
            primaryBody.position,
            primaryBody.mu,
            primaryBody.radius,
            orbitingPosition,
            orbitingVelocity.Subtract(primaryBody.velocity),
            pointCount,
            primaryBody.sphereOfInfluenceRadius
        );
    }

    /// <summary>
    /// Osculating two-body path around any primary. <paramref name="orbitingVelocity"/>
    /// must already be relative to that primary.
    /// </summary>
    public static SimVector3[] BuildOrbitPath(
        SimVector3 primaryPosition,
        double primaryMu,
        double primaryRadius,
        SimVector3 orbitingPosition,
        SimVector3 orbitingVelocity,
        int pointCount,
        double sphereOfInfluenceRadius = 0.0
    )
    {
        if (pointCount < 2 || primaryMu <= 0.0)
        {
            return Array.Empty<SimVector3>();
        }

        SimVector3 relativePosition = orbitingPosition.Subtract(primaryPosition);
        SimVector3 relativeVelocity = orbitingVelocity;
        double radius = relativePosition.Length();

        if (radius <= 0.0)
        {
            return Array.Empty<SimVector3>();
        }

        double angularMomentum = GetAngularMomentumMagnitude(
            relativePosition,
            relativeVelocity
        );
        SimVector3 eccentricityVector = GetEccentricityVector(
            relativePosition,
            relativeVelocity,
            primaryMu
        );

        if (IsDegenerateInPlaneConic(
            radius,
            relativeVelocity.Length(),
            angularMomentum,
            primaryMu
        ))
        {
            return BuildRadialOrbitPath(
                primaryPosition,
                primaryRadius,
                primaryMu,
                relativePosition,
                relativeVelocity,
                eccentricityVector,
                pointCount,
                sphereOfInfluenceRadius
            );
        }

        double eccentricity = eccentricityVector.Length();
        double semiLatusRectum = angularMomentum * angularMomentum / primaryMu;

        if (semiLatusRectum <= 0.0)
        {
            return BuildRadialOrbitPath(
                primaryPosition,
                primaryRadius,
                primaryMu,
                relativePosition,
                relativeVelocity,
                eccentricityVector,
                pointCount,
                sphereOfInfluenceRadius
            );
        }

        double pathEccentricity =
            eccentricity < OrbitPathNearCircularEccentricity ?
            0.0 :
            eccentricity;
        SimVector3 orbitNormal = GetOrbitNormal(relativePosition, relativeVelocity);
        SimVector3 periapsisDirection =
            pathEccentricity > 0.0 ?
            eccentricityVector.Normalize() :
            relativePosition.Normalize();
        SimVector3 sideDirection = SimVector3.Cross(orbitNormal, periapsisDirection);

        if (sideDirection.LengthSquared() <= 1.0e-20)
        {
            return BuildRadialOrbitPath(
                primaryPosition,
                primaryRadius,
                primaryMu,
                relativePosition,
                relativeVelocity,
                eccentricityVector,
                pointCount,
                sphereOfInfluenceRadius
            );
        }

        sideDirection = sideDirection.Normalize();
        double periapsisRadius = GetPeriapsisRadius(
            relativePosition,
            relativeVelocity,
            primaryMu
        );

        List<SimVector3> points = new List<SimVector3>();

        if (pathEccentricity < 1.0)
        {
            SampleBoundOrbitPath(
                points,
                primaryPosition,
                primaryRadius,
                periapsisDirection,
                sideDirection,
                semiLatusRectum,
                pathEccentricity,
                periapsisRadius,
                pointCount
            );
        }
        else
        {
            SampleUnboundOrbitPath(
                points,
                primaryPosition,
                primaryRadius,
                relativePosition,
                periapsisDirection,
                sideDirection,
                semiLatusRectum,
                pathEccentricity,
                pointCount,
                sphereOfInfluenceRadius
            );
        }

        if (points.Count >= 2)
        {
            return points.ToArray();
        }

        return BuildRadialOrbitPath(
            primaryPosition,
            primaryRadius,
            primaryMu,
            relativePosition,
            relativeVelocity,
            eccentricityVector,
            pointCount,
            sphereOfInfluenceRadius
        );
    }

    public static bool TryGetOrbitApsides(
        SimVector3 primaryPosition,
        double primaryMu,
        double primaryRadius,
        SimVector3 orbitingPosition,
        SimVector3 orbitingVelocity,
        out SimVector3 periapsisPosition,
        out double periapsisAltitude,
        out bool hasPeriapsis,
        out SimVector3 apoapsisPosition,
        out double apoapsisAltitude,
        out bool hasApoapsis
    )
    {
        periapsisPosition = primaryPosition;
        periapsisAltitude = double.NaN;
        hasPeriapsis = false;
        apoapsisPosition = primaryPosition;
        apoapsisAltitude = double.NaN;
        hasApoapsis = false;

        SimVector3 relativePosition = orbitingPosition.Subtract(primaryPosition);
        double radius = relativePosition.Length();

        if (radius <= 0.0 || primaryMu <= 0.0)
        {
            return false;
        }

        SimVector3 eccentricityVector = GetEccentricityVector(
            relativePosition,
            orbitingVelocity,
            primaryMu
        );
        double eccentricity = eccentricityVector.Length();
        SimVector3 periapsisDirection =
            eccentricity > OrbitPathNearCircularEccentricity ?
            eccentricityVector.Normalize() :
            relativePosition.Normalize();

        double periapsisRadius = GetPeriapsisRadius(
            relativePosition,
            orbitingVelocity,
            primaryMu
        );
        double apoapsisRadius = GetApoapsisRadius(
            relativePosition,
            orbitingVelocity,
            primaryMu
        );
        double specificEnergy = GetSpecificOrbitalEnergy(
            relativePosition,
            orbitingVelocity,
            primaryMu
        );

        if (
            (double.IsInfinity(apoapsisRadius) || apoapsisRadius <= 0.0) &&
            specificEnergy < 0.0
        )
        {
            apoapsisRadius = -primaryMu / specificEnergy;
        }

        hasPeriapsis =
            !double.IsNaN(periapsisRadius) &&
            !double.IsInfinity(periapsisRadius) &&
            periapsisRadius >= 0.0;
        hasApoapsis =
            !double.IsNaN(apoapsisRadius) &&
            !double.IsInfinity(apoapsisRadius) &&
            apoapsisRadius > 0.0 &&
            specificEnergy < 0.0;

        if (hasPeriapsis)
        {
            double placedPeriapsisRadius = Math.Max(
                periapsisRadius,
                Math.Max(primaryRadius * 1.0e-4, 1.0)
            );
            periapsisPosition = primaryPosition.Add(
                periapsisDirection.Multiply(placedPeriapsisRadius)
            );
            periapsisAltitude = periapsisRadius - primaryRadius;
        }

        if (hasApoapsis)
        {
            apoapsisPosition = primaryPosition.Add(
                periapsisDirection.Multiply(-apoapsisRadius)
            );
            apoapsisAltitude = apoapsisRadius - primaryRadius;
        }

        return hasPeriapsis || hasApoapsis;
    }

    internal static bool TryGetOrbitApsides(
        Body primaryBody,
        SimVector3 orbitingPosition,
        SimVector3 orbitingVelocity,
        out SimVector3 periapsisPosition,
        out double periapsisAltitude,
        out bool hasPeriapsis,
        out SimVector3 apoapsisPosition,
        out double apoapsisAltitude,
        out bool hasApoapsis
    )
    {
        if (primaryBody == null)
        {
            periapsisPosition = SimVector3.Zero();
            periapsisAltitude = double.NaN;
            hasPeriapsis = false;
            apoapsisPosition = SimVector3.Zero();
            apoapsisAltitude = double.NaN;
            hasApoapsis = false;
            return false;
        }

        return TryGetOrbitApsides(
            primaryBody.position,
            primaryBody.mu,
            primaryBody.radius,
            orbitingPosition,
            orbitingVelocity.Subtract(primaryBody.velocity),
            out periapsisPosition,
            out periapsisAltitude,
            out hasPeriapsis,
            out apoapsisPosition,
            out apoapsisAltitude,
            out hasApoapsis
        );
    }

    static bool IsDegenerateInPlaneConic(
        double radius,
        double speed,
        double angularMomentum,
        double mu
    )
    {
        if (mu <= 0.0 || radius <= 0.0)
        {
            return true;
        }

        double circularAngularMomentum = Math.Sqrt(mu * radius);
        double semiLatusRectum = angularMomentum * angularMomentum / mu;
        double radialAlignmentSine = speed <= 1.0e-12 ?
            0.0 :
            angularMomentum / (radius * speed);

        return
            angularMomentum < circularAngularMomentum * DegenerateAngularMomentumFraction ||
            semiLatusRectum < Math.Max(1.0e-6, radius * DegenerateSemiLatusRectumFraction) ||
            radialAlignmentSine < DegenerateRadialFlightPathSine;
    }

    static void SampleBoundOrbitPath(
        List<SimVector3> points,
        SimVector3 primaryPosition,
        double primaryRadius,
        SimVector3 periapsisDirection,
        SimVector3 sideDirection,
        double semiLatusRectum,
        double eccentricity,
        double periapsisRadius,
        int pointCount
    )
    {
        double minTrueAnomaly = 0.0;
        double maxTrueAnomaly = Math.PI * 2.0;

        if (
            primaryRadius > 0.0 &&
            !double.IsNaN(periapsisRadius) &&
            periapsisRadius < primaryRadius &&
            eccentricity > OrbitPathNearCircularEccentricity &&
            eccentricity < 1.0
        )
        {
            double cosine = (semiLatusRectum / primaryRadius - 1.0) / eccentricity;

            if (cosine > -1.0 && cosine < 1.0)
            {
                double surfaceTrueAnomaly = Math.Acos(cosine);
                minTrueAnomaly = surfaceTrueAnomaly;
                maxTrueAnomaly = Math.PI * 2.0 - surfaceTrueAnomaly;
            }
        }

        double span = maxTrueAnomaly - minTrueAnomaly;
        int sampleCount = Math.Max(pointCount, 8);

        for (int i = 0; i <= sampleCount; i++)
        {
            double t = sampleCount == 0 ? 0.0 : (double)i / sampleCount;
            double trueAnomaly = minTrueAnomaly + span * t;
            AddOrbitPathPoint(
                points,
                primaryPosition,
                periapsisDirection,
                sideDirection,
                semiLatusRectum,
                eccentricity,
                trueAnomaly
            );
        }
    }

    static void SampleUnboundOrbitPath(
        List<SimVector3> points,
        SimVector3 primaryPosition,
        double primaryRadius,
        SimVector3 relativePosition,
        SimVector3 periapsisDirection,
        SimVector3 sideDirection,
        double semiLatusRectum,
        double eccentricity,
        int pointCount,
        double sphereOfInfluenceRadius
    )
    {
        double maxTrueAnomaly = GetUnboundVisibleTrueAnomalyLimit(
            eccentricity,
            semiLatusRectum,
            sphereOfInfluenceRadius
        );

        double shipTrueAnomaly = Math.Atan2(
            SimVector3.Dot(relativePosition, sideDirection),
            SimVector3.Dot(relativePosition, periapsisDirection)
        );

        double minTrueAnomaly = FindSurfaceLimitedTrueAnomaly(
            semiLatusRectum,
            eccentricity,
            primaryRadius,
            shipTrueAnomaly,
            -1,
            -maxTrueAnomaly
        );
        double maxTrueAnomalyVisible = FindSurfaceLimitedTrueAnomaly(
            semiLatusRectum,
            eccentricity,
            primaryRadius,
            shipTrueAnomaly,
            1,
            maxTrueAnomaly
        );

        if (maxTrueAnomalyVisible <= minTrueAnomaly)
        {
            double fallbackSpan = maxTrueAnomaly * 0.3;
            minTrueAnomaly = Math.Max(-maxTrueAnomaly, shipTrueAnomaly - fallbackSpan);
            maxTrueAnomalyVisible = Math.Min(maxTrueAnomaly, shipTrueAnomaly + fallbackSpan);
        }

        double trueAnomalySpan = maxTrueAnomalyVisible - minTrueAnomaly;
        int sampleCount = Math.Max(
            pointCount,
            (int)Math.Ceiling(trueAnomalySpan / HyperbolicPathMinRadiansPerSegment) + 1
        );
        sampleCount = Math.Min(sampleCount, HyperbolicPathMaxSamples);

        for (int i = 0; i < sampleCount; i++)
        {
            double t = sampleCount == 1 ?
                0.0 :
                (double)i / (sampleCount - 1);
            double trueAnomaly = minTrueAnomaly + trueAnomalySpan * t;
            AddOrbitPathPoint(
                points,
                primaryPosition,
                periapsisDirection,
                sideDirection,
                semiLatusRectum,
                eccentricity,
                trueAnomaly
            );
        }
    }

    static SimVector3 GetEccentricityVector(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu
    )
    {
        double radius = relativePosition.Length();

        if (radius <= 0.0 || mu <= 0.0)
        {
            return SimVector3.Zero();
        }

        double speedSquared = relativeVelocity.LengthSquared();
        double radialVelocity = SimVector3.Dot(relativePosition, relativeVelocity);
        return relativePosition
            .Multiply((speedSquared - mu / radius) / mu)
            .Subtract(relativeVelocity.Multiply(radialVelocity / mu));
    }

    static SimVector3[] BuildRadialOrbitPath(
        SimVector3 primaryPosition,
        double primaryRadius,
        double primaryMu,
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        SimVector3 eccentricityVector,
        int pointCount,
        double sphereOfInfluenceRadius = 0.0
    )
    {
        double radius = relativePosition.Length();
        if (radius <= 0.0 || pointCount < 2 || primaryMu <= 0.0)
        {
            return Array.Empty<SimVector3>();
        }

        SimVector3 axis = eccentricityVector.LengthSquared() > 1.0e-20 ?
            eccentricityVector.Normalize() :
            relativePosition.Normalize();
        double specificEnergy =
            0.5 * relativeVelocity.LengthSquared() - primaryMu / radius;
        double apoapsisRadius = specificEnergy < 0.0 ?
            -primaryMu / specificEnergy :
            Math.Max(
                radius * 4.0,
                radius + Math.Sqrt(Math.Max(relativeVelocity.LengthSquared(), 1.0)) * 300.0
            );
        if (sphereOfInfluenceRadius > radius)
        {
            apoapsisRadius = specificEnergy < 0.0 ?
                Math.Min(apoapsisRadius, sphereOfInfluenceRadius) :
                sphereOfInfluenceRadius;
        }

        apoapsisRadius = Math.Max(apoapsisRadius, radius * 1.02);
        double coreRadius = Math.Max(primaryRadius * 0.02, 1.0);

        int count = Math.Max(pointCount, 8);
        SimVector3[] points = new SimVector3[count];
        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? 0.0 : (double)i / (count - 1);
            double signedRadius = -apoapsisRadius + 2.0 * apoapsisRadius * t;
            if (Math.Abs(signedRadius) < coreRadius)
            {
                signedRadius = signedRadius >= 0.0 ? coreRadius : -coreRadius;
            }

            points[i] = primaryPosition.Add(axis.Multiply(signedRadius));
        }

        return points;
    }

    static SimVector3 GetOrbitNormal(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity
    )
    {
        SimVector3 orbitNormal = SimVector3.Cross(relativePosition, relativeVelocity);

        if (orbitNormal.LengthSquared() <= 0.000001)
        {
            return SimVector3.UnitZ();
        }

        return orbitNormal.Normalize();
    }

    static SimVector3 GetPerifocalVelocity(
        SimVector3 periapsisDirection,
        SimVector3 sideDirection,
        double mu,
        double semiLatusRectum,
        double eccentricity,
        double trueAnomaly
    )
    {
        if (semiLatusRectum <= 0.0 || mu <= 0.0)
        {
            return SimVector3.Zero();
        }

        double scale = Math.Sqrt(mu / semiLatusRectum);
        double sinTrueAnomaly = Math.Sin(trueAnomaly);
        double cosTrueAnomaly = Math.Cos(trueAnomaly);
        return periapsisDirection.Multiply(-scale * sinTrueAnomaly).Add(
            sideDirection.Multiply(scale * (eccentricity + cosTrueAnomaly))
        );
    }

    static double GetOrbitRadiusAtTrueAnomaly(
        double semiLatusRectum,
        double eccentricity,
        double trueAnomaly
    )
    {
        double denominator = 1.0 + eccentricity * Math.Cos(trueAnomaly);

        if (Math.Abs(denominator) < 0.000001)
        {
            return double.PositiveInfinity;
        }

        return semiLatusRectum / denominator;
    }

    static double FindSurfaceLimitedTrueAnomaly(
        double semiLatusRectum,
        double eccentricity,
        double bodyRadius,
        double startTrueAnomaly,
        int direction,
        double limitTrueAnomaly
    )
    {
        const double stepRadians = 0.25 * Math.PI / 180.0;
        double trueAnomaly = startTrueAnomaly;

        while (
            direction < 0 ?
            trueAnomaly > limitTrueAnomaly :
            trueAnomaly < limitTrueAnomaly)
        {
            double radius = GetOrbitRadiusAtTrueAnomaly(
                semiLatusRectum,
                eccentricity,
                trueAnomaly
            );

            if (radius <= bodyRadius)
            {
                return trueAnomaly;
            }

            trueAnomaly += direction * stepRadians;
        }

        return limitTrueAnomaly;
    }

    static double GetUnboundVisibleTrueAnomalyLimit(
        double eccentricity,
        double semiLatusRectum,
        double sphereOfInfluenceRadius
    )
    {
        double asymptoteTrueAnomaly = eccentricity <= 1.0 + 1.0e-9 ?
            Math.PI * 0.85 :
            Math.Acos(Math.Max(-1.0, Math.Min(1.0, -1.0 / eccentricity)));
        double referenceTrueAnomaly = asymptoteTrueAnomaly * 0.92;
        double soiTrueAnomaly = TryGetTrueAnomalyAtRadius(
            eccentricity,
            semiLatusRectum,
            sphereOfInfluenceRadius,
            asymptoteTrueAnomaly
        );
        if (!double.IsNaN(soiTrueAnomaly))
        {
            return soiTrueAnomaly;
        }

        double referenceRadius = GetOrbitRadiusAtTrueAnomaly(
            semiLatusRectum,
            eccentricity,
            referenceTrueAnomaly
        );

        if (
            double.IsNaN(referenceRadius) ||
            double.IsInfinity(referenceRadius) ||
            referenceRadius <= 0.0
        )
        {
            return referenceTrueAnomaly;
        }

        double targetRadius = referenceRadius * HyperbolicPathLengthScale;
        double cosine = (semiLatusRectum / targetRadius - 1.0) /
            Math.Max(eccentricity, 1.0e-12);
        double asymptoteCosine = eccentricity <= 1.0 + 1.0e-9 ?
            Math.Cos(asymptoteTrueAnomaly) :
            (-1.0 / eccentricity) * 0.997;
        cosine = Math.Max(cosine, asymptoteCosine);
        cosine = Math.Min(cosine, 1.0);
        return Math.Acos(cosine);
    }

    static double TryGetTrueAnomalyAtRadius(
        double eccentricity,
        double semiLatusRectum,
        double targetRadius,
        double asymptoteTrueAnomaly
    )
    {
        if (targetRadius <= 0.0 || eccentricity <= 0.0 || semiLatusRectum <= 0.0)
        {
            return double.NaN;
        }

        double cosine = (semiLatusRectum / targetRadius - 1.0) /
            Math.Max(eccentricity, 1.0e-12);
        double asymptoteCosine = eccentricity <= 1.0 + 1.0e-9 ?
            Math.Cos(asymptoteTrueAnomaly) :
            (-1.0 / eccentricity) * 0.997;
        if (cosine < asymptoteCosine || cosine > 1.0)
        {
            return double.NaN;
        }

        return Math.Acos(Math.Max(asymptoteCosine, Math.Min(1.0, cosine)));
    }

    static double GetTimeToTrueAnomaly(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity,
        double mu,
        double targetTrueAnomaly
    )
    {
        double eccentricity = GetOrbitalEccentricity(
            relativePosition,
            relativeVelocity,
            mu
        );
        double semiMajorAxis = GetSemiMajorAxis(
            relativePosition,
            relativeVelocity,
            mu
        );
        SimVector3 eccentricityVector = GetEccentricityVector(
            relativePosition,
            relativeVelocity,
            mu
        );
        if (eccentricityVector.LengthSquared() <= 1.0e-20 || mu <= 0.0)
        {
            return double.NaN;
        }

        SimVector3 orbitNormal = GetOrbitNormal(relativePosition, relativeVelocity);
        SimVector3 periapsisDirection = eccentricityVector.Normalize();
        SimVector3 sideDirection = SimVector3.Cross(orbitNormal, periapsisDirection);
        if (sideDirection.LengthSquared() <= 1.0e-20)
        {
            return double.NaN;
        }

        sideDirection = sideDirection.Normalize();
        double currentTrueAnomaly = Math.Atan2(
            SimVector3.Dot(relativePosition, sideDirection),
            SimVector3.Dot(relativePosition, periapsisDirection)
        );

        if (eccentricity < 1.0)
        {
            if (semiMajorAxis <= 0.0)
            {
                return double.NaN;
            }

            double currentMean = TrueAnomalyToMeanAnomaly(currentTrueAnomaly, eccentricity);
            double targetMean = TrueAnomalyToMeanAnomaly(targetTrueAnomaly, eccentricity);
            double delta = targetMean - currentMean;
            if (delta < 0.0)
            {
                delta += Math.PI * 2.0;
            }

            double meanMotion = Math.Sqrt(mu / (semiMajorAxis * semiMajorAxis * semiMajorAxis));
            return delta / meanMotion;
        }

        double absSemiMajor = Math.Abs(semiMajorAxis);
        if (absSemiMajor <= 0.0)
        {
            return double.NaN;
        }

        double currentMeanHyperbolic = TrueAnomalyToHyperbolicMeanAnomaly(
            currentTrueAnomaly,
            eccentricity
        );
        double targetMeanHyperbolic = TrueAnomalyToHyperbolicMeanAnomaly(
            targetTrueAnomaly,
            eccentricity
        );
        double hyperbolicDelta = targetMeanHyperbolic - currentMeanHyperbolic;
        if (hyperbolicDelta < 0.0)
        {
            return double.NaN;
        }

        double hyperbolicMotion = Math.Sqrt(mu / (absSemiMajor * absSemiMajor * absSemiMajor));
        return hyperbolicDelta / hyperbolicMotion;
    }

    static double TrueAnomalyToMeanAnomaly(double trueAnomaly, double eccentricity)
    {
        double eccentricAnomaly = 2.0 * Math.Atan(
            Math.Sqrt((1.0 - eccentricity) / (1.0 + eccentricity)) *
            Math.Tan(trueAnomaly * 0.5)
        );
        return eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly);
    }

    static double TrueAnomalyToHyperbolicMeanAnomaly(double trueAnomaly, double eccentricity)
    {
        double tanhHalfF =
            Math.Sqrt((eccentricity - 1.0) / (eccentricity + 1.0)) *
            Math.Tan(trueAnomaly * 0.5);
        tanhHalfF = Math.Max(-0.999999, Math.Min(0.999999, tanhHalfF));
        double hyperbolicAnomaly = Math.Log((1.0 + tanhHalfF) / (1.0 - tanhHalfF));
        return eccentricity * Math.Sinh(hyperbolicAnomaly) - hyperbolicAnomaly;
    }

    static void AddOrbitPathPoint(
        List<SimVector3> points,
        SimVector3 primaryPosition,
        SimVector3 periapsisDirection,
        SimVector3 sideDirection,
        double semiLatusRectum,
        double eccentricity,
        double trueAnomaly
    )
    {
        double denominator = 1.0 + eccentricity * Math.Cos(trueAnomaly);

        if (Math.Abs(denominator) < 0.000001)
        {
            return;
        }

        double orbitRadius = semiLatusRectum / denominator;

        if (
            double.IsNaN(orbitRadius) ||
            double.IsInfinity(orbitRadius) ||
            orbitRadius <= 0.0
        )
        {
            return;
        }

        SimVector3 direction = periapsisDirection
            .Multiply(Math.Cos(trueAnomaly))
            .Add(sideDirection.Multiply(Math.Sin(trueAnomaly)));

        points.Add(primaryPosition.Add(direction.Multiply(orbitRadius)));
    }

    static double NormalizeAngle(double angle)
    {
        double fullTurn = Math.PI * 2.0;
        double normalizedAngle = angle % fullTurn;

        if (normalizedAngle < 0.0)
        {
            normalizedAngle += fullTurn;
        }

        return normalizedAngle;
    }
}
