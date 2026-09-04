#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes <c>.partdef.json</c> sidecars next to VAB prefabs (ReStock pipeline + manual parts).
/// </summary>
static class VabPartDefinitionWriter
{
    static readonly Dictionary<string, string> DisplayNameOverrides =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "restock-mk1-pod", "Mk1 Command Pod" },
            { "restock-cupola-1", "Mk1 Cupola" },
            { "restock-fueltank-0625-1", "Oscar-B Fuel Tank (Tall)" },
            { "restock-fueltank-0625-4", "Oscar-B Fuel Tank" },
            { "restock-fueltank-125-1", "FL-T800 Fuel Tank" },
            { "restock-fueltank-125-2", "FL-T400 Fuel Tank" },
            { "restock-fueltank-125-4", "FL-T100 Fuel Tank" },
            { "restock-fueltank-375-1", "Kerbodyne S3-14400" },
            { "restock-fueltank-375-2", "Kerbodyne S3-7200" },
            { "restock-fueltank-375-3", "Kerbodyne S3-3600" },
            { "restock-fueltank-375-4", "Kerbodyne S3-1800" },
            { "restock-engine-terrier-1", "LV-909 Terrier" },
            { "restock-engine-reliant-2", "LV-T30 Reliant" },
            { "restock-engine-ant-1", "LV-1 Ant" },
            { "restock-engine-boar-1", "KR-1 Boar" },
            { "restock-pod-sphere-1", "KV-1 Onion Pod" },
        };

    internal static void WriteForPrefab(
        string prefabAssetPath,
        string category,
        string displayNameOverride = null
    )
    {
        if (string.IsNullOrWhiteSpace(prefabAssetPath))
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);

        if (prefab == null)
        {
            Debug.LogWarning("VAB: cannot write partdef, prefab missing at " + prefabAssetPath);
            return;
        }

        string partId = System.IO.Path.GetFileNameWithoutExtension(prefabAssetPath);
        string categoryValue = string.IsNullOrWhiteSpace(category) ?
            PartDatabase.CategoryMisc :
            category;
        string displayName = displayNameOverride;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            DisplayNameOverrides.TryGetValue(partId, out displayName);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = PartCatalogHelpers.HumanizePartId(partId);
        }

        PartDefinitionData data = new PartDefinitionData
        {
            id = partId,
            displayName = displayName,
            category = categoryValue,
            dryMass = PartCatalogHelpers.GuessDryMass(partId, categoryValue),
            fuelCapacity = PartCatalogHelpers.GuessFuelCapacity(partId, categoryValue),
            maxThrustVac = PartCatalogHelpers.GuessMaxThrustVac(partId, categoryValue),
            ispVacuum = PartCatalogHelpers.GuessIspVacuum(partId, categoryValue),
            nodes = PartPrefabBuilder.ReadAuthoredNodes(prefab),
        };

        string partDefPath = PartDefinitionFile.GetSidecarPathForPrefab(prefabAssetPath);
        PartDefinitionFile.Save(partDefPath, data);
        AssetDatabase.ImportAsset(partDefPath);
    }
}
#endif
