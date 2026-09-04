struct AerodynamicProperties
{
    public double dragCoeff;
    public double liftCoeff;
    public double inducedDragFactor;
    public double forwardArea;
    public double sideArea;
    public double topArea;
    public SimVector3 centerOfPressureOffset;
    public SimVector3 centerOfMassLocal;

    public static AerodynamicProperties FromStageData(
        StageData stage,
        SimVector3 centerOfPressureOffset
    )
    {
        return new AerodynamicProperties
        {
            dragCoeff = stage.dragCoefficient,
            liftCoeff = 0.8,
            inducedDragFactor = 0.45,
            forwardArea = 6.0,
            sideArea = 10.0,
            topArea = 8.0,
            centerOfPressureOffset = centerOfPressureOffset,
            centerOfMassLocal = SimVector3.Zero()
        };
    }
}

struct AtmosphericDragResult
{
    public SimVector3 force;
    public SimVector3 acceleration;
    public SimVector3 torqueLocal;
    public double torqueZ;
    public double effectiveArea;
    public double density;
    public double dynamicPressure;
    public double angleOfAttack;
    public double dragForceMagnitude;
    public double liftForceMagnitude;
}
