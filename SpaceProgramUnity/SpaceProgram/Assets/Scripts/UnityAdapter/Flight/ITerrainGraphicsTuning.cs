/// <summary>
/// Live terrain LOD tuning exposed by sandbox and main-game planet drivers.
/// </summary>
public interface ITerrainGraphicsTuning
{
    float LodDistanceFactor { get; set; }
    int LoadSpeedPerFrame { get; set; }
    int MaxLod { get; set; }
    float ViewConeHalfAngleDegrees { get; set; }
    float CloudDetail { get; set; }
    float CloudCoverage { get; set; }
    float CloudFrequency { get; set; }
    float CloudCluster { get; set; }
    CloudRendererMode CloudRenderer { get; set; }
    float ViewConeUnloadHalfAngleDegrees { get; }
    int ActiveChunkCount { get; }
    bool IsAvailable { get; }
}
