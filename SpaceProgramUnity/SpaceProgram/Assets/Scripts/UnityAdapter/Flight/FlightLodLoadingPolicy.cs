/// <summary>
/// When to drop the optional pad-start loading cover. Does not change LOD split/merge.
/// Waits until the leaf under the rocket is at the current Max LOD, pad span is fine,
/// bootstrap/warmup budgets have finished — not the whole horizon disk.
/// </summary>
public static class FlightLodLoadingPolicy
{
    public const int LevelsBelowMax = 0;
    public const float MinVisibleSeconds = 0.2f;
    public const float NoTerrainGraceSeconds = 0.75f;
    public const float SettleSeconds = 0.75f;
    public const float TimeoutSeconds = 90f;

    public static int GetMinReadyLeafLevel(int activeMaxLodLevel)
    {
        int maxLod = activeMaxLodLevel < 0 ? 0 : activeMaxLodLevel;
        int ready = maxLod - LevelsBelowMax;
        return ready < 0 ? 0 : ready;
    }

    public static bool IsPadShellReady(
        bool hasViewerMesh,
        int viewerLeafLevel,
        int activeMaxLodLevel,
        bool padStillCoarse,
        bool bootstrapping,
        bool warmupActive
    )
    {
        return hasViewerMesh &&
            !padStillCoarse &&
            !bootstrapping &&
            !warmupActive &&
            viewerLeafLevel >= GetMinReadyLeafLevel(activeMaxLodLevel);
    }

    public static bool ShouldReveal(
        bool hasPlanetSphere,
        bool hasQuadtree,
        bool hasViewerMesh,
        int viewerLeafLevel,
        int activeMaxLodLevel,
        bool padStillCoarse,
        bool bootstrapping,
        bool warmupActive,
        float elapsedSeconds,
        float readyStreakSeconds
    )
    {
        if (elapsedSeconds < MinVisibleSeconds)
        {
            return false;
        }

        if (elapsedSeconds >= TimeoutSeconds)
        {
            return true;
        }

        if (!hasPlanetSphere)
        {
            return elapsedSeconds >= NoTerrainGraceSeconds;
        }

        if (!hasQuadtree)
        {
            return false;
        }

        return IsPadShellReady(
                hasViewerMesh,
                viewerLeafLevel,
                activeMaxLodLevel,
                padStillCoarse,
                bootstrapping,
                warmupActive
            ) &&
            readyStreakSeconds >= SettleSeconds;
    }
}
