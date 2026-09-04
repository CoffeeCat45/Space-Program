using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared 9-sliced rounded-rect sprite for VAB modal panels.
/// </summary>
public static class VabRoundedPanelUi
{
    public const float CornerRadius = 10f;

    const int SpriteSize = 128;
    const float PixelsPerUnit = 100f;

    static Sprite cachedSprite;

    public static Sprite GetSprite()
    {
        if (cachedSprite != null)
        {
            return cachedSprite;
        }

        Texture2D texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[SpriteSize * SpriteSize];
        Vector2 halfSize = new Vector2(SpriteSize * 0.5f, SpriteSize * 0.5f);

        for (int y = 0; y < SpriteSize; y++)
        {
            for (int x = 0; x < SpriteSize; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f) - halfSize;
                bool inside = IsInsideRoundedRect(point, halfSize, CornerRadius);
                pixels[y * SpriteSize + x] = inside ?
                    Color.white :
                    new Color(1f, 1f, 1f, 0f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector4 border = new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius);
        cachedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, SpriteSize, SpriteSize),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit,
            0u,
            SpriteMeshType.FullRect,
            border
        );
        return cachedSprite;
    }

    public static void ApplyRoundedBackground(Image image, Color color)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = GetSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    static bool IsInsideRoundedRect(Vector2 point, Vector2 halfSize, float radius)
    {
        Vector2 inset = halfSize - new Vector2(radius, radius);
        Vector2 offset = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - inset;
        float outside = new Vector2(Mathf.Max(offset.x, 0f), Mathf.Max(offset.y, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(offset.x, offset.y), 0f);
        return outside + inside <= radius;
    }
}
