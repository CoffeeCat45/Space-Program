class Vessel : Body
{
    public Vector2 thrust;

    public Vessel(string name, Vector2 position, Vector2 velocity)
        : base(name, position, velocity, Vector2.Zero(), 0, false)
    {
        thrust = Vector2.Zero();
    }
}
