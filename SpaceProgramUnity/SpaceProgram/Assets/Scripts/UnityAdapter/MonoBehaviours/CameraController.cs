using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float followSmoothness = 8.0f;
    public float zoomSpeed = 0.4f;
    public float minZoom = 2.0f;
    public float maxZoom = 40.0f;
    public float doubleClickMaxDelay = 0.3f;
    public float orbitSensitivity = 0.2f;
    public float minOrbitPitch = -80.0f;
    public float maxOrbitPitch = 80.0f;
    public float orbitDistance = 40.0f;

    Transform target;
    Transform defaultTarget;
    Camera controlledCamera;
    double lastLeftClickTime = -10.0;
    double lastRightClickTime = -10.0;
    float orbitYaw;
    float orbitPitch;

    void Awake()
    {
        controlledCamera = GetComponent<Camera>();

        if (controlledCamera != null)
        {
            controlledCamera.orthographic = true;
            controlledCamera.orthographicSize = Mathf.Clamp(
                controlledCamera.orthographicSize,
                minZoom,
                maxZoom
            );
        }

        transform.rotation = Quaternion.identity;
        orbitYaw = transform.eulerAngles.y;
        orbitPitch = transform.eulerAngles.x;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (defaultTarget == null)
        {
            defaultTarget = newTarget;
        }
    }

    void LateUpdate()
    {
        UpdateDoubleClickFocus();
        UpdateDoubleRightClickReset();
        UpdateOrbitRotation();
        UpdateZoom();
        FollowTarget();
    }

    void UpdateOrbitRotation()
    {
        if (
            Mouse.current == null ||
            Mouse.current.rightButton == null ||
            !Mouse.current.rightButton.isPressed
        )
        {
            return;
        }

        UnityEngine.Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        orbitYaw += mouseDelta.x * orbitSensitivity;
        orbitPitch -= mouseDelta.y * orbitSensitivity;
        orbitPitch = Mathf.Clamp(orbitPitch, minOrbitPitch, maxOrbitPitch);
    }

    void UpdateDoubleClickFocus()
    {
        if (
            controlledCamera == null ||
            Mouse.current == null ||
            Mouse.current.leftButton == null ||
            !Mouse.current.leftButton.wasPressedThisFrame
        )
        {
            return;
        }

        double clickTime = Time.unscaledTime;
        bool isDoubleClick = clickTime - lastLeftClickTime <= doubleClickMaxDelay;
        lastLeftClickTime = clickTime;

        if (!isDoubleClick)
        {
            return;
        }

        Transform clickedTarget = GetClickedFocusTarget();

        if (clickedTarget != null)
        {
            target = clickedTarget;
        }
        else if (defaultTarget != null)
        {
            target = defaultTarget;
        }
    }

    void UpdateDoubleRightClickReset()
    {
        if (
            Mouse.current == null ||
            Mouse.current.rightButton == null ||
            !Mouse.current.rightButton.wasPressedThisFrame
        )
        {
            return;
        }

        double clickTime = Time.unscaledTime;
        bool isDoubleClick = clickTime - lastRightClickTime <= doubleClickMaxDelay;
        lastRightClickTime = clickTime;

        if (!isDoubleClick)
        {
            return;
        }

        orbitYaw = 0.0f;
        orbitPitch = 0.0f;
    }

    Transform GetClickedFocusTarget()
    {
        Ray ray = controlledCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return null;
        }

        return GetFocusRoot(hit.collider.transform);
    }

    Transform GetFocusRoot(Transform hitTransform)
    {
        Transform currentTransform = hitTransform;

        while (currentTransform.parent != null)
        {
            currentTransform = currentTransform.parent;
        }

        return currentTransform;
    }

    void UpdateZoom()
    {
        if (controlledCamera == null || Mouse.current == null)
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (scroll == 0)
        {
            return;
        }

        controlledCamera.orthographicSize = Mathf.Clamp(
            controlledCamera.orthographicSize - scroll * zoomSpeed,
            minZoom,
            maxZoom
        );
    }

    void FollowTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = new Vector3(
            target.position.x,
            target.position.y,
            target.position.z
        );
        Quaternion orbitRotation = Quaternion.Euler(
            orbitPitch,
            orbitYaw,
            0.0f
        );
        Vector3 cameraPosition =
            targetPosition + orbitRotation * new Vector3(0, 0, -orbitDistance);

        transform.position = Vector3.Lerp(
            transform.position,
            cameraPosition,
            followSmoothness * Time.deltaTime
        );
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            orbitRotation,
            followSmoothness * Time.deltaTime
        );
    }
}
