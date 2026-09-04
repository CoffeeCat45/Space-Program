/// <summary>
/// Shared PQS / quadtree LOD crack-prevention constants (terrain_plan §1.3).
/// </summary>
public static class PlanetPqsConstants
{
    /// <summary>Default skirt depth as a fraction of chunk span (Unity visual fix).</summary>
    public const double DefaultSkirtDepthFraction = 0.02;

    public const double MinSkirtDepthFraction = 0.01;

    public const double MaxSkirtDepthFraction = 0.05;

    /// <summary>Neighbors must not be more than this many LOD levels finer when merging.</summary>
    public const int MaxNeighborLodGapForMerge = 1;

    /// <summary>
    /// Parent level of the pad 2×2. L0/L1 is the cube face — sibling-balance there
    /// force-splits the whole face and merge snaps it back (LOD jerk).
    /// </summary>
    public const int MinViewerQuadParentLevel = 2;

    /// <summary>
    /// L0 is the whole cube face. Even with <see cref="MaxSkirtDepthMeters"/> a face shell
    /// skirt reads as an inner rim. Distant ring leaves start at L1.
    /// </summary>
    public const int MinLodLevelForSkirts = 1;

    /// <summary>Hard cap so a skirt never reads as a mountain.</summary>
    public const double MaxSkirtDepthMeters = 400.0;

    /// <summary>Skirt depth is also limited to this many grid cells of the chunk.</summary>
    public const double SkirtDepthGridCellMultiple = 1.5;

    /// <summary>
    /// Split when this node's world span &gt; target. Merge when the same node's span
    /// &lt;= target × this. Must be below 1. Comparing child span (half) to target × this
    /// overlaps the split band and flaps the LOD ring every tick.
    /// </summary>
    public const double SplitMergeHysteresis = 0.72;
}
