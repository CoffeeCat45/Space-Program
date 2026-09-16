using NUnit.Framework;
using UnityEngine;

public sealed class PartEnvironmentReflectionsTests
{
    [Test]
    public void ComputeSpaceBlend_PadIsGround_OrbitIsSpace()
    {
        Assert.AreEqual(0f, PartEnvironmentReflections.ComputeSpaceBlend(0.0, false), 1e-4f);
        Assert.Greater(PartEnvironmentReflections.ComputeSpaceBlend(200000.0, false), 0.99f);
        Assert.AreEqual(1f, PartEnvironmentReflections.ComputeSpaceBlend(0.0, true), 1e-4f);
    }

    [Test]
    public void GroundCubemap_ZenithIsBluerThanNadir()
    {
        Color zenith = PartEnvironmentReflections.EvaluateGroundColor(Vector3.up);
        Color nadir = PartEnvironmentReflections.EvaluateGroundColor(Vector3.down);
        Assert.Greater(zenith.b, zenith.r);
        Assert.Greater(zenith.b, nadir.b);
        Assert.Greater(nadir.r, nadir.b);
    }

    [Test]
    public void SpaceCubemap_IsDarkerThanDayZenith()
    {
        Color day = PartEnvironmentReflections.EvaluateGroundColor(Vector3.up);
        float dayLuma = day.r + day.g + day.b;
        float darkestSpace = 99f;
        Vector3[] dirs =
        {
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right,
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            Color space = PartEnvironmentReflections.EvaluateSpaceColor(dirs[i]);
            darkestSpace = Mathf.Min(darkestSpace, space.r + space.g + space.b);
        }

        Assert.Less(darkestSpace, 0.12f);
        Assert.Greater(dayLuma, darkestSpace * 4f);
    }
}
