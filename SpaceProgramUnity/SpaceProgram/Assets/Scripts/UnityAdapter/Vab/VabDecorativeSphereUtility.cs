using UnityEngine;

/// <summary>
/// Hides imported KSP hangar helper spheres (e.g. VABlvl3 root "Sphere", scale ~14 m).
/// They must not be toggled on by <see cref="GameSessionBootstrap"/> VAB root activation.
/// </summary>
static class VabDecorativeSphereUtility
{
    public static bool IsImportedDecorativeSphere(Transform candidate)
    {
        if (candidate == null || candidate.name != "Sphere")
        {
            return false;
        }

        if (candidate.GetComponent<VabRotationGizmo>() != null)
        {
            return false;
        }

        return candidate.GetComponent<MeshRenderer>() != null;
    }

    public static bool ShouldExcludeFromVabRootToggle(GameObject sceneRoot)
    {
        return sceneRoot != null && IsImportedDecorativeSphere(sceneRoot.transform);
    }

    public static void HideAllImportedDecorativeSpheres()
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (!IsImportedDecorativeSphere(candidate))
            {
                continue;
            }

            candidate.gameObject.SetActive(false);
        }
    }

    public static void HideDecorativeSpheresUnder(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (!IsImportedDecorativeSphere(candidate))
            {
                continue;
            }

            candidate.gameObject.SetActive(false);
        }
    }
}
