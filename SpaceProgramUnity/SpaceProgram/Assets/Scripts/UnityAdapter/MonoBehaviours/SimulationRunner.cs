using System.Collections.Generic;
using UnityEngine;

public class SimulationRunner : MonoBehaviour
{
    [Header("Simulation")]
    public SimulationScenario scenario = SimulationScenario.MoonFlyby;
    public float fixedDt = 0.02f;
    public float timeScale = 1.0f;
    public float worldScale = 0.05f;

    Simulation simulation;
    float accumulator;
    Dictionary<string, GameObject> bodyObjects = new Dictionary<string, GameObject>();

    void Start()
    {
        simulation = new Simulation(scenario);
        RenderBodies();
    }

    void Update()
    {
        accumulator += Time.deltaTime * Mathf.Max(0.0f, timeScale);

        while (accumulator >= fixedDt)
        {
            simulation.Update(fixedDt);
            accumulator -= fixedDt;
        }

        RenderBodies();
    }

    void RenderBodies()
    {
        BodyState[] bodyStates = simulation.GetBodyStates();

        for (int i = 0; i < bodyStates.Length; i++)
        {
            BodyState bodyState = bodyStates[i];

            if (!bodyObjects.ContainsKey(bodyState.name))
            {
                bodyObjects[bodyState.name] = BodyView.Create(bodyState.name);
            }

            BodyView.Update(
                bodyObjects[bodyState.name],
                bodyState,
                worldScale
            );
        }
    }
}
