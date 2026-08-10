# 视觉+逻辑混合方案实现进度

## 实现日期
2026-08-07

## 已完成的工作

### 1. 核心数据模型
**文件**: `PsdHierarchyVisualScore.cs`
- ✅ `PsdHierarchyVisualScore` - 节点视觉评分结构（0-100分）
- ✅ `PsdHierarchyVisualAnalysisResult` - 分析结果集合
- ✅ 推荐阈值（80%）、可接受阈值（60%）判断逻辑

### 2. 视觉分析基础设施
**文件**: `PsdHierarchyVisualAnalyzer.cs`
- ✅ `TryCaptureThumbnail()` - RenderTexture 渲染节点缩略图
- ✅ `CaptureThumbnails()` - 批量截图（支持 Prefab Stage）
- ✅ `SaveThumbnail()` - 保存 PNG 到临时目录
- ✅ `CleanupTempScreenshots()` - 清理临时文件
- ✅ 路径解析（支持重名节点的 `#索引` 标记）
- ✅ 保持宽高比的缩略图缩放（256x256）

### 3. AI 提示词构建
**文件**: `PsdHierarchyVisualChatPrompt.cs`
- ✅ `BuildVisualSimilarityPrompt()` - 生成结构化提示词
  - 模板组说明
  - 候选组合列表
  - 评分标准（布局、颜色、风格）
  - JSON 输出格式定义
- ✅ `TryParseVisualScores()` - 解析 AI 返回的 JSON 评分

### 4. 数据结构增强
**文件**: `PsdHierarchyLocalRepairScope.cs`
- ✅ `PsdHierarchyCrossParentPrefabExtraction` 添加 `visualAnalysis` 字段
- ✅ `GetVisualScore()` - 获取实例的平均视觉评分
- ✅ `GetRecommendedInstances()` - 过滤推荐实例

### 5. 执行流程集成
**文件**: `PsdHierarchyChatCleanupExecution.cs`
- ✅ `PsdHierarchyLocalRepairAnalysisResult` - 分析结果结构体
- ✅ `TryBuildLocalPrefabOrganizationPlanWithVisualAsync()` - 异步分析入口
- ⚠️ `PerformVisualAnalysisAsync()` - **当前使用模拟数据**

### 6. UI 集成
**文件**: `PsdHierarchyLocalRepairWindow.cs`
- ✅ 修改 `AnalyzeLocalRepair()` 为异步方法
- ✅ `BuildPlanDetailsWithVisualScores()` - 显示评分详情
  - 实例序号 + 评分百分比
  - 评分图标（✓✓/✓/?/✗）
  - 评分原因说明
- ✅ `GetScoreIcon()` - 评分图标映射

### 7. 单元测试
**文件**: `PsdHierarchyVisualScoreTests.cs`
- ✅ 评分范围限制测试（0-100）
- ✅ 推荐/可接受阈值测试
- ✅ 结果集查询测试

---

## 当前架构

```
用户选择节点
    ↓
锁定选区
    ↓
点击"分析局部整理"
    ↓
┌─────────────────────────────────────┐
│ TryBuildLocalPrefabOrganizationPlan │ ← 纯逻辑分析
│ (结构、命名模式匹配)                  │
└─────────────────────────────────────┘
    ↓ 生成候选实例
┌─────────────────────────────────────┐
│ PerformVisualAnalysisAsync          │ ← 视觉分析（TODO）
│ 1. 生成缩略图                        │
│ 2. 构建 AI 提示词                    │
│ 3. 调用 AI API                       │
│ 4. 解析评分 JSON                     │
└─────────────────────────────────────┘
    ↓ 视觉评分结果
┌─────────────────────────────────────┐
│ BuildPlanDetailsWithVisualScores    │ ← UI 显示
│ 显示每个实例的评分和原因              │
└─────────────────────────────────────┘
    ↓
用户审查并确认
```

---

## TODO：待实现功能

### 高优先级

1. **真实 AI 视觉分析实现**
   ```csharp
   // 在 PsdHierarchyChatCleanupExecution.cs 中替换模拟实现
   private static async Task<PsdHierarchyVisualAnalysisResult> PerformVisualAnalysisAsync(
       PsdHierarchyChatContext context,
       PsdHierarchyCrossParentPrefabExtraction extraction)
   {
       // 1. 生成所有候选节点的缩略图
       var allNodeIds = extraction.instances
           .SelectMany(inst => inst.sourceNodeIds)
           .Concat(extraction.templateSourceNodeIds)
           .Distinct()
           .ToArray();
       
       var thumbnails = PsdHierarchyVisualAnalyzer.CaptureThumbnails(context, allNodeIds);
       
       // 2. 构建提示词
       string prompt = PsdHierarchyVisualChatPrompt.BuildVisualSimilarityPrompt(
           context,
           extraction.templateSourceNodeIds,
           extraction.instances);
       
       // 3. 调用 AI API（需要集成 PsdHierarchyChatClient）
       // TODO: 发送 prompt + thumbnails 到 AI
       string aiResponse = await CallAiWithImages(prompt, thumbnails);
       
       // 4. 解析结果
       if (!PsdHierarchyVisualChatPrompt.TryParseVisualScores(
               aiResponse,
               out PsdHierarchyVisualAnalysisResult result,
               out string error))
       {
           throw new Exception("解析视觉评分失败: " + error);
       }
       
       // 5. 清理临时文件
       PsdHierarchyVisualAnalyzer.CleanupTempScreenshots();
       
       return result;
   }
   ```

2. **集成 PsdHierarchyChatClient 发送图像**
   - 研究现有的 `PsdHierarchyChatClient` API
   - 添加支持多模态（文本+图像）的方法
   - 处理图像编码（Base64 或文件路径）

### 中优先级

3. **UI 增强**
   - 添加"启用视觉分析"开关（Toggle）
   - 添加"视觉相似度阈值"滑块（60%-100%）
   - 显示缩略图预览（可选）

4. **性能优化**
   - 缓存已分析的结果（避免重复调用 AI）
   - 异步生成缩略图（后台线程）
   - 限制同时分析的节点数量

### 低优先级

5. **错误处理**
   - AI 调用失败时的降级策略（回退到纯逻辑）
   - 截图失败时的提示和跳过逻辑
   - 网络超时处理

6. **用户体验**
   - 显示分析进度条
   - 支持取消正在进行的分析
   - 保存用户的阈值设置

---

## 验证步骤

### 编译验证
```bash
# 在 Unity 中等待编译完成
# 检查 Console 是否有编译错误
```

### 功能验证
1. 打开包含重复 UI 元素的 Prefab
2. 进入 Prefab Stage
3. 选择几个代表性节点（例如：GiftBox4, DateMarker5, DateText4）
4. 右键 → "AI 局部整理"
5. 点击"锁定当前选区"
6. 输入组件名称（例如：DaySignRewardItem）
7. 点击"分析局部整理"
8. 查看是否显示：
   - ✅ 模板组信息
   - ✅ 匹配实例数量
   - ✅ 每个实例的视觉评分（当前是模拟数据）
   - ✅ 评分图标和原因

### 预期输出示例
```
模板组：node:n004, node:n205, node:n304
匹配实例：4 组

【视觉相似度评分】
  实例 1 (序号 1): ✓ 85% - 视觉高度相似
  实例 2 (序号 2): ✓ 85% - 视觉高度相似
  实例 3 (序号 3): ✓ 85% - 视觉高度相似
  实例 4 (序号 4): ✓✓ 100% - 模板组

评分说明：
  ✓ 90-100%: 强烈推荐
  ✓ 80-89%: 推荐
  ? 60-79%: 有差异，建议审查
  ✗ 0-59%: 不推荐

未匹配节点：无
```

---

## 设计决策记录

### 为什么使用模拟数据？
- **原因**: 真实 AI 集成需要调研现有 `PsdHierarchyChatClient` 的多模态支持
- **计划**: 第一步先验证数据流和 UI，第二步再替换为真实 AI 调用

### 为什么评分阈值是 80%？
- **推荐阈值 80%**: 高度相似，适合自动组件化
- **可接受阈值 60%**: 有一定差异但可用，需要用户审查
- **拒绝阈值 <60%**: 差异过大，不推荐组件化

### 为什么在结构体中不能直接用 LINQ？
- **问题**: C# 结构体的 lambda 表达式不能访问 `this`
- **解决**: 将 `this` 复制到局部变量 `self`，在 lambda 中使用 `self`

---

## 文件清单

### 新增文件
- ✅ `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualScore.cs`
- ✅ `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualAnalyzer.cs`
- ✅ `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyVisualChatPrompt.cs`
- ✅ `Assets/PSDLayoutTool2/Editor/Tests/PsdHierarchyVisualScoreTests.cs`

### 修改文件
- ✅ `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyLocalRepairScope.cs`
- ✅ `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyChatCleanupExecution.cs`
- ✅ `Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyLocalRepairWindow.cs`

---

## 下一步行动

### 立即执行
1. ✅ 修复编译错误（结构体 lambda 访问问题）
2. 🔄 在 Unity 中验证编译通过
3. 🔄 运行单元测试验证基础功能

### 后续开发
1. 实现真实的视觉分析（替换模拟数据）
2. 集成 AI API 调用
3. 添加 UI 开关和配置选项

---

## 参考资料

- `PsdHierarchyChatClient.cs` - 现有 AI 调用接口
- `PsdHierarchyLocalRepairWindow.cs` - UI 窗口实现
- `PsdHierarchyLocalRepairScope.cs` - 选区和验证逻辑
