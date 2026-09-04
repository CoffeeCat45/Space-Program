struct DockingPortContactMetrics
{
    public bool hasSourcePort;
    public bool hasTargetPort;
    public double portSeparation;
    public double relativePortSpeed;
    public double sourcePortAlignment;
    public double targetPortAlignment;

    public bool HasValidPorts => hasSourcePort && hasTargetPort;

    public bool IsAlignedForCapture(double minAlignmentDot)
    {
        return
            HasValidPorts &&
            sourcePortAlignment >= minAlignmentDot &&
            targetPortAlignment >= minAlignmentDot;
    }
}
