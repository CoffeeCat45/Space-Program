using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Reads authored pad markers from the Cove launch-site prefab into <see cref="LaunchPadConstants"/>.
/// </summary>
static class LaunchSitePadAuthoring
{
    const string DefaultLaunchSitePrefabAssetPath =
        "Assets/GameObject/Cove_Launch_Site.prefab";

    public static void RefreshActive(GameObject assignedPrefab)
    {
        RefreshFromPrefab(assignedPrefab != null ? assignedPrefab : TryLoadDefaultPrefab());
    }

    public static void RefreshFromPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        LaunchPadMarkerLayout.SampleSpawnPointOffsetMeters(
            prefab,
            out double heightAbovePadBaseMeters,
            out double tangentEastMeters,
            out double tangentNorthMeters
        );

        LaunchPadConstants.SetLaunchSiteSpawnPointOffsetMeters(
            heightAbovePadBaseMeters,
            tangentEastMeters,
            tangentNorthMeters
        );
    }

    static GameObject TryLoadDefaultPrefab()
    {
#if UNITY_EDITOR
        GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLaunchSitePrefabAssetPath);

        if (editorPrefab != null)
        {
            return editorPrefab;
        }
#endif

        RuntimePartCatalog catalog = RuntimePartCatalog.Load();

        if (catalog != null && catalog.coveLaunchSite != null)
        {
            return catalog.coveLaunchSite;
        }

        return Resources.Load<GameObject>("Environments/Cove_Launch_Site");
    }
}
