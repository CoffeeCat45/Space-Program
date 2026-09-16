#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

enum CustomPartReflectionKind
{
    Roughness = 0,
    Smoothness = 1,
}

enum CustomPartTextureRole
{
    Unknown = 0,
    Albedo = 1,
    Metallic = 2,
    Reflection = 3,
}

sealed class CustomPartImportRequest
{
    public string SourceObjFullPath = string.Empty;
    public string AlbedoFullPath = string.Empty;
    public string MetallicFullPath = string.Empty;
    public string ReflectionFullPath = string.Empty;
    public CustomPartReflectionKind ReflectionKind = CustomPartReflectionKind.Roughness;
    public string Category = PartDatabase.CategoryEngine;
    public string PartId = string.Empty;
    public string DisplayName = string.Empty;
    public float DryMass = 0.5f;
    public float FuelCapacity;
    public float MaxThrustVac;
    public float IspVacuum;
    public bool PreserveAuthoredMarkers = true;
}

sealed class CustomPartImportResult
{
    public bool Success;
    public string PrefabAssetPath = string.Empty;
    public string MaterialAssetPath = string.Empty;
    public bool ReusedMaterial;
    public string Message = string.Empty;
}

/// <summary>
/// Builds VAB container prefabs from original (non-KSP) OBJ + PBR PNGs.
/// Does not use the ReStock UV-flip or KSP material aliases.
/// </summary>
static class CustomPartImportPipeline
{
    internal const string CustomMeshesAssetRoot = "Assets/Models/Parts/Custom";
    internal const string CustomMaterialsAssetRoot = "Assets/Materials/Parts/Custom";
    internal const string SharedAtlasFolder = CustomMeshesAssetRoot + "/_shared";
    internal const int MaxMaterialNameLength = 48;
    const string VisualChildName = "Visual";
    const string AlbedoFileSuffix = "_albedo.png";
    const string MetallicFileSuffix = "_metallic.png";
    const string ReflectionFileSuffix = "_roughness.png";
    const string MaskFileSuffix = "_Mask.png";
    const float DefaultSmoothness = 0.35f;

    struct MarkerSnapshot
    {
        public string Name;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public int NodeIndex;
    }

    public static CustomPartImportResult Import(CustomPartImportRequest request)
    {
        CustomPartImportResult result = new CustomPartImportResult();

        if (request == null)
        {
            result.Message = "Import request is empty.";
            return result;
        }

        string objPath = NormalizeFullPath(request.SourceObjFullPath);

        if (string.IsNullOrWhiteSpace(objPath) || !File.Exists(objPath))
        {
            result.Message = "Select an .obj file.";
            return result;
        }

        string albedoPath = NormalizeFullPath(request.AlbedoFullPath);

        if (string.IsNullOrWhiteSpace(albedoPath) || !File.Exists(albedoPath))
        {
            result.Message = "Select an albedo / color PNG.";
            return result;
        }

        string category = NormalizeCategory(request.Category);
        string partId = SanitizePartId(
            string.IsNullOrWhiteSpace(request.PartId) ?
                Path.GetFileNameWithoutExtension(objPath) :
                request.PartId
        );

        if (string.IsNullOrWhiteSpace(partId))
        {
            result.Message = "Part id is empty.";
            return result;
        }

        string displayName = string.IsNullOrWhiteSpace(request.DisplayName) ?
            PartCatalogHelpers.HumanizePartId(partId) :
            request.DisplayName.Trim();

        VabAttachmentNodeSetup.EnsureLayer();

        string partMeshFolder = GetCustomPartFolderAssetPath(category, partId);
        EnsureAssetFolder(partMeshFolder);
        EnsureAssetFolder(CustomMaterialsAssetRoot);
        EnsureAssetFolder(SharedAtlasFolder);

        string objAssetPath = CopyIntoFolder(objPath, partMeshFolder, partId + ".obj");
        CopySidecarIfPresent(objPath, partMeshFolder, partId + ".mtl");

        string metallicFull = NormalizeFullPath(request.MetallicFullPath);
        string reflectionFull = NormalizeFullPath(request.ReflectionFullPath);
        SharedAtlasBindResult atlasBind = BindSharedAtlas(
            partId,
            albedoPath,
            metallicFull,
            reflectionFull,
            request.ReflectionKind
        );

        AssetDatabase.Refresh();

        GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(objAssetPath);

        if (modelRoot == null)
        {
            result.Message = "Unity failed to import OBJ: " + objAssetPath;
            return result;
        }

        string prefabPath = ReStockPartPaths.GetContainerPrefabAssetPath(category, partId);
        ReStockPartPaths.EnsurePartPrefabFolder(category, partId);

        if (!BuildContainerPrefab(
                prefabPath,
                partId,
                modelRoot,
                atlasBind.Material,
                category,
                request.PreserveAuthoredMarkers
            ))
        {
            result.Message = "Failed to write prefab: " + prefabPath;
            return result;
        }

        WritePartDefinition(
            prefabPath,
            partId,
            displayName,
            category,
            request
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PartCatalog.Refresh();
        VabPartThumbnailGenerator.GenerateForContainerPrefab(prefabPath, force: true);

        result.Success = true;
        result.PrefabAssetPath = prefabPath;
        result.MaterialAssetPath = atlasBind.MaterialPath;
        result.ReusedMaterial = atlasBind.Reused;
        result.Message =
            "Prefab: " + prefabPath +
            "\nMesh: " + objAssetPath +
            "\n" + (atlasBind.Reused ? "Материал уже был (общий атлас): " : "Новый материал: ") +
            atlasBind.MaterialPath +
            "\nДетали на этом атласе: " + atlasBind.UsersText;
        Debug.Log("Custom part import: " + result.Message.Replace('\n', ' '));
        return result;
    }

    internal static string SanitizePartId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string trimmed = raw.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
        StringBuilder builder = new StringBuilder(trimmed.Length);

        for (int i = 0; i < trimmed.Length; i++)
        {
            char current = trimmed[i];

            if (
                (current >= 'a' && current <= 'z') ||
                (current >= '0' && current <= '9') ||
                current == '-'
            )
            {
                if (current == '-' && builder.Length > 0 && builder[builder.Length - 1] == '-')
                {
                    continue;
                }

                builder.Append(current);
            }
        }

        string sanitized = builder.ToString().Trim('-');
        return sanitized;
    }

    internal static string[] DistinctPartIds(string[] partIds)
    {
        List<string> unique = new List<string>();

        if (partIds == null)
        {
            return Array.Empty<string>();
        }

        for (int i = 0; i < partIds.Length; i++)
        {
            string id = SanitizePartId(partIds[i]);

            if (string.IsNullOrEmpty(id) || ContainsPartId(unique, id))
            {
                continue;
            }

            unique.Add(id);
        }

        return unique.ToArray();
    }

    internal static string[] AddPartId(string[] partIds, string partId)
    {
        string sanitized = SanitizePartId(partId);

        if (string.IsNullOrEmpty(sanitized))
        {
            return DistinctPartIds(partIds);
        }

        List<string> unique = new List<string>(DistinctPartIds(partIds));

        if (!ContainsPartId(unique, sanitized))
        {
            unique.Add(sanitized);
        }

        return unique.ToArray();
    }

    internal static string[] RemovePartId(string[] partIds, string partId)
    {
        string sanitized = SanitizePartId(partId);
        string[] unique = DistinctPartIds(partIds);

        if (string.IsNullOrEmpty(sanitized) || unique.Length == 0)
        {
            return unique;
        }

        List<string> kept = new List<string>(unique.Length);

        for (int i = 0; i < unique.Length; i++)
        {
            if (!string.Equals(unique[i], sanitized, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(unique[i]);
            }
        }

        return kept.ToArray();
    }

    internal static string AtlasStemFromAlbedoFileName(string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return "shared-atlas";
        }

        string name = fileNameWithoutExtension.Trim();
        string[] suffixes =
        {
            "_basecolor",
            "_base_color",
            "_base-color",
            "_albedo",
            "_diffuse",
            "_diff",
            "_color",
            "_col",
            "_base",
            "_d",
            "-d",
        };

        string lower = name.ToLowerInvariant();

        for (int i = 0; i < suffixes.Length; i++)
        {
            if (lower.EndsWith(suffixes[i], StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - suffixes[i].Length);
                break;
            }
        }

        string stem = SanitizePartId(name);
        return string.IsNullOrEmpty(stem) ? "shared-atlas" : stem;
    }

    internal static string BuildMaterialAssetName(string[] partIds, string atlasStem)
    {
        string stem = SanitizePartId(atlasStem);

        if (string.IsNullOrEmpty(stem))
        {
            stem = "shared-atlas";
        }

        string[] ids = DistinctPartIds(partIds);

        if (ids.Length == 0)
        {
            return TruncateName(stem, MaxMaterialNameLength);
        }

        if (ids.Length == 1)
        {
            return TruncateName(ids[0], MaxMaterialNameLength);
        }

        string joined = string.Join("+", ids);

        if (joined.Length <= MaxMaterialNameLength)
        {
            return joined;
        }

        return TruncateName(stem, MaxMaterialNameLength);
    }

    internal static string FormatPartList(string[] partIds)
    {
        string[] ids = DistinctPartIds(partIds);
        return ids.Length == 0 ? "—" : string.Join(", ", ids);
    }

    static bool ContainsPartId(List<string> partIds, string partId)
    {
        for (int i = 0; i < partIds.Count; i++)
        {
            if (string.Equals(partIds[i], partId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static string TruncateName(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength).Trim('-', '+');
    }

    struct SharedAtlasBindResult
    {
        public Material Material;
        public string MaterialPath;
        public bool Reused;
        public string UsersText;
    }

    static SharedAtlasBindResult BindSharedAtlas(
        string partId,
        string albedoFullPath,
        string metallicFullPath,
        string reflectionFullPath,
        CustomPartReflectionKind reflectionKind
    )
    {
        string contentHash = ComputeAtlasContentHash(
            albedoFullPath,
            metallicFullPath,
            reflectionFullPath,
            reflectionKind
        );
        string atlasStem = AtlasStemFromAlbedoFileName(Path.GetFileNameWithoutExtension(albedoFullPath));
        CustomPartAtlasRegistryData registry = CustomPartAtlasRegistry.Load();
        CustomPartAtlasRegistry.RemovePartFromOtherAtlases(registry, partId, contentHash);
        CustomPartAtlasRecord record = CustomPartAtlasRegistry.FindByHash(registry, contentHash);
        bool reused = record != null &&
            !string.IsNullOrWhiteSpace(record.materialPath) &&
            AssetDatabase.LoadAssetAtPath<Material>(record.materialPath) != null;

        if (record == null)
        {
            record = new CustomPartAtlasRecord
            {
                contentHash = contentHash,
                atlasStem = atlasStem,
                reflectionKind = (int)reflectionKind,
                partIds = new[] { partId },
            };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(record.atlasStem))
            {
                record.atlasStem = atlasStem;
            }

            record.partIds = AddPartId(record.partIds, partId);
        }

        EnsureSharedAtlasFiles(
            record,
            albedoFullPath,
            metallicFullPath,
            reflectionFullPath,
            reflectionKind
        );

        string desiredName = BuildMaterialAssetName(record.partIds, record.atlasStem);
        record.materialPath = ResolveMaterialAssetPath(record.materialPath, desiredName);
        Material material = CreateOrUpdateMaterial(
            record.materialPath,
            record.albedoAssetPath,
            record.maskAssetPath,
            hasMetallicMap: !string.IsNullOrWhiteSpace(record.metallicAssetPath),
            hasReflectionMap: !string.IsNullOrWhiteSpace(record.reflectionAssetPath)
        );
        ApplyMaterialLabels(material, record.partIds);

        CustomPartAtlasRegistry.Upsert(registry, record);
        CustomPartAtlasRegistry.Save(registry);

        return new SharedAtlasBindResult
        {
            Material = material,
            MaterialPath = record.materialPath,
            Reused = reused,
            UsersText = FormatPartList(record.partIds),
        };
    }

    static void EnsureSharedAtlasFiles(
        CustomPartAtlasRecord record,
        string albedoFullPath,
        string metallicFullPath,
        string reflectionFullPath,
        CustomPartReflectionKind reflectionKind
    )
    {
        string folderId = UniqueSharedFolderId(record);
        record.sharedFolder = SharedAtlasFolder + "/" + folderId;
        EnsureAssetFolder(record.sharedFolder);

        bool albedoReady = AssetExists(record.albedoAssetPath);
        bool maskReady = AssetExists(record.maskAssetPath);

        if (!albedoReady)
        {
            record.albedoAssetPath = CopyIntoFolder(
                albedoFullPath,
                record.sharedFolder,
                folderId + AlbedoFileSuffix
            );
            ConfigureAlbedoImporter(record.albedoAssetPath);
        }

        bool hasMetallic = !string.IsNullOrWhiteSpace(metallicFullPath) && File.Exists(metallicFullPath);
        bool hasReflection = !string.IsNullOrWhiteSpace(reflectionFullPath) && File.Exists(reflectionFullPath);

        if (hasMetallic && !AssetExists(record.metallicAssetPath))
        {
            record.metallicAssetPath = CopyIntoFolder(
                metallicFullPath,
                record.sharedFolder,
                folderId + MetallicFileSuffix
            );
            ConfigureLinearMapImporter(record.metallicAssetPath, false);
        }

        if (hasReflection && !AssetExists(record.reflectionAssetPath))
        {
            record.reflectionAssetPath = CopyIntoFolder(
                reflectionFullPath,
                record.sharedFolder,
                folderId + ReflectionFileSuffix
            );
            ConfigureLinearMapImporter(record.reflectionAssetPath, false);
        }

        bool needsMask = hasMetallic || hasReflection;

        if (needsMask && !maskReady)
        {
            record.maskAssetPath = PackMetallicSmoothnessMask(
                record.sharedFolder + "/" + folderId + MaskFileSuffix,
                albedoFullPath,
                metallicFullPath,
                reflectionFullPath,
                reflectionKind
            );
        }
    }

    static string UniqueSharedFolderId(CustomPartAtlasRecord record)
    {
        string stem = SanitizePartId(record.atlasStem);

        if (string.IsNullOrEmpty(stem))
        {
            stem = "atlas";
        }

        if (string.IsNullOrWhiteSpace(record.contentHash) || record.contentHash.Length < 8)
        {
            return stem;
        }

        string hashed = stem + "-" + record.contentHash.Substring(0, 8);
        return hashed;
    }

    static string ResolveMaterialAssetPath(string existingPath, string desiredName)
    {
        string targetPath = CustomMaterialsAssetRoot + "/" + desiredName + ".mat";

        if (string.IsNullOrWhiteSpace(existingPath) || !AssetExists(existingPath))
        {
            return targetPath;
        }

        string currentName = Path.GetFileNameWithoutExtension(existingPath);

        if (string.Equals(currentName, desiredName, StringComparison.OrdinalIgnoreCase))
        {
            return existingPath;
        }

        if (AssetExists(targetPath) &&
            !string.Equals(existingPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return existingPath;
        }

        string renameError = AssetDatabase.RenameAsset(existingPath, desiredName);

        if (!string.IsNullOrEmpty(renameError))
        {
            Debug.LogWarning("Custom part material rename skipped: " + renameError);
            return existingPath;
        }

        return targetPath;
    }

    static void ApplyMaterialLabels(Material material, string[] partIds)
    {
        if (material == null)
        {
            return;
        }

        string[] ids = DistinctPartIds(partIds);
        string[] labels = new string[ids.Length + 1];
        labels[0] = "custom-atlas";
        Array.Copy(ids, 0, labels, 1, ids.Length);
        AssetDatabase.SetLabels(material, labels);
    }

    static bool AssetExists(string assetPath)
    {
        return !string.IsNullOrWhiteSpace(assetPath) &&
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null;
    }

    internal static string ComputeAtlasContentHash(
        string albedoFullPath,
        string metallicFullPath,
        string reflectionFullPath,
        CustomPartReflectionKind reflectionKind
    )
    {
        byte[] scratch = new byte[1];

        using (SHA1 hasher = SHA1.Create())
        {
            AppendFileBytes(hasher, albedoFullPath);
            AppendSeparator(hasher, scratch);
            AppendFileBytes(hasher, metallicFullPath);
            AppendSeparator(hasher, scratch);
            AppendFileBytes(hasher, reflectionFullPath);
            scratch[0] = (byte)reflectionKind;
            hasher.TransformBlock(scratch, 0, 1, scratch, 0);
            hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return ToHex(hasher.Hash);
        }
    }

    static void AppendFileBytes(SHA1 hasher, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);

        if (bytes.Length == 0)
        {
            return;
        }

        hasher.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
    }

    static void AppendSeparator(SHA1 hasher, byte[] scratch)
    {
        scratch[0] = 0;
        hasher.TransformBlock(scratch, 0, 1, scratch, 0);
    }

    static string ToHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(bytes.Length * 2);

        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }

        return builder.ToString();
    }

    internal static string NormalizeCategory(string category)
    {
        if (string.Equals(category, PartDatabase.CategoryCommand, StringComparison.OrdinalIgnoreCase))
        {
            return PartDatabase.CategoryCommand;
        }

        if (string.Equals(category, PartDatabase.CategoryFuelTank, StringComparison.OrdinalIgnoreCase))
        {
            return PartDatabase.CategoryFuelTank;
        }

        return PartDatabase.CategoryEngine;
    }

    internal static float ToSmoothness(float sample, CustomPartReflectionKind kind)
    {
        float clamped = Mathf.Clamp01(sample);
        return kind == CustomPartReflectionKind.Roughness ? 1f - clamped : clamped;
    }

    internal static CustomPartTextureRole ClassifyTextureFileName(string fileNameWithoutExtension, string partStem)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return CustomPartTextureRole.Unknown;
        }

        string name = fileNameWithoutExtension.Trim().ToLowerInvariant();
        string stem = string.IsNullOrWhiteSpace(partStem) ?
            string.Empty :
            partStem.Trim().ToLowerInvariant();

        if (
            ContainsToken(name, "metal") ||
            name.EndsWith("-m", StringComparison.Ordinal) ||
            name.EndsWith("_m", StringComparison.Ordinal)
        )
        {
            return CustomPartTextureRole.Metallic;
        }

        if (
            ContainsToken(name, "rough") ||
            ContainsToken(name, "smooth") ||
            ContainsToken(name, "gloss") ||
            ContainsToken(name, "refl") ||
            ContainsToken(name, "spec") ||
            ContainsToken(name, "отраж")
        )
        {
            return CustomPartTextureRole.Reflection;
        }

        if (
            ContainsToken(name, "normal") ||
            ContainsToken(name, "bump") ||
            ContainsToken(name, "mask") ||
            name.EndsWith("-n", StringComparison.Ordinal) ||
            name.EndsWith("_n", StringComparison.Ordinal) ||
            ContainsToken(name, "emiss") ||
            name.EndsWith("-e", StringComparison.Ordinal) ||
            ContainsToken(name, "ao") ||
            ContainsToken(name, "occlusion")
        )
        {
            return CustomPartTextureRole.Unknown;
        }

        if (
            ContainsToken(name, "albedo") ||
            ContainsToken(name, "diff") ||
            ContainsToken(name, "color") ||
            ContainsToken(name, "base") ||
            ContainsToken(name, "textur") ||
            name.EndsWith("-d", StringComparison.Ordinal) ||
            name.EndsWith("_d", StringComparison.Ordinal)
        )
        {
            return CustomPartTextureRole.Albedo;
        }

        if (!string.IsNullOrEmpty(stem) && name == stem)
        {
            return CustomPartTextureRole.Albedo;
        }

        if (!string.IsNullOrEmpty(stem) && name.StartsWith(stem, StringComparison.Ordinal))
        {
            return CustomPartTextureRole.Albedo;
        }

        return CustomPartTextureRole.Unknown;
    }

    internal static void DetectMapsInFolder(
        string objFullPath,
        out string albedoFullPath,
        out string metallicFullPath,
        out string reflectionFullPath
    )
    {
        albedoFullPath = string.Empty;
        metallicFullPath = string.Empty;
        reflectionFullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(objFullPath) || !File.Exists(objFullPath))
        {
            return;
        }

        string folder = Path.GetDirectoryName(objFullPath);

        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        string stem = Path.GetFileNameWithoutExtension(objFullPath);
        string[] pngFiles = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
        List<string> unknown = new List<string>();

        for (int i = 0; i < pngFiles.Length; i++)
        {
            string filePath = pngFiles[i];
            string name = Path.GetFileNameWithoutExtension(filePath);
            CustomPartTextureRole role = ClassifyTextureFileName(name, stem);

            if (role == CustomPartTextureRole.Albedo && string.IsNullOrEmpty(albedoFullPath))
            {
                albedoFullPath = filePath;
            }
            else if (role == CustomPartTextureRole.Metallic && string.IsNullOrEmpty(metallicFullPath))
            {
                metallicFullPath = filePath;
            }
            else if (role == CustomPartTextureRole.Reflection && string.IsNullOrEmpty(reflectionFullPath))
            {
                reflectionFullPath = filePath;
            }
            else if (role == CustomPartTextureRole.Unknown)
            {
                unknown.Add(filePath);
            }
        }

        if (string.IsNullOrEmpty(albedoFullPath) && unknown.Count == 1)
        {
            albedoFullPath = unknown[0];
        }

        if (string.IsNullOrEmpty(albedoFullPath) && pngFiles.Length == 1)
        {
            albedoFullPath = pngFiles[0];
        }
    }

    internal static string GetCustomPartFolderAssetPath(string category, string partId)
    {
        return CustomMeshesAssetRoot + "/" + category + "/" + partId;
    }

    static bool ContainsToken(string name, string token)
    {
        return name.IndexOf(token, StringComparison.Ordinal) >= 0;
    }

    static string NormalizeFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(path.Trim().Trim('"'));
    }

    static string CopyIntoFolder(string sourceFullPath, string destAssetFolder, string destFileName)
    {
        string destAssetPath = destAssetFolder + "/" + destFileName;
        string destFullPath = AssetPathToFullPath(destAssetPath);
        string destDirectory = Path.GetDirectoryName(destFullPath);

        if (!string.IsNullOrEmpty(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }

        string sourceFull = Path.GetFullPath(sourceFullPath);

        if (!string.Equals(sourceFull, destFullPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFull, destFullPath, overwrite: true);
        }

        AssetDatabase.ImportAsset(destAssetPath, ImportAssetOptions.ForceUpdate);
        return destAssetPath;
    }

    static void CopySidecarIfPresent(string objFullPath, string destAssetFolder, string destFileName)
    {
        string sourceDirectory = Path.GetDirectoryName(objFullPath);
        string sourceStem = Path.GetFileNameWithoutExtension(objFullPath);

        if (string.IsNullOrEmpty(sourceDirectory) || string.IsNullOrEmpty(sourceStem))
        {
            return;
        }

        string sourceMtl = Path.Combine(sourceDirectory, sourceStem + ".mtl");

        if (!File.Exists(sourceMtl))
        {
            return;
        }

        CopyIntoFolder(sourceMtl, destAssetFolder, destFileName);
    }

    static string PackMetallicSmoothnessMask(
        string maskAssetPath,
        string albedoFullPath,
        string metallicFullPath,
        string reflectionFullPath,
        CustomPartReflectionKind reflectionKind
    )
    {
        bool hasMetallic = !string.IsNullOrWhiteSpace(metallicFullPath) && File.Exists(metallicFullPath);
        bool hasReflection = !string.IsNullOrWhiteSpace(reflectionFullPath) && File.Exists(reflectionFullPath);

        if (!hasMetallic && !hasReflection)
        {
            return string.Empty;
        }

        Texture2D albedo = LoadPng(albedoFullPath, linear: false);
        Texture2D metallic = hasMetallic ? LoadPng(metallicFullPath, linear: true) : null;
        Texture2D reflection = hasReflection ? LoadPng(reflectionFullPath, linear: true) : null;

        int width = albedo != null ? albedo.width : 0;
        int height = albedo != null ? albedo.height : 0;

        if (metallic != null)
        {
            width = Mathf.Max(width, metallic.width);
            height = Mathf.Max(height, metallic.height);
        }

        if (reflection != null)
        {
            width = Mathf.Max(width, reflection.width);
            height = Mathf.Max(height, reflection.height);
        }

        if (width < 1 || height < 1)
        {
            DestroyTemp(albedo);
            DestroyTemp(metallic);
            DestroyTemp(reflection);
            return string.Empty;
        }

        Texture2D mask = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;

            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float metal = metallic != null ? ChannelValue(metallic.GetPixelBilinear(u, v)) : 0f;
                float reflectionSample = reflection != null ?
                    ChannelValue(reflection.GetPixelBilinear(u, v)) :
                    DefaultSmoothness;
                float smoothness = reflection != null ?
                    ToSmoothness(reflectionSample, reflectionKind) :
                    DefaultSmoothness;
                pixels[y * width + x] = new Color(metal, 1f, 1f, smoothness);
            }
        }

        mask.SetPixels(pixels);
        mask.Apply(false, false);

        string maskFullPath = AssetPathToFullPath(maskAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(maskFullPath) ?? Directory.GetCurrentDirectory());
        File.WriteAllBytes(maskFullPath, mask.EncodeToPNG());

        DestroyTemp(albedo);
        DestroyTemp(metallic);
        DestroyTemp(reflection);
        DestroyTemp(mask);

        AssetDatabase.ImportAsset(maskAssetPath, ImportAssetOptions.ForceUpdate);
        ConfigureLinearMapImporter(maskAssetPath, keepAlpha: true);
        return maskAssetPath;
    }

    static Texture2D LoadPng(string fullPath, bool linear)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);

        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        if (!texture.LoadImage(bytes, markNonReadable: false))
        {
            DestroyTemp(texture);
            return null;
        }

        return texture;
    }

    static float ChannelValue(Color color)
    {
        return Mathf.Clamp01(color.r);
    }

    static void DestroyTemp(Texture2D texture)
    {
        if (texture != null)
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static Material CreateOrUpdateMaterial(
        string materialAssetPath,
        string albedoAssetPath,
        string maskAssetPath,
        bool hasMetallicMap,
        bool hasReflectionMap
    )
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        EnsureAssetFolder(Path.GetDirectoryName(materialAssetPath)?.Replace('\\', '/') ?? CustomMaterialsAssetRoot);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialAssetPath);
        }
        else
        {
            material.shader = shader;
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoAssetPath);
        Texture2D mask = string.IsNullOrWhiteSpace(maskAssetPath) ?
            null :
            AssetDatabase.LoadAssetAtPath<Texture2D>(maskAssetPath);

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", albedo);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", albedo);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_WorkflowMode"))
        {
            material.SetFloat("_WorkflowMode", 1f);
        }

        if (material.HasProperty("_SmoothnessTextureChannel"))
        {
            material.SetFloat("_SmoothnessTextureChannel", 0f);
        }

        bool useMask = mask != null && (hasMetallicMap || hasReflectionMap);
        bool assignedMask = false;

        if (useMask)
        {
            assignedMask = TryAssignMaskMap(material, mask);

            if (assignedMask)
            {
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_MASKMAP");

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", 1f);
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 1f);
                }
            }
        }

        if (!assignedMask)
        {
            TryAssignMaskMap(material, null);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_MASKMAP");

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", DefaultSmoothness);
            }
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    static bool TryAssignMaskMap(Material material, Texture2D mask)
    {
        if (material == null)
        {
            return false;
        }

        string[] propertyNames =
        {
            "_MetallicGlossMap",
            "_SpecGlossMap",
            "_MaskMap",
        };
        bool assigned = false;

        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (!material.HasProperty(propertyNames[i]))
            {
                continue;
            }

            material.SetTexture(propertyNames[i], mask);
            assigned = mask != null;
        }

        return assigned;
    }

    static bool BuildContainerPrefab(
        string prefabPath,
        string partId,
        GameObject modelRoot,
        Material material,
        string category,
        bool preserveAuthoredMarkers
    )
    {
        bool containerExists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        List<MarkerSnapshot> savedMarkers = new List<MarkerSnapshot>();
        Vector3? savedThrustLocalPosition = null;
        Quaternion? savedThrustLocalRotation = null;
        bool loadedFromPrefabContents = false;
        GameObject containerRoot;

        if (containerExists)
        {
            containerRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            loadedFromPrefabContents = true;

            if (preserveAuthoredMarkers)
            {
                CaptureMarkerSnapshots(containerRoot, savedMarkers);
                CaptureThrustSnapshot(containerRoot, out savedThrustLocalPosition, out savedThrustLocalRotation);
            }

            ReplaceVisualChild(containerRoot, modelRoot, material);
        }
        else
        {
            containerRoot = new GameObject(partId);
            AttachVisualChild(containerRoot, modelRoot, material);
        }

        containerRoot.name = partId;
        EnsurePartVisualAdapter(containerRoot, partId);

        bool hasMarkers = PartPrefabBuilder.HasAuthoredNodes(containerRoot);

        if (!hasMarkers || !preserveAuthoredMarkers)
        {
            RemoveExistingMarkers(containerRoot);
            SpawnDefaultMarkers(containerRoot, category);
        }
        else
        {
            RestoreMarkerSnapshots(containerRoot, savedMarkers);
        }

        bool isEngine = string.Equals(category, PartDatabase.CategoryEngine, StringComparison.OrdinalIgnoreCase);
        bool hasThrust = PartPrefabBuilder.FindThrustTransform(containerRoot.transform) != null;

        if (isEngine)
        {
            if (preserveAuthoredMarkers && savedThrustLocalPosition.HasValue)
            {
                RestoreThrustSnapshot(
                    containerRoot,
                    savedThrustLocalPosition.Value,
                    savedThrustLocalRotation ?? Quaternion.identity
                );
            }
            else if (!hasThrust || !preserveAuthoredMarkers)
            {
                EnsureEngineThrustTransform(containerRoot);
            }
        }

        PrefabUtility.SaveAsPrefabAsset(containerRoot, prefabPath);

        if (loadedFromPrefabContents)
        {
            PrefabUtility.UnloadPrefabContents(containerRoot);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(containerRoot);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
    }

    static void AttachVisualChild(GameObject containerRoot, GameObject modelRoot, Material material)
    {
        GameObject visual = PrefabUtility.InstantiatePrefab(modelRoot) as GameObject;

        if (visual == null)
        {
            visual = UnityEngine.Object.Instantiate(modelRoot);
        }

        visual.name = VisualChildName;
        visual.transform.SetParent(containerRoot.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        AssignMaterial(visual, material);
    }

    static void ReplaceVisualChild(GameObject containerRoot, GameObject modelRoot, Material material)
    {
        Transform existing = containerRoot.transform.Find(VisualChildName);

        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        AttachVisualChild(containerRoot, modelRoot, material);
    }

    static void AssignMaterial(GameObject visualRoot, Material material)
    {
        if (visualRoot == null || material == null)
        {
            return;
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || renderer.GetComponent<AttachmentNodeMarker>() != null)
            {
                continue;
            }

            Material[] slots = renderer.sharedMaterials;
            Material[] filled = new Material[Mathf.Max(1, slots.Length)];

            for (int slot = 0; slot < filled.Length; slot++)
            {
                filled[slot] = material;
            }

            renderer.sharedMaterials = filled;
            renderer.enabled = true;
            renderer.gameObject.SetActive(true);
        }
    }

    static void EnsurePartVisualAdapter(GameObject root, string partId)
    {
        PartVisualAdapter adapter = root.GetComponent<PartVisualAdapter>();

        if (adapter == null)
        {
            adapter = root.AddComponent<PartVisualAdapter>();
        }

        adapter.PartTemplateId = partId;
    }

    static void CaptureMarkerSnapshots(GameObject root, List<MarkerSnapshot> snapshots)
    {
        AttachmentNodeMarker[] markers = root.GetComponentsInChildren<AttachmentNodeMarker>(true);

        for (int i = 0; i < markers.Length; i++)
        {
            AttachmentNodeMarker marker = markers[i];

            if (marker == null || !PartPrefabBuilder.IsMarkerOwnedByPartRoot(marker, root))
            {
                continue;
            }

            snapshots.Add(
                new MarkerSnapshot
                {
                    Name = marker.gameObject.name,
                    LocalPosition = marker.transform.localPosition,
                    LocalRotation = marker.transform.localRotation,
                    NodeIndex = marker.NodeIndex,
                }
            );
        }
    }

    static void RestoreMarkerSnapshots(GameObject root, List<MarkerSnapshot> snapshots)
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            MarkerSnapshot snapshot = snapshots[i];
            Transform existing = root.transform.Find(snapshot.Name);

            if (existing == null)
            {
                continue;
            }

            existing.localPosition = snapshot.LocalPosition;
            existing.localRotation = snapshot.LocalRotation;
        }
    }

    static void CaptureThrustSnapshot(
        GameObject root,
        out Vector3? localPosition,
        out Quaternion? localRotation
    )
    {
        Transform thrust = PartPrefabBuilder.FindThrustTransform(root.transform);
        localPosition = null;
        localRotation = null;

        if (thrust == null)
        {
            return;
        }

        localPosition = thrust.localPosition;
        localRotation = thrust.localRotation;
    }

    static void RestoreThrustSnapshot(GameObject root, Vector3 localPosition, Quaternion localRotation)
    {
        Transform thrust = EnsureThrustTransformObject(root);

        if (thrust == null)
        {
            return;
        }

        thrust.localPosition = localPosition;
        thrust.localRotation = localRotation;
    }

    static void RemoveExistingMarkers(GameObject root)
    {
        AttachmentNodeMarker[] markers = root.GetComponentsInChildren<AttachmentNodeMarker>(true);

        for (int i = 0; i < markers.Length; i++)
        {
            AttachmentNodeMarker marker = markers[i];

            if (marker == null || !PartPrefabBuilder.IsMarkerOwnedByPartRoot(marker, root))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(marker.gameObject);
        }
    }

    static void SpawnDefaultMarkers(GameObject root, string category)
    {
        Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(root);
        List<AttachmentNode> nodes = PartCatalogHelpers.InferNodes(root.name, bounds, category);

        for (int i = 0; i < nodes.Count; i++)
        {
            AttachmentNode node = nodes[i];
            string objectName = i == 0 ?
                PartPrefabBuilder.NodeTopName :
                i == 1 ? PartPrefabBuilder.NodeBottomName : PartPrefabBuilder.NodeIndexPrefix + i;
            CreateMarker(
                root,
                objectName,
                i,
                new Vector3(
                    (float)node.localPosition.x,
                    (float)node.localPosition.y,
                    (float)node.localPosition.z
                ),
                DirectionToQuaternion(node.direction)
            );
        }
    }

    static void EnsureEngineThrustTransform(GameObject root)
    {
        Bounds bounds = PartCatalogHelpers.CalculateLocalBounds(root);
        Transform thrust = EnsureThrustTransformObject(root);
        thrust.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        thrust.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.down);
    }

    static Transform EnsureThrustTransformObject(GameObject root)
    {
        Transform existing = PartPrefabBuilder.FindThrustTransform(root.transform);

        if (existing != null)
        {
            return existing;
        }

        GameObject thrustObject = new GameObject(PartPrefabBuilder.ThrustTransformName);
        thrustObject.transform.SetParent(root.transform, false);
        return thrustObject.transform;
    }

    static GameObject CreateMarker(
        GameObject root,
        string objectName,
        int nodeIndex,
        Vector3 localPosition,
        Quaternion localRotation
    )
    {
        GameObject nodeObject = new GameObject(objectName);
        nodeObject.transform.SetParent(root.transform, false);
        nodeObject.transform.localPosition = localPosition;
        nodeObject.transform.localRotation = localRotation;

        AttachmentNodeMarker marker = nodeObject.AddComponent<AttachmentNodeMarker>();
        marker.Configure(string.Empty, nodeIndex);

        SphereCollider trigger = nodeObject.GetComponent<SphereCollider>();

        if (trigger == null)
        {
            trigger = nodeObject.AddComponent<SphereCollider>();
        }

        trigger.isTrigger = true;
        trigger.radius = VabAttachmentNodeLayers.TriggerRadiusMeters;
        trigger.center = Vector3.zero;

        int attachmentLayer = LayerMask.NameToLayer(VabAttachmentNodeLayers.LayerName);

        if (attachmentLayer >= 0)
        {
            nodeObject.layer = attachmentLayer;
        }

        return nodeObject;
    }

    static Quaternion DirectionToQuaternion(SimVector3 direction)
    {
        Vector3 up = new Vector3(
            (float)direction.x,
            (float)direction.y,
            (float)direction.z
        ).normalized;

        if (up.sqrMagnitude < 1e-6f)
        {
            up = Vector3.up;
        }

        return Quaternion.FromToRotation(Vector3.up, up);
    }

    static void WritePartDefinition(
        string prefabAssetPath,
        string partId,
        string displayName,
        string category,
        CustomPartImportRequest request
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        List<AttachmentNode> nodes = prefab != null ?
            PartPrefabBuilder.ReadAuthoredNodes(prefab) :
            new List<AttachmentNode>();

        PartDefinitionData data = new PartDefinitionData
        {
            id = partId,
            displayName = displayName,
            category = category,
            dryMass = request.DryMass > 0f ? request.DryMass : PartCatalogHelpers.GuessDryMass(partId, category),
            fuelCapacity = request.FuelCapacity,
            maxThrustVac = request.MaxThrustVac,
            ispVacuum = request.IspVacuum,
            nodes = nodes,
        };

        string partDefPath = PartDefinitionFile.GetSidecarPathForPrefab(prefabAssetPath);
        PartDefinitionFile.Save(partDefPath, data);
        AssetDatabase.ImportAsset(partDefPath);
    }

    static void ConfigureAlbedoImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
    }

    static void ConfigureLinearMapImporter(string assetPath, bool keepAlpha)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.alphaSource = keepAlpha ?
            TextureImporterAlphaSource.FromInput :
            TextureImporterAlphaSource.None;
        importer.SaveAndReimport();
    }

    internal static void EnsureAssetFolder(string assetFolder)
    {
        if (string.IsNullOrWhiteSpace(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
        string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        string leaf = Path.GetFileName(normalized);

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

    static string AssetPathToFullPath(string assetPath)
    {
        string normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));
    }
}

/// <summary>
/// Import settings for original custom part meshes. No KSP UV-V flip.
/// </summary>
class CustomPartModelImportPostprocessor : AssetPostprocessor
{
    public override uint GetVersion() => 1;

    static bool IsCustomPartMesh(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalized = path.Replace('\\', '/');
        return normalized.StartsWith(
                CustomPartImportPipeline.CustomMeshesAssetRoot + "/",
                StringComparison.OrdinalIgnoreCase
            ) &&
            normalized.EndsWith(".obj", StringComparison.OrdinalIgnoreCase);
    }

    void OnPreprocessModel()
    {
        if (!IsCustomPartMesh(assetPath))
        {
            return;
        }

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.weldVertices = false;
        importer.bakeAxisConversion = false;
        importer.swapUVChannels = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.addCollider = false;
        importer.animationType = ModelImporterAnimationType.None;
    }
}
#endif
