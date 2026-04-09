class Body
{
    public Vector2 position;
    public Vector2 velocity;
    public double gravityParameter;
    public bool isStatic;

    public Body(Vector2 position, Vector2 velocity, double gravityParameter, bool isStatic)
    {
        this.position = position;
        this.velocity = velocity;
        this.gravityParameter = gravityParameter;
        this.isStatic = isStatic;
    }
}
