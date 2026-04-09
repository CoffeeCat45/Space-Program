using System;

static class Gravity
{
    public static Vector2 CalculateAcceleration(
        Vector2 objectPosition,
        Vector2 sourcePosition,
        double sourceGravityParameter
    )
    {
        // Вектор от объекта к источнику гравитации.
        Vector2 toSource = sourcePosition.Subtract(objectPosition);
        double distance = toSource.Length();

        if (distance == 0)
        {
            return new Vector2(0, 0);
        }

        Vector2 directionToSource = toSource.Normalize();

        // Чем дальше объект от источника, тем слабее гравитация: acceleration = mu / distance^2.
        double accelerationSize = sourceGravityParameter / (distance * distance);

        return directionToSource.Multiply(accelerationSize);
    }

    public static double CalculateCircularOrbitSpeed(double gravityParameter, double radius)
    {
        // Скорость для круговой орбиты: speed = sqrt(mu / radius).
        return Math.Sqrt(gravityParameter / radius);
    }
}
