#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Picks a ReStock .mu and runs scan → export → Unity import in one flow.
/// </summary>
static class ReStockMuPicker
{
    internal static bool TryPickMuFile(out string sourceMuRelative)
    {
        sourceMuRelative = string.Empty;
        string assetsRoot = Path.Combine(ReStockPartPaths.GetRepoRoot(), "ReStock", "Assets");

        if (!Directory.Exists(assetsRoot))
        {
            EditorUtility.DisplayDialog(
                "ReStock",
                "Folder not found:\n" + assetsRoot,
                "OK"
            );
            return false;
        }

        string picked = EditorUtility.OpenFilePanel("Select ReStock .mu", assetsRoot, "mu");

        if (string.IsNullOrWhiteSpace(picked))
        {
            return false;
        }

        string normalizedAssets = assetsRoot.Replace('\\', '/');
        string normalizedPick = picked.Replace('\\', '/');

        if (!normalizedPick.StartsWith(normalizedAssets, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "ReStock",
                "Pick a .mu file inside:\n" + assetsRoot,
                "OK"
            );
            return false;
        }

        sourceMuRelative = normalizedPick.Substring(normalizedAssets.Length).TrimStart('/');
        return !string.IsNullOrWhiteSpace(sourceMuRelative);
    }

    internal static string GetPartIdFromSourceMu(string sourceMuRelative)
    {
        return Path.GetFileNameWithoutExtension(sourceMuRelative.Replace('\\', '/'));
    }
}
#endif
