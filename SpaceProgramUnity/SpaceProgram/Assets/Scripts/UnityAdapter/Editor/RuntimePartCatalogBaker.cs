#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes <see cref="RuntimePartCatalog"/> so the player can instantiate ReStock prefabs
/// without <c>AssetDatabase</c>.
/// </summary>
static class RuntimePartCatalogBaker
{
    const string CoveLaunchSitePrefabAssetPath = "Assets/GameObject/Cove_Launch_Site.prefab";

    [InitializeOnLoadMethod]
    static void AutoBakeMissingPlayerAssets()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            BakeShaderBundle();

            if (AssetDatabase.LoadAssetAtPath<RuntimePartCatalog>(RuntimePartCatalog.AssetPath) == null)
            {
                Bake();
            }
        };
    }

    [MenuItem("Space Program/Prepare Player Content")]
    public static void BakeFromMenu()
    {
        Bake();
        Debug.Log("Space Program: baked RuntimePartCatalog and shader bundle for player builds.");
    }

    public static RuntimePartCatalog Bake()
    {
        EnsureResourcesFolder();
        BakeShaderBundle();
        RuntimePartCatalog catalog = LoadOrCreateAsset();
        List<PartTemplate> templates = PartCatalog.DiscoverTemplatesForPlayerBake();
        catalog.parts = new RuntimePartCatalog.PartRecord[templates.Count];

        for (int i = 0; i < templates.Count; i++)
        {
            catalog.parts[i] = BuildRecord(templates[i]);
        }

        catalog.coveLaunchSite = AssetDatabase.LoadAssetAtPath<GameObject>(CoveLaunchSitePrefabAssetPath);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    public static void BakeShaderBundle()
    {
        EnsureResourcesFolder();
        PlayerIncludedShaders bundle = AssetDatabase.LoadAssetAtPath<PlayerIncludedShaders>(
            PlayerIncludedShaders.AssetPath
        );

        if (bundle == null)
        {
            bundle = ScriptableObject.CreateInstance<PlayerIncludedShaders>();
            AssetDatabase.CreateAsset(bundle, PlayerIncludedShaders.AssetPath);
        }

        Shader[] found = new Shader[BuildShaderIncluder.RequiredShaderNames.Length];
        int count = 0;

        for (int i = 0; i < BuildShaderIncluder.RequiredShaderNames.Length; i++)
        {
            Shader shader = Shader.Find(BuildShaderIncluder.RequiredShaderNames[i]);

            if (shader == null)
            {
                Debug.LogWarning(
                    "PlayerIncludedShaders: missing " +
                    BuildShaderIncluder.RequiredShaderNames[i]
                );
                continue;
            }

            found[count] = shader;
            count++;
        }

        Shader[] compact = new Shader[count];
        System.Array.Copy(found, compact, count);
        bundle.shaders = compact;
        EditorUtility.SetDirty(bundle);
        BakePlayerShaderMaterials(compact);
        AssetDatabase.SaveAssets();
    }

    const string PlayerMaterialsFolder = "Assets/Resources/PlayerMaterials";

    static void BakePlayerShaderMaterials(Shader[] shaders)
    {
        if (!AssetDatabase.IsValidFolder(PlayerMaterialsFolder))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "PlayerMaterials");
        }

        if (shaders == null)
        {
            return;
        }

        for (int i = 0; i < shaders.Length; i++)
        {
            Shader shader = shaders[i];
            if (shader == null || !shader.name.StartsWith("SpaceProgram/"))
            {
                continue;
            }

            string fileName = shader.name.Substring("SpaceProgram/".Length) + ".mat";
            string assetPath = PlayerMaterialsFolder + "/" + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = shader.name.Substring("SpaceProgram/".Length),
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                material.shader = shader;
            }

            if (shader.name == "SpaceProgram/SandboxEarthGlobe")
            {
                material.DisableKeyword("_SANDBOX_VERTEX_PAINT");
            }

            EditorUtility.SetDirty(material);
        }
    }

    static RuntimePartCatalog.PartRecord BuildRecord(PartTemplate template)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(template.prefabPath);
        RuntimePartCatalog.PartRecord record = new RuntimePartCatalog.PartRecord
        {
            id = template.id,
            displayName = template.displayName,
            category = template.category,
            dryMass = template.dryMass,
            fuelCapacity = template.fuelCapacity,
            maxThrustVac = template.maxThrustVac,
            ispVacuum = template.ispVacuum,
            prefabPath = template.prefabPath,
            iconPath = template.iconPath,
            prefab = prefab,
            icon = string.IsNullOrWhiteSpace(template.iconPath) ?
                null :
                AssetDatabase.LoadAssetAtPath<Texture2D>(template.iconPath),
            nodes = CopyNodes(template.nodes),
        };

        FillThrustFrame(record, prefab);
        return record;
    }

    static RuntimePartCatalog.NodeRecord[] CopyNodes(List<AttachmentNode> nodes)
    {
        if (nodes == null || nodes.Count == 0)
        {
            return System.Array.Empty<RuntimePartCatalog.NodeRecord>();
        }

        RuntimePartCatalog.NodeRecord[] records = new RuntimePartCatalog.NodeRecord[nodes.Count];

        for (int i = 0; i < nodes.Count; i++)
        {
            AttachmentNode node = nodes[i];
            records[i] = new RuntimePartCatalog.NodeRecord
            {
                localPosition = new Vector3(
                    (float)node.localPosition.x,
                    (float)node.localPosition.y,
                    (float)node.localPosition.z
                ),
                direction = new Vector3(
                    (float)node.direction.x,
                    (float)node.direction.y,
                    (float)node.direction.z
                ),
                role = node.role,
            };
        }

        return records;
    }

    static void FillThrustFrame(RuntimePartCatalog.PartRecord record, GameObject prefab)
    {
        record.hasThrustFrame = false;

        if (prefab == null)
        {
            return;
        }

        Transform thrustTransform = PartPrefabBuilder.FindThrustTransform(prefab.transform);

        if (thrustTransform != null)
        {
            Vector3 offset = prefab.transform.InverseTransformPoint(thrustTransform.position);
            Vector3 forceDirection = prefab.transform
                .InverseTransformDirection(-thrustTransform.up)
                .normalized;
            record.thrustOffsetPartLocal = offset;
            record.thrustForceDirectionPartLocal = forceDirection;
            record.hasThrustFrame = true;
            return;
        }

        Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(prefab);
        record.thrustOffsetPartLocal = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        record.thrustForceDirectionPartLocal = Vector3.up;
        record.hasThrustFrame = true;
    }

    static RuntimePartCatalog LoadOrCreateAsset()
    {
        RuntimePartCatalog catalog = AssetDatabase.LoadAssetAtPath<RuntimePartCatalog>(
            RuntimePartCatalog.AssetPath
        );

        if (catalog != null)
        {
            return catalog;
        }

        catalog = ScriptableObject.CreateInstance<RuntimePartCatalog>();
        AssetDatabase.CreateAsset(catalog, RuntimePartCatalog.AssetPath);
        return catalog;
    }

    static void EnsureResourcesFolder()
    {
        if (AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            return;
        }

        AssetDatabase.CreateFolder("Assets", "Resources");
    }
}
#endif
