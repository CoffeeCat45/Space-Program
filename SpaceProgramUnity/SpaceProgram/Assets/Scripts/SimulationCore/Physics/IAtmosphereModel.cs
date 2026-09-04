interface IAtmosphereModel
{
    double GetDensity(AtmosphereProfile profile, double altitudeAboveSurface);

    AtmosphericDragResult CalculateDrag(
        AtmosphereProfile profile,
        double altitudeAboveSurface,
        SimVector3 relativeVelocity,
        AerodynamicProperties aerodynamics,
        SimQuaternion shipOrientation,
        double mass
    );
}
