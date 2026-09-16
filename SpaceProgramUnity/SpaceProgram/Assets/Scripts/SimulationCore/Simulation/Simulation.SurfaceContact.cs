using System;

partial class Simulation
{
    struct LandedBodyFixedPose
    {
        public SimVector3 comOffsetBodyFixed;
        public SimQuaternion orientationBodyFixed;
        public bool isValid;
    }

    LandedBodyFixedPose CaptureLandedBodyFixedPose(Body primary)
    {
        LandedBodyFixedPose pose = new LandedBodyFixedPose();

        if (primary == null || ship == null)
        {
            return pose;
        }

        SimQuaternion spin = primary.GetSpinRotation();
        SimVector3 comOffsetWorld = ship.GetCenterOfMassWorld().Subtract(primary.position);
        pose.comOffsetBodyFixed = spin.InverseRotate(comOffsetWorld);
        pose.orientationBodyFixed = SimQuaternion
            .Multiply(spin.Conjugate(), ship.orientation)
            .Normalize();
        pose.isValid = true;
        return pose;
    }

    void RestoreLandedBodyFixedPose(Body primary, LandedBodyFixedPose pose)
    {
        if (primary == null || ship == null || !pose.isValid)
        {
            return;
        }

        SimQuaternion spin = primary.GetSpinRotation();
        SimVector3 newCom = primary.position.Add(spin.Rotate(pose.comOffsetBodyFixed));
        ship.orientation = SimQuaternion.Multiply(spin, pose.orientationBodyFixed).Normalize();
        ship.position = newCom.Subtract(ship.orientation.Rotate(ship.centerOfMassLocal));
        ship.SyncHeadingFromOrientation();
    }

    internal void PushShipOutOfSurface(Body surfaceBody)
    {
        if (surfaceBody == null || ship == null || surfaceBody.radius <= 0.0)
        {
            return;
        }

        SimVector3 comWorld = ship.GetCenterOfMassWorld();
        SimVector3 fromBody = comWorld.Subtract(surfaceBody.position);
        double distance = fromBody.Length();

        if (distance <= 1e-9)
        {
            PinShipToSurface(surfaceBody);
            return;
        }

        SimVector3 radial = fromBody.Multiply(1.0 / distance);
        double desired = GetSurfaceRestComDistance(surfaceBody, radial);

        if (distance >= desired - 1e-9)
        {
            ship.lastSurfaceNormal = radial;
            return;
        }

        SimVector3 newCom = surfaceBody.position.Add(radial.Multiply(desired));
        ship.position = newCom.Subtract(ship.orientation.Rotate(ship.centerOfMassLocal));
        ship.lastSurfaceNormal = radial;
    }

    double GetSurfaceRestComDistance(Body surfaceBody, SimVector3 radialOut)
    {
        bool usePadDeckHeight = IsShipOnCovePadContact();
        double bottomExtent = LaunchPadConstants.GetEffectiveBottomExtentMeters(
            ship.GetBottomExtentAlongWorldDirection(radialOut)
        );

        if (usePadDeckHeight)
        {
            return LaunchPadConstants.GetPadRestComDistance(bottomExtent);
        }

        double terrainHeight = PlanetSurfaceContact.GetHeightAboveSphereMeters(
            surfaceBody,
            radialOut
        );
        double comDistance = surfaceBody.radius + terrainHeight + bottomExtent;

        if (!PlanetSurfaceContact.TrySampleMapAt(surfaceBody, radialOut, out _))
        {
            comDistance += LandingConfig.SurfacePad;
        }

        return comDistance;
    }
}
