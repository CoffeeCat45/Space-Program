using NUnit.Framework;

public sealed class FlightLodLoadingPolicyTests
{
    [Test]
    public void GetMinReadyLeafLevel_IsActiveMax()
    {
        Assert.AreEqual(8, FlightLodLoadingPolicy.GetMinReadyLeafLevel(8));
        Assert.AreEqual(15, FlightLodLoadingPolicy.GetMinReadyLeafLevel(15));
        Assert.AreEqual(0, FlightLodLoadingPolicy.GetMinReadyLeafLevel(0));
    }

    [Test]
    public void ShouldReveal_WaitsForMinimumVisibleTime()
    {
        Assert.IsFalse(
            FlightLodLoadingPolicy.ShouldReveal(
                hasPlanetSphere: true,
                hasQuadtree: true,
                hasViewerMesh: true,
                viewerLeafLevel: 8,
                activeMaxLodLevel: 8,
                padStillCoarse: false,
                bootstrapping: false,
                warmupActive: false,
                elapsedSeconds: 0.05f,
                readyStreakSeconds: 2f
            )
        );
    }

    [Test]
    public void ShouldReveal_WaitsWhilePadBootstrapOrWarmup()
    {
        Assert.IsFalse(
            FlightLodLoadingPolicy.ShouldReveal(
                hasPlanetSphere: true,
                hasQuadtree: true,
                hasViewerMesh: true,
                viewerLeafLevel: 8,
                activeMaxLodLevel: 8,
                padStillCoarse: true,
                bootstrapping: false,
                warmupActive: false,
                elapsedSeconds: 2f,
                readyStreakSeconds: 2f
            )
        );
        Assert.IsFalse(
            FlightLodLoadingPolicy.ShouldReveal(
                hasPlanetSphere: true,
                hasQuadtree: true,
                hasViewerMesh: true,
                viewerLeafLevel: 8,
                activeMaxLodLevel: 8,
                padStillCoarse: false,
                bootstrapping: true,
                warmupActive: false,
                elapsedSeconds: 2f,
                readyStreakSeconds: 2f
            )
        );
        Assert.IsFalse(
            FlightLodLoadingPolicy.ShouldReveal(
                hasPlanetSphere: true,
                hasQuadtree: true,
                hasViewerMesh: true,
                viewerLeafLevel: 8,
                activeMaxLodLevel: 8,
                padStillCoarse: false,
                bootstrapping: false,
                warmupActive: true,
                elapsedSeconds: 2f,
                readyStreakSeconds: 2f
            )
        );
    }

    [Test]
    public void ShouldReveal_WaitsForSettleAfterPadIsQuiet()
    {
        Assert.IsFalse(
            FlightLodLoadingPolicy.ShouldReveal(
                hasPlanetSphere: true,
                hasQuadtree: true,
                hasViewerMesh: true,
                viewerLeafLevel: 8,
                activeMaxLodLevel: 8,
                padStillCoarse: false,
                bootstrapping: false,
                warmupActive: false,
                elapsedSeconds: 2f,
                readyStreakSeconds: 0.2f
            )
        );
        Assert.IsTrue(
            FlightLodLoadingPolicy.ShouldReveal(
                hasPlanetSphere: true,
                hasQuadtree: true,
                hasViewerMesh: true,
                viewerLeafLevel: 8,
                activeMaxLodLevel: 8,
                padStillCoarse: false,
                bootstrapping: false,
                warmupActive: false,
                elapsedSeconds: 2f,
                readyStreakSeconds: FlightLodLoadingPolicy.SettleSeconds
            )
        );
    }

    [Test]
    public void ShouldReveal_WithoutPlanetAfterGrace()
    {
        Assert.IsTrue(
            FlightLodLoadingPolicy.ShouldReveal(
                hasPlanetSphere: false,
                hasQuadtree: false,
                hasViewerMesh: false,
                viewerLeafLevel: -1,
                activeMaxLodLevel: 8,
                padStillCoarse: false,
                bootstrapping: false,
                warmupActive: false,
                elapsedSeconds: 0.8f,
                readyStreakSeconds: 0f
            )
        );
    }

    [Test]
    public void ShouldReveal_TimeoutAlwaysReveals()
    {
        Assert.IsTrue(
            FlightLodLoadingPolicy.ShouldReveal(
                hasPlanetSphere: true,
                hasQuadtree: true,
                hasViewerMesh: false,
                viewerLeafLevel: 0,
                activeMaxLodLevel: 8,
                padStillCoarse: true,
                bootstrapping: true,
                warmupActive: false,
                elapsedSeconds: FlightLodLoadingPolicy.TimeoutSeconds,
                readyStreakSeconds: 0f
            )
        );
    }

    [Test]
    public void GameSettingsPlay_LodLoadingScreenDefaultsOff()
    {
        Assert.IsFalse(new GameSettingsPlay().showLodLoadingScreen);
    }
}
