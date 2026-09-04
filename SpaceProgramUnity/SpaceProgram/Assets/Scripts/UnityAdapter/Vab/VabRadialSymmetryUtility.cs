using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds radial symmetry siblings for stack/surface parts sharing the same parent attachment.
/// </summary>
static class VabRadialSymmetryUtility
{
    public static List<PlacedPart> FindSymmetricParents(
        PlacedPart anchor,
        IReadOnlyList<PlacedPart> allParts,
        int symmetryCount
    )
    {
        List<PlacedPart> empty = new List<PlacedPart>();

        if (anchor == null || allParts == null || symmetryCount <= 1)
        {
            return empty;
        }

        string templateId = anchor.template?.id;

        if (string.IsNullOrEmpty(templateId))
        {
            return empty;
        }

        PlacedPart sharedParent = anchor.parent;
        int attachNodeIndex = anchor.attachedToNodeIndex;
        List<PlacedPart> candidates = new List<PlacedPart>();

        for (int i = 0; i < allParts.Count; i++)
        {
            PlacedPart part = allParts[i];

            if (
                part == null ||
                part.isDisjointed ||
                part.template == null ||
                part.template.id != templateId ||
                part.parent != sharedParent ||
                part.attachedToNodeIndex != attachNodeIndex
            )
            {
                continue;
            }

            candidates.Add(part);
        }

        if (candidates.Count <= 1)
        {
            return empty;
        }

        Vector3 origin = sharedParent != null ?
            sharedParent.transform.position :
            anchor.transform.position;
        Vector3 axis = sharedParent != null ?
            sharedParent.transform.up :
            Vector3.up;

        candidates.Sort((left, right) =>
            CompareAngleAroundAxis(left.transform.position, right.transform.position, origin, axis)
        );

        return SelectSymmetricSubset(candidates, anchor, symmetryCount);
    }

    static List<PlacedPart> SelectSymmetricSubset(
        List<PlacedPart> sortedCandidates,
        PlacedPart anchor,
        int symmetryCount
    )
    {
        List<PlacedPart> selected = new List<PlacedPart>();

        if (sortedCandidates.Count == 0 || anchor == null)
        {
            return selected;
        }

        int anchorIndex = sortedCandidates.IndexOf(anchor);

        if (anchorIndex < 0)
        {
            return selected;
        }

        int count = Mathf.Min(symmetryCount, sortedCandidates.Count);
        int step = Mathf.Max(1, sortedCandidates.Count / count);

        for (int i = 0; i < count; i++)
        {
            int pickIndex = (anchorIndex + i * step) % sortedCandidates.Count;
            selected.Add(sortedCandidates[pickIndex]);
        }

        return selected;
    }

    static int CompareAngleAroundAxis(
        Vector3 leftWorld,
        Vector3 rightWorld,
        Vector3 origin,
        Vector3 axis
    )
    {
        float leftAngle = GetAngleAroundAxis(leftWorld, origin, axis);
        float rightAngle = GetAngleAroundAxis(rightWorld, origin, axis);
        return leftAngle.CompareTo(rightAngle);
    }

    static float GetAngleAroundAxis(Vector3 worldPoint, Vector3 origin, Vector3 axis)
    {
        Vector3 flat = worldPoint - origin;
        flat -= Vector3.Project(flat, axis);

        if (flat.sqrMagnitude < 1e-6f)
        {
            return 0f;
        }

        flat.Normalize();
        Vector3 reference = Vector3.Cross(axis, Vector3.right);

        if (reference.sqrMagnitude < 1e-6f)
        {
            reference = Vector3.Cross(axis, Vector3.forward);
        }

        reference.Normalize();
        return Mathf.Atan2(
            Vector3.Dot(Vector3.Cross(reference, flat), axis),
            Vector3.Dot(reference, flat)
        ) * Mathf.Rad2Deg;
    }
}
