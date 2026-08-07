# 日志切块与打包优化设计

日期:2026-08-07

## 背景

MATR 的 MaaCore 日志(`<工作目录>/debug/maafw.log`)由 MaaFramework 原生层持续写入,每次写入附带时间戳、PID、TID、文件名、行号、函数名等完整元数据,内容冗余量大。MaaCore 运行期间不切块,仅在 MATR 启动时由 `AppPaths.CleanupOldDebugLogs`(App.axaml.cs:138)轮转为 `maafw.bak.{时间戳}.log`。一次数小时的长任务即可产生上百 MB 的单个日志文件,既占磁盘又难以打开排查。

导出功能(`FileLogExporter.CompressRecentLogs`)已具备压缩与分卷能力,分卷阈值为 24_500_000 字节(约 24.5 MB),用户希望收紧为 20 MiB,每卷不超过该上限。

## 目标

1. MaaCore 日志在运行期间按大小切块,单块(含 bak 文件)不超过 20 MiB
2. 导出 zip 分卷阈值改为 20 MiB,每卷不超过 20 MiB
3. 切块过程不影响任务运行,失败自动降级,不丢失日志内容

## 现状与关键事实

- `maafw.log` 由 MaaCore 写入 `<工作目录>/debug/`,运行期间持续追加,不切块
- MATR 启动时轮转一次(重命名加时间戳),保留策略:3 天内的 bak;debug 目录超 100 MB 时仅保留最新 2 条 bak 与最新 5 张 on_error 截图(AppPaths.cs:190)
- Windows 上正在被写入的日志文件无法直接 `File.Move`(MaaCore 以标准库流打开,句柄无 DELETE 共享),运行中直接移动必然失败
- MaaFramework 5.x 官方 API `MaaGlobalSetOption(MaaGlobalOption_LogDir, dir)` 会调用 `OptionMgr::set_log_dir` → `Logger::start_logging(dir)`,运行中切换目录后 MaaCore 关闭旧文件句柄、在新目录重开日志文件(OptionMgr.cpp:36-44)
- C# 侧已有 `tasker.Global.SetOption_LogDir()` 调用先例(MaaProcessor.cs:1411,截图 tasker)
- tasker 引用获取路径:`MaaProcessorManager.Instance.Current.MaaTasker`(MaaProcessor.cs:657)
- 后台轮询范式:参照 `AvaloniaMemoryCracker.Cracker()`(App.axaml.cs:148 启动,Task.Run + CancellationToken 循环)
- 任务停止入口:`TaskQueueViewModel.StopTask` → `Processor.Stop()`

## 设计

### 1. 常量

| 常量 | 值 | 位置 |
|---|---|---|
| 切块阈值 `MaxLogChunkBytes` | 20_971_520(20 MiB) | 新文件 `MaaLogRotator.cs` |
| 分卷阈值 `MaxArchiveVolumeBytes` | 20_971_520(原 24_500_000) | `FileLogExporter.cs:17` |
| 轮询间隔 | 30 秒 | `MaaLogRotator.cs` |

### 2. 新组件 MaaLogRotator(MFAAvalonia/Helper/MaaLogRotator.cs)

后台循环任务,每 30 秒检查一次:

检查条件(全部满足才切块):
- `debug/maafw.log` 存在且大小超过 20 MiB
- `MaaProcessorManager.Instance.Current.MaaTasker` 非空

切块操作序列(任一步失败即中止本轮,记录警告日志,不影响任务运行):
1. 在 `AppPaths.TempMaaFwDirectory` 下创建本次切块的独立临时目录
2. `tasker.Global.SetOption_LogDir(临时目录)` → MaaCore 关闭旧文件句柄,开始写临时目录中的新文件
3. `File.Move(debug/maafw.log → debug/maafw.bak.{yyyy.MM.dd-HH.mm.ss.fff}.log)`,命名沿用现有格式(AppPaths.cs:209)
4. 将临时目录中的过渡小文件(切换瞬间的日志)移动为另一个 `maafw.bak.{时间戳}.log`,保证日志不丢失
5. `tasker.Global.SetOption_LogDir(切回 debug 根目录)` → MaaCore 继续写新的小 `maafw.log`
6. 删除已清空的临时目录

生命周期:
- 启动:在 `App.axaml.cs` Initialize() 中与 `AvaloniaMemoryCracker` 并列创建并启动
- 退出:随应用退出终止,无需额外清理
- 使用 `IDisposable` + CancellationTokenSource 规范实现

### 3. 任务停止兜底

在任务停止流程末尾(`TaskQueueViewModel.StopTask` 的停止动作完成后)调用一次切块检查。此时日志写入已停止,切块最干净。复用轮询器内部的同一切块方法,不另写逻辑。

### 4. 打包阈值修改

`FileLogExporter.cs:17` 的 `MaxArchiveVolumeBytes` 从 24_500_000 改为 20_971_520。分卷机制、文件快照复制、体积估算逻辑全部保持不变。

已知限制(现状延续):单个文件压缩后超过 20 MiB 时无法拆分(zip 分卷不支持拆分单文件)。文本日志压缩率约 10:1,20 MiB 原文压缩后约 2 MiB,实际几乎不会触发。

### 5. 错误处理

- 轮询器所有文件操作与 API 调用均 try/catch,异常只记录 LoggerHelper.Warning,不中断任务
- `SetOption_LogDir` 返回 false(切换失败)时不执行移动,等待下一轮
- 移动失败时跳过该文件,其余步骤继续
- 临时目录清理失败仅记录,不阻塞后续轮次

### 6. 保留策略调整(验证阶段变更)

切块后日志块变小(20 MiB),原"debug 超 100 MB 仅保最新 2 条 bak"策略会快速清掉历史日志。验证阶段经用户确认调整为中等保留:

- debug 目录磁盘占用超 **500 MB** 时,仅保留最新 **10 条** `maafw.bak.*.log`(约 200 MB)
- on_error 截图清理条件同步改为超 500 MB 时保留最新 **50 张**(单张约 0.8 MB,占用有限)
- 3 天时间保留机制不变

### 7. 不动的部分

- GUI 日志(Serilog 已按 10 MB 滚动 + 保留 14 个文件)不变
- GUI 日志(Serilog 已按 10 MB 滚动 + 保留 14 个文件)不变
- 导出对话框 UI、`ExportLogPackageOptions`、分卷命名、`GetEligibleFiles` 收集逻辑全部不变

## 前置验证

正式实现前先运行验证程序:加载项目 `runtimes/libs/MaaFramework.Binding.dll` 与原生库,创建 tasker 并 `SetOption_LogDir` 写入日志,再切换 LogDir 到新目录,验证:
1. 旧文件句柄关闭后旧文件可被移动(成功标志)
2. 新目录正常产生新的日志文件
3. 切回原目录后日志继续写入

验证通过才进入正式实现;不通过则退回备选方案(仅任务停止时切块,运行中不切)。

## 测试

1. 前置验证程序(见上)
2. 手动验证:运行任务,观察 `maafw.log` 在约 20 MiB 处被切块,生成的 bak 文件均不超过 20 MiB,且日志内容连续(时间戳无大段缺失)
3. 导出验证:导出日志,确认分卷均不超过 20 MiB,内容完整
4. 异常验证:切块期间手动占用文件模拟失败,确认任务不受影响、下一轮恢复
