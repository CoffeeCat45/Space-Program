using UnityEngine;

/// <summary>
/// Debug marker showing where the mouse ray hits the focus plane (VAB roadmap 1.2).
/// </summary>
[DisallowMultipleComponent]
public sealed class VabFocusPlaneCursor : MonoBehaviour
{
    [SerializeField]
    float markerDiameter = 0.18f;

    Transform marker;

    void Awake()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "FocusPlaneHit";
        sphere.transform.SetParent(transform, worldPositionStays: false);
        sphere.transform.localScale = Vector3.one * markerDiameter;

        Collider collider = sphere.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        marker = sphere.transform;
        Renderer markerRenderer = sphere.GetComponent<Renderer>();
        RuntimeMaterialFactory.ApplyColor(
            markerRenderer,
            new Color(0.35f, 0.75f, 1f, 0.85f)
        );
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (marker != null)
        {
            marker.gameObject.SetActive(visible);
        }
    }

    public void SetWorldPosition(Vector3 worldPosition)
    {
        if (marker != null)
        {
            marker.position = worldPosition;
        }
    }

}
