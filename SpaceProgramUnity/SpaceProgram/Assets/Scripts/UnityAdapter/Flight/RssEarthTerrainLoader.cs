using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads RSS-Terrain close-up DDS packs (Earth, Moon, …) from Content or PluginData.
/// </summary>
static class RssEarthTerrainLoader
{
    public static bool TryLoadTerrainSet(
        ICollection<Texture2D> runtimeOwnership,
        out PlanetParallaxTerrainSet terrainSet
    )
    {
        return TryLoadTerrainSet(RssTerrainSplatPack.Earth, runtimeOwnership, out terrainSet);
    }

    public static bool TryLoadTerrainSet(
        RssTerrainSplatPack pack,
        ICollection<Texture2D> runtimeOwnership,
        out PlanetParallaxTerrainSet terrainSet
    )
    {
        terrainSet = new PlanetParallaxTerrainSet();

        if (!RssTerrainPaths.TerrainPackExistsOnDisk(pack))
        {
            Debug.LogWarning(
                "RssEarthTerrainLoader: RSS-Terrain pack not found for " +
                pack.ContentBodyFolderName +
                " at " +
                pack.ResolveContentAbsoluteDirectory()
            );
            return false;
        }

        terrainSet.InfluenceMap = Load(
            pack,
            pack.InfluenceFileName,
            PlanetParallaxTextures.TextureChannel.Data,
            runtimeOwnership
        );
        terrainSet.LowAlbedo = Load(
            pack,
            pack.LowColorFileName,
            PlanetParallaxTextures.TextureChannel.Color,
            runtimeOwnership
        );
        terrainSet.LowNormal = Load(
            pack,
            pack.LowNormalFileName,
            PlanetParallaxTextures.TextureChannel.Normal,
            runtimeOwnership
        );
        terrainSet.MidAlbedo = Load(
            pack,
            pack.MidColorFileName,
            PlanetParallaxTextures.TextureChannel.Color,
            runtimeOwnership
        );
        terrainSet.MidNormal = Load(
            pack,
            pack.MidNormalFileName,
            PlanetParallaxTextures.TextureChannel.Normal,
            runtimeOwnership
        );
        terrainSet.HighAlbedo = Load(
            pack,
            pack.HighColorFileName,
            PlanetParallaxTextures.TextureChannel.Color,
            runtimeOwnership
        );
        terrainSet.HighNormal = Load(
            pack,
            pack.HighNormalFileName,
            PlanetParallaxTextures.TextureChannel.Normal,
            runtimeOwnership
        );
        terrainSet.SteepAlbedo = Load(
            pack,
            pack.SteepColorFileName,
            PlanetParallaxTextures.TextureChannel.Color,
            runtimeOwnership
        );
        terrainSet.SteepNormal = Load(
            pack,
            pack.SteepNormalFileName,
            PlanetParallaxTextures.TextureChannel.Normal,
            runtimeOwnership
        );

        bool valid = terrainSet.MidAlbedo != null;
        if (pack.ContentBodyFolderName == RssTerrainSplatPack.Earth.ContentBodyFolderName)
        {
            valid = valid && terrainSet.InfluenceMap != null;
        }

        if (valid)
        {
            pack.TryResolveRootDirectory(out string rootDirectory);
            Debug.Log(
                "RssEarthTerrainLoader: loaded RSS-Terrain " +
                pack.ContentBodyFolderName +
                " set from " +
                rootDirectory
            );
        }
        else
        {
            Debug.LogError(
                "RssEarthTerrainLoader: failed to load required close-up textures for " +
                pack.ContentBodyFolderName +
                " from " +
                pack.ResolveContentAbsoluteDirectory()
            );
        }

        return valid;
    }

    static Texture2D Load(
        RssTerrainSplatPack pack,
        string textureBaseName,
        PlanetParallaxTextures.TextureChannel channel,
        ICollection<Texture2D> runtimeOwnership
    )
    {
        if (!RssTerrainPaths.TryResolveTexturePath(pack, textureBaseName, out string path))
        {
            Debug.LogWarning(
                "RssEarthTerrainLoader: missing file " + textureBaseName +
                " under " + pack.ResolveContentAbsoluteDirectory()
            );
            return null;
        }

        return PlanetParallaxTextures.LoadDdsFromAbsolutePath(
            path,
            textureBaseName,
            channel,
            runtimeOwnership
        );
    }
}
