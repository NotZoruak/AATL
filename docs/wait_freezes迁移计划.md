# 硬编码等待升级为 wait_freezes 计划

## 背景与动机

MaaKEDR 项目 AGENTS.md 明确禁止引入硬编码延迟（hard delays），推荐使用 `pre_wait_freezes` / `post_wait_freezes`（画面冻结等待）或中间识别 node 替代固定毫秒等待。该建议同样适用于 MATR：

- **固定时长基于不可靠假设**：`post_delay: 2000` 隐含"切换动画恰好 2 秒"的假设。慢设备上不够（点击过早失效 → 识别失败连锁），快设备上浪费（每次循环白等）
- **等待的目标是画面状态而非时间**：`wait_freezes` 等待"画面连续 N 毫秒无显著变化"这一状态出现，动画 3 秒就等 3 秒、0.5 秒就等 0.5 秒，完全自适应
- **维护成本**：游戏更新动画时长变化后，固定延迟需逐个重新实测，`wait_freezes` 无需调整

MaaFW v5.12.2 支持该字段，执行顺序为 `pre_wait_freezes` → `pre_delay` → action → `post_wait_freezes` → `post_delay`。

## wait_freezes 格式说明

### 基本格式（uint 毫秒）

```json
"S_IsGameIcon": {
    "recognition": { ... },
    "action": { ... },
    "post_wait_freezes": 800,
    "next": [ ... ],
    "on_error": [ ... ]
}
```

与 `post_delay` 同级，语义为：画面连续 800ms 无显著变化后才进入下一步。

### 对象格式（完整参数）

```json
"post_wait_freezes": {
    "time": 800,                   // 连续无显著变化的毫秒数，默认 1
    "target": true,                // 等待监控区域：true=全屏；[x, y] 点；[x, y, w, h] ROI
    "target_offset": [0, 0, 0, 0], // target 的偏移
    "threshold": 0.95,             // "无显著变化"判定阈值（相邻帧模板匹配相似度），默认 0.95
    "method": 5,                   // 帧对比算法（cv::TemplateMatchModes），默认 5
    "rate_limit": 1000,            // 冻结检测的识别间隔 ms，默认 1000
    "timeout": 20000               // 冻结检测超时 ms，默认 20s，-1 为无限等待
}
```

### 注意事项

- **检测机制**：引擎连续截屏，用模板匹配对比相邻帧，相似度 ≥ threshold 持续 time 毫秒判定为静止。time 为 800 实际是"相邻帧相似度 ≥0.95 且持续 0.8 秒"
- **`target` 是解决"永不冻结"的关键**：画面背景有持续动画（飘花、角色呼吸、微光）时全屏模式永远达不到静止 → 死等到 timeout（默认 20s）。此时用对象形式缩小监控区域，例如等待弹窗出现只监控弹窗区域：

```json
"post_wait_freezes": {
    "time": 800,
    "target": [947, 533, 67, 75]
}
```

- **`rate_limit` 默认 1000ms**：冻结检测每轮至少 1 秒，time 设 800 与 1000 实际差别不大
- **`timeout` 默认 20 秒**：超时后行为与识别超时一致（走错误处理路径），是防止死等的保护伞
- **位置语义**：`post_wait_freezes` 夹在 action 与 `post_delay` 之间（action 之后、post_delay 之前）

## 现状盘点

全部 9 个 pipeline 文件的 pre/post_delay 使用分布（2026-08-08 统计）：

| 文件 | pre_delay 主要值 | post_delay 主要值 | 高频值合计 |
|---|---|---|---|
| Sortie.json | 100 ×100 | 300 ×16、500/800 ×6、1000/2000/3000 ×4 | 约 140 处 |
| Underground.json | 100 ×62 | 800 ×5、300 ×4、500 ×4、2000/3000 ×3 | 约 85 处 |
| Expedition.json | 100 ×71 | 300 ×15、1000 ×9、2000/3000 ×4、4000 ×1、60000 ×1 | 约 105 处 |
| LRentaisen.json | 100 ×42 | 800 ×5、500 ×4、3000 ×2、2000 ×2 | 约 60 处 |
| FlowerBrush.json | 100 ×40 | 800 ×5、500 ×3、2000/3000 ×2 | 约 55 处 |
| TacticalTraining.json | 100 ×24 | 300 ×5、500 ×4、2000/3000 ×2 | 约 40 处 |
| Mix.json | 500 ×10、300 ×6 | 500 ×12、2000/3000 ×2 | 约 35 处 |
| Disassemble.json | 300 ×23、1000 ×5 | 500 ×26、2000 ×4、3000 ×2 | 约 70 处 |
| GoHome.json | 1000 ×2、3000 ×1 | 3000 ×2、2000 ×2 | 约 10 处 |

**关键观察**：

- 闪退恢复类（`*_IsGameIcon` / `*_IsLoginButton` / `*_IsGameUpdatePopup` / `*_IsInGameUpdatePopup`）全部带 `post_delay: 2000-3000`，用于等待游戏启动/页面加载动画结束——这是最典型的硬编码等待，且 9 个任务完全一致
- 点击后过渡等待（`post_delay: 500/800/1000/2000/3000`）遍布各流程，等待目标都是"下一画面稳定出现"
- 特例：`E_WaitRefresh` 的 `post_delay: 60000`（60 秒）为刷新冷却的故意设置，不在迁移范围；各枢纽 `timeout: 120000` 同理
- `pre_delay: 100` 为识别与动作间的保险等待，量最大但开销极小，不在迁移范围

## 迁移目标分类

按 wait_freezes 适配性分为三类：

### A 类：动画/切换等待（迁移目标）

- 点击后等待画面切换完成的 `post_delay`（500ms 及以上）
- 闪退恢复类等待游戏启动的 `post_delay`（2000-3000ms）
- 迁移方式：`post_delay` → `post_wait_freezes`（示例初始值 800ms，需实机校准）

### B 类：小额保险等待（保留）

- `pre_delay: 100`（识别成功与动作执行间的保险，无明确动画可等，迁移无收益）
- `post_delay: 200-300`（与引擎轮询粒度相当的短等待，等效轮询周期内完成）

### C 类：有意保留（不迁移）

- `E_WaitRefresh`（60000ms）：等待刷新冷却结束的故意设置，行为依赖固定时长语义，不迁移
- 各枢纽 node 的 `timeout: 120000`（120s）：识别循环的总超时上限，属于有意设置，不迁移
- 其他流程中经人工确认的刻意等待同理保留

## 合战场闪退恢复 node 清单（实施前置分析）

按主枢纽/次枢纽的 next 排列顺序列出，包含 children node（next 与 on_error）与 delay 现状。target 方案待逐 node 判断，不批量执行。

### 主枢纽 S_DetectWhereAmI（重排后顺序）

闪退恢复 node 位于 next 列表第 23-27 位：

| 位序  | node                  | 识别                                                      | 动作                             | pre_delay | post_delay | children                                              |
| --- | --------------------- | ------------------------------------------------------- | ------------------------------ | --------- | ---------- | ----------------------------------------------------- |
| 23  | S_IsGameIcon          | TemplateMatch 游戏图标（ROI [0,0,1600,900]，threshold 0.85）   | Click                          | 100       | 3000       | next: [S_DetectWhereAmI]；on_error: [S_DetectWhereAmI] |
| 24  | S_IsLoginButton       | TemplateMatch 登录（ROI [665,529,124,78]，threshold 0.98）   | Click                          | 100       | 3000       | next: [S_DetectWhereAmI]；on_error: [S_DetectWhereAmI] |
| 25  | S_IsGameUpdatePopup   | TemplateMatch 游戏更新（ROI [443,431,120,46]，threshold 0.98） | Click                          | 100       | 2000       | next: [S_DetectWhereAmI]；on_error: [S_DetectWhereAmI] |
| 26  | S_IsInGameUpdatePopup | OCR 更新（ROI [498,291,77,50]）                             | Click（target [578,440,130,55]） | 100       | 2000       | next: [S_DetectWhereAmI]；on_error: [S_DetectWhereAmI] |
| 27  | S_IsInternalReport    | TemplateMatch 内部报告（ROI [569,322,138,31]，threshold 0.98） | Click                          | 100       | 0          | next: [S_DetectWhereAmI]；on_error: [S_DetectWhereAmI] |

主枢纽 on_error 分支（不在 next 列表内，命中后由引擎直接执行）：

| node | 识别 | 动作 | pre_delay | post_delay | children |
|---|---|---|---|---|---|
| S_RestartGame | 无 | Custom RestartGameAction（模拟器重启 + 游戏进程重启，内部已含 10-15 秒等待与 ADB 就绪轮询） | 0 | 0 | next: [S_DetectWhereAmI]；无 on_error |

### 次枢纽 SF_DetectWhereAmI（重排后顺序）

闪退恢复 node 位于 next 列表第 16-20 位，结构与主枢纽完全同构（children 均回 SF_DetectWhereAmI）：

| 位序 | node | 识别 | 动作 | pre_delay | post_delay |
|---|---|---|---|---|---|
| 16 | SF_IsGameIcon | TemplateMatch 游戏图标（ROI [0,0,1600,900]，threshold 0.85） | Click | 100 | 3000 |
| 17 | SF_IsLoginButton | TemplateMatch 登录（ROI [665,529,124,78]，threshold 0.98） | Click | 100 | 3000 |
| 18 | SF_IsGameUpdatePopup | TemplateMatch 游戏更新（ROI [443,431,120,46]，threshold 0.98） | Click | 100 | 2000 |
| 19 | SF_IsInGameUpdatePopup | OCR 更新（ROI [498,291,77,50]） | Click（target [578,440,130,55]） | 100 | 2000 |
| 20 | SF_IsInternalReport | TemplateMatch 内部报告（ROI [569,322,138,31]，threshold 0.98） | Click | 100 | 0 |

次枢纽 on_error 分支：SF_RestartGame（与 S_RestartGame 同构，children 回 SF_DetectWhereAmI）。

### 逐 node 判断要素（target 方案依据）

| node | 点击后画面变化 | target 初判 | 说明 |
|---|---|---|---|
| IsGameIcon / IsLoginButton | 游戏启动：多阶段动画（logo → 标题 → 主界面），最终主界面静止 | 全屏 | 等待目标是最终静止的主界面，中途动画期间 freeze 不满足属正常 |
| IsGameUpdatePopup | 弹窗关闭，画面恢复 | 全屏或弹窗 ROI | 需确认弹窗关闭后背景（登录/标题画面）是否静止，若背景有动态元素则用弹窗 ROI |
| IsInGameUpdatePopup | 弹窗关闭，画面恢复 | 全屏或弹窗 ROI | 同上；点击 target 为 [578,440,130,55]（弹窗确认按钮），监控区域可复用该区域 |
| IsInternalReport | 弹窗关闭，画面恢复 | 弹窗 ROI | 无 post_delay，点击后立即回调度；需新增等待 |
| RestartGame | 模拟器/游戏重启（内部已等待） | 不处理 | 动作内部已含完整等待，next 回调度轮询 |

### pre_delay: 100 处理说明

- 所有闪退恢复 node 均带 pre_delay: 100（动作前保险等待），语义与 post_wait_freezes 不同（动作前画面静止立即满足，无迁移收益）
- 处理方向待定：保留（推荐，开销极小）或统一移除，需用户拍板
- S_IsGameIcon 附带发现：识别 ROI [0,0,1600,900] 超出 1280×720 基准（日志中存在 roi out of range 警告），属全屏检测误写，顺带修正为 [0,0,1280,720] 或省略

## 分批实施（按流水线推进）

以 pipeline 文件为最小实施与验证单元：一个文件对应一个任务，改完即可实机跑完整任务验证，回归隔离清晰，不采用横切分类（避免改一类要跑 9 个任务才能验证）。

### 实施顺序建议

| 批次 | 流水线 | 理由 |
|---|---|---|
| 1 | GoHome.json | 体量最小（约 10 处 delay），用于验证迁移手法与验证流程 |
| 2 | FlowerBrush.json / TacticalTraining.json | 小任务，模式简单 |
| 3 | Mix.json / Disassemble.json | 中型任务 |
| 4 | LRentaisen.json / Underground.json | 流程较长 |
| 5 | Expedition.json | 含 E_WaitRefresh 特例，验证时注意其 60s 等待不被误改 |
| 6 | Sortie.json | 体量最大（约 140 处 delay，含主/次两套流程），最后攻坚 |

### 每批内步骤

1. 盘点该文件 A 类等待（动画/切换类 post_delay 500ms 及以上，闪退恢复类 2000-3000ms）
2. 逐个对照任务设计文档确认等待目标为「下一画面稳定出现」
3. `post_delay` → `post_wait_freezes`（初始值 800ms，实机校准）
4. 实机完整跑一遍该任务，对照日志时间线验证
5. 通过后进入下一流水线

## 风险与规避

| 风险 | 规避 |
|---|---|
| 目标区域持续动画（旋转 loading、闪烁）导致 freeze 永不满足 | 使用对象形式指定 ROI（只等待特定区域静止），或保留少量固定等待 |
| 冻结检测需连续截屏比较，增加截图开销 | 仅迁移高频过渡场景，且 wait_freezes 在静止后立即退出，总体开销小于固定等待的浪费 |
| 迁移后行为变化导致点击时机偏移 | 每批独立实机验证，对比迁移前后「点击 → 下一识别命中」时间线与失败率 |

## 验证方式

- 每个流水线完成迁移后，实机完整运行该任务，检查日志：识别命中 → 点击 → 画面切换 → 下一识别命中的时间戳
- 确认无"点击过早失效"导致的识别失败（对照迁移前 `debug/maafw.log` 中的失败模式）
- 模拟器与真机（或快慢两档设备）各跑一轮，验证自适应效果
- 全部流水线通过后递增资源版本（行为结构调整，按 CLAUDE.md 版本规则为次版本号级别）

## 参考

- MaaKEDR AGENTS.md：「Fix an unstable node → Add intermediate recognition nodes or pre_wait_freezes / post_wait_freezes — never introduce hard delays」
- MaaFramework 官方 Pipeline 协议 3.1：`pre_wait_freezes` / `post_wait_freezes` 字段定义（*uint* | *object*，连续 N 毫秒无显著变化后继续；object 形式可指定监控 ROI）

## 已实施修改 node

### 2026-08-08：合战场低频 node 类

> 涵盖所有低频弹窗/通知类 node 的 delay 优化（移除无收益等待、迁移 wait_freezes）。**此类后续继续增加 node**，新增的低频 node 处理归入本节。

**决策通则**：

- 点击后 next 为各自枢纽（无条件回环结构）的 node：画面推进由循环轮询兜底，移除 delay/freeze 无收益
- 点击后画面必然变化（启动动画/弹窗关闭）的 node：正常路径不会重复命中，等待防不住"无效点击"（固定 N 秒后照样重复点）
- 点击后需等画面静止再继续的 node（弹窗关闭、连续弹窗判断）：迁移 `post_wait_freezes`，背景可能动态时必须指定 target ROI

**处理明细**：

1. **移除 delay（10 个）**——IsGameIcon / IsLoginButton / IsGameUpdatePopup / IsInGameUpdatePopup（闪退恢复类）、IsInternalReport（低频弹窗类，非闪退恢复），S 与 SF 系列各 5 个：

| node | 原 pre_delay | 原 post_delay |
|---|---|---|
| IsGameIcon | 100 | 3000 |
| IsLoginButton | 100 | 3000 |
| IsGameUpdatePopup | 100 | 2000 |
| IsInGameUpdatePopup | 100 | 2000 |
| IsInternalReport | 100 | 无 |

2. **公告弹窗（2 个，需 freeze）**——S_IsAnnouncementPopup / SF_IsAnnouncementPopup：移除 pre_delay 100，新增 `post_wait_freezes: {"time": 200, "target": [1056, 463, 63, 193]}`（target 为弹窗识别 ROI；timeout 3000 与自循环 next 保留）。点击关闭后等弹窗区域静止，再自循环判断弹窗是否仍在

3. **修行书信（2 个，移除 delay）**——S_IsTrainingLetter / SF_IsTrainingLetter：移除 pre_delay 100

4. **登录奖励链（6 个，需 freeze + target）**——S_IsLoginReward / SF_IsLoginReward 及确认链 Click2/Click3（S 与 SF 各 3 个）：移除 pre_delay，新增 `post_wait_freezes: {"time": 500, "target": [232, 99, 817, 568]}`（弹窗区域监控；Click3 的 timeout 3000 保留）

5. **检非与部队记录（2 个）**——S_IsKebiishi、S_LeaveTroopRecord：主枢纽 next 列表中移至 S_IsAnnouncementPopup 之前（第 18、19 位）；S_IsKebiishi 移除 pre_delay 100（无需等待）；S_LeaveTroopRecord 新增 `post_wait_freezes: {"time": 500, "target": [577, 8, 130, 35]}`（部队记录页面关闭后等页面区域静止）

**未处理**：S_RestartGame / SF_RestartGame（Custom 动作内部已有模拟器重启 + 游戏启动完整等待，next 回调度轮询）。

**说明**：`post_wait_freezes` 不设置 target 时默认监控全屏（true），背景有动态元素时必须用对象形式指定 ROI。

**验证**：Sortie.json 解析通过，累计 24 个 node（15 个移除 delay + 9 个 freeze 迁移），其余 node 未受影响。

### 2026-08-08：合战场主枢纽高频 node 类

**决策**：主枢纽高频 node（每次战斗/远征/掉落都触发）与低频 node 同构——点击后 next 为 [S_IsMarching, S_DetectWhereAmI]（无条件回环），画面推进由循环轮询兜底，动作前等待无收益；DoNothing 动作前的 pre_delay 更无作用（动作不需要准备时间）。

**修改**：移除以下 7 个 node 的 `pre_delay: 100`：

| node | 识别 | 动作 |
|---|---|---|
| S_HasExpeditionReturn_Exp | OCR 经验 | Click [947, 533, 67, 75] |
| S_HasExpeditionReturn_Title | OCR 远征结果 | Click [947, 533, 67, 75] |
| S_IsBattleResult_Exp | OCR 经验 | Click [947, 533, 67, 75] |
| S_IsBattleResult_Title | OCR 战斗结果 | Click [947, 533, 67, 75] |
| S_IsSwordDropColor | ColorMatch 掉落色条 | Click [1067, 558, 120, 79] |
| S_IsMenuDirectory | TemplateMatch 菜单目录 | DoNothing |
| S_IsTeamStatusPanel | TemplateMatch 本丸远征 | DoNothing |

**验证**：Sortie.json 解析通过，7 个 node 均无 delay 残留，其余 node 未受影响。

### 2026-08-08：合战场 children 点击链（逐链处理）

**决策通则**：链式点击 node 按"点击后画面是否变化"判断——画面不变的等待无对象（freeze 立即满足等于没有），画面变化后回调度的等待由轮询兜底。

**链 1：阵形选择链**（S_IsFormationSelect → S_ClickFormation1 → S_ClickFormation2）

- 实机确认：第一次点击（选阵形）后画面几乎不变，post 200 无等待对象；第二次点击后回调度，有无 delay 均无影响
- 修改：S_ClickFormation1 移除 pre_delay 100 + post_delay 200；S_ClickFormation2 移除 pre_delay 100

**链 2：装备弹窗链**（S_CheckEquipmentPopup → S_PreCheckTroopRecord → S_PreCheckUseRecord → S_PreCheckClickRecord1-5 → S_PreCheckUseRecordBtn → S_PreConfirmSupply → S_IsPreSortieConfirm）

- 整链禁用（enabled false），按与启用 node 相同规则处理（点击后画面变化处加 freeze）
- S_CheckEquipmentPopup：post_delay 500 → `post_wait_freezes: {"time": 500, "target": [423, 484, 145, 56]}`（弹窗识别 ROI）
- S_PreCheckTroopRecord：新增 `post_wait_freezes: {"time": 200}`（全屏）
- S_PreCheckUseRecord：无需 freeze
- S_PreCheckClickRecord1-5：新增 `post_wait_freezes: {"time": 200, "target": [655, 121, 540, 562]}`
- S_PreCheckUseRecordBtn：新增 `post_wait_freezes: {"time": 200, "target": [442, 253, 395, 125]}`
- S_PreConfirmSupply：新增 `post_wait_freezes: {"time": 1500}`（全屏）

**链 3：SF 队伍选择/装备补充链**（SF_IsTeamSelect → SF_CheckEquipmentPopup → SF_EqRefill_Step1-4 / SF_ClickTeamN）

- 与 S 版装备弹窗链同构，按同配置映射：
  - SF_ClickTeamN：同 S_ClickTeam——移除 pre 100，`post_wait_freezes: {"time": 200, "target": [38, 123, 1059, 561]}`
  - SF_EqRefill_Step1：同 S_PreCheckTroopRecord——post 300 → `post_wait_freezes: {"time": 200}`（全屏）
  - SF_EqRefill_Step2_Rec1-5：同 S_PreCheckClickRecordN——post 300 → `post_wait_freezes: {"time": 200, "target": [655, 121, 540, 562]}`
  - SF_EqRefill_Step3：同 S_PreCheckUseRecordBtn——新增 `post_wait_freezes: {"time": 200, "target": [442, 253, 395, 125]}`
  - SF_EqRefill_Step4：同 S_PreConfirmSupply——post 1000 → `post_wait_freezes: {"time": 1500}`（全屏）

**链 4：队伍选择链收尾**

- S_StopOnEquipmentPopup：移除 pre 100，新增 `post_wait_freezes: {"time": 200, "target": [427, 269, 426, 130]}`
- SF_CheckFatigue / SF_ClickSortieNow：移除 pre 100（无需 delay）
- S_FatigueCheck：移除 pre 100（Custom 动作，与 SF_CheckFatigue 一致）

**链 5：时代选择链**（S 与 SF 版，均启用）

- S_ClickEra / SF_ClickFirstEra：移除 pre 100（SF 另移除 post 500）——实机确认点击选时代后画面几乎无变化
- S_ConfirmEra / SF_ConfirmEra：移除 pre 100，新增 `post_wait_freezes: {"time": 500}`（全屏，确认后等画面稳定回调度）

**链 6：地域选择链**（S 与 SF 版，均启用）

- 结构调整：删除冗余验证 node `S_VerifyRegionSelect`（仅被 S_IsRegionSelect 引用，无独立识别价值），`S_IsRegionSelect` next 变为 `[S_ClickRegion, S_DetectWhereAmI]`
- S_ClickRegion：移除 pre 500 + post 800，新增 `post_wait_freezes: {"time": 1000}`（全屏）
- SF_ClickRegion1_1：移除 pre 100，新增 `post_wait_freezes: {"time": 1000}`（全屏，与 S 版一致）

**链 7：队长选择链**（S_IsSwordSelect → S_ClickCaptain1-5 → S_DetectSortOrder，含 S_ClickCaptainSlot，全部禁用）

| node | freeze 配置 |
|---|---|
| S_ClickCaptain1 | {time: 200} 全屏 |
| S_ClickCaptain2 | {time: 100, target: [752, 137, 97, 29]}（点击位置） |
| S_ClickCaptain3 | {time: 100, target: [407, 510, 90, 31]}（点击位置） |
| S_ClickCaptain4 | {time: 100, target: [965, 438, 91, 34]}（点击位置） |
| S_ClickCaptain5 | {time: 500} 全屏 |
| S_ClickCaptainSlot | {time: 200} 全屏 |

全部移除原 pre_delay 100 / post_delay 800-1000。

**链 7 延续：排序链**（S_ClickCaptain5 → S_DetectSortOrder → S_ClickDescending / S_ConfirmSortAsc）

- S_ClickDescending：移除 pre 100 + post 800，新增 `post_wait_freezes: {"time": 100}`（全屏，点击降序后列表重排）
- S_ConfirmSortAsc：移除 pre 100，新增 `post_wait_freezes: {"time": 100}`（全屏）
- S_ClickSortieNow（S 版）：移除 pre 100（与 SF 版一致）

**全局修正：全屏 freeze 统一 time 100ms**

- 实机发现：点击特效（涟漪、选中高亮等动态效果）会干扰全屏冻结判断，time 越大越容易在特效期间判定"画面未静止"而延迟
- 将所有无 target 的全屏 freeze time 统一改为 100ms（13 处：S_PreCheckTroopRecord、SF_EqRefill_Step1、S_ConfirmEra、SF_ConfirmEra、S_ClickRegion、SF_ClickRegion1_1、S_PreConfirmSupply、SF_EqRefill_Step4、S_ClickCaptain1/5/Slot、S_ClickDescending、S_ConfirmSortAsc）
- 点击区域与 freeze 区域一致（含隐式一致：action 无显式 target、freeze 监控识别 ROI）的 time 也统一为 100ms（3 处：S_CheckEquipmentPopup、S_IsAnnouncementPopup、SF_IsAnnouncementPopup；S_ClickCaptain2/3/4 原本已是 100）
- 其余带 target 的 freeze（25 处）监控区域与点击点分离，不受点击特效影响，保持原 time

**链 8：SF 使用记录链**（SF_UseRecord_Step1 → Step2_Rec1-5 → Step3 → Step4，与 S 补充刀装链点击坐标完全对应）

| SF node | 点击坐标 | 对应 S node | freeze 配置 |
|---|---|---|---|
| SF_UseRecord_Step1（✗，post 300） | 无显式 target | S_PreCheckTroopRecord | {time: 100} 全屏 |
| SF_UseRecord_Step2_Rec1-5（✗，post 300） | [1209, 140/250/360/465/575, 30, 71] | S_PreCheckClickRecord1-5 | {time: 200, target: [655, 121, 540, 562]} |
| SF_UseRecord_Step3（✓，post 300） | [707, 53, 72, 64] | S_PreCheckUseRecordBtn | {time: 200, target: [442, 253, 395, 125]} |
| SF_UseRecord_Step4（✓，post 1000） | [789, 512, 1, 4] | S_PreConfirmSupply | {time: 100} 全屏 |

**链 9：修刀链**（S_IsRepair → S_UseSpeedup → S_ConfirmRepair → S_ReturnHome → S_ClickMenuFromRepair → S_ClickHomeInMenu）

- 入口：S_IsRepair（主枢纽第 16 位，OCR 修复界面识别）
- S_UseSpeedup：移除 pre 100，新增 `post_wait_freezes: {"time": 100, "target": [1050, 580, 1, 1]}`（点击位置）
- S_ConfirmRepair：移除 pre 100 + post 200，新增 `post_wait_freezes: {"time": 100}`（全屏）
- S_ClickMenuFromRepair / S_ClickHomeInMenu：移除 pre 100，新增 `post_wait_freezes: {"time": 100}`（全屏）

**链 10：菜单入口修刀链**（S_NavigateToRepair → S_ClickMenuForRepair → S_ClickRepairInMenu → S_SelectDamagedSword → S_FindSlot1-3 → S_ConfirmSelectSlot → S_CheckAccel → S_ClickAccel → S_ClickRepairDone → S_LogRepairDone）

- S_ClickMenuForRepair：移除 pre 100 + post 300，新增 `post_wait_freezes: {"time": 100}`（全屏）
- S_ClickRepairInMenu：移除 pre 100，新增 `post_wait_freezes: {"time": 100}`（全屏）
- S_FindSlot1-3：移除 pre 100，新增 `post_wait_freezes: {"time": 100}`（全屏）
- S_ConfirmSelectSlot：移除 pre 100，新增 `post_wait_freezes: {"time": 200, "target": [262, 307, 147, 46]}`
- S_ClickAccel：移除 pre 100，新增 `post_wait_freezes: {"time": 100, "target": [1156, 241, 27, 25]}`（点击位置）
- S_ClickRepairDone：移除 pre 100，新增 `post_wait_freezes: {"time": 100, "target": [1159, 588, 83, 44]}`（点击位置）
- **结构调整**：S_LogRepairDone 的 next 由 [S_DirAfterRepair] 改为 [S_NavigateToSortie]——修复完成后直接导航出阵，替代修后菜单关闭链
- **孤儿链清理**：S_LogRepairDone 改道后，"修后导航"子链（S_DirAfterRepair → S_VerifyMenuAfterRepair → S_SortieAfterRepair ↔ S_VerifyAfterRepairMenuClosed ↔ S_TryClickAfterRepair，共 5 个 node）自成闭环且无外部入口，全部删除（Sortie.json 255 → 250 nodes）

**链 11：撤退/王点链**（S_CheckBoss → S_Boss_*/S_MidRetreat_* → S_ClickRetreat → S_ConfirmRetreat）

- S_ClickRetreat：移除 pre 100 + post 500，新增 `post_wait_freezes: {"time": 200, "target": [548, 295, 182, 56]}`（撤退确认弹窗 ROI）
- 14 个 Boss/MidRetreat 检测 node（E7 ×6、E8 ×4、MidRetreat_E8 ×4）移除 pre 100——纯识别跳转（无动作），pre 无意义；其余 29 个 Boss node（E2-E6）本无 delay 且无动作，无需处理（共 43 个）
- S_ConfirmRetreat：移除 pre 100，不加 freeze（点击确认后回调度轮询兜底）
- S_ReturnHomeFromMarching：移除 pre 100，新增 `post_wait_freezes: {"time": 200, "target": [548, 295, 182, 56]}`（与 S_ClickRetreat 一致，确认返回本丸弹窗）

**链 12：重伤停止链**（S_IsPreDamage1/2 → S_LogPreDamage*、S_StopOnDamagePopup/Text → S_LogStopOnDamage*、S_ConfirmCancelPreDamage2）

- S_IsPreDamage1：移除 pre 100，新增 `post_wait_freezes: {"time": 100, "target": [368, 464, 539, 90]}`
- S_IsPreDamage2：新增同配置 freeze（原无 delay 无动作）
- S_ConfirmCancelPreDamage2：移除 pre 100 + post 500，新增同配置 freeze
- S_StopOnDamagePopup：移除 pre 100，新增同配置 freeze
- S_StopOnDamageText：补上与 S_StopOnDamagePopup 相同的 Click 动作（target [788, 465, 1, 1]）与相同 freeze（原无动作）
- 注：S_ConfirmCancelPreDamage1 不存在（用户曾提及，实际仅 PreDamage2）

**链 13：导航/菜单点击收尾（S 与 SF 系列 21 个）**

- 全屏 freeze 100ms：S_ClickBattlefieldTab、S_ClickMenu、S_ClickSortieInMenu、S_CheckNavDirForRetry、S_ClickMenuStop、S_ClickHomeStop、SF_ClickMenu、SF_ClickSortieInMenu、SF_NavigateBack
- 移除 delay：S_PostClickMarching、S_ConfirmReturnHome、S_VerifyMenuOpened、S_ClickHomeFallback、S_NavigateToHomeFromStop（post 300）、SF_HasExpeditionReturn_Exp/Title、SF_IsMenuDirectory、SF_IsFormationSelect（post 500）、SF_IsBattleResult_Exp/Title、SF_IsSwordDrop
- S_WaitRefresh（post 1000）有意保留

**合战场完成状态**：全文件扫描确认仅剩 S_WaitRefresh 一处有意保留的 delay；全部 pre_delay 100 清除，过渡等待迁移为 post_wait_freezes（全屏统一 100ms、点击区域一致的 100ms、分离区域按场景 100-200ms）。

### 2026-08-08：重伤出阵循环修复（实机发现）

**现象**：重伤修刀实机测试中，出阵确认后约 3-4 秒一轮反复"命中部队选择 → 点部队 → 拖拽 → 出阵确认 → 点即刻 → 又回部队选择"，循环约 12 秒。

**根因链**：

1. 点击"即刻"出阵后，若队伍有重伤刀剑，游戏停留部队选择页面并显示重伤提示——`Common/重伤.png` 模板匹配的是**刀剑头像上的重伤标志**（静态，页面加载即有）
2. `S_ClickSortieNow` 点击后无任何等待 → 立即进 `S_PostSortieHub` 检查重伤
3. 出阵确认弹窗关闭动画期间，标志区域被遮罩/过渡影响，`S_IsPreDamage1` 匹配分数不足（0.63 → 0.857 → 0.978，随弹窗关闭进程上升）→ 漏检 → 通过 → 回主枢纽
4. 画面仍停留部队选择 → 再次命中部队 → 重复出阵 → 弹窗再次触发 → 循环，直到弹窗完全关闭（0.978）才命中

**修复**：`S_ClickSortieNow` 点击后新增 `post_wait_freezes: {"time": 100, "target": [1156, 584, 96, 47]}`（出阵确认弹窗区域）——等弹窗关闭动画完成、画面静止后再进 `S_PostSortieHub` 检查，重伤标志区域已干净（0.978 命中）；无重伤时弹窗关闭后该区域静止，不拖慢正常出阵。

**附带修复**：`S_ClickRepairInMenu` next 改为 `[S_VerifyRepairMenuClosed, S_SelectDamagedSword, S_DetectWhereAmI]`——修刀流程中 `S_VerifyRepairMenuClosed` 识别失败（菜单已关闭、进入修复界面）时直接进入选刀流程，不再被主枢纽兜底吞掉（原实现导致重伤修刀在菜单关闭后卡死）。interface.json"修刀并继续"case 移除已删除 node `S_ClickMenuFromRepair` 的引用。
