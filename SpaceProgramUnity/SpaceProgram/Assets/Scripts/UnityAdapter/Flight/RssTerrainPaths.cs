using System;
using System.IO;
using UnityEngine;

/// <summary>
/// RSS-Terrain close-up splat paths.
/// Earth: <see cref="EarthAssetFolder"/> then PluginData/03_Earth.
/// Moon: Content/.../RSS-Terrain/Moon then PluginData/03-01_Moon.
/// Does not use Kerbin Parallax_StockTerrainTextures.
/// </summary>
public static class RssTerrainPaths
{
    public const string RootFolderName = "RSS_Terrain";
    public const string GameDataFolderName = "GameData";
    public const string ModFolderName = "RSS-Terrain";
    public const string TerrainFolderName = "Terrain";
    public const string PluginDataFolderName = "PluginData";
    public const string EarthBodyFolderName = "03_Earth";

    public const string InfluenceFileName = "Earth_Influence";
    public const string LowColorFileName = "EarthLow_Color";
    public const string LowNormalFileName = "EarthLow_NRM";
    public const string MidColorFileName = "EarthMid_Color";
    public const string MidNormalFileName = "EarthMid_NRM";
    public const string HighColorFileName = "EarthHigh_Color";
    public const string HighNormalFileName = "EarthHigh_NRM";
    public const string SteepColorFileName = "EarthSteep_Color";
    public const string SteepNormalFileName = "EarthSteep_NRM";
    public const string DisplacementFileName = "Earth_Displacement";

    public const string EarthAssetFolder =
        "Assets/Content/Environment/RSS-Terrain/Earth";

    static readonly string[] TextureExtensions = { ".dds", ".png", ".DDS", ".PNG" };

    public static string ResolveProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    /// <summary>Absolute path to Assets/Content/Environment/RSS-Terrain/Earth.</summary>
    public static string ResolveEarthContentAbsoluteDirectory()
    {
        return Path.Combine(
            Application.dataPath,
            "Content",
            "Environment",
            "RSS-Terrain",
            "Earth"
        );
    }

    public static string ResolveLegacyPluginDataDirectory()
    {
        return Path.Combine(
            ResolveProjectRoot(),
            RootFolderName,
            GameDataFolderName,
            ModFolderName,
            TerrainFolderName,
            PluginDataFolderName,
            EarthBodyFolderName
        );
    }

    public static string ResolveEarthPluginDataDirectory()
    {
        if (TryResolveEarthTextureRootDirectory(out string resolved))
        {
            return resolved;
        }

        return ResolveEarthContentAbsoluteDirectory();
    }

    /// <summary>
    /// Preferred Earth RSS texture folder (Content first, then legacy 03_Earth).
    /// </summary>
    public static bool TryResolveEarthTextureRootDirectory(out string resolvedPath)
    {
        string streamingDirectory = PlayerContentPaths.RssTerrainStreamingDirectory(
            RssTerrainSplatPack.Earth.ContentBodyFolderName
        );
        if (Directory.Exists(streamingDirectory))
        {
            resolvedPath = streamingDirectory;
            return true;
        }

        string contentDirectory = ResolveEarthContentAbsoluteDirectory();
        if (Directory.Exists(contentDirectory))
        {
            resolvedPath = contentDirectory;
            return true;
        }

        string legacyDirectory = ResolveLegacyPluginDataDirectory();
        if (Directory.Exists(legacyDirectory))
        {
            resolvedPath = legacyDirectory;
            return true;
        }

        resolvedPath = null;
        return false;
    }

    public static bool TryResolveEarthPluginDataDirectory(out string resolvedPath)
    {
        return TryResolveEarthTextureRootDirectory(out resolvedPath);
    }

    public static string ResolveAssetTexturePath(string textureBaseName)
    {
        return ResolveAssetTexturePath(RssTerrainSplatPack.Earth, textureBaseName);
    }

    public static string ResolveAssetTexturePath(
        RssTerrainSplatPack pack,
        string textureBaseName
    )
    {
        return pack.ContentAssetFolder + "/" + NormalizeTextureBaseName(textureBaseName) + ".dds";
    }

    public static bool TryResolveTexturePath(string textureBaseName, out string resolvedPath)
    {
        return TryResolveTexturePath(RssTerrainSplatPack.Earth, textureBaseName, out resolvedPath);
    }

    public static bool TryResolveTexturePath(
        RssTerrainSplatPack pack,
        string textureBaseName,
        out string resolvedPath
    )
    {
        resolvedPath = null;

        if (string.IsNullOrWhiteSpace(textureBaseName))
        {
            return false;
        }

        string normalizedBaseName = NormalizeTextureBaseName(textureBaseName);

        if (TryFindTextureInDirectory(
                PlayerContentPaths.RssTerrainStreamingDirectory(pack.ContentBodyFolderName),
                normalizedBaseName,
                out resolvedPath
            ))
        {
            return true;
        }

        if (TryFindTextureInDirectory(
                pack.ResolveContentAbsoluteDirectory(),
                normalizedBaseName,
                out resolvedPath
            ))
        {
            return true;
        }

        return TryFindTextureInDirectory(
            pack.ResolveLegacyPluginDataDirectory(),
            normalizedBaseName,
            out resolvedPath
        );
    }

    public static bool EarthTerrainPackExistsOnDisk()
    {
        return TerrainPackExistsOnDisk(RssTerrainSplatPack.Earth);
    }

    public static bool TerrainPackExistsOnDisk(RssTerrainSplatPack pack)
    {
        if (!TryResolveTexturePath(pack, pack.MidColorFileName, out _))
        {
            return false;
        }

        if (pack.ContentBodyFolderName == RssTerrainSplatPack.Earth.ContentBodyFolderName)
        {
            return TryResolveTexturePath(pack, pack.InfluenceFileName, out _);
        }

        return true;
    }

    static string NormalizeTextureBaseName(string textureBaseName)
    {
        return Path.GetFileNameWithoutExtension(textureBaseName.Trim());
    }

    static bool TryFindTextureInDirectory(
        string directory,
        string textureBaseName,
        out string resolvedPath
    )
    {
        resolvedPath = null;

        if (!Directory.Exists(directory))
        {
            return false;
        }

        for (int i = 0; i < TextureExtensions.Length; i++)
        {
            string candidate = Path.Combine(directory, textureBaseName + TextureExtensions[i]);

            if (File.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        return false;
    }
}
