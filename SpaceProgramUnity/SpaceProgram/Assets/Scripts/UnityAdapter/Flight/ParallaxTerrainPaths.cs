using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Resolves Parallax_StockTerrainTextures paths in the original KSP layout:
/// Parallax_StockTerrainTextures/{BodyName}/PluginData/{texture}.dds
/// </summary>
public static class ParallaxTerrainPaths
{
    public const string RootFolderName = "Parallax_StockTerrainTextures";
    public const string PluginDataFolderName = "PluginData";
    public const string TerrainConfigFileName = "Terrain.cfg";

    /// <summary>
    /// Flight mode simulates Earth but uses Kerbin's Parallax terrain pack.
    /// </summary>
    public const string FlightParallaxBodyName = "Kerbin";

    public const string InfluenceTextureName = "influence";
    public const string LowAlbedoTextureName = "low00";
    public const string LowNormalTextureName = "low01";
    public const string MidAlbedoTextureName = "mid00";
    public const string MidNormalTextureName = "mid01";
    public const string HighAlbedoTextureName = "high00";
    public const string HighNormalTextureName = "high01";
    public const string SteepAlbedoTextureName = "steep00";
    public const string SteepNormalTextureName = "steep01";

    public const string ParallaxAssetFolder =
        "Assets/Content/Environment/Parallax/Kerbin/PluginData";

    static readonly string[] TextureExtensions = { ".dds", ".png", ".DDS", ".PNG" };

    public static string ResolveProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    public static string ResolveRoot()
    {
        return TryResolveRoot(out string resolvedPath) ?
            resolvedPath :
            Path.Combine(ResolveProjectRoot(), RootFolderName);
    }

    public static bool TryResolveRoot(out string resolvedPath)
    {
        List<string> candidates = new List<string>
        {
            Path.Combine(Application.streamingAssetsPath, RootFolderName),
            Path.Combine(ResolveProjectRoot(), RootFolderName),
            Path.Combine(Application.dataPath, RootFolderName),
        };

        string currentDirectory = Application.dataPath;

        for (int depth = 0; depth < 6; depth++)
        {
            candidates.Add(Path.Combine(currentDirectory, RootFolderName));

            string parentDirectory = Directory.GetParent(currentDirectory)?.FullName;

            if (string.IsNullOrEmpty(parentDirectory))
            {
                break;
            }

            currentDirectory = parentDirectory;
        }

        string preferredBodyName = NormalizeBodyName(FlightParallaxBodyName);

        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];

            if (!Directory.Exists(candidate))
            {
                continue;
            }

            if (HasPluginDataTexture(candidate, preferredBodyName, "low00"))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];

            if (Directory.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        resolvedPath = Path.Combine(ResolveProjectRoot(), RootFolderName);
        return false;
    }

    public static bool TerrainPackExistsOnDisk(string bodyName, string textureBaseName = "low00")
    {
        return TryResolveTexturePath(bodyName, textureBaseName, out _);
    }

    public static string ResolveBodyDirectory(string bodyName)
    {
        if (!TryResolveBodyDirectory(bodyName, out string bodyDirectory))
        {
            return Path.Combine(ResolveRoot(), NormalizeBodyName(bodyName));
        }

        return bodyDirectory;
    }

    public static string ResolveBodyPluginDataDirectory(string bodyName)
    {
        string bodyDirectory = ResolveBodyDirectory(bodyName);
        string pluginDataDirectory = Path.Combine(bodyDirectory, PluginDataFolderName);

        if (Directory.Exists(pluginDataDirectory))
        {
            return pluginDataDirectory;
        }

        return pluginDataDirectory;
    }

    public static bool TryResolveBodyDirectory(string bodyName, out string resolvedPath)
    {
        resolvedPath = null;

        if (!TryResolveRoot(out string root))
        {
            return false;
        }

        string normalizedBodyName = NormalizeBodyName(bodyName);
        string exactPath = Path.Combine(root, normalizedBodyName);

        if (Directory.Exists(exactPath))
        {
            resolvedPath = exactPath;
            return true;
        }

        string[] directories = Directory.GetDirectories(root);

        for (int i = 0; i < directories.Length; i++)
        {
            string directoryName = Path.GetFileName(directories[i]);

            if (string.Equals(
                    directoryName,
                    normalizedBodyName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                resolvedPath = directories[i];
                return true;
            }
        }

        return false;
    }

    public static string ResolveAssetTexturePath(string textureBaseName)
    {
        return ParallaxAssetFolder + "/" + NormalizeTextureBaseName(textureBaseName) + ".dds";
    }

    public static bool TryResolveTexturePath(
        string bodyName,
        string textureBaseName,
        out string resolvedPath
    )
    {
        resolvedPath = null;

        if (string.IsNullOrWhiteSpace(textureBaseName))
        {
            return false;
        }

        string assetPath = ResolveAssetTexturePath(textureBaseName);
        string absoluteAssetPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", assetPath)
        );

        if (File.Exists(absoluteAssetPath))
        {
            resolvedPath = absoluteAssetPath;
            return true;
        }

        if (!TryResolveBodyDirectory(bodyName, out string bodyDirectory))
        {
            return false;
        }

        string normalizedTextureName = NormalizeTextureBaseName(textureBaseName);
        string pluginDataDirectory = Path.Combine(bodyDirectory, PluginDataFolderName);

        if (Directory.Exists(pluginDataDirectory) &&
            TryFindTextureInDirectory(pluginDataDirectory, normalizedTextureName, out resolvedPath))
        {
            return true;
        }

        if (TryFindTextureInDirectory(bodyDirectory, normalizedTextureName, out resolvedPath))
        {
            return true;
        }

        return TryFindTextureRecursive(
            bodyDirectory,
            normalizedTextureName,
            maxDepth: 3,
            out resolvedPath
        );
    }

    public static IReadOnlyList<string> GetConfiguredBodyNames()
    {
        var bodyNames = new List<string>();

        if (!TryResolveRoot(out string root))
        {
            return bodyNames;
        }

        string[] directories = Directory.GetDirectories(root);

        for (int i = 0; i < directories.Length; i++)
        {
            string directoryName = Path.GetFileName(directories[i]);

            if (string.IsNullOrEmpty(directoryName) || directoryName.StartsWith("_"))
            {
                continue;
            }

            string pluginDataDirectory = Path.Combine(
                directories[i],
                PluginDataFolderName
            );

            if (Directory.Exists(pluginDataDirectory))
            {
                bodyNames.Add(directoryName);
            }
        }

        bodyNames.Sort();
        return bodyNames;
    }

    public static string NormalizeBodyName(string bodyName)
    {
        if (string.IsNullOrWhiteSpace(bodyName))
        {
            return FlightParallaxBodyName;
        }

        return bodyName.Trim();
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

        for (int extensionIndex = 0; extensionIndex < TextureExtensions.Length; extensionIndex++)
        {
            string candidatePath = Path.Combine(
                directory,
                textureBaseName + TextureExtensions[extensionIndex]
            );

            if (File.Exists(candidatePath))
            {
                resolvedPath = candidatePath;
                return true;
            }
        }

        string[] files = Directory.GetFiles(directory);

        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(files[i]);

            if (string.Equals(fileName, textureBaseName, StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = files[i];
                return true;
            }
        }

        return false;
    }

    static bool HasPluginDataTexture(
        string rootDirectory,
        string bodyName,
        string textureBaseName
    )
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return false;
        }

        string pluginDataDirectory = Path.Combine(
            rootDirectory,
            NormalizeBodyName(bodyName),
            PluginDataFolderName
        );

        return TryFindTextureInDirectory(
            pluginDataDirectory,
            NormalizeTextureBaseName(textureBaseName),
            out _
        );
    }

    static bool TryFindTextureRecursive(
        string directory,
        string textureBaseName,
        int maxDepth,
        out string resolvedPath
    )
    {
        resolvedPath = null;

        if (maxDepth < 0 || !Directory.Exists(directory))
        {
            return false;
        }

        if (TryFindTextureInDirectory(directory, textureBaseName, out resolvedPath))
        {
            return true;
        }

        string[] subdirectories = Directory.GetDirectories(directory);

        for (int i = 0; i < subdirectories.Length; i++)
        {
            if (TryFindTextureRecursive(
                    subdirectories[i],
                    textureBaseName,
                    maxDepth - 1,
                    out resolvedPath
                ))
            {
                return true;
            }
        }

        return false;
    }
}
