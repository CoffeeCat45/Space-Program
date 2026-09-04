using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime binding for attachment nodes authored on VAB part prefabs.
/// </summary>
[DisallowMultipleComponent]
public sealed class PartVisualAdapter : MonoBehaviour
{
    readonly List<Transform> nodeTransforms = new List<Transform>();
    readonly List<AttachmentNodeMarker> ownedMarkers = new List<AttachmentNodeMarker>();

    Transform thrustTransform;

    [SerializeField]
    string blockId;

    [SerializeField]
    string partTemplateId;

    public string BlockId
    {
        get => blockId;
        set => blockId = value;
    }

    public string PartTemplateId
    {
        get => partTemplateId;
        set => partTemplateId = value;
    }

    public int NodeCount => nodeTransforms.Count;

    public string TargetParentBlockId { get; set; }
    public int TargetParentNodeIndex { get; set; } = VabAttachmentIndices.Unset;
    public int TargetChildNodeIndex { get; set; } = VabAttachmentIndices.Unset;

    public Transform ThrustTransform => thrustTransform;

    public bool IsDisjointed { get; set; }

    void Awake()
    {
        if (!string.IsNullOrWhiteSpace(partTemplateId))
        {
            RebuildAttachmentNodes();
        }
    }

    public void Bind(string newBlockId, string templateId)
    {
        blockId = newBlockId;
        partTemplateId = templateId;
        ClearSnapTarget();
        RebuildAttachmentNodes();
    }

    public void RebuildAttachmentNodesFromMeshBounds()
    {
        RebuildAttachmentNodes();
    }

    public void ClearSnapTarget()
    {
        TargetParentBlockId = string.Empty;
        TargetParentNodeIndex = VabAttachmentIndices.Unset;
        TargetChildNodeIndex = VabAttachmentIndices.Unset;
    }

    public bool TryGetNodeWorldPosition(int nodeIndex, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (TryGetOwnedMarker(nodeIndex, out AttachmentNodeMarker marker))
        {
            worldPosition = marker.transform.position;
            return true;
        }

        if (!TryGetNodeTransform(nodeIndex, out Transform nodeTransform))
        {
            return false;
        }

        worldPosition = nodeTransform.position;
        return true;
    }

    public bool TryGetNodeWorldDirection(int nodeIndex, out Vector3 worldDirection)
    {
        worldDirection = default;

        if (TryGetOwnedMarker(nodeIndex, out AttachmentNodeMarker marker))
        {
            worldDirection = marker.transform.up;
            return true;
        }

        if (!TryGetNodeTransform(nodeIndex, out Transform nodeTransform))
        {
            return false;
        }

        worldDirection = nodeTransform.up;
        return true;
    }

    public bool TryGetNodeLocalPosition(int nodeIndex, out Vector3 localPosition)
    {
        localPosition = default;

        if (!TryGetNodeTransform(nodeIndex, out Transform nodeTransform))
        {
            return false;
        }

        localPosition = transform.InverseTransformPoint(nodeTransform.position);
        return true;
    }

    public bool TryGetNodeTransform(int nodeIndex, out Transform nodeTransform)
    {
        if (nodeIndex < 0 || nodeIndex >= nodeTransforms.Count)
        {
            nodeTransform = null;
            return false;
        }

        nodeTransform = nodeTransforms[nodeIndex];

        if (nodeTransform != null)
        {
            return true;
        }

        if (TryGetOwnedMarker(nodeIndex, out AttachmentNodeMarker marker))
        {
            nodeTransform = marker.transform;
            return nodeTransform != null;
        }

        return false;
    }

    public void RebuildAttachmentNodes()
    {
        nodeTransforms.Clear();
        ownedMarkers.Clear();
        thrustTransform = null;

        if (string.IsNullOrWhiteSpace(partTemplateId))
        {
            return;
        }

        PartPrefabBuilder.BindAuthoredNodes(gameObject, blockId);
        CacheAuthoredTransforms();
    }

    void CacheAuthoredTransforms()
    {
        nodeTransforms.Clear();
        ownedMarkers.Clear();
        thrustTransform = PartPrefabBuilder.FindThrustTransform(transform);

        AttachmentNodeMarker[] markers =
            GetComponentsInChildren<AttachmentNodeMarker>(true);

        if (markers == null || markers.Length == 0)
        {
            return;
        }

        int maxNodeIndex = -1;
        List<(int index, Transform nodeTransform)> discovered = new List<(int, Transform)>();

        for (int i = 0; i < markers.Length; i++)
        {
            AttachmentNodeMarker marker = markers[i];

            if (
                marker == null ||
                !PartPrefabBuilder.IsMarkerOwnedByPartRoot(marker, gameObject)
            )
            {
                continue;
            }

            ownedMarkers.Add(marker);
            int nodeIndex = marker.NodeIndex;
            discovered.Add((nodeIndex, marker.transform));
            maxNodeIndex = Mathf.Max(maxNodeIndex, nodeIndex);
        }

        for (int i = 0; i <= maxNodeIndex; i++)
        {
            nodeTransforms.Add(null);
        }

        for (int i = 0; i < discovered.Count; i++)
        {
            (int index, Transform nodeTransform) entry = discovered[i];

            if (entry.index >= 0 && entry.index < nodeTransforms.Count)
            {
                nodeTransforms[entry.index] = entry.nodeTransform;
            }
        }
    }

    bool TryGetOwnedMarker(int nodeIndex, out AttachmentNodeMarker marker)
    {
        for (int i = 0; i < ownedMarkers.Count; i++)
        {
            AttachmentNodeMarker candidate = ownedMarkers[i];

            if (candidate != null && candidate.NodeIndex == nodeIndex)
            {
                marker = candidate;
                return true;
            }
        }

        marker = null;
        return false;
    }
}
