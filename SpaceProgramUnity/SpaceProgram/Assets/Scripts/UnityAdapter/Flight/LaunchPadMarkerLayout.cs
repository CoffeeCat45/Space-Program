using UnityEngine;

/// <summary>
/// Shared pad marker layout: terrain contact via <c>Node_Bottom</c>, spawn anchor via <c>SpawnPoint</c>.
/// </summary>
static class LaunchPadMarkerLayout
{
    public const string SpawnPointName = "SpawnPoint";
    public const string NodeBottomName = "Node_Bottom";

    public static void AlignPrefabInstanceToPadRoot(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Transform root = instance.transform;
        Transform spawnPoint = FindSpawnPoint(root);
        Transform bottom = FindPadContactBottom(root, spawnPoint);

        if (bottom != null)
        {
            float bottomLocalY = root.InverseTransformPoint(bottom.position).y;
            Vector3 localPosition = root.localPosition;
            localPosition.y -= bottomLocalY;
            root.localPosition = localPosition;
        }

        if (spawnPoint != null)
        {
            Vector3 spawnLocal = root.InverseTransformPoint(spawnPoint.position);
            Vector3 localPosition = root.localPosition;
            localPosition.x -= spawnLocal.x;
            localPosition.z -= spawnLocal.z;
            root.localPosition = localPosition;
        }
        else if (bottom == null)
        {
            AlignPrefabBottomFromRenderers(instance);
        }
    }

    public static void SampleSpawnPointOffsetMeters(
        GameObject prefab,
        out double heightAbovePadBaseMeters,
        out double tangentEastMeters,
        out double tangentNorthMeters
    )
    {
        heightAbovePadBaseMeters = 0.0;
        tangentEastMeters = 0.0;
        tangentNorthMeters = 0.0;

        if (prefab == null)
        {
            return;
        }

        Transform root = prefab.transform;
        Transform spawnPoint = FindSpawnPoint(root);
        Transform bottom = FindPadContactBottom(root, spawnPoint);

        if (spawnPoint == null || bottom == null)
        {
            return;
        }

        Vector3 bottomLocal = root.InverseTransformPoint(bottom.position);
        Vector3 spawnLocal = root.InverseTransformPoint(spawnPoint.position);
        heightAbovePadBaseMeters = spawnLocal.y - bottomLocal.y;
    }

    public static Transform FindSpawnPoint(Transform root)
    {
        return FindChildTransform(root, SpawnPointName);
    }

    public static Transform FindPadContactBottom(Transform root, Transform spawnPoint)
    {
        if (spawnPoint?.parent != null)
        {
            for (int i = 0; i < spawnPoint.parent.childCount; i++)
            {
                Transform child = spawnPoint.parent.GetChild(i);

                if (child != null && child.name == NodeBottomName)
                {
                    return child;
                }
            }
        }

        return FindChildTransform(root, NodeBottomName);
    }

    static void AlignPrefabBottomFromRenderers(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        float meshBottomLocalY = instance.transform.InverseTransformPoint(combinedBounds.min).y;
        Vector3 meshLocalPosition = instance.transform.localPosition;
        meshLocalPosition.y -= meshBottomLocalY;
        instance.transform.localPosition = meshLocalPosition;
    }

    static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate != null && candidate.name == childName)
            {
                return candidate;
            }
        }

        return null;
    }
}
