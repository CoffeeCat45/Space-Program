using System;
using System.Collections.Generic;

/// <summary>
/// Runtime VAB edit state in SimulationCore: blueprint tree + suspended placement.
/// UnityAdapter mirrors transforms and prefabs from this data.
/// </summary>
public sealed class VabEditSession
{
    public enum HoldMode
    {
        None,
        NewPart,
        DetachedBranch,
        SinglePart,
        DisjointedPart,
    }

    public sealed class PendingHold
    {
        public HoldMode mode;
        public string primaryBlockId;
        public string partTemplateId;
        public List<string> heldBlockIds = new List<string>();
        public SimVector3 worldPosition;
        public SimQuaternion worldRotation;
    }

    readonly VesselBlueprint blueprint;
    readonly Dictionary<string, BlockSnapshot> blocksById = new Dictionary<string, BlockSnapshot>();
    int nextBlockSerial;

    public VabEditSession(string vesselName)
    {
        blueprint = new VesselBlueprint
        {
            vesselName = string.IsNullOrWhiteSpace(vesselName) ? "Untitled" : vesselName.Trim(),
            blocks = new List<BlockSnapshot>(),
        };
    }

    public VesselBlueprint Blueprint => blueprint;
    public PendingHold Pending { get; private set; }
    public bool IsHolding => Pending != null;

    public bool IsVesselEmpty => GetConnectedBlockCount() == 0;

    public int GetConnectedBlockCount()
    {
        int count = 0;

        foreach (KeyValuePair<string, BlockSnapshot> entry in blocksById)
        {
            if (!entry.Value.isDisjointed)
            {
                count++;
            }
        }

        return count;
    }

    public bool IsDisjointedBlock(string blockId)
    {
        return blocksById.TryGetValue(blockId, out BlockSnapshot snapshot) && snapshot.isDisjointed;
    }

    public IReadOnlyDictionary<string, BlockSnapshot> Blocks => blocksById;

    public string VesselName
    {
        get => blueprint.vesselName;
        set => blueprint.vesselName = string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Trim();
    }

    public void ReplaceBlueprint(VesselBlueprint loadedBlueprint)
    {
        blocksById.Clear();
        blueprint.blocks.Clear();
        nextBlockSerial = 0;

        if (loadedBlueprint == null)
        {
            blueprint.vesselName = "Untitled";
            return;
        }

        blueprint.vesselName = loadedBlueprint.vesselName ?? "Untitled";
        blueprint.schemaVersion = loadedBlueprint.schemaVersion;

        if (loadedBlueprint.blocks == null)
        {
            return;
        }

        for (int i = 0; i < loadedBlueprint.blocks.Count; i++)
        {
            BlockSnapshot snapshot = loadedBlueprint.blocks[i];

            if (snapshot == null || string.IsNullOrEmpty(snapshot.blockId))
            {
                continue;
            }

            blocksById[snapshot.blockId] = snapshot;
            blueprint.blocks.Add(snapshot);
            nextBlockSerial++;
        }
    }

    public string BeginHoldNewPart(string partTemplateId)
    {
        if (string.IsNullOrEmpty(partTemplateId))
        {
            throw new ArgumentException("Part template id is required.", nameof(partTemplateId));
        }

        Pending = new PendingHold
        {
            mode = HoldMode.NewPart,
            primaryBlockId = AllocateBlockId(),
            partTemplateId = partTemplateId,
            worldRotation = SimQuaternion.Identity(),
        };
        Pending.heldBlockIds.Add(Pending.primaryBlockId);
        return Pending.primaryBlockId;
    }

    public List<string> BeginHoldSinglePart(string blockId)
    {
        if (!blocksById.TryGetValue(blockId, out BlockSnapshot snapshot))
        {
            return null;
        }

        string previousParentId = snapshot.parentBlockId;
        ReparentDirectChildren(blockId, previousParentId);

        snapshot.parentBlockId = string.Empty;
        snapshot.attachedToNodeIndex = VabAttachmentIndices.Unset;
        snapshot.isDisjointed = false;

        Pending = new PendingHold
        {
            mode = HoldMode.SinglePart,
            primaryBlockId = blockId,
            partTemplateId = snapshot.partTemplateId,
            heldBlockIds = new List<string> { blockId },
        };

        return Pending.heldBlockIds;
    }

    public List<string> BeginHoldDisjointedPart(string blockId)
    {
        if (!blocksById.TryGetValue(blockId, out BlockSnapshot snapshot) || !snapshot.isDisjointed)
        {
            return null;
        }

        Pending = new PendingHold
        {
            mode = HoldMode.DisjointedPart,
            primaryBlockId = blockId,
            partTemplateId = snapshot.partTemplateId,
            heldBlockIds = new List<string> { blockId },
        };

        return Pending.heldBlockIds;
    }

    void ReparentDirectChildren(string blockId, string newParentId)
    {
        foreach (KeyValuePair<string, BlockSnapshot> entry in blocksById)
        {
            BlockSnapshot child = entry.Value;

            if (child.parentBlockId == blockId)
            {
                child.parentBlockId = newParentId ?? string.Empty;
            }
        }
    }

    public List<string> BeginHoldDetachedBranch(string rootBlockId)
    {
        if (!blocksById.ContainsKey(rootBlockId))
        {
            return null;
        }

        List<string> branchIds = CollectSubtreeBlockIds(rootBlockId);
        Pending = new PendingHold
        {
            mode = HoldMode.DetachedBranch,
            primaryBlockId = rootBlockId,
            partTemplateId = blocksById[rootBlockId].partTemplateId,
            heldBlockIds = branchIds,
        };

        BlockSnapshot rootSnapshot = blocksById[rootBlockId];
        rootSnapshot.parentBlockId = string.Empty;
        rootSnapshot.attachedToNodeIndex = VabAttachmentIndices.Unset;
        rootSnapshot.isDisjointed = false;

        return branchIds;
    }

    public void MarkBlocksConnected(IEnumerable<string> blockIds)
    {
        if (blockIds == null)
        {
            return;
        }

        foreach (string blockId in blockIds)
        {
            if (
                string.IsNullOrEmpty(blockId) ||
                !blocksById.TryGetValue(blockId, out BlockSnapshot snapshot)
            )
            {
                continue;
            }

            snapshot.isDisjointed = false;
            RegisterBlock(snapshot, disjointed: false);
        }
    }

    public void MarkBlockDisjointed(
        string blockId,
        SimVector3 localPosition,
        SimQuaternion localRotation
    )
    {
        if (!blocksById.TryGetValue(blockId, out BlockSnapshot snapshot))
        {
            return;
        }

        snapshot.isDisjointed = true;
        snapshot.parentBlockId = string.Empty;
        snapshot.attachedToNodeIndex = VabAttachmentIndices.Unset;
        snapshot.localPosition = localPosition;
        snapshot.localRotation = localRotation;
        EnsureRemovedFromBlueprint(blockId);
    }

    public void UpdatePendingPose(SimVector3 worldPosition, SimQuaternion worldRotation)
    {
        if (Pending == null)
        {
            return;
        }

        Pending.worldPosition = worldPosition;
        Pending.worldRotation = worldRotation;
    }

    public void CancelPending()
    {
        Pending = null;
    }

    public void Clear()
    {
        blocksById.Clear();
        blueprint.blocks.Clear();
        Pending = null;
        nextBlockSerial = 0;
        blueprint.vesselName = "Untitled";
    }

    public void DeleteBlocks(IEnumerable<string> blockIds)
    {
        if (blockIds == null)
        {
            return;
        }

        HashSet<string> idsToDelete = new HashSet<string>();

        foreach (string blockId in blockIds)
        {
            if (!string.IsNullOrEmpty(blockId))
            {
                idsToDelete.Add(blockId);
            }
        }

        if (idsToDelete.Count == 0)
        {
            return;
        }

        blueprint.blocks.RemoveAll(snapshot => snapshot != null && idsToDelete.Contains(snapshot.blockId));

        foreach (string blockId in idsToDelete)
        {
            blocksById.Remove(blockId);
        }

        for (int i = blueprint.blocks.Count - 1; i >= 0; i--)
        {
            BlockSnapshot snapshot = blueprint.blocks[i];

            if (snapshot != null && idsToDelete.Contains(snapshot.parentBlockId))
            {
                DeleteBlocks(CollectSubtreeBlockIds(snapshot.blockId));
            }
        }
    }

    public BlockSnapshot CommitPendingAsRoot(
        SimVector3 worldPosition,
        SimQuaternion worldRotation
    )
    {
        if (Pending == null || Pending.mode != HoldMode.NewPart)
        {
            return null;
        }

        BlockSnapshot snapshot = new BlockSnapshot
        {
            blockId = Pending.primaryBlockId,
            partTemplateId = Pending.partTemplateId,
            parentBlockId = string.Empty,
            attachedToNodeIndex = -1,
            localPosition = worldPosition,
            localRotation = worldRotation,
        };

        RegisterBlock(snapshot, disjointed: false);
        Pending = null;
        return snapshot;
    }

    public BlockSnapshot CommitPendingAsDisjointed(
        SimVector3 worldPosition,
        SimQuaternion worldRotation
    )
    {
        if (Pending == null)
        {
            return null;
        }

        BlockSnapshot snapshot;

        if (Pending.mode == HoldMode.NewPart)
        {
            snapshot = new BlockSnapshot
            {
                blockId = Pending.primaryBlockId,
                partTemplateId = Pending.partTemplateId,
                parentBlockId = string.Empty,
                attachedToNodeIndex = VabAttachmentIndices.Unset,
                localPosition = worldPosition,
                localRotation = worldRotation,
                isDisjointed = true,
            };
        }
        else if (
            Pending.mode == HoldMode.DetachedBranch ||
            Pending.mode == HoldMode.SinglePart ||
            Pending.mode == HoldMode.DisjointedPart
        )
        {
            snapshot = blocksById[Pending.primaryBlockId];
            snapshot.parentBlockId = string.Empty;
            snapshot.attachedToNodeIndex = VabAttachmentIndices.Unset;
            snapshot.localPosition = worldPosition;
            snapshot.localRotation = worldRotation;
            snapshot.isDisjointed = true;
            EnsureRemovedFromBlueprint(snapshot.blockId);
        }
        else
        {
            return null;
        }

        blocksById[snapshot.blockId] = snapshot;
        Pending = null;
        return snapshot;
    }

    public BlockSnapshot CommitPendingSnap(
        string parentBlockId,
        int parentNodeIndex,
        SimVector3 localPosition,
        SimQuaternion localRotation
    )
    {
        return CommitPendingSnapWithSymmetry(
            parentBlockId,
            parentNodeIndex,
            localPosition,
            localRotation,
            null
        )?[0];
    }

    public List<BlockSnapshot> CommitPendingSnapWithSymmetry(
        string parentBlockId,
        int parentNodeIndex,
        SimVector3 localPosition,
        SimQuaternion localRotation,
        IReadOnlyList<(SimVector3 localPosition, SimQuaternion localRotation)> symmetryLocals,
        IReadOnlyList<StackSymmetryCommit> symmetryStackCommits = null
    )
    {
        if (Pending == null || string.IsNullOrEmpty(parentBlockId))
        {
            return null;
        }

        if (Pending.mode == HoldMode.NewPart)
        {
            List<BlockSnapshot> created = new List<BlockSnapshot>();

            BlockSnapshot primary = new BlockSnapshot
            {
                blockId = Pending.primaryBlockId,
                partTemplateId = Pending.partTemplateId,
                parentBlockId = parentBlockId,
                attachedToNodeIndex = parentNodeIndex,
                localPosition = localPosition,
                localRotation = localRotation,
                isDisjointed = false,
            };

            RegisterBlock(primary, disjointed: false);
            created.Add(primary);

            if (symmetryLocals != null)
            {
                for (int i = 0; i < symmetryLocals.Count; i++)
                {
                    (SimVector3 symmetryLocalPosition, SimQuaternion symmetryLocalRotation) =
                        symmetryLocals[i];

                    BlockSnapshot clone = new BlockSnapshot
                    {
                        blockId = AllocateBlockId(),
                        partTemplateId = Pending.partTemplateId,
                        parentBlockId = parentBlockId,
                        attachedToNodeIndex = parentNodeIndex,
                        localPosition = symmetryLocalPosition,
                        localRotation = symmetryLocalRotation,
                        isDisjointed = false,
                    };

                    RegisterBlock(clone, disjointed: false);
                    created.Add(clone);
                }
            }

            if (symmetryStackCommits != null)
            {
                for (int i = 0; i < symmetryStackCommits.Count; i++)
                {
                    StackSymmetryCommit stackCommit = symmetryStackCommits[i];

                    if (string.IsNullOrEmpty(stackCommit.parentBlockId))
                    {
                        continue;
                    }

                    BlockSnapshot stackClone = new BlockSnapshot
                    {
                        blockId = AllocateBlockId(),
                        partTemplateId = Pending.partTemplateId,
                        parentBlockId = stackCommit.parentBlockId,
                        attachedToNodeIndex = stackCommit.parentNodeIndex,
                        localPosition = stackCommit.localPosition,
                        localRotation = stackCommit.localRotation,
                        isDisjointed = false,
                    };

                    RegisterBlock(stackClone, disjointed: false);
                    created.Add(stackClone);
                }
            }

            Pending = null;
            return created;
        }

        if (
            Pending.mode == HoldMode.DetachedBranch ||
            Pending.mode == HoldMode.SinglePart ||
            Pending.mode == HoldMode.DisjointedPart
        )
        {
            BlockSnapshot rootSnapshot = blocksById[Pending.primaryBlockId];
            rootSnapshot.parentBlockId = parentBlockId;
            rootSnapshot.attachedToNodeIndex = parentNodeIndex;
            rootSnapshot.localPosition = localPosition;
            rootSnapshot.localRotation = localRotation;
            rootSnapshot.isDisjointed = false;
            RegisterBlock(rootSnapshot, disjointed: false);
            Pending = null;
            return new List<BlockSnapshot> { rootSnapshot };
        }

        return null;
    }

    public sealed class StackSymmetryCommit
    {
        public string parentBlockId;
        public int parentNodeIndex;
        public SimVector3 localPosition;
        public SimQuaternion localRotation;
    }

    public sealed class HoldCommitRequest
    {
        public string parentBlockId;
        public int parentNodeIndex;
        public SimVector3 localPosition;
        public SimQuaternion localRotation;
        public IReadOnlyList<(SimVector3 localPosition, SimQuaternion localRotation)> symmetryLocals;
        public IReadOnlyList<StackSymmetryCommit> symmetryStackCommits;
    }

    public bool TryCommitHold(HoldCommitRequest request, out List<BlockSnapshot> createdBlocks)
    {
        createdBlocks = null;

        if (Pending == null)
        {
            return false;
        }

        if (Pending.mode == HoldMode.NewPart && string.IsNullOrEmpty(request.parentBlockId))
        {
            BlockSnapshot rootSnapshot = CommitPendingAsRoot(
                request.localPosition,
                request.localRotation
            );
            createdBlocks = rootSnapshot != null ?
                new List<BlockSnapshot> { rootSnapshot } :
                null;
            return createdBlocks != null;
        }

        if (string.IsNullOrEmpty(request.parentBlockId))
        {
            BlockSnapshot disjointed = CommitPendingAsDisjointed(
                request.localPosition,
                request.localRotation
            );
            createdBlocks = disjointed != null ?
                new List<BlockSnapshot> { disjointed } :
                null;
            return createdBlocks != null;
        }

        createdBlocks = CommitPendingSnapWithSymmetry(
            request.parentBlockId,
            request.parentNodeIndex,
            request.localPosition,
            request.localRotation,
            request.symmetryLocals,
            request.symmetryStackCommits
        );

        return createdBlocks != null && createdBlocks.Count > 0;
    }

    public List<string> CollectSubtreeBlockIds(string rootBlockId)
    {
        List<string> collected = new List<string>();

        if (string.IsNullOrEmpty(rootBlockId) || !blocksById.ContainsKey(rootBlockId))
        {
            return collected;
        }

        Queue<string> queue = new Queue<string>();
        queue.Enqueue(rootBlockId);

        while (queue.Count > 0)
        {
            string currentId = queue.Dequeue();
            collected.Add(currentId);

            foreach (KeyValuePair<string, BlockSnapshot> entry in blocksById)
            {
                BlockSnapshot snapshot = entry.Value;

                if (snapshot.parentBlockId == currentId && !collected.Contains(snapshot.blockId))
                {
                    queue.Enqueue(snapshot.blockId);
                }
            }
        }

        return collected;
    }

    public List<VabSnapSolver.PlacedBlockPose> BuildSnapPoses(
        Func<string, PartTemplate> templateLookup,
        Func<string, SimVector3> worldPositionLookup,
        Func<string, SimQuaternion> worldRotationLookup,
        HashSet<string> excludedBlockIds = null
    )
    {
        List<VabSnapSolver.PlacedBlockPose> poses = new List<VabSnapSolver.PlacedBlockPose>();

        foreach (KeyValuePair<string, BlockSnapshot> entry in blocksById)
        {
            string blockId = entry.Key;

            if (excludedBlockIds != null && excludedBlockIds.Contains(blockId))
            {
                continue;
            }

            BlockSnapshot snapshot = entry.Value;

            if (snapshot.isDisjointed)
            {
                continue;
            }

            PartTemplate template = templateLookup(blockId);

            if (template == null)
            {
                continue;
            }

            poses.Add(new VabSnapSolver.PlacedBlockPose
            {
                blockId = blockId,
                template = template,
                worldPosition = worldPositionLookup(blockId),
                worldRotation = worldRotationLookup(blockId),
            });
        }

        return poses;
    }

    void RegisterBlock(BlockSnapshot snapshot, bool disjointed)
    {
        snapshot.isDisjointed = disjointed;
        blocksById[snapshot.blockId] = snapshot;

        if (!disjointed)
        {
            EnsureInBlueprint(snapshot);
        }
        else
        {
            EnsureRemovedFromBlueprint(snapshot.blockId);
        }
    }

    void EnsureInBlueprint(BlockSnapshot snapshot)
    {
        for (int i = 0; i < blueprint.blocks.Count; i++)
        {
            if (blueprint.blocks[i].blockId == snapshot.blockId)
            {
                blueprint.blocks[i] = snapshot;
                return;
            }
        }

        blueprint.blocks.Add(snapshot);
    }

    void EnsureRemovedFromBlueprint(string blockId)
    {
        blueprint.blocks.RemoveAll(snapshot => snapshot != null && snapshot.blockId == blockId);
    }

    string AllocateBlockId()
    {
        nextBlockSerial++;
        return "block_" + nextBlockSerial.ToString();
    }
}
