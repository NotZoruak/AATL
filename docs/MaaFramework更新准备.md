# MaaFramework 更新准备

## 当前版本

| 包 | 当前 | 最新 |
|---|------|------|
| `Maa.Framework`（C# 绑定） | 5.8.0 | 5.12.2 |
| `Maa.Framework.Runtimes`（原生库） | 5.10.2 | 5.12.2 |

跨 v5.10.2 → v5.12.2（v5.11.2, v5.12.1, v5.12.2 三个版本）

## 各版本变更分析

### v5.11.2（2026-07-11）

| 变更 | 类型 | MATR 影响 |
|------|------|-----------|
| Win32 interception input | 新功能 | **可能有用** — 当前用 SendMessage，intercept 模式更底层更可靠 |
| AdbControlUnit 兼容 vivo/新 Android Viewport | Bug 修复 | 间接有益，提升 ADB 截图兼容性 |
| MuMu v6 设备查找适配 | Bug 修复 | **直接相关** — MuMu 12 更新后出现过识别断裂，此修复可能解决 |
| KWin 控制器（Python 绑定） | 新功能 | 无关 — Linux 桌面环境 |
| PI v2.7 pretask / v2.8 setting,hotkey,import | 协议扩展 | 可关注 — pretask 可用于优化任务前后逻辑 |

### v5.12.1（2026-07-13）

| 变更 | 类型 | MATR 影响 |
|------|------|-----------|
| Win32 interception input 支持 | 新功能 | 同上（v5.11.2 引入，v5.12.1 完善） |
| **MuMu 模拟器搜索和连接错误修复** | Bug 修复 | **重点关注** — 你之前遇到的 MuMu 12 更新后识别为通用 emulator-5554 问题，此修复在框架层面解决 |
| MaaPiCli import-only interface.json | Bug 修复 | 无关 — CLI 工具 |

### v5.12.2（2026-07-19）

| 变更 | 类型 | MATR 影响 |
|------|------|-----------|
| **Batch OCR 收集时名称去重移除，修复内联子识别缓存冲突** | Bug 修复 | **直接相关** — 中枢批量 OCR（`S_DetectWhereAmI` 等）可能受此缓存冲突影响，导致偶发误匹配 |
| Agent 套接字析构死锁 | Bug 修复 | 间接 — Agent 模式稳定性 |
| Touchup 窗口未变化时减少 send_activate_message | Bug 修复 | 无关 — 输入优化 |
| 应用宝兼容 | Bug 修复 | 无关 |

## 更新状态

**2026-07-22 已更新至 v5.12.2**：GitHub Releases 的 `MAA-win-x86_64-v5.12.2.zip` 含编译好的原生 DLL，直接替换 `runtimes/win-x64/native/` 下同名文件。C# 绑定层保持 NuGet `Maa.Framework 5.8.0`，编译验证 ABI 兼容（0 错误）。

**替换清单**（16 个文件）：`MaaFramework.dll`、`MaaToolkit.dll`、`MaaAdbControlUnit.dll`、`MaaWin32ControlUnit.dll`、`MaaCustomControlUnit.dll`、`MaaGamepadControlUnit.dll`、`MaaRecordControlUnit.dll`、`MaaReplayControlUnit.dll`、`MaaUtils.dll`、`MaaAgentClient.dll`、`MaaAgentServer.dll`、`DirectML.dll`、`ViGEmClient.dll`、`fastdeploy_ppocr_maa.dll`、`onnxruntime_maa.dll`、`opencv_world4_maa.dll`、`MaaNode.node`、`MaaNodeServer.node`

## 更新步骤（再次更新时使用）

1. 修改 `_src/MFAAvalonia/MFAAvalonia.csproj`：
   - `Maa.Framework` 5.8.0 → 5.12.2
   - `Maa.Framework.Runtimes` 5.10.2 → 5.12.2
2. `dotnet restore` / `dotnet publish`
3. 确认 `runtimes/` 下原生库更新正确
4. 测试 MuMu 连接 + 中枢 OCR + 行军检测

## 风险评估

- **低风险**：运行时库版本不会变 API，C# 绑定层 5.8→5.12 可能有小 API 变动但大概率兼容
- **MuMu 修复**：v5.12.1 的模拟器搜索修复可能改变 ADB 设备发现逻辑，需验证现有 MuMu 配置是否仍正常
- **Batch OCR**：v5.12.2 移除名称去重，需关注中枢批量识别是否有行为变化
