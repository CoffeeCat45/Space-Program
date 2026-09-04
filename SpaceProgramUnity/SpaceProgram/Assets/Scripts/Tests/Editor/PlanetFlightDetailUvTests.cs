using NUnit.Framework;
using UnityEngine;

public sealed class PlanetFlightDetailUvTests
{
    const float MoonRadiusUnity = 173700f;
    const double UvNeighborToleranceMeters = 50.0;

    [Test]
    public void WrapCentered_Zero_StaysAtOriginNotSeam()
    {
        Assert.AreEqual(0.0, PlanetFlightDetailUv.WrapCentered(0.0, PlanetFlightDetailUv.WrapMeters), 1e-9);
        Assert.AreEqual(
            1.0,
            PlanetFlightDetailUv.WrapCentered(1.0, PlanetFlightDetailUv.WrapMeters),
            1e-9
        );
        Assert.AreEqual(
            -1.0,
            PlanetFlightDetailUv.WrapCentered(-1.0, PlanetFlightDetailUv.WrapMeters),
            1e-9
        );
    }

    [Test]
    public void MoonPadOrigin_EastAndWestNeighbors_DoNotJumpAFullWrap()
    {
        SimVector3 pad = PlanetGeographicCoords.DirectionFromLatitudeLongitude(0.0, 0.0);
        SimVector3 east = PlanetGeographicCoords.DirectionFromLatitudeLongitude(0.0, 0.0002);
        SimVector3 west = PlanetGeographicCoords.DirectionFromLatitudeLongitude(0.0, -0.0002);

        Vector2 padUv = PlanetFlightDetailUv.ComputeWrappedDetailMetersUv(pad, MoonRadiusUnity);
        Vector2 eastUv = PlanetFlightDetailUv.ComputeWrappedDetailMetersUv(east, MoonRadiusUnity);
        Vector2 westUv = PlanetFlightDetailUv.ComputeWrappedDetailMetersUv(west, MoonRadiusUnity);

        Assert.AreEqual(0f, padUv.x, 0.5f);
        Assert.AreEqual(0f, padUv.y, 0.5f);
        Assert.Less(Mathf.Abs(eastUv.x - westUv.x), PlanetFlightDetailUv.WrapMeters * 0.5f);
        Assert.Less(Mathf.Abs(eastUv.x - padUv.x), UvNeighborToleranceMeters);
        Assert.Less(Mathf.Abs(westUv.x - padUv.x), UvNeighborToleranceMeters);
    }

    [Test]
    public void MoonPadOrigin_NorthAndSouthNeighbors_DoNotJumpAFullWrap()
    {
        SimVector3 pad = PlanetGeographicCoords.DirectionFromLatitudeLongitude(0.0, 0.0);
        SimVector3 north = PlanetGeographicCoords.DirectionFromLatitudeLongitude(0.0002, 0.0);
        SimVector3 south = PlanetGeographicCoords.DirectionFromLatitudeLongitude(-0.0002, 0.0);

        Vector2 padUv = PlanetFlightDetailUv.ComputeWrappedDetailMetersUv(pad, MoonRadiusUnity);
        Vector2 northUv = PlanetFlightDetailUv.ComputeWrappedDetailMetersUv(north, MoonRadiusUnity);
        Vector2 southUv = PlanetFlightDetailUv.ComputeWrappedDetailMetersUv(south, MoonRadiusUnity);

        Assert.Less(Mathf.Abs(northUv.y - southUv.y), PlanetFlightDetailUv.WrapMeters * 0.5f);
        Assert.Less(Mathf.Abs(northUv.y - padUv.y), UvNeighborToleranceMeters);
        Assert.Less(Mathf.Abs(southUv.y - padUv.y), UvNeighborToleranceMeters);
    }
}
