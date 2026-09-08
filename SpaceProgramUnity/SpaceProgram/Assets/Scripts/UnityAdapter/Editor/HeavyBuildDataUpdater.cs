#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Copies planet maps / GEBCO / RSS into an existing Windows player without
/// running BuildPipeline. Does not run after the main player build.
/// </summary>
static class HeavyBuildDataUpdater
{
    const string WindowsPlayerPathRelative = "../../../Build/SpaceProgram.exe";

    [MenuItem("Space Program/Update Heavy Build Data")]
    public static void UpdateHeavyBuildDataMenu()
    {
        string buildExe = Path.GetFullPath(
            Path.Combine(Application.dataPath, WindowsPlayerPathRelative)
        );

        if (!File.Exists(buildExe))
        {
            EditorUtility.DisplayDialog(
                "Heavy Build Data",
                "Нет Build/SpaceProgram.exe.\nСначала: Space Program → Build Windows Player.",
                "OK"
            );
            return;
        }

        string playerDirectory = Path.GetDirectoryName(buildExe);
        string dataDirectory = Path.Combine(
            playerDirectory,
            Path.GetFileNameWithoutExtension(buildExe) + "_Data"
        );
        string streamingRoot = Path.Combine(dataDirectory, "StreamingAssets");

        try
        {
            int copied = CopyHeavyPlayerData(
                Application.dataPath,
                streamingRoot,
                showProgress: true
            );
            EditorUtility.DisplayDialog(
                "Heavy Build Data",
                "Обновлено файлов: " + copied + "\n→ " + streamingRoot,
                "OK"
            );
            Debug.Log(
                "Space Program: heavy player data " + copied +
                " file(s) → " + streamingRoot
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("HeavyBuildDataUpdater: " + exception.Message);
            EditorUtility.DisplayDialog(
                "Heavy Build Data",
                "Ошибка: " + exception.Message,
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    public static int CopyHeavyPlayerData(
        string assetsDirectory,
        string playerStreamingRoot,
        bool showProgress = false
    )
    {
        if (string.IsNullOrEmpty(assetsDirectory) ||
            string.IsNullOrEmpty(playerStreamingRoot) ||
            !Directory.Exists(assetsDirectory))
        {
            return 0;
        }

        Directory.CreateDirectory(playerStreamingRoot);
        int copied = 0;

        copied += CopyDirectory(
            Path.Combine(assetsDirectory, "StreamingAssets", "Planet"),
            Path.Combine(playerStreamingRoot, "Planet"),
            showProgress
        );
        copied += CopyDirectory(
            Path.Combine(assetsDirectory, "StreamingAssets", "MapMode"),
            Path.Combine(playerStreamingRoot, "MapMode"),
            showProgress
        );
        copied += CopyDirectory(
            Path.Combine(
                assetsDirectory,
                "Content",
                "Environment",
                "RSS-Terrain"
            ),
            Path.Combine(
                playerStreamingRoot,
                "Content",
                "Environment",
                "RSS-Terrain"
            ),
            showProgress
        );
        copied += CopyDirectory(
            Path.Combine(
                assetsDirectory,
                "Content",
                "Environment",
                "RSS-Textures"
            ),
            Path.Combine(
                playerStreamingRoot,
                "Content",
                "Environment",
                "RSS-Textures"
            ),
            showProgress
        );
        copied += CopyExtraHeavyStreamingAssets(
            Path.Combine(assetsDirectory, "StreamingAssets"),
            playerStreamingRoot
        );
        return copied;
    }

    static int CopyExtraHeavyStreamingAssets(string sourceStreamingRoot, string destinationStreamingRoot)
    {
        if (!Directory.Exists(sourceStreamingRoot))
        {
            return 0;
        }

        int copied = 0;
        string[] files = Directory.GetFiles(sourceStreamingRoot, "*", SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            string sourceFile = files[i];
            if (!ShouldCopyFile(sourceFile))
            {
                continue;
            }

            string relative = sourceFile.Substring(sourceStreamingRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string playerRelative = ("StreamingAssets/" + relative.Replace('\\', '/'))
                .TrimStart('/');

            if (!ShareBuildExporter.ShouldSkipRelativePath(playerRelative))
            {
                continue;
            }

            if (IsCoveredByNamedHeavyFolder(relative))
            {
                continue;
            }

            string destinationFile = Path.Combine(destinationStreamingRoot, relative);
            copied += CopyOneFile(sourceFile, destinationFile);
        }

        return copied;
    }

    static bool IsCoveredByNamedHeavyFolder(string relativeFromStreamingAssets)
    {
        string normalized = relativeFromStreamingAssets.Replace('\\', '/');
        return
            StartsWithFolder(normalized, "Planet") ||
            StartsWithFolder(normalized, "MapMode") ||
            StartsWithFolder(normalized, "Content/Environment/RSS-Terrain") ||
            StartsWithFolder(normalized, "Content/Environment/RSS-Textures");
    }

    static bool StartsWithFolder(string normalizedRelative, string folder)
    {
        string needle = folder.Trim('/');
        return
            normalizedRelative.StartsWith(needle + "/", StringComparison.OrdinalIgnoreCase) ||
            normalizedRelative.Equals(needle, StringComparison.OrdinalIgnoreCase);
    }

    static int CopyDirectory(
        string sourceDirectory,
        string destinationDirectory,
        bool showProgress
    )
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return 0;
        }

        Directory.CreateDirectory(destinationDirectory);
        int copied = 0;
        string[] files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            if (showProgress && (i == 0 || i % 16 == 0))
            {
                EditorUtility.DisplayProgressBar(
                    "Heavy Build Data",
                    "Updating maps…",
                    (float)i / Mathf.Max(1, files.Length)
                );
            }

            string sourceFile = files[i];
            if (!ShouldCopyFile(sourceFile))
            {
                continue;
            }

            string relative = sourceFile.Substring(sourceDirectory.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destinationFile = Path.Combine(destinationDirectory, relative);
            copied += CopyOneFile(sourceFile, destinationFile);
        }

        return copied;
    }

    static int CopyOneFile(string sourceFile, string destinationFile)
    {
        string destinationFolder = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        File.Copy(sourceFile, destinationFile, overwrite: true);
        return 1;
    }

    static bool ShouldCopyFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        if (extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string normalized = path.Replace('\\', '/');
        return
            normalized.IndexOf("/@thumbs/", StringComparison.OrdinalIgnoreCase) < 0 &&
            !normalized.StartsWith("@thumbs/", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
