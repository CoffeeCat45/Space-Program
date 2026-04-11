using System;

static class Gravity
{
    // CalculateAcceleration = вычислить ускорение от гравитации.
    public static Vector2 CalculateAcceleration(
        Vector2 objectPosition,
        Vector2 sourcePosition,
        double sourceMu
    )
    {
        Vector2 toSource = sourcePosition.Subtract(objectPosition);
        double distanceSquared = toSource.LengthSquared();

        if (distanceSquared == 0)
        {
            return Vector2.Zero();
        }

        double distance = Math.Sqrt(distanceSquared);
        Vector2 directionToSource = toSource.Multiply(1.0 / distance);

        double accelerationSize = sourceMu / distanceSquared;

        return directionToSource.Multiply(accelerationSize);
    }

    // CalculateCircularOrbitSpeed = вычислить скорость для круговой орбиты.
    public static double CalculateCircularOrbitSpeed(double mu, double radius)
    {
        return Math.Sqrt(mu / radius);
    }
}
