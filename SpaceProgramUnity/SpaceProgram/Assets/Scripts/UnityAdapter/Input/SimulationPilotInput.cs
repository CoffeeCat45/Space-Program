using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

class SimulationPilotInput
{
    public void Update(SimulationRunner runner)
    {
        UpdateShip(runner);
        UpdateManeuvers(runner);
        UpdateTime(runner);
        UpdateTarget(runner);
        UpdateAtmosphericPreviewToggle(runner);
    }

    void UpdateShip(SimulationRunner runner)
    {
        Keyboard keyboard = Keyboard.current;
        Vessel vessel = runner.GetLocalControlledVessel();

        if (vessel != null)
        {
            vessel.controlState.throttle = runner.GetThrottle();
        }

        if (keyboard == null)
        {
            if (vessel != null)
            {
                vessel.controlState.rcsVector = global::SimVector3.Zero();
                vessel.controlState.pitchInput = 0.0f;
                vessel.controlState.yawInput = 0.0f;
                vessel.controlState.rollInput = 0.0f;
            }

            return;
        }

        UpdateOrientationModeInput(runner, keyboard);
        UpdateThrottleInput(runner, keyboard);
        UpdateRespawnInput(runner, keyboard);

        if (runner.IsMultiplayerMode)
        {
            UpdateMultiplayerControlState(runner, keyboard, vessel);
            return;
        }

        UpdateRcsInput(runner, keyboard, vessel);
        ApplyRotationInputs(runner, keyboard, vessel);
    }

    void ApplyRotationInputs(
        SimulationRunner runner,
        Keyboard keyboard,
        Vessel vessel
    )
    {
        // Core ramp is 🔒: pitchTarget = −pitchInput, pad yaw Z = −yawInput.
        // WASD here is flipped so W = pitch-down toward pad +Z (east/Atlantic)
        // and D = tip south (right from behind). Do not flip Core to "fix" this.
        float pitchInput = ReadAxisInput(
            keyboard.sKey != null && keyboard.sKey.isPressed,
            keyboard.wKey != null && keyboard.wKey.isPressed
        );
        float yawInput = ReadAxisInput(
            keyboard.aKey != null && keyboard.aKey.isPressed,
            keyboard.dKey != null && keyboard.dKey.isPressed
        );
        float rollInput = ReadAxisInput(
            keyboard.eKey != null && keyboard.eKey.isPressed,
            keyboard.qKey != null && keyboard.qKey.isPressed
        );

        if (
            runner.ShipOrientationMode != ShipOrientationMode.StabilityAssist &&
            runner.ShipOrientationMode != ShipOrientationMode.DevStabilityAssist &&
            (
                Math.Abs(pitchInput) > 1e-6f ||
                Math.Abs(yawInput) > 1e-6f ||
                Math.Abs(rollInput) > 1e-6f
            )
        )
        {
            runner.ShipOrientationMode = ShipOrientationMode.Manual;
        }

        if (vessel != null)
        {
            vessel.controlState.throttle = runner.GetThrottle();
            vessel.controlState.pitchInput = pitchInput;
            vessel.controlState.yawInput = yawInput;
            vessel.controlState.rollInput = rollInput;
        }
    }

    void UpdateRcsInput(
        SimulationRunner runner,
        Keyboard keyboard,
        Vessel vessel
    )
    {
        global::SimVector3 rcsVector = ReadLocalRcsVector(keyboard);

        if (vessel != null)
        {
            vessel.controlState.rcsVector = rcsVector;
        }
    }

    void UpdateMultiplayerControlState(
        SimulationRunner runner,
        Keyboard keyboard,
        Vessel vessel
    )
    {
        global::SimVector3 rcsVector = ReadLocalRcsVector(keyboard);
        runner.SetLocalNetworkRcsVector(rcsVector.x, rcsVector.y);
        ApplyRotationInputs(runner, keyboard, vessel);

        if (vessel == null)
        {
            return;
        }

        vessel.controlState.rcsVector = rcsVector;
    }

    void UpdateThrottleInput(SimulationRunner runner, Keyboard keyboard)
    {
        if (keyboard.zKey != null && keyboard.zKey.wasPressedThisFrame)
        {
            runner.Throttle = 1.0f;
        }

        if (keyboard.xKey != null && keyboard.xKey.wasPressedThisFrame)
        {
            runner.Throttle = 0.0f;
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
            runner.Throttle +=
                throttleInput * runner.ThrottleChangeSpeed * Time.unscaledDeltaTime;
            runner.Throttle = Mathf.Clamp01(runner.Throttle);
        }
    }

    void UpdateRespawnInput(SimulationRunner runner, Keyboard keyboard)
    {
        if (
            keyboard.rKey == null ||
            !keyboard.rKey.wasPressedThisFrame ||
            runner.Simulation.GetFlightStatus() != FlightStatus.Crashed
        )
        {
            return;
        }

        runner.RespawnVessel();
    }

    void UpdateOrientationModeInput(SimulationRunner runner, Keyboard keyboard)
    {
        if (WasPressed(keyboard.digit1Key, keyboard.numpad1Key))
        {
            runner.ShipOrientationMode = ShipOrientationMode.Manual;
        }

        if (WasPressed(keyboard.digit2Key, keyboard.numpad2Key))
        {
            runner.ShipOrientationMode = ShipOrientationMode.Prograde;
        }

        if (WasPressed(keyboard.digit3Key, keyboard.numpad3Key))
        {
            runner.ShipOrientationMode = ShipOrientationMode.Retrograde;
        }

        if (WasPressed(keyboard.digit4Key, keyboard.numpad4Key))
        {
            runner.ShipOrientationMode = ShipOrientationMode.Maneuver;
        }

        if (WasPressed(keyboard.digit5Key, keyboard.numpad5Key))
        {
            runner.ShipOrientationMode = ShipOrientationMode.TargetAlignment;
        }

        if (WasPressed(keyboard.digit6Key, keyboard.numpad6Key))
        {
            runner.ShipOrientationMode = ShipOrientationMode.StabilityAssist;
        }

        if (WasPressed(keyboard.digit0Key, keyboard.numpad0Key))
        {
            runner.ShipOrientationMode = ShipOrientationMode.DevStabilityAssist;
        }
    }

    void UpdateAtmosphericPreviewToggle(SimulationRunner runner)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || keyboard.oKey == null)
        {
            return;
        }

        if (keyboard.oKey.wasPressedThisFrame)
        {
            runner.ToggleAtmosphericPreview();
        }
    }

    void UpdateManeuvers(SimulationRunner runner)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        double maneuverStep = runner.GetManeuverStep();

        if (keyboard.upArrowKey != null && keyboard.upArrowKey.wasPressedThisFrame)
        {
            runner.AddManeuverDeltaV(
                runner.GetPlanningProgradeDirection().Multiply(maneuverStep)
            );
        }

        if (keyboard.downArrowKey != null && keyboard.downArrowKey.wasPressedThisFrame)
        {
            runner.AddManeuverDeltaV(
                runner.GetPlanningRetrogradeDirection().Multiply(maneuverStep)
            );
        }

        if (keyboard.rightArrowKey != null && keyboard.rightArrowKey.wasPressedThisFrame)
        {
            runner.AddManeuverDeltaV(
                runner.GetPlanningRadialOutDirection().Multiply(maneuverStep)
            );
        }

        if (keyboard.leftArrowKey != null && keyboard.leftArrowKey.wasPressedThisFrame)
        {
            runner.AddManeuverDeltaV(
                runner.GetPlanningRadialInDirection().Multiply(maneuverStep)
            );
        }

        if (keyboard.cKey != null && keyboard.cKey.wasPressedThisFrame)
        {
            runner.SetManeuverDeltaV(runner.GetPlanningCircularizeDeltaV());
        }

        if (keyboard.bKey != null && keyboard.bKey.wasPressedThisFrame)
        {
            runner.StartAutoBurn();
        }

        if (keyboard.enterKey != null && keyboard.enterKey.wasPressedThisFrame)
        {
            runner.ApplyOrWarpManeuver();
        }

        if (keyboard.backspaceKey != null && keyboard.backspaceKey.wasPressedThisFrame)
        {
            runner.ClearManeuverPlan();
        }
    }

    void UpdateTime(SimulationRunner runner)
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
            if (runner.IsMultiplayerMode && runner.IsLockstepReady)
            {
                runner.ToggleNetworkPauseRequest();
            }
            else if (runner.IsMultiplayerMode)
            {
                runner.IsPaused = !runner.IsPaused;
            }
            else
            {
                runner.IsPaused = !runner.IsPaused;
            }
        }

        if (runner.IsMultiplayerMode && runner.IsLockstepReady)
        {
            return;
        }

        if (keyboard.periodKey != null && keyboard.periodKey.wasPressedThisFrame)
        {
            runner.IncreaseTimeScale();
        }

        if (keyboard.commaKey != null && keyboard.commaKey.wasPressedThisFrame)
        {
            runner.DecreaseTimeScale();
        }

        if (keyboard.rightBracketKey != null && keyboard.rightBracketKey.wasPressedThisFrame)
        {
            runner.IncreaseTimeScale();
        }

        if (keyboard.leftBracketKey != null && keyboard.leftBracketKey.wasPressedThisFrame)
        {
            runner.DecreaseTimeScale();
        }
    }

    void UpdateTarget(SimulationRunner runner)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.tabKey != null && keyboard.tabKey.wasPressedThisFrame)
        {
            runner.CycleTarget();
        }

        if (keyboard.vKey != null && keyboard.vKey.wasPressedThisFrame)
        {
            runner.WarpToClosestApproach();
        }

        if (keyboard.fKey != null && keyboard.fKey.wasPressedThisFrame)
        {
            runner.FocusCameraOnShip();
        }

        if (keyboard.tKey != null && keyboard.tKey.wasPressedThisFrame)
        {
            if (runner.IsMultiplayerMode)
            {
                runner.QueuePendingNetworkStaging();
            }
            else
            {
                runner.TryStage();
            }
        }
    }

    static global::SimVector3 ReadLocalRcsVector(Keyboard keyboard)
    {
        double rcsX = 0.0;
        double rcsY = 0.0;

        if (keyboard.iKey != null && keyboard.iKey.isPressed)
        {
            rcsX += 1.0;
        }

        if (keyboard.kKey != null && keyboard.kKey.isPressed)
        {
            rcsX -= 1.0;
        }

        if (keyboard.jKey != null && keyboard.jKey.isPressed)
        {
            rcsY -= 1.0;
        }

        if (keyboard.lKey != null && keyboard.lKey.isPressed)
        {
            rcsY += 1.0;
        }

        double lengthSquared = rcsX * rcsX + rcsY * rcsY;

        if (lengthSquared > 1.0)
        {
            double invLength = 1.0 / Math.Sqrt(lengthSquared);
            rcsX *= invLength;
            rcsY *= invLength;
        }

        return new global::SimVector3(rcsX, rcsY, 0.0);
    }

    static float ReadAxisInput(bool positivePressed, bool negativePressed)
    {
        if (positivePressed && !negativePressed)
        {
            return 1.0f;
        }

        if (negativePressed && !positivePressed)
        {
            return -1.0f;
        }

        return 0.0f;
    }

    static bool WasPressed(KeyControl primaryKey, KeyControl secondaryKey)
    {
        return
            (primaryKey != null && primaryKey.wasPressedThisFrame) ||
            (secondaryKey != null && secondaryKey.wasPressedThisFrame);
    }
}
