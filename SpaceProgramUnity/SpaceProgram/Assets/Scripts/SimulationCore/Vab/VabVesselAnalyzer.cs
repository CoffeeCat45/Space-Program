using System;

/// <summary>
/// Computes vessel mass / TWR from a VAB blueprint using part-template stats.
/// Unity-free — part lookup is injected from UnityAdapter.
/// </summary>
public static class VabVesselAnalyzer
{
    public delegate bool PartStatsLookup(string partTemplateId, out VabPartPhysicsStats stats);

    public static VabVesselStats Analyze(
        VesselBlueprint blueprint,
        PartStatsLookup lookup,
        double surfaceGravity
    )
    {
        VabVesselStats result = new VabVesselStats
        {
            surfaceGravity = surfaceGravity > 0.0 ?
                surfaceGravity :
                LandingConfig.GetSurfaceStartGravity(),
        };

        if (blueprint?.blocks == null || lookup == null)
        {
            return result;
        }

        for (int i = 0; i < blueprint.blocks.Count; i++)
        {
            BlockSnapshot block = blueprint.blocks[i];

            if (block == null || block.isDisjointed)
            {
                continue;
            }

            if (!lookup(block.partTemplateId, out VabPartPhysicsStats stats))
            {
                continue;
            }

            AccumulatePartStats(ref result, stats);
        }

        FinalizeTotals(ref result);
        return result;
    }

    public static VabVesselStats AnalyzeConnectedParts(
        System.Collections.Generic.IReadOnlyList<VabPartPhysicsStats> connectedParts,
        double surfaceGravity
    )
    {
        VabVesselStats result = new VabVesselStats
        {
            surfaceGravity = surfaceGravity > 0.0 ?
                surfaceGravity :
                LandingConfig.GetSurfaceStartGravity(),
        };

        if (connectedParts == null)
        {
            return result;
        }

        for (int i = 0; i < connectedParts.Count; i++)
        {
            AccumulatePartStats(ref result, connectedParts[i]);
        }

        FinalizeTotals(ref result);
        return result;
    }

    static void AccumulatePartStats(ref VabVesselStats result, VabPartPhysicsStats stats)
    {
        result.connectedPartCount++;
        result.dryMassKg += stats.dryMassKg;

        if (stats.isFuelTank)
        {
            result.fuelMassKg += stats.fuelMassKg;
        }

        if (stats.thrustNewtons > 0.0)
        {
            result.engineCount++;
            result.activeThrustNewtons += stats.thrustNewtons;
        }
    }

    static void FinalizeTotals(ref VabVesselStats result)
    {
        result.totalMassKg = result.dryMassKg + result.fuelMassKg;

        if (result.totalMassKg > 0.0 && result.surfaceGravity > 0.0)
        {
            double weight = result.totalMassKg * result.surfaceGravity;
            result.thrustToWeightRatio = weight > 0.0 ?
                result.activeThrustNewtons / weight :
                0.0;
        }
    }
}
