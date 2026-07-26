# MATR 项目（刀剑乱舞自动化助手）

## 开发规范

- **git commit 必须由用户明确提出**，禁止在用户未要求的情况下自行提交。
- **禁止在任何 JSON 文件中使用 `target_offset`**，所有坐标偏移应在 `target` 数组内直接表达。
- **每个新增节点都必须确认是否需要 `on_error`**。仅当该节点已在主枢纽 `next` 扫描链中，或 `next` 列表中不含任何需要识别匹配的节点时，才无需设置。纯 `Click` 节点如果 `next` 指向了 OCR/TemplateMatch 等识别节点，其 `next` 匹配阶段可能超时，仍需要 `on_error`。
- **术语规范**：描述节点层级关系时统一使用英文 parent node、child node、sister node，禁止使用"父节点""子节点""兄弟节点"等中文亲属称谓。

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
