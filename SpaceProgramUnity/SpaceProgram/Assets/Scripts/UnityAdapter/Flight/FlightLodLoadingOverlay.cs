using UnityEngine;

/// <summary>
/// Optional black cover after a flight session starts, until pad LOD is ready to show.
/// Off in settings = current behaviour (world visible while chunks stream in).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(32760)]
public sealed class FlightLodLoadingOverlay : MonoBehaviour
{
    const string CoverCameraName = "LodLoadingCoverCamera";
    const float CoverCameraDepth = 100f;

    static FlightLodLoadingOverlay activeInstance;
    static Texture2D coverTexture;
    static GUIStyle titleStyle;
    static GUIStyle hintStyle;

    Camera coverCamera;
    SimulationRunner runner;
    bool blocking;
    bool heldPause;
    float startedUnscaledTime = -1f;
    float readyStreakSeconds;
    int statusViewerLevel = -1;
    int statusMaxLod;
    string statusPhase = "";

    public static bool IsBlocking =>
        activeInstance != null && activeInstance.blocking;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        activeInstance = null;
        coverTexture = null;
        titleStyle = null;
        hintStyle = null;
    }

    public static void NotifyFlightSessionStarted(SimulationRunner sessionRunner)
    {
        if (sessionRunner == null)
        {
            return;
        }

        FlightLodLoadingOverlay overlay = sessionRunner.GetComponent<FlightLodLoadingOverlay>();
        if (overlay == null)
        {
            overlay = sessionRunner.gameObject.AddComponent<FlightLodLoadingOverlay>();
        }

        overlay.runner = sessionRunner;
        overlay.BeginForSession();
    }

    public static void HideActive()
    {
        if (activeInstance != null)
        {
            activeInstance.Hide(restorePause: false);
        }
    }

    void OnEnable()
    {
        activeInstance = this;
    }

    void OnDisable()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    void LateUpdate()
    {
        if (!blocking)
        {
            return;
        }

        bool hasPlanetSphere = FlightPlanetSphere.TryGetLodLoadingReadiness(
            out bool hasQuadtree,
            out bool hasViewerMesh,
            out int viewerLeafLevel,
            out int activeMaxLodLevel,
            out bool padStillCoarse,
            out bool bootstrapping,
            out bool warmupActive
        );

        statusViewerLevel = viewerLeafLevel;
        statusMaxLod = activeMaxLodLevel;
        if (warmupActive)
        {
            statusPhase = "WARM";
        }
        else if (bootstrapping)
        {
            statusPhase = "BOOT";
        }
        else if (padStillCoarse || !hasViewerMesh)
        {
            statusPhase = "PAD";
        }
        else
        {
            statusPhase = "OK";
        }

        bool shellReady = FlightLodLoadingPolicy.IsPadShellReady(
            hasViewerMesh,
            viewerLeafLevel,
            activeMaxLodLevel,
            padStillCoarse,
            bootstrapping,
            warmupActive
        );
        if (shellReady)
        {
            readyStreakSeconds += Time.unscaledDeltaTime;
        }
        else
        {
            readyStreakSeconds = 0f;
        }

        if (FlightLodLoadingPolicy.ShouldReveal(
            hasPlanetSphere,
            hasQuadtree,
            hasViewerMesh,
            viewerLeafLevel,
            activeMaxLodLevel,
            padStillCoarse,
            bootstrapping,
            warmupActive,
            ElapsedSeconds,
            readyStreakSeconds
        ))
        {
            Hide(restorePause: true);
        }
    }

    void OnGUI()
    {
        if (!blocking)
        {
            return;
        }

        GUI.depth = -4000;
        EnsureStyles();
        if (Event.current != null && Event.current.isMouse)
        {
            Event.current.Use();
        }

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), coverTexture, ScaleMode.StretchToFill);

        float titleWidth = Mathf.Min(720f, Screen.width - 48f);
        Rect titleRect = new Rect(
            (Screen.width - titleWidth) * 0.5f,
            Screen.height * 0.42f,
            titleWidth,
            48f
        );
        GUI.Label(titleRect, Localization.Translate("Loading terrain"), titleStyle);

        Rect statusRect = new Rect(
            titleRect.x,
            titleRect.yMax + 4f,
            titleWidth,
            28f
        );
        GUI.Label(
            statusRect,
            "L " + statusViewerLevel + " / " + statusMaxLod + "  " + statusPhase,
            hintStyle
        );

        Rect hintRect = new Rect(
            titleRect.x,
            statusRect.yMax + 4f,
            titleWidth,
            40f
        );
        GUI.Label(hintRect, Localization.Translate("Loading terrain hint"), hintStyle);
    }

    void BeginForSession()
    {
        GameViewSettings.Load();
        if (!GameViewSettings.ShowLodLoadingScreen)
        {
            Hide(restorePause: false);
            return;
        }

        blocking = true;
        startedUnscaledTime = Time.unscaledTime;
        readyStreakSeconds = 0f;
        EnsureCoverCamera();
        SetCoverVisible(true);

        if (runner != null && !runner.IsPaused)
        {
            runner.IsPaused = true;
            heldPause = true;
        }
        else
        {
            heldPause = false;
        }
    }

    void Hide(bool restorePause)
    {
        blocking = false;
        startedUnscaledTime = -1f;
        readyStreakSeconds = 0f;
        if (coverCamera != null)
        {
            coverCamera.enabled = false;
        }

        if (restorePause && heldPause && runner != null)
        {
            runner.IsPaused = false;
        }

        heldPause = false;
    }

    float ElapsedSeconds =>
        startedUnscaledTime < 0f ? 0f : Time.unscaledTime - startedUnscaledTime;

    void EnsureCoverCamera()
    {
        if (coverCamera != null)
        {
            return;
        }

        Transform existing = transform.Find(CoverCameraName);
        GameObject cameraObject = existing != null ?
            existing.gameObject :
            new GameObject(CoverCameraName);
        cameraObject.transform.SetParent(transform, false);

        coverCamera = cameraObject.GetComponent<Camera>();
        if (coverCamera == null)
        {
            coverCamera = cameraObject.AddComponent<Camera>();
        }

        coverCamera.clearFlags = CameraClearFlags.SolidColor;
        coverCamera.backgroundColor = Color.black;
        coverCamera.cullingMask = 0;
        coverCamera.depth = CoverCameraDepth;
        coverCamera.allowHDR = false;
        coverCamera.allowMSAA = false;
        coverCamera.orthographic = true;
        coverCamera.orthographicSize = 1f;
        coverCamera.nearClipPlane = 0.1f;
        coverCamera.farClipPlane = 10f;
        coverCamera.enabled = false;
        cameraObject.tag = "Untagged";
        AudioListener listener = cameraObject.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = false;
        }
    }

    void SetCoverVisible(bool visible)
    {
        EnsureCoverCamera();
        if (coverCamera != null)
        {
            coverCamera.enabled = visible;
        }
    }

    static void EnsureStyles()
    {
        if (coverTexture == null)
        {
            coverTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            coverTexture.SetPixel(0, 0, Color.black);
            coverTexture.Apply(false, true);
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            titleStyle.normal.textColor = Color.white;
        }

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 16,
                wordWrap = true,
            };
            hintStyle.normal.textColor = new Color(0.78f, 0.82f, 0.88f, 1f);
        }
    }
}
