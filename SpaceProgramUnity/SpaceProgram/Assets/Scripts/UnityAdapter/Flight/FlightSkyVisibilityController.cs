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
            SyncDistantEarthShells();
            return;
        }

        double altitudeMeters = simulation.GetShipAltitudeAbovePlanet();
        GalaxySkyboxEnvironment.ApplyAltitudeVisibility(altitudeMeters);

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

    static FlightPlanetAtmosphereSphere FindAtmosphere(Transform planetTransform)
    {
        if (planetTransform == null)
        {
            return null;
        }

        return planetTransform.Find("PlanetAtmosphere")
            ?.GetComponent<FlightPlanetAtmosphereSphere>();
    }

    void SyncDistantEarthShells()
    {
        if (runner == null || !runner.TryGetBodyVisualTransform("Planet", out Transform earth))
        {
            return;
        }

        DistantCelestialGlobe globe = earth.GetComponent<DistantCelestialGlobe>();

        if (globe == null)
        {
            return;
        }

        globe.ApplyCompositing(false);
        Camera camera = Camera.main;

        if (camera != null)
        {
            globe.SyncTransparentShells(camera.transform.position);
        }
    }
}
