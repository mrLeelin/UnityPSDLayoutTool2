[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ProjectPath,

    [Parameter(Mandatory)]
    [string]$PrefabAssetPath,

    [string]$PythonPath = "python"
)

$ErrorActionPreference = "Stop"

$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath "Assets"))) {
    throw "ProjectPath is not a Unity project: $ProjectPath"
}
if (-not $PrefabAssetPath.StartsWith("Assets/")) {
    throw "PrefabAssetPath must be project-relative and start with Assets/: $PrefabAssetPath"
}

$renderer = Join-Path $PSScriptRoot "render_prefab_cleanup.py"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "prefab-hierarchy-cleanup"
[System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
$payloadPath = Join-Path $temporaryRoot ("snapshot." + [guid]::NewGuid().ToString("N") + ".cs")

$unityCommand = Get-Command -Name "unity.exe", "unity" -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $unityCommand) {
    throw "The Unity Pipeline CLI command is unavailable. Install or expose the project's unity CLI before taking a NativeUnity snapshot."
}

try {
    & $PythonPath $renderer --mode snapshot --prefab-path $PrefabAssetPath --output $payloadPath
    if ($LASTEXITCODE -ne 0) {
        throw "Snapshot payload rendering failed with exit code $LASTEXITCODE."
    }

    $unityResult = & $unityCommand.Source `
        --json `
        --non-interactive `
        command `
        --project-path $ProjectPath `
        --timeout 40 `
        eval_file `
        -- `
        --file $payloadPath `
        --timeout 30000 2>&1 | Out-String
    $unityExitCode = $LASTEXITCODE
    Write-Output $unityResult
    if ($unityExitCode -ne 0 -or $unityResult -notmatch '"success"\s*:\s*true') {
        throw "NativeUnity snapshot failed: $($unityResult.Trim())"
    }
}
finally {
    if (Test-Path -LiteralPath $payloadPath) {
        Remove-Item -LiteralPath $payloadPath -Force
    }
}
