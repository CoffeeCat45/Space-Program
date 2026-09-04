/// <summary>
/// Physical surface parameters for a resolved biome.
/// </summary>
public readonly struct BiomeProperties
{
    public BiomeProperties(BiomeType biomeType, double staticFrictionCoefficient)
    {
        BiomeType = biomeType;
        StaticFrictionCoefficient = staticFrictionCoefficient;
    }

    public BiomeType BiomeType { get; }

    /// <summary>Dry Coulomb friction coefficient (μ) used by landing/contact code.</summary>
    public double StaticFrictionCoefficient { get; }
}
