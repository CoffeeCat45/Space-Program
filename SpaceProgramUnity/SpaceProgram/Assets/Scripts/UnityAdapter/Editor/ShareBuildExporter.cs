#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// After a Windows player build, copies the exe folder to repo-root ShareBuild/
/// without planet maps / GEBCO / RSS DDS — small enough to send a friend.
/// Full Build/ stays local with datasets.
/// </summary>
class ShareBuildExporter : IPostprocessBuildWithReport
{
    const string ShareFolderName = "ShareBuild";
    const string ShareZipFileName = "ShareBuild.zip";
    const string ShareReadmeFileName = "SHARE.txt";

    public int callbackOrder => 50;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.result != BuildResult.Succeeded)
        {
            return;
        }

        if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
            report.summary.platform != BuildTarget.StandaloneWindows)
        {
            return;
        }

        string exePath = report.summary.outputPath;

        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            return;
        }

        ExportFromPlayerDirectory(Path.GetDirectoryName(exePath));
    }

    [MenuItem("Space Program/Export Share Build (no maps)")]
    public static void ExportFromLastWindowsPlayerMenu()
    {
        string buildExe = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../../../Build/SpaceProgram.exe")
        );

        if (!File.Exists(buildExe))
        {
            EditorUtility.DisplayDialog(
                "Share Build",
                "Нет Build/SpaceProgram.exe.\nСначала: Space Program → Build Windows Player.",
                "OK"
            );
            return;
        }

        ExportFromPlayerDirectory(Path.GetDirectoryName(buildExe));
    }

    public static void ExportFromPlayerDirectory(string playerDirectory)
    {
        if (string.IsNullOrEmpty(playerDirectory) || !Directory.Exists(playerDirectory))
        {
            Debug.LogWarning("ShareBuildExporter: player directory missing: " + playerDirectory);
            return;
        }

        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string shareDirectory = Path.Combine(repoRoot, ShareFolderName);
        string zipPath = Path.Combine(repoRoot, ShareZipFileName);

        try
        {
            if (Directory.Exists(shareDirectory))
            {
                Directory.Delete(shareDirectory, recursive: true);
            }

            Directory.CreateDirectory(shareDirectory);
            int copied = CopyPlayerWithoutHeavyMaps(playerDirectory, shareDirectory);
            WriteShareReadme(shareDirectory);

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(
                shareDirectory,
                zipPath,
                System.IO.Compression.CompressionLevel.Fastest,
                includeBaseDirectory: false
            );

            Debug.Log(
                "Space Program: ShareBuild " + copied + " file(s) → " + shareDirectory +
                " and " + zipPath
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("ShareBuildExporter: " + exception.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static int CopyPlayerWithoutHeavyMaps(string sourceDirectory, string destinationDirectory)
    {
        string[] files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        int copied = 0;

        for (int i = 0; i < files.Length; i++)
        {
            if (i == 0 || i % 32 == 0)
            {
                EditorUtility.DisplayProgressBar(
                    "Share Build",
                    "Copying player without maps…",
                    (float)i / Mathf.Max(1, files.Length)
                );
            }

            string sourceFile = files[i];
            string relative = sourceFile.Substring(sourceDirectory.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (ShouldSkipRelativePath(relative))
            {
                continue;
            }

            string destinationFile = Path.Combine(destinationDirectory, relative);
            string destinationFolder = Path.GetDirectoryName(destinationFile);

            if (!string.IsNullOrEmpty(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            File.Copy(sourceFile, destinationFile, overwrite: true);
            copied++;
        }

        return copied;
    }

    static bool ShouldSkipRelativePath(string relative)
    {
        if (string.IsNullOrEmpty(relative))
        {
            return true;
        }

        string normalized = relative.Replace('\\', '/');

        if (
            normalized.IndexOf(
                "_BurstDebugInformation_DoNotShip",
                StringComparison.OrdinalIgnoreCase
            ) >= 0 ||
            normalized.IndexOf(
                "BackUpThisFolder_ButDontShipItWithYourGame",
                StringComparison.OrdinalIgnoreCase
            ) >= 0
        )
        {
            return true;
        }

        if (
            ContainsFolder(normalized, "StreamingAssets/Planet") ||
            ContainsFolder(normalized, "StreamingAssets/MapMode") ||
            ContainsFolder(normalized, "StreamingAssets/Content/Environment/RSS-Terrain") ||
            ContainsFolder(normalized, "StreamingAssets/Content/Environment/RSS-Textures")
        )
        {
            return true;
        }

        string extension = Path.GetExtension(normalized);

        return
            extension.Equals(".raw", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".dds", StringComparison.OrdinalIgnoreCase);
    }

    static bool ContainsFolder(string normalizedRelative, string folder)
    {
        string needle = folder.Trim('/');
        return
            normalizedRelative.IndexOf("/" + needle + "/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedRelative.StartsWith(needle + "/", StringComparison.OrdinalIgnoreCase);
    }

    static void WriteShareReadme(string shareDirectory)
    {
        string text =
            "Space Program — сборка без карт планет (GEBCO/LDEM/PNG/RSS DDS).\n" +
            "Полный Build/ с датасетами остаётся у автора.\n\n" +
            "Игра запустится, но земля/луна без локальных карт не нарисуются.\n" +
            "Список тяжёлых файлов: GITHUB_HEAVY_FILES.md в репозитории.\n";
        File.WriteAllText(Path.Combine(shareDirectory, ShareReadmeFileName), text);
    }
}
#endif
