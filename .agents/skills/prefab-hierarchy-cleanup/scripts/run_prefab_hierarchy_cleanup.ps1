[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ProjectPath,

    [Parameter(Mandatory)]
    [string]$PlanPath,

    [switch]$ApplyConfirmed,

    [switch]$VerifyOnly,

    [switch]$CompileOnly,

    [switch]$Preflight,

    [switch]$Reapply,

    [string]$PythonPath = "python"
)

$ErrorActionPreference = "Stop"
$script:UloopExitCode = 0

function Get-UnityFailureDetail {
    param([string]$Result)

    $detail = if ($null -eq $Result) { "" } else { $Result.Trim() }
    if ([string]::IsNullOrWhiteSpace($detail)) {
        return "Unity did not return a failure detail."
    }

    try {
        $response = $detail | ConvertFrom-Json
        foreach ($propertyName in @("ErrorMessage", "Error", "Exception", "Message")) {
            $property = $response.PSObject.Properties[$propertyName]
            if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
                return ([string]$property.Value).Trim()
            }
        }
    }
    catch {
        # Preserve the original output when a runner writes non-JSON diagnostics.
    }

    return $detail
}

function Invoke-UloopCli {
    param([string[]]$Arguments)

    $uloopCommand = Get-Command -Name "uloop.cmd", "uloop" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -ne $uloopCommand) {
        & $uloopCommand.Source @Arguments
        $script:UloopExitCode = $LASTEXITCODE
        return
    }

    # Unity inherits the system Node.js path, but a global uloop-cli install is optional.
    # Use the package-matched CLI through npx when the global command is unavailable.
    $npxCommand = Get-Command -Name "npx.cmd", "npx" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $npxCommand) {
        throw "Neither uloop nor npx is available. Install Node.js or the uloop-cli package."
    }

    & $npxCommand.Source --yes "uloop-cli@2.2.0" @Arguments
    $script:UloopExitCode = $LASTEXITCODE
}

$selectedModes = 0
if ($ApplyConfirmed) { $selectedModes++ }
if ($VerifyOnly) { $selectedModes++ }
if ($CompileOnly) { $selectedModes++ }
if ($Preflight) { $selectedModes++ }
if ($Reapply) { $selectedModes++ }
if ($selectedModes -ne 1) {
    throw "Choose exactly one mode: -ApplyConfirmed, -VerifyOnly, -CompileOnly, -Preflight, or -Reapply."
}

$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
$PlanPath = [System.IO.Path]::GetFullPath($PlanPath)
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath "Assets"))) {
    throw "ProjectPath is not a Unity project: $ProjectPath"
}
if (-not (Test-Path -LiteralPath $PlanPath)) {
    throw "PlanPath does not exist: $PlanPath"
}

try {
    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $planJson = [System.IO.File]::ReadAllText($PlanPath, $utf8)
    $rawPlan = $planJson | ConvertFrom-Json
}
catch {
    throw "PlanPath does not contain valid JSON: $($_.Exception.Message)"
}

$prefabAssetPath = ([string]$rawPlan.prefabAssetPath).Trim().Replace('\', '/')
$outputMode = ([string]$rawPlan.output.mode).Trim()
$outputAssetPath = ([string]$rawPlan.output.assetPath).Trim().Replace('\', '/')
if ([string]::IsNullOrWhiteSpace($prefabAssetPath) -or
    $outputMode -ne 'in_place' -or
    -not [string]::Equals($outputAssetPath, $prefabAssetPath, [System.StringComparison]::Ordinal)) {
    throw "This cleanup only supports in-place output: output.mode must be 'in_place' and output.assetPath must exactly equal prefabAssetPath. Copy and .cleaned.prefab outputs are not allowed."
}

$skillRoot = Split-Path -Parent $PSScriptRoot
$renderer = Join-Path $PSScriptRoot "render_prefab_cleanup.py"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "prefab-hierarchy-cleanup"
[System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
$payloadPath = Join-Path $temporaryRoot (([System.IO.Path]::GetFileNameWithoutExtension($PlanPath)) + "." + [guid]::NewGuid().ToString("N") + ".cs")

if ($ApplyConfirmed) {
    $mode = "apply"
} elseif ($CompileOnly) {
    $mode = "apply"
} elseif ($Preflight) {
    $mode = "preflight"
} elseif ($Reapply) {
    $mode = "reapply"
} else {
    $mode = "verify"
}

$priorOutputEncoding = $OutputEncoding
$priorConsoleOutputEncoding = [Console]::OutputEncoding
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = $OutputEncoding

try {
    & $PythonPath $renderer --plan $PlanPath --mode $mode --output $payloadPath
    if ($LASTEXITCODE -ne 0) {
        throw "Plan validation/rendering failed with exit code $LASTEXITCODE."
    }

    $cliArguments = @("execute-dynamic-code", "--project-path", $ProjectPath)
    if ($CompileOnly) {
        $cliArguments += @("--compile-only", "true")
    }
    $cliArguments += @("--code-file", $payloadPath)
    $unityResult = Invoke-UloopCli -Arguments $cliArguments 2>&1 | Out-String
    $unityExitCode = $script:UloopExitCode
    if ($unityExitCode -ne 0 -or $unityResult -notmatch '"Success"\s*:\s*true') {
        $failureDetail = Get-UnityFailureDetail $unityResult
        if ($ApplyConfirmed) {
            throw "Unity apply failed: $failureDetail"
        }
        if ($CompileOnly) {
            throw "Unity compile failed: $failureDetail"
        }
        if ($Preflight) {
            throw "Unity preflight failed: $failureDetail"
        }
        if ($Reapply) {
            throw "Unity reapply failed: $failureDetail"
        }
        throw "Unity verification failed: $failureDetail"
    }
    Write-Output $unityResult
}
catch {
    [pscustomobject]@{
        success = $false
        error = $_.Exception.Message
    } | ConvertTo-Json -Compress
    exit 1
}
finally {
    if (Test-Path -LiteralPath $payloadPath) {
        Remove-Item -LiteralPath $payloadPath -Force
    }
    [Console]::OutputEncoding = $priorConsoleOutputEncoding
    $OutputEncoding = $priorOutputEncoding
}
