using NUnit.Framework;

public sealed class VabSessionSceneUtilityTests
{
    [Test]
    public void HostScene_IsVabScene_NotHangarFileName()
    {
        Assert.IsTrue(VabSessionSceneUtility.IsVabHostSceneName("VabScene"));
        Assert.IsTrue(VabSessionSceneUtility.IsVabHostSceneName("vabscene"));
        Assert.IsFalse(VabSessionSceneUtility.IsVabHostSceneName("VABlvl3"));
    }

    [Test]
    public void HangarScene_MatchesVABlvl3()
    {
        Assert.IsTrue(VabSessionSceneUtility.IsVabHangarSceneName("VABlvl3"));
        Assert.IsTrue(VabSessionSceneUtility.IsVabHangarSceneName("vablvl3"));
        Assert.IsTrue(VabSessionSceneUtility.IsVabSceneName("VABlvl3"));
    }
}
