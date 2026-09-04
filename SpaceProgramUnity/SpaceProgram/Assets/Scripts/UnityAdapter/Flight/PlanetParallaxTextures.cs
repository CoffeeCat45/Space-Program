using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Parallax terrain texture set for local flight chunks (KSP-style splat layers).
/// Inspector slots are optional; missing maps load from the LOD-body RSS-Terrain pack
/// (Earth or Moon Content folder, then PluginData).
/// </summary>
[System.Serializable]
public sealed class PlanetParallaxTerrainSet
{
    public Texture2D InfluenceMap;
    public Texture2D LowAlbedo;
    public Texture2D LowNormal;
    public Texture2D MidAlbedo;
    public Texture2D MidNormal;
    public Texture2D HighAlbedo;
    public Texture2D HighNormal;
    public Texture2D SteepAlbedo;
    public Texture2D SteepNormal;

    public bool HasInfluenceMap => InfluenceMap != null;

    public bool HasAnyAlbedo =>
        LowAlbedo != null ||
        MidAlbedo != null ||
        HighAlbedo != null ||
        SteepAlbedo != null;
}

/// <summary>
/// Binds Parallax/RSS textures to planet materials.
/// </summary>
static class PlanetParallaxTextures
{
    public const float DefaultDetailMetersPerTile = 200f;
    public const float DefaultMacroMetersPerTile = 180f;
    public const float DefaultLowAltitudeStartMeters = 0f;
    public const float DefaultLowAltitudeEndMeters = 12f;
    public const float DefaultHighAltitudeStartMeters = 2572f;
    public const float DefaultHighAltitudeEndMeters = 3072f;
    public const float DefaultParallaxBlend = 0.88f;

    public const float DefaultChunkDetailTileRepeat = 4f;
    public const float DefaultChunkMacroTileRepeat = 1.4f;

    public static void FillMissingFromDisk(
        PlanetParallaxTerrainSet terrainSet,
        ICollection<Texture2D> runtimeOwnership
    )
    {
        FillMissingFromDisk(terrainSet, runtimeOwnership, RssTerrainSplatPack.Earth);
    }

    public static void FillMissingFromDisk(
        PlanetParallaxTerrainSet terrainSet,
        ICollection<Texture2D> runtimeOwnership,
        RssTerrainSplatPack pack
    )
    {
        if (terrainSet == null)
        {
            return;
        }

        terrainSet.InfluenceMap ??= LoadTerrainTexture(
            pack,
            pack.InfluenceFileName,
            TextureLoadMode.Data,
            runtimeOwnership
        );
        terrainSet.LowAlbedo ??= LoadTerrainTexture(
            pack,
            pack.LowColorFileName,
            TextureLoadMode.Color,
            runtimeOwnership
        );
        terrainSet.LowNormal ??= LoadTerrainTexture(
            pack,
            pack.LowNormalFileName,
            TextureLoadMode.Normal,
            runtimeOwnership
        );
        terrainSet.MidAlbedo ??= LoadTerrainTexture(
            pack,
            pack.MidColorFileName,
            TextureLoadMode.Color,
            runtimeOwnership
        );
        terrainSet.MidNormal ??= LoadTerrainTexture(
            pack,
            pack.MidNormalFileName,
            TextureLoadMode.Normal,
            runtimeOwnership
        );
        terrainSet.HighAlbedo ??= LoadTerrainTexture(
            pack,
            pack.HighColorFileName,
            TextureLoadMode.Color,
            runtimeOwnership
        );
        terrainSet.HighNormal ??= LoadTerrainTexture(
            pack,
            pack.HighNormalFileName,
            TextureLoadMode.Normal,
            runtimeOwnership
        );
        terrainSet.SteepAlbedo ??= LoadTerrainTexture(
            pack,
            pack.SteepColorFileName,
            TextureLoadMode.Color,
            runtimeOwnership
        );
        terrainSet.SteepNormal ??= LoadTerrainTexture(
            pack,
            pack.SteepNormalFileName,
            TextureLoadMode.Normal,
            runtimeOwnership
        );
    }

    public static void ApplyTerrainSetToMaterial(Material material, PlanetParallaxTerrainSet terrainSet)
    {
        if (material == null || terrainSet == null)
        {
            return;
        }

        AssignTexture(material, "_SplatMap", terrainSet.InfluenceMap, linear: true, clampWrap: false);
        AssignTexture(material, "_LowAlbedo", terrainSet.LowAlbedo, linear: false, clampWrap: false);
        AssignTexture(material, "_LowNormal", terrainSet.LowNormal, linear: true, clampWrap: false);
        AssignTexture(material, "_MidAlbedo", terrainSet.MidAlbedo, linear: false, clampWrap: false);
        AssignTexture(material, "_MidNormal", terrainSet.MidNormal, linear: true, clampWrap: false);
        AssignTexture(material, "_HighAlbedo", terrainSet.HighAlbedo, linear: false, clampWrap: false);
        AssignTexture(material, "_HighNormal", terrainSet.HighNormal, linear: true, clampWrap: false);
        AssignTexture(material, "_SteepAlbedo", terrainSet.SteepAlbedo, linear: false, clampWrap: false);
        AssignTexture(material, "_SteepNormal", terrainSet.SteepNormal, linear: true, clampWrap: false);
        AssignTexture(
            material,
            "_WaveNormal",
            terrainSet.LowNormal != null ? terrainSet.LowNormal : terrainSet.SteepNormal,
            linear: true,
            clampWrap: false
        );
    }

    public static void ConfigureTextureSampling(Texture2D texture, bool clampWrap = false)
    {
        if (texture == null)
        {
            return;
        }

        texture.wrapMode = clampWrap ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;
    }

    static Texture2D LoadTerrainTexture(
        RssTerrainSplatPack pack,
        string rssTextureBaseName,
        TextureLoadMode loadMode,
        ICollection<Texture2D> runtimeOwnership
    )
    {
        Texture2D assetTexture = TryLoadTerrainTextureAsset(pack, rssTextureBaseName);

        if (assetTexture != null)
        {
            return assetTexture;
        }

        if (RssTerrainPaths.TryResolveTexturePath(pack, rssTextureBaseName, out string rssPath))
        {
            return LoadTextureFromPath(
                rssPath,
                rssTextureBaseName,
                loadMode,
                runtimeOwnership
            );
        }

        return null;
    }

    static Texture2D TryLoadTerrainTextureAsset(
        RssTerrainSplatPack pack,
        string textureBaseNameOrAssetPath
    )
    {
#if UNITY_EDITOR
        string assetPath = textureBaseNameOrAssetPath.StartsWith("Assets/", StringComparison.Ordinal) ?
            textureBaseNameOrAssetPath :
            RssTerrainPaths.ResolveAssetTexturePath(pack, textureBaseNameOrAssetPath);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#else
        return null;
#endif
    }

    static Texture2D LoadDiskTexture(
        string bodyName,
        string textureBaseName,
        TextureLoadMode loadMode,
        ICollection<Texture2D> runtimeOwnership
    )
    {
        if (!ParallaxTerrainPaths.TryResolveTexturePath(
                bodyName,
                textureBaseName,
                out string path
            ))
        {
            return null;
        }

        return LoadTextureFromPath(path, textureBaseName, loadMode, runtimeOwnership);
    }

    static Texture2D LoadTextureFromPath(
        string path,
        string textureBaseName,
        TextureLoadMode loadMode,
        ICollection<Texture2D> runtimeOwnership
    )
    {
        try
        {
            Texture2D texture = loadMode switch
            {
                TextureLoadMode.Color => DdsTextureLoader.LoadColorMap(path),
                TextureLoadMode.Normal => DdsTextureLoader.LoadNormalMap(path),
                _ => DdsTextureLoader.LoadDataMap(path),
            };
            runtimeOwnership?.Add(texture);
            return texture;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "PlanetParallaxTextures: failed to load " +
                textureBaseName +
                " from " +
                path +
                " (" +
                exception.Message +
                ")."
            );
            return null;
        }
    }

    enum TextureLoadMode
    {
        Color,
        Normal,
        Data,
    }

    public enum TextureChannel
    {
        Color,
        Normal,
        Data,
    }

    public static Texture2D LoadDdsFromAbsolutePath(
        string path,
        string textureBaseName,
        TextureChannel channel,
        ICollection<Texture2D> runtimeOwnership
    )
    {
        TextureLoadMode loadMode = channel switch
        {
            TextureChannel.Color => TextureLoadMode.Color,
            TextureChannel.Normal => TextureLoadMode.Normal,
            _ => TextureLoadMode.Data,
        };

        return LoadTextureFromPath(path, textureBaseName, loadMode, runtimeOwnership);
    }

    static void AssignTexture(
        Material material,
        string propertyName,
        Texture2D texture,
        bool linear,
        bool clampWrap
    )
    {
        if (!material.HasProperty(propertyName))
        {
            return;
        }

        if (texture == null)
        {
            if (!RssTerrainPaths.TerrainPackExistsOnDisk(RssTerrainSplatPack.ForLodBody()))
            {
                Debug.LogWarning(
                    "PlanetParallaxTextures: missing material slot " + propertyName + " — using placeholder."
                );
            }

            material.SetTexture(
                propertyName,
                propertyName.Contains("Normal") ?
                    Texture2D.normalTexture :
                    Texture2D.whiteTexture
            );
            return;
        }

        ConfigureTextureSampling(texture, clampWrap);
        material.SetTexture(propertyName, texture);
    }
}
