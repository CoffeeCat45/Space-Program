using UnityEngine;

/// <summary>
/// RSS terrain height under the ship for flight-mode visual grounding.
/// </summary>
static class PlanetFlightTerrainHeight
{
    public static float SampleEffectiveHeightMeters(
        Simulation simulation,
        global::SimVector3 outwardDirection
    )
    {
        if (simulation == null || outwardDirection.LengthSquared() <= 1e-12)
        {
            return 0f;
        }

        if (ShouldUseLaunchPadFlattenedHeight(simulation))
        {
            return (float)LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters();
        }

        return SampleHeightAtDirection(outwardDirection);
    }

    static bool ShouldUseLaunchPadFlattenedHeight(Simulation simulation)
    {
        return SimulationScenarios.IsAnySurfacePad(simulation.scenario) &&
            simulation.IsShipOnCoveLaunchPad();
    }

    static float SampleHeightAtDirection(global::SimVector3 outwardDirection)
    {
        return PlanetFlightElevationSampling.SampleMeshHeightOffsetMeters(outwardDirection);
    }
}
