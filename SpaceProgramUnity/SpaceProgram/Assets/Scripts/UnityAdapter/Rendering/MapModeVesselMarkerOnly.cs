using UnityEngine;

/// <summary>
/// Map mode keeps only the vessel billboard. Flight renderer enabled/active
/// flags are snapshotted on enter so leaving map does not turn on shrouds,
/// gizmos, or other meshes that were already hidden in flight.
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
sealed class MapModeVesselMarkerOnly : MonoBehaviour
{
    const string MapModeMarkerName = "MapModeMarker";

    public bool MapModeActive;

    Renderer[] capturedRenderers = System.Array.Empty<Renderer>();
    bool[] capturedRendererEnabled = System.Array.Empty<bool>();
    bool[] capturedGameObjectActive = System.Array.Empty<bool>();
    bool hasCapture;

    public void CaptureFlightRendererState()
    {
        if (hasCapture)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        int count = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && !IsUnderMapModeMarker(renderers[i].transform))
            {
                count++;
            }
        }

        capturedRenderers = new Renderer[count];
        capturedRendererEnabled = new bool[count];
        capturedGameObjectActive = new bool[count];
        int writeIndex = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || IsUnderMapModeMarker(renderer.transform))
            {
                continue;
            }

            capturedRenderers[writeIndex] = renderer;
            capturedRendererEnabled[writeIndex] = renderer.enabled;
            capturedGameObjectActive[writeIndex] = renderer.gameObject.activeSelf;
            writeIndex++;
        }

        hasCapture = true;
    }

    public void RestoreFlightRendererState()
    {
        if (!hasCapture)
        {
            return;
        }

        for (int i = 0; i < capturedRenderers.Length; i++)
        {
            Renderer renderer = capturedRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = capturedRendererEnabled[i];
            renderer.gameObject.SetActive(capturedGameObjectActive[i]);
        }

        hasCapture = false;
        capturedRenderers = System.Array.Empty<Renderer>();
        capturedRendererEnabled = System.Array.Empty<bool>();
        capturedGameObjectActive = System.Array.Empty<bool>();
    }

    void LateUpdate()
    {
        if (!MapModeActive)
        {
            return;
        }

        BodyView.ApplyMapModeMarkerOnly(gameObject);
    }

    static bool IsUnderMapModeMarker(Transform transform)
    {
        while (transform != null)
        {
            if (transform.name == MapModeMarkerName)
            {
                return true;
            }

            transform = transform.parent;
        }

        return false;
    }
}
