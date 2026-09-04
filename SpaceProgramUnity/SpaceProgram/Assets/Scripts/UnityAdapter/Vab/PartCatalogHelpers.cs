using System;
using System.Collections.Generic;
using UnityEngine;

public static class PartCatalogHelpers
{
    public static Bounds CalculateLocalBounds(GameObject root)
    {
        if (root == null)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || ShouldIgnoreRendererForBounds(renderer))
            {
                continue;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Bounds meshLocalBounds = TransformMeshBoundsToRootLocal(
                root.transform,
                filter.transform,
                filter.sharedMesh.bounds
            );

            if (!hasBounds)
            {
                bounds = meshLocalBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(meshLocalBounds.min);
                bounds.Encapsulate(meshLocalBounds.max);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one);
        }

        return bounds;
    }

    static bool ShouldIgnoreRendererForBounds(Renderer renderer)
    {
        if (renderer == null)
        {
            return true;
        }

        string objectName = renderer.gameObject.name;

        return objectName.StartsWith("Node_Index_", StringComparison.Ordinal) ||
            objectName == PartPrefabBuilder.NodeTopName ||
            objectName == PartPrefabBuilder.NodeBottomName ||
            objectName == PartPrefabBuilder.ThrustTransformName ||
            objectName == "FocusPlaneHit" ||
            renderer is LineRenderer;
    }

    /// <summary>
    /// Pick-geometry bounds in part-root local space (matches VAB colliders).
    /// </summary>
    public static Bounds CalculateColliderLocalBounds(GameObject root)
    {
        if (root == null)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (ShouldIgnoreColliderForBounds(collider))
            {
                continue;
            }

            Bounds worldBounds = collider.bounds;
            Vector3 localCenter = root.transform.InverseTransformPoint(worldBounds.center);
            Vector3 localExtents = root.transform.InverseTransformVector(worldBounds.extents);
            localExtents.x = Mathf.Abs(localExtents.x);
            localExtents.y = Mathf.Abs(localExtents.y);
            localExtents.z = Mathf.Abs(localExtents.z);

            Bounds localBounds = new Bounds(localCenter, localExtents * 2f);

            if (!hasBounds)
            {
                bounds = localBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(localBounds.min);
                bounds.Encapsulate(localBounds.max);
            }
        }

        if (!hasBounds)
        {
            return CalculateLocalBounds(root);
        }

        return bounds;
    }

    static bool ShouldIgnoreColliderForBounds(Collider collider)
    {
        if (collider == null)
        {
            return true;
        }

        if (collider.isTrigger)
        {
            return true;
        }

        string objectName = collider.gameObject.name;

        return objectName.StartsWith("Node_Index_", StringComparison.Ordinal) ||
            objectName == PartPrefabBuilder.NodeTopName ||
            objectName == PartPrefabBuilder.NodeBottomName ||
            objectName == "thrustTransform" ||
            objectName == "FocusPlaneHit";
    }

    public static List<AttachmentNode> InferNodes(Bounds localBounds, string category)
    {
        return InferNodes(string.Empty, localBounds, category);
    }

    public static List<AttachmentNode> InferNodes(
        string partId,
        Bounds localBounds,
        string category
    )
    {
        if (IsStackCategory(category))
        {
            return InferStackNodes(localBounds);
        }

        return new List<AttachmentNode>();
    }

    public static bool IsStackCategory(string category)
    {
        return category == PartDatabase.CategoryCommand ||
            category == PartDatabase.CategoryFuelTank ||
            category == PartDatabase.CategoryEngine;
    }

    /// <summary>
    /// KSP-style stack: node 0 = top (+Y), node 1 = bottom (-Y) at collider face centers.
    /// </summary>
    public static List<AttachmentNode> InferStackNodes(Bounds localBounds)
    {
        Vector3 centerXZ = new Vector3(localBounds.center.x, 0f, localBounds.center.z);

        return new List<AttachmentNode>
        {
            new AttachmentNode(
                new SimVector3(centerXZ.x, localBounds.max.y, centerXZ.z),
                SimVector3.UnitY(),
                PartAttachmentRoles.StackTop
            ),
            new AttachmentNode(
                new SimVector3(centerXZ.x, localBounds.min.y, centerXZ.z),
                SimVector3.UnitY().Multiply(-1.0),
                PartAttachmentRoles.StackBottom
            ),
        };
    }

    /// <summary>
    /// Render bounds for stack resolution. Optional mesh-name filter excludes decorative submeshes.
    /// </summary>
    public static Bounds GetStackGeometryBounds(GameObject root, string[] meshNameIncludes)
    {
        if (root == null)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        bool useFilter = meshNameIncludes != null && meshNameIncludes.Length > 0;
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || ShouldIgnoreRendererForBounds(renderer))
            {
                continue;
            }

            if (useFilter && !MatchesMeshNameFilter(renderer.gameObject.name, meshNameIncludes))
            {
                continue;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Bounds meshLocalBounds = TransformMeshBoundsToRootLocal(
                root.transform,
                filter.transform,
                filter.sharedMesh.bounds
            );

            if (!hasBounds)
            {
                bounds = meshLocalBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(meshLocalBounds.min);
                bounds.Encapsulate(meshLocalBounds.max);
            }
        }

        if (!hasBounds)
        {
            return CalculateLocalBounds(root);
        }

        return bounds;
    }

    static bool MatchesMeshNameFilter(string objectName, string[] meshNameIncludes)
    {
        for (int i = 0; i < meshNameIncludes.Length; i++)
        {
            string token = meshNameIncludes[i];

            if (
                !string.IsNullOrWhiteSpace(token) &&
                objectName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Union of render + pick collider bounds so stack nodes sit on visible faces.
    /// </summary>
    public static Bounds GetStackAttachmentBounds(GameObject root)
    {
        Bounds bounds = CalculateLocalBounds(root);
        Bounds colliderBounds = CalculateColliderLocalBounds(root);

        if (colliderBounds.size.sqrMagnitude > 1e-8f)
        {
            bounds.Encapsulate(colliderBounds.min);
            bounds.Encapsulate(colliderBounds.max);
        }

        return bounds;
    }

    /// <summary>
    /// Legacy helper — do not use at runtime. Stack nodes come from baked <c>.partdef.json</c>.
    /// </summary>
    [Obsolete("Stack nodes are frozen in .partdef.json at import time.")]
    public static List<AttachmentNode> AlignStackNodesToBounds(
        GameObject root,
        List<AttachmentNode> sourceNodes
    )
    {
        if (root == null || sourceNodes == null || sourceNodes.Count == 0)
        {
            return sourceNodes;
        }

        Bounds bounds = GetStackAttachmentBounds(root);
        Vector3 centerXZ = new Vector3(bounds.center.x, 0f, bounds.center.z);
        List<AttachmentNode> aligned = new List<AttachmentNode>(sourceNodes.Count);

        for (int i = 0; i < sourceNodes.Count; i++)
        {
            AttachmentNode node = sourceNodes[i];
            Vector3 direction = ToUnityVector(node.direction).normalized;
            Vector3 fallback = ToUnityVector(node.localPosition);
            float y = ResolveStackFaceY(bounds, direction, fallback.y);

            aligned.Add(
                new AttachmentNode(
                    new SimVector3(centerXZ.x, y, centerXZ.z),
                    node.direction
                )
            );
        }

        aligned.Sort(CompareStackNodeOrder);
        return aligned;
    }

    static float ResolveStackFaceY(Bounds bounds, Vector3 direction, float fallbackY)
    {
        if (direction.y > 0.85f)
        {
            return bounds.max.y;
        }

        if (direction.y < -0.85f)
        {
            return bounds.min.y;
        }

        return fallbackY;
    }

    static int CompareStackNodeOrder(AttachmentNode left, AttachmentNode right)
    {
        float leftDot = ToUnityVector(left.direction).y;
        float rightDot = ToUnityVector(right.direction).y;
        return rightDot.CompareTo(leftDot);
    }

    static Vector3 ToUnityVector(SimVector3 value)
    {
        return new Vector3((float)value.x, (float)value.y, (float)value.z);
    }

    public static float GuessDryMass(string partId, string category)
    {
        if (TryGetKspStats(partId, out KspPartStats stats))
        {
            return stats.dryMass;
        }

        switch (category)
        {
            case PartDatabase.CategoryCommand:
                return 1f;
            case PartDatabase.CategoryFuelTank:
                return 0.5f;
            case PartDatabase.CategoryEngine:
                return 1f;
            default:
                return 0.25f;
        }
    }

    public static float GuessFuelCapacity(string partId, string category)
    {
        if (TryGetKspStats(partId, out KspPartStats stats))
        {
            return stats.fuelCapacity;
        }

        return category == PartDatabase.CategoryFuelTank ? 100f : 0f;
    }

    public static float GuessMaxThrustVac(string partId, string category)
    {
        if (TryGetKspStats(partId, out KspPartStats stats))
        {
            return stats.maxThrustVac;
        }

        return category == PartDatabase.CategoryEngine ? 0f : 0f;
    }

    public static float GuessIspVacuum(string partId, string category)
    {
        if (TryGetKspStats(partId, out KspPartStats stats))
        {
            return stats.ispVacuum;
        }

        return 0f;
    }

    struct KspPartStats
    {
        public float dryMass;
        public float fuelCapacity;
        public float maxThrustVac;
        public float ispVacuum;
    }

    static readonly Dictionary<string, KspPartStats> KspStatsByPartId =
        new Dictionary<string, KspPartStats>(StringComparer.OrdinalIgnoreCase)
        {
            // Command
            { "restock-mk1-pod", Stats(0.84f) },
            { "restock-cupola-1", Stats(0.76f) },
            { "restock-mk2-pod", Stats(0.8f) },
            { "restock-pod-sphere-1", Stats(0.75f) },
            // Engines (mass t, thrust kN vac, isp s)
            { "restock-engine-terrier-1", Stats(0.5f, 0f, 60f, 345f) },
            { "restock-engine-reliant-2", Stats(1.25f, 0f, 240f, 310f) },
            { "restock-engine-ant-1", Stats(0.02f, 0f, 2f, 315f) },
            { "restock-engine-boar-1", Stats(2.5f, 0f, 1000f, 300f) },
            // Fuel tanks 1.25 m (LF + Oxidizer units as liters)
            { "restock-fueltank-125-1", Stats(2.5f, 1680f) },
            { "restock-fueltank-125-2", Stats(0.5f, 400f) },
            { "restock-fueltank-125-4", Stats(0.125f, 100f) },
            // Fuel tanks 0.625 m — Oscar-B baseline + height-scaled variants
            { "restock-fueltank-0625-4", Stats(0.025f, 80f) },
            { "restock-fueltank-0625-1", Stats(0.092f, 295f) },
            { "restock-fueltank-0625-2", Stats(0.093f, 298f) },
            { "restock-fueltank-0625-3", Stats(0.048f, 154f) },
            { "restock-fueltank-0625-5", Stats(0.014f, 43f) },
            // Fuel tanks 3.75 m
            { "restock-fueltank-375-1", Stats(5f, 14400f) },
            { "restock-fueltank-375-2", Stats(3f, 7200f) },
            { "restock-fueltank-375-3", Stats(1.5f, 3600f) },
            { "restock-fueltank-375-4", Stats(0.75f, 1800f) },
        };

    static KspPartStats Stats(
        float dryMass,
        float fuelCapacity = 0f,
        float maxThrustVac = 0f,
        float ispVacuum = 0f
    )
    {
        return new KspPartStats
        {
            dryMass = dryMass,
            fuelCapacity = fuelCapacity,
            maxThrustVac = maxThrustVac,
            ispVacuum = ispVacuum,
        };
    }

    static bool TryGetKspStats(string partId, out KspPartStats stats)
    {
        if (!string.IsNullOrWhiteSpace(partId) &&
            KspStatsByPartId.TryGetValue(partId, out stats))
        {
            return true;
        }

        stats = default;
        return false;
    }

    public static string HumanizePartId(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
        {
            return "Part";
        }

        string[] tokens = partId.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> words = new List<string>();

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];

            if (token.Equals("restock", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (token.Length == 0)
            {
                continue;
            }

            if (char.IsDigit(token[0]))
            {
                words.Add(token);
                continue;
            }

            words.Add(char.ToUpperInvariant(token[0]) + token.Substring(1));
        }

        return words.Count > 0 ? string.Join(" ", words) : partId;
    }

    static Bounds TransformMeshBoundsToRootLocal(
        Transform root,
        Transform meshTransform,
        Bounds localMeshBounds
    )
    {
        Vector3 center = localMeshBounds.center;
        Vector3 extents = localMeshBounds.extents;
        bool hasBounds = false;
        Bounds bounds = default;

        AppendRootLocalCorner(root, meshTransform, center, extents, 1f, 1f, 1f, ref bounds, ref hasBounds);
        AppendRootLocalCorner(root, meshTransform, center, extents, 1f, 1f, -1f, ref bounds, ref hasBounds);
        AppendRootLocalCorner(root, meshTransform, center, extents, 1f, -1f, 1f, ref bounds, ref hasBounds);
        AppendRootLocalCorner(root, meshTransform, center, extents, 1f, -1f, -1f, ref bounds, ref hasBounds);
        AppendRootLocalCorner(root, meshTransform, center, extents, -1f, 1f, 1f, ref bounds, ref hasBounds);
        AppendRootLocalCorner(root, meshTransform, center, extents, -1f, 1f, -1f, ref bounds, ref hasBounds);
        AppendRootLocalCorner(root, meshTransform, center, extents, -1f, -1f, 1f, ref bounds, ref hasBounds);
        AppendRootLocalCorner(root, meshTransform, center, extents, -1f, -1f, -1f, ref bounds, ref hasBounds);

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    static void AppendRootLocalCorner(
        Transform root,
        Transform meshTransform,
        Vector3 center,
        Vector3 extents,
        float xSign,
        float ySign,
        float zSign,
        ref Bounds bounds,
        ref bool hasBounds
    )
    {
        Vector3 meshLocalCorner = center + new Vector3(
            extents.x * xSign,
            extents.y * ySign,
            extents.z * zSign
        );
        Vector3 rootLocalCorner = root.InverseTransformPoint(
            meshTransform.TransformPoint(meshLocalCorner)
        );

        if (!hasBounds)
        {
            bounds = new Bounds(rootLocalCorner, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(rootLocalCorner);
    }
}
