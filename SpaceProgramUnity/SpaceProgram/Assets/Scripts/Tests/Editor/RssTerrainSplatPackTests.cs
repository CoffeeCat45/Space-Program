using NUnit.Framework;

/// <summary>
/// Close-up splat packs are per LOD body (Earth grass/sand, Moon Low/Mid/High), not a second LOD tree.
/// </summary>
public sealed class RssTerrainSplatPackTests
{
    [Test]
    public void EarthPack_FileNames_MatchLegacyRssTerrainConstants()
    {
        RssTerrainSplatPack pack = RssTerrainSplatPack.Earth;

        Assert.AreEqual(RssTerrainPaths.InfluenceFileName, pack.InfluenceFileName);
        Assert.AreEqual(RssTerrainPaths.LowColorFileName, pack.LowColorFileName);
        Assert.AreEqual(RssTerrainPaths.MidColorFileName, pack.MidColorFileName);
        Assert.AreEqual(RssTerrainPaths.HighColorFileName, pack.HighColorFileName);
        Assert.AreEqual(RssTerrainPaths.SteepColorFileName, pack.SteepColorFileName);
        Assert.AreEqual(RssTerrainPaths.EarthBodyFolderName, pack.LegacyPluginDataFolderName);
    }

    [Test]
    public void MoonPack_FileNames_UseMoonPrefixNotEarth()
    {
        RssTerrainSplatPack pack = RssTerrainSplatPack.Moon;

        Assert.AreEqual("Moon_Influence", pack.InfluenceFileName);
        Assert.AreEqual("MoonLow_Color", pack.LowColorFileName);
        Assert.AreEqual("MoonMid_Color", pack.MidColorFileName);
        Assert.AreEqual("MoonHigh_Color", pack.HighColorFileName);
        Assert.AreEqual("MoonSteep_Color", pack.SteepColorFileName);
        Assert.AreEqual("03-01_Moon", pack.LegacyPluginDataFolderName);
        Assert.AreEqual("Moon", pack.ContentBodyFolderName);
        StringAssert.DoesNotStartWith("Earth", pack.MidColorFileName);
    }

    [Test]
    public void MoonPack_ResolvesContentAndLegacyFolders()
    {
        RssTerrainSplatPack pack = RssTerrainSplatPack.Moon;
        string content = pack.ResolveContentAbsoluteDirectory();
        string legacy = pack.ResolveLegacyPluginDataDirectory();

        StringAssert.Contains("RSS-Terrain", content);
        StringAssert.Contains("Moon", content);
        StringAssert.Contains("03-01_Moon", legacy);
        StringAssert.AreEqualIgnoringCase(
            "Assets/Content/Environment/RSS-Terrain/Moon",
            pack.ContentAssetFolder
        );
    }

    [Test]
    public void ForLodBody_DefaultIsEarthPack()
    {
        Assert.IsFalse(CelestialTerrainRuntime.IsMoon);
        Assert.AreEqual(
            RssTerrainSplatPack.Earth.MidColorFileName,
            RssTerrainSplatPack.ForLodBody().MidColorFileName
        );
    }

    [Test]
    public void LodBodyRealRadius_DefaultMatchesEarth()
    {
        Assert.AreEqual(
            PlanetSurfaceConstants.RealEarthRadiusMeters,
            CelestialTerrainRuntime.LodBodyRealRadiusMeters,
            1f
        );
    }

    [Test]
    public void WaveHeight_OffWhenBodyHasNoOcean()
    {
        Assert.AreEqual(
            PlanetBiomeTerrainConfig.WaveHeightRealMeters,
            PlanetBiomeTerrainConfig.WaveHeightMetersForBody(true)
        );
        Assert.AreEqual(0f, PlanetBiomeTerrainConfig.WaveHeightMetersForBody(false));
    }
}
