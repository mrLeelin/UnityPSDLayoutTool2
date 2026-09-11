# Copy and Rename NineSlice Module
# From: PSDLayoutTool2 -> To: PSD2UIForm

param(
    [switch]$IncludeEditor = $true,
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

$sourceDir = "E:\Project\Demo\monsterhunter\Assets\UnityPSDLayoutTool2\Assets\PSDLayoutTool2\Editor\NineSlice"
$targetDir = "E:\Project\Demo\monsterhunter\Assets\UnityPSDLayoutTool2\Assets\PSD2UIForm\Src\Editor\Psd2UGUI\NineSlice"

Write-Host "========================================"
Write-Host "PSDLayoutTool2 NineSlice Copy Script"
Write-Host "========================================"
Write-Host ""
Write-Host "Source: $sourceDir"
Write-Host "Target: $targetDir"
Write-Host "Include Editor: $IncludeEditor"
Write-Host "Dry Run: $DryRun"
Write-Host ""

if (-not (Test-Path $sourceDir)) {
    Write-Host "ERROR: Source directory not found: $sourceDir"
    exit 1
}

$coreFiles = @(
    "PsdNineSliceModels.cs",
    "PsdNineSliceRaster.cs",
    "PsdNineSliceNameRules.cs",
    "PsdNineSliceAnalyzer.cs",
    "PsdNineSliceCropper.cs",
    "PsdNineSliceCropSafety.cs",
    "PsdNineSliceAutoProcessor.cs",
    "PsdNineSliceAutoProcessorCore.cs",
    "PsdNineSliceImportPolicy.cs",
    "PsdNineSliceTextureProcessor.cs"
)

$optionalFiles = @(
    "PsdNineSliceWindow.cs",
    "PsdNineSliceOverrideStore.cs",
    "PsdNineSlicePsdLayerSession.cs",
    "PsdNineSliceAssetState.cs"
)

$allFiles = $coreFiles
if ($IncludeEditor) {
    $allFiles += $optionalFiles
    Write-Host "Including editor files ($($optionalFiles.Count) files)"
}

Write-Host "Total files to process: $($allFiles.Count)"
Write-Host ""

$classReplacements = @(
    @{ Old = "PsdNineSliceUnityAutoProcessor"; New = "Psd2UiNineSliceUnityAutoProcessor" },
    @{ Old = "PsdNineSliceAutoProcessorCore"; New = "Psd2UiNineSliceAutoProcessorCore" },
    @{ Old = "PsdNineSliceTextureProcessor"; New = "Psd2UiNineSliceTextureProcessor" },
    @{ Old = "PsdNineSlicePsdLayerSession"; New = "Psd2UiNineSlicePsdLayerSession" },
    @{ Old = "PsdNineSliceOverrideStore"; New = "Psd2UiNineSliceOverrideStore" },
    @{ Old = "PsdNineSliceImportPolicy"; New = "Psd2UiNineSliceImportPolicy" },
    @{ Old = "PsdNineSliceAutoProcessor"; New = "Psd2UiNineSliceAutoProcessor" },
    @{ Old = "PsdNineSliceCropSafety"; New = "Psd2UiNineSliceCropSafety" },
    @{ Old = "PsdNineSliceAssetState"; New = "Psd2UiNineSliceAssetState" },
    @{ Old = "PsdNineSliceConfidence"; New = "Psd2UiNineSliceConfidence" },
    @{ Old = "PsdNineSliceInference"; New = "Psd2UiNineSliceInference" },
    @{ Old = "PsdNineSliceNameRules"; New = "Psd2UiNineSliceNameRules" },
    @{ Old = "PsdNineSliceNameRule"; New = "Psd2UiNineSliceNameRule" },
    @{ Old = "PsdNineSliceAnalyzer"; New = "Psd2UiNineSliceAnalyzer" },
    @{ Old = "PsdNineSliceCropper"; New = "Psd2UiNineSliceCropper" },
    @{ Old = "PsdNineSliceWindow"; New = "Psd2UiNineSliceWindow" },
    @{ Old = "PsdNineSliceRaster"; New = "Psd2UiNineSliceRaster" },
    @{ Old = "PsdNineSliceBorder"; New = "Psd2UiNineSliceBorder" },
    @{ Old = "PsdNineSliceMode"; New = "Psd2UiNineSliceMode" }
)

if (-not $DryRun) {
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        Write-Host "Created target directory"
    }
}

$successCount = 0
$failCount = 0
$totalLines = 0

foreach ($file in $allFiles) {
    $sourcePath = Join-Path $sourceDir $file

    if (-not (Test-Path $sourcePath)) {
        Write-Host "SKIP: File not found: $file"
        $failCount++
        continue
    }

    $targetFileName = $file -replace "^Psd", "Psd2Ui"
    $targetPath = Join-Path $targetDir $targetFileName

    Write-Host "Processing: $file"
    Write-Host "  -> $targetFileName"

    try {
        $content = Get-Content $sourcePath -Raw -Encoding UTF8
        $lineCount = ($content -split "`n").Count
        $totalLines += $lineCount

        # Replace namespace
        $content = $content -replace "namespace PsdLayoutTool2", "namespace UGF.EditorTools.Psd2UGUI.NineSlice"

        # Replace class names
        foreach ($replacement in $classReplacements) {
            $pattern = [regex]::Escape($replacement.Old)
            $content = $content -replace $pattern, $replacement.New
        }

        # Special handling for Window menu path
        if ($file -eq "PsdNineSliceWindow.cs") {
            $content = $content -replace "Assets/PSD Layout/", "Assets/PSD2UIForm/"
            $content = $content -replace 'GetWindow<PsdNineSliceWindow>', 'GetWindow<Psd2UiNineSliceWindow>'
        }

        if (-not $DryRun) {
            [System.IO.File]::WriteAllText($targetPath, $content, [System.Text.Encoding]::UTF8)

            $sourceMetaPath = "$sourcePath.meta"
            $targetMetaPath = "$targetPath.meta"
            if (Test-Path $sourceMetaPath) {
                Copy-Item $sourceMetaPath $targetMetaPath -Force
            }
        }

        Write-Host "  OK ($lineCount lines)"
        $successCount++

    } catch {
        Write-Host "  FAILED: $_"
        $failCount++
    }
}

Write-Host ""
Write-Host "========================================"
Write-Host "Complete!"
Write-Host "========================================"
Write-Host "Success: $successCount files"
Write-Host "Failed: $failCount files"
Write-Host "Total lines: $totalLines"
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN - No files written"
    Write-Host "Remove -DryRun to execute"
} else {
    Write-Host "Files written to: $targetDir"
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "  1. Wait for Unity to compile"
    Write-Host "  2. Check Console for errors"
    Write-Host "  3. Modify UGUIParser.cs"
    Write-Host "  4. Modify Psd2UIFormConverter.cs"
}
