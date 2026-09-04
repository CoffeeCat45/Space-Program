/// <summary>
/// Runtime LOD mesh-build counters for flight HUD / debug overlays.
/// </summary>
public readonly struct PlanetLodLoadSnapshot
{
    public readonly int LeavesWithoutMesh;
    public readonly int InFlightLeafBuilds;
    public readonly int PendingAsyncCompletions;
    public readonly int MeshQueueCount;
    public readonly bool SplitBackpressure;
    public readonly bool HardBackpressure;
    public readonly string SummaryLine;
    public readonly string SamplesLine;

    public PlanetLodLoadSnapshot(
        int leavesWithoutMesh,
        int inFlightLeafBuilds,
        int pendingAsyncCompletions,
        int meshQueueCount,
        bool splitBackpressure,
        bool hardBackpressure,
        string summaryLine,
        string samplesLine
    )
    {
        LeavesWithoutMesh = leavesWithoutMesh;
        InFlightLeafBuilds = inFlightLeafBuilds;
        PendingAsyncCompletions = pendingAsyncCompletions;
        MeshQueueCount = meshQueueCount;
        SplitBackpressure = splitBackpressure;
        HardBackpressure = hardBackpressure;
        SummaryLine = summaryLine;
        SamplesLine = samplesLine;
    }
}
