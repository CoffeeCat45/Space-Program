using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads attachment nodes authored on VAB part prefabs (Node_Top, Node_Bottom, …).
/// Does not spawn or move nodes at runtime.
/// </summary>
public static class PartPrefabBuilder
{
    public const string NodeIndexPrefix = "Node_Index_";
    public const string NodeTopName = "Node_Top";
    public const string NodeBottomName = "Node_Bottom";
    public const string ThrustTransformName = "thrustTransform";

    public static bool HasAuthoredNodes(GameObject partRoot)
    {
        return partRoot != null &&
            partRoot.GetComponentInChildren<AttachmentNodeMarker>(true) != null;
    }

    public static List<AttachmentNode> ReadAuthoredNodes(GameObject partRoot)
    {
        List<AttachmentNode> nodes = new List<AttachmentNode>();

        if (partRoot == null)
        {
            return nodes;
        }

        AttachmentNodeMarker[] markers =
            partRoot.GetComponentsInChildren<AttachmentNodeMarker>(true);

        if (markers == null || markers.Length == 0)
        {
            return nodes;
        }

        Array.Sort(markers, CompareMarkersByIndex);
        Transform rootTransform = partRoot.transform;

        for (int i = 0; i < markers.Length; i++)
        {
            AttachmentNodeMarker marker = markers[i];

            if (marker == null || !IsMarkerOwnedByPartRoot(marker, partRoot))
            {
                continue;
            }

            Transform nodeTransform = marker.transform;
            Vector3 localPosition = rootTransform.InverseTransformPoint(nodeTransform.position);
            Vector3 localDirection = rootTransform.InverseTransformDirection(nodeTransform.up).normalized;
            nodes.Add(
                new AttachmentNode(
                    new SimVector3(localPosition.x, localPosition.y, localPosition.z),
                    new SimVector3(localDirection.x, localDirection.y, localDirection.z),
                    InferRole(marker, markers[i].NodeIndex)
                )
            );
        }

        return nodes;
    }

    public static void BindAuthoredNodes(GameObject partRoot, string blockId)
    {
        if (partRoot == null)
        {
            return;
        }

        AttachmentNodeMarker[] markers =
            partRoot.GetComponentsInChildren<AttachmentNodeMarker>(true);

        for (int i = 0; i < markers.Length; i++)
        {
            AttachmentNodeMarker marker = markers[i];

            if (marker == null || !IsMarkerOwnedByPartRoot(marker, partRoot))
            {
                continue;
            }

            marker.Configure(blockId, marker.NodeIndex);
            EnsureNodeTrigger(marker.gameObject);
        }
    }

    /// <summary>
    /// Attachment markers belong to the nearest <see cref="PartVisualAdapter"/> root,
    /// not to nested placed parts parented under this transform.
    /// </summary>
    public static bool IsMarkerOwnedByPartRoot(AttachmentNodeMarker marker, GameObject partRoot)
    {
        if (marker == null || partRoot == null)
        {
            return false;
        }

        PartVisualAdapter ownerAdapter = marker.GetComponentInParent<PartVisualAdapter>();

        if (ownerAdapter != null)
        {
            return ownerAdapter.gameObject == partRoot;
        }

        return marker.transform == partRoot.transform ||
            marker.transform.IsChildOf(partRoot.transform);
    }

    public static Transform FindThrustTransform(Transform partRoot)
    {
        if (partRoot == null)
        {
            return null;
        }

        GameObject partRootObject = partRoot.gameObject;
        Transform[] transforms = partRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate == null || candidate.name != ThrustTransformName)
            {
                continue;
            }

            if (candidate == partRoot)
            {
                return candidate;
            }

            PartVisualAdapter nestedAdapter = candidate.GetComponentInParent<PartVisualAdapter>();

            if (nestedAdapter != null && nestedAdapter.gameObject != partRootObject)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    static int CompareMarkersByIndex(AttachmentNodeMarker left, AttachmentNodeMarker right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return left.NodeIndex.CompareTo(right.NodeIndex);
    }

    static string InferRole(AttachmentNodeMarker marker, int nodeIndex)
    {
        string objectName = marker.gameObject.name;

        if (objectName == NodeTopName || nodeIndex == 0)
        {
            return PartAttachmentRoles.StackTop;
        }

        if (objectName == NodeBottomName || nodeIndex == 1)
        {
            return PartAttachmentRoles.StackBottom;
        }

        return PartAttachmentRoles.Attach;
    }

    public static void RestoreAttachmentNodeLayers(GameObject partRoot, string blockId)
    {
        if (partRoot == null || string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        BindAuthoredNodes(partRoot, blockId);
    }

    static void EnsureNodeTrigger(GameObject nodeObject)
    {
        SphereCollider trigger = nodeObject.GetComponent<SphereCollider>();

        if (trigger == null)
        {
            trigger = nodeObject.AddComponent<SphereCollider>();
        }

        trigger.isTrigger = true;
        trigger.radius = VabAttachmentNodeLayers.TriggerRadiusMeters;
        trigger.center = Vector3.zero;

        int attachmentLayer = LayerMask.NameToLayer(VabAttachmentNodeLayers.LayerName);

        if (attachmentLayer >= 0)
        {
            nodeObject.layer = attachmentLayer;
        }
    }
}
