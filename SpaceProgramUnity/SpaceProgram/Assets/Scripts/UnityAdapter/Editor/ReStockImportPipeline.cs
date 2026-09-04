#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch pipeline for ReStock textures -> URP materials and FBX material slot binding.
/// Menu: Space Program / ReStock / ...
/// </summary>
static class ReStockImportPipeline
{
    const string MenuRoot = "Space Program/ReStock/";
    const string DefaultExternalModRoot = @"D:\Projects\games\Space Program\ReStock\Assets";
    const string TexturesAssetRoot = "Assets/Models/Parts/Textures";
    const string MaterialsAssetRoot = "Assets/Materials/Parts";
    const string MeshesAssetRoot = "Assets/Models/Parts/Meshes";

    /// <summary>
    /// Default paint variant when VAB UI swaps skins on the same material slot (future).
    /// Import still maps each FBX slot to its own named material (-1, -2, …).
    /// </summary>
    internal const int DefaultMaterialVariantIndex = 1;

    static readonly string[] TextureExtensions = { ".png", ".dds", ".tga", ".jpg" };

    static readonly Dictionary<string, string> MapSuffixToShaderProperty = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        { "_d", "_BaseMap" },
        { "_diff", "_BaseMap" },
        { "_diffuse", "_BaseMap" },
        { "_albedo", "_BaseMap" },
        { "_c", "_BaseMap" },
        { "_n", "_BumpMap" },
        { "_normal", "_BumpMap" },
        { "_bump", "_BumpMap" },
        { "_e", "_EmissionMap" },
        { "_emissive", "_EmissionMap" },
        { "_a", "_OcclusionMap" },
        { "_ao", "_OcclusionMap" },
        // ReStock / KSP convention: hyphen before map letter (restock-part-1-n.dds)
        { "-n", "_BumpMap" },
        { "-e", "_EmissionMap" },
        { "-d", "_BaseMap" },
        { "-a", "_OcclusionMap" },
        // NOTE: KSP -s / -m are legacy specular maps — do NOT plug into URP MetallicGlossMap (turns parts black).
    };

    static readonly string[] KspHiddenMeshNameFragments =
    {
        "Mk1PodWhite",
        "Mk1PodBlackWhite",
        "Mk2PodWhite",
        "Mk2PodDark",
        "Flag",
    };

    /// <summary>
    /// Pod/tank: first matching name wins (one mesh). Engine: keep all listed meshes (body + nozzle).
    /// </summary>
    static readonly Dictionary<string, string[]> PilotSingleMeshPriority =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "restock-mk1-pod",
                new[] { "Mk1PodDark", "Mk1PodBlackWhite", "Mk1PodWhite" }
            },
            {
                "restock-fueltank-0625-1",
                new[] { "Tank0625", "Tank0625_White" }
            },
        };

    static readonly Dictionary<string, string[]> PilotMultiKeepMeshes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "restock-engine-terrier-1",
                new[] { "Engine", "EngineCore909", "Frame 2", "Frame 4", "Frame_MLI" }
            },
        };

    static readonly Dictionary<string, string> PilotMeshToMaterial =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Mk1PodDark", "restock-mk1-pod-1" },
            { "Mk1PodBlackWhite", "restock-mk1-pod-1" },
            { "Mk1PodWhite", "restock-mk1-pod-1" },
            { "Tank0625", "restock-fueltank-0625-1" },
            { "Tank0625_White", "restock-fueltank-0625-1-white" },
            { "Engine", "restock-engine-125-revised-1" },
            { "EngineCore909", "restock-engine-125-revised-2" },
            { "LV_T30", "restock-engine-125-revised-1" },
            { "EngineCoreT30Glow", "restock-engine-125-revised-2" },
            { "fairingWhite", "restock-engine-shroud-125-1" },
            { "fairingOrange", "restock-engine-shroud-125-2" },
            { "25mCupola 1", "restock-cupola-lab-1" },
            { "Tank800_A", "restock-fueltank-125-1" },
            { "Frame 2", "restock-engine-125-revised-1" },
            { "Frame 4", "restock-engine-125-revised-1" },
            { "Frame_MLI", "restock-engine-125-additional-3" },
        };

    static readonly Dictionary<string, HashSet<string>> PilotKeepMeshWhitelist =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "restock-mk1-pod",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mk1PodDark" }
            },
            {
                "restock-fueltank-0625-1",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Tank0625" }
            },
            {
                "restock-fueltank-125-1",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Tank800_A" }
            },
            {
                "restock-engine-terrier-1",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Engine",
                    "EngineCore909",
                    "Frame 2",
                    "Frame 4",
                    "Frame_MLI",
                }
            },
        };

    static readonly string[] PilotAlwaysDeleteMeshFragments =
    {
        "Flag",
        "Glow",
        "Shroud",
        "Frame",
        "Mk2Pod",
    };

    const string PrefabsAssetRoot = "Assets/Prefabs/Parts";

    static readonly string[] PilotLegacyModelAssetPaths =
    {
        $"{MeshesAssetRoot}/Command/restock-mk1-pod.fbx",
        $"{MeshesAssetRoot}/FuelTank/restock-fueltank-0625-1.fbx",
        $"{MeshesAssetRoot}/Engine/restock-engine-terrier-1.fbx",
        $"{MeshesAssetRoot}/Command/restock-mk1-pod.glb",
        $"{MeshesAssetRoot}/FuelTank/restock-fueltank-0625-1.glb",
        $"{MeshesAssetRoot}/Engine/restock-engine-terrier-1.glb",
        $"{MeshesAssetRoot}/Engine/restock-engine-terrier-1.obj",
        $"{MeshesAssetRoot}/Command/restock-cupola-1.fbx",
        $"{MeshesAssetRoot}/Command/restock-cupola-1.glb",
        $"{MeshesAssetRoot}/FuelTank/restock-fueltank-125-1.fbx",
        $"{MeshesAssetRoot}/FuelTank/restock-fueltank-125-1.glb",
        $"{MeshesAssetRoot}/Engine/restock-engine-reliant-2.fbx",
        $"{MeshesAssetRoot}/Engine/restock-engine-reliant-2.glb",
        $"{MeshesAssetRoot}/Engine/restock-engine-reliant-2.obj",
        $"{MeshesAssetRoot}/Command/restock-mk1-pod.obj",
        $"{MeshesAssetRoot}/Command/restock-mk1-pod.manifest.json",
        $"{MeshesAssetRoot}/Command/restock-cupola-1.obj",
        $"{MeshesAssetRoot}/Command/restock-cupola-1.manifest.json",
        $"{MeshesAssetRoot}/FuelTank/restock-fueltank-0625-1.obj",
        $"{MeshesAssetRoot}/FuelTank/restock-fueltank-0625-1.manifest.json",
        $"{MeshesAssetRoot}/FuelTank/restock-fueltank-125-1.obj",
        $"{MeshesAssetRoot}/FuelTank/restock-fueltank-125-1.manifest.json",
    };

    static readonly string[] PilotMaterialNames =
    {
        "restock-mk1-pod-1",
        "restock-fueltank-0625-1",
        "restock-fueltank-0625-1-white",
        "restock-fueltank-125-1",
        "restock-cupola-lab-1",
        "restock-crewed-hatch-1",
        "restock-endcap-crewed-1",
        "restock-engine-125-revised-1",
        "restock-engine-125-revised-2",
        "restock-engine-125-additional-3",
        "restock-engine-shroud-125-1",
        "restock-engine-shroud-125-2",
    };

    static readonly string[] PilotTextureSourcePatterns =
    {
        "restock-mk1-pod-1",
        "restock-fueltank-0625-1",
        "restock-fueltank-125-1",
        "restock-cupola-lab-1",
        "restock-crewed-hatch-1",
        "restock-endcap-crewed-1",
        "restock-cupola-1",
        "restock-engine-125-revised",
        "restock-engine-125-additional-3",
        "restock-engine-shroud-125",
    };

    static readonly Dictionary<string, string> MaterialSlotAliases = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        { "restock-engine-125-1", "restock-engine-125-revised-1" },
        { "restock-engine-125-2", "restock-engine-125-revised-2" },
        { "restock-engine-125-1-grey", "restock-engine-125-revised-1" },
        { "restock-engine-shrouds-125-1", "restock-engine-shroud-125-1" },
        { "restock-engine-shrouds-125-2", "restock-engine-shroud-125-2" },
        { "restock-engine-glow", "restock-engine-glow-basic-1" },
        { "restock-engine-125-additional-1", "restock-engine-125-additional-1" },
        { "restock-crewed-endcap-1", "restock-endcap-crewed-1" },
        { "restock-crewed-endcap-2", "restock-mk1-pod-1" },
        { "25mCapBlack", "restock-endcap-crewed-1" },
        { "25mCapFoil", "restock-endcap-crewed-1" },
        { "CommonHatchRound", "restock-crewed-hatch-1" },
        { "restock-crewed-hatch-1-grey", "restock-crewed-hatch-1" },
        { "restock-endcap-crewed-metallic", "restock-endcap-crewed-1" },
        { "restock-engine-boar-metallic-1", "restock-engine-boar-1" },
    };

    static readonly Dictionary<string, string> PilotPrimaryMaterialOverrides = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        { "restock-engine-terrier-1", "restock-engine-125-revised-1" },
        { "restock-engine-reliant-2", "restock-engine-125-revised-1" },
    };

    internal const bool FlipPilotMeshUvV = true;
    internal static bool PilotAssignNormalMaps = false;

    [MenuItem(MenuRoot + "8. Re-export Pilot meshes (instructions)")]
    static void ShowPilotReexportInstructions()
    {
        string scriptPath = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "..",
                "..",
                "Pipeline",
                "Blender",
                "export_pilot_parts.py"
            )
        );

        if (!File.Exists(scriptPath))
        {
            scriptPath = @"D:\Projects\games\Space Program\Pipeline\Blender\export_pilot_parts.py";
        }

        EditorUtility.DisplayDialog(
            "Re-export batch meshes",
            "Exports 6 ReStock parts (OBJ + manifest) from Blender.\n\n" +
            "1. Open Blender 3.6+\n" +
            "2. Install addon io_object_mu (KSP .mu importer)\n" +
            "3. Scripting -> Open:\n" + scriptPath + "\n" +
            "4. Run Script (exports .obj into Assets/Models/Parts/Meshes/)\n" +
            "5. Unity: Space Program -> ReStock -> 9\n" +
            "6. Replace prefabs in scene from Assets/Prefabs/Parts/\n\n" +
            "Until then VAB can use untextured/colored placeholders.",
            "OK"
        );

        EditorUtility.RevealInFinder(scriptPath);
    }

    [MenuItem(MenuRoot + "0. Pilot Quick Setup (Cleanup + Materials + Prefabs)")]
    static void PilotQuickSetup()
    {
        if (
            !EditorUtility.DisplayDialog(
                "ReStock pilot setup",
                "Keep only batch-exported parts (6 pilot + expansion).\n" +
                "Copy 2D textures → URP materials → prefabs.\n" +
                "Primary meshes visible; attachment skins disabled by default.\n" +
                "Perfect UVs still need Blender re-export (menu 8).",
                "Run",
                "Cancel"
            )
        )
        {
            return;
        }

        KeepPilotPartsOnly(silent: true);
        int copiedTextures = CopyPilotTexturesFromReStock();
        RebuildPartMaterials();
        BuildPilotPartPrefabs();
        Debug.Log(
            $"ReStock pilot: done (textures copied={copiedTextures}). " +
            "Drag prefabs from Assets/Prefabs/Parts/."
        );
    }

    [MenuItem(MenuRoot + "1. Copy Textures from ReStock Folder")]
    static void CopyTexturesFromMod()
    {
        string sourceRoot = EditorUtility.OpenFolderPanel(
            "ReStock textures root (select ReStock/Assets — contains Command, Engine, FuelTank…)",
            DefaultExternalModRoot,
            string.Empty
        );

        if (string.IsNullOrEmpty(sourceRoot))
        {
            return;
        }

        EnsureAssetFolder(TexturesAssetRoot);

        string[] files = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => TextureExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        int copied = 0;

        for (int i = 0; i < files.Length; i++)
        {
            string sourceFile = files[i];
            string relative = GetRelativePathSafe(sourceRoot, sourceFile);
            string targetAssetPath = Path.Combine(TexturesAssetRoot, relative).Replace('\\', '/');
            string targetDirectory = Path.GetDirectoryName(targetAssetPath);

            if (!string.IsNullOrEmpty(targetDirectory))
            {
                EnsureAssetFolder(targetDirectory.Replace('\\', '/'));
            }

            string targetFullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                targetAssetPath
            );

            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath) ?? Directory.GetCurrentDirectory());
            File.Copy(sourceFile, targetFullPath, true);
            copied++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"ReStock: copied {copied} textures to {TexturesAssetRoot}");
    }

    [MenuItem(MenuRoot + "1b. Copy Pilot Textures from ReStock")]
    static void CopyPilotTexturesMenu()
    {
        int copied = CopyPilotTexturesFromReStock();
        AssetDatabase.Refresh();
        Debug.Log($"ReStock: copied {copied} pilot textures to {TexturesAssetRoot}");
    }

    static int CopyPilotTexturesFromReStock()
    {
        string sourceRoot = DefaultExternalModRoot;

        if (!Directory.Exists(sourceRoot))
        {
            Debug.LogWarning($"ReStock: ReStock source not found: {sourceRoot}");
            return 0;
        }

        EnsureAssetFolder(TexturesAssetRoot);

        string[] files = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => TextureExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        int copied = 0;

        for (int i = 0; i < files.Length; i++)
        {
            string sourceFile = files[i];
            string fileName = Path.GetFileNameWithoutExtension(sourceFile);

            if (!ShouldKeepPilotTexture(fileName))
            {
                continue;
            }

            string relative = GetRelativePathSafe(sourceRoot, sourceFile);
            string targetAssetPath = Path.Combine(TexturesAssetRoot, relative).Replace('\\', '/');
            string targetDirectory = Path.GetDirectoryName(targetAssetPath);

            if (!string.IsNullOrEmpty(targetDirectory))
            {
                EnsureAssetFolder(targetDirectory.Replace('\\', '/'));
            }

            string targetFullPath = Path.Combine(Directory.GetCurrentDirectory(), targetAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath) ?? Directory.GetCurrentDirectory());
            File.Copy(sourceFile, targetFullPath, true);
            copied++;
        }

        if (copied == 0)
        {
            Debug.LogWarning(
                $"ReStock: no pilot textures copied from {sourceRoot}. " +
                "Check DefaultExternalModRoot path in ReStockImportPipeline.cs."
            );
        }

        AssetDatabase.Refresh();
        return copied;
    }

    [MenuItem(MenuRoot + "2. Rebuild Part Materials from Textures")]
    internal static void RebuildPartMaterials()
    {
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesAssetRoot });

        if (textureGuids.Length == 0)
        {
            Debug.LogWarning($"ReStock: no textures found under {TexturesAssetRoot}");
            return;
        }

        Dictionary<string, List<TextureMapEntry>> groups = new Dictionary<string, List<TextureMapEntry>>(
            StringComparer.OrdinalIgnoreCase
        );

        for (int i = 0; i < textureGuids.Length; i++)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(texturePath);

            if (!TrySplitTextureName(fileName, out string materialKey, out string mapSuffix))
            {
                materialKey = fileName;
                mapSuffix = "_d";
            }

            if (!groups.TryGetValue(materialKey, out List<TextureMapEntry> entries))
            {
                entries = new List<TextureMapEntry>();
                groups.Add(materialKey, entries);
            }

            entries.Add(new TextureMapEntry(texturePath, mapSuffix));
        }

        foreach (KeyValuePair<string, List<TextureMapEntry>> pair in groups.ToList())
        {
            groups[pair.Key] = PreferPngTextureEntries(pair.Value);
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogError("ReStock: URP Lit shader not found.");
            return;
        }

        EnsureAssetFolder(MaterialsAssetRoot);

        // Group materials flat by basename; keep category subfolders only for textures.
        string materialsRoot = MaterialsAssetRoot;
        int created = 0;
        int updated = 0;
        int missingAlbedo = 0;
        List<string> missingAlbedoNames = new List<string>();

        foreach (KeyValuePair<string, List<TextureMapEntry>> pair in groups.OrderBy(entry => entry.Key))
        {
            string materialAssetPath = $"{materialsRoot}/{pair.Key}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialAssetPath);
                created++;
            }
            else
            {
                updated++;
            }

            material.shader = shader;
            ApplyTextureMaps(material, pair.Value);
            ResetMaterialSurfaceDefaults(material);

            if (!MaterialHasBaseMap(material))
            {
                TryAssignFallbackBaseMap(material, pair.Key);
            }

            if (!MaterialHasBaseMap(material))
            {
                missingAlbedo++;
                missingAlbedoNames.Add(pair.Key);
            }

            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Material sample = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsAssetRoot}/restock-mk1-pod-1.mat");

        if (sample != null)
        {
            Texture sampleTexture = MaterialHasBaseMap(sample) ? GetBaseMapTexture(sample) : null;
            Debug.Log(
                $"ReStock: sample restock-mk1-pod-1 albedo={(sampleTexture != null ? sampleTexture.name : "MISSING")}. " +
                $"Use materials in {MaterialsAssetRoot}."
            );

            if (sampleTexture == null)
            {
                Debug.LogWarning(
                    "ReStock: materials have no albedo — run step 1b (Copy Pilot Textures) then step 2 again."
                );
            }
        }
        else
        {
            Debug.Log(
                $"ReStock: materials created={created}, updated={updated}, missingAlbedo={missingAlbedo}, " +
                $"root={MaterialsAssetRoot}."
            );
        }

        if (textureGuids.Length == 0)
        {
            Debug.LogWarning($"ReStock: no textures under {TexturesAssetRoot} — run Copy Pilot Textures first.");
        }

        Debug.Log(
            $"ReStock: materials created={created}, updated={updated}, missingAlbedo={missingAlbedo}, " +
            $"root={MaterialsAssetRoot}."
        );

        if (missingAlbedoNames.Count > 0)
        {
            Debug.LogWarning(
                "ReStock: materials without albedo texture: " + string.Join(", ", missingAlbedoNames)
            );
        }
    }

    [MenuItem(MenuRoot + "5. Delete stale embedded Materials under Meshes")]
    static void DeleteStaleEmbeddedMaterials()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { MeshesAssetRoot });
        int deleted = 0;

        for (int i = 0; i < materialGuids.Length; i++)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[i]);

            if (
                materialPath.IndexOf("/Materials/", StringComparison.OrdinalIgnoreCase) < 0 ||
                !materialPath.StartsWith(MeshesAssetRoot, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            if (AssetDatabase.DeleteAsset(materialPath))
            {
                deleted++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log(
            $"ReStock: deleted {deleted} stale embedded materials under {MeshesAssetRoot}. " +
            $"Real textured materials live in {MaterialsAssetRoot}."
        );
    }

    [MenuItem(MenuRoot + "3. Assign Materials to Imported FBX")]
    static void AssignMaterialsToFbx()
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { MeshesAssetRoot });

        if (modelGuids.Length == 0)
        {
            Debug.LogWarning($"ReStock: no models found under {MeshesAssetRoot}");
            return;
        }

        int imported = 0;
        int skipped = 0;
        int missingMaterial = 0;
        List<string> missingSamples = new List<string>();

        for (int i = 0; i < modelGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(modelGuids[i]);

            if (!ShouldProcessFbx(assetPath))
            {
                skipped++;
                continue;
            }

            if (ResolvePrimaryMaterialForModel(assetPath) == null)
            {
                missingMaterial++;

                if (missingSamples.Count < 8)
                {
                    missingSamples.Add(Path.GetFileName(assetPath));
                }

                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (importer == null)
            {
                missingMaterial++;
                continue;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            EditorUtility.SetDirty(importer);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            imported++;
        }

        AssetDatabase.Refresh();

        Debug.Log(
            $"ReStock: imported {imported} FBX with per-slot materials from {MaterialsAssetRoot}, " +
            $"skipped={skipped}, missingMaterial={missingMaterial}."
        );

        if (missingSamples.Count > 0)
        {
            Debug.LogWarning(
                "ReStock: no .mat in Materials/Parts for (examples): " + string.Join(", ", missingSamples)
            );
        }
    }

    static bool ShouldProcessFbx(string assetPath)
    {
        if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (assetPath.IndexOf("/DepthMasks/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return true;
    }

    [MenuItem(MenuRoot + "4. Clean KSP Variant Meshes (Selected)")]
    static void CleanVariantMeshesOnSelection()
    {
        GameObject[] selectedRoots = Selection.gameObjects;

        if (selectedRoots.Length == 0)
        {
            Debug.LogWarning("ReStock: select one or more part roots in the scene or Project.");
            return;
        }

        int cleaned = 0;

        for (int i = 0; i < selectedRoots.Length; i++)
        {
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedRoots[i]);
            ApplySimpleVabPartSetup(selectedRoots[i], assetPath);
            cleaned++;
        }

        Debug.Log($"ReStock: cleaned variant meshes on {cleaned} selected object(s).");
    }

    internal static bool IsPilotModelAsset(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalized = path.Replace('\\', '/');

        return ReStockBatchParts.IsBatchPartMeshAsset(normalized);
    }

    internal static bool IsPilotEngineSubmeshAsset(string normalizedPath) =>
        ReStockBatchParts.IsSplitSubmeshAsset(normalizedPath);

    internal static bool IsPilotFbxAsset(string path) => IsPilotModelAsset(path);

    internal static void ForceReimportPilotAndRebuild()
    {
        ReStockBatchParts.RefreshDefinitions();
        ReStockPartImporter.ImportDefinitions(ReStockBatchParts.Definitions, true);
    }

    internal static void ImportSelectedParts(
        IEnumerable<ReStockBatchParts.PartDefinition> definitions,
        bool preserveAuthoredMarkers
    )
    {
        ReStockPartImporter.ImportDefinitions(definitions, preserveAuthoredMarkers);
    }

    internal static void ImportModelAssetIfPresent(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        string normalized = assetPath.Replace('\\', '/');
        string fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));

        if (!File.Exists(fullPath))
        {
            return;
        }

        AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceUpdate);
    }

    static void DeleteLegacyPilotModelFiles()
    {
        int deleted = 0;

        for (int i = 0; i < PilotLegacyModelAssetPaths.Length; i++)
        {
            if (AssetDatabase.DeleteAsset(PilotLegacyModelAssetPaths[i]))
            {
                deleted++;
            }
        }

        if (deleted > 0)
        {
            Debug.Log($"ReStock: deleted {deleted} legacy pilot FBX/GLB file(s). Use OBJ exports.");
        }
    }

    static void DeleteLegacyPilotFbxFiles() => DeleteLegacyPilotModelFiles();

    [MenuItem(MenuRoot + "6. Build Pilot Part Prefabs (Batch)")]
    internal static void BuildPilotPartPrefabs()
    {
        ReStockBatchParts.RefreshDefinitions();
        ReStockPartImporter.ImportDefinitions(ReStockBatchParts.Definitions, true);
    }

    internal static bool BuildEngineTerrierPrefab()
    {
        ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition("restock-engine-terrier-1");

        if (definition == null)
        {
            return false;
        }

        ReStockBatchParts.MergeManifest(definition);
        return BuildSplitMeshPartPrefab(definition);
    }

    internal static bool BuildSplitMeshPartPrefab(ReStockBatchParts.PartDefinition definition)
    {
        if (definition == null || !definition.SplitSubmeshes)
        {
            return false;
        }

        return ReStockPartImporter.ImportDefinition(definition, true);
    }

    internal static bool TryCreateEnginePieceFromObj(string assetPath, string meshName, out GameObject piece)
    {
        piece = null;

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        ImportModelAssetIfPresent(assetPath);

        GameObject importedRoot = LoadImportedModelRoot(assetPath);

        if (importedRoot != null)
        {
            piece = UnityEngine.Object.Instantiate(importedRoot);
            piece.name = meshName;

            Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].gameObject.name = meshName;
            }

            return HasRenderableMesh(piece);
        }

        Mesh importedMesh = LoadImportedMesh(assetPath);

        if (importedMesh == null)
        {
            return false;
        }

        piece = new GameObject(meshName);
        MeshFilter meshFilter = piece.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = importedMesh;
        piece.AddComponent<MeshRenderer>();
        return true;
    }

    internal static GameObject LoadImportedModelRoot(string assetPath)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

        if (root != null)
        {
            return root;
        }

        UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

        if (mainAsset is GameObject mainGameObject)
        {
            return mainGameObject;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is GameObject candidate)
            {
                return candidate;
            }
        }

        return null;
    }

    static Mesh LoadImportedMesh(string assetPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Mesh mesh)
            {
                return mesh;
            }
        }

        return null;
    }

    static bool HasRenderableMesh(GameObject root)
    {
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i].sharedMesh != null)
            {
                return true;
            }
        }

        return false;
    }

    static bool ValidateImportedModelHasMeshes(GameObject modelRoot, string modelAssetPath)
    {
        MeshFilter[] meshFilters = modelRoot.GetComponentsInChildren<MeshFilter>(true);
        int meshCount = 0;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i].sharedMesh != null)
            {
                meshCount++;
            }
        }

        if (meshCount > 0)
        {
            return true;
        }

        Debug.LogError(
            $"ReStock pilot: imported model has no meshes: {modelAssetPath}. " +
            "Re-run Blender export_pilot_parts.py (OBJ), then ReStock -> 9."
        );
        return false;
    }

    [MenuItem(MenuRoot + "7. Keep Pilot Parts Only (Cleanup)")]
    static void KeepPilotPartsOnlyMenu()
    {
        if (
            !EditorUtility.DisplayDialog(
                "ReStock cleanup",
                "Delete all part FBX, materials and textures except the 6 batch-exported parts?",
                "Delete extras",
                "Cancel"
            )
        )
        {
            return;
        }

        KeepPilotPartsOnly(silent: false);
    }

    static void KeepPilotPartsOnly(bool silent)
    {
        HashSet<string> keepModels = BuildBatchKeepModelPaths();
        HashSet<string> keepMaterials = new HashSet<string>(PilotMaterialNames, StringComparer.OrdinalIgnoreCase);

        int deletedFbx = DeleteAssetsExcept(
            AssetDatabase.FindAssets("t:Model", new[] { MeshesAssetRoot }),
            assetPath =>
            {
                string normalized = assetPath.Replace('\\', '/');

                if (keepModels.Contains(normalized))
                {
                    return true;
                }

                return ReStockBatchParts.IsSplitSubmeshAsset(normalized);
            }
        );

        for (int i = 0; i < PilotLegacyModelAssetPaths.Length; i++)
        {
            if (AssetDatabase.DeleteAsset(PilotLegacyModelAssetPaths[i]))
            {
                deletedFbx++;
            }
        }

        int deletedMaterials = DeleteAssetsExcept(
            AssetDatabase.FindAssets("t:Material", new[] { MaterialsAssetRoot }),
            assetPath =>
            {
                string name = Path.GetFileNameWithoutExtension(assetPath);

                return keepMaterials.Contains(name);
            }
        );

        int deletedTextures = 0;
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesAssetRoot });

        for (int i = 0; i < textureGuids.Length; i++)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(texturePath);

            if (ShouldKeepPilotTexture(fileName))
            {
                continue;
            }

            if (AssetDatabase.DeleteAsset(texturePath))
            {
                deletedTextures++;
            }
        }

        int deletedEmbedded = DeleteEmbeddedMeshMaterials();

        DeleteEmptyTextureCategoryFolders();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!silent)
        {
            Debug.Log(
                $"ReStock cleanup: deleted fbx={deletedFbx}, materials={deletedMaterials}, " +
                $"textures={deletedTextures}, embedded={deletedEmbedded}. Kept batch OBJ meshes."
            );
        }
    }

    static int DeleteEmbeddedMeshMaterials()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { MeshesAssetRoot });
        int deleted = 0;

        for (int i = 0; i < materialGuids.Length; i++)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[i]);

            if (!materialPath.Contains("/Materials/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (AssetDatabase.DeleteAsset(materialPath))
            {
                deleted++;
            }
        }

        return deleted;
    }

    static void DeleteEmptyTextureCategoryFolders()
    {
        string[] keepCategories = { "Command", "FuelTank", "Engine" };
        string texturesFullPath = Path.Combine(Directory.GetCurrentDirectory(), TexturesAssetRoot);

        if (!Directory.Exists(texturesFullPath))
        {
            return;
        }

        string[] directories = Directory.GetDirectories(texturesFullPath);

        for (int i = 0; i < directories.Length; i++)
        {
            string directoryName = Path.GetFileName(directories[i]);

            if (keepCategories.Contains(directoryName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            string assetFolder = $"{TexturesAssetRoot}/{directoryName}";

            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                AssetDatabase.DeleteAsset(assetFolder);
            }
        }
    }

    static bool ShouldKeepPilotTexture(string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return false;
        }

        if (!TrySplitTextureName(fileNameWithoutExtension, out string materialKey, out _))
        {
            materialKey = fileNameWithoutExtension;
        }

        for (int i = 0; i < PilotTextureSourcePatterns.Length; i++)
        {
            string pattern = PilotTextureSourcePatterns[i];

            if (
                materialKey.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                materialKey.Equals(pattern, StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    static int DeleteAssetsExcept(string[] guids, Func<string, bool> shouldKeep)
    {
        int deleted = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');

            if (shouldKeep(assetPath))
            {
                continue;
            }

            if (AssetDatabase.DeleteAsset(assetPath))
            {
                deleted++;
            }
        }

        return deleted;
    }

    static void ApplyTextureMaps(Material material, List<TextureMapEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            TextureMapEntry entry = entries[i];
            Texture2D texture = LoadTextureAsset(entry.TexturePath);

            if (texture == null)
            {
                continue;
            }

            if (!MapSuffixToShaderProperty.TryGetValue(entry.MapSuffix, out string propertyName))
            {
                propertyName = "_BaseMap";
            }

            propertyName = ResolveShaderTextureProperty(material, propertyName);

            if (propertyName == null)
            {
                continue;
            }

            material.SetTexture(propertyName, texture);

            if (propertyName == "_BaseMap" || propertyName == "_MainTex")
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", Color.white);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", Color.white);
                }
            }

            if (propertyName == "_BumpMap" && material.HasProperty("_BumpScale"))
            {
                if (PilotAssignNormalMaps)
                {
                    material.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    // KSP/ReStock normal maps are DXTnm (red-heavy) until converted for URP.
                    material.SetTexture("_BumpMap", null);
                    material.DisableKeyword("_NORMALMAP");
                }
            }

            if (propertyName == "_EmissionMap" && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.white);
            }
        }
    }

    static void ResetMaterialSurfaceDefaults(Material material)
    {
        if (material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", null);
        }

        if (material.HasProperty("_SpecGlossMap"))
        {
            material.SetTexture("_SpecGlossMap", null);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.35f);
        }
    }

    static List<TextureMapEntry> PreferPngTextureEntries(List<TextureMapEntry> entries)
    {
        Dictionary<string, TextureMapEntry> bestBySuffix = new Dictionary<string, TextureMapEntry>(
            StringComparer.OrdinalIgnoreCase
        );

        for (int i = 0; i < entries.Count; i++)
        {
            TextureMapEntry entry = entries[i];

            if (
                !bestBySuffix.TryGetValue(entry.MapSuffix, out TextureMapEntry existing) ||
                PreferTexturePath(entry.TexturePath, existing.TexturePath)
            )
            {
                bestBySuffix[entry.MapSuffix] = entry;
            }
        }

        return bestBySuffix.Values.ToList();
    }

    static bool PreferTexturePath(string candidatePath, string existingPath)
    {
        bool candidateIsPng = candidatePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        bool existingIsPng = existingPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        if (candidateIsPng && !existingIsPng)
        {
            return true;
        }

        return false;
    }

    static bool IsPilotMaterialAsset(Material material)
    {
        if (material == null)
        {
            return false;
        }

        for (int i = 0; i < PilotMaterialNames.Length; i++)
        {
            if (material.name.Equals(PilotMaterialNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static Texture2D LoadTextureAsset(string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath))
        {
            return null;
        }

        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
    }

    static string ResolveShaderTextureProperty(Material material, string propertyName)
    {
        if (material.HasProperty(propertyName))
        {
            return propertyName;
        }

        if (propertyName == "_BaseMap" && material.HasProperty("_MainTex"))
        {
            return "_MainTex";
        }

        if (propertyName == "_BumpMap" && material.HasProperty("_BumpMap"))
        {
            return "_BumpMap";
        }

        if (propertyName == "_MetallicGlossMap" && material.HasProperty("_SpecGlossMap"))
        {
            return "_SpecGlossMap";
        }

        return null;
    }

    static Texture GetBaseMapTexture(Material material)
    {
        if (material.HasProperty("_BaseMap"))
        {
            Texture texture = material.GetTexture("_BaseMap");

            if (texture != null)
            {
                return texture;
            }
        }

        if (material.HasProperty("_MainTex"))
        {
            return material.GetTexture("_MainTex");
        }

        return null;
    }

    static bool MaterialHasBaseMap(Material material)
    {
        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
        {
            return true;
        }

        return material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null;
    }

    static void TryAssignFallbackBaseMap(Material material, string materialKey)
    {
        Texture2D texture = FindTextureByMaterialKey(materialKey);

        if (texture == null)
        {
            return;
        }

        string propertyName = ResolveShaderTextureProperty(material, "_BaseMap");

        if (propertyName == null)
        {
            return;
        }

        material.SetTexture(propertyName, texture);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }
    }

    static Texture2D FindTextureByMaterialKey(string materialKey)
    {
        string[] guids = AssetDatabase.FindAssets($"{materialKey} t:Texture2D", new[] { TexturesAssetRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(texturePath);

            if (!fileName.Equals(materialKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    static bool TrySplitTextureName(string fileName, out string materialKey, out string mapSuffix)
    {
        materialKey = fileName;
        mapSuffix = "_d";

        foreach (string suffix in MapSuffixToShaderProperty.Keys.OrderByDescending(key => key.Length))
        {
            if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            materialKey = fileName.Substring(0, fileName.Length - suffix.Length);
            mapSuffix = suffix;
            return true;
        }

        return false;
    }

    internal static Material ResolveMaterialForFbxSlot(string slotName, string modelAssetPath, int slotIndex)
    {
        return ResolveMaterialForSourceSlot(slotName, modelAssetPath, slotIndex);
    }

    internal static Material ResolveMaterialForSourceSlot(string slotName, string modelAssetPath, int slotIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            return null;
        }

        string partId = GetPartIdFromModelAssetPath(modelAssetPath);
        ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition(partId);

        if (
            definition != null &&
            definition.SlotMaterialOverrides.TryGetValue(slotName, out string slotOverride)
        )
        {
            Material configuredSlot = TryLoadMaterialExact(slotOverride);

            if (configuredSlot != null)
            {
                return configuredSlot;
            }
        }

        Material exact = TryLoadMaterialExact(slotName);

        if (exact != null)
        {
            return exact;
        }

        if (MaterialSlotAliases.TryGetValue(slotName, out string aliasName))
        {
            Material aliased = TryLoadMaterialExact(aliasName);

            if (aliased != null)
            {
                return aliased;
            }
        }

        if (IsGenericMaterialSlotName(slotName))
        {
            string fbxBaseName = Path.GetFileNameWithoutExtension(modelAssetPath);
            string materialBaseName = GetMaterialBaseName(fbxBaseName);
            Material bySlotIndex = TryLoadMaterialExact($"{materialBaseName}-{slotIndex + 1}");

            if (bySlotIndex != null)
            {
                return bySlotIndex;
            }

            return ResolvePrimaryMaterialForModel(modelAssetPath);
        }

        return null;
    }

    internal static Material ResolvePrimaryMaterialForModel(string modelAssetPath)
    {
        string partId = GetPartIdFromModelAssetPath(modelAssetPath);
        ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition(partId);

        if (
            definition != null &&
            !string.IsNullOrWhiteSpace(definition.PrimaryMaterialOverride)
        )
        {
            Material fromConfig = TryLoadMaterialExact(definition.PrimaryMaterialOverride);

            if (fromConfig != null)
            {
                return fromConfig;
            }
        }

        string fbxBaseName = GetFbxBaseName(modelAssetPath);

        if (PilotPrimaryMaterialOverrides.TryGetValue(fbxBaseName, out string overrideName))
        {
            Material overridden = TryLoadMaterialExact(overrideName);

            if (overridden != null)
            {
                return overridden;
            }
        }

        string variantSuffix = $"-{DefaultMaterialVariantIndex}";

        foreach (string candidate in GetMaterialNameCandidates(fbxBaseName))
        {
            Material exact = TryLoadMaterialExact(candidate);

            if (exact != null)
            {
                return exact;
            }

            if (!candidate.EndsWith(variantSuffix, StringComparison.OrdinalIgnoreCase))
            {
                Material withVariant = TryLoadMaterialExact($"{candidate}{variantSuffix}");

                if (withVariant != null)
                {
                    return withVariant;
                }
            }
        }

        return FindBestVariantOneMaterial(GetMaterialBaseName(fbxBaseName));
    }

    static IEnumerable<string> GetMaterialNameCandidates(string fbxBaseName)
    {
        string materialBaseName = GetMaterialBaseName(fbxBaseName);
        HashSet<string> yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        List<string> ordered = new List<string>
        {
            $"{materialBaseName}-{DefaultMaterialVariantIndex}",
            fbxBaseName,
            materialBaseName,
        };

        Match kspMidIndex = Regex.Match(
            fbxBaseName,
            @"^(?<prefix>.+)-(?<index>\d+)-(?<tail>[a-zA-Z][a-zA-Z0-9]*)$"
        );

        if (kspMidIndex.Success)
        {
            string swapped =
                $"{kspMidIndex.Groups["prefix"].Value}-{kspMidIndex.Groups["tail"].Value}-{kspMidIndex.Groups["index"].Value}";
            ordered.Add(swapped);
            ordered.Add($"{swapped}-{DefaultMaterialVariantIndex}");
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            string value = ordered[i];

            if (!string.IsNullOrWhiteSpace(value) && yielded.Add(value))
            {
                yield return value;
            }
        }
    }

    static Material FindBestVariantOneMaterial(string materialBaseName)
    {
        if (string.IsNullOrWhiteSpace(materialBaseName))
        {
            return null;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsAssetRoot });
        Material bestMatch = null;
        string variantOneSuffix = $"-{DefaultMaterialVariantIndex}";

        for (int i = 0; i < guids.Length; i++)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            string materialName = Path.GetFileNameWithoutExtension(materialPath);

            if (materialName.Equals($"{materialBaseName}{variantOneSuffix}", StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }

            if (
                !materialName.StartsWith(materialBaseName, StringComparison.OrdinalIgnoreCase) ||
                !materialName.EndsWith(variantOneSuffix, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            Material candidate = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (candidate != null && (bestMatch == null || materialName.Length < bestMatch.name.Length))
            {
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }

    static string GetMaterialBaseName(string fbxBaseName)
    {
        if (EndsWithMaterialIndex(fbxBaseName))
        {
            return fbxBaseName.Substring(0, fbxBaseName.LastIndexOf('-'));
        }

        return fbxBaseName;
    }

    static bool EndsWithMaterialIndex(string value)
    {
        return Regex.IsMatch(value, @"-\d+$");
    }

    static bool IsGenericMaterialSlotName(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            return true;
        }

        return slotName.StartsWith("Material", StringComparison.OrdinalIgnoreCase)
            || slotName.Equals("Default-Material", StringComparison.OrdinalIgnoreCase)
            || slotName.Equals("no name", StringComparison.OrdinalIgnoreCase);
    }

    static Material FindPrimaryMaterialForFbx(string modelAssetPath)
    {
        return ResolvePrimaryMaterialForModel(modelAssetPath);
    }

    static Material TryLoadMaterialExact(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsAssetRoot}/{materialName}.mat");

        if (material != null)
        {
            return material;
        }

        if (MaterialSlotAliases.TryGetValue(materialName, out string aliasName))
        {
            material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsAssetRoot}/{aliasName}.mat");

            if (material != null)
            {
                return material;
            }
        }

        // KSP/ReStock albedo atlases often use a trailing "-1" (e.g. restock-engine-puff-ant-1.png)
        // while Blender material slots omit it (restock-engine-puff-ant).
        string variantSuffix = $"-{DefaultMaterialVariantIndex}";

        if (!materialName.EndsWith(variantSuffix, StringComparison.OrdinalIgnoreCase))
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(
                $"{MaterialsAssetRoot}/{materialName}{variantSuffix}.mat"
            );
        }

        return material;
    }

    internal static void ApplySimpleVabPartSetup(
        GameObject root,
        string fbxAssetPath,
        ReStockBatchParts.PartDefinition definitionOverride = null
    )
    {
        if (root == null || string.IsNullOrWhiteSpace(fbxAssetPath))
        {
            return;
        }

        string fbxBaseName = GetFbxBaseName(fbxAssetPath);
        bool isContainerPrefab = PartPrefabBuilder.HasAuthoredNodes(root);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        List<string> keptLog = new List<string>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            bool isRoot = renderer.transform == root.transform;

            if (isRoot && renderer.GetComponent<MeshFilter>() == null)
            {
                continue;
            }

            if (renderer.GetComponent<AttachmentNodeMarker>() != null)
            {
                continue;
            }

            string normalizedMeshName = GetNormalizedRendererMeshName(renderer);

            if (ShouldDisableBatchAttachment(fbxBaseName, renderer, definitionOverride))
            {
                renderer.enabled = false;
                keptLog.Add($"{renderer.gameObject.name}->DISABLED");
                continue;
            }

            if (!isContainerPrefab && ShouldHideForSimpleVab(normalizedMeshName, fbxBaseName, renderers))
            {
                renderer.enabled = false;
                continue;
            }

            renderer.enabled = true;
            renderer.gameObject.SetActive(true);
            string materialMeshKey = ResolvePilotMeshMaterialKey(renderer, fbxBaseName);
            ApplyPilotMaterialsToRenderer(renderer, fbxAssetPath, materialMeshKey);
            keptLog.Add($"{materialMeshKey}->{GetAppliedMaterialName(renderer)}");
        }

        Debug.Log($"ReStock: setup {fbxBaseName} -> {string.Join(", ", keptLog)}");
    }

    internal static void ApplyContainerPrefabMaterials(
        GameObject root,
        string modelAssetPath,
        ReStockBatchParts.PartDefinition definitionOverride = null
    )
    {
        if (root == null || string.IsNullOrWhiteSpace(modelAssetPath))
        {
            return;
        }

        EnableContainerMeshRenderers(root);
        ApplySimpleVabPartSetup(root, modelAssetPath, definitionOverride);
        ForceContainerRendererMaterials(root, modelAssetPath);
    }

    static void EnableContainerMeshRenderers(GameObject root)
    {
        if (!PartPrefabBuilder.HasAuthoredNodes(root))
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || renderer.GetComponent<AttachmentNodeMarker>() != null)
            {
                continue;
            }

            renderer.enabled = true;
            renderer.gameObject.SetActive(true);
        }
    }

    static void ForceContainerRendererMaterials(GameObject root, string modelAssetPath)
    {
        Material primaryMaterial = ResolvePrimaryMaterialForModel(modelAssetPath);
        string fbxBaseName = GetFbxBaseName(modelAssetPath);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        int attachmentNodeLayer = LayerMask.NameToLayer(VabAttachmentNodeLayers.LayerName);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (
                renderer == null ||
                !renderer.enabled ||
                renderer.GetComponent<AttachmentNodeMarker>() != null
            )
            {
                continue;
            }

            if (attachmentNodeLayer >= 0 && renderer.gameObject.layer == attachmentNodeLayer)
            {
                continue;
            }

            string materialMeshKey = ResolvePilotMeshMaterialKey(renderer, fbxBaseName);
            ApplyPilotMaterialsToRenderer(renderer, modelAssetPath, materialMeshKey);

            if (
                !RendererHasAuthoredMaterial(renderer) &&
                !IsSplitSubmeshPart(fbxBaseName)
            )
            {
                Material meshMaterial = ResolveMaterialForMeshName(materialMeshKey, modelAssetPath);

                if (meshMaterial == null)
                {
                    meshMaterial = primaryMaterial;
                }

                if (meshMaterial != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    Material[] filled = new Material[Mathf.Max(1, materials.Length)];

                    for (int slot = 0; slot < filled.Length; slot++)
                    {
                        filled[slot] = meshMaterial;
                    }

                    renderer.sharedMaterials = filled;
                }
            }
        }
    }

    static bool RendererHasAuthoredMaterial(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        Material[] materials = renderer.sharedMaterials;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];

            if (material == null || IsPlaceholderMaterial(material))
            {
                return false;
            }
        }

        return materials.Length > 0;
    }

    static bool IsPlaceholderMaterial(Material material)
    {
        if (material == null)
        {
            return true;
        }

        string materialName = material.name ?? string.Empty;

        return materialName.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0 ||
            materialName.Equals("Lit", StringComparison.OrdinalIgnoreCase) ||
            materialName.StartsWith("Default-Material", StringComparison.OrdinalIgnoreCase);
    }

    static string GetNormalizedRendererMeshName(Renderer renderer)
    {
        if (renderer == null)
        {
            return string.Empty;
        }

        string objectName = NormalizeMeshName(renderer.gameObject.name);
        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            string meshName = NormalizeMeshName(meshFilter.sharedMesh.name);

            if (!string.IsNullOrWhiteSpace(meshName) && !IsGenericImportedMeshName(meshName))
            {
                return meshName;
            }
        }

        return objectName;
    }

    static bool IsGenericImportedMeshName(string normalizedMeshName)
    {
        return normalizedMeshName.Equals("default", StringComparison.OrdinalIgnoreCase)
            || normalizedMeshName.Equals("mesh", StringComparison.OrdinalIgnoreCase)
            || normalizedMeshName.Equals("object", StringComparison.OrdinalIgnoreCase);
    }

    static string ResolvePilotMeshMaterialKey(Renderer renderer, string modelBaseName)
    {
        if (renderer == null)
        {
            return string.Empty;
        }

        string normalizedMeshName = GetNormalizedRendererMeshName(renderer);

        if (
            !IsGenericImportedMeshName(normalizedMeshName) &&
            !IsPartRootMeshName(normalizedMeshName, modelBaseName)
        )
        {
            return normalizedMeshName;
        }

        string fromObjectName = TryMatchPilotKeepMesh(renderer.gameObject.name, modelBaseName);

        if (!string.IsNullOrWhiteSpace(fromObjectName))
        {
            return fromObjectName;
        }

        string fromHierarchy = TryMatchPilotKeepMeshFromHierarchy(renderer.transform, modelBaseName);

        if (!string.IsNullOrWhiteSpace(fromHierarchy))
        {
            return fromHierarchy;
        }

        string fromPrefabSource = TryMatchPilotKeepMeshFromPrefabSource(renderer, modelBaseName);

        if (!string.IsNullOrWhiteSpace(fromPrefabSource))
        {
            return fromPrefabSource;
        }

        string fromMeshAsset = TryMatchPilotKeepMeshFromMeshAsset(renderer, modelBaseName);

        if (!string.IsNullOrWhiteSpace(fromMeshAsset))
        {
            return fromMeshAsset;
        }

        return ResolvePilotMeshMaterialKeyFromPartId(normalizedMeshName, modelBaseName);
    }

    static string ResolvePilotMeshMaterialKeyFromPartId(string normalizedMeshName, string modelBaseName)
    {
        if (!IsGenericImportedMeshName(normalizedMeshName))
        {
            return normalizedMeshName;
        }

        string mappedKey = MapGenericMeshToPilotKey(modelBaseName);

        if (!string.IsNullOrWhiteSpace(mappedKey))
        {
            return mappedKey;
        }

        return normalizedMeshName;
    }

    static bool IsPartRootMeshName(string normalizedMeshName, string modelBaseName)
    {
        return !string.IsNullOrWhiteSpace(normalizedMeshName)
            && !string.IsNullOrWhiteSpace(modelBaseName)
            && normalizedMeshName.Equals(modelBaseName, StringComparison.OrdinalIgnoreCase);
    }

    static string TryMatchPilotKeepMeshFromHierarchy(Transform transform, string modelBaseName)
    {
        Transform current = transform != null ? transform.parent : null;

        while (current != null)
        {
            string match = TryMatchPilotKeepMesh(current.name, modelBaseName);

            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }

            current = current.parent;
        }

        return string.Empty;
    }

    internal static string GetRendererPilotMeshKey(Renderer renderer, string partId)
    {
        return ResolvePilotMeshMaterialKey(renderer, partId);
    }

    static string TryMatchPilotKeepMeshFromPrefabSource(Renderer renderer, string modelBaseName)
    {
        if (renderer == null)
        {
            return string.Empty;
        }

        string sourcePrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);

        if (!string.IsNullOrWhiteSpace(sourcePrefabPath))
        {
            string fromSourceStem = TryMatchPilotKeepMesh(
                Path.GetFileNameWithoutExtension(sourcePrefabPath),
                modelBaseName
            );

            if (!string.IsNullOrWhiteSpace(fromSourceStem))
            {
                return fromSourceStem;
            }
        }

        return string.Empty;
    }

    internal static bool TryGetRendererMeshAssetGuid(Renderer renderer, out string meshAssetGuid)
    {
        meshAssetGuid = string.Empty;

        if (renderer == null)
        {
            return false;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        string assetPath = string.Empty;

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            assetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
        }

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
        }

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        meshAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
        return !string.IsNullOrWhiteSpace(meshAssetGuid);
    }

    static string TryMatchPilotKeepMesh(string candidateName, string modelBaseName)
    {
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return string.Empty;
        }

        string normalizedCandidate = NormalizeMeshName(candidateName);

        if (IsGenericImportedMeshName(normalizedCandidate))
        {
            return string.Empty;
        }

        if (TryParseMaterialSplitMeshName(normalizedCandidate, out string keepMeshName, out _))
        {
            if (!PilotKeepMeshWhitelist.TryGetValue(modelBaseName, out HashSet<string> keepMeshes))
            {
                return string.Empty;
            }

            foreach (string keepName in keepMeshes)
            {
                if (KeepMeshNamesMatch(keepMeshName, keepName))
                {
                    return normalizedCandidate;
                }
            }

            return string.Empty;
        }

        if (PilotMeshToMaterial.ContainsKey(normalizedCandidate))
        {
            return normalizedCandidate;
        }

        if (!PilotKeepMeshWhitelist.TryGetValue(modelBaseName, out HashSet<string> whitelist))
        {
            return string.Empty;
        }

        foreach (string keepName in whitelist)
        {
            if (KeepMeshNamesMatch(normalizedCandidate, keepName))
            {
                return keepName;
            }
        }

        return string.Empty;
    }

    static bool TryParseMaterialSplitMeshName(
        string normalizedMeshName,
        out string keepMeshName,
        out string materialKey
    )
    {
        keepMeshName = normalizedMeshName;
        materialKey = string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedMeshName))
        {
            return false;
        }

        int materialSuffixIndex = normalizedMeshName.IndexOf("__", StringComparison.Ordinal);

        if (materialSuffixIndex <= 0)
        {
            return false;
        }

        keepMeshName = normalizedMeshName.Substring(0, materialSuffixIndex);
        materialKey = normalizedMeshName.Substring(materialSuffixIndex + 2);
        return !string.IsNullOrWhiteSpace(keepMeshName) && !string.IsNullOrWhiteSpace(materialKey);
    }

    static string TryMatchPilotKeepMeshFromMeshAsset(Renderer renderer, string modelBaseName)
    {
        MeshFilter meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return string.Empty;
        }

        string fromMeshName = TryMatchPilotKeepMesh(meshFilter.sharedMesh.name, modelBaseName);

        if (!string.IsNullOrWhiteSpace(fromMeshName))
        {
            return fromMeshName;
        }

        string assetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return string.Empty;
        }

        string fileStem = Path.GetFileNameWithoutExtension(assetPath);
        return TryMatchPilotKeepMesh(fileStem, modelBaseName);
    }

    static bool KeepMeshNamesMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string compactLeft = left.Replace(" ", string.Empty).Replace("_", string.Empty);
        string compactRight = right.Replace(" ", string.Empty).Replace("_", string.Empty);

        return compactLeft.Equals(compactRight, StringComparison.OrdinalIgnoreCase);
    }

    static bool IsSplitSubmeshPart(string modelBaseName)
    {
        ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition(modelBaseName);

        if (definition == null)
        {
            return false;
        }

        ReStockBatchParts.MergeManifest(definition);
        return definition.SplitSubmeshes;
    }

    static string ResolvePilotMeshMaterialKey(string normalizedMeshName, string modelBaseName)
    {
        return ResolvePilotMeshMaterialKeyFromPartId(normalizedMeshName, modelBaseName);
    }

    static string MapGenericMeshToPilotKey(string modelBaseName)
    {
        if (string.IsNullOrWhiteSpace(modelBaseName))
        {
            return string.Empty;
        }

        if (modelBaseName.Equals("restock-fueltank-125-1", StringComparison.OrdinalIgnoreCase))
        {
            return "Tank800_A";
        }

        if (modelBaseName.Equals("restock-fueltank-0625-1", StringComparison.OrdinalIgnoreCase))
        {
            return "Tank0625";
        }

        if (modelBaseName.Equals("restock-mk1-pod", StringComparison.OrdinalIgnoreCase))
        {
            return "Mk1PodDark";
        }

        if (modelBaseName.Equals("restock-cupola-1", StringComparison.OrdinalIgnoreCase))
        {
            return "25mCupola 1";
        }

        return string.Empty;
    }

    static int CountMeshRenderers(Renderer[] renderers)
    {
        int count = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshFilter meshFilter = renderers[i].GetComponent<MeshFilter>();

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                count++;
            }
        }

        return count;
    }

    static bool ShouldDisableBatchAttachment(
        string partId,
        Renderer renderer,
        ReStockBatchParts.PartDefinition definitionOverride = null
    )
    {
        if (renderer == null)
        {
            return true;
        }

        ReStockBatchParts.PartDefinition definition = definitionOverride ?? ReStockBatchParts.FindDefinition(partId);

        if (definition == null)
        {
            return false;
        }

        if (definitionOverride == null)
        {
            ReStockBatchParts.MergeManifest(definition);
        }

        string meshName = GetNormalizedRendererMeshName(renderer);
        string objectName = NormalizeMeshName(renderer.gameObject.name);

        if (!definition.SplitSubmeshes)
        {
            return definition.AttachmentMeshes.Contains(meshName)
                || definition.AttachmentMeshes.Contains(objectName);
        }

        if (definition.EnabledMeshes.Count == 0)
        {
            return definition.AttachmentMeshes.Contains(meshName)
                || definition.AttachmentMeshes.Contains(objectName);
        }

        if (definition.EnabledMeshes.Contains(meshName) || definition.EnabledMeshes.Contains(objectName))
        {
            return false;
        }

        foreach (string enabledName in definition.EnabledMeshes)
        {
            if (
                meshName.StartsWith(enabledName + "__", StringComparison.OrdinalIgnoreCase)
                || objectName.StartsWith(enabledName + "__", StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }
        }

        return true;
    }

    static HashSet<string> BuildBatchKeepModelPaths()
    {
        HashSet<string> keepModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < ReStockBatchParts.Definitions.Length; i++)
        {
            ReStockBatchParts.PartDefinition definition = ReStockBatchParts.Definitions[i];

            if (definition.SplitSubmeshes)
            {
                continue;
            }

            keepModels.Add(ReStockBatchParts.GetSingleModelAssetPath(definition));
        }

        return keepModels;
    }

    static bool ShouldHideForSimpleVab(string normalizedMeshName, string fbxBaseName, Renderer[] allRenderers)
    {
        if (string.IsNullOrWhiteSpace(normalizedMeshName))
        {
            return true;
        }

        int meshRendererCount = CountMeshRenderers(allRenderers);

        if (
            PilotKeepMeshWhitelist.TryGetValue(fbxBaseName, out HashSet<string> keepMeshes)
        )
        {
            if (keepMeshes.Contains(normalizedMeshName))
            {
                return false;
            }

            // Unity OBJ import may rename meshes; keep sole mesh on multi-mesh parts too.
            if (meshRendererCount == 1)
            {
                return false;
            }

            return true;
        }

        return ShouldHideForSimpleVabLegacy(normalizedMeshName, fbxBaseName);
    }

    static bool ShouldHideForSimpleVabLegacy(string normalizedMeshName, string fbxBaseName)
    {
        if (string.IsNullOrWhiteSpace(normalizedMeshName))
        {
            return true;
        }

        if (
            PilotKeepMeshWhitelist.TryGetValue(fbxBaseName, out HashSet<string> keepMeshes)
        )
        {
            return !keepMeshes.Contains(normalizedMeshName);
        }

        if (
            normalizedMeshName.IndexOf("Flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedMeshName.IndexOf("Shroud", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedMeshName.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0
        )
        {
            return true;
        }

        if (fbxBaseName.Equals("restock-mk1-pod", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedMeshName.Equals("Mk1PodWhite", StringComparison.OrdinalIgnoreCase) ||
                normalizedMeshName.Equals("Mk1PodBlackWhite", StringComparison.OrdinalIgnoreCase);
        }

        if (fbxBaseName.Equals("restock-fueltank-0625-1", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedMeshName.Equals("Tank0625_White", StringComparison.OrdinalIgnoreCase);
        }

        if (fbxBaseName.Equals("restock-engine-terrier-1", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedMeshName.IndexOf("Frame_Boat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static void SimplifyPartHierarchyForVab(GameObject root, string fbxAssetPath)
    {
        if (root == null || string.IsNullOrWhiteSpace(fbxAssetPath))
        {
            return;
        }

        string fbxBaseName = GetFbxBaseName(fbxAssetPath);
        HashSet<string> keepMeshNames = BuildPilotKeepMeshSet(root, fbxBaseName);

        if (keepMeshNames.Count == 0)
        {
            Debug.LogWarning($"ReStock: no meshes selected to keep for {fbxAssetPath}");
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        List<string> keptLog = new List<string>();

        for (int i = renderers.Length - 1; i >= 0; i--)
        {
            Renderer renderer = renderers[i];

            if (renderer.transform == root.transform)
            {
                continue;
            }

            string normalizedMeshName = NormalizeMeshName(renderer.gameObject.name);

            if (!keepMeshNames.Contains(normalizedMeshName))
            {
                UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                continue;
            }

            renderer.enabled = true;
            renderer.gameObject.SetActive(true);
            ApplyPilotMaterialsToRenderer(renderer, fbxAssetPath, normalizedMeshName);
            keptLog.Add($"{renderer.gameObject.name}->{GetAppliedMaterialName(renderer)}");
        }

        Debug.Log($"ReStock: simplified {fbxBaseName} -> {string.Join(", ", keptLog)}");
    }

    static void FixPilotMeshUvForUnity(Renderer renderer)
    {
        if (!FlipPilotMeshUvV || renderer == null)
        {
            return;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        Mesh sourceMesh = meshFilter.sharedMesh;
        Mesh meshCopy = UnityEngine.Object.Instantiate(sourceMesh);
        meshCopy.name = sourceMesh.name + "_pilot";

        Vector2[] uvs = meshCopy.uv;

        if (uvs == null || uvs.Length == 0)
        {
            return;
        }

        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i].y = 1f - uvs[i].y;
        }

        meshCopy.uv = uvs;
        meshFilter.sharedMesh = meshCopy;
    }

    static HashSet<string> BuildPilotKeepMeshSet(GameObject root, string fbxBaseName)
    {
        HashSet<string> keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (PilotMultiKeepMeshes.TryGetValue(fbxBaseName, out string[] multiKeep))
        {
            for (int i = 0; i < multiKeep.Length; i++)
            {
                if (TryFindRendererByMeshName(renderers, multiKeep[i], out _))
                {
                    keep.Add(multiKeep[i]);
                }
            }

            return keep;
        }

        if (PilotSingleMeshPriority.TryGetValue(fbxBaseName, out string[] priority))
        {
            for (int candidateIndex = 0; candidateIndex < priority.Length; candidateIndex++)
            {
                if (TryFindRendererByMeshName(renderers, priority[candidateIndex], out _))
                {
                    keep.Add(priority[candidateIndex]);
                    return keep;
                }
            }
        }

        return keep;
    }

    static bool TryFindRendererByMeshName(Renderer[] renderers, string candidateName, out Renderer found)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            string normalizedMeshName = NormalizeMeshName(renderers[i].gameObject.name);

            if (MeshNameMatchesCandidate(normalizedMeshName, candidateName))
            {
                found = renderers[i];
                return true;
            }
        }

        found = null;
        return false;
    }

    static bool MeshNameMatchesCandidate(string normalizedMeshName, string candidateName)
    {
        return normalizedMeshName.Equals(candidateName, StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeMeshName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return objectName;
        }

        string normalized = objectName
            .Replace("^", string.Empty)
            .Replace("∧", string.Empty)
            .Trim();

        return Regex.Replace(normalized, @"[^\w]+$", string.Empty);
    }

    static bool ShouldAlwaysDeletePilotMesh(string normalizedMeshName)
    {
        for (int i = 0; i < PilotAlwaysDeleteMeshFragments.Length; i++)
        {
            if (normalizedMeshName.IndexOf(PilotAlwaysDeleteMeshFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    static void ApplyPilotMaterialsToRenderer(Renderer renderer, string fbxAssetPath, string normalizedMeshName)
    {
        Material mappedMeshMaterial = ResolveMaterialForMeshName(normalizedMeshName, fbxAssetPath);
        Material[] sourceMaterials = renderer.sharedMaterials;
        bool useMappedMeshMaterial =
            mappedMeshMaterial != null &&
            !string.IsNullOrWhiteSpace(normalizedMeshName) &&
            sourceMaterials.Length == 1 &&
            (
                PilotMeshToMaterial.ContainsKey(normalizedMeshName) ||
                TryParseMaterialSplitMeshName(normalizedMeshName, out _, out _) ||
                HasConfiguredMaterialOverride(normalizedMeshName, fbxAssetPath)
            );
        Material[] materials = new Material[Mathf.Max(1, sourceMaterials.Length)];

        for (int slot = 0; slot < materials.Length; slot++)
        {
            Material replacement = null;

            if (useMappedMeshMaterial)
            {
                replacement = mappedMeshMaterial;
            }
            else
            {
                string slotName = CleanMaterialSlotName(
                    slot < sourceMaterials.Length && sourceMaterials[slot] != null
                        ? sourceMaterials[slot].name
                        : null
                );

                replacement = ResolveMaterialForSourceSlot(slotName, fbxAssetPath, slot);

                if (replacement == null)
                {
                    replacement = mappedMeshMaterial;
                }

                if (replacement == null)
                {
                    replacement = ResolvePrimaryMaterialForModel(fbxAssetPath);
                }
            }

            materials[slot] = replacement;
        }

        renderer.sharedMaterials = materials;
    }

    static Material ResolveMaterialForMeshName(string normalizedMeshName, string fbxAssetPath)
    {
        string partId = GetPartIdFromModelAssetPath(fbxAssetPath);
        ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition(partId);

        if (
            definition != null &&
            !string.IsNullOrWhiteSpace(normalizedMeshName) &&
            definition.MaterialOverrides.TryGetValue(normalizedMeshName, out string overrideMaterialName)
        )
        {
            Material overrideMaterial = TryLoadMaterialExact(overrideMaterialName);

            if (overrideMaterial != null)
            {
                return overrideMaterial;
            }

            if (MaterialSlotAliases.TryGetValue(overrideMaterialName, out string aliasedOverride))
            {
                overrideMaterial = TryLoadMaterialExact(aliasedOverride);

                if (overrideMaterial != null)
                {
                    return overrideMaterial;
                }
            }
        }

        if (
            !string.IsNullOrWhiteSpace(normalizedMeshName) &&
            PilotMeshToMaterial.TryGetValue(normalizedMeshName, out string materialName)
        )
        {
            Material material = TryLoadMaterialExact(materialName);

            if (material != null)
            {
                return material;
            }
        }

        string meshOverride = GetMeshMaterialOverride(normalizedMeshName, partId);

        if (!string.IsNullOrWhiteSpace(meshOverride))
        {
            return TryLoadMaterialExact(meshOverride);
        }

        if (
            definition != null &&
            !string.IsNullOrWhiteSpace(definition.PrimaryMaterialOverride)
        )
        {
            return TryLoadMaterialExact(definition.PrimaryMaterialOverride);
        }

        return null;
    }

    internal static string GetPartIdFromModelAssetPath(string modelAssetPath)
    {
        if (string.IsNullOrWhiteSpace(modelAssetPath))
        {
            return string.Empty;
        }

        string normalized = modelAssetPath.Replace('\\', '/').TrimEnd('/');

        if (normalized.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
        {
            string folderName = Path.GetFileName(Path.GetDirectoryName(normalized));
            return folderName ?? string.Empty;
        }

        return Path.GetFileName(normalized);
    }

    static string GetAppliedMaterialName(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;

        if (materials.Length == 0 || materials[0] == null)
        {
            return "MISSING";
        }

        return materials[0].name;
    }

    internal static void DisableKspPaintVariantMeshes(GameObject root, string modelAssetPath = null)
    {
        string fbxBaseName = GetFbxBaseName(modelAssetPath);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer.transform == root.transform)
            {
                continue;
            }

            if (!ShouldHideImportedMesh(renderer.gameObject.name, fbxBaseName))
            {
                continue;
            }

            renderer.enabled = false;
        }
    }

    internal static void ApplyPartMaterialsToGameObject(GameObject root, string fbxAssetPath)
    {
        string fbxBaseName = GetFbxBaseName(fbxAssetPath);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer.transform == root.transform)
            {
                continue;
            }

            if (ShouldHideImportedMesh(renderer.gameObject.name, fbxBaseName))
            {
                renderer.enabled = false;
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            string meshName = renderer.gameObject.name;

            for (int slot = 0; slot < materials.Length; slot++)
            {
                string slotName = CleanMaterialSlotName(materials[slot] != null ? materials[slot].name : null);
                Material replacement = ResolveMaterialForMeshSlot(slotName, fbxAssetPath, meshName, slot);

                if (replacement != null)
                {
                    materials[slot] = replacement;
                }
            }

            renderer.sharedMaterials = materials;
        }
    }

    static string GetFbxBaseName(string modelAssetPath)
    {
        if (string.IsNullOrWhiteSpace(modelAssetPath))
        {
            return null;
        }

        string fileName = Path.GetFileName(modelAssetPath.Replace('\\', '/'));
        return Path.GetFileNameWithoutExtension(fileName);
    }

    internal static Material ResolveMaterialForMeshSlot(
        string slotName,
        string modelAssetPath,
        string meshName,
        int slotIndex
    )
    {
        Material resolved = ResolveMaterialForSourceSlot(slotName, modelAssetPath, slotIndex);

        if (resolved != null)
        {
            return resolved;
        }

        string meshMaterialName = GetMeshMaterialOverride(meshName, GetPartIdFromModelAssetPath(modelAssetPath));

        if (!string.IsNullOrWhiteSpace(meshMaterialName))
        {
            return TryLoadMaterialExact(meshMaterialName);
        }

        return null;
    }

    static bool HasConfiguredMaterialOverride(string normalizedMeshName, string modelAssetPath)
    {
        string partId = GetPartIdFromModelAssetPath(modelAssetPath);
        ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition(partId);

        return definition != null &&
            !string.IsNullOrWhiteSpace(normalizedMeshName) &&
            definition.MaterialOverrides.ContainsKey(normalizedMeshName);
    }

    static string GetMeshMaterialOverride(string meshName, string partId = null)
    {
        if (string.IsNullOrWhiteSpace(meshName))
        {
            return null;
        }

        string normalized = meshName.Replace("^", string.Empty).Trim();

        int materialSuffixIndex = normalized.IndexOf("__", StringComparison.Ordinal);

        if (materialSuffixIndex > 0)
        {
            string materialKey = normalized.Substring(materialSuffixIndex + 2);

            if (TryLoadMaterialExact(materialKey) != null)
            {
                return materialKey;
            }

            if (MaterialSlotAliases.TryGetValue(materialKey, out string aliasedMaterialKey))
            {
                return aliasedMaterialKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(partId))
        {
            ReStockBatchParts.PartDefinition definition = ReStockBatchParts.FindDefinition(partId);

            if (
                definition != null &&
                definition.MaterialOverrides.TryGetValue(normalized, out string configuredMaterial)
            )
            {
                return configuredMaterial;
            }
        }

        if (normalized.Equals("EngineCore", StringComparison.OrdinalIgnoreCase))
        {
            return "restock-engine-glow-basic-1";
        }

        if (normalized.Equals("Tank0625", StringComparison.OrdinalIgnoreCase))
        {
            return "restock-fueltank-0625-1";
        }

        if (normalized.Equals("Tank0625_White", StringComparison.OrdinalIgnoreCase))
        {
            return "restock-fueltank-0625-1-white";
        }

        if (
            normalized.IndexOf("Mk1Pod", StringComparison.OrdinalIgnoreCase) >= 0 &&
            normalized.IndexOf("Flag", StringComparison.OrdinalIgnoreCase) < 0
        )
        {
            return "restock-mk1-pod-1";
        }

        if (normalized.Equals("Tank800_A", StringComparison.OrdinalIgnoreCase))
        {
            return "restock-fueltank-125-1";
        }

        if (normalized.IndexOf("Cupola", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "restock-cupola-lab-1";
        }

        if (normalized.IndexOf("Hatch", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "restock-crewed-hatch-1";
        }

        if (
            normalized.IndexOf("CapBlack", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("CapFoil", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("Endcap", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("endcap-crewed", StringComparison.OrdinalIgnoreCase) >= 0
        )
        {
            return "restock-endcap-crewed-1";
        }

        if (normalized.IndexOf("fairingWhite", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "restock-engine-shroud-125-1";
        }

        if (normalized.IndexOf("fairingOrange", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "restock-engine-shroud-125-2";
        }

        if (normalized.IndexOf("fairing", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "restock-engine-shroud-125-1";
        }

        if (normalized.IndexOf("Shroud", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (normalized.IndexOf("2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "restock-engine-shroud-125-2";
            }

            return "restock-engine-shroud-125-1";
        }

        if (normalized.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "restock-engine-glow-basic-1";
        }

        return null;
    }

    static string CleanMaterialSlotName(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return materialName;
        }

        int instanceSuffix = materialName.IndexOf(" (Instance)", StringComparison.Ordinal);

        if (instanceSuffix > 0)
        {
            return materialName.Substring(0, instanceSuffix);
        }

        return materialName;
    }

    internal static bool ShouldHideImportedMesh(string objectName, string fbxBaseName = null)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        string normalized = objectName.Replace("^", string.Empty).Trim();

        if (normalized.IndexOf("Flag", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        for (int i = 0; i < KspHiddenMeshNameFragments.Length; i++)
        {
            if (normalized.IndexOf(KspHiddenMeshNameFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static void RemoveOverlappingVariantRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer primary = renderers[i];

            if (!primary.enabled || !primary.gameObject.activeInHierarchy)
            {
                continue;
            }

            for (int j = i + 1; j < renderers.Length; j++)
            {
                Renderer secondaryCandidate = renderers[j];

                if (!secondaryCandidate.enabled || !secondaryCandidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!BoundsOverlapSignificantly(primary.bounds, secondaryCandidate.bounds))
                {
                    continue;
                }

                bool primaryIsSecondaryOnly = UsesOnlySecondaryKspMaterials(primary);
                bool candidateIsSecondaryOnly = UsesOnlySecondaryKspMaterials(secondaryCandidate);

                if (primaryIsSecondaryOnly && !candidateIsSecondaryOnly)
                {
                    primary.enabled = false;
                    break;
                }

                if (candidateIsSecondaryOnly && !primaryIsSecondaryOnly)
                {
                    secondaryCandidate.enabled = false;
                }
            }
        }
    }

    static bool BoundsOverlapSignificantly(Bounds a, Bounds b)
    {
        if (!a.Intersects(b))
        {
            return false;
        }

        Vector3 min = Vector3.Max(a.min, b.min);
        Vector3 max = Vector3.Min(a.max, b.max);
        Vector3 overlap = max - min;

        if (overlap.x <= 0f || overlap.y <= 0f || overlap.z <= 0f)
        {
            return false;
        }

        float overlapVolume = overlap.x * overlap.y * overlap.z;
        float smallerVolume = Mathf.Min(a.size.x * a.size.y * a.size.z, b.size.x * b.size.y * b.size.z);

        if (smallerVolume <= Mathf.Epsilon)
        {
            return false;
        }

        return overlapVolume / smallerVolume >= 0.7f;
    }

    static bool UsesOnlySecondaryKspMaterials(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;

        if (materials.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];

            if (material == null)
            {
                return false;
            }

            string materialName = material.name;

            if (
                materialName.EndsWith("-2", StringComparison.OrdinalIgnoreCase) ||
                materialName.EndsWith("_2", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            return false;
        }

        return true;
    }

    static void EnsureAssetFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
        string leaf = Path.GetFileName(assetFolder);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureAssetFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }

    static string GetRelativePathSafe(string root, string fullPath)
    {
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedFull = Path.GetFullPath(fullPath);

        if (!normalizedFull.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(fullPath);
        }

        return normalizedFull.Substring(normalizedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    readonly struct TextureMapEntry
    {
        public readonly string TexturePath;
        public readonly string MapSuffix;

        public TextureMapEntry(string texturePath, string mapSuffix)
        {
            TexturePath = texturePath;
            MapSuffix = mapSuffix;
        }
    }
}

/// <summary>
/// Marks ReStock map suffixes on import (-n normal, -e emission).
/// </summary>
class ReStockTextureImportPostprocessor : AssetPostprocessor
{
    public override uint GetVersion() => 2;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Models/Parts/Textures/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        TextureImporter importer = (TextureImporter)assetImporter;

        if (fileName.EndsWith("-n", StringComparison.OrdinalIgnoreCase))
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            return;
        }

        importer.wrapMode = TextureWrapMode.Repeat;

        if (fileName.EndsWith("-e", StringComparison.OrdinalIgnoreCase))
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
        }
    }
}

/// <summary>
/// Sets model import options for ReStock meshes. Materials are applied when building prefabs.
/// </summary>
class ReStockModelImportPostprocessor : AssetPostprocessor
{
    public override uint GetVersion() => 16;

    static bool IsReStockMesh(string path)
    {
        if (!path.StartsWith("Assets/Models/Parts/Meshes/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.IndexOf("/DepthMasks/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase);
    }

    void OnPreprocessModel()
    {
        if (!IsReStockMesh(assetPath))
        {
            return;
        }

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.weldVertices = false;
        importer.bakeAxisConversion = false;
        importer.swapUVChannels = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;
    }

    void OnPostprocessModel(GameObject root)
    {
        if (!ReStockImportPipeline.IsPilotModelAsset(assetPath))
        {
            return;
        }

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            Mesh mesh = meshFilters[i].sharedMesh;

            if (mesh == null)
            {
                continue;
            }

            if (ReStockImportPipeline.FlipPilotMeshUvV)
            {
                FlipMeshUvV(mesh);
            }

            int uv2Count = mesh.uv2 != null ? mesh.uv2.Length : 0;
            Vector2 sampleUv = mesh.uv.Length > 0 ? mesh.uv[0] : Vector2.zero;
            Debug.Log(
                $"ReStock UV: {assetPath} / {meshFilters[i].name} mesh={mesh.name} " +
                $"uv0={mesh.uv.Length} uv2={uv2Count} sample=({sampleUv.x:F4},{sampleUv.y:F4}) " +
                $"scale={meshFilters[i].transform.localScale}"
            );
        }
    }

    static void FlipMeshUvV(Mesh mesh)
    {
        Vector2[] uvs = mesh.uv;

        if (uvs == null || uvs.Length == 0)
        {
            return;
        }

        // KSP/ReStock atlases sometimes tile outside 0..1 (e.g. large engine bells).
        // Flipping those UVs wraps into wrong atlas texels (yellow garbage in Unity).
        for (int i = 0; i < uvs.Length; i++)
        {
            if (uvs[i].y < -0.001f || uvs[i].y > 1.001f)
            {
                return;
            }
        }

        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i].y = 1f - uvs[i].y;
        }

        mesh.uv = uvs;
    }
}

#endif
