using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-side snap resolution: trigger-based stack nodes, optional surface attach, symmetry ghosts.
/// </summary>
static class VabSnapSolverAdapter
{
    public const float SnapAcquireMeters = 0.25f;
    public const float SnapPreviewMeters = 1.2f;
    public const float SnapReleaseMeters = 0.4f;
    public const float SoftSnapLerpSpeed = 12f;

    public const float SnapThresholdMeters = SnapAcquireMeters;
    public const float ReleaseThresholdMeters = SnapReleaseMeters;

    public static LayerMask AttachmentNodeMask { get; set; }

    public struct SymmetryClonePose
    {
        public SimVector3 localPosition;
        public SimQuaternion localRotation;
    }

    public struct StackSymmetryClonePose
    {
        public string parentBlockId;
        public int parentNodeIndex;
        public SimVector3 localPosition;
        public SimQuaternion localRotation;
    }

    static readonly List<GameObject> ActiveSymmetryGhosts = new List<GameObject>();
    static readonly List<SymmetryClonePose> PendingSymmetryClones = new List<SymmetryClonePose>();
    static readonly List<StackSymmetryClonePose> PendingStackSymmetryClones = new List<StackSymmetryClonePose>();

    public static IReadOnlyList<SymmetryClonePose> PendingSymmetryClonePoses => PendingSymmetryClones;
    public static IReadOnlyList<StackSymmetryClonePose> PendingStackSymmetryClonePoses =>
        PendingStackSymmetryClones;

    public static void ApplySoftSnapTowardAlignedPose(
        Transform ghostTransform,
        Vector3 targetPosition,
        Quaternion targetRotation
    )
    {
        if (ghostTransform == null)
        {
            return;
        }

        float blend = Mathf.Clamp01(Time.deltaTime * SoftSnapLerpSpeed);
        ghostTransform.SetPositionAndRotation(
            Vector3.Lerp(ghostTransform.position, targetPosition, blend),
            Quaternion.Slerp(ghostTransform.rotation, targetRotation, blend)
        );
    }

    public static bool ExecuteSnapResolution(
        Transform ghostTransform,
        PartVisualAdapter activeAdapter,
        Vector3 cursorWorld,
        IReadOnlyList<VabNodeSnapResolver.SnapTarget> placedTargets,
        Func<VabNodeSnapResolver.SnapTarget, int, bool> isNodeFree,
        string heldBlockId,
        RaycastHit hit,
        bool hasSurfaceHit,
        Func<string, bool> isPlacedBlock,
        int symmetryCount,
        float acquireDistanceMeters,
        float cursorApproachMeters,
        ref VabNodeSnapResolver.NodeSnapMatch stickyNodeMatch,
        ref bool hasStickyNodeMatch
    )
    {
        ClearSymmetryGhosts();

        if (ghostTransform == null || activeAdapter == null)
        {
            ClearSnapTarget(activeAdapter);
            return false;
        }

        string preferredParentBlockId = null;
        Vector3? meshHitPoint = null;

        if (hasSurfaceHit && hit.collider != null)
        {
            PartVisualAdapter hitPart = hit.collider.GetComponentInParent<PartVisualAdapter>();

            if (
                hitPart != null &&
                !string.IsNullOrEmpty(hitPart.BlockId) &&
                hitPart.BlockId != heldBlockId &&
                isPlacedBlock(hitPart.BlockId)
            )
            {
                preferredParentBlockId = hitPart.BlockId;
                meshHitPoint = hit.point;
            }
        }

        float stackApproach = Mathf.Max(acquireDistanceMeters, cursorApproachMeters);

        if (TryResolveNodeSnap(
                ghostTransform,
                activeAdapter,
                cursorWorld,
                placedTargets,
                isNodeFree,
                stackApproach,
                acquireDistanceMeters,
                ref stickyNodeMatch,
                ref hasStickyNodeMatch,
                out VabNodeSnapResolver.NodeSnapMatch nodeMatch,
                preferredParentBlockId,
                meshHitPoint
            ))
        {
            ghostTransform.SetPositionAndRotation(nodeMatch.worldPosition, nodeMatch.worldRotation);
            activeAdapter.TargetParentBlockId = nodeMatch.parentBlockId;
            activeAdapter.TargetParentNodeIndex = nodeMatch.parentNodeIndex;
            activeAdapter.TargetChildNodeIndex = nodeMatch.childNodeIndex;
            return true;
        }

        if (TryResolveAttachmentTriggerSnap(
                ghostTransform,
                activeAdapter,
                cursorWorld,
                placedTargets,
                isNodeFree,
                heldBlockId,
                isPlacedBlock,
                acquireDistanceMeters,
                ref stickyNodeMatch,
                ref hasStickyNodeMatch,
                out VabNodeSnapResolver.NodeSnapMatch triggerMatch
            ))
        {
            ghostTransform.SetPositionAndRotation(triggerMatch.worldPosition, triggerMatch.worldRotation);
            activeAdapter.TargetParentBlockId = triggerMatch.parentBlockId;
            activeAdapter.TargetParentNodeIndex = triggerMatch.parentNodeIndex;
            activeAdapter.TargetChildNodeIndex = triggerMatch.childNodeIndex;
            return true;
        }

        if (
            hasSurfaceHit &&
            hit.collider != null &&
            TryResolveSurfaceSnap(
                ghostTransform,
                activeAdapter,
                heldBlockId,
                hit,
                isPlacedBlock,
                symmetryCount,
                out VabNodeSnapResolver.SnapTarget parentTarget
            )
        )
        {
            activeAdapter.TargetParentBlockId = parentTarget.blockId;
            activeAdapter.TargetParentNodeIndex = VabAttachmentIndices.SurfaceAttach;
            activeAdapter.TargetChildNodeIndex = VabAttachmentIndices.Unset;
            hasStickyNodeMatch = false;
            return true;
        }

        ClearSnapTarget(activeAdapter);
        return false;
    }

    public static void ClearSnapTarget(PartVisualAdapter adapter)
    {
        if (adapter != null)
        {
            adapter.ClearSnapTarget();
        }
    }

    public static void ClearSymmetryGhosts()
    {
        for (int i = 0; i < ActiveSymmetryGhosts.Count; i++)
        {
            if (ActiveSymmetryGhosts[i] != null)
            {
                UnityEngine.Object.Destroy(ActiveSymmetryGhosts[i]);
            }
        }

        ActiveSymmetryGhosts.Clear();
        PendingSymmetryClones.Clear();
        PendingStackSymmetryClones.Clear();
    }

    public static void GenerateStackSnapSymmetry(
        GameObject ghostObject,
        PartVisualAdapter ghostAdapter,
        VabNodeSnapResolver.NodeSnapMatch primaryMatch,
        int childNodeIndex,
        IReadOnlyList<PlacedPart> symmetricParents,
        int symmetryCount
    )
    {
        ClearSymmetryGhosts();

        if (
            symmetryCount <= 1 ||
            ghostObject == null ||
            ghostAdapter == null ||
            symmetricParents == null ||
            symmetricParents.Count <= 1
        )
        {
            return;
        }

        if (
            !ghostAdapter.TryGetNodeWorldDirection(childNodeIndex, out Vector3 childNodeDirection)
        )
        {
            return;
        }

        Vector3 savedPosition = ghostObject.transform.position;
        Quaternion savedRotation = ghostObject.transform.rotation;
        int clonesCreated = 0;
        int maxClones = symmetryCount - 1;

        for (int parentIndex = 0; parentIndex < symmetricParents.Count; parentIndex++)
        {
            if (clonesCreated >= maxClones)
            {
                break;
            }

            PlacedPart parentPart = symmetricParents[parentIndex];

            if (
                parentPart == null ||
                parentPart.blockId == primaryMatch.parentBlockId
            )
            {
                continue;
            }

            PartVisualAdapter parentAdapter = parentPart.transform.GetComponent<PartVisualAdapter>();

            if (
                parentAdapter == null ||
                !parentAdapter.TryGetNodeWorldPosition(
                    primaryMatch.parentNodeIndex,
                    out Vector3 parentNodeWorld
                ) ||
                !parentAdapter.TryGetNodeWorldDirection(
                    primaryMatch.parentNodeIndex,
                    out Vector3 parentNodeDirection
                )
            )
            {
                continue;
            }

            ghostObject.transform.SetPositionAndRotation(savedPosition, savedRotation);

            if (
                !VabNodeSnapResolver.TryComputeAlignedPose(
                    ghostObject.transform,
                    ghostAdapter,
                    childNodeIndex,
                    childNodeDirection,
                    parentNodeWorld,
                    parentNodeDirection,
                    out Vector3 alignedPosition,
                    out Quaternion alignedRotation
                )
            )
            {
                continue;
            }

            ghostObject.transform.SetPositionAndRotation(alignedPosition, alignedRotation);

            Vector3 localPosition = parentPart.transform.InverseTransformPoint(alignedPosition);
            Quaternion localRotation =
                Quaternion.Inverse(parentPart.transform.rotation) * alignedRotation;

            PendingStackSymmetryClones.Add(new StackSymmetryClonePose
            {
                parentBlockId = parentPart.blockId,
                parentNodeIndex = primaryMatch.parentNodeIndex,
                localPosition = ToSim(localPosition),
                localRotation = ToSim(localRotation),
            });

            GameObject symGhost = UnityEngine.Object.Instantiate(ghostObject);
            symGhost.name = ghostObject.name + "_stackSym" + clonesCreated.ToString();
            symGhost.transform.SetPositionAndRotation(alignedPosition, alignedRotation);

            PartVisualAdapter symAdapter = symGhost.GetComponent<PartVisualAdapter>();

            if (symAdapter != null)
            {
                UnityEngine.Object.Destroy(symAdapter);
            }

            Collider[] colliders = symGhost.GetComponentsInChildren<Collider>();

            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                colliders[colliderIndex].enabled = false;
            }

            SetGhostAlpha(symGhost, 0.5f);
            ActiveSymmetryGhosts.Add(symGhost);
            clonesCreated++;
        }

        ghostObject.transform.SetPositionAndRotation(savedPosition, savedRotation);
    }

    static bool TryResolveAttachmentTriggerSnap(
        Transform ghostTransform,
        PartVisualAdapter activeAdapter,
        Vector3 cursorWorld,
        IReadOnlyList<VabNodeSnapResolver.SnapTarget> placedTargets,
        Func<VabNodeSnapResolver.SnapTarget, int, bool> isNodeFree,
        string heldBlockId,
        Func<string, bool> isPlacedBlock,
        float acquireDistanceMeters,
        ref VabNodeSnapResolver.NodeSnapMatch stickyNodeMatch,
        ref bool hasStickyNodeMatch,
        out VabNodeSnapResolver.NodeSnapMatch nodeMatch
    )
    {
        nodeMatch = default;

        if (AttachmentNodeMask.value == 0 || placedTargets == null || placedTargets.Count == 0)
        {
            return false;
        }

        return VabAttachmentSnapResolver.TryFindSnapWithHysteresis(
            activeAdapter,
            ghostTransform,
            placedTargets,
            AttachmentNodeMask,
            cursorWorld,
            acquireDistanceMeters,
            ReleaseThresholdMeters,
            isNodeFree,
            heldBlockId,
            isPlacedBlock,
            ref stickyNodeMatch,
            ref hasStickyNodeMatch,
            out nodeMatch
        );
    }

    static bool TryResolveNodeSnap(
        Transform ghostTransform,
        PartVisualAdapter activeAdapter,
        Vector3 cursorWorld,
        IReadOnlyList<VabNodeSnapResolver.SnapTarget> placedTargets,
        Func<VabNodeSnapResolver.SnapTarget, int, bool> isNodeFree,
        float cursorApproachMeters,
        float snapAcquireMeters,
        ref VabNodeSnapResolver.NodeSnapMatch stickyNodeMatch,
        ref bool hasStickyNodeMatch,
        out VabNodeSnapResolver.NodeSnapMatch nodeMatch,
        string preferredParentBlockId = null,
        Vector3? meshHitPoint = null
    )
    {
        nodeMatch = default;

        if (placedTargets == null || placedTargets.Count == 0 || activeAdapter.NodeCount == 0)
        {
            return false;
        }

        return VabNodeSnapResolver.TryFindNodeSnapWithHysteresis(
            activeAdapter,
            ghostTransform,
            placedTargets,
            isNodeFree,
            cursorWorld,
            cursorApproachMeters,
            snapAcquireMeters,
            ReleaseThresholdMeters,
            ref stickyNodeMatch,
            ref hasStickyNodeMatch,
            out nodeMatch,
            preferredParentBlockId,
            meshHitPoint
        );
    }

    public static bool TryApplySurfaceSnap(
        GameObject ghostObject,
        PartVisualAdapter ghostAdapter,
        string heldBlockId,
        RaycastHit hit,
        Func<string, bool> isPlacedBlock,
        int symmetryCount,
        out string parentBlockId
    )
    {
        parentBlockId = string.Empty;
        ClearSymmetryGhosts();

        if (
            ghostObject == null ||
            ghostAdapter == null ||
            hit.collider == null
        )
        {
            return false;
        }

        PartVisualAdapter hitPart = hit.collider.GetComponentInParent<PartVisualAdapter>();

        if (
            hitPart == null ||
            string.IsNullOrEmpty(hitPart.BlockId) ||
            hitPart.BlockId == heldBlockId ||
            !isPlacedBlock(hitPart.BlockId)
        )
        {
            return false;
        }

        if (Mathf.Abs(Vector3.Dot(hit.normal.normalized, Vector3.up)) > 0.85f)
        {
            return false;
        }

        float ghostRadius = ResolveGhostSurfaceAttachRadius(ghostObject);
        Vector3 ghostRadiusOffset = hit.normal.normalized * ghostRadius;
        ghostObject.transform.position = hit.point + ghostRadiusOffset;
        ghostObject.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);

        ghostAdapter.TargetParentBlockId = hitPart.BlockId;
        ghostAdapter.TargetParentNodeIndex = VabAttachmentIndices.SurfaceAttach;
        ghostAdapter.TargetChildNodeIndex = VabAttachmentIndices.Unset;

        parentBlockId = hitPart.BlockId;

        if (symmetryCount > 1)
        {
            GenerateRadialSymmetry(hitPart.transform, ghostObject, symmetryCount);
        }

        return true;
    }

    static float ResolveGhostSurfaceAttachRadius(GameObject ghostObject)
    {
        if (ghostObject == null)
        {
            return 0.5f;
        }

        Bounds localBounds = PartCatalogHelpers.CalculateLocalBounds(ghostObject);

        if (localBounds.size.sqrMagnitude <= 0.0001f)
        {
            return 0.5f;
        }

        return Mathf.Max(localBounds.extents.x, localBounds.extents.z);
    }

    static bool TryResolveSurfaceSnap(
        Transform ghostTransform,
        PartVisualAdapter ghostAdapter,
        string heldBlockId,
        RaycastHit hit,
        Func<string, bool> isPlacedBlock,
        int symmetryCount,
        out VabNodeSnapResolver.SnapTarget parentTarget
    )
    {
        parentTarget = default;

        if (
            ghostTransform == null ||
            ghostAdapter == null ||
            !TryApplySurfaceSnap(
                ghostTransform.gameObject,
                ghostAdapter,
                heldBlockId,
                hit,
                isPlacedBlock,
                symmetryCount,
                out string parentBlockId
            )
        )
        {
            return false;
        }

        parentTarget = new VabNodeSnapResolver.SnapTarget
        {
            blockId = parentBlockId,
            transform = hit.collider.GetComponentInParent<PartVisualAdapter>()?.transform,
            adapter = hit.collider.GetComponentInParent<PartVisualAdapter>(),
        };
        return true;
    }

    static void GenerateRadialSymmetry(Transform parentTransform, GameObject ghostObject, int count)
    {
        Vector3 localHitPos = parentTransform.InverseTransformPoint(ghostObject.transform.position);
        Quaternion localHitRot =
            Quaternion.Inverse(parentTransform.rotation) * ghostObject.transform.rotation;

        float angleStep = 360f / count;

        for (int i = 1; i < count; i++)
        {
            float currentAngle = angleStep * i;
            Quaternion symmetryRotation = Quaternion.AngleAxis(currentAngle, Vector3.up);

            Vector3 rotatedLocalPos = symmetryRotation * localHitPos;
            Vector3 worldSymmetryPos = parentTransform.TransformPoint(rotatedLocalPos);
            Quaternion worldSymmetryRot =
                parentTransform.rotation * (symmetryRotation * localHitRot);

            PendingSymmetryClones.Add(new SymmetryClonePose
            {
                localPosition = ToSim(rotatedLocalPos),
                localRotation = ToSim(symmetryRotation * localHitRot),
            });

            GameObject symGhost = UnityEngine.Object.Instantiate(ghostObject);
            symGhost.name = ghostObject.name + "_sym" + i.ToString();
            symGhost.transform.position = worldSymmetryPos;
            symGhost.transform.rotation = worldSymmetryRot;

            PartVisualAdapter symAdapter = symGhost.GetComponent<PartVisualAdapter>();

            if (symAdapter != null)
            {
                UnityEngine.Object.Destroy(symAdapter);
            }

            Collider[] colliders = symGhost.GetComponentsInChildren<Collider>();

            for (int c = 0; c < colliders.Length; c++)
            {
                colliders[c].enabled = false;
            }

            SetGhostAlpha(symGhost, 0.5f);
            ActiveSymmetryGhosts.Add(symGhost);
        }
    }

    static void SetGhostAlpha(GameObject root, float alpha)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
            renderer.SetPropertyBlock(block);
        }
    }

    static SimVector3 ToSim(Vector3 value)
    {
        return new SimVector3(value.x, value.y, value.z);
    }

    static SimQuaternion ToSim(Quaternion value)
    {
        return new SimQuaternion(value.x, value.y, value.z, value.w);
    }
}
