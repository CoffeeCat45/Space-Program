#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Packs editor-only disk content into the Windows player:
/// baked part catalog, always-included shaders, skybox Resources copy,
/// RSS DDS under StreamingAssets, VAB ship JSON next to the exe.
/// </summary>
class PlayerContentBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    static readonly string[] CopyFileExtensions =
    {
        ".dds",
        ".png",
        ".jpg",
        ".jpeg",
        ".json",
        ".ship",
        ".bin",
        ".raw",
        ".size",
    };

    static readonly string[] RssTextureFileNames =
    {
        "EarthColor.dds",
        "Earth_NRM.dds",
        "MoonColor.dds",
        "Moon_NRM.dds",
    };

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        BuildShaderIncluder.EnsureRegistered();
        GalaxySkyboxSetup.EnsurePlayerSkyboxMaterial();
        RuntimePartCatalogBaker.Bake();
        CopyShipsIntoProjectStreamingAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Space Program: player content prepared (catalog, shaders, skybox, Ships JSON).");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
            report.summary.platform != BuildTarget.StandaloneWindows)
        {
            return;
        }

        string exePath = report.summary.outputPath;

        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        string outputDirectory = Path.GetDirectoryName(exePath);
        string dataDirectory = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(exePath) + "_Data"
        );
        string streamingRoot = Path.Combine(dataDirectory, "StreamingAssets");
        Directory.CreateDirectory(streamingRoot);

        int copied = 0;
        copied += CopyDirectoryFiltered(
            Path.Combine(Application.dataPath, "Content", "Environment", "RSS-Terrain"),
            Path.Combine(streamingRoot, "Content", "Environment", "RSS-Terrain")
        );
        copied += CopyRssTextureSubset(
            Path.Combine(Application.dataPath, "Content", "Environment", "RSS-Textures"),
            Path.Combine(streamingRoot, "Content", "Environment", "RSS-Textures")
        );
        copied += CopyShipsNextToPlayer(outputDirectory, streamingRoot);

        Debug.Log(
            "Space Program: copied " + copied +
            " player data file(s) into StreamingAssets / Ships next to " + exePath
        );
    }

    static void CopyShipsIntoProjectStreamingAssets()
    {
        string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Ships"));
        string destination = Path.Combine(Application.dataPath, "StreamingAssets", "Ships");
        CopyDirectoryFiltered(source, destination);
    }

    static int CopyShipsNextToPlayer(string outputDirectory, string streamingRoot)
    {
        string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Ships"));
        int copied = CopyDirectoryFiltered(source, Path.Combine(outputDirectory, "Ships"));
        copied += CopyDirectoryFiltered(source, Path.Combine(streamingRoot, "Ships"));
        return copied;
    }

    static int CopyRssTextureSubset(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return 0;
        }

        Directory.CreateDirectory(destinationDirectory);
        int copied = 0;

        for (int i = 0; i < RssTextureFileNames.Length; i++)
        {
            string fileName = RssTextureFileNames[i];
            string sourceFile = Path.Combine(sourceDirectory, fileName);

            if (!File.Exists(sourceFile))
            {
                continue;
            }

            string destinationFile = Path.Combine(destinationDirectory, fileName);
            File.Copy(sourceFile, destinationFile, overwrite: true);
            copied++;
        }

        return copied;
    }

    static int CopyDirectoryFiltered(string sourceDirectory, string destinationDirectory)
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
            string sourceFile = files[i];

            if (!ShouldCopyFile(sourceFile))
            {
                continue;
            }

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

    static bool ShouldCopyFile(string path)
    {
        string extension = Path.GetExtension(path);

        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        if (extension.Equals(".meta", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (int i = 0; i < CopyFileExtensions.Length; i++)
        {
            if (extension.Equals(CopyFileExtensions[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static bool ShouldSkipRelativePath(string relative)
    {
        if (string.IsNullOrEmpty(relative))
        {
            return true;
        }

        string normalized = relative.Replace('\\', '/');

        if (
            normalized.IndexOf("/@thumbs/", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.StartsWith("@thumbs/", System.StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        string fileName = Path.GetFileName(normalized);
        return fileName.StartsWith("_vab_launch", System.StringComparison.OrdinalIgnoreCase);
    }
}
#endif
