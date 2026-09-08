using NUnit.Framework;
using UnityEngine;

public sealed class OrbitEventBadgeTests
{
    static readonly Color Teal = new Color(0.12f, 0.82f, 0.78f, 1.0f);

    [Test]
    public void Bake_PeIcon_HasOpaqueBodyTransparentCornersAndDownwardTip()
    {
        Texture2D texture = OrbitEventBadgeIcon.Bake("Pe", Teal);

        try
        {
            Assert.Greater(texture.GetPixel(64, 80).a, 0.5f);
            Assert.Less(texture.GetPixel(2, 2).a, 0.1f);
            Assert.Less(texture.GetPixel(125, 125).a, 0.1f);
            Assert.Greater(texture.GetPixel(64, 8).a, 0.5f);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void Bake_ApIcon_HasOpaqueFill()
    {
        Texture2D texture = OrbitEventBadgeIcon.Bake("Ap", Teal);

        try
        {
            Assert.Greater(texture.GetPixel(64, 80).a, 0.5f);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void Initialize_CreatesVisiblePinQuadWithIconTexture()
    {
        GameObject root = new GameObject("Current Ap Marker");

        try
        {
            OrbitEventBadge badge = root.AddComponent<OrbitEventBadge>();
            badge.Initialize(Teal, "Ap");

            Transform pin = root.transform.Find(OrbitEventBadge.PinChildName);
            Assert.IsNotNull(pin);

            MeshRenderer pinRenderer = pin.GetComponent<MeshRenderer>();
            Assert.IsNotNull(pinRenderer);
            Assert.IsTrue(pinRenderer.enabled);
            Assert.IsNotNull(pinRenderer.sharedMaterial);
            Assert.IsNotNull(pinRenderer.sharedMaterial.mainTexture);
            Assert.Greater(pin.localPosition.y, 0.4f);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void FindOn_FindsBadgeWhileRootIsInactive()
    {
        GameObject root = new GameObject("Current Ap Marker");

        try
        {
            root.AddComponent<OrbitEventBadge>().Initialize(Teal, "Ap");
            GameObject label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            TextMesh textMesh = label.AddComponent<TextMesh>();
            root.SetActive(false);

            OrbitEventBadge found = OrbitEventBadge.FindOn(textMesh);
            Assert.IsNotNull(found);
            found.gameObject.SetActive(true);
            Assert.IsTrue(root.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ContainsScreenPoint_HitsIconQuadNotOrbitTipOnly()
    {
        GameObject cameraObject = new GameObject("BadgeCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.SetPositionAndRotation(
            new Vector3(0f, 0f, -10f),
            Quaternion.identity
        );

        GameObject root = new GameObject("Current Ap Marker");

        try
        {
            OrbitEventBadge badge = root.AddComponent<OrbitEventBadge>();
            badge.Initialize(Teal, "Ap");
            root.transform.position = Vector3.zero;
            root.transform.localScale = Vector3.one;
            root.SetActive(true);

            Vector3 iconScreen = camera.WorldToScreenPoint(new Vector3(0f, 0.5f, 0f));
            Vector2 iconMouse = new Vector2(iconScreen.x, iconScreen.y);
            Assert.IsTrue(
                OrbitEventBadge.ContainsScreenPoint(
                    camera,
                    root.transform,
                    iconMouse,
                    out _
                )
            );

            Vector3 farScreen = camera.WorldToScreenPoint(new Vector3(20f, 0f, 0f));
            Assert.IsFalse(
                OrbitEventBadge.ContainsScreenPoint(
                    camera,
                    root.transform,
                    new Vector2(farScreen.x, farScreen.y),
                    out _
                )
            );
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
