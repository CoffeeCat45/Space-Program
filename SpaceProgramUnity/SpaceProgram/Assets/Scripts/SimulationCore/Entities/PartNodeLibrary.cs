using System;
using System.Collections.Generic;

/// <summary>
/// Obsolete shim — use <see cref="PartStackProfileLibrary"/> instead.
/// </summary>
[Obsolete("Use PartStackProfileLibrary instead.")]
public static class PartNodeLibrary
{
    public static bool TryGetPilotNodes(string partTemplateId, out List<AttachmentNode> nodes)
    {
        nodes = null;

        if (!PartStackProfileLibrary.TryGetProfile(partTemplateId, out PartStackProfileLibrary.Profile profile))
        {
            return false;
        }

        if (profile.TopY.HasValue && profile.BottomY.HasValue)
        {
            nodes = PartStackProfileLibrary.CreateStackNodes(
                profile.TopY.Value,
                profile.BottomY.Value
            );
            return true;
        }

        if (profile.StackHalfHeightMeters.HasValue)
        {
            double half = profile.StackHalfHeightMeters.Value;
            nodes = PartStackProfileLibrary.CreateStackNodes(half, -half);
            return true;
        }

        return false;
    }

    public static bool TryGetThrustLocalPosition(string partTemplateId, out SimVector3 localPosition)
    {
        localPosition = default;
        return false;
    }

    public static List<AttachmentNode> CreateStackNodesFromHeight(float halfHeightMeters)
    {
        return PartStackProfileLibrary.CreateStackNodes(halfHeightMeters, -halfHeightMeters);
    }
}
