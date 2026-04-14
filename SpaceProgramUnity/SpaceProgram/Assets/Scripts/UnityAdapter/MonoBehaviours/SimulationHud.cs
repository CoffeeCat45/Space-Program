using UnityEngine;
using UnityEngine.InputSystem;

public class SimulationHud : MonoBehaviour
{
    public bool showHud = true;
    public bool showDetails;
    public int fontSize = 15;

    Simulation simulation;
    SimulationRunner runner;
    GUIStyle labelStyle;

    internal void SetSimulation(Simulation newSimulation)
    {
        simulation = newSimulation;
    }

    internal void SetRunner(SimulationRunner newRunner)
    {
        runner = newRunner;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (
            keyboard != null &&
            keyboard.hKey != null &&
            keyboard.hKey.wasPressedThisFrame
        )
        {
            showDetails = !showDetails;
        }
    }

    void OnGUI()
    {
        if (!showHud || simulation == null)
        {
            return;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
        }

        labelStyle.fontSize = fontSize;

        GUILayout.BeginArea(new Rect(12, 12, 430, showDetails ? 800 : 510));
        GUILayout.Label("Flight", labelStyle);
        GUILayout.Label(
            "Speed: " +
            FormatNumber(simulation.GetShipSpeed()) +
            "  Accel: " +
            FormatNumber(simulation.GetShipAcceleration()),
            labelStyle
        );
        GUILayout.Label(
            "Mass: " +
            FormatNumber(simulation.GetShipMass()) +
            "  Fuel: " +
            FormatNumber(simulation.GetShipFuelMass()) +
            " (" +
            FormatPercent(simulation.GetShipFuelRatio()) +
            ")",
            labelStyle
        );
        GUILayout.Label(
            "Alt: " +
            FormatNumber(simulation.GetShipAltitudeAboveActivePrimary()) +
            "  Body: " +
            simulation.activePrimaryBodyName,
            labelStyle
        );
        GUILayout.Label(
            "Orbit: " + simulation.GetShipOrbitTypeRelativeToActivePrimary(),
            labelStyle
        );
        GUILayout.Label(
            "Pe: " +
            FormatNumber(simulation.GetShipPeriapsisAltitudeRelativeToActivePrimary()) +
            "  Ap: " +
            FormatNumber(simulation.GetShipApoapsisAltitudeRelativeToActivePrimary()),
            labelStyle
        );

        if (runner != null)
        {
            DrawRunnerHud();
        }

        GUILayout.Space(8);
        GUILayout.Label("H: details  Tab: target  C: circularize", labelStyle);
        GUILayout.Label("1: manual  2: prograde  3: retrograde  4: maneuver", labelStyle);
        GUILayout.Label("V: warp to CA  J: Pe  K: Ap", labelStyle);
        GUILayout.Label("RMB: warp to maneuver start", labelStyle);
        GUILayout.Label("Enter near node: apply  Backspace: clear", labelStyle);
        GUILayout.Label("Arrows: maneuver  B: auto-burn", labelStyle);
        GUILayout.Label("Shift/Ctrl: throttle  Z: max  X: cut", labelStyle);
        GUILayout.Label(", / .: time x0.25..x100  P / Space: pause", labelStyle);
        GUILayout.EndArea();
    }

    void DrawRunnerHud()
    {
        global::Vector2 plannedDeltaV = runner.GetPlannedDeltaV();
        ClosestApproachInfo closestApproach =
            runner.GetCachedClosestApproach();
        SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview =
            runner.GetCachedSphereOfInfluenceEntryPreview();

        GUILayout.Label(
            "Time: x" + runner.GetEffectiveTimeScale().ToString("0.##"),
            labelStyle
        );
        GUILayout.Label(
            "Orientation: " + runner.GetShipOrientationModeName(),
            labelStyle
        );

        if (runner.IsWarping())
        {
            GUILayout.Label("Warping to " + runner.GetWarpLabel(), labelStyle);
        }

        if (runner.IsPaused())
        {
            GUILayout.Label("Paused", labelStyle);
        }

        GUILayout.Label(
            "Target: " +
            runner.GetTargetBodyName() +
            "  Dist: " +
            FormatNumber(runner.GetTargetDistance()),
            labelStyle
        );
        GUILayout.Label(
            "Target relative speed: " +
            FormatNumber(runner.GetTargetRelativeSpeed()),
            labelStyle
        );
        GUILayout.Label(
            "Target CA: " +
            FormatNumber(closestApproach.distance) +
            " in " +
            FormatTime(closestApproach.time),
            labelStyle
        );
        GUILayout.Label(
            "Time to Pe: " +
            FormatTime(runner.GetTimeToPeriapsis()) +
            "  Ap: " +
            FormatTime(runner.GetTimeToApoapsis()),
            labelStyle
        );
        GUILayout.Label(
            "Thrust accel: " +
            FormatNumber(simulation.GetShipThrustAcceleration()),
            labelStyle
        );
        GUILayout.Label(
            "Throttle: " + FormatPercent(runner.GetThrottle()),
            labelStyle
        );

        DrawManeuverHud(plannedDeltaV);

        if (runner.IsAutoBurnActive())
        {
            GUILayout.Label(
                runner.IsAutoBurning() ?
                "Auto-burn: firing" :
                "Auto-burn: waiting",
                labelStyle
            );
        }

        GUILayout.Label(
            "Circularize: " +
            FormatNumber(
                simulation
                    .GetShipCircularizeDeltaVRelativeToActivePrimary()
                    .Length()
            ),
            labelStyle
        );

        if (closestApproach.entersMoonSphereOfInfluence)
        {
            GUILayout.Label("Moon SoI encounter", labelStyle);
        }

        if (sphereOfInfluenceEntryPreview.entersSphereOfInfluence)
        {
            GUILayout.Label(
                "Time to Moon SoI: " +
                FormatTime(sphereOfInfluenceEntryPreview.entryTime),
                labelStyle
            );
        }

        if (showDetails)
        {
            DrawDetails(plannedDeltaV, closestApproach);
        }
    }

    void DrawManeuverHud(global::Vector2 plannedDeltaV)
    {
        double plannedDeltaVLength = plannedDeltaV.Length();
        double availableDeltaV = simulation.GetShipAvailableDeltaV();
        double burnDuration = runner.GetPlannedBurnDuration();

        GUILayout.Space(6);
        GUILayout.Label("Maneuver", labelStyle);
        GUILayout.Label(
            "Planned delta-v: " +
            FormatNumber(plannedDeltaVLength) +
            "  Available: " +
            FormatNumber(availableDeltaV),
            labelStyle
        );

        if (plannedDeltaVLength <= 0.0)
        {
            GUILayout.Label("Node: none", labelStyle);
            return;
        }

        GUILayout.Label(
            "Burn duration: " +
            FormatTime(burnDuration) +
            "  burn start T+" +
            FormatTime(runner.GetPlannedBurnStartTime()),
            labelStyle
        );

        if (runner.HasManeuverNode())
        {
            GUILayout.Label(
                "Node time T+" +
                FormatTime(runner.GetManeuverNodeTime()) +
                "  node dv " +
                FormatNumber(runner.GetManeuverNodeDeltaV().Length()),
                labelStyle
            );
        }
        else
        {
            GUILayout.Label("Node time T+0.0s  immediate plan", labelStyle);
        }

        if (plannedDeltaVLength > availableDeltaV)
        {
            GUILayout.Label("Warning: not enough fuel for maneuver", labelStyle);
        }

        if (IsLongBurn(burnDuration))
        {
            GUILayout.Label("Warning: long burn, result can drift", labelStyle);
        }
    }

    bool IsLongBurn(double burnDuration)
    {
        double orbitalPeriod = simulation.GetShipOrbitalPeriodRelativeToActivePrimary();

        if (
            burnDuration <= 0.0 ||
            double.IsInfinity(burnDuration) ||
            double.IsInfinity(orbitalPeriod) ||
            orbitalPeriod <= 0.0
        )
        {
            return false;
        }

        return burnDuration > orbitalPeriod * 0.08;
    }

    void DrawDetails(
        global::Vector2 plannedDeltaV,
        ClosestApproachInfo closestApproach
    )
    {
        GUILayout.Space(8);
        GUILayout.Label("Details", labelStyle);
        GUILayout.Label(
            "Altitude over planet: " +
            FormatNumber(simulation.GetShipAltitudeAbovePlanet()),
            labelStyle
        );
        GUILayout.Label(
            "Moon distance: " + FormatNumber(simulation.GetShipDistanceToMoon()),
            labelStyle
        );
        GUILayout.Label(
            "Closest approach to " +
            closestApproach.targetBodyName +
            ": " +
            FormatNumber(closestApproach.distance),
            labelStyle
        );
        GUILayout.Label(
            "Time to CA: " + FormatTime(closestApproach.time),
            labelStyle
        );
        GUILayout.Label(
            "Relative speed at CA: " +
            FormatNumber(closestApproach.relativeSpeed),
            labelStyle
        );
        GUILayout.Label(
            "Delta-v step: " +
            FormatNumber(runner.plannedDeltaVStep),
            labelStyle
        );

        if (plannedDeltaV.LengthSquared() > 0)
        {
            if (runner.HasManeuverNode())
            {
                GUILayout.Label(
                    "Preview orbit: from maneuver node",
                    labelStyle
                );
                return;
            }

            GUILayout.Label("Preview orbit: finite burn", labelStyle);
            GUILayout.Label(
                "Preview period: " + FormatTime(runner.GetPlannedOrbitPeriod()),
                labelStyle
            );
        }
    }

    string FormatNumber(double value)
    {
        if (double.IsInfinity(value))
        {
            return "infinity";
        }

        return value.ToString("0.00");
    }

    string FormatTime(double value)
    {
        if (double.IsInfinity(value))
        {
            return "not useful";
        }

        return value.ToString("0.0") + "s";
    }

    string FormatPercent(double value)
    {
        return (value * 100.0).ToString("0") + "%";
    }
}
