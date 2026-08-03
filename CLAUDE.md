# MATR 项目（刀剑乱舞自动化助手）

## 开发规范

- **git commit 必须由用户明确提出**，禁止在用户未要求的情况下自行提交。
- **禁止在任何 JSON 文件中使用 `target_offset`**，所有坐标偏移应在 `target` 数组内直接表达。
- **每个新增节点都必须确认是否需要 `on_error`**。仅当该节点已在主枢纽 `next` 扫描链中，或 `next` 列表中不含任何需要识别匹配的节点时，才无需设置。纯 `Click` 节点如果 `next` 指向了 OCR/TemplateMatch 等识别节点，其 `next` 匹配阶段可能超时，仍需要 `on_error`。
- **术语规范**：描述节点层级关系时统一使用英文 parent node、child node、sister node，禁止使用"父节点""子节点""兄弟节点"等中文亲属称谓。

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
