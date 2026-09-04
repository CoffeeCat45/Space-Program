using UnityEngine;

/// <summary>
/// Marks the imported VAB hangar / editor-only scene content.
/// Attach to the root of the KSP hangar prefab in the scene (e.g. VABlvl3).
/// Imported KSP props may still carry missing script slots from the original game —
/// clean them via <b>Space Program → VAB → Remove Missing Scripts</b>.
/// </summary>
public class VabSceneEnvironment : MonoBehaviour
{
    [Tooltip("Optional explicit world spawn point for the first/root part.")]
    [SerializeField]
    Transform buildOrigin;

    [Tooltip("Meters above the hangar floor when Build Origin is not assigned.")]
    [SerializeField]
    float buildOriginHeightAboveFloor = 0f;

    public bool TryGetBuildOrigin(out Vector3 worldPosition)
    {
        if (buildOrigin != null)
        {
            worldPosition = buildOrigin.position;
            return true;
        }

        worldPosition = default;
        return false;
    }

    public float BuildOriginHeightAboveFloor => buildOriginHeightAboveFloor;
}
