using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds catalog thumbnails from two UI layers: shared KSP background + part foreground PNG.
/// </summary>
public static class PartThumbnailUi
{
    public static void CreateGridThumbnail(
        RectTransform cellRect,
        PartTemplate template,
        System.Func<RectTransform, string, string, int, FontStyle, TextAnchor, Vector2, Vector2, Text> createText,
        int fallbackFontSize,
        System.Func<string, string> categoryLetter,
        System.Func<string, Color> categoryColor
    )
    {
        RectTransform clipRect = PartThumbnailRoundedMask.CreateClipContainer(cellRect);
        CreateBackgroundLayer(clipRect);

        Sprite partSprite = PartThumbnailLoader.TryLoadSprite(template.iconPath);

        if (partSprite != null)
        {
            CreateForegroundLayer(clipRect, partSprite);
            return;
        }

        RectTransform fallbackRect = CreateForegroundLayer(clipRect, null);
        Text letter = createText(
            fallbackRect,
            "Letter",
            categoryLetter(template.category),
            fallbackFontSize,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero
        );
        letter.color = categoryColor(template.category);
        letter.raycastTarget = false;
    }

    public static void CreateBackgroundLayer(RectTransform parent)
    {
        GameObject backgroundObject = new GameObject("IconBackground");
        backgroundObject.transform.SetParent(parent, false);

        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.sprite = PartThumbnailCatalogBackground.GetSprite();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = Color.white;
        backgroundImage.raycastTarget = false;
    }

    public static RectTransform CreateForegroundLayer(RectTransform parent, Sprite partSprite)
    {
        GameObject foregroundObject = new GameObject("IconForeground");
        foregroundObject.transform.SetParent(parent, false);

        RectTransform foregroundRect = foregroundObject.AddComponent<RectTransform>();
        foregroundRect.anchorMin = Vector2.zero;
        foregroundRect.anchorMax = Vector2.one;
        foregroundRect.offsetMin = new Vector2(2f, 2f);
        foregroundRect.offsetMax = new Vector2(-2f, -2f);

        if (partSprite == null)
        {
            return foregroundRect;
        }

        Image foregroundImage = foregroundObject.AddComponent<Image>();
        foregroundImage.sprite = partSprite;
        foregroundImage.color = Color.white;
        foregroundImage.preserveAspect = true;
        foregroundImage.type = Image.Type.Simple;
        foregroundImage.raycastTarget = false;
        return foregroundRect;
    }
}
