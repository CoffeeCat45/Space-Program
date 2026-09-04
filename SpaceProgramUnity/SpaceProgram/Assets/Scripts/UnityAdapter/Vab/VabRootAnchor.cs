using UnityEngine;

/// <summary>
/// Fixed root anchor for the VAB rocket tree (roadmap 2.1).
/// All connected blocks are parented under <see cref="VesselRoot"/> at this local pose.
/// </summary>
public static class VabRootAnchor
{
    public const float HeightMeters = 15f;

    public static readonly Vector3 LocalPosition = new Vector3(0f, HeightMeters, 0f);
    public static readonly Quaternion LocalRotation = Quaternion.identity;

    public static SimVector3 SimLocalPosition => new SimVector3(0f, HeightMeters, 0f);
    public static SimQuaternion SimLocalRotation => SimQuaternion.Identity();

    public static Vector3 GetWorldWorkCenter(Transform vesselRoot)
    {
        return vesselRoot != null ?
            vesselRoot.TransformPoint(LocalPosition) :
            LocalPosition;
    }
}
