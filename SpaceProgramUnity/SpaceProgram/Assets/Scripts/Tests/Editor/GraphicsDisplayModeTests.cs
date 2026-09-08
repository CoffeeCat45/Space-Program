using NUnit.Framework;
using UnityEngine;

public sealed class GraphicsDisplayModeTests
{
    [Test]
    public void ClampDisplayMode_UnknownValue_FallsBackToWindowed()
    {
        Assert.AreEqual(
            VabGraphicsManager.DisplayModeWindowed,
            VabGraphicsManager.ClampDisplayMode(-1)
        );
        Assert.AreEqual(
            VabGraphicsManager.DisplayModeWindowed,
            VabGraphicsManager.ClampDisplayMode(99)
        );
    }

    [Test]
    public void ToUnityFullScreenMode_MapsWindowExclusiveAndBorderless()
    {
        Assert.AreEqual(
            FullScreenMode.Windowed,
            VabGraphicsManager.ToUnityFullScreenMode(VabGraphicsManager.DisplayModeWindowed)
        );
        Assert.AreEqual(
            FullScreenMode.ExclusiveFullScreen,
            VabGraphicsManager.ToUnityFullScreenMode(VabGraphicsManager.DisplayModeExclusive)
        );
        Assert.AreEqual(
            FullScreenMode.FullScreenWindow,
            VabGraphicsManager.ToUnityFullScreenMode(VabGraphicsManager.DisplayModeBorderless)
        );
    }

    [Test]
    public void FromUnityFullScreenMode_RoundtripsKnownModes()
    {
        Assert.AreEqual(
            VabGraphicsManager.DisplayModeWindowed,
            VabGraphicsManager.FromUnityFullScreenMode(FullScreenMode.Windowed)
        );
        Assert.AreEqual(
            VabGraphicsManager.DisplayModeExclusive,
            VabGraphicsManager.FromUnityFullScreenMode(FullScreenMode.ExclusiveFullScreen)
        );
        Assert.AreEqual(
            VabGraphicsManager.DisplayModeBorderless,
            VabGraphicsManager.FromUnityFullScreenMode(FullScreenMode.FullScreenWindow)
        );
        Assert.AreEqual(
            VabGraphicsManager.DisplayModeBorderless,
            VabGraphicsManager.FromUnityFullScreenMode(FullScreenMode.MaximizedWindow)
        );
    }

    [Test]
    public void ClampTargetFrameRate_ZeroOrAbove400_MeansUnlimited()
    {
        Assert.AreEqual(0, VabGraphicsManager.ClampTargetFrameRate(0));
        Assert.AreEqual(0, VabGraphicsManager.ClampTargetFrameRate(-30));
        Assert.AreEqual(0, VabGraphicsManager.ClampTargetFrameRate(401));
        Assert.AreEqual(0, VabGraphicsManager.ClampTargetFrameRate(1000));
        Assert.AreEqual(60, VabGraphicsManager.ClampTargetFrameRate(60));
        Assert.AreEqual(144, VabGraphicsManager.ClampTargetFrameRate(144));
        Assert.AreEqual(400, VabGraphicsManager.ClampTargetFrameRate(400));
    }

    [Test]
    public void IsPostAntiAliasingTarget_SkipsOrbitOverlayCamera()
    {
        GameObject overlayObject = new GameObject("Orbit Overlay Camera");
        Camera overlayCamera = overlayObject.AddComponent<Camera>();

        GameObject mainObject = new GameObject("Main Camera");
        Camera mainCamera = mainObject.AddComponent<Camera>();

        try
        {
            Assert.IsFalse(VabGraphicsManager.IsPostAntiAliasingTarget(overlayCamera));
            Assert.IsTrue(VabGraphicsManager.IsPostAntiAliasingTarget(mainCamera));
        }
        finally
        {
            Object.DestroyImmediate(overlayObject);
            Object.DestroyImmediate(mainObject);
        }
    }

    [Test]
    public void ApplySavedCameraQuality_DoesNotThrowWithoutSnapshot()
    {
        Assert.DoesNotThrow(VabGraphicsManager.ApplySavedCameraQuality);
    }
}
