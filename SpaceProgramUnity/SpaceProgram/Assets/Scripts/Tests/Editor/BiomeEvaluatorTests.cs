using System;
using NUnit.Framework;

public sealed class BiomeEvaluatorTests
{
  const double FlatSlopeDotUp = 1.0;
  const double SteepSlopeDotUp = 0.5;

  [Test]
  public void Evaluate_BelowSeaLevel_ReturnsOcean()
  {
    BiomeProperties biome = BiomeEvaluator.Evaluate(
      0.0,
      0.0,
      -120.0,
      FlatSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Ocean, biome.BiomeType);
    Assert.AreEqual(0.15, biome.StaticFrictionCoefficient, 1e-9);
  }

  [Test]
  public void GetPhysicalSurfaceElevation_OceanFloor_IsSeaLevel()
  {
    Assert.AreEqual(
      PlanetSurfaceConstants.SeaLevelMeters,
      BiomeEvaluator.GetPhysicalSurfaceElevationMeters(-4200.0),
      1e-9
    );
  }

  [Test]
  public void GetPhysicalSurfaceElevation_Land_KeepsGebco()
  {
    Assert.AreEqual(
      468.0,
      BiomeEvaluator.GetPhysicalSurfaceElevationMeters(468.0),
      1e-9
    );
  }

  [Test]
  public void Evaluate_HighLatitude_ReturnsIce()
  {
    double latitudeRadians = 75.0 * (Math.PI / 180.0);

    BiomeProperties biome = BiomeEvaluator.Evaluate(
      latitudeRadians,
      0.0,
      50.0,
      FlatSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Ice, biome.BiomeType);
    Assert.AreEqual(0.05, biome.StaticFrictionCoefficient, 1e-9);
  }

  [Test]
  public void Evaluate_SteepSlope_ReturnsRock()
  {
    BiomeProperties biome = BiomeEvaluator.Evaluate(
      0.4,
      0.0,
      500.0,
      SteepSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Rock, biome.BiomeType);
    Assert.AreEqual(0.85, biome.StaticFrictionCoefficient, 1e-9);
  }

  [Test]
  public void Evaluate_LowTropicalFlat_ReturnsSand()
  {
    double latitudeRadians = 20.0 * (Math.PI / 180.0);

    BiomeProperties biome = BiomeEvaluator.Evaluate(
      latitudeRadians,
      0.0,
      25.0,
      FlatSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Sand, biome.BiomeType);
    Assert.AreEqual(0.55, biome.StaticFrictionCoefficient, 1e-9);
  }

  [Test]
  public void Evaluate_CapeCanaveralFlat_ReturnsSand()
  {
    double latitudeRadians = 28.57 * (Math.PI / 180.0);
    double longitudeRadians = -80.65 * (Math.PI / 180.0);

    BiomeProperties biome = BiomeEvaluator.Evaluate(
      latitudeRadians,
      longitudeRadians,
      5.0,
      FlatSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Sand, biome.BiomeType);
  }

  [Test]
  public void Evaluate_HighMountain_ReturnsSnow()
  {
    BiomeProperties biome = BiomeEvaluator.Evaluate(
      0.7,
      0.0,
      4000.0,
      FlatSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Snow, biome.BiomeType);
    Assert.AreEqual(0.35, biome.StaticFrictionCoefficient, 1e-9);
  }

  [Test]
  public void Evaluate_TemperateLand_ReturnsGrass()
  {
    double latitudeRadians = 45.0 * (Math.PI / 180.0);

    BiomeProperties biome = BiomeEvaluator.Evaluate(
      latitudeRadians,
      0.0,
      250.0,
      FlatSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Grass, biome.BiomeType);
    Assert.AreEqual(0.65, biome.StaticFrictionCoefficient, 1e-9);
  }

  [Test]
  public void Evaluate_Priority_IceBeatsRock()
  {
    double latitudeRadians = 80.0 * (Math.PI / 180.0);

    BiomeProperties biome = BiomeEvaluator.Evaluate(
      latitudeRadians,
      0.0,
      200.0,
      SteepSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Ice, biome.BiomeType);
  }

  [Test]
  public void Evaluate_Priority_RockBeatsSand()
  {
    double latitudeRadians = 10.0 * (Math.PI / 180.0);

    BiomeProperties biome = BiomeEvaluator.Evaluate(
      latitudeRadians,
      0.0,
      20.0,
      SteepSlopeDotUp
    );

    Assert.AreEqual(BiomeType.Rock, biome.BiomeType);
  }

  [Test]
  public void ComputeSurfaceSlopeDotUp_FlatSurface_ReturnsOne()
  {
    SimVector3 up = SimVector3.UnitY();
    double dot = BiomeEvaluator.ComputeSurfaceSlopeDotUp(up, up);

    Assert.AreEqual(1.0, dot, 1e-9);
  }

  [Test]
  public void Evaluate_WithSurfaceNormal_MatchesSlopeOverload()
  {
    SimVector3 up = PlanetGeographicCoords.DirectionFromLatitudeLongitude(0.5, 1.0);
    SimVector3 tiltedNormal = up
      .Multiply(0.6)
      .Add(PlanetGeographicCoords.DirectionFromLatitudeLongitude(0.5, 2.0).Multiply(0.8))
      .Normalize();

    BiomeProperties fromNormal = BiomeEvaluator.Evaluate(
      0.5,
      1.0,
      500.0,
      tiltedNormal,
      up
    );
    BiomeProperties fromDot = BiomeEvaluator.Evaluate(
      0.5,
      1.0,
      500.0,
      BiomeEvaluator.ComputeSurfaceSlopeDotUp(tiltedNormal, up)
    );

    Assert.AreEqual(fromDot.BiomeType, fromNormal.BiomeType);
    Assert.AreEqual(fromDot.StaticFrictionCoefficient, fromNormal.StaticFrictionCoefficient, 1e-9);
  }
}
