static class Integrator
{
    public static void StepPosition(Body body, SimVector3 acceleration, double dt)
    {
        if (body.isStatic)
        {
            return;
        }

        SimVector3 velocityPart = body.velocity.Multiply(dt);
        SimVector3 accelerationPart = acceleration.Multiply(0.5 * dt * dt);
        SimVector3 movement = velocityPart.Add(accelerationPart);

        body.position = body.position.Add(movement);
    }

    public static void StepVelocity(
        Body body,
        SimVector3 oldAcceleration,
        SimVector3 newAcceleration,
        double dt
    )
    {
        if (body.isStatic)
        {
            return;
        }

        SimVector3 averageAcceleration = oldAcceleration.Add(newAcceleration).Multiply(0.5);
        body.velocity = body.velocity.Add(averageAcceleration.Multiply(dt));
    }
}
