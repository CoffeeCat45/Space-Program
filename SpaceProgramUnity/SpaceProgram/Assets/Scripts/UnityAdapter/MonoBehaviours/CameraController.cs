using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(200)]
public class CameraController : MonoBehaviour
{
    public float followSmoothness = 8.0f;
    public float doubleClickMaxDelay = 0.3f;
    public float orbitSensitivity = 0.2f;
    /// <summary>
    /// Flight orbit pitch. Stay off ±90°: Euler yaw/pitch would flip at the pole.
    /// ±80° blocked top/bottom ship views; ±89.5° matches map.
    /// </summary>
    public float minOrbitPitch = -89.5f;
    public float maxOrbitPitch = 89.5f;
    public float mapMinOrbitPitch = -89.5f;
    public float mapMaxOrbitPitch = 89.5f;
    public float orbitDistanceScale = 1.0f;
    public float fieldOfView = 55.0f;
    public float shipBoundsDistanceMultiplier = 3.5f;

    public Func<Transform, Transform> ResolveFocusRoot;
    public Func<Vector2, Transform> ResolveMapClick;
    public Func<Transform, bool> AcceptFocusTarget;
    public event Action<Transform> FocusTargetChanged;

    Transform target;
    Transform defaultTarget;
    Camera unityCamera;
    double lastLeftClickTime = -10.0;
    double lastRightClickTime = -10.0;
    float orbitYaw;
    float orbitPitch;
    bool useExplicitPivot;
    Vector3 explicitPivotPosition;
    float shipRollDegrees;
    float autoBoundsRadius;
    bool useChaseCamera;
    bool snapChaseCameraNextFrame;
    Vector3 chasePivot;
    Vector3 chaseForward;
    Vector3 chaseUp;
    float chaseDistance;
    Quaternion orbitFrameRotation = Quaternion.identity;
    bool hasOrbitFrameRotation;

    bool isMapModeActive;
    bool snapOrbitNextLateUpdate;
    float positionScaleForZoom = WorldVisualBridge.FlightPositionScale;
    double currentOrbitDistanceSimMeters = 45.0;
    double targetOrbitDistanceSimMeters = 45.0;
    double currentOrbitDistanceUnity = 45.0;
    double targetOrbitDistanceUnity = 45.0;

    bool hasStoredFlightOrbit;
    float storedFlightYaw;
    float storedFlightPitch;
    float storedFlightRoll;
    double storedFlightDistanceUnity = 45.0;
    Quaternion storedFlightOrbitFrame = Quaternion.identity;
    bool storedFlightHasOrbitFrame;
    bool hasStoredMapOrbit;
    float storedMapYaw;
    float storedMapPitch;
    double storedMapDistanceSimMeters = 100_000.0;

    double? normalOrbitUnityMinOverride;
    double? normalOrbitUnityMaxOverride;

    const double NormalZoomFactorPerNotch = 1.24;
    const double MapZoomFactorPerNotch = 1.52;
    const double ScrollZoomSpeedMultiplier = 7.5;
    const float minOrbitDistance = 0.05f;
    const float MaxCameraDeltaTime = 0.05f;
    const float ZoomFollowSpeed = 10.0f;
    const float NormalNearClip = WorldVisualBridge.FlightCameraNearClip;
    const float NormalFarClip = WorldVisualBridge.FlightCameraFarClip;
    const float MapNearClip = WorldVisualBridge.MapCameraNearClip;
    const float MapFarClip = WorldVisualBridge.MapCameraFarClip;
    double mapOrbitDistanceFloorSimMeters = WorldVisualBridge.MapOrbitSimMetersMin;

    void Awake()
    {
        unityCamera = GetComponent<Camera>();

        if (unityCamera != null)
        {
            unityCamera.orthographic = false;
            unityCamera.fieldOfView = fieldOfView;
            ApplyCameraClipPlanes();
        }

        transform.rotation = Quaternion.identity;
        orbitYaw = transform.eulerAngles.y;
        orbitPitch = transform.eulerAngles.x;
    }

    public void SetTarget(Transform newTarget)
    {
        SetTarget(newTarget, updateDefault: false);
    }

    public void SetTarget(Transform newTarget, bool updateDefault)
    {
        target = newTarget;

        if (defaultTarget == null || updateDefault)
        {
            defaultTarget = newTarget;
        }
    }

    /// <summary>
    /// Extra orbit frame on top of zenith-up. Horizon rig uses identity.
    /// Vessel rig uses the ship's visual rotation so yaw/pitch stay on the nose.
    /// </summary>
    public void SetOrbitFrameRotation(Quaternion frameRotation)
    {
        if (hasOrbitFrameRotation)
        {
            frameRotation = WorldVisualBridge.PickNearestQuaternion(
                orbitFrameRotation,
                frameRotation
            );
        }

        orbitFrameRotation = frameRotation;
        hasOrbitFrameRotation = true;
    }

    public void ClearOrbitFrameRotation()
    {
        orbitFrameRotation = Quaternion.identity;
        hasOrbitFrameRotation = false;
    }

    /// <summary>
    /// Switch chase frame without jumping the current Game view.
    /// </summary>
    public void SetOrbitFrameKeepingWorldView(Quaternion newFrame)
    {
        Vector3 worldOffset = GetOrbitOffsetDirection();
        SetOrbitFrameRotation(newFrame);
        Quaternion frame = ResolveOrbitFrameRotation();
        Vector3 localOffset = Quaternion.Inverse(frame) * worldOffset;
        WorldVisualBridge.FlightOrbitOffsetToYawPitch(
            localOffset,
            out orbitYaw,
            out orbitPitch
        );
        orbitPitch = Mathf.Clamp(orbitPitch, GetActiveMinOrbitPitch(), GetActiveMaxOrbitPitch());
        RecaptureRollFromCurrentPose();
        SnapOrbitNextUpdate();
    }

    void RecaptureRollFromCurrentPose()
    {
        Vector3 look = -GetOrbitOffsetDirection();
        if (look.sqrMagnitude <= 1e-8f)
        {
            shipRollDegrees = 0.0f;
            return;
        }

        Vector3 frameUp = ResolveOrbitUpReference();
        shipRollDegrees = WorldVisualBridge.SignedOrbitRollDegrees(
            look,
            frameUp,
            transform.rotation * Vector3.up
        );
    }

    public void UpdateShipFollow(
        Vector3 pivotWorldPosition,
        float rollDegrees,
        double envelopeRadiusSim,
        float positionScale
    )
    {
        useChaseCamera = false;
        useExplicitPivot = true;
        explicitPivotPosition = pivotWorldPosition;
        // Roll is owned by SoI preserve / Recapture / double-RMB reset.
        // Callers pass GetOrbitRollDegrees() so a 0 here cannot wipe it.
        shipRollDegrees = rollDegrees;
        positionScaleForZoom = Mathf.Max(1e-8f, positionScale);
        autoBoundsRadius = Mathf.Max(
            0.5f,
            (float)(envelopeRadiusSim * shipBoundsDistanceMultiplier)
        );
        ApplyCameraClipPlanes();
    }

    public void UpdateChaseFollow(
        Vector3 pivotWorldPosition,
        Vector3 forwardWorld,
        Vector3 upWorld,
        float chaseDistanceUnity,
        float positionScale,
        bool snapImmediate
    )
    {
        useChaseCamera = true;
        useExplicitPivot = false;
        chasePivot = pivotWorldPosition;
        chaseForward = forwardWorld.sqrMagnitude > 1e-8f ?
            forwardWorld.normalized :
            Vector3.up;
        chaseUp = upWorld.sqrMagnitude > 1e-8f ?
            upWorld.normalized :
            Vector3.forward;
        chaseDistance = Mathf.Max(4.0f, chaseDistanceUnity);
        positionScaleForZoom = Mathf.Max(1e-8f, positionScale);
        snapChaseCameraNextFrame = snapImmediate;
        ApplyCameraClipPlanes();
    }

    public void SnapInitialOrbitToChase(
        Vector3 pivotWorld,
        Vector3 offsetFromPivot,
        Vector3 upReference,
        Quaternion frameRotation
    )
    {
        useChaseCamera = false;
        useExplicitPivot = true;
        explicitPivotPosition = pivotWorld;
        shipRollDegrees = 0.0f;
        orbitFrameRotation = frameRotation;
        hasOrbitFrameRotation = true;

        Vector3 offset = offsetFromPivot;

        if (offset.sqrMagnitude <= 1e-8f)
        {
            offset = frameRotation * new Vector3(52.0f, 14.0f, -6.0f);
        }

        Vector3 localOffset = Quaternion.Inverse(frameRotation) * offset.normalized;
        WorldVisualBridge.FlightOrbitOffsetToYawPitch(
            localOffset,
            out orbitYaw,
            out orbitPitch
        );
        orbitPitch = Mathf.Clamp(orbitPitch, GetActiveMinOrbitPitch(), GetActiveMaxOrbitPitch());

        float desiredDistance = offset.magnitude;
        float zoomDistance = Mathf.Max(0.01f, desiredDistance - autoBoundsRadius);

        if (isMapModeActive)
        {
            double simMeters = positionScaleForZoom > 0.0f ?
                zoomDistance / positionScaleForZoom :
                zoomDistance;
            currentOrbitDistanceSimMeters = ClampMapOrbitSimMeters(simMeters);
            targetOrbitDistanceSimMeters = currentOrbitDistanceSimMeters;
        }
        else
        {
            currentOrbitDistanceUnity = ClampNormalOrbitUnity(zoomDistance);
            targetOrbitDistanceUnity = currentOrbitDistanceUnity;
        }

        Quaternion orbitRotation = frameRotation * Quaternion.Euler(
            orbitPitch,
            orbitYaw,
            0.0f
        );
        Vector3 orbitDirection = orbitRotation * Vector3.back;
        transform.position = pivotWorld + orbitDirection * GetOrbitDistance();
        Vector3 lookDirection = pivotWorld - transform.position;

        if (lookDirection.sqrMagnitude > 1e-8f)
        {
            Vector3 up = frameRotation * Vector3.up;

            if (up.sqrMagnitude <= 1e-8f)
            {
                up = upReference.sqrMagnitude > 1e-8f ?
                    upReference.normalized :
                    Vector3.up;
            }

            transform.rotation = Quaternion.LookRotation(lookDirection.normalized, up.normalized);
        }
    }

    public void SetMapModeActive(bool active, float positionScale)
    {
        float scale = Mathf.Max(1e-8f, positionScale);
        if (
            isMapModeActive == active &&
            Mathf.Approximately(positionScaleForZoom, scale)
        )
        {
            return;
        }

        if (active && !isMapModeActive)
        {
            StoreFlightOrbit();
        }
        else if (!active && isMapModeActive)
        {
            StoreMapOrbit();
        }

        isMapModeActive = active;
        positionScaleForZoom = scale;

        if (isMapModeActive)
        {
            useChaseCamera = false;
            ClearOrbitFrameRotation();
            mapOrbitDistanceFloorSimMeters = WorldVisualBridge.MapOrbitSimMetersMin;
            RestoreMapOrbitIfStored();
        }
        else
        {
            RestoreFlightOrbitIfStored();
        }

        ApplyCameraClipPlanes();
    }

    public bool HasStoredMapOrbit => hasStoredMapOrbit;

    void StoreFlightOrbit()
    {
        storedFlightYaw = orbitYaw;
        storedFlightPitch = orbitPitch;
        storedFlightRoll = shipRollDegrees;
        storedFlightDistanceUnity = currentOrbitDistanceUnity;
        storedFlightOrbitFrame = orbitFrameRotation;
        storedFlightHasOrbitFrame = hasOrbitFrameRotation;
        hasStoredFlightOrbit = true;
    }

    void StoreMapOrbit()
    {
        storedMapYaw = orbitYaw;
        storedMapPitch = orbitPitch;
        storedMapDistanceSimMeters = currentOrbitDistanceSimMeters;
        hasStoredMapOrbit = true;
    }

    void RestoreFlightOrbitIfStored()
    {
        if (!hasStoredFlightOrbit)
        {
            return;
        }

        SetOrbitAngles(storedFlightYaw, storedFlightPitch);
        shipRollDegrees = storedFlightRoll;
        if (storedFlightHasOrbitFrame)
        {
            SetOrbitFrameRotation(storedFlightOrbitFrame);
        }
        else
        {
            ClearOrbitFrameRotation();
        }
        targetOrbitDistanceUnity = ClampNormalOrbitUnity(storedFlightDistanceUnity);
        currentOrbitDistanceUnity = targetOrbitDistanceUnity;
    }

    void RestoreMapOrbitIfStored()
    {
        if (!hasStoredMapOrbit)
        {
            return;
        }

        SetOrbitAngles(storedMapYaw, storedMapPitch);
        targetOrbitDistanceSimMeters = ClampMapOrbitSimMeters(storedMapDistanceSimMeters);
        currentOrbitDistanceSimMeters = targetOrbitDistanceSimMeters;
    }

    /// <summary>
    /// Next LateUpdate places the camera at the flight orbit instantly (no lerp through Earth).
    /// </summary>
    public void SnapOrbitNextUpdate()
    {
        snapOrbitNextLateUpdate = true;
        currentOrbitDistanceUnity = targetOrbitDistanceUnity;
        currentOrbitDistanceSimMeters = targetOrbitDistanceSimMeters;
    }

    /// <summary>
    /// Keep the chase offset glued to the ship when the flight universe jumps
    /// (Earth↔Moon SoI). Live yaw/pitch stay identity-frame — not Mesh-to-flight
    /// and not Q/E. Map live orbit is left alone; stored flight orbit still follows.
    /// </summary>
    public void PreserveOrbitThroughWorldReorientation(
        Quaternion worldDelta,
        bool applyToLiveOrbit
    )
    {
        worldDelta = WorldVisualBridge.PickNearestQuaternion(
            Quaternion.identity,
            worldDelta
        );

        if (hasStoredFlightOrbit)
        {
            ApplyWorldDeltaToYawPitch(
                worldDelta,
                Quaternion.identity,
                ref storedFlightYaw,
                ref storedFlightPitch,
                ref storedFlightRoll,
                minOrbitPitch,
                maxOrbitPitch
            );
        }

        if (!applyToLiveOrbit || isMapModeActive)
        {
            return;
        }

        ApplyWorldDeltaToYawPitch(
            worldDelta,
            ResolveOrbitFrameRotation(),
            ref orbitYaw,
            ref orbitPitch,
            ref shipRollDegrees,
            GetActiveMinOrbitPitch(),
            GetActiveMaxOrbitPitch()
        );
        SnapOrbitNextUpdate();
    }

    public Vector3 GetOrbitOffsetDirection()
    {
        return ResolveOrbitRotation() * Vector3.back;
    }

    public Vector3 GetFollowPivotWorldPosition()
    {
        if (useExplicitPivot)
        {
            return explicitPivotPosition;
        }

        if (target != null)
        {
            return target.position;
        }

        return transform.position;
    }

    public void GetOrbitAngles(out float yaw, out float pitch)
    {
        yaw = orbitYaw;
        pitch = orbitPitch;
    }

    public float GetOrbitRollDegrees()
    {
        return shipRollDegrees;
    }

    static void ApplyWorldDeltaToYawPitch(
        Quaternion worldDelta,
        Quaternion orbitFrame,
        ref float yaw,
        ref float pitch,
        ref float rollDegrees,
        float minPitch,
        float maxPitch
    )
    {
        Quaternion localOrbit = Quaternion.Euler(pitch, yaw, 0.0f);
        Vector3 oldOffset = (orbitFrame * localOrbit) * Vector3.back;
        Vector3 oldLook = -oldOffset;
        Vector3 oldUp = Quaternion.LookRotation(
            oldLook.sqrMagnitude > 1e-8f ? oldLook.normalized : Vector3.forward,
            orbitFrame * Vector3.up
        ) * Quaternion.Euler(0.0f, 0.0f, rollDegrees) * Vector3.up;
        Vector3 newOffset = worldDelta * oldOffset;
        Vector3 newLook = -newOffset;
        Vector3 newUp = worldDelta * oldUp;
        Vector3 localOffset = Quaternion.Inverse(orbitFrame) * newOffset;
        WorldVisualBridge.FlightOrbitOffsetToYawPitch(
            localOffset,
            out yaw,
            out pitch
        );
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        rollDegrees = WorldVisualBridge.SignedOrbitRollDegrees(
            newLook,
            orbitFrame * Vector3.up,
            newUp
        );
    }

    public bool IsMapModeActive => isMapModeActive;

    public void FocusOnDefaultTarget()
    {
        if (defaultTarget != null)
        {
            target = defaultTarget;
        }
    }

    public string GetFocusTargetName()
    {
        if (target == null)
        {
            return string.Empty;
        }

        return target.name;
    }

    public bool IsChaseCameraActive => useChaseCamera;

    public Quaternion GetResolvedOrbitFrameRotation()
    {
        return ResolveOrbitFrameRotation();
    }

    public float GetOrbitDistanceUnity()
    {
        return GetOrbitDistance();
    }

    public bool IsFocusedOnDefaultTarget()
    {
        return target == defaultTarget;
    }

    public void ResetScenarioDefaults()
    {
        fieldOfView = 55.0f;
        orbitDistanceScale = 1.0f;
        useExplicitPivot = false;
        autoBoundsRadius = 0.0f;
        shipRollDegrees = 0.0f;
        useChaseCamera = false;
        snapChaseCameraNextFrame = false;
        isMapModeActive = false;
        ClearOrbitFrameRotation();
        hasStoredFlightOrbit = false;
        storedFlightHasOrbitFrame = false;
        storedFlightOrbitFrame = Quaternion.identity;
        hasStoredMapOrbit = false;
        currentOrbitDistanceUnity = 45.0;
        targetOrbitDistanceUnity = 45.0;
        currentOrbitDistanceSimMeters = 100_000.0;
        targetOrbitDistanceSimMeters = 100_000.0;
        mapOrbitDistanceFloorSimMeters = WorldVisualBridge.MapOrbitSimMetersMin;
        SetOrbitAngles(0.0f, 0.0f);

        if (unityCamera != null)
        {
            unityCamera.fieldOfView = fieldOfView;
            ApplyCameraClipPlanes();
        }
    }

    public void SetOrbitAngles(float yaw, float pitch)
    {
        orbitYaw = yaw;
        orbitPitch = Mathf.Clamp(pitch, GetActiveMinOrbitPitch(), GetActiveMaxOrbitPitch());
    }

    /// <summary>
    /// Map mode: orbit the planet center from the exterior side facing the launch site.
    /// Mesh Frame yaw uses atan2(-x, -z) so Euler(pitch, yaw, 0) matches sim→mesh outward
    /// (see work_terrain.md §3 — flight orbit uses atan2(x, -z) and mirrors longitude).
    /// </summary>
    public void SnapMapModeOrbitToPlanetExterior(
        Vector3 planetCenterWorld,
        Vector3 outwardFromPlanetToSurface
    )
    {
        StabilizeMapOrbitOnTarget(
            planetCenterWorld,
            outwardFromPlanetToSurface,
            WorldVisualBridge.MapOrbitSimMetersMin
        );
    }

    /// <summary>
    /// Map mode: lock the orbit pivot on a clicked body and look at it from outside.
    /// Used for Sun / Moon / Planet / vessel — not only Earth.
    /// </summary>
    public void StabilizeMapOrbitOnTarget(
        Vector3 pivotWorld,
        Vector3 outwardFromTarget,
        double minOrbitSimMeters
    )
    {
        if (!isMapModeActive)
        {
            return;
        }

        useChaseCamera = false;
        useExplicitPivot = true;
        explicitPivotPosition = pivotWorld;
        ClearOrbitFrameRotation();

        if (outwardFromTarget.sqrMagnitude <= 1e-8f)
        {
            outwardFromTarget = transform.position - pivotWorld;
        }

        if (outwardFromTarget.sqrMagnitude <= 1e-8f)
        {
            outwardFromTarget = Vector3.back;
        }

        outwardFromTarget.Normalize();
        MeshFrameOutwardToOrbitAngles(
            outwardFromTarget,
            out orbitYaw,
            out orbitPitch
        );
        orbitPitch = Mathf.Clamp(orbitPitch, GetActiveMinOrbitPitch(), GetActiveMaxOrbitPitch());

        mapOrbitDistanceFloorSimMeters = Math.Max(1_000.0, minOrbitSimMeters);
        if (targetOrbitDistanceSimMeters < mapOrbitDistanceFloorSimMeters)
        {
            targetOrbitDistanceSimMeters = mapOrbitDistanceFloorSimMeters * 1.15;
        }

        targetOrbitDistanceSimMeters = ClampMapOrbitSimMeters(targetOrbitDistanceSimMeters);
        currentOrbitDistanceSimMeters = targetOrbitDistanceSimMeters;
        snapOrbitNextLateUpdate = true;

        float orbitDistance = GetOrbitDistance();
        Vector3 desiredPosition = pivotWorld + outwardFromTarget * orbitDistance;
        transform.position = desiredPosition;

        Vector3 lookDirection = pivotWorld - desiredPosition;

        if (lookDirection.sqrMagnitude > 1e-8f)
        {
            transform.rotation = Quaternion.LookRotation(
                lookDirection.normalized,
                Vector3.up
            );
        }
    }

    public void SetMapOrbitPivotWorld(Vector3 pivotWorld)
    {
        if (!isMapModeActive)
        {
            return;
        }

        useExplicitPivot = true;
        explicitPivotPosition = pivotWorld;
    }

    static void MeshFrameOutwardToOrbitAngles(
        Vector3 outwardUnit,
        out float yawDegrees,
        out float pitchDegrees
    )
    {
        float horizontalDistance = Mathf.Sqrt(
            (outwardUnit.x * outwardUnit.x) +
            (outwardUnit.z * outwardUnit.z)
        );
        yawDegrees = Mathf.Atan2(-outwardUnit.x, -outwardUnit.z) * Mathf.Rad2Deg;
        pitchDegrees = Mathf.Atan2(outwardUnit.y, horizontalDistance) * Mathf.Rad2Deg;
    }

    float GetActiveMinOrbitPitch()
    {
        return isMapModeActive ? mapMinOrbitPitch : minOrbitPitch;
    }

    float GetActiveMaxOrbitPitch()
    {
        return isMapModeActive ? mapMaxOrbitPitch : maxOrbitPitch;
    }

    public void SetNormalOrbitDistanceUnity(double distanceUnity)
    {
        targetOrbitDistanceUnity = ClampNormalOrbitUnity(distanceUnity);
        currentOrbitDistanceUnity = targetOrbitDistanceUnity;
    }

    public void SetNormalOrbitUnityLimits(double minUnity, double maxUnity)
    {
        normalOrbitUnityMinOverride = minUnity;
        normalOrbitUnityMaxOverride = maxUnity;
        targetOrbitDistanceUnity = ClampNormalOrbitUnity(targetOrbitDistanceUnity);
        currentOrbitDistanceUnity = ClampNormalOrbitUnity(currentOrbitDistanceUnity);
    }

    public double GetNormalOrbitUnityMax()
    {
        return normalOrbitUnityMaxOverride ?? WorldVisualBridge.NormalCameraOrbitUnityMax;
    }

    public void SetMapOrbitDistanceSimMeters(double distanceSimMeters)
    {
        targetOrbitDistanceSimMeters = ClampMapOrbitSimMeters(distanceSimMeters);
        currentOrbitDistanceSimMeters = targetOrbitDistanceSimMeters;
    }

    public void SetMapOrbitDistanceFloorSimMeters(double minSimMeters)
    {
        mapOrbitDistanceFloorSimMeters = Math.Max(1_000.0, minSimMeters);
        targetOrbitDistanceSimMeters = ClampMapOrbitSimMeters(targetOrbitDistanceSimMeters);
        currentOrbitDistanceSimMeters = ClampMapOrbitSimMeters(currentOrbitDistanceSimMeters);
    }

    /// <summary>
    /// After focusing the planet globe, enforce exterior orbit distance (sim meters).
    /// </summary>
    public void EnsureMapOrbitExteriorDistance()
    {
        if (!isMapModeActive)
        {
            return;
        }

        double safeDistance = WorldVisualBridge.MapOrbitSimMetersMin;
        targetOrbitDistanceSimMeters = ClampMapOrbitSimMeters(
            Math.Max(targetOrbitDistanceSimMeters, safeDistance)
        );
        currentOrbitDistanceSimMeters = ClampMapOrbitSimMeters(
            Math.Max(currentOrbitDistanceSimMeters, safeDistance)
        );
        ApplyCameraClipPlanes();
    }

    void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.0001f)
        {
            return;
        }

        float scrollNotches = scroll / 120f * (float)ScrollZoomSpeedMultiplier;

        if (isMapModeActive)
        {
            targetOrbitDistanceSimMeters *= Math.Pow(
                MapZoomFactorPerNotch,
                -scrollNotches
            );
            targetOrbitDistanceSimMeters = ClampMapOrbitSimMeters(
                targetOrbitDistanceSimMeters
            );
        }
        else
        {
            targetOrbitDistanceUnity *= Math.Pow(
                NormalZoomFactorPerNotch,
                -scrollNotches
            );
            targetOrbitDistanceUnity = ClampNormalOrbitUnity(targetOrbitDistanceUnity);
        }
    }

    void LateUpdate()
    {
        UpdateDoubleClickFocus();
        UpdateDoubleRightClickReset();
        UpdateOrbitRotation();

        if (isMapModeActive)
        {
            targetOrbitDistanceSimMeters = ClampMapOrbitSimMeters(
                targetOrbitDistanceSimMeters
            );
            currentOrbitDistanceSimMeters = ClampMapOrbitSimMeters(
                currentOrbitDistanceSimMeters
            );
        }

        float followT = ExpFollow(ZoomFollowSpeed, GetCameraDeltaTime());
        currentOrbitDistanceSimMeters += (
            targetOrbitDistanceSimMeters - currentOrbitDistanceSimMeters
        ) * followT;
        currentOrbitDistanceUnity += (
            targetOrbitDistanceUnity - currentOrbitDistanceUnity
        ) * followT;

        FollowTarget();
        ApplyCameraClipPlanes();
    }

    static float GetCameraDeltaTime()
    {
        return Mathf.Min(Time.unscaledDeltaTime, MaxCameraDeltaTime);
    }

    static float ExpFollow(float speed, float deltaTime)
    {
        return 1.0f - Mathf.Exp(-speed * Mathf.Max(deltaTime, 0.0f));
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

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        orbitYaw += mouseDelta.x * orbitSensitivity;
        orbitPitch -= mouseDelta.y * orbitSensitivity;
        orbitPitch = Mathf.Clamp(orbitPitch, GetActiveMinOrbitPitch(), GetActiveMaxOrbitPitch());
    }

    void UpdateDoubleClickFocus()
    {
        if (
            unityCamera == null ||
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
        if (clickedTarget == null)
        {
            return;
        }

        Transform newTarget = clickedTarget;

        if (newTarget != null)
        {
            if (AcceptFocusTarget != null && !AcceptFocusTarget(newTarget))
            {
                return;
            }

            target = newTarget;
            FocusTargetChanged?.Invoke(target);
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
        shipRollDegrees = 0.0f;
    }

    Transform GetClickedFocusTarget()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (isMapModeActive && ResolveMapClick != null)
        {
            Transform screenPick = ResolveMapClick(mousePosition);

            if (screenPick != null)
            {
                return screenPick;
            }
        }

        Ray ray = unityCamera.ScreenPointToRay(mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray);

        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        System.Array.Sort(
            hits,
            (RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance)
        );

        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsPriorityMapPickHit(hits[i].collider))
            {
                continue;
            }

            Transform resolved = GetFocusRoot(hits[i].collider.transform);

            if (resolved != null)
            {
                return resolved;
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Transform resolved = GetFocusRoot(hits[i].collider.transform);

            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }

    static bool IsPriorityMapPickHit(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform current = collider.transform;

        while (current != null)
        {
            if (
                current.name == BodyView.MapModePickColliderName ||
                current.name == "MapModeMarker"
            )
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    Transform GetFocusRoot(Transform hitTransform)
    {
        if (ResolveFocusRoot != null)
        {
            Transform resolved = ResolveFocusRoot(hitTransform);
            if (resolved != null)
            {
                return resolved;
            }
        }

        Transform currentTransform = hitTransform;

        while (currentTransform.parent != null)
        {
            currentTransform = currentTransform.parent;
        }

        return currentTransform;
    }

    float GetOrbitDistance()
    {
        float unityDistance;

        if (isMapModeActive)
        {
            unityDistance = (float)(currentOrbitDistanceSimMeters * positionScaleForZoom);
        }
        else
        {
            unityDistance = (float)currentOrbitDistanceUnity;
            float boundsBlend = Mathf.InverseLerp(
                WorldVisualBridge.NormalCameraOrbitUnityMin,
                WorldVisualBridge.NormalCameraOrbitUnityMax,
                unityDistance
            );
            unityDistance += autoBoundsRadius * boundsBlend;
        }

        return Mathf.Max(minOrbitDistance, unityDistance) * orbitDistanceScale;
    }

    public float GetCurrentOrbitDistanceUnity()
    {
        return GetOrbitDistance();
    }

    double ClampNormalOrbitUnity(double distanceUnity)
    {
        return Math.Clamp(
            distanceUnity,
            normalOrbitUnityMinOverride ?? WorldVisualBridge.NormalCameraOrbitUnityMin,
            normalOrbitUnityMaxOverride ?? WorldVisualBridge.NormalCameraOrbitUnityMax
        );
    }

    double ClampMapOrbitSimMeters(double distanceSimMeters)
    {
        return Math.Max(distanceSimMeters, mapOrbitDistanceFloorSimMeters);
    }

    void ApplyCameraClipPlanes()
    {
        if (unityCamera == null)
        {
            return;
        }

        if (isMapModeActive)
        {
            float orbitDistance = GetOrbitDistance();
            unityCamera.nearClipPlane = Mathf.Clamp(orbitDistance * 0.0015f, MapNearClip, 400f);
            unityCamera.farClipPlane = Mathf.Max(MapFarClip, orbitDistance * 25f);
        }
        else
        {
            unityCamera.nearClipPlane = NormalNearClip;
            unityCamera.farClipPlane = NormalFarClip;
        }
    }

    void FollowTarget()
    {
        if (useChaseCamera)
        {
            FollowChaseTarget();
            return;
        }

        Vector3 targetPosition;

        if (useExplicitPivot)
        {
            targetPosition = explicitPivotPosition;
        }
        else if (target != null)
        {
            targetPosition = target.position;
        }
        else
        {
            return;
        }

        Quaternion orbitRotation = ResolveOrbitRotation();
        Vector3 desiredPosition =
            targetPosition +
            orbitRotation * new Vector3(0, 0, -GetOrbitDistance());

        bool orbitDragging =
            Mouse.current != null &&
            Mouse.current.rightButton != null &&
            Mouse.current.rightButton.isPressed;
        bool snapOrbit = isMapModeActive || orbitDragging || snapOrbitNextLateUpdate;
        float followT = ExpFollow(followSmoothness, GetCameraDeltaTime());

        if (snapOrbit)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                followT
            );

            if ((transform.position - desiredPosition).sqrMagnitude <= 1e-6f)
            {
                transform.position = desiredPosition;
            }
        }

        Vector3 lookDirection = targetPosition - desiredPosition;

        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 upReference = ResolveOrbitUpReference();
            Quaternion lookRotation = Quaternion.LookRotation(
                lookDirection.normalized,
                upReference
            );
            Quaternion rollRotation = isMapModeActive ?
                Quaternion.identity :
                Quaternion.Euler(
                    0.0f,
                    0.0f,
                    shipRollDegrees
                );
            Quaternion desiredRotation = lookRotation * rollRotation;

            if (snapOrbit)
            {
                transform.rotation = desiredRotation;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    followT
                );

                if (Quaternion.Angle(transform.rotation, desiredRotation) <= 0.01f)
                {
                    transform.rotation = desiredRotation;
                }
            }
        }

        if (unityCamera != null)
        {
            unityCamera.fieldOfView = fieldOfView;
        }

        snapOrbitNextLateUpdate = false;
    }

    void FollowChaseTarget()
    {
        Vector3 cameraPosition = chasePivot - chaseForward * chaseDistance;
        Quaternion targetRotation = Quaternion.LookRotation(chaseForward, chaseUp);

        if (snapChaseCameraNextFrame)
        {
            transform.position = cameraPosition;
            transform.rotation = targetRotation;
            snapChaseCameraNextFrame = false;
            return;
        }

        float followT = ExpFollow(followSmoothness, GetCameraDeltaTime());
        transform.position = Vector3.Lerp(
            transform.position,
            cameraPosition,
            followT
        );

        if ((transform.position - cameraPosition).sqrMagnitude <= 1e-6f)
        {
            transform.position = cameraPosition;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            followT
        );

        if (Quaternion.Angle(transform.rotation, targetRotation) <= 0.01f)
        {
            transform.rotation = targetRotation;
        }

        if (unityCamera != null)
        {
            unityCamera.fieldOfView = fieldOfView;
        }
    }

    Quaternion ResolveOrbitFrameRotation()
    {
        if (hasOrbitFrameRotation)
        {
            return orbitFrameRotation;
        }

        // Horizon / zenith +Y for pad and flying. Never fall back to vessel
        // rotation or inertial Mesh-to-flight (that rolls Game vs Scene).
        return Quaternion.identity;
    }

    Quaternion ResolveOrbitRotation()
    {
        Quaternion localOrbit = Quaternion.Euler(orbitPitch, orbitYaw, 0.0f);
        Quaternion frameRotation = ResolveOrbitFrameRotation();

        if (frameRotation == Quaternion.identity)
        {
            return localOrbit;
        }

        return frameRotation * localOrbit;
    }

    Vector3 ResolveOrbitUpReference()
    {
        Quaternion frameRotation = ResolveOrbitFrameRotation();
        Vector3 up = frameRotation * Vector3.up;
        return up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.up;
    }
}
