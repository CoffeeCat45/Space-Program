using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves stack attachment nodes. Runtime uses baked <c>.partdef.json</c> only.
/// Editor import uses <see cref="PartStackProfileLibrary"/> + mesh center to bake partdef once.
/// </summary>
public static class PartStackNodeResolver
{
    public static List<AttachmentNode> ResolveForRuntime(
        string partTemplateId,
        List<AttachmentNode> partDefNodes
    )
    {
        if (partDefNodes != null && partDefNodes.Count > 0)
        {
            return partDefNodes;
        }

        if (
            PartStackProfileLibrary.TryGetProfile(partTemplateId, out PartStackProfileLibrary.Profile profile) &&
            profile.HasExplicitFaces
        )
        {
            return PartStackProfileLibrary.CreateStackNodes(
                profile.TopY.GetValueOrDefault(0.0),
                profile.BottomY.GetValueOrDefault(0.0)
            );
        }

        PartTemplate template = PartDatabase.GetById(partTemplateId);
        string category = template?.category ?? PartDatabase.CategoryMisc;

        if (
            PartStackProfileLibrary.TryGetProfile(partTemplateId, out profile) &&
            profile.StackHalfHeightMeters.HasValue
        )
        {
            double half = profile.StackHalfHeightMeters.Value;
            return PartStackProfileLibrary.CreateStackNodes(half, -half);
        }

        return new List<AttachmentNode>();
    }

    public static List<AttachmentNode> BakeForPrefab(GameObject prefab, string partTemplateId)
    {
        if (!PartStackProfileLibrary.TryGetProfile(partTemplateId, out PartStackProfileLibrary.Profile profile))
        {
            PartTemplate template = PartDatabase.GetById(partTemplateId);
            string category = template?.category ?? PartDatabase.CategoryMisc;
            Bounds bounds = PartCatalogHelpers.GetStackGeometryBounds(prefab, null);
            return PartCatalogHelpers.InferNodes(partTemplateId, bounds, category);
        }

        return ResolveFromProfile(prefab, profile);
    }

    static List<AttachmentNode> ResolveFromProfile(
        GameObject partRoot,
        PartStackProfileLibrary.Profile profile
    )
    {
        Bounds stackBounds = PartCatalogHelpers.GetStackGeometryBounds(
            partRoot,
            profile.StackMeshNameIncludes
        );
        Vector3 center = stackBounds.center;
        Vector3 centerXZ = new Vector3(center.x, 0f, center.z);

        double topY = profile.TopY ?? center.y + profile.StackHalfHeightMeters.GetValueOrDefault(stackBounds.extents.y);
        double bottomY = profile.BottomY ?? center.y - profile.StackHalfHeightMeters.GetValueOrDefault(stackBounds.extents.y);

        return new List<AttachmentNode>
        {
            new AttachmentNode(
                new SimVector3(centerXZ.x, topY, centerXZ.z),
                SimVector3.UnitY(),
                PartAttachmentRoles.StackTop
            ),
            new AttachmentNode(
                new SimVector3(centerXZ.x, bottomY, centerXZ.z),
                SimVector3.UnitY().Multiply(-1.0),
                PartAttachmentRoles.StackBottom
            ),
        };
    }
}
