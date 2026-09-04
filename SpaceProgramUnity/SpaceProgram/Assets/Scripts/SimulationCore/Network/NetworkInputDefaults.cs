using System;

static class NetworkInputDefaults
{
    public static NetworkInputFrame CreateIdleFrame(int playerSlot, long tick = 0L)
    {
        return new NetworkInputFrame
        {
            tick = tick,
            vesselId = playerSlot,
            throttle = 0.0f,
            staging = false,
            rcsVector = SimVector3.Zero(),
            orientationMode = ShipOrientationMode.Manual,
            pitchInput = 0.0f,
            yawInput = 0.0f,
            rollInput = 0.0f,
            isPauseRequested = false,
        };
    }

    public static void ApplyLockstepTickInputs(
        Simulation simulation,
        long tick,
        NetworkInputFrame player1Frame,
        NetworkInputFrame player2Frame,
        Action<int, NetworkInputFrame> onSlotApplied = null
    )
    {
        simulation.ApplyLockstepTickInputs(player1Frame, player2Frame);
        onSlotApplied?.Invoke(NetworkInputFrame.Player1VesselId, player1Frame);
        onSlotApplied?.Invoke(NetworkInputFrame.Player2VesselId, player2Frame);
    }
}
