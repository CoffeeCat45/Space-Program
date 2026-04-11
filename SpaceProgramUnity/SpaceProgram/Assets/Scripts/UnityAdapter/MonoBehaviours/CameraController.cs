using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float followSmoothness = 8.0f;
    public float zoomSpeed = 0.4f;
    public float minZoom = 2.0f;
    public float maxZoom = 40.0f;

    Transform target;
    Camera controlledCamera;

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
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void LateUpdate()
    {
        UpdateZoom();
        FollowTarget();
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
            transform.position.z
        );

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSmoothness * Time.deltaTime
        );
    }
}
