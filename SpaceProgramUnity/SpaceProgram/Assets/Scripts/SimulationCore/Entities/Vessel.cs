class Vessel : Body
{
    public Vector2 thrust;
    public Vector2 thrustAcceleration;
    public double dryMass;
    public double fuelMass;
    public double fuelCapacity;
    public double fuelConsumptionPerForceSecond;

    public Vessel(string name, Vector2 position, Vector2 velocity)
        : base(name, position, velocity, Vector2.Zero(), 0, 0, false)
    {
        thrust = Vector2.Zero();
        thrustAcceleration = Vector2.Zero();
        dryMass = 10.0;
        fuelMass = 20.0;
        fuelCapacity = 20.0;
        fuelConsumptionPerForceSecond = 0.002;
    }

    public double GetMass()
    {
        return dryMass + fuelMass;
    }

    public double GetFuelRatio()
    {
        if (fuelCapacity <= 0.0)
        {
            return 0.0;
        }

        return fuelMass / fuelCapacity;
    }
}
