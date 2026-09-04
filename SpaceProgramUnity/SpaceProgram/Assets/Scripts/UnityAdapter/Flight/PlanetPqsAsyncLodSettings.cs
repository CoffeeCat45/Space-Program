using UnityEngine;

/// <summary>
/// View-cone and backpressure settings shared by sandbox and main-game PQS drivers.
/// </summary>
public sealed class PlanetPqsAsyncLodSettings
{
    public const float DefaultViewConeHalfAngleDegrees = 75f;
    public const float ViewConeUnloadHysteresisDegrees = 14f;
    public const int DefaultMaxMeshCompletionsPerFrame = 2;
    public const int DefaultSyncMeshBuildMaxLod = 2;
    public const int DefaultViewerSyncMeshBuildMaxLod = 6;
    public const int DefaultMaxLeavesWithoutMeshBeforeSplitHold = 24;
    public const int DefaultMaxPendingAsyncBeforeSplitHold = 32;
    public const int DefaultMaxMeshDestroysPerFrame = 8;
    public const int DefaultMaxUnloadMergesPerFrame = 2;

    public bool UseAsyncMeshBuild = true;
    /// <summary>
    /// When true, camera-cone Dead chunks skip mesh allocation. Keep false: residency is the
    /// ship disk; Unity frustum-culls drawing. Collapsing behind the camera rebuilds on turn.
    /// </summary>
    public bool SkipBacksideMeshBuilds = false;
    public int SyncMeshBuildMaxLod = DefaultSyncMeshBuildMaxLod;
    public int ViewerSyncMeshBuildMaxLod = DefaultViewerSyncMeshBuildMaxLod;
    public int MaxMeshCompletionsPerFrame = DefaultMaxMeshCompletionsPerFrame;
    public int MaxLeavesWithoutMeshBeforeSplitHold = DefaultMaxLeavesWithoutMeshBeforeSplitHold;
    public int MaxPendingAsyncBeforeSplitHold = DefaultMaxPendingAsyncBeforeSplitHold;
    public int MaxMeshDestroysPerFrame = DefaultMaxMeshDestroysPerFrame;
    public int MaxUnloadMergesPerFrame = DefaultMaxUnloadMergesPerFrame;

    float viewConeHalfAngleDegrees = DefaultViewConeHalfAngleDegrees;
    float meshAllocationHalfAngleDegrees = DefaultViewConeHalfAngleDegrees;
    float horizonUnloadHalfAngleDegrees =
        DefaultViewConeHalfAngleDegrees + ViewConeUnloadHysteresisDegrees;

    public float ViewConeHalfAngleDegrees
    {
        get => viewConeHalfAngleDegrees;
        set => ApplyViewConeHalfAngleDegrees(value);
    }

    public float HorizonUnloadHalfAngleDegrees => horizonUnloadHalfAngleDegrees;

    public void ApplyViewConeHalfAngleDegrees(float degrees)
    {
        viewConeHalfAngleDegrees = Mathf.Clamp(degrees, 40f, 110f);
        meshAllocationHalfAngleDegrees = viewConeHalfAngleDegrees;
        horizonUnloadHalfAngleDegrees = PlanetLodViewPolicy.GetHorizonUnloadHalfAngleDegrees(
            viewConeHalfAngleDegrees
        );
    }

    public int MeshCullBudgetRemaining { get; set; }
    public int UnloadMergeBudgetRemaining { get; set; }
    public int MeshCompletionBudgetRemaining { get; set; }
    public int LeavesWithoutMesh { get; set; }
    public int InFlightLeafBuilds { get; set; }

    public bool IsSplitBackpressureActive()
    {
        return LeavesWithoutMesh > MaxLeavesWithoutMeshBeforeSplitHold ||
            PlanetFlightChunkMeshPipeline.PendingCount + InFlightLeafBuilds >
            MaxPendingAsyncBeforeSplitHold;
    }

    public bool TryConsumeMeshCullBudget()
    {
        if (MeshCullBudgetRemaining <= 0)
        {
            return false;
        }

        MeshCullBudgetRemaining--;
        return true;
    }

    public bool TryConsumeUnloadMergeBudget()
    {
        if (UnloadMergeBudgetRemaining <= 0)
        {
            return false;
        }

        UnloadMergeBudgetRemaining--;
        return true;
    }
}
