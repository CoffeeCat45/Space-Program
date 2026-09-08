using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// KSP-style Ap/Pe pin: house icon on the orbit line, letters baked into a quad.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrbitEventBadge : MonoBehaviour
{
    public const string PinChildName = "Pin";
    public const float LabelLocalY = 0.52f;

    MeshRenderer pinRenderer;
    Color tintColor = Color.white;

    public static OrbitEventBadge FindOn(TextMesh marker)
    {
        if (marker == null)
        {
            return null;
        }

        return marker.GetComponentInParent<OrbitEventBadge>(true);
    }

    public static bool ContainsScreenPoint(
        Camera camera,
        Transform badgeRoot,
        Vector2 mousePosition,
        out float distanceToCenterPixels
    )
    {
        distanceToCenterPixels = float.MaxValue;

        if (camera == null || badgeRoot == null)
        {
            return false;
        }

        Transform pinTransform = badgeRoot.Find(PinChildName);
        Renderer pinRenderer = pinTransform != null ?
            pinTransform.GetComponent<Renderer>() :
            null;

        if (pinRenderer == null)
        {
            return false;
        }

        Bounds bounds = pinRenderer.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        bool anyInFront = false;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z
            );
            Vector3 screen = camera.WorldToScreenPoint(corner);

            if (screen.z <= 0.0f)
            {
                continue;
            }

            anyInFront = true;
            minX = Mathf.Min(minX, screen.x);
            minY = Mathf.Min(minY, screen.y);
            maxX = Mathf.Max(maxX, screen.x);
            maxY = Mathf.Max(maxY, screen.y);
        }

        if (!anyInFront)
        {
            return false;
        }

        const float pixelPadding = 4.0f;
        minX -= pixelPadding;
        minY -= pixelPadding;
        maxX += pixelPadding;
        maxY += pixelPadding;

        if (
            mousePosition.x < minX ||
            mousePosition.x > maxX ||
            mousePosition.y < minY ||
            mousePosition.y > maxY
        )
        {
            return false;
        }

        Vector2 screenCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        distanceToCenterPixels = Vector2.Distance(mousePosition, screenCenter);
        return true;
    }

    public void Initialize(Color pinColor, string label)
    {
        tintColor = Color.white;
        EnsurePin(pinColor, label);
        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (pinRenderer == null)
        {
            return;
        }

        Color color = highlighted ?
            new Color(1.18f, 1.18f, 1.18f, 1.0f) :
            tintColor;
        RuntimeMaterialFactory.ApplyColor(pinRenderer.material, color);
    }

    void EnsurePin(Color pinColor, string label)
    {
        Transform pinTransform = transform.Find(PinChildName);
        GameObject pinObject;

        if (pinTransform == null)
        {
            pinObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pinObject.name = PinChildName;
            pinObject.transform.SetParent(transform, false);
            pinObject.transform.localRotation = Quaternion.identity;
            pinObject.transform.localScale = Vector3.one;

            Collider[] colliders = pinObject.GetComponents<Collider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(colliders[i]);
                }
                else
                {
                    Object.DestroyImmediate(colliders[i]);
                }
            }

            pinRenderer = pinObject.GetComponent<MeshRenderer>();
        }
        else
        {
            pinObject = pinTransform.gameObject;
            pinRenderer = pinObject.GetComponent<MeshRenderer>();

            if (pinRenderer == null)
            {
                pinRenderer = pinObject.AddComponent<MeshRenderer>();
            }
        }

        // Unity Quad is centered; shift so the house tip sits on the orbit line.
        pinObject.transform.localPosition = new Vector3(0.0f, 0.50f, 0.0f);

        MeshFilter meshFilter = pinObject.GetComponent<MeshFilter>();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Mesh mesh = meshFilter.mesh;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            Color[] colors = new Color[mesh.vertexCount];

            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }

            mesh.colors = colors;
        }

        Texture iconTexture = OrbitEventBadgeIcon.Get(label, pinColor);
        Material material = RuntimeMaterialFactory.CreateOrbitOverlayMarkerMaterial(
            Color.white
        );
        RuntimeMaterialFactory.ApplyMainTexture(material, iconTexture);
        pinRenderer.sharedMaterial = material;
        pinRenderer.shadowCastingMode = ShadowCastingMode.Off;
        pinRenderer.receiveShadows = false;
        pinRenderer.lightProbeUsage = LightProbeUsage.Off;
        pinRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        pinRenderer.allowOcclusionWhenDynamic = false;
        pinRenderer.enabled = true;
        RuntimeMaterialFactory.ApplyOrbitOverlayRenderer(pinRenderer);
        RuntimeMaterialFactory.ApplyMainTexture(pinRenderer.material, iconTexture);
        RuntimeMaterialFactory.ApplyColor(pinRenderer.material, Color.white);
        OrbitLineVisualUtility.ApplyOrbitOverlayLayer(pinObject);
    }
}
