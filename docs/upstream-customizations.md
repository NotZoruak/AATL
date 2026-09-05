# MFAAvalonia 定制层总览

本文件是 `upstream-customizations.json` 的人读说明。升级时先以目标上游版本建立桌面 GUI 基线，再按条目重放 MATR 行为；不能通过整文件覆盖旧源码来伪装升级。

## 维护规则

- 每次上游升级前，导出当前工作区补丁与来源清单。
- 每个与上游不同的源码文件，必须能关联一个台账 ID 或一条明确的桌面排除记录。
- `drop` 只表示已有证据表明上游实现等价吸收；它不表示未经审查地删除 MATR 功能。
- 每次升级结束后，将保留项的 `last_verified_upstream` 更新为实际验证版本，并写入升级报告。

## 定制项

### `task-loader.global-options-hidden`

全局选项不应显示为普通任务项，但仍必须能在设置界面配置。升级时需适配上游模板和预设模型。

设置区固定为“常规 / 全局”两页，不保留上游“高级”页。全局页需要保留 `global_option`，以及刀解/合成许可名单和刀剑掉落播报入口；名单编辑在设置区子页打开。任务勾选框必须绑定 `IsCheckBoxEnabled`，不能让资源设置项或运行中的实例参与勾选。

任务列表顶部的全选与取消全选必须合并为一个 `ToggleSelectAllCommand` 按钮：存在未选任务时点击全选；所有可选任务均已选中时再次点击全不选。不得恢复为上游的两个独立按钮。

任务选项的下级选项有两种明确模式：默认模式显示小齿轮，点击后在设置区子页编辑；资源定义设置 `inline_sub_options: true` 时，则直接在母选项下展开。`MaaInterfaceOption` 必须保留该 JSON 字段及合并规则，`TaskOptionGenerator` 必须据此选择呈现方式。checkbox 类型必须保持方框加文字，而不是上游的 ToggleButton 卡片样式。

下拉框搜索必须由资源中的 `is_searchable` 单项控制：`MaaInterfaceOption` 保留该 JSON 字段，`TaskOptionGenerator` 将其传给 ComboBox 搜索行为。不得把所有下拉框强制设置为可搜索。

### `task-formation-config.normal-task`

“自定编队”必须作为 `assets/interface.json` 中的普通任务注册，入口为 `FormationConfig`，通过任务专属的 `FC_选择预设` 设置选择编队预设；不得重新加入上游的特殊任务列表。

预设选择页支持新增、编辑、复制、粘贴、删除和切换当前预设。运行前由 `MaaProcessor` 将所选预设转换为 `FormationConfigAction` 参数与编队 pipeline 覆盖；每日任务的“预设部队”也复用同一套预设及装备编辑页。升级时不得仅保留 `FormationConfig.json`，否则任务虽有 pipeline 却无法选择预设或注入参数。

### `ui.matr-tools-and-layout`

保留 MATR 的业务工具入口、中文界面布局及任务区宽度。桌面 UI 以新上游根窗口为基础；`Views/Mobile/RootViewContent` 是桌面和移动端共用的根级导航壳，必须随桌面端迁入。仅独立的移动端宿主与页面不在本次范围。

桌面图标使用 MATR 的 `Assets/logo.ico`，不可被上游默认图标替换。

### `services.update-data-scheduling`

更新数据任务按间隔执行，状态存入实例配置。需验证间隔跳过与实例重新加载。

### `work-records.name-dialog-registration`

工作记录的保存、另存与重命名均通过 `WorkRecordNameDialogViewModel` 输入名称。该 ViewModel 必须在 `App.ConfigureViews` 注册为 `WorkRecordNameDialogView`；上游升级时即使两个源文件仍存在，也不得遗漏这条映射，否则保存会提示找不到对应视图。

### `queue.resume-and-continue-on-error`

保留失败继续、用户停止、断点续跑、轮次统计和结束后操作边界。必须基于新上游 `RunResult`/取消模型重放，并覆盖关键队列路径。

任务队列的外层异步执行必须等待 `ExecuteTasks` 完整返回后再进入停止流程。调用 `TaskManager.RunTaskAsync` 时应使用 `Func<Task>` 重载，不能把异步 lambda 绑定到 `Action` 重载，否则会在第一个 `await` 后提前执行停止逻辑，并将正常完成的任务记录为“手动停止”。

任务队列需要在普通任务之间自动插入 `GoHome`，确保下一个游戏任务从本丸开始；最后一个任务后不插入。MFAA 提供的特殊任务通过 `Entry` 标识（如 `CountdownAction`、`WebhookAction`）识别，特殊任务前不得插入回本丸。特殊任务集合由任务队列策略与任务添加界面共用，升级时不得恢复为无条件插入。

### `recovery.game-and-emulator-restart`

保留 MATR 对 MFAAvalonia 卡死恢复动作的二次开发。`RestartGameAction` 必须从当前 ADB 配置读取目标应用包名，优先通过 `cmd package resolve-activity` 解析实际启动 Activity，再使用 `am start -n` 启动，不能依赖部分模拟器缺失的 `monkey` 命令。

恢复流程必须区分模拟器重启失败与游戏启动失败：模拟器重启失败时记录错误并让恢复动作失败；模拟器已恢复但游戏启动失败时记录警告，并将控制权交回任务 pipeline，继续尝试游戏图标、登录和主枢纽流程。升级 MFAAvalonia 的自定义动作注册、ADB 配置读取或任务错误处理时，必须保留该行为。

### `runtime.resource-path-and-packaging`

保留资源大小写兼容、桌面发布结构、图标与 `libloader` 启动钩子；不恢复 Python agent。验证完整包资源加载、Windows/macOS 发布及 agent 排除。

Windows 发布目录可能同时在 `runtimes/libs` 与根级 `libs` 放置托管依赖。发布包统一使用 `runtimes/libs`，并且 `MATR.runtimeconfig.json` 的 `NetBeautyLibsDir` 与 `SubdirectoriesToProbe` 必须都指向该目录；打包脚本仅在输入目录存在根级 `libs` 时合并其内容，但必须随后以 `runtimes/libs` 的本次发布产物覆盖同名文件，防止残留的旧核心程序集进入压缩包。

桌面项目的 `AssemblyName` 与 `OutputName` 必须保持为 `MATR`。上游默认的 `MFAAvalonia` 输出名会使 `pack_win.ps1` 误用开发根目录残留的旧 `MATR.exe`，从而将旧核心程序集打入测试包；Windows 打包必须先 `dotnet publish`，将宿主文件和 `runtimes` 同步到开发根目录，再以根目录为输入执行 `pack_win.ps1`。

MATR 的资源包固定在 `assets/`：`AppPaths.InterfaceJsonPath`、`AppPaths.InterfaceJsoncPath` 和 `AppPaths.ResourceDirectory` 必须分别解析到 `assets/interface.json`、`assets/interface.jsonc` 与 `assets/resource`。上游若改回程序根目录布局，会触发默认资源兜底并显示空任务列表。

`MaaProcessor.ProjectDir` 必须解析为 `interface.json` 所在目录（MATR 即 `assets/`），并且所有 resource 路径均以它替换 `{PROJECT_DIR}`。`MaaProcessor.CheckInterface` 的默认资源路径必须为 `{PROJECT_DIR}/resource/base`；两项必须配套，最终解析到 `assets/resource/base`，不得在程序根目录创建不需要的 `resource/base/pipeline/sample.json`。

### `privacy.telemetry-disabled`

MATR 的 `TelemetryService` 是不采集、不保存、不发送信息的兼容入口。关于页面不得显示上游“帮助改进软件”开关；它会默认显示开启，却没有实际效果并误导用户。升级时保留服务兼容入口以避免上游调用点失效，但移除该无效 UI。

导出日志成功提示仍应提供人工反馈入口：`FileLogExporter` 调用 `ToastHelper.SuccessWithSurvey`，由用户自行点击“去反馈bug”打开问卷链接。该入口不采集或发送任何遥测数据。
