static class Gravity
{
    static readonly IGravityModel DefaultModel = new NewtonianGravityModel();

    public static SimVector3 CalculateAcceleration(
        SimVector3 objectPosition,
        SimVector3 sourcePosition,
        double sourceMu
    )
    {
        return DefaultModel.CalculateAcceleration(
            objectPosition,
            sourcePosition,
            sourceMu
        );
    }

    public static double CalculateCircularOrbitSpeed(double mu, double radius)
    {
        return DefaultModel.CalculateCircularOrbitSpeed(mu, radius);
    }
}
