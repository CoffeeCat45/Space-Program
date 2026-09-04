#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Patches legacy Plugins (Vectrosity, KSP ProjectModelLoad) for Unity 6.3 compile rules.
/// Note: firstpass plugins compile before Editor scripts — if compile is blocked, run Tools/FixUnity6Plugins.ps1
/// or keep LegacyKspFirstPass.asmdef (LEGACY_KSP_PLUGINS_ENABLED unset).
/// </summary>
static class Unity6PluginCompatibilityFixer
{
    const string MenuPath = "Space Program/Fix Unity 6 Plugin Compile Errors";

    [MenuItem(MenuPath)]
    static void FixFromMenu()
    {
        if (TryFixAll(logWhenChanged: true))
        {
            Debug.Log("[Unity6PluginFix] Patched plugin source. Recompile should clear CS0592 / WWW warnings.");
        }
        else
        {
            Debug.Log(
                "[Unity6PluginFix] No changes. Legacy plugins may already be disabled via LegacyKspFirstPass.asmdef."
            );
        }
    }

    static bool TryFixAll(bool logWhenChanged)
    {
        bool changed = false;
        string assetsRoot = Application.dataPath;

        foreach (string absolutePath in Directory.GetFiles(
            assetsRoot,
            "VectorLine.cs",
            SearchOption.AllDirectories
        ))
        {
            if (IsEditorScriptPath(absolutePath))
            {
                continue;
            }

            changed |= FixVectorLine(absolutePath);
        }

        foreach (string absolutePath in Directory.GetFiles(
            assetsRoot,
            "ProjectModelLoad.cs",
            SearchOption.AllDirectories
        ))
        {
            if (IsEditorScriptPath(absolutePath))
            {
                continue;
            }

            changed |= FixProjectModelLoad(absolutePath);
        }

        if (changed)
        {
            AssetDatabase.Refresh();
            if (logWhenChanged)
            {
                Debug.Log("[Unity6PluginFix] Patched legacy plugin source file(s).");
            }
        }

        return changed;
    }

    static bool IsEditorScriptPath(string absolutePath)
    {
        return absolutePath.Replace('\\', '/').Contains("/Editor/");
    }

    static bool FixVectorLine(string absolutePath)
    {
        string text = File.ReadAllText(absolutePath);
        string patched = RemoveSerializeFieldLines(text);

        if (patched == text)
        {
            return false;
        }

        File.WriteAllText(absolutePath, patched);
        Debug.Log("[Unity6PluginFix] Removed SerializeField lines in " + ToAssetPath(absolutePath));
        return true;
    }

    static string RemoveSerializeFieldLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var output = new System.Collections.Generic.List<string>(lines.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed == "[SerializeField]" || trimmed == "[UnityEngine.SerializeField]")
            {
                continue;
            }

            output.Add(lines[i]);
        }

        return string.Join("\n", output);
    }

    static bool FixProjectModelLoad(string absolutePath)
    {
        string text = File.ReadAllText(absolutePath);
        string patched = text;

        if (!patched.Contains("using UnityEngine.Networking;") && patched.Contains("UnityWebRequest"))
        {
            patched = InsertUsing(patched, "using UnityEngine.Networking;");
        }

        patched = Regex.Replace(
            patched,
            @"public\s+static\s+Mesh\s+ReadMesh\s*\(\s*WWW\s+(\w+)\s*\)\s*\{[^{}]*\}",
            "public static Mesh ReadMesh(UnityEngine.Networking.UnityWebRequest $1)\n" +
            "    {\n" +
            "        if ($1 == null || $1.downloadHandler == null)\n" +
            "        {\n" +
            "            return null;\n" +
            "        }\n\n" +
            "        return ReadMesh($1.downloadHandler.data);\n" +
            "    }",
            RegexOptions.Singleline
        );

        if (patched == text)
        {
            return false;
        }

        File.WriteAllText(absolutePath, patched);
        Debug.Log("[Unity6PluginFix] Patched WWW usage in " + ToAssetPath(absolutePath));
        return true;
    }

    static string InsertUsing(string text, string usingLine)
    {
        int lastUsing = -1;
        var lines = text.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("using ") && lines[i].EndsWith(";"))
            {
                lastUsing = i;
            }
        }

        if (lastUsing < 0)
        {
            return usingLine + "\n" + text;
        }

        var output = new System.Collections.Generic.List<string>(lines);
        output.Insert(lastUsing + 1, usingLine);
        return string.Join("\n", output);
    }

    static string ToAssetPath(string absolutePath)
    {
        string normalized = absolutePath.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');

        if (!normalized.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath;
        }

        return "Assets" + normalized.Substring(dataPath.Length);
    }
}
#endif
