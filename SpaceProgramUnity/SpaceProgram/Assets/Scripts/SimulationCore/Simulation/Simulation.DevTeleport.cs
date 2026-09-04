using System;

/// <summary>Developer-only ship placement helpers (Unity dev menu).</summary>
partial class Simulation
{
    public bool TryDevTeleportShipAboveActivePrimary(
        double targetAltitudeMeters,
        SimVector3 velocityWorld,
        out string error
    )
    {
        if (ship == null)
        {
            error = "Ship missing.";
            return false;
        }

        Body primary = GetActivePrimaryBody();

        if (primary == null)
        {
            error = "Active primary missing.";
            return false;
        }

        if (double.IsNaN(targetAltitudeMeters) || double.IsInfinity(targetAltitudeMeters))
        {
            error = "Invalid altitude.";
            return false;
        }

        targetAltitudeMeters = Math.Max(0.0, targetAltitudeMeters);

        ExitKeplerRails();

        SimVector3 surfaceNormal = GetSurfaceNormal(primary);

        if (surfaceNormal.LengthSquared() <= 1e-12)
        {
            surfaceNormal = GetDefaultLaunchSurfaceNormal();
        }

        double bottomExtent = LaunchPadConstants.GetEffectiveBottomExtentMeters(
            ship.GetBottomExtentAlongWorldDirection(surfaceNormal)
        );
        double terrainHeight = 0.0;

        Body padBody = GetVisualTerrainBody();
        if (
            padBody != null &&
            primary == padBody &&
            ScenarioUsesSurfaceLaunchPad() &&
            targetAltitudeMeters <= 0.01
        )
        {
            terrainHeight = LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters();
        }

        double comDistance =
            primary.radius + terrainHeight + bottomExtent + targetAltitudeMeters;
        SimVector3 comWorld = primary.position.Add(surfaceNormal.Multiply(comDistance));

        ship.position = comWorld.Subtract(
            ship.orientation.Rotate(ship.centerOfMassLocal)
        );
        ship.velocity = velocityWorld;
        ship.thrust = SimVector3.Zero();
        ship.thrustAcceleration = SimVector3.Zero();
        ship.lastSurfaceNormal = surfaceNormal;

        if (targetAltitudeMeters <= 0.01)
        {
            ship.flightStatus = FlightStatus.Landed;
            ship.landedOnBodyName = primary.name;
            PinShipToSurface(primary, surfaceNormal);
        }
        else
        {
            ship.flightStatus = FlightStatus.Flying;
            ship.landedOnBodyName = string.Empty;
            ship.landedSurfaceDirectionBodyFixed = SimVector3.Zero();
        }

        error = string.Empty;
        return true;
    }

    public double GetShipHorizontalSpeedRelativeToActivePrimary()
    {
        Body primary = GetActivePrimaryBody();

        if (primary == null || ship == null)
        {
            return 0.0;
        }

        SimVector3 relativeVelocity = ship.velocity.Subtract(primary.velocity);
        SimVector3 surfaceNormal = GetSurfaceNormal(primary);
        double verticalSpeed = SimVector3.Dot(relativeVelocity, surfaceNormal);
        SimVector3 horizontal = relativeVelocity.Subtract(
            surfaceNormal.Multiply(verticalSpeed)
        );
        return horizontal.Length();
    }
}
