using System.IO;
using UnityEngine;

/// <summary>
/// Player-build file roots. Editor keeps reading from <c>Assets/</c> and folders
/// next to the Unity project; the standalone player reads copies under
/// <c>StreamingAssets/Content</c> packed at build time.
/// </summary>
public static class PlayerContentPaths
{
    public const string ContentFolderName = "Content";
    public const string EnvironmentFolderName = "Environment";
    public const string RssTerrainFolderName = "RSS-Terrain";
    public const string RssTexturesFolderName = "RSS-Textures";
    public const string ShipsFolderName = "Ships";

    public static string StreamingRoot => Application.streamingAssetsPath;

    public static string CombineStreaming(params string[] relativeParts)
    {
        string path = StreamingRoot;

        if (relativeParts == null)
        {
            return path;
        }

        for (int i = 0; i < relativeParts.Length; i++)
        {
            if (string.IsNullOrEmpty(relativeParts[i]))
            {
                continue;
            }

            path = Path.Combine(path, relativeParts[i]);
        }

        return path;
    }

    public static string RssTerrainStreamingDirectory(string bodyFolderName)
    {
        return CombineStreaming(
            ContentFolderName,
            EnvironmentFolderName,
            RssTerrainFolderName,
            bodyFolderName
        );
    }

    public static string RssTexturesStreamingDirectory()
    {
        return CombineStreaming(
            ContentFolderName,
            EnvironmentFolderName,
            RssTexturesFolderName
        );
    }

    public static string ShipsStreamingDirectory()
    {
        return CombineStreaming(ShipsFolderName);
    }

    public static bool TryFirstExistingDirectory(out string resolvedPath, params string[] candidates)
    {
        resolvedPath = null;

        if (candidates == null)
        {
            return false;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];

            if (string.IsNullOrEmpty(candidate) || !Directory.Exists(candidate))
            {
                continue;
            }

            resolvedPath = candidate;
            return true;
        }

        return false;
    }

    public static bool TryFirstExistingFile(out string resolvedPath, params string[] candidates)
    {
        resolvedPath = null;

        if (candidates == null)
        {
            return false;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];

            if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate))
            {
                continue;
            }

            resolvedPath = candidate;
            return true;
        }

        return false;
    }
}
