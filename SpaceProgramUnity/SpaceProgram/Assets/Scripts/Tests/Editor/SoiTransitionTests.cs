using NUnit.Framework;

public sealed class SoiTransitionTests
{
    [Test]
    public void HoldKeplerRailsForMoonApproach_BlocksInboundInsidePatch()
    {
        Assert.IsTrue(
            SoiGeometry.HoldKeplerRailsForMoonApproach(
                primaryIsMoon: false,
                distanceToMoon: 1.0,
                moonSphereOfInfluenceRadius: 1.0,
                recedingSpeedFromMoon: -10.0
            )
        );
    }

    [Test]
    public void HoldKeplerRailsForMoonApproach_AllowsRecedingAfterExit()
    {
        Assert.IsFalse(
            SoiGeometry.HoldKeplerRailsForMoonApproach(
                primaryIsMoon: false,
                distanceToMoon: 1.0,
                moonSphereOfInfluenceRadius: 1.0,
                recedingSpeedFromMoon: 10.0
            )
        );
    }

    [Test]
    public void HoldKeplerRailsForMoonApproach_AllowsMoonPrimary()
    {
        Assert.IsFalse(
            SoiGeometry.HoldKeplerRailsForMoonApproach(
                primaryIsMoon: true,
                distanceToMoon: 0.5,
                moonSphereOfInfluenceRadius: 1.0,
                recedingSpeedFromMoon: -10.0
            )
        );
    }

    [Test]
    public void SegmentComesWithinRadius_DetectsChordThroughSphere()
    {
        SimVector3 start = new SimVector3(-2.0, 0.0, 0.0);
        SimVector3 end = new SimVector3(2.0, 0.0, 0.0);
        SimVector3 center = SimVector3.Zero();

        Assert.IsTrue(
            SoiGeometry.SegmentComesWithinRadius(start, end, center, 1.0)
        );
        Assert.IsFalse(
            SoiGeometry.SegmentComesWithinRadius(start, end, center, 0.1)
        );
    }

    [Test]
    public void TryFindSphereEntryFraction_HitsSphereFromOutside()
    {
        SimVector3 start = new SimVector3(-2.0, 0.0, 0.0);
        SimVector3 end = new SimVector3(2.0, 0.0, 0.0);
        bool found = SoiGeometry.TryFindSphereEntryFraction(
            start,
            end,
            SimVector3.Zero(),
            1.0,
            out double fraction
        );

        Assert.IsTrue(found);
        Assert.AreEqual(0.25, fraction, 1e-9);
    }

    [Test]
    public void TryFindSphereEntryFraction_StartInsideUsesMinFraction()
    {
        bool found = SoiGeometry.TryFindSphereEntryFraction(
            SimVector3.Zero(),
            new SimVector3(2.0, 0.0, 0.0),
            SimVector3.Zero(),
            1.0,
            out double fraction
        );

        Assert.IsTrue(found);
        Assert.AreEqual(1e-4, fraction, 1e-12);
    }

    [Test]
    public void TryFindSphereEntryFraction_MissesWhenSegmentStaysOutside()
    {
        bool found = SoiGeometry.TryFindSphereEntryFraction(
            new SimVector3(-2.0, 2.0, 0.0),
            new SimVector3(2.0, 2.0, 0.0),
            SimVector3.Zero(),
            1.0,
            out _
        );

        Assert.IsFalse(found);
    }

    [Test]
    public void HoldMoonPrimaryOnInvalidDistance_MoonPadOrPreviousMoon()
    {
        Assert.IsTrue(SoiGeometry.HoldMoonPrimaryOnInvalidDistance(true, false));
        Assert.IsTrue(SoiGeometry.HoldMoonPrimaryOnInvalidDistance(false, true));
        Assert.IsFalse(SoiGeometry.HoldMoonPrimaryOnInvalidDistance(false, false));
    }

    [Test]
    public void IsInsideMoonNearSurfaceLock_RejectsNaN()
    {
        Assert.IsFalse(
            SoiGeometry.IsInsideMoonNearSurfaceLock(double.NaN, 1_737_000.0)
        );
        Assert.IsTrue(
            SoiGeometry.IsInsideMoonNearSurfaceLock(1_800_000.0, 1_737_000.0)
        );
        Assert.IsFalse(
            SoiGeometry.IsInsideMoonNearSurfaceLock(10_000_000.0, 1_737_000.0)
        );
    }

    [Test]
    public void IsFalseSunPrimaryInsidePhotosphere_DetectsLunarPadEjection()
    {
        Assert.IsTrue(
            SoiGeometry.IsFalseSunPrimaryInsidePhotosphere(
                200_000.0,
                4_000_000.0,
                150_000_000_000.0,
                1_737_000.0
            )
        );
        Assert.IsFalse(
            SoiGeometry.IsFalseSunPrimaryInsidePhotosphere(
                150_000_000_000.0,
                4_000_000.0,
                1_800_000.0,
                1_737_000.0
            )
        );
        Assert.IsTrue(
            SoiGeometry.IsFalseSunPrimaryInsidePhotosphere(
                double.NaN,
                4_000_000.0,
                double.NaN,
                1_737_000.0
            )
        );
    }
}
