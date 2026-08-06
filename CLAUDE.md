# MATR 项目（刀剑乱舞自动化助手）

## 项目结构

```
MATR/
├── assets/
│   ├── interface.json               # 项目入口配置（资源版本号、任务定义、pipeline 引用）
│   └── resource/                    # MaaFW 资源包
│       ├── base/
│       │   ├── pipeline/            #   Pipeline 流程定义（JSON）
│       │   ├── custom/              #   自定义动作（C# 脚本，每个文件一个动作类）
│       │   ├── image/               #   模板匹配图片（1280×720 基准）
│       │   └── model/               #   OCR 模型文件
│       ├── logo/                    #   启动 logo 资源
│       ├── silhouette/              #   剪影识别样板图片
│       └── announcement/            #   版本更新公告（Markdown）
├── _src/                        # C# 源代码（Avalonia 桌面应用）
│   ├── MFAAvalonia/             #   核心库：Models、ViewModels、Views、Services、Controls 等
│   ├── MFAAvalonia.Desktop/     #   桌面宿主项目（MATR.exe 入口）
│   └── MFAAvalonia.Android/     #   Android 宿主项目
├── docs/                        # 项目文档（任务设计、使用规范、开发日志等）
├── tools/                       # 构建/发布脚本（clean_build.ps1、compress_json.py、pack.ps1）
├── runtimes/                    # .NET 原生运行时库（多平台多架构，本地分发资源，不随 git 提交）
└── .github/                     # Issue 模板
```

> 以下目录由程序运行时自动生成，已在 `.gitignore` 中排除：`config/`、`debug/`、`logs/`、`temp/`、`backup/`、`libs/`、`plugins/`。

### 上游源码自定义修改

`_src/` 基于 [MFAAvalonia](https://github.com/SweetSmellFox/MFAAvalonia) 二次开发，以下为 MATR 对上游的修改：

| 修改 | 涉及文件 | 说明 |
|---|---|---|
| 移除 `agent/` 目录 | `AppPaths.cs`、`VersionChecker.cs`、`PendingUpdateDeletionHelper.cs` | MATR 不使用 Python agent，删除相关路径创建和更新逻辑 |
| 启动时自动生成流水线坐标 | `Program.cs` | 根据用户设置的屏幕分辨率，启动时调用 `pipeline_gen.py` 动态计算坐标 |
| 全局选项不显示为任务项 | `TaskLoader.cs` | 全局选项通过设置面板直接控制，不展示在任务列表中 |
| 替换 EXE 图标 | `MFAAvalonia.Desktop/` | 使用 MATR 自定义图标替代上游默认图标 |
| 左侧栏增加小工具 | `_src/MFAAvalonia/` | 在界面左侧栏集成了剪影识别、开发日志等实用工具入口 |
| 调整任务栏宽度 | `_src/MFAAvalonia/` | 修改任务列表侧栏宽度以适配中文显示和操作习惯 |

> 升级 MFAAvalonia 上游版本时，需确认以上修改是否受影响。

### 关键目录说明

| 目录 | 用途 | 何时修改 |
|---|---|---|
| `resource/base/pipeline/` | Pipeline 流程定义，每个 JSON 对应一个任务 | 新增/修改任务流程 |
| `resource/base/custom/` | 自定义动作 C# 脚本 | 需要非标准操作时编写新动作 |
| `resource/base/image/` | 模板匹配图片，基于 1280×720 | 新增识别节点时添加模板图 |
| `resource/base/model/` | PaddleOCR 模型 | 需要 OCR 识别时放置模型 |
| `_src/MFAAvalonia/` | 核心 C# 代码 | 修改程序功能、UI、配置 |

### 自定义动作

当前自定义动作位于 `resource/base/custom/`，共 7 个：

| 文件 | 用途 |
|---|---|
| `TeamSwitchAction.cs` | 队伍切换 |
| `CaptainDamageAction.cs` | 队长伤害处理 |
| `DamageLogAction.cs` | 伤害日志记录 |
| `DungeonFloorSelectAction.cs` | 地下城楼层选择 |
| `ExpeditionMapSelectAction.cs` | 远征地图选择 |
| `DispatchLogAction.cs` | 内番日志记录 |
| `StopOnDamageAction.cs` | 遇伤害停止 |

新增自定义动作时，参考已有文件的命名和结构，一个文件对应一个动作类。

### Pipeline 任务清单

| 文件 | 任务 |
|---|---|
| `Sortie.json` | 出阵（合战场） |
| `Expedition.json` | 远征 |
| `Underground.json` | 地下城（大阪城） |
| `Disassemble.json` | 刀装解体 |
| `FlowerBrush.json` | 刷花 |
| `GoHome.json` | 一键回城 |
| `LRentaisen.json` | 演练 |
| `Mix.json` | 习合 |
| `TacticalTraining.json` | 战术强化训练 |

## AI 响应指引

当用户提出以下请求时，AI 应遵循对应的默认做法：

| 用户要求 | AI 默认做法 |
|---|---|
| "修复不稳定节点" | 添加中间识别节点或调整识别阈值/区域 |
| "失败时重试" | 分析根因（哪个节点、哪个识别不匹配）并修复节点，绝不盲目添加重试 |
| "写一个 pipeline" | 向用户索要截图、ROI 和界面切换信息后再写，不凭空编造坐标 |
| "写一个自定义动作" | 遵循 `resource/base/custom/` 现有文件的命名和结构，一个文件一个类 |

## 编码风格

### C#（_src/ 下）

- .NET 10.0，C# 14，Nullable 启用
- 文件级命名空间声明（`namespace MFAAvalonia.Helper;`）
- 4 空格缩进，PascalCase 公共成员，`_camelCase` 私有字段
- 日志通过 `LoggerHelper` 输出，避免 `Console.WriteLine`

### JSON（Pipeline / 资源配置）

- 4 空格缩进
- 禁止使用 `target_offset`，所有坐标偏移在 `target` 数组内直接表达
- 每个节点必须设置 `on_error`
- 所有坐标、ROI、模板图片基于 **1280×720** 基准分辨率
- 描述节点层级关系时使用英文 parent node、child node、sibling node，禁止使用"父节点""子节点""兄弟节点"等中文亲属称谓
- 资源路径统一使用正斜杠 `/`

### 资源文件编码

- `.ps1` 文件：UTF-8 with BOM
- 其他所有源文件（`.cs`、`.json`、`.md` 等）：UTF-8 without BOM

## 构建与常用命令

| 命令 | 用途 |
|---|---|
| `dotnet build _src/MFAAvalonia.sln` | 编译整个解决方案 |
| `dotnet publish _src/MFAAvalonia.Desktop` | 发布桌面版本 |
| `pwsh tools/clean_build.ps1` | 清理构建输出 |
| `python tools/compress_json.py` | 压缩 JSON 文件 |
| `pwsh tools/pack.ps1` | 打包发布 |

## Commit 规范

遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/) v1.0.0：

| 类型 | 使用场景 |
|---|---|
| `feat` | 新功能（任务、节点、识别逻辑） |
| `fix` | Bug 修复 |
| `perf` | 性能优化 |
| `refactor` | 代码重构（非功能、非修复） |
| `docs` | 仅文档变更 |
| `style` | 格式化、空格调整（无语义变更） |
| `chore` | 依赖更新、构建脚本、维护杂项 |

> **AI 不得自行执行 `git commit` 或 `git push`**，除非用户明确要求提交。

## 分支策略

- **`main`**：稳定发布分支。仅允许以下情况直接提交：
  - 小范围修复（文档修正、单节点调整、配置更新）
  - 紧急 bug 修复
- **`develop`**：日常开发分支。所有新功能、多节点流程改动、需要测试的新逻辑，都应在 `develop` 上开发，验证通过后合并到 `main`
- 复杂功能可基于 `develop` 创建 `feat/<功能名>` 分支，完成后合并回 `develop`

## 审核清单

修改代码或 pipeline 时需确认：

- [ ] JSON 字段符合 MaaFW 协议，无拼写错误或不支持的属性
- [ ] 无 `target_offset`，所有坐标在 `target` 内直接表达
- [ ] 每个节点都设置了 `on_error`
- [ ] `next` 列表覆盖了所有可能的后续界面，确保首个识别周期就能命中正确节点
- [ ] 坐标、ROI、模板图片基于 1280×720 基准
- [ ] 新增自定义动作已放入 `resource/base/custom/`，文件名即动作类名
- [ ] Pipeline、interface.json、资源文件保持一致
- [ ] 异常中断（弹窗、意外对话框）有处理路径

## 版本号规则（SemVer 2.0.0）

全部版本号遵循[语义化版本](https://semver.org/lang/zh-CN/) `MAJOR.MINOR.PATCH` 格式。

### MFAAvalonia 程序本体

- 版本号在 `_src/MFAAvalonia/MFAAvalonia.csproj` 的 `ApplicationVersion` 和 `_src/MFAAvalonia/ViewModels/Windows/RootViewModel.cs` 的 `Version` 属性，**两处须保持同步**
- 程序本体更新频率低，发版时手动修改即可

### 资源版本

- 版本号写在资源包根目录 `interface.json` 的 `Version` 字段
- 递增规则：

| 递增位 | 触发条件 | 示例 |
|---|---|---|
| **修订号 PATCH** | 修 bug、微调识别阈值/区域/时序 | `1.2.3 → 1.2.4` |
| **次版本号 MINOR** | 新增节点/任务/识别逻辑，调整任务结构；向下兼容 | `1.2.3 → 1.3.0` |
| **主版本号 MAJOR** | 整个流程推翻重来的大规模重构（极少触发） | `1.2.3 → 2.0.0` |

- 日常的删改节点名、重组织任务是**次版本号**级别，不触发主版本号
- 递增次版本号时修订号归零；递增主版本号时次版本号和修订号均归零
- 资源处于 `0.y.z` 阶段视为开发中，稳定后发布 `1.0.0`

## 发布流程

`dotnet publish` 后必须手动拷贝文件才能运行：

```
# 核心库（含 TaskOptionGenerator、TaskQueueView 等）
cp _src/MFAAvalonia/bin/Release/net10.0/MFAAvalonia.Core.dll runtimes/libs/

# 桌面宿主
cp _src/bin/AnyCPU/Release/publish/MATR.dll ./
cp _src/bin/AnyCPU/Release/publish/MATR.exe ./
```

> ⚠️ `_src/bin/AnyCPU/Release/MFAAvalonia.Core.dll` 是桌面项目拷贝的旧缓存，文件大小和 `_src/MFAAvalonia/bin/Release/net10.0/` 不同，**必须从项目自身输出目录拷贝**。
