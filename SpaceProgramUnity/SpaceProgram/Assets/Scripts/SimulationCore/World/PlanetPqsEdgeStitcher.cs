using System;

/// <summary>
/// CPU edge stitching for PQS height grids (terrain_plan §1.3, SimulationCore path).
/// Binds fine-edge vertices to the linear envelope of a coarser neighbor edge.
/// </summary>
public static class PlanetPqsEdgeStitcher
{
    public static void StitchEdgeHeights(
        double[] fineEdgeHeights,
        int fineVertexCount,
        double[] coarseEdgeHeights,
        int coarseVertexCount
    )
    {
        ValidateEdgeArrays(fineEdgeHeights, fineVertexCount, coarseEdgeHeights, coarseVertexCount);

        if (fineVertexCount == 1)
        {
            fineEdgeHeights[0] = coarseEdgeHeights[0];
            return;
        }

        for (int i = 0; i < fineVertexCount; i++)
        {
            double t = i / (double)(fineVertexCount - 1);
            fineEdgeHeights[i] = SampleEdgeLinear(coarseEdgeHeights, coarseVertexCount, t);
        }
    }

    public static void StitchHeightGridEdge(
        double[] heights,
        int resolution,
        PlanetChunkEdgeSide edge,
        double[] neighborCoarseEdgeHeights,
        int neighborCoarseResolution
    )
    {
        if (heights == null)
        {
            throw new ArgumentNullException(nameof(heights));
        }

        if (resolution < 1 || heights.Length != resolution * resolution)
        {
            throw new ArgumentException(
                "Height grid must be resolution × resolution.",
                nameof(heights)
            );
        }

        var fineEdge = new double[resolution];
        ExtractGridEdge(heights, resolution, edge, fineEdge);
        StitchEdgeHeights(fineEdge, resolution, neighborCoarseEdgeHeights, neighborCoarseResolution);
        WriteGridEdge(heights, resolution, edge, fineEdge);
    }

    public static double SampleEdgeLinear(double[] edgeHeights, int vertexCount, double t)
    {
        if (edgeHeights == null)
        {
            throw new ArgumentNullException(nameof(edgeHeights));
        }

        if (vertexCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount));
        }

        if (vertexCount == 1)
        {
            return edgeHeights[0];
        }

        if (t <= 0.0)
        {
            return edgeHeights[0];
        }

        if (t >= 1.0)
        {
            return edgeHeights[vertexCount - 1];
        }

        double position = t * (vertexCount - 1);
        int index0 = (int)Math.Floor(position);
        int index1 = Math.Min(index0 + 1, vertexCount - 1);
        double fraction = position - index0;
        return edgeHeights[index0] + ((edgeHeights[index1] - edgeHeights[index0]) * fraction);
    }

    public static void ExtractGridEdge(
        double[] heights,
        int resolution,
        PlanetChunkEdgeSide edge,
        double[] edgeOut
    )
    {
        if (edgeOut == null || edgeOut.Length != resolution)
        {
            throw new ArgumentException("Edge output must match resolution.", nameof(edgeOut));
        }

        switch (edge)
        {
            case PlanetChunkEdgeSide.MinU:
                for (int y = 0; y < resolution; y++)
                {
                    edgeOut[y] = heights[y * resolution];
                }
                break;

            case PlanetChunkEdgeSide.MaxU:
                for (int y = 0; y < resolution; y++)
                {
                    edgeOut[y] = heights[(y * resolution) + (resolution - 1)];
                }
                break;

            case PlanetChunkEdgeSide.MinV:
                for (int x = 0; x < resolution; x++)
                {
                    edgeOut[x] = heights[x];
                }
                break;

            case PlanetChunkEdgeSide.MaxV:
                for (int x = 0; x < resolution; x++)
                {
                    edgeOut[x] = heights[((resolution - 1) * resolution) + x];
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(edge), edge, "Unknown chunk edge.");
        }
    }

    public static void WriteGridEdge(
        double[] heights,
        int resolution,
        PlanetChunkEdgeSide edge,
        double[] edgeValues
    )
    {
        if (edgeValues == null || edgeValues.Length != resolution)
        {
            throw new ArgumentException("Edge values must match resolution.", nameof(edgeValues));
        }

        switch (edge)
        {
            case PlanetChunkEdgeSide.MinU:
                for (int y = 0; y < resolution; y++)
                {
                    heights[y * resolution] = edgeValues[y];
                }
                break;

            case PlanetChunkEdgeSide.MaxU:
                for (int y = 0; y < resolution; y++)
                {
                    heights[(y * resolution) + (resolution - 1)] = edgeValues[y];
                }
                break;

            case PlanetChunkEdgeSide.MinV:
                for (int x = 0; x < resolution; x++)
                {
                    heights[x] = edgeValues[x];
                }
                break;

            case PlanetChunkEdgeSide.MaxV:
                for (int x = 0; x < resolution; x++)
                {
                    heights[((resolution - 1) * resolution) + x] = edgeValues[x];
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(edge), edge, "Unknown chunk edge.");
        }
    }

    static void ValidateEdgeArrays(
        double[] fineEdgeHeights,
        int fineVertexCount,
        double[] coarseEdgeHeights,
        int coarseVertexCount
    )
    {
        if (fineEdgeHeights == null)
        {
            throw new ArgumentNullException(nameof(fineEdgeHeights));
        }

        if (coarseEdgeHeights == null)
        {
            throw new ArgumentNullException(nameof(coarseEdgeHeights));
        }

        if (fineVertexCount <= 0 || fineEdgeHeights.Length < fineVertexCount)
        {
            throw new ArgumentOutOfRangeException(nameof(fineVertexCount));
        }

        if (coarseVertexCount <= 0 || coarseEdgeHeights.Length < coarseVertexCount)
        {
            throw new ArgumentOutOfRangeException(nameof(coarseVertexCount));
        }
    }
}
