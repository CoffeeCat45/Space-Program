using UnityEngine;

/// <summary>
/// KSP-style hold pivot: command pods by bottom node, stack parts by top node.
/// </summary>
static class VabGhostPivotUtility
{
    public static int ResolveDefaultHoldNodeIndex(PartTemplate template, PartVisualAdapter adapter)
    {
        if (adapter == null || adapter.NodeCount == 0)
        {
            return 0;
        }

        if (!VabGhostMovementController.TryFindStackTopBottomIndices(adapter, out int topIndex, out int bottomIndex))
        {
            return 0;
        }

        string category = template?.category ?? string.Empty;

        if (string.Equals(category, PartDatabase.CategoryCommand, System.StringComparison.OrdinalIgnoreCase))
        {
            return bottomIndex;
        }

        return topIndex;
    }
}
