using UnityEngine;

static class ScenarioVisualDefaults
{
    public static void ApplyToRunner(SimulationRunner runner, Simulation simulation)
    {
        if (runner == null || simulation == null)
        {
            return;
        }
    }

    public static void ApplyToCamera(
        CameraController camera,
        Simulation simulation,
        bool isMapMode
    )
    {
        if (camera == null || simulation == null)
        {
            return;
        }

        switch (simulation.scenario)
        {
            case SimulationScenario.PlanetSurfaceStart:
            case SimulationScenario.PlanetSurfaceFlorida:
            case SimulationScenario.PlanetSurfaceMorocco:
            case SimulationScenario.PlanetSurfaceTibet:
            case SimulationScenario.PlanetSurfaceGreenwich:
            case SimulationScenario.MoonSurfaceStart:
                ApplyPadCamera(camera, isMapMode);
                break;
            case SimulationScenario.InclinedOrbit:
                ApplyInclinedOrbitCamera(camera, isMapMode);
                break;
            case SimulationScenario.DockingApproach:
                ApplyDockingCamera(camera, isMapMode);
                break;
            default:
                camera.ResetScenarioDefaults();
                break;
        }
    }

    static void ApplyPadCamera(CameraController camera, bool isMapMode)
    {
        camera.fieldOfView = 62.0f;
        camera.orbitDistanceScale = 1.0f;

        if (isMapMode)
        {
            // Orbit yaw/pitch are snapped to the launch-site outward radial when map mode
            // activates (see SimulationRunner.SnapMapModeCameraToLaunchHemisphere /
            // CameraController.SnapMapModeOrbitToPlanetExterior).
            camera.SetMapOrbitDistanceSimMeters(800_000.0);
        }
        else
        {
            camera.SetOrbitAngles(90.0f, 10.0f);
            camera.SetNormalOrbitDistanceUnity(55.0f);
        }
    }

    static void ApplyInclinedOrbitCamera(CameraController camera, bool isMapMode)
    {
        camera.fieldOfView = 55.0f;
        camera.SetOrbitAngles(90.0f, 12.0f);
        camera.orbitDistanceScale = 1.0f;

        if (isMapMode)
        {
            camera.SetMapOrbitDistanceSimMeters(800_000.0);
        }
        else
        {
            camera.SetNormalOrbitDistanceUnity(48.0f);
        }
    }

    static void ApplyDockingCamera(CameraController camera, bool isMapMode)
    {
        camera.fieldOfView = 58.0f;
        camera.SetOrbitAngles(90.0f, 10.0f);
        camera.orbitDistanceScale = 1.0f;

        if (isMapMode)
        {
            camera.SetMapOrbitDistanceSimMeters(2_000.0);
        }
        else
        {
            camera.SetNormalOrbitDistanceUnity(55.0f);
        }
    }
}
