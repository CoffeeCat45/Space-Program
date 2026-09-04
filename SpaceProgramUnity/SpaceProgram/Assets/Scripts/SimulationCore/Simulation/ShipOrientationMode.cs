enum ShipOrientationMode
{
    Manual,
    Prograde,
    Retrograde,
    Maneuver,
    RadialPlus,
    RadialMinus,
    TargetAlignment,
    StabilityAssist,
    /// <summary>Developer-only SAS: same hold logic as 6, unlimited counter-torque.</summary>
    DevStabilityAssist,
}

struct ShipOrientationInput
{
    public ShipOrientationMode mode;
    public SimVector3 maneuverTargetDirection;
    public double autoTurnRateRadiansPerSecond;
    public double atmosphericAutoTurnRateMultiplier;

    public static ShipOrientationInput Idle()
    {
        return new ShipOrientationInput
        {
            mode = ShipOrientationMode.Manual,
            autoTurnRateRadiansPerSecond = 0.0,
            atmosphericAutoTurnRateMultiplier = 1.0,
        };
    }
}
