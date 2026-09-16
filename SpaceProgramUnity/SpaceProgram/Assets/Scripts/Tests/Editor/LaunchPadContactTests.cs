using System;
using NUnit.Framework;

public sealed class LaunchPadContactTests
{
    [SetUp]
    public void SetUp()
    {
        SimulationCheatFlags.PlanetSpinDisabled = false;
        LaunchSiteConfig.ApplyScenario(SimulationScenario.PlanetSurfaceFlorida);
        LaunchPadConstants.BindForScenario(SimulationScenario.PlanetSurfaceFlorida);
        LaunchPadConstants.SetLaunchSiteTerrainHeightAboveSphereMeters(0.0);
        LaunchPadConstants.SetLaunchSiteSpawnPointOffsetMeters(double.NaN, 0.0, 0.0);
    }

    [Test]
    public void PadPhysicsRadius_IsTighterThanVisibility()
    {
        Assert.Less(
            LaunchPadConstants.PadPhysicsContactRadiusMeters,
            LaunchPadConstants.LaunchSitePadVisibilityRadiusMeters
        );
        Assert.Greater(LaunchPadConstants.PadPhysicsContactRadiusMeters, 8.0);
    }

    [Test]
    public void SpawnedCapeShip_SitsOnPadContactAtAltitudeZero()
    {
        Simulation simulation = new Simulation(SimulationScenario.PlanetSurfaceFlorida);

        Assert.IsTrue(simulation.IsShipOnCovePadContact());
        Assert.IsTrue(simulation.IsShipOnCoveLaunchPad());
        Assert.AreEqual(0.0, simulation.GetShipAltitudeAbovePlanet(), 0.05);
    }

    [Test]
    public void SeventyMetersFromPad_IsVisibleNotContact()
    {
        Simulation simulation = new Simulation(SimulationScenario.PlanetSurfaceFlorida);
        PlaceAtArcFromPad(simulation, 70.0, 20.0);

        Assert.IsTrue(simulation.IsShipOnCoveLaunchPad());
        Assert.IsFalse(simulation.IsShipOnCovePadContact());
    }

    [Test]
    public void PinBesidePad_UsesSphereRestNotPadDeck()
    {
        Simulation simulation = new Simulation(SimulationScenario.PlanetSurfaceFlorida);
        PlaceAtArcFromPad(simulation, 70.0, 40.0);

        SimVector3 normal = simulation.GetSurfaceContactNormal(simulation.planet);
        simulation.PinShipToSurface(simulation.planet, normal);

        double bottomExtent = LaunchPadConstants.GetEffectiveBottomExtentMeters(
            simulation.ship.GetBottomExtentAlongWorldDirection(normal)
        );
        double comDistance = simulation.ship.GetCenterOfMassWorld()
            .Subtract(simulation.planet.position)
            .Length();
        double sphereRest =
            simulation.planet.radius + bottomExtent + LandingConfig.SurfacePad;
        double padRest = LaunchPadConstants.GetPadRestComDistance(bottomExtent);

        Assert.AreEqual(sphereRest, comDistance, 0.25);
        Assert.Greater(Math.Abs(padRest - sphereRest), 2.0);
    }

    [Test]
    public void LandedTilt_IsNotResetToPadUp()
    {
        Simulation simulation = new Simulation(SimulationScenario.PlanetSurfaceFlorida);
        SimVector3 pad = simulation.GetSurfaceContactNormal(simulation.planet);
        SimVector3 east = LandingConfig.GetSurfaceEastTangent(pad);
        SimQuaternion tilted = SimQuaternion.Multiply(
            SimQuaternion.FromAxisAngle(east, 18.0 * Math.PI / 180.0),
            simulation.ship.orientation
        );
        simulation.ship.orientation = tilted;
        SimVector3 tiltedUp = simulation.ship.orientation.Up();

        simulation.Update(0.02);

        Assert.AreEqual(FlightStatus.Landed, simulation.ship.flightStatus);
        Assert.Greater(
            SimVector3.Dot(simulation.ship.orientation.Up(), tiltedUp),
            0.995,
            "Landed must keep the tilt instead of snapping to pad vertical."
        );
        Assert.Less(
            SimVector3.Dot(simulation.ship.orientation.Up(), pad),
            0.99,
            "A 18° tip must remain visibly off pad-up."
        );
    }

    [Test]
    public void FlyingContact_PushesAlongCurrentRadialWithoutSnappingToPad()
    {
        Simulation simulation = new Simulation(SimulationScenario.PlanetSurfaceFlorida);
        PlaceAtArcFromPad(simulation, 70.0, 1.0);
        SimVector3 before = simulation.ship.GetCenterOfMassWorld()
            .Subtract(simulation.planet.position)
            .Normalize();

        simulation.PushShipOutOfSurface(simulation.planet);

        SimVector3 after = simulation.ship.GetCenterOfMassWorld()
            .Subtract(simulation.planet.position)
            .Normalize();
        Assert.Greater(SimVector3.Dot(before, after), 0.999999);
    }

    [Test]
    public void VisualGlobeOffset_UsesPadBaseOnlyInsidePadYard()
    {
        LaunchPadConstants.SetLaunchSiteTerrainHeightAboveSphereMeters(12.0);
        Simulation onPad = new Simulation(SimulationScenario.PlanetSurfaceFlorida);
        CelestialTerrainRuntime.BindForScenario(SimulationScenario.PlanetSurfaceFlorida);

        Assert.AreEqual(
            12.0,
            WorldVisualBridge.GetPlanetSurfaceHeightOffsetMeters(onPad),
            1e-9
        );

        Simulation offPad = new Simulation(SimulationScenario.PlanetSurfaceFlorida);
        PlaceAtArcFromPad(offPad, 200.0, 40.0);
        offPad.ship.flightStatus = FlightStatus.Landed;
        offPad.ship.landedOnBodyName = offPad.planet.name;

        Assert.AreEqual(
            0.0,
            WorldVisualBridge.GetPlanetSurfaceHeightOffsetMeters(offPad),
            1e-9
        );
        Assert.IsFalse(offPad.IsShipOnCovePadContact());
        Assert.IsFalse(offPad.IsShipOnCoveLaunchPad());
    }

    [Test]
    public void LandedOffPad_ContactNormalFollowsBodyFixedSpin()
    {
        Simulation simulation = new Simulation(SimulationScenario.PlanetSurfaceFlorida);
        PlaceAtArcFromPad(simulation, 70.0, 20.0);

        SimVector3 worldBefore = simulation.ship.GetCenterOfMassWorld()
            .Subtract(simulation.planet.position)
            .Normalize();
        simulation.ship.flightStatus = FlightStatus.Landed;
        simulation.ship.landedOnBodyName = simulation.planet.name;
        simulation.ship.landedSurfaceDirectionBodyFixed = simulation.planet
            .GetSpinRotation()
            .Conjugate()
            .Rotate(worldBefore)
            .Normalize();

        simulation.planet.spinAngleRadians += 30.0 * Math.PI / 180.0;

        SimVector3 contact = simulation.GetSurfaceContactNormal(simulation.planet);
        SimVector3 expected = simulation.planet
            .RotateBodyFixedToWorld(simulation.ship.landedSurfaceDirectionBodyFixed)
            .Normalize();
        SimVector3 comRadial = simulation.ship.GetCenterOfMassWorld()
            .Subtract(simulation.planet.position)
            .Normalize();

        Assert.Greater(SimVector3.Dot(contact, expected), 0.999);
        Assert.Less(SimVector3.Dot(contact, comRadial), 0.98);
    }

    static void PlaceAtArcFromPad(
        Simulation simulation,
        double arcMeters,
        double heightAboveSphereMeters
    )
    {
        Body planet = simulation.planet;
        SimVector3 pad = planet
            .RotateBodyFixedToWorld(LandingConfig.GetSurfaceLaunchPadNormal())
            .Normalize();
        SimVector3 east = LandingConfig.GetSurfaceEastTangent(pad);
        SimVector3 axis = SimVector3.Cross(pad, east);
        Assert.Greater(axis.LengthSquared(), 1e-12);

        double angle = arcMeters / planet.radius;
        SimVector3 radial = SimQuaternion
            .FromAxisAngle(axis.Normalize(), angle)
            .Rotate(pad)
            .Normalize();

        simulation.ship.position = planet.position.Add(
            radial.Multiply(planet.radius + heightAboveSphereMeters)
        );
        simulation.ship.flightStatus = FlightStatus.Flying;
        simulation.ship.landedOnBodyName = string.Empty;
        simulation.ship.landedSurfaceDirectionBodyFixed = SimVector3.Zero();
        simulation.ship.velocity = planet.GetAbsoluteSurfaceVelocityAt(
            simulation.ship.GetCenterOfMassWorld()
        );
    }
}
