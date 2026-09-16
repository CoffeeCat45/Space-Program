/// <summary>
/// KSP-style speed / SAS frame: Surface (minus ω×r), Orbit (body CM), Target.
/// </summary>
public enum ShipHudSpeedMode
{
    Surface = 0,
    Orbit = 1,
    Target = 2
}

public static class ShipSpeedReference
{
    public const int ModeCount = 3;

    public static ShipHudSpeedMode Cycle(ShipHudSpeedMode current)
    {
        int next = ((int)current + 1) % ModeCount;
        if (next < 0)
        {
            next = 0;
        }

        return (ShipHudSpeedMode)next;
    }

    public static SimVector3 ResolveRelativeVelocity(
        ShipHudSpeedMode mode,
        SimVector3 orbitRelativeVelocity,
        SimVector3 surfaceRelativeVelocity,
        SimVector3 targetRelativeVelocity,
        bool hasTarget
    )
    {
        if (mode == ShipHudSpeedMode.Target && hasTarget)
        {
            return targetRelativeVelocity;
        }

        if (mode == ShipHudSpeedMode.Surface)
        {
            return surfaceRelativeVelocity;
        }

        return orbitRelativeVelocity;
    }

    public static string GetModeLabel(ShipHudSpeedMode mode)
    {
        if (mode == ShipHudSpeedMode.Surface)
        {
            return "Surface";
        }

        if (mode == ShipHudSpeedMode.Target)
        {
            return "Target speed";
        }

        return "Orbital speed";
    }
}
