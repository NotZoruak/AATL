# 卡死循环检测(LoopDetector)实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 MATR「卡死重启」机制补齐"冻结画面恰好命中识别节点"形态的检测:统计连续相同动作(节点+类型+坐标),连续 200 次并通过 50 次二次确认后,自动执行 停止任务→重启模拟器→重启游戏→重连→恢复挂机。

**Architecture:** 新增 `LoopDetector` 纯统计类,挂入 `MaaProcessor.HandleCallBack` 的 `Action.Succeeded` 分支;判定后经 `LoopStuckDetected` 事件通知 `TaskQueueViewModel` 编排恢复;`RestartGameAction` 抽出公共静态入口 `RestartAndReloadGame()` 供两层复用。

**Tech Stack:** C# / .NET 10 / Avalonia / MaaFramework v5.12.2(回调事件 `MaaTasker.Callback`)

## Global Constraints

- 全局选项「卡死重启」开启(Index==0)时检测才生效,关闭时行为与现状完全一致
- 循环次数阈值硬编码:200 次,二次确认追加 50 次(用户已确认,不做可配置项)
- pipeline 层(所有 `*.json`)零改动;仅 `_src/` 应用层改动
- 不引入测试项目;按项目惯例以编译 + 实机/日志验证
- 日志统一走 `LoggerHelper`;禁止 `Console.WriteLine`
- 源码编码 UTF-8 无 BOM;4 空格缩进;注释中文
- git commit 前必须先获得用户明确许可(项目 CLAUDE.md 规则),计划中 commit 步骤执行前需用户确认

---

### Task 1: LoopDetector 类与 HandleCallBack 接入

**Files:**
- Create: `_src/MFAAvalonia/Helper/LoopDetector.cs`
- Modify: `_src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs`(HandleCallBack 分支,约 L1658-1720;字段区;新增事件与开关判断)

**Interfaces:**
- Produces: `LoopDetector`(public sealed class,`bool Feed(string nodeName, string action, int x, int y)`、`void Reset()`、`bool IsTriggered`、常量 `LoopThreshold=200`/`ConfirmCount=50`)
- Produces: `MaaProcessor.LoopStuckDetected`(public event `Action?`,Maa 回调线程触发)

- [ ] **Step 1: 新建 `LoopDetector.cs`**

```csharp
#nullable enable
using System;

namespace MFAAvalonia.Helper;

/// <summary>
/// 卡死循环检测器:统计连续相同的动作键(节点名+动作类型+目标坐标),
/// 连续达到阈值并通过二次确认后判定画面冻结。
/// 用于「卡死重启」开启时的补充检测,覆盖枢纽 timeout 检测不到的形态:
/// 冻结画面恰好命中某个识别节点,形成"识别命中→执行→跳回枢纽→timeout 重置"的无限循环。
/// </summary>
public sealed class LoopDetector
{
    /// <summary>连续相同动作次数阈值:画面冻结约 70-400 秒(按 350ms-2s 循环周期)</summary>
    public const int LoopThreshold = 200;

    /// <summary>达到阈值后的二次确认追加次数,确认期内动作键变化则放弃判定</summary>
    public const int ConfirmCount = 50;

    private string? _lastKey;
    private int _count;
    private int _confirmCount;

    /// <summary>是否已判定卡死触发(触发后需调用 <see cref="Reset"/> 复位)</summary>
    public bool IsTriggered { get; private set; }

    /// <summary>
    /// 喂入一次成功执行的动作事件。
    /// </summary>
    /// <returns>是否达到触发条件</returns>
    public bool Feed(string nodeName, string action, int x, int y)
    {
        var key = $"{nodeName}|{action}|{x}|{y}";
        if (key != _lastKey)
        {
            // 动作键变化:画面已变化,计数清零
            _lastKey = key;
            _count = 1;
            _confirmCount = 0;
            return false;
        }

        _count++;
        if (_count < LoopThreshold)
            return false;

        // 二次确认阶段:再累计 ConfirmCount 次(期间键不变)才触发
        _confirmCount++;
        if (_confirmCount < ConfirmCount)
            return false;

        IsTriggered = true;
        return true;
    }

    /// <summary>复位状态(任务停止、恢复流程开始时调用)</summary>
    public void Reset()
    {
        _lastKey = null;
        _count = 0;
        _confirmCount = 0;
        IsTriggered = false;
    }
}
```

- [ ] **Step 2: 编译验证新增文件**

Run: `dotnet build _src/MFAAvalonia.sln`
Expected: 编译通过,无新警告

- [ ] **Step 3: MaaProcessor 接入 —— 字段与事件**

在 `MaaProcessor` 类字段区(约 L1075 `_screencapAbortLogPending` 附近)新增:

```csharp
private readonly Helper.LoopDetector _loopDetector = new();
```

在类内合适位置(如 `HandleCallBack` 附近)新增事件与开关判断:

```csharp
/// <summary>循环卡死检测触发事件(Maa 回调线程触发,订阅方自行调度到主线程)</summary>
public event Action? LoopStuckDetected;

/// <summary>「卡死重启」全局选项是否开启(Index==0 即 Yes)。开启时循环检测才生效。</summary>
private bool IsLoopDetectorEnabled()
{
    return Interface?.GlobalSelectOptions
        ?.FirstOrDefault(o => o.Name == "卡死重启")?.Index == 0;
}
```

注意:`GlobalSelectOption.Index` 为 `int?`(与 `MergeGlobalOptionParams` 中 `selectOption.Index` 用法一致);`FirstOrDefault` 需要 `using System.Linq;`(文件已有)。

- [ ] **Step 4: HandleCallBack 分支接入**

在 `HandleCallBack` 现有 `if (args.Message.StartsWith(MaaMsg.Node.Recognition.Succeeded) || args.Message.StartsWith(MaaMsg.Node.Action.Succeeded))` 判断块内(L1680 起),新增独立分支(放在 ShowHitDraw 处理之前或之后均可,独立 if):

```csharp
if (args.Message.StartsWith(MaaMsg.Node.Action.Succeeded) && IsLoopDetectorEnabled())
{
    var nodeName = jObject["name"]?.ToString() ?? "";
    var actionName = jObject["action_details"]?["action"]?.ToString() ?? "";
    int hitX = 0, hitY = 0;
    if (jObject["action_details"]?["box"] is JArray boxArr && boxArr.Count >= 2)
    {
        hitX = boxArr[0].Value<int>();
        hitY = boxArr[1].Value<int>();
    }

    if (_loopDetector.Feed(nodeName, actionName, hitX, hitY))
    {
        LoggerHelper.Warning($"检测到动作循环卡死(画面冻结):节点={nodeName}, 动作={actionName}, 坐标=({hitX},{hitY})。触发自动恢复。");
        LoopStuckDetected?.Invoke();
    }
}
```

回调 `details` JSON 结构参考(maafw.log OnEventNotify):`{"action_details":{"action":"Click","box":[937,118,51,37],"detail":{"point":[965,141]},...},"name":"E_FlowerIsGameIcon",...}`。

- [ ] **Step 5: 编译验证**

Run: `dotnet build _src/MFAAvalonia.sln`
Expected: 编译通过

- [ ] **Step 6: 逻辑验证(临时观测)**

将 `LoopDetector.LoopThreshold` 临时改为 5、`ConfirmCount` 改为 3,运行 MATR 挂机 2-3 分钟,观察 `debug/logs/log-*.log`:
- 正常挂机时:无"检测到动作循环卡死"日志(画面变化使计数持续清零)
- 若正常挂机出现误触发,说明动作键粒度不够,检查 `box` 提取是否包含动态坐标
验证完成后**恢复 200/50 并提交**(提交前请用户确认)

- [ ] **Step 7: Commit(需用户许可)**

```bash
git add _src/MFAAvalonia/Helper/LoopDetector.cs _src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs
git commit -m "feat: 新增卡死循环检测(LoopDetector),补全冻结画面命中识别节点形态的检测"
```

---

### Task 2: RestartGameAction 抽取公共静态入口

**Files:**
- Modify: `_src/MFAAvalonia/Extensions/MaaFW/Custom/RestartGameAction.cs`(Run 方法 L297-331;新增静态方法)

**Interfaces:**
- Consumes: `LoopDetector`(Task 1,本任务不依赖);`MaaProcessorManager.Instance.Current`(现有)
- Produces: `public static void RestartGameAction.RestartAndReloadGame()` —— 完整执行 重启模拟器→force-stop→monkey 启动游戏,供 Task 3 调用

- [ ] **Step 1: 新增静态入口 `RestartAndReloadGame`**

在 `RestartGameAction` 类内(如 `Run` 方法前)新增:

```csharp
/// <summary>
/// 从当前处理器收集模拟器环境,执行完整的"重启模拟器+重启游戏"流程。
/// 供 pipeline 节点(Run)与 MATR 层卡死循环检测恢复复用。
/// </summary>
public static void RestartAndReloadGame()
{
    var action = new RestartGameAction();
    action.EnsureAdbInfo();

    var package = GetPackageName();

    // 0. 重启模拟器(模拟器重启会连带杀死游戏进程)
    action.RestartEmulator();

    // 1. 强制停止游戏进程,确保从卡死状态恢复
    LoggerHelper.Info($"[RestartGameAction] 强制停止游戏进程: {package}");
    RunAdbCommand(action._adbPath!, action._adbSerial ?? "", $"shell am force-stop {package}");
    Thread.Sleep(2000);

    // 2. 重新启动游戏
    LoggerHelper.Info($"[RestartGameAction] 重新启动游戏: {package}");
    RunAdbCommand(action._adbPath!, action._adbSerial ?? "", $"shell monkey -p {package} -c android.intent.category.LAUNCHER 1");

    LoggerHelper.Info("[RestartGameAction] 游戏重启完成");
}
```

- [ ] **Step 2: 简化 Run 方法为调用静态入口**

替换 `Run<T>` 方法体(保留原 try/catch 结构):

```csharp
public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
{
    try
    {
        ActionParamHelper.ThrowIfStopping(context);
        RestartAndReloadGame();
        return true;
    }
    catch (MaaStopException)
    {
        LoggerHelper.Info("[RestartGameAction] 检测到手动停止，已取消执行");
        return false;
    }
    catch (Exception e)
    {
        LoggerHelper.Error($"[RestartGameAction] 错误: {e.Message}");
        return false;
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build _src/MFAAvalonia.sln`
Expected: 编译通过;`Run` 中不再使用 `context` 以外的旧局部逻辑,无未使用字段警告

- [ ] **Step 4: 回归验证(功能等价)**

验证 RestartGameAction 原路径不受影响:在模拟器正常运行时,通过现有「卡死重启」触发路径(或临时从调试入口)执行一次 `RestartAndReloadGame`,观察:
- mumu-cli 重启命令执行、模拟器重启、force-stop/monkey 执行、日志完整
- 游戏重新启动成功,MATR 重连后任务可继续

- [ ] **Step 5: Commit(需用户许可)**

```bash
git add _src/MFAAvalonia/Extensions/MaaFW/Custom/RestartGameAction.cs
git commit -m "refactor: RestartGameAction 抽取公共静态入口 RestartAndReloadGame 供 MATR 层复用"
```

---

### Task 3: TaskQueueViewModel 自动恢复编排

**Files:**
- Modify: `_src/MFAAvalonia/ViewModels/Pages/TaskQueueViewModel.cs`(构造函数区订阅事件,约 L42-70;新增恢复方法)

**Interfaces:**
- Consumes: `MaaProcessor.LoopStuckDetected`(Task 1)、`RestartGameAction.RestartAndReloadGame()`(Task 2)、现有 `StopTask()`(L533)/`StartTask()`(L452)/`Processor.ReconnectAsync()`
- Produces: 无(事件消费者)

- [ ] **Step 1: 订阅事件**

构造函数(约 L46 `TaskQueueViewModel(string instanceId)`)内,`_processorField` 初始化后新增:

```csharp
_processorField.LoopStuckDetected += OnLoopStuckDetected;
```

- [ ] **Step 2: 实现恢复编排**

类内新增:

```csharp
/// <summary>
/// 循环卡死触发处理:Maa 回调线程触发,切主线程编排恢复流程。
/// </summary>
private void OnLoopStuckDetected()
{
    DispatcherHelper.PostOnMainThread(() =>
    {
        _ = RunAutoRecoverAsync();
    });
}

/// <summary>
/// 循环卡死自动恢复:停止任务 → 重启模拟器与游戏 → 重连 → 重新启动任务队列。
/// </summary>
private async Task RunAutoRecoverAsync()
{
    LoggerHelper.Warning("自动恢复开始:停止任务 → 重启模拟器 → 重启游戏 → 重连 → 恢复挂机。");
    StopTask();
    await Task.Delay(1500);
    await Task.Run(() => RestartGameAction.RestartAndReloadGame());
    LoggerHelper.Info("自动恢复:模拟器与游戏重启完成,重新连接模拟器...");
    await Processor.ReconnectAsync();
    LoggerHelper.Info("自动恢复:重连完成,重新启动任务队列。");
    StartTask();
}
```

新增 using:`using MFAAvalonia.Extensions.MaaFW.Custom;`(如不存在)。

- [ ] **Step 3: 编译验证**

Run: `dotnet build _src/MFAAvalonia.sln`
Expected: 编译通过

- [ ] **Step 4: 实机验证全流程**

复现冻结场景验证完整恢复链路:
1. 将 `LoopDetector.LoopThreshold` 临时改为 10、`ConfirmCount` 改为 5(验证后恢复)
2. 启动挂机任务,任务运行中用任务管理器**暂停**(或挂起)`MuMuNxDevice.exe` 模拟画面冻结
3. 观察 `debug/logs/log-*.log` 依次出现:检测到动作循环卡死日志 → 自动恢复开始 → 停止任务 → 模拟器重启(mumu-cli)→ 游戏重启 → 重连 → 重新启动任务队列
4. 确认挂机自动恢复运行,无人工干预
5. 恢复 200/50

- [ ] **Step 5: Commit(需用户许可)**

```bash
git add _src/MFAAvalonia/ViewModels/Pages/TaskQueueViewModel.cs
git commit -m "feat: 循环卡死自动恢复编排(停止→重启模拟器→重连→恢复挂机)"
```

---

### Task 4: 文档更新与回归验证

**Files:**
- Modify: `docs/全局设置设计.md`(「卡死重启」小节)

- [ ] **Step 1: 更新全局设置设计文档**

在「卡死重启」小节「重启流程」之后补充:

```markdown
### 循环检测(补充检测形态)

枢纽 timeout 只覆盖「全部识别不命中」的冻结形态。若冻结画面恰好命中某个识别
node(如 `E_FlowerIsGameIcon`),会形成"识别命中→执行→跳回枢纽→timeout 重置"
的无限循环,timeout 永不触发。

MATR 应用层(LoopDetector)统计连续相同的动作(节点名+动作类型+目标坐标):
- 连续 200 次相同动作 → 二次确认再累计 50 次(期间动作变化则放弃)→ 判定画面冻结
- 触发后自动执行:停止任务 → 重启模拟器(mumu-cli)→ 重启游戏 → 重连 → 重新启动任务队列
- 仅「卡死重启」开启时生效;阈值硬编码,不做配置项
```

- [ ] **Step 2: 正常挂机回归**

恢复 200/50 后,正常挂机 30 分钟以上,确认:
- `debug/logs/log-*.log` 无"检测到动作循环卡死"误触发
- 挂机流程(合战场/远征)不受影响

- [ ] **Step 3: Commit(需用户许可)**

```bash
git add docs/全局设置设计.md
git commit -m "docs: 补充卡死重启循环检测说明"
```

---

## 自审记录

- **Spec 覆盖**:设计文档的检测(200 次+50 次确认)、作用范围(跟随卡死重启开关)、恢复流程(停止→重启→重连→StartTask)、硬编码参数、文档更新,分别由 Task 1/3/4 覆盖;RestartGameAction 复用由 Task 2 覆盖
- **无占位符**:各步骤含完整代码与验证命令
- **类型一致**:`LoopDetector.Feed(string,string,int,int)` 在 Task 1 定义、Task 1 Step 4 使用一致;`LoopStuckDetected` 事件在 Task 1 定义、Task 3 订阅一致;`RestartAndReloadGame()` 静态方法在 Task 2 定义、Task 3 调用一致;`StopTask()/StartTask()/ReconnectAsync()` 均使用现有签名

---

### Task 5: SmartWaitTracker 共享状态 + SmartWaitAction 接入

**Files:**
- Create: `_src/MFAAvalonia/Extensions/MaaFW/Custom/SmartWaitTracker.cs`
- Modify: `_src/MFAAvalonia/Extensions/MaaFW/Custom/SmartWaitAction.cs`

**Interfaces:**
- Produces: `SmartWaitTracker`(static class):`void BeginWait(DateTime endTime)` / `void Clear()` / `bool IsInWaitWindow()`
- Consumes: 无

- [ ] **Step 1: 新建 SmartWaitTracker.cs**(仿 ExpeditionReturnTracker 模式)

```csharp
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 智能等待窗口追踪器:SmartWaitAction 记录等待窗口,供 MATR 层无响应检测排除合法静默。
/// </summary>
public static class SmartWaitTracker
{
    private static DateTime? _waitEndsAt;

    /// <summary>设置等待窗口(等待开始)</summary>
    public static void BeginWait(DateTime endTime)
    {
        _waitEndsAt = endTime;
    }

    /// <summary>清除等待窗口(等待结束/任务停止)</summary>
    public static void Clear()
    {
        _waitEndsAt = null;
    }

    /// <summary>是否正处于智能等待窗口内</summary>
    public static bool IsInWaitWindow()
    {
        return _waitEndsAt != null && DateTime.Now < _waitEndsAt.Value;
    }
}
```

- [ ] **Step 2: SmartWaitAction 接入窗口记录**

在 `Run` 中计算 `waitSeconds` 后、sleep 前:

```csharp
if (waitSeconds > 0)
{
    SmartWaitTracker.BeginWait(DateTime.Now.AddSeconds(waitSeconds));
    try
    {
        var deadline = DateTime.Now.AddSeconds(waitSeconds);
        while (DateTime.Now < deadline)
        {
            ActionParamHelper.ThrowIfStopping(context);
            var chunk = (int)Math.Min(5, (deadline - DateTime.Now).TotalSeconds);
            if (chunk <= 0) break;
            Thread.Sleep(chunk * 1000);
        }
    }
    finally
    {
        SmartWaitTracker.Clear();
    }
}
```

(用 try/finally 保证等待结束、停止、异常三条路径都清除窗口)

- [ ] **Step 3: 编译验证**

Run: `dotnet build _src/MFAAvalonia.sln`
Expected: 编译通过

- [ ] **Step 4: Commit(需用户许可,当前统一提交)**

---

### Task 6: MaaProcessor 最后回调时间 + TaskQueueViewModel 无响应检查循环

**Files:**
- Modify: `_src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs`
- Modify: `_src/MFAAvalonia/ViewModels/Pages/TaskQueueViewModel.cs`

**Interfaces:**
- Consumes: `SmartWaitTracker.IsInWaitWindow()`(Task 5)、现有 `RunAutoRecoverAsync`(Task 3)、`StartTask`/`StopTask`
- Produces: `MaaProcessor.LastCallbackTime`(public DateTime)

- [ ] **Step 1: MaaProcessor 记录最后回调时间**

HandleCallBack 开头(解析 jObject 前)新增:

```csharp
LastCallbackTime = DateTime.Now;
```

类内新增属性:

```csharp
/// <summary>最后一次 Maa 回调时间(供无响应检测)</summary>
public DateTime LastCallbackTime { get; private set; } = DateTime.Now;
```

- [ ] **Step 2: TaskQueueViewModel 启动/停止检查循环**

StartTask 成功路径(IsRunning 检查通过后)启动:

```csharp
_stuckCheckCts?.Cancel();
_stuckCheckCts = new CancellationTokenSource();
_ = RunStuckCheckAsync(_stuckCheckCts.Token);
```

StopTask 内(停止执行器后)取消:

```csharp
_stuckCheckCts?.Cancel();
_stuckCheckCts = null;
```

字段:`private CancellationTokenSource? _stuckCheckCts;`

- [ ] **Step 3: 实现检查循环**

```csharp
/// <summary>
/// 无响应检测:任务运行中,若连续超过阈值无任何 Maa 回调且不在智能等待窗口,
/// 判定模拟器无响应,触发与循环检测相同的自动恢复流程。
/// </summary>
private const double StuckSilentThresholdSeconds = 120;

private async Task RunStuckCheckAsync(CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            if (!IsRunning)
                continue;
            if (Custom.SmartWaitTracker.IsInWaitWindow())
                continue;
            if ((DateTime.Now - Processor.LastCallbackTime).TotalSeconds < StuckSilentThresholdSeconds)
                continue;

            LoggerHelper.Warning("检测到模拟器无响应(超过 120 秒无回调且不在智能等待窗口),触发自动恢复。");
            await RunAutoRecoverAsync();
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"无响应检测异常: {e.Message}");
        }
    }
}
```

注意:SmartWaitTracker 命名空间为 `MFAAvalonia.Extensions.MaaFW.Custom`,TaskQueueViewModel 已有 using(引用 RestartGameAction 时已添加);`RunAutoRecoverAsync` 触发后内部 StopTask 会取消本循环,StartTask 重新启动,无重复触发。

- [ ] **Step 4: 编译验证**

Run: `dotnet build _src/MFAAvalonia.sln`
Expected: 编译通过

- [ ] **Step 5: 实机验证(主会话协调用户)**

1. 挂起 MuMuNxDevice:无回调且不在 SmartWait 窗口 → 120s 后应触发"检测到模拟器无响应"→ 自动恢复(模拟器重启 → 游戏重启 → 重连 → 任务续跑)
2. 远征任务运行中(有远征在途):SmartWait 等待窗口内不误判(观察窗口期间无触发)
3. 正常合战场挂机 30 分钟:无误判

- [ ] **Step 6: Commit(需用户许可,当前统一提交)**
