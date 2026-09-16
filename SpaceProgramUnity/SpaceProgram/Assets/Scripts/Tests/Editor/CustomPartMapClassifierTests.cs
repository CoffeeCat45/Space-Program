using NUnit.Framework;

public sealed class CustomPartMapClassifierTests
{
    [Test]
    public void Classify_AlbedoNames()
    {
        Assert.AreEqual(
            CustomPartTextureRole.Albedo,
            CustomPartImportPipeline.ClassifyTextureFileName("first-engine", "first-engine")
        );
        Assert.AreEqual(
            CustomPartTextureRole.Albedo,
            CustomPartImportPipeline.ClassifyTextureFileName("first-engine_albedo", "first-engine")
        );
        Assert.AreEqual(
            CustomPartTextureRole.Albedo,
            CustomPartImportPipeline.ClassifyTextureFileName("texture", "pod")
        );
    }

    [Test]
    public void Classify_MetallicAndReflection()
    {
        Assert.AreEqual(
            CustomPartTextureRole.Metallic,
            CustomPartImportPipeline.ClassifyTextureFileName("first-engine_metallic", "first-engine")
        );
        Assert.AreEqual(
            CustomPartTextureRole.Metallic,
            CustomPartImportPipeline.ClassifyTextureFileName("metals", "first-engine")
        );
        Assert.AreEqual(
            CustomPartTextureRole.Reflection,
            CustomPartImportPipeline.ClassifyTextureFileName("first-engine_roughness", "first-engine")
        );
        Assert.AreEqual(
            CustomPartTextureRole.Reflection,
            CustomPartImportPipeline.ClassifyTextureFileName("reflection", "first-engine")
        );
    }

    [Test]
    public void Classify_SkipsMaskAndNormal()
    {
        Assert.AreEqual(
            CustomPartTextureRole.Unknown,
            CustomPartImportPipeline.ClassifyTextureFileName("first-engine_Mask", "first-engine")
        );
        Assert.AreEqual(
            CustomPartTextureRole.Unknown,
            CustomPartImportPipeline.ClassifyTextureFileName("first-engine-n", "first-engine")
        );
    }

    [Test]
    public void SanitizePartId_LowerDash()
    {
        Assert.AreEqual("first-engine", CustomPartImportPipeline.SanitizePartId("First Engine"));
        Assert.AreEqual("mk1-pod", CustomPartImportPipeline.SanitizePartId("mk1_pod"));
        Assert.AreEqual("", CustomPartImportPipeline.SanitizePartId("   "));
    }

    [Test]
    public void BuildMaterialAssetName_JoinsUntilTooLong()
    {
        Assert.AreEqual(
            "first-engine",
            CustomPartImportPipeline.BuildMaterialAssetName(new[] { "first-engine" }, "family")
        );
        Assert.AreEqual(
            "first-engine+booster",
            CustomPartImportPipeline.BuildMaterialAssetName(new[] { "first-engine", "booster" }, "family")
        );

        string[] many =
        {
            "very-long-engine-name",
            "very-long-tank-name",
            "very-long-pod-name",
        };
        Assert.AreEqual(
            "family-atlas",
            CustomPartImportPipeline.BuildMaterialAssetName(many, "family-atlas")
        );
    }

    [Test]
    public void AtlasStem_StripsAlbedoSuffix()
    {
        Assert.AreEqual(
            "mk1-family",
            CustomPartImportPipeline.AtlasStemFromAlbedoFileName("Mk1 Family_albedo")
        );
        Assert.AreEqual(
            "first-engine",
            CustomPartImportPipeline.AtlasStemFromAlbedoFileName("first-engine")
        );
    }

    [Test]
    public void FormatPartList_UsesCommaForDisplay()
    {
        Assert.AreEqual(
            "first-engine, booster",
            CustomPartImportPipeline.FormatPartList(new[] { "first-engine", "booster" })
        );
    }
}
