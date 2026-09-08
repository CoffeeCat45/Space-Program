using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Live duplicate of VAB/Options landscape sliders. Flight/map: hidden until GFX button
/// (above CoM). Sandbox: always visible in Play Mode.
/// </summary>
[DisallowMultipleComponent]
public sealed class TerrainGraphicsDebugPanel : MonoBehaviour
{
    const float LodDistanceMin = FlightPlanetTerrainTuning.LodDistanceFactorMin;
    const float LodDistanceMax = FlightPlanetTerrainTuning.LodDistanceFactorMax;
    const int LoadSpeedMin = FlightPlanetTerrainTuning.LoadSpeedMin;
    const int LoadSpeedMax = FlightPlanetTerrainTuning.LoadSpeedMax;
    const int MaxLodMin = FlightPlanetTerrainTuning.MaxLodMin;
    const float ViewConeMin = FlightPlanetTerrainTuning.ViewConeHalfAngleMin;
    const float ViewConeMax = FlightPlanetTerrainTuning.ViewConeHalfAngleMax;
    const float CloudDetailMin = FlightPlanetTerrainTuning.CloudDetailMin;
    const float CloudDetailMax = FlightPlanetTerrainTuning.CloudDetailMax;
    const float CloudCoverageMin = FlightPlanetTerrainTuning.CloudCoverageMin;
    const float CloudCoverageMax = FlightPlanetTerrainTuning.CloudCoverageMax;
    const float CloudFrequencyMin = FlightPlanetTerrainTuning.CloudFrequencyMin;
    const float CloudFrequencyMax = FlightPlanetTerrainTuning.CloudFrequencyMax;
    const float CloudClusterMin = FlightPlanetTerrainTuning.CloudClusterMin;
    const float CloudClusterMax = FlightPlanetTerrainTuning.CloudClusterMax;
    const float ExpCoverageMin = FlightPlanetTerrainTuning.ExpCloudCoverageMin;
    const float ExpCoverageMax = FlightPlanetTerrainTuning.ExpCloudCoverageMax;
    const float ExpFlowMin = FlightPlanetTerrainTuning.ExpCloudFlowMin;
    const float ExpFlowMax = FlightPlanetTerrainTuning.ExpCloudFlowMax;
    const float ExpVortexMin = FlightPlanetTerrainTuning.ExpCloudVortexMin;
    const float ExpVortexMax = FlightPlanetTerrainTuning.ExpCloudVortexMax;
    const float ExpGrainMin = FlightPlanetTerrainTuning.ExpCloudGrainMin;
    const float ExpGrainMax = FlightPlanetTerrainTuning.ExpCloudGrainMax;
    const float ExpMassScaleMin = FlightPlanetTerrainTuning.ExpCloudMassScaleMin;
    const float ExpMassScaleMax = FlightPlanetTerrainTuning.ExpCloudMassScaleMax;
    const float ExpSoftnessMin = FlightPlanetTerrainTuning.ExpCloudSoftnessMin;
    const float ExpSoftnessMax = FlightPlanetTerrainTuning.ExpCloudSoftnessMax;

    ITerrainGraphicsTuning tuning;
    GameObject canvasRoot;
    Text lodFactorValueLabel;
    Text loadSpeedValueLabel;
    Text maxLodValueLabel;
    Text viewConeValueLabel;
    Text cloudDetailValueLabel;
    Text cloudCoverageValueLabel;
    Text cloudFrequencyValueLabel;
    Text cloudClusterValueLabel;
    Text expCoverageValueLabel;
    Text expFlowValueLabel;
    Text expVortexValueLabel;
    Text expGrainValueLabel;
    Text expMassScaleValueLabel;
    Text expSoftnessValueLabel;
    Text chunkCountValueLabel;
    Slider lodFactorSlider;
    Slider loadSpeedSlider;
    Slider maxLodSlider;
    Slider viewConeSlider;
    Slider cloudDetailSlider;
    Slider cloudCoverageSlider;
    Slider cloudFrequencySlider;
    Slider cloudClusterSlider;
    Slider expCoverageSlider;
    Slider expFlowSlider;
    Slider expVortexSlider;
    Slider expGrainSlider;
    Slider expMassScaleSlider;
    Slider expSoftnessSlider;
    RectTransform panelRect;
    Text cloudRendererValueLabel;
    Text experimentalCloudHint;
    bool uiReady;
    bool userRequestedVisible;

    public bool IsUserVisible => userRequestedVisible;

    public void ToggleUserVisible()
    {
        userRequestedVisible = !userRequestedVisible;
        if (!userRequestedVisible)
        {
            FlightPlanetSphere.SetLodDebugVisible(false);
        }
    }

    void Awake()
    {
        if (!Application.isPlaying)
        {
            enabled = false;
        }
    }

    void OnEnable()
    {
        PlanetLodSettings.Changed += OnPlanetTerrainGraphicsSettingsChanged;
    }

    void OnDisable()
    {
        PlanetLodSettings.Changed -= OnPlanetTerrainGraphicsSettingsChanged;
    }

    void OnPlanetTerrainGraphicsSettingsChanged()
    {
        if (EarthSandboxScene.IsActive() || !uiReady)
        {
            return;
        }

        SyncSlidersFromTuning();
    }

    bool syncedForCurrentSession;

    void Start()
    {
        TryResolveTuning();
        BuildUi();
        SyncSlidersFromTuning();
        uiReady = true;

        if (canvasRoot != null && !ShouldShowPanel())
        {
            canvasRoot.SetActive(false);
        }
    }

    void Update()
    {
        if (canvasRoot == null)
        {
            return;
        }

        TryResolveTuning();

        bool shouldShow = ShouldShowPanel();

        if (canvasRoot.activeSelf != shouldShow)
        {
            canvasRoot.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            syncedForCurrentSession = false;
            return;
        }

        if (tuning != null && tuning.IsAvailable && !syncedForCurrentSession)
        {
            SyncSlidersFromTuning();
            syncedForCurrentSession = true;
        }

        if (uiReady)
        {
            RefreshValueLabels();
        }
    }

    void OnDestroy()
    {
        if (canvasRoot != null)
        {
            Destroy(canvasRoot);
        }
    }

    bool ShouldShowPanel()
    {
        if (EarthSandboxScene.IsActive())
        {
            return tuning != null && tuning.IsAvailable;
        }

        SimulationRunner runner = FindFirstObjectByType<SimulationRunner>();

        return
            runner != null &&
            runner.IsInitialized &&
            !runner.IsMainMenuVisible &&
            !runner.IsVabEditorActive &&
            !FlightLodLoadingOverlay.IsBlocking &&
            userRequestedVisible &&
            tuning != null &&
            tuning.IsAvailable;
    }

    bool TryResolveTuning()
    {
        if (EarthSandboxScene.IsActive())
        {
            EarthSandboxQuadtreeManager manager = GetComponent<EarthSandboxQuadtreeManager>();

            if (manager == null)
            {
                manager = FindFirstObjectByType<EarthSandboxQuadtreeManager>();
            }

            if (manager == null)
            {
                return false;
            }

            tuning = new EarthSandboxTerrainTuning(manager);
            return true;
        }

        tuning = FlightPlanetTerrainTuning.Instance;
        return true;
    }

    void BuildUi()
    {
        EnsureEventSystem();

        canvasRoot = new GameObject("TerrainGraphicsDebugCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject panelObject = CreateUiObject("TerrainGraphicsPanel", canvasRoot.transform);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.1f, 0.12f, 0.84f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        this.panelRect = panelRect;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(786f, 0f);
        panelRect.sizeDelta = new Vector2(320f, 0f);

        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 16);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UiOverlayDragHandle panelDrag = panelObject.AddComponent<UiOverlayDragHandle>();
        panelDrag.Bind(panelRect, canvas);

        string hint = EarthSandboxScene.IsActive() ?
            "Sandbox LOD tuning — live in Play Mode. Drag the title to move." :
            "Same sliders as Graphics / VAB landscape. Drag the title to move. Close with GFX.";

        CreateHeader(panelObject.transform, panelRect, canvas, font, "Terrain Graphics");
        CreateHint(panelObject.transform, font, hint);

        int maxLodMax = EarthSandboxScene.IsActive() ?
            EarthSandboxQuadtreeManager.MaxLod :
            FlightPlanetSphere.DefaultMaxLodLevel;

        lodFactorSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Detail reach (lower = farther)",
            LodDistanceMin,
            LodDistanceMax,
            false,
            out lodFactorValueLabel
        );
        lodFactorSlider.onValueChanged.AddListener(OnLodFactorSliderChanged);

        loadSpeedSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Load Speed (deeper)",
            LoadSpeedMin,
            LoadSpeedMax,
            true,
            out loadSpeedValueLabel
        );
        loadSpeedSlider.onValueChanged.AddListener(OnLoadSpeedSliderChanged);

        maxLodSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Max LOD (ground detail)",
            MaxLodMin,
            maxLodMax,
            true,
            out maxLodValueLabel
        );
        maxLodSlider.onValueChanged.AddListener(OnMaxLodSliderChanged);

        viewConeSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "View Width (look cone)",
            ViewConeMin,
            ViewConeMax,
            false,
            out viewConeValueLabel
        );
        viewConeSlider.onValueChanged.AddListener(OnViewConeSliderChanged);

        Button cloudRendererButton = CreateActionButtonRow(
            panelObject.transform,
            font,
            "Cloud renderer / рендер",
            out cloudRendererValueLabel
        );
        cloudRendererButton.onClick.AddListener(OnCloudRendererClicked);

        cloudDetailSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Cloud detail (light → rich)",
            CloudDetailMin,
            CloudDetailMax,
            false,
            out cloudDetailValueLabel
        );
        cloudDetailSlider.onValueChanged.AddListener(OnCloudDetailSliderChanged);

        cloudCoverageSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Cloud coverage / густота",
            CloudCoverageMin,
            CloudCoverageMax,
            false,
            out cloudCoverageValueLabel
        );
        cloudCoverageSlider.onValueChanged.AddListener(OnCloudCoverageSliderChanged);

        cloudFrequencySlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Cloud frequency / частота",
            CloudFrequencyMin,
            CloudFrequencyMax,
            false,
            out cloudFrequencyValueLabel
        );
        cloudFrequencySlider.onValueChanged.AddListener(OnCloudFrequencySliderChanged);

        cloudClusterSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Cloud clusters / скопления",
            CloudClusterMin,
            CloudClusterMax,
            false,
            out cloudClusterValueLabel
        );
        cloudClusterSlider.onValueChanged.AddListener(OnCloudClusterSliderChanged);

        expCoverageSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Exp coverage / густота масс",
            ExpCoverageMin,
            ExpCoverageMax,
            false,
            out expCoverageValueLabel
        );
        expCoverageSlider.onValueChanged.AddListener(OnExpCoverageSliderChanged);

        expFlowSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Exp flow / потоки",
            ExpFlowMin,
            ExpFlowMax,
            false,
            out expFlowValueLabel
        );
        expFlowSlider.onValueChanged.AddListener(OnExpFlowSliderChanged);

        expVortexSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Exp vortex / вихри",
            ExpVortexMin,
            ExpVortexMax,
            false,
            out expVortexValueLabel
        );
        expVortexSlider.onValueChanged.AddListener(OnExpVortexSliderChanged);

        expGrainSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Exp grain / кайма барашки",
            ExpGrainMin,
            ExpGrainMax,
            false,
            out expGrainValueLabel
        );
        expGrainSlider.onValueChanged.AddListener(OnExpGrainSliderChanged);

        expMassScaleSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Exp mass scale / размер масс",
            ExpMassScaleMin,
            ExpMassScaleMax,
            false,
            out expMassScaleValueLabel
        );
        expMassScaleSlider.onValueChanged.AddListener(OnExpMassScaleSliderChanged);

        expSoftnessSlider = CreateSliderRow(
            panelObject.transform,
            font,
            "Exp softness / мягкость",
            ExpSoftnessMin,
            ExpSoftnessMax,
            false,
            out expSoftnessValueLabel
        );
        expSoftnessSlider.onValueChanged.AddListener(OnExpSoftnessSliderChanged);

        experimentalCloudHint = CreateHint(
            panelObject.transform,
            font,
            "Experimental sliders: coverage, flow, vortex, grain, mass scale, softness. Simple-shell values stay saved."
        );

        GameObject chunkRow = CreateUiObject("ChunkCount", panelObject.transform);
        LayoutElement chunkLayout = chunkRow.AddComponent<LayoutElement>();
        chunkLayout.minHeight = 24f;
        chunkCountValueLabel = chunkRow.AddComponent<Text>();
        chunkCountValueLabel.font = font;
        chunkCountValueLabel.fontSize = 13;
        chunkCountValueLabel.alignment = TextAnchor.MiddleLeft;
        chunkCountValueLabel.color = new Color(0.82f, 0.9f, 1f, 0.95f);
        chunkCountValueLabel.raycastTarget = false;
        RefreshCloudLookVisibility();
    }

    void SyncSlidersFromTuning()
    {
        if (tuning == null)
        {
            return;
        }

        SetSliderWithoutNotify(lodFactorSlider, tuning.LodDistanceFactor);
        SetSliderWithoutNotify(loadSpeedSlider, tuning.LoadSpeedPerFrame);
        SetSliderWithoutNotify(maxLodSlider, tuning.MaxLod);
        SetSliderWithoutNotify(viewConeSlider, tuning.ViewConeHalfAngleDegrees);
        SetSliderWithoutNotify(cloudDetailSlider, tuning.CloudDetail);
        SetSliderWithoutNotify(cloudCoverageSlider, tuning.CloudCoverage);
        SetSliderWithoutNotify(cloudFrequencySlider, tuning.CloudFrequency);
        SetSliderWithoutNotify(cloudClusterSlider, tuning.CloudCluster);
        SetSliderWithoutNotify(expCoverageSlider, PlanetLodSettings.ExpCloudCoverage);
        SetSliderWithoutNotify(expFlowSlider, PlanetLodSettings.ExpCloudFlow);
        SetSliderWithoutNotify(expVortexSlider, PlanetLodSettings.ExpCloudVortex);
        SetSliderWithoutNotify(expGrainSlider, PlanetLodSettings.ExpCloudGrain);
        SetSliderWithoutNotify(expMassScaleSlider, PlanetLodSettings.ExpCloudMassScale);
        SetSliderWithoutNotify(expSoftnessSlider, PlanetLodSettings.ExpCloudSoftness);
        RefreshCloudLookVisibility();
        RefreshValueLabels();
    }

    void OnLodFactorSliderChanged(float value)
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.LodDistanceFactor = value;
        RefreshValueLabels();
    }

    void OnLoadSpeedSliderChanged(float value)
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.LoadSpeedPerFrame = Mathf.RoundToInt(value);
        RefreshValueLabels();
    }

    void OnMaxLodSliderChanged(float value)
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.MaxLod = Mathf.RoundToInt(value);
        RefreshValueLabels();
    }

    void OnViewConeSliderChanged(float value)
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.ViewConeHalfAngleDegrees = value;
        RefreshValueLabels();
    }

    void OnCloudDetailSliderChanged(float value)
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.CloudDetail = value;
        RefreshValueLabels();
    }

    void OnCloudCoverageSliderChanged(float value)
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.CloudCoverage = value;
        RefreshValueLabels();
    }

    void OnCloudFrequencySliderChanged(float value)
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.CloudFrequency = value;
        RefreshValueLabels();
    }

    void OnCloudClusterSliderChanged(float value)
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.CloudCluster = value;
        RefreshValueLabels();
    }

    void OnExpCoverageSliderChanged(float value)
    {
        if (!uiReady)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudCoverage(value);
        RefreshValueLabels();
    }

    void OnExpFlowSliderChanged(float value)
    {
        if (!uiReady)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudFlow(value);
        RefreshValueLabels();
    }

    void OnExpVortexSliderChanged(float value)
    {
        if (!uiReady)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudVortex(value);
        RefreshValueLabels();
    }

    void OnExpGrainSliderChanged(float value)
    {
        if (!uiReady)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudGrain(value);
        RefreshValueLabels();
    }

    void OnExpMassScaleSliderChanged(float value)
    {
        if (!uiReady)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudMassScale(value);
        RefreshValueLabels();
    }

    void OnExpSoftnessSliderChanged(float value)
    {
        if (!uiReady)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudSoftness(value);
        RefreshValueLabels();
    }

    void OnCloudRendererClicked()
    {
        if (!uiReady || tuning == null)
        {
            return;
        }

        tuning.CloudRenderer = NextCloudRenderer(tuning.CloudRenderer);
        RefreshCloudLookVisibility();
        RefreshValueLabels();
    }

    static CloudRendererMode NextCloudRenderer(CloudRendererMode current)
    {
        if (current == CloudRendererMode.SimpleShells)
        {
            return CloudRendererMode.Experimental;
        }

        if (current == CloudRendererMode.Experimental)
        {
            return CloudRendererMode.NasaPng;
        }

        if (current == CloudRendererMode.NasaPng)
        {
            return CloudRendererMode.NasaPngEnhanced;
        }

        return CloudRendererMode.SimpleShells;
    }

    static string CloudRendererLabel(CloudRendererMode mode)
    {
        if (mode == CloudRendererMode.SimpleShells)
        {
            return "Simple shells / простые оболочки";
        }

        if (mode == CloudRendererMode.NasaPng)
        {
            return "NASA PNG / снимок";
        }

        if (mode == CloudRendererMode.NasaPngEnhanced)
        {
            return "NASA PNG+ / живой снимок";
        }

        return "Experimental / экспериментальный";
    }

    void RefreshCloudLookVisibility()
    {
        CloudRendererMode mode = tuning == null
            ? PlanetLodSettings.CloudRenderer
            : tuning.CloudRenderer;
        bool simpleLook = mode == CloudRendererMode.SimpleShells;
        bool experimentalLook = mode == CloudRendererMode.Experimental;
        bool nasaLook = mode == CloudRendererMode.NasaPng
            || mode == CloudRendererMode.NasaPngEnhanced;

        SetSliderBlockActive(cloudDetailSlider, !nasaLook);
        SetSliderBlockActive(cloudCoverageSlider, simpleLook);
        SetSliderBlockActive(cloudFrequencySlider, simpleLook);
        SetSliderBlockActive(cloudClusterSlider, simpleLook);
        SetSliderBlockActive(expCoverageSlider, experimentalLook);
        SetSliderBlockActive(expFlowSlider, experimentalLook);
        SetSliderBlockActive(expVortexSlider, experimentalLook);
        SetSliderBlockActive(expGrainSlider, experimentalLook);
        SetSliderBlockActive(expMassScaleSlider, experimentalLook);
        SetSliderBlockActive(expSoftnessSlider, experimentalLook);

        if (experimentalCloudHint != null)
        {
            experimentalCloudHint.gameObject.SetActive(experimentalLook || nasaLook);
            experimentalCloudHint.text = mode == CloudRendererMode.NasaPngEnhanced
                ? "NASA PNG+: та же clouds-alpha.png. Ветер на восток, два слоя без двоения."
                : mode == CloudRendererMode.NasaPng
                    ? "NASA PNG: StreamingAssets/MapMode/clouds-alpha.png. Equirect, geographic UV, без ветра."
                    : "Experimental sliders: coverage, flow, vortex, grain, mass scale, softness. Simple-shell values stay saved.";
        }

        if (panelRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }
    }

    void RefreshValueLabels()
    {
        if (tuning == null)
        {
            return;
        }

        lodFactorValueLabel.text =
            "Detail reach: " + tuning.LodDistanceFactor.ToString("F2") +
            " (↓ spreads detail farther)";
        loadSpeedValueLabel.text =
            "Load Speed: " + tuning.LoadSpeedPerFrame + " splits/meshes per frame";
        maxLodValueLabel.text = "Max LOD: " + tuning.MaxLod;
        viewConeValueLabel.text =
            "View Width: unused for LOD (frustum culls draw)";
        if (cloudDetailValueLabel != null && cloudDetailValueLabel.gameObject.activeInHierarchy)
        {
            cloudDetailValueLabel.text =
                "Cloud detail: " + tuning.CloudDetail.ToString("F2") +
                " (↓ lighter / faster)";
        }
        if (cloudRendererValueLabel != null)
        {
            cloudRendererValueLabel.text = CloudRendererLabel(tuning.CloudRenderer);
        }
        if (cloudCoverageValueLabel != null && cloudCoverageValueLabel.gameObject.activeInHierarchy)
        {
            cloudCoverageValueLabel.text =
                "Cloud coverage: " + tuning.CloudCoverage.ToString("F2") +
                " (thin → thick)";
        }
        if (cloudFrequencyValueLabel != null && cloudFrequencyValueLabel.gameObject.activeInHierarchy)
        {
            cloudFrequencyValueLabel.text =
                "Cloud frequency: " + tuning.CloudFrequency.ToString("F2") +
                " (coarse → fine)";
        }
        if (cloudClusterValueLabel != null && cloudClusterValueLabel.gameObject.activeInHierarchy)
        {
            cloudClusterValueLabel.text =
                "Cloud clusters: " + tuning.CloudCluster.ToString("F2") +
                " (even → patchy)";
        }
        if (expCoverageValueLabel != null && expCoverageValueLabel.gameObject.activeInHierarchy)
        {
            expCoverageValueLabel.text =
                "Exp coverage: " + PlanetLodSettings.ExpCloudCoverage.ToString("F2") +
                " (more → larger masses)";
        }
        if (expFlowValueLabel != null && expFlowValueLabel.gameObject.activeInHierarchy)
        {
            expFlowValueLabel.text =
                "Exp flow: " + PlanetLodSettings.ExpCloudFlow.ToString("F2");
        }
        if (expVortexValueLabel != null && expVortexValueLabel.gameObject.activeInHierarchy)
        {
            expVortexValueLabel.text =
                "Exp vortex: " + PlanetLodSettings.ExpCloudVortex.ToString("F2");
        }
        if (expGrainValueLabel != null && expGrainValueLabel.gameObject.activeInHierarchy)
        {
            expGrainValueLabel.text =
                "Exp grain: " + PlanetLodSettings.ExpCloudGrain.ToString("F2") +
                " (edge flocks)";
        }
        if (expMassScaleValueLabel != null && expMassScaleValueLabel.gameObject.activeInHierarchy)
        {
            expMassScaleValueLabel.text =
                "Exp mass scale: " + PlanetLodSettings.ExpCloudMassScale.ToString("F2");
        }
        if (expSoftnessValueLabel != null && expSoftnessValueLabel.gameObject.activeInHierarchy)
        {
            expSoftnessValueLabel.text =
                "Exp softness: " + PlanetLodSettings.ExpCloudSoftness.ToString("F2");
        }
        chunkCountValueLabel.text = "Active chunks: " + tuning.ActiveChunkCount;
    }

    static void SetSliderBlockActive(Slider slider, bool active)
    {
        if (slider == null)
        {
            return;
        }

        slider.transform.parent.gameObject.SetActive(active);
    }

    static void SetSliderWithoutNotify(Slider slider, float value)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(value);
    }

    static void CreateHeader(
        Transform parent,
        RectTransform windowRect,
        Canvas canvas,
        Font font,
        string text
    )
    {
        GameObject row = CreateUiObject("Header", parent);
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.minHeight = 36f;
        layout.preferredHeight = 36f;

        Image background = row.AddComponent<Image>();
        background.color = new Color(0.14f, 0.18f, 0.24f, 0.95f);
        background.raycastTarget = true;

        UiOverlayDragHandle dragHandle = row.AddComponent<UiOverlayDragHandle>();
        dragHandle.Bind(windowRect, canvas);

        GameObject titleObject = CreateUiObject("Title", row.transform);
        Text label = titleObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.92f, 0.95f, 1f, 1f);
        label.raycastTarget = false;
        label.text = text + "  ·  drag";
        StretchRect(titleObject.GetComponent<RectTransform>());
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.offsetMin = new Vector2(8f, 0f);
        titleRect.offsetMax = new Vector2(-8f, 0f);
    }

    static Text CreateHint(Transform parent, Font font, string text)
    {
        GameObject row = CreateUiObject("Hint", parent);
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.minHeight = 36f;

        Text label = row.AddComponent<Text>();
        label.font = font;
        label.fontSize = 13;
        label.alignment = TextAnchor.UpperLeft;
        label.color = new Color(0.72f, 0.78f, 0.86f, 0.95f);
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    static Button CreateActionButtonRow(
        Transform parent,
        Font font,
        string title,
        out Text valueLabel
    )
    {
        GameObject block = CreateUiObject(title.Replace(" ", string.Empty) + "Block", parent);
        VerticalLayoutGroup blockLayout = block.AddComponent<VerticalLayoutGroup>();
        blockLayout.spacing = 4f;
        blockLayout.childControlWidth = true;
        blockLayout.childControlHeight = true;
        blockLayout.childForceExpandWidth = true;
        blockLayout.childForceExpandHeight = false;

        LayoutElement blockElement = block.AddComponent<LayoutElement>();
        blockElement.minHeight = 72f;

        GameObject titleRow = CreateUiObject("Title", block.transform);
        LayoutElement titleLayout = titleRow.AddComponent<LayoutElement>();
        titleLayout.minHeight = 22f;
        Text titleLabel = titleRow.AddComponent<Text>();
        titleLabel.font = font;
        titleLabel.fontSize = 15;
        titleLabel.alignment = TextAnchor.MiddleLeft;
        titleLabel.color = new Color(0.88f, 0.92f, 0.98f, 1f);
        titleLabel.raycastTarget = false;
        titleLabel.text = title;

        GameObject buttonObject = CreateUiObject("Button", block.transform);
        LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
        buttonLayout.minHeight = 32f;
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.22f, 0.32f, 0.44f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        valueLabel = labelObject.AddComponent<Text>();
        valueLabel.font = font;
        valueLabel.fontSize = 14;
        valueLabel.alignment = TextAnchor.MiddleCenter;
        valueLabel.color = new Color(0.92f, 0.95f, 1f, 1f);
        valueLabel.raycastTarget = false;
        StretchRect(labelObject.GetComponent<RectTransform>());
        return button;
    }

    static Slider CreateSliderRow(
        Transform parent,
        Font font,
        string title,
        float min,
        float max,
        bool wholeNumbers,
        out Text valueLabel
    )
    {
        GameObject block = CreateUiObject(title.Replace(" ", string.Empty) + "Block", parent);
        VerticalLayoutGroup blockLayout = block.AddComponent<VerticalLayoutGroup>();
        blockLayout.spacing = 4f;
        blockLayout.childControlWidth = true;
        blockLayout.childControlHeight = true;
        blockLayout.childForceExpandWidth = true;
        blockLayout.childForceExpandHeight = false;

        LayoutElement blockElement = block.AddComponent<LayoutElement>();
        blockElement.minHeight = 88f;

        GameObject titleRow = CreateUiObject("Title", block.transform);
        LayoutElement titleLayout = titleRow.AddComponent<LayoutElement>();
        titleLayout.minHeight = 22f;
        Text titleLabel = titleRow.AddComponent<Text>();
        titleLabel.font = font;
        titleLabel.fontSize = 15;
        titleLabel.alignment = TextAnchor.MiddleLeft;
        titleLabel.color = new Color(0.88f, 0.92f, 0.98f, 1f);
        titleLabel.raycastTarget = false;
        titleLabel.text = title;

        GameObject valueRow = CreateUiObject("Value", block.transform);
        LayoutElement valueLayout = valueRow.AddComponent<LayoutElement>();
        valueLayout.minHeight = 20f;
        valueLabel = valueRow.AddComponent<Text>();
        valueLabel.font = font;
        valueLabel.fontSize = 13;
        valueLabel.alignment = TextAnchor.MiddleLeft;
        valueLabel.color = new Color(0.65f, 0.82f, 1f, 1f);
        valueLabel.raycastTarget = false;

        Slider slider = CreateSlider(block.transform, min, max, wholeNumbers);
        return slider;
    }

    static Slider CreateSlider(Transform parent, float min, float max, bool wholeNumbers)
    {
        GameObject sliderObject = CreateUiObject("Slider", parent);
        LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
        sliderLayout.minHeight = 28f;

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;

        GameObject background = CreateUiObject("Background", sliderObject.transform);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.18f, 0.2f, 0.24f, 1f);
        StretchRect(background.GetComponent<RectTransform>());

        GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.35f, 0.62f, 0.95f, 1f);
        StretchRect(fill.GetComponent<RectTransform>());

        GameObject handleSlideArea = CreateUiObject("Handle Slide Area", sliderObject.transform);
        StretchRect(handleSlideArea.GetComponent<RectTransform>());

        GameObject handle = CreateUiObject("Handle", handleSlideArea.transform);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.92f, 0.95f, 1f, 1f);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 24f);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        return slider;
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
}
