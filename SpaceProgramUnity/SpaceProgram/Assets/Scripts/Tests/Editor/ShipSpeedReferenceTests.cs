using NUnit.Framework;

public sealed class ShipSpeedReferenceTests
{
    [Test]
    public void Cycle_WalksSurfaceOrbitTarget()
    {
        Assert.AreEqual(ShipHudSpeedMode.Orbit, ShipSpeedReference.Cycle(ShipHudSpeedMode.Surface));
        Assert.AreEqual(ShipHudSpeedMode.Target, ShipSpeedReference.Cycle(ShipHudSpeedMode.Orbit));
        Assert.AreEqual(ShipHudSpeedMode.Surface, ShipSpeedReference.Cycle(ShipHudSpeedMode.Target));
    }

    [Test]
    public void ResolveRelativeVelocity_SurfaceUsesGroundFrame()
    {
        SimVector3 orbit = new SimVector3(400.0, 0.0, 0.0);
        SimVector3 surface = new SimVector3(1.0, 0.0, 0.0);
        SimVector3 target = new SimVector3(50.0, 0.0, 0.0);

        SimVector3 resolved = ShipSpeedReference.ResolveRelativeVelocity(
            ShipHudSpeedMode.Surface,
            orbit,
            surface,
            target,
            hasTarget: true
        );

        Assert.AreEqual(1.0, resolved.x, 1e-12);
        Assert.AreEqual(0.0, resolved.y, 1e-12);
    }

    [Test]
    public void ResolveRelativeVelocity_OrbitIgnoresSpinAndTarget()
    {
        SimVector3 orbit = new SimVector3(400.0, 0.0, 0.0);
        SimVector3 surface = SimVector3.Zero();
        SimVector3 target = new SimVector3(50.0, 0.0, 0.0);

        SimVector3 resolved = ShipSpeedReference.ResolveRelativeVelocity(
            ShipHudSpeedMode.Orbit,
            orbit,
            surface,
            target,
            hasTarget: true
        );

        Assert.AreEqual(400.0, resolved.x, 1e-12);
    }

    [Test]
    public void ResolveRelativeVelocity_TargetUsesTargetWhenPresent()
    {
        SimVector3 orbit = new SimVector3(400.0, 0.0, 0.0);
        SimVector3 surface = SimVector3.Zero();
        SimVector3 target = new SimVector3(-12.0, 3.0, 0.0);

        SimVector3 resolved = ShipSpeedReference.ResolveRelativeVelocity(
            ShipHudSpeedMode.Target,
            orbit,
            surface,
            target,
            hasTarget: true
        );

        Assert.AreEqual(-12.0, resolved.x, 1e-12);
        Assert.AreEqual(3.0, resolved.y, 1e-12);
    }

    [Test]
    public void ResolveRelativeVelocity_TargetFallsBackToOrbitWithoutTarget()
    {
        SimVector3 orbit = new SimVector3(400.0, 0.0, 0.0);
        SimVector3 surface = SimVector3.Zero();

        SimVector3 resolved = ShipSpeedReference.ResolveRelativeVelocity(
            ShipHudSpeedMode.Target,
            orbit,
            surface,
            SimVector3.Zero(),
            hasTarget: false
        );

        Assert.AreEqual(400.0, resolved.x, 1e-12);
    }

    [Test]
    public void GetModeLabel_MatchesHudKeys()
    {
        Assert.AreEqual("Surface", ShipSpeedReference.GetModeLabel(ShipHudSpeedMode.Surface));
        Assert.AreEqual("Orbital speed", ShipSpeedReference.GetModeLabel(ShipHudSpeedMode.Orbit));
        Assert.AreEqual("Target speed", ShipSpeedReference.GetModeLabel(ShipHudSpeedMode.Target));
    }
}
