using NUnit.Framework;

public sealed class SoiTransitionTests
{
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
}
