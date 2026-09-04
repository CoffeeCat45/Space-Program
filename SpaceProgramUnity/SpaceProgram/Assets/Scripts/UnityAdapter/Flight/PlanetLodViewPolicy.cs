using UnityEngine;

/// <summary>
/// Visibility policy for flight PQS chunks.
/// </summary>
/// <remarks>
/// Split/merge residency is a disk around the ship, not the camera look cone:
///
/// * <b>Screen-space error</b> sets how large a chunk may be on screen. Near the ship that is
///   fine LOD; at the horizon the same pixel threshold stays coarse even if Max LOD is 9.
/// * The <b>horizon</b> is a geocentric angle, <c>acos(R / (R + altitude))</c>. On this 637 km
///   planet that is 3.2° at 1 km altitude and 10° at 10 km — everything past it is below the
///   curve and cannot be on screen at all.
/// * The <b>view cone</b> is camera-space. Unity already frustum-culls drawing. The cone must
///   not split, merge, or destroy meshes: looking away would collapse the disk and looking
///   back would rebuild it.
///
/// Mixing the view-cone slider (40°–110°) with the <i>geocentric</i> angle used to request a
/// spherical cap covering up to half the planet. Keep those angles separate.
/// </remarks>
public static class PlanetLodViewPolicy
{
    public const float PaddingHalfAngleDegrees = 15f;
    public const float PeripheralViewWeightMax = 2.25f;
    public const int HardPendingAsyncSplitHold = 64;
    /// <summary>
    /// Projected chunk size that triggers a split, in pixels, at Detail reach = 1.
    /// Runtime pixel error is this times the LOD-distance slider (clamped).
    /// </summary>
    public const float ScreenSpacePixelErrorBase = 48f;
    public const float ScreenSpacePixelErrorMin = 24f;
    public const float ScreenSpacePixelErrorMax = 256f;
    public const float DefaultTanHalfVerticalFov = 0.5773503f;
    public const float DefaultScreenHeightPixels = 1080f;

    /// <summary>Terrain just past the geometric horizon is still visible when it is elevated.</summary>
    public const float HorizonMarginDegrees = 1.5f;
    public const float HorizonMarginFraction = 0.35f;

    public enum ViewZone
    {
        Focus,
        Active,
        Padding,
        Dead,
    }

    /// <summary>Camera pose and derived limits, resolved once per LOD tick.</summary>
    public struct ViewerFrame
    {
        public bool HasCamera;
        public Vector3 CameraWorldPosition;
        public Vector3 CameraForward;
        /// <summary>Camera-space half angle used for debug classification, not residency.</summary>
        public float ConeHalfAngleDegrees;
        /// <summary>Geocentric half angle to the horizon (plus margin).</summary>
        public float HorizonHalfAngleDegrees;
        /// <summary><c>tan(verticalFov / 2)</c> for screen-space error.</summary>
        public float TanHalfVerticalFov;
        public float ScreenHeightPixels;
    }

    public static ViewerFrame BuildViewerFrame(
        Camera camera,
        float planetRadius,
        float viewerAltitudeMeters,
        float sliderHalfAngleDegrees
    )
    {
        ViewerFrame frame = new ViewerFrame
        {
            HasCamera = false,
            ConeHalfAngleDegrees = sliderHalfAngleDegrees,
            HorizonHalfAngleDegrees = GetHorizonHalfAngleDegrees(
                planetRadius,
                viewerAltitudeMeters
            ),
            TanHalfVerticalFov = DefaultTanHalfVerticalFov,
            ScreenHeightPixels = DefaultScreenHeightPixels,
        };

        if (camera == null)
        {
            return frame;
        }

        frame.HasCamera = true;
        frame.CameraWorldPosition = camera.transform.position;
        frame.CameraForward = camera.transform.forward;
        frame.ConeHalfAngleDegrees = Mathf.Max(
            GetFrustumCornerHalfAngleDegrees(camera),
            sliderHalfAngleDegrees
        );
        frame.TanHalfVerticalFov = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        frame.ScreenHeightPixels = Mathf.Max(1f, camera.pixelHeight);
        return frame;
    }

    /// <summary>
    /// World span whose projection is <paramref name="pixelError"/> pixels tall.
    /// Split when the chunk is larger than this.
    /// </summary>
    public static float ComputeScreenSpaceTargetSpanMeters(
        float slantRangeMeters,
        float tanHalfVerticalFov,
        float screenHeightPixels,
        float pixelError
    )
    {
        float tanHalf = tanHalfVerticalFov > 1e-4f ?
            tanHalfVerticalFov :
            DefaultTanHalfVerticalFov;
        float height = screenHeightPixels > 1f ?
            screenHeightPixels :
            DefaultScreenHeightPixels;
        float error = Mathf.Max(1f, pixelError);
        float sseFactor = 2f * tanHalf * error / height;
        return Mathf.Max(1f, slantRangeMeters) * sseFactor;
    }

    /// <summary>Half angle of the cone that circumscribes the camera frustum.</summary>
    public static float GetFrustumCornerHalfAngleDegrees(Camera camera)
    {
        if (camera == null)
        {
            return 45f;
        }

        float tanVertical = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanHorizontal = tanVertical * Mathf.Max(0.1f, camera.aspect);
        float corner = Mathf.Sqrt((tanVertical * tanVertical) + (tanHorizontal * tanHorizontal));
        return Mathf.Atan(corner) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Geocentric angle from the sub-viewer point out to the horizon, widened a little so
    /// elevated terrain just beyond it still loads.
    /// </summary>
    public static float GetHorizonHalfAngleDegrees(float planetRadius, float altitudeMeters)
    {
        if (planetRadius <= 1f)
        {
            return 90f;
        }

        float altitude = Mathf.Max(0f, altitudeMeters);
        float ratio = Mathf.Clamp01(planetRadius / (planetRadius + altitude));
        float horizon = Mathf.Acos(ratio) * Mathf.Rad2Deg;
        return Mathf.Min(
            180f,
            horizon + Mathf.Max(HorizonMarginDegrees, horizon * HorizonMarginFraction)
        );
    }

    /// <summary>
    /// Angular radius of the cap that bounds a chunk, seen from the planet centre. Visibility
    /// tests must subtract this before culling: a coarse node covers a huge patch, and culling
    /// it on its centre alone throws away every descendant that would have been visible.
    /// </summary>
    public static float GetNodeAngularRadiusDegrees(float nodeSpanMeters, float planetRadius)
    {
        if (planetRadius <= 1f)
        {
            return 90f;
        }

        float halfSpan = Mathf.Max(0f, nodeSpanMeters) * 0.5f;
        float circumRadius = halfSpan * 1.41421356f;
        return Mathf.Min(90f, (circumRadius / planetRadius) * Mathf.Rad2Deg);
    }

    /// <summary>
    /// Angle between the camera forward axis and the nearest point of the chunk's bounding
    /// sphere. Zero when the chunk overlaps the view axis or engulfs the camera.
    /// </summary>
    public static float GetCameraConeNearestAngleDegrees(
        ViewerFrame frame,
        Vector3 chunkWorldPosition,
        float chunkBoundingRadius
    )
    {
        if (!frame.HasCamera)
        {
            return 0f;
        }

        Vector3 toChunk = chunkWorldPosition - frame.CameraWorldPosition;
        float distance = toChunk.magnitude;

        if (distance <= chunkBoundingRadius || distance <= 1e-4f)
        {
            return 0f;
        }

        float centerAngle = Vector3.Angle(frame.CameraForward, toChunk);
        float angularRadius = GetBoundingSphereAngularRadiusDegrees(
            chunkBoundingRadius,
            distance
        );
        return Mathf.Max(0f, centerAngle - angularRadius);
    }

    /// <summary>
    /// Farthest point on the chunk bounding sphere from the camera forward axis. Used for
    /// merge/unload: only cull when the entire cap is outside the cone.
    /// </summary>
    public static float GetCameraConeFarthestAngleDegrees(
        ViewerFrame frame,
        Vector3 chunkWorldPosition,
        float chunkBoundingRadius
    )
    {
        if (!frame.HasCamera)
        {
            return 180f;
        }

        Vector3 toChunk = chunkWorldPosition - frame.CameraWorldPosition;
        float distance = toChunk.magnitude;

        if (distance <= chunkBoundingRadius || distance <= 1e-4f)
        {
            return 0f;
        }

        float centerAngle = Vector3.Angle(frame.CameraForward, toChunk);
        float angularRadius = GetBoundingSphereAngularRadiusDegrees(
            chunkBoundingRadius,
            distance
        );
        return centerAngle + angularRadius;
    }

    static float GetBoundingSphereAngularRadiusDegrees(
        float chunkBoundingRadius,
        float distance
    )
    {
        return Mathf.Asin(Mathf.Clamp01(chunkBoundingRadius / distance)) * Mathf.Rad2Deg;
    }

    /// <summary>Backward-compatible alias for split/visibility tests.</summary>
    public static float GetCameraConeAngleDegrees(
        ViewerFrame frame,
        Vector3 chunkWorldPosition,
        float chunkBoundingRadius
    )
    {
        return GetCameraConeNearestAngleDegrees(frame, chunkWorldPosition, chunkBoundingRadius);
    }

    /// <summary>
    /// Geocentric angle from nadir to the farthest point on the chunk bounding cap.
    /// Debug / cone analog only. Merge must use nearest: a tile that straddles the
    /// limb has farthest past the horizon while the near edge is still on the disk.
    /// </summary>
    public static float GetGeocentricFarthestAngleDegrees(
        Vector3 nadirUnit,
        Vector3 chunkCenterUnit,
        float nodeSpanMeters,
        float planetRadius
    )
    {
        float centerAngle = Vector3.Angle(nadirUnit, chunkCenterUnit);
        return centerAngle + GetNodeAngularRadiusDegrees(nodeSpanMeters, planetRadius);
    }

    /// <summary>
    /// Geocentric angle from nadir to the nearest point on the chunk cap (split/visibility).
    /// </summary>
    public static float GetGeocentricNearestAngleDegrees(
        Vector3 nadirUnit,
        Vector3 chunkCenterUnit,
        float nodeSpanMeters,
        float planetRadius
    )
    {
        float centerAngle = Vector3.Angle(nadirUnit, chunkCenterUnit);
        return Mathf.Max(
            0f,
            centerAngle - GetNodeAngularRadiusDegrees(nodeSpanMeters, planetRadius)
        );
    }

    /// <summary>
    /// Entire chunk is past the limb only when even the nearest surface point is.
    /// Farthest-angle merge treats overlapping tiles as fully beyond and snaps
    /// sibling-balance splits back the same tick.
    /// </summary>
    public static bool IsEntireCapBeyondHorizon(
        float nearestAngleDegrees,
        float horizonHalfAngleDegrees
    )
    {
        return nearestAngleDegrees > horizonHalfAngleDegrees;
    }

    public static ViewZone ClassifyZone(float cameraConeAngleDegrees, float coneHalfAngleDegrees)
    {
        if (cameraConeAngleDegrees > coneHalfAngleDegrees + PaddingHalfAngleDegrees)
        {
            return ViewZone.Dead;
        }

        if (cameraConeAngleDegrees > coneHalfAngleDegrees)
        {
            return ViewZone.Padding;
        }

        if (cameraConeAngleDegrees <= coneHalfAngleDegrees * 0.5f)
        {
            return ViewZone.Focus;
        }

        return ViewZone.Active;
    }

    /// <summary>Centre of the screen splits soonest; the periphery is allowed to stay coarser.</summary>
    public static float ComputeViewWeight(float cameraConeAngleDegrees, float coneHalfAngleDegrees)
    {
        float focusHalfAngle = coneHalfAngleDegrees * 0.5f;

        if (cameraConeAngleDegrees <= focusHalfAngle)
        {
            return 1f;
        }

        if (cameraConeAngleDegrees >= coneHalfAngleDegrees)
        {
            return PeripheralViewWeightMax;
        }

        float blend = (cameraConeAngleDegrees - focusHalfAngle) /
            Mathf.Max(1e-3f, coneHalfAngleDegrees - focusHalfAngle);
        return Mathf.Lerp(1f, PeripheralViewWeightMax, blend);
    }

    public static float GetDeadZoneHalfAngleDegrees(float coneHalfAngleDegrees)
    {
        return coneHalfAngleDegrees + PaddingHalfAngleDegrees;
    }

    public static float GetHorizonUnloadHalfAngleDegrees(float coneHalfAngleDegrees)
    {
        return GetDeadZoneHalfAngleDegrees(coneHalfAngleDegrees) +
            PlanetPqsAsyncLodSettings.ViewConeUnloadHysteresisDegrees;
    }

    /// <summary>
    /// Outward unit direction (Mesh Frame) under the camera crosshair on the planet sphere.
    /// </summary>
    public static bool TryGetCameraViewSurfaceUnitMesh(
        Transform planetTransform,
        Camera camera,
        float planetRadius,
        out Vector3 outwardUnitMesh
    )
    {
        outwardUnitMesh = Vector3.up;

        if (planetTransform == null || camera == null || planetRadius <= 1f)
        {
            return false;
        }

        Vector3 rayOriginLocal = planetTransform.InverseTransformPoint(camera.transform.position);
        Vector3 rayDirectionLocal = planetTransform.InverseTransformDirection(
            camera.transform.forward
        );

        if (rayDirectionLocal.sqrMagnitude <= 1e-8f)
        {
            return false;
        }

        rayDirectionLocal.Normalize();

        float a = Vector3.Dot(rayDirectionLocal, rayDirectionLocal);
        float b = 2f * Vector3.Dot(rayOriginLocal, rayDirectionLocal);
        float c = Vector3.Dot(rayOriginLocal, rayOriginLocal) - (planetRadius * planetRadius);
        float discriminant = (b * b) - (4f * a * c);

        if (discriminant >= 0f)
        {
            float sqrtDisc = Mathf.Sqrt(discriminant);
            float tNear = (-b - sqrtDisc) / (2f * a);
            float tFar = (-b + sqrtDisc) / (2f * a);
            float t = tNear >= 0f ? tNear : tFar;

            if (t >= 0f)
            {
                outwardUnitMesh = (rayOriginLocal + (rayDirectionLocal * t)).normalized;
                return true;
            }
        }

        float tClosest = -Vector3.Dot(rayOriginLocal, rayDirectionLocal);

        if (tClosest > 0f)
        {
            outwardUnitMesh = (rayOriginLocal + (rayDirectionLocal * tClosest)).normalized;
            return false;
        }

        if (rayOriginLocal.sqrMagnitude > 1e-8f)
        {
            outwardUnitMesh = rayOriginLocal.normalized;
        }

        return false;
    }

    public static Vector3 MeshUnitToSimUnit(Vector3 meshUnit)
    {
        return PlanetCubeSphere.MeshFrameUnitToSimUnit(meshUnit);
    }

    /// <summary>
    /// Lead the ship focus along surface velocity for preloading terrain ahead on fast flight.
    /// </summary>
    public static Vector3 ApplyVelocityLookAheadOnSphere(
        Vector3 focusSimUnit,
        Vector3 velocityWorld,
        float planetRadius,
        float leadTimeSeconds,
        float maxLeadAngleDegrees = 12f
    )
    {
        if (focusSimUnit.sqrMagnitude <= 0.5f || velocityWorld.sqrMagnitude <= 0.25f)
        {
            return focusSimUnit.normalized;
        }

        Vector3 radial = focusSimUnit.normalized;
        Vector3 tangentVelocity = velocityWorld - (Vector3.Dot(velocityWorld, radial) * radial);

        if (tangentVelocity.sqrMagnitude <= 1e-6f)
        {
            return radial;
        }

        float leadAngleRadians = (tangentVelocity.magnitude * leadTimeSeconds) / planetRadius;
        float maxRadians = maxLeadAngleDegrees * Mathf.Deg2Rad;
        leadAngleRadians = Mathf.Min(leadAngleRadians, maxRadians);

        Vector3 tangentUnit = tangentVelocity.normalized;
        Vector3 ledDirection = radial + (tangentUnit * leadAngleRadians);
        return ledDirection.sqrMagnitude > 1e-8f ? ledDirection.normalized : radial;
    }
}
