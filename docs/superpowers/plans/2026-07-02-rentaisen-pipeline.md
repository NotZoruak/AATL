# 联队战（陆联）实现计划

> **Goal:** 创建联队战任务流水线，注册到 GUI

**Architecture:** 新建 `resource/base/pipeline/联队战.json`（入口节点 `联队战`，前缀 `LR_`），在 `resource/interface.json` 注册任务和 4 个选项。需 `联队战.png` 模板图（已就位）。

---

### Task 1: 创建流水线 JSON

**Files:**
- Create: `resource/base/pipeline/联队战.json`

从地下城 `Underground.json` 提取仅改前缀节点，新增联队战特有节点，组装完整流水线。

### Task 2: 注册任务和选项到 interface.json

**Files:**
- Modify: `resource/interface.json`

添加联队战任务条目 + 4 个选项定义（选择难度、补充门票、选择部队、换队长）。

### Task 3: 编译验证并打包

**Files:**
- (验证 pipeline JSON 语法、interface.json 语法、dotnet build)
