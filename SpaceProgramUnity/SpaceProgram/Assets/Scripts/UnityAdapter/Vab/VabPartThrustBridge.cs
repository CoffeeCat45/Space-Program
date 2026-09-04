using UnityEngine;

/// <summary>
/// Reads engine thrust point and force direction from part prefab thrustTransform.
/// </summary>
public static class VabPartThrustBridge
{
    public static bool TryGetEngineThrustFrame(
        string partTemplateId,
        out SimVector3 thrustOffsetPartLocal,
        out SimVector3 thrustForceDirectionPartLocal
    )
    {
        thrustOffsetPartLocal = SimVector3.Zero();
        thrustForceDirectionPartLocal = SimVector3.UnitY();

        if (string.IsNullOrWhiteSpace(partTemplateId))
        {
            return false;
        }

        GameObject prefab = FlightPartVisualFactory.LoadPrefab(partTemplateId);

        if (prefab != null)
        {
            Transform thrustTransform = PartPrefabBuilder.FindThrustTransform(prefab.transform);

            if (thrustTransform != null)
            {
                Vector3 offset = prefab.transform.InverseTransformPoint(thrustTransform.position);
                thrustOffsetPartLocal = new SimVector3(offset.x, offset.y, offset.z);
                Vector3 forceDirection = prefab.transform
                    .InverseTransformDirection(-thrustTransform.up)
                    .normalized;
                thrustForceDirectionPartLocal = new SimVector3(
                    forceDirection.x,
                    forceDirection.y,
                    forceDirection.z
                );
                return true;
            }

            Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(prefab);
            thrustOffsetPartLocal = new SimVector3(
                bounds.center.x,
                bounds.min.y,
                bounds.center.z
            );
            return true;
        }

        RuntimePartCatalog catalog = RuntimePartCatalog.Load();
        return catalog != null &&
            catalog.TryGetThrustFrame(
                partTemplateId,
                out thrustOffsetPartLocal,
                out thrustForceDirectionPartLocal
            );
    }
}
