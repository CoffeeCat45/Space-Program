#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

static class ReStockBlenderRunner
{
    const string BlenderPathEditorPref = "SpaceProgram.BlenderExecutablePath";

    internal static bool TryRunScanMu(string sourceMuRelative, out string log)
    {
        return TryRunBlenderScript("--scan-mu \"" + sourceMuRelative.Replace("\\", "/") + "\"", out log);
    }

    internal static bool TryRunExport(string partId, out string log)
    {
        if (string.IsNullOrWhiteSpace(partId))
        {
            return TryRunExportAll(out log);
        }

        return TryRunBlenderScript("--part " + partId, out log);
    }

    static bool TryRunBlenderScript(string scriptArguments, out string log)
    {
        log = string.Empty;

        string blenderPath = EditorPrefs.GetString(BlenderPathEditorPref, "blender");
        string scriptPath = Path.Combine(ReStockPartPaths.GetRepoRoot(), "Pipeline", "Blender", "export_part.py");

        if (!File.Exists(scriptPath))
        {
            log = "Missing export_part.py at " + scriptPath;
            return false;
        }

        string arguments = "--background --python \"" + scriptPath + "\" -- " + scriptArguments;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = blenderPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = ReStockPartPaths.GetRepoRoot(),
        };

        try
        {
            using Process process = Process.Start(startInfo);

            if (process == null)
            {
                log = "Failed to start Blender. Set path in Part Importer window.";
                return false;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            log = stdout + "\n" + stderr;

            if (process.ExitCode != 0)
            {
                log = "Blender exit code " + process.ExitCode + "\n" + log;
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            log = exception.Message;
            return false;
        }
    }

    internal static bool TryRunExportAll(out string log)
    {
        return TryRunBlenderScript("--all", out log);
    }

    internal static string GetBlenderPath()
    {
        return EditorPrefs.GetString(BlenderPathEditorPref, "blender");
    }

    internal static void SetBlenderPath(string path)
    {
        EditorPrefs.SetString(BlenderPathEditorPref, path ?? "blender");
    }
}
#endif
