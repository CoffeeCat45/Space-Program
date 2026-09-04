/// <summary>
/// Physics stats for one VAB part template, in simulation units (kg, N, s).
/// </summary>
public struct VabPartPhysicsStats
{
    public double dryMassKg;
    public double fuelMassKg;
    public double thrustNewtons;
    public double ispSeconds;
    public bool isEngine;
    public bool isFuelTank;
    public string category;

    public double GetWetMassKg()
    {
        return dryMassKg + (isFuelTank ? fuelMassKg : 0.0);
    }
}
