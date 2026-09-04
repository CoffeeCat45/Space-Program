using System;
using NUnit.Framework;

public sealed class PlanetPqsCrackPreventionTests
{
    const double HeightTolerance = 1e-9;

    [Test]
    public void RequiresEdgeStitching_WhenNeighborIsCoarser_ReturnsTrue()
    {
        Assert.IsTrue(PlanetPqsLodRules.RequiresEdgeStitching(localLodLevel: 4, neighborLodLevel: 3));
    }

    [Test]
    public void RequiresEdgeStitching_WhenNeighborIsSameOrFiner_ReturnsFalse()
    {
        Assert.IsFalse(PlanetPqsLodRules.RequiresEdgeStitching(3, 3));
        Assert.IsFalse(PlanetPqsLodRules.RequiresEdgeStitching(3, 4));
    }

    [Test]
    public void CanSplitWithoutCrack_BlocksWhenNeighborIsCoarser()
    {
        Assert.IsFalse(PlanetPqsLodRules.CanSplitWithoutCrack(3, 2, 3, 3, 3));
        Assert.IsTrue(PlanetPqsLodRules.CanSplitWithoutCrack(3, 3, 3, 3, 3));
        Assert.IsTrue(PlanetPqsLodRules.CanSplitWithoutCrack(3, 4, 3, 3, 3));
    }

    [Test]
    public void CanMergeWithoutCrack_BlocksWhenNeighborIsTooFine()
    {
        Assert.IsFalse(PlanetPqsLodRules.CanMergeWithoutCrack(3, 5, 3, 3, 3));
        Assert.IsTrue(PlanetPqsLodRules.CanMergeWithoutCrack(3, 4, 3, 3, 3));
        Assert.IsTrue(PlanetPqsLodRules.CanMergeWithoutCrack(3, 3, 3, 3, 3));
    }

    [Test]
    public void StitchEdgeHeights_BindsFineVerticesToCoarseEnvelope()
    {
        var coarse = new[] { 0.0, 100.0, 200.0 };
        var fine = new double[5];

        PlanetPqsEdgeStitcher.StitchEdgeHeights(fine, fine.Length, coarse, coarse.Length);

        Assert.AreEqual(0.0, fine[0], HeightTolerance);
        Assert.AreEqual(50.0, fine[1], HeightTolerance);
        Assert.AreEqual(100.0, fine[2], HeightTolerance);
        Assert.AreEqual(150.0, fine[3], HeightTolerance);
        Assert.AreEqual(200.0, fine[4], HeightTolerance);
    }

    [Test]
    public void StitchHeightGridEdge_WritesStitchedMinUEdge()
    {
        const int resolution = 3;
        var heights = new double[]
        {
            10, 20, 30,
            40, 50, 60,
            70, 80, 90,
        };
        var coarseNeighborEdge = new[] { 0.0, 100.0 };

        PlanetPqsEdgeStitcher.StitchHeightGridEdge(
            heights,
            resolution,
            PlanetChunkEdgeSide.MinU,
            coarseNeighborEdge,
            coarseNeighborEdge.Length
        );

        Assert.AreEqual(0.0, heights[0], HeightTolerance);
        Assert.AreEqual(50.0, heights[3], HeightTolerance);
        Assert.AreEqual(100.0, heights[6], HeightTolerance);
        Assert.AreEqual(20.0, heights[1], HeightTolerance);
        Assert.AreEqual(90.0, heights[8], HeightTolerance);
    }

    [Test]
    public void SampleEdgeLinear_InterpolatesBetweenCoarseVertices()
    {
        var coarse = new[] { 0.0, 100.0, 300.0 };

        Assert.AreEqual(50.0, PlanetPqsEdgeStitcher.SampleEdgeLinear(coarse, 3, 0.25), HeightTolerance);
        Assert.AreEqual(200.0, PlanetPqsEdgeStitcher.SampleEdgeLinear(coarse, 3, 0.75), HeightTolerance);
    }

    [Test]
    public void ComputeSkirtDepthMeters_UsesChunkSpanFraction()
    {
        double depth = PlanetPqsSkirtBuilder.ComputeSkirtDepthMeters(
            1000.0,
            PlanetPqsConstants.DefaultSkirtDepthFraction
        );

        Assert.AreEqual(20.0, depth, HeightTolerance);
    }

    [Test]
    public void ComputeSkirtDepthMeters_CoarseLod_ReturnsZero()
    {
        double depth = PlanetPqsSkirtBuilder.ComputeSkirtDepthMeters(
            1_274_200.0,
            PlanetPqsConstants.DefaultSkirtDepthFraction,
            vertexResolution: 33,
            lodLevel: 0
        );

        Assert.AreEqual(0.0, depth, HeightTolerance);
    }

    [Test]
    public void ComputeSkirtDepthMeters_Lod1WideChunk_UsesCapNotZero()
    {
        double depth = PlanetPqsSkirtBuilder.ComputeSkirtDepthMeters(
            1_274_200.0,
            PlanetPqsConstants.DefaultSkirtDepthFraction,
            vertexResolution: 33,
            lodLevel: 1
        );

        Assert.AreEqual(PlanetPqsConstants.MaxSkirtDepthMeters, depth, HeightTolerance);
    }

    [Test]
    public void ComputeSkirtDepthMeters_CapsWideFineChunks()
    {
        double depth = PlanetPqsSkirtBuilder.ComputeSkirtDepthMeters(
            80_000.0,
            PlanetPqsConstants.DefaultSkirtDepthFraction,
            vertexResolution: 33,
            lodLevel: 4
        );

        Assert.AreEqual(PlanetPqsConstants.MaxSkirtDepthMeters, depth, HeightTolerance);
    }

    [Test]
    public void OffsetTowardPlanetCenter_MovesSurfacePointInward()
    {
        SimVector3 center = SimVector3.Zero();
        SimVector3 surface = new SimVector3(0.0, 637100.0, 0.0);

        SimVector3 skirt = PlanetPqsSkirtBuilder.OffsetTowardPlanetCenter(surface, center, 50.0);

        Assert.AreEqual(637050.0, skirt.y, 1e-6);
        Assert.AreEqual(0.0, skirt.x, 1e-6);
        Assert.AreEqual(0.0, skirt.z, 1e-6);
    }
}
