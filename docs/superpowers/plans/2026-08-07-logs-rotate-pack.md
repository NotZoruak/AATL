# 日志切块与打包优化 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MaaCore 日志运行期间按 20 MiB 切块,导出 zip 分卷阈值收紧为 20 MiB。

**Architecture:** 新增静态类 MaaLogRotator,后台轮询检查 `debug/maafw.log` 大小,超阈值时通过 MaaCore 官方 API `SetOption_LogDir` 切换日志目录使旧句柄关闭,再移动旧文件为 bak;任务停止时在同一位置执行一次兜底切块。分卷阈值仅改一个常量。

**Tech Stack:** .NET 10 / C# 14 / Avalonia / MaaFramework 5.12(Maa.Framework NuGet 5.10.0 + Maa.Framework.Runtimes 5.12.2)

## Global Constraints

- 不得自行执行 `git commit` 或 `git push`,每次提交前必须先获得用户明确许可
- 代码注释、日志消息、变量说明全部使用中文
- C# 代码:.NET 10、C# 14、Nullable 启用、文件级命名空间(`namespace MFAAvalonia.Helper;`)、4 空格缩进、PascalCase 公共成员、`_camelCase` 私有字段
- 日志一律通过 `LoggerHelper` 输出,不使用 `Console.WriteLine`
- `.cs` 文件编码:UTF-8 without BOM
- 切块阈值与分卷阈值统一为 20 MiB = 20_971_520 字节
- bak 命名沿用现有格式:`maafw.bak.{yyyy.MM.dd-HH.mm.ss.fff}.log`(见 AppPaths.cs:209)

---

## 文件结构

| 文件 | 操作 | 职责 |
|---|---|---|
| `_src/LogDirVerify/`(临时) | 创建,验证后删除 | 前置验证程序:验证 MaaCore LogDir 切换行为 |
| `_src/MFAAvalonia/Helper/MaaLogRotator.cs` | 创建 | 运行期切块组件(轮询 + 单次切块方法) |
| `_src/MFAAvalonia/App.axaml.cs` | 修改(约 138 行后) | 启动时挂载 MaaLogRotator.Start() |
| `_src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs` | 修改(4035 行附近) | 任务停止后执行兜底切块 |
| `_src/MFAAvalonia/Helper/FileLogExporter.cs` | 修改(17 行) | 分卷阈值 24_500_000 → 20_971_520 |

---

## Task 1: 前置验证程序(验证 MaaCore LogDir 切换行为)

**Files:**
- Create: `_src/LogDirVerify/LogDirVerify.csproj`、`_src/LogDirVerify/Program.cs`
- 临时目录,验证通过后整个删除,不进入版本管理

**Interfaces:**
- Produces: 验证结论(旧文件可移动 / 新目录正常写入 / 切回后继续写),供 Task 2 决定是否采用 LogDir 切换方案

背景:验证程序利用 MaaCore 自身特性——`OptionMgr::set_log_dir` 内部会写日志(`LogFunc`/`LogInfo`),因此每次 `SetOption_LogDir` 调用都会产生日志文件,无需 controller 与模拟器。

- [ ] **Step 1: 创建验证项目**

创建 `_src/LogDirVerify/LogDirVerify.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDefaultItems>false</EnableDefaultItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Reference Include="MaaFramework.Binding">
      <HintPath>..\..\runtimes\libs\MaaFramework.Binding.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 编写验证逻辑**

创建 `_src/LogDirVerify/Program.cs`:

```csharp
using System.Text;
using MaaFramework.Binding;

// 设置 native 库搜索路径:先试当前目录(构建后从 runtimes/win-x64/native 复制),再试项目内路径
var nativeCandidates = new[]
{
    AppContext.BaseDirectory,
    @"D:\Claude_Workspace\MATR\runtimes\win-x64\native",
    @"D:\Claude_Workspace\MATR\runtimes\libs",
};
AppContext.SetData("NATIVE_DLL_SEARCH_DIRECTORIES", string.Join(Path.PathSeparator, nativeCandidates));

var debugDir = @"D:\Claude_Workspace\MATR\debug";
var dir1 = Path.Combine(debugDir, "verify_1");
var dir2 = Path.Combine(debugDir, "verify_2");
Directory.CreateDirectory(dir1);
Directory.CreateDirectory(dir2);

try
{
    using var tasker = new MaaTasker();

    Console.WriteLine("第 1 次 SetOption_LogDir -> " + dir1);
    tasker.Global.SetOption_LogDir(dir1);
    Thread.Sleep(2000);

    var file1 = Path.Combine(dir1, "maafw.log");
    Console.WriteLine("dir1/maafw.log 存在: " + File.Exists(file1));
    if (File.Exists(file1))
        Console.WriteLine("dir1/maafw.log 大小: " + new FileInfo(file1).Length);

    Console.WriteLine("第 2 次 SetOption_LogDir -> " + dir2);
    tasker.Global.SetOption_LogDir(dir2);
    Thread.Sleep(2000);

    var file2 = Path.Combine(dir2, "maafw.log");
    Console.WriteLine("dir2/maafw.log 存在: " + File.Exists(file2));

    Console.WriteLine("尝试移动 dir1/maafw.log -> dir1/maafw.bak.verify.log");
    if (File.Exists(file1))
    {
        File.Move(file1, Path.Combine(dir1, "maafw.bak.verify.log"));
        Console.WriteLine("移动成功(旧句柄已关闭)");
    }
    else
    {
        Console.WriteLine("跳过: dir1/maafw.log 不存在");
    }

    Thread.Sleep(2000);
    Console.WriteLine("切回后验证: 第 3 次 SetOption_LogDir -> " + debugDir);
    tasker.Global.SetOption_LogDir(debugDir);
    Thread.Sleep(2000);

    var backFile = Path.Combine(debugDir, "maafw.log");
    Console.WriteLine("切回后 debug/maafw.log 存在: " + File.Exists(backFile));
    Console.WriteLine("验证结束");
}
catch (Exception ex)
{
    Console.WriteLine("验证失败: " + ex);
    Environment.ExitCode = 1;
}
```

- [ ] **Step 3: 准备 native 依赖并运行**

```bash
cd _src/LogDirVerify
dotnet build
# 将 native DLL 复制到输出目录(binding 的 P/Invoke 依赖)
cp ../../runtimes/win-x64/native/*.dll bin/Debug/net10.0/
cp ../../runtimes/libs/MaaFramework.Binding*.dll bin/Debug/net10.0/
./bin/Debug/net10.0/LogDirVerify.exe
```

预期输出(全部满足才算验证通过):
1. `dir1/maafw.log 存在: True` 且大小 > 0(切换后新目录正常写日志)
2. `dir2/maafw.log 存在: True`(二次切换正常)
3. `移动成功(旧句柄已关闭)`(旧文件可移动,切块方案成立)
4. `切回后 debug/maafw.log 存在: True`(切回原目录继续写)

若 `SetOption_LogDir` 调用抛异常或某文件不存在,截图输出反馈用户,与用户讨论是否退回备选方案(仅任务停止时切块)。

- [ ] **Step 4: 清理验证产物并记录结论**

```bash
rm -rf _src/LogDirVerify
# 清理验证期间产生的目录
rm -rf debug/verify_1 debug/verify_2
# 若 debug/maafw.log 被验证程序创建且为空或极小,可一并删除(正常运行时 MaaCore 会重新创建)
rm -f debug/maafw.log
```

将验证结论记录到 Task 2 的实现注释中(通过:LogDir 切换方案成立;不通过:与用户商定降级方案)。

- [ ] **Step 5: 向用户汇报验证结果,征得许可后提交**

不执行 commit,向用户报告验证输出,由用户决定是否继续 Task 2。

---

## Task 2: MaaLogRotator 切块组件

**Files:**
- Create: `_src/MFAAvalonia/Helper/MaaLogRotator.cs`

**Interfaces:**
- Consumes: `MaaProcessorManager.Instance.Current.MaaTasker`(MaaProcessor.cs:657,类型 `MaaFramework.Binding.MaaTasker?`)、`AppPaths.InstallRoot`、`AppPaths.TempMaaFwDirectory`、`LoggerHelper.Warning/Info`
- Produces: `MaaLogRotator.Start()`(App.axaml.cs 调用)、`MaaLogRotator.TryRotateIfNeeded()`(轮询与 Task 4 停止兜底共用)

- [ ] **Step 1: 创建 MaaLogRotator.cs**

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.Helper;

/// <summary>
/// MaaCore 日志(debug/maafw.log)运行期切块器。
/// 利用 MaaCore 官方 API SetOption_LogDir 运行期切换日志目录,
/// 切换后旧文件句柄关闭,即可安全移动为 bak,实现按大小切块。
/// </summary>
public static class MaaLogRotator
{
    // 切块阈值:20 MiB
    public const long MaxLogChunkBytes = 20L * 1024 * 1024;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private static CancellationTokenSource? _cts;
    private static Task? _pollTask;

    // 当前 MaaCore 日志目录。正常情况下为 debug 根目录(null 表示默认);
    // 若切回原目录失败,记录切块临时目录,使轮询能继续追踪该目录下的日志,避免失去切块控制。
    private static string? _activeLogDir;

    /// <summary>
    /// 启动后台轮询切块。应用启动时调用一次,幂等。
    /// </summary>
    public static void Start()
    {
        if (_pollTask is { IsCompleted: false })
            return;

        _cts = new CancellationTokenSource();
        _pollTask = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    TryRotateIfNeeded();
                }
                catch (Exception ex)
                {
                    // 轮询异常不影响任务运行,仅记录
                    LoggerHelper.Warning($"日志切块轮询异常: {ex.Message}");
                }

                try
                {
                    await Task.Delay(PollInterval, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, _cts.Token);
    }

    /// <summary>
    /// 检查 debug/maafw.log 是否超过阈值,超过则执行一次切块。
    /// 轮询与任务停止兜底共用;任何异常均内部消化,不向外抛出。
    /// </summary>
    public static void TryRotateIfNeeded()
    {
        var debugDir = Path.Combine(AppPaths.InstallRoot, "debug");
        var activeDir = _activeLogDir ?? debugDir;
        var mainLog = Path.Combine(activeDir, "maafw.log");

        if (!File.Exists(mainLog))
            return;

        if (new FileInfo(mainLog).Length <= MaxLogChunkBytes)
            return;

        var tasker = MaaProcessorManager.Instance.Current.MaaTasker;
        if (tasker == null)
            return;

        RotateCore(tasker, debugDir, mainLog);
    }

    /// <summary>
    /// 执行切块:切换日志目录 → 移动旧日志 → 保留过渡日志 → 切回原目录。
    /// </summary>
    private static void RotateCore(MaaFramework.Binding.MaaTasker tasker, string debugDir, string mainLog)
    {
        var rotateDir = Path.Combine(AppPaths.TempMaaFwDirectory, $"rotate_{DateTime.Now:yyyyMMdd_HHmmss_fff}");
        Directory.CreateDirectory(rotateDir);

        try
        {
            LoggerHelper.Info($"日志切块: 主日志 {new FileInfo(mainLog).Length} 字节, 切换日志目录至 {rotateDir}");

            // 1. 切到临时目录,使 MaaCore 关闭旧文件句柄
            if (!tasker.Global.SetOption_LogDir(rotateDir))
            {
                LoggerHelper.Warning("日志切块失败: SetOption_LogDir 切换到临时目录失败");
                return;
            }

            // 2. 移动旧日志为 bak(此刻旧句柄已关闭,移动必然成功)
            MoveToBak(mainLog, debugDir);

            // 3. 保留切换间隙的过渡日志(临时目录中的新文件,此时仅几行),一并移入 debug 目录
            var transitionLog = Path.Combine(rotateDir, "maafw.log");
            if (File.Exists(transitionLog) && new FileInfo(transitionLog).Length > 0)
            {
                MoveToBak(transitionLog, debugDir);
            }

            // 4. 切回原目录,后续日志继续写 debug/maafw.log
            if (tasker.Global.SetOption_LogDir(debugDir))
            {
                _activeLogDir = null;
            }
            else
            {
                // 切回失败:记录当前日志目录,轮询继续追踪 rotateDir 下的日志,保持切块能力
                _activeLogDir = rotateDir;
                LoggerHelper.Warning($"日志切块: 切回日志目录 {debugDir} 失败,已记录当前日志目录 {rotateDir}");
            }

            LoggerHelper.Info("日志切块完成");
        }
        catch (Exception ex)
        {
            // 切块失败不影响任务运行,仅记录;原日志文件若未移动则保留原样
            LoggerHelper.Warning($"日志切块失败: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(rotateDir) && !Directory.EnumerateFileSystemEntries(rotateDir).Any())
                    Directory.Delete(rotateDir);
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"清理切块临时目录失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 移动日志文件为 bak 到指定目录,命名沿用现有格式(与 AppPaths.CleanupOldDebugLogs 一致)。
    /// </summary>
    private static void MoveToBak(string sourcePath, string targetDir)
    {
        var bakName = $"maafw.bak.{DateTime.Now:yyyy.MM.dd-HH.mm.ss.fff}.log";
        var bakPath = Path.Combine(targetDir, bakName);
        File.Move(sourcePath, bakPath);
        LoggerHelper.Info($"日志切块: {Path.GetFileName(sourcePath)} → {bakName}");
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
dotnet build _src/MFAAvalonia.sln
```

预期:编译通过,无警告错误。

- [ ] **Step 3: 代码自检**

核对:
1. `MaaProcessorManager.Instance.Current` 为 `MaaProcessor`(非空),其 `MaaTasker` 属性可为 null,已判空
2. `tasker.Global.SetOption_LogDir(string)` 签名与 MaaProcessor.cs:1411 的现有调用一致,返回 bool 已检查
3. `Directory.EnumerateFileSystemEntries(rotateDir).Any()` 需要 `using System.Linq;`(已包含)
4. 异常路径:任意一步失败只记录不抛出,主流程(任务运行)不受影响
5. 所有注释为中文

- [ ] **Step 4: 向用户汇报,征得许可后提交**

不执行 commit,报告自检结果,由用户决定。

---

## Task 3: 启动挂载

**Files:**
- Modify: `_src/MFAAvalonia/App.axaml.cs:138`(CleanupOldDebugLogs 调用之后)

**Interfaces:**
- Consumes: `MaaLogRotator.Start()`(Task 2)

- [ ] **Step 1: 在启动初始化中添加挂载**

修改 `App.axaml.cs` Initialize() 中,`AppPaths.CleanupOldDebugLogs(...)` 调用之后追加一行:

```csharp
AppPaths.CleanupOldDebugLogs(
    logInfo: message => LoggerHelper.Info(message),
    logWarning: message => LoggerHelper.Warning(message));
// 启动 MaaCore 日志运行期切块
MaaLogRotator.Start();
```

- [ ] **Step 2: 构建验证**

```bash
dotnet build _src/MFAAvalonia.sln
```

预期:编译通过。

- [ ] **Step 3: 向用户汇报,征得许可后提交**

---

## Task 4: 任务停止兜底

**Files:**
- Modify: `_src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs:4031-4038`(ExecuteStopCore)

**Interfaces:**
- Consumes: `MaaLogRotator.TryRotateIfNeeded()`(Task 2)

- [ ] **Step 1: 在停止完成后追加兜底切块**

修改 `ExecuteStopCore` 方法,在 `stopAction.Invoke()` 与 `Idle = true` 之间插入兜底调用。此处运行于后台线程(`TaskManager.RunTaskAsync`),此时 `AbortCurrentTasker` 已调用 `MaaTasker.Stop().Wait()`,日志写入已停止,是运行期最干净的切块时机:

```csharp
private void ExecuteStopCore(bool finished, Action stopAction)
{
    TaskManager.RunTaskAsync(() =>
    {
        if (!finished) DispatcherHelper.PostOnMainThread(() => AddLogByKey(LangKeys.Stopping, (IBrush?)null));

        stopAction.Invoke();

        // 任务已停止,日志写入静止,执行一次兜底切块
        try
        {
            MaaLogRotator.TryRotateIfNeeded();
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"停止后日志切块异常: {ex.Message}");
        }

        DispatcherHelper.PostOnMainThread(() => Instances.RootViewModel.Idle = true);
    }, null, "停止maafw任务");
}
```

- [ ] **Step 2: 构建验证**

```bash
dotnet build _src/MFAAvalonia.sln
```

预期:编译通过。

- [ ] **Step 3: 向用户汇报,征得许可后提交**

---

## Task 5: 导出分卷阈值

**Files:**
- Modify: `_src/MFAAvalonia/Helper/FileLogExporter.cs:17`

**Interfaces:**
- Produces: 分卷阈值 20 MiB,与切块阈值一致

- [ ] **Step 1: 修改常量**

```csharp
// 修改前
private const long MaxArchiveVolumeBytes = 24_500_000;
// 修改后:20 MiB,与 MaaLogRotator 切块阈值一致
private const long MaxArchiveVolumeBytes = 20_971_520;
```

- [ ] **Step 2: 构建验证**

```bash
dotnet build _src/MFAAvalonia.sln
```

预期:编译通过。

- [ ] **Step 3: 向用户汇报,征得许可后提交**

---

## Task 6: 发布与手动验证

**Files:**
- 无代码改动

- [ ] **Step 1: 发布并拷贝产物(按 CLAUDE.md 发布流程)**

```bash
dotnet publish _src/MFAAvalonia.Desktop -c Release
# 核心库(含 MaaLogRotator)
cp _src/MFAAvalonia/bin/Release/net10.0/MFAAvalonia.Core.dll runtimes/libs/
# 桌面宿主(注意:必须从项目自身输出目录拷贝,不能取 AnyCPU/Release 缓存)
cp _src/bin/AnyCPU/Release/publish/MATR.dll ./
cp _src/bin/AnyCPU/Release/publish/MATR.exe ./
```

- [ ] **Step 2: 运行期切块验证**

启动 MATR 并运行任务,持续观察:
1. `debug/maafw.log` 超过 20 MiB 后被切块为 `maafw.bak.{时间戳}.log`(轮询间隔 30 秒内)
2. 生成的 bak 文件大小均不超过 20 MiB
3. 相邻 bak 与新的 maafw.log 时间戳连续,无明显大段日志缺失
4. 任务运行不受影响,UI 无卡顿
5. 停止任务后,若日志超阈值,立即产生一次切块

- [ ] **Step 3: 导出分卷验证**

通过界面导出日志,确认:
1. 生成的 zip 分卷均不超过 20 MiB
2. 分卷内容完整可解压,日志与截图齐全

- [ ] **Step 4: 失败降级验证(可选)**

切块轮询期间用编辑器打开 `debug/maafw.log` 模拟占用,确认任务不受影响、日志无损坏、下一轮轮询恢复。

- [ ] **Step 5: 向用户汇报验证结果**

---

## 自审记录

- Spec 覆盖:切块组件(Task 2)、启动挂载(Task 3)、停止兜底(Task 4)、分卷阈值(Task 5)、前置验证(Task 1)、手动验证(Task 6)与设计文档六节一一对应;不动的部分(清理策略、Serilog、导出 UI)均无对应任务,符合预期
- 占位符扫描:无 TBD/TODO,所有步骤含完整代码
- 类型一致性:`SetOption_LogDir(string) → bool`、`TryRotateIfNeeded()`、`Start()` 在 Task 2 定义,Task 3/4 引用一致;`MaaProcessorManager.Instance.Current.MaaTasker` 与 MaaProcessor.cs:657 一致
