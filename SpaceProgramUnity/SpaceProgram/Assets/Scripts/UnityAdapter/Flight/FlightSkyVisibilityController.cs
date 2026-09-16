using UnityEngine;

/// <summary>
/// Drives KSP-style sky transitions in flight. Map mode always shows full galaxy skybox.
/// </summary>
[DefaultExecutionOrder(105)]
public class FlightSkyVisibilityController : MonoBehaviour
{
    SimulationRunner runner;

    public void Bind(SimulationRunner sessionRunner)
    {
        runner = sessionRunner;
    }

    void OnDisable()
    {
        PartEnvironmentReflections.NotifyLeftFlight();
    }

    void LateUpdate()
    {
        if (runner == null || !runner.IsInitialized)
        {
            return;
        }

        runner.TryGetPlanetVisualTransform(out Transform planetTransform);

        if (runner.IsMapModeActive)
        {
            GalaxySkyboxEnvironment.ApplyMapModeVisibility();
            SyncEarthCloudCompositing(mapMode: true);
            PartEnvironmentReflections.SetEnabled(
                GameSettingsStore.ReadPartEnvironmentReflections()
            );
            PartEnvironmentReflections.TickFlight(0.0, useSpaceCubemap: true);

            if (planetTransform != null)
            {
                FindAtmosphere(planetTransform)?.ApplyMapModePresentation(true);
            }

            return;
        }

        Simulation simulation = runner.Simulation;

        if (simulation == null)
        {
            return;
        }

        if (CelestialTerrainRuntime.IsMoon)
        {
            GalaxySkyboxEnvironment.ApplyMapModeVisibility();
            ApplyFlightSkyboxOrientation(planetTransform);
            SyncEarthCloudCompositing(mapMode: false);
            PartEnvironmentReflections.SetEnabled(
                GameSettingsStore.ReadPartEnvironmentReflections()
            );
            PartEnvironmentReflections.TickFlight(
                simulation.GetShipAltitudeAbovePlanet(),
                useSpaceCubemap: true
            );
            return;
        }

        double altitudeMeters = simulation.GetShipAltitudeAbovePlanet();
        GalaxySkyboxEnvironment.ApplyAltitudeVisibility(altitudeMeters);
        ApplyFlightSkyboxOrientation(planetTransform);
        PartEnvironmentReflections.SetEnabled(
            GameSettingsStore.ReadPartEnvironmentReflections()
        );
        PartEnvironmentReflections.TickFlight(altitudeMeters, useSpaceCubemap: false);

        FlightPlanetAtmosphereSphere atmosphere = FindAtmosphere(planetTransform);

        if (atmosphere == null)
        {
            return;
        }

        atmosphere.ApplyCameraAltitude(altitudeMeters);

        Camera camera = Camera.main;

        if (camera != null)
        {
            atmosphere.UpdateCameraFacing(camera.transform.position);
        }
    }

    void ApplyFlightSkyboxOrientation(Transform planetTransform)
    {
        if (WorldVisualBridge.TryGetFlightVisualInertialToFlight(
            out Quaternion inertialToFlight
        ))
        {
            GalaxySkyboxEnvironment.ApplySkyboxRotation(inertialToFlight);
            return;
        }

        if (planetTransform == null || runner == null)
        {
            GalaxySkyboxEnvironment.ApplySkyboxRotation(Quaternion.identity);
            return;
        }

        inertialToFlight = WorldVisualBridge.GetInertialMeshToFlightRotation(
            planetTransform.localRotation,
            runner.GetInterpolatedPlanetSpinRotation()
        );
        GalaxySkyboxEnvironment.ApplySkyboxRotation(inertialToFlight);
    }

    static FlightPlanetAtmosphereSphere FindAtmosphere(Transform planetTransform)
    {
        if (planetTransform == null)
        {
            return null;
        }

        return planetTransform.Find("PlanetAtmosphere")
            ?.GetComponent<FlightPlanetAtmosphereSphere>();
    }

    void SyncEarthCloudCompositing(bool mapMode)
    {
        if (runner == null || !runner.TryGetBodyVisualTransform("Planet", out Transform earth))
        {
            return;
        }

        DistantCelestialGlobe globe = earth.GetComponent<DistantCelestialGlobe>();

        if (globe != null)
        {
            globe.ApplyCompositing(mapMode);
            Camera camera = Camera.main;

            if (camera != null)
            {
                globe.SyncTransparentShells(camera.transform.position);
            }

            return;
        }

        FlightPlanetSphere lod = earth.GetComponent<FlightPlanetSphere>();

        if (lod != null && lod.enabled)
        {
            FlightPlanetSphere.ApplyRuntimeAtmosphereCloudVisuals();
        }
    }
}
