using NUnit.Framework;

public sealed class VesselEngineLossTests
{
    [SetUp]
    public void SetUp()
    {
        SimulationCheatFlags.DisableCrashDamage = false;
    }

    [TearDown]
    public void TearDown()
    {
        SimulationCheatFlags.DisableCrashDamage = false;
    }

    [Test]
    public void CrashThresholds_AreFiftyPercentSofter()
    {
        Assert.AreEqual(18.0, LandingConfig.CrashSpeedThreshold, 1e-9);
        Assert.AreEqual(15.0, ShipBlock.GetDefaultStructuralStrength(ShipBlockType.Engine), 1e-9);
        Assert.AreEqual(21.0, ShipBlock.GetDefaultStructuralStrength(ShipBlockType.FuelTank), 1e-9);
    }

    [Test]
    public void ReconcileScalarsFromBlocks_ZerosThrustWhenEngineDestroyed()
    {
        var stage = new StageData("s1", 2.0, 10.0, 10.0, 50000.0, 300.0);
        stage.blocks.Add(CreateEngine(50000.0, destroyed: true));
        stage.blocks.Add(CreateTank());

        StageData.ReconcileScalarsFromBlocks(stage);

        Assert.AreEqual(0.0, stage.thrust, 1e-9);
    }

    [Test]
    public void GetActiveEngineThrustNewtons_IgnoresStaleStageThrustWithoutLiveEngine()
    {
        Vessel vessel = new Vessel("Ship", SimVector3.Zero(), SimVector3.Zero());
        var stage = new StageData("s1", 2.0, 10.0, 10.0, 80000.0, 300.0);
        stage.blocks.Add(CreateTank());
        vessel.stages.Add(stage);

        Assert.AreEqual(0.0, vessel.GetActiveEngineThrustNewtons(), 1e-9);
        Assert.IsFalse(vessel.HasLiveMainEngine());
    }

    [Test]
    public void GetActiveEngineThrustNewtons_DropsWhenEngineMarkedDestroyed()
    {
        Vessel vessel = new Vessel("Ship", SimVector3.Zero(), SimVector3.Zero());
        var stage = new StageData("s1", 2.0, 10.0, 10.0, 80000.0, 300.0);
        ShipBlock engine = CreateEngine(80000.0, destroyed: false);
        stage.blocks.Add(engine);
        stage.blocks.Add(CreateTank());
        vessel.stages.Add(stage);

        Assert.AreEqual(80000.0, vessel.GetActiveEngineThrustNewtons(), 1e-6);

        engine.isDestroyed = true;
        StageData.ReconcileScalarsFromBlocks(stage);

        Assert.AreEqual(0.0, vessel.GetActiveEngineThrustNewtons(), 1e-9);
        Assert.AreEqual(0.0, stage.thrust, 1e-9);
        Assert.IsFalse(vessel.HasLiveMainEngine());
        Assert.AreEqual(
            0.0,
            vessel.GetAggregatedMainEngineThrustLocal(1.0).LengthSquared(),
            1e-18
        );
    }

    [Test]
    public void CheckCrash_IgnoresSpeedWhenCheatEnabled()
    {
        Vessel vessel = new Vessel("Ship", SimVector3.Zero(), SimVector3.Zero());
        SimulationCheatFlags.DisableCrashDamage = true;

        bool crashed = vessel.CheckCrash(200.0, 80.0, LandingConfig.CrashSpeedThreshold);

        Assert.IsFalse(crashed);
        Assert.AreEqual(FlightStatus.Flying, vessel.flightStatus);
    }

    [Test]
    public void CheckCrash_StillTriggersWhenCheatDisabled()
    {
        Vessel vessel = new Vessel("Ship", SimVector3.Zero(), SimVector3.Zero());

        bool crashed = vessel.CheckCrash(200.0, 0.0, LandingConfig.CrashSpeedThreshold);

        Assert.IsTrue(crashed);
        Assert.AreEqual(FlightStatus.Crashed, vessel.flightStatus);
    }

    [Test]
    public void ProcessBlockSurfaceImpacts_SkipsBreakupWhenCheatEnabled()
    {
        Vessel vessel = CreateImpactVessel();
        vessel.velocity = new SimVector3(0.0, -80.0, 0.0);
        SimulationCheatFlags.DisableCrashDamage = true;

        bool destroyed = vessel.ProcessBlockSurfaceImpacts(
            SimVector3.UnitY(),
            SimVector3.Zero(),
            9.81
        );

        Assert.IsFalse(destroyed);
        Assert.IsFalse(vessel.stages[0].blocks[0].isDestroyed);
        Assert.AreEqual(FlightStatus.Flying, vessel.flightStatus);
    }

    static Vessel CreateImpactVessel()
    {
        Vessel vessel = new Vessel("Ship", SimVector3.Zero(), SimVector3.Zero());
        vessel.centerOfMassLocal = SimVector3.Zero();
        vessel.orientation = SimQuaternion.Identity();

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

    static ShipBlock CreateEngine(double thrustNewtons, bool destroyed)
    {
        return new ShipBlock
        {
            blockType = ShipBlockType.Engine,
            isEngine = true,
            maxThrustNewtons = thrustNewtons,
            ispSeconds = 300.0,
            thrustDirection = SimVector3.UnitY(),
            isDestroyed = destroyed,
            dryMass = 1.0
        };
    }

    static ShipBlock CreateTank()
    {
        return new ShipBlock
        {
            blockType = ShipBlockType.FuelTank,
            isFuelTank = true,
            dryMass = 1.0,
            fuelMass = 10.0,
            fuelCapacity = 10.0
        };
    }
}
