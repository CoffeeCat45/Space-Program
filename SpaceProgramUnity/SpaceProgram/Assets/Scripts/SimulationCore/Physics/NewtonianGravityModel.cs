using System;

class NewtonianGravityModel : IGravityModel
{
    public SimVector3 CalculateAcceleration(
        SimVector3 objectPosition,
        SimVector3 sourcePosition,
        double sourceMu
    )
    {
        SimVector3 toSource = sourcePosition.Subtract(objectPosition);
        double distanceSquared = toSource.LengthSquared();

        if (distanceSquared == 0)
        {
            return SimVector3.Zero();
        }

        double distance = Math.Sqrt(distanceSquared);
        SimVector3 directionToSource = toSource.Multiply(1.0 / distance);
        double accelerationSize = sourceMu / distanceSquared;

        return directionToSource.Multiply(accelerationSize);
    }

    public double CalculateCircularOrbitSpeed(double mu, double radius)
    {
        return Math.Sqrt(mu / radius);
    }
}
