using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SimulationHud : MonoBehaviour
{
    public bool showHud = true;
    public bool showDetails;
    public bool showComDebug;
    public int fontSize = 12;

    ISimulationStateView simulation;
    SimulationRunner runner;
    GUIStyle labelStyle;
    GUIStyle mapModeIndicatorStyle;
    string mpServerHostInput = "127.0.0.1";
    string mpServerPortInput = "27777";
    private bool showMultiplayerPanel;
    int mpPreferredPlayerSlot = 1;
    float leftHudPanelHeight = 160f;
    float debugHudPanelHeight = 88f;
    Texture2D hudCornerTexture;

    internal void SetSimulation(ISimulationStateView newSimulation)
    {
        simulation = newSimulation;
    }

    internal void SetRunner(SimulationRunner newRunner)
    {
        runner = newRunner;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (
            keyboard != null &&
            keyboard.hKey != null &&
            keyboard.hKey.wasPressedThisFrame
        )
        {
            showDetails = !showDetails;
        }

        if (
            keyboard != null &&
            keyboard.bKey != null &&
            keyboard.bKey.wasPressedThisFrame
        )
        {
            showComDebug = !showComDebug;
        }

        HandleMainMenuKeyboardShortcuts(keyboard);
    }

    void HandleMainMenuKeyboardShortcuts(Keyboard keyboard)
    {
        if (runner == null || !runner.IsMainMenuVisible || keyboard == null)
        {
            return;
        }

        VabOptionsUi optionsUi = VabOptionsUi.TryGetInstance();
        if (optionsUi != null && optionsUi.IsOpen)
        {
            return;
        }

        if (FlightHudSettingsUi.IsOpen)
        {
            return;
        }

        if (keyboard.digit1Key != null && keyboard.digit1Key.wasPressedThisFrame)
        {
            Debug.Log("SimulationHud: menu shortcut 1 — Cape Canaveral.");
            runner.LaunchPlanetSurfaceStart();
        }
        else if (keyboard.digit2Key != null && keyboard.digit2Key.wasPressedThisFrame)
        {
            Debug.Log("SimulationHud: menu shortcut 2 — Morocco.");
            runner.LaunchPlanetSurfaceMorocco();
        }
        else if (keyboard.digit3Key != null && keyboard.digit3Key.wasPressedThisFrame)
        {
            Debug.Log("SimulationHud: menu shortcut 3 — Tibet.");
            runner.LaunchPlanetSurfaceTibet();
        }
        else if (keyboard.digit4Key != null && keyboard.digit4Key.wasPressedThisFrame)
        {
            Debug.Log("SimulationHud: menu shortcut 4 — Greenwich LOD test (0°N 0°E).");
            runner.LaunchPlanetSurfaceGreenwich();
        }
        else if (keyboard.digit5Key != null && keyboard.digit5Key.wasPressedThisFrame)
        {
            Debug.Log("SimulationHud: menu shortcut 5 — ThreePoints probe 1 (Andes 80°W).");
            runner.LaunchThreePointsTruthProbe(0);
        }
        else if (keyboard.digit6Key != null && keyboard.digit6Key.wasPressedThisFrame)
        {
            Debug.Log("SimulationHud: menu shortcut 6 — ThreePoints probe 2 (Indian Ocean 60°E).");
            runner.LaunchThreePointsTruthProbe(1);
        }
        else if (keyboard.digit7Key != null && keyboard.digit7Key.wasPressedThisFrame)
        {
            Debug.Log("SimulationHud: menu shortcut 7 — ThreePoints probe 3 (Cape 80.6°W).");
            runner.LaunchThreePointsTruthProbe(2);
        }
        else if (keyboard.vKey != null && keyboard.vKey.wasPressedThisFrame)
        {
            Debug.Log("SimulationHud: menu shortcut V — VAB editor.");
            runner.EnterVabEditor();
        }
    }

    void OnGUI()
    {
        if (runner != null)
        {
            runner.DrawPerformanceOverlayGui();
            runner.DrawCenterHudMessageGui();
        }

        EnsureLabelStyle();

        if (runner != null && runner.IsMainMenuVisible)
        {
            VabOptionsUi optionsUi = VabOptionsUi.TryGetInstance();
            bool settingsOpen =
                (optionsUi != null && optionsUi.IsOpen) ||
                FlightHudSettingsUi.IsOpen;

            if (!settingsOpen)
            {
                DrawMainMenu();
                DrawMultiplayerPanelToggle();
                if (showMultiplayerPanel)
                {
                    DrawMultiplayerPanel();
                }
            }

            return;
        }

        if (runner != null && runner.IsVabEditorActive)
        {
            return;
        }

        if (FlightHudSettingsUi.IsOpen)
        {
            return;
        }

        DrawMultiplayerPanelToggle();
        if (showMultiplayerPanel)
        {
            DrawMultiplayerPanel();
        }

        if (!showHud || simulation == null)
        {
            return;
        }

        DrawMapModeIndicator();

        DrawCompassHud();
        bool showGraphicsBridge = AnyDebugGraphicsBridgeVisible();
        float leftPanelTop = runner != null && runner.IsMapModeActive ? 34f : 10f;
        if (showGraphicsBridge)
        {
            DrawDebugGraphicsBridgePanel(leftPanelTop);
            leftPanelTop += debugHudPanelHeight + 8f;
        }

        DrawTerrainGraphicsToggleButton();
        DrawComDebugToggleButton();

        DrawAdaptiveHudPanel(
            10f,
            leftPanelTop,
            470f,
            ref leftHudPanelHeight,
            DrawLeftFlightHudContent
        );
    }

    void DrawLeftFlightHudContent()
    {
        DrawLanguageSelector();
        DrawFlightSection();

        if (runner != null)
        {
            DrawRunnerHud();
        }

        if (showDetails)
        {
            DrawControlsSection();
        }
        else if (ShowHudField(FlightHudField.Keys))
        {
            GUILayout.Space(6);
            GUILayout.Label(T("H: details"), labelStyle);
            GUILayout.Label("B: CoM / blocks", labelStyle);
        }

        if (showComDebug)
        {
            DrawComDebugSection();
        }
    }

    void EnsureLabelStyle()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
        }

        labelStyle.fontSize = fontSize;
    }

    void DrawMainMenu()
    {
        bool canResume = runner != null && runner.IsPauseMenuOpen;

        if (canResume)
        {
            DrawPauseMenuBackdrop();
        }

        const float menuWidth = 440f;
        float menuHeight = canResume ? 560f : 564f;
        Rect menuRect = new Rect(
            (Screen.width - menuWidth) * 0.5f,
            (Screen.height - menuHeight) * 0.5f,
            menuWidth,
            menuHeight
        );

        GUIStyle menuBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(18, 18, 18, 18),
        };
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };
        GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.85f, 0.9f, 1.0f) },
        };
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fixedHeight = 38f,
        };

        GUILayout.BeginArea(menuRect, menuBoxStyle);
        GUILayout.Label("Space Program", titleStyle);
        GUILayout.Label(canResume ? "Пауза" : "Главное меню", subtitleStyle);
        if (!canResume)
        {
            GUILayout.Label(
                "Если клик не работает: кликни вкладку Game, затем клавиши 1 / 2 / 3 / 4 / V",
                subtitleStyle
            );
            GUILayout.Label(
                "4 = Гринвич 0°N 0°E (LOD тест) | ThreePointsOfTruth: 5 / 6 / 7",
                subtitleStyle
            );
        }
        GUILayout.Space(16);

        if (canResume)
        {
            if (GUILayout.Button("Продолжить", buttonStyle))
            {
                runner.ClosePauseMenu();
            }

            GUILayout.Space(8);
        }

        if (GUILayout.Button("Старт с Мыса Канаверал", buttonStyle))
        {
            Debug.Log("SimulationHud: clicked Cape Canaveral launch.");
            runner.LaunchPlanetSurfaceStart();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Старт в Марокко", buttonStyle))
        {
            runner.LaunchPlanetSurfaceMorocco();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Старт в Тибете", buttonStyle))
        {
            runner.LaunchPlanetSurfaceTibet();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Гринвич 0°N 0°E — LOD тест", buttonStyle))
        {
            Debug.Log("SimulationHud: clicked Greenwich LOD diagnostic spawn.");
            runner.LaunchPlanetSurfaceGreenwich();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Посадка на Луну (LOD)", buttonStyle))
        {
            Debug.Log("SimulationHud: clicked Moon pad spawn.");
            runner.LaunchMoonSurfaceStart();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Редактор (VAB)", buttonStyle))
        {
            runner.EnterVabEditor();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Настройки", buttonStyle))
        {
            VabOptionsUi optionsUi = VabOptionsUi.TryGetInstance();
            if (optionsUi != null)
            {
                optionsUi.OpenPanel();
            }
        }

        if (GUILayout.Button("Выход", buttonStyle))
        {
            Application.Quit();
        }

        GUILayout.EndArea();
    }

    void DrawPauseMenuBackdrop()
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    void DrawMultiplayerPanelToggle()
    {
        const float rightMargin = 10f;
        const float panelWidth = 260f;
        const float topMargin = 10f;
        const float configurationButtonHeight = 28f;
        const float scenariosButtonHeight = 30f;
        const float networkButtonHeight = 28f;
        const float buttonGap = 8f;
        float buttonX = Screen.width - panelWidth - rightMargin;
        float scenariosButtonY = topMargin + configurationButtonHeight + buttonGap;
        float buttonY = scenariosButtonY + scenariosButtonHeight + buttonGap;

        if (GUI.Button(
            new Rect(buttonX, buttonY, panelWidth, networkButtonHeight),
            "🌐 Сеть"
        ))
        {
            showMultiplayerPanel = !showMultiplayerPanel;
        }
    }

    void DrawMultiplayerPanel()
    {
        const float rightMargin = 10f;
        const float panelWidth = 260f;
        const float topMargin = 10f;
        const float configurationButtonHeight = 28f;
        const float scenariosButtonHeight = 30f;
        const float networkButtonHeight = 28f;
        const float buttonGap = 8f;
        float buttonX = Screen.width - panelWidth - rightMargin;
        float scenariosButtonY = topMargin + configurationButtonHeight + buttonGap;
        float networkButtonY = scenariosButtonY + scenariosButtonHeight + buttonGap;
        float panelY = networkButtonY + networkButtonHeight + buttonGap;
        Rect panelRect = new Rect(buttonX, panelY, panelWidth, 260f);
        GUIStyle panelBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 10, 10),
        };

        GUILayout.BeginArea(panelRect, panelBoxStyle);
        DrawSectionHeader("Multiplayer");

        if (runner == null)
        {
            GUILayout.EndArea();
            return;
        }

        if (runner.IsMultiplayerMode)
        {
            GUILayout.Label(
                "MP: vessel " + runner.GetAssignedVesselId() +
                (runner.IsLockstepReady ?
                    " | sync OK" :
                    " | ожидание снимка..."),
                labelStyle
            );

            if (runner.IsLockstepReady)
            {
                GUILayout.Label(
                    "UDP пакетов: " + runner.diagPacketsReceivedInLoop,
                    labelStyle
                );
                GUILayout.Label(
                    "Буфер: " + runner.GetNetworkBufferCount(),
                    labelStyle
                );
                GUILayout.Label(
                    "Тиков Unity: " + runner.diagPacketsDequeued,
                    labelStyle
                );
                GUILayout.Label(
                    "Статус: " + runner.GetLockstepDiagMessage(),
                    labelStyle
                );
            }

            GUILayout.EndArea();
            return;
        }

        if (mpServerHostInput == null)
        {
            mpServerHostInput = "127.0.0.1";
        }

        if (mpServerPortInput == null)
        {
            mpServerPortInput = "27777";
        }

        GUILayout.Label("IP сервера:", labelStyle);
        mpServerHostInput = GUILayout.TextField(mpServerHostInput);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Порт:", labelStyle, GUILayout.Width(40));
        mpServerPortInput = GUILayout.TextField(mpServerPortInput, GUILayout.Width(80));
        GUILayout.EndHorizontal();

        GUILayout.Label("Слот:", labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(mpPreferredPlayerSlot == 1, "Игрок 1", GUI.skin.button))
        {
            mpPreferredPlayerSlot = 1;
        }

        if (GUILayout.Toggle(mpPreferredPlayerSlot == 2, "Игрок 2", GUI.skin.button))
        {
            mpPreferredPlayerSlot = 2;
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Подключиться"))
        {
            if (!int.TryParse(mpServerPortInput, out int port))
            {
                port = 27777;
            }

            runner.ConnectMultiplayer(
                mpServerHostInput,
                port,
                mpPreferredPlayerSlot
            );
        }

        GUILayout.EndArea();
    }

    void DrawComDebugSection()
    {
        DrawSectionHeader("CoM / Blocks");
        SimVector3 comLocal = simulation.GetShipCenterOfMassLocal();
        GUILayout.Label(
            "CoM local: " +
            FormatNumber(comLocal.x) + ", " +
            FormatNumber(comLocal.y) + ", " +
            FormatNumber(comLocal.z),
            labelStyle
        );
        GUILayout.Label(
            "Envelope R: " + FormatNumber(simulation.GetShipEnvelopeRadius()),
            labelStyle
        );

        ShipBlockView[] blocks = simulation.GetShipBlocks();

        for (int i = 0; i < blocks.Length; i++)
        {
            ShipBlockView block = blocks[i];
            GUILayout.Label(
                "S" + (block.stageIndex + 1) + " " +
                block.id + " [" + block.blockType + "] m=" +
                FormatNumber(block.mass),
                labelStyle
            );
        }
    }

    void DrawMapModeIndicator()
    {
        if (runner == null || !runner.IsMapModeActive)
        {
            return;
        }

        EnsureMapModeIndicatorStyle();

        const float width = 520f;
        const float height = 24f;
        Rect indicatorRect = new Rect(10f, 8f, width, height);
        GUI.Label(
            indicatorRect,
            "РЕЖИМ КАРТЫ (ОРБИТАЛЬНАЯ НАВИГАЦИЯ)",
            mapModeIndicatorStyle
        );
    }

    void EnsureMapModeIndicatorStyle()
    {
        if (mapModeIndicatorStyle == null)
        {
            mapModeIndicatorStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            mapModeIndicatorStyle.normal.textColor = new Color(0.35f, 0.95f, 0.45f);
        }

        mapModeIndicatorStyle.fontSize = fontSize + 1;
    }

    void DrawCompassHud()
    {
        if (runner == null || !runner.IsInitialized)
        {
            return;
        }

        runner.GetNavBallAngles(out double pitch, out double heading, out double roll);

        EnsureLabelStyle();
        GUIStyle compassStyle = new GUIStyle(labelStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize + 2,
        };

        const float width = 460f;
        const float height = 30f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            Screen.height - height - 14f,
            width,
            height
        );

        string text = string.Format(
            "HDG {0:0}°   PIT {1:+0.0;-0.0;+0.0}°   ROL {2:+0.0;-0.0;+0.0}°",
            heading,
            pitch,
            roll
        );
        GUI.Label(rect, text, compassStyle);
    }

    void DrawTerrainGraphicsToggleButton()
    {
        if (runner == null || !runner.IsInitialized || runner.IsMainMenuVisible)
        {
            return;
        }

        TerrainGraphicsDebugPanel panel = runner.GetComponent<TerrainGraphicsDebugPanel>();

        if (panel == null)
        {
            return;
        }

        EnsureLabelStyle();
        GetRightDebugButtonRect(0, out float x, out float y, out float width, out float height);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = fontSize + 1,
        };

        string label = panel.IsUserVisible ? "GFX: ON" : "GFX: OFF";

        if (GUI.Button(new Rect(x, y, width, height), label, buttonStyle))
        {
            panel.ToggleUserVisible();
        }
    }

    void DrawComDebugToggleButton()
    {
        if (runner == null || !runner.IsInitialized || runner.IsMainMenuVisible)
        {
            return;
        }

        EnsureLabelStyle();
        GetRightDebugButtonRect(1, out float x, out float y, out float width, out float height);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = fontSize + 1,
        };

        string label = showComDebug ? "CoM: ON" : "CoM: OFF";

        if (GUI.Button(new Rect(x, y, width, height), label, buttonStyle))
        {
            showComDebug = !showComDebug;
        }
    }

    static void GetRightDebugButtonRect(
        int stackIndexFromTop,
        out float x,
        out float y,
        out float width,
        out float height
    )
    {
        const float buttonWidth = 112f;
        const float buttonHeight = 34f;
        const float margin = 16f;
        const float gap = 8f;
        x = Screen.width - buttonWidth - margin;
        float bottomY = Screen.height * 0.58f - buttonHeight * 0.5f;
        y = bottomY - (1 - stackIndexFromTop) * (buttonHeight + gap);
        width = buttonWidth;
        height = buttonHeight;
    }

    void DrawDebugGraphicsBridgePanel(float top)
    {
        if (runner == null || simulation == null)
        {
            return;
        }

        EnsureLabelStyle();
        GraphicsBridgeDebugSnapshot debug = runner.GetGraphicsBridgeDebug();
        string modeLabel = debug.IsMapMode ? "Map" : "Flight";

        DrawAdaptiveHudPanel(
            10f,
            top,
            420f,
            ref debugHudPanelHeight,
            () =>
            {
                GUILayout.Label("[DEBUG GRAPHICS BRIDGE]", labelStyle);
                DrawHudLabel(FlightHudField.DebugGraphicsMode, "Mode: " + modeLabel);
                DrawHudLabel(
                    FlightHudField.DebugGraphicsShip,
                    "Ship pos: " +
                    FormatVector3(debug.ShipUnityPosition) +
                    "  scale: " +
                    FormatVector3(debug.ShipUnityLocalScale)
                );
                DrawHudLabel(
                    FlightHudField.DebugGraphicsPlanet,
                    "Planet pos: " +
                    FormatVector3(debug.PlanetUnityPosition) +
                    "  scale: " +
                    FormatVector3(debug.PlanetUnityLocalScale)
                );
                DrawHudLabel(
                    FlightHudField.DebugGraphicsUnityDistance,
                    "Unity distance ship-planet: " +
                    FormatNumber(debug.UnitySceneDistance)
                );
                DrawHudLabel(
                    FlightHudField.DebugGraphicsSimDistance,
                    T("Ship to planet center") + " (sim): " +
                    FormatNumber(simulation.GetShipDistanceToPlanetCenter())
                );
            }
        );
    }

    void DrawAdaptiveHudPanel(
        float x,
        float y,
        float width,
        ref float cachedHeight,
        Action drawContent
    )
    {
        const float pad = 8f;
        float maxHeight = Mathf.Max(48f, Screen.height - y - 10f);
        float backdropHeight = Mathf.Clamp(cachedHeight, 36f, maxHeight);
        DrawHudBackdrop(new Rect(x, y, width, backdropHeight));

        GUILayout.BeginArea(new Rect(x, y, width, maxHeight));
        GUILayout.BeginVertical();
        GUILayout.Space(pad);
        GUILayoutUtility.GetRect(0f, 0f, GUILayout.Height(0f));
        Rect contentTop = GUILayoutUtility.GetLastRect();
        drawContent();
        GUILayoutUtility.GetRect(0f, 0f, GUILayout.Height(0f));
        Rect contentBottom = GUILayoutUtility.GetLastRect();
        GUILayout.Space(pad);
        GUILayout.EndVertical();

        if (Event.current.type == EventType.Repaint)
        {
            float contentHeight = contentBottom.yMax - contentTop.yMin;
            cachedHeight = Mathf.Clamp(contentHeight + pad * 2f + 4f, 36f, maxHeight);
        }

        GUILayout.EndArea();
    }

    void DrawHudBackdrop(Rect rect)
    {
        Color background = FlightHudSettings.GetBackgroundColor();
        if (background.a <= 0.01f || rect.width < 4f || rect.height < 4f)
        {
            return;
        }

        const float cornerRadius = 10f;
        float radius = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
        Color previous = GUI.color;
        GUI.color = background;
        Texture2D fill = Texture2D.whiteTexture;
        Texture2D corner = GetHudCornerTexture();

        GUI.DrawTexture(
            new Rect(rect.x, rect.y + radius, rect.width, rect.height - radius * 2f),
            fill
        );
        GUI.DrawTexture(
            new Rect(rect.x + radius, rect.y, rect.width - radius * 2f, radius),
            fill
        );
        GUI.DrawTexture(
            new Rect(rect.x + radius, rect.yMax - radius, rect.width - radius * 2f, radius),
            fill
        );
        GUI.DrawTextureWithTexCoords(
            new Rect(rect.x, rect.y, radius, radius),
            corner,
            new Rect(0f, 0.5f, 0.5f, 0.5f),
            true
        );
        GUI.DrawTextureWithTexCoords(
            new Rect(rect.xMax - radius, rect.y, radius, radius),
            corner,
            new Rect(0.5f, 0.5f, 0.5f, 0.5f),
            true
        );
        GUI.DrawTextureWithTexCoords(
            new Rect(rect.x, rect.yMax - radius, radius, radius),
            corner,
            new Rect(0f, 0f, 0.5f, 0.5f),
            true
        );
        GUI.DrawTextureWithTexCoords(
            new Rect(rect.xMax - radius, rect.yMax - radius, radius, radius),
            corner,
            new Rect(0.5f, 0f, 0.5f, 0.5f),
            true
        );
        GUI.color = previous;
    }

    Texture2D GetHudCornerTexture()
    {
        if (hudCornerTexture != null)
        {
            return hudCornerTexture;
        }

        const int size = 32;
        hudCornerTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float alpha = Mathf.Clamp01(center - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                hudCornerTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        hudCornerTexture.Apply(false, true);
        return hudCornerTexture;
    }

    void OnDestroy()
    {
        if (hudCornerTexture != null)
        {
            Destroy(hudCornerTexture);
            hudCornerTexture = null;
        }
    }

    static bool AnyDebugGraphicsBridgeVisible()
    {
        return FlightHudSettings.AnyVisible(
            FlightHudField.DebugGraphicsMode,
            FlightHudField.DebugGraphicsShip,
            FlightHudField.DebugGraphicsPlanet,
            FlightHudField.DebugGraphicsUnityDistance,
            FlightHudField.DebugGraphicsSimDistance
        );
    }

    string FormatVector3(Vector3 value)
    {
        return value.x.ToString("0.00") + ", " +
            value.y.ToString("0.00") + ", " +
            value.z.ToString("0.00");
    }

    void DrawFlightSection()
    {
        DrawSectionHeader(
            "Flight",
            FlightHudField.SpeedAccel,
            FlightHudField.MassFuel,
            FlightHudField.Stage,
            FlightHudField.Status,
            FlightHudField.VerticalSurfaceGlide
        );
        DrawHudLabel(
            FlightHudField.SpeedAccel,
            T("Speed") + " (" + T(simulation.GetShipHudSpeedModeLabel()) + "): " +
            FormatNumber(simulation.GetShipHudSpeed()) +
            "  " + T("Accel") + ": " +
            FormatNumber(simulation.GetShipAcceleration())
        );
        DrawHudLabel(
            FlightHudField.MassFuel,
            T("Mass") + ": " +
            FormatNumber(simulation.GetShipMass()) +
            "  " + T("Fuel") + ": " +
            FormatNumber(simulation.GetShipFuelMass()) +
            " (" +
            FormatPercent(simulation.GetShipFuelRatio()) +
            ")"
        );
        DrawHudLabel(
            FlightHudField.Stage,
            T("Stage") + ": " + simulation.GetActiveStageName() +
            " (" + simulation.GetActiveStageNumber() +
            "/" + simulation.GetRemainingStageCount() + ")"
        );
        DrawHudLabel(
            FlightHudField.Status,
            T("Status") + ": " + T(simulation.GetFlightStatusName()) +
            GetLandedOnSuffix()
        );

        if (
            simulation.GetFlightStatus() != FlightStatus.Flying ||
            simulation.IsShipInAtmosphere() ||
            simulation.ShouldShowDescentPreview()
        )
        {
            DrawHudLabel(
                FlightHudField.VerticalSurfaceGlide,
                T("Vertical speed") + ": " +
                FormatNumber(simulation.GetShipVerticalSpeedRelativeToActivePrimary()) +
                "  " + T("Surface speed") + ": " +
                FormatNumber(simulation.GetShipSurfaceSpeedRelativeToActivePrimary()) +
                "  " + T("Glide ratio") + ": " +
                FormatNumber(simulation.GetShipGlideRatio())
            );
        }
    }

    string GetLandedOnSuffix()
    {
        if (simulation.GetFlightStatus() != FlightStatus.Landed)
        {
            return string.Empty;
        }

        string landedOn = simulation.GetLandedOnBodyName();

        if (string.IsNullOrEmpty(landedOn))
        {
            return string.Empty;
        }

        return " (" + T("Landed on") + " " + landedOn + ")";
    }

    void DrawRunnerHud()
    {
        global::SimVector3 plannedDeltaV = runner.GetPlannedDeltaV();
        ClosestApproachInfo closestApproach =
            runner.GetCachedClosestApproach();
        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview =
            runner.GetCachedSphereOfInfluenceEntryPreview();

        GUILayout.Space(4);
        string statusLine =
            T("Orientation") + ": " +
            TranslateOrientationMode(runner.GetShipOrientationModeName());

        if (runner.IsPaused && !runner.IsMultiplayerMode)
        {
            statusLine += "  |  " + T("Paused");
        }
        else if (runner.IsMultiplayerMode && runner.IsLockstepTickPaused())
        {
            statusLine += "  |  " + T("Paused") + " (MP)";
        }

        if (runner.IsWarping())
        {
            statusLine += "  |  " + T("Warping to") + " " + runner.GetWarpLabel();
        }

        if (!runner.IsCameraFocusedOnShip())
        {
            statusLine += "  |  " + T("Camera") + ": " + runner.GetCameraFocusName();
        }

        DrawHudLabel(FlightHudField.Orientation, statusLine);

        DrawOrbitSection();
        DrawTargetSection(closestApproach, sphereOfInfluenceEntryPreview);
        DrawEventsSection();
        DrawMissionSection();
        DrawFlightControlSection();

        if (simulation.IsShipInAtmosphere())
        {
            DrawAtmosphereSection();
        }
        else if (simulation.ShouldShowDescentPreview())
        {
            DrawDescentPreviewToggleSection();
        }

        DrawManeuverHud(plannedDeltaV);

        if (showDetails)
        {
            DrawDetails(plannedDeltaV, closestApproach);
        }
    }

    void DrawOrbitSection()
    {
        DrawSectionHeader(
            "Orbit",
            FlightHudField.OrbitBodyAlt,
            FlightHudField.OrbitType,
            FlightHudField.OrbitPeAp,
            FlightHudField.OrbitTimeToPeAp,
            FlightHudField.OrbitCircularize
        );
        DrawHudLabel(
            FlightHudField.OrbitBodyAlt,
            T("Body") + ": " + simulation.ActivePrimaryBodyName +
            "  " + T("Alt") + ": " +
            FormatNumber(simulation.GetShipAltitudeAboveActivePrimary())
        );
        DrawHudLabel(
            FlightHudField.OrbitType,
            T("Type") + ": " + TranslateOrbitType(simulation.GetShipOrbitTypeRelativeToActivePrimary())
        );
        DrawHudLabel(
            FlightHudField.OrbitPeAp,
            T("Pe") + ": " +
            FormatNumber(simulation.GetShipPeriapsisAltitudeRelativeToActivePrimary()) +
            "  " + T("Ap") + ": " +
            FormatNumber(simulation.GetShipApoapsisAltitudeRelativeToActivePrimary())
        );
        DrawHudLabel(
            FlightHudField.OrbitTimeToPeAp,
            T("Time to Pe") + ": " +
            FormatTime(runner.GetTimeToPeriapsis()) +
            "  " + T("Time to Ap") + ": " +
            FormatTime(runner.GetTimeToApoapsis())
        );
        DrawHudLabel(
            FlightHudField.OrbitCircularize,
            T("Circularize") + ": " +
            FormatNumber(
                simulation
                    .GetShipCircularizeDeltaVRelativeToActivePrimary()
                    .Length()
            )
        );
    }

    void DrawTargetSection(
        ClosestApproachInfo closestApproach,
        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview
    )
    {
        DrawSectionHeader(
            "Target",
            FlightHudField.TargetBodyDist,
            FlightHudField.TargetRelVel,
            FlightHudField.TargetPortGap,
            FlightHudField.TargetClosestApproach,
            FlightHudField.TargetDockingMessage,
            FlightHudField.TargetSoiEncounter
        );
        DrawHudLabel(
            FlightHudField.TargetBodyDist,
            T("Target body") + ": " +
            runner.GetTargetBodyName() +
            "  " + T("Dist") + ": " +
            FormatNumber(runner.GetTargetDistance())
        );
        DrawHudLabel(
            FlightHudField.TargetRelVel,
            T("Rel vel to target") + ": " +
            FormatCompactRelativeVelocity(
                runner.GetTargetRelativeVelocity(),
                runner.GetTargetClosureRate()
            )
        );

        double portGap = runner.GetDockingPortSeparationToTarget();

        if (!double.IsNaN(portGap))
        {
            DrawHudLabel(
                FlightHudField.TargetPortGap,
                T("Port gap") + ": " + FormatNumber(portGap)
            );
        }

        DrawHudLabel(
            FlightHudField.TargetClosestApproach,
            T("Target CA") + ": " +
            FormatNumber(closestApproach.distance) +
            T(" in ") +
            FormatTime(closestApproach.time)
        );

        string dockingMessage = runner.GetLastDockingMessage();

        if (!string.IsNullOrEmpty(dockingMessage))
        {
            DrawHudLabel(FlightHudField.TargetDockingMessage, dockingMessage);
        }

        if (closestApproach.entersMoonSphereOfInfluence)
        {
            DrawHudLabel(FlightHudField.TargetSoiEncounter, T("Moon SoI encounter"));
        }

        if (sphereOfInfluenceEntryPreview.entersSphereOfInfluence)
        {
            string soiLabel = sphereOfInfluenceEntryPreview.targetBodyName == "Sun" ?
                "Sun SoI" :
                "Moon SoI";
            string timeLabel = sphereOfInfluenceEntryPreview.targetBodyName == "Sun" ?
                "Time to Sun SoI" :
                "Time to Moon SoI";

            DrawHudLabel(
                FlightHudField.TargetSoiEncounter,
                T(timeLabel) + ": " +
                FormatTime(sphereOfInfluenceEntryPreview.entryTime)
            );
            DrawHudLabel(
                FlightHudField.TargetSoiEncounter,
                T(soiLabel + " encounter")
            );
        }
    }

    void DrawMissionSection()
    {
        if (
            runner == null ||
            !runner.PlaySession.ShouldShowMissionHints(runner.scenario)
        )
        {
            return;
        }

        string hintKey = runner.PlaySession.GetMissionHintKey();

        if (string.IsNullOrEmpty(hintKey))
        {
            return;
        }

        DrawSectionHeader("Mission", FlightHudField.MissionHint);
        DrawHudLabel(FlightHudField.MissionHint, T(hintKey));

        if (runner.PlaySession.MissionPhase == PlayMissionPhase.Crashed)
        {
            DrawHudLabel(FlightHudField.MissionHint, T("Mission hint restart keys"));
        }
        else if (runner.PlaySession.MissionPhase == PlayMissionPhase.Complete)
        {
            DrawHudLabel(FlightHudField.MissionHint, T("Mission hint menu restart"));
        }
    }

    void DrawFlightControlSection()
    {
        DrawSectionHeader(
            "Control",
            FlightHudField.ControlThrustThrottle,
            FlightHudField.ControlTwr,
            FlightHudField.ControlAutoBurn
        );
        DrawHudLabel(
            FlightHudField.ControlThrustThrottle,
            T("Thrust accel") + ": " +
            FormatNumber(simulation.GetShipThrustAcceleration()) +
            "  " + T("Throttle") + ": " +
            FormatPercent(runner.GetThrottle())
        );
        DrawHudLabel(
            FlightHudField.ControlTwr,
            T("TWR") + ": " +
            FormatTwr(GetDisplayedThrustToWeightRatio()) +
            "  (" + T("TWR max") + " " +
            FormatTwr(simulation.GetShipMaxThrustToWeightRatio()) + ")"
        );

        if (runner.IsAutoBurnActive())
        {
            DrawHudLabel(
                FlightHudField.ControlAutoBurn,
                runner.IsAutoBurning() ?
                T("Auto-burn: firing") :
                T("Auto-burn: waiting")
            );
        }
    }

    void DrawAtmosphereSection()
    {
        DrawSectionHeader(
            "Atmosphere",
            FlightHudField.AtmosphereDensityDrag,
            FlightHudField.AtmosphereQDragAccel,
            FlightHudField.AtmosphereAoALift,
            FlightHudField.AtmosphereArea,
            FlightHudField.AtmosphereHeating,
            FlightHudField.AtmospherePreview,
            FlightHudField.DescentDrift
        );
        DrawHudLabel(
            FlightHudField.AtmosphereDensityDrag,
            T("Density") + ": " +
            FormatNumber(simulation.GetShipAtmosphericDensity()) +
            "  " + T("Drag") + ": " +
            FormatNumber(simulation.GetShipDragForce())
        );
        DrawHudLabel(
            FlightHudField.AtmosphereQDragAccel,
            T("Dynamic pressure") + ": " +
            FormatNumber(simulation.GetShipDynamicPressure()) +
            "  " + T("Drag accel") + ": " +
            FormatNumber(simulation.GetShipDragAcceleration())
        );
        DrawHudLabel(
            FlightHudField.AtmosphereAoALift,
            T("Angle of attack") + ": " +
            FormatNumber(simulation.GetShipAngleOfAttackDegrees()) + "°" +
            "  " + T("Lift") + ": " +
            FormatNumber(simulation.GetShipLiftForce())
        );
        DrawHudLabel(
            FlightHudField.AtmosphereArea,
            T("Effective area") + ": " +
            FormatNumber(simulation.GetShipEffectiveDragArea())
        );

        if (simulation.IsShipHeatingCritical())
        {
            DrawHudLabel(
                FlightHudField.AtmosphereHeating,
                T("Warning: critical heating")
            );
        }
        else if (simulation.IsShipHeatingWarningActive())
        {
            DrawHudLabel(
                FlightHudField.AtmosphereHeating,
                T("Warning: high dynamic pressure")
            );
        }

        DrawDescentPreviewToggleSection();
        DrawDescentDriftDiagnostics();
    }

    void DrawDescentPreviewToggleSection()
    {
        string state = runner.IsAtmosphericPreviewEnabled() ?
            T("On") :
            T("Off");
        DrawHudLabel(
            FlightHudField.AtmospherePreview,
            T("Atmospheric preview") + ": " + state +
            "  (" + T("O: toggle atmospheric preview and path calculation") + ")"
        );
    }

    void DrawDescentDriftDiagnostics()
    {
        PreviewExecutionDriftInfo driftInfo = runner.GetLastDescentDriftInfo();

        if (!driftInfo.hasData)
        {
            return;
        }

        DrawSectionHeader("Descent drift", FlightHudField.DescentDrift);
        DrawHudLabel(
            FlightHudField.DescentDrift,
            T("Drift position") + ": " + FormatNumber(driftInfo.positionError)
        );
        DrawHudLabel(
            FlightHudField.DescentDrift,
            T("Drift velocity") + ": " + FormatNumber(driftInfo.velocityError)
        );
        DrawHudLabel(
            FlightHudField.DescentDrift,
            T("Drift path max") + ": " + FormatNumber(driftInfo.maxPathDeviation)
        );
        DrawHudLabel(FlightHudField.DescentDrift, T("Descent drift legend"));
    }

    void DrawEventsSection()
    {
        DrawSectionHeader(
            "Events",
            FlightHudField.EventsNext,
            FlightHudField.EventsLastStaging,
            FlightHudField.EventsLastFlight
        );
        DrawHudLabel(
            FlightHudField.EventsNext,
            T("Next") + ": " + GetNextEventLabel()
        );

        string stagingMessage = simulation.GetLastStagingMessage();

        if (!string.IsNullOrEmpty(stagingMessage))
        {
            DrawHudLabel(
                FlightHudField.EventsLastStaging,
                T("Last staging") + ": " + stagingMessage
            );
        }

        string flightEventMessage = simulation.GetLastFlightEventMessage();

        if (!string.IsNullOrEmpty(flightEventMessage))
        {
            DrawHudLabel(
                FlightHudField.EventsLastFlight,
                T("Last flight event") + ": " + flightEventMessage
            );
        }
    }

    void DrawManeuverHud(global::SimVector3 plannedDeltaV)
    {
        double plannedDeltaVLength = plannedDeltaV.Length();
        double availableDeltaV = simulation.GetShipAvailableDeltaV();
        double burnDuration = runner.GetPlannedBurnDuration();

        DrawSectionHeader(
            "Maneuver",
            FlightHudField.ManeuverPlannedDv,
            FlightHudField.ManeuverBurn,
            FlightHudField.ManeuverNodeTime,
            FlightHudField.ManeuverWarnings
        );
        DrawHudLabel(
            FlightHudField.ManeuverPlannedDv,
            T("Planned delta-v") + ": " +
            FormatNumber(plannedDeltaVLength) +
            "  " + T("Available") + ": " +
            FormatNumber(availableDeltaV)
        );

        if (plannedDeltaVLength <= 0.0)
        {
            DrawHudLabel(FlightHudField.ManeuverBurn, T("Node: none"));
            return;
        }

        DrawHudLabel(
            FlightHudField.ManeuverBurn,
            T("Burn duration") + ": " +
            FormatTime(burnDuration) +
            "  " + T("burn start T+") +
            FormatTime(runner.GetPlannedBurnStartTime())
        );

        if (runner.HasManeuverNode())
        {
            DrawHudLabel(
                FlightHudField.ManeuverNodeTime,
                T("Node time T+") +
                FormatTime(runner.GetManeuverNodeTime()) +
                "  " + T("node dv") + " " +
                FormatNumber(runner.GetManeuverNodeDeltaV().Length())
            );
        }
        else
        {
            DrawHudLabel(
                FlightHudField.ManeuverNodeTime,
                T("Node time T+0.0s  immediate plan")
            );
        }

        if (plannedDeltaVLength > availableDeltaV)
        {
            DrawHudLabel(
                FlightHudField.ManeuverWarnings,
                T("Warning: not enough fuel for maneuver")
            );
        }

        if (runner.ExpectsBurnDrift())
        {
            DrawHudLabel(
                FlightHudField.ManeuverWarnings,
                T("Warning: long burn, result can drift")
            );
        }
    }


    void DrawLanguageSelector()
    {
        if (!ShowHudField(FlightHudField.Language))
        {
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(T("Language") + ":", labelStyle, GUILayout.Width(52));

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor =
            Localization.currentLanguage == Localization.Language.Russian ?
            new Color(0.45f, 0.45f, 0.45f) :
            Color.white;

        if (GUILayout.Button("RU", GUILayout.Width(34)))
        {
            Localization.currentLanguage = Localization.Language.Russian;
            GameSettingsStore.SetLanguage(Localization.Language.Russian);
        }

        GUI.backgroundColor =
            Localization.currentLanguage == Localization.Language.English ?
            new Color(0.45f, 0.45f, 0.45f) :
            Color.white;

        if (GUILayout.Button("EN", GUILayout.Width(34)))
        {
            Localization.currentLanguage = Localization.Language.English;
            GameSettingsStore.SetLanguage(Localization.Language.English);
        }

        GUI.backgroundColor = previousColor;
        GUILayout.EndHorizontal();
    }

    void DrawControlsSection()
    {
        if (!ShowHudField(FlightHudField.Keys))
        {
            return;
        }
        GUILayout.Space(6);
        DrawSectionHeader("Keys");
        GUILayout.Label(
            T("H: details") + "  " +
            T("Tab: target") + "  " +
            T("C: circularize"),
            labelStyle
        );
        GUILayout.Label(
            T("I/K/J/L: RCS") + "  " +
            T("W/S/A/D/Q/E: rotate"),
            labelStyle
        );
        GUILayout.Label(
            T("1: manual") + "  " +
            T("2: prograde") + "  " +
            T("3: retrograde") + "  " +
            T("4: maneuver") + "  " +
            T("5: target port") + "  " +
            T("6: stability") + "  " +
            T("0: dev stability"),
            labelStyle
        );
        GUILayout.Label(
            T("V: warp to CA") + "  " +
            T("F: focus ship"),
            labelStyle
        );
        GUILayout.Label(T("RMB: warp to maneuver start"), labelStyle);
        GUILayout.Label(
            T("Enter near node: apply") + "  " +
            T("Backspace: clear"),
            labelStyle
        );
        GUILayout.Label(
            T("Arrows: maneuver") + "  " +
            T("B: auto-burn"),
            labelStyle
        );
        GUILayout.Label(
            T("Shift/Ctrl: throttle") + "  " +
            T("Z: max") + "  " +
            T("X: cut"),
            labelStyle
        );
        GUILayout.Label(T(", / . / ] / [: time x0.25..x100"), labelStyle);
        GUILayout.Label(T("P / Space: pause"), labelStyle);
        GUILayout.Label(T("O: toggle atmospheric preview"), labelStyle);
        GUILayout.Label(T("T: stage"), labelStyle);
        GUILayout.Label("M: orbital map mode", labelStyle);
    }

    string GetNextEventLabel()
    {
        NextOrbitalEventInfo nextEvent = runner.GetNextOrbitalEvent();

        if (!nextEvent.hasEvent)
        {
            return T("none");
        }

        return T(nextEvent.name) + T(" in ") + FormatTime(nextEvent.time);
    }

    void DrawDetails(
        global::SimVector3 plannedDeltaV,
        ClosestApproachInfo closestApproach
    )
    {
        DrawSectionHeader(
            "Details",
            FlightHudField.DetailsAltitude,
            FlightHudField.DetailsMoonDistance,
            FlightHudField.DetailsClosestApproach,
            FlightHudField.DetailsTimeToCa,
            FlightHudField.DetailsRelSpeedCa,
            FlightHudField.DetailsDeltaVStep,
            FlightHudField.DetailsPreviewOrbit,
            FlightHudField.DetailsBurnDrift,
            FlightHudField.DetailsVesselMarkers
        );
        DrawHudLabel(
            FlightHudField.DetailsAltitude,
            T("Altitude over planet") + ": " +
            FormatNumber(simulation.GetShipAltitudeAbovePlanet())
        );
        DrawHudLabel(
            FlightHudField.DetailsMoonDistance,
            T("Moon distance") + ": " +
            FormatNumber(simulation.GetShipDistanceToMoon())
        );
        DrawHudLabel(
            FlightHudField.DetailsClosestApproach,
            T("Closest approach to") + " " +
            closestApproach.targetBodyName +
            ": " +
            FormatNumber(closestApproach.distance)
        );
        DrawHudLabel(
            FlightHudField.DetailsTimeToCa,
            T("Time to CA") + ": " + FormatTime(closestApproach.time)
        );
        DrawHudLabel(
            FlightHudField.DetailsRelSpeedCa,
            T("Relative speed at CA") + ": " +
            FormatNumber(closestApproach.relativeSpeed)
        );
        DrawHudLabel(
            FlightHudField.DetailsDeltaVStep,
            T("Delta-v step") + ": " +
            FormatNumber(runner.plannedDeltaVStep)
        );

        if (plannedDeltaV.LengthSquared() > 0)
        {
            if (runner.HasManeuverNode())
            {
                DrawHudLabel(
                    FlightHudField.DetailsPreviewOrbit,
                    T("Preview orbit: from maneuver node")
                );
            }
            else
            {
                DrawHudLabel(
                    FlightHudField.DetailsPreviewOrbit,
                    T("Preview orbit: finite burn")
                );
                DrawHudLabel(
                    FlightHudField.DetailsPreviewOrbit,
                    T("Preview period") + ": " + FormatTime(runner.GetPlannedOrbitPeriod())
                );
            }
        }

        DrawBurnDriftDiagnostics();
        DrawViewSettings();
    }

    void DrawViewSettings()
    {
        if (runner == null || !ShowHudField(FlightHudField.DetailsVesselMarkers))
        {
            return;
        }

        DrawSectionHeader("View");
        bool showVesselTargetMarkers = runner.GetShowVesselTargetMarkers();
        string markerState = showVesselTargetMarkers ? T("On") : T("Off");

        GUILayout.Label(
            T("Vessel target markers") + ": " + markerState,
            labelStyle
        );

        if (GUILayout.Button(T("Toggle vessel target markers")))
        {
            runner.SetShowVesselTargetMarkers(!showVesselTargetMarkers);
        }
    }

    void DrawBurnDriftDiagnostics()
    {
        PreviewExecutionDriftInfo driftInfo = runner.GetLastBurnDriftInfo();

        if (!driftInfo.hasData || !ShowHudField(FlightHudField.DetailsBurnDrift))
        {
            return;
        }

        DrawSectionHeader("Drift");
        GUILayout.Label(
            T("Drift position") + ": " + FormatNumber(driftInfo.positionError),
            labelStyle
        );
        GUILayout.Label(
            T("Drift velocity") + ": " + FormatNumber(driftInfo.velocityError),
            labelStyle
        );
        GUILayout.Label(
            T("Drift path max") + ": " + FormatNumber(driftInfo.maxPathDeviation),
            labelStyle
        );
        GUILayout.Label(
            T("Drift throttle") + ": " + FormatPercent(driftInfo.throttle) +
            "  " + T("Drift thrust") + ": " +
            FormatNumber(driftInfo.actualThrustForce) +
            " / " + FormatNumber(driftInfo.previewThrustForce),
            labelStyle
        );
        GUILayout.Label(T("Drift legend"), labelStyle);
    }

    double GetDisplayedThrustToWeightRatio()
    {
        double appliedTwr = simulation.GetShipThrustToWeightRatio();

        if (appliedTwr > 0.001)
        {
            return appliedTwr;
        }

        double weight = simulation.GetShipWeightRelativeToActivePrimary();

        if (weight <= 0.0)
        {
            return 0.0;
        }

        return runner.GetCurrentThrustForce() / weight;
    }

    string FormatTwr(double value)
    {
        return value.ToString("0.00");
    }

    void DrawSectionHeader(string title)
    {
        GUILayout.Space(4);
        GUILayout.Label(T(title), labelStyle);
    }

    void DrawSectionHeader(string title, params FlightHudField[] fields)
    {
        if (!FlightHudSettings.AnyVisible(fields))
        {
            return;
        }

        DrawSectionHeader(title);
    }

    bool ShowHudField(FlightHudField field)
    {
        return FlightHudSettings.IsVisible(field);
    }

    void DrawHudLabel(FlightHudField field, string text)
    {
        if (!ShowHudField(field))
        {
            return;
        }

        GUILayout.Label(text, labelStyle);
    }

    string T(string key)
    {
        return Localization.Translate(key);
    }

    string TranslateOrientationMode(string modeName)
    {
        if (modeName == "Maneuver")
        {
            return T("Orientation Maneuver");
        }

        if (modeName == "TargetAlignment")
        {
            return T("TargetAlignment");
        }

        if (modeName == "StabilityAssist")
        {
            return T("Stability Assist");
        }

        if (modeName == "DevStabilityAssist")
        {
            return T("Dev Stability Assist");
        }

        return T(modeName);
    }

    string TranslateOrbitType(string orbitType)
    {
        return T(orbitType);
    }

    string FormatNumber(double value)
    {
        if (double.IsInfinity(value))
        {
            return T("infinity");
        }

        return value.ToString("0.00");
    }

    string FormatTime(double value)
    {
        if (double.IsInfinity(value))
        {
            return T("not useful");
        }

        return value.ToString("0.0") + "s";
    }

    string FormatPercent(double value)
    {
        return (value * 100.0).ToString("0") + "%";
    }

    string FormatCompactRelativeVelocity(
        global::SimVector3 relativeVelocity,
        double closureRate
    )
    {
        double speed = relativeVelocity.Length();
        double lateralSquared = speed * speed - closureRate * closureRate;
        double lateral = lateralSquared > 0.0 ?
            Math.Sqrt(lateralSquared) :
            0.0;

        return FormatClosureRate(closureRate) +
            "  |  " + T("Lateral") + " " + FormatNumber(lateral);
    }

    string FormatClosureRate(double closureRate)
    {
        if (Math.Abs(closureRate) < 0.005)
        {
            return T("Closure zero");
        }

        if (closureRate < 0.0)
        {
            return T("Closure closing") + " " + FormatNumber(Math.Abs(closureRate));
        }

        return T("Closure opening") + " " + FormatNumber(closureRate);
    }
}
