using UnityEngine;

/// <summary>
/// Moving visual container for planet + launch site. Ship visuals stay outside at frame zero.
/// </summary>
public class FlightUniverseRoot : MonoBehaviour
{
    public const string RootObjectName = "FlightUniverseRoot";

    SimulationRunner runner;

    public static FlightUniverseRoot Ensure(SimulationRunner sessionRunner)
    {
        if (sessionRunner == null)
        {
            return null;
        }

        Transform existing = sessionRunner.transform.Find(RootObjectName);

        if (existing != null)
        {
            FlightUniverseRoot component = existing.GetComponent<FlightUniverseRoot>();

            if (component != null)
            {
                component.runner = sessionRunner;
                EnsureMovement(sessionRunner, existing.gameObject);
                EnsureSkyVisibility(sessionRunner, existing.gameObject);
                return component;
            }
        }

        GameObject rootObject = new GameObject(RootObjectName);
        rootObject.transform.SetParent(sessionRunner.transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;

        FlightUniverseRoot created = rootObject.AddComponent<FlightUniverseRoot>();
        created.runner = sessionRunner;
        EnsureMovement(sessionRunner, rootObject);
        EnsureSkyVisibility(sessionRunner, rootObject);
        return created;
    }

    static void EnsureMovement(SimulationRunner sessionRunner, GameObject rootObject)
    {
        FlightUniverseMovement movement = rootObject.GetComponent<FlightUniverseMovement>();

        if (movement == null)
        {
            movement = rootObject.AddComponent<FlightUniverseMovement>();
        }

        movement.Bind(sessionRunner);
    }

    static void EnsureSkyVisibility(SimulationRunner sessionRunner, GameObject rootObject)
    {
        FlightSkyVisibilityController skyVisibility =
            rootObject.GetComponent<FlightSkyVisibilityController>();

        if (skyVisibility == null)
        {
            skyVisibility = rootObject.AddComponent<FlightSkyVisibilityController>();
        }

        skyVisibility.Bind(sessionRunner);
    }

    public Transform RootTransform => transform;

    public void AlignZeroFrame()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    public bool IsPadLaunchScenario()
    {
        return
            runner != null &&
            runner.Simulation != null &&
            SimulationScenarios.IsAnySurfacePad(runner.Simulation.scenario);
    }
}
