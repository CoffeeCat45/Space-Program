using System;

public struct SimVector3
{
    public double x;
    public double y;
    public double z;

    public SimVector3(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static SimVector3 Zero()
    {
        return new SimVector3(0, 0, 0);
    }

    public static SimVector3 UnitX()
    {
        return new SimVector3(1, 0, 0);
    }

    public static SimVector3 UnitY()
    {
        return new SimVector3(0, 1, 0);
    }

    public static SimVector3 UnitZ()
    {
        return new SimVector3(0, 0, 1);
    }

    public SimVector3 Add(SimVector3 other)
    {
        return new SimVector3(
            x + other.x,
            y + other.y,
            z + other.z
        );
    }

    public SimVector3 Subtract(SimVector3 other)
    {
        return new SimVector3(
            x - other.x,
            y - other.y,
            z - other.z
        );
    }

    public SimVector3 Multiply(double value)
    {
        return new SimVector3(
            x * value,
            y * value,
            z * value
        );
    }

    public double LengthSquared()
    {
        return x * x + y * y + z * z;
    }

    public double Length()
    {
        return Math.Sqrt(LengthSquared());
    }

    public SimVector3 Normalize()
    {
        double length = Length();

        if (length == 0)
        {
            return Zero();
        }

        return Multiply(1.0 / length);
    }

    public static double Dot(SimVector3 left, SimVector3 right)
    {
        return
            left.x * right.x +
            left.y * right.y +
            left.z * right.z;
    }

    public static SimVector3 Cross(SimVector3 left, SimVector3 right)
    {
        return new SimVector3(
            left.y * right.z - left.z * right.y,
            left.z * right.x - left.x * right.z,
            left.x * right.y - left.y * right.x
        );
    }

    public static SimVector3 ClampMagnitude(SimVector3 vector, double maxMagnitude)
    {
        if (maxMagnitude <= 0.0)
        {
            return Zero();
        }

        double lengthSquared = vector.LengthSquared();
        double maxSquared = maxMagnitude * maxMagnitude;

        if (lengthSquared <= maxSquared || lengthSquared <= 1e-24)
        {
            return vector;
        }

        return vector.Multiply(maxMagnitude / Math.Sqrt(lengthSquared));
    }
}
