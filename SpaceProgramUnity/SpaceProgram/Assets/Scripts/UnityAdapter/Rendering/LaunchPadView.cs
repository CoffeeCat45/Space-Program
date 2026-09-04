using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(110)]
public class LaunchPadView : MonoBehaviour
{
    const string DefaultLaunchSitePrefabAssetPath =
        "Assets/GameObject/Cove_Launch_Site.prefab";
    const string SpawnPointName = LaunchPadMarkerLayout.SpawnPointName;
    const string NodeTopName = "Node_Top";
    const string NodeBottomName = LaunchPadMarkerLayout.NodeBottomName;

    const float PlatformWidth = 3.2f;
    const float PlatformHeight = 0.35f;
    const float PlatformDepth = 3.2f;
    const float TowerRadius = 0.22f;
    const float TowerHeight = 5.5f;
    const float TowerOffset = 1.6f;
    const float PadHideAltitudeMeters = 25000.0f;
    const double PadHideSurfaceDistanceMeters =
        LaunchPadConstants.LaunchSitePadVisibilityRadiusMeters;

    Simulation simulation;
    SimulationRunner runner;
    GameObject padRoot;
    GameObject launchSitePrefab;

    internal void SetSimulation(Simulation newSimulation)
    {
        simulation = newSimulation;
    }

    internal void SetRunner(SimulationRunner newRunner)
    {
        runner = newRunner;
    }

    internal void SetLaunchSitePrefab(GameObject prefab)
    {
        launchSitePrefab = prefab;
    }

    internal void RefreshPadTransformForRender()
    {
        if (
            simulation == null ||
            runner == null ||
            runner.IsMapModeActive ||
            !IsPadScenario() ||
            simulation.planet == null ||
            !ShouldShowPadNearLaunchSite()
        )
        {
            HidePad();
            return;
        }

        EnsurePadBuilt();
        ApplyFixedPadTransform();
        padRoot.SetActive(true);
        AlignLandedVesselFeetToSpawnPoint();
    }

    void LateUpdate()
    {
        RefreshPadTransformForRender();
    }

    /// <summary>
    /// World position for the vessel's lowest <c>Node_Bottom</c> (pad <see cref="SpawnPointName"/>).
    /// </summary>
    internal bool TryGetPadVesselFeetWorldPosition(out Vector3 feetWorldPosition)
    {
        feetWorldPosition = Vector3.zero;

        if (!IsPadScenario() || simulation == null)
        {
            return false;
        }

        EnsurePadBuilt();

        if (padRoot == null)
        {
            return false;
        }

        Transform spawnPoint = LaunchPadMarkerLayout.FindSpawnPoint(padRoot.transform);

        if (spawnPoint != null)
        {
            feetWorldPosition = spawnPoint.position;
            return true;
        }

        Transform deckTop = FindChildTransform(padRoot.transform, NodeTopName);

        if (deckTop != null)
        {
            feetWorldPosition = deckTop.position;
            return true;
        }

        if (TryGetMeshDeckWorldPosition(out feetWorldPosition))
        {
            return true;
        }

        feetWorldPosition = padRoot.transform.position;
        return true;
    }

    /// <summary>
    /// Lowest vessel <c>Node_Bottom</c> (else lowest renderer) onto pad <see cref="SpawnPointName"/>.
    /// Layout resets the Blocks offset every Update, so this must run after pad world transform.
    /// </summary>
    void AlignLandedVesselFeetToSpawnPoint()
    {
        if (
            runner == null ||
            simulation == null ||
            simulation.ship == null ||
            simulation.ship.flightStatus != FlightStatus.Landed
        )
        {
            return;
        }

        Transform shipTransform = runner.GetShipVisualTransform();

        if (shipTransform == null)
        {
            return;
        }

        if (!TryGetPadVesselFeetWorldPosition(out Vector3 spawnWorldPosition))
        {
            return;
        }

        BodyView.TryAlignVesselVisualToWorldSpawnPoint(
            shipTransform.gameObject,
            spawnWorldPosition
        );
    }

    bool TryGetMeshDeckWorldPosition(out Vector3 deckWorldPosition)
    {
        deckWorldPosition = Vector3.zero;

        if (padRoot == null || !TryGetMeshTopLocalY(out float topLocalY))
        {
            return false;
        }

        deckWorldPosition = padRoot.transform.TransformPoint(new Vector3(0f, topLocalY, 0f));
        return true;
    }

    bool TryGetMeshTopLocalY(out float topLocalY)
    {
        topLocalY = 0f;

        if (padRoot == null)
        {
            return false;
        }

        Renderer[] renderers = padRoot.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return false;
        }

        bool found = false;
        float maxLocalY = float.NegativeInfinity;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            Bounds worldBounds = renderers[i].bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            for (int cx = -1; cx <= 1; cx += 2)
            {
                for (int cy = -1; cy <= 1; cy += 2)
                {
                    for (int cz = -1; cz <= 1; cz += 2)
                    {
                        Vector3 worldCorner = center + new Vector3(
                            extents.x * cx,
                            extents.y * cy,
                            extents.z * cz
                        );
                        float localY = padRoot.transform.InverseTransformPoint(worldCorner).y;

                        if (localY > maxLocalY)
                        {
                            maxLocalY = localY;
                            found = true;
                        }
                    }
                }
            }
        }

        topLocalY = maxLocalY;
        return found;
    }

    bool ShouldShowPadNearLaunchSite()
    {
        if (simulation.GetShipSurfaceDistanceFromLaunchSiteMeters() > PadHideSurfaceDistanceMeters)
        {
            return false;
        }

        if (simulation.GetFlightStatus() == FlightStatus.Landed)
        {
            return true;
        }

        if (simulation.GetFlightStatus() != FlightStatus.Flying)
        {
            return false;
        }

        return simulation.GetShipAltitudeAbovePlanet() < PadHideAltitudeMeters;
    }

    static bool IsPadScenario(SimulationScenario scenario)
    {
        return SimulationScenarios.IsAnySurfacePad(scenario);
    }

    bool IsPadScenario()
    {
        return IsPadScenario(simulation.scenario);
    }

    void HidePad()
    {
        if (padRoot != null)
        {
            padRoot.SetActive(false);
        }
    }

    internal void Teardown()
    {
        simulation = null;

        if (padRoot != null)
        {
            Destroy(padRoot);
            padRoot = null;
        }
    }

    void EnsurePadBuilt()
    {
        if (padRoot != null)
        {
            return;
        }

        padRoot = new GameObject("Cove_Launch_Site");
        GameObject prefab = ResolveLaunchSitePrefab();

        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, padRoot.transform);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            LaunchPadMarkerLayout.AlignPrefabInstanceToPadRoot(instance);
            DisableColliders(instance);
            FlightRenderLayers.ApplyLocalPhysicsLayer(padRoot);
            return;
        }

        BuildFallbackPrimitives();
    }

    void ApplyFixedPadTransform()
    {
        if (padRoot == null)
        {
            return;
        }

        bool attachToPlanet =
            IsPadScenario() &&
            simulation != null &&
            simulation.GetShipSurfaceDistanceFromLaunchSiteMeters() <=
                PadHideSurfaceDistanceMeters;

        Transform padParent = null;

        if (runner != null)
        {
            if (
                attachToPlanet &&
                runner.TryGetPlanetVisualTransform(out Transform planetTransform)
            )
            {
                padParent = planetTransform;
            }
            else
            {
                padParent = runner.GetFlightUniverseRoot();
            }
        }

        if (padParent == null)
        {
            return;
        }

        if (padRoot.transform.parent != padParent)
        {
            padRoot.transform.SetParent(padParent, false);
        }

        Vector3 outward = WorldVisualBridge.GetLaunchSiteNormalMeshFrameUnity();

        if (attachToPlanet)
        {
            padRoot.transform.localPosition =
                WorldVisualBridge.GetLaunchPadFixedLocalPositionOnPlanet();
            padRoot.transform.localRotation = Quaternion.FromToRotation(Vector3.up, outward);
        }
        else
        {
            padRoot.transform.localPosition =
                WorldVisualBridge.GetLaunchPadFixedLocalPositionInFlightUniverse();
            padRoot.transform.localRotation = Quaternion.identity;
        }

        padRoot.transform.localScale = Vector3.one;
    }

    GameObject ResolveLaunchSitePrefab()
    {
        if (launchSitePrefab != null)
        {
            return launchSitePrefab;
        }

#if UNITY_EDITOR
        GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLaunchSitePrefabAssetPath);

        if (editorPrefab != null)
        {
            return editorPrefab;
        }
#endif

        RuntimePartCatalog catalog = RuntimePartCatalog.Load();

        if (catalog != null && catalog.coveLaunchSite != null)
        {
            return catalog.coveLaunchSite;
        }

        return Resources.Load<GameObject>("Environments/Cove_Launch_Site");
    }

    void BuildFallbackPrimitives()
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "PadPlatform";
        platform.transform.SetParent(padRoot.transform, false);
        platform.transform.localPosition = new Vector3(0, PlatformHeight * 0.5f, 0);
        platform.transform.localScale = new Vector3(
            PlatformWidth,
            PlatformHeight,
            PlatformDepth
        );
        SetPadColor(platform, new Color(0.45f, 0.47f, 0.5f));

        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tower.name = "PadTower";
        tower.transform.SetParent(padRoot.transform, false);
        tower.transform.localPosition = new Vector3(0, TowerHeight * 0.5f, -TowerOffset);
        tower.transform.localScale = new Vector3(
            TowerRadius * 2.0f,
            TowerHeight * 0.5f,
            TowerRadius * 2.0f
        );
        SetPadColor(tower, new Color(0.62f, 0.64f, 0.68f));

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "PadMarker";
        marker.transform.SetParent(padRoot.transform, false);
        marker.transform.localPosition = new Vector3(0, 0.55f, 0);
        marker.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
        SetPadColor(marker, new Color(1.0f, 0.55f, 0.12f));

        DisableColliders(padRoot);
        FlightRenderLayers.ApplyLocalPhysicsLayer(padRoot);
    }

    /// <summary>
    /// Pins pad prefab so <see cref="LaunchPadMarkerLayout.SpawnPointName"/> sits on padRoot XZ origin.
    /// </summary>
    static void AlignLaunchSitePrefabToTerrainContact(GameObject instance)
    {
        LaunchPadMarkerLayout.AlignPrefabInstanceToPadRoot(instance);
    }

    static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate != null && candidate.name == childName)
            {
                return candidate;
            }
        }

        return null;
    }

    static void SetPadColor(GameObject padPart, Color color)
    {
        Renderer renderer = padPart.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    static void DisableColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }
}
