using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// VAB 3D editor: KSP-style part catalog UI, mouse placement, attachment-node snap.
/// Attach to <c>SceneLogic</c> in VabScene.
/// </summary>
public class VabEditorController : MonoBehaviour
{
    const float SnapDistanceMeters = VabSnapSolverAdapter.SnapAcquireMeters;
    const float SnapPreviewMeters = VabSnapSolverAdapter.SnapPreviewMeters;
    const float SnapReleaseDistanceMeters = VabSnapSolverAdapter.SnapReleaseMeters;
    const float SnapReleaseHorizontalMeters = 0.85f;
    const float SnapReleaseVerticalMeters = 0.85f;
    const float SidebarWidth = 320f;
    const float CombinedHeaderHeight = 136f;
    const float CategoryStripHeight = 44f;
    const float CategoryButtonHeight = 40f;
    const float ActionBarHeight = 52f;
    const float PartGridCellSize = 96f;
    const int PartGridColumns = 3;
    const int UiFontSizeTitle = 22;
    const int UiFontSizeBody = 17;
    const int UiFontSizeSmall = 14;
    const int UiFontSizeIcon = 26;
    const int UiFontSizeCategory = 18;
    const float DefaultBuildOriginHeightAboveFloor = 0f;
    const float FocusPanVerticalRangeMeters = 28f;
    const float FocusPanHorizontalRangeMeters = 22f;
    const float HeaderSideInset = 12f;
    const float TitleTopInset = 6f;
    const float TitleBottomInset = 88f;
    const float VesselNameTopInset = 57f;
    const float VesselNameBottomInset = 42f;
    const float PlacementRaycastMaxMeters = 60f;
    const float MaxPlacementCursorRadiusMeters = 14f;
    const float GhostStablePlaneMaxHorizontalMeters = 16f;
    const float DefaultOrbitDistanceAtWorkCenter = 14f;
    const float PartDoubleClickSeconds = 0.4f;
    const float PartDoubleClickScreenRadius = 14f;
    static readonly int[] SymmetryModes = { 1, 2, 3, 4, 6, 8 };

    [SerializeField]
    string vesselName = "Untitled";

    [SerializeField]
    LayerMask placementRaycastMask = ~0;
    LayerMask attachmentNodeMask;

    [SerializeField]
    float buildOriginHeightAboveFloor = DefaultBuildOriginHeightAboveFloor;

    readonly List<PlacedPart> placedParts = new List<PlacedPart>();
    readonly Dictionary<string, Button> categoryButtons = new Dictionary<string, Button>();

    Transform vesselRoot;
    Camera vabCamera;
    Canvas uiCanvas;
    RectTransform partsListContent;
    VabPartCatalogTooltip partCatalogTooltip;
    Text headerLabel;
    Text placementStatusText;
    InputField vesselNameField;

    PlacedPart placementGhost;
    PartTemplate placementTemplate;
    string activeCategory;

    VabEditSession editSession;
    Transform holdPivot;
    readonly List<PlacedPart> heldBranchParts = new List<PlacedPart>();
    PlacedPart detachedFromParent;
    int detachedFromNodeIndex = -1;
    Vector3 detachedLocalPosition;
    Quaternion detachedLocalRotation;
    bool isDetachedBranchHold;
    bool detachedBranchWasConnected;
    float detachHoldVerticalAnchorY;
    float detachIntentAnchorY;

    PlacedPart selectedPart;
    VabSelectionHighlighter selectedHighlighter;
    VabCameraController vabCameraController;
    VabFocusPlaneCursor focusPlaneCursor;
    VabRotationGizmo rotationGizmo;
    VabOptionsUi optionsUi;
    SimulationRunner sessionRunner;
    Vector3 buildOriginPosition;
    bool buildOriginResolved;
    float lastPartClickTime;
    Vector2 lastPartClickScreenPos;

    VabNodeSnapResolver.NodeSnapMatch stickyNodeMatch;
    bool hasStickyNodeMatch;
    int lastActiveGhostNodeIndex = -1;
    Vector3 snapLatchCursorPoint;
    float snapLatchPlaneY;
    bool hasSnapPlacementLatch;

    bool placementGhostSnapped;
    int symmetryModeIndex;
    int currentSymmetryCount = 1;
    bool holdCameraLocked;
    VabPlacementAttachMode placementAttachMode = VabPlacementAttachMode.Auto;
    readonly VabGhostMovementController.SnapAcquireSuppressState snapAcquireSuppress =
        new VabGhostMovementController.SnapAcquireSuppressState();

    Text debugHudText;
    Text vesselStatsText;
    Button launchButton;
    GameObject debugHudPanel;
    GameObject debugExpandButton;
    bool isDebugHudExpanded;
    VabShipBrowserUi shipBrowser;
    VabSaveShipDialogUi saveShipDialog;
    int lastStatsRevision = -1;
    Coroutine saveThumbnailRoutine;

    void Start()
    {
        sessionRunner = FindFirstObjectByType<SimulationRunner>();
        vabCamera = Camera.main;

        if (vabCamera == null)
        {
            vabCamera = FindFirstObjectByType<Camera>();
        }

        SetupVabCamera();
        ConfigurePlacementRaycastMask();
        VabSnapSolverAdapter.AttachmentNodeMask = attachmentNodeMask;
        HideImportedDecorativeSpheres();
        SetupRotationGizmo();

        VabSceneEnvironment environment = FindFirstObjectByType<VabSceneEnvironment>();
        GameObject rootObject = new GameObject("VesselRoot");

        if (environment != null)
        {
            rootObject.transform.SetParent(environment.transform, worldPositionStays: true);
        }

        vesselRoot = rootObject.transform;
        InitializeBuildOrigin();

        GameObject holdPivotObject = new GameObject("VabHoldPivot");
        holdPivotObject.transform.SetParent(vesselRoot, worldPositionStays: true);
        holdPivot = holdPivotObject.transform;

        SetupFocusPlaneCursor();
        optionsUi = VabOptionsUi.TryGetInstance();

        editSession = new VabEditSession(vesselName);

        int partCount = PartCatalog.Refresh();

        if (partCount == 0)
        {
            Debug.LogWarning(
                "VAB: no parts found in " + PartCatalog.PrefabsAssetRoot +
                ". Run Space Program → ReStock → 9 to build prefabs."
            );
        }

        EnsureEventSystem();
        PartThumbnailLoader.ClearCache();
        BuildUi();

        Transform existingBrowser = transform.Find("VabShipBrowser");

        if (existingBrowser != null)
        {
            Destroy(existingBrowser.gameObject);
        }

        GameObject browserHost = new GameObject("VabShipBrowser");
        browserHost.transform.SetParent(transform, false);
        shipBrowser = browserHost.AddComponent<VabShipBrowserUi>();
        shipBrowser.Initialize(this);

        GameObject saveDialogHost = new GameObject("VabSaveShipDialog");
        saveDialogHost.transform.SetParent(transform, false);
        saveShipDialog = saveDialogHost.AddComponent<VabSaveShipDialogUi>();
        saveShipDialog.Initialize();

        IReadOnlyList<string> categories = PartDatabase.GetCategories();

        if (categories.Count > 0)
        {
            SelectCategory(categories[0]);
        }

        RefreshUiVisibility();
        EnsureVabCameraReady();
        Debug.Log(
            "VAB UI ready: " + PartDatabase.Templates.Count +
            " parts, categories=" + categories.Count
        );
    }

    void Update()
    {
        RefreshUiVisibility();

        bool vabActive = ShouldShowEditorUi();

        if (!vabActive)
        {
            if (HasActiveHold())
            {
                CancelPlacement();
            }

            return;
        }

        if (editSession == null)
        {
            return;
        }

        if (WasCancelPlacementPressed())
        {
            HandleEscapeInVab();
            return;
        }

        UpdateFocusPlaneCursor();
        UpdatePlacementStatusUi();
        UpdateDebugHud();
        RefreshVesselStatsIfNeeded();

        if (placementGhost != null)
        {
            UpdatePlacementInput();
            return;
        }

        HandleSceneSelectionInput();
    }

    public bool HasEmptyHands => !HasActiveHold();

    bool HasActiveHold()
    {
        return placementGhost != null || (editSession != null && editSession.IsHolding);
    }

    void HandleEscapeInVab()
    {
        if (HasActiveHold())
        {
            CancelPlacement();
            return;
        }

        if (optionsUi == null)
        {
            optionsUi = VabOptionsUi.TryGetInstance();
        }

        if (optionsUi != null && optionsUi.TryHandleEscape())
        {
            return;
        }

        if (saveShipDialog != null && saveShipDialog.TryHandleEscape())
        {
            return;
        }

        if (shipBrowser != null && shipBrowser.TryHandleEscape())
        {
            return;
        }

        sessionRunner?.ReturnToMainMenu();
    }

    void UpdatePlacementInput()
    {
        if (!IsPointerOverUi())
        {
            UpdatePlacementGhostMovement(GetPlacementTransform().position, 1f);
        }

        UpdateHeldDeleteInput();

        if (WasSymmetryTogglePressed())
        {
            CycleSymmetryMode();
        }

        if (WasPlacementAttachModeTogglePressed())
        {
            CyclePlacementAttachMode();
        }

        if (WasDeletePressed())
        {
            DeleteHeldParts();
            return;
        }

        if (WasLeftMousePressed() && !IsPointerOverUi())
        {
            ConfirmPlacement();
        }
    }

    void CyclePlacementAttachMode()
    {
        placementAttachMode = placementAttachMode switch
        {
            VabPlacementAttachMode.Auto => VabPlacementAttachMode.StackOnly,
            VabPlacementAttachMode.StackOnly => VabPlacementAttachMode.SurfaceOnly,
            _ => VabPlacementAttachMode.Auto,
        };
        UpdatePlacementStatusUi();
        Debug.Log("VAB attach mode: " + placementAttachMode);
    }

    static bool WasPlacementAttachModeTogglePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.rKey.wasPressedThisFrame;
    }

    static bool IsStrictNodeAttachHeld()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return false;
        }

        return keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
    }

    VabPlacementAttachMode ResolveEffectivePlacementAttachMode()
    {
        if (IsStrictNodeAttachHeld())
        {
            return VabPlacementAttachMode.StackOnly;
        }

        return placementAttachMode;
    }

    static string FormatPlacementAttachMode(VabPlacementAttachMode mode)
    {
        return mode switch
        {
            VabPlacementAttachMode.StackOnly => "Стек",
            VabPlacementAttachMode.SurfaceOnly => "Радиал",
            _ => "Авто",
        };
    }

    float ResolveStackApproachMeters(PartVisualAdapter adapter, PartTemplate template)
    {
        return VabPartStackMetrics.ResolveStackApproachHorizontalMeters(adapter, template);
    }

    void CycleSymmetryMode()
    {
        symmetryModeIndex = (symmetryModeIndex + 1) % SymmetryModes.Length;
        currentSymmetryCount = SymmetryModes[symmetryModeIndex];
        UpdatePlacementStatusUi();
        Debug.Log("VAB symmetry: x" + currentSymmetryCount);
    }

    static string FormatPlacementAttachModeLong(VabPlacementAttachMode mode, bool altStrict)
    {
        if (altStrict)
        {
            return "Только стек (удерж. Alt)";
        }

        return mode switch
        {
            VabPlacementAttachMode.StackOnly => "Только стек (торцы)",
            VabPlacementAttachMode.SurfaceOnly => "Только бок (радиал)",
            _ => "Авто (стек + бок)",
        };
    }

    string BuildPlacementStatusText()
    {
        bool altStrict = IsStrictNodeAttachHeld();
        VabPlacementAttachMode effectiveMode = ResolveEffectivePlacementAttachMode();
        System.Text.StringBuilder sb = new System.Text.StringBuilder(160);
        sb.AppendLine("Симметрия: ×" + currentSymmetryCount);
        sb.AppendLine("Крепление: " + FormatPlacementAttachModeLong(effectiveMode, altStrict));

        if (altStrict && placementAttachMode != VabPlacementAttachMode.StackOnly)
        {
            sb.AppendLine("Базовый: " + FormatPlacementAttachModeLong(placementAttachMode, false));
        }

        sb.Append("X — симметрия   R — режим   Alt — строгий стек");
        return sb.ToString();
    }

    void UpdatePlacementStatusUi()
    {
        if (placementStatusText != null)
        {
            placementStatusText.text = BuildPlacementStatusText();
        }
    }

    void UpdateSymmetryHeaderLabel()
    {
        if (headerLabel == null)
        {
            return;
        }

        headerLabel.text = "Сборка ракеты";
    }

    static bool WasSymmetryTogglePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.xKey.wasPressedThisFrame;
    }

    static bool WasCancelPlacementPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    void HandleSceneSelectionInput()
    {
        if (WasDeletePressed())
        {
            if (TryPickPlacedPart(VabPartPickResolver.PickDepth.Closest, out PlacedPart partToDelete))
            {
                DeletePlacedBranch(partToDelete);
                ClearPartSelection();
            }

            return;
        }

        if (!WasLeftMousePressed() || IsPointerOverUi())
        {
            return;
        }

        Vector3 mouseScreen = GetMouseScreenPosition();
        Vector2 mouseScreenPos = new Vector2(mouseScreen.x, mouseScreen.y);
        bool isDoubleClick =
            Time.unscaledTime - lastPartClickTime < PartDoubleClickSeconds &&
            (mouseScreenPos - lastPartClickScreenPos).sqrMagnitude <=
                PartDoubleClickScreenRadius * PartDoubleClickScreenRadius;

        if (
            isDoubleClick &&
            TryPickPlacedPart(VabPartPickResolver.PickDepth.ForHold, out PlacedPart heldPart)
        )
        {
            lastPartClickTime = 0f;
            SelectPart(heldPart);
            TryBeginHoldPlacedPart(heldPart);
            return;
        }

        if (!TryPickPlacedPart(VabPartPickResolver.PickDepth.ForHold, out PlacedPart pickedPart))
        {
            ClearPartSelection();
            return;
        }

        lastPartClickTime = Time.unscaledTime;
        lastPartClickScreenPos = mouseScreenPos;

        if (selectedPart != pickedPart)
        {
            SelectPart(pickedPart);
            return;
        }

        bool detachSinglePart = IsCtrlHeld();

        if (pickedPart.isDisjointed)
        {
            BeginHoldDisjointedPart(pickedPart);
            return;
        }

        if (detachSinglePart)
        {
            BeginDetachSinglePart(pickedPart);
        }
        else
        {
            BeginDetachBranch(pickedPart);
        }
    }

    void TryBeginHoldPlacedPart(PlacedPart part)
    {
        if (part == null || placementGhost != null)
        {
            return;
        }

        if (part.isDisjointed)
        {
            BeginHoldDisjointedPart(part);
            return;
        }

        if (IsCtrlHeld())
        {
            BeginDetachSinglePart(part);
            return;
        }

        BeginDetachBranch(part);
    }

    static bool IsCtrlHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
    }

    void UpdateHeldDeleteInput()
    {
        if (placementGhost == null)
        {
            return;
        }

        if (IsPointerOverUi() && WasDeletePressed())
        {
            DeleteHeldParts();
        }
    }

    void FocusCameraOnPart(PlacedPart part)
    {
        if (part?.transform == null || vabCameraController == null)
        {
            return;
        }

        Bounds bounds = CalculatePartRenderBounds(part.transform.gameObject);
        vabCameraController.FocusOnPart(bounds, immediate: true, minDistance: 8f, maxDistance: 24f);
        vabCameraController.LockFocusPoint(bounds.center, immediate: true);
    }

    void RestoreDefaultCameraFocus(bool immediate = true)
    {
        if (vabCameraController == null)
        {
            return;
        }

        if (placedParts.Count > 0)
        {
            Bounds rocketBounds = CalculateRocketBounds();
            vabCameraController.FocusOnPart(
                rocketBounds,
                immediate,
                minDistance: 10f,
                maxDistance: 28f
            );
            vabCameraController.LockFocusPoint(rocketBounds.center, immediate);
            return;
        }

        vabCameraController.EnableOrbitAtFocus(GetVabWorkCenter(), immediate);
        vabCameraController.SetOrbitDistance(DefaultOrbitDistanceAtWorkCenter);
    }

    void RefreshUiVisibility()
    {
        bool showEditor = ShouldShowEditorUi();
        SetEditorVisible(showEditor);
    }

    bool ShouldShowEditorUi()
    {
        return
            sessionRunner != null &&
            sessionRunner.IsVabEditorActive &&
            !sessionRunner.IsMainMenuVisible;
    }

    void SetEditorVisible(bool visible)
    {
        if (uiCanvas != null)
        {
            uiCanvas.gameObject.SetActive(visible);
        }

        if (vesselRoot != null)
        {
            vesselRoot.gameObject.SetActive(visible);
        }

        if (visible)
        {
            vabCamera = Camera.main;

            if (vabCamera == null)
            {
                vabCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (!visible && HasActiveHold())
        {
            CancelPlacement();
        }
    }

    public static void ApplySessionVisibility(SimulationRunner runner)
    {
        bool show = runner != null && runner.IsVabEditorActive && !runner.IsMainMenuVisible;
        VabEditorController[] controllers = FindObjectsByType<VabEditorController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < controllers.Length; i++)
        {
            VabEditorController controller = controllers[i];

            if (controller != null)
            {
                controller.SetEditorVisible(show);
            }
        }

        GameObject orphanCanvas = GameObject.Find("VabEditorCanvas");

        if (orphanCanvas != null)
        {
            orphanCanvas.SetActive(show);
        }
    }

    void EnsureVabCameraReady()
    {
        if (vabCameraController == null || vesselRoot == null)
        {
            return;
        }

        vabCameraController.EnableOrbitAtFocus(GetVabWorkCenter(), immediate: true);
        vabCameraController.SetOrbitDistance(DefaultOrbitDistanceAtWorkCenter);
    }

    Vector3 GetVabWorkCenter()
    {
        EnsureBuildOriginResolved();
        return VabRootAnchor.GetWorldWorkCenter(vesselRoot);
    }

    Vector3 ClampFocusPointForVab(Vector3 focusPoint)
    {
        return VabWorkspaceBounds.ClampFocusPoint(focusPoint, GetVabWorkCenter());
    }

    void SetupFocusPlaneCursor()
    {
        GameObject cursorHost = new GameObject("VabFocusPlaneCursor");

        if (vesselRoot != null)
        {
            cursorHost.transform.SetParent(vesselRoot, worldPositionStays: true);
        }

        focusPlaneCursor = cursorHost.AddComponent<VabFocusPlaneCursor>();
    }

    void UpdateFocusPlaneCursor()
    {
        if (focusPlaneCursor == null)
        {
            return;
        }

        bool showCursor = placementGhost == null && !IsPointerOverUi();

        if (!showCursor)
        {
            focusPlaneCursor.SetVisible(false);
            return;
        }

        if (TryGetPlacementSurfacePoint(out Vector3 point))
        {
            focusPlaneCursor.SetWorldPosition(point);
            focusPlaneCursor.SetVisible(true);
            return;
        }

        focusPlaneCursor.SetVisible(false);
    }

    void ConfigurePlacementRaycastMask()
    {
        int heldLayer = LayerMask.NameToLayer(VabColliderUtility.HeldPartLayerName);

        if (heldLayer >= 0)
        {
            placementRaycastMask &= ~(1 << heldLayer);
        }

        int highlightLayer = LayerMask.NameToLayer(
            VabSelectionHighlighter.SelectionHighlightLayerName
        );

        if (highlightLayer >= 0)
        {
            placementRaycastMask &= ~(1 << highlightLayer);
        }

        int attachmentNodeLayer = LayerMask.NameToLayer(VabAttachmentNodeLayers.LayerName);

        if (attachmentNodeLayer >= 0)
        {
            placementRaycastMask &= ~(1 << attachmentNodeLayer);
            attachmentNodeMask = 1 << attachmentNodeLayer;
        }
        else
        {
            attachmentNodeMask = 0;
            Debug.LogWarning(
                "VAB: layer '" + VabAttachmentNodeLayers.LayerName +
                "' is missing. Run Space Program/VAB/Setup Attachment Node Layer."
            );
        }
    }

    void SetupVabCamera()
    {
        if (vabCamera == null)
        {
            return;
        }

        vabCameraController = vabCamera.GetComponent<VabCameraController>();

        if (vabCameraController == null)
        {
            vabCameraController = vabCamera.gameObject.AddComponent<VabCameraController>();
        }

        vabCameraController.SetInputBlockedCheck(IsPointerOverUi);
        vabCameraController.SetFocusPanClamp(ClampFocusPointForVab);
    }

    void SetupRotationGizmo()
    {
        if (rotationGizmo != null)
        {
            return;
        }

        GameObject gizmoObject = new GameObject("VabRotationGizmo");
        rotationGizmo = gizmoObject.AddComponent<VabRotationGizmo>();
        rotationGizmo.Detach();
    }

    void DetachRotationGizmo()
    {
        if (rotationGizmo != null)
        {
            rotationGizmo.Detach();
        }

        if (rotationGizmo == null)
        {
            SetupRotationGizmo();
        }
    }

    void HideImportedDecorativeSpheres()
    {
        VabDecorativeSphereUtility.HideAllImportedDecorativeSpheres();
    }

    void EnsureEventSystem()
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

    void BuildUi()
    {
        GameObject existingCanvas = GameObject.Find("VabEditorCanvas");

        if (existingCanvas != null)
        {
            Destroy(existingCanvas);
        }

        GameObject canvasObject = new GameObject("VabEditorCanvas");
        canvasObject.transform.SetParent(transform, false);
        uiCanvas = canvasObject.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.65f;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

        Image sidebarBackground = CreateLeftSidebar(
            canvasRect,
            SidebarWidth,
            new Color(0.08f, 0.1f, 0.14f, 0.94f)
        );

        RectTransform sidebar = sidebarBackground.rectTransform;

        BuildHeader(sidebar);
        BuildPartsScrollView(sidebar);
        BuildCategoryStrip(sidebar);
        BuildActionBar(sidebar);
        BuildLaunchBar(canvasRect);
        BuildPlacementStatusBar(canvasRect);
        BuildVesselStatsBar(canvasRect);
        partCatalogTooltip = VabPartCatalogTooltip.Create(canvasRect, SidebarWidth);
        BuildDebugPanel(canvasRect);
    }

    void BuildLaunchBar(RectTransform canvasRect)
    {
        GameObject launchObject = new GameObject("LaunchButton");
        launchObject.transform.SetParent(canvasRect, false);

        RectTransform launchRect = launchObject.AddComponent<RectTransform>();
        launchRect.anchorMin = new Vector2(1f, 1f);
        launchRect.anchorMax = new Vector2(1f, 1f);
        launchRect.pivot = new Vector2(1f, 1f);
        launchRect.sizeDelta = new Vector2(150f, 48f);
        launchRect.anchoredPosition = new Vector2(-12f, -12f);

        Image launchBackground = launchObject.AddComponent<Image>();
        launchBackground.color = new Color(0.18f, 0.52f, 0.28f, 1f);

        launchButton = launchObject.AddComponent<Button>();
        launchButton.onClick.AddListener(LaunchCurrentVessel);

        CreateText(
            launchRect,
            "Label",
            "Запуск",
            UiFontSizeBody,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero
        );
    }

    void BuildPlacementStatusBar(RectTransform canvasRect)
    {
        const float panelWidth = 320f;
        const float panelHeight = 88f;

        RectTransform panel = CreateRect(
            canvasRect,
            "PlacementStatusBar",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(SidebarWidth + 12f, -12f),
            new Vector2(SidebarWidth + 12f + panelWidth, -12f - panelHeight)
        );

        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.12f, 0.9f);
        background.raycastTarget = false;

        placementStatusText = CreateText(
            panel,
            "PlacementStatus",
            BuildPlacementStatusText(),
            UiFontSizeSmall,
            FontStyle.Bold,
            TextAnchor.UpperLeft,
            new Vector2(12f, 8f),
            new Vector2(-12f, -8f)
        );
        placementStatusText.color = new Color(0.86f, 0.93f, 1f, 1f);
        placementStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        placementStatusText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    void BuildVesselStatsBar(RectTransform canvasRect)
    {
        const float statsBarHeight = 52f;

        RectTransform statsBar = CreateRect(
            canvasRect,
            "VesselStatsBar",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(SidebarWidth + 8f, 8f),
            new Vector2(-12f, 8f + statsBarHeight)
        );

        Image background = statsBar.gameObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.12f, 0.9f);
        background.raycastTarget = false;

        vesselStatsText = CreateText(
            statsBar,
            "VesselStats",
            "Масса: —   TWR: —",
            UiFontSizeBody,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(12f, 4f),
            new Vector2(-12f, -4f)
        );
        vesselStatsText.color = new Color(0.88f, 0.93f, 1f, 1f);
    }

    void BuildDebugPanel(RectTransform canvasRect)
    {
        debugExpandButton = CreateOverlayButton(
            canvasRect,
            "DebugExpandButton",
            "DBG",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(52f, 36f),
            new Vector2(-10f, 0f),
            new Color(0.14f, 0.18f, 0.24f, 0.92f),
            () => SetDebugHudExpanded(true),
            true
        );

        debugHudPanel = new GameObject("DebugHud");
        debugHudPanel.transform.SetParent(canvasRect, false);

        RectTransform panelRect = debugHudPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.sizeDelta = new Vector2(340f, 420f);
        panelRect.anchoredPosition = new Vector2(-10f, 0f);

        Image background = debugHudPanel.AddComponent<Image>();
        background.color = new Color(0.04f, 0.06f, 0.1f, 0.88f);
        background.raycastTarget = true;

        debugHudText = CreateText(
            panelRect,
            "DebugText",
            string.Empty,
            UiFontSizeSmall,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Vector2(10f, 44f),
            new Vector2(-10f, -10f)
        );
        debugHudText.font = Font.CreateDynamicFontFromOSFont("Consolas", UiFontSizeSmall);
        debugHudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        debugHudText.verticalOverflow = VerticalWrapMode.Overflow;
        debugHudText.color = new Color(0.72f, 0.9f, 0.78f, 1f);
        debugHudText.raycastTarget = false;

        CreateOverlayButton(
            panelRect,
            "DebugCollapseButton",
            "Свернуть",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 36f),
            new Vector2(0f, 0f),
            new Color(0.16f, 0.2f, 0.28f, 1f),
            () => SetDebugHudExpanded(false),
            false
        );

        SetDebugHudExpanded(false);
    }

    void SetDebugHudExpanded(bool expanded)
    {
        isDebugHudExpanded = expanded;

        if (debugHudPanel != null)
        {
            debugHudPanel.SetActive(expanded);
        }

        if (debugExpandButton != null)
        {
            debugExpandButton.SetActive(!expanded);
        }

        if (expanded)
        {
            UpdateDebugHud();
        }
    }

    GameObject CreateOverlayButton(
        RectTransform parent,
        string name,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta,
        Vector2 anchoredPosition,
        Color backgroundColor,
        Action onClick,
        bool stretchWidth
    )
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;

        if (stretchWidth)
        {
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
        }
        else
        {
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, 8f + sizeDelta.y);
        }

        Image image = buttonObject.AddComponent<Image>();
        image.color = backgroundColor;

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => onClick());

        CreateText(
            rect,
            "Label",
            label,
            UiFontSizeSmall,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero
        );

        return buttonObject;
    }

    void UpdateDebugHud()
    {
        if (debugHudText == null || !isDebugHudExpanded)
        {
            return;
        }

        debugHudText.text = BuildDebugHudText();
    }

    string BuildDebugHudText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
        sb.AppendLine("VAB DEBUG");

        if (vabCamera != null)
        {
            Vector3 camPos = vabCamera.transform.position;
            sb.AppendLine("Cam: " + FormatVector(camPos));
        }

        if (vabCameraController != null)
        {
            sb.AppendLine("Focus: " + FormatVector(vabCameraController.FocusPoint));
            sb.AppendLine("Orbit: " + vabCameraController.OrbitDistance.ToString("F1") + " m");
            sb.AppendLine("CamLock: " + holdCameraLocked);
        }

        sb.AppendLine("Work: " + FormatVector(GetVabWorkCenter()));
        sb.AppendLine("PlaneY: " + ResolvePlacementPlaneY().ToString("F1") + " m");
        sb.AppendLine("VesselRoot: " + FormatVector(vesselRoot != null ? vesselRoot.position : Vector3.zero));
        sb.AppendLine("Parts: " + placedParts.Count);

        if (placementGhost?.transform != null)
        {
            sb.AppendLine("Ghost: " + FormatVector(placementGhost.transform.position));
            sb.AppendLine("Snapped: " + placementGhostSnapped);
            sb.AppendLine(
                "Attach: " + FormatPlacementAttachMode(ResolveEffectivePlacementAttachMode()) +
                (IsStrictNodeAttachHeld() ? " (Alt strict)" : string.Empty)
            );
            sb.AppendLine(
                "SymClones: surface=" + VabSnapSolverAdapter.PendingSymmetryClonePoses.Count +
                " stack=" + VabSnapSolverAdapter.PendingStackSymmetryClonePoses.Count
            );
            sb.AppendLine("Sticky: " + hasStickyNodeMatch);

            if (hasStickyNodeMatch)
            {
                sb.AppendLine(
                    "Snap→ " + stickyNodeMatch.parentBlockId +
                    " p" + stickyNodeMatch.parentNodeIndex +
                    " c" + stickyNodeMatch.childNodeIndex
                );

                PartVisualAdapter ghostAdapterForDelta =
                    placementGhost.transform.GetComponent<PartVisualAdapter>();

                if (ghostAdapterForDelta != null)
                {
                    float liveNodeDelta = Mathf.Sqrt(
                        VabNodeSnapResolver.ComputeStickyAlignmentDistanceSquared(
                            ghostAdapterForDelta,
                            BuildSnapTargets(null),
                            stickyNodeMatch
                        )
                    );
                    sb.AppendLine("NodeΔ: " + liveNodeDelta.ToString("F3") + " m");
                }
                else
                {
                    sb.AppendLine(
                        "NodeΔ: " + Mathf.Sqrt(stickyNodeMatch.distanceSquared).ToString("F3") + " m"
                    );
                }
            }

            PartVisualAdapter ghostAdapter = placementGhost.transform.GetComponent<PartVisualAdapter>();

            if (ghostAdapter != null)
            {
                sb.AppendLine("Nodes: " + ghostAdapter.NodeCount + " (0=top 1=bottom)");
            }

            if (TryGetPlacementSurfacePoint(out Vector3 planePoint))
            {
                sb.AppendLine("Plane: " + FormatVector(planePoint));
                sb.AppendLine(
                    "ΔY: " + (placementGhost.transform.position.y - planePoint.y).ToString("F1") + " m"
                );
            }
        }
        else if (selectedPart?.transform != null)
        {
            sb.AppendLine("Selected: " + selectedPart.blockId);
            sb.AppendLine("Pos: " + FormatVector(selectedPart.transform.position));
            sb.AppendLine("Disjoint: " + selectedPart.isDisjointed);
            sb.AppendLine("2×LMB = pick up | click part = select/move");
        }

        return sb.ToString();
    }

    static string FormatVector(Vector3 value)
    {
        return "(" +
            value.x.ToString("F1") + ", " +
            value.y.ToString("F1") + ", " +
            value.z.ToString("F1") + ")";
    }

    static Image CreateLeftSidebar(RectTransform parent, float width, Color color)
    {
        GameObject panelObject = new GameObject("Sidebar");
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(width, 0f);

        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    void BuildHeader(RectTransform sidebar)
    {
        RectTransform header = CreateRect(
            sidebar,
            "Header",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -CombinedHeaderHeight),
            Vector2.zero
        );

        headerLabel = CreateText(
            header,
            "Title",
            "Сборка ракеты",
            UiFontSizeTitle,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            new Vector2(HeaderSideInset, TitleBottomInset),
            new Vector2(-HeaderSideInset, -TitleTopInset)
        );
        UpdateSymmetryHeaderLabel();
        UpdatePlacementStatusUi();

        vesselNameField = CreateInputField(
            header,
            "VesselName",
            vesselName,
            new Vector2(HeaderSideInset, VesselNameBottomInset),
            new Vector2(-HeaderSideInset, -VesselNameTopInset)
        );
        vesselNameField.onEndEdit.AddListener(value =>
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                vesselName = value.Trim();
                editSession.VesselName = vesselName;
            }
        });
    }

    void BuildCategoryStrip(RectTransform sidebar)
    {
        float stripTop = CombinedHeaderHeight;
        float stripBottom = stripTop + CategoryStripHeight;

        RectTransform strip = CreateRect(
            sidebar,
            "Categories",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -stripBottom),
            new Vector2(0f, -stripTop)
        );

        Image stripBackground = strip.gameObject.AddComponent<Image>();
        stripBackground.color = new Color(0.06f, 0.08f, 0.11f, 0.92f);
        stripBackground.raycastTarget = false;

        HorizontalLayoutGroup layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(8, 8, 2, 2);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        IReadOnlyList<string> categories = PartDatabase.GetCategories();

        for (int i = 0; i < categories.Count; i++)
        {
            string category = categories[i];
            Button button = CreateCategoryButton(strip, category);
            string capturedCategory = category;
            button.onClick.AddListener(() => SelectCategory(capturedCategory));
            categoryButtons[category] = button;
        }
    }

    void BuildPartsScrollView(RectTransform sidebar)
    {
        float scrollTop = CombinedHeaderHeight + CategoryStripHeight + 4f;

        RectTransform scrollRectTransform = CreateRect(
            sidebar,
            "PartsScroll",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, ActionBarHeight),
            new Vector2(0f, -scrollTop)
        );

        ScrollRect scroll = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        Image scrollBackground = scrollRectTransform.gameObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.05f, 0.06f, 0.09f, 0.5f);

        RectTransform viewport = CreateRect(
            scrollRectTransform,
            "Viewport",
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;

        RectTransform content = CreateRect(
            viewport,
            "Content",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        GridLayoutGroup gridLayout = content.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(PartGridCellSize, PartGridCellSize);
        gridLayout.spacing = new Vector2(6f, 6f);
        gridLayout.padding = new RectOffset(8, 8, 8, 8);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = PartGridColumns;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        partsListContent = content;
    }

    void BuildActionBar(RectTransform sidebar)
    {
        RectTransform bar = CreateRect(
            sidebar,
            "ActionBar",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, ActionBarHeight)
        );

        HorizontalLayoutGroup layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateActionButton(bar, "Сохранить", BeginSaveFlow);
        CreateActionButton(bar, "Корабли", OpenShipBrowser);
        CreateActionButton(bar, "Очистить", ClearVessel);
    }

    void SelectCategory(string category)
    {
        activeCategory = category;
        RefreshCategoryHighlight();
        RebuildPartList();
    }

    void RefreshCategoryHighlight()
    {
        foreach (KeyValuePair<string, Button> entry in categoryButtons)
        {
            ColorBlock colors = entry.Value.colors;
            bool isActive = entry.Key == activeCategory;
            colors.normalColor = isActive ?
                new Color(0.22f, 0.42f, 0.62f, 1f) :
                new Color(0.14f, 0.16f, 0.2f, 1f);
            entry.Value.colors = colors;
        }
    }

    void RebuildPartList()
    {
        for (int i = partsListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(partsListContent.GetChild(i).gameObject);
        }

        List<PartTemplate> templates = PartDatabase.GetByCategory(activeCategory);

        for (int i = 0; i < templates.Count; i++)
        {
            CreatePartListButton(templates[i]);
        }
    }

    void CreatePartListButton(PartTemplate template)
    {
        GameObject buttonObject = new GameObject(template.id);
        buttonObject.transform.SetParent(partsListContent, false);

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = PartGridCellSize;
        layoutElement.minHeight = PartGridCellSize;
        layoutElement.preferredWidth = PartGridCellSize;
        layoutElement.preferredHeight = PartGridCellSize;

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.1f, 0.12f, 0.16f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.1f, 0.12f, 0.16f, 1f);
        colors.highlightedColor = new Color(0.18f, 0.24f, 0.32f, 1f);
        colors.pressedColor = new Color(0.14f, 0.2f, 0.28f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(() => BeginPlacement(template));

        RectTransform cellRect = buttonObject.GetComponent<RectTransform>();
        CreatePartGridThumbnail(cellRect, template);
        BindPartCatalogHover(buttonObject, template, cellRect);
    }

    void BindPartCatalogHover(GameObject buttonObject, PartTemplate template, RectTransform cellRect)
    {
        if (partCatalogTooltip == null)
        {
            return;
        }

        EventTrigger trigger = buttonObject.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger = buttonObject.AddComponent<EventTrigger>();
        }

        AddPointerEvent(trigger, EventTriggerType.PointerEnter, () =>
        {
            partCatalogTooltip.NotifyCellEnter(template, cellRect);
        });
        AddPointerEvent(trigger, EventTriggerType.PointerExit, () =>
        {
            partCatalogTooltip.NotifyCellExit(template);
        });
    }

    static void AddPointerEvent(EventTrigger trigger, EventTriggerType eventType, Action callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType,
        };
        entry.callback.AddListener(_ => callback());
        trigger.triggers.Add(entry);
    }

    static void CreatePartGridThumbnail(RectTransform cellRect, PartTemplate template)
    {
        PartThumbnailUi.CreateGridThumbnail(
            cellRect,
            template,
            CreateText,
            UiFontSizeIcon,
            CategoryLetter,
            CategoryColor
        );
    }

    void BeginPlacement(PartTemplate template)
    {
        CancelPlacement();
        ClearPartSelection();

        if (editSession.IsVesselEmpty)
        {
            SpawnRootPartInstant(template);
            return;
        }

        BeginPlacementGhost(template);
    }

    void SpawnRootPartInstant(PartTemplate template)
    {
        GameObject prefab = LoadPartPrefab(template.prefabPath);

        if (prefab == null)
        {
            Debug.LogError(
                "VAB: prefab not found at '" + template.prefabPath +
                "'. Run ReStock rebuild or assign prefabs in Assets/Prefabs/Parts/."
            );
            return;
        }

        string blockId = editSession.BeginHoldNewPart(template.id);
        GameObject instance = Instantiate(prefab, vesselRoot);
        instance.name = blockId;
        instance.transform.localPosition = VabRootPartPlacement.GetLocalPosition(template);
        instance.transform.localRotation = VabRootAnchor.LocalRotation;
        VabColliderUtility.EnsurePickColliders(instance, rebuild: true);
        VabColliderUtility.SetCollidersEnabled(instance, true);
        VabGhostVisualUtility.ApplyPlacedVisual(instance);
        GetOrAddPartAdapter(instance, blockId, template.id);

        PlacedPart placed = new PlacedPart
        {
            blockId = blockId,
            template = template,
            transform = instance.transform,
        };

        editSession.CommitPendingAsRoot(
            VabRootPartPlacement.GetSimLocalPosition(template),
            VabRootAnchor.SimLocalRotation
        );
        placedParts.Add(placed);
        RefreshAllPlacedPartAttachmentNodes();
        RefreshPlacedPartVisuals();
        ClearStickySnap();
        SelectPart(placed);
        RestoreDefaultCameraFocus(immediate: true);
        InvalidateVesselStats();
    }

    static bool ShouldIgnorePartRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return true;
        }

        string objectName = renderer.gameObject.name;

        if (
            objectName == "Sphere" ||
            objectName.StartsWith("Node_Index_", StringComparison.Ordinal) ||
            objectName == "thrustTransform"
        )
        {
            return true;
        }

        return renderer is LineRenderer;
    }

    static Bounds CalculatePartRenderBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || ShouldIgnorePartRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(root.transform.position, Vector3.one);
        }

        return bounds;
    }

    static Bounds CalculateWorldRenderBounds(GameObject root)
    {
        return CalculatePartRenderBounds(root);
    }

    void ResetCameraToBuildOrigin()
    {
        if (vabCameraController == null)
        {
            return;
        }

        EnsureBuildOriginResolved();
        vabCameraController.EnableOrbitAtFocus(GetVabWorkCenter(), immediate: true);
        vabCameraController.SetOrbitDistance(DefaultOrbitDistanceAtWorkCenter);
    }

    void BeginPlacementGhost(PartTemplate template)
    {
        GameObject prefab = LoadPartPrefab(template.prefabPath);

        if (prefab == null)
        {
            Debug.LogError(
                "VAB: prefab not found at '" + template.prefabPath +
                "'. Run ReStock rebuild or assign prefabs in Assets/Prefabs/Parts/."
            );
            return;
        }

        string blockId = editSession.BeginHoldNewPart(template.id);
        placementTemplate = template;
        GameObject instance = Instantiate(prefab, vesselRoot);
        instance.name = template.id + "_ghost";
        VabColliderUtility.EnsurePickColliders(instance, rebuild: true);

        placementGhost = new PlacedPart
        {
            blockId = blockId,
            template = template,
            transform = instance.transform,
        };

        isDetachedBranchHold = false;
        snapAcquireSuppress.suppressed = false;
        ClearStickySnap();
        placementGhostSnapped = false;
        PartVisualAdapter ghostAdapter = GetOrAddPartAdapter(instance, blockId, template.id);
        VabColliderUtility.SetCollidersEnabled(instance, false);
        VabGhostVisualUtility.ApplyHeldGhostVisual(instance, snapped: false);

        instance.transform.position = ResolveGhostSpawnAnchor();
        VabPartStackMetrics.AlignBottomToPlaneY(
            instance.transform,
            template,
            ResolveVesselStackPlaneY(),
            ghostAdapter
        );

        lastActiveGhostNodeIndex = VabGhostPivotUtility.ResolveDefaultHoldNodeIndex(template, ghostAdapter);

        if (TryGetPlacementSurfacePoint(out Vector3 startPoint))
        {
            TryGetPlacementMouseRay(out Ray spawnRay);
            Vector3 intentPoint = startPoint;

            if (TryGetPlacementIntentPoint(spawnRay, out Vector3 focusPoint))
            {
                intentPoint = focusPoint;
            }

            List<VabNodeSnapResolver.SnapTarget> spawnTargets = BuildSnapTargets(null);
            Vector3? meshHitPoint = null;
            RaycastHit spawnHit = default;
            bool hasSpawnHit = TryGetClosestVesselRaycastHit(
                spawnRay,
                GetRaycastIgnoreRoot(),
                PlacementRaycastMaxMeters,
                out spawnHit
            );

            if (hasSpawnHit)
            {
                meshHitPoint = spawnHit.point;
            }

            VabGhostMovementController.Context moveContext = new VabGhostMovementController.Context
            {
                ghostRoot = instance.transform,
                ghostAdapter = ghostAdapter,
                ghostTemplate = template,
                cursorWorld = startPoint,
                verticalIntentWorld = intentPoint,
                verticalIntentReferenceY = VabGhostMovementController.ResolveVerticalIntentReferenceY(
                    intentPoint,
                    spawnTargets,
                    ResolveStackReferenceY(),
                    meshHitPoint
                ),
                vesselAxisWorld = vesselRoot != null ? vesselRoot.position : GetVabWorkCenter(),
                stackReferenceY = ResolveStackReferenceY(),
                snapTargets = spawnTargets,
                attachmentNodeMask = attachmentNodeMask,
                isNodeFree = IsStackNodeFree,
                heldBlockId = blockId,
                isPlacedBlock = IsCommittedPlacedBlock,
                acquireMeters = SnapDistanceMeters,
                previewMeters = SnapPreviewMeters,
                releaseMeters = SnapReleaseDistanceMeters,
                releaseHorizontalMeters = SnapReleaseHorizontalMeters,
                releaseVerticalMeters = SnapReleaseVerticalMeters,
                lastActiveGhostNodeIndex = lastActiveGhostNodeIndex,
                hasVesselRaycastHit = hasSpawnHit,
                vesselHit = spawnHit,
                symmetryCount = currentSymmetryCount,
                stabilizeVerticalHold = false,
                holdVerticalAnchorY = 0f,
                holdIntentAnchorY = 0f,
                attachMode = ResolveEffectivePlacementAttachMode(),
                strictNodeAttach = IsStrictNodeAttachHeld(),
                stackApproachMeters = ResolveStackApproachMeters(ghostAdapter, template),
                resolveSymmetricStackParents = ResolveSymmetricStackParents,
                snapAcquireSuppress = snapAcquireSuppress,
            };

            VabNodeSnapResolver.NodeSnapMatch spawnSticky = default;
            bool hasSpawnSticky = false;
            VabGhostMovementController.Result spawnResult = VabGhostMovementController.HandleGhostMovement(
                moveContext,
                ref spawnSticky,
                ref hasSpawnSticky,
                default,
                false
            );
            lastActiveGhostNodeIndex = spawnResult.activeGhostNodeIndex;
        }

        AttachRotationGizmo(GetPlacementRotationTarget(), template);
        RefreshPlacedPartVisuals();
        LockCameraForHold();
    }

    void CancelPlacementIfActive()
    {
        if (!HasActiveHold())
        {
            return;
        }

        CancelPlacement();
    }

    void BeginDetachSinglePart(PlacedPart part)
    {
        if (part == null || placementGhost != null)
        {
            return;
        }

        CancelPlacementIfActive();
        ClearPartSelectionHighlight();
        selectedPart = part;

        PlacedPart previousParent = part.parent;
        List<string> heldIds = editSession.BeginHoldSinglePart(part.blockId);

        if (heldIds == null || heldIds.Count == 0)
        {
            return;
        }

        ReparentChildrenInScene(part, previousParent);

        isDetachedBranchHold = true;
        detachedBranchWasConnected =
            previousParent != null &&
            CollectConnectedParts(FindVesselRootPart()).Contains(part);
        heldBranchParts.Clear();
        heldBranchParts.Add(part);

        detachedFromParent = previousParent;
        detachedFromNodeIndex = part.attachedToNodeIndex;
        detachedLocalPosition = part.transform.localPosition;
        detachedLocalRotation = part.transform.localRotation;

        Vector3 detachPivotWorld = GetDetachPivotWorldPosition(part);
        holdPivot.SetPositionAndRotation(detachPivotWorld, part.transform.rotation);
        part.transform.SetParent(holdPivot, worldPositionStays: true);

        placementTemplate = part.template;
        placementGhost = part;

        part.parent = null;
        part.attachedToNodeIndex = VabAttachmentIndices.Unset;
        part.sourceNodeIndex = VabAttachmentIndices.Unset;

        VabColliderUtility.SetCollidersEnabled(part.transform.gameObject, false);
        VabGhostVisualUtility.ApplyHeldGhostVisual(part.transform.gameObject, snapped: false);
        CaptureDetachVerticalAnchors();
        AttachRotationGizmo(holdPivot, part.template);
        LockCameraForHold();
    }

    void BeginHoldDisjointedPart(PlacedPart part)
    {
        if (part == null || placementGhost != null)
        {
            return;
        }

        CancelPlacementIfActive();

        if (!editSession.IsDisjointedBlock(part.blockId) && part.isDisjointed)
        {
            editSession.MarkBlockDisjointed(
                part.blockId,
                ToSim(part.transform.localPosition),
                ToSim(part.transform.localRotation)
            );
        }

        List<string> heldIds = editSession.BeginHoldDisjointedPart(part.blockId);

        if (heldIds == null || heldIds.Count == 0)
        {
            return;
        }

        isDetachedBranchHold = false;
        heldBranchParts.Clear();
        placementTemplate = part.template;
        placementGhost = part;

        VabColliderUtility.SetCollidersEnabled(part.transform.gameObject, false);
        VabGhostVisualUtility.ApplyHeldGhostVisual(part.transform.gameObject, snapped: false);
        AttachRotationGizmo(part.transform, part.template);
        LockCameraForHold();
    }

    void ReparentChildrenInScene(PlacedPart detachedPart, PlacedPart newParent)
    {
        Transform parentTransform = newParent?.transform ?? vesselRoot;

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart child = placedParts[i];

            if (child?.parent == detachedPart)
            {
                child.parent = newParent;
                child.transform.SetParent(parentTransform, worldPositionStays: true);

                if (editSession.Blocks.TryGetValue(child.blockId, out BlockSnapshot childSnapshot))
                {
                    childSnapshot.parentBlockId = newParent != null ? newParent.blockId : string.Empty;
                }
            }
        }
    }

    void BeginDetachBranch(PlacedPart rootPart)
    {
        if (rootPart == null || placementGhost != null)
        {
            return;
        }

        CancelPlacementIfActive();
        ClearPartSelectionHighlight();
        selectedPart = rootPart;

        List<PlacedPart> branchParts = CollectBranchParts(rootPart);
        List<string> branchIds = editSession.BeginHoldDetachedBranch(rootPart.blockId);

        if (branchIds == null || branchIds.Count == 0)
        {
            Debug.LogWarning("VAB: cannot pick up branch for '" + rootPart.blockId + "'.");
            return;
        }

        isDetachedBranchHold = true;
        detachedBranchWasConnected = CollectConnectedParts(FindVesselRootPart()).Contains(rootPart);
        heldBranchParts.Clear();
        heldBranchParts.AddRange(branchParts);

        detachedFromParent = rootPart.parent;
        detachedFromNodeIndex = rootPart.attachedToNodeIndex;
        detachedLocalPosition = rootPart.transform.localPosition;
        detachedLocalRotation = rootPart.transform.localRotation;

        Vector3 detachPivotWorld = GetDetachPivotWorldPosition(rootPart);
        holdPivot.SetPositionAndRotation(detachPivotWorld, rootPart.transform.rotation);
        rootPart.transform.SetParent(holdPivot, worldPositionStays: true);

        placementTemplate = rootPart.template;
        placementGhost = rootPart;

        for (int i = 0; i < branchParts.Count; i++)
        {
            VabColliderUtility.SetCollidersEnabled(branchParts[i].transform.gameObject, false);
            VabGhostVisualUtility.ApplyHeldGhostVisual(branchParts[i].transform.gameObject, snapped: false);
        }

        CaptureDetachVerticalAnchors();
        AttachRotationGizmo(holdPivot, rootPart.template);
        LockCameraForHold();
    }

    Vector3 GetDetachPivotWorldPosition(PlacedPart rootPart)
    {
        if (
            rootPart?.parent == null ||
            rootPart.attachedToNodeIndex < 0
        )
        {
            return rootPart.transform.position;
        }

        PartVisualAdapter parentAdapter = rootPart.parent.transform.GetComponent<PartVisualAdapter>();

        if (
            parentAdapter != null &&
            parentAdapter.TryGetNodeWorldPosition(rootPart.attachedToNodeIndex, out Vector3 nodeWorld)
        )
        {
            return nodeWorld;
        }

        return rootPart.transform.position;
    }

    void CaptureDetachVerticalAnchors()
    {
        detachHoldVerticalAnchorY = holdPivot != null ?
            holdPivot.position.y :
            0f;

        if (TryGetPlacementMouseRay(out Ray ray) && TryGetPlacementIntentPoint(ray, out Vector3 intentPoint))
        {
            detachIntentAnchorY = intentPoint.y;
            return;
        }

        detachIntentAnchorY = detachHoldVerticalAnchorY;
    }

    void UpdatePlacementGhostMovement(Vector3 lerpFromWorld, float lerpT)
    {
        if (placementGhost == null || vabCamera == null || placementTemplate == null)
        {
            return;
        }

        if (lerpT < 1f)
        {
            if (TryResolveGhostTargetPosition(out Vector3 targetWorld, out _, out _))
            {
                GetPlacementTransform().position = Vector3.Lerp(lerpFromWorld, targetWorld, lerpT);
            }

            UpdateGhostVisuals(false);
            return;
        }

        UpdatePlacementGhost();
    }

    void UpdatePlacementGhost()
    {
        if (placementGhost == null || vabCamera == null || placementTemplate == null)
        {
            return;
        }

        if (!TryResolveGhostTargetPosition(
                out Vector3 targetWorld,
                out Quaternion targetRotation,
                out bool snapped
            ))
        {
            UpdateGhostVisuals(false);
            return;
        }

        Transform placementTransform = GetPlacementTransform();

        if (isDetachedBranchHold)
        {
            holdPivot.SetPositionAndRotation(targetWorld, targetRotation);
        }
        else
        {
            placementTransform.SetPositionAndRotation(targetWorld, targetRotation);
        }

        UpdateGhostVisuals(snapped);
    }

    bool TryResolveGhostTargetPosition(
        out Vector3 targetWorld,
        out Quaternion targetRotation,
        out bool snapped
    )
    {
        targetWorld = GetPlacementTransform().position;
        targetRotation = GetPlacementTransform().rotation;
        snapped = false;

        if (IsCtrlHeld() && hasStickyNodeMatch)
        {
            ClearStickySnap(armAcquireSuppress: true);
        }

        if (!TryGetPlacementMouseRay(out Ray ray))
        {
            return false;
        }

        if (!TryGetPlacementSurfacePoint(out Vector3 surfacePoint))
        {
            return false;
        }

        if (!TryGetPlacementIntentPoint(ray, out Vector3 intentPoint))
        {
            intentPoint = surfacePoint;
        }

        VabGhostMovementController.UpdateSnapAcquireSuppress(
            snapAcquireSuppress,
            surfacePoint,
            intentPoint,
            SnapPreviewMeters
        );

        bool hasRaycastHit = TryGetClosestVesselRaycastHit(
            ray,
            GetRaycastIgnoreRoot(),
            PlacementRaycastMaxMeters,
            out RaycastHit placementHit
        );

        if (placementGhost?.transform == null || placementTemplate == null)
        {
            return false;
        }

        PartVisualAdapter adapter = GetOrAddPartAdapter(
            placementGhost.transform.gameObject,
            placementGhost.blockId,
            placementTemplate.id
        );

        Transform placementTransform = GetPlacementTransform();

        HashSet<string> excludedIds = null;

        if (isDetachedBranchHold && editSession.Pending != null)
        {
            excludedIds = new HashSet<string>(editSession.Pending.heldBlockIds);
        }

        List<VabNodeSnapResolver.SnapTarget> snapTargets = BuildSnapTargets(excludedIds);

        Vector3? meshHitPoint = hasRaycastHit ? placementHit.point : (Vector3?)null;
        float verticalIntentReferenceY = VabGhostMovementController.ResolveVerticalIntentReferenceY(
            intentPoint,
            snapTargets,
            ResolveStackReferenceY(),
            meshHitPoint
        );

        VabGhostMovementController.Context moveContext = new VabGhostMovementController.Context
        {
            ghostRoot = placementTransform,
            ghostAdapter = adapter,
            ghostTemplate = placementTemplate,
            cursorWorld = surfacePoint,
            verticalIntentWorld = intentPoint,
            verticalIntentReferenceY = verticalIntentReferenceY,
            vesselAxisWorld = vesselRoot != null ? vesselRoot.position : GetVabWorkCenter(),
            stackReferenceY = ResolveStackReferenceY(),
            snapTargets = snapTargets,
            attachmentNodeMask = attachmentNodeMask,
            isNodeFree = IsStackNodeFree,
            heldBlockId = placementGhost.blockId,
            isPlacedBlock = IsCommittedPlacedBlock,
            acquireMeters = SnapDistanceMeters,
            previewMeters = SnapPreviewMeters,
            releaseMeters = SnapReleaseDistanceMeters,
            releaseHorizontalMeters = SnapReleaseHorizontalMeters,
            releaseVerticalMeters = SnapReleaseVerticalMeters,
            lastActiveGhostNodeIndex = lastActiveGhostNodeIndex,
            hasVesselRaycastHit = hasRaycastHit,
            vesselHit = placementHit,
            symmetryCount = currentSymmetryCount,
            stabilizeVerticalHold = isDetachedBranchHold,
            holdVerticalAnchorY = detachHoldVerticalAnchorY,
            holdIntentAnchorY = detachIntentAnchorY,
            attachMode = ResolveEffectivePlacementAttachMode(),
            strictNodeAttach = IsStrictNodeAttachHeld(),
            stackApproachMeters = ResolveStackApproachMeters(adapter, placementTemplate),
            resolveSymmetricStackParents = ResolveSymmetricStackParents,
            forceUnsnap = IsCtrlHeld(),
            snapAcquireSuppress = snapAcquireSuppress,
        };

        VabGhostMovementController.Result moveResult = VabGhostMovementController.HandleGhostMovement(
            moveContext,
            ref stickyNodeMatch,
            ref hasStickyNodeMatch,
            snapLatchCursorPoint,
            hasSnapPlacementLatch
        );

        lastActiveGhostNodeIndex = moveResult.activeGhostNodeIndex;

        if (moveResult.snapped)
        {
            if (!placementGhostSnapped)
            {
                UpdateSnapPlacementLatch(surfacePoint);
            }
        }
        else if (
            hasSnapPlacementLatch &&
            ShouldReleaseSnapPlacementLatch(surfacePoint, intentPoint)
        )
        {
            ClearStickySnap(armAcquireSuppress: true);
        }

        targetWorld = moveResult.worldPosition;
        targetRotation = moveResult.worldRotation;
        targetWorld = ClampGhostHorizontalDistance(targetWorld);
        snapped = moveResult.snapped;
        return true;
    }

    float ResolveStackReferenceY()
    {
        PlacedPart rootPart = FindVesselRootPart();

        if (rootPart?.transform != null)
        {
            return rootPart.transform.position.y;
        }

        return GetVabWorkCenter().y;
    }

    float ResolveVesselStackPlaneY()
    {
        if (GetSnapEligiblePlacedCount() == 0)
        {
            return GetVabWorkCenter().y;
        }

        return ResolveStackReferenceY();
    }

    Vector3 ResolveGhostSpawnAnchor()
    {
        Vector3 workCenter = GetVabWorkCenter();

        if (GetSnapEligiblePlacedCount() == 0)
        {
            return workCenter;
        }

        PlacedPart rootPart = FindVesselRootPart();

        if (rootPart?.transform != null)
        {
            return new Vector3(workCenter.x, rootPart.transform.position.y, workCenter.z);
        }

        return workCenter;
    }

    Vector3 ClampPlacementCursorNearVessel(Vector3 cursorWorld)
    {
        if (GetSnapEligiblePlacedCount() == 0)
        {
            return cursorWorld;
        }

        Vector3 center = GetVabWorkCenter();
        Vector3 offset = cursorWorld - center;
        offset.y = 0f;
        float maxRadiusSquared = MaxPlacementCursorRadiusMeters * MaxPlacementCursorRadiusMeters;

        if (offset.sqrMagnitude > maxRadiusSquared)
        {
            offset = offset.normalized * MaxPlacementCursorRadiusMeters;
        }

        Vector3 clamped = center + offset;
        clamped.y = cursorWorld.y;
        return clamped;
    }

    float ResolvePlacementPlaneY()
    {
        if (placementGhost != null && (hasStickyNodeMatch || hasSnapPlacementLatch))
        {
            if (TryResolveFocusPlaneY(out float focusY))
            {
                return focusY;
            }
        }

        if (placementGhost != null && hasStickyNodeMatch)
        {
            return stickyNodeMatch.worldPosition.y;
        }

        if (hasSnapPlacementLatch)
        {
            return snapLatchPlaneY;
        }

        if (placementGhost != null && GetSnapEligiblePlacedCount() > 0)
        {
            Vector3 workCenter = GetVabWorkCenter();
            Vector3 ghostPosition = GetPlacementTransform().position;
            Vector3 flatDelta = ghostPosition - workCenter;
            flatDelta.y = 0f;
            float stableRadiusSquared =
                GhostStablePlaneMaxHorizontalMeters * GhostStablePlaneMaxHorizontalMeters;

            if (flatDelta.sqrMagnitude <= stableRadiusSquared)
            {
                return ghostPosition.y;
            }

            return ResolveVesselStackPlaneY();
        }

        float? emptyVesselFaceY = null;

        if (GetSnapEligiblePlacedCount() == 0)
        {
            if (
                TryGetPlacementMouseRay(out Ray ray) &&
                TryGetClosestVesselRaycastHit(
                    ray,
                    GetRaycastIgnoreRoot(),
                    PlacementRaycastMaxMeters,
                    out RaycastHit hit
                ) &&
                Mathf.Abs(hit.normal.y) > 0.85f
            )
            {
                emptyVesselFaceY = hit.point.y;
            }
        }

        float? focusPlaneY = null;

        if (
            TryGetPlacementMouseRay(out Ray focusRay) &&
            vabCamera != null &&
            vabCameraController != null &&
            VabFocusPlane.TryRaycast(
                vabCamera,
                vabCameraController.FocusPoint,
                focusRay,
                out Vector3 focusHit
            )
        )
        {
            focusPlaneY = focusHit.y;
        }

        return VabStackPlacementHeight.ResolvePlacementPlaneY(
            hasStickyNodeMatch,
            stickyNodeMatch.worldPosition.y,
            hasSnapPlacementLatch,
            snapLatchPlaneY,
            GetSnapEligiblePlacedCount(),
            GetVabWorkCenter().y,
            emptyVesselFaceY,
            focusPlaneY
        );
    }

    bool IsCommittedPlacedBlock(string blockId)
    {
        PlacedPart part = FindPlacedPart(blockId);

        if (part == null)
        {
            return false;
        }

        if (isDetachedBranchHold && heldBranchParts.Contains(part))
        {
            return false;
        }

        return part != placementGhost;
    }

    List<VabNodeSnapResolver.SnapTarget> BuildSnapTargets(HashSet<string> excludedIds)
    {
        List<VabNodeSnapResolver.SnapTarget> targets = new List<VabNodeSnapResolver.SnapTarget>();
        SyncPlacedPartLinksFromSession();
        HashSet<PlacedPart> connectedParts = CollectConnectedParts(FindVesselRootPart());

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part == null || part == placementGhost || !connectedParts.Contains(part))
            {
                continue;
            }

            if (excludedIds != null && excludedIds.Contains(part.blockId))
            {
                continue;
            }

            if (isDetachedBranchHold && heldBranchParts.Contains(part))
            {
                continue;
            }

            PartVisualAdapter adapter = part.transform.GetComponent<PartVisualAdapter>();

            if (adapter == null)
            {
                continue;
            }

            targets.Add(new VabNodeSnapResolver.SnapTarget
            {
                blockId = part.blockId,
                transform = part.transform,
                adapter = adapter,
            });
        }

        return targets;
    }

    PlacedPart FindVesselRootPart()
    {
        if (editSession?.Blocks != null)
        {
            foreach (KeyValuePair<string, BlockSnapshot> entry in editSession.Blocks)
            {
                BlockSnapshot snapshot = entry.Value;

                if (
                    snapshot == null ||
                    snapshot.isDisjointed ||
                    !string.IsNullOrEmpty(snapshot.parentBlockId)
                )
                {
                    continue;
                }

                PlacedPart part = FindPlacedPart(snapshot.blockId);

                if (part != null)
                {
                    return part;
                }
            }
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part?.parent != null || part?.template == null || string.IsNullOrEmpty(part.blockId))
            {
                continue;
            }

            if (!part.isDisjointed)
            {
                return part;
            }
        }

        return null;
    }

    void SyncPlacedPartLinksFromSession()
    {
        if (editSession?.Blocks == null)
        {
            return;
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (
                part == null ||
                string.IsNullOrEmpty(part.blockId) ||
                part == placementGhost ||
                (isDetachedBranchHold && heldBranchParts.Contains(part))
            )
            {
                continue;
            }

            if (!editSession.Blocks.TryGetValue(part.blockId, out BlockSnapshot snapshot))
            {
                continue;
            }

            if (snapshot.isDisjointed || string.IsNullOrEmpty(snapshot.parentBlockId))
            {
                part.parent = null;
                part.attachedToNodeIndex = VabAttachmentIndices.Unset;
                part.sourceNodeIndex = VabAttachmentIndices.Unset;
                continue;
            }

            PlacedPart sessionParent = FindPlacedPart(snapshot.parentBlockId);

            if (
                sessionParent == null ||
                !IsPhysicallyAttachedToParent(part, sessionParent)
            )
            {
                editSession.MarkBlockDisjointed(
                    part.blockId,
                    ToSim(part.transform.localPosition),
                    ToSim(part.transform.localRotation)
                );
                part.parent = null;
                part.attachedToNodeIndex = VabAttachmentIndices.Unset;
                part.sourceNodeIndex = VabAttachmentIndices.Unset;
                continue;
            }

            part.parent = sessionParent;
            part.attachedToNodeIndex = snapshot.attachedToNodeIndex;
            part.sourceNodeIndex = InferSourceNodeIndex(snapshot.attachedToNodeIndex);
        }
    }

    static bool IsPhysicallyAttachedToParent(PlacedPart child, PlacedPart parentPart)
    {
        if (child?.transform == null || parentPart?.transform == null)
        {
            return false;
        }

        return child.transform.parent == parentPart.transform;
    }

    HashSet<PlacedPart> CollectConnectedParts(PlacedPart rootPart)
    {
        HashSet<PlacedPart> connected = new HashSet<PlacedPart>();

        if (rootPart == null || string.IsNullOrEmpty(rootPart.blockId))
        {
            return connected;
        }

        if (editSession == null)
        {
            connected.Add(rootPart);
            return connected;
        }

        if (editSession.IsDisjointedBlock(rootPart.blockId))
        {
            return connected;
        }

        connected.Add(rootPart);
        Queue<string> pendingBlockIds = new Queue<string>();
        pendingBlockIds.Enqueue(rootPart.blockId);

        while (pendingBlockIds.Count > 0)
        {
            string parentBlockId = pendingBlockIds.Dequeue();
            PlacedPart parentPart = FindPlacedPart(parentBlockId);

            if (parentPart == null)
            {
                continue;
            }

            if (editSession.Blocks == null)
            {
                break;
            }

            foreach (KeyValuePair<string, BlockSnapshot> entry in editSession.Blocks)
            {
                BlockSnapshot snapshot = entry.Value;

                if (
                    snapshot == null ||
                    snapshot.isDisjointed ||
                    snapshot.parentBlockId != parentBlockId
                )
                {
                    continue;
                }

                PlacedPart childPart = FindPlacedPart(snapshot.blockId);

                if (childPart == null || connected.Contains(childPart))
                {
                    continue;
                }

                if (!IsPhysicallyAttachedToParent(childPart, parentPart))
                {
                    continue;
                }

                childPart.parent = parentPart;
                childPart.attachedToNodeIndex = snapshot.attachedToNodeIndex;
                childPart.sourceNodeIndex = InferSourceNodeIndex(snapshot.attachedToNodeIndex);
                connected.Add(childPart);
                pendingBlockIds.Enqueue(snapshot.blockId);
            }
        }

        return connected;
    }

    void RefreshPlacedPartVisuals()
    {
        SyncPlacedPartLinksFromSession();
        PlacedPart rootPart = FindVesselRootPart();
        HashSet<PlacedPart> connectedParts = CollectConnectedParts(rootPart);

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part?.transform == null)
            {
                continue;
            }

            if (
                part == placementGhost ||
                (isDetachedBranchHold && heldBranchParts.Contains(part))
            )
            {
                continue;
            }

            bool isConnected = connectedParts.Contains(part);
            part.isDisjointed = !isConnected;

            PartVisualAdapter adapter = part.transform.GetComponent<PartVisualAdapter>();

            if (adapter != null)
            {
                adapter.IsDisjointed = !isConnected;
            }

            if (isConnected)
            {
                VabGhostVisualUtility.ApplyPlacedVisual(part.transform.gameObject);
            }
            else
            {
                VabGhostVisualUtility.ApplyDisjointedVisual(part.transform.gameObject);
            }
        }
    }

    void RefreshAllPlacedPartAttachmentNodes()
    {
        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part?.transform == null || string.IsNullOrEmpty(part.blockId) || part.template == null)
            {
                continue;
            }

            PartPrefabBuilder.RestoreAttachmentNodeLayers(part.transform.gameObject, part.blockId);

            PartVisualAdapter adapter = part.transform.GetComponent<PartVisualAdapter>();

            if (adapter != null)
            {
                adapter.Bind(part.blockId, part.template.id);
            }
        }
    }

    bool IsStackNodeFree(VabNodeSnapResolver.SnapTarget target, int nodeIndex)
    {
        if (nodeIndex < 0)
        {
            return false;
        }

        PlacedPart part = FindPlacedPart(target.blockId);

        if (part == null)
        {
            return false;
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart child = placedParts[i];

            if (child == null || child == placementGhost)
            {
                continue;
            }

            if (
                child.parent == part &&
                child.attachedToNodeIndex == nodeIndex &&
                child.attachedToNodeIndex != VabAttachmentIndices.SurfaceAttach
            )
            {
                return false;
            }
        }

        if (
            part.sourceNodeIndex == nodeIndex &&
            part.sourceNodeIndex != VabAttachmentIndices.Unset &&
            part.parent != null
        )
        {
            return false;
        }

        return true;
    }

    Vector3 ClampGhostHorizontalDistance(Vector3 worldPosition)
    {
        return VabWorkspaceBounds.ClampHorizontal(worldPosition, GetVabWorkCenter());
    }

    Transform GetPlacementTransform()
    {
        return isDetachedBranchHold ? holdPivot : placementGhost.transform;
    }

    Transform GetPlacementRotationTarget()
    {
        return isDetachedBranchHold ? holdPivot : placementGhost.transform;
    }

    int GetSnapEligiblePlacedCount()
    {
        SyncPlacedPartLinksFromSession();
        HashSet<PlacedPart> connectedParts = CollectConnectedParts(FindVesselRootPart());
        int count = 0;

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part == null || !connectedParts.Contains(part))
            {
                continue;
            }

            if (isDetachedBranchHold && heldBranchParts.Contains(part))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    void UpdateGhostVisuals(bool snapped)
    {
        placementGhostSnapped = snapped;

        if (isDetachedBranchHold)
        {
            for (int i = 0; i < heldBranchParts.Count; i++)
            {
                PlacedPart part = heldBranchParts[i];

                if (part?.transform != null)
                {
                    VabGhostVisualUtility.ApplyHeldGhostVisual(part.transform.gameObject, snapped);
                }
            }

            return;
        }

        if (placementGhost?.transform != null)
        {
            VabGhostVisualUtility.ApplyHeldGhostVisual(placementGhost.transform.gameObject, snapped);
        }
    }

    void ConfirmPlacement()
    {
        if (placementGhost == null || placementTemplate == null)
        {
            return;
        }

        PlacedPart placed = placementGhost;
        Transform placementTransform = GetPlacementTransform();
        int placedCount = GetSnapEligiblePlacedCount();
        GetPlacementSnapProbe(out Vector3 snapProbePosition, out Quaternion snapProbeRotation);

        PartVisualAdapter placementAdapter = placementGhost?.transform != null ?
            placementGhost.transform.GetComponent<PartVisualAdapter>() :
            null;

        SnapResult snap = default;
        bool hasSnap = false;
        bool isSurfaceAttach = false;
        PlacedPart surfaceParent = null;

        if (!editSession.IsVesselEmpty && placedCount > 0 && placementGhostSnapped)
        {
            if (
                placementAdapter != null &&
                placementAdapter.TargetParentNodeIndex == VabAttachmentIndices.SurfaceAttach &&
                !string.IsNullOrEmpty(placementAdapter.TargetParentBlockId)
            )
            {
                surfaceParent = FindPlacedPart(placementAdapter.TargetParentBlockId);

                if (surfaceParent != null)
                {
                    hasSnap = true;
                    isSurfaceAttach = true;
                }
            }
            else if (TryBuildSnapResult(stickyNodeMatch, out snap))
            {
                hasSnap = true;
            }
            else if (
                placementAdapter != null &&
                !string.IsNullOrEmpty(placementAdapter.TargetParentBlockId) &&
                placementAdapter.TargetParentNodeIndex >= 0
            )
            {
                VabNodeSnapResolver.NodeSnapMatch adapterMatch = new VabNodeSnapResolver.NodeSnapMatch
                {
                    parentBlockId = placementAdapter.TargetParentBlockId,
                    parentNodeIndex = placementAdapter.TargetParentNodeIndex,
                    childNodeIndex = placementAdapter.TargetChildNodeIndex,
                    worldPosition = placed.transform.position,
                    worldRotation = placed.transform.rotation,
                };
                hasSnap = TryBuildSnapResult(adapterMatch, out snap);
            }
        }

        if (hasSnap && !isSurfaceAttach && snap.parentPart != null)
        {
            PartVisualAdapter parentAdapter =
                snap.parentPart.transform.GetComponent<PartVisualAdapter>();

            VabNodeSnapResolver.SnapTarget snapTarget = new VabNodeSnapResolver.SnapTarget
            {
                blockId = snap.parentPart.blockId,
                transform = snap.parentPart.transform,
                adapter = parentAdapter,
            };

            if (!IsStackNodeFree(snapTarget, snap.parentNodeIndex))
            {
                hasSnap = false;
            }
        }

        if (isDetachedBranchHold)
        {
            ConfirmDetachedBranchPlacement(placed, snap, hasSnap);
            return;
        }

        if (editSession.IsVesselEmpty)
        {
            placed.transform.SetParent(vesselRoot, worldPositionStays: false);
            placed.transform.localPosition = VabRootPartPlacement.GetLocalPosition(placementTemplate);
            placed.transform.localRotation = VabRootAnchor.LocalRotation;

            editSession.CommitPendingAsRoot(
                VabRootPartPlacement.GetSimLocalPosition(placementTemplate),
                VabRootAnchor.SimLocalRotation
            );
            FinalizePlacement(placed);
            return;
        }

        if (!hasSnap)
        {
            ConfirmDisjointedPlacement(placed, placementTransform);
            return;
        }

        if (isSurfaceAttach)
        {
            ConfirmSurfacePlacement(placed, surfaceParent);
            return;
        }

        PlacedPart parentPart = snap.parentPart;
        placed.parent = parentPart;
        placed.attachedToNodeIndex = snap.parentNodeIndex;
        placed.sourceNodeIndex = snap.childNodeIndex;
        placed.isDisjointed = false;

        placed.transform.SetParent(parentPart.transform, worldPositionStays: true);
        SimVector3 localPosition = ToSim(placed.transform.localPosition);
        SimQuaternion localRotation = ToSim(placed.transform.localRotation);

        editSession.TryCommitHold(
            new VabEditSession.HoldCommitRequest
            {
                parentBlockId = parentPart.blockId,
                parentNodeIndex = snap.parentNodeIndex,
                localPosition = localPosition,
                localRotation = localRotation,
                symmetryStackCommits = BuildStackSymmetryCommits(),
            },
            out List<BlockSnapshot> created
        );

        FinalizePlacement(placed);

        if (created == null)
        {
            return;
        }

        for (int i = 1; i < created.Count; i++)
        {
            PlacedPart cloneParent = FindPlacedPart(created[i].parentBlockId);

            if (cloneParent != null)
            {
                SpawnCommittedPart(created[i], cloneParent, snap.childNodeIndex);
            }
        }
    }

    void ConfirmDisjointedPlacement(PlacedPart placed, Transform placementTransform)
    {
        placed.transform.SetParent(vesselRoot, worldPositionStays: true);
        placed.transform.position = placementTransform.position;
        placed.transform.rotation = placementTransform.rotation;

        SimVector3 localPosition = ToSim(placed.transform.localPosition);
        SimQuaternion localRotation = ToSim(placed.transform.localRotation);

        editSession.TryCommitHold(
            new VabEditSession.HoldCommitRequest
            {
                parentBlockId = string.Empty,
                localPosition = localPosition,
                localRotation = localRotation,
            },
            out _
        );

        placed.parent = null;
        placed.attachedToNodeIndex = VabAttachmentIndices.Unset;
        placed.sourceNodeIndex = VabAttachmentIndices.Unset;
        placed.isDisjointed = true;

        VabSnapSolverAdapter.ClearSymmetryGhosts();
        FinalizePlacement(placed, disjointed: true);
    }

    void ConfirmSurfacePlacement(PlacedPart placed, PlacedPart parentPart)
    {
        SimVector3 localPosition = WorldToLocalPosition(
            parentPart,
            ToSim(placed.transform.position)
        );
        SimQuaternion localRotation = WorldToLocalRotation(
            parentPart,
            ToSim(placed.transform.rotation)
        );

        placed.parent = parentPart;
        placed.attachedToNodeIndex = VabAttachmentIndices.SurfaceAttach;
        placed.sourceNodeIndex = VabAttachmentIndices.Unset;
        placed.isDisjointed = false;

        placed.transform.SetParent(parentPart.transform, worldPositionStays: false);
        placed.transform.localPosition = ToUnity(localPosition);
        placed.transform.localRotation = ToUnity(localRotation);

        List<(SimVector3 localPosition, SimQuaternion localRotation)> symmetryLocals =
            BuildSymmetryLocalPoses();

        editSession.TryCommitHold(
            new VabEditSession.HoldCommitRequest
            {
                parentBlockId = parentPart.blockId,
                parentNodeIndex = VabAttachmentIndices.SurfaceAttach,
                localPosition = localPosition,
                localRotation = localRotation,
                symmetryLocals = symmetryLocals,
            },
            out List<BlockSnapshot> created
        );

        VabSnapSolverAdapter.ClearSymmetryGhosts();
        FinalizePlacement(placed);

        if (created == null)
        {
            return;
        }

        for (int i = 1; i < created.Count; i++)
        {
            SpawnCommittedPart(created[i], parentPart);
        }
    }

    List<VabEditSession.StackSymmetryCommit> BuildStackSymmetryCommits()
    {
        List<VabEditSession.StackSymmetryCommit> commits =
            new List<VabEditSession.StackSymmetryCommit>();

        IReadOnlyList<VabSnapSolverAdapter.StackSymmetryClonePose> clones =
            VabSnapSolverAdapter.PendingStackSymmetryClonePoses;

        for (int i = 0; i < clones.Count; i++)
        {
            VabSnapSolverAdapter.StackSymmetryClonePose clone = clones[i];
            commits.Add(new VabEditSession.StackSymmetryCommit
            {
                parentBlockId = clone.parentBlockId,
                parentNodeIndex = clone.parentNodeIndex,
                localPosition = clone.localPosition,
                localRotation = clone.localRotation,
            });
        }

        return commits;
    }

    IReadOnlyList<PlacedPart> ResolveSymmetricStackParents(VabNodeSnapResolver.NodeSnapMatch match)
    {
        PlacedPart anchorParent = FindPlacedPart(match.parentBlockId);
        return VabRadialSymmetryUtility.FindSymmetricParents(
            anchorParent,
            placedParts,
            currentSymmetryCount
        );
    }

    List<(SimVector3 localPosition, SimQuaternion localRotation)> BuildSymmetryLocalPoses()
    {
        List<(SimVector3 localPosition, SimQuaternion localRotation)> symmetryLocals =
            new List<(SimVector3 localPosition, SimQuaternion localRotation)>();

        IReadOnlyList<VabSnapSolverAdapter.SymmetryClonePose> clones =
            VabSnapSolverAdapter.PendingSymmetryClonePoses;

        for (int i = 0; i < clones.Count; i++)
        {
            symmetryLocals.Add((clones[i].localPosition, clones[i].localRotation));
        }

        return symmetryLocals;
    }

    void SpawnCommittedPart(BlockSnapshot snapshot, PlacedPart parentPart, int sourceNodeIndex = -1)
    {
        PartTemplate template = PartDatabase.GetById(snapshot.partTemplateId);

        if (template == null)
        {
            Debug.LogWarning(
                "VAB: unknown part template '" + snapshot.partTemplateId + "' for symmetry clone."
            );
            return;
        }

        GameObject prefab = LoadPartPrefab(template.prefabPath);

        if (prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = snapshot.blockId;
        instance.transform.SetParent(parentPart.transform, worldPositionStays: false);
        instance.transform.localPosition = ToUnity(snapshot.localPosition);
        instance.transform.localRotation = ToUnity(snapshot.localRotation);

        PlacedPart placed = new PlacedPart
        {
            blockId = snapshot.blockId,
            template = template,
            transform = instance.transform,
            parent = parentPart,
            attachedToNodeIndex = snapshot.attachedToNodeIndex,
            sourceNodeIndex = sourceNodeIndex >= 0 ?
                sourceNodeIndex :
                VabAttachmentIndices.Unset,
        };

        VabColliderUtility.EnsurePickColliders(instance, rebuild: true);
        VabColliderUtility.SetCollidersEnabled(instance, true);
        VabColliderUtility.ClearSavedLayers(instance);
        VabGhostVisualUtility.ApplyPlacedVisual(instance);
        GetOrAddPartAdapter(instance, snapshot.blockId, template.id);
        placedParts.Add(placed);
    }

    void ConfirmDetachedBranchPlacement(PlacedPart rootPart, SnapResult snap, bool hasSnap)
    {
        if (hasSnap)
        {
            PlacedPart parentPart = snap.parentPart;
            rootPart.parent = parentPart;
            rootPart.attachedToNodeIndex = snap.parentNodeIndex;
            rootPart.sourceNodeIndex = snap.childNodeIndex;
            rootPart.isDisjointed = false;

            rootPart.transform.SetParent(parentPart.transform, worldPositionStays: false);
            SimVector3 localPosition = WorldToLocalPosition(parentPart, snap.worldPosition);
            SimQuaternion localRotation = WorldToLocalRotation(parentPart, snap.worldRotation);
            rootPart.transform.localPosition = ToUnity(localPosition);
            rootPart.transform.localRotation = ToUnity(localRotation);

            editSession.TryCommitHold(
                new VabEditSession.HoldCommitRequest
                {
                    parentBlockId = parentPart.blockId,
                    parentNodeIndex = snap.parentNodeIndex,
                    localPosition = localPosition,
                    localRotation = localRotation,
                },
                out _
            );

            List<string> branchBlockIds = new List<string>(heldBranchParts.Count);

            for (int i = 0; i < heldBranchParts.Count; i++)
            {
                PlacedPart branchPart = heldBranchParts[i];

                if (branchPart != null && !string.IsNullOrEmpty(branchPart.blockId))
                {
                    branchBlockIds.Add(branchPart.blockId);
                }
            }

            editSession.MarkBlocksConnected(branchBlockIds);
        }
        else if (detachedBranchWasConnected)
        {
            ConfirmRelocatedBranchPlacement(rootPart);
        }
        else
        {
            ConfirmDisjointedDetachedBranch(rootPart);
        }

        for (int i = 0; i < heldBranchParts.Count; i++)
        {
            PlacedPart part = heldBranchParts[i];

            if (part?.transform != null)
            {
                VabColliderUtility.SetCollidersEnabled(part.transform.gameObject, true);
            }
        }

        bool preserveConnectedBranch = detachedBranchWasConnected;
        heldBranchParts.Clear();
        isDetachedBranchHold = false;
        detachedBranchWasConnected = false;
        detachedFromParent = null;
        FinalizePlacement(rootPart, disjointed: !hasSnap && !preserveConnectedBranch);
    }

    void ConfirmRelocatedBranchPlacement(PlacedPart rootPart)
    {
        Transform sceneParent = detachedFromParent?.transform ?? vesselRoot;
        rootPart.transform.SetParent(sceneParent, worldPositionStays: true);
        rootPart.parent = detachedFromParent;
        rootPart.attachedToNodeIndex = detachedFromNodeIndex;

        editSession.CancelPending();
        SyncBranchSnapshotsFromScene(heldBranchParts);
    }

    void SyncBranchSnapshotsFromScene(IReadOnlyList<PlacedPart> branchParts)
    {
        List<string> blockIds = new List<string>();

        for (int i = 0; i < branchParts.Count; i++)
        {
            PlacedPart part = branchParts[i];

            if (part == null || string.IsNullOrEmpty(part.blockId))
            {
                continue;
            }

            blockIds.Add(part.blockId);

            if (!editSession.Blocks.TryGetValue(part.blockId, out BlockSnapshot snapshot))
            {
                continue;
            }

            snapshot.isDisjointed = false;
            snapshot.parentBlockId = part.parent != null ? part.parent.blockId : string.Empty;
            snapshot.attachedToNodeIndex = part.parent != null ?
                part.attachedToNodeIndex :
                VabAttachmentIndices.Unset;
            snapshot.localPosition = ToSim(part.transform.localPosition);
            snapshot.localRotation = ToSim(part.transform.localRotation);
        }

        editSession.MarkBlocksConnected(blockIds);
    }

    void ConfirmDisjointedDetachedBranch(PlacedPart rootPart)
    {
        rootPart.transform.SetParent(vesselRoot, worldPositionStays: true);
        editSession.MarkBlockDisjointed(
            rootPart.blockId,
            ToSim(rootPart.transform.localPosition),
            ToSim(rootPart.transform.localRotation)
        );
        editSession.CancelPending();

        rootPart.parent = null;
        rootPart.attachedToNodeIndex = VabAttachmentIndices.Unset;
        rootPart.isDisjointed = true;

        for (int i = 0; i < heldBranchParts.Count; i++)
        {
            PlacedPart part = heldBranchParts[i];

            if (part == null || part == rootPart)
            {
                continue;
            }

            part.transform.SetParent(vesselRoot, worldPositionStays: true);
            part.parent = null;
            part.isDisjointed = true;

            editSession.MarkBlockDisjointed(
                part.blockId,
                ToSim(part.transform.localPosition),
                ToSim(part.transform.localRotation)
            );
        }
    }

    void FinalizePlacement(PlacedPart placed, bool disjointed = false)
    {
        placed.isDisjointed = disjointed;
        placed.transform.gameObject.name = placed.blockId;

        PartVisualAdapter adapter = GetOrAddPartAdapter(
            placed.transform.gameObject,
            placed.blockId,
            placed.template.id
        );
        adapter.IsDisjointed = disjointed;

        VabColliderUtility.EnsurePickColliders(placed.transform.gameObject, rebuild: true);
        VabColliderUtility.SetCollidersEnabled(placed.transform.gameObject, true);
        VabColliderUtility.ClearSavedLayers(placed.transform.gameObject);

        if (!placedParts.Contains(placed))
        {
            placedParts.Add(placed);
        }

        placementGhost = null;
        placementTemplate = null;
        isDetachedBranchHold = false;
        detachedBranchWasConnected = false;
        heldBranchParts.Clear();

        RefreshAllPlacedPartAttachmentNodes();
        RefreshPlacedPartVisuals();

        EnsureSelectionHighlighter(placed);

        DetachRotationGizmo();
        ClearStickySnap();
        VabSnapSolverAdapter.ClearSymmetryGhosts();
        UnlockCameraAfterHold();
        SelectPart(placed);
        InvalidateVesselStats();
    }

    void SelectPart(PlacedPart part)
    {
        if (selectedPart == part)
        {
            return;
        }

        ClearPartSelectionHighlight();
        selectedPart = part;

        if (selectedPart?.transform == null)
        {
            return;
        }

        selectedHighlighter = EnsureSelectionHighlighter(selectedPart);
        selectedHighlighter.SetSelected(true);
    }

    void ClearPartSelection()
    {
        ClearPartSelectionHighlight();
        selectedPart = null;
    }

    void ClearPartSelectionHighlight()
    {
        if (selectedHighlighter != null)
        {
            selectedHighlighter.SetSelected(false);
            selectedHighlighter = null;
        }
    }

    static VabSelectionHighlighter EnsureSelectionHighlighter(PlacedPart part)
    {
        if (part?.transform == null)
        {
            return null;
        }

        VabSelectionHighlighter highlighter =
            part.transform.GetComponent<VabSelectionHighlighter>();

        if (highlighter == null)
        {
            highlighter = part.transform.gameObject.AddComponent<VabSelectionHighlighter>();
        }

        return highlighter;
    }

    static bool WasLeftMousePressed()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    void CancelPlacement()
    {
        VabEditSession.HoldMode holdMode = editSession?.Pending?.mode ?? VabEditSession.HoldMode.None;

        if (holdMode == VabEditSession.HoldMode.DetachedBranch ||
            holdMode == VabEditSession.HoldMode.SinglePart ||
            isDetachedBranchHold)
        {
            RestoreDetachedBranch();
            return;
        }

        if (holdMode == VabEditSession.HoldMode.DisjointedPart)
        {
            ReleaseDisjointedHold();
            return;
        }

        if (placementGhost != null && placementGhost.transform != null)
        {
            DetachRotationGizmo();
            VabColliderUtility.ClearSavedLayers(placementGhost.transform.gameObject);
            Destroy(placementGhost.transform.gameObject);
        }

        ClearActiveHoldState();
        RefreshPlacedPartVisuals();
        RestorePartSelectionHighlight();
    }

    void ReleaseDisjointedHold()
    {
        if (placementGhost?.transform != null)
        {
            VabColliderUtility.SetCollidersEnabled(placementGhost.transform.gameObject, true);
        }

        ClearActiveHoldState();
        RefreshPlacedPartVisuals();
        RestorePartSelectionHighlight();
    }

    void ClearActiveHoldState()
    {
        editSession.CancelPending();
        heldBranchParts.Clear();
        isDetachedBranchHold = false;
        detachedFromParent = null;
        placementGhost = null;
        placementTemplate = null;
        ClearStickySnap();
        VabSnapSolverAdapter.ClearSymmetryGhosts();
        placementGhostSnapped = false;
        DetachRotationGizmo();
        UnlockCameraAfterHold();
    }

    void LockCameraForHold()
    {
        if (vabCameraController == null)
        {
            return;
        }

        vabCameraController.LockFocusPoint(vabCameraController.FocusPoint, immediate: true);
        holdCameraLocked = true;
    }

    void UnlockCameraAfterHold()
    {
        holdCameraLocked = false;
    }

    Bounds CalculateRocketBounds()
    {
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part?.transform == null || part.isDisjointed)
            {
                continue;
            }

            Bounds partBounds = CalculatePartRenderBounds(part.transform.gameObject);

            if (!hasBounds)
            {
                bounds = partBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(partBounds.min);
                bounds.Encapsulate(partBounds.max);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(GetVabWorkCenter(), Vector3.one);
        }

        return bounds;
    }

    void AlignGhostPivotToSurfacePoint(
        Vector3 surfacePoint,
        Vector3 surfaceNormal,
        PartVisualAdapter adapter,
        Transform placementTransform
    )
    {
        int attachNodeIndex = FindAttachNodeForSurface(adapter, surfaceNormal);

        if (
            attachNodeIndex < 0 ||
            !adapter.TryGetNodeLocalPosition(attachNodeIndex, out Vector3 localNode)
        )
        {
            placementTransform.position = surfacePoint;
            return;
        }

        Vector3 worldNodeOffset = placementTransform.rotation * localNode;
        placementTransform.position = surfacePoint - worldNodeOffset;
    }

    int FindAttachNodeForSurface(PartVisualAdapter adapter, Vector3 surfaceNormal)
    {
        if (adapter == null || adapter.NodeCount == 0)
        {
            return -1;
        }

        Vector3 normal = surfaceNormal.sqrMagnitude > 1e-6f ?
            surfaceNormal.normalized :
            Vector3.up;

        int bestIndex = -1;
        float bestScore = float.MinValue;

        for (int i = 0; i < adapter.NodeCount; i++)
        {
            if (!adapter.TryGetNodeWorldDirection(i, out Vector3 nodeDirection))
            {
                continue;
            }

            float score = Vector3.Dot(nodeDirection.normalized, -normal);

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    bool TryGetClosestVesselRaycastHit(
        Ray ray,
        Transform ignoreRoot,
        float maxDistance,
        out RaycastHit hit
    )
    {
        hit = default;

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maxDistance,
            placementRaycastMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidate = hits[i];

            if (ignoreRoot != null &&
                VabColliderUtility.IsUnderTransform(candidate.transform, ignoreRoot))
            {
                continue;
            }

            PartVisualAdapter adapter = candidate.collider.GetComponentInParent<PartVisualAdapter>();

            if (adapter == null || string.IsNullOrEmpty(adapter.BlockId))
            {
                continue;
            }

            PlacedPart placedPart = FindPlacedPart(adapter.BlockId);

            if (
                placedPart == null ||
                placedPart == placementGhost ||
                placedPart.isDisjointed ||
                (isDetachedBranchHold && heldBranchParts.Contains(placedPart))
            )
            {
                continue;
            }

            if (candidate.distance < bestDistance)
            {
                hit = candidate;
                bestDistance = candidate.distance;
                found = true;
            }
        }

        return found;
    }

    void RestorePartSelectionHighlight()
    {
        if (selectedPart?.transform == null)
        {
            return;
        }

        selectedHighlighter = EnsureSelectionHighlighter(selectedPart);
        selectedHighlighter.SetSelected(true);
    }

    void RestoreDetachedBranchSession(PlacedPart rootPart)
    {
        if (!editSession.Blocks.TryGetValue(rootPart.blockId, out BlockSnapshot snapshot))
        {
            return;
        }

        snapshot.parentBlockId = detachedFromParent != null ?
            detachedFromParent.blockId :
            string.Empty;
        snapshot.attachedToNodeIndex = detachedFromNodeIndex;
        snapshot.localPosition = ToSim(detachedLocalPosition);
        snapshot.localRotation = ToSim(detachedLocalRotation);
    }

    void RestoreDetachedBranch()
    {
        if (placementGhost == null)
        {
            return;
        }

        PlacedPart rootPart = placementGhost;
        RestoreDetachedBranchSession(rootPart);
        Transform parentTransform = detachedFromParent != null ?
            detachedFromParent.transform :
            vesselRoot;

        rootPart.transform.SetParent(parentTransform, worldPositionStays: false);
        rootPart.transform.localPosition = detachedLocalPosition;
        rootPart.transform.localRotation = detachedLocalRotation;
        rootPart.parent = detachedFromParent;
        rootPart.attachedToNodeIndex = detachedFromNodeIndex;

        for (int i = 0; i < heldBranchParts.Count; i++)
        {
            PlacedPart part = heldBranchParts[i];

            if (part?.transform != null)
            {
                VabColliderUtility.SetCollidersEnabled(part.transform.gameObject, true);
            }
        }

        editSession.CancelPending();
        heldBranchParts.Clear();
        isDetachedBranchHold = false;
        detachedFromParent = null;
        placementGhost = null;
        placementTemplate = null;
        DetachRotationGizmo();
        RefreshPlacedPartVisuals();
        RestorePartSelectionHighlight();
        UnlockCameraAfterHold();
    }

    void DeletePlacedBranch(PlacedPart rootPart)
    {
        if (rootPart == null)
        {
            return;
        }

        List<PlacedPart> branchParts = CollectBranchParts(rootPart);
        List<string> blockIds = editSession.CollectSubtreeBlockIds(rootPart.blockId);

        for (int i = branchParts.Count - 1; i >= 0; i--)
        {
            PlacedPart part = branchParts[i];

            if (part?.transform != null)
            {
                Destroy(part.transform.gameObject);
            }

            placedParts.Remove(part);
        }

        editSession.DeleteBlocks(blockIds);

        if (selectedPart != null && branchParts.Contains(selectedPart))
        {
            ClearPartSelection();
        }

        RefreshPlacedPartVisuals();
        InvalidateVesselStats();
    }

    void DeleteHeldParts()
    {
        if (placementGhost == null)
        {
            return;
        }

        if (isDetachedBranchHold)
        {
            List<string> blockIds = editSession.Pending?.heldBlockIds;

            for (int i = heldBranchParts.Count - 1; i >= 0; i--)
            {
                PlacedPart part = heldBranchParts[i];

                if (part?.transform != null)
                {
                    VabColliderUtility.ClearSavedLayers(part.transform.gameObject);
                    Destroy(part.transform.gameObject);
                }

                placedParts.Remove(part);
            }

            if (blockIds != null)
            {
                editSession.DeleteBlocks(blockIds);
            }

            heldBranchParts.Clear();
            isDetachedBranchHold = false;
            detachedFromParent = null;
        }
        else if (placementGhost.transform != null)
        {
            VabColliderUtility.ClearSavedLayers(placementGhost.transform.gameObject);
            Destroy(placementGhost.transform.gameObject);
        }

        editSession.CancelPending();
        placementGhost = null;
        placementTemplate = null;
        placementGhostSnapped = false;
        DetachRotationGizmo();
    }

    void ClearVessel()
    {
        CancelPlacement();
        ClearPartSelection();

        for (int i = placedParts.Count - 1; i >= 0; i--)
        {
            if (placedParts[i].transform != null)
            {
                Destroy(placedParts[i].transform.gameObject);
            }
        }

        placedParts.Clear();
        editSession.Clear();
        ResetCameraToBuildOrigin();
        lastStatsRevision = -1;
        RefreshVesselStats();
    }

    void BeginSaveFlow()
    {
        if (editSession == null || editSession.IsVesselEmpty)
        {
            Debug.LogWarning("VAB save: vessel is empty.");
            return;
        }

        shipBrowser?.Open();

        string suggestedName = vesselNameField != null ?
            vesselNameField.text :
            vesselName;
        saveShipDialog?.Open(suggestedName, OnVesselSavedFromDialog);
    }

    void OnVesselSavedFromDialog(string savedName)
    {
        ApplyVesselName(savedName);
        PerformSaveVehicle();
        shipBrowser?.RefreshIfOpen();
    }

    void ApplyVesselName(string name)
    {
        vesselName = string.IsNullOrWhiteSpace(name) ? "Untitled" : name.Trim();
        editSession.VesselName = vesselName;

        if (vesselNameField != null)
        {
            vesselNameField.text = vesselName;
        }
    }

    void PerformSaveVehicle()
    {
        VesselBlueprint blueprint = BuildBlueprint();
        VabFileSystem.SaveVehicle(blueprint);
        VabShipBrowserUi.InvalidateThumbnailCache(vesselName);

        if (saveThumbnailRoutine != null)
        {
            StopCoroutine(saveThumbnailRoutine);
        }

        saveThumbnailRoutine = StartCoroutine(
            VabVesselThumbnailCapture.CaptureAndSaveCoroutine(
                vesselRoot,
                vesselName,
                OnVesselThumbnailSaved
            )
        );

        Debug.Log("VAB: saved vessel '" + vesselName + "' to " + VabFileSystem.ShipsDirectory);
    }

    void OnVesselThumbnailSaved()
    {
        shipBrowser?.RefreshIfOpen();
    }

    void SaveCurrentVessel()
    {
        ApplyVesselName(vesselNameField != null ? vesselNameField.text : vesselName);
        PerformSaveVehicle();
    }

    void OpenShipBrowser()
    {
        shipBrowser?.Open();
    }

    public void LoadVesselFromBrowser(string name)
    {
        LoadVessel(name);
    }

    void LaunchCurrentVessel()
    {
        if (sessionRunner == null)
        {
            sessionRunner = FindFirstObjectByType<SimulationRunner>();
        }

        if (sessionRunner == null)
        {
            Debug.LogWarning("VAB launch: SimulationRunner not found.");
            return;
        }

        if (editSession == null || editSession.IsVesselEmpty)
        {
            Debug.LogWarning("VAB launch: vessel is empty.");
            return;
        }

        VabVesselStats stats = AnalyzeCurrentVessel();

        if (!stats.HasEngines)
        {
            Debug.LogWarning("VAB launch: no engines on vessel.");
            return;
        }

        if (!stats.CanLiftOff)
        {
            Debug.LogWarning(
                "VAB launch: TWR " + stats.thrustToWeightRatio.ToString("F2") +
                " < 1.0 — rocket may not lift off."
            );
        }

        if (!string.IsNullOrWhiteSpace(vesselNameField.text))
        {
            ApplyVesselName(vesselNameField.text);
        }

        SaveCurrentVessel();
        VesselBlueprint blueprint = BuildBlueprint();
        int blockCount = blueprint?.blocks != null ? blueprint.blocks.Count : 0;
        Debug.Log(
            "VAB launch: starting pad launch for '" + blueprint.vesselName +
            "' with " + blockCount + " blueprint block(s)."
        );
        sessionRunner.LaunchVesselFromVab(blueprint);
    }

    VabVesselStats AnalyzeCurrentVessel()
    {
        List<VabPartPhysicsStats> connectedParts = new List<VabPartPhysicsStats>();
        SyncPlacedPartLinksFromSession();
        HashSet<PlacedPart> connected = CollectConnectedParts(FindVesselRootPart());

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part == null || part.template == null || !connected.Contains(part))
            {
                continue;
            }

            PartTemplate template = ResolvePartTemplate(part);
            connectedParts.Add(VabBlueprintExporter.StatsFromPartTemplate(template));
        }

        return VabVesselAnalyzer.AnalyzeConnectedParts(
            connectedParts,
            LandingConfig.GetSurfaceStartGravity()
        );
    }

    static PartTemplate ResolvePartTemplate(PlacedPart part)
    {
        if (part?.template == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(part.template.id))
        {
            PartTemplate fromDatabase = PartDatabase.GetById(part.template.id);

            if (fromDatabase != null)
            {
                return fromDatabase;
            }
        }

        return part.template;
    }

    int ComputeVesselStatsRevision()
    {
        int revision = 17;

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part == null)
            {
                continue;
            }

            revision = revision * 31 + (part.blockId?.GetHashCode() ?? 0);
            revision = revision * 31 + part.isDisjointed.GetHashCode();
            revision = revision * 31 + (part.template?.id?.GetHashCode() ?? 0);
            revision = revision * 31 + (part.parent?.blockId?.GetHashCode() ?? 0);
        }

        return revision;
    }

    void InvalidateVesselStats()
    {
        lastStatsRevision = -1;
        RefreshVesselStats();
    }

    void RefreshVesselStatsIfNeeded()
    {
        int revision = ComputeVesselStatsRevision();

        if (revision == lastStatsRevision)
        {
            return;
        }

        lastStatsRevision = revision;
        RefreshVesselStats();
    }

    void RefreshVesselStats()
    {
        if (vesselStatsText == null)
        {
            return;
        }

        VabVesselStats stats = AnalyzeCurrentVessel();

        if (stats.connectedPartCount == 0)
        {
            vesselStatsText.text = "Масса: —   TWR: —";
            vesselStatsText.color = new Color(0.88f, 0.93f, 1f, 1f);

            if (launchButton != null)
            {
                launchButton.interactable = false;
            }

            return;
        }

        double massTonnes = stats.totalMassKg / 1000.0;
        string twrText = stats.HasEngines ?
            stats.thrustToWeightRatio.ToString("F2") :
            "—";

        vesselStatsText.text =
            "Масса: " + massTonnes.ToString("F2") + " т" +
            "   TWR: " + twrText;

        if (stats.HasEngines)
        {
            vesselStatsText.color = stats.CanLiftOff ?
                new Color(0.45f, 0.95f, 0.55f, 1f) :
                new Color(1f, 0.55f, 0.45f, 1f);
        }
        else
        {
            vesselStatsText.color = new Color(1f, 0.75f, 0.45f, 1f);
        }

        if (launchButton != null)
        {
            launchButton.interactable = stats.HasEngines;
        }
    }

    void LoadVessel(string name)
    {
        VesselBlueprint blueprint = VabFileSystem.LoadVehicle(name);
        ClearVessel();
        vesselName = blueprint.vesselName;
        vesselNameField.text = vesselName;
        editSession.ReplaceBlueprint(blueprint);

        if (blueprint.blocks == null || blueprint.blocks.Count == 0)
        {
            return;
        }

        Dictionary<string, PlacedPart> lookup = new Dictionary<string, PlacedPart>();

        for (int pass = 0; pass < blueprint.blocks.Count; pass++)
        {
            bool anySpawned = false;

            for (int i = 0; i < blueprint.blocks.Count; i++)
            {
                BlockSnapshot snapshot = blueprint.blocks[i];

                if (lookup.ContainsKey(snapshot.blockId))
                {
                    continue;
                }

                PartTemplate template = PartDatabase.GetById(snapshot.partTemplateId);

                if (template == null)
                {
                    Debug.LogWarning(
                        "VAB: unknown part template '" + snapshot.partTemplateId + "'."
                    );
                    continue;
                }

                bool hasParent = !string.IsNullOrEmpty(snapshot.parentBlockId);

                if (hasParent && !lookup.ContainsKey(snapshot.parentBlockId))
                {
                    continue;
                }

                GameObject prefab = LoadPartPrefab(template.prefabPath);

                if (prefab == null)
                {
                    continue;
                }

                Transform parentTransform = hasParent ?
                    lookup[snapshot.parentBlockId].transform :
                    vesselRoot;

                GameObject instance = Instantiate(prefab, parentTransform);
                instance.name = snapshot.blockId;
                instance.transform.localPosition = ToUnity(snapshot.localPosition);
                instance.transform.localRotation = ToUnity(snapshot.localRotation);
                VabColliderUtility.EnsurePickColliders(instance, rebuild: true);
                GetOrAddPartAdapter(instance, snapshot.blockId, template.id);

                PlacedPart placed = new PlacedPart
                {
                    blockId = snapshot.blockId,
                    template = template,
                    transform = instance.transform,
                    parent = hasParent ? lookup[snapshot.parentBlockId] : null,
                    attachedToNodeIndex = snapshot.attachedToNodeIndex,
                    sourceNodeIndex = InferSourceNodeIndex(snapshot.attachedToNodeIndex),
                    isDisjointed = snapshot.isDisjointed,
                };

                lookup[snapshot.blockId] = placed;
                placedParts.Add(placed);
                anySpawned = true;
            }

            if (!anySpawned)
            {
                break;
            }
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];

            if (part?.transform == null)
            {
                continue;
            }

            VabColliderUtility.SetCollidersEnabled(part.transform.gameObject, true);
        }

        RefreshAllPlacedPartAttachmentNodes();
        RefreshPlacedPartVisuals();
        lastStatsRevision = -1;
        RefreshVesselStats();
    }

    VesselBlueprint BuildBlueprint()
    {
        editSession.VesselName = vesselName;
        return editSession.Blueprint;
    }

    struct SnapResult
    {
        public PlacedPart parentPart;
        public int parentNodeIndex;
        public int childNodeIndex;
        public SimVector3 worldPosition;
        public SimQuaternion worldRotation;
    }

    static int InferSourceNodeIndex(int parentNodeIndex)
    {
        if (parentNodeIndex < 0)
        {
            return VabAttachmentIndices.Unset;
        }

        if (parentNodeIndex == 1)
        {
            return 0;
        }

        if (parentNodeIndex == 0)
        {
            return 1;
        }

        return VabAttachmentIndices.Unset;
    }

    static PartVisualAdapter GetOrAddPartAdapter(
        GameObject partObject,
        string blockId,
        string templateId
    )
    {
        if (partObject == null)
        {
            return null;
        }

        PartVisualAdapter adapter = partObject.GetComponent<PartVisualAdapter>();

        if (adapter == null)
        {
            adapter = partObject.AddComponent<PartVisualAdapter>();
        }

        adapter.Bind(blockId, templateId);
        return adapter;
    }

    void GetPlacementSnapProbe(out Vector3 position, out Quaternion rotation)
    {
        if (placementGhost?.transform != null)
        {
            position = placementGhost.transform.position;
            rotation = placementGhost.transform.rotation;
            return;
        }

        Transform placementTransform = GetPlacementTransform();
        position = placementTransform.position;
        rotation = placementTransform.rotation;
    }

    bool TryBuildSnapResult(VabNodeSnapResolver.NodeSnapMatch candidate, out SnapResult snap)
    {
        snap = default;
        PlacedPart parentPart = FindPlacedPart(candidate.parentBlockId);

        if (parentPart == null)
        {
            return false;
        }

        snap = new SnapResult
        {
            parentPart = parentPart,
            parentNodeIndex = candidate.parentNodeIndex,
            childNodeIndex = candidate.childNodeIndex,
            worldPosition = ToSim(candidate.worldPosition),
            worldRotation = ToSim(candidate.worldRotation),
        };

        return true;
    }

    PlacedPart FindPlacedPart(string blockId)
    {
        for (int i = 0; i < placedParts.Count; i++)
        {
            if (placedParts[i].blockId == blockId)
            {
                return placedParts[i];
            }
        }

        return null;
    }

    List<PlacedPart> CollectBranchParts(PlacedPart rootPart)
    {
        List<PlacedPart> branch = new List<PlacedPart>();
        Queue<PlacedPart> queue = new Queue<PlacedPart>();
        queue.Enqueue(rootPart);

        while (queue.Count > 0)
        {
            PlacedPart current = queue.Dequeue();
            branch.Add(current);

            for (int i = 0; i < placedParts.Count; i++)
            {
                PlacedPart candidate = placedParts[i];

                if (candidate.parent == current && !branch.Contains(candidate))
                {
                    queue.Enqueue(candidate);
                }
            }
        }

        return branch;
    }

    bool TryPickPlacedPart(
        VabPartPickResolver.PickDepth depth,
        out PlacedPart pickedPart
    )
    {
        pickedPart = null;

        if (!TryGetPlacementMouseRay(out Ray ray))
        {
            return false;
        }

        return VabPartPickResolver.TryPick(
            ray,
            PlacementRaycastMaxMeters,
            placementRaycastMask,
            placedParts,
            placementGhost,
            GetRaycastIgnoreRoot(),
            depth,
            out pickedPart
        );
    }

    Transform GetRaycastIgnoreRoot()
    {
        if (isDetachedBranchHold)
        {
            return holdPivot;
        }

        return placementGhost?.transform;
    }

    static bool WasDeletePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.deleteKey.wasPressedThisFrame;
    }

    static SimVector3 WorldToLocalPosition(PlacedPart parent, SimVector3 worldPosition)
    {
        SimVector3 parentWorld = GetWorldPosition(parent);
        SimQuaternion parentRotation = GetWorldRotation(parent);
        return parentRotation.Conjugate().Rotate(worldPosition.Subtract(parentWorld));
    }

    static SimQuaternion WorldToLocalRotation(PlacedPart parent, SimQuaternion worldRotation)
    {
        SimQuaternion parentRotation = GetWorldRotation(parent);
        return SimQuaternion.Multiply(parentRotation.Conjugate(), worldRotation);
    }

    static SimVector3 GetWorldPosition(PlacedPart part)
    {
        return part == null ? SimVector3.Zero() : ToSim(part.transform.position);
    }

    static SimQuaternion GetWorldRotation(PlacedPart part)
    {
        return part == null ? SimQuaternion.Identity() : ToSim(part.transform.rotation);
    }

    void AttachRotationGizmo(Transform partTransform, PartTemplate template)
    {
        SetupRotationGizmo();

        if (rotationGizmo == null || partTransform == null)
        {
            return;
        }

        GameObject prefab = LoadPartPrefab(template.prefabPath);
        float boundsRadius = 1f;

        if (prefab != null)
        {
            Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(prefab);
            boundsRadius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        }

        rotationGizmo.AttachTo(partTransform, boundsRadius);
    }

    bool TryResolveFocusPlaneY(out float focusY)
    {
        focusY = 0f;

        if (
            !TryGetPlacementMouseRay(out Ray focusRay) ||
            vabCamera == null ||
            vabCameraController == null ||
            !VabFocusPlane.TryRaycast(
                vabCamera,
                vabCameraController.FocusPoint,
                focusRay,
                out Vector3 focusHit
            )
        )
        {
            return false;
        }

        focusY = focusHit.y;
        return true;
    }

    void ClearStickySnap(bool armAcquireSuppress = false)
    {
        if (armAcquireSuppress)
        {
            snapAcquireSuppress.suppressed = true;
            snapAcquireSuppress.origin = hasSnapPlacementLatch ?
                snapLatchCursorPoint :
                GetPlacementTransform().position;
        }

        hasStickyNodeMatch = false;
        stickyNodeMatch = default;
        lastActiveGhostNodeIndex = -1;
        ClearSnapPlacementLatch();
    }

    void UpdateSnapPlacementLatch(Vector3 cursorSurface)
    {
        snapLatchCursorPoint = cursorSurface;
        snapLatchPlaneY = cursorSurface.y;
        hasSnapPlacementLatch = true;
    }

    void ClearSnapPlacementLatch()
    {
        hasSnapPlacementLatch = false;
        snapLatchPlaneY = 0f;
        snapLatchCursorPoint = default;
    }

    bool ShouldReleaseSnapPlacementLatch(Vector3 cursorSurface, Vector3 intentPoint)
    {
        return VabGhostMovementController.HasExceededSnapRelease(
            cursorSurface,
            intentPoint,
            snapLatchCursorPoint,
            SnapReleaseHorizontalMeters,
            SnapReleaseVerticalMeters
        );
    }

    bool TryGetPlacementIntentPoint(Ray ray, out Vector3 point)
    {
        point = default;

        Vector3 focusPoint = vabCameraController != null ?
            vabCameraController.FocusPoint :
            GetVabWorkCenter();

        if (vabCamera != null && VabFocusPlane.TryRaycast(vabCamera, focusPoint, ray, out point))
        {
            point = ClampPlacementCursorNearVessel(point);
            point = ClampGhostHorizontalDistance(point);
            return true;
        }

        return false;
    }

    bool TryGetPlacementSurfacePoint(out Vector3 point, Transform ignoreRoot = null)
    {
        point = default;

        if (!TryGetPlacementMouseRay(out Ray ray))
        {
            return false;
        }

        bool hasVesselHit = TryGetClosestVesselRaycastHit(
            ray,
            ignoreRoot ?? GetRaycastIgnoreRoot(),
            PlacementRaycastMaxMeters,
            out RaycastHit vesselHit
        );
        float planeY = ResolvePlacementPlaneY();
        Vector3 focusPoint = vabCameraController != null ?
            vabCameraController.FocusPoint :
            GetVabWorkCenter();
        Vector3 vesselAxis = vesselRoot != null ? vesselRoot.position : GetVabWorkCenter();
        bool useVesselAxisFocusPlane = GetSnapEligiblePlacedCount() > 0;

        if (!VabGhostMovementController.ResolvePlacementCursorPoint(
                ray,
                vabCamera,
                focusPoint,
                vesselAxis,
                planeY,
                hasVesselHit,
                vesselHit,
                useVesselAxisFocusPlane,
                out point
            ))
        {
            return false;
        }

        point = ClampPlacementCursorNearVessel(point);
        point = ClampGhostHorizontalDistance(point);
        return true;
    }

    bool TryGetPlacementRaycastHit(out RaycastHit hit, Transform ignoreRoot = null)
    {
        return TryGetPlacementRaycastHit(out hit, ignoreRoot, PlacementRaycastMaxMeters);
    }

    bool TryGetPlacementRaycastHit(
        out RaycastHit hit,
        Transform ignoreRoot,
        float maxDistance
    )
    {
        hit = default;

        if (!TryGetPlacementMouseRay(out Ray ray))
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maxDistance,
            placementRaycastMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (ignoreRoot != null &&
                VabColliderUtility.IsUnderTransform(hits[i].transform, ignoreRoot))
            {
                continue;
            }

            hit = hits[i];
            return true;
        }

        return false;
    }

    void InitializeBuildOrigin()
    {
        VabSceneEnvironment environment = FindFirstObjectByType<VabSceneEnvironment>();

        if (environment != null && environment.TryGetBuildOrigin(out Vector3 explicitOrigin))
        {
            buildOriginPosition = explicitOrigin;
        }
        else
        {
            float heightAboveFloor = environment != null ?
                environment.BuildOriginHeightAboveFloor :
                buildOriginHeightAboveFloor;

            Vector3 floorPoint = ProbeHangarFloorPoint();
            buildOriginPosition = floorPoint + Vector3.up * heightAboveFloor;
        }

        vesselRoot.position = buildOriginPosition;
        vesselRoot.rotation = Quaternion.identity;
        buildOriginResolved = true;
    }

    Vector3 ProbeHangarFloorPoint()
    {
        Vector3 probeOrigin = vesselRoot != null ?
            vesselRoot.position :
            buildOriginPosition;
        probeOrigin.y += 24f;

        if (Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out RaycastHit downHit,
                80f,
                placementRaycastMask,
                QueryTriggerInteraction.Ignore
            ))
        {
            return downHit.point;
        }

        return probeOrigin - Vector3.up * 24f;
    }

    void EnsureBuildOriginResolved()
    {
        if (!buildOriginResolved)
        {
            InitializeBuildOrigin();
        }
    }

    bool TryGetPlacementMouseRay(out Ray ray)
    {
        ray = default;

        if (vabCamera == null || IsPointerOverUi())
        {
            return false;
        }

        if (!TryGetMouseScreenPositionInCamera(out Vector3 screenPosition))
        {
            return false;
        }

        ray = vabCamera.ScreenPointToRay(screenPosition);
        return true;
    }

    bool TryGetMouseScreenPositionInCamera(out Vector3 screenPosition)
    {
        screenPosition = default;
        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return false;
        }

        Vector2 pointer = mouse.position.ReadValue();
        Rect pixelRect = vabCamera.pixelRect;

        if (!pixelRect.Contains(pointer))
        {
            return false;
        }

        screenPosition = new Vector3(pointer.x, pointer.y, 0f);
        return true;
    }

    static Vector3 GetMouseScreenPosition()
    {
        Mouse mouse = Mouse.current;

        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            return new Vector3(position.x, position.y, 0f);
        }

        return Vector3.zero;
    }

    bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    static GameObject LoadPartPrefab(string assetPath)
    {
        GameObject prefab = FlightPartVisualFactory.LoadPrefabAtPath(assetPath);

        if (prefab != null)
        {
            return prefab;
        }

        Debug.LogWarning("VAB: prefab load failed for '" + assetPath + "'.");
        return null;
    }

    static Color CategoryColor(string category)
    {
        switch (category)
        {
            case PartDatabase.CategoryCommand:
                return new Color(0.2f, 0.72f, 0.38f, 1f);
            case PartDatabase.CategoryFuelTank:
                return new Color(0.92f, 0.58f, 0.12f, 1f);
            case PartDatabase.CategoryEngine:
                return new Color(0.82f, 0.22f, 0.22f, 1f);
            default:
                return new Color(0.45f, 0.48f, 0.52f, 1f);
        }
    }

    static SimVector3 ToSim(Vector3 value)
    {
        return new SimVector3(value.x, value.y, value.z);
    }

    static SimQuaternion ToSim(Quaternion value)
    {
        return new SimQuaternion(value.w, value.x, value.y, value.z);
    }

    static Vector3 ToUnity(SimVector3 value)
    {
        return new Vector3((float)value.x, (float)value.y, (float)value.z);
    }

    static Quaternion ToUnity(SimQuaternion value)
    {
        return new Quaternion(
            (float)value.x,
            (float)value.y,
            (float)value.z,
            (float)value.w
        );
    }

    static RectTransform CreateRect(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        GameObject rectObject = new GameObject(name);
        rectObject.transform.SetParent(parent, false);

        RectTransform rect = rectObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    static Text CreateText(
        RectTransform parent,
        string name,
        string text,
        int fontSize,
        FontStyle style,
        TextAnchor alignment,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Text label = textObject.AddComponent<Text>();
        label.font = GetUiFont();
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.text = text;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = false;
        return label;
    }

    static InputField CreateInputField(
        RectTransform parent,
        string name,
        string initialValue,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        GameObject fieldObject = new GameObject(name);
        fieldObject.transform.SetParent(parent, false);

        RectTransform rect = fieldObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image background = fieldObject.AddComponent<Image>();
        background.color = new Color(0.04f, 0.05f, 0.08f, 1f);

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(fieldObject.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);

        Text text = textObject.AddComponent<Text>();
        text.font = GetUiFont();
        text.fontSize = UiFontSizeBody;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(0.9f, 0.92f, 0.95f, 1f);
        text.supportRichText = false;

        InputField input = fieldObject.AddComponent<InputField>();
        input.textComponent = text;
        input.text = initialValue;
        return input;
    }

    static Button CreateCategoryButton(RectTransform parent, string category)
    {
        GameObject buttonObject = new GameObject(category);
        buttonObject.transform.SetParent(parent, false);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.14f, 0.16f, 0.2f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.24f, 0.3f, 0.4f, 1f);
        colors.pressedColor = new Color(0.18f, 0.24f, 0.34f, 1f);
        button.colors = colors;

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = CategoryButtonHeight;
        layoutElement.preferredHeight = CategoryButtonHeight;

        string label = CategoryLetter(category) + "  " + CategoryLabel(category);
        CreateText(
            buttonObject.GetComponent<RectTransform>(),
            "Label",
            label,
            UiFontSizeCategory,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(4f, 2f),
            new Vector2(-4f, -2f)
        );

        return button;
    }

    void CreateActionButton(RectTransform parent, string label, Action onClick)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.16f, 0.2f, 0.28f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => onClick());

        CreateText(
            buttonObject.GetComponent<RectTransform>(),
            "Label",
            label,
            UiFontSizeBody,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero
        );
    }

    static Font uiFont;

    static Font GetUiFont()
    {
        if (uiFont != null)
        {
            return uiFont;
        }

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return uiFont;
    }

    static string CategoryLetter(string category)
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

    static string CategoryLabel(string category)
    {
        switch (category)
        {
            case PartDatabase.CategoryCommand:
                return "Капсулы";
            case PartDatabase.CategoryFuelTank:
                return "Баки";
            case PartDatabase.CategoryEngine:
                return "Двигатели";
            default:
                return category;
        }
    }

    static Image CreateImage(
        RectTransform parent,
        string name,
        Color color,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Vector2 pivot
    )
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = pivot;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

}
