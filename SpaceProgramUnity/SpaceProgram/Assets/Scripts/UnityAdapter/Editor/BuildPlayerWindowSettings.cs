#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
static class BuildPlayerWindowSettings
{
    const int DefaultWidth = 1600;
    const int DefaultHeight = 900;
    const string WindowsPlayerPathRelative = "../../../Build/SpaceProgram.exe";

    static BuildPlayerWindowSettings()
    {
        ApplyWindowedDefaults();
    }

    [MenuItem("Space Program/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        ApplyWindowedDefaults();
        string outputPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, WindowsPlayerPathRelative)
        );
        string outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log(
            "Space Program: Windows player " +
            report.summary.result +
            " → " +
            outputPath
        );

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception(
                "Windows player build failed: " + report.summary.result
            );
        }
    }

    [MenuItem("Space Program/Use Windowed Player Build")]
    static void ApplyWindowedDefaultsMenuItem()
    {
        ApplyWindowedDefaults();
        Debug.Log(
            "Space Program: player build set to windowed " +
            DefaultWidth + "x" + DefaultHeight + "."
        );
    }

    static void ApplyWindowedDefaults()
    {
        bool changed = false;

        if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed)
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            changed = true;
        }

        if (PlayerSettings.defaultScreenWidth != DefaultWidth)
        {
            PlayerSettings.defaultScreenWidth = DefaultWidth;
            changed = true;
        }

        if (PlayerSettings.defaultScreenHeight != DefaultHeight)
        {
            PlayerSettings.defaultScreenHeight = DefaultHeight;
            changed = true;
        }

        if (!PlayerSettings.resizableWindow)
        {
            PlayerSettings.resizableWindow = true;
            changed = true;
        }

        if (changed)
        {
            Debug.Log(
                "Space Program: applied windowed player defaults (" +
                DefaultWidth + "x" + DefaultHeight + ")."
            );
        }
    }
}
#endif
