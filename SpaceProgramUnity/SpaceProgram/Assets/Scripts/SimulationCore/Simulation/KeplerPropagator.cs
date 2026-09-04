using System;

static class KeplerPropagator
{
    const double MeanAnomalyTolerance = 1e-10;
    const int MaxKeplerIterations = 24;
    const double ParabolaEccentricitySlop = 1e-8;

    public static bool TryCreateState(
        Body primaryBody,
        SimVector3 shipPosition,
        SimVector3 shipVelocity,
        double simulationTime,
        out KeplerRailsState state
    )
    {
        state = KeplerRailsState.Invalid();

        if (primaryBody == null || primaryBody.mu <= 0.0)
        {
            return false;
        }

        SimVector3 relativePosition =
            shipPosition.Subtract(primaryBody.position);
        SimVector3 relativeVelocity =
            shipVelocity.Subtract(primaryBody.velocity);
        double radius = relativePosition.Length();

        if (radius <= 0.0)
        {
            return false;
        }

        double eccentricity = OrbitalMechanics.GetOrbitalEccentricity(
            relativePosition,
            relativeVelocity,
            primaryBody.mu
        );

        if (Math.Abs(eccentricity - 1.0) < ParabolaEccentricitySlop)
        {
            return false;
        }

        double semiMajorAxis = OrbitalMechanics.GetSemiMajorAxis(
            relativePosition,
            relativeVelocity,
            primaryBody.mu
        );

        bool isHyperbola = eccentricity > 1.0;

        if (isHyperbola)
        {
            if (semiMajorAxis >= 0.0 || double.IsInfinity(semiMajorAxis))
            {
                return false;
            }
        }
        else if (semiMajorAxis <= 0.0 || double.IsInfinity(semiMajorAxis))
        {
            return false;
        }

        double angularMomentum = OrbitalMechanics.GetAngularMomentumMagnitude(
            relativePosition,
            relativeVelocity
        );

        if (angularMomentum <= 1e-8)
        {
            return false;
        }

        double speedSquared = relativeVelocity.LengthSquared();
        double radialVelocity = SimVector3.Dot(relativePosition, relativeVelocity);
        SimVector3 eccentricityVector = relativePosition
            .Multiply((speedSquared - primaryBody.mu / radius) / primaryBody.mu)
            .Subtract(relativeVelocity.Multiply(radialVelocity / primaryBody.mu));
        SimVector3 orbitNormal = GetOrbitNormal(relativePosition, relativeVelocity);
        SimVector3 periapsisDirection =
            eccentricity > 1e-6 ?
            eccentricityVector.Normalize() :
            relativePosition.Normalize();
        SimVector3 sideDirection =
            SimVector3.Cross(orbitNormal, periapsisDirection).Normalize();
        double trueAnomaly = Math.Atan2(
            SimVector3.Dot(relativePosition, sideDirection),
            SimVector3.Dot(relativePosition, periapsisDirection)
        );

        double meanAnomalyAtEpoch;

        if (isHyperbola)
        {
            if (!TryTrueAnomalyToHyperbolicAnomaly(
                trueAnomaly,
                eccentricity,
                out double hyperbolicAnomaly
            ))
            {
                return false;
            }

            meanAnomalyAtEpoch =
                eccentricity * Math.Sinh(hyperbolicAnomaly) - hyperbolicAnomaly;
        }
        else
        {
            double eccentricAnomaly = TrueAnomalyToEccentricAnomaly(
                trueAnomaly,
                eccentricity
            );
            meanAnomalyAtEpoch = EccentricToMeanAnomaly(
                eccentricAnomaly,
                eccentricity
            );
        }

        state.primaryBodyName = primaryBody.name;
        state.epochTime = simulationTime;
        state.meanAnomalyAtEpoch = meanAnomalyAtEpoch;
        state.semiMajorAxis = semiMajorAxis;
        state.eccentricity = eccentricity;
        state.semiLatusRectum = angularMomentum * angularMomentum / primaryBody.mu;
        state.periapsisDirection = periapsisDirection;
        state.sideDirection = sideDirection;
        state.orbitNormal = orbitNormal;
        state.isValid = true;
        return true;
    }

    public static bool TryPropagate(
        ref KeplerRailsState state,
        Body primaryBody,
        double targetSimulationTime,
        out SimVector3 relativePosition,
        out SimVector3 relativeVelocity
    )
    {
        relativePosition = SimVector3.Zero();
        relativeVelocity = SimVector3.Zero();

        if (
            !state.isValid ||
            primaryBody == null ||
            primaryBody.mu <= 0.0 ||
            state.semiLatusRectum <= 0.0
        )
        {
            return false;
        }

        double dt = targetSimulationTime - state.epochTime;
        double trueAnomaly;

        if (state.eccentricity > 1.0)
        {
            double absSemiMajor = Math.Abs(state.semiMajorAxis);

            if (absSemiMajor <= 1e-12)
            {
                return false;
            }

            double meanMotion = Math.Sqrt(
                primaryBody.mu / (absSemiMajor * absSemiMajor * absSemiMajor)
            );
            double meanAnomaly = state.meanAnomalyAtEpoch + meanMotion * dt;
            double hyperbolicAnomaly = SolveHyperbolicAnomaly(
                meanAnomaly,
                state.eccentricity
            );
            trueAnomaly = HyperbolicAnomalyToTrueAnomaly(
                hyperbolicAnomaly,
                state.eccentricity
            );
        }
        else
        {
            if (state.semiMajorAxis <= 0.0)
            {
                return false;
            }

            double meanMotion = Math.Sqrt(
                primaryBody.mu /
                (
                    state.semiMajorAxis *
                    state.semiMajorAxis *
                    state.semiMajorAxis
                )
            );
            double meanAnomaly = NormalizeAngle(
                state.meanAnomalyAtEpoch + meanMotion * dt
            );
            double eccentricAnomaly = SolveEccentricAnomaly(
                meanAnomaly,
                state.eccentricity
            );
            trueAnomaly = EccentricAnomalyToTrueAnomaly(
                eccentricAnomaly,
                state.eccentricity
            );
        }

        double orbitRadius =
            state.semiLatusRectum /
            (1.0 + state.eccentricity * Math.Cos(trueAnomaly));

        if (orbitRadius <= 0.0)
        {
            return false;
        }

        SimVector3 radialDirection = state.periapsisDirection
            .Multiply(Math.Cos(trueAnomaly))
            .Add(state.sideDirection.Multiply(Math.Sin(trueAnomaly)));
        SimVector3 tangentialDirection =
            SimVector3.Cross(state.orbitNormal, radialDirection).Normalize();
        double sqrtMuOverP = Math.Sqrt(primaryBody.mu / state.semiLatusRectum);
        double radialVelocity =
            sqrtMuOverP * state.eccentricity * Math.Sin(trueAnomaly);
        double tangentialVelocity =
            sqrtMuOverP * (1.0 + state.eccentricity * Math.Cos(trueAnomaly));

        relativePosition = radialDirection.Multiply(orbitRadius);
        relativeVelocity = radialDirection
            .Multiply(radialVelocity)
            .Add(tangentialDirection.Multiply(tangentialVelocity));
        return true;
    }

    static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
    {
        double eccentricAnomaly = eccentricity < 0.8 ?
            meanAnomaly :
            Math.PI;

        for (int iteration = 0; iteration < MaxKeplerIterations; iteration++)
        {
            double delta = eccentricAnomaly -
                eccentricity * Math.Sin(eccentricAnomaly) -
                meanAnomaly;
            double derivative = 1.0 - eccentricity * Math.Cos(eccentricAnomaly);

            if (Math.Abs(derivative) < 1e-12)
            {
                break;
            }

            eccentricAnomaly -= delta / derivative;

            if (Math.Abs(delta) < MeanAnomalyTolerance)
            {
                break;
            }
        }

        return eccentricAnomaly;
    }

    static double SolveHyperbolicAnomaly(double meanAnomaly, double eccentricity)
    {
        double absMean = Math.Abs(meanAnomaly);
        double hyperbolicAnomaly = absMean < 0.5 ?
            meanAnomaly / Math.Max(eccentricity - 1.0, 1e-6) :
            Math.Sign(meanAnomaly) * Math.Log(2.0 * absMean / eccentricity + 1.8);

        if (meanAnomaly == 0.0)
        {
            hyperbolicAnomaly = 0.0;
        }

        for (int iteration = 0; iteration < MaxKeplerIterations; iteration++)
        {
            double residual =
                eccentricity * Math.Sinh(hyperbolicAnomaly) -
                hyperbolicAnomaly -
                meanAnomaly;
            double derivative = eccentricity * Math.Cosh(hyperbolicAnomaly) - 1.0;

            if (Math.Abs(derivative) < 1e-12)
            {
                break;
            }

            hyperbolicAnomaly -= residual / derivative;

            if (Math.Abs(residual) < MeanAnomalyTolerance)
            {
                break;
            }
        }

        return hyperbolicAnomaly;
    }

    static double EccentricToMeanAnomaly(double eccentricAnomaly, double eccentricity)
    {
        return NormalizeAngle(
            eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly)
        );
    }

    static double TrueAnomalyToEccentricAnomaly(double trueAnomaly, double eccentricity)
    {
        return 2.0 * Math.Atan2(
            Math.Sqrt(1.0 - eccentricity) * Math.Sin(trueAnomaly * 0.5),
            Math.Sqrt(1.0 + eccentricity) * Math.Cos(trueAnomaly * 0.5)
        );
    }

    static bool TryTrueAnomalyToHyperbolicAnomaly(
        double trueAnomaly,
        double eccentricity,
        out double hyperbolicAnomaly
    )
    {
        hyperbolicAnomaly = 0.0;
        double denom = 1.0 + eccentricity * Math.Cos(trueAnomaly);

        if (denom <= 1e-12)
        {
            return false;
        }

        double coshF = (eccentricity + Math.Cos(trueAnomaly)) / denom;
        double sinhF =
            Math.Sqrt(eccentricity * eccentricity - 1.0) *
            Math.Sin(trueAnomaly) /
            denom;
        double expF = coshF + sinhF;

        if (expF <= 1e-18)
        {
            return false;
        }

        hyperbolicAnomaly = Math.Log(expF);
        return true;
    }

    static double HyperbolicAnomalyToTrueAnomaly(
        double hyperbolicAnomaly,
        double eccentricity
    )
    {
        double coshF = Math.Cosh(hyperbolicAnomaly);
        double denom = eccentricity * coshF - 1.0;

        if (Math.Abs(denom) < 1e-12)
        {
            return 0.0;
        }

        double sinTrue =
            Math.Sqrt(eccentricity * eccentricity - 1.0) *
            Math.Sinh(hyperbolicAnomaly) /
            denom;
        double cosTrue = (eccentricity - coshF) / denom;
        return Math.Atan2(sinTrue, cosTrue);
    }

    static double EccentricAnomalyToTrueAnomaly(
        double eccentricAnomaly,
        double eccentricity
    )
    {
        double sinE = Math.Sin(eccentricAnomaly);
        double cosE = Math.Cos(eccentricAnomaly);
        return Math.Atan2(
            Math.Sqrt(1.0 - eccentricity * eccentricity) * sinE,
            cosE - eccentricity
        );
    }

    static SimVector3 GetOrbitNormal(
        SimVector3 relativePosition,
        SimVector3 relativeVelocity
    )
    {
        SimVector3 orbitNormal = SimVector3.Cross(relativePosition, relativeVelocity);

        if (orbitNormal.LengthSquared() <= 1e-12)
        {
            return SimVector3.UnitZ();
        }

        return orbitNormal.Normalize();
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
