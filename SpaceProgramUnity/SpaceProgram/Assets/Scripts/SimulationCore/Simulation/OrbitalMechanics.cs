using System;
using System.Collections.Generic;

static class OrbitalMechanics
{
    const double OrbitPathNearCircularEccentricity = 0.001;
    const double HyperbolicPathMinRadiansPerSegment = 0.3 * Math.PI / 180.0;
    const int HyperbolicPathMaxSamples = 2048;

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

    public static SimVector3 GetCircularizeDeltaV(
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

    public static SimVector3[] BuildOrbitPathRelativeToBody(
        Body primaryBody,
        SimVector3 orbitingPosition,
        SimVector3 orbitingVelocity,
        int pointCount
    )
    {
        if (pointCount < 2 || primaryBody.mu <= 0)
        {
            return Array.Empty<SimVector3>();
        }

        SimVector3 relativePosition = orbitingPosition.Subtract(primaryBody.position);
        SimVector3 relativeVelocity = orbitingVelocity.Subtract(primaryBody.velocity);
        double radius = relativePosition.Length();

        if (radius == 0)
        {
            return Array.Empty<SimVector3>();
        }

        double angularMomentum = GetAngularMomentumMagnitude(
            relativePosition,
            relativeVelocity
        );

        if (angularMomentum < 0.000001)
        {
            return Array.Empty<SimVector3>();
        }

        double speedSquared = relativeVelocity.LengthSquared();
        double radialVelocity = SimVector3.Dot(relativePosition, relativeVelocity);
        SimVector3 eccentricityVector = relativePosition
            .Multiply((speedSquared - primaryBody.mu / radius) / primaryBody.mu)
            .Subtract(relativeVelocity.Multiply(radialVelocity / primaryBody.mu));
        double eccentricity = eccentricityVector.Length();
        double semiLatusRectum =
            angularMomentum * angularMomentum / primaryBody.mu;

        if (semiLatusRectum <= 0)
        {
            return Array.Empty<SimVector3>();
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
        SimVector3 sideDirection = SimVector3.Cross(orbitNormal, periapsisDirection).Normalize();

        List<SimVector3> points = new List<SimVector3>();

        if (pathEccentricity < 1.0)
        {
            for (int i = 0; i <= pointCount; i++)
            {
                double trueAnomaly =
                    (Math.PI * 2.0 * i) / pointCount;
                AddOrbitPathPoint(
                    points,
                    primaryBody.position,
                    periapsisDirection,
                    sideDirection,
                    semiLatusRectum,
                    pathEccentricity,
                    trueAnomaly
                );
            }
        }
        else
        {
            double maxTrueAnomaly = pathEccentricity == 1.0 ?
                Math.PI * 0.85 :
                Math.Acos(-1.0 / pathEccentricity) * 0.92;

            double shipTrueAnomaly = Math.Atan2(
                SimVector3.Dot(relativePosition, sideDirection),
                SimVector3.Dot(relativePosition, periapsisDirection)
            );

            double minTrueAnomaly = FindSurfaceLimitedTrueAnomaly(
                semiLatusRectum,
                pathEccentricity,
                primaryBody.radius,
                shipTrueAnomaly,
                -1,
                -maxTrueAnomaly
            );
            double maxTrueAnomalyVisible = FindSurfaceLimitedTrueAnomaly(
                semiLatusRectum,
                pathEccentricity,
                primaryBody.radius,
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
                    primaryBody.position,
                    periapsisDirection,
                    sideDirection,
                    semiLatusRectum,
                    pathEccentricity,
                    trueAnomaly
                );
            }
        }

        return points.ToArray();
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
