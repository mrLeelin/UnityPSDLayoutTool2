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

try {
    & $PythonPath $renderer --mode snapshot --prefab-path $PrefabAssetPath --output $payloadPath
    if ($LASTEXITCODE -ne 0) {
        throw "Snapshot payload rendering failed with exit code $LASTEXITCODE."
    }

    $unityResult = & uloop execute-dynamic-code --project-path $ProjectPath --code-file $payloadPath 2>&1 | Out-String
    $unityExitCode = $LASTEXITCODE
    Write-Output $unityResult
    if ($unityExitCode -ne 0 -or $unityResult -notmatch '"Success"\s*:\s*true') {
        throw "Unity snapshot did not return a successful result."
    }
}
finally {
    if (Test-Path -LiteralPath $payloadPath) {
        Remove-Item -LiteralPath $payloadPath -Force
    }
}
