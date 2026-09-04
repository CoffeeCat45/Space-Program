using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global map-mode pass that hides 3D UI markers occluded by the planet disk.
/// </summary>
[DefaultExecutionOrder(600)]
public sealed class MapModeUiOcclusionPass : MonoBehaviour
{
    static readonly string[] MarkerNameTokens =
    {
        "Marker",
        "Handle",
        "Point",
        "Preview",
    };

    const int RescanIntervalFrames = 30;
    const float ScreenSpaceReferenceDistance = 1000f;
    const float ScreenSpaceBaseScale = 1.5f;
    const float ScreenSpaceMinScale = 0.5f;
    const float ScreenSpaceMaxScale = 5.0f;

    SimulationRunner runner;
    readonly List<MapModeOcclusionTarget> targets = new List<MapModeOcclusionTarget>();
    int rescanFrameCounter;

    public void Bind(SimulationRunner sessionRunner)
    {
        runner = sessionRunner;
    }

    void LateUpdate()
    {
        if (
            runner == null ||
            !runner.IsInitialized ||
            !runner.IsMapModeActive
        )
        {
            ClearAllOcclusion();
            targets.Clear();
            rescanFrameCounter = 0;
            return;
        }

        if (targets.Count == 0)
        {
            RescanTargets();
        }
        else if (
            !runner.IsSimulationFrozen() &&
            ++rescanFrameCounter >= RescanIntervalFrames
        )
        {
            rescanFrameCounter = 0;
            RescanTargets();
        }

        Camera camera = Camera.main;

        if (camera == null)
        {
            ClearAllOcclusion();
            return;
        }

        Vector3 planetCenter = Vector3.zero;
        float planetRadius = WorldVisualBridge.MapPlanetRadiusUnity;
        bool hasPlanetSphere =
            runner.TryGetPlanetVisualTransform(out Transform planetTransform) &&
            WorldVisualBridge.TryGetMapModePlanetOcclusionSphere(
                planetTransform,
                out planetCenter,
                out planetRadius
            );

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            MapModeOcclusionTarget target = targets[i];

            if (target == null)
            {
                targets.RemoveAt(i);
                continue;
            }

            target.ApplyOcclusion(
                camera,
                planetCenter,
                planetRadius,
                hasPlanetSphere
            );

            if (!target.SkipsCentralPresentation())
            {
                target.ApplyScreenSpacePresentation(
                    camera,
                    ScreenSpaceReferenceDistance,
                    ScreenSpaceBaseScale,
                    ScreenSpaceMinScale,
                    ScreenSpaceMaxScale
                );
            }
        }
    }

    void OnDisable()
    {
        ClearAllOcclusion();
        targets.Clear();
    }

    void ClearAllOcclusion()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
            {
                targets[i].ClearOcclusion();
            }
        }
    }

    void RescanTargets()
    {
        targets.Clear();

        if (runner == null)
        {
            return;
        }

        CollectTargetsFromRoot(runner.GetVesselVisualRoot());
        CollectTargetsFromRoot(runner.GetCelestialVisualRoot());

        OrbitPreviewView orbitPreview = runner.GetOrbitPreviewView();

        if (orbitPreview != null)
        {
            CollectTargetsFromRoot(orbitPreview.transform);
        }
    }

    void CollectTargetsFromRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < descendants.Length; i++)
        {
            Transform descendant = descendants[i];

            if (descendant == null || !IsOcclusionCandidate(descendant.gameObject))
            {
                continue;
            }

            MapModeOcclusionTarget target =
                descendant.GetComponent<MapModeOcclusionTarget>();

            if (target == null)
            {
                target = descendant.gameObject.AddComponent<MapModeOcclusionTarget>();
            }

            if (!targets.Contains(target))
            {
                targets.Add(target);
            }
        }
    }

    static bool IsOcclusionCandidate(GameObject gameObject)
    {
        if (gameObject == null || !MatchesMarkerName(gameObject.name))
        {
            return false;
        }

        if (IsPlanetGeometry(gameObject))
        {
            return false;
        }

        if (
            gameObject.name == "VesselVisuals" ||
            gameObject.name == "CelestialVisuals"
        )
        {
            return false;
        }

        LineRenderer lineRenderer = gameObject.GetComponent<LineRenderer>();
        TextMesh textMesh = gameObject.GetComponent<TextMesh>();

        if (lineRenderer != null && textMesh == null)
        {
            return false;
        }

        return
            gameObject.GetComponentInChildren<Renderer>(true) != null ||
            gameObject.GetComponentInChildren<CanvasGroup>(true) != null;
    }

    static bool MatchesMarkerName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        for (int i = 0; i < MarkerNameTokens.Length; i++)
        {
            if (objectName.IndexOf(
                MarkerNameTokens[i],
                StringComparison.OrdinalIgnoreCase
            ) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    static bool IsPlanetGeometry(GameObject gameObject)
    {
        return
            gameObject.GetComponentInParent<FlightPlanetMapSphere>() != null ||
            gameObject.GetComponentInParent<FlightPlanetSphere>() != null;
    }
}
