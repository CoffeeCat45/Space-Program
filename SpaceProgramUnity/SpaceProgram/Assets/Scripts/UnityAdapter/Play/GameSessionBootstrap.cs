using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures <see cref="SimulationRunner"/> exists on any startup scene (including VabScene)
/// and keeps editor-only geometry hidden while the main menu is shown.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameSessionBootstrap : MonoBehaviour
{
    static readonly string[] BootstrapSceneNames =
    {
        "VabScene",
        "PlaybackScene",
        "MenuScene",
        "MenuBootstrap",
    };

    static readonly string[] VabEnvironmentSceneHints =
    {
        "VAB",
        "Hangar",
        "SpaceCenter",
        "vablvl",
    };

    SimulationRunner runner;
    GameObject[] vabRoots = Array.Empty<GameObject>();
    bool vabRootsCollected;
    GameSessionMode lastSessionMode = GameSessionMode.MainMenu;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureSimulationSession()
    {
        if (EarthSandboxScene.IsActive() && EarthSandboxScene.EnteredFromMainMenu)
        {
            return;
        }

        if (EarthSandboxScene.IsActive())
        {
            EarthSandboxScene.TryRedirectStartupToMainMenu();
        }
        else if (ShouldRedirectColdStartFromVabToMainMenu())
        {
            FlightSessionSceneUtility.TryLoadMainMenuBootstrapScene();
        }

        CreateSimulationSessionHost();
    }

    static bool ShouldRedirectColdStartFromVabToMainMenu()
    {
        if (!VabSessionSceneUtility.IsActiveSceneVab())
        {
            return false;
        }

        return FlightSessionSceneUtility.CanLoadMainMenuBootstrapScene();
    }

    static void CreateSimulationSessionHost()
    {
        SimulationRunner existingRunner = FindRunnerInScene();

        GameObject host = existingRunner != null ?
            existingRunner.gameObject :
            new GameObject("SimulationSession");

        if (existingRunner == null)
        {
            existingRunner = host.AddComponent<SimulationRunner>();
            Debug.Log(
                "[GameSessionBootstrap] SimulationRunner was missing in the loaded scene. " +
                "Created SimulationSession object so the main menu can appear."
            );
        }

        if (host.GetComponent<GameSessionBootstrap>() == null)
        {
            host.AddComponent<GameSessionBootstrap>();
        }

        PersistSessionHost(host);
        DestroyOrphanGraphicsOptionsCanvases();
    }

    public static void PersistSessionHost(GameObject host)
    {
        if (host == null)
        {
            return;
        }

        host.name = "SimulationSession";

        if (host.transform.parent != null)
        {
            host.transform.SetParent(null, true);
        }

        DontDestroyOnLoad(host);
    }

    public static void PersistRunner(SimulationRunner runner)
    {
        if (runner == null)
        {
            return;
        }

        PersistSessionHost(runner.gameObject);
    }

    static void DestroyOrphanGraphicsOptionsCanvases()
    {
        VabOptionsUi[] allInstances = FindObjectsByType<VabOptionsUi>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < allInstances.Length; i++)
        {
            VabOptionsUi instance = allInstances[i];
            if (instance == null || instance.GetComponent<SimulationRunner>() != null)
            {
                continue;
            }

            if (instance.gameObject.name == "GraphicsOptions")
            {
                Destroy(instance.gameObject);
            }
        }

        GameObject[] roots = FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != "GraphicsOptionsCanvas")
            {
                continue;
            }

            if (root.transform.parent == null ||
                root.transform.parent.GetComponentInParent<SimulationRunner>() == null)
            {
                Destroy(root);
            }
        }
    }

    static SimulationRunner FindRunnerInScene()
    {
        SimulationRunner[] runners = FindObjectsByType<SimulationRunner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (runners.Length == 0)
        {
            return null;
        }

        return runners[0];
    }

    void Awake()
    {
        GameViewSettings.Load();
        DevSettings.Load();
        PlanetLodSettings.Load();
        FlightHudSettings.Load();
        runner = GetComponent<SimulationRunner>();

        if (runner == null)
        {
            runner = FindRunnerInScene();
        }

        if (GetComponent<VabOptionsUi>() == null)
        {
            gameObject.AddComponent<VabOptionsUi>();
        }

        if (GetComponent<FlightHudSettingsUi>() == null)
        {
            gameObject.AddComponent<FlightHudSettingsUi>();
        }
    }

    void Start()
    {
        if (!ShouldSkipVabRootCollection())
        {
            CollectVabRootsIfNeeded();
        }

        StartCoroutine(ApplyPresentationAfterStartup());
    }

    bool ShouldSkipVabRootCollection()
    {
        if (EarthSandboxScene.IsActive() && !EarthSandboxScene.EnteredFromMainMenu)
        {
            return true;
        }

        if (runner == null)
        {
            runner = FindRunnerInScene();
        }

        if (
            runner != null &&
            runner.SessionMode == GameSessionMode.MainMenu &&
            !VabSessionSceneUtility.IsActiveSceneVab()
        )
        {
            return true;
        }

        return false;
    }

    IEnumerator ApplyPresentationAfterStartup()
    {
        yield return null;
        ApplyPresentation(force: true);
    }

    void Update()
    {
        if (runner == null)
        {
            runner = FindRunnerInScene();
        }

        GameSessionMode sessionMode = runner == null ?
            GameSessionMode.MainMenu :
            runner.SessionMode;

        if (sessionMode != lastSessionMode)
        {
            if (sessionMode == GameSessionMode.VabEditor)
            {
                vabRootsCollected = false;
            }

            ApplyPresentation(force: true);
        }
        else if (sessionMode == GameSessionMode.MainMenu)
        {
            SetVabRootsActive(false);
        }
    }

    public static void SyncPresentationForRunner(SimulationRunner sessionRunner)
    {
        if (sessionRunner == null)
        {
            return;
        }

        GameSessionBootstrap bootstrap = sessionRunner.GetComponent<GameSessionBootstrap>();

        if (bootstrap == null)
        {
            return;
        }

        bootstrap.runner = sessionRunner;
        bootstrap.vabRootsCollected = false;
        bootstrap.ApplyPresentation(force: true);
        bootstrap.ScheduleDeferredPresentationSync();
    }

    void ScheduleDeferredPresentationSync()
    {
        StopCoroutine(nameof(DeferredPresentationSync));
        StartCoroutine(DeferredPresentationSync());
    }

    IEnumerator DeferredPresentationSync()
    {
        yield return null;

        if (
            runner != null &&
            runner.SessionMode == GameSessionMode.VabEditor
        )
        {
            VabSessionSceneUtility.TryLoadVabScene(logErrorIfMissing: false);
        }

        vabRootsCollected = false;
        ApplyPresentation(force: true);
    }

    void ApplyPresentation(bool force)
    {
        GameSessionMode sessionMode = runner == null ?
            GameSessionMode.MainMenu :
            runner.SessionMode;
        lastSessionMode = sessionMode;

        if (force && !ShouldSkipVabRootCollection())
        {
            CollectVabRootsIfNeeded();
        }

        bool showVab = sessionMode == GameSessionMode.VabEditor;

        if (sessionMode == GameSessionMode.Flight)
        {
            if (runner != null)
            {
                runner.RestoreFlightPresentation();

                if (FlightSessionSceneUtility.CanLoadBootstrapScene())
                {
                    FlightSceneIsolation.PurgeEditorArtifacts(runner);
                    DestroyVabRoots();
                }
                else
                {
                    FlightSceneIsolation.PurgeEditorSessionArtifacts(runner);
                    SetVabRootsActive(false);
                }

                FlightSceneIsolation.PurgeStrayVesselBlocks(runner);
            }
        }
        else
        {
            if (runner != null)
            {
                runner.TeardownFlightPresentation();
            }

            if (sessionMode == GameSessionMode.MainMenu)
            {
                ApplyMenuCamera();
            }

            SetVabRootsActive(showVab);
        }

        if (showVab)
        {
            EnsureVabEditorController();
            ApplyVabEditorCamera();
            VabDecorativeSphereUtility.HideAllImportedDecorativeSpheres();
        }

        VabEditorController.ApplySessionVisibility(runner);
    }

    static void EnsureVabEditorController()
    {
        VabEditorController[] existingControllers = FindObjectsByType<VabEditorController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (existingControllers.Length > 0)
        {
            return;
        }

        GameObject host = GameObject.Find("SceneLogic");

        if (host == null)
        {
            host = new GameObject("SceneLogic");
        }

        host.AddComponent<VabEditorController>();
        Debug.Log(
            "[GameSessionBootstrap] VabEditorController was missing. " +
            "Auto-created on SceneLogic."
        );
    }

    public static void ApplyFlightPresentationInPlace(SimulationRunner sessionRunner)
    {
        if (sessionRunner == null)
        {
            return;
        }

        GameSessionBootstrap bootstrap = sessionRunner.GetComponent<GameSessionBootstrap>();

        if (bootstrap == null)
        {
            return;
        }

        bootstrap.runner = sessionRunner;
        bootstrap.vabRootsCollected = false;
        bootstrap.CollectVabRootsIfNeeded();
        FlightSceneIsolation.PurgeEditorSessionArtifacts(sessionRunner);
        bootstrap.SetVabRootsActive(false);
        sessionRunner.RestoreFlightPresentation();
        FlightSceneIsolation.PurgeStrayVesselBlocks(sessionRunner);
        FlightCameraBootstrap.EnsureMainCamera(sessionRunner.transform);
        bootstrap.lastSessionMode = GameSessionMode.Flight;
        VabEditorController.ApplySessionVisibility(sessionRunner);
    }

    void CollectVabRootsIfNeeded()
    {
        if (vabRootsCollected)
        {
            return;
        }

        vabRootsCollected = true;
        vabRoots = CollectVabRoots();
    }

    static GameObject[] CollectVabRoots()
    {
        List<GameObject> roots = new List<GameObject>();
        HashSet<int> instanceIds = new HashSet<int>();

        VabSceneEnvironment[] markers = FindObjectsByType<VabSceneEnvironment>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null)
            {
                continue;
            }

            GameObject markerRoot = markers[i].gameObject;

            if (instanceIds.Add(markerRoot.GetInstanceID()))
            {
                roots.Add(markerRoot);
            }
        }

        if (roots.Count > 0)
        {
            return roots.ToArray();
        }

        // VAB is often a separate loaded scene (Hierarchy header with the Unity icon).
        // Scene headers cannot receive components — hide every root object inside matching scenes.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.isLoaded || !MatchesVabEnvironmentScene(scene.name))
            {
                continue;
            }

            AddSceneRootObjects(scene, roots, instanceIds);
        }

        if (roots.Count > 0)
        {
            return roots.ToArray();
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] activeSceneRoots = activeScene.GetRootGameObjects();

        for (int i = 0; i < activeSceneRoots.Length; i++)
        {
            GameObject sceneRoot = activeSceneRoots[i];

            if (
                sceneRoot == null ||
                IsSessionHost(sceneRoot) ||
                VabDecorativeSphereUtility.ShouldExcludeFromVabRootToggle(sceneRoot) ||
                !MatchesVabRootName(sceneRoot.name)
            )
            {
                continue;
            }

            if (instanceIds.Add(sceneRoot.GetInstanceID()))
            {
                roots.Add(sceneRoot);
            }
        }

        if (roots.Count == 0 && ShouldWarnMissingVabHangar())
        {
            Debug.LogWarning(
                "[GameSessionBootstrap] VAB hangar was not found. " +
                "Either load the hangar as objects in the main scene, " +
                "or add VabSceneEnvironment to a hangar GameObject."
            );
        }

        return roots.ToArray();
    }

    static bool ShouldWarnMissingVabHangar()
    {
        if (EarthSandboxScene.IsActive())
        {
            return false;
        }

        SimulationRunner sessionRunner = FindRunnerInScene();

        if (
            sessionRunner != null &&
            sessionRunner.SessionMode == GameSessionMode.MainMenu &&
            !VabSessionSceneUtility.IsActiveSceneVab()
        )
        {
            return false;
        }

        if (!VabSessionSceneUtility.CanLoadAnyVabScene())
        {
            return false;
        }

        return sessionRunner == null ||
            sessionRunner.SessionMode == GameSessionMode.VabEditor ||
            VabSessionSceneUtility.IsActiveSceneVab();
    }

    static void AddSceneRootObjects(
        Scene scene,
        List<GameObject> roots,
        HashSet<int> instanceIds
    )
    {
        if (!scene.isLoaded)
        {
            return;
        }

        GameObject[] sceneRoots = scene.GetRootGameObjects();

        for (int i = 0; i < sceneRoots.Length; i++)
        {
            GameObject sceneRoot = sceneRoots[i];

            if (
                sceneRoot == null ||
                IsSessionHost(sceneRoot) ||
                VabDecorativeSphereUtility.ShouldExcludeFromVabRootToggle(sceneRoot)
            )
            {
                continue;
            }

            if (instanceIds.Add(sceneRoot.GetInstanceID()))
            {
                roots.Add(sceneRoot);
            }
        }
    }

    static bool IsSessionHost(GameObject candidate)
    {
        return candidate.GetComponent<SimulationRunner>() != null ||
            candidate.GetComponent<GameSessionBootstrap>() != null;
    }

    static bool MatchesVabEnvironmentScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || IsBootstrapScene(sceneName))
        {
            return false;
        }

        return VabSessionSceneUtility.MatchesVabEnvironmentSceneName(sceneName);
    }

    static bool IsBootstrapScene(string sceneName)
    {
        for (int i = 0; i < BootstrapSceneNames.Length; i++)
        {
            if (string.Equals(sceneName, BootstrapSceneNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static bool MatchesVabRootName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        for (int i = 0; i < VabEnvironmentSceneHints.Length; i++)
        {
            if (objectName.IndexOf(VabEnvironmentSceneHints[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return objectName.IndexOf("VabEnvironment", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("VabRoot", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void SetVabRootsActive(bool active)
    {
        for (int i = 0; i < vabRoots.Length; i++)
        {
            GameObject root = vabRoots[i];

            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        if (active)
        {
            VabDecorativeSphereUtility.HideAllImportedDecorativeSpheres();
        }
    }

    void DestroyVabRoots()
    {
        for (int i = 0; i < vabRoots.Length; i++)
        {
            GameObject root = vabRoots[i];

            if (root != null)
            {
                Destroy(root);
            }
        }

        vabRoots = Array.Empty<GameObject>();
        vabRootsCollected = false;
    }

    static void ApplyMenuCamera()
    {
        SimulationRunner sessionRunner = FindRunnerInScene();

        if (sessionRunner != null)
        {
            FlightCameraBootstrap.EnsureMenuCamera(sessionRunner.transform);
            return;
        }

        Camera camera = FindBootstrapCamera();

        if (camera == null)
        {
            FlightCameraBootstrap.EnsureMenuCamera(null);
            return;
        }

        if (!camera.gameObject.activeSelf)
        {
            camera.gameObject.SetActive(true);
        }

        camera.enabled = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.03f, 0.08f, 1.0f);
    }

    static void ApplyVabEditorCamera()
    {
        SimulationRunner sessionRunner = UnityEngine.Object.FindFirstObjectByType<SimulationRunner>();

        if (sessionRunner != null)
        {
            FlightCameraBootstrap.RestoreVabEnvironmentCamera(sessionRunner.transform);
            return;
        }

        Camera camera = FindBootstrapCamera();

        if (camera == null)
        {
            return;
        }

        if (!camera.gameObject.activeSelf)
        {
            camera.gameObject.SetActive(true);
        }

        camera.enabled = true;

        if (camera.clearFlags == CameraClearFlags.SolidColor)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
        }
    }

    static Camera FindBootstrapCamera()
    {
        Camera camera = Camera.main;

        if (camera != null && !MatchesVabEnvironmentScene(camera.gameObject.scene.name))
        {
            return camera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];

            if (candidate == null || !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (MatchesVabEnvironmentScene(candidate.gameObject.scene.name))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }
}
