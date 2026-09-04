/// <summary>
/// Aggregated VAB vessel stats for HUD (mass, thrust, TWR at pad).
/// </summary>
public struct VabVesselStats
{
    public int connectedPartCount;
    public int engineCount;
    public double totalMassKg;
    public double dryMassKg;
    public double fuelMassKg;
    public double activeThrustNewtons;
    public double surfaceGravity;
    public double thrustToWeightRatio;

    public bool HasEngines => engineCount > 0;

    public bool CanLiftOff => thrustToWeightRatio >= 1.0;
}
