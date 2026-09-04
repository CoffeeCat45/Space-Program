using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Global graphics options modal (main menu + VAB).
/// Builds its own Canvas when UI references are not assigned in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class VabOptionsUi : MonoBehaviour
{
    const int MsaaDropdown2x = 0;
    const int MsaaDropdown4x = 1;
    const int MsaaDropdown8x = 2;

    const int PostModeDropdownFxaa = 0;
    const int PostModeDropdownSmaa = 1;
    const int CloudRendererDropdownSimple = 0;
    const int CloudRendererDropdownExperimental = 1;
    const int CloudRendererDropdownNasa = 2;
    const int CloudRendererDropdownNasaEnhanced = 3;

    const float PanelWidth = 580f;
    const float PanelHeight = 700f;

    static readonly Color PanelBackground = new Color(0.10f, 0.12f, 0.16f, 0.98f);
    static readonly Color ControlBackground = new Color(0.16f, 0.19f, 0.24f, 1f);
    static readonly Color ControlBorder = new Color(0.34f, 0.42f, 0.52f, 1f);
    static readonly Color LabelColor = new Color(0.95f, 0.97f, 1f, 1f);
    static readonly Color MutedLabelColor = new Color(0.78f, 0.84f, 0.92f, 1f);
    static readonly Color CheckmarkColor = new Color(0.35f, 0.92f, 0.55f, 1f);
    static readonly Color PrimaryButtonColor = new Color(0.20f, 0.42f, 0.62f, 1f);
    static readonly Color SecondaryButtonColor = new Color(0.18f, 0.21f, 0.27f, 1f);

    static Font uiFont;
    static Sprite whiteSprite;
    static VabOptionsUi cachedInstance;

    [SerializeField]
    VabEditorController vabEditor;

    [SerializeField]
    GameObject optionsPanel;

    [SerializeField]
    GameObject modalBlocker;

    [SerializeField]
    Toggle msaaToggle;

    [SerializeField]
    Dropdown msaaLevelDropdown;

    [SerializeField]
    Toggle postAaToggle;

    [SerializeField]
    Dropdown postAaModeDropdown;

    [SerializeField]
    Dropdown postAaQualityDropdown;

    [SerializeField]
    Toggle showTargetLineToggle;

    [SerializeField]
    Slider terrainLodDistanceSlider;

    [SerializeField]
    Slider terrainLoadSpeedSlider;

    [SerializeField]
    Slider terrainMaxLodSlider;

    [SerializeField]
    Slider terrainViewConeSlider;

    [SerializeField]
    Slider terrainCloudDetailSlider;

    [SerializeField]
    Slider terrainCloudCoverageSlider;

    [SerializeField]
    Slider terrainCloudFrequencySlider;

    [SerializeField]
    Slider terrainCloudClusterSlider;

    [SerializeField]
    Slider expCloudCoverageSlider;

    [SerializeField]
    Slider expCloudFlowSlider;

    [SerializeField]
    Slider expCloudVortexSlider;

    [SerializeField]
    Slider expCloudGrainSlider;

    [SerializeField]
    Slider expCloudMassScaleSlider;

    [SerializeField]
    Slider expCloudSoftnessSlider;

    [SerializeField]
    Dropdown cloudRendererDropdown;

    [SerializeField]
    Button hudSettingsButton;

    [SerializeField]
    Text terrainLodDistanceValueLabel;

    [SerializeField]
    Text terrainLoadSpeedValueLabel;

    [SerializeField]
    Text terrainMaxLodValueLabel;

    [SerializeField]
    Text terrainViewConeValueLabel;

    [SerializeField]
    Text terrainCloudDetailValueLabel;

    [SerializeField]
    Text terrainCloudCoverageValueLabel;

    [SerializeField]
    Text terrainCloudFrequencyValueLabel;

    [SerializeField]
    Text terrainCloudClusterValueLabel;

    [SerializeField]
    Text expCloudCoverageValueLabel;

    [SerializeField]
    Text expCloudFlowValueLabel;

    [SerializeField]
    Text expCloudVortexValueLabel;

    [SerializeField]
    Text expCloudGrainValueLabel;

    [SerializeField]
    Text expCloudMassScaleValueLabel;

    [SerializeField]
    Text expCloudSoftnessValueLabel;

    [SerializeField]
    Text experimentalCloudHint;

    [SerializeField]
    Text nasaCloudHint;

    [SerializeField]
    Button saveButton;

    [SerializeField]
    Button closeButton;

    [SerializeField]
    Text statusLabel;

    bool suppressUiEvents;
    bool uiBuilt;
    bool isPanelOpen;
    bool uiEventsBound;
    GameObject canvasRoot;
    Coroutine statusClearRoutine;

    public bool IsOpen => isPanelOpen;

    public static VabOptionsUi TryGetInstance()
    {
        if (cachedInstance != null)
        {
            return cachedInstance;
        }

        cachedInstance = FindFirstObjectByType<VabOptionsUi>();
        return cachedInstance;
    }

    void Awake()
    {
        if (cachedInstance != null && cachedInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        cachedInstance = this;
        DestroyOrphanHosts();

        if (vabEditor == null)
        {
            vabEditor = FindFirstObjectByType<VabEditorController>();
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
        if (!uiBuilt || suppressUiEvents)
        {
            return;
        }

        SyncTerrainGraphicsSlidersFromSettings();
    }

    void Start()
    {
        GameViewSettings.Load();
        PlanetLodSettings.Load();
        FlightHudSettings.Load();

        if (VabGraphicsManager.TryLoadSnapshot(out VabGraphicsManager.GraphicsSettingsSnapshot saved))
        {
            VabGraphicsManager.ApplySnapshot(saved);
        }
    }

    void EnsureUiBuilt()
    {
        if (uiBuilt)
        {
            return;
        }

        try
        {
            BuildDefaultUi();
            BindUiEvents();
            SyncUiFromGraphics();
            RefreshDependentControls();
            SetPanelOpen(false);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("VAB options UI build failed: " + exception.Message);
            DestroyExistingUi();
            throw;
        }
    }

    static void DestroyOrphanHosts()
    {
        VabOptionsUi[] allInstances = FindObjectsByType<VabOptionsUi>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < allInstances.Length; i++)
        {
            VabOptionsUi instance = allInstances[i];
            if (instance == null)
            {
                continue;
            }

            bool onSessionHost = instance.GetComponent<SimulationRunner>() != null;
            if (!onSessionHost && instance.gameObject.name == "GraphicsOptions")
            {
                Destroy(instance.gameObject);
            }
        }
    }

    void OnDestroy()
    {
        if (cachedInstance == this)
        {
            cachedInstance = null;
        }
    }

    void DestroyExistingUi()
    {
        Transform existingCanvas = transform.Find("GraphicsOptionsCanvas");
        if (existingCanvas != null)
        {
            Destroy(existingCanvas.gameObject);
        }

        optionsPanel = null;
        modalBlocker = null;
        msaaToggle = null;
        msaaLevelDropdown = null;
        postAaToggle = null;
        postAaModeDropdown = null;
        postAaQualityDropdown = null;
        showTargetLineToggle = null;
        terrainLodDistanceSlider = null;
        terrainLoadSpeedSlider = null;
        terrainMaxLodSlider = null;
        terrainViewConeSlider = null;
        terrainCloudDetailSlider = null;
        terrainCloudCoverageSlider = null;
        terrainCloudFrequencySlider = null;
        terrainCloudClusterSlider = null;
        expCloudCoverageSlider = null;
        expCloudFlowSlider = null;
        expCloudVortexSlider = null;
        expCloudGrainSlider = null;
        expCloudMassScaleSlider = null;
        expCloudSoftnessSlider = null;
        cloudRendererDropdown = null;
        hudSettingsButton = null;
        terrainLodDistanceValueLabel = null;
        terrainLoadSpeedValueLabel = null;
        terrainMaxLodValueLabel = null;
        terrainViewConeValueLabel = null;
        terrainCloudDetailValueLabel = null;
        terrainCloudCoverageValueLabel = null;
        terrainCloudFrequencyValueLabel = null;
        terrainCloudClusterValueLabel = null;
        expCloudCoverageValueLabel = null;
        expCloudFlowValueLabel = null;
        expCloudVortexValueLabel = null;
        expCloudGrainValueLabel = null;
        expCloudMassScaleValueLabel = null;
        expCloudSoftnessValueLabel = null;
        experimentalCloudHint = null;
        nasaCloudHint = null;
        saveButton = null;
        closeButton = null;
        statusLabel = null;
        canvasRoot = null;
        uiBuilt = false;
        uiEventsBound = false;
        isPanelOpen = false;
    }

    public void OpenPanel()
    {
        EnsureUiBuilt();
        SyncUiFromGraphics();
        RefreshDependentControls();
        ClearStatus();
        SetPanelOpen(true);
    }

    public bool TryHandleEscape()
    {
        if (IsVabBlockingEscape())
        {
            return false;
        }

        FlightHudSettingsUi hudSettingsUi = FlightHudSettingsUi.TryGetInstance();
        if (hudSettingsUi != null && hudSettingsUi.TryHandleEscape())
        {
            return true;
        }

        if (IsOpen)
        {
            SetPanelOpen(false);
            return true;
        }

        if (IsVabContext())
        {
            OpenPanel();
            return true;
        }

        return false;
    }

    void BindUiEvents()
    {
        if (uiEventsBound)
        {
            return;
        }

        uiEventsBound = true;
        if (msaaToggle != null)
        {
            msaaToggle.onValueChanged.AddListener(OnMsaaToggleChanged);
        }

        if (msaaLevelDropdown != null)
        {
            msaaLevelDropdown.onValueChanged.AddListener(OnMsaaLevelDropdownChanged);
        }

        if (postAaToggle != null)
        {
            postAaToggle.onValueChanged.AddListener(OnPostAaToggleChanged);
        }

        if (postAaModeDropdown != null)
        {
            postAaModeDropdown.onValueChanged.AddListener(OnPostAaModeDropdownChanged);
        }

        if (postAaQualityDropdown != null)
        {
            postAaQualityDropdown.onValueChanged.AddListener(OnPostAaQualityDropdownChanged);
        }

        if (showTargetLineToggle != null)
        {
            showTargetLineToggle.onValueChanged.AddListener(OnShowTargetLineToggleChanged);
        }

        if (terrainLodDistanceSlider != null)
        {
            terrainLodDistanceSlider.onValueChanged.AddListener(OnTerrainLodDistanceSliderChanged);
        }

        if (terrainLoadSpeedSlider != null)
        {
            terrainLoadSpeedSlider.onValueChanged.AddListener(OnTerrainLoadSpeedSliderChanged);
        }

        if (terrainMaxLodSlider != null)
        {
            terrainMaxLodSlider.onValueChanged.AddListener(OnTerrainMaxLodSliderChanged);
        }

        if (terrainViewConeSlider != null)
        {
            terrainViewConeSlider.onValueChanged.AddListener(OnTerrainViewConeSliderChanged);
        }

        if (terrainCloudDetailSlider != null)
        {
            terrainCloudDetailSlider.onValueChanged.AddListener(OnTerrainCloudDetailSliderChanged);
        }

        if (terrainCloudCoverageSlider != null)
        {
            terrainCloudCoverageSlider.onValueChanged.AddListener(
                OnTerrainCloudCoverageSliderChanged
            );
        }

        if (terrainCloudFrequencySlider != null)
        {
            terrainCloudFrequencySlider.onValueChanged.AddListener(
                OnTerrainCloudFrequencySliderChanged
            );
        }

        if (terrainCloudClusterSlider != null)
        {
            terrainCloudClusterSlider.onValueChanged.AddListener(
                OnTerrainCloudClusterSliderChanged
            );
        }

        if (expCloudCoverageSlider != null)
        {
            expCloudCoverageSlider.onValueChanged.AddListener(OnExpCloudCoverageSliderChanged);
        }

        if (expCloudFlowSlider != null)
        {
            expCloudFlowSlider.onValueChanged.AddListener(OnExpCloudFlowSliderChanged);
        }

        if (expCloudVortexSlider != null)
        {
            expCloudVortexSlider.onValueChanged.AddListener(OnExpCloudVortexSliderChanged);
        }

        if (expCloudGrainSlider != null)
        {
            expCloudGrainSlider.onValueChanged.AddListener(OnExpCloudGrainSliderChanged);
        }

        if (expCloudMassScaleSlider != null)
        {
            expCloudMassScaleSlider.onValueChanged.AddListener(OnExpCloudMassScaleSliderChanged);
        }

        if (expCloudSoftnessSlider != null)
        {
            expCloudSoftnessSlider.onValueChanged.AddListener(OnExpCloudSoftnessSliderChanged);
        }

        if (cloudRendererDropdown != null)
        {
            cloudRendererDropdown.onValueChanged.AddListener(OnCloudRendererDropdownChanged);
        }

        if (hudSettingsButton != null)
        {
            hudSettingsButton.onClick.AddListener(OnHudSettingsButtonClicked);
        }

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveSettings);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    void SyncUiFromGraphics()
    {
        suppressUiEvents = true;

        int msaaLevel = VabGraphicsManager.GetMsaaLevel();
        if (msaaToggle != null)
        {
            msaaToggle.isOn = msaaLevel != VabGraphicsManager.MsaaDisabled;
        }

        if (msaaLevelDropdown != null)
        {
            msaaLevelDropdown.value = MsaaLevelToDropdownIndex(msaaLevel);
        }

        int postMode = VabGraphicsManager.GetPostAntiAliasingMode();
        if (postAaToggle != null)
        {
            postAaToggle.isOn = postMode != VabGraphicsManager.PostAaModeNone;
        }

        if (postAaModeDropdown != null)
        {
            postAaModeDropdown.value = PostModeToDropdownIndex(postMode);
        }

        if (postAaQualityDropdown != null)
        {
            postAaQualityDropdown.value = VabGraphicsManager.GetPostAntiAliasingQuality();
        }

        if (showTargetLineToggle != null)
        {
            showTargetLineToggle.isOn = GameViewSettings.ShowTargetDirectionLine;
        }

        SyncTerrainGraphicsSlidersFromSettings();

        suppressUiEvents = false;
    }

    void SyncTerrainGraphicsSlidersFromSettings()
    {
        PlanetLodSettings.Load();

        if (terrainLodDistanceSlider != null)
        {
            terrainLodDistanceSlider.SetValueWithoutNotify(PlanetLodSettings.LodDistanceFactor);
        }

        if (terrainLoadSpeedSlider != null)
        {
            terrainLoadSpeedSlider.SetValueWithoutNotify(PlanetLodSettings.LoadSpeedPerFrame);
        }

        if (terrainMaxLodSlider != null)
        {
            terrainMaxLodSlider.SetValueWithoutNotify(PlanetLodSettings.MaxLod);
        }

        if (terrainViewConeSlider != null)
        {
            terrainViewConeSlider.SetValueWithoutNotify(PlanetLodSettings.ViewConeHalfAngleDegrees);
        }

        if (terrainCloudDetailSlider != null)
        {
            terrainCloudDetailSlider.SetValueWithoutNotify(PlanetLodSettings.CloudDetail);
        }

        if (terrainCloudCoverageSlider != null)
        {
            terrainCloudCoverageSlider.SetValueWithoutNotify(PlanetLodSettings.CloudCoverage);
        }

        if (terrainCloudFrequencySlider != null)
        {
            terrainCloudFrequencySlider.SetValueWithoutNotify(PlanetLodSettings.CloudFrequency);
        }

        if (terrainCloudClusterSlider != null)
        {
            terrainCloudClusterSlider.SetValueWithoutNotify(PlanetLodSettings.CloudCluster);
        }

        if (expCloudCoverageSlider != null)
        {
            expCloudCoverageSlider.SetValueWithoutNotify(PlanetLodSettings.ExpCloudCoverage);
        }

        if (expCloudFlowSlider != null)
        {
            expCloudFlowSlider.SetValueWithoutNotify(PlanetLodSettings.ExpCloudFlow);
        }

        if (expCloudVortexSlider != null)
        {
            expCloudVortexSlider.SetValueWithoutNotify(PlanetLodSettings.ExpCloudVortex);
        }

        if (expCloudGrainSlider != null)
        {
            expCloudGrainSlider.SetValueWithoutNotify(PlanetLodSettings.ExpCloudGrain);
        }

        if (expCloudMassScaleSlider != null)
        {
            expCloudMassScaleSlider.SetValueWithoutNotify(PlanetLodSettings.ExpCloudMassScale);
        }

        if (expCloudSoftnessSlider != null)
        {
            expCloudSoftnessSlider.SetValueWithoutNotify(PlanetLodSettings.ExpCloudSoftness);
        }

        if (cloudRendererDropdown != null)
        {
            cloudRendererDropdown.SetValueWithoutNotify(
                CloudRendererToDropdownIndex(PlanetLodSettings.CloudRenderer)
            );
        }

        RefreshCloudLookControls();

        RefreshTerrainGraphicsValueLabels();
    }

    void RefreshTerrainGraphicsValueLabels()
    {
        if (terrainLodDistanceValueLabel != null)
        {
            terrainLodDistanceValueLabel.text =
                "LOD Distance: " + PlanetLodSettings.LodDistanceFactor.ToString("F2");
        }

        if (terrainLoadSpeedValueLabel != null)
        {
            terrainLoadSpeedValueLabel.text =
                "Load Speed: " + PlanetLodSettings.LoadSpeedPerFrame + " splits/meshes per frame";
        }

        if (terrainMaxLodValueLabel != null)
        {
            terrainMaxLodValueLabel.text = "Max LOD: " + PlanetLodSettings.MaxLod;
        }

        if (terrainViewConeValueLabel != null)
        {
            terrainViewConeValueLabel.text =
                "View Width: unused for LOD (frustum culls draw)";
        }

        if (terrainCloudDetailValueLabel != null)
        {
            terrainCloudDetailValueLabel.text =
                "Cloud detail: " + PlanetLodSettings.CloudDetail.ToString("F2");
        }

        if (terrainCloudCoverageValueLabel != null)
        {
            terrainCloudCoverageValueLabel.text =
                "Cloud coverage: " + PlanetLodSettings.CloudCoverage.ToString("F2");
        }

        if (terrainCloudFrequencyValueLabel != null)
        {
            terrainCloudFrequencyValueLabel.text =
                "Cloud frequency: " + PlanetLodSettings.CloudFrequency.ToString("F2");
        }

        if (terrainCloudClusterValueLabel != null)
        {
            terrainCloudClusterValueLabel.text =
                "Cloud clusters: " + PlanetLodSettings.CloudCluster.ToString("F2");
        }

        if (expCloudCoverageValueLabel != null)
        {
            expCloudCoverageValueLabel.text =
                "Exp coverage: " + PlanetLodSettings.ExpCloudCoverage.ToString("F2");
        }

        if (expCloudFlowValueLabel != null)
        {
            expCloudFlowValueLabel.text =
                "Exp flow: " + PlanetLodSettings.ExpCloudFlow.ToString("F2");
        }

        if (expCloudVortexValueLabel != null)
        {
            expCloudVortexValueLabel.text =
                "Exp vortex: " + PlanetLodSettings.ExpCloudVortex.ToString("F2");
        }

        if (expCloudGrainValueLabel != null)
        {
            expCloudGrainValueLabel.text =
                "Exp grain: " + PlanetLodSettings.ExpCloudGrain.ToString("F2");
        }

        if (expCloudMassScaleValueLabel != null)
        {
            expCloudMassScaleValueLabel.text =
                "Exp mass scale: " + PlanetLodSettings.ExpCloudMassScale.ToString("F2");
        }

        if (expCloudSoftnessValueLabel != null)
        {
            expCloudSoftnessValueLabel.text =
                "Exp softness: " + PlanetLodSettings.ExpCloudSoftness.ToString("F2");
        }
    }

    void OnMsaaToggleChanged(bool enabled)
    {
        if (suppressUiEvents)
        {
            return;
        }

        if (enabled)
        {
            int dropdownIndex = msaaLevelDropdown != null ? msaaLevelDropdown.value : MsaaDropdown4x;
            VabGraphicsManager.SetMsaaLevel(DropdownIndexToMsaaLevel(dropdownIndex));
        }
        else
        {
            VabGraphicsManager.SetMsaaLevel(VabGraphicsManager.MsaaDisabled);
        }

        RefreshDependentControls();
    }

    void OnMsaaLevelDropdownChanged(int dropdownIndex)
    {
        if (suppressUiEvents)
        {
            return;
        }

        if (msaaToggle != null && !msaaToggle.isOn)
        {
            return;
        }

        VabGraphicsManager.SetMsaaLevel(DropdownIndexToMsaaLevel(dropdownIndex));
    }

    void OnPostAaToggleChanged(bool enabled)
    {
        if (suppressUiEvents)
        {
            return;
        }

        ApplyPostAntiAliasing();
        RefreshDependentControls();
    }

    void OnPostAaModeDropdownChanged(int dropdownIndex)
    {
        if (suppressUiEvents)
        {
            return;
        }

        ApplyPostAntiAliasing();
    }

    void OnPostAaQualityDropdownChanged(int qualityIndex)
    {
        if (suppressUiEvents)
        {
            return;
        }

        ApplyPostAntiAliasing();
    }

    void ApplyPostAntiAliasing()
    {
        bool enabled = postAaToggle != null && postAaToggle.isOn;
        if (!enabled)
        {
            VabGraphicsManager.SetPostAntiAliasing(
                VabGraphicsManager.PostAaModeNone,
                VabGraphicsManager.PostAaQualityLow
            );
            return;
        }

        int modeIndex = postAaModeDropdown != null
            ? DropdownIndexToPostMode(postAaModeDropdown.value)
            : VabGraphicsManager.PostAaModeFxaa;
        int qualityIndex = postAaQualityDropdown != null
            ? postAaQualityDropdown.value
            : VabGraphicsManager.PostAaQualityMedium;

        VabGraphicsManager.SetPostAntiAliasing(modeIndex, qualityIndex);
    }

    void OnShowTargetLineToggleChanged(bool enabled)
    {
        if (suppressUiEvents)
        {
            return;
        }

        GameViewSettings.SetShowTargetDirectionLine(enabled);
    }

    void OnTerrainLodDistanceSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetLodDistanceFactor(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnTerrainLoadSpeedSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetLoadSpeedPerFrame(Mathf.RoundToInt(value));
        RefreshTerrainGraphicsValueLabels();
    }

    void OnTerrainMaxLodSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetMaxLod(Mathf.RoundToInt(value));
        RefreshTerrainGraphicsValueLabels();
    }

    void OnTerrainViewConeSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetViewConeHalfAngleDegrees(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnTerrainCloudDetailSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetCloudDetail(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnTerrainCloudCoverageSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetCloudCoverage(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnTerrainCloudFrequencySliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetCloudFrequency(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnTerrainCloudClusterSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetCloudCluster(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnExpCloudCoverageSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudCoverage(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnExpCloudFlowSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudFlow(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnExpCloudVortexSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudVortex(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnExpCloudGrainSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudGrain(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnExpCloudMassScaleSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudMassScale(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnExpCloudSoftnessSliderChanged(float value)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetExpCloudSoftness(value);
        RefreshTerrainGraphicsValueLabels();
    }

    void OnCloudRendererDropdownChanged(int dropdownIndex)
    {
        if (suppressUiEvents)
        {
            return;
        }

        PlanetLodSettings.SetCloudRenderer(DropdownIndexToCloudRenderer(dropdownIndex));
        RefreshCloudLookControls();
    }

    void OnHudSettingsButtonClicked()
    {
        if (suppressUiEvents)
        {
            return;
        }

        FlightHudSettingsUi hudSettingsUi = FlightHudSettingsUi.TryGetInstance();
        if (hudSettingsUi == null)
        {
            hudSettingsUi = gameObject.AddComponent<FlightHudSettingsUi>();
        }

        hudSettingsUi.OpenPanel();
    }

    void SaveSettings()
    {
        ApplyCurrentUiToGpu();

        VabGraphicsManager.GraphicsSettingsSnapshot snapshot = ReadSnapshotFromUi();
        VabGraphicsManager.SaveSnapshot(snapshot);

        if (showTargetLineToggle != null)
        {
            GameViewSettings.SetShowTargetDirectionLine(showTargetLineToggle.isOn);
        }

        PlanetLodSettings.Save();
        FlightPlanetSphere.ApplyRuntimeLodSettings();
        FlightPlanetSphere.ApplyRuntimeCloudSettings();
        ShowStatus("Настройки сохранены");
    }

    void ApplyCurrentUiToGpu()
    {
        if (msaaToggle != null && msaaToggle.isOn)
        {
            int dropdownIndex = msaaLevelDropdown != null ? msaaLevelDropdown.value : MsaaDropdown4x;
            VabGraphicsManager.SetMsaaLevel(DropdownIndexToMsaaLevel(dropdownIndex));
        }
        else
        {
            VabGraphicsManager.SetMsaaLevel(VabGraphicsManager.MsaaDisabled);
        }

        ApplyPostAntiAliasing();
    }

    VabGraphicsManager.GraphicsSettingsSnapshot ReadSnapshotFromUi()
    {
        bool msaaEnabled = msaaToggle != null && msaaToggle.isOn;
        int msaaLevel = msaaEnabled
            ? DropdownIndexToMsaaLevel(msaaLevelDropdown != null ? msaaLevelDropdown.value : MsaaDropdown4x)
            : VabGraphicsManager.Msaa4x;
        bool postEnabled = postAaToggle != null && postAaToggle.isOn;

        return new VabGraphicsManager.GraphicsSettingsSnapshot
        {
            msaaEnabled = msaaEnabled,
            msaaLevel = msaaLevel,
            postAaEnabled = postEnabled,
            postAaMode = postEnabled
                ? DropdownIndexToPostMode(postAaModeDropdown != null ? postAaModeDropdown.value : PostModeDropdownFxaa)
                : VabGraphicsManager.PostAaModeFxaa,
            postAaQuality = postAaQualityDropdown != null
                ? postAaQualityDropdown.value
                : VabGraphicsManager.PostAaQualityMedium,
        };
    }

    void RefreshDependentControls()
    {
        bool msaaEnabled = msaaToggle != null && msaaToggle.isOn;
        if (msaaLevelDropdown != null)
        {
            msaaLevelDropdown.interactable = msaaEnabled;
            SetDropdownMuted(msaaLevelDropdown, !msaaEnabled);
        }

        bool postEnabled = postAaToggle != null && postAaToggle.isOn;
        if (postAaModeDropdown != null)
        {
            postAaModeDropdown.interactable = postEnabled;
            SetDropdownMuted(postAaModeDropdown, !postEnabled);
        }

        if (postAaQualityDropdown != null)
        {
            postAaQualityDropdown.interactable = postEnabled;
            SetDropdownMuted(postAaQualityDropdown, !postEnabled);
        }

        RefreshCloudLookControls();
    }

    void RefreshCloudLookControls()
    {
        bool simpleLook = PlanetLodSettings.UsesSimpleCloudLook;
        bool experimentalLook = PlanetLodSettings.UsesExperimentalCloudLook;
        bool nasaLook = PlanetLodSettings.UsesNasaCloudLook;
        SetSliderBlockActive(terrainCloudDetailSlider, !nasaLook);
        SetSliderBlockActive(terrainCloudCoverageSlider, simpleLook);
        SetSliderBlockActive(terrainCloudFrequencySlider, simpleLook);
        SetSliderBlockActive(terrainCloudClusterSlider, simpleLook);
        SetSliderBlockActive(expCloudCoverageSlider, experimentalLook);
        SetSliderBlockActive(expCloudFlowSlider, experimentalLook);
        SetSliderBlockActive(expCloudVortexSlider, experimentalLook);
        SetSliderBlockActive(expCloudGrainSlider, experimentalLook);
        SetSliderBlockActive(expCloudMassScaleSlider, experimentalLook);
        SetSliderBlockActive(expCloudSoftnessSlider, experimentalLook);

        if (experimentalCloudHint != null)
        {
            experimentalCloudHint.gameObject.SetActive(experimentalLook);
        }

        if (nasaCloudHint != null)
        {
            nasaCloudHint.gameObject.SetActive(nasaLook);
            nasaCloudHint.text = PlanetLodSettings.UsesNasaEnhancedCloudLook
                ? "NASA PNG+: та же clouds-alpha.png. Ветер на восток, два слоя без двоения. Без переворота полушарий."
                : "NASA PNG: StreamingAssets/MapMode/clouds-alpha.png. Equirect, geographic UV, без ветра.";
        }
    }

    void SetPanelOpen(bool open)
    {
        isPanelOpen = open;

        if (canvasRoot != null)
        {
            canvasRoot.SetActive(open);
            return;
        }

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(open);
        }

        if (modalBlocker != null)
        {
            modalBlocker.SetActive(open);
        }

        if (!open)
        {
            ClearStatus();
        }
    }

    void ClosePanel()
    {
        SetPanelOpen(false);
    }

    void ShowStatus(string message)
    {
        if (statusLabel == null)
        {
            return;
        }

        statusLabel.text = message;
        statusLabel.color = CheckmarkColor;

        if (statusClearRoutine != null)
        {
            StopCoroutine(statusClearRoutine);
        }

        statusClearRoutine = StartCoroutine(ClearStatusAfterDelay(2.5f));
    }

    void ClearStatus()
    {
        if (statusLabel == null)
        {
            return;
        }

        statusLabel.text = string.Empty;
    }

    IEnumerator ClearStatusAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        ClearStatus();
        statusClearRoutine = null;
    }

    bool IsVabContext()
    {
        return vabEditor != null && vabEditor.isActiveAndEnabled;
    }

    bool IsVabBlockingEscape()
    {
        return IsVabContext() && !vabEditor.HasEmptyHands;
    }

    void BuildDefaultUi()
    {
        if (uiBuilt)
        {
            return;
        }

        DestroyExistingUi();
        EnsureEventSystem();
        EnsureUiFont();

        GameObject canvasObject = new GameObject("GraphicsOptionsCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvasRoot = canvasObject;
        canvasRoot.SetActive(false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.65f;
        canvasObject.AddComponent<GraphicRaycaster>();

        modalBlocker = CreateStretchPanel(
            canvasObject.transform,
            "OptionsModalBlocker",
            new Color(0f, 0f, 0f, 0.62f)
        );

        optionsPanel = CreateCenteredPanel(
            canvasObject.transform,
            "OptionsPanel",
            new Vector2(PanelWidth, PanelHeight),
            PanelBackground
        );

        VerticalLayoutGroup panelLayout = optionsPanel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(24, 24, 20, 20);
        panelLayout.spacing = 10f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        CreateTitleLabel(optionsPanel.transform, "Настройки");

        Transform settingsContent = CreateSettingsScrollArea(optionsPanel.transform);

        msaaToggle = CreateSettingRow(
            settingsContent,
            "Включить геометрическое сглаживание (MSAA)",
            out msaaLevelDropdown,
            168f
        );
        SetDropdownOptions(msaaLevelDropdown, "2x", "4x", "8x");
        msaaLevelDropdown.value = MsaaDropdown4x;

        postAaToggle = CreateSettingRow(
            settingsContent,
            "Включить экранное сглаживание (Пост-эффект)",
            out postAaModeDropdown,
            196f
        );
        SetDropdownOptions(postAaModeDropdown, "FXAA (Четко)", "SMAA (Гладко)");

        CreateCaptionLabel(settingsContent, "Качество пост-эффекта");
        postAaQualityDropdown = CreateStyledDropdown(settingsContent, -1f);
        SetDropdownOptions(postAaQualityDropdown, "Низкое", "Среднее", "Высокое");
        postAaQualityDropdown.value = VabGraphicsManager.PostAaQualityMedium;

        CreateCaptionLabel(settingsContent, "Отображение");
        showTargetLineToggle = CreateToggleOnlyRow(
            settingsContent,
            "Линия к цели"
        );
        showTargetLineToggle.isOn = GameViewSettings.ShowTargetDirectionLine;

        CreateCaptionLabel(settingsContent, "HUD полёта");
        hudSettingsButton = CreateStyledButton(
            settingsContent,
            "Настройки HUD",
            PrimaryButtonColor,
            flexible: true
        );

        CreateCaptionLabel(settingsContent, "Ландшафт (LOD)");
        terrainLodDistanceSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "LOD Distance (wider)",
            FlightPlanetTerrainTuning.LodDistanceFactorMin,
            FlightPlanetTerrainTuning.LodDistanceFactorMax,
            false,
            out terrainLodDistanceValueLabel
        );
        terrainLoadSpeedSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Load Speed (deeper)",
            FlightPlanetTerrainTuning.LoadSpeedMin,
            FlightPlanetTerrainTuning.LoadSpeedMax,
            true,
            out terrainLoadSpeedValueLabel
        );
        terrainMaxLodSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Max LOD (ground detail)",
            FlightPlanetTerrainTuning.MaxLodMin,
            FlightPlanetSphere.DefaultMaxLodLevel,
            true,
            out terrainMaxLodValueLabel
        );
        terrainViewConeSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "View Width (look cone)",
            FlightPlanetTerrainTuning.ViewConeHalfAngleMin,
            FlightPlanetTerrainTuning.ViewConeHalfAngleMax,
            false,
            out terrainViewConeValueLabel
        );

        CreateCaptionLabel(settingsContent, "Облака");
        CreateCaptionLabel(settingsContent, "Рендер облаков");
        cloudRendererDropdown = CreateStyledDropdown(settingsContent, -1f);
        SetDropdownOptions(
            cloudRendererDropdown,
            "Простые оболочки",
            "Экспериментальный",
            "NASA PNG",
            "NASA PNG+"
        );

        terrainCloudDetailSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Cloud detail (light → rich)",
            FlightPlanetTerrainTuning.CloudDetailMin,
            FlightPlanetTerrainTuning.CloudDetailMax,
            false,
            out terrainCloudDetailValueLabel
        );
        terrainCloudCoverageSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Cloud coverage / густота",
            FlightPlanetTerrainTuning.CloudCoverageMin,
            FlightPlanetTerrainTuning.CloudCoverageMax,
            false,
            out terrainCloudCoverageValueLabel
        );
        terrainCloudFrequencySlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Cloud frequency / частота",
            FlightPlanetTerrainTuning.CloudFrequencyMin,
            FlightPlanetTerrainTuning.CloudFrequencyMax,
            false,
            out terrainCloudFrequencyValueLabel
        );
        terrainCloudClusterSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Cloud clusters / скопления",
            FlightPlanetTerrainTuning.CloudClusterMin,
            FlightPlanetTerrainTuning.CloudClusterMax,
            false,
            out terrainCloudClusterValueLabel
        );
        expCloudCoverageSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Exp coverage / густота масс",
            FlightPlanetTerrainTuning.ExpCloudCoverageMin,
            FlightPlanetTerrainTuning.ExpCloudCoverageMax,
            false,
            out expCloudCoverageValueLabel
        );
        expCloudFlowSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Exp flow / потоки",
            FlightPlanetTerrainTuning.ExpCloudFlowMin,
            FlightPlanetTerrainTuning.ExpCloudFlowMax,
            false,
            out expCloudFlowValueLabel
        );
        expCloudVortexSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Exp vortex / вихри",
            FlightPlanetTerrainTuning.ExpCloudVortexMin,
            FlightPlanetTerrainTuning.ExpCloudVortexMax,
            false,
            out expCloudVortexValueLabel
        );
        expCloudGrainSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Exp grain / кайма барашки",
            FlightPlanetTerrainTuning.ExpCloudGrainMin,
            FlightPlanetTerrainTuning.ExpCloudGrainMax,
            false,
            out expCloudGrainValueLabel
        );
        expCloudMassScaleSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Exp mass scale / размер масс",
            FlightPlanetTerrainTuning.ExpCloudMassScaleMin,
            FlightPlanetTerrainTuning.ExpCloudMassScaleMax,
            false,
            out expCloudMassScaleValueLabel
        );
        expCloudSoftnessSlider = CreateTerrainGraphicsSlider(
            settingsContent,
            "Exp softness / мягкость",
            FlightPlanetTerrainTuning.ExpCloudSoftnessMin,
            FlightPlanetTerrainTuning.ExpCloudSoftnessMax,
            false,
            out expCloudSoftnessValueLabel
        );
        experimentalCloudHint = CreateHintLabel(
            settingsContent,
            "Экспериментальный: густота / потоки / вихри / кайма / размер масс / мягкость. Ползунки простых оболочек сохраняются."
        );
        nasaCloudHint = CreateHintLabel(
            settingsContent,
            "NASA PNG: StreamingAssets/MapMode/clouds-alpha.png. Equirect, geographic UV, без ветра."
        );
        CreateHintLabel(
            settingsContent,
            "Те же параметры, что и в панели Terrain Graphics в полёте. Изменения применяются сразу и сохраняются после перезапуска."
        );
        RefreshCloudLookControls();

        statusLabel = CreateStatusLabel(optionsPanel.transform);
        CreateFooterButtons(optionsPanel.transform, out saveButton, out closeButton);

        uiBuilt = true;
        canvasRoot.SetActive(false);
    }

    static Transform CreateSettingsScrollArea(Transform parent)
    {
        GameObject scrollRoot = new GameObject("SettingsScroll");
        scrollRoot.transform.SetParent(parent, false);

        LayoutElement scrollLayout = scrollRoot.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = 320f;
        scrollLayout.preferredHeight = 380f;

        ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollRoot.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 12f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        return content.transform;
    }

    static GameObject CreateCenteredPanel(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject panelObject = new GameObject(name);
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panelObject;
    }

    static DefaultControls.Resources GetUiResources()
    {
        Sprite sprite = GetWhiteSprite();
        return new DefaultControls.Resources
        {
            standard = sprite,
            background = sprite,
            inputField = sprite,
            knob = sprite,
            checkmark = sprite,
            dropdown = sprite,
            mask = sprite,
        };
    }

    static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        return whiteSprite;
    }

    static void EnsureUiFont()
    {
        if (uiFont != null)
        {
            return;
        }

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (uiFont == null)
        {
            uiFont = Font.CreateDynamicFontFromOSFont("Arial", 18);
        }
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

    static GameObject CreateStretchPanel(Transform parent, string name, Color color)
    {
        GameObject panelObject = new GameObject(name);
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panelObject;
    }

    static void CreateTitleLabel(Transform parent, string text)
    {
        GameObject labelObject = new GameObject("Title");
        labelObject.transform.SetParent(parent, false);

        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;

        Text label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontSize = 26;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = LabelColor;
    }

    static void CreateCaptionLabel(Transform parent, string text)
    {
        GameObject labelObject = new GameObject("Caption");
        labelObject.transform.SetParent(parent, false);

        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.minHeight = 28f;
        layout.preferredHeight = 28f;

        Text label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = LabelColor;
    }

    static Text CreateHintLabel(Transform parent, string text)
    {
        GameObject labelObject = new GameObject("Hint");
        labelObject.transform.SetParent(parent, false);

        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.minHeight = 52f;
        layout.preferredHeight = 52f;
        layout.flexibleHeight = 0f;

        Text label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontSize = 14;
        label.alignment = TextAnchor.UpperLeft;
        label.color = MutedLabelColor;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.supportRichText = false;
        return label;
    }

    static Text CreateStatusLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("Status");
        labelObject.transform.SetParent(parent, false);

        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.minHeight = 24f;
        layout.preferredHeight = 24f;

        Text label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.fontSize = 16;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = CheckmarkColor;
        return label;
    }

    static Toggle CreateToggleOnlyRow(Transform parent, string labelText)
    {
        GameObject rowObject = CreateLayoutRow(parent, "ToggleOnlyRow", 44f);

        Toggle toggle = CreateStyledToggle(rowObject.transform);
        CreateRowLabel(rowObject.transform, labelText);
        return toggle;
    }

    static Toggle CreateSettingRow(
        Transform parent,
        string labelText,
        out Dropdown dropdown,
        float dropdownWidth
    )
    {
        GameObject rowObject = CreateLayoutRow(parent, "SettingRow", 56f);

        Toggle toggle = CreateStyledToggle(rowObject.transform);
        CreateRowLabel(rowObject.transform, labelText);
        dropdown = CreateStyledDropdown(rowObject.transform, dropdownWidth);
        return toggle;
    }

    static Toggle CreateStyledToggle(Transform parent)
    {
        GameObject toggleObject = new GameObject("Toggle");
        toggleObject.transform.SetParent(parent, false);

        LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
        layout.minWidth = 30f;
        layout.preferredWidth = 30f;
        layout.minHeight = 30f;
        layout.preferredHeight = 30f;

        Toggle toggle = toggleObject.AddComponent<Toggle>();

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = ControlBackground;

        Outline outline = backgroundObject.AddComponent<Outline>();
        outline.effectColor = ControlBorder;
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject checkmarkObject = new GameObject("Checkmark");
        checkmarkObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform checkmarkRect = checkmarkObject.AddComponent<RectTransform>();
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;

        Text checkLabel = checkmarkObject.AddComponent<Text>();
        checkLabel.font = uiFont;
        checkLabel.text = "✓";
        checkLabel.fontSize = 20;
        checkLabel.fontStyle = FontStyle.Bold;
        checkLabel.alignment = TextAnchor.MiddleCenter;
        checkLabel.color = CheckmarkColor;

        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkLabel;
        toggle.isOn = false;

        ColorBlock toggleColors = toggle.colors;
        toggleColors.normalColor = Color.white;
        toggleColors.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
        toggleColors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        toggleColors.selectedColor = Color.white;
        toggle.colors = toggleColors;

        return toggle;
    }

    static void CreateRowLabel(Transform parent, string text)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);

        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minHeight = 34f;
        layout.preferredHeight = 34f;

        Text label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontSize = 17;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = LabelColor;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.supportRichText = false;
    }

    static Dropdown CreateStyledDropdown(Transform parent, float width)
    {
        DefaultControls.Resources resources = GetUiResources();
        GameObject dropdownObject = DefaultControls.CreateDropdown(resources);
        dropdownObject.name = "Dropdown";
        dropdownObject.transform.SetParent(parent, false);

        LayoutElement layout = dropdownObject.AddComponent<LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;

        if (width > 0f)
        {
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }
        else
        {
            layout.flexibleWidth = 1f;
            layout.minWidth = 120f;
        }

        Image background = dropdownObject.GetComponent<Image>();
        if (background != null)
        {
            background.color = ControlBackground;
        }

        Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
        StyleDropdown(dropdown);
        EnsureDropdownCaptionLayout(dropdownObject.transform);
        return dropdown;
    }

    static void EnsureDropdownCaptionLayout(Transform dropdownRoot)
    {
        Transform label = dropdownRoot.Find("Label");
        if (label == null)
        {
            return;
        }

        RectTransform labelRect = label.GetComponent<RectTransform>();
        if (labelRect != null)
        {
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 2f);
            labelRect.offsetMax = new Vector2(-28f, -2f);
        }

        Text caption = label.GetComponent<Text>();
        if (caption != null)
        {
            caption.alignment = TextAnchor.MiddleLeft;
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }

    static void StyleDropdown(Dropdown dropdown)
    {
        if (dropdown.captionText != null)
        {
            dropdown.captionText.font = uiFont;
            dropdown.captionText.fontSize = 17;
            dropdown.captionText.color = LabelColor;
            dropdown.captionText.alignment = TextAnchor.MiddleLeft;
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.font = uiFont;
            dropdown.itemText.fontSize = 16;
            dropdown.itemText.color = LabelColor;
        }

        Transform arrow = dropdown.transform.Find("Arrow");
        if (arrow != null)
        {
            Image arrowImage = arrow.GetComponent<Image>();
            if (arrowImage != null)
            {
                arrowImage.enabled = false;
            }

            Text arrowText = GetOrCreateDropdownArrowLabel(arrow);
            arrowText.font = uiFont;
            arrowText.text = "▾";
            arrowText.fontSize = 18;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = LabelColor;
            arrowText.raycastTarget = false;

            RectTransform arrowRect = arrow.GetComponent<RectTransform>();
            if (arrowRect != null)
            {
                arrowRect.anchorMin = new Vector2(1f, 0.5f);
                arrowRect.anchorMax = new Vector2(1f, 0.5f);
                arrowRect.pivot = new Vector2(1f, 0.5f);
                arrowRect.sizeDelta = new Vector2(24f, 24f);
                arrowRect.anchoredPosition = new Vector2(-8f, 0f);
            }
        }

        Transform template = dropdown.transform.Find("Template");
        if (template == null)
        {
            return;
        }

        LayoutElement templateLayout = template.GetComponent<LayoutElement>();
        if (templateLayout == null)
        {
            templateLayout = template.gameObject.AddComponent<LayoutElement>();
        }

        templateLayout.ignoreLayout = true;

        Image templateBackground = template.GetComponent<Image>();
        if (templateBackground != null)
        {
            templateBackground.color = new Color(0.14f, 0.17f, 0.22f, 0.98f);
        }

        Transform viewport = template.Find("Viewport");
        if (viewport != null)
        {
            Image viewportMask = viewport.GetComponent<Image>();
            if (viewportMask != null)
            {
                viewportMask.color = new Color(1f, 1f, 1f, 0.04f);
            }
        }

        Transform item = template.Find("Viewport/Content/Item");
        if (item != null)
        {
            Transform itemBackground = item.Find("Item Background");
            if (itemBackground != null)
            {
                Image itemBackgroundImage = itemBackground.GetComponent<Image>();
                if (itemBackgroundImage != null)
                {
                    itemBackgroundImage.color = new Color(0.18f, 0.22f, 0.28f, 1f);
                }
            }

            Transform itemLabel = item.Find("Item Label");
            if (itemLabel != null)
            {
                Text itemLabelText = itemLabel.GetComponent<Text>();
                if (itemLabelText != null)
                {
                    itemLabelText.font = uiFont;
                    itemLabelText.fontSize = 16;
                    itemLabelText.color = LabelColor;
                }
            }

            Toggle itemToggle = item.GetComponent<Toggle>();
            if (itemToggle != null)
            {
                ColorBlock colors = itemToggle.colors;
                colors.normalColor = new Color(0.18f, 0.22f, 0.28f, 1f);
                colors.highlightedColor = new Color(0.24f, 0.32f, 0.42f, 1f);
                colors.pressedColor = new Color(0.20f, 0.42f, 0.62f, 1f);
                colors.selectedColor = new Color(0.20f, 0.42f, 0.62f, 1f);
                itemToggle.colors = colors;
            }
        }
    }

    static Text GetOrCreateDropdownArrowLabel(Transform arrow)
    {
        Transform labelTransform = arrow.Find("ArrowLabel");
        GameObject labelObject;

        if (labelTransform == null)
        {
            labelObject = new GameObject("ArrowLabel");
            labelObject.transform.SetParent(arrow, false);

            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
        else
        {
            labelObject = labelTransform.gameObject;
        }

        Text arrowText = labelObject.GetComponent<Text>();

        if (arrowText == null)
        {
            arrowText = labelObject.AddComponent<Text>();
        }

        return arrowText;
    }

    static void SetDropdownMuted(Dropdown dropdown, bool muted)
    {
        if (dropdown == null || dropdown.captionText == null)
        {
            return;
        }

        dropdown.captionText.color = muted ?
            new Color(0.62f, 0.68f, 0.76f, 1f) :
            LabelColor;
    }

    static GameObject CreateLayoutRow(Transform parent, string name, float height)
    {
        GameObject rowObject = new GameObject(name);
        rowObject.transform.SetParent(parent, false);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = height;
        rowLayout.preferredHeight = height;

        HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return rowObject;
    }

    static void CreateFooterButtons(Transform parent, out Button save, out Button close)
    {
        GameObject rowObject = CreateLayoutRow(parent, "FooterButtons", 44f);

        save = CreateStyledButton(rowObject.transform, "Сохранить", PrimaryButtonColor, flexible: true);
        close = CreateStyledButton(rowObject.transform, "Выйти", SecondaryButtonColor, flexible: true);
    }

    static Button CreateStyledButton(Transform parent, string label, Color backgroundColor, bool flexible)
    {
        GameObject buttonObject = new GameObject(label + "Button");
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 42f;
        layout.preferredHeight = 42f;

        if (flexible)
        {
            layout.flexibleWidth = 1f;
        }

        Image image = buttonObject.AddComponent<Image>();
        image.color = backgroundColor;
        image.type = Image.Type.Sliced;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("Text");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelObject.AddComponent<Text>();
        labelText.font = uiFont;
        labelText.text = label;
        labelText.fontSize = 18;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = LabelColor;

        return button;
    }

    static void SetDropdownOptions(Dropdown dropdown, params string[] options)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
    }

    static int DropdownIndexToMsaaLevel(int dropdownIndex)
    {
        return dropdownIndex switch
        {
            MsaaDropdown2x => VabGraphicsManager.Msaa2x,
            MsaaDropdown8x => VabGraphicsManager.Msaa8x,
            _ => VabGraphicsManager.Msaa4x,
        };
    }

    static int MsaaLevelToDropdownIndex(int msaaLevel)
    {
        return msaaLevel switch
        {
            VabGraphicsManager.Msaa2x => MsaaDropdown2x,
            VabGraphicsManager.Msaa8x => MsaaDropdown8x,
            _ => MsaaDropdown4x,
        };
    }

    static int DropdownIndexToPostMode(int dropdownIndex)
    {
        return dropdownIndex == PostModeDropdownSmaa
            ? VabGraphicsManager.PostAaModeSmaa
            : VabGraphicsManager.PostAaModeFxaa;
    }

    static int PostModeToDropdownIndex(int modeIndex)
    {
        return modeIndex == VabGraphicsManager.PostAaModeSmaa
            ? PostModeDropdownSmaa
            : PostModeDropdownFxaa;
    }

    static CloudRendererMode DropdownIndexToCloudRenderer(int dropdownIndex)
    {
        if (dropdownIndex == CloudRendererDropdownNasaEnhanced)
        {
            return CloudRendererMode.NasaPngEnhanced;
        }

        if (dropdownIndex == CloudRendererDropdownNasa)
        {
            return CloudRendererMode.NasaPng;
        }

        if (dropdownIndex == CloudRendererDropdownExperimental)
        {
            return CloudRendererMode.Experimental;
        }

        return CloudRendererMode.SimpleShells;
    }

    static int CloudRendererToDropdownIndex(CloudRendererMode mode)
    {
        if (mode == CloudRendererMode.NasaPngEnhanced)
        {
            return CloudRendererDropdownNasaEnhanced;
        }

        if (mode == CloudRendererMode.NasaPng)
        {
            return CloudRendererDropdownNasa;
        }

        if (mode == CloudRendererMode.Experimental)
        {
            return CloudRendererDropdownExperimental;
        }

        return CloudRendererDropdownSimple;
    }

    static void SetSliderBlockActive(Slider slider, bool active)
    {
        if (slider == null)
        {
            return;
        }

        slider.transform.parent.gameObject.SetActive(active);
    }

    static Slider CreateTerrainGraphicsSlider(
        Transform parent,
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
        titleLabel.font = uiFont;
        titleLabel.fontSize = 15;
        titleLabel.alignment = TextAnchor.MiddleLeft;
        titleLabel.color = LabelColor;
        titleLabel.text = title;

        GameObject valueRow = CreateUiObject("Value", block.transform);
        LayoutElement valueLayout = valueRow.AddComponent<LayoutElement>();
        valueLayout.minHeight = 20f;
        valueLabel = valueRow.AddComponent<Text>();
        valueLabel.font = uiFont;
        valueLabel.fontSize = 13;
        valueLabel.alignment = TextAnchor.MiddleLeft;
        valueLabel.color = MutedLabelColor;

        return CreateTerrainGraphicsSliderControl(block.transform, min, max, wholeNumbers);
    }

    static Slider CreateTerrainGraphicsSliderControl(
        Transform parent,
        float min,
        float max,
        bool wholeNumbers
    )
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
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        StretchRect(fillRect);

        GameObject handleSlideArea = CreateUiObject("Handle Slide Area", sliderObject.transform);
        StretchRect(handleSlideArea.GetComponent<RectTransform>());

        GameObject handle = CreateUiObject("Handle", handleSlideArea.transform);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.92f, 0.95f, 1f, 1f);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 24f);

        slider.fillRect = fillRect;
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
}
