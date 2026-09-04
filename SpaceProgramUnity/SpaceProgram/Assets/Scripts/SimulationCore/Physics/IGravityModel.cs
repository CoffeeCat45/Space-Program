interface IGravityModel
{
    SimVector3 CalculateAcceleration(
        SimVector3 objectPosition,
        SimVector3 sourcePosition,
        double sourceMu
    );

    double CalculateCircularOrbitSpeed(double mu, double radius);
}
