using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// KSP-style part detail panel: appears on catalog hover and stays open while the
/// pointer is over the grid cell or this panel.
/// </summary>
public sealed class VabPartCatalogTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    const float PanelWidth = 380f;
    const float PanelHeight = 248f;
    const float HideDelaySeconds = 0.07f;
    const int TitleFontSize = 19;
    const int BodyFontSize = 15;
    const int SmallFontSize = 13;
    const float PreviewSize = 120f;

    RectTransform canvasRect;
    RectTransform panelRect;
    Text titleText;
    Text categoryText;
    Text detailsText;
    Text footerText;
    Image previewImage;
    Image previewBackground;
    Text previewFallbackLetter;

    float sidebarWidth;
    bool pointerOverPanel;
    bool pointerOverCell;
    float hideTimer;
    PartTemplate currentTemplate;

    public static VabPartCatalogTooltip Create(RectTransform parentCanvas, float sidebarWidthPixels)
    {
        GameObject panelObject = new GameObject("VabPartCatalogTooltip");
        panelObject.transform.SetParent(parentCanvas, false);

        VabPartCatalogTooltip tooltip = panelObject.AddComponent<VabPartCatalogTooltip>();
        tooltip.canvasRect = parentCanvas;
        tooltip.sidebarWidth = sidebarWidthPixels;
        tooltip.panelRect = panelObject.AddComponent<RectTransform>();
        tooltip.BuildPanel();
        return tooltip;
    }

    void BuildPanel()
    {
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image background = panelRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.1f, 0.14f, 0.96f);
        background.raycastTarget = true;

        Outline outline = panelRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.55f, 0.72f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup rootLayout = panelRect.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(12, 12, 12, 12);
        rootLayout.spacing = 8f;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        GameObject titleObject = CreateLayoutChild(panelRect, "Title", 44f);
        titleText = CreateText(
            titleObject.GetComponent<RectTransform>(),
            string.Empty,
            TitleFontSize,
            FontStyle.Bold,
            TextAnchor.UpperLeft,
            new Color(0.98f, 0.86f, 0.34f, 1f)
        );
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject bodyObject = CreateLayoutChild(panelRect, "Body", 132f);
        HorizontalLayoutGroup bodyLayout = bodyObject.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 10f;
        bodyLayout.childAlignment = TextAnchor.UpperLeft;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = false;
        bodyLayout.childForceExpandHeight = true;

        GameObject previewObject = CreateLayoutChild(bodyObject.transform, "Preview", PreviewSize, PreviewSize);
        RectTransform previewClip = PartThumbnailRoundedMask.CreateClipContainer(
            previewObject.GetComponent<RectTransform>()
        );

        GameObject previewBackgroundObject = new GameObject("PreviewBackground");
        previewBackgroundObject.transform.SetParent(previewClip, false);
        RectTransform previewBackgroundRect = previewBackgroundObject.AddComponent<RectTransform>();
        previewBackgroundRect.anchorMin = Vector2.zero;
        previewBackgroundRect.anchorMax = Vector2.one;
        previewBackgroundRect.offsetMin = Vector2.zero;
        previewBackgroundRect.offsetMax = Vector2.zero;
        previewBackground = previewBackgroundObject.AddComponent<Image>();
        previewBackground.sprite = PartThumbnailCatalogBackground.GetSprite();
        previewBackground.type = Image.Type.Simple;
        previewBackground.raycastTarget = false;

        RectTransform previewImageRect = CreateInsetRect(previewClip, "PreviewImage", 4f);
        previewImage = previewImageRect.gameObject.AddComponent<Image>();
        previewImage.color = Color.white;
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;

        previewFallbackLetter = CreateText(
            previewImageRect,
            "?",
            36,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white
        );
        previewFallbackLetter.raycastTarget = false;

        GameObject detailsObject = CreateFlexChild(bodyObject.transform, "Details");
        VerticalLayoutGroup detailsLayout = detailsObject.AddComponent<VerticalLayoutGroup>();
        detailsLayout.spacing = 6f;
        detailsLayout.childAlignment = TextAnchor.UpperLeft;
        detailsLayout.childControlWidth = true;
        detailsLayout.childControlHeight = true;
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;

        GameObject categoryObject = CreateLayoutChild(detailsObject.transform, "Category", 24f);
        categoryText = CreateText(
            categoryObject.GetComponent<RectTransform>(),
            string.Empty,
            BodyFontSize,
            FontStyle.Bold,
            TextAnchor.UpperLeft,
            new Color(0.45f, 0.82f, 0.55f, 1f)
        );

        GameObject detailsBodyObject = CreateFlexChild(detailsObject.transform, "DetailsBody");
        detailsText = CreateText(
            detailsBodyObject.GetComponent<RectTransform>(),
            string.Empty,
            BodyFontSize,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Color(0.9f, 0.93f, 0.97f, 1f)
        );
        detailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailsText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject footerObject = CreateLayoutChild(panelRect, "Footer", 22f);
        footerText = CreateText(
            footerObject.GetComponent<RectTransform>(),
            "ЛКМ — установить деталь",
            SmallFontSize,
            FontStyle.Italic,
            TextAnchor.LowerLeft,
            new Color(0.62f, 0.68f, 0.76f, 1f)
        );

        panelRect.gameObject.SetActive(false);
    }

    public void NotifyCellEnter(PartTemplate template, RectTransform cellRect)
    {
        if (template == null || cellRect == null)
        {
            return;
        }

        pointerOverCell = true;
        hideTimer = 0f;
        currentTemplate = template;
        Populate(template);
        panelRect.gameObject.SetActive(true);
        PositionNearCell(cellRect);
        transform.SetAsLastSibling();
    }

    public void NotifyCellExit(PartTemplate template)
    {
        if (currentTemplate != null && template != null && currentTemplate.id != template.id)
        {
            return;
        }

        pointerOverCell = false;
    }

    void LateUpdate()
    {
        if (!panelRect.gameObject.activeSelf)
        {
            return;
        }

        if (pointerOverCell || pointerOverPanel)
        {
            hideTimer = 0f;
            return;
        }

        hideTimer += Time.unscaledDeltaTime;

        if (hideTimer >= HideDelaySeconds)
        {
            Hide();
        }
    }

    void Populate(PartTemplate template)
    {
        titleText.text = template.displayName;
        categoryText.text = GetCategoryLabel(template.category);
        detailsText.text = BuildDetails(template);
        footerText.text = "ЛКМ — установить деталь";

        Sprite iconSprite = PartThumbnailLoader.TryLoadSprite(template.iconPath);
        previewImage.sprite = iconSprite;
        previewImage.enabled = iconSprite != null;
        previewBackground.enabled = true;
        previewFallbackLetter.text = GetCategoryLetter(template.category);
        previewFallbackLetter.enabled = iconSprite == null;
    }

    void PositionNearCell(RectTransform cellRect)
    {
        Canvas canvas = canvasRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ?
            canvas.worldCamera :
            null;

        Vector3[] corners = new Vector3[4];
        cellRect.GetWorldCorners(corners);
        float cellCenterY = (corners[0].y + corners[1].y) * 0.5f;
        Vector2 screenPoint = new Vector2(corners[2].x, cellCenterY);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            uiCamera,
            out Vector2 localPoint
        );

        float panelHalfHeight = panelRect.sizeDelta.y * 0.5f;
        float canvasHalfHeight = canvasRect.rect.height * 0.5f;
        float clampedY = Mathf.Clamp(
            localPoint.y,
            -canvasHalfHeight + panelHalfHeight + 12f,
            canvasHalfHeight - panelHalfHeight - 12f
        );

        panelRect.anchoredPosition = new Vector2(sidebarWidth + 10f, clampedY);
    }

    void Hide()
    {
        pointerOverCell = false;
        pointerOverPanel = false;
        hideTimer = 0f;
        currentTemplate = null;
        panelRect.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerOverPanel = true;
        hideTimer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOverPanel = false;
    }

    static string BuildDetails(PartTemplate template)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.Append("Масса: ");
        builder.Append(template.dryMass.ToString("0.##"));
        builder.Append(" т");

        if (template.fuelCapacity > 0f)
        {
            builder.Append('\n');
            builder.Append("Топливо: ");
            builder.Append(template.fuelCapacity.ToString("0"));
            builder.Append(" л");
        }

        if (template.maxThrustVac > 0f)
        {
            builder.Append('\n');
            builder.Append("Тяга (вак.): ");
            builder.Append(template.maxThrustVac.ToString("0.##"));
            builder.Append(" кН");
        }

        if (template.ispVacuum > 0f)
        {
            builder.Append('\n');
            builder.Append("Isp (вак.): ");
            builder.Append(template.ispVacuum.ToString("0"));
            builder.Append(" с");
        }

        if (template.nodes != null && template.nodes.Count > 0)
        {
            builder.Append('\n');
            builder.Append("Узлы крепления: ");
            builder.Append(template.nodes.Count);
        }

        builder.Append('\n');
        builder.Append("ID: ");
        builder.Append(template.id);

        return builder.ToString();
    }

    static string GetCategoryLabel(string category)
    {
        switch (category)
        {
            case PartDatabase.CategoryCommand:
                return "Категория: Капсулы";
            case PartDatabase.CategoryFuelTank:
                return "Категория: Баки";
            case PartDatabase.CategoryEngine:
                return "Категория: Двигатели";
            default:
                return "Категория: " + category;
        }
    }

    static string GetCategoryLetter(string category)
    {
        switch (category)
        {
            case PartDatabase.CategoryCommand:
                return "К";
            case PartDatabase.CategoryFuelTank:
                return "Б";
            case PartDatabase.CategoryEngine:
                return "Д";
            default:
                return "?";
        }
    }

    static GameObject CreateLayoutChild(Transform parent, string name, float height)
    {
        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);

        childObject.AddComponent<RectTransform>();
        LayoutElement layout = childObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleWidth = 1f;
        return childObject;
    }

    static GameObject CreateLayoutChild(Transform parent, string name, float width, float height)
    {
        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);

        childObject.AddComponent<RectTransform>();
        LayoutElement layout = childObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleWidth = 0f;
        return childObject;
    }

    static GameObject CreateFlexChild(Transform parent, string name)
    {
        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);

        childObject.AddComponent<RectTransform>();
        LayoutElement layout = childObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 1f;
        layout.minHeight = 0f;
        return childObject;
    }

    static RectTransform CreateInsetRect(Transform parent, string name, float inset)
    {
        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);

        RectTransform rect = childObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    static Text CreateText(
        RectTransform parent,
        string text,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color
    )
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text label = textObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.text = text;
        label.supportRichText = false;
        return label;
    }
}
