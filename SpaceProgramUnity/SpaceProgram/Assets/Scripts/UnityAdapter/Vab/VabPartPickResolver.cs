using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Picks placed VAB parts along a mouse ray. Uses physics hits plus render-bounds
/// fallback so nested / overlapping parts (e.g. engine under tank skirt) stay selectable.
/// </summary>
static class VabPartPickResolver
{
    public enum PickDepth
    {
        Closest,
        Innermost,
        ForHold,
    }

    struct Candidate
    {
        public PlacedPart part;
        public float enterDistance;
    }

    public static bool TryPick(
        Ray ray,
        float maxDistance,
        LayerMask physicsMask,
        IReadOnlyList<PlacedPart> placedParts,
        PlacedPart placementGhost,
        Transform ignoreRoot,
        PickDepth depth,
        out PlacedPart pickedPart
    )
    {
        pickedPart = null;
        List<Candidate> candidates = CollectCandidates(
            ray,
            maxDistance,
            physicsMask,
            placedParts,
            placementGhost,
            ignoreRoot
        );

        if (candidates.Count == 0)
        {
            return false;
        }

        candidates.Sort((left, right) => left.enterDistance.CompareTo(right.enterDistance));

        if (depth == PickDepth.ForHold)
        {
            pickedPart = PickPreferredHoldTarget(candidates);
            return pickedPart != null;
        }

        pickedPart = depth == PickDepth.Innermost ?
            candidates[candidates.Count - 1].part :
            candidates[0].part;
        return pickedPart != null;
    }

    static PlacedPart PickPreferredHoldTarget(List<Candidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        PlacedPart bestPart = candidates[0].part;
        int bestDepth = GetAttachmentDepth(bestPart);

        for (int i = 1; i < candidates.Count; i++)
        {
            PlacedPart candidate = candidates[i].part;
            int candidateDepth = GetAttachmentDepth(candidate);

            if (candidateDepth > bestDepth)
            {
                bestPart = candidate;
                bestDepth = candidateDepth;
            }
        }

        return bestPart;
    }

    static int GetAttachmentDepth(PlacedPart part)
    {
        int depth = 0;

        while (part?.parent != null)
        {
            depth++;
            part = part.parent;
        }

        return depth;
    }

    static List<Candidate> CollectCandidates(
        Ray ray,
        float maxDistance,
        LayerMask physicsMask,
        IReadOnlyList<PlacedPart> placedParts,
        PlacedPart placementGhost,
        Transform ignoreRoot
    )
    {
        Dictionary<string, Candidate> bestByBlockId =
            new Dictionary<string, Candidate>(StringComparer.Ordinal);

        RaycastHit[] physicsHits = Physics.RaycastAll(
            ray,
            maxDistance,
            physicsMask,
            QueryTriggerInteraction.Ignore
        );

        if (physicsHits != null)
        {
            for (int i = 0; i < physicsHits.Length; i++)
            {
                RaycastHit hit = physicsHits[i];

                if (ignoreRoot != null &&
                    VabColliderUtility.IsUnderTransform(hit.transform, ignoreRoot))
                {
                    continue;
                }

                if (!TryResolvePlacedPart(hit.collider, placedParts, placementGhost, out PlacedPart part))
                {
                    continue;
                }

                TryAddCandidate(bestByBlockId, part, hit.distance);
            }
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (
                part == null ||
                part == placementGhost ||
                part.transform == null ||
                string.IsNullOrEmpty(part.blockId)
            )
            {
                continue;
            }

            if (ignoreRoot != null &&
                VabColliderUtility.IsUnderTransform(part.transform, ignoreRoot))
            {
                continue;
            }

            Bounds worldBounds = CalculateWorldRenderBounds(part.transform.gameObject);

            if (!worldBounds.IntersectRay(ray, out float enterDistance))
            {
                continue;
            }

            if (enterDistance < 0f || enterDistance > maxDistance)
            {
                continue;
            }

            TryAddCandidate(bestByBlockId, part, enterDistance);
        }

        return new List<Candidate>(bestByBlockId.Values);
    }

    static void TryAddCandidate(
        Dictionary<string, Candidate> bestByBlockId,
        PlacedPart part,
        float enterDistance
    )
    {
        if (part == null || string.IsNullOrEmpty(part.blockId))
        {
            return;
        }

        if (
            bestByBlockId.TryGetValue(part.blockId, out Candidate existing) &&
            existing.enterDistance <= enterDistance
        )
        {
            return;
        }

        bestByBlockId[part.blockId] = new Candidate
        {
            part = part,
            enterDistance = enterDistance,
        };
    }

    static bool TryResolvePlacedPart(
        Collider collider,
        IReadOnlyList<PlacedPart> placedParts,
        PlacedPart placementGhost,
        out PlacedPart part
    )
    {
        part = null;

        if (collider == null)
        {
            return false;
        }

        PartVisualAdapter adapter = collider.GetComponentInParent<PartVisualAdapter>();

        if (adapter == null || string.IsNullOrEmpty(adapter.BlockId))
        {
            return false;
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart candidate = placedParts[i];

            if (
                candidate != null &&
                candidate != placementGhost &&
                candidate.blockId == adapter.BlockId
            )
            {
                part = candidate;
                return true;
            }
        }

        return false;
    }

    static Bounds CalculateWorldRenderBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || ShouldIgnoreRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(root.transform.position, Vector3.one);
        }

        return bounds;
    }

    static bool ShouldIgnoreRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return true;
        }

        string objectName = renderer.gameObject.name;

        if (
            objectName == "Sphere" ||
            objectName.StartsWith(PartPrefabBuilder.NodeIndexPrefix, StringComparison.Ordinal) ||
            objectName == PartPrefabBuilder.NodeTopName ||
            objectName == PartPrefabBuilder.NodeBottomName ||
            objectName == PartPrefabBuilder.ThrustTransformName
        )
        {
            return true;
        }

        return renderer is LineRenderer;
    }
}
