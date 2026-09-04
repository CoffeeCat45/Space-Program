using UnityEngine;

[DefaultExecutionOrder(32000)]
public sealed class DevSettingsHud : MonoBehaviour
{
    public bool showMenu;
    public bool showShipDebugWindow;
    public int fontSize = 14;

    SimulationRunner runner;
    GUIStyle titleStyle;
    GUIStyle buttonStyle;
    GUIStyle labelStyle;
    GUIStyle boxStyle;
    GUIStyle modalBlockerStyle;
    Texture2D opaquePanelBackground;
    Texture2D transparentBackground;
    string thrustMultiplierInput = DevSettings.DefaultThrustMultiplier.ToString("0.##");
    string altitudeInput = "0";
    string velocityXInput = "0";
    string velocityYInput = "0";
    string velocityZInput = "0";
    bool wasMenuOpen;
    bool wasShipDebugOpen;
    bool menuPositionInitialized;
    bool shipDebugPositionInitialized;
    Rect menuRect;
    Rect shipDebugRect;
    Vector2 shipDebugScroll;

    const float MenuWidth = 360f;
    const float MenuHeight = 560f;
    const float ShipDebugWidth = 400f;
    const float ShipDebugHeight = 500f;
    const float TitleDragHeight = 34f;
    const float ShipDebugScrollHeight = 430f;
    const int MenuWindowId = 0x5F5F4445;
    const int ShipDebugWindowId = 0x5F5F5344;

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
            runner.IsVabEditorActive
        )
        {
            return;
        }

        EnsureStyles();

        float panelWidth = 260f;
        float buttonHeight = 28f;
        float rightMargin = 10f;
        float bottomMargin = 14f;
        float buttonX = Screen.width - panelWidth - rightMargin;
        float buttonY = Screen.height - buttonHeight - bottomMargin;

        if (!showMenu && !showShipDebugWindow)
        {
            DrawPlanetLodLoadStatus(buttonX, buttonY, panelWidth);

            if (GUI.Button(
                new Rect(buttonX, buttonY, panelWidth, buttonHeight),
                T("Developer settings"),
                buttonStyle
            ))
            {
                showMenu = true;
            }

            return;
        }

        if (showMenu || showShipDebugWindow)
        {
            DrawModalInputBlocker();
        }

        if (showMenu)
        {
            DrawDeveloperSettingsMenu();
        }

        if (showShipDebugWindow)
        {
            DrawShipDebugMenu();
        }
    }

    void DrawDeveloperSettingsMenu()
    {
        if (!wasMenuOpen)
        {
            thrustMultiplierInput = DevSettings.ThrustMultiplier.ToString("0.##");
            wasMenuOpen = true;
        }

        if (!menuPositionInitialized)
        {
            menuRect = new Rect(
                (Screen.width - MenuWidth) * 0.5f,
                (Screen.height - MenuHeight) * 0.5f,
                MenuWidth,
                MenuHeight
            );
            menuPositionInitialized = true;
        }

        ClampRect(menuRect);
        menuRect = GUI.Window(
            MenuWindowId,
            menuRect,
            DrawDeveloperSettingsWindow,
            GUIContent.none,
            boxStyle
        );
        ClampRect(menuRect);
    }

    void DrawShipDebugMenu()
    {
        if (!wasShipDebugOpen)
        {
            RefreshShipDebugInputsFromSimulation();
            wasShipDebugOpen = true;
        }

        if (!shipDebugPositionInitialized)
        {
            float initialX = Mathf.Max(12f, (Screen.width - ShipDebugWidth) * 0.5f);
            float initialY = Mathf.Max(
                12f,
                (Screen.height - ShipDebugHeight) * 0.5f
            );
            shipDebugRect = new Rect(
                initialX,
                initialY,
                ShipDebugWidth,
                ShipDebugHeight
            );
            shipDebugPositionInitialized = true;
        }

        ClampRect(shipDebugRect);
        shipDebugRect = GUI.Window(
            ShipDebugWindowId,
            shipDebugRect,
            DrawShipDebugWindow,
            GUIContent.none,
            boxStyle
        );
        ClampRect(shipDebugRect);
    }

    void DrawDeveloperSettingsWindow(int windowId)
    {
        GUILayout.Label(T("Developer settings"), titleStyle);
        GUI.DragWindow(new Rect(0f, 0f, menuRect.width, TitleDragHeight));

        DraggableGap(10f);

        string fuelLabel = DevSettings.InfiniteFuel ?
            T("Infinite fuel") + ": " + T("On") :
            T("Infinite fuel") + ": " + T("Off");

        if (GUILayout.Button(fuelLabel, buttonStyle))
        {
            DevSettings.SetInfiniteFuel(!DevSettings.InfiniteFuel);
        }

        DraggableGap(10f);
        GUILayout.Label(
            T("Thrust multiplier") + ": x" + DevSettings.ThrustMultiplier.ToString("0.##"),
            labelStyle
        );
        RegisterDragRect(GUILayoutUtility.GetLastRect());

        GUILayout.BeginHorizontal();
        GUILayout.Label(T("x"), labelStyle, GUILayout.Width(16f));
        thrustMultiplierInput = GUILayout.TextField(thrustMultiplierInput, GUILayout.Width(80f));

        if (GUILayout.Button(T("Apply"), buttonStyle, GUILayout.Width(72f)))
        {
            ApplyThrustMultiplierInput();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("x0.5", buttonStyle))
        {
            SetThrustMultiplierPreset(0.5);
        }

        if (GUILayout.Button("x1", buttonStyle))
        {
            SetThrustMultiplierPreset(1.0);
        }

        if (GUILayout.Button("x10", buttonStyle))
        {
            SetThrustMultiplierPreset(10.0);
        }
        GUILayout.EndHorizontal();

        DraggableGap(12f);

        string cloudsLabel = DevSettings.CloudVisualsDisabled ?
            T("Cloud visuals") + ": " + T("Off") :
            T("Cloud visuals") + ": " + T("On");

        if (GUILayout.Button(cloudsLabel, buttonStyle))
        {
            DevSettings.SetCloudVisualsDisabled(!DevSettings.CloudVisualsDisabled);
        }

        DraggableGap(6f);

        string atmosphereLabel = DevSettings.AtmosphereVisualsDisabled ?
            T("Atmosphere visuals") + ": " + T("Off") :
            T("Atmosphere visuals") + ": " + T("On");

        if (GUILayout.Button(atmosphereLabel, buttonStyle))
        {
            DevSettings.SetAtmosphereVisualsDisabled(!DevSettings.AtmosphereVisualsDisabled);
        }

        DraggableGap(6f);
        GUILayout.Label(
            T("Atmosphere opacity") + ": " +
            Mathf.RoundToInt(DevSettings.AtmosphereOpacity * 100f) + "%",
            labelStyle
        );
        RegisterDragRect(GUILayoutUtility.GetLastRect());
        float atmosphereOpacity = GUILayout.HorizontalSlider(
            DevSettings.AtmosphereOpacity,
            0f,
            1f
        );
        if (!Mathf.Approximately(atmosphereOpacity, DevSettings.AtmosphereOpacity))
        {
            DevSettings.SetAtmosphereOpacity(atmosphereOpacity);
        }

        DraggableGap(6f);

        string planetSpinLabel = DevSettings.PlanetSpinDisabled ?
            T("Earth spin") + ": " + T("Off") :
            T("Earth spin") + ": " + T("On");

        if (GUILayout.Button(planetSpinLabel, buttonStyle))
        {
            DevSettings.SetPlanetSpinDisabled(!DevSettings.PlanetSpinDisabled);
        }

        DraggableGap(6f);

        string shipTrailLabel = DevSettings.ShipTrailEnabled ?
            T("Ship trail") + ": " + T("On") :
            T("Ship trail") + ": " + T("Off");

        if (GUILayout.Button(shipTrailLabel, buttonStyle))
        {
            DevSettings.SetShipTrailEnabled(!DevSettings.ShipTrailEnabled);
        }

        DraggableGap(6f);

        string soiSphereLabel = DevSettings.SoiSphereEnabled ?
            T("SoI sphere") + ": " + T("On") :
            T("SoI sphere") + ": " + T("Off");

        if (GUILayout.Button(soiSphereLabel, buttonStyle))
        {
            DevSettings.SetSoiSphereEnabled(!DevSettings.SoiSphereEnabled);
        }

        DraggableGap(6f);

        string earthMapLabel = DevSettings.EarthGeographicMapHigh8K ?
            T("Earth map texture") + ": " + T("8K") :
            T("Earth map texture") + ": " + T("2K");

        if (GUILayout.Button(earthMapLabel, buttonStyle))
        {
            DevSettings.SetEarthGeographicMapHigh8K(!DevSettings.EarthGeographicMapHigh8K);
        }

        DraggableGap(6f);

        string moonMapLabel = DevSettings.MoonGeographicMapHigh8K ?
            T("Moon map texture") + ": " + T("8K") :
            T("Moon map texture") + ": " + T("4K");

        if (GUILayout.Button(moonMapLabel, buttonStyle))
        {
            DevSettings.SetMoonGeographicMapHigh8K(!DevSettings.MoonGeographicMapHigh8K);
        }

        DraggableGap(12f);

        string shipDebugLabel = showShipDebugWindow ?
            T("Ship debug panel") + ": " + T("On") :
            T("Ship debug panel") + ": " + T("Off");

        if (GUILayout.Button(shipDebugLabel, buttonStyle))
        {
            showShipDebugWindow = !showShipDebugWindow;
            wasShipDebugOpen = !showShipDebugWindow;
        }

        DraggableGap(12f);

        if (GUILayout.Button(T("Close"), buttonStyle))
        {
            showMenu = false;
            wasMenuOpen = false;
        }
    }

    void DrawShipDebugWindow(int windowId)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("Ship debug panel"), titleStyle);
        if (GUILayout.Button("X", buttonStyle, GUILayout.Width(32f), GUILayout.Height(26f)))
        {
            CloseShipDebugWindow();
        }
        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0f, 0f, shipDebugRect.width - 40f, TitleDragHeight));

        shipDebugScroll = GUILayout.BeginScrollView(
            shipDebugScroll,
            false,
            true,
            GUILayout.Width(shipDebugRect.width - 24f),
            GUILayout.Height(ShipDebugScrollHeight)
        );

        DrawShipDebugWindowBody();
        GUILayout.EndScrollView();
    }

    void DrawShipDebugWindowBody()
    {
        DraggableGap(4f);

        if (runner?.Simulation?.ship != null)
        {
            Simulation simulation = runner.Simulation;
            GUILayout.Label(
                T("Current altitude") + ": " +
                    simulation.GetShipAltitudeAbovePlanet().ToString("0") + " m",
                labelStyle
            );
            GUILayout.Label(
                T("Current speed") + ": " +
                    simulation.GetShipSpeed().ToString("0.0") + " m/s",
                labelStyle
            );
            GUILayout.Label(
                T("Vertical speed") + ": " +
                    simulation.GetShipVerticalSpeedRelativeToActivePrimary().ToString("0.0") +
                    " m/s",
                labelStyle
            );
            GUILayout.Label(
                T("Horizontal speed") + ": " +
                    simulation.GetShipHorizontalSpeedRelativeToActivePrimary().ToString("0.0") +
                    " m/s",
                labelStyle
            );
        }

        DraggableGap(6f);

        GUILayout.Label(T("Target altitude") + " (m)", labelStyle);
        altitudeInput = GUILayout.TextField(altitudeInput);

        DraggableGap(4f);
        GUILayout.Label(T("Velocity world") + " (m/s)", labelStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("X", labelStyle, GUILayout.Width(16f));
        velocityXInput = GUILayout.TextField(velocityXInput);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Y", labelStyle, GUILayout.Width(16f));
        velocityYInput = GUILayout.TextField(velocityYInput);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Z", labelStyle, GUILayout.Width(16f));
        velocityZInput = GUILayout.TextField(velocityZInput);
        GUILayout.EndHorizontal();

        DraggableGap(6f);

        if (GUILayout.Button(T("Teleport apply"), buttonStyle))
        {
            ApplyShipTeleport();
        }

        DraggableGap(4f);
        GUILayout.Label(T("Altitude presets"), labelStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(T("Pad"), buttonStyle))
        {
            SetAltitudePreset(0.0);
        }

        if (GUILayout.Button("25 km", buttonStyle))
        {
            SetAltitudePreset(25000.0);
        }

        if (GUILayout.Button("38 km", buttonStyle))
        {
            SetAltitudePreset(38000.0);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("52 km", buttonStyle))
        {
            SetAltitudePreset(52000.0);
        }

        if (GUILayout.Button("76 km", buttonStyle))
        {
            SetAltitudePreset(76000.0);
        }

        if (GUILayout.Button("85 km", buttonStyle))
        {
            SetAltitudePreset(85000.0);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("100 km", buttonStyle))
        {
            SetAltitudePreset(100000.0);
        }
        GUILayout.EndHorizontal();

        DraggableGap(8f);

        if (GUILayout.Button(T("Close"), buttonStyle))
        {
            CloseShipDebugWindow();
        }
    }

    void CloseShipDebugWindow()
    {
        showShipDebugWindow = false;
        wasShipDebugOpen = false;
        shipDebugScroll = Vector2.zero;
    }

    void DrawModalInputBlocker()
    {
        Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.Box(screen, GUIContent.none, modalBlockerStyle);
        ConsumePointerEventsOutsideRects();
    }

    void ConsumePointerEventsOutsideRects()
    {
        Event e = Event.current;

        if (e == null)
        {
            return;
        }

        if (showMenu && menuRect.Contains(e.mousePosition))
        {
            return;
        }

        if (showShipDebugWindow && shipDebugRect.Contains(e.mousePosition))
        {
            return;
        }

        Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);

        if (!screen.Contains(e.mousePosition))
        {
            return;
        }

        if (
            e.type == EventType.MouseDown ||
            e.type == EventType.MouseUp ||
            e.type == EventType.MouseDrag ||
            e.type == EventType.ScrollWheel
        )
        {
            e.Use();
        }
    }

    void DraggableGap(float height)
    {
        GUILayout.Box(GUIContent.none, GUIStyle.none, GUILayout.Height(height), GUILayout.ExpandWidth(true));
        RegisterDragRect(GUILayoutUtility.GetLastRect());
    }

    void RegisterDragRect(Rect localRect)
    {
        if (localRect.width > 0f && localRect.height > 0f)
        {
            GUI.DragWindow(localRect);
        }
    }

    void ClampRect(Rect rect)
    {
        float maxX = Mathf.Max(0f, Screen.width - rect.width);
        float maxY = Mathf.Max(0f, Screen.height - rect.height);
        rect.x = Mathf.Clamp(rect.x, 0f, maxX);
        rect.y = Mathf.Clamp(rect.y, 0f, maxY);
    }

    void RefreshShipDebugInputsFromSimulation()
    {
        if (runner?.Simulation?.ship == null)
        {
            return;
        }

        Simulation simulation = runner.Simulation;
        altitudeInput = simulation.GetShipAltitudeAbovePlanet().ToString("0");
        global::SimVector3 velocity = simulation.ship.velocity;
        velocityXInput = velocity.x.ToString("0.##");
        velocityYInput = velocity.y.ToString("0.##");
        velocityZInput = velocity.z.ToString("0.##");
    }

    void SetAltitudePreset(double altitudeMeters)
    {
        altitudeInput = altitudeMeters.ToString("0");
        velocityXInput = "0";
        velocityYInput = "0";
        velocityZInput = "0";
        ApplyShipTeleport();
    }

    void ApplyShipTeleport()
    {
        if (runner == null)
        {
            return;
        }

        if (!TryParseDouble(altitudeInput, out double altitudeMeters))
        {
            RefreshShipDebugInputsFromSimulation();
            return;
        }

        if (
            !TryParseDouble(velocityXInput, out double velocityX) ||
            !TryParseDouble(velocityYInput, out double velocityY) ||
            !TryParseDouble(velocityZInput, out double velocityZ)
        )
        {
            RefreshShipDebugInputsFromSimulation();
            return;
        }

        Vector3 velocity = new Vector3(
            (float)velocityX,
            (float)velocityY,
            (float)velocityZ
        );

        if (!runner.TryDevTeleportShip(altitudeMeters, velocity))
        {
            RefreshShipDebugInputsFromSimulation();
            return;
        }

        RefreshShipDebugInputsFromSimulation();
    }

    static bool TryParseDouble(string input, out double value)
    {
        value = 0.0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string normalized = input.Trim().Replace(',', '.');
        return double.TryParse(
            normalized,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value
        );
    }

    void DrawPlanetLodLoadStatus(float buttonX, float buttonY, float panelWidth)
    {
        const float lineHeight = 18f;
        float blockHeight = lineHeight * 2f + 6f;
        float blockY = buttonY - blockHeight - 6f;
        float blockWidth = Mathf.Max(panelWidth, 420f);
        float blockX = buttonX + panelWidth - blockWidth;

        if (!FlightPlanetSphere.TryCaptureLodLoadSnapshot(out PlanetLodLoadSnapshot snapshot))
        {
            GUI.Label(
                new Rect(blockX, blockY, blockWidth, lineHeight * 2f),
                "LOD: дерево выкл (нет quadtree / материал чанков не собрался)",
                labelStyle
            );
            return;
        }

        GUI.Label(
            new Rect(blockX, blockY, blockWidth, lineHeight),
            snapshot.SummaryLine,
            labelStyle
        );
        GUI.Label(
            new Rect(blockX, blockY + lineHeight, blockWidth, lineHeight),
            snapshot.SamplesLine,
            labelStyle
        );
    }

    void ApplyThrustMultiplierInput()
    {
        if (DevSettings.TrySetThrustMultiplierFromInput(
            thrustMultiplierInput,
            out double appliedMultiplier
        ))
        {
            thrustMultiplierInput = appliedMultiplier.ToString("0.##");
            return;
        }

        thrustMultiplierInput = DevSettings.ThrustMultiplier.ToString("0.##");
    }

    void SetThrustMultiplierPreset(double multiplier)
    {
        DevSettings.SetThrustMultiplier(multiplier, persist: true);
        thrustMultiplierInput = DevSettings.ThrustMultiplier.ToString("0.##");
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
            boxStyle.normal.background = GetOpaquePanelBackground();
            boxStyle.onNormal.background = GetOpaquePanelBackground();
            boxStyle.border = new RectOffset(8, 8, 8, 8);
            boxStyle.padding = new RectOffset(12, 12, 12, 12);
        }

        if (modalBlockerStyle == null)
        {
            modalBlockerStyle = new GUIStyle(GUI.skin.box);
            modalBlockerStyle.normal.background = GetTransparentBackground();
            modalBlockerStyle.border = new RectOffset(0, 0, 0, 0);
            modalBlockerStyle.padding = new RectOffset(0, 0, 0, 0);
        }

        titleStyle.fontSize = fontSize + 2;
        labelStyle.fontSize = fontSize;
        buttonStyle.fontSize = fontSize;
    }

    Texture2D GetOpaquePanelBackground()
    {
        if (opaquePanelBackground == null)
        {
            opaquePanelBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            opaquePanelBackground.SetPixel(0, 0, new Color(0.11f, 0.11f, 0.13f, 0.96f));
            opaquePanelBackground.Apply();
            opaquePanelBackground.hideFlags = HideFlags.HideAndDontSave;
        }

        return opaquePanelBackground;
    }

    Texture2D GetTransparentBackground()
    {
        if (transparentBackground == null)
        {
            transparentBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            transparentBackground.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.001f));
            transparentBackground.Apply();
            transparentBackground.hideFlags = HideFlags.HideAndDontSave;
        }

        return transparentBackground;
    }

    static string T(string key)
    {
        return Localization.Translate(key);
    }
}
