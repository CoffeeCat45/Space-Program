using NUnit.Framework;

public sealed class TimeWarpLimitsTests
{
    [Test]
    public void AtmosphereCap_DenseAirAllowsPhysicsWarp()
    {
        Assert.AreEqual(4.0, TimeWarpLimits.GetAtmosphereCap(1.0));
        Assert.AreEqual(4.0, TimeWarpLimits.GetAtmosphereCap(0.5));
        Assert.AreEqual(10.0, TimeWarpLimits.GetAtmosphereCap(0.1));
        Assert.AreEqual(50.0, TimeWarpLimits.GetAtmosphereCap(0.05));
    }

    [Test]
    public void GetMaxTimeScale_PadAndLowAirAllowFour()
    {
        double pad = TimeWarpLimits.GetMaxTimeScale(
            thrustRestricted: false,
            thrustActive: false,
            onKeplerRails: false,
            landedOrCrashed: true,
            inAtmosphere: true,
            atmosphereDepthFraction: 1.0
        );
        Assert.AreEqual(4.0, pad);

        double lowAir = TimeWarpLimits.GetMaxTimeScale(
            thrustRestricted: false,
            thrustActive: false,
            onKeplerRails: false,
            landedOrCrashed: false,
            inAtmosphere: true,
            atmosphereDepthFraction: 0.8
        );
        Assert.AreEqual(4.0, lowAir);
    }

    [Test]
    public void GetMaxTimeScale_RailsInVacuumUnlimited()
    {
        double rails = TimeWarpLimits.GetMaxTimeScale(
            thrustRestricted: false,
            thrustActive: false,
            onKeplerRails: true,
            landedOrCrashed: false,
            inAtmosphere: false,
            atmosphereDepthFraction: 0.0
        );
        Assert.IsTrue(double.IsPositiveInfinity(rails));
    }

    [Test]
    public void GetMaxTimeScale_EngineOnStaysAnalyticCap()
    {
        double burning = TimeWarpLimits.GetMaxTimeScale(
            thrustRestricted: true,
            thrustActive: true,
            onKeplerRails: false,
            landedOrCrashed: false,
            inAtmosphere: true,
            atmosphereDepthFraction: 0.8
        );
        Assert.AreEqual(1.0, burning);
    }

    [Test]
    public void GetMaxTimeScale_ThrottleParkedCapsAtPhysicsWarp()
    {
        double parked = TimeWarpLimits.GetMaxTimeScale(
            thrustRestricted: true,
            thrustActive: false,
            onKeplerRails: false,
            landedOrCrashed: false,
            inAtmosphere: false,
            atmosphereDepthFraction: 0.0
        );
        Assert.AreEqual(4.0, parked);
    }
}
