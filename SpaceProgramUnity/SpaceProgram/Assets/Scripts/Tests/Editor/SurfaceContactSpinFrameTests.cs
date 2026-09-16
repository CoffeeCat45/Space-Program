using System;
using NUnit.Framework;

public sealed class SurfaceContactSpinFrameTests
{
    [TearDown]
    public void TearDown()
    {
        SimulationCheatFlags.DisableCrashDamage = false;
        SimulationCheatFlags.PlanetSpinDisabled = false;
    }

    [Test]
    public void EarthCapeGroundTrack_IsFarAboveCrashThresholdVersusBodyCenter()
    {
        Body earth = CreateSpinningEarth();
        SimVector3 pad = LandingConfig.GetSurfaceLaunchPadNormal()
            .Multiply(earth.radius);
        SimVector3 world = earth.position.Add(pad);
        double versusCenter = earth.GetAbsoluteSurfaceVelocityAt(world)
            .Subtract(earth.velocity)
            .Length();

        Assert.Greater(
            versusCenter,
            LandingConfig.CrashSpeedThreshold,
            "Cape ω×r vs body.velocity must not be the crash metric."
        );
        Assert.Greater(versusCenter, 100.0);
    }

    [Test]
    public void CoRotatingEngine_DoesNotBreakOnSpinningGround()
    {
        Body earth = CreateSpinningEarth();
        Vessel vessel = CreateContactVessel(earth);
        SimVector3 radial = LandingConfig.GetSurfaceLaunchPadNormal();
        SimVector3 surfaceVel = earth.GetAbsoluteSurfaceVelocityAt(
            vessel.GetCenterOfMassWorld()
        );
        vessel.velocity = surfaceVel;

        bool destroyed = vessel.ProcessBlockSurfaceImpacts(
            radial,
            surfaceVel,
            9.81
        );

        Assert.IsFalse(destroyed);
        Assert.IsFalse(vessel.stages[0].blocks[0].isDestroyed);
    }

    [Test]
    public void InertialRestOnSpinningGround_StillBreaksEngine()
    {
        Body earth = CreateSpinningEarth();
        Vessel vessel = CreateContactVessel(earth);
        SimVector3 radial = LandingConfig.GetSurfaceLaunchPadNormal();
        SimVector3 surfaceVel = earth.GetAbsoluteSurfaceVelocityAt(
            vessel.GetCenterOfMassWorld()
        );
        vessel.velocity = earth.velocity;

        bool destroyed = vessel.ProcessBlockSurfaceImpacts(
            radial,
            surfaceVel,
            9.81
        );

        Assert.IsTrue(destroyed);
        Assert.IsTrue(vessel.stages[0].blocks[0].isDestroyed);
    }

    static Body CreateSpinningEarth()
    {
        var earth = new Body(
            CelestialBody.Earth.Name,
            SimVector3.Zero(),
            SimVector3.Zero(),
            SimVector3.Zero(),
            CelestialBody.Earth.Mu,
            CelestialBody.Earth.Radius,
            true
        );
        earth.spinRadiansPerSecond = CelestialBody.Earth.GetSpinRadiansPerSecond();
        earth.spinAxis = SimVector3.UnitY();
        SimulationCheatFlags.PlanetSpinDisabled = false;
        return earth;
    }

    static Vessel CreateContactVessel(Body earth)
    {
        SimVector3 radial = LandingConfig.GetSurfaceLaunchPadNormal();
        SimVector3 pos = earth.position.Add(
            radial.Multiply(earth.radius + 12.0)
        );
        Vessel vessel = new Vessel("Ship", pos, SimVector3.Zero());
        vessel.flightStatus = FlightStatus.Flying;
        vessel.centerOfMassLocal = SimVector3.Zero();
        vessel.orientation = SimQuaternion.FromForwardAndUp(
            LandingConfig.GetSurfaceEastTangent(radial),
            radial
        );

        var stage = new StageData("s1", 2.0, 10.0, 10.0, 80000.0, 300.0);
        stage.blocks.Add(new ShipBlock
        {
            blockType = ShipBlockType.Engine,
            isEngine = true,
            maxThrustNewtons = 80000.0,
            structuralStrength = 15.0,
            localOffset = new SimVector3(0.0, -2.0, 0.0),
            dimensions = new SimVector3(1.0, 2.0, 1.0),
            dryMass = 1.0
        });
        vessel.stages.Add(stage);
        return vessel;
    }
}
