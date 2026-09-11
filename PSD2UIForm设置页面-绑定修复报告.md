# PSD2UIForm 设置页面 ↔ Psd2UIFormConfig.asset 绑定问题 · 排查与修复报告

结论先行：**不是"没绑定"，而是绑定链路有三处漏洞，导致（a）页面显示/Inspector 显示的是 Unity 内存值，而磁盘上的 `.asset` 文件是另一套值；（b）页面一旦没读到 Unity 配置，"保存设置"会把页面的表单默认值直接覆盖到 asset 上。** 两条路都会让你看到"两边值不一致"。

---

## 一、先说数据现状（这是"两边不一致"的直接现场）

`Assets/UnityPSDLayoutTool2/Assets/PSD2UIForm/Psd2UIFormConfig.asset`，磁盘上的内容（文件时间 19:34:48，之后没再被写过）：

| 字段 | 磁盘 `.asset` | Unity 内存里（19:52 起导出日志） |
|---|---|---|
| defaultTextType | 12 (TMPText) | 12 |
| defaultImageType | 1 (Image) | 1 |
| forceUseTMP | 1 | true |
| sharedAssetsOutput | **（空）** | `Assets\Examples\Common` |
| sharedPrefabOutput | **（空）** | `Assets\Examples\CommonPrefab` |
| convertZh2En | **0** | true |
| readmeDoc | **（空）** | 完整的长篇「快速上手…」 |

也就是说：**内存 = 你期望的值，磁盘文件 = 被洗过的默认值**。内存里那组值与 2026-07-08 的旧 `Psd2UIFormConfig.json` 完全一致。

证据：
- `Editor.log:18996 / 19016` —— 整个会话里 `Psd2UIFormConfig.asset` 只被写入过这两次（对应 `配置已保存` ×2）。
- `Editor.log:23526 / 25204 / 25339` —— 三次 `ExportConfig` 导出的 JSON 都是内存里那组"期望值"，且 stack 显示走的是 `HandleRequest → EditorApplication.Internal_CallDelayFunctions`，说明页面确实拿到了内存值。
- 磁盘文件 mtime 停在 `19:34:48`，而日志最后一笔是 `19:58`，Unity 一直在跑 → 后续的内存修改**从未落盘**。

> ⚠️ **域重载或重启 Unity 时，内存里未落盘的值会被磁盘内容覆盖。** 现场那组长篇说明/复用路径会丢。

---

## 二、根因（4 个，都已在代码里定位）

### 1. 页面没读到配置时，仍可"保存"，把表单默认值写回 asset —— 这是 asset 被洗成默认值的元凶
`Resources/Psd2UIFormSettings.html` 里 `loadSettings()` 只在 `fetch('/config')` 成功后执行；失败时表单停留在 HTML 硬编码值：导出路径空、使用说明空、中文转英文不勾、文本类型 TMP、强制 TMP 勾选。而 `saveSettings()` **完全不检查是否已加载**，直接 POST 覆盖。磁盘上那组值（空路径 + 空说明 + convertZh2En=0 + TMP + 强制TMP）与这个"未加载表单状态"逐字吻合。

更糟的是页面是用 `file://` 打开的，却去 `fetch('http://localhost:9527')` —— 跨源 + 浏览器对 `file://` → 回环地址的私网访问限制，一旦被拦，页面只会弹一个 alert，之后照常可点保存。日志里也有对应痕迹：`[Psd2UIForm] HTTP 错误: InternalCreate can only be called from the main thread.` 共 8 次（`Editor.log:21937 / 21955 / 21973 / 21991 / 22009 / 22027 / 23647 / 23665`）—— 那几次 `GET /config` 直接在监听线程抛异常、响应为空。

### 2. 两类"只改内存不落盘"的路径
- `UGUIParserEditor.ImportConfigFromJson()`（Inspector 的「从json导入配置」）只调用了 `serializedObject.ApplyModifiedProperties()`，**没有 `SetDirty` / `SaveAssets`**。改完内存是新的，磁盘 `.asset` 还是旧的。
- `Psd2UIFormSettingsServer.ApplyConfig()` 里改 `ScriptableSingleton<Psd2UIFormSettings>.Instance.AutoCropMinimalNineSlice`（自动裁剪九宫格）后**从不调用 `SaveInstance()`**。注意这个 singleton 不是 AssetDatabase 资源（它由 `InternalEditorUtility.LoadSerializedFileAndForget` 读写 `ProjectSettings/Psd2UIFormSettings.asset`），`AssetDatabase.SaveAssets()` 对它无效 —— 页面里勾/取消"自动裁剪"，重启 Unity 就丢。全项目只有 `Psd2UIFormConverterInspector` 调过 `SaveInstance()`。

### 3. 页面里的枚举值和 Unity 的 `GUIType` 对不上 —— 改了也对不上
`Src/Runtime/Psd2UGUI/Runtime/GUIType.cs`：`Text = 3`、`RawImage = 2`（`Null = 0`、`Image = 1`）。
但 HTML 里写的是 `<option value="2">Unity UI Text</option>`、`<option value="0">RawImage</option>`：
- 选「Unity UI Text」→ 写入 2（其实是 RawImage/原始贴图）；
- 选「RawImage」→ 写入 0（其实是 Null）；
- 反过来，asset 里是 3 或 2 时，下拉框里**没有对应 option**，浏览器静默回落到第一个选项（TextMeshPro / Image），看起来就是"页面显示的和 asset 里的不是一回事"。

### 4. 一个页面绑了两个文件
8 个字段里 7 个在 `Assets/…/Psd2UIFormConfig.asset`（UGUIParser），1 个（自动裁剪九宫格）在 `ProjectSettings/Psd2UIFormSettings.asset`（Psd2UIFormSettings）。

### 附带隐患（本次一并修掉）
- `Psd2UIFormConfigRepair.ImportConfigJson()` 给枚举用了 `enumValueIndex = (int)UIType`。`GUIType` 的值不连续（`Background = 101` … `ScrollView_VerticalBar = 119`），`enumValueIndex` 是"枚举名数组下标"，一旦走 JSON 恢复分支，子类型规则会整体错位。
- 同一方法还在写已被删除的字段 `nineSliceBorderTolerance`（`UGUIParser` 里已无此字段）→ `FindProperty` 返回 null → NullReferenceException。
- `UGUIParserEditor.OnDisable()` 里停服务器：Inspector 一失焦/切换选中对象服务器就停，浏览器里已打开的页面立刻保存失败（`Failed to fetch`），这也是"像是没绑定上"的一种表现。

---

## 三、本次改动

备份：`.workbuddy/backup/2026-09-11-psd2uiform-settings-binding/`（4 个源文件 + `Psd2UIFormConfig.asset` 原件）

| 文件 | 改动 |
|---|---|
| `Src/Editor/Psd2UGUI/EditorTools/Psd2UIFormSettingsServer.cs` | 重写。① 页面改为从 **`http://localhost:9527/` 同源打开**（不再用 `file://`），并新增 `/` 路由直接返回页面，彻底避开跨源/私网限制；② 所有 Unity API 只在主线程执行（`EditorApplication.delayCall` 派发 + 10s 超时，超时不再把监听线程卡死）；③ `ApplyConfig` 保存时 `SetDirty + SaveAssetIfDirty + SaveAssets` 立刻落盘，并额外调 `Psd2UIFormSettings.SaveInstance()` 让"自动裁剪"也落盘；④ `FindProperty` 全部 null 安全，缺字段只警告不炸；⑤ `/save` 失败会返回 `{"success":false,"error":…}`，页面不再假报成功；⑥ 注入一段脚本：把保存地址写进页面，并在**成功读到配置前禁止保存**，读不到时在页面顶部显示红色告警条；⑦ `Stop()` 去掉 `Thread.Abort()`，改为优雅退出，并在 `beforeAssemblyReload` / `quitting` 时自动关闭监听（避免端口泄漏刷 `Listener closed`）。 |
| `Src/Editor/Resources/Psd2UIFormSettings.html` | ① 「Unity UI Text」的值 2 → **3**，「RawImage」的值 0 → **2**；② `loadSettings` 不再用 `\|\| '12'` 把 0/空值悄悄变成 TMP，并新增 `applySelectValue()`：option 缺失时在控制台明确警告；③ `saveSettings()` 加守卫：未成功加载配置时弹窗并**拒绝保存**；④ 成功加载后置 `window.__psd2uiformLoaded = true`。 |
| `Src/Editor/Psd2UGUI/EditorTools/UGUIParserEditor.cs` | ① `ImportConfigFromJson()` 补 `SetDirty + AssetDatabase.SaveAssets()`，日志里带上目标 asset 路径；② `OnDisable()` 不再停服务器（改由服务器自己在域重载/退出时关闭）。 |
| `Src/Editor/Psd2UGUI/EditorTools/Psd2UIFormConfigRepair.cs` | ① `enumValueIndex` → `intValue`（`defaultTextType`/`defaultImageType`/规则 `UIType`）；② `nineSliceBorderTolerance` 改为存在性判断，不再 NRE。 |

---

## 四、怎么验证

1. **先保数据**：在 Unity 里按一次 `Ctrl+S`（把内存里那组期望值落盘）。然后再让 Unity 编译新代码。
   —— 如果这组值已经丢了，用 Inspector 的「从json导入配置」选 `Assets/…/PSD2UIForm/Psd2UIFormConfig.json` 恢复（该方法本次已修好会落盘），再把「默认文本类型」改成 TextMeshPro、「强制优先使用TMP」勾上。
2. 选中 `Psd2UIFormConfig`，点「⚙️ 打开设置」。浏览器地址应变成 `http://localhost:9527/`（不再是 `file:///…/Temp/…`）。
3. 页面顶部不应出现红色告警条；F12 Console 应有 `[PSD2UIForm] 已从 Unity 读取配置: {...}`。
4. 改几个值 → 点「保存设置」→ 浏览器弹「✅ 配置已保存到 Unity」，Unity Console 输出 `[Psd2UIForm] ✅ 配置已保存到 …`。
5. **核对两边**（关键）：把 `Psd2UIFormConfig.asset` 用文本编辑器打开，确认字段和页面一致；再确认 `ProjectSettings/Psd2UIFormSettings.asset` 的 `AutoCropMinimalNineSlice` 也跟着变。
6. 故意断链测试：先关掉 Unity（或让页面加载失败），点「保存设置」，应被守卫拦下，不会再覆盖 asset。

---

## 五、遗留建议（未改，等你决定）

- 「恢复默认」按钮的默认值仍是"空导出路径 / 空使用说明 / 中文转英文关"，和插件 C# 侧的真实默认值（`readmeDoc = "使用说明"`、`sharedAssetsOutput = "Assets/SharedUIAssets"`、`sharedPrefabOutput = "Assets/SharedPrefab"`、`convertZh2En = true`）不一致。建议要么改成真实默认值，要么给这个按钮加二次确认（现在是 `confirm` 后直接改表单，紧接着点保存就覆盖 asset）。
- 自动裁剪九宫格目前寄在 `ProjectSettings` 的 singleton 上，和页面其余 7 个字段不是同一个文件。若想彻底"一个页面一个数据源"，建议把该字段挪进 `UGUIParser`。
- `Psd2UIFormConfig.json`（2026-07-08）是过期快照，`Psd2UIFormConfigRepair.NeedsJsonRestore` 一旦判定 asset 需要修复，会用这份旧 JSON 覆盖 asset。建议同步更新或干脆删掉它，避免成为"自动回滚到旧值"的暗坑。

---

## 六、复查结果（2026-09-11 20:12）

### ✅ 已确认：编译通过

`Editor.log` 20:11 的刷新 `Asset Pipeline Refresh (id=6c8a67c1376ec3442b085aaefe296a43)`：

- `Scripting: domain reloads=1, compile time=3038 ms` —— 编译完成且成功
- 全日志无 `error CS`（`tail -3000 | grep "error CS"` 为空）
- 4 个改动文件均已被重新导入（`Start importing … Psd2UIFormSettingsServer.cs / UGUIParserEditor.cs / Psd2UIFormSettings.html`）

### ✅ 已确认：四处改动在源码中生效

| 位置 | 复查到的内容 |
|---|---|
| `Psd2UIFormSettings.html:510-523` | `<option value="3">Unity UI Text</option>`、`<option value="2">RawImage</option>` |
| `Psd2UIFormSettings.html:635 / 660` | `window.__psd2uiformLoaded = true` / `if (!window.__psd2uiformLoaded)` 守卫 |
| `UGUIParserEditor.cs:588-594` | `OnDisable()` 只剩注释，不再停服务器 |
| `Psd2UIFormSettingsServer.cs:235` | `if (path == "/" \|\| path == "/index.html")` 同源路由 |

### ⏳ 未确认：端到端点击链路

HTTP 服务器只在 Unity 编辑器进程内监听，无法从外部触发，需要你点一次页面（上面"四、怎么验证"第 2~5 步）。

### ⚠️ 关键现状：磁盘 asset 还没被修

- `Psd2UIFormConfig.asset` 的 mtime 仍是 **19:34:48**（早于本次改动），内容仍是旧值：`sharedAssetsOutput` 空、`sharedPrefabOutput` 空、`convertZh2En: 0`、`readmeDoc` 空。
- 20:11 那次刷新**没有**重新导入这个 asset（`Imports: total=2`，实际是 HTML + 本报告），所以内存里那组期望值大概率还在，打开页面即可看到。
- 一旦看到页面字段是空的，说明内存值已被清掉，用下面这份备份恢复。

### 保命文件

19:52:49 由页面导出、内容完整的配置（9 个字段，含那篇长篇使用说明）已备份到：

`Assets/UnityPSDLayoutTool2/.workbuddy/backup/2026-09-11-psd2uiform-settings-binding/current-config-good-1952.json`

同目录还留着改动前的 4 个源文件与 `.asset` 原始副本，随时可回退。

---

## 七、第二轮修复（2026-09-11 20:18）：读取不再依赖 Unity 主线程

### 现象（用户反馈）

「刷新页面时不一致，点一下 Unity 之后就一致了。」——**这个直觉是对的，确实是个 bug**，而且正是第一轮修复引入的设计缺陷。

### 日志证据

`tail -400 Editor.log` 里新代码留下的记录：

```
[Psd2UIForm] 等待 Unity 主线程超时（编辑器可能处于后台或正在编译）
  at Psd2UIFormSettingsServer.TryRunOnMainThread<string> (...)  :341
  at Psd2UIFormSettingsServer.HandleRequest (...)              :245
  at Psd2UIFormSettingsServer.ListenLoop ()                     :204
```

前后又夹着多次成功的 `导出当前配置`，所以是**间歇性**失败，不是必现。

### 根因

第一轮的 `GET /config` 是「把读取派发到 Unity 主线程（`EditorApplication.delayCall`）再等结果，超时 10s」。问题在于主线程在**脚本编译 / 域重载**期间不消费 `delayCall`——而 `delayCall` 是托管静态事件，域重载会连同队列一起重置。

于是：

1. 请求等不到主线程 → 超时 → 返回 500；
2. 页面 `loadSettings()` 根本没被调用，表单停在 HTML 里写死的默认值（空导出路径 / 空使用说明 / 中文转英文关）；
3. 拿去和 Inspector、`.asset` 一比 → 「不一致」；
4. 切回 Unity，主线程恢复，再刷新就正常 → 「点一下 Unity 之后就一致了」。

### 本轮改动

| 文件 | 改动 |
|---|---|
| `Psd2UIFormSettingsServer.cs` | ① 新增 `cachedConfigJson` 快照 + `EditorApplication.update` 心跳（0.5s 节流，内容变化才重写文件）：`GET /config` **只读快照、完全不碰主线程**，主线程再忙也能应答；快照为空时退回上一次导出的 `current-config.json`。② `Start()` 时先建快照（页面第一个请求就有数据），并顺手 `AssetDatabase.SaveAssetIfDirty(config)`，把「改了但没落盘」的值写下去。③ 保存成功后立刻重建快照。④ 主线程派发只留给 `POST /save`，超时 10s → **30s**，错误信息直指「Unity 正在编译/重载」。⑤ 心跳在 `Start`/`Stop` 正确挂载与卸载。 |
| `Psd2UIFormSettings.html` | ① 保存自动重试 4 次（800/1600/3200ms 退避），保存期间禁止重复点击；② 新增就地状态提示（正在保存… / Unity 忙，第 N 次尝试… / ✅ 已保存 / ❌ 失败），失败时明确说明「本次没有写进 asset，表单值未被改动」。 |
| 注入脚本（在服务器内） | 加载失败自动重试 5 次（300/600/1200/2400/4800ms），全部失败才挂红色告警条；加载成功自动清除告警条；告警条按 id 去重，不会叠加多条。 |

### 校验（静态）

- C# 括号配平：`braces=0 parens=0 brackets=0`
- 注入 JS：抽出 2523 字符 → `node --check` 通过；4 项行为自检（同源 URL / 读取重试 / 告警条去重 / 成功后清除）全 ✓
- 页面内联 JS：0 个语法错误

### 还需要你做

1. 切回 Unity 让它编译（会域重载一次），然后**关掉旧标签页**，重新点「⚙️ 打开设置」。
2. 之后任何时候刷新页面，都不应再出现「表单是空的/默认值」或「两边不一致」。
3. 在页面里点一次「保存设置」，把内存那组值写进 `.asset`——在此之前 `.asset` **文件本体**仍停在 19:34 的旧值（页面显示的是 Unity 内存值，两者在保存前仍会不同，但保存后会立刻一致，且现在保存失败会明确报错、不再静默假成功）。

---

## 八、第三轮修复（2026-09-11 20:26）：保存不再等 Unity 获得焦点

### 现象（用户反馈）

> 点击保存设置，一直等到必须点击 Unity 才保存成功。这也是个错误。

### 根因：第二轮的读路径修好了，**写路径还在死等主线程**

第二轮把 `GET /config` 改成"只读主线程维护的快照"，读取摆脱了主线程。但 `POST /save` 仍然是老写法：

```
HTTP 监听线程 ──> EditorApplication.delayCall += 落盘回调 ──> done.Wait(30s)
```

问题在于 **Unity 编辑器失去焦点（用户正开在浏览器里的设置页）时会节流主循环**，
而 `EditorApplication.delayCall` 正是靠主循环消费的。于是：

1. 用户点「保存设置」→ 请求进了队列，没人执行；
2. HTTP 线程在 `done.Wait(30s)` 上干等，浏览器也一直收不到响应；
3. 用户点一下 Unity → 主循环恢复 → 回调被执行 → 落盘 → 响应终于发出。

日志里能直接看到这条链路（`Editor.log`）：

```
27454:[Psd2UIForm] 等待 Unity 主线程超时（编辑器可能处于后台或正在编译）
27461:  at Psd2UIFormSettingsServer.TryRunOnMainThread<string>   :341
27462:  at Psd2UIFormSettingsServer.HandleRequest                :245
27463:  at Psd2UIFormSettingsServer.ListenLoop                   :204
```

（这是第二轮的读路径残留；写路径同理，只是你点得比 30s 超时早，看起来像"点一下就好了"。）

### 本轮改动

核心思路：**HTTP 响应绝不依赖主线程可用性；主线程只负责落盘，且要想办法把它叫醒。**

| 位置 | 改动 |
|---|---|
| `Psd2UIFormSettingsServer.SubmitSave`（新） | 保存拆成两段：HTTP 线程先做能独立完成的事（校验 → 更新内存快照让页面立刻能读回新值 → 入队 → 唤醒编辑器），主线程只负责写 asset。响应最多等 `SaveAckWaitSeconds = 2s` 确认；超时就回 `applied:false / pending:true`（**不是失败**，值已在队列里），彻底告别 30s 卡死。 |
| `ApplyPendingSave`（新） | 主线程真正落盘的地方。由 `EditorApplication.update` 驱动；`Stop()` / 域重载 / 退出 Unity 时也兜底调一次 → **只要提交过就一定落盘，不会因为"编辑器一直没 tick"丢数据。** 日志带时间戳（入队时刻 vs 落盘时刻），便于核对延迟。 |
| `WakeEditor`（新） | 主动调 `EditorApplication.QueuePlayerLoopUpdate()` 把编辑器叫醒（后台线程调用失败只记一次警告，不影响正确性）。 |
| `OnEditorUpdate` | ① 有排队保存立即落盘，不等 0.5s 心跳；② **保活**：收到过 HTTP 请求后 15s 内持续请求 player loop 更新，把 tick 链续上（社区验证过的 `EditorApplication.update += QueuePlayerLoopUpdate` 做法），自限时不会让编辑器永久满速跑。 |
| `/save-status`（新接口） | 页面拿不到 `applied:true` 时轮询它，确认最终有没有写进 asset：`{"pending":bool,"error":"…"}`。 |
| `RefreshCache` | 新增 `ignorePendingSave` 参数：**有排队保存时不刷缓存**（那一刻 asset 还是旧值，刷下去会把刚提交的新值冲掉，页面一刷新又"不一致"）；`ApplyConfig` 落盘后用 `true` 强制重建。 |
| `ApplyConfig` 失败分支 | 快照回滚成 `lastGoodConfigJson`（asset 里的真实值），避免页面长期显示一组其实没写进去的值。 |
| `Psd2UIFormSettings.html` | `postSave` 不再"重试 4 次 + 长退避"（现在请求本身就会立刻回应），只在网络层失败时退避重试 3 次；新增 `confirmSaveApplied` 轮询落盘结果（500ms × 12 次），状态依次为「正在保存…」→「已提交，正在写入 asset…」→「✅ 已保存到 Unity」。 |

`TryRunOnMainThread`（含 30s 死等）已整体删除。

### 校验（静态）

- C# 括号配平：`braces=0 parens=0 min=0`
- 注入 JS：抽出 2591 字符 → `node --check` 通过；5 项行为自检（同源 URL / 落盘状态 URL / 读取重试 / 告警条去重 / 成功后清除）全 ✓
- 页面内联 JS：1 个 script 块，0 个语法错误
- 残留引用检查：`TryRunOnMainThread` / `MainThreadTimeoutSeconds` 已无引用

### 怎么验证这轮真的修好了

1. 切回 Unity 让它编译（会域重载一次），重新点「⚙️ 打开设置」，**别切回 Unity**，就停在浏览器里。
2. 改几个值 → 点「保存设置」。
3. 预期：**2 秒内**出结果，不需要碰 Unity。
   - 正常情况：直接弹「✅ 配置已保存到 Unity！」，状态栏闪一下「正在保存…」。
   - 编辑器确实被节流时：状态栏先「已提交，正在写入 asset…」，随后自行变成 ✅。
4. 去 `Editor.log` 搜 `保存已写入 asset`，看两个时间戳（入队于 xx:xx:xx / 落盘 xx:xx:xx.xxx）差多少 —— 这就是本轮修复效果的量化证据。
5. 若日志出现 `请求编辑器更新失败`，把这行发我，说明该 Unity 版本不允许后台线程调 `QueuePlayerLoopUpdate`，我再换成"窗口消息唤醒"方案。

---

## 九、数据现状核对（2026-09-11 20:28）

你 20:21 那次点击最终**是成功落盘的**，`.asset` 已经和页面显示一致了。实测：

| 文件 | 字段 | 现值 |
|---|---|---|
| `Assets/PSD2UIForm/Psd2UIFormConfig.asset`（mtime **20:21:47**，21049 字节） | `defaultTextType` | 12（TMPText） |
| | `defaultImageType` | 1（Image） |
| | `forceUseTMP` | 1 |
| | `sharedAssetsOutput` | `Assets\Examples\Common` |
| | `sharedPrefabOutput` | `Assets\Examples\CommonPrefab` |
| | `convertZh2En` | 1 |
| | `readmeDoc` | 完整长篇说明（YAML 折行，374 行起） |
| `ProjectSettings/Psd2UIFormSettings.asset` | `AutoCropMinimalNineSlice` | 1 |

也就是说：**"两边不一致"这件事本身已经结束了**，第三轮修的是"每次保存都要点一下 Unity"这个体验缺陷。`Editor.log` 末尾那几行正好留下了旧机制的物证 ——

```
Psd2UIFormSettingsServer:HandleRequest
Psd2UIFormSettingsServer/<>c__DisplayClass24_0`1<string>:<TryRunOnMainThread>b__0
UnityEditor.EditorApplication:Internal_CallDelayFunctions ()
```

`Internal_CallDelayFunctions` 就是 `delayCall` 被主循环消费的那一刻——也就是你点回 Unity 的那一刻。

复盘结论（写给以后）：**编辑器里的 HTTP 服务，读和写都不能让响应依赖主线程可用性。** 读走快照，写走队列 + 主动唤醒 + 兜底落盘。


