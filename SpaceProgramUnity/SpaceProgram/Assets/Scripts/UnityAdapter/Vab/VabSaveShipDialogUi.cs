using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Modal dialogs for naming a vessel before save and confirming overwrite.
/// </summary>
public class VabSaveShipDialogUi : MonoBehaviour
{
    const float DialogWidth = 440f;
    const float NameDialogHeight = 220f;
    const float ConfirmDialogHeight = 180f;

    static readonly Color PanelBackground = new Color(0.10f, 0.12f, 0.16f, 0.99f);
    static readonly Color LabelColor = new Color(0.95f, 0.97f, 1f, 1f);
    static readonly Color MutedColor = new Color(0.72f, 0.78f, 0.86f, 1f);
    static readonly Color ErrorColor = new Color(1f, 0.55f, 0.45f, 1f);
    static readonly Color PrimaryButton = new Color(0.20f, 0.42f, 0.62f, 1f);
    static readonly Color SecondaryButton = new Color(0.18f, 0.21f, 0.27f, 1f);
    static readonly Color WarningButton = new Color(0.62f, 0.38f, 0.18f, 1f);

    static Font uiFont;

    GameObject canvasRoot;
    GameObject modalBlocker;
    Button modalBlockerButton;
    GameObject nameDialogRoot;
    GameObject overwriteDialogRoot;
    InputField nameInput;
    Text nameErrorLabel;
    Text overwriteMessageLabel;
    string pendingName;
    Action<string> onSaved;
    Action onCancelled;
    bool isOpen;

    public bool IsOpen => isOpen;

    public void Initialize()
    {
        BuildUi();
        SetOpen(false);
    }

    public void Open(string suggestedName, Action<string> savedCallback, Action cancelledCallback = null)
    {
        onSaved = savedCallback;
        onCancelled = cancelledCallback;
        pendingName = string.Empty;
        nameInput.text = string.IsNullOrWhiteSpace(suggestedName) ? string.Empty : suggestedName.Trim();
        ClearNameError();
        ShowNameDialog();
        SetOpen(true);
        nameInput.ActivateInputField();
        nameInput.MoveTextEnd(false);
    }

    public void Close()
    {
        HideOverwriteDialog();
        SetOpen(false);
        onSaved = null;
        onCancelled = null;
        pendingName = string.Empty;
    }

    public bool TryHandleEscape()
    {
        if (!isOpen)
        {
            return false;
        }

        if (overwriteDialogRoot != null && overwriteDialogRoot.activeSelf)
        {
            CancelOverwrite();
            return true;
        }

        CancelNameDialog();
        return true;
    }

    void SetOpen(bool open)
    {
        isOpen = open;

        if (canvasRoot != null)
        {
            canvasRoot.SetActive(open);
        }
    }

    void HandleBlockerClick()
    {
        if (overwriteDialogRoot != null && overwriteDialogRoot.activeSelf)
        {
            CancelOverwrite();
            return;
        }

        CancelNameDialog();
    }

    void ShowNameDialog()
    {
        HideOverwriteDialog();

        if (nameDialogRoot != null)
        {
            nameDialogRoot.SetActive(true);
        }
    }

    void HideNameDialog()
    {
        if (nameDialogRoot != null)
        {
            nameDialogRoot.SetActive(false);
        }
    }

    void ShowOverwriteDialog(string vesselName)
    {
        pendingName = vesselName;
        overwriteMessageLabel.text =
            "Корабль «" + vesselName + "» уже существует.\nПерезаписать?";
        overwriteDialogRoot.SetActive(true);
    }

    void HideOverwriteDialog()
    {
        if (overwriteDialogRoot != null)
        {
            overwriteDialogRoot.SetActive(false);
        }
    }

    void ConfirmNameDialog()
    {
        string trimmedName = nameInput.text != null ? nameInput.text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ShowNameError("Введите название чертежа.");
            return;
        }

        ClearNameError();

        if (VabFileSystem.VehicleExists(trimmedName))
        {
            ShowOverwriteDialog(trimmedName);
            return;
        }

        CompleteSave(trimmedName);
    }

    void ConfirmOverwrite()
    {
        if (string.IsNullOrWhiteSpace(pendingName))
        {
            CancelOverwrite();
            return;
        }

        CompleteSave(pendingName);
    }

    void CancelOverwrite()
    {
        HideOverwriteDialog();
        ShowNameDialog();
        nameInput.ActivateInputField();
        nameInput.MoveTextEnd(false);
    }

    void CancelNameDialog()
    {
        Action cancelled = onCancelled;
        Close();
        cancelled?.Invoke();
    }

    void CompleteSave(string vesselName)
    {
        Action<string> saved = onSaved;
        Close();
        saved?.Invoke(vesselName);
    }

    void ShowNameError(string message)
    {
        nameErrorLabel.text = message;
        nameErrorLabel.color = ErrorColor;
    }

    void ClearNameError()
    {
        nameErrorLabel.text = string.Empty;
    }

    void BuildUi()
    {
        if (canvasRoot != null)
        {
            Destroy(canvasRoot);
            canvasRoot = null;
        }

        EnsureEventSystem();
        canvasRoot = new GameObject("VabSaveShipDialogCanvas", typeof(RectTransform));
        canvasRoot.SetActive(false);
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.65f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        GameObject blocker = CreateStretchPanel(canvasRoot.transform, "Blocker", new Color(0f, 0f, 0f, 0.45f));
        modalBlocker = blocker;
        modalBlockerButton = blocker.AddComponent<Button>();
        ColorBlock blockerColors = modalBlockerButton.colors;
        blockerColors.normalColor = new Color(0f, 0f, 0f, 0.45f);
        blockerColors.highlightedColor = blockerColors.normalColor;
        blockerColors.pressedColor = blockerColors.normalColor;
        modalBlockerButton.colors = blockerColors;
        modalBlockerButton.onClick.AddListener(HandleBlockerClick);

        nameDialogRoot = BuildNameDialog(canvasRoot.transform);
        overwriteDialogRoot = BuildOverwriteDialog(canvasRoot.transform);
        overwriteDialogRoot.SetActive(false);
    }

    GameObject BuildNameDialog(Transform parent)
    {
        GameObject panel = CreateCenteredPanel(
            parent,
            "NameDialog",
            new Vector2(DialogWidth, NameDialogHeight),
            PanelBackground
        );

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 18);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateDialogLabel(panel.transform, "Сохранить чертёж", 22, FontStyle.Bold, LabelColor);

        GameObject inputObject = CreateUiObject("NameInput", panel.transform);
        LayoutElement inputLayout = inputObject.AddComponent<LayoutElement>();
        inputLayout.minHeight = 40f;
        inputLayout.preferredHeight = 40f;
        inputLayout.flexibleHeight = 0f;

        Image inputBackground = inputObject.AddComponent<Image>();
        inputBackground.color = new Color(0.04f, 0.05f, 0.08f, 1f);

        GameObject textObject = CreateUiObject("Text", inputObject.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        Text text = textObject.AddComponent<Text>();
        text.font = GetUiFont();
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = LabelColor;
        text.supportRichText = false;

        nameInput = inputObject.AddComponent<InputField>();
        nameInput.textComponent = text;
        nameInput.lineType = InputField.LineType.SingleLine;
        nameInput.onValueChanged.AddListener(_ => ClearNameError());

        GameObject errorObject = CreateUiObject("Error", panel.transform);
        LayoutElement errorLayout = errorObject.AddComponent<LayoutElement>();
        errorLayout.minHeight = 20f;
        errorLayout.preferredHeight = 20f;
        errorLayout.flexibleHeight = 0f;
        nameErrorLabel = errorObject.AddComponent<Text>();
        nameErrorLabel.font = GetUiFont();
        nameErrorLabel.fontSize = 14;
        nameErrorLabel.alignment = TextAnchor.MiddleLeft;
        nameErrorLabel.color = ErrorColor;

        CreateDialogButtonRow(
            panel.transform,
            ("Сохранить", PrimaryButton, ConfirmNameDialog),
            ("Отмена", SecondaryButton, CancelNameDialog)
        );

        return panel;
    }

    GameObject BuildOverwriteDialog(Transform parent)
    {
        GameObject panel = CreateCenteredPanel(
            parent,
            "OverwriteDialog",
            new Vector2(DialogWidth, ConfirmDialogHeight),
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

        CreateDialogLabel(panel.transform, "Имя занято", 22, FontStyle.Bold, LabelColor);

        GameObject messageObject = CreateUiObject("Message", panel.transform);
        LayoutElement messageLayout = messageObject.AddComponent<LayoutElement>();
        messageLayout.minHeight = 52f;
        messageLayout.preferredHeight = 52f;
        messageLayout.flexibleHeight = 0f;
        overwriteMessageLabel = messageObject.AddComponent<Text>();
        overwriteMessageLabel.font = GetUiFont();
        overwriteMessageLabel.fontSize = 16;
        overwriteMessageLabel.alignment = TextAnchor.MiddleCenter;
        overwriteMessageLabel.color = MutedColor;
        overwriteMessageLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        overwriteMessageLabel.verticalOverflow = VerticalWrapMode.Overflow;

        CreateDialogButtonRow(
            panel.transform,
            ("Перезаписать", WarningButton, ConfirmOverwrite),
            ("Отмена", SecondaryButton, CancelOverwrite)
        );

        return panel;
    }

    static void CreateDialogLabel(
        Transform parent,
        string text,
        int fontSize,
        FontStyle style,
        Color color
    )
    {
        GameObject labelObject = CreateUiObject("Label", parent);
        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.minHeight = 28f;
        layout.preferredHeight = 28f;
        layout.flexibleHeight = 0f;

        Text label = labelObject.AddComponent<Text>();
        label.font = GetUiFont();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
    }

    static void CreateDialogButtonRow(
        Transform parent,
        (string label, Color color, Action onClick) left,
        (string label, Color color, Action onClick) right
    )
    {
        GameObject rowObject = CreateUiObject("Buttons", parent);
        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = 40f;
        rowLayout.preferredHeight = 40f;
        rowLayout.flexibleHeight = 0f;

        HorizontalLayoutGroup row = rowObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 10f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = false;

        CreateDialogButton(rowObject.transform, left.label, left.color, left.onClick);
        CreateDialogButton(rowObject.transform, right.label, right.color, right.onClick);
    }

    static void CreateDialogButton(Transform parent, string label, Color color, Action onClick)
    {
        GameObject buttonObject = CreateUiObject(label + "Button", parent);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 38f;
        layout.preferredHeight = 38f;
        layout.flexibleHeight = 0f;
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
