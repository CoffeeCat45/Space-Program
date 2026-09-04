using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Instantiates VAB part prefabs for in-flight vessel rendering.
/// Editor Play uses AssetDatabase; the player uses <see cref="RuntimePartCatalog"/>.
/// </summary>
static class FlightPartVisualFactory
{
    public static bool TryCreatePartVisual(string partTemplateId, out GameObject instance)
    {
        instance = null;
        GameObject prefab = LoadPrefab(partTemplateId);

        if (prefab == null)
        {
            return false;
        }

        instance = Object.Instantiate(prefab);
        instance.name = prefab.name;
        DisableColliders(instance);
        return true;
    }

    public static GameObject LoadPrefab(string partTemplateId)
    {
        if (string.IsNullOrWhiteSpace(partTemplateId))
        {
            return null;
        }

        PartTemplate template = PartDatabase.GetById(partTemplateId);

#if UNITY_EDITOR
        if (template != null && !string.IsNullOrWhiteSpace(template.prefabPath))
        {
            GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(template.prefabPath);

            if (editorPrefab != null)
            {
                return editorPrefab;
            }
        }
#endif

        RuntimePartCatalog catalog = RuntimePartCatalog.Load();

        if (catalog != null)
        {
            GameObject bakedPrefab = catalog.GetPrefab(partTemplateId);

            if (bakedPrefab != null)
            {
                return bakedPrefab;
            }

            if (template != null && !string.IsNullOrWhiteSpace(template.prefabPath))
            {
                bakedPrefab = catalog.GetPrefabByAssetPath(template.prefabPath);

                if (bakedPrefab != null)
                {
                    return bakedPrefab;
                }
            }
        }

        return null;
    }

    public static GameObject LoadPrefabAtPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

#if UNITY_EDITOR
        GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

        if (editorPrefab != null)
        {
            return editorPrefab;
        }
#endif

        RuntimePartCatalog catalog = RuntimePartCatalog.Load();

        if (catalog == null)
        {
            return null;
        }

        GameObject bakedPrefab = catalog.GetPrefabByAssetPath(assetPath);

        if (bakedPrefab != null)
        {
            return bakedPrefab;
        }

        return catalog.GetPrefab(Path.GetFileNameWithoutExtension(assetPath));
    }

    static void DisableColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }
}
