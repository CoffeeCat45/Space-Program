static class PhysicsConstants
{
    public const double StandardGravity = 9.80665;

    // Жёсткий предел ω после потери массы/инерции (рад/с), C18.
    public const double MaxPhysicsAngularRate = 5.0;

    /// <summary>
    /// Vessel mass is carried in tonnes and engine thrust in kilonewtons, so acceleration is
    /// force/mass without extra factors. Aerodynamic pressure is honest SI (kg/m³ × m²), which
    /// yields newtons — scale those by this before dividing by a tonne mass or a tonne·m²
    /// moment of inertia, or drag and drag torque come out 1000× too strong.
    /// </summary>
    public const double VesselForceUnitsPerNewton = 0.001;
}
