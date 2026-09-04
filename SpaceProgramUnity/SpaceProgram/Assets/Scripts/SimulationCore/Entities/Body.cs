using System;

class Body
{
    public string name;
    public SimVector3 position;
    public SimVector3 velocity;
    public SimVector3 acceleration;
    public double mu;
    public double radius;
    public bool isStatic;
    /// <summary>True for Sun: infinite SOI; Laplace radius is not defined.</summary>
    public bool isRootBody;
    public double sphereOfInfluenceRadius;
    public AtmosphereProfile atmosphereProfile;
    public double spinRadiansPerSecond;
    public double spinAngleRadians;
    public SimVector3 spinAxis;

    public Body(
        string name,
        SimVector3 position,
        SimVector3 velocity,
        SimVector3 acceleration,
        double mu,
        double radius,
        bool isStatic
    )
    {
        this.name = name;
        this.position = position;
        this.velocity = velocity;
        this.acceleration = acceleration;
        this.mu = mu;
        this.radius = radius;
        this.isStatic = isStatic;
        atmosphereProfile = AtmosphereProfile.None;
        spinRadiansPerSecond = 0.0;
        spinAngleRadians = 0.0;
        spinAxis = SimVector3.UnitY();
    }

    public void AdvanceSpin(double dt)
    {
        if (dt <= 0.0 || Math.Abs(GetEffectiveSpinRadiansPerSecond()) <= 1e-12)
        {
            return;
        }

        spinAngleRadians += GetEffectiveSpinRadiansPerSecond() * dt;
    }

    public SimQuaternion GetSpinRotation()
    {
        if (spinAxis.LengthSquared() <= 1e-12)
        {
            return SimQuaternion.FromAxisAngle(SimVector3.UnitY(), spinAngleRadians);
        }

        return SimQuaternion.FromAxisAngle(spinAxis.Normalize(), spinAngleRadians);
    }

    public SimVector3 RotateBodyFixedToWorld(SimVector3 bodyFixedDirection)
    {
        return GetSpinRotation().Rotate(bodyFixedDirection);
    }

    public SimVector3 GetSurfaceLinearVelocity(SimVector3 worldPosition)
    {
        if (Math.Abs(GetEffectiveSpinRadiansPerSecond()) <= 1e-12)
        {
            return SimVector3.Zero();
        }

        SimVector3 axis = spinAxis.LengthSquared() > 1e-12 ?
            spinAxis.Normalize() :
            SimVector3.UnitY();
        SimVector3 radiusVector = worldPosition.Subtract(position);

        return SimVector3.Cross(
            axis.Multiply(GetEffectiveSpinRadiansPerSecond()),
            radiusVector
        );
    }

    double GetEffectiveSpinRadiansPerSecond()
    {
        if (SimulationCheatFlags.PlanetSpinDisabled && name == CelestialBody.Earth.Name)
        {
            return 0.0;
        }

        return spinRadiansPerSecond;
    }

    /// <summary>Body inertial velocity plus eastward spin at a surface world position.</summary>
    public SimVector3 GetAbsoluteSurfaceVelocityAt(SimVector3 worldPosition)
    {
        return velocity.Add(GetSurfaceLinearVelocity(worldPosition));
    }
}
