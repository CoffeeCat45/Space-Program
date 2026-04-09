static class Integrator
{
    public static void StepPosition(Body body, Vector2 acceleration, double dt)
    {
        if (body.isStatic)
        {
            return;
        }

        // Velocity Verlet: сначала двигаем позицию по текущей скорости и ускорению.
        Vector2 velocityPart = body.velocity.Multiply(dt);
        Vector2 accelerationPart = acceleration.Multiply(0.5 * dt * dt);
        Vector2 movement = velocityPart.Add(accelerationPart);

        body.position = body.position.Add(movement);
    }

    public static void StepVelocity(
        Body body,
        Vector2 oldAcceleration,
        Vector2 newAcceleration,
        double dt
    )
    {
        if (body.isStatic)
        {
            return;
        }

        // Скорость меняем по среднему ускорению за шаг.
        Vector2 averageAcceleration = oldAcceleration.Add(newAcceleration).Multiply(0.5);
        body.velocity = body.velocity.Add(averageAcceleration.Multiply(dt));
    }
}
