using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cached rounded-rect mask sprite for catalog part thumbnails.
/// </summary>
public static class PartThumbnailRoundedMask
{
    const int SpriteSize = 64;
    const float CornerRadius = 6f;

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
                float alpha = IsInsideRoundedRect(point, halfSize, CornerRadius) ? 1f : 0f;
                pixels[y * SpriteSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        cachedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, SpriteSize, SpriteSize),
            new Vector2(0.5f, 0.5f),
            100f
        );
        return cachedSprite;
    }

    public static RectTransform CreateClipContainer(RectTransform parent)
    {
        GameObject clipObject = new GameObject("IconClip");
        clipObject.transform.SetParent(parent, false);

        RectTransform clipRect = clipObject.AddComponent<RectTransform>();
        clipRect.anchorMin = Vector2.zero;
        clipRect.anchorMax = Vector2.one;
        clipRect.offsetMin = Vector2.zero;
        clipRect.offsetMax = Vector2.zero;

        Image maskImage = clipObject.AddComponent<Image>();
        maskImage.sprite = GetSprite();
        maskImage.type = Image.Type.Simple;
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;

        Mask mask = clipObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        return clipRect;
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
