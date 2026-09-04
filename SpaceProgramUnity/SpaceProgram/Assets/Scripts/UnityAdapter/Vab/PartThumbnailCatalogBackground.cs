using UnityEngine;

/// <summary>
/// Shared two-tone gray backdrop for part thumbnails in the VAB UI.
/// </summary>
public static class PartThumbnailCatalogBackground
{
    public static readonly Color WallTopColor = new Color(0.79f, 0.81f, 0.83f, 1f);
    public static readonly Color WallBottomColor = new Color(0.71f, 0.73f, 0.75f, 1f);
    public static readonly Color FloorColor = new Color(0.56f, 0.58f, 0.60f, 1f);
    public static readonly Color FloorShadowColor = new Color(0.49f, 0.51f, 0.53f, 1f);
    public const float HorizonNormalized = 0.36f;

    const int CachedSpriteSize = 128;

    static Texture2D cachedTexture;
    static Sprite cachedSprite;

    public static Sprite GetSprite()
    {
        if (cachedSprite != null)
        {
            return cachedSprite;
        }

        cachedTexture = BuildBackgroundTexture(CachedSpriteSize, CachedSpriteSize);
        cachedSprite = Sprite.Create(
            cachedTexture,
            new Rect(0f, 0f, CachedSpriteSize, CachedSpriteSize),
            new Vector2(0.5f, 0.5f),
            100f
        );
        return cachedSprite;
    }

    public static Texture2D MakeReadableCopy(Texture source)
    {
        if (source == null)
        {
            return null;
        }

        if (source is Texture2D texture2D && texture2D.isReadable)
        {
            return texture2D;
        }

        RenderTexture temporary = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB
        );

        Graphics.Blit(source, temporary);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;

        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        copy.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return copy;
    }

    public static Color Sample(float normalizedY)
    {
        const float horizonBand = 0.018f;

        if (normalizedY <= HorizonNormalized - horizonBand)
        {
            float floorT = HorizonNormalized <= 0f ?
                1f :
                normalizedY / Mathf.Max(HorizonNormalized - horizonBand, 0.001f);
            return Color.Lerp(FloorShadowColor, FloorColor, floorT * 0.55f + 0.45f);
        }

        if (normalizedY <= HorizonNormalized + horizonBand)
        {
            float lineT = (normalizedY - (HorizonNormalized - horizonBand)) /
                Mathf.Max(horizonBand * 2f, 0.001f);
            return Color.Lerp(FloorColor, WallBottomColor, lineT);
        }

        float wallT = (normalizedY - (HorizonNormalized + horizonBand)) /
            Mathf.Max(1f - (HorizonNormalized + horizonBand), 0.001f);
        return Color.Lerp(WallBottomColor, WallTopColor, wallT);
    }

    public static void ClearCache()
    {
        if (cachedSprite != null)
        {
            Object.Destroy(cachedSprite);
            cachedSprite = null;
        }

        if (cachedTexture != null)
        {
            Object.Destroy(cachedTexture);
            cachedTexture = null;
        }
    }

    static Texture2D BuildBackgroundTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float normalizedY = height <= 1 ? 0f : (float)y / (height - 1);
            Color color = Sample(normalizedY);

            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }
}
