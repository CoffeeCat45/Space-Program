using System;
using System.IO;
using UnityEngine;

public enum PlayMissionPhase
{
    None,
    Liftoff,
    Ascent,
    Stage,
    OrbitCircularize,
    Deorbit,
    Reentry,
    Landing,
    Complete,
    Crashed
}

class SimulationPlaySession
{
    const string DefaultPadVesselName = "test";
    const string DefaultLaunchShipFileName = "default_launch_v2.ship";
    const string LegacyLaunchShipFileName = "default_launch.ship";
    const string PadLaunchShipFileName = "pad_ship.ship";
    const string VabLaunchShipFileName = "_vab_launch.ship";
    const string ShipsFolderName = "Ships";

    bool hasLaunched;
    bool reachedOrbit;
    PlayMissionPhase missionPhase = PlayMissionPhase.None;
    SimulationScenario missionScenario;
    string overrideLaunchShipPath;
    string sessionPadLaunchShipPath;

    public PlayMissionPhase MissionPhase => missionPhase;

    public SimulationScenario GetStartupScenario(SimulationScenario inspectorDefault)
    {
        SimulationScenario startup = inspectorDefault;
        GameSettingsStore.EnsureLoaded();

        if (Enum.IsDefined(typeof(SimulationScenario), GameSettingsStore.Data.play.lastScenario))
        {
            startup = (SimulationScenario)GameSettingsStore.Data.play.lastScenario;
        }

        return NormalizePlayableScenario(startup, inspectorDefault);
    }

    public SimulationScenario NormalizePlayableScenario(
        SimulationScenario scenario,
        SimulationScenario fallback = SimulationScenario.PlanetSurfaceStart
    )
    {
        switch (scenario)
        {
            case SimulationScenario.PlanetSurfaceStart:
            case SimulationScenario.PlanetSurfaceFlorida:
            case SimulationScenario.PlanetSurfaceMorocco:
            case SimulationScenario.PlanetSurfaceTibet:
            case SimulationScenario.PlanetSurfaceGreenwich:
            case SimulationScenario.MoonSurfaceStart:
            case SimulationScenario.InclinedOrbit:
            case SimulationScenario.PlanetMoonShip:
            case SimulationScenario.SolarTransfer:
            case SimulationScenario.DockingApproach:
                return scenario;
            case SimulationScenario.MoonFlyby:
            case SimulationScenario.PlanetMoonEllipticMoon:
            case SimulationScenario.TwoBodyCircular:
            case SimulationScenario.PlanetAtmosphericEntry:
                return SimulationScenario.PlanetMoonShip;
            default:
                return fallback;
        }
    }

    public bool IsPlayableScenario(SimulationScenario scenario)
    {
        return
            SimulationScenarios.IsAnySurfacePad(scenario) ||
            scenario == SimulationScenario.InclinedOrbit ||
            scenario == SimulationScenario.PlanetMoonShip ||
            scenario == SimulationScenario.SolarTransfer ||
            scenario == SimulationScenario.DockingApproach;
    }

    public void SaveLastScenario(SimulationScenario scenario)
    {
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.play.lastScenario = (int)scenario;
        GameSettingsStore.SaveToDisk();
    }

    public void ResetMissionTracking()
    {
        hasLaunched = false;
        reachedOrbit = false;
        missionPhase = PlayMissionPhase.None;
        missionScenario = SimulationScenario.PlanetSurfaceStart;
        overrideLaunchShipPath = null;
    }

    public void SetSessionPadLaunchShipPath(string shipPath)
    {
        sessionPadLaunchShipPath = shipPath;
    }

    public void ClearSessionPadLaunchShipPath()
    {
        sessionPadLaunchShipPath = null;
    }

    public void SetOverrideLaunchShipPath(string shipPath)
    {
        overrideLaunchShipPath = shipPath;
    }

    public void ClearOverrideLaunchShipPath()
    {
        overrideLaunchShipPath = null;
    }

    public string GetVabLaunchShipPath()
    {
        return VabFileSystem.GetShipFilePath(VabLaunchShipFileName);
    }

    static string GetPadLaunchShipPath()
    {
        return VabFileSystem.GetShipFilePath(PadLaunchShipFileName);
    }

    static string GetBundledLaunchShipPath(string fileName)
    {
        return Path.Combine(Application.streamingAssetsPath, ShipsFolderName, fileName);
    }

    public bool ShouldShowMissionHints(SimulationScenario scenario)
    {
        return SimulationScenarios.IsPlanetSurfacePad(scenario);
    }

    public string GetScenarioDisplayKey(SimulationScenario scenario)
    {
        switch (NormalizePlayableScenario(scenario))
        {
            case SimulationScenario.PlanetSurfaceStart:
                return "Scenario from pad";
            case SimulationScenario.PlanetSurfaceFlorida:
                return "Scenario from pad";
            case SimulationScenario.PlanetSurfaceMorocco:
                return "Scenario morocco pad";
            case SimulationScenario.PlanetSurfaceTibet:
                return "Scenario tibet pad";
            case SimulationScenario.PlanetSurfaceGreenwich:
                return "Scenario greenwich pad";
            case SimulationScenario.MoonSurfaceStart:
                return "Scenario moon pad";
            case SimulationScenario.InclinedOrbit:
                return "Mission hint inclined orbit";
            case SimulationScenario.PlanetMoonShip:
                return "Scenario planet moon orbit";
            case SimulationScenario.SolarTransfer:
                return "Scenario solar system orbit";
            case SimulationScenario.DockingApproach:
                return "Scenario docking approach";
            default:
                return scenario.ToString();
        }
    }

    public SimulationScenario[] GetMenuScenarios()
    {
        return new[]
        {
            SimulationScenario.PlanetSurfaceStart,
            SimulationScenario.PlanetSurfaceMorocco,
            SimulationScenario.PlanetSurfaceTibet,
            SimulationScenario.PlanetSurfaceGreenwich,
            SimulationScenario.MoonSurfaceStart,
            SimulationScenario.InclinedOrbit,
            SimulationScenario.PlanetMoonShip,
            SimulationScenario.SolarTransfer,
            SimulationScenario.DockingApproach,
        };
    }

    internal bool TryApplyLaunchShip(Simulation simulation)
    {
        if (
            simulation == null ||
            !SimulationScenarios.IsAnySurfacePad(simulation.scenario)
        )
        {
            return false;
        }

        string launchShipPath = ResolveLaunchShipPath();

        if (string.IsNullOrEmpty(launchShipPath))
        {
            Debug.LogError(
                "[PadLaunch] No exported VAB ship file found. " +
                "Build a vessel in VAB and launch from the pad."
            );
            return false;
        }

        if (simulation.TryApplySpawnShip(launchShipPath))
        {
            simulation.FinalizeLandedShipOnSurface();
            simulation.RefreshLaunchPadGrounding();
            simulation.RefreshInitialShipSnapshot();
            return true;
        }

        Debug.LogError(
            "[PadLaunch] Failed to load launch ship: " + launchShipPath
        );
        return false;
    }

    public string GetDefaultPadVesselName()
    {
        return DefaultPadVesselName;
    }

    public string ResolveLaunchShipPath()
    {
        if (
            !string.IsNullOrWhiteSpace(overrideLaunchShipPath) &&
            File.Exists(overrideLaunchShipPath)
        )
        {
            return overrideLaunchShipPath;
        }

        if (
            !string.IsNullOrWhiteSpace(sessionPadLaunchShipPath) &&
            File.Exists(sessionPadLaunchShipPath)
        )
        {
            return sessionPadLaunchShipPath;
        }

        string vabLaunchShipPath = GetVabLaunchShipPath();

        if (File.Exists(vabLaunchShipPath))
        {
            return vabLaunchShipPath;
        }

        string padShipPath = GetPadLaunchShipPath();

        if (File.Exists(padShipPath))
        {
            return padShipPath;
        }

        string defaultShipPath = VabFileSystem.GetShipFilePath(DefaultLaunchShipFileName);

        if (File.Exists(defaultShipPath))
        {
            return defaultShipPath;
        }

        string legacyShipPath = VabFileSystem.GetShipFilePath(LegacyLaunchShipFileName);

        if (File.Exists(legacyShipPath))
        {
            return legacyShipPath;
        }

        string streamingPath = GetBundledLaunchShipPath(DefaultLaunchShipFileName);

        if (File.Exists(streamingPath))
        {
            return streamingPath;
        }

        string legacyStreamingPath = GetBundledLaunchShipPath(LegacyLaunchShipFileName);

        if (File.Exists(legacyStreamingPath))
        {
            return legacyStreamingPath;
        }

        return string.Empty;
    }

    internal void UpdateMissionPhase(
        SimulationScenario scenario,
        ISimulationStateView simulation
    )
    {
        missionScenario = scenario;

        if (!ShouldShowMissionHints(scenario) || simulation == null)
        {
            missionPhase = PlayMissionPhase.None;
            return;
        }

        FlightStatus flightStatus = simulation.GetFlightStatus();

        if (flightStatus == FlightStatus.Crashed)
        {
            missionPhase = PlayMissionPhase.Crashed;
            return;
        }

        if (flightStatus == FlightStatus.Landed)
        {
            missionPhase = hasLaunched ?
                PlayMissionPhase.Complete :
                PlayMissionPhase.Liftoff;
            return;
        }

        hasLaunched = true;

        if (simulation.IsShipInAtmosphere())
        {
            double altitude = simulation.GetShipAltitudeAboveActivePrimary();
            double verticalSpeed =
                simulation.GetShipVerticalSpeedRelativeToActivePrimary();

            if (altitude < 25000.0)
            {
                missionPhase = PlayMissionPhase.Landing;
                return;
            }

            if (verticalSpeed < -5.0 || simulation.GetShipReentryIntensity() > 0.05)
            {
                missionPhase = PlayMissionPhase.Reentry;
                return;
            }

            missionPhase = PlayMissionPhase.Ascent;
            return;
        }

        if (
            simulation.GetActiveStageNumber() == 1 &&
            simulation.GetRemainingStageCount() > 1 &&
            simulation.GetShipAltitudeAboveActivePrimary() > 40000.0
        )
        {
            missionPhase = PlayMissionPhase.Stage;
            return;
        }

        string orbitType = simulation.GetShipOrbitTypeRelativeToActivePrimary();
        double apoapsis = simulation.GetShipApoapsisAltitudeRelativeToActivePrimary();
        double periapsis = simulation.GetShipPeriapsisAltitudeRelativeToActivePrimary();

        if (
            orbitType == "Ellipse" &&
            apoapsis > 0.0 &&
            apoapsis - periapsis > 120.0
        )
        {
            reachedOrbit = true;
            missionPhase = PlayMissionPhase.OrbitCircularize;
            return;
        }

        if (reachedOrbit || apoapsis > 70000.0)
        {
            reachedOrbit = true;
            missionPhase = PlayMissionPhase.Deorbit;
            return;
        }

        missionPhase = PlayMissionPhase.Ascent;
    }

    public string GetMissionHintKey()
    {
        switch (missionPhase)
        {
            case PlayMissionPhase.Liftoff:
                return "Mission hint liftoff";
            case PlayMissionPhase.Ascent:
                return "Mission hint ascent";
            case PlayMissionPhase.Stage:
                return "Mission hint stage";
            case PlayMissionPhase.OrbitCircularize:
                return "Mission hint circularize";
            case PlayMissionPhase.Deorbit:
                return "Mission hint deorbit";
            case PlayMissionPhase.Reentry:
                return "Mission hint reentry";
            case PlayMissionPhase.Landing:
                return "Mission hint landing";
            case PlayMissionPhase.Complete:
                return "Mission hint complete";
            case PlayMissionPhase.Crashed:
                return "Mission hint crashed";
            default:
                return string.Empty;
        }
    }
}
