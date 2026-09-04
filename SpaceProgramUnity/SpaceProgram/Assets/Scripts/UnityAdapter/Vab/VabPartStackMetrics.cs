using UnityEngine;

/// <summary>
/// Stack extent helpers for snap probe radius and placement approach distance.
/// </summary>
static class VabPartStackMetrics
{
    public const float MinSnapAcquireMeters = 0.75f;
    public const float MaxSnapAcquireMeters = 2.5f;
    public const float MinStackApproachHorizontalMeters = 2.5f;
    public const float MaxStackApproachHorizontalMeters = 6f;

    public static float ResolveStackHalfHeight(PartVisualAdapter adapter, PartTemplate template)
    {
        float fromAdapter = TryGetHalfHeightFromAdapter(adapter);

        if (fromAdapter > 0f)
        {
            return fromAdapter;
        }

        float fromTemplate = TryGetHalfHeightFromTemplate(template);

        if (fromTemplate > 0f)
        {
            return fromTemplate;
        }

        if (adapter != null)
        {
            Bounds bounds = CalculateRenderBounds(adapter.gameObject);

            if (bounds.size.y > 0.01f)
            {
                return bounds.extents.y;
            }
        }

        return 0.5f;
    }

    public static float ResolveSnapAcquireMeters(PartVisualAdapter adapter, PartTemplate template)
    {
        float halfHeight = ResolveStackHalfHeight(adapter, template);
        return Mathf.Clamp(
            Mathf.Max(MinSnapAcquireMeters, halfHeight * 0.14f),
            MinSnapAcquireMeters,
            MaxSnapAcquireMeters
        );
    }

    public static float ResolveStackApproachHorizontalMeters(
        PartVisualAdapter adapter,
        PartTemplate template
    )
    {
        float halfHeight = ResolveStackHalfHeight(adapter, template);
        return Mathf.Clamp(
            Mathf.Max(MinStackApproachHorizontalMeters, halfHeight * 0.5f),
            MinStackApproachHorizontalMeters,
            MaxStackApproachHorizontalMeters
        );
    }

    static float TryGetHalfHeightFromAdapter(PartVisualAdapter adapter)
    {
        if (adapter == null || adapter.NodeCount == 0)
        {
            return 0f;
        }

        float maxAbsLocalY = 0f;

        for (int nodeIndex = 0; nodeIndex < adapter.NodeCount; nodeIndex++)
        {
            if (!adapter.TryGetNodeLocalPosition(nodeIndex, out Vector3 localPosition))
            {
                continue;
            }

            maxAbsLocalY = Mathf.Max(maxAbsLocalY, Mathf.Abs(localPosition.y));
        }

        return maxAbsLocalY;
    }

    static float TryGetHalfHeightFromTemplate(PartTemplate template)
    {
        if (template?.nodes == null || template.nodes.Count == 0)
        {
            return 0f;
        }

        float maxAbsLocalY = 0f;

        for (int i = 0; i < template.nodes.Count; i++)
        {
            AttachmentNode node = template.nodes[i];

            if (node == null)
            {
                continue;
            }

            maxAbsLocalY = Mathf.Max(
                maxAbsLocalY,
                Mathf.Abs((float)node.localPosition.y)
            );
        }

        return maxAbsLocalY;
    }

    static Bounds CalculateRenderBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (renderer is LineRenderer)
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

    public static float GetBottomStackLocalY(PartTemplate template, PartVisualAdapter adapter = null)
    {
        float fromAdapter = TryGetBottomLocalYFromAdapter(adapter);

        if (!float.IsNegativeInfinity(fromAdapter))
        {
            return fromAdapter;
        }

        if (template?.nodes != null)
        {
            for (int i = 0; i < template.nodes.Count; i++)
            {
                AttachmentNode node = template.nodes[i];

                if (node == null)
                {
                    continue;
                }

                if (node.role == PartAttachmentRoles.StackBottom)
                {
                    return (float)node.localPosition.y;
                }
            }

            for (int i = 0; i < template.nodes.Count; i++)
            {
                AttachmentNode node = template.nodes[i];

                if (node == null)
                {
                    continue;
                }

                if (node.direction.y < -0.85)
                {
                    return (float)node.localPosition.y;
                }
            }
        }

        return -ResolveStackHalfHeight(adapter, template);
    }

    public static void AlignBottomToPlaneY(
        Transform partTransform,
        PartTemplate template,
        float planeWorldY,
        PartVisualAdapter adapter = null
    )
    {
        if (partTransform == null)
        {
            return;
        }

        float bottomLocalY = GetBottomStackLocalY(template, adapter);
        Vector3 bottomWorld = partTransform.TransformPoint(new Vector3(0f, bottomLocalY, 0f));
        Vector3 position = partTransform.position;
        position.y += planeWorldY - bottomWorld.y;
        partTransform.position = position;
    }

    static float TryGetBottomLocalYFromAdapter(PartVisualAdapter adapter)
    {
        if (adapter == null || adapter.NodeCount == 0)
        {
            return float.NegativeInfinity;
        }

        float best = float.PositiveInfinity;
        bool found = false;

        for (int nodeIndex = 0; nodeIndex < adapter.NodeCount; nodeIndex++)
        {
            if (!adapter.TryGetNodeLocalPosition(nodeIndex, out Vector3 localPosition))
            {
                continue;
            }

            if (!adapter.TryGetNodeWorldDirection(nodeIndex, out Vector3 direction))
            {
                continue;
            }

            if (direction.y >= -0.85f)
            {
                continue;
            }

            if (localPosition.y < best)
            {
                best = localPosition.y;
                found = true;
            }
        }

        return found ? best : float.NegativeInfinity;
    }
}
