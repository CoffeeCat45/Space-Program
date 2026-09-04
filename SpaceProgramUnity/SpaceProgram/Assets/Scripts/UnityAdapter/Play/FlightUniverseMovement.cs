using UnityEngine;

/// <summary>
/// Keeps the visual universe aligned with SimulationCore while the vessel stays at frame zero.
/// Universe root slides on -Y for altitude; planet center on -Y.
/// Flight: unique surface-frame planet attitude (geography under the ship, no free twist).
/// Map globe: GetMapPlanetMeshFrameSpinRotation (inertial Mesh Frame spin).
/// </summary>
[DefaultExecutionOrder(100)]
public class FlightUniverseMovement : MonoBehaviour
{
    SimulationRunner runner;
    Quaternion lastPlanetRotation = Quaternion.identity;
    bool hasLastPlanetRotation;

    public void Bind(SimulationRunner sessionRunner)
    {
        runner = sessionRunner;
    }

    void LateUpdate()
    {
        ApplyVisual(forcePlanetRotation: false);
    }

    /// <summary>
    /// Apply immediately (map → flight) so the camera is not left inside the globe
    /// for a frame while LateUpdate has not run yet.
    /// </summary>
    public void ApplyVisualNow()
    {
        ApplyVisual(forcePlanetRotation: true);
    }

    void ApplyVisual(bool forcePlanetRotation)
    {
        if (runner == null || !runner.IsInitialized)
        {
            return;
        }

        Simulation simulation = runner.Simulation;

        if (simulation == null || simulation.planet == null || simulation.ship == null)
        {
            return;
        }

        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        bool frozen = runner.IsSimulationFrozen();

        if (runner.IsMapModeActive)
        {
            ApplyMapModeVisual(frozen);
            return;
        }

        ApplyFlightModeUniverseVisual(forcePlanetRotation);
    }

    void ApplyMapModeVisual(bool frozen)
    {
        transform.localPosition = Vector3.zero;

        if (
            frozen ||
            !runner.TryGetPlanetVisualTransform(out Transform mapPlanet) ||
            mapPlanet == null
        )
        {
            runner.SyncFlightVisualLate();
            return;
        }

        // Map compresses the baked 1:1 globe (MapPositionScale). Do not reset to 1 —
        // that leaves the camera at the planet origin inside a full-radius Earth.
        mapPlanet.localScale = Vector3.one * WorldVisualBridge.MapPositionScale;
        Quaternion mapSpin = WorldVisualBridge.GetMapPlanetMeshFrameSpinRotation(
            runner.GetInterpolatedPlanetSpinRotation()
        );
        mapPlanet.localRotation = QuaternionIsFinite(mapSpin) ?
            mapSpin :
            Quaternion.identity;

        runner.SyncFlightVisualLate();
    }

    void ApplyFlightModeUniverseVisual(bool forcePlanetRotation)
    {
        Simulation simulation = runner.Simulation;

        global::SimVector3 shipRadial = runner.GetInterpolatedShipRadialOutSim();
        double bottomExtent = LaunchPadConstants.GetEffectiveBottomExtentMeters(
            simulation.ship.GetBottomExtentAlongWorldDirection(shipRadial)
        );
        double altitude = WorldVisualBridge.GetFlightVisualAltitudeAboveSphereMeters(simulation);
        double surfaceHeight = WorldVisualBridge.GetPlanetSurfaceHeightOffsetMeters(simulation);
        float distantScale = WorldVisualBridge.GetFlightDistantVisualScale(
            altitude,
            bottomExtent,
            surfaceHeight
        );
        transform.localPosition = WorldVisualBridge.GetFlightUniverseRootLocalOffsetForShipCom(
            altitude,
            bottomExtent
        ) * distantScale;

        if (
            !runner.TryGetPlanetVisualTransform(out Transform planetTransform) ||
            planetTransform == null
        )
        {
            return;
        }

        planetTransform.localScale = Vector3.one * distantScale;
        planetTransform.localPosition = WorldVisualBridge.GetFlightPlanetFixedLocalPosition(
            surfaceHeight
        ) * distantScale;

        if (!IsFiniteVector(planetTransform.localPosition))
        {
            planetTransform.localScale = Vector3.one;
            planetTransform.localPosition = WorldVisualBridge.GetFlightPlanetFixedLocalPosition(0.0);
        }

        Quaternion flightRotation = WorldVisualBridge.GetFlightPlanetRotationFromBodyFixed(
            runner.GetInterpolatedBodyFixedUnderShip()
        );

        if (hasLastPlanetRotation)
        {
            flightRotation = WorldVisualBridge.PickNearestQuaternion(
                lastPlanetRotation,
                flightRotation
            );
        }

        bool padLocked = WorldVisualBridge.IsFlightPadSurfaceLocked(simulation);
        bool rotationChanged = Quaternion.Angle(
            planetTransform.localRotation,
            flightRotation
        ) > 0.0001f;

        if (QuaternionIsFinite(flightRotation) &&
            (forcePlanetRotation || !padLocked || rotationChanged))
        {
            planetTransform.localRotation = flightRotation;
            lastPlanetRotation = flightRotation;
            hasLastPlanetRotation = true;
        }

        runner.RefreshLaunchPadVisual();
        runner.RefreshFlightDistantCelestialTransforms();
        runner.SyncFlightVisualLate();
    }

    static bool QuaternionIsFinite(Quaternion rotation)
    {
        return
            !float.IsNaN(rotation.x) &&
            !float.IsNaN(rotation.y) &&
            !float.IsNaN(rotation.z) &&
            !float.IsNaN(rotation.w) &&
            !float.IsInfinity(rotation.x) &&
            !float.IsInfinity(rotation.y) &&
            !float.IsInfinity(rotation.z) &&
            !float.IsInfinity(rotation.w);
    }

    static bool IsFiniteVector(Vector3 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y) &&
            !float.IsInfinity(value.z);
    }
}
