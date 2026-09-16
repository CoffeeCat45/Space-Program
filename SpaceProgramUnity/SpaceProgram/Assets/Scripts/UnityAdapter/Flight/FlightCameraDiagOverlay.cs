using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// F7 shows camera-follow classification and logs it to the Console.
/// Shift+F7 hides. Compact line also sits under the FPS overlay.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(32150)]
public sealed class FlightCameraDiagOverlay : MonoBehaviour
{
    const int FontSize = 13;
    const float LineHeight = 16f;
    const float Pad = 8f;

    SimulationRunner runner;
    GUIStyle textStyle;
    GUIStyle boxStyle;
    Texture2D boxTexture;
    bool visible;
    string cachedReport = "";
    float nextRefreshTime;

    public void SetRunner(SimulationRunner newRunner)
    {
        runner = newRunner;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || keyboard.f7Key == null || !keyboard.f7Key.wasPressedThisFrame)
        {
            return;
        }

        if (keyboard.leftShiftKey != null && keyboard.leftShiftKey.isPressed)
        {
            visible = false;
            return;
        }

        visible = true;
        RefreshReport();
        Debug.Log("[CAM DIAG]\n" + cachedReport);
    }

    void OnGUI()
    {
        if (
            !visible ||
            !FlightDebugSettings.IsVisible(FlightDebugOverlay.Camera) ||
            runner == null ||
            !runner.IsInitialized ||
            runner.IsMainMenuVisible ||
            runner.IsVabEditorActive
        )
        {
            return;
        }

        if (Event.current.type == EventType.Repaint && Time.unscaledTime >= nextRefreshTime)
        {
            RefreshReport();
            nextRefreshTime = Time.unscaledTime + 0.25f;
        }

        if (string.IsNullOrEmpty(cachedReport))
        {
            return;
        }

        EnsureStyles();
        int lines = CountLines(cachedReport);
        float width = Mathf.Min(720f, Screen.width - 16f);
        float height = Pad * 2f + lines * LineHeight + 4f;
        float y = Screen.height - height - 56f;
        Rect rect = new Rect(8f, Mathf.Max(8f, y), width, height);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(
            new Rect(rect.x + Pad, rect.y + 4f, rect.width - Pad * 2f, rect.height - 8f),
            cachedReport,
            textStyle
        );
    }

    void RefreshReport()
    {
        cachedReport = runner != null ?
            runner.CaptureCameraDiag().FullReport :
            "CAM DIAG: no runner";
    }

    void EnsureStyles()
    {
        if (textStyle != null)
        {
            return;
        }

        boxTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        boxTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.78f));
        boxTexture.Apply();
        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = boxTexture;
        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = FontSize,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.UpperLeft,
            wordWrap = false,
            richText = false
        };
        textStyle.normal.textColor = new Color(0.85f, 0.95f, 1f);
    }

    static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        int lines = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }
}
