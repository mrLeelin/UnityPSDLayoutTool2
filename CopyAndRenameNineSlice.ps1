# ============================================
# PSDLayoutTool2 九宫模块复制并改名脚本
# ============================================
# 功能：将 PSDLayoutTool2 的九宫模块复制到 PSD2UIForm，并改名避免冲突
# 作者：Claude Code
# 日期：2025-01-20
# ============================================

param(
    [switch]$IncludeEditor = $true,  # 是否包含可视化编辑器
    [switch]$DryRun = $false         # 试运行模式（不实际写入文件）
)

$ErrorActionPreference = "Stop"

# 路径配置
$sourceDir = "E:\Project\Demo\monsterhunter\Assets\UnityPSDLayoutTool2\Assets\PSDLayoutTool2\Editor\NineSlice"
$targetDir = "E:\Project\Demo\monsterhunter\Assets\UnityPSDLayoutTool2\Assets\PSD2UIForm\Src\Editor\Psd2UGUI\NineSlice"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PSDLayoutTool2 九宫模块复制脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查源目录
if (-not (Test-Path $sourceDir)) {
    Write-Host "❌ 错误：源目录不存在: $sourceDir" -ForegroundColor Red
    exit 1
}

Write-Host "源目录: $sourceDir" -ForegroundColor Yellow
Write-Host "目标目录: $targetDir" -ForegroundColor Yellow
Write-Host "包含编辑器: $IncludeEditor" -ForegroundColor Yellow
Write-Host "试运行模式: $DryRun" -ForegroundColor Yellow
Write-Host ""

# 核心文件列表（必需）
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

# 可选文件列表（可视化编辑器）
$optionalFiles = @(
    "PsdNineSliceWindow.cs",
    "PsdNineSliceOverrideStore.cs",
    "PsdNineSlicePsdLayerSession.cs",
    "PsdNineSliceAssetState.cs"
)

# 确定处理的文件列表
$allFiles = $coreFiles
if ($IncludeEditor) {
    $allFiles += $optionalFiles
    Write-Host "✅ 将包含可视化编辑器文件（共 $($optionalFiles.Count) 个）" -ForegroundColor Green
} else {
    Write-Host "⚠️  跳过可视化编辑器文件" -ForegroundColor Yellow
}

Write-Host "总共将处理 $($allFiles.Count) 个文件" -ForegroundColor Cyan
Write-Host ""

# 类名替换映射（按长度降序，避免部分替换）
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

# 创建目标目录
if (-not $DryRun) {
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        Write-Host "✅ 创建目标目录: $targetDir" -ForegroundColor Green
    }
}

# 统计
$successCount = 0
$failCount = 0
$totalLines = 0

# 处理每个文件
foreach ($file in $allFiles) {
    $sourcePath = Join-Path $sourceDir $file

    # 检查源文件是否存在
    if (-not (Test-Path $sourcePath)) {
        Write-Host "⚠️  跳过不存在的文件: $file" -ForegroundColor Yellow
        $failCount++
        continue
    }

    # 目标文件名（Psd -> Psd2Ui）
    $targetFileName = $file -replace "^Psd", "Psd2Ui"
    $targetPath = Join-Path $targetDir $targetFileName

    Write-Host "处理: $file" -ForegroundColor White
    Write-Host "  -> $targetFileName" -ForegroundColor Gray

    try {
        # 读取源文件
        $content = Get-Content $sourcePath -Raw -Encoding UTF8
        $originalLength = $content.Length
        $lineCount = ($content -split "`n").Count
        $totalLines += $lineCount

        # 1. 替换命名空间
        $content = $content -replace "namespace PsdLayoutTool2", "namespace UGF.EditorTools.Psd2UGUI.NineSlice"

        # 2. 替换类名（按顺序，避免部分替换）
        foreach ($replacement in $classReplacements) {
            $pattern = [regex]::Escape($replacement.Old)
            $content = $content -replace $pattern, $replacement.New
        }

        # 3. 特殊处理：菜单路径
        if ($file -eq "PsdNineSliceWindow.cs") {
            $content = $content -replace "Assets/PSD Layout/", "Assets/PSD2UIForm/"
            $content = $content -replace 'GetWindow<PsdNineSliceWindow>', 'GetWindow<Psd2UiNineSliceWindow>'
        }

        # 写入目标文件
        if (-not $DryRun) {
            [System.IO.File]::WriteAllText($targetPath, $content, [System.Text.Encoding]::UTF8)

            # 复制 .meta 文件（如果存在）
            $sourceMetaPath = "$sourcePath.meta"
            $targetMetaPath = "$targetPath.meta"
            if (Test-Path $sourceMetaPath) {
                Copy-Item $sourceMetaPath $targetMetaPath -Force
            }
        }

        Write-Host "  ✅ 完成 ($lineCount 行)" -ForegroundColor Green
        $successCount++

    } catch {
        Write-Host "  ❌ 失败: $_" -ForegroundColor Red
        $failCount++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "处理完成！" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ 成功: $successCount 个文件" -ForegroundColor Green
Write-Host "❌ 失败: $failCount 个文件" -ForegroundColor Red
Write-Host "📝 总行数: $totalLines 行" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "⚠️  这是试运行，未实际写入文件" -ForegroundColor Yellow
    Write-Host "   移除 -DryRun 参数以执行实际操作" -ForegroundColor Yellow
} else {
    Write-Host "✅ 文件已写入到: $targetDir" -ForegroundColor Green
    Write-Host ""
    Write-Host "🔍 下一步操作:" -ForegroundColor Yellow
    Write-Host "  1. 在 Unity 中等待编译完成" -ForegroundColor White
    Write-Host "  2. 检查 Console 是否有错误" -ForegroundColor White
    Write-Host "  3. 修改 UGUIParser.cs 调用新的九宫算法" -ForegroundColor White
    Write-Host "  4. 修改 Psd2UIFormConverter.cs 传递图层名" -ForegroundColor White
    Write-Host "  5. 更新 Photoshop 脚本菜单（可选）" -ForegroundColor White
}

Write-Host ""
Write-Host "📄 详细方案文档: 九宫功能复制改名方案.md" -ForegroundColor Cyan
