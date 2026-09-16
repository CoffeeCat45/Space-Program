using UnityEngine;
public class SimulationScenarioMenu : MonoBehaviour
{
    public bool showMenu;
    public int fontSize = 14;

    SimulationRunner runner;
    GUIStyle titleStyle;
    GUIStyle buttonStyle;
    GUIStyle labelStyle;
    GUIStyle boxStyle;

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

        if (!FlightDebugSettings.IsVisible(FlightDebugOverlay.Scenarios))
        {
            showMenu = false;
        }

        if (FlightDebugChromeLayout.TryGetTopRightButtonRect(
            FlightDebugOverlay.Scenarios,
            out Rect buttonRect
        ) && GUI.Button(buttonRect, T("Scenarios"), buttonStyle))
        {
            showMenu = !showMenu;
        }

        if (!showMenu)
        {
            return;
        }

        float panelWidth = 320.0f;
        float panelHeight = 510.0f;
        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = (Screen.height - panelHeight) * 0.5f;

        GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), boxStyle);
        GUILayout.Label(T("Scenario menu"), titleStyle);
        GUILayout.Space(8);
        GUILayout.Label(
            T("Current scenario") + ": " +
            T(runner.GetScenarioDisplayKey()),
            labelStyle
        );
        GUILayout.Space(10);

        SimulationScenario[] scenarios = runner.GetMenuScenarios();

        for (int i = 0; i < scenarios.Length; i++)
        {
            SimulationScenario menuScenario = scenarios[i];

            if (GUILayout.Button(T(runner.GetScenarioDisplayKey(menuScenario)), buttonStyle))
            {
                runner.LoadScenario(menuScenario);
                showMenu = false;
            }
        }

        GUILayout.Space(12);

        if (GUILayout.Button(T("Restart scenario"), buttonStyle))
        {
            runner.RestartCurrentScenario();
            showMenu = false;
        }

        GUILayout.Space(8);
        GUILayout.EndArea();
    }

    void EnsureStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.normal.textColor = Color.white;
            titleStyle.fontStyle = FontStyle.Bold;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button);
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
        }

        titleStyle.fontSize = fontSize + 2;
        labelStyle.fontSize = fontSize;
        buttonStyle.fontSize = fontSize;
    }

    static string T(string key)
    {
        return Localization.Translate(key);
    }
}
