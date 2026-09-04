using System;
using System.Collections.Generic;

/// <summary>
/// Authoritative stack attachment metadata for pilot ReStock parts.
/// Positions are resolved at bake/import time against mesh stack bounds — not at runtime.
/// </summary>
public static class PartStackProfileLibrary
{
    public readonly struct Profile
    {
        public readonly double? TopY;
        public readonly double? BottomY;
        public readonly double? StackHalfHeightMeters;
        public readonly string[] StackMeshNameIncludes;

        public Profile(
            double? topY = null,
            double? bottomY = null,
            double? stackHalfHeightMeters = null,
            string[] stackMeshNameIncludes = null
        )
        {
            TopY = topY;
            BottomY = bottomY;
            StackHalfHeightMeters = stackHalfHeightMeters;
            StackMeshNameIncludes = stackMeshNameIncludes;
        }

        public bool HasExplicitFaces => TopY.HasValue || BottomY.HasValue;
        public bool HasSymmetricStack => StackHalfHeightMeters.HasValue;
    }

    public static bool TryGetProfile(string partTemplateId, out Profile profile)
    {
        profile = default;

        if (string.IsNullOrWhiteSpace(partTemplateId))
        {
            return false;
        }

        if (partTemplateId.Equals("restock-mk1-pod", StringComparison.OrdinalIgnoreCase))
        {
            profile = new Profile(topY: 0.663, bottomY: -0.46);
            return true;
        }

        if (partTemplateId.Equals("restock-cupola-1", StringComparison.OrdinalIgnoreCase))
        {
            profile = new Profile(stackHalfHeightMeters: 0.333);
            return true;
        }

        if (partTemplateId.Equals("restock-fueltank-125-1", StringComparison.OrdinalIgnoreCase))
        {
            // KSP FL-T800 cylinder stack height = 1.875 m (collar mesh extends beyond).
            profile = new Profile(stackHalfHeightMeters: 0.9375);
            return true;
        }

        if (
            partTemplateId.Equals("restock-fueltank-375-1", StringComparison.OrdinalIgnoreCase) ||
            partTemplateId.Equals("restock-fueltank-375-2", StringComparison.OrdinalIgnoreCase) ||
            partTemplateId.Equals("restock-fueltank-375-3", StringComparison.OrdinalIgnoreCase) ||
            partTemplateId.Equals("restock-fueltank-375-4", StringComparison.OrdinalIgnoreCase)
        )
        {
            profile = new Profile(stackHalfHeightMeters: 3.75);
            return true;
        }

        if (partTemplateId.Equals("restock-fueltank-0625-1", StringComparison.OrdinalIgnoreCase))
        {
            profile = new Profile(stackHalfHeightMeters: 0.705);
            return true;
        }

        if (partTemplateId.Equals("restock-engine-terrier-1", StringComparison.OrdinalIgnoreCase))
        {
            profile = new Profile(
                topY: 0.111,
                bottomY: -0.8067,
                stackMeshNameIncludes: new[]
                {
                    "Engine",
                    "EngineCore909",
                    "Frame 2",
                    "Frame 4",
                    "Frame_MLI",
                }
            );
            return true;
        }

        if (partTemplateId.Equals("restock-engine-reliant-2", StringComparison.OrdinalIgnoreCase))
        {
            profile = new Profile(topY: 0.2562, bottomY: -1.656);
            return true;
        }

        return false;
    }

    public static List<AttachmentNode> CreateStackNodes(double topY, double bottomY)
    {
        return new List<AttachmentNode>
        {
            new AttachmentNode(
                new SimVector3(0.0, topY, 0.0),
                SimVector3.UnitY(),
                PartAttachmentRoles.StackTop
            ),
            new AttachmentNode(
                new SimVector3(0.0, bottomY, 0.0),
                SimVector3.UnitY().Multiply(-1.0),
                PartAttachmentRoles.StackBottom
            ),
        };
    }
}
