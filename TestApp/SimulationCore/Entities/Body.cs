class Body
{
    public string name;
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 acceleration;
    public double mu;
    public double radius;
    public bool isStatic;

    public Body(
        string name,
        Vector2 position,
        Vector2 velocity,
        Vector2 acceleration,
        double mu,
        double radius,
        bool isStatic
    )
    {
        this.name = name;
        this.position = position;
        this.velocity = velocity;
        this.acceleration = acceleration;
        this.mu = mu;
        this.radius = radius;
        this.isStatic = isStatic;
    }
}
