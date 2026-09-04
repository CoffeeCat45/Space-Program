using UnityEngine;

/// <summary>
/// Marks an attachment node transform authored on a VAB part prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class AttachmentNodeMarker : MonoBehaviour
{
    [SerializeField]
    string ownerBlockId;

    [SerializeField]
    int nodeIndex;

    public string OwnerBlockId
    {
        get => ownerBlockId;
        set => ownerBlockId = value;
    }

    public int NodeIndex => nodeIndex;

    public void Configure(string blockId, int index)
    {
        ownerBlockId = blockId ?? string.Empty;
        nodeIndex = index;
    }
}
