using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SimulationRunner : MonoBehaviour
{
    [Header("Simulation")]
    public SimulationScenario scenario = SimulationScenario.MoonFlyby;
    public float fixedDt = 0.02f;
    public float timeScale = 1.0f;
    public float worldScale = 0.05f;

    [Header("Ship Control")]
    public float shipTurnSpeedDegrees = 120.0f;
    public float shipThrustAcceleration = 2.0f;

    [Header("Maneuver Planning")]
    public double plannedDeltaVStep = 0.25;
    public double fineManeuverStepMultiplier = 0.2;
    public double coarseManeuverStepMultiplier = 5.0;

    [Header("Preview Analysis")]
    public int previewAnalysisStepCount = 1500;
    public double previewAnalysisDt = 0.2;
    public float previewAnalysisUpdateInterval = 0.5f;

    Simulation simulation;
    float accumulator;
    float shipRotationDegrees;
    global::Vector2 plannedDeltaV;
    global::Vector2 cachedPreviewPlannedDeltaV;
    ClosestApproachInfo cachedClosestApproach;
    SphereOfInfluenceEntryPreview cachedSphereOfInfluenceEntryPreview;
    bool isPaused;
    bool isInitialized;
    bool hasCachedPreviewAnalysis;
    string lastActivePrimaryBodyName;
    float previewAnalysisTimer;
    double previewAnalysisSimTimeSinceCache;
    float[] timeScaleSteps = new float[] { 1.0f, 5.0f, 10.0f, 50.0f, 100.0f };
    Dictionary<string, GameObject> bodyObjects = new Dictionary<string, GameObject>();

    void Start()
    {
        InitializeSimulation();
    }

    void InitializeSimulation()
    {
        simulation = new Simulation(scenario);
        RenderBodies();
        SetupCamera();
        SetupHud();
        SetupOrbitPreview();
        SetupSphereOfInfluenceView();
        lastActivePrimaryBodyName = simulation.activePrimaryBodyName;
        isInitialized = true;
        RebuildPreviewAnalysis();
    }

    void Update()
    {
        if (!isInitialized || simulation == null)
        {
            InitializeSimulation();
        }

        UpdateShipInput();
        UpdateManeuverInput();
        UpdateTimeScaleInput();

        accumulator += Time.deltaTime * Mathf.Max(0.0f, GetEffectiveTimeScale());

        while (accumulator >= fixedDt)
        {
            simulation.Update(fixedDt);
            previewAnalysisSimTimeSinceCache += fixedDt;
            UpdateAutomaticTimeSlowdown();
            accumulator -= fixedDt;
        }

        UpdatePreviewAnalysis();
        RenderBodies();
    }

    internal global::Vector2 GetPlannedDeltaV()
    {
        return plannedDeltaV;
    }

    internal float GetEffectiveTimeScale()
    {
        return isPaused ? 0.0f : timeScale;
    }

    internal bool IsPaused()
    {
        return isPaused;
    }

    internal double GetFineManeuverStep()
    {
        return plannedDeltaVStep * fineManeuverStepMultiplier;
    }

    internal double GetCoarseManeuverStep()
    {
        return plannedDeltaVStep * coarseManeuverStepMultiplier;
    }

    internal ClosestApproachInfo GetCachedClosestApproach()
    {
        EnsurePreviewAnalysis();
        ClosestApproachInfo closestApproach = cachedClosestApproach;
        closestApproach.time = SubtractPreviewAnalysisElapsedTime(
            closestApproach.time
        );
        return closestApproach;
    }

    internal SphereOfInfluenceEntryPreview GetCachedSphereOfInfluenceEntryPreview()
    {
        EnsurePreviewAnalysis();
        SphereOfInfluenceEntryPreview preview =
            cachedSphereOfInfluenceEntryPreview;
        preview.entryTime = SubtractPreviewAnalysisElapsedTime(
            preview.entryTime
        );
        return preview;
    }

    void UpdatePreviewAnalysis()
    {
        previewAnalysisTimer += Time.deltaTime;

        if (
            !hasCachedPreviewAnalysis ||
            PlannedDeltaVChanged() ||
            previewAnalysisTimer >= previewAnalysisUpdateInterval
        )
        {
            RebuildPreviewAnalysis();
        }
    }

    void EnsurePreviewAnalysis()
    {
        if (!hasCachedPreviewAnalysis)
        {
            RebuildPreviewAnalysis();
        }
    }

    void RebuildPreviewAnalysis()
    {
        if (simulation == null)
        {
            return;
        }

        cachedPreviewPlannedDeltaV = plannedDeltaV;
        cachedClosestApproach = simulation.GetClosestApproachToMoon(
            plannedDeltaV,
            previewAnalysisStepCount,
            previewAnalysisDt
        );
        cachedSphereOfInfluenceEntryPreview =
            simulation.GetMoonSphereOfInfluenceEntryPreview(
                plannedDeltaV,
                previewAnalysisStepCount,
                previewAnalysisDt
        );
        previewAnalysisTimer = 0.0f;
        previewAnalysisSimTimeSinceCache = 0.0;
        hasCachedPreviewAnalysis = true;
    }

    double SubtractPreviewAnalysisElapsedTime(double cachedTime)
    {
        double remainingTime = cachedTime - previewAnalysisSimTimeSinceCache;

        if (remainingTime < 0.0)
        {
            return 0.0;
        }

        return remainingTime;
    }

    bool PlannedDeltaVChanged()
    {
        return plannedDeltaV
            .Subtract(cachedPreviewPlannedDeltaV)
            .LengthSquared() > 0.000001;
    }

    void UpdateShipInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            simulation.SetShipThrust(global::Vector2.Zero());
            return;
        }

        float turnInput = 0.0f;

        if (keyboard.aKey != null && keyboard.aKey.isPressed)
        {
            turnInput += 1.0f;
        }

        if (keyboard.dKey != null && keyboard.dKey.isPressed)
        {
            turnInput -= 1.0f;
        }

        shipRotationDegrees += turnInput * shipTurnSpeedDegrees * Time.deltaTime;

        float shipRotationRadians = shipRotationDegrees * Mathf.Deg2Rad;
        global::Vector2 shipForward = new global::Vector2(
            Mathf.Cos(shipRotationRadians),
            Mathf.Sin(shipRotationRadians)
        );

        if (keyboard.wKey != null && keyboard.wKey.isPressed)
        {
            simulation.SetShipThrust(shipForward.Multiply(shipThrustAcceleration));
        }
        else
        {
            simulation.SetShipThrust(global::Vector2.Zero());
        }
    }

    void UpdateManeuverInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        double maneuverStep = GetManeuverStep(keyboard);

        if (keyboard.upArrowKey != null && keyboard.upArrowKey.wasPressedThisFrame)
        {
            plannedDeltaV = plannedDeltaV.Add(
                simulation
                    .GetShipProgradeDirectionRelativeToActivePrimary()
                    .Multiply(maneuverStep)
            );
        }

        if (keyboard.downArrowKey != null && keyboard.downArrowKey.wasPressedThisFrame)
        {
            plannedDeltaV = plannedDeltaV.Add(
                simulation
                    .GetShipRetrogradeDirectionRelativeToActivePrimary()
                    .Multiply(maneuverStep)
            );
        }

        if (keyboard.rightArrowKey != null && keyboard.rightArrowKey.wasPressedThisFrame)
        {
            plannedDeltaV = plannedDeltaV.Add(
                simulation
                    .GetShipRadialOutDirectionRelativeToActivePrimary()
                    .Multiply(maneuverStep)
            );
        }

        if (keyboard.leftArrowKey != null && keyboard.leftArrowKey.wasPressedThisFrame)
        {
            plannedDeltaV = plannedDeltaV.Add(
                simulation
                    .GetShipRadialInDirectionRelativeToActivePrimary()
                    .Multiply(maneuverStep)
            );
        }

        if (keyboard.enterKey != null && keyboard.enterKey.wasPressedThisFrame)
        {
            simulation.ApplyShipDeltaV(plannedDeltaV);
            plannedDeltaV = global::Vector2.Zero();
        }

        if (keyboard.backspaceKey != null && keyboard.backspaceKey.wasPressedThisFrame)
        {
            plannedDeltaV = global::Vector2.Zero();
        }
    }

    double GetManeuverStep(Keyboard keyboard)
    {
        bool fineStep =
            (keyboard.leftCtrlKey != null && keyboard.leftCtrlKey.isPressed) ||
            (keyboard.rightCtrlKey != null && keyboard.rightCtrlKey.isPressed);
        bool coarseStep =
            (keyboard.leftShiftKey != null && keyboard.leftShiftKey.isPressed) ||
            (keyboard.rightShiftKey != null && keyboard.rightShiftKey.isPressed);

        if (fineStep)
        {
            return GetFineManeuverStep();
        }

        if (coarseStep)
        {
            return GetCoarseManeuverStep();
        }

        return plannedDeltaVStep;
    }

    void UpdateTimeScaleInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        bool pauseKeyPressed =
            (keyboard.pKey != null && keyboard.pKey.wasPressedThisFrame) ||
            (keyboard.spaceKey != null && keyboard.spaceKey.wasPressedThisFrame);

        if (pauseKeyPressed)
        {
            isPaused = !isPaused;
        }

        if (keyboard.periodKey != null && keyboard.periodKey.wasPressedThisFrame)
        {
            IncreaseTimeScale();
        }

        if (keyboard.commaKey != null && keyboard.commaKey.wasPressedThisFrame)
        {
            DecreaseTimeScale();
        }
    }

    void UpdateAutomaticTimeSlowdown()
    {
        if (simulation.activePrimaryBodyName == lastActivePrimaryBodyName)
        {
            return;
        }

        lastActivePrimaryBodyName = simulation.activePrimaryBodyName;
        timeScale = 1.0f;
        isPaused = false;
    }

    void IncreaseTimeScale()
    {
        isPaused = false;
        int currentIndex = FindNearestTimeScaleIndex();
        int nextIndex = Mathf.Min(currentIndex + 1, timeScaleSteps.Length - 1);
        timeScale = timeScaleSteps[nextIndex];
    }

    void DecreaseTimeScale()
    {
        isPaused = false;
        int currentIndex = FindNearestTimeScaleIndex();
        int nextIndex = Mathf.Max(currentIndex - 1, 0);
        timeScale = timeScaleSteps[nextIndex];
    }

    int FindNearestTimeScaleIndex()
    {
        int nearestIndex = 0;
        float nearestDistance = Mathf.Abs(timeScale - timeScaleSteps[0]);

        for (int i = 1; i < timeScaleSteps.Length; i++)
        {
            float distance = Mathf.Abs(timeScale - timeScaleSteps[i]);

            if (distance < nearestDistance)
            {
                nearestIndex = i;
                nearestDistance = distance;
            }
        }

        return nearestIndex;
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

            if (bodyState.name == "Ship")
            {
                bodyObjects[bodyState.name].transform.rotation =
                    Quaternion.Euler(0, 0, shipRotationDegrees - 90.0f);
                BodyView.UpdateThrustDirection(
                    bodyObjects[bodyState.name],
                    shipRotationDegrees
                );
                BodyView.UpdatePlannedDeltaV(
                    bodyObjects[bodyState.name],
                    plannedDeltaV
                );
            }
        }
    }

    void SetupCamera()
    {
        if (Camera.main == null)
        {
            return;
        }

        CameraController cameraController =
            Camera.main.GetComponent<CameraController>();

        if (cameraController == null)
        {
            cameraController =
                Camera.main.gameObject.AddComponent<CameraController>();
        }

        cameraController.SetTarget(bodyObjects["Ship"].transform);
    }

    void SetupHud()
    {
        SimulationHud simulationHud = GetComponent<SimulationHud>();

        if (simulationHud == null)
        {
            simulationHud = gameObject.AddComponent<SimulationHud>();
        }

        simulationHud.SetSimulation(simulation);
        simulationHud.SetRunner(this);
    }

    void SetupOrbitPreview()
    {
        OrbitPreviewView orbitPreviewView = GetComponent<OrbitPreviewView>();

        if (orbitPreviewView == null)
        {
            orbitPreviewView = gameObject.AddComponent<OrbitPreviewView>();
        }

        orbitPreviewView.SetSimulation(simulation);
        orbitPreviewView.SetRunner(this);
        orbitPreviewView.SetWorldScale(worldScale);
    }

    void SetupSphereOfInfluenceView()
    {
        SphereOfInfluenceView sphereOfInfluenceView =
            GetComponent<SphereOfInfluenceView>();

        if (sphereOfInfluenceView == null)
        {
            sphereOfInfluenceView =
                gameObject.AddComponent<SphereOfInfluenceView>();
        }

        sphereOfInfluenceView.SetSimulation(simulation);
        sphereOfInfluenceView.SetWorldScale(worldScale);
    }
}
