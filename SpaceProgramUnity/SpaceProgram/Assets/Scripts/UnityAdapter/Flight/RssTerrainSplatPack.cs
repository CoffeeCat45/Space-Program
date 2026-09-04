using System.IO;
using UnityEngine;

/// <summary>
/// RSS-Terrain close-up splat pack (Low/Mid/High/Steep + influence noise).
/// Same layout for every LOD body; only folder names and file prefixes change.
/// </summary>
public readonly struct RssTerrainSplatPack
{
    public readonly string ContentBodyFolderName;
    public readonly string LegacyPluginDataFolderName;
    public readonly string InfluenceFileName;
    public readonly string LowColorFileName;
    public readonly string LowNormalFileName;
    public readonly string MidColorFileName;
    public readonly string MidNormalFileName;
    public readonly string HighColorFileName;
    public readonly string HighNormalFileName;
    public readonly string SteepColorFileName;
    public readonly string SteepNormalFileName;

    public RssTerrainSplatPack(
        string contentBodyFolderName,
        string legacyPluginDataFolderName,
        string filePrefix
    )
    {
        ContentBodyFolderName = contentBodyFolderName;
        LegacyPluginDataFolderName = legacyPluginDataFolderName;
        InfluenceFileName = filePrefix + "_Influence";
        LowColorFileName = filePrefix + "Low_Color";
        LowNormalFileName = filePrefix + "Low_NRM";
        MidColorFileName = filePrefix + "Mid_Color";
        MidNormalFileName = filePrefix + "Mid_NRM";
        HighColorFileName = filePrefix + "High_Color";
        HighNormalFileName = filePrefix + "High_NRM";
        SteepColorFileName = filePrefix + "Steep_Color";
        SteepNormalFileName = filePrefix + "Steep_NRM";
    }

    public static RssTerrainSplatPack Earth { get; } = new RssTerrainSplatPack(
        "Earth",
        RssTerrainPaths.EarthBodyFolderName,
        "Earth"
    );

    public static RssTerrainSplatPack Moon { get; } = new RssTerrainSplatPack(
        "Moon",
        "03-01_Moon",
        "Moon"
    );

    public static RssTerrainSplatPack ForLodBody()
    {
        return CelestialTerrainRuntime.IsMoon ? Moon : Earth;
    }

    public string ContentAssetFolder =>
        "Assets/Content/Environment/RSS-Terrain/" + ContentBodyFolderName;

    public string ResolveContentAbsoluteDirectory()
    {
        return Path.Combine(
            Application.dataPath,
            "Content",
            "Environment",
            "RSS-Terrain",
            ContentBodyFolderName
        );
    }

    public string ResolveLegacyPluginDataDirectory()
    {
        return Path.Combine(
            RssTerrainPaths.ResolveProjectRoot(),
            RssTerrainPaths.RootFolderName,
            RssTerrainPaths.GameDataFolderName,
            RssTerrainPaths.ModFolderName,
            RssTerrainPaths.TerrainFolderName,
            RssTerrainPaths.PluginDataFolderName,
            LegacyPluginDataFolderName
        );
    }

    public bool TryResolveRootDirectory(out string resolvedPath)
    {
        string streamingDirectory = PlayerContentPaths.RssTerrainStreamingDirectory(
            ContentBodyFolderName
        );
        if (DirectoryHasSplatTextures(streamingDirectory, MidColorFileName))
        {
            resolvedPath = streamingDirectory;
            return true;
        }

        string contentDirectory = ResolveContentAbsoluteDirectory();
        if (DirectoryHasSplatTextures(contentDirectory, MidColorFileName))
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

    static bool DirectoryHasSplatTextures(string directory, string midColorFileName)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, midColorFileName + ".dds")) ||
            File.Exists(Path.Combine(directory, midColorFileName + ".png"));
    }
}
