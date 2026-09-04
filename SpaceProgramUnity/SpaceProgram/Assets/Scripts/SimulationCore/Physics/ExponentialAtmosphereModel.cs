using System;

class ExponentialAtmosphereModel : IAtmosphereModel
{
    public double GetDensity(AtmosphereProfile profile, double altitudeAboveSurface)
    {
        if (!profile.HasAtmosphere || altitudeAboveSurface < 0.0)
        {
            return 0.0;
        }

        double rawDensity = Math.Exp(-altitudeAboveSurface / profile.scaleHeight);
        double limitDensity = Math.Exp(-profile.topAltitude / profile.scaleHeight);

        return Math.Max(
            0.0,
            profile.surfaceDensity * (rawDensity - limitDensity)
        );
    }

    public AtmosphericDragResult CalculateDrag(
        AtmosphereProfile profile,
        double altitudeAboveSurface,
        SimVector3 relativeVelocity,
        AerodynamicProperties aerodynamics,
        SimQuaternion shipOrientation,
        double mass
    )
    {
        AtmosphericDragResult result = new AtmosphericDragResult();
        result.density = GetDensity(profile, altitudeAboveSurface);

        if (result.density <= 0.0 || mass <= 0.0)
        {
            return result;
        }

        double speed = relativeVelocity.Length();

        if (speed <= 0.0)
        {
            return result;
        }

        SimVector3 velocityDirection = relativeVelocity.Normalize();
        SimVector3 shipForward = shipOrientation.Forward();
        result.angleOfAttack = Aerodynamics.CalculateAngleOfAttackRadians(
            shipForward,
            velocityDirection
        );
        result.effectiveArea = aerodynamics.forwardArea;

        if (result.effectiveArea <= 0.0)
        {
            return result;
        }

        double liftCoefficient = Aerodynamics.CalculateLiftCoefficient(
            result.angleOfAttack,
            aerodynamics.liftCoeff
        );
        double profileDragCoefficient = Aerodynamics.CalculateProfileDragCoefficient(
            result.angleOfAttack,
            aerodynamics.dragCoeff,
            aerodynamics.forwardArea,
            aerodynamics.sideArea
        );
        double inducedDragCoefficient = Aerodynamics.CalculateInducedDragCoefficient(
            liftCoefficient,
            aerodynamics.inducedDragFactor
        );
        double totalDragCoefficient =
            profileDragCoefficient + inducedDragCoefficient;
        // Dynamic pressure stays in pascals — the heating thresholds compare against real Pa.
        // Forces, however, are consumed alongside tonne masses and kilonewton thrust, so they
        // are converted out of newtons here (see PhysicsConstants.VesselForceUnitsPerNewton).
        result.dynamicPressure = 0.5 * result.density * speed * speed;
        result.dragForceMagnitude =
            result.dynamicPressure * totalDragCoefficient * result.effectiveArea *
            PhysicsConstants.VesselForceUnitsPerNewton;
        result.liftForceMagnitude =
            result.dynamicPressure * liftCoefficient * result.effectiveArea *
            PhysicsConstants.VesselForceUnitsPerNewton;

        SimVector3 dragForce =
            velocityDirection.Multiply(-result.dragForceMagnitude);
        SimVector3 liftForce = Aerodynamics
            .CalculateLiftDirection(velocityDirection)
            .Multiply(result.liftForceMagnitude);
        result.force = dragForce.Add(liftForce);
        result.acceleration = result.force.Multiply(1.0 / mass);
        result.torqueLocal = Aerodynamics.CalculateTorqueLocal(
            aerodynamics.centerOfPressureOffset,
            aerodynamics.centerOfMassLocal,
            result.force,
            shipOrientation
        );
        result.torqueZ = result.torqueLocal.z;

        return result;
    }
}
