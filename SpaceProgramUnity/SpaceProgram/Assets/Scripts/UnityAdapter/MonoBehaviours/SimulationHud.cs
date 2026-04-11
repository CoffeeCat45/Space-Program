using UnityEngine;

public class SimulationHud : MonoBehaviour
{
    public bool showHud = true;

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

    void OnGUI()
    {
        if (!showHud || simulation == null)
        {
            return;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 18;
            labelStyle.normal.textColor = Color.white;
        }

        GUILayout.BeginArea(new Rect(12, 12, 520, 520));
        GUILayout.Label("Flight", labelStyle);
        GUILayout.Label("Speed: " + FormatNumber(simulation.GetShipSpeed()), labelStyle);
        GUILayout.Label("Acceleration: " + FormatNumber(simulation.GetShipAcceleration()), labelStyle);
        GUILayout.Label("Altitude over planet: " + FormatNumber(simulation.GetShipAltitudeAbovePlanet()), labelStyle);
        GUILayout.Label("Altitude over active body: " + FormatNumber(simulation.GetShipAltitudeAboveActivePrimary()), labelStyle);
        GUILayout.Label("Moon distance: " + FormatNumber(simulation.GetShipDistanceToMoon()), labelStyle);
        GUILayout.Label("Active body: " + simulation.activePrimaryBodyName, labelStyle);
        GUILayout.Label("Orbit: " + simulation.GetShipOrbitTypeRelativeToActivePrimary(), labelStyle);

        if (runner != null)
        {
            global::Vector2 plannedDeltaV = runner.GetPlannedDeltaV();
            ClosestApproachInfo closestApproach =
                runner.GetCachedClosestApproach();
            SphereOfInfluenceEntryPreview sphereOfInfluenceEntryPreview =
                runner.GetCachedSphereOfInfluenceEntryPreview();
            GUILayout.Label("Time scale: x" + runner.GetEffectiveTimeScale().ToString("0.##"), labelStyle);

            if (runner.IsPaused())
            {
                GUILayout.Label("Paused", labelStyle);
            }

            GUILayout.Label("Planned delta-v: " + FormatNumber(plannedDeltaV.Length()), labelStyle);
            GUILayout.Label("Closest approach: " + FormatNumber(closestApproach.distance), labelStyle);
            GUILayout.Label("Time to CA: " + FormatTime(closestApproach.time), labelStyle);

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

            GUILayout.Label(
                "Delta-v step: " +
                FormatNumber(runner.plannedDeltaVStep) +
                " / Ctrl " +
                FormatNumber(runner.GetFineManeuverStep()) +
                " / Shift " +
                FormatNumber(runner.GetCoarseManeuverStep()),
                labelStyle
            );

            if (plannedDeltaV.LengthSquared() > 0)
            {
                GUILayout.Label("Preview orbit: " + simulation.GetPreviewShipOrbitTypeRelativeToActivePrimary(plannedDeltaV), labelStyle);
                GUILayout.Label("Preview eccentricity: " + FormatNumber(simulation.GetPreviewShipOrbitalEccentricityRelativeToActivePrimary(plannedDeltaV)), labelStyle);
                GUILayout.Label("Preview periapsis alt: " + FormatNumber(simulation.GetPreviewShipPeriapsisAltitudeRelativeToActivePrimary(plannedDeltaV)), labelStyle);
                GUILayout.Label("Preview apoapsis alt: " + FormatNumber(simulation.GetPreviewShipApoapsisAltitudeRelativeToActivePrimary(plannedDeltaV)), labelStyle);
            }
        }

        GUILayout.Label("Arrows: plan maneuver, Enter: apply, Backspace: clear", labelStyle);
        GUILayout.Label("Ctrl + arrows: fine, Shift + arrows: coarse", labelStyle);
        GUILayout.Label("б / ю: time scale down / up", labelStyle);
        GUILayout.Label("P / Space: pause", labelStyle);
        GUILayout.EndArea();
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
        return value.ToString("0.0") + "s";
    }
}
