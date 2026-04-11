using System;

struct Vector2
{
    public double x;
    public double y;

    public Vector2(double x, double y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vector2 Zero()
    {
        return new Vector2(0, 0);
    }

    public Vector2 Add(Vector2 other)
    {
        return new Vector2(
            this.x + other.x,
            this.y + other.y
        );
    }

    public Vector2 Subtract(Vector2 other)
    {
        return new Vector2(
            this.x - other.x,
            this.y - other.y
        );
    }

    public Vector2 Multiply(double value)
    {
        return new Vector2(
            this.x * value,
            this.y * value
        );
    }

    public double LengthSquared()
    {
        return this.x * this.x + this.y * this.y;
    }

    public double Length()
    {
        return Math.Sqrt(LengthSquared());
    }

    public Vector2 Normalize()
    {
        double length = Length();

        if (length == 0)
        {
            return Zero();
        }

        return new Vector2(
            this.x / length,
            this.y / length
        );
    }
}
