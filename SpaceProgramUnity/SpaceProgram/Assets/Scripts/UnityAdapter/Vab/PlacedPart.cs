using UnityEngine;

public sealed class PlacedPart
{
    public string blockId;
    public PartTemplate template;
    public Transform transform;
    public PlacedPart parent;
    public int attachedToNodeIndex = -1;
    public int sourceNodeIndex = -1;
    public bool isDisjointed;
}
