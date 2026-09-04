using System;

static class Aerodynamics
{
    public static double CalculateAngleOfAttackRadians(
        SimVector3 shipForward,
        SimVector3 velocityDirection
    )
    {
        double dot = SimVector3.Dot(shipForward, velocityDirection);
        double cross =
            shipForward.x * velocityDirection.y -
            shipForward.y * velocityDirection.x;

        return Math.Atan2(cross, dot);
    }

    public static double CalculateLiftCoefficient(
        double angleOfAttack,
        double liftCoeff
    )
    {
        return liftCoeff * Math.Sin(2.0 * angleOfAttack);
    }

    public static double CalculateInducedDragCoefficient(
        double liftCoefficient,
        double inducedDragFactor
    )
    {
        return inducedDragFactor * liftCoefficient * liftCoefficient;
    }

    public static double CalculateProfileDragCoefficient(
        double angleOfAttack,
        double baseDragCoeff,
        double forwardArea,
        double sideArea
    )
    {
        if (forwardArea <= 0.0)
        {
            return baseDragCoeff;
        }

        double sinAlpha = Math.Sin(angleOfAttack);
        double cosAlpha = Math.Cos(angleOfAttack);
        double sideDragCoeff = baseDragCoeff * Math.Max(1.0, sideArea / forwardArea);
        double sinSquared = sinAlpha * sinAlpha;
        double cosSquared = cosAlpha * cosAlpha;

        return baseDragCoeff * cosSquared + sideDragCoeff * sinSquared;
    }

    public static SimVector3 CalculateLiftDirection(SimVector3 velocityDirection)
    {
        return new SimVector3(
            -velocityDirection.y,
            velocityDirection.x,
            0.0
        ).Normalize();
    }

    public static double CalculateEffectiveArea(
        SimVector3 shipForward,
        SimVector3 shipRight,
        SimVector3 shipUp,
        SimVector3 velocityDirection,
        double forwardArea,
        double sideArea,
        double topArea
    )
    {
        double dotForward = Math.Abs(SimVector3.Dot(shipForward, velocityDirection));
        double dotSide = Math.Abs(SimVector3.Dot(shipRight, velocityDirection));
        double dotUp = Math.Abs(SimVector3.Dot(shipUp, velocityDirection));

        return
            forwardArea * dotForward +
            sideArea * dotSide +
            topArea * dotUp;
    }

    public static SimVector3 CalculateTorqueLocal(
        SimVector3 centerOfPressureLocal,
        SimVector3 centerOfMassLocal,
        SimVector3 forceWorld,
        SimQuaternion orientation
    )
    {
        SimVector3 forceLocal = orientation.InverseRotate(forceWorld);
        SimVector3 arm = centerOfPressureLocal.Subtract(centerOfMassLocal);
        return SimVector3.Cross(arm, forceLocal);
    }

    public static double CalculateYawTorque(
        SimVector3 centerOfPressureOffsetWorld,
        SimVector3 aeroForce
    )
    {
        return
            centerOfPressureOffsetWorld.x * aeroForce.y -
            centerOfPressureOffsetWorld.y * aeroForce.x;
    }
}
