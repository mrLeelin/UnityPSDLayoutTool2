# 工单03：七日签到的具体抽取示例

状态：讨论示例，尚未作为实现方案确认。2026-09-14 只读检查现有 Prefab；未修改资源。

## 已核对的现有结果

素材目录：`E:/Project/Demo/monsterhunter/Assets/PSDLayoutTool2/TestData/7日签到拆分/Prefab`。

主界面引用 `Common/ReusableItemVariant.prefab` 七次。其两个分支通过实例的启用状态覆盖选择显示内容，第七个实例关闭 State_1、开启 State_2。

```text
ReusableItemVariant
├─ [Common]（目前为空）
└─ [States]
   ├─ [State_1] → [FlatSibling_flat_sibling_001]
   │  ├─ CardBackground
   │  ├─ DayLabel
   │  ├─ RewardIcon
   │  └─ RewardAmount
   └─ [State_2] → [FlatSibling_flat_sibling_007]
      ├─ CardFrame
      ├─ day 7
      ├─ Currency_Gold_Activity
      ├─ 100m_2
      ├─ Currency_Power
      └─ 35
```

以上按用途排列展示，非序列化兄弟顺序。普通分支是日期、背景、一个奖励图标和数量；第七天分支包含日期、边框和两种奖励及数量。

另有 `DayVariant.prefab`，主界面引用三次（Day_1、Day_2、Day_7）；Common 也为空，两个分支分别包含 CardFrame/StateIndicator 和 CardBackground。不能把这个资产与七个奖励实例当成同一种结构。

## 建议 PSD2UIForm 达到的效果

以七个奖励区为例：整理前分别存在，整理后七个位置引用同一个奖励公共 Prefab。普通日期显示普通分支，第七天显示双奖励分支；每个实例保留自身图片、文字、布局和默认可见性。

这一步的最低目标是复用一个包含不同结构的 Prefab。现有素材的 Common 为空，说明它当前主要复用完整分支，并没有把日期、背景等进一步提取到共同节点。不能仅凭名称相似就把 CardBackground 和 CardFrame 合并。真正共享的成员需要映射与布局、排序、引用验证；无法安全合并的节点继续留在各自分支。

建议首版沿用这种可见结果，不要求用户编写切换代码。是否附带运行时切换接口不影响本示例；此项仍未确认，不视为已授权的新功能。

## 待用户判断

七个奖励位置共用一个 Prefab，其中保留普通和双奖励两种结构，各位置仍显示原来的样子，是否就是期望的抽取结果？
