# Patches legacy Plugins for Unity 6.3+ (CS0592 SerializeField, CS0618 WWW).
# Run while Unity is closed if firstpass compile is blocked:
#   powershell -ExecutionPolicy Bypass -File Tools\FixUnity6Plugins.ps1
#
# Default: legacy KSP/Vectrosity plugins are disabled via LegacyKspFirstPass.asmdef
# (define LEGACY_KSP_PLUGINS_ENABLED only if you need that code).

param(
    [string]$UnityProjectPath = (Join-Path $PSScriptRoot "..\SpaceProgramUnity\SpaceProgram")
)

$ErrorActionPreference = "Stop"

function Get-UnityAssetsPath {
    param([string]$ProjectPath)
    $assets = Join-Path $ProjectPath "Assets"
    if (-not (Test-Path $assets)) {
        throw "Unity Assets folder not found: $assets"
    }
    return $assets
}

function Find-PluginFiles {
    param([string]$AssetsPath, [string]$FileName)
    Get-ChildItem -Path $AssetsPath -Recurse -Filter $FileName -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]' }
}

function Remove-SerializeFieldLines {
    param([string]$Text)
    $lines = $Text -split "`r?`n"
    $output = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ($trimmed -eq "[SerializeField]" -or $trimmed -eq "[UnityEngine.SerializeField]") {
            continue
        }
        $output.Add($line)
    }
    return ($output -join "`n")
}

function Fix-VectorLineFile {
    param([string]$Path)
    $original = Get-Content -Raw -LiteralPath $Path
    $patched = Remove-SerializeFieldLines $original
    if ($patched -eq $original) {
        return $false
    }
    Set-Content -LiteralPath $Path -Value $patched -NoNewline
    Write-Host "[FixUnity6Plugins] Removed SerializeField lines in $Path"
    return $true
}

function Fix-ProjectModelLoadFile {
    param([string]$Path)
    $original = Get-Content -Raw -LiteralPath $Path
    $patched = $original

    if ($patched -notmatch "using UnityEngine\.Networking;") {
        $patched = $patched -replace "(using UnityEngine;)", "`$1`nusing UnityEngine.Networking;"
    }

    $patched = [regex]::Replace(
        $patched,
        "public\s+static\s+Mesh\s+ReadMesh\s*\(\s*WWW\s+(\w+)\s*\)\s*\{[^{}]*\}",
        "public static Mesh ReadMesh(UnityWebRequest `$1)`n    {`n        if (`$1 == null || `$1.downloadHandler == null)`n        {`n            return null;`n        }`n`n        return ReadMesh(`$1.downloadHandler.data);`n    }",
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    if ($patched -eq $original) {
        return $false
    }

    Set-Content -LiteralPath $Path -Value $patched -NoNewline
    Write-Host "[FixUnity6Plugins] Patched WWW usage in $Path"
    return $true
}

$assetsPath = Get-UnityAssetsPath $UnityProjectPath
$changed = 0

foreach ($path in (Find-PluginFiles $assetsPath "VectorLine.cs")) {
    if (Fix-VectorLineFile $path.FullName) { $changed++ }
}

foreach ($path in (Find-PluginFiles $assetsPath "ProjectModelLoad.cs")) {
    if (Fix-ProjectModelLoadFile $path.FullName) { $changed++ }
}

if ($changed -eq 0) {
    Write-Host "[FixUnity6Plugins] No plugin source files changed."
    Write-Host "If compile errors persist, ensure LegacyKspFirstPass.asmdef is present and LEGACY_KSP_PLUGINS_ENABLED is NOT set."
} else {
    Write-Host "[FixUnity6Plugins] Done ($changed file(s)). Reopen Unity or let it recompile."
}
