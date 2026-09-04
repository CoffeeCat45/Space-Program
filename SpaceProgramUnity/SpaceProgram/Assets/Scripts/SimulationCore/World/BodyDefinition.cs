class BodyDefinition
{
    public string name;
    public double mu;
    public double radius;
    public bool isStatic;
    public string parentName;
    public double orbitRadius;
    public double orbitEccentricity;
    public double orbitPhaseRadians;
    public bool includeInSimulation;
    public AtmosphereProfile atmosphereProfile;

    public BodyDefinition(
        string name,
        double mu,
        double radius,
        bool isStatic,
        string parentName = null,
        double orbitRadius = 0,
        double orbitEccentricity = 0,
        double orbitPhaseRadians = 0,
        bool includeInSimulation = true
    )
    {
        this.name = name;
        this.mu = mu;
        this.radius = radius;
        this.isStatic = isStatic;
        this.parentName = parentName;
        this.orbitRadius = orbitRadius;
        this.orbitEccentricity = orbitEccentricity;
        this.orbitPhaseRadians = orbitPhaseRadians;
        this.includeInSimulation = includeInSimulation;
        atmosphereProfile = AtmosphereProfile.None;
    }
}
