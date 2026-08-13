# 任务轮次进度报告 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 设置了重复次数（repeat_count > 0）的 MAAFW 任务每结束一轮，在任务日志输出「任务完成: {任务名} 进度 {X}/{Y}」。

**Architecture:** 在 `MFATask.Run` 的轮次 for 循环内、`await Action()` 成功后追加一行进度日志；文案走 `LangKeys.TaskRoundComplete` 本地化 key，三语资源同步；无限重复与单次任务不输出。

**Tech Stack:** C# / .NET 10 / Avalonia / MaaFW

## Global Constraints

- 代码注释、日志文案使用中文（项目规范）
- 不做 git commit（CLAUDE.md：AI 不得自行提交，除非用户明确要求）
- 仅修改 `_src/` 下代码与资源；验证方式为 `dotnet build` + 实际运行（本项目无单元测试框架）
- 进度日志仅在 `!infinite && Count > 1 && Type == MFATaskType.MAAFW` 时输出

---

### Task 1: 新增 TaskRoundComplete 本地化资源

**Files:**
- Modify: `_src/MFAAvalonia/Helper/LangKeys.cs`（TaskStart 声明附近）
- Modify: `_src/MFAAvalonia/Assets/Localization/Strings.resx`（TaskStart 数据块附近）
- Modify: `_src/MFAAvalonia/Assets/Localization/Strings.zh-Hant.resx`
- Modify: `_src/MFAAvalonia/Assets/Localization/Strings.ja-JP.resx`

**Interfaces:**
- Produces: `LangKeys.TaskRoundComplete`（字符串常量 `"TaskRoundComplete"`），供 Task 2 的 `AddLogByKey` 使用

- [ ] **Step 1: LangKeys.cs 添加常量**

在 `public static readonly string TaskStart = "TaskStart";` 之前插入：

```csharp
	public static readonly string TaskRoundComplete = "TaskRoundComplete";
```

- [ ] **Step 2: Strings.resx（简体中文）添加文案**

在 TaskStart 数据块之前插入（沿用 4 空格缩进格式）：

```xml
    <data name="TaskRoundComplete" xml:space="preserve">
        <value>任务完成: {0} 进度 {1}/{2}</value>
    </data>
```

- [ ] **Step 3: Strings.zh-Hant.resx 添加文案**

在 TaskStart 数据块之前插入：

```xml
    <data name="TaskRoundComplete" xml:space="preserve">
        <value>任務完成: {0} 進度 {1}/{2}</value>
    </data>
```

- [ ] **Step 4: Strings.ja-JP.resx 添加文案**

在 TaskStart 数据块之前插入（沿用 2 空格缩进格式）：

```xml
  <data name="TaskRoundComplete" xml:space="preserve">
    <value>タスク完了: {0} 進度 {1}/{2}</value>
  </data>
```

- [ ] **Step 5: 验证资源完整性**

Run: `python -c "import xml.etree.ElementTree as ET; [ET.parse(p) for p in [r'_src/MFAAvalonia/Assets/Localization/Strings.resx', r'_src/MFAAvalonia/Assets/Localization/Strings.zh-Hant.resx', r'_src/MFAAvalonia/Assets/Localization/Strings.ja-JP.resx']]; print('XML 合法')"`
Expected: 输出「XML 合法」，三个文件均含 TaskRoundComplete 数据块

---

### Task 2: MFATask.Run 添加轮次进度日志

**Files:**
- Modify: `_src/MFAAvalonia/Helper/ValueType/MFATask.cs:41-73`（Run 方法）

**Interfaces:**
- Consumes: `LangKeys.TaskRoundComplete`（Task 1 产出）
- Produces: 每轮完成后调用 `OwnerViewModel.AddLogByKey(...)` 输出进度日志（行为，无新接口）

- [ ] **Step 1: 修改 Run 方法**

在 `Run` 方法中：循环前记录无限标记，循环内 `await Action()` 后追加进度日志。完整目标代码：

```csharp
    public async Task<MFATaskStatus> Run(CancellationToken token)
    {
        try
        {
            var infinite = Count < 0;   // 无限重复标记，先记录再转 int.MaxValue
            if (Count < 0)
                Count = int.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Type == MFATaskType.MAAFW)
                {
                    OwnerViewModel?.AddLogByKey(LangKeys.TaskStart, (Avalonia.Media.IBrush?)null, true, true, LanguageHelper.GetLocalizedString(Name));
                    OwnerViewModel?.SetCurrentTaskName(LanguageHelper.GetLocalizedString(Name));
                }
                await Action();
                // 有限重复的 MAAFW 任务每轮结束后报告进度；无限重复与单次任务不报
                if (!infinite && Count > 1 && Type == MFATaskType.MAAFW)
                {
                    OwnerViewModel?.AddLogByKey(LangKeys.TaskRoundComplete, (Avalonia.Media.IBrush?)null, true, true,
                        LanguageHelper.GetLocalizedString(Name), (i + 1).ToString(), Count.ToString());
                }
            }
            return MFATaskStatus.SUCCEEDED;
        }
        catch (MaaJobStatusException)
        {
            LoggerHelper.Error($"任务执行失败：{LanguageHelper.GetLocalizedString(Name)}");
            return MFATaskStatus.FAILED;
        }
        catch (OperationCanceledException)
        {
            return MFATaskStatus.STOPPED;
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"任务执行异常：任务={LanguageHelper.GetLocalizedString(Name)}，原因={ex.Message}", ex);
            return MFATaskStatus.FAILED;
        }
    }
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build _src/MFAAvalonia.sln`
Expected: 构建成功，无 CS 错误

---

### Task 3: 发布与运行验证

**Files:**
- 无代码改动；按需发布构建产物到工作区根目录

**Interfaces:**
- Consumes: Task 1、Task 2 的完整代码

- [ ] **Step 1: 发布并同步产物**

Run: `dotnet publish _src/MFAAvalonia.Desktop`，然后按 CLAUDE.md 复制产物：

```bash
cp _src/MFAAvalonia/bin/Release/net10.0/MFAAvalonia.Core.dll runtimes/libs/
cp _src/bin/AnyCPU/Release/publish/MATR.dll ./
cp _src/bin/AnyCPU/Release/publish/MATR.exe ./
```

（若实际输出路径不同，以 dotnet publish 实际产物为准；MFAAvalonia.Core.dll 必须从项目自身输出目录复制，不用 AnyCPU 缓存副本）

- [ ] **Step 2: 运行验证（用户执行）**

1. 启动 MATR，运行地下城任务（默认重复 9999 次），至少完成 1 轮
2. 检查任务日志出现「地下城 任务完成 进度 1/9999」
3. 运行合战场（repeat_count 已改为 3），确认日志输出 1/3、2/3、3/3，且 3 轮后队列结束
4. 运行一个单次任务（repeat_count = 1），确认**不**输出进度日志
