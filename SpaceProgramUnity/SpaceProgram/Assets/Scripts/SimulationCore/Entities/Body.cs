class Body
{
    public string name;
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 acceleration;
    public double mu;
    public bool isStatic;

    public Body(
        string name,
        Vector2 position,
        Vector2 velocity,
        Vector2 acceleration,
        double mu,
        bool isStatic
    )
    {
        this.name = name;
        this.position = position;
        this.velocity = velocity;
        this.acceleration = acceleration;
        this.mu = mu;
        this.isStatic = isStatic;
    }
}
