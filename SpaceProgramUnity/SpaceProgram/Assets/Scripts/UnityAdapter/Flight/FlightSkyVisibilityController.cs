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
                FlightPlanetAtmosphereSphere atmosphereSphere = planetTransform
                    .Find("PlanetAtmosphere")
                    ?.GetComponent<FlightPlanetAtmosphereSphere>();

                atmosphereSphere?.ApplyMapModePresentation(true);
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
            return;
        }

        if (simulation.planet == null)
        {
            return;
        }

        double altitudeMeters = simulation.GetShipAltitudeAbovePlanet();
        GalaxySkyboxEnvironment.ApplyAltitudeVisibility(altitudeMeters);

        if (planetTransform != null)
        {
            FlightPlanetAtmosphereSphere atmosphereSphere = planetTransform
                .Find("PlanetAtmosphere")
                ?.GetComponent<FlightPlanetAtmosphereSphere>();

            if (atmosphereSphere != null)
            {
                atmosphereSphere.ApplyCameraAltitude(altitudeMeters);
            }
        }
    }
}
