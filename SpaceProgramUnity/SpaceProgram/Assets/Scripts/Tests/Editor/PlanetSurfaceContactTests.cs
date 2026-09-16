using System;
using NUnit.Framework;

public sealed class PlanetSurfaceContactTests
{
    [Test]
    public void MissingMap_ReportsZeroHeight()
    {
        Body earth = CreateBody(CelestialBody.Earth.Name);
        Assert.AreEqual(
            0.0,
            PlanetSurfaceContact.GetHeightAboveSphereMeters(earth, SimVector3.UnitY()),
            1e-12
        );
        Assert.IsFalse(
            PlanetSurfaceContact.TrySampleMapAt(earth, SimVector3.UnitY(), out _)
        );
    }

    [Test]
    public void Earth_ClampsOceanFloor_MoonKeepsSignedHeight()
    {
        Assert.IsTrue(
            PlanetSurfaceContact.TreatNegativeElevationAsSeaLevel(
                CreateBody(CelestialBody.Earth.Name)
            )
        );
        Assert.IsFalse(
            PlanetSurfaceContact.TreatNegativeElevationAsSeaLevel(
                CreateBody(CelestialBody.Moon.Name)
            )
        );
    }

    [Test]
    public void BodyFixedRadial_InvertsBodySpin()
    {
        Body earth = CreateBody(CelestialBody.Earth.Name);
        earth.spinAxis = SimVector3.UnitY();
        earth.spinAngleRadians = 0.7;
        SimVector3 local = new SimVector3(0.2, 0.8, 0.1).Normalize();
        SimVector3 world = earth.RotateBodyFixedToWorld(local);
        SimVector3 back = PlanetSurfaceContact.ToBodyFixedRadial(earth, world);
        Assert.Greater(SimVector3.Dot(back, local), 0.999);
    }

    static Body CreateBody(string name)
    {
        return new Body(
            name,
            SimVector3.Zero(),
            SimVector3.Zero(),
            SimVector3.Zero(),
            1.0,
            1000.0,
            true
        );
    }
}
