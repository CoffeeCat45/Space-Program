using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public enum ShipOrientationMode
{
    Manual,
    Prograde,
    Retrograde,
    Maneuver
}

public class SimulationRunner : MonoBehaviour
{
    [Header("Simulation")]
    public SimulationScenario scenario = SimulationScenario.MoonFlyby;
    public float fixedDt = 0.02f;
    public float timeScale = 1.0f;
    public float worldScale = 0.05f;

    [Header("Ship Control")]
    public float shipTurnSpeedDegrees = 120.0f;
    public float shipAutoTurnSpeedDegrees = 240.0f;
    public float shipThrustForce = 60.0f;
    public float throttle = 1.0f;
    public float throttleChangeSpeed = 0.75f;

    [Header("Maneuver Planning")]
    public double plannedDeltaVStep = 0.25;
    public int circularizeFiniteBurnIterations = 4;

    [Header("Preview Analysis")]
    public int previewAnalysisStepCount = 1500;
    public double previewAnalysisDt = 0.2;
    public float previewAnalysisUpdateInterval = 1.5f;

    [Header("Warp")]
    public float warpToPointTimeScale = 50.0f;
    public double maneuverWarpLeadTime = 60.0;

    [Header("Target")]
    public string targetBodyName = "Moon";

    Simulation simulation;
    double accumulator;
    float shipRotationDegrees;
    ShipOrientationMode shipOrientationMode = ShipOrientationMode.Manual;
    global::Vector2 plannedDeltaV;
    global::Vector2 maneuverNodeDeltaV;
    global::Vector2 maneuverNodePosition;
    global::Vector2 cachedPreviewPlannedDeltaV;
    global::Vector2 cachedPreviewManeuverNodeDeltaV;
    ClosestApproachInfo cachedClosestApproach;
    SphereOfInfluenceEntryPreview cachedSphereOfInfluenceEntryPreview;
    bool isPaused;
    bool isInitialized;
    bool hasCachedPreviewAnalysis;
    string lastActivePrimaryBodyName;
    string cachedPreviewTargetBodyName;
    string warpLabel = "point";
    bool hasManeuverNode;
    bool cachedPreviewHasManeuverNode;
    bool autoBurnActive;
    bool autoBurnIsBurning;
    float previewAnalysisTimer;
    double previewAnalysisSimTimeSinceCache;
    double warpRemainingTime;
    double maneuverNodeTime;
    double cachedPreviewManeuverNodeTime;
    double autoBurnDuration;
    double autoBurnRemainingTime;
    float[] timeScaleSteps = new float[] {
        0.25f,
        0.5f,
        1.0f,
        5.0f,
        10.0f,
        50.0f,
        100.0f
    };
    string[] targetBodyNames = new string[] { "Moon", "Planet" };
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
        EnsureValidTarget();
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
        UpdateTargetInput();

        double scaledDeltaTime =
            Time.deltaTime * Mathf.Max(0.0f, GetEffectiveTimeScale());

        if (IsWarping())
        {
            scaledDeltaTime = System.Math.Min(
                scaledDeltaTime,
                warpRemainingTime
            );
        }

        accumulator += scaledDeltaTime;

        while (accumulator >= fixedDt || (IsWarping() && accumulator > 0.0))
        {
            double stepDt = fixedDt;

            if (IsWarping())
            {
                stepDt = System.Math.Min(
                    stepDt,
                    System.Math.Min(accumulator, warpRemainingTime)
                );
            }

            if (
                autoBurnActive &&
                !autoBurnIsBurning &&
                hasManeuverNode &&
                GetTimeToManeuverBurnStart() > 0.0 &&
                GetTimeToManeuverBurnStart() < stepDt
            )
            {
                stepDt = GetTimeToManeuverBurnStart();
            }

            if (stepDt <= 0.0)
            {
                break;
            }

            UpdateAutoBurnThrustBeforeStep();
            simulation.Update(stepDt);
            previewAnalysisSimTimeSinceCache += stepDt;
            UpdateAutoBurnAfterStep(stepDt);
            UpdateManeuverNodeTime(stepDt);
            UpdateAutomaticTimeSlowdown();
            accumulator -= stepDt;

            if (IsWarping())
            {
                warpRemainingTime -= stepDt;

                if (warpRemainingTime <= 0.000001)
                {
                    warpRemainingTime = 0.0;
                    accumulator = 0.0;
                    RebuildPreviewAnalysis();
                    break;
                }
            }
        }

        UpdatePreviewAnalysis();
        RenderBodies();
    }

    internal global::Vector2 GetPlannedDeltaV()
    {
        return GetActivePlannedDeltaV();
    }

    internal float GetEffectiveTimeScale()
    {
        if (isPaused)
        {
            return 0.0f;
        }

        if (IsWarping())
        {
            return warpToPointTimeScale;
        }

        return timeScale;
    }

    internal bool IsPaused()
    {
        return isPaused;
    }

    internal bool IsWarping()
    {
        return warpRemainingTime > 0.0;
    }

    internal string GetWarpLabel()
    {
        return warpLabel;
    }

    internal string GetTargetBodyName()
    {
        EnsureValidTarget();
        return targetBodyName;
    }

    internal double GetTargetDistance()
    {
        EnsureValidTarget();
        return simulation.GetShipDistanceToBody(targetBodyName);
    }

    internal double GetTargetRelativeSpeed()
    {
        EnsureValidTarget();
        return simulation.GetShipRelativeSpeedToBody(targetBodyName);
    }

    internal string GetShipOrientationModeName()
    {
        return shipOrientationMode.ToString();
    }

    internal float GetThrottle()
    {
        return Mathf.Clamp01(throttle);
    }

    internal double GetTimeToPeriapsis()
    {
        return simulation.GetShipTimeToPeriapsisRelativeToActivePrimary();
    }

    internal double GetTimeToApoapsis()
    {
        return simulation.GetShipTimeToApoapsisRelativeToActivePrimary();
    }

    internal bool HasManeuverNode()
    {
        return hasManeuverNode;
    }

    internal double GetManeuverNodeTime()
    {
        return maneuverNodeTime;
    }

    internal global::Vector2 GetManeuverNodeDeltaV()
    {
        return maneuverNodeDeltaV;
    }

    internal global::Vector2 GetManeuverNodePosition()
    {
        return maneuverNodePosition;
    }

    internal bool IsAutoBurnActive()
    {
        return autoBurnActive;
    }

    internal bool IsAutoBurning()
    {
        return autoBurnIsBurning;
    }

    internal double GetEstimatedManeuverBurnDuration()
    {
        if (!hasManeuverNode || maneuverNodeDeltaV.LengthSquared() <= 0.0)
        {
            return 0.0;
        }

        return GetEstimatedBurnDuration(maneuverNodeDeltaV);
    }

    internal double GetPlannedBurnDuration()
    {
        return GetActiveBurnDuration();
    }

    internal double GetPlannedBurnStartTime()
    {
        return GetActiveBurnStartTime();
    }

    internal double GetPlannedManeuverTime()
    {
        return GetActiveManeuverTime();
    }

    double GetEstimatedBurnDuration(global::Vector2 deltaV)
    {
        return simulation.EstimateShipBurnDurationForDeltaV(
            deltaV,
            shipThrustForce
        );
    }

    internal double GetTimeToManeuverBurnStart()
    {
        if (!hasManeuverNode)
        {
            return double.PositiveInfinity;
        }

        return maneuverNodeTime;
    }

    internal global::Vector2 GetManeuverNodeProgradeDirection()
    {
        if (!hasManeuverNode)
        {
            return global::Vector2.Zero();
        }

        return simulation.GetShipProgradeDirectionRelativeToActivePrimaryAtTime(
            maneuverNodeTime,
            previewAnalysisDt
        );
    }

    internal global::Vector2 GetManeuverNodeRetrogradeDirection()
    {
        return GetManeuverNodeProgradeDirection().Multiply(-1);
    }

    internal global::Vector2 GetManeuverNodeRadialOutDirection()
    {
        if (!hasManeuverNode)
        {
            return global::Vector2.Zero();
        }

        return simulation.GetShipRadialOutDirectionRelativeToActivePrimaryAtTime(
            maneuverNodeTime,
            previewAnalysisDt
        );
    }

    internal global::Vector2 GetManeuverNodeRadialInDirection()
    {
        return GetManeuverNodeRadialOutDirection().Multiply(-1);
    }

    internal void SetManeuverNode(double time)
    {
        if (double.IsInfinity(time) || double.IsNaN(time) || time <= fixedDt)
        {
            return;
        }

        maneuverNodeTime = time;
        maneuverNodePosition =
            simulation.GetShipPositionAtTime(time, previewAnalysisDt);
        plannedDeltaV = global::Vector2.Zero();
        maneuverNodeDeltaV = global::Vector2.Zero();
        hasManeuverNode = true;
        RebuildPreviewAnalysis();
    }

    internal void SetManeuverNode(double time, global::Vector2 position)
    {
        SetManeuverNode(time);

        if (hasManeuverNode)
        {
            maneuverNodePosition = position;
        }
    }

    internal void MoveManeuverNode(double time)
    {
        if (double.IsInfinity(time) || double.IsNaN(time) || time <= fixedDt)
        {
            return;
        }

        if (!hasManeuverNode)
        {
            SetManeuverNode(time);
            return;
        }

        maneuverNodeTime = time;
        maneuverNodePosition =
            simulation.GetShipPositionAtTime(time, previewAnalysisDt);
        RebuildPreviewAnalysis();
    }

    internal void MoveManeuverNode(double time, global::Vector2 position)
    {
        MoveManeuverNode(time);

        if (hasManeuverNode)
        {
            maneuverNodePosition = position;
        }
    }

    internal void SetManeuverNodeAtPeriapsis()
    {
        SetManeuverNode(GetTimeToPeriapsis());
    }

    internal void SetManeuverNodeAtApoapsis()
    {
        SetManeuverNode(GetTimeToApoapsis());
    }

    internal void WarpToManeuverStart()
    {
        if (!hasManeuverNode || maneuverNodeTime <= fixedDt)
        {
            return;
        }

        double burnStartTime = GetTimeToManeuverBurnStart();

        if (double.IsInfinity(burnStartTime))
        {
            return;
        }

        double warpTime = burnStartTime - maneuverWarpLeadTime;

        if (warpTime <= fixedDt)
        {
            return;
        }

        warpRemainingTime = warpTime;
        warpLabel = "maneuver start";
        isPaused = false;
    }

    internal void StartAutoBurn()
    {
        if (!hasManeuverNode && plannedDeltaV.LengthSquared() > 0.0)
        {
            PromotePlannedDeltaVToImmediateManeuverNode();
        }

        if (!hasManeuverNode || maneuverNodeDeltaV.LengthSquared() <= 0.0)
        {
            return;
        }

        autoBurnDuration = GetEstimatedManeuverBurnDuration();

        if (double.IsInfinity(autoBurnDuration) || autoBurnDuration <= 0.0)
        {
            return;
        }

        autoBurnRemainingTime = autoBurnDuration;
        autoBurnActive = true;
        autoBurnIsBurning = false;
        throttle = 1.0f;
        shipOrientationMode = ShipOrientationMode.Maneuver;
        isPaused = false;
    }

    void PromotePlannedDeltaVToImmediateManeuverNode()
    {
        maneuverNodeDeltaV = plannedDeltaV;
        maneuverNodePosition = simulation.ship.position;
        maneuverNodeTime = 0.0;
        plannedDeltaV = global::Vector2.Zero();
        hasManeuverNode = true;
    }

    internal void ClearManeuverNode()
    {
        ClearManeuverPlan();
    }

    internal global::Vector2[] GetPlannedOrbitPath(int pointCount)
    {
        if (hasManeuverNode)
        {
            return simulation.GetFiniteBurnPreviewShipOrbitPathRelativeToActivePrimary(
                maneuverNodeDeltaV,
                shipThrustForce,
                GetEstimatedManeuverBurnDuration(),
                pointCount,
                GetActiveBurnStartTime(),
                previewAnalysisDt,
                fixedDt
            );
        }

        return simulation.GetFiniteBurnPreviewShipOrbitPathRelativeToActivePrimary(
            plannedDeltaV,
            shipThrustForce,
            GetEstimatedBurnDuration(plannedDeltaV),
            pointCount,
            0.0,
            previewAnalysisDt,
            fixedDt
        );
    }

    internal double GetPlannedOrbitPeriod()
    {
        return simulation.GetFiniteBurnPreviewShipOrbitalPeriodRelativeToActivePrimary(
            GetActivePlannedDeltaV(),
            shipThrustForce,
            GetActiveBurnDuration(),
            GetActiveBurnStartTime(),
            previewAnalysisDt,
            fixedDt
        );
    }

    internal void WarpBy(double seconds)
    {
        if (simulation == null || seconds <= 0)
        {
            return;
        }

        warpRemainingTime = seconds;
        warpLabel = "point";
        isPaused = false;
    }

    internal void WarpToClosestApproach()
    {
        ClosestApproachInfo closestApproach = GetCachedClosestApproach();

        if (!closestApproach.hasApproach || closestApproach.time <= fixedDt)
        {
            return;
        }

        warpRemainingTime = closestApproach.time;
        warpLabel = "CA";
        isPaused = false;
    }

    internal void WarpToPeriapsis()
    {
        WarpToOrbitalEvent(GetTimeToPeriapsis(), "Pe");
    }

    internal void WarpToApoapsis()
    {
        WarpToOrbitalEvent(GetTimeToApoapsis(), "Ap");
    }

    void WarpToOrbitalEvent(double eventTime, string label)
    {
        if (double.IsInfinity(eventTime) || eventTime <= fixedDt)
        {
            return;
        }

        warpRemainingTime = eventTime;
        warpLabel = label;
        isPaused = false;
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
        cachedPreviewManeuverNodeDeltaV = maneuverNodeDeltaV;
        cachedPreviewHasManeuverNode = hasManeuverNode;
        cachedPreviewManeuverNodeTime = maneuverNodeTime;
        cachedPreviewTargetBodyName = targetBodyName;
        cachedClosestApproach = simulation.GetClosestApproachToBody(
            targetBodyName,
            GetActivePlannedDeltaV(),
            previewAnalysisStepCount,
            previewAnalysisDt,
            GetActiveBurnStartTime(),
            shipThrustForce,
            GetActiveBurnDuration(),
            fixedDt
        );
        cachedSphereOfInfluenceEntryPreview =
            simulation.GetMoonSphereOfInfluenceEntryPreview(
                GetActivePlannedDeltaV(),
                previewAnalysisStepCount,
                previewAnalysisDt,
                GetActiveBurnStartTime(),
                shipThrustForce,
                GetActiveBurnDuration(),
                fixedDt
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
            .LengthSquared() > 0.000001 ||
            maneuverNodeDeltaV
                .Subtract(cachedPreviewManeuverNodeDeltaV)
                .LengthSquared() > 0.000001 ||
            cachedPreviewHasManeuverNode != hasManeuverNode ||
            cachedPreviewTargetBodyName != targetBodyName;
    }

    global::Vector2 GetActivePlannedDeltaV()
    {
        return hasManeuverNode ? maneuverNodeDeltaV : plannedDeltaV;
    }

    double GetActiveManeuverTime()
    {
        return hasManeuverNode ? maneuverNodeTime : 0.0;
    }

    double GetActiveBurnStartTime()
    {
        return hasManeuverNode ? GetTimeToManeuverBurnStart() : 0.0;
    }

    double GetActiveBurnDuration()
    {
        global::Vector2 activePlannedDeltaV = GetActivePlannedDeltaV();

        if (activePlannedDeltaV.LengthSquared() <= 0.0)
        {
            return 0.0;
        }

        return GetEstimatedBurnDuration(activePlannedDeltaV);
    }

    void UpdateManeuverNodeTime(double dt)
    {
        if (!hasManeuverNode)
        {
            return;
        }

        maneuverNodeTime -= dt;

        if (maneuverNodeTime < 0.0)
        {
            maneuverNodeTime = 0.0;
        }
    }

    void UpdateShipInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            simulation.SetShipThrust(global::Vector2.Zero());
            return;
        }

        UpdateOrientationModeInput(keyboard);
        UpdateThrottleInput(keyboard);

        float turnInput = 0.0f;

        if (keyboard.aKey != null && keyboard.aKey.isPressed)
        {
            turnInput += 1.0f;
        }

        if (keyboard.dKey != null && keyboard.dKey.isPressed)
        {
            turnInput -= 1.0f;
        }

        if (turnInput != 0.0f)
        {
            shipOrientationMode = ShipOrientationMode.Manual;
            shipRotationDegrees += turnInput * shipTurnSpeedDegrees * Time.deltaTime;
        }
        else
        {
            UpdateAutoOrientation();
        }

        float shipRotationRadians = shipRotationDegrees * Mathf.Deg2Rad;
        global::Vector2 shipForward = new global::Vector2(
            Mathf.Cos(shipRotationRadians),
            Mathf.Sin(shipRotationRadians)
        );

        if (keyboard.wKey != null && keyboard.wKey.isPressed)
        {
            simulation.SetShipThrust(
                shipForward.Multiply(GetCurrentThrustForce())
            );
        }
        else
        {
            simulation.SetShipThrust(global::Vector2.Zero());
        }
    }

    void UpdateThrottleInput(Keyboard keyboard)
    {
        if (keyboard.zKey != null && keyboard.zKey.wasPressedThisFrame)
        {
            throttle = 1.0f;
        }

        if (keyboard.xKey != null && keyboard.xKey.wasPressedThisFrame)
        {
            throttle = 0.0f;
        }

        float throttleInput = 0.0f;

        if (
            (keyboard.leftShiftKey != null && keyboard.leftShiftKey.isPressed) ||
            (keyboard.rightShiftKey != null && keyboard.rightShiftKey.isPressed)
        )
        {
            throttleInput += 1.0f;
        }

        if (
            (keyboard.leftCtrlKey != null && keyboard.leftCtrlKey.isPressed) ||
            (keyboard.rightCtrlKey != null && keyboard.rightCtrlKey.isPressed)
        )
        {
            throttleInput -= 1.0f;
        }

        if (throttleInput != 0.0f)
        {
            throttle += throttleInput * throttleChangeSpeed * Time.unscaledDeltaTime;
            throttle = Mathf.Clamp01(throttle);
        }
    }

    float GetCurrentThrustForce()
    {
        return shipThrustForce * GetThrottle();
    }

    void UpdateOrientationModeInput(Keyboard keyboard)
    {
        if (WasPressed(keyboard.digit1Key, keyboard.numpad1Key))
        {
            shipOrientationMode = ShipOrientationMode.Manual;
        }

        if (WasPressed(keyboard.digit2Key, keyboard.numpad2Key))
        {
            shipOrientationMode = ShipOrientationMode.Prograde;
        }

        if (WasPressed(keyboard.digit3Key, keyboard.numpad3Key))
        {
            shipOrientationMode = ShipOrientationMode.Retrograde;
        }

        if (WasPressed(keyboard.digit4Key, keyboard.numpad4Key))
        {
            shipOrientationMode = ShipOrientationMode.Maneuver;
        }
    }

    bool WasPressed(KeyControl primaryKey, KeyControl secondaryKey)
    {
        return
            (primaryKey != null && primaryKey.wasPressedThisFrame) ||
            (secondaryKey != null && secondaryKey.wasPressedThisFrame);
    }

    void UpdateAutoOrientation()
    {
        global::Vector2 targetDirection = GetAutoOrientationDirection();

        if (targetDirection.LengthSquared() <= 0.000001)
        {
            return;
        }

        float targetAngleDegrees = Mathf.Atan2(
            (float)targetDirection.y,
            (float)targetDirection.x
        ) * Mathf.Rad2Deg;
        shipRotationDegrees = Mathf.MoveTowardsAngle(
            shipRotationDegrees,
            targetAngleDegrees,
            shipAutoTurnSpeedDegrees * Time.deltaTime
        );
    }

    global::Vector2 GetAutoOrientationDirection()
    {
        if (shipOrientationMode == ShipOrientationMode.Prograde)
        {
            return simulation.GetShipProgradeDirectionRelativeToActivePrimary();
        }

        if (shipOrientationMode == ShipOrientationMode.Retrograde)
        {
            return simulation.GetShipRetrogradeDirectionRelativeToActivePrimary();
        }

        if (shipOrientationMode == ShipOrientationMode.Maneuver)
        {
            global::Vector2 maneuverDirection = GetActivePlannedDeltaV();

            if (maneuverDirection.LengthSquared() > 0.000001)
            {
                return maneuverDirection.Normalize();
            }
        }

        return global::Vector2.Zero();
    }

    void UpdateManeuverInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        double maneuverStep = GetManeuverStep();

        if (keyboard.upArrowKey != null && keyboard.upArrowKey.wasPressedThisFrame)
        {
            AddManeuverDeltaV(
                GetPlanningProgradeDirection().Multiply(maneuverStep)
            );
        }

        if (keyboard.downArrowKey != null && keyboard.downArrowKey.wasPressedThisFrame)
        {
            AddManeuverDeltaV(
                GetPlanningRetrogradeDirection().Multiply(maneuverStep)
            );
        }

        if (keyboard.rightArrowKey != null && keyboard.rightArrowKey.wasPressedThisFrame)
        {
            AddManeuverDeltaV(
                GetPlanningRadialOutDirection().Multiply(maneuverStep)
            );
        }

        if (keyboard.leftArrowKey != null && keyboard.leftArrowKey.wasPressedThisFrame)
        {
            AddManeuverDeltaV(
                GetPlanningRadialInDirection().Multiply(maneuverStep)
            );
        }

        if (keyboard.cKey != null && keyboard.cKey.wasPressedThisFrame)
        {
            SetManeuverDeltaV(GetPlanningCircularizeDeltaV());
        }

        if (keyboard.bKey != null && keyboard.bKey.wasPressedThisFrame)
        {
            StartAutoBurn();
        }

        if (keyboard.enterKey != null && keyboard.enterKey.wasPressedThisFrame)
        {
            ApplyOrWarpManeuver();
        }

        if (keyboard.backspaceKey != null && keyboard.backspaceKey.wasPressedThisFrame)
        {
            ClearManeuverPlan();
        }
    }

    void AddManeuverDeltaV(global::Vector2 deltaV)
    {
        if (hasManeuverNode)
        {
            maneuverNodeDeltaV = maneuverNodeDeltaV.Add(deltaV);
            return;
        }

        plannedDeltaV = plannedDeltaV.Add(deltaV);
    }

    void SetManeuverDeltaV(global::Vector2 deltaV)
    {
        if (hasManeuverNode)
        {
            maneuverNodeDeltaV = deltaV;
            return;
        }

        plannedDeltaV = deltaV;
    }

    global::Vector2 GetPlanningProgradeDirection()
    {
        if (hasManeuverNode)
        {
            return simulation.GetShipProgradeDirectionRelativeToActivePrimaryAtTime(
                maneuverNodeTime,
                previewAnalysisDt
            );
        }

        return simulation.GetShipProgradeDirectionRelativeToActivePrimary();
    }

    global::Vector2 GetPlanningRetrogradeDirection()
    {
        if (hasManeuverNode)
        {
            return simulation.GetShipRetrogradeDirectionRelativeToActivePrimaryAtTime(
                maneuverNodeTime,
                previewAnalysisDt
            );
        }

        return simulation.GetShipRetrogradeDirectionRelativeToActivePrimary();
    }

    global::Vector2 GetPlanningRadialOutDirection()
    {
        if (hasManeuverNode)
        {
            return simulation.GetShipRadialOutDirectionRelativeToActivePrimaryAtTime(
                maneuverNodeTime,
                previewAnalysisDt
            );
        }

        return simulation.GetShipRadialOutDirectionRelativeToActivePrimary();
    }

    global::Vector2 GetPlanningRadialInDirection()
    {
        if (hasManeuverNode)
        {
            return simulation.GetShipRadialInDirectionRelativeToActivePrimaryAtTime(
                maneuverNodeTime,
                previewAnalysisDt
            );
        }

        return simulation.GetShipRadialInDirectionRelativeToActivePrimary();
    }

    global::Vector2 GetPlanningCircularizeDeltaV()
    {
        return simulation.GetFiniteBurnCircularizeDeltaVRelativeToActivePrimary(
            shipThrustForce,
            GetActiveManeuverTime(),
            GetActiveBurnStartTime(),
            previewAnalysisDt,
            fixedDt,
            circularizeFiniteBurnIterations
        );
    }

    void ApplyOrWarpManeuver()
    {
        if (hasManeuverNode)
        {
            if (GetTimeToManeuverBurnStart() > fixedDt * 2.0)
            {
                return;
            }

            StartAutoBurn();
            return;
        }

        StartAutoBurn();
    }

    void ClearManeuverPlan()
    {
        plannedDeltaV = global::Vector2.Zero();
        maneuverNodeDeltaV = global::Vector2.Zero();
        maneuverNodePosition = global::Vector2.Zero();
        maneuverNodeTime = 0.0;
        hasManeuverNode = false;
        autoBurnActive = false;
        autoBurnIsBurning = false;
        autoBurnDuration = 0.0;
        autoBurnRemainingTime = 0.0;
        simulation.SetShipThrust(global::Vector2.Zero());
    }

    void UpdateAutoBurnThrustBeforeStep()
    {
        if (!autoBurnActive)
        {
            return;
        }

        if (!hasManeuverNode || maneuverNodeDeltaV.LengthSquared() <= 0.0)
        {
            ClearManeuverPlan();
            return;
        }

        shipOrientationMode = ShipOrientationMode.Maneuver;

        if (
            autoBurnIsBurning ||
            GetTimeToManeuverBurnStart() <= fixedDt
        )
        {
            autoBurnIsBurning = true;
            float currentThrustForce = GetCurrentThrustForce();

            if (currentThrustForce <= 0.000001f)
            {
                simulation.SetShipThrust(global::Vector2.Zero());
                return;
            }

            simulation.SetShipThrust(
                maneuverNodeDeltaV.Normalize().Multiply(currentThrustForce)
            );
            return;
        }

        simulation.SetShipThrust(global::Vector2.Zero());
    }

    void UpdateAutoBurnAfterStep(double dt)
    {
        if (!autoBurnActive || !autoBurnIsBurning)
        {
            return;
        }

        autoBurnRemainingTime -= dt * GetThrottle();

        if (autoBurnRemainingTime > 0.0)
        {
            return;
        }

        simulation.SetShipThrust(global::Vector2.Zero());
        ClearManeuverPlan();
        RebuildPreviewAnalysis();
    }

    double GetManeuverStep()
    {
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

    void UpdateTargetInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.tabKey != null && keyboard.tabKey.wasPressedThisFrame)
        {
            CycleTarget();
        }

        if (keyboard.vKey != null && keyboard.vKey.wasPressedThisFrame)
        {
            WarpToClosestApproach();
        }

        if (keyboard.jKey != null && keyboard.jKey.wasPressedThisFrame)
        {
            WarpToPeriapsis();
        }

        if (keyboard.kKey != null && keyboard.kKey.wasPressedThisFrame)
        {
            WarpToApoapsis();
        }

        if (keyboard.nKey != null && keyboard.nKey.wasPressedThisFrame)
        {
            SetManeuverNodeAtApoapsis();
        }
    }

    void CycleTarget()
    {
        if (simulation == null || targetBodyNames.Length == 0)
        {
            return;
        }

        int startIndex = FindTargetIndex();

        for (int offset = 1; offset <= targetBodyNames.Length; offset++)
        {
            int nextIndex = (startIndex + offset) % targetBodyNames.Length;
            string nextTargetBodyName = targetBodyNames[nextIndex];

            if (simulation.HasBody(nextTargetBodyName))
            {
                targetBodyName = nextTargetBodyName;
                RebuildPreviewAnalysis();
                return;
            }
        }
    }

    int FindTargetIndex()
    {
        for (int i = 0; i < targetBodyNames.Length; i++)
        {
            if (targetBodyNames[i] == targetBodyName)
            {
                return i;
            }
        }

        return -1;
    }

    void EnsureValidTarget()
    {
        if (simulation == null || simulation.HasBody(targetBodyName))
        {
            return;
        }

        for (int i = 0; i < targetBodyNames.Length; i++)
        {
            if (simulation.HasBody(targetBodyNames[i]))
            {
                targetBodyName = targetBodyNames[i];
                return;
            }
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
                    GetActivePlannedDeltaV()
                );
                BodyView.UpdateEngineExhaust(
                    bodyObjects[bodyState.name],
                    GetThrottle(),
                    simulation.GetShipThrustAcceleration() > 0.000001
                );
            }
        }
    }

    void ClearShipTrail()
    {
        if (!bodyObjects.ContainsKey("Ship"))
        {
            return;
        }

        ShipTrailView shipTrailView =
            bodyObjects["Ship"].GetComponent<ShipTrailView>();

        if (shipTrailView != null)
        {
            shipTrailView.Clear();
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

        StarfieldBackground starfieldBackground =
            Camera.main.GetComponent<StarfieldBackground>();

        if (starfieldBackground == null)
        {
            starfieldBackground =
                Camera.main.gameObject.AddComponent<StarfieldBackground>();
        }

        starfieldBackground.Setup(Camera.main);

        SunVisual sunVisual = Camera.main.GetComponent<SunVisual>();

        if (sunVisual == null)
        {
            sunVisual = Camera.main.gameObject.AddComponent<SunVisual>();
        }

        sunVisual.Setup(Camera.main);
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
