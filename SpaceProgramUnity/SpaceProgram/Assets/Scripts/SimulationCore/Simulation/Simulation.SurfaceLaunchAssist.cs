using System;

/// <summary>
/// Pad contact: keep the vessel pinned on the rotating surface until liftoff.
/// Attitude, RCS, and main-engine thrust use the normal flight controls.
/// </summary>
partial class Simulation
{
    public bool IsSurfaceLaunchAssistActive()
    {
        return ShouldUseSurfaceLaunchAssist();
    }

    bool ShouldUseSurfaceLaunchAssist()
    {
        return
            ScenarioUsesSurfaceLaunchPad() &&
            ship != null &&
            ship.flightStatus == FlightStatus.Landed;
    }

    bool VesselAllowsPadFlightControls(Vessel vessel)
    {
        return
            vessel != null &&
            (
                vessel.flightStatus == FlightStatus.Flying ||
                (vessel == ship && ShouldUseSurfaceLaunchAssist())
            );
    }

    void HandleSurfaceLaunchAssistStep(double dt)
    {
        if (dt <= 0.0)
        {
            return;
        }

        ExitKeplerRails();

        Body primary = GetActivePrimaryBody();

        if (primary == null || primary.radius <= 0.0)
        {
            return;
        }

        if (!IsPrimaryBodyStaticForLandedAssist(primary))
        {
            AdvanceUniverseBodiesWithoutShip(dt);
        }
        else
        {
            primary.AdvanceSpin(dt);
        }
        SyncLandedVesselToRotatingSurface(primary, ship);

        SimVector3 padNormal = GetBodyFixedSurfaceNormal(primary);

        ProcessVesselStagingRequest(ship);
        ApplyVesselOrientationControl(ship, dt);
        ship.IntegrateOrientation(dt);
        ApplyVesselRcsThrust(ship, dt);

        if (ship.controlState.throttle > 0.01f)
        {
            ApplyVesselMainEngineThrust(ship, dt);
        }
        else
        {
            ship.thrust = SimVector3.Zero();
            ship.thrustAcceleration = SimVector3.Zero();
        }

        PinShipToSurface(primary, padNormal);

        double verticalThrust = Math.Max(0.0, SimVector3.Dot(ship.thrust, padNormal));
        double weight = ship.GetMass() * GetBodySurfaceGravity(primary);
        bool canLift = verticalThrust >= weight * LiftoffThrustMargin;

        if (canLift)
        {
            ship.flightStatus = FlightStatus.Flying;
            ship.landedOnBodyName = string.Empty;
            ship.landedSurfaceDirectionBodyFixed = SimVector3.Zero();
            lastFlightEventMessage = "Liftoff from " + primary.name;

            double mass = ship.GetMass();
            double localGravity = GetBodySurfaceGravity(primary);

            if (mass > 0.0 && verticalThrust > weight * LiftoffThrustMargin)
            {
                double netAccel = verticalThrust / mass - localGravity;

                if (netAccel > 0.0)
                {
                    SimVector3 surfaceVelocity = GetLandedSurfaceVelocity(primary);
                    double liftoffSpeed = Math.Max(1.0, netAccel * Math.Max(dt, 1.0 / 60.0));
                    ship.velocity = surfaceVelocity.Add(
                        padNormal.Multiply(liftoffSpeed)
                    );
                }
            }

            ship.angularVelocity = SimVector3.Zero();
        }
        else
        {
            ship.velocity = GetLandedSurfaceVelocity(primary);
        }

        ship.acceleration = SimVector3.Zero();
        ship.rcsAcceleration = SimVector3.Zero();
        ship.dragAcceleration = SimVector3.Zero();
    }

    bool IsPrimaryBodyStaticForLandedAssist(Body primary)
    {
        return primary != null && primary.isStatic && (sun == null || sun.isStatic);
    }
}
