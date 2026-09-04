using UnityEngine;

/// <summary>
/// Cube-sphere mapping shared by planet LOD chunks.
/// </summary>
static class PlanetCubeSphere
{
    public static readonly Vector3[] FaceNormals =
    {
        Vector3.up,
        Vector3.down,
        Vector3.left,
        Vector3.right,
        Vector3.forward,
        Vector3.back,
    };

    public static Vector3 FaceUvToUnitDirection(int faceIndex, float u, float v)
    {
        Vector2 insetUv = InsetPolarFaceUv(faceIndex, u, v);
        SimVector3 direction = PlanetCubeSphereCoords.FaceUvToUnitDirection(
            faceIndex,
            insetUv.x,
            insetUv.y
        );
        return new Vector3((float)direction.x, (float)direction.y, (float)direction.z);
    }

    /// <summary>
    /// Polar cube faces (+Y/-Y) collapse UV center to a pole singularity.
    /// Push samples onto a small ring around the pole before normalization.
    /// </summary>
    public static Vector2 InsetPolarFaceUv(int faceIndex, float u, float v, float minRadius = 0.015f)
    {
        if (faceIndex != 0 && faceIndex != 1)
        {
            return new Vector2(u, v);
        }

        float deltaU = u - 0.5f;
        float deltaV = v - 0.5f;
        float radius = Mathf.Sqrt((deltaU * deltaU) + (deltaV * deltaV));

        if (radius >= minRadius)
        {
            return new Vector2(u, v);
        }

        if (radius <= 1e-6f)
        {
            return new Vector2(0.5f + minRadius, 0.5f);
        }

        float scale = minRadius / radius;
        return new Vector2(0.5f + (deltaU * scale), 0.5f + (deltaV * scale));
    }

    /// <summary>
    /// Avoid atan2 instability and pole heightmap seams when sampling displacement.
    /// </summary>
    public static Vector3 SanitizeUnitDirectionForHeightSampling(Vector3 direction)
    {
        direction.Normalize();
        const float minHorizontal = 0.0025f;
        float horizontalMagnitude = Mathf.Sqrt(
            (direction.x * direction.x) + (direction.z * direction.z)
        );

        if (horizontalMagnitude >= minHorizontal)
        {
            return direction;
        }

        float poleSign = direction.y >= 0f ? 1f : -1f;
        float verticalComponent = Mathf.Sqrt(1f - (minHorizontal * minHorizontal));
        return new Vector3(minHorizontal, poleSign * verticalComponent, minHorizontal).normalized;
    }

    /// <summary>
    /// Inverse of <see cref="FaceUvToUnitDirection"/>. Delegates to the simulation-core
    /// Nowell inverse so chunk lookups land on the chunk that actually renders at
    /// <paramref name="direction"/>.
    /// </summary>
    public static bool TryDirectionToFaceUv(
        Vector3 direction,
        out int faceIndex,
        out float u,
        out float v
    )
    {
        direction.Normalize();
        bool resolved = PlanetCubeSphereCoords.TryUnitDirectionToFaceUv(
            new SimVector3(direction.x, direction.y, direction.z),
            out faceIndex,
            out double faceU,
            out double faceV
        );
        u = (float)faceU;
        v = (float)faceV;
        return resolved;
    }

    public static bool IsDirectionInsideFaceUvBounds(
        Vector3 direction,
        int faceIndex,
        float minU,
        float maxU,
        float minV,
        float maxV
    )
    {
        if (!TryDirectionToFaceUv(direction, out int directionFace, out float u, out float v))
        {
            return false;
        }

        if (directionFace != faceIndex)
        {
            return false;
        }

        return u >= minU && u <= maxU && v >= minV && v <= maxV;
    }

    /// <summary>
    /// Canonical geographic UV. South pole V=0, north V=1.
    /// </summary>
    public static Vector2 DirectionToGeographicUv(Vector3 direction)
    {
        direction.Normalize();
        var simDirection = new SimVector3(direction.x, direction.y, direction.z).Normalize();
        PlanetGeographicCoords.DirectionToGeographicUv(
            simDirection,
            out double geographicU,
            out double geographicV
        );
        return new Vector2((float)geographicU, (float)geographicV);
    }

    /// <summary>Alias for <see cref="DirectionToGeographicUv"/>.</summary>
    public static Vector2 DirectionToSimulationGeographicUv(Vector3 direction)
    {
        return DirectionToGeographicUv(direction);
    }

    /// <summary>
    /// UV for row-flipped RSS EarthColor/EarthHeight DDS (north at V=0).
    /// </summary>
    public static Vector2 DirectionToRssTextureUv(Vector3 direction)
    {
        Vector2 geographicUv = DirectionToSimulationGeographicUv(direction);
        return new Vector2(
            geographicUv.x,
            (float)PlanetGeographicCoords.MapGeographicVToRssEarthTextureV(geographicUv.y)
        );
    }

    /// <summary>
    /// Mesh macro color / flight terrain UV (canonical geographic).
    /// </summary>
    public static Vector2 DirectionToLatLongUv(Vector3 direction)
    {
        return DirectionToGeographicUv(direction);
    }

    /// <summary>
    /// Mesh Frame (Stage 4): negate X on Unity mesh positions so NASA equirect U matches visuals.
    /// Simulation and CPU/GPU samplers stay canonical (west = −X in sim).
    /// </summary>
    public static Vector3 FlipMeshVertexPositionX(Vector3 position)
    {
        position.x = -position.x;
        return position;
    }

    /// <summary>Sim unit direction → Mesh Frame unit direction for Unity 3D placement.</summary>
    public static Vector3 SimDirectionToMeshFrame(Vector3 simUnitDirection)
    {
        return FlipMeshVertexPositionX(simUnitDirection);
    }

    /// <summary>Mesh Frame unit → sim unit (cube-sphere / GEBCO canon).</summary>
    public static Vector3 MeshFrameUnitToSimUnit(Vector3 meshUnitDirection)
    {
        return FlipMeshVertexPositionX(meshUnitDirection);
    }

    /// <summary>
    /// Inverse of <see cref="DirectionToLatLongUv"/> (Y = north pole, lon +east).
    /// </summary>
    public static Vector3 DirectionFromLatLongRadians(float latitudeRadians, float longitudeRadians)
    {
        float cosLat = Mathf.Cos(latitudeRadians);
        float sinLat = Mathf.Sin(latitudeRadians);
        float sinLon = Mathf.Sin(longitudeRadians);
        float cosLon = Mathf.Cos(longitudeRadians);
        return new Vector3(cosLat * sinLon, sinLat, cosLat * cosLon).normalized;
    }

    public static float SurfaceDistanceMeters(
        Vector3 directionA,
        Vector3 directionB,
        float planetRadius
    )
    {
        directionA.Normalize();
        directionB.Normalize();
        float angleRadians = Mathf.Acos(Mathf.Clamp(Vector3.Dot(directionA, directionB), -1f, 1f));
        return angleRadians * planetRadius;
    }
}
