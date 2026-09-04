using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Modal list of saved vessel blueprints with thumbnails (KSP background + rendered PNG).
/// </summary>
public class VabShipBrowserUi : MonoBehaviour
{
    const float PanelWidth = 720f;
    const float PanelHeight = 560f;
    const float RowHeight = 112f;
    const float ThumbnailSize = 96f;

    static readonly Color PanelBackground = new Color(0.10f, 0.12f, 0.16f, 0.98f);
    static readonly Color RowBackground = new Color(0.14f, 0.17f, 0.22f, 1f);
    static readonly Color RowHover = new Color(0.20f, 0.26f, 0.34f, 1f);
    static readonly Color LabelColor = new Color(0.95f, 0.97f, 1f, 1f);
    static readonly Color MutedColor = new Color(0.72f, 0.78f, 0.86f, 1f);
    static readonly Color PrimaryButton = new Color(0.20f, 0.42f, 0.62f, 1f);
    static readonly Color DangerButton = new Color(0.55f, 0.22f, 0.22f, 1f);
    static readonly Color SecondaryButton = new Color(0.18f, 0.21f, 0.27f, 1f);

    static Font uiFont;
    static Sprite whiteSprite;
    static readonly Dictionary<string, Sprite> thumbnailSpriteCache =
        new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    VabEditorController editor;
    GameObject canvasRoot;
    GameObject modalBlocker;
    RectTransform listContent;
    ScrollRect shipListScroll;
    Text statusLabel;
    GameObject deleteConfirmRoot;
    Text deleteConfirmMessage;
    string pendingDeleteName;
    bool isOpen;

    struct ShipListEntry
    {
        public string storageName;
        public string displayName;
        public int partCount;
        public double massTonnes;
    }

    public bool IsOpen => isOpen;

    public void Initialize(VabEditorController vabEditor)
    {
        editor = vabEditor;
        BuildUi();
        SetOpen(false);
    }

    void Start()
    {
        SetOpen(false);
    }

    public void Open()
    {
        RefreshList();
        SetOpen(true);
    }

    public void RefreshIfOpen()
    {
        if (!isOpen)
        {
            return;
        }

        RefreshList();
    }

    public void Close()
    {
        SetOpen(false);
    }

    public bool TryHandleEscape()
    {
        if (!isOpen)
        {
            return false;
        }

        if (IsDeleteConfirmOpen())
        {
            CancelDelete();
            return true;
        }

        Close();
        return true;
    }

    bool IsDeleteConfirmOpen()
    {
        return deleteConfirmRoot != null && deleteConfirmRoot.activeSelf;
    }

    void SetOpen(bool open)
    {
        isOpen = open;

        if (!open)
        {
            HideDeleteConfirm();
        }

        if (canvasRoot != null)
        {
            canvasRoot.SetActive(open);
        }
    }

    void RefreshList()
    {
        if (listContent == null)
        {
            return;
        }

        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Destroy(listContent.GetChild(i).gameObject);
        }

        List<string> names = VabFileSystem.GetSavedVehicleNames();

        if (names.Count == 0)
        {
            CreateStatusRow("Нет сохранённых кораблей");
            RebuildListLayout();
            return;
        }

        ClearStatus();

        for (int i = 0; i < names.Count; i++)
        {
            CreateShipRow(LoadEntry(names[i]));
        }

        RebuildListLayout();
    }

    void RebuildListLayout()
    {
        if (listContent == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);

        if (shipListScroll != null)
        {
            shipListScroll.verticalNormalizedPosition = 1f;
        }
    }

    ShipListEntry LoadEntry(string storageName)
    {
        ShipListEntry entry = new ShipListEntry
        {
            storageName = storageName,
            displayName = storageName,
            partCount = 0,
            massTonnes = 0.0,
        };

        try
        {
            VesselBlueprint blueprint = VabFileSystem.LoadVehicle(storageName);
            entry.displayName = string.IsNullOrWhiteSpace(blueprint.vesselName) ?
                storageName :
                blueprint.vesselName.Trim();

            VabVesselStats stats = VabVesselAnalyzer.Analyze(
                blueprint,
                VabPartStatsBridge.TryLookup,
                LandingConfig.GetSurfaceStartGravity()
            );
            entry.partCount = stats.connectedPartCount;
            entry.massTonnes = stats.totalMassKg / 1000.0;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "VAB ship browser: failed to read '" + storageName + "': " + exception.Message
            );
        }

        return entry;
    }

    void CreateStatusRow(string message)
    {
        GameObject rowObject = CreateRowShell("EmptyState");
        Text label = CreateRowLabel(rowObject.transform, message, 18, TextAnchor.MiddleCenter);
        label.color = MutedColor;
    }

    void CreateShipRow(ShipListEntry entry)
    {
        string vesselName = entry.storageName;
        GameObject rowObject = CreateRowShell(vesselName);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();

        RectTransform thumbRect = new GameObject("Thumbnail").AddComponent<RectTransform>();
        thumbRect.SetParent(rowRect, false);
        thumbRect.anchorMin = new Vector2(0f, 0.5f);
        thumbRect.anchorMax = new Vector2(0f, 0.5f);
        thumbRect.pivot = new Vector2(0f, 0.5f);
        thumbRect.sizeDelta = new Vector2(ThumbnailSize, ThumbnailSize);
        thumbRect.anchoredPosition = new Vector2(12f, 0f);

        RectTransform clipRect = PartThumbnailRoundedMask.CreateClipContainer(thumbRect);
        PartThumbnailUi.CreateBackgroundLayer(clipRect);
        Sprite thumbnailSprite = LoadThumbnailSprite(vesselName);

        if (thumbnailSprite != null)
        {
            PartThumbnailUi.CreateForegroundLayer(clipRect, thumbnailSprite);
        }
        else
        {
            RectTransform fallback = PartThumbnailUi.CreateForegroundLayer(clipRect, null);
            Text letter = fallback.gameObject.AddComponent<Text>();
            letter.font = GetUiFont();
            letter.text = "🚀";
            letter.fontSize = 36;
            letter.alignment = TextAnchor.MiddleCenter;
            letter.color = MutedColor;
            letter.raycastTarget = false;
        }

        GameObject infoObject = new GameObject("Info");
        infoObject.transform.SetParent(rowRect, false);
        RectTransform infoRect = infoObject.AddComponent<RectTransform>();
        infoRect.anchorMin = Vector2.zero;
        infoRect.anchorMax = Vector2.one;
        infoRect.offsetMin = new Vector2(ThumbnailSize + 20f, 10f);
        infoRect.offsetMax = new Vector2(-196f, -10f);

        Text nameLabel = CreateRowLabel(
            infoObject.transform,
            entry.displayName,
            20,
            TextAnchor.UpperLeft
        );
        RectTransform nameRect = nameLabel.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0.55f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        string statsText =
            "Деталей: " + entry.partCount.ToString() +
            "   Масса: " + entry.massTonnes.ToString("F2") + " т";
        Text statsLabel = CreateRowLabel(
            infoObject.transform,
            statsText,
            15,
            TextAnchor.LowerLeft
        );
        statsLabel.fontStyle = FontStyle.Normal;
        statsLabel.color = MutedColor;
        RectTransform statsRect = statsLabel.rectTransform;
        statsRect.anchorMin = new Vector2(0f, 0f);
        statsRect.anchorMax = new Vector2(1f, 0.5f);
        statsRect.offsetMin = Vector2.zero;
        statsRect.offsetMax = Vector2.zero;

        CreateRowButton(rowObject.transform, "Загрузить", PrimaryButton, -108f, () =>
        {
            editor.LoadVesselFromBrowser(vesselName);
            Close();
        });
        CreateRowButton(rowObject.transform, "Удалить", DangerButton, -12f, () =>
        {
            RequestDelete(entry.displayName, vesselName);
        });
    }

    void RequestDelete(string displayName, string storageName)
    {
        pendingDeleteName = storageName;
        deleteConfirmMessage.text =
            "Удалить чертёж «" + displayName + "»?\nЭто действие нельзя отменить.";
        deleteConfirmRoot.SetActive(true);
    }

    void ExecuteDelete()
    {
        if (string.IsNullOrWhiteSpace(pendingDeleteName))
        {
            CancelDelete();
            return;
        }

        string vesselName = pendingDeleteName;
        HideDeleteConfirm();
        VabFileSystem.DeleteVehicle(vesselName);
        thumbnailSpriteCache.Remove(vesselName);
        ShowStatus("Удалено: " + vesselName);
        RefreshList();
    }

    void CancelDelete()
    {
        HideDeleteConfirm();
    }

    void HideDeleteConfirm()
    {
        pendingDeleteName = string.Empty;

        if (deleteConfirmRoot != null)
        {
            deleteConfirmRoot.SetActive(false);
        }
    }

    void ShowStatus(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
    }

    void ClearStatus()
    {
        if (statusLabel != null)
        {
            statusLabel.text = string.Empty;
        }
    }

    static Sprite LoadThumbnailSprite(string vesselName)
    {
        if (thumbnailSpriteCache.TryGetValue(vesselName, out Sprite cached) && cached != null)
        {
            return cached;
        }

        if (!VabFileSystem.TryLoadThumbnailBytes(vesselName, out byte[] pngBytes))
        {
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(pngBytes);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        thumbnailSpriteCache[vesselName] = sprite;
        return sprite;
    }

    public static void InvalidateThumbnailCache(string vesselName)
    {
        if (string.IsNullOrWhiteSpace(vesselName))
        {
            return;
        }

        thumbnailSpriteCache.Remove(vesselName);
    }

    GameObject CreateRowShell(string name)
    {
        GameObject rowObject = new GameObject(name);
        rowObject.transform.SetParent(listContent, false);

        LayoutElement layout = rowObject.AddComponent<LayoutElement>();
        layout.minHeight = RowHeight;
        layout.preferredHeight = RowHeight;

        Image background = rowObject.AddComponent<Image>();
        background.color = RowBackground;

        return rowObject;
    }

    static Text CreateRowLabel(Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.font = GetUiFont();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = alignment;
        label.color = LabelColor;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    void CreateRowButton(Transform parent, string label, Color color, float rightInset, Action onClick)
    {
        GameObject buttonObject = new GameObject(label + "Button");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(88f, 40f);
        rect.anchoredPosition = new Vector2(rightInset, 0f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => onClick());

        GameObject labelObject = new GameObject("Text");
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelObject.AddComponent<Text>();
        labelText.font = GetUiFont();
        labelText.text = label;
        labelText.fontSize = 15;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = LabelColor;
    }

    void BuildUi()
    {
        if (canvasRoot != null)
        {
            Destroy(canvasRoot);
            canvasRoot = null;
        }

        EnsureEventSystem();
        canvasRoot = new GameObject("VabShipBrowserCanvas", typeof(RectTransform));
        canvasRoot.SetActive(false);
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.65f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        modalBlocker = CreateStretchPanel(canvasRoot.transform, "Blocker", new Color(0f, 0f, 0f, 0.62f));
        Button blockerButton = modalBlocker.AddComponent<Button>();
        ColorBlock blockerColors = blockerButton.colors;
        blockerColors.normalColor = new Color(0f, 0f, 0f, 0.62f);
        blockerColors.highlightedColor = blockerColors.normalColor;
        blockerColors.pressedColor = blockerColors.normalColor;
        blockerButton.colors = blockerColors;
        blockerButton.onClick.AddListener(Close);

        GameObject panel = CreateCenteredPanel(
            canvasRoot.transform,
            "ShipBrowserPanel",
            new Vector2(PanelWidth, PanelHeight),
            PanelBackground
        );

        VerticalLayoutGroup rootLayout = panel.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(20, 20, 20, 20);
        rootLayout.spacing = 12f;
        rootLayout.childAlignment = TextAnchor.UpperCenter;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        CreatePanelHeader(panel.transform, "Сохранённые корабли", Close);

        GameObject scrollObject = CreateUiObject("Scroll", panel.transform);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();

        LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = 380f;
        scrollLayout.preferredHeight = 380f;

        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.06f, 0.08f, 0.11f, 0.85f);

        shipListScroll = scrollObject.AddComponent<ScrollRect>();
        shipListScroll.horizontal = false;
        shipListScroll.vertical = true;
        shipListScroll.movementType = ScrollRect.MovementType.Clamped;

        RectTransform viewport = CreateStretchChild(scrollRectTransform, "Viewport");
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUiObject("Content", viewport);
        listContent = contentObject.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        shipListScroll.viewport = viewport;
        shipListScroll.content = listContent;

        GameObject statusObject = CreateUiObject("Status", panel.transform);
        LayoutElement statusLayout = statusObject.AddComponent<LayoutElement>();
        statusLayout.minHeight = 22f;
        statusLabel = statusObject.AddComponent<Text>();
        statusLabel.font = GetUiFont();
        statusLabel.fontSize = 15;
        statusLabel.alignment = TextAnchor.MiddleCenter;
        statusLabel.color = new Color(0.45f, 0.92f, 0.55f, 1f);

        CreateCompactFooter(panel.transform, Close);

        deleteConfirmRoot = BuildDeleteConfirmDialog(canvasRoot.transform);
        deleteConfirmRoot.SetActive(false);

        isOpen = false;
        canvasRoot.SetActive(false);
    }

    static void CreatePanelHeader(Transform parent, string text, Action onClose)
    {
        GameObject headerObject = CreateUiObject("Header", parent);

        LayoutElement headerLayoutElement = headerObject.AddComponent<LayoutElement>();
        headerLayoutElement.minHeight = 44f;
        headerLayoutElement.preferredHeight = 44f;

        HorizontalLayoutGroup headerLayout = headerObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(4, 4, 0, 0);
        headerLayout.spacing = 8f;
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;

        GameObject titleObject = CreateUiObject("Title", headerObject.transform);
        LayoutElement titleLayout = titleObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;
        titleLayout.minHeight = 40f;

        Text title = titleObject.AddComponent<Text>();
        title.font = GetUiFont();
        title.text = text;
        title.fontSize = 24;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = LabelColor;

        CreateCloseButton(headerObject.transform, onClose);
    }

    static void CreateCloseButton(Transform parent, Action onClose)
    {
        GameObject buttonObject = CreateUiObject("CloseButton", parent);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minWidth = 28f;
        layout.preferredWidth = 28f;
        layout.minHeight = 28f;
        layout.preferredHeight = 28f;

        Image image = buttonObject.AddComponent<Image>();
        image.color = SecondaryButton;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => onClose());

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.font = GetUiFont();
        label.text = "✕";
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = LabelColor;
    }

    GameObject BuildDeleteConfirmDialog(Transform parent)
    {
        GameObject root = CreateUiObject("DeleteConfirm", parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image blocker = root.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.55f);
        Button blockerButton = root.AddComponent<Button>();
        ColorBlock blockerColors = blockerButton.colors;
        blockerColors.normalColor = new Color(0f, 0f, 0f, 0.55f);
        blockerColors.highlightedColor = blockerColors.normalColor;
        blockerColors.pressedColor = blockerColors.normalColor;
        blockerButton.colors = blockerColors;
        blockerButton.onClick.AddListener(CancelDelete);

        GameObject panel = CreateCenteredPanel(
            root.transform,
            "DeleteConfirmPanel",
            new Vector2(420f, 180f),
            PanelBackground
        );

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 18);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        GameObject titleObject = CreateUiObject("Title", panel.transform);
        LayoutElement titleLayout = titleObject.AddComponent<LayoutElement>();
        titleLayout.minHeight = 28f;
        titleLayout.preferredHeight = 28f;
        Text title = titleObject.AddComponent<Text>();
        title.font = GetUiFont();
        title.text = "Удалить чертёж?";
        title.fontSize = 22;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = LabelColor;

        GameObject messageObject = CreateUiObject("Message", panel.transform);
        LayoutElement messageLayout = messageObject.AddComponent<LayoutElement>();
        messageLayout.minHeight = 52f;
        messageLayout.preferredHeight = 52f;
        deleteConfirmMessage = messageObject.AddComponent<Text>();
        deleteConfirmMessage.font = GetUiFont();
        deleteConfirmMessage.fontSize = 16;
        deleteConfirmMessage.alignment = TextAnchor.MiddleCenter;
        deleteConfirmMessage.color = MutedColor;
        deleteConfirmMessage.horizontalOverflow = HorizontalWrapMode.Wrap;
        deleteConfirmMessage.verticalOverflow = VerticalWrapMode.Overflow;

        CreateConfirmButtonRow(
            panel.transform,
            ("Удалить", DangerButton, ExecuteDelete),
            ("Отмена", SecondaryButton, CancelDelete)
        );

        return root;
    }

    static void CreateConfirmButtonRow(
        Transform parent,
        (string label, Color color, Action onClick) left,
        (string label, Color color, Action onClick) right
    )
    {
        GameObject rowObject = CreateUiObject("Buttons", parent);
        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = 40f;
        rowLayout.preferredHeight = 40f;

        HorizontalLayoutGroup row = rowObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 10f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = false;

        CreateConfirmButton(rowObject.transform, left.label, left.color, left.onClick);
        CreateConfirmButton(rowObject.transform, right.label, right.color, right.onClick);
    }

    static void CreateConfirmButton(Transform parent, string label, Color color, Action onClick)
    {
        GameObject buttonObject = CreateUiObject(label + "Button", parent);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 38f;
        layout.preferredHeight = 38f;
        layout.flexibleWidth = 1f;

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => onClick());

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelObject.AddComponent<Text>();
        labelText.font = GetUiFont();
        labelText.text = label;
        labelText.fontSize = 16;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = LabelColor;
    }

    static void CreateCompactFooter(Transform parent, Action onClose)
    {
        GameObject footerObject = CreateUiObject("Footer", parent);
        LayoutElement footerLayout = footerObject.AddComponent<LayoutElement>();
        footerLayout.minHeight = 44f;
        footerLayout.preferredHeight = 44f;
        footerLayout.flexibleHeight = 0f;

        HorizontalLayoutGroup footerRow = footerObject.AddComponent<HorizontalLayoutGroup>();
        footerRow.padding = new RectOffset(0, 0, 4, 0);
        footerRow.spacing = 8f;
        footerRow.childAlignment = TextAnchor.MiddleRight;
        footerRow.childControlWidth = false;
        footerRow.childControlHeight = true;
        footerRow.childForceExpandWidth = false;
        footerRow.childForceExpandHeight = false;

        GameObject spacer = CreateUiObject("Spacer", footerObject.transform);
        LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1f;
        spacerLayout.minWidth = 0f;

        GameObject buttonObject = CreateUiObject("CloseButton", footerObject.transform);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minWidth = 112f;
        layout.preferredWidth = 112f;
        layout.minHeight = 34f;
        layout.preferredHeight = 34f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        Image image = buttonObject.AddComponent<Image>();
        image.color = SecondaryButton;

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => onClose());

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelObject.AddComponent<Text>();
        labelText.font = GetUiFont();
        labelText.text = "Закрыть";
        labelText.fontSize = 16;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = LabelColor;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject childObject = new GameObject(name, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject;
    }

    static GameObject CreateStretchPanel(Transform parent, string name, Color color)
    {
        GameObject panelObject = CreateUiObject(name, parent);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        return panelObject;
    }

    static GameObject CreateCenteredPanel(
        Transform parent,
        string name,
        Vector2 size,
        Color color
    )
    {
        GameObject panelObject = CreateUiObject(name, parent);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = panelObject.AddComponent<Image>();
        VabRoundedPanelUi.ApplyRoundedBackground(image, color);
        return panelObject;
    }

    static RectTransform CreateStretchChild(RectTransform parent, string name)
    {
        GameObject childObject = CreateUiObject(name, parent);
        RectTransform rect = childObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    static void EnsureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() != null)
        {
            return;
        }

        StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();

        if (legacyModule != null)
        {
            Destroy(legacyModule);
        }

        eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    static Font GetUiFont()
    {
        if (uiFont != null)
        {
            return uiFont;
        }

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (uiFont == null)
        {
            uiFont = Font.CreateDynamicFontFromOSFont("Arial", 18);
        }

        return uiFont;
    }
}
