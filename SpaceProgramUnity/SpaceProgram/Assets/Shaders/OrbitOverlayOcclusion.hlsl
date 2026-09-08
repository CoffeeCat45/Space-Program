#ifndef ORBIT_OVERLAY_OCCLUSION_INCLUDED
#define ORBIT_OVERLAY_OCCLUSION_INCLUDED

float _SPOrbitOccluderEnabled;
float4 _SPOrbitOccluderCenter;
float _SPOrbitOccluderRadius;
float _SPOrbitOccluder2Enabled;
float4 _SPOrbitOccluder2Center;
float _SPOrbitOccluder2Radius;

bool SphereOccludesRay(
    float3 cameraPosition,
    float3 rayDirection,
    float targetDistance,
    float3 center,
    float radius
)
{
    if (radius <= 0.0)
    {
        return false;
    }

    float3 originToCenter = cameraPosition - center;
    float radiusSq = radius * radius;
    float centerDistanceSq = dot(originToCenter, originToCenter);
    float projection = dot(rayDirection, originToCenter);
    float discriminant =
        projection * projection - (centerDistanceSq - radiusSq);

    if (discriminant < 0.0)
    {
        return false;
    }

    float sqrtDiscriminant = sqrt(discriminant);

    if (centerDistanceSq < radiusSq)
    {
        return false;
    }

    float intersectionDistance = -projection - sqrtDiscriminant;
    return intersectionDistance > 1e-4 && intersectionDistance < targetDistance;
}

bool SphereOccludesPoint(float3 positionWS, float3 center, float radius)
{
    if (radius <= 0.0)
    {
        return false;
    }

    float3 cameraPosition = _WorldSpaceCameraPos;
    float3 originToCenter = cameraPosition - center;
    float radiusSq = radius * radius;

    if (dot(originToCenter, originToCenter) < radiusSq)
    {
        return false;
    }

    float3 toCenter = positionWS - center;
    if (dot(toCenter, toCenter) < radiusSq)
    {
        return true;
    }

    float3 toPoint = positionWS - cameraPosition;
    float targetDistance = length(toPoint);
    if (targetDistance <= 1e-5)
    {
        return false;
    }

    return SphereOccludesRay(
        cameraPosition,
        toPoint / targetDistance,
        targetDistance,
        center,
        radius
    );
}

bool IsOccludedByPlanet(float3 positionWS)
{
    if (_SPOrbitOccluderEnabled > 0.5 &&
        SphereOccludesPoint(
            positionWS,
            _SPOrbitOccluderCenter.xyz,
            max(_SPOrbitOccluderRadius, 0.0)))
    {
        return true;
    }

    if (_SPOrbitOccluder2Enabled > 0.5 &&
        SphereOccludesPoint(
            positionWS,
            _SPOrbitOccluder2Center.xyz,
            max(_SPOrbitOccluder2Radius, 0.0)))
    {
        return true;
    }

    return false;
}

#endif
