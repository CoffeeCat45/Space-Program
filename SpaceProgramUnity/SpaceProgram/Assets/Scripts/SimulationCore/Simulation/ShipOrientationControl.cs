using System;

static class ShipOrientationControl
{
    public static SimVector3 GetTargetDirection(
        Simulation simulation,
        ShipOrientationInput input
    )
    {
        if (input.mode == ShipOrientationMode.Prograde)
        {
            if (simulation.IsShipInAtmosphere())
            {
                return simulation.GetShipProgradeDirectionForAtmosphere();
            }

            return simulation.GetShipProgradeDirectionRelativeToActivePrimary();
        }

        if (input.mode == ShipOrientationMode.Retrograde)
        {
            if (simulation.IsShipInAtmosphere())
            {
                return simulation.GetShipRetrogradeDirectionForAtmosphere();
            }

            return simulation.GetShipRetrogradeDirectionRelativeToActivePrimary();
        }

        if (
            input.mode == ShipOrientationMode.Maneuver &&
            input.maneuverTargetDirection.LengthSquared() > 1e-12
        )
        {
            return input.maneuverTargetDirection.Normalize();
        }

        if (input.mode == ShipOrientationMode.RadialPlus)
        {
            return simulation.GetShipRadialPlusDirectionRelativeToActivePrimary();
        }

        if (input.mode == ShipOrientationMode.RadialMinus)
        {
            return simulation.GetShipRadialMinusDirectionRelativeToActivePrimary();
        }

        if (input.mode == ShipOrientationMode.TargetAlignment)
        {
            return simulation.GetShipTargetAlignmentDirection();
        }

        return SimVector3.Zero();
    }

    public static double NormalizeAngleRadians(double angleRadians)
    {
        while (angleRadians > Math.PI)
        {
            angleRadians -= 2.0 * Math.PI;
        }

        while (angleRadians < -Math.PI)
        {
            angleRadians += 2.0 * Math.PI;
        }

        return angleRadians;
    }
}
