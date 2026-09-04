/// <summary>
/// Runtime cheat toggles set from the Unity dev menu. SimulationCore reads these directly.
/// </summary>
public static class SimulationCheatFlags
{
    public static bool InfiniteFuel;

    public static double ThrustMultiplier = 1.0;

    /// <summary>Dev: freeze Earth sidereal spin; heliocentric orbit still runs.</summary>
    public static bool PlanetSpinDisabled;

    public static double ScaleThrustNewtons(double thrustNewtons)
    {
        if (ThrustMultiplier == 1.0)
        {
            return thrustNewtons;
        }

        return thrustNewtons * ThrustMultiplier;
    }
}
