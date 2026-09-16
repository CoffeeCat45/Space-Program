using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Checkbox catalog of flight debug chrome. Opened from graphics options.
/// Visibility is stored in <c>settings.json</c> (<c>debug.hiddenOverlays</c>).
/// </summary>
[DisallowMultipleComponent]
public sealed class FlightDebugSettingsUi : MonoBehaviour
{
    static readonly Color PanelBackground = new Color(0.08f, 0.10f, 0.14f, 0.97f);
    static readonly Color ControlBackground = new Color(0.16f, 0.19f, 0.24f, 1f);
    static readonly Color ControlBorder = new Color(0.34f, 0.42f, 0.52f, 1f);
    static readonly Color LabelColor = new Color(0.95f, 0.97f, 1f, 1f);
    static readonly Color MutedLabelColor = new Color(0.78f, 0.84f, 0.92f, 1f);
    static readonly Color CheckmarkColor = new Color(0.35f, 0.92f, 0.55f, 1f);
    static readonly Color PrimaryButtonColor = new Color(0.20f, 0.42f, 0.62f, 1f);
    static readonly Color SecondaryButtonColor = new Color(0.18f, 0.21f, 0.27f, 1f);
    static readonly Color RowOdd = new Color(0.12f, 0.14f, 0.18f, 0.55f);
    static readonly Color RowEven = new Color(0.10f, 0.12f, 0.16f, 0.25f);

    static FlightDebugSettingsUi cachedInstance;
    static Font uiFont;

    GameObject canvasRoot;
    GameObject panelRoot;
    Transform rowsParent;
    bool uiBuilt;
    bool isPanelOpen;
    bool suppressToggleEvents;
    readonly Toggle[] fieldToggles = new Toggle[FlightDebugSettings.Items.Length];
    readonly Text[] nameLabels = new Text[FlightDebugSettings.Items.Length];
    Text titleLabel;
    Text hintLabel;
    Text showAllLabel;
    Text hideAllLabel;
    Text closeLabel;

    public static FlightDebugSettingsUi TryGetInstance()
    {
        if (cachedInstance != null)
        {
            return cachedInstance;
        }

        cachedInstance = FindFirstObjectByType<FlightDebugSettingsUi>();
        return cachedInstance;
    }

    public static bool IsOpen =>
        cachedInstance != null && cachedInstance.isPanelOpen;

    public bool IsPanelOpen => isPanelOpen;

    void Awake()
    {
        cachedInstance = this;
        FlightDebugSettings.Load();
    }

    void OnDestroy()
    {
        if (cachedInstance == this)
        {
            cachedInstance = null;
        }
    }

    public void OpenPanel()
    {
        EnsureUiBuilt();
        RefreshLanguage();
        SyncTogglesFromSettings();
        SetPanelOpen(true);
    }

    public void ClosePanel()
    {
        SetPanelOpen(false);
    }

    public bool TryHandleEscape()
    {
        if (!isPanelOpen)
        {
            return false;
        }

        ClosePanel();
        return true;
    }

    void SetPanelOpen(bool open)
    {
        isPanelOpen = open;
        if (canvasRoot != null)
        {
            canvasRoot.SetActive(open);
        }
    }

    void EnsureUiBuilt()
    {
        if (uiBuilt)
        {
            return;
        }

        EnsureEventSystem();
        EnsureUiFont();

        canvasRoot = new GameObject("DebugSettingsCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 530;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        GameObject blocker = CreateUiObject("DebugSettingsBlocker", canvasRoot.transform);
        Image blockerImage = blocker.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.72f);
        StretchRect(blocker.GetComponent<RectTransform>());
        Button blockerButton = blocker.AddComponent<Button>();
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.onClick.AddListener(ClosePanel);

        panelRoot = CreateUiObject("DebugSettingsPanel", canvasRoot.transform);
        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = PanelBackground;
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.18f, 0.10f);
        panelRect.anchorMax = new Vector2(0.82f, 0.90f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup panelLayout = panelRoot.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(28, 28, 20, 20);
        panelLayout.spacing = 10f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        titleLabel = CreateText(
            panelRoot.transform,
            "Title",
            32,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            LabelColor,
            40f
        );
        hintLabel = CreateText(
            panelRoot.transform,
            "Hint",
            16,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            MutedLabelColor,
            44f
        );
        hintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

        GameObject toolbar = CreateUiObject("Toolbar", panelRoot.transform);
        LayoutElement toolbarLayout = toolbar.AddComponent<LayoutElement>();
        toolbarLayout.minHeight = 44f;
        toolbarLayout.preferredHeight = 44f;
        HorizontalLayoutGroup toolbarGroup = toolbar.AddComponent<HorizontalLayoutGroup>();
        toolbarGroup.spacing = 12f;
        toolbarGroup.childAlignment = TextAnchor.MiddleLeft;
        toolbarGroup.childControlWidth = true;
        toolbarGroup.childControlHeight = true;
        toolbarGroup.childForceExpandWidth = false;
        toolbarGroup.childForceExpandHeight = false;

        Button showAll = CreateToolbarButton(toolbar.transform, PrimaryButtonColor, out showAllLabel);
        showAll.onClick.AddListener(OnShowAllClicked);
        Button hideAll = CreateToolbarButton(toolbar.transform, SecondaryButtonColor, out hideAllLabel);
        hideAll.onClick.AddListener(OnHideAllClicked);
        Button close = CreateToolbarButton(toolbar.transform, SecondaryButtonColor, out closeLabel);
        close.onClick.AddListener(ClosePanel);

        Transform scrollContent = CreateScrollContent(panelRoot.transform);
        rowsParent = scrollContent;

        string lastSection = null;
        for (int i = 0; i < FlightDebugSettings.Items.Length; i++)
        {
            FlightDebugSettings.Item item = FlightDebugSettings.Items[i];
            if (item.SectionKey != lastSection)
            {
                lastSection = item.SectionKey;
                CreateSectionHeader(scrollContent, item.SectionKey);
            }

            bool odd = (i % 2) == 0;
            fieldToggles[i] = CreateFieldRow(
                scrollContent,
                odd,
                out nameLabels[i]
            );
            int captured = i;
            fieldToggles[i].onValueChanged.AddListener(on => OnFieldToggleChanged(captured, on));
        }

        uiBuilt = true;
        canvasRoot.SetActive(false);
    }

    void OnShowAllClicked()
    {
        FlightDebugSettings.SetAllVisible(true);
        SyncTogglesFromSettings();
    }

    void OnHideAllClicked()
    {
        FlightDebugSettings.SetAllVisible(false);
        SyncTogglesFromSettings();
    }

    void OnFieldToggleChanged(int index, bool on)
    {
        if (suppressToggleEvents || index < 0 || index >= FlightDebugSettings.Items.Length)
        {
            return;
        }

        FlightDebugSettings.SetVisible(FlightDebugSettings.Items[index].Overlay, on);
    }

    void SyncTogglesFromSettings()
    {
        suppressToggleEvents = true;
        for (int i = 0; i < fieldToggles.Length; i++)
        {
            if (fieldToggles[i] == null)
            {
                continue;
            }

            fieldToggles[i].SetIsOnWithoutNotify(
                FlightDebugSettings.IsVisible(FlightDebugSettings.Items[i].Overlay)
            );
        }

        suppressToggleEvents = false;
        RefreshLanguage();
    }

    void RefreshLanguage()
    {
        if (titleLabel != null)
        {
            titleLabel.text = Localization.Translate("Debug settings");
        }

        if (hintLabel != null)
        {
            hintLabel.text = Localization.Translate("Debug settings hint");
        }

        if (showAllLabel != null)
        {
            showAllLabel.text = Localization.Translate("Show all debug overlays");
        }

        if (hideAllLabel != null)
        {
            hideAllLabel.text = Localization.Translate("Hide all debug overlays");
        }

        if (closeLabel != null)
        {
            closeLabel.text = Localization.Translate("Close");
        }

        for (int i = 0; i < FlightDebugSettings.Items.Length; i++)
        {
            if (nameLabels[i] != null)
            {
                nameLabels[i].text = Localization.Translate(FlightDebugSettings.Items[i].NameKey);
            }
        }

        if (rowsParent == null)
        {
            return;
        }

        for (int i = 0; i < rowsParent.childCount; i++)
        {
            Transform child = rowsParent.GetChild(i);
            if (!child.name.StartsWith("Section:"))
            {
                continue;
            }

            Text sectionText = child.GetComponent<Text>();
            if (sectionText == null)
            {
                continue;
            }

            string key = child.name.Substring("Section:".Length);
            sectionText.text = Localization.Translate(key);
        }
    }

    static Transform CreateScrollContent(Transform parent)
    {
        GameObject scrollRoot = CreateUiObject("Scroll", parent);
        LayoutElement scrollLayout = scrollRoot.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = 240f;

        ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        GameObject viewport = CreateUiObject("Viewport", scrollRoot.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        StretchRect(viewportRect);
        viewport.AddComponent<RectMask2D>();
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 2f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        return content.transform;
    }

    static void CreateSectionHeader(Transform parent, string sectionKey)
    {
        GameObject header = CreateUiObject("Section:" + sectionKey, parent);
        LayoutElement layout = header.AddComponent<LayoutElement>();
        layout.minHeight = 34f;
        layout.preferredHeight = 34f;
        Text label = header.AddComponent<Text>();
        label.font = uiFont;
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = LabelColor;
        label.text = Localization.Translate(sectionKey);
    }

    static Toggle CreateFieldRow(Transform parent, bool odd, out Text nameLabel)
    {
        GameObject row = CreateUiObject("FieldRow", parent);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 38f;
        rowLayout.preferredHeight = 38f;
        Image rowImage = row.AddComponent<Image>();
        rowImage.color = odd ? RowOdd : RowEven;

        HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
        rowGroup.padding = new RectOffset(10, 10, 4, 4);
        rowGroup.spacing = 12f;
        rowGroup.childAlignment = TextAnchor.MiddleLeft;
        rowGroup.childControlWidth = true;
        rowGroup.childControlHeight = true;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = false;

        Toggle toggle = CreateToggle(row.transform);

        GameObject nameObject = CreateUiObject("Name", row.transform);
        LayoutElement nameLayout = nameObject.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1f;
        nameLayout.minHeight = 28f;
        nameLabel = nameObject.AddComponent<Text>();
        nameLabel.font = uiFont;
        nameLabel.fontSize = 16;
        nameLabel.alignment = TextAnchor.MiddleLeft;
        nameLabel.color = LabelColor;
        nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        return toggle;
    }

    static Toggle CreateToggle(Transform parent)
    {
        GameObject toggleObject = CreateUiObject("Toggle", parent);
        LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
        layout.minWidth = 28f;
        layout.preferredWidth = 28f;
        layout.minHeight = 28f;
        layout.preferredHeight = 28f;

        Toggle toggle = toggleObject.AddComponent<Toggle>();
        GameObject background = CreateUiObject("Background", toggleObject.transform);
        StretchRect(background.GetComponent<RectTransform>());
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = ControlBackground;
        Outline outline = background.AddComponent<Outline>();
        outline.effectColor = ControlBorder;
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject checkmark = CreateUiObject("Checkmark", background.transform);
        StretchRect(checkmark.GetComponent<RectTransform>());
        Text checkLabel = checkmark.AddComponent<Text>();
        checkLabel.font = uiFont;
        checkLabel.text = "✓";
        checkLabel.fontSize = 18;
        checkLabel.fontStyle = FontStyle.Bold;
        checkLabel.alignment = TextAnchor.MiddleCenter;
        checkLabel.color = CheckmarkColor;

        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkLabel;
        toggle.isOn = true;
        return toggle;
    }

    static Button CreateToolbarButton(Transform parent, Color color, out Text label)
    {
        GameObject buttonObject = CreateUiObject("Button", parent);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 42f;
        layout.preferredHeight = 42f;
        layout.flexibleWidth = 1f;
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        StretchRect(labelObject.GetComponent<RectTransform>());
        label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.fontSize = 17;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = LabelColor;
        return button;
    }

    static Text CreateText(
        Transform parent,
        string name,
        int size,
        FontStyle style,
        TextAnchor align,
        Color color,
        float height
    )
    {
        GameObject labelObject = CreateUiObject(name, parent);
        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        Text label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = align;
        label.color = color;
        return label;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void EnsureUiFont()
    {
        if (uiFont != null)
        {
            return;
        }

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
            Destroy(legacy);
        }

        eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
