using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Loads baked part foreground PNGs for the VAB catalog UI.
/// The KSP background is drawn separately by <see cref="PartThumbnailUi"/>.
/// </summary>
public static class PartThumbnailLoader
{
    static readonly Dictionary<string, Sprite> spriteCache =
        new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

    public static Sprite TryLoadSprite(string iconAssetPath)
    {
        if (string.IsNullOrWhiteSpace(iconAssetPath))
        {
            return null;
        }

        if (spriteCache.TryGetValue(iconAssetPath, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Texture2D texture = null;

#if UNITY_EDITOR
        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconAssetPath);
#endif

        if (texture == null)
        {
            RuntimePartCatalog catalog = RuntimePartCatalog.Load();
            texture = catalog != null ? catalog.GetIconByAssetPath(iconAssetPath) : null;
        }

        if (texture == null)
        {
            return null;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        spriteCache[iconAssetPath] = sprite;
        return sprite;
    }

    public static void ClearCache()
    {
        foreach (KeyValuePair<string, Sprite> entry in spriteCache)
        {
            if (entry.Value != null)
            {
                Object.Destroy(entry.Value);
            }
        }

        spriteCache.Clear();
        PartThumbnailCatalogBackground.ClearCache();
    }
}
