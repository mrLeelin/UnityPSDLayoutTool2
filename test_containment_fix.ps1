# 测试 containmentResolutions 自动修复
$snapshotPath = "E:\Project\Demo\monsterhunter\Library\PSDLayoutTool2\HierarchySnapshots\57e5a61f71300531b0f0a2821593c239f1528ba6c53e8c6a7b22a8ac34eb08fb.json"

if (Test-Path $snapshotPath) {
    $snapshot = Get-Content $snapshotPath -Raw | ConvertFrom-Json
    Write-Host "=== 快照中的 containmentFindings ==="
    Write-Host "数量: $($snapshot.containmentFindings.Count)"

    if ($snapshot.containmentFindings.Count -gt 0) {
        $finding = $snapshot.containmentFindings[0]
        Write-Host "`n第一个 finding:"
        Write-Host "  innerParent: $($finding.innerParent)"
        Write-Host "  innerCandidateId: $($finding.innerCandidateId)"
        Write-Host "  maxAreaRatio: $($finding.maxAreaRatio)"
        Write-Host "  mapping 数量: $($finding.mapping.Count)"

        if ($finding.mapping.Count -gt 0) {
            Write-Host "`n需要解决的 sources:"
            foreach ($pair in $finding.mapping) {
                Write-Host "  - $($pair.source) (位于 $($pair.containedBy) 内)"
            }
        }
    }
} else {
    Write-Host "快照文件不存在"
}
