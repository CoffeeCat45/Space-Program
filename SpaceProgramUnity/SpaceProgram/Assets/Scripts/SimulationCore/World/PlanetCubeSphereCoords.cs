using System;

/// <summary>
/// Cube-sphere coordinate math for SimulationCore (Philip Nowell spherification).
/// Face layout matches Unity <see cref="PlanetCubeSphere"/> for future PQS integration.
/// </summary>
public static class PlanetCubeSphereCoords
{
    public const int FaceCount = 6;

    /// <summary>+Y, -Y, -X, +X, +Z, -Z.</summary>
    public static readonly SimVector3[] FaceNormals =
    {
        SimVector3.UnitY(),
        new SimVector3(0.0, -1.0, 0.0),
        new SimVector3(-1.0, 0.0, 0.0),
        SimVector3.UnitX(),
        SimVector3.UnitZ(),
        new SimVector3(0.0, 0.0, -1.0),
    };

    /// <summary>
    /// Face UV in [0, 1] to a unit-sphere direction using Philip Nowell spherification.
    /// </summary>
    public static SimVector3 FaceUvToUnitDirection(int faceIndex, double u, double v)
    {
        ValidateFaceIndex(faceIndex);
        SimVector3 cubePoint = FaceUvToCubePoint(faceIndex, u, v);
        return SpherifyCubePoint(cubePoint).Normalize();
    }

    /// <summary>
    /// Point on the cube face (one axis = ±1) from face UV in [0, 1].
    /// Local tangent coordinates are in [-1, 1] before spherification.
    /// </summary>
    public static SimVector3 FaceUvToCubePoint(int faceIndex, double u, double v)
    {
        ValidateFaceIndex(faceIndex);

        double cubeU = (Clamp01(u) * 2.0) - 1.0;
        double cubeV = (Clamp01(v) * 2.0) - 1.0;
        SimVector3 normal = FaceNormals[faceIndex];
        SimVector3 axisU = GetFaceAxisU(faceIndex);
        SimVector3 axisV = GetFaceAxisV(faceIndex);

        return normal
            .Add(axisU.Multiply(cubeU))
            .Add(axisV.Multiply(cubeV));
    }

    /// <summary>
    /// Philip Nowell cube-to-sphere mapping. Input must lie on the cube surface.
    /// </summary>
    public static SimVector3 SpherifyCubePoint(SimVector3 cubeSurfacePoint)
    {
        double x = cubeSurfacePoint.x;
        double y = cubeSurfacePoint.y;
        double z = cubeSurfacePoint.z;
        double x2 = x * x;
        double y2 = y * y;
        double z2 = z * z;

        return new SimVector3(
            x * Math.Sqrt(1.0 - (y2 / 2.0) - (z2 / 2.0) + ((y2 * z2) / 3.0)),
            y * Math.Sqrt(1.0 - (z2 / 2.0) - (x2 / 2.0) + ((z2 * x2) / 3.0)),
            z * Math.Sqrt(1.0 - (x2 / 2.0) - (y2 / 2.0) + ((x2 * y2) / 3.0))
        );
    }

    /// <summary>
    /// Dominant cube face for a direction. Ties on cube edges/corners resolve to the
    /// lowest-index face, matching <see cref="TryUnitDirectionToFaceUv"/>.
    /// </summary>
    public static int GetDominantFaceIndex(SimVector3 direction)
    {
        double absX = Math.Abs(direction.x);
        double absY = Math.Abs(direction.y);
        double absZ = Math.Abs(direction.z);

        if (absX >= absY && absX >= absZ)
        {
            return direction.x >= 0.0 ? 3 : 2;
        }

        if (absY >= absZ)
        {
            return direction.y >= 0.0 ? 0 : 1;
        }

        return direction.z >= 0.0 ? 4 : 5;
    }

    /// <summary>
    /// Projects a unit-sphere direction onto the dominant cube face and returns UV in [0, 1].
    /// Exact inverse of <see cref="FaceUvToUnitDirection"/>.
    /// </summary>
    public static bool TryUnitDirectionToFaceUv(
        SimVector3 direction,
        out int faceIndex,
        out double u,
        out double v
    )
    {
        direction = direction.Normalize();
        faceIndex = GetDominantFaceIndex(direction);

        SimVector3 normal = FaceNormals[faceIndex];
        double normalComponent = Math.Abs(SimVector3.Dot(direction, normal));

        if (normalComponent < 1e-12)
        {
            u = 0.5;
            v = 0.5;
            return false;
        }

        UnspherifyToFaceTangentCoords(
            direction,
            faceIndex,
            out double cubeU,
            out double cubeV
        );
        u = Clamp01((cubeU * 0.5) + 0.5);
        v = Clamp01((cubeV * 0.5) + 0.5);
        return true;
    }

    /// <summary>
    /// Resolve a face-UV probe that may sit just outside [0, 1] onto the cube face that
    /// actually owns that direction. In-range probes stay on the same face; out-of-range
    /// probes step off the shared edge onto the adjacent face so restricted-quadtree
    /// neighbor tests work across cube seams, not only inside one face.
    /// </summary>
    public static bool TryResolveUvProbe(
        int faceIndex,
        double probeU,
        double probeV,
        out int resolvedFace,
        out double resolvedU,
        out double resolvedV
    )
    {
        ValidateFaceIndex(faceIndex);

        if (probeU >= 0.0 && probeU <= 1.0 && probeV >= 0.0 && probeV <= 1.0)
        {
            resolvedFace = faceIndex;
            resolvedU = probeU;
            resolvedV = probeV;
            return true;
        }

        double edgeU = Clamp01(probeU);
        double edgeV = Clamp01(probeV);
        SimVector3 edgeDirection = FaceUvToUnitDirection(faceIndex, edgeU, edgeV);

        const double inset = 0.05;
        double inwardU = edgeU;
        double inwardV = edgeV;

        if (probeU < 0.0)
        {
            inwardU = Math.Min(1.0, edgeU + inset);
        }
        else if (probeU > 1.0)
        {
            inwardU = Math.Max(0.0, edgeU - inset);
        }

        if (probeV < 0.0)
        {
            inwardV = Math.Min(1.0, edgeV + inset);
        }
        else if (probeV > 1.0)
        {
            inwardV = Math.Max(0.0, edgeV - inset);
        }

        SimVector3 inwardDirection = FaceUvToUnitDirection(faceIndex, inwardU, inwardV);
        SimVector3 tangent = edgeDirection.Subtract(inwardDirection);

        if (tangent.LengthSquared() <= 1e-16)
        {
            resolvedFace = faceIndex;
            resolvedU = edgeU;
            resolvedV = edgeV;
            return true;
        }

        SimVector3 probeDirection = edgeDirection
            .Add(tangent.Normalize().Multiply(0.02))
            .Normalize();
        return TryUnitDirectionToFaceUv(
            probeDirection,
            out resolvedFace,
            out resolvedU,
            out resolvedV
        );
    }

    /// <summary>
    /// Inverts <see cref="SpherifyCubePoint"/> back to the face tangent coords in [-1, 1].
    /// </summary>
    /// <remarks>
    /// A gnomonic projection (<c>direction / |direction · faceNormal|</c>) is NOT the inverse
    /// of the Nowell spherification — the two mappings only agree at face centres and cube
    /// edges, and diverge by up to 6.7° in between. On Earth that is ~750 km of ground error,
    /// which silently sends chunk lookups (LOD focus, launch site, elevation) to a chunk far
    /// away from the one that actually renders at that direction.
    ///
    /// Writing the face-local sphere components as (a, b) along axisU/axisV, the forward
    /// mapping gives 6a² = cu²·(3 − cv²) and 6b² = cv²·(3 − cu²). Adding and subtracting
    /// those leaves cu² − cv² known outright and cu² + cv² as the smaller root of a
    /// quadratic, which recovers both tangent coords in closed form.
    /// </remarks>
    public static void UnspherifyToFaceTangentCoords(
        SimVector3 unitDirection,
        int faceIndex,
        out double cubeU,
        out double cubeV
    )
    {
        ValidateFaceIndex(faceIndex);

        double alongU = SimVector3.Dot(unitDirection, GetFaceAxisU(faceIndex));
        double alongV = SimVector3.Dot(unitDirection, GetFaceAxisV(faceIndex));
        double scaledU = 6.0 * alongU * alongU;
        double scaledV = 6.0 * alongV * alongV;

        double difference = (scaledU - scaledV) / 3.0;
        double discriminant = 9.0 - (2.0 * (scaledU + scaledV)) + (difference * difference);
        double root = Math.Sqrt(discriminant > 0.0 ? discriminant : 0.0);

        // Rationalized form of 3 - root: near a face centre both terms approach 3 and the
        // direct subtraction loses most of the mantissa.
        double sum = ((2.0 * (scaledU + scaledV)) - (difference * difference)) / (3.0 + root);

        cubeU = SignedSquareRoot((sum + difference) * 0.5, alongU);
        cubeV = SignedSquareRoot((sum - difference) * 0.5, alongV);
    }

    static double SignedSquareRoot(double squaredMagnitude, double sign)
    {
        double magnitude = Math.Sqrt(squaredMagnitude > 0.0 ? squaredMagnitude : 0.0);

        if (magnitude > 1.0)
        {
            magnitude = 1.0;
        }

        return sign < 0.0 ? -magnitude : magnitude;
    }

    /// <summary>
    /// Geographic lat/lon (radians) to dominant cube face and UV in [0, 1].
    /// </summary>
    public static void LatitudeLongitudeRadiansToFaceUv(
        double latitudeRadians,
        double longitudeRadians,
        out int faceIndex,
        out double u,
        out double v
    )
    {
        SimVector3 direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            latitudeRadians,
            longitudeRadians
        );
        TryUnitDirectionToFaceUv(direction, out faceIndex, out u, out v);
    }

    /// <summary>
    /// PQS mesh path: face UV → Philip Nowell sphere → GEBCO sample.
    /// </summary>
    public static double SampleElevationAtFaceUv(
        PlanetElevationField elevationField,
        int faceIndex,
        double u,
        double v
    )
    {
        if (elevationField == null)
        {
            throw new ArgumentNullException(nameof(elevationField));
        }

        SimVector3 sampleDirection = FaceUvToUnitDirection(faceIndex, u, v);
        return elevationField.GetElevationAtDirection(sampleDirection);
    }

    /// <summary>
    /// Geographic query at exact lat/lon (canonical GEBCO sample direction).
    /// </summary>
    public static double SampleElevationAtLatitudeLongitude(
        PlanetElevationField elevationField,
        double latitudeRadians,
        double longitudeRadians
    )
    {
        if (elevationField == null)
        {
            throw new ArgumentNullException(nameof(elevationField));
        }

        SimVector3 direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            latitudeRadians,
            longitudeRadians
        );
        return elevationField.GetElevationAtDirection(direction);
    }

    /// <summary>
    /// Lat/lon → cube-face assignment, then GEBCO at the geographic point on the sphere.
    /// Cube UV is returned for chunk lookup; elevation uses the true lat/lon direction.
    /// </summary>
    public static double SampleElevationWithCubeFaceAssignment(
        PlanetElevationField elevationField,
        double latitudeRadians,
        double longitudeRadians,
        out int faceIndex,
        out double faceU,
        out double faceV
    )
    {
        LatitudeLongitudeRadiansToFaceUv(
            latitudeRadians,
            longitudeRadians,
            out faceIndex,
            out faceU,
            out faceV
        );

        return SampleElevationAtLatitudeLongitude(
            elevationField,
            latitudeRadians,
            longitudeRadians
        );
    }

    public static SimVector3 GetFaceAxisU(int faceIndex)
    {
        ValidateFaceIndex(faceIndex);
        SimVector3 normal = FaceNormals[faceIndex];
        SimVector3 axisU = new SimVector3(normal.y, normal.z, normal.x);

        if (axisU.LengthSquared() < 1e-16)
        {
            axisU = SimVector3.UnitX();
        }

        return axisU.Normalize();
    }

    public static SimVector3 GetFaceAxisV(int faceIndex)
    {
        ValidateFaceIndex(faceIndex);
        SimVector3 normal = FaceNormals[faceIndex];
        return SimVector3.Cross(normal, GetFaceAxisU(faceIndex)).Normalize();
    }

    static void ValidateFaceIndex(int faceIndex)
    {
        if (faceIndex < 0 || faceIndex >= FaceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(faceIndex),
                faceIndex,
                "Cube face index must be in [0, " + (FaceCount - 1) + "]."
            );
        }
    }

    static double Clamp01(double value)
    {
        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}
