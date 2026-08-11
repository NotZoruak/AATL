# 卡死循环检测设计(模拟器画面冻结恢复)

## 背景与事故

2026-08-11 实际事故:模拟器(MuMu 6.0)连续运行约 27 小时后安卓系统层冻结,游戏画面停在固定画面,MATR「卡死重启」机制未触发,挂机死循环约 20 分钟直至手动停止。

### 根因

现有「卡死重启」的检测条件是**枢纽节点全部识别不命中后累计 timeout(120 秒)超时**。本次冻结画面恰好能命中识别节点 `E_FlowerIsGameIcon`(游戏图标,score=1.0),形成循环:

```
E_FlowerBattleHub → 识别列表命中 E_FlowerIsGameIcon → 执行点击
  → next 跳回 E_FlowerBattleHub(重新进入枢纽,timeout 重新计时)→ 再次命中 → 无限循环
```

识别命中→执行→跳回枢纽的循环使枢纽 timeout 每次重新计时,永不触发。日志证实 12:46-13:06 为上述循环,恢复流程一次未触发。

### 结论

现有机制只覆盖「全部识别不命中」的冻结形态,不覆盖「恰好命中某个识别节点」的冻结形态与「模拟器无响应(截图挂起)」形态。本设计补齐后两者。

### 实机验证补充(2026-08-11)

挂起 MuMuNxDevice 进程实测:截图请求挂起 → 识别循环卡死在截图上(maafw.log 停摆)→ **timeout 检查依赖识别循环执行,循环停则检查停,永不触发**;截图既不成功也不失败,ScreencapFailure 检测同样不计数;无回调,LoopDetector 无输入。三次形态覆盖现状:

| 形态 | 机制 | 现状 |
|---|---|---|
| 全识别不命中 | 枢纽 timeout | ✅ 有效(模拟器正常时) |
| 冻结但命中节点(循环) | LoopDetector | ✅ 已实现 |
| 模拟器无响应(截图挂起) | 无响应检测 | 本设计补充(见下) |

## 机制现状(不改动部分)

- 全局选项「卡死重启」(switch,默认 No):
  - 开启时禁用 12 个 `*_FallbackWait` 兜底节点,枢纽 timeout 覆盖为「卡死等待时间」(默认 120 秒)
  - 枢纽超时 → `on_error` → `*_RestartGame` → `RestartGameAction`
- `RestartGameAction`(custom action)恢复链路:
  - `mumu-cli.exe control --vmindex {n} restart`(MuMu 12+/6.0 均适用,已验证本机命令可用)
  - 失败回退旧版 `MuMuPlayer.exe` 方式
  - `WaitForAdbReady` adb 轮询等待就绪
  - `RunAdbCommand` 执行 `am force-stop` + `monkey` 重启游戏

## 方案:动作循环检测(LoopDetector)

### 检测

在 `MaaProcessor.HandleCallBack`(已注册的 `MaaTasker.Callback`)中监听 `Node.Action.Succeeded` 消息,统计连续动作循环:

- 提取动作键:`(节点名, 动作类型, 目标坐标)`。数据来源:回调 `details` JSON 的 `action_details.action`(如 Click/Swipe)与 `action_details.box`/`detail.point`(目标位置)
- 维护状态:上一次动作键 + 连续计数
- 当前动作键与上次相同 → 计数 +1;不同 → 计数清零
- 连续计数达到阈值(200,用户确认)→ 判定画面冻结 → 进入恢复流程

### 防误判

- 只统计 `Action.Succeeded`,识别失败/跳过不参与
- 正常挂机中同节点点击会被画面变化打断(画面一变,命中的节点或坐标即改变,计数清零);连续 200 次同坐标点击意味着画面冻结约 70-400 秒(按 350ms-2s 循环周期),是强冻结信号
- **二次确认**:达到 200 次后不立即恢复,继续统计,若再累计 50 次(循环仍未被打断)才真正触发;期间出现任何不同动作键则放弃本次判定并清零
- 检测仅在任务运行中生效,任务停止/空闲不计数

### 作用范围

跟随全局选项「卡死重启」:开关关闭时检测器不启动,保持现有语义不变。

### 参数

循环检测次数**硬编码 200**(用户确认,不做可配置子选项),后续如需调整改常量即可。

## 恢复流程(全自动)

检测判定后由 MATR 层编排,不依赖 pipeline 节点:

1. 停止当前任务(现有停止入口)
2. 模拟器重启:抽公共方法复用 `RestartGameAction` 逻辑(`mumu-cli` 重启 → 回退 → `WaitForAdbReady`)
3. 游戏启动:`am force-stop` + `monkey`(复用 `RunAdbCommand`)
4. 重新连接:`MaaProcessor.ReconnectAsync`(已有)
5. 重新启动任务队列:`TaskQueueViewModel.StartTask`(队列在内存中,重启模拟器不丢失;合战场 `repeat_count=-1` 失败自动续跑,挂机自动恢复)

### 与现有机制衔接

- pipeline 层零改动,`*_RestartGame` 节点与 RestartGameAction 保持原样(覆盖「全不命中」形态)
- LoopDetector 覆盖「命中循环」形态,两种形态互补

## 实现文件

| 文件 | 改动 |
|---|---|
| `_src/MFAAvalonia/Extensions/MaaFW/MaaProcessor.cs` | `HandleCallBack` 内接入 LoopDetector 统计;新增恢复流程编排入口(停止任务、重启模拟器、重连、重启队列的调用) |
| `_src/MFAAvalonia/Extensions/MaaFW/Custom/RestartGameAction.cs` | 将模拟器重启与游戏启动逻辑抽为可复用方法(供 MATR 层直接调用) |
| `_src/MFAAvalonia/ViewModels/Pages/TaskQueueViewModel.cs` | 恢复流程编排(停止→重启→重连→StartTask) |
| `docs/全局设置设计.md` | 补充说明(如有必要) |

## 测试计划

1. 实机复现冻结:任务运行中手动暂停 `MuMuNxDevice.exe` 进程(或断网),观察检测计数累积与触发
2. 验证恢复链路:触发后模拟器重启、游戏重启、重连、任务自动续跑全流程
3. 回归验证:正常挂机 30 分钟以上无误触发(计数不达阈值)
4. 边界:任务停止后计数清零;不同任务切换计数清零;二次确认窗口内循环打断不触发

## 无响应检测(挂起形态,2026-08-11 补充)

### 背景

模拟器无响应(如进程挂起、系统层僵死)时,截图请求挂起不返回:识别循环卡死、timeout 检查停、无回调——上述三种机制全部失效。实测挂起 MuMuNxDevice 3 分钟无任何恢复,早上真实故障(13:06 后)同样未自动恢复。

### 判定条件

```
任务运行中 + 连续 120 秒无任何 Maa 回调 + 当前不在 SmartWait 等待窗口 → 判定模拟器无响应 → 触发恢复(复用现有恢复编排)
```

- **为什么用"无回调"**:识别循环正常时每 500ms-2s 一轮必有回调(含识别失败);循环停摆 = 引擎卡死
- **为什么排除 SmartWait**:远征任务的 `SmartWaitAction` 分段 sleep 最长 `min(归队剩余, RefreshInterval=3600s)`(用户实测配置),期间零回调属合法静默。**仅远征任务注入 SmartWait**(用户确认),其他任务无长静默
- **阈值 120s**:非 SmartWait 场景最大合法静默 = post_delay 60s(E_WaitRefresh)+ wait_freezes 20s ≈ 80s,120s 留余量;SmartWait 窗口内不计时

### 实现

1. `SmartWaitTracker`:SmartWaitAction 等待开始时记录窗口 `[开始时间, 开始时间+waitSeconds]`(try/finally 保证停止/异常也清除),供检测排除合法静默
2. MaaProcessor 记录 `LastCallbackTime`(HandleCallBack 首行);TaskQueueViewModel 启动检查循环(10s 轮询):`任务运行中 && now - 最后回调 > 120s && !在SmartWait窗口` → 触发与 LoopDetector 相同的恢复流程
3. `RestartGameAction` 强制重启路径:CLI 重启超时且旧版 MuMuPlayer.exe 不可用时,`taskkill /F` 强杀 `MuMuNxDevice.exe`(无响应进程也能强杀)→ `mumu-cli control launch --vmindex {n}` 重启实例 → WaitForAdbReady
4. `RunAutoRecoverAsync` 开始时 `ResetLastCallbackTime()`(防止恢复流程自身耗时被误判再次触发,消除恢复风暴)
5. 复用现有恢复编排(停止任务 → RestartAndReloadGame → 重连 → 重启队列 → 复位)

### 恢复语义:整队重新开始

恢复流程的"重新启动任务队列"为**整队重新开始**(StopTask 清空队列,StartTask 按界面当前勾选全部重新入队),非从卡死位置续跑。对无限循环任务(合战场 repeat_count: -1)两语义等价;对有限次任务重启后重新计次;流水线为画面状态机,重复执行按游戏实际状态走,不会重复派遣已执行的远征。

### 覆盖验证

| 场景 | 结果 |
|---|---|
| 远征等待(SmartWait 窗口内) | 排除,不误判 |
| 挂起形态(SmartWait 结束后截图卡住) | 120s 触发恢复 |
| 挂起发生在 SmartWait 期间 | 倒计时结束后触发 |
| 循环形态 | LoopDetector(已实现) |
| 正常挂机 | 回调持续,不触发 |

## 版本

「卡死重启」机制修复,纯应用层改动,不涉及 interface.json 与资源版本,无需版本号变更。
