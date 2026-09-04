struct KeplerRailsState
{
    public string primaryBodyName;
    public double epochTime;
    public double meanAnomalyAtEpoch;
    public double semiMajorAxis;
    public double eccentricity;
    public double semiLatusRectum;
    public SimVector3 periapsisDirection;
    public SimVector3 sideDirection;
    public SimVector3 orbitNormal;
    public bool isValid;

    public static KeplerRailsState Invalid()
    {
        return new KeplerRailsState();
    }
}
