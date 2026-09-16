using UnityEngine;

[DefaultExecutionOrder(1000)]
public class ShipConfigurationHud : MonoBehaviour
{
    public bool showPanel;
    public int fontSize = 12;
    public string saveFileName = "custom_ship.ship";

    SimulationRunner runner;
    GUIStyle labelStyle;
    GUIStyle buttonStyle;
    GUIStyle boxStyle;
    Texture2D opaquePanelBackground;

    bool enableStage2 = true;
    bool enableStage3;
    float stage1FuelFraction = 1.0f;
    float stage2FuelFraction = 1.0f;
    float stage3FuelFraction = 1.0f;

    internal void SetRunner(SimulationRunner newRunner)
    {
        runner = newRunner;
    }

    void OnGUI()
    {
        if (
            runner == null ||
            !runner.IsInitialized ||
            runner.IsMainMenuVisible ||
            runner.IsVabEditorActive ||
            FlightLodLoadingOverlay.IsBlocking ||
            FlightHudSettingsUi.IsOpen ||
            FlightDebugSettingsUi.IsOpen
        )
        {
            return;
        }

        EnsureStyles();

        if (!FlightDebugSettings.IsVisible(FlightDebugOverlay.Configuration))
        {
            showPanel = false;
        }

        float panelWidth = 260.0f;
        bool buttonVisible = FlightDebugChromeLayout.TryGetTopRightButtonRect(
            FlightDebugOverlay.Configuration,
            out Rect buttonRect
        );

        if (buttonVisible && GUI.Button(buttonRect, T("Configuration"), buttonStyle))
        {
            showPanel = !showPanel;
        }

        if (!showPanel)
        {
            return;
        }

        float buttonX = Screen.width - panelWidth - 10.0f;
        float panelTopY = FlightDebugChromeLayout.GetTopRightStackBottomY();
        float panelHeight = 360.0f;
        Rect panelRect = new Rect(
            buttonX,
            panelTopY,
            panelWidth,
            panelHeight
        );
        GUI.Box(panelRect, GUIContent.none, boxStyle);
        GUILayout.BeginArea(panelRect, boxStyle);
        GUILayout.Label(T("Ship configuration"), labelStyle);
        GUILayout.Space(6);

        GUILayout.Label(T("Stage") + " 1", labelStyle);
        stage1FuelFraction = DrawFuelSlider(
            T("Stage") + " 1 " + T("Fuel"),
            stage1FuelFraction
        );

        enableStage2 = GUILayout.Toggle(enableStage2, T("Stage") + " 2", labelStyle);

        if (enableStage2)
        {
            stage2FuelFraction = DrawFuelSlider(
                T("Stage") + " 2 " + T("Fuel"),
                stage2FuelFraction
            );
        }

        enableStage3 = GUILayout.Toggle(enableStage3, T("Stage") + " 3", labelStyle);

        if (enableStage3)
        {
            stage3FuelFraction = DrawFuelSlider(
                T("Stage") + " 3 " + T("Fuel"),
                stage3FuelFraction
            );
        }

        GUILayout.Space(8);

        if (GUILayout.Button(T("Apply configuration"), buttonStyle))
        {
            runner.ApplyShipConfiguration(
                enableStage2,
                enableStage3,
                stage1FuelFraction,
                stage2FuelFraction,
                stage3FuelFraction
            );
        }

        if (GUILayout.Button(T("Save configuration"), buttonStyle))
        {
            runner.ApplyShipConfiguration(
                enableStage2,
                enableStage3,
                stage1FuelFraction,
                stage2FuelFraction,
                stage3FuelFraction
            );
            runner.SaveCurrentShipConfiguration(saveFileName);
        }

        if (GUILayout.Button(T("Load configuration"), buttonStyle))
        {
            runner.LoadShipConfiguration(saveFileName);
        }

        GUILayout.EndArea();
    }

    float DrawFuelSlider(string label, float value)
    {
        GUILayout.Label(
            label + ": " + Mathf.RoundToInt(value * 100.0f) + "%",
            labelStyle
        );
        return GUILayout.HorizontalSlider(value, 0.0f, 1.0f);
    }

    void EnsureStyles()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = fontSize;
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = GetOpaquePanelBackground();
            boxStyle.onNormal.background = GetOpaquePanelBackground();
            boxStyle.border = new RectOffset(8, 8, 8, 8);
            boxStyle.padding = new RectOffset(10, 10, 10, 10);
        }

        labelStyle.fontSize = fontSize;
    }

    Texture2D GetOpaquePanelBackground()
    {
        if (opaquePanelBackground == null)
        {
            opaquePanelBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            opaquePanelBackground.SetPixel(0, 0, new Color(0.11f, 0.11f, 0.13f, 1f));
            opaquePanelBackground.Apply();
            opaquePanelBackground.hideFlags = HideFlags.HideAndDontSave;
        }

        return opaquePanelBackground;
    }

    static string T(string key)
    {
        return Localization.Translate(key);
    }
}
