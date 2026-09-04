using System;

public struct SimQuaternion
{
    public double w;
    public double x;
    public double y;
    public double z;

    public SimQuaternion(double w, double x, double y, double z)
    {
        this.w = w;
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static SimQuaternion Identity()
    {
        return new SimQuaternion(1, 0, 0, 0);
    }

    public static SimQuaternion FromYawRadians(double yaw)
    {
        double halfYaw = yaw * 0.5;
        return new SimQuaternion(
            Math.Cos(halfYaw),
            0,
            Math.Sin(halfYaw),
            0
        );
    }

    public static SimQuaternion Multiply(SimQuaternion left, SimQuaternion right)
    {
        return new SimQuaternion(
            left.w * right.w - left.x * right.x - left.y * right.y - left.z * right.z,
            left.w * right.x + left.x * right.w + left.y * right.z - left.z * right.y,
            left.w * right.y - left.x * right.z + left.y * right.w + left.z * right.x,
            left.w * right.z + left.x * right.y - left.y * right.x + left.z * right.w
        );
    }

    public SimQuaternion Normalize()
    {
        double lengthSquared = w * w + x * x + y * y + z * z;

        if (lengthSquared <= 1e-18)
        {
            return Identity();
        }

        double invLength = 1.0 / Math.Sqrt(lengthSquared);
        return new SimQuaternion(
            w * invLength,
            x * invLength,
            y * invLength,
            z * invLength
        );
    }

    public static SimQuaternion FromAxisAngle(SimVector3 axis, double angleRadians)
    {
        if (axis.LengthSquared() <= 1e-18)
        {
            return Identity();
        }

        SimVector3 normalizedAxis = axis.Normalize();
        double halfAngle = angleRadians * 0.5;
        double sinHalf = Math.Sin(halfAngle);

        return new SimQuaternion(
            Math.Cos(halfAngle),
            normalizedAxis.x * sinHalf,
            normalizedAxis.y * sinHalf,
            normalizedAxis.z * sinHalf
        );
    }

    public SimVector3 Rotate(SimVector3 value)
    {
        SimQuaternion valueQuaternion = new SimQuaternion(0, value.x, value.y, value.z);
        SimQuaternion rotated = Multiply(Multiply(this, valueQuaternion), Conjugate());
        return new SimVector3(rotated.x, rotated.y, rotated.z);
    }

    public SimVector3 Forward()
    {
        return Rotate(new SimVector3(0, 0, 1));
    }

    public SimVector3 Right()
    {
        return Rotate(new SimVector3(1, 0, 0));
    }

    public SimVector3 Up()
    {
        return Rotate(new SimVector3(0, 1, 0));
    }

    public static SimQuaternion FromForwardAndUp(
        SimVector3 forwardWorld,
        SimVector3 upReferenceWorld
    )
    {
        SimVector3 forward = forwardWorld.LengthSquared() > 1e-18 ?
            forwardWorld.Normalize() :
            SimVector3.UnitZ();
        SimVector3 upReference = upReferenceWorld.LengthSquared() > 1e-18 ?
            upReferenceWorld.Normalize() :
            SimVector3.UnitY();
        SimVector3 right = SimVector3.Cross(upReference, forward);

        if (right.LengthSquared() <= 1e-18)
        {
            return FromYawRadians(Math.Atan2(forward.y, forward.x));
        }

        right = right.Normalize();
        SimVector3 up = SimVector3.Cross(forward, right).Normalize();
        return FromOrthonormalBasis(forward, right, up);
    }

    static SimQuaternion FromOrthonormalBasis(
        SimVector3 forwardWorld,
        SimVector3 rightWorld,
        SimVector3 upWorld
    )
    {
        // Body +X -> right, +Y -> up, +Z -> forward (KSP basis).
        double m00 = rightWorld.x;
        double m01 = upWorld.x;
        double m02 = forwardWorld.x;
        double m10 = rightWorld.y;
        double m11 = upWorld.y;
        double m12 = forwardWorld.y;
        double m20 = rightWorld.z;
        double m21 = upWorld.z;
        double m22 = forwardWorld.z;
        double trace = m00 + m11 + m22;

        if (trace > 0.0)
        {
            double s = Math.Sqrt(trace + 1.0) * 2.0;
            return new SimQuaternion(
                0.25 * s,
                (m21 - m12) / s,
                (m02 - m20) / s,
                (m10 - m01) / s
            ).Normalize();
        }

        if (m00 > m11 && m00 > m22)
        {
            double s = Math.Sqrt(1.0 + m00 - m11 - m22) * 2.0;
            return new SimQuaternion(
                (m21 - m12) / s,
                0.25 * s,
                (m01 + m10) / s,
                (m02 + m20) / s
            ).Normalize();
        }

        if (m11 > m22)
        {
            double s = Math.Sqrt(1.0 + m11 - m00 - m22) * 2.0;
            return new SimQuaternion(
                (m02 - m20) / s,
                (m01 + m10) / s,
                0.25 * s,
                (m12 + m21) / s
            ).Normalize();
        }

        double sZ = Math.Sqrt(1.0 + m22 - m00 - m11) * 2.0;
        return new SimQuaternion(
            (m10 - m01) / sZ,
            (m02 + m20) / sZ,
            (m12 + m21) / sZ,
            0.25 * sZ
        ).Normalize();
    }

    public SimQuaternion Conjugate()
    {
        return new SimQuaternion(w, -x, -y, -z);
    }

    public SimVector3 InverseRotate(SimVector3 value)
    {
        return Conjugate().Rotate(value);
    }
}
