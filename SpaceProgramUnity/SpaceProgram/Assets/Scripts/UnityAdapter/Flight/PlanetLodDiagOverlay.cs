using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Always-off until F8: why each 2×2 around the ship is not splitting/meshing.
/// F8 shows the overlay and logs to the Console (paste into chat). Shift+F8 hides it.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(32100)]
public sealed class PlanetLodDiagOverlay : MonoBehaviour
{
    const int FontSize = 13;
    const float LineHeight = 16f;
    const float Pad = 8f;

    FlightPlanetSphere planetSphere;
    GUIStyle textStyle;
    GUIStyle boxStyle;
    Texture2D boxTexture;
    bool visible;
    bool loggedStartup;
    string cachedReport = "";
    float nextRefreshTime;

    public void Bind(FlightPlanetSphere sphere)
    {
        planetSphere = sphere;
    }

    void Update()
    {
        if (!loggedStartup && Time.unscaledTime >= 2f)
        {
            loggedStartup = true;
            RefreshReport(force: true);
            Debug.Log("[LOD DIAG startup]\n" + cachedReport);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || keyboard.f8Key == null || !keyboard.f8Key.wasPressedThisFrame)
        {
            return;
        }

        if (keyboard.leftShiftKey != null && keyboard.leftShiftKey.isPressed)
        {
            visible = false;
            return;
        }

        visible = true;
        RefreshReport(force: true);
        Debug.Log("[LOD DIAG]\n" + cachedReport);
    }

    void OnGUI()
    {
        if (!visible || planetSphere == null || planetSphere.IsMapModeActive)
        {
            return;
        }

        if (Event.current.type == EventType.Repaint && Time.unscaledTime >= nextRefreshTime)
        {
            RefreshReport(force: false);
            nextRefreshTime = Time.unscaledTime + 0.25f;
        }

        if (string.IsNullOrEmpty(cachedReport))
        {
            return;
        }

        EnsureStyles();
        int lines = CountLines(cachedReport);
        float width = Mathf.Min(920f, Screen.width - 16f);
        float height = Pad * 2f + lines * LineHeight + 4f;
        Rect rect = new Rect(8f, 8f, width, height);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(
            new Rect(rect.x + Pad, rect.y + 4f, rect.width - Pad * 2f, rect.height - 8f),
            cachedReport,
            textStyle
        );
    }

    void RefreshReport(bool force)
    {
        if (planetSphere == null)
        {
            cachedReport = "LOD DIAG: no planet";
            return;
        }

        cachedReport = planetSphere.CaptureLodDiagnosticsReport();
        if (force)
        {
            nextRefreshTime = Time.unscaledTime + 0.25f;
        }
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
            richText = false,
        };
        textStyle.normal.textColor = new Color(0.85f, 1f, 0.55f);
    }

    void OnDestroy()
    {
        if (boxTexture != null)
        {
            Destroy(boxTexture);
        }
    }

    static int CountLines(string text)
    {
        int count = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }
}
