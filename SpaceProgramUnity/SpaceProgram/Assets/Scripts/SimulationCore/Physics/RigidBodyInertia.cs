using System;
using System.Collections.Generic;

struct RigidBodyInertia
{
    public double ixx;
    public double iyy;
    public double izz;
    public double ixy;
    public double ixz;
    public double iyz;

    public static RigidBodyInertia Zero()
    {
        return new RigidBodyInertia();
    }

    public static RigidBodyInertia Add(RigidBodyInertia left, RigidBodyInertia right)
    {
        return new RigidBodyInertia
        {
            ixx = left.ixx + right.ixx,
            iyy = left.iyy + right.iyy,
            izz = left.izz + right.izz,
            ixy = left.ixy + right.ixy,
            ixz = left.ixz + right.ixz,
            iyz = left.iyz + right.iyz
        };
    }

    public static RigidBodyInertia ComputeBoxAboutCenter(
        double mass,
        double lengthX,
        double lengthY,
        double lengthZ
    )
    {
        double xx = mass * (lengthY * lengthY + lengthZ * lengthZ) / 12.0;
        double yy = mass * (lengthX * lengthX + lengthZ * lengthZ) / 12.0;
        double zz = mass * (lengthX * lengthX + lengthY * lengthY) / 12.0;

        return new RigidBodyInertia
        {
            ixx = xx,
            iyy = yy,
            izz = zz
        };
    }

    public static RigidBodyInertia ComputeCylinderAboutCenterZ(
        double mass,
        double lengthZ,
        double diameterXY
    )
    {
        double radius = diameterXY * 0.5;
        double radiusSquared = radius * radius;
        double lengthSquared = lengthZ * lengthZ;

        return new RigidBodyInertia
        {
            ixx = mass * (3.0 * radiusSquared + lengthSquared) / 12.0,
            iyy = mass * (3.0 * radiusSquared + lengthSquared) / 12.0,
            izz = 0.5 * mass * radiusSquared
        };
    }

    public static RigidBodyInertia ShiftByOffset(
        RigidBodyInertia inertiaAboutCenter,
        double mass,
        SimVector3 offsetFromCom
    )
    {
        double distanceSquared = offsetFromCom.LengthSquared();
        double x = offsetFromCom.x;
        double y = offsetFromCom.y;
        double z = offsetFromCom.z;

        return new RigidBodyInertia
        {
            ixx = inertiaAboutCenter.ixx + mass * (distanceSquared - x * x),
            iyy = inertiaAboutCenter.iyy + mass * (distanceSquared - y * y),
            izz = inertiaAboutCenter.izz + mass * (distanceSquared - z * z),
            ixy = inertiaAboutCenter.ixy - mass * x * y,
            ixz = inertiaAboutCenter.ixz - mass * x * z,
            iyz = inertiaAboutCenter.iyz - mass * y * z
        };
    }

    public static RigidBodyInertia Compute(
        List<StageData> stages,
        SimVector3 centerOfMassLocal
    )
    {
        RigidBodyInertia total = Zero();

        if (stages == null)
        {
            return total;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];

            if (stage.blocks == null)
            {
                continue;
            }

            for (int b = 0; b < stage.blocks.Count; b++)
            {
                ShipBlock block = stage.blocks[b];

                if (block.isDestroyed)
                {
                    continue;
                }

                double mass = block.GetMass();

                if (mass <= 0.0)
                {
                    continue;
                }

                SimVector3 blockCenter = stage.stageMountOffset.Add(block.localOffset);
                SimVector3 offsetFromCom = blockCenter.Subtract(centerOfMassLocal);
                RigidBodyInertia blockInertia;

                if (
                    block.blockType == ShipBlockType.FuelTank ||
                    block.blockType == ShipBlockType.Engine
                )
                {
                    blockInertia = ComputeCylinderAboutCenterZ(
                        mass,
                        block.dimensions.z,
                        Math.Max(block.dimensions.x, block.dimensions.y)
                    );
                }
                else
                {
                    blockInertia = ComputeBoxAboutCenter(
                        mass,
                        block.dimensions.x,
                        block.dimensions.y,
                        block.dimensions.z
                    );
                }

                total = Add(
                    total,
                    ShiftByOffset(blockInertia, mass, offsetFromCom)
                );
            }
        }

        return ClampMinimumDiagonal(total, 0.001);
    }

    static RigidBodyInertia ClampMinimumDiagonal(
        RigidBodyInertia inertia,
        double minimum
    )
    {
        inertia.ixx = Math.Max(inertia.ixx, minimum);
        inertia.iyy = Math.Max(inertia.iyy, minimum);
        inertia.izz = Math.Max(inertia.izz, minimum);
        return inertia;
    }

    public SimVector3 Multiply(SimVector3 omega)
    {
        return new SimVector3(
            ixx * omega.x + ixy * omega.y + ixz * omega.z,
            ixy * omega.x + iyy * omega.y + iyz * omega.z,
            ixz * omega.x + iyz * omega.y + izz * omega.z
        );
    }

    public SimVector3 SolveAngularAcceleration(
        SimVector3 torque,
        SimVector3 omega
    )
    {
        SimVector3 gyroscopic = SimVector3.Cross(omega, Multiply(omega));
        SimVector3 rhs = torque.Subtract(gyroscopic);
        return InvertSymmetric().Multiply(rhs);
    }

    public SimVector3 ApplyAngularImpulse(SimVector3 angularImpulse)
    {
        return InvertSymmetric().Multiply(angularImpulse);
    }

    RigidBodyInertia InvertSymmetric()
    {
        double det =
            ixx * (iyy * izz - iyz * iyz) -
            ixy * (ixy * izz - ixz * iyz) +
            ixz * (ixy * iyz - iyy * ixz);

        if (Math.Abs(det) < 1e-12)
        {
            return new RigidBodyInertia
            {
                ixx = 1.0 / Math.Max(ixx, 0.001),
                iyy = 1.0 / Math.Max(iyy, 0.001),
                izz = 1.0 / Math.Max(izz, 0.001)
            };
        }

        double invDet = 1.0 / det;

        return new RigidBodyInertia
        {
            ixx = (iyy * izz - iyz * iyz) * invDet,
            iyy = (ixx * izz - ixz * ixz) * invDet,
            izz = (ixx * iyy - ixy * ixy) * invDet,
            ixy = (ixz * iyz - ixy * izz) * invDet,
            ixz = (ixy * iyz - ixz * iyy) * invDet,
            iyz = (ixy * ixz - ixx * iyz) * invDet
        };
    }
}
