using System;

public static class SoiGeometry
{
    public static bool SegmentComesWithinRadius(
        SimVector3 start,
        SimVector3 end,
        SimVector3 center,
        double radius
    )
    {
        if (radius <= 0.0)
        {
            return false;
        }

        double radiusSquared = radius * radius;
        SimVector3 fromCenterToStart = start.Subtract(center);
        SimVector3 fromCenterToEnd = end.Subtract(center);

        if (fromCenterToStart.LengthSquared() <= radiusSquared)
        {
            return true;
        }

        if (fromCenterToEnd.LengthSquared() <= radiusSquared)
        {
            return true;
        }

        SimVector3 span = fromCenterToEnd.Subtract(fromCenterToStart);
        double spanSquared = span.LengthSquared();

        if (spanSquared <= 1e-18)
        {
            return false;
        }

        double along = -SimVector3.Dot(fromCenterToStart, span) / spanSquared;

        if (along <= 0.0 || along >= 1.0)
        {
            return false;
        }

        SimVector3 closest = fromCenterToStart.Add(span.Multiply(along));
        return closest.LengthSquared() <= radiusSquared;
    }

    /// <summary>
    /// Fraction along start→end where the segment first meets the sphere.
    /// Start already inside → a small positive fraction so the caller can switch primary.
    /// </summary>
    public static bool TryFindSphereEntryFraction(
        SimVector3 start,
        SimVector3 end,
        SimVector3 center,
        double radius,
        out double entryFraction
    )
    {
        const double minFraction = 1e-4;
        entryFraction = 1.0;

        if (radius <= 0.0)
        {
            return false;
        }

        SimVector3 fromCenterToStart = start.Subtract(center);
        double radiusSquared = radius * radius;
        double startDistanceSquared = fromCenterToStart.LengthSquared();

        if (startDistanceSquared <= radiusSquared)
        {
            entryFraction = minFraction;
            return true;
        }

        SimVector3 span = end.Subtract(start);
        double spanSquared = span.LengthSquared();

        if (spanSquared <= 1e-18)
        {
            return false;
        }

        double spanDotStart = SimVector3.Dot(fromCenterToStart, span);
        double discriminant =
            spanDotStart * spanDotStart -
            spanSquared * (startDistanceSquared - radiusSquared);

        if (discriminant < 0.0)
        {
            return false;
        }

        double root = Math.Sqrt(discriminant);
        double entry = (-spanDotStart - root) / spanSquared;

        if (entry <= 0.0 || entry > 1.0)
        {
            return false;
        }

        entryFraction = Math.Max(entry, minFraction);
        return true;
    }

    /// <summary>
    /// Kepler rails off while approaching the Moon inside 1.25×SoI (so a long
    /// Earth-coast step cannot chord through the bubble). After Moon→Earth exit,
    /// receding ships may use Earth rails again — otherwise warp stays numerical
    /// and ×50–×1000 barely advances.
    /// </summary>
    public const double MoonNumericalPatchSoiMultiplier = 1.25;

    public static bool HoldKeplerRailsForMoonApproach(
        bool primaryIsMoon,
        double distanceToMoon,
        double moonSphereOfInfluenceRadius,
        double recedingSpeedFromMoon
    )
    {
        if (primaryIsMoon || moonSphereOfInfluenceRadius <= 0.0)
        {
            return false;
        }

        double patchRadius = moonSphereOfInfluenceRadius * MoonNumericalPatchSoiMultiplier;
        if (distanceToMoon > patchRadius)
        {
            return false;
        }

        return recedingSpeedFromMoon <= 0.0;
    }

    public static bool IsFiniteNumber(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// A hop a few tens of km above the Moon must stay lunar. Invalid distance
    /// must not fall through to the Sun (photosphere ejects the ship; Unity Moon
    /// transform becomes NaN).
    /// </summary>
    public static bool IsInsideMoonNearSurfaceLock(double distanceToMoon, double moonRadius)
    {
        return
            IsFiniteNumber(distanceToMoon) &&
            moonRadius > 0.0 &&
            distanceToMoon <= moonRadius * 2.0;
    }

    public static bool HoldMoonPrimaryOnInvalidDistance(
        bool previousPrimaryIsMoon,
        bool moonSurfacePadScenario
    )
    {
        return previousPrimaryIsMoon || moonSurfacePadScenario;
    }

    /// <summary>
    /// False Sun primary while still on/near the Moon pins the ship into the
    /// photosphere (HUD: Body Sun, altitude 0, Moon localPosition NaN).
    /// </summary>
    public static bool IsFalseSunPrimaryInsidePhotosphere(
        double distanceToSun,
        double sunRadius,
        double distanceToMoon,
        double moonRadius
    )
    {
        if (sunRadius <= 0.0 || moonRadius <= 0.0)
        {
            return false;
        }

        if (!IsFiniteNumber(distanceToSun) || !IsFiniteNumber(distanceToMoon))
        {
            return true;
        }

        return
            distanceToSun < sunRadius * 2.0 &&
            distanceToMoon > moonRadius * 2.0;
    }
}

static class SoiTransition
{
    const int MaxBisectionIterations = 28;
    const double BoundaryTolerance = 1e-4;

    public static bool TryFindBoundaryFraction(
        Simulation simulation,
        Body primaryAtStart,
        SimVector3 startPosition,
        SimVector3 startVelocity,
        double dt,
        out double boundaryFraction,
        out Body primaryAfterTransition
    )
    {
        boundaryFraction = 1.0;
        primaryAfterTransition = primaryAtStart;

        if (dt <= 0.0)
        {
            return false;
        }

        simulation.RestoreShipState(startPosition, startVelocity);
        simulation.IntegrateShipGravityOnly(dt);
        Body primaryAtEnd = simulation.ResolveGeometricPrimaryAtPosition(
            simulation.ship.position,
            primaryAtStart
        );

        if (primaryAtEnd == primaryAtStart)
        {
            return false;
        }

        double low = 0.0;
        double high = 1.0;

        for (int iteration = 0; iteration < MaxBisectionIterations; iteration++)
        {
            double mid = 0.5 * (low + high);
            simulation.RestoreShipState(startPosition, startVelocity);
            simulation.IntegrateShipGravityOnly(dt * mid);
            Body primaryAtMid = simulation.ResolveGeometricPrimaryAtPosition(
                simulation.ship.position,
                primaryAtStart
            );

            if (primaryAtMid != primaryAtStart)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }

        boundaryFraction = high;
        primaryAfterTransition = primaryAtEnd;

        if (boundaryFraction <= BoundaryTolerance)
        {
            boundaryFraction = BoundaryTolerance;
        }

        return true;
    }

    public static void TransformShipToPrimary(
        Simulation simulation,
        Body newPrimary
    )
    {
        simulation.SetActivePrimaryForIntegration(newPrimary.name);
    }
}
