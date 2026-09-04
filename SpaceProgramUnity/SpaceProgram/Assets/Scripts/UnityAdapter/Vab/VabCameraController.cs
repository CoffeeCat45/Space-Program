using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// KSP-like VAB spherical orbit camera around a dynamic focus point (roadmap 1.1).
/// RMB = orbit with inertia, scroll = exponential zoom, Shift+RMB = vertical focus pan.
/// </summary>
[DisallowMultipleComponent]
public class VabCameraController : MonoBehaviour
{
    [SerializeField]
    Vector3 fixedPosition = new Vector3(6f, 7f, -9f);

    [SerializeField]
    Vector3 fixedLookAt = new Vector3(0f, 2f, 0f);

    [SerializeField]
    float orbitSensitivity = 0.25f;

    [SerializeField]
    float minOrbitPitch = -85f;

    [SerializeField]
    float maxOrbitPitch = 85f;

    [SerializeField]
    float orbitDistance = 12f;

    [SerializeField]
    float minOrbitDistance = 2f;

    [SerializeField]
    float maxOrbitDistance = 80f;

    [SerializeField]
    float zoomSpeed = 3.35f;

    [SerializeField]
    float focusPanSpeed = 0.0065f;

    [SerializeField]
    float focusPanSpeedScale = 0.55f;

    [SerializeField]
    float focusLerpSpeed = 12f;

    [SerializeField]
    float orbitInertiaDecay = 4.5f;

    [SerializeField]
    float nearClipClose = 0.01f;

    [SerializeField]
    float nearClipFar = 0.3f;

    bool followTrackedPivot = true;

    Vector3 focusPoint;
    Vector3 focusPointTarget;
    Transform trackedPivot;
    Camera unityCamera;
    CameraController flightCameraController;
    Func<bool> inputBlockedCheck;
    Func<Vector3, Vector3> focusPanClamp;
    bool orbitUnlocked;
    bool focusInitialized;
    float orbitYaw = 35f;
    float orbitPitch = 22f;
    float orbitYawVelocity;
    float orbitPitchVelocity;

    public bool OrbitUnlocked => orbitUnlocked;
    public Vector3 FocusPoint => focusPoint;
    public float OrbitDistance => orbitDistance;

    public void SetInputBlockedCheck(Func<bool> check)
    {
        inputBlockedCheck = check;
    }

    public void SetFocusPanClamp(Func<Vector3, Vector3> clamp)
    {
        focusPanClamp = clamp;
    }

    void ApplyFocusPanClamp()
    {
        if (focusPanClamp == null)
        {
            return;
        }

        focusPointTarget = focusPanClamp(focusPointTarget);
        focusPoint = focusPanClamp(focusPoint);
    }

    void Awake()
    {
        unityCamera = GetComponent<Camera>();
        flightCameraController = GetComponent<CameraController>();

        if (flightCameraController != null)
        {
            flightCameraController.enabled = false;
        }

        if (!Application.isPlaying)
        {
            return;
        }

        CaptureSceneOverviewPose();
        focusPoint = fixedLookAt;
        focusPointTarget = focusPoint;
        focusInitialized = true;
        ApplyFixedPose();
        UpdateNearClipPlane();
    }

    void OnEnable()
    {
        if (flightCameraController != null)
        {
            flightCameraController.enabled = false;
        }
    }

    void OnDisable()
    {
        if (flightCameraController != null)
        {
            flightCameraController.enabled = true;
        }
    }

    void LateUpdate()
    {
        if (trackedPivot != null && followTrackedPivot)
        {
            focusPointTarget = trackedPivot.position;
            ApplyFocusPanClamp();
            focusPoint = Vector3.Lerp(
                focusPoint,
                focusPointTarget,
                Time.deltaTime * focusLerpSpeed
            );
        }
        else
        {
            focusPoint = focusPointTarget;
        }

        if (orbitUnlocked)
        {
            UpdateOrbitInput();
            ApplyOrbitInertia();
            ApplyOrbitPose();
            UpdateNearClipPlane();
            return;
        }

        ApplyFixedPose();
    }

    public void EnableOrbitAtFocus(Vector3 worldFocus, bool immediate = true)
    {
        followTrackedPivot = false;
        trackedPivot = null;
        orbitUnlocked = true;
        SetFocusPoint(worldFocus, immediate);
        SyncOrbitAnglesFromCameraPose();
    }

    public void UnlockOrbitAround(Transform pivot)
    {
        if (pivot == null)
        {
            return;
        }

        trackedPivot = pivot;
        followTrackedPivot = true;
        orbitUnlocked = true;
        focusPoint = pivot.position;
        focusPointTarget = focusPoint;
        focusInitialized = true;
        SyncOrbitAnglesFromCameraPose();
    }

    public void SetOrbitTarget(Transform pivot, bool immediateFocus = false)
    {
        if (pivot == null)
        {
            return;
        }

        trackedPivot = pivot;
        followTrackedPivot = true;
        orbitUnlocked = true;
        focusPointTarget = pivot.position;
        ApplyFocusPanClamp();

        if (immediateFocus || !focusInitialized)
        {
            focusPoint = focusPointTarget;
            focusInitialized = true;
        }
    }

    public void LockFocusPoint(Vector3 worldPoint, bool immediate = false)
    {
        followTrackedPivot = false;
        trackedPivot = null;
        SetFocusPoint(worldPoint, immediate);
    }

    public void ClearOrbitTarget()
    {
        trackedPivot = null;
    }

    public void SetFocusPoint(Vector3 worldPoint, bool immediate = false)
    {
        focusPointTarget = worldPoint;

        if (immediate || !focusInitialized)
        {
            focusPoint = worldPoint;
            focusInitialized = true;
        }
    }

    public void FocusOnBounds(Bounds bounds, bool immediate = false)
    {
        FocusOnPart(bounds, immediate);
    }

    public void FocusOnPart(Bounds bounds, bool immediate = false, float minDistance = 6f, float maxDistance = 22f)
    {
        Vector3 center = bounds.center;
        followTrackedPivot = false;
        trackedPivot = null;
        SetFocusPoint(center, immediate);

        float size = Mathf.Max(bounds.extents.magnitude, 0.35f);
        orbitDistance = Mathf.Clamp(size * 2.4f, minDistance, maxDistance);
        UpdateNearClipPlane();
        SyncOrbitAnglesFromCameraPose();
    }

    public void SetOrbitDistance(float distance)
    {
        orbitDistance = Mathf.Clamp(distance, minOrbitDistance, maxOrbitDistance);
        UpdateNearClipPlane();
    }

    public void LockToOverview()
    {
        orbitUnlocked = false;
        trackedPivot = null;
        ApplyFixedPose();
    }

    void CaptureSceneOverviewPose()
    {
        fixedPosition = transform.position;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 500f))
        {
            fixedLookAt = hit.point;
            return;
        }

        fixedLookAt = transform.position + transform.forward * 8f;
    }

    void SyncOrbitAnglesFromCameraPose()
    {
        Vector3 offset = transform.position - focusPoint;
        orbitDistance = Mathf.Clamp(offset.magnitude, minOrbitDistance, maxOrbitDistance);

        if (offset.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
        Vector3 euler = lookRotation.eulerAngles;
        orbitYaw = euler.y;
        orbitPitch = euler.x;
    }

    void ApplyFixedPose()
    {
        transform.position = fixedPosition;
        transform.rotation = Quaternion.LookRotation(
            fixedLookAt - fixedPosition,
            Vector3.up
        );
    }

    bool IsInputBlocked()
    {
        return inputBlockedCheck != null && inputBlockedCheck.Invoke();
    }

    void UpdateOrbitInput()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        if (mouse == null || IsInputBlocked())
        {
            return;
        }

        bool shiftHeld = keyboard != null && keyboard.shiftKey.isPressed;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();

            if (shiftHeld)
            {
                float panScale = Mathf.Lerp(0.65f, 1.35f, Mathf.InverseLerp(
                    minOrbitDistance,
                    maxOrbitDistance,
                    orbitDistance
                ));
                Vector3 panDelta = Vector3.up * (
                    -delta.y * focusPanSpeed * orbitDistance * panScale * focusPanSpeedScale
                );
                focusPointTarget += panDelta;
                focusPoint += panDelta;
                ApplyFocusPanClamp();
            }
            else
            {
                float yawDelta = delta.x * orbitSensitivity;
                float pitchDelta = -delta.y * orbitSensitivity;
                orbitYaw += yawDelta;
                orbitPitch += pitchDelta;
                orbitPitch = Mathf.Clamp(orbitPitch, minOrbitPitch, maxOrbitPitch);
                orbitYawVelocity = Mathf.Clamp(
                    yawDelta / Mathf.Max(Time.deltaTime, 0.0001f),
                    -180f,
                    180f
                );
                orbitPitchVelocity = Mathf.Clamp(
                    pitchDelta / Mathf.Max(Time.deltaTime, 0.0001f),
                    -120f,
                    120f
                );
            }
        }

        float scroll = mouse.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            float scrollNotches = scroll / 120f;
            float distanceScale = Mathf.Lerp(1f, 2.2f, Mathf.InverseLerp(
                minOrbitDistance,
                maxOrbitDistance,
                orbitDistance
            ));
            float zoomFactor = Mathf.Pow(zoomSpeed, scrollNotches * distanceScale);
            orbitDistance /= zoomFactor;
            orbitDistance = Mathf.Clamp(orbitDistance, minOrbitDistance, maxOrbitDistance);
        }
    }

    void ApplyOrbitInertia()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool shiftHeld = keyboard != null && keyboard.shiftKey.isPressed;

        if (mouse != null && mouse.rightButton.isPressed && !shiftHeld)
        {
            return;
        }

        if (Mathf.Abs(orbitYawVelocity) < 0.01f && Mathf.Abs(orbitPitchVelocity) < 0.01f)
        {
            return;
        }

        orbitYaw += orbitYawVelocity * Time.deltaTime;
        orbitPitch += orbitPitchVelocity * Time.deltaTime;
        orbitPitch = Mathf.Clamp(orbitPitch, minOrbitPitch, maxOrbitPitch);

        float decay = Mathf.Exp(-orbitInertiaDecay * Time.deltaTime);
        orbitYawVelocity *= decay;
        orbitPitchVelocity *= decay;
    }

    void ApplyOrbitPose()
    {
        Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -orbitDistance);
        transform.position = focusPoint + offset;
        transform.rotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
    }

    void UpdateNearClipPlane()
    {
        if (unityCamera == null)
        {
            return;
        }

        float zoomT = Mathf.InverseLerp(minOrbitDistance, maxOrbitDistance, orbitDistance);
        unityCamera.nearClipPlane = Mathf.Lerp(nearClipClose, nearClipFar, zoomT);
    }
}
