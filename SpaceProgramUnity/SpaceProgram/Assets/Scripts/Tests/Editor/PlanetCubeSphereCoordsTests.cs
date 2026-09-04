using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for terrain_plan.md step 1.1 (Cube-to-Sphere in SimulationCore).
/// </summary>
public sealed class PlanetCubeSphereCoordsTests
{
    const double DirectionTolerance = 1e-6;
    const double UvTolerance = 1e-9;
    const double ElevationToleranceMeters = 0.5;

    [Test]
    public void SpherifyCubePoint_FaceCenter_ProducesUnitVector()
    {
        SimVector3 cubePoint = PlanetCubeSphereCoords.FaceNormals[3];
        SimVector3 spherePoint = PlanetCubeSphereCoords.SpherifyCubePoint(cubePoint).Normalize();

        Assert.AreEqual(1.0, spherePoint.Length(), 1e-9);
        Assert.AreEqual(1.0, spherePoint.x, DirectionTolerance);
        Assert.AreEqual(0.0, spherePoint.y, DirectionTolerance);
        Assert.AreEqual(0.0, spherePoint.z, DirectionTolerance);
    }

    [Test]
    public void FaceUvToCubePoint_RoundTrip_RecoversUvExactly()
    {
        for (int faceIndex = 0; faceIndex < PlanetCubeSphereCoords.FaceCount; faceIndex++)
        {
            for (int ui = 1; ui <= 8; ui++)
            {
                for (int vi = 1; vi <= 8; vi++)
                {
                    double u = ui / 9.0;
                    double v = vi / 9.0;
                    SimVector3 cubePoint = PlanetCubeSphereCoords.FaceUvToCubePoint(faceIndex, u, v);
                    RecoverCubeFaceUv(cubePoint, faceIndex, out double recoveredU, out double recoveredV);

                    Assert.AreEqual(u, recoveredU, UvTolerance);
                    Assert.AreEqual(v, recoveredV, UvTolerance);
                }
            }
        }
    }

    [Test]
    public void FaceUvToUnitDirection_PreservesDominantFace()
    {
        for (int faceIndex = 0; faceIndex < PlanetCubeSphereCoords.FaceCount; faceIndex++)
        {
            for (int ui = 1; ui <= 8; ui++)
            {
                for (int vi = 1; vi <= 8; vi++)
                {
                    double u = ui / 9.0;
                    double v = vi / 9.0;
                    SimVector3 direction = PlanetCubeSphereCoords.FaceUvToUnitDirection(faceIndex, u, v);

                    Assert.AreEqual(1.0, direction.Length(), 1e-9);
                    Assert.IsTrue(
                        PlanetCubeSphereCoords.TryUnitDirectionToFaceUv(
                            direction,
                            out int recoveredFace,
                            out _,
                            out _
                        )
                    );
                    Assert.AreEqual(faceIndex, recoveredFace);
                }
            }
        }
    }

    [Test]
    public void LatitudeLongitudeToFaceUv_EquatorPrimeMeridian_IsPlusZFaceCenter()
    {
        PlanetCubeSphereCoords.LatitudeLongitudeRadiansToFaceUv(
            0.0,
            0.0,
            out int faceIndex,
            out double u,
            out double v
        );

        Assert.AreEqual(4, faceIndex);
        Assert.AreEqual(0.5, u, 1e-5);
        Assert.AreEqual(0.5, v, 1e-5);
    }

    [Test]
    public void LatitudeLongitudeToFaceUv_EquatorNinetyEast_IsPlusXFaceCenter()
    {
        PlanetCubeSphereCoords.LatitudeLongitudeRadiansToFaceUv(
            0.0,
            Math.PI * 0.5,
            out int faceIndex,
            out double u,
            out double v
        );

        Assert.AreEqual(3, faceIndex);
        Assert.AreEqual(0.5, u, 1e-5);
        Assert.AreEqual(0.5, v, 1e-5);
    }

    [Test]
    public void SampleElevationAtFaceUv_SyntheticGrid_ReturnsKnownValue()
    {
        const int width = 2;
        const int height = 1;
        var elevations = new float[width * height];

        for (int i = 0; i < elevations.Length; i++)
        {
            elevations[i] = 123.45f;
        }

        PlanetElevationField field = LoadFieldFromMemory(elevations, width, height);
        double elevation = PlanetCubeSphereCoords.SampleElevationAtFaceUv(field, 4, 0.5, 0.5);

        Assert.AreEqual(123.45, elevation, ElevationToleranceMeters);
    }

    [Test]
    public void SampleElevationWithCubeFaceAssignment_MatchesDirectGeographicSampling()
    {
        if (!TryLoadGebcoField(out PlanetElevationField field))
        {
            Assert.Ignore("GEBCO .bin is not baked yet. Run Space Program → Bake GEBCO Elevation (.bin).");
            return;
        }

        (double latDeg, double lonDeg)[] referencePoints =
        {
            (28.57, -80.65),
            (31.48, 91.10),
            (27.9881, 86.9250),
            (0.0, 0.0),
        };

        foreach ((double latDeg, double lonDeg) point in referencePoints)
        {
            double latRad = point.latDeg * (Math.PI / 180.0);
            double lonRad = point.lonDeg * (Math.PI / 180.0);

            double assignedPath = PlanetCubeSphereCoords.SampleElevationWithCubeFaceAssignment(
                field,
                latRad,
                lonRad,
                out int faceIndex,
                out double faceU,
                out double faceV
            );
            double directPath = field.SampleLatitudeLongitudeRadians(latRad, lonRad);

            Assert.AreEqual(directPath, assignedPath, ElevationToleranceMeters);
            Assert.GreaterOrEqual(faceIndex, 0);
            Assert.Less(faceIndex, PlanetCubeSphereCoords.FaceCount);
            Assert.GreaterOrEqual(faceU, 0.0);
            Assert.LessOrEqual(faceU, 1.0);
            Assert.GreaterOrEqual(faceV, 0.0);
            Assert.LessOrEqual(faceV, 1.0);
        }
    }

    [Test]
    public void SampleElevationAtLatitudeLongitude_GebcoLandmarks_AreInExpectedRanges()
    {
        if (!TryLoadGebcoField(out PlanetElevationField field))
        {
            Assert.Ignore("GEBCO .bin is not baked yet. Run Space Program → Bake GEBCO Elevation (.bin).");
            return;
        }

        if (HasBathymetry(field))
        {
            AssertElevationInRange(field, 28.57, -80.65, -50.0, 200.0, "Cape Canaveral");
            AssertElevationInRange(field, 31.48, 91.10, 3500.0, 5500.0, "Tibet plateau");
            AssertElevationInRange(field, 27.9881, 86.9250, 8400.0, 9200.0, "Everest");
            return;
        }

        // Earthrev 8-bit UInt bake: no bathymetry, ~42 m vertical resolution, max ~10.7 km.
        AssertElevationInRange(field, 28.57, -80.65, -10.0, 500.0, "Cape Canaveral");
        AssertElevationInRange(field, 31.48, 91.10, 6000.0, 9000.0, "Tibet/Himalaya region");
        AssertElevationInRange(field, 27.9881, 86.9250, 7500.0, 10710.0, "Everest region");
    }

    [Test]
    public void TryResolveUvProbe_InsideFace_StaysOnSameFace()
    {
        Assert.IsTrue(
            PlanetCubeSphereCoords.TryResolveUvProbe(
                4,
                0.25,
                0.75,
                out int face,
                out double u,
                out double v
            )
        );
        Assert.AreEqual(4, face);
        Assert.AreEqual(0.25, u, UvTolerance);
        Assert.AreEqual(0.75, v, UvTolerance);
    }

    [Test]
    public void TryResolveUvProbe_OutsideFace_MapsToNeighborFace()
    {
        for (int faceIndex = 0; faceIndex < PlanetCubeSphereCoords.FaceCount; faceIndex++)
        {
            Assert.IsTrue(
                PlanetCubeSphereCoords.TryResolveUvProbe(
                    faceIndex,
                    1.05,
                    0.5,
                    out int neighborFace,
                    out double neighborU,
                    out double neighborV
                ),
                "face " + faceIndex
            );
            Assert.AreNotEqual(faceIndex, neighborFace, "face " + faceIndex);
            Assert.GreaterOrEqual(neighborU, 0.0);
            Assert.LessOrEqual(neighborU, 1.0);
            Assert.GreaterOrEqual(neighborV, 0.0);
            Assert.LessOrEqual(neighborV, 1.0);
        }
    }

    [Test]
    public void FaceUvToUnitDirection_FaceCenter_MatchesGeographicLatLon()
    {
        PlanetCubeSphereCoords.LatitudeLongitudeRadiansToFaceUv(
            0.0,
            0.0,
            out int faceIndex,
            out double u,
            out double v
        );

        SimVector3 direction = PlanetCubeSphereCoords.FaceUvToUnitDirection(faceIndex, u, v);
        PlanetGeographicCoords.DirectionToLatitudeLongitude(
            direction,
            out double latitudeRadians,
            out double longitudeRadians
        );

        Assert.AreEqual(0.0, latitudeRadians, 1e-4);
        Assert.AreEqual(0.0, longitudeRadians, 1e-4);
    }

    /// <summary>
    /// The quadtree decides which chunk holds the viewer by mapping a direction back to face
    /// UV, then builds that chunk's mesh by mapping the UV forward again. If the two are not
    /// inverses the "viewer chunk" renders somewhere else entirely — a gnomonic inverse put
    /// max LOD up to 750 km off the vessel, and only agreed at 0°N 0°E (a face centre).
    /// </summary>
    [Test]
    public void UnitDirectionToFaceUv_RoundTripsThroughSphere_OnEveryFace()
    {
        for (int faceIndex = 0; faceIndex < PlanetCubeSphereCoords.FaceCount; faceIndex++)
        {
            for (int ui = 1; ui <= 16; ui++)
            {
                for (int vi = 1; vi <= 16; vi++)
                {
                    double u = ui / 17.0;
                    double v = vi / 17.0;
                    SimVector3 direction = PlanetCubeSphereCoords.FaceUvToUnitDirection(
                        faceIndex,
                        u,
                        v
                    );

                    Assert.IsTrue(
                        PlanetCubeSphereCoords.TryUnitDirectionToFaceUv(
                            direction,
                            out int recoveredFace,
                            out double recoveredU,
                            out double recoveredV
                        )
                    );

                    Assert.AreEqual(faceIndex, recoveredFace);
                    Assert.AreEqual(u, recoveredU, 1e-7);
                    Assert.AreEqual(v, recoveredV, 1e-7);
                }
            }
        }
    }

    [Test]
    public void LatitudeLongitudeToFaceUv_ChunkUnderSite_RendersAtThatSite()
    {
        AssertFaceUvResolvesToSite(0.0, 0.0, "Greenwich");
        AssertFaceUvResolvesToSite(28.57, -80.65, "Cape Canaveral");
        AssertFaceUvResolvesToSite(45.96, 63.31, "Baikonur");
        AssertFaceUvResolvesToSite(5.24, -52.77, "Kourou");
        AssertFaceUvResolvesToSite(-57.0, -45.0, "Worst-case gnomonic divergence");
    }

    static void AssertFaceUvResolvesToSite(
        double latitudeDegrees,
        double longitudeDegrees,
        string label
    )
    {
        double latRad = latitudeDegrees * (Math.PI / 180.0);
        double lonRad = longitudeDegrees * (Math.PI / 180.0);
        SimVector3 site = PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRad, lonRad);

        PlanetCubeSphereCoords.LatitudeLongitudeRadiansToFaceUv(
            latRad,
            lonRad,
            out int faceIndex,
            out double u,
            out double v
        );

        SimVector3 chunkDirection = PlanetCubeSphereCoords.FaceUvToUnitDirection(
            faceIndex,
            u,
            v
        );
        double cosine = Math.Min(1.0, Math.Max(-1.0, SimVector3.Dot(site, chunkDirection)));
        double groundErrorMeters = Math.Acos(cosine) * PlanetSurfaceConstants.EarthRadiusMeters;

        Assert.Less(
            groundErrorMeters,
            1.0,
            label + " face UV renders " + groundErrorMeters + " m away from the site"
        );
    }

    static void RecoverCubeFaceUv(
        SimVector3 cubePoint,
        int faceIndex,
        out double u,
        out double v
    )
    {
        SimVector3 normal = PlanetCubeSphereCoords.FaceNormals[faceIndex];
        SimVector3 axisU = PlanetCubeSphereCoords.GetFaceAxisU(faceIndex);
        SimVector3 axisV = PlanetCubeSphereCoords.GetFaceAxisV(faceIndex);
        double cubeU = SimVector3.Dot(cubePoint.Subtract(normal), axisU);
        double cubeV = SimVector3.Dot(cubePoint.Subtract(normal), axisV);
        u = (cubeU * 0.5) + 0.5;
        v = (cubeV * 0.5) + 0.5;
    }

    static bool HasBathymetry(PlanetElevationField field)
    {
        double pacificOceanMeters = field.SampleLatitudeLongitudeRadians(
            0.0,
            -160.0 * (Math.PI / 180.0)
        );
        return pacificOceanMeters < -1.0;
    }

    static void AssertElevationInRange(
        PlanetElevationField field,
        double latitudeDegrees,
        double longitudeDegrees,
        double minMeters,
        double maxMeters,
        string label
    )
    {
        double latRad = latitudeDegrees * (Math.PI / 180.0);
        double lonRad = longitudeDegrees * (Math.PI / 180.0);
        double elevation = PlanetCubeSphereCoords.SampleElevationAtLatitudeLongitude(
            field,
            latRad,
            lonRad
        );

        Assert.GreaterOrEqual(
            elevation,
            minMeters,
            label + " elevation was too low: " + elevation + " m"
        );
        Assert.LessOrEqual(
            elevation,
            maxMeters,
            label + " elevation was too high: " + elevation + " m"
        );
    }

    static PlanetElevationField LoadFieldFromMemory(float[] elevations, int width, int height)
    {
        using var stream = new MemoryStream();
        PlanetElevationBinaryFormat.Write(stream, elevations, width, height);
        stream.Position = 0;
        return PlanetElevationField.LoadFromStream(stream);
    }

    static bool TryLoadGebcoField(out PlanetElevationField field)
    {
        string path = Path.Combine(
            Application.streamingAssetsPath,
            "Planet",
            PlanetSurfaceConstants.ElevationBinaryFileName
        );

        if (!File.Exists(path))
        {
            field = null;
            return false;
        }

        field = PlanetElevationField.LoadFromFile(path);
        return true;
    }
}
