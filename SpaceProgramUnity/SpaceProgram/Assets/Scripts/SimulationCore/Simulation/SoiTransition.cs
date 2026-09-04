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
