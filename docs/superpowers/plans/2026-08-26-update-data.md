# 更新数据任务 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增支持「仓库」「刀帐」「仓库+刀帐」和每次/每天/每周频率的更新数据任务。

**Architecture:** 资源层新增 `UpdateData.json`，包含 `UD_DetectWhereAmI` 主枢纽、三次本丸确认、仓库回本丸流程和独立的 `UD_SwordBookDetectWhereAmI` 刀帐枢纽。应用层负责频率跳过，专用持久化服务负责把成功草稿写入正式数据。

**Tech Stack:** C# 14、.NET 10、Avalonia、MaaFramework pipeline JSON、Newtonsoft.Json。

**Spec:** `docs/superpowers/specs/2026-08-26-update-data-design.md`

## Global Constraints

- 新 pipeline node 使用 `UD_` 或 `UD_SB_` 前缀，恢复路径不得跳转到其他任务枢纽。
- `UD_DetectWhereAmI` 使用 `timeout: 120000`，超时进入 `UD_RestartGame`。
- 本丸连续确认三次，每次成功确认后硬等待 1 秒；任意失败重新计数。
- 回本丸区域固定为 `[1234,9,37,38]`，点击后冻结 500ms，直到识别不到 `×`、`x` 或 `X`。
- 所有坐标以 1280×720 为基准；每个 pipeline node 都设置 `on_error`；不使用 `target_offset`。
- 识别失败、停止或中断时不得覆盖正式数据，也不得更新成功时间。
- 不修改 `D:\Apps\小只工具\MATR`。
- Git commit message 使用英文，不添加 Co-Authored-By。

## 文件职责

- `assets/interface.json`：任务、`识别内容` 和 `触发间隔`选项。
- `assets/resource/base/pipeline/UpdateData.json`：两个枢纽与完整业务路由。
- `assets/resource/base/custom/UpdateDataPrepareAction.cs`：清理阶段草稿。
- `assets/resource/base/custom/UpdateDataSaveAction.cs`：提交阶段正式数据。
- `assets/resource/base/custom/UpdateDataMarkSuccessAction.cs`：记录最终成功时间。
- `_src/MFAAvalonia/Services/UpdateDataScheduleService.cs`：频率判断和成功时间。
- `_src/MFAAvalonia/Services/UpdateDataPersistenceService.cs`：仓库/刀帐草稿提交。
- `_src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs`：构建队列时跳过未到期任务。
- `_src/MFAAvalonia/Configuration/ConfigurationKeys.cs`：新增实例配置键。
- `_src/MFAAvalonia.Tests/Program.cs`：回归测试。
- `docs/复用节点清单.md`、`docs/开发日志.md`、版本公告：文档和发布说明。

### Task 1: Add schedule and persistence services

**Files:**
- Create: `_src/MFAAvalonia/Services/UpdateDataScheduleService.cs`
- Create: `_src/MFAAvalonia/Services/UpdateDataPersistenceService.cs`
- Modify: `_src/MFAAvalonia/Configuration/ConfigurationKeys.cs`
- Test: `_src/MFAAvalonia.Tests/Program.cs`

**Interfaces:**
- `UpdateDataScheduleService.ShouldRun(InstanceConfiguration configuration, string interval, DateTime now) -> bool`
- `UpdateDataScheduleService.GetLastSucceeded(InstanceConfiguration configuration) -> DateTime?`
- `UpdateDataScheduleService.MarkSucceeded(InstanceConfiguration configuration, DateTime now) -> void`
- `UpdateDataPersistenceService.TrySaveWarehouseDraft(string draftPath) -> bool`
- `UpdateDataPersistenceService.TrySaveSwordBookDraft(string draftPath) -> bool`

- [ ] **Step 1: Write failing schedule tests.** Assert missing time is runnable, `每次` always runs, `每天` skips on the same local date and runs on the next date, and `每周` skips within the same ISO week and runs in the next ISO week.
- [ ] **Step 2: Write failing persistence tests.** Assert valid warehouse and swordbook drafts replace only their own formal data; invalid drafts return `false` and leave existing data unchanged.
- [ ] **Step 3: Implement schedule storage.** Store an invariant round-trip timestamp under `UpdateData.LastSucceededAt` in the instance configuration. Treat missing or invalid values as runnable.
- [ ] **Step 4: Implement atomic draft import.** Reuse warehouse normalization and swordbook draft parsing; build new values first, then write the corresponding configuration key. Never clear the old value before validation.
- [ ] **Step 5: Run `dotnet run --project _src/MFAAvalonia.Tests/MFAAvalonia.Tests.csproj`; expected result: PASS.**
- [ ] **Step 6: Commit with `git commit -m "feat: add update data scheduling and persistence"`.**

### Task 2: Add custom actions for preparation, saving, and success

**Files:**
- Create: `assets/resource/base/custom/UpdateDataPrepareAction.cs`
- Create: `assets/resource/base/custom/UpdateDataSaveAction.cs`
- Create: `assets/resource/base/custom/UpdateDataMarkSuccessAction.cs`
- Modify: `_src/MFAAvalonia/Services/UpdateDataPersistenceService.cs`

**Interfaces:** Each action implements `IMaaCustomAction.Run<T>(T context, in RunArgs args, in RunResults results) -> bool`; action parameter `stage` accepts only `warehouse` or `swordbook`.

- [ ] **Step 1: Add parameter tests.** Reject missing and unknown `stage`; accept both supported stages.
- [ ] **Step 2: Implement prepare.** Check stop state, delete only `warehouse_scan.json` or `swordbook_scan.json`, and return `false` for invalid input.
- [ ] **Step 3: Implement save.** Call the matching persistence method and log `[更新数据]` success/failure; return the persistence result.
- [ ] **Step 4: Implement success marker.** Use the current instance configuration and call `MarkSucceeded` only from the final successful branch.
- [ ] **Step 5: Register actions using the existing custom-action registration pattern and run `dotnet build _src/MFAAvalonia.sln`; expected result: no errors.**
- [ ] **Step 6: Commit with `git commit -m "feat: add update data pipeline actions"`.**

### Task 3: Implement the `UD_` main hub and warehouse route

**Files:**
- Create: `assets/resource/base/pipeline/UpdateData.json`
- Modify: `_src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs`
- Test: `_src/MFAAvalonia.Tests/Program.cs`

**Interfaces:** Add `MaaProcessor.ShouldSkipUpdateDataTask(MaaInterface.MaaInterfaceTask task, out string reason) -> bool` and use it before creating runnable task parameters.

- [ ] **Step 1: Add queue-filter tests.** Assert no saved time runs, matching day/week skips, next day/week runs, and non-`UpdateData` tasks are untouched.
- [ ] **Step 2: Copy the complete recovery chain from `U_DetectWhereAmI` with `UD_` names.** Every recovery node returns to `UD_DetectWhereAmI`; the hub timeout is 120000 and its error route is `UD_RestartGame`.
- [ ] **Step 3: Add three home confirmations.** Build three explicit `UD_CheckHomeBrightness → UD_IsHome` passes, each followed by a 1000ms hard wait; any failure returns to pass one.
- [ ] **Step 4: Add the warehouse branch.** Prepare the warehouse draft, route through a UD-specific copy of the existing warehouse scan flow, save on scan success, and route to the return-home loop.
- [ ] **Step 5: Add the return-home loop.** OCR `[1234,9,37,38]` for `×`, `x`, and `X`; click the recognized area, freeze the same area for 500ms, and loop until no close button remains.
- [ ] **Step 6: Route scopes.** `仓库` marks success after return-home; `仓库+刀帐` enters the swordbook hub; `刀帐` skips the warehouse branch.
- [ ] **Step 7: Parse `UpdateData.json`, verify references, `on_error`, and absence of `target_offset`; expected result: valid resource JSON.**
- [ ] **Step 8: Commit with `git commit -m "feat: add update data main hub and warehouse route"`.**

### Task 4: Implement the swordbook hub and navigation

**Files:**
- Modify: `assets/resource/base/pipeline/UpdateData.json`
- Test: `_src/MFAAvalonia.Tests/Program.cs`

- [ ] **Step 1: Add an independent `UD_SB_` recovery chain returning to `UD_SwordBookDetectWhereAmI`.**
- [ ] **Step 2: Add `CheckHomeBrightness → IsHome`, OCR `[2,163,104,28]` for `刀剑男士`, click it, and return to the swordbook hub.**
- [ ] **Step 3: Add `刀剑男士一览` OCR `[534,2,220,46]`, click `[10,571,26,80]`, and return to the hub.**
- [ ] **Step 4: Add `刀帐` OCR `[600,7,91,42]`; color-match `[140,98,4,5]` against `[178,33,32]`; matched path clicks `[154,161,106,206]`, unmatched path clicks `[140,98,4,5]`, freezes 500ms, then returns to the hub.**
- [ ] **Step 5: Add `序号` OCR `[527,3,229,43]`, run `SwordBookScanAction`, save the swordbook draft, click `[134,5,36,38]`, mark success, and terminate.**
- [ ] **Step 6: Add route-order tests for `仓库+刀帐` and a no-warehouse test for `刀帐`; expected result: PASS.**
- [ ] **Step 7: Commit with `git commit -m "feat: add update data swordbook route"`.**

### Task 5: Register task options and integrate frequency filtering

**Files:**
- Modify: `assets/interface.json`
- Modify: `_src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs`
- Modify: `_src/MFAAvalonia/Extensions/MaaFW/TaskLoader.cs`
- Test: `_src/MFAAvalonia.Tests/Program.cs`

- [ ] **Step 1: Register task `更新数据`, Entry `UpdateData`, and label `更新数据`.**
- [ ] **Step 2: Register select option exactly named `识别内容`, with `仓库`, `刀帐`, `仓库+刀帐`; default to `仓库+刀帐`.**
- [ ] **Step 3: Register select option `触发间隔`, with `每次`, `每天`, `每周`; default to `每天`.**
- [ ] **Step 4: Pass selected scope into pipeline overrides and selected interval into `ShouldSkipUpdateDataTask`; preserve old configurations by applying defaults.**
- [ ] **Step 5: Filter only expired `UpdateData` tasks before parameter creation and log `[更新数据] 本次按触发间隔跳过：{reason}`.**
- [ ] **Step 6: Parse interface JSON and assert task/option cases; expected result: valid JSON and PASS.**
- [ ] **Step 7: Commit with `git commit -m "feat: register update data task options"`.**

### Task 6: Update documentation, version metadata, and release package

**Files:**
- Modify: `docs/复用节点清单.md`
- Modify: `docs/开发日志.md`
- Modify: `assets/resource/announcement/0-v0.12.0-beta.3 更新公告.md`
- Modify: `assets/interface.json`
- Modify: `tools/pack.ps1`

- [ ] **Step 1: Add `UD_` and `UD_SB_` to the reuse list and correct its pipeline count.**
- [ ] **Step 2: Document the `识别内容` selector, trigger intervals, automatic saves, three home confirmations, and warehouse-plus-swordbook order.**
- [ ] **Step 3: Synchronize version/custom title/pack script/announcement according to the project release rules while preserving pre-existing user changes.**
- [ ] **Step 4: Run `dotnet run --project _src/MFAAvalonia.Tests/MFAAvalonia.Tests.csproj`, `dotnet build _src/MFAAvalonia.sln`, `git diff --check`, and `pwsh tools/pack.ps1`; expected result: all pass and zip contains `UpdateData.json` and beta.3 announcement.**
- [ ] **Step 5: Commit with `git commit -m "docs: document update data task release"`.**

## Self-review

- Spec coverage: configuration is Task 5; hubs and routes are Tasks 3-4; frequency and persistence are Tasks 1-2 and 5; documentation and packaging are Task 6.
- No placeholder terms are present; every step identifies files, interfaces, commands, and expected results.
- Existing uncommitted user changes must be reviewed before each commit and preserved unless they directly conflict with this feature.
