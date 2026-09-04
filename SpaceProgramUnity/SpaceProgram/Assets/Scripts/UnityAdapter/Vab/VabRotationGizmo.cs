using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// KSP-style rotation while placing: Q/E yaw, W/S pitch, A/D roll (15° tap or hold).
/// </summary>
[DisallowMultipleComponent]
public class VabRotationGizmo : MonoBehaviour
{
    const int RingSegments = 48;
    const float StepDegrees = 15f;

    [SerializeField]
    float radius = 1.2f;

    [SerializeField]
    float lineWidth = 0.02f;

    [SerializeField]
    float holdSpeedDegrees = 90f;

    Transform target;
    Transform hostTransform;
    LineRenderer xRing;
    LineRenderer yRing;
    LineRenderer zRing;

    void Awake()
    {
        hostTransform = transform;
    }

    public void AttachTo(Transform partTransform, float boundsRadius)
    {
        target = partTransform;
        radius = Mathf.Clamp(boundsRadius * 1.25f, 0.6f, 4f);
        hostTransform.SetParent(null, true);
        SyncToTarget();
        EnsureRings();
        UpdateRingGeometry();
        hostTransform.gameObject.SetActive(true);
    }

    public void Detach()
    {
        target = null;

        if (hostTransform == null)
        {
            return;
        }

        hostTransform.SetParent(null, true);
        hostTransform.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        SyncToTarget();
    }

    void SyncToTarget()
    {
        if (hostTransform == null)
        {
            return;
        }

        if (target == null)
        {
            return;
        }

        hostTransform.SetPositionAndRotation(target.position, target.rotation);
    }

    void Update()
    {
        if (target == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            target.Rotate(Vector3.up, -StepDegrees, Space.World);
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            target.Rotate(Vector3.up, StepDegrees, Space.World);
        }

        if (keyboard.wKey.wasPressedThisFrame)
        {
            target.Rotate(Vector3.right, -StepDegrees, Space.Self);
        }

        if (keyboard.sKey.wasPressedThisFrame)
        {
            target.Rotate(Vector3.right, StepDegrees, Space.Self);
        }

        if (keyboard.aKey.wasPressedThisFrame)
        {
            target.Rotate(Vector3.forward, StepDegrees, Space.Self);
        }

        if (keyboard.dKey.wasPressedThisFrame)
        {
            target.Rotate(Vector3.forward, -StepDegrees, Space.Self);
        }

        float yawHold = 0f;

        if (keyboard.qKey.isPressed)
        {
            yawHold -= holdSpeedDegrees * Time.deltaTime;
        }

        if (keyboard.eKey.isPressed)
        {
            yawHold += holdSpeedDegrees * Time.deltaTime;
        }

        if (Mathf.Abs(yawHold) > 0.0001f)
        {
            target.Rotate(Vector3.up, yawHold, Space.World);
        }
    }

    void EnsureRings()
    {
        xRing = EnsureRing("RingX", new Color(1f, 0.25f, 0.25f, 0.85f));
        yRing = EnsureRing("RingY", new Color(0.25f, 1f, 0.35f, 0.85f));
        zRing = EnsureRing("RingZ", new Color(0.3f, 0.55f, 1f, 0.85f));
    }

    LineRenderer EnsureRing(string ringName, Color color)
    {
        Transform existing = transform.Find(ringName);

        if (existing != null)
        {
            LineRenderer existingRenderer = existing.GetComponent<LineRenderer>();

            if (existingRenderer != null)
            {
                ApplyRingStyle(existingRenderer, color, lineWidth);
                return existingRenderer;
            }
        }

        GameObject ringObject = new GameObject(ringName);
        ringObject.transform.SetParent(transform, false);
        LineRenderer renderer = ringObject.AddComponent<LineRenderer>();
        ApplyRingStyle(renderer, color, lineWidth);
        return renderer;
    }

    static void ApplyRingStyle(LineRenderer renderer, Color color, float width)
    {
        renderer.useWorldSpace = false;
        renderer.loop = true;
        renderer.widthMultiplier = width;
        renderer.positionCount = RingSegments;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void UpdateRingGeometry()
    {
        SetCircle(yRing, Vector3.up, radius);
        SetCircle(xRing, Vector3.right, radius);
        SetCircle(zRing, Vector3.forward, radius);
    }

    static void SetCircle(LineRenderer renderer, Vector3 axis, float circleRadius)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.positionCount = RingSegments;
        Vector3 tangentA;
        Vector3 tangentB;

        if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.95f)
        {
            tangentA = Vector3.Cross(axis, Vector3.up).normalized;
        }
        else
        {
            tangentA = Vector3.Cross(axis, Vector3.right).normalized;
        }

        tangentB = Vector3.Cross(axis, tangentA).normalized;

        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / RingSegments;
            Vector3 point =
                tangentA * (Mathf.Cos(angle) * circleRadius) +
                tangentB * (Mathf.Sin(angle) * circleRadius);
            renderer.SetPosition(i, point);
        }
    }
}
