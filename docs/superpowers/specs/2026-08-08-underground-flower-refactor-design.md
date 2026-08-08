# 地下城刷花状态机与流水线优化设计

日期: 2026-08-08

## 背景

地下城（Underground.json，156 个 node）已有疲劳检测（U_FatigueDetect，行军时模板检测"已疲劳"→回本丸）与刀装检查/补充链（U_EqCheck 18 格 ColorMatch、U_CheckEquipmentPopup 补充链），但**刷花功能未实现**：interface.json 的"U_疲劳处理"选项"刷花"case 与"停止"完全相同（都只启用 U_FatigueDetect，处理动作都是回本丸）。

合战场已建立完整的刷花状态机（SF_ = Sortie Flower，57 个 node）：部队选择页面 OCR 首位疲劳值（S_FatigueCheck threshold 30）→ 低于阈值 → SF_Hub 菜单链 → 合战场 1-1 出阵刷花 → 疲劳恢复满（SF_CheckFatigue threshold 100 reversed）→ 回主流程。

用户目标：为地下城建立对应的 **UF_ 状态机**（Underground Flower），参照 SF 完整复刻，刷花地点为合战场 1-1，不跨任务引用。

## 目标

1. 新增 UF_ 状态机（57 个 node，完整对应 SF_），实现地下城刷花
2. interface.json"U_疲劳处理-刷花"case 接入 UF 状态机
3. 地下城全 pipeline 冗余结构优化（孤儿链清理、冗余 node 删除）
4. 硬编码等待迁移 wait_freezes（参照合战场已完成模式）
5. 刀装破坏处理验证（补充链/保护链已存在，确认可用并纳入优化）

## 现状与关键事实

### 地下城主流程（U_）

- 主枢纽 U_DetectWhereAmI（28 分支，timeout 120000）
- 行军检查路由 U_CheckBeforeMarch：[U_IsMarchingDamage, U_FatigueDetect, U_EqCheck, U_CheckRoundComplete, U_ClickMarching]
- 部队选择 U_IsTeamSelect → U_CheckEquipmentPopup / U_StopOnEquipmentPopup / U_ClickTeam → U_CaptainHub → [U_ClickCaptainSlot, U_IsPreSortieConfirm]
- U_DragCaptain（Custom DragCaptainAction，OFF）存在，由"U_换队长"选项启用，next 指向 U_IsPreSortieConfirm
- delay 分布：pre_delay 100 ×62、post_delay 800 ×5、300 ×4、2000/3000 等

### 合战场刷花状态机（SF_，57 node）作为模板

关键链路：

```
SF_Hub → SF_ClickMenu（部队选择页点"目"菜单）→ SF_PostClickMenuHub → SF_IsMenuDirectory
  → SF_ClickSortieInMenu（菜单出阵）→ SF_DetectWhereAmI 枢纽
  → SF_IsEraSelect → SF_ClickFirstEra → SF_ConfirmEra（时代一）
  → SF_IsRegionSelect → SF_ClickRegion1_1（地域 1-1）
  → SF_IsTeamSelect → SF_CheckEquipmentPopup / SF_ClickTeamN → SF_DissolveHub
  → SF_DissolveCheck / SF_CheckFatigue（FatigueCheckAction threshold 100 reversed）
  → SF_ClickSortieNow（<100 未满出阵）→ SF_IsMarching（行军）→ SF_IsFormationSelect
  → SF_IsBattleResult_Exp/Title → SF_IsSwordDrop（战斗掉落循环）
  → 疲劳满（=100）→ SF_UseRecord_Step1 → SF_UseRecord_Step2_Rec1-5 → SF_UseRecord_Step3/4
  → SF_NavigateBack → S_DetectWhereAmI（回主流程）
```

附属：SF_CheckEquipmentPopup → SF_EqRefill_Step1-4（装备补充）、闪退恢复全套（SF_IsGameIcon/IsLoginButton/IsGameUpdatePopup/IsInGameUpdatePopup/IsInternalReport/IsAnnouncementPopup/IsTrainingLetter/IsLoginReward/LoginRewardClick2/3）、SF_CheckHomeBrightness、SF_IsHome、SF_FallbackWait、SF_RestartGame

### 刀装破坏处理比对结论

- 补充链：地下城与合战场完全同构（CheckEquipmentPopup → PreCheckTroopRecord → PreCheckUseRecord → PreCheckClickRecord1-5 → PreCheckUseRecordBtn → PreConfirmSupply → IsPreSortieConfirm），已可用
- 保护链：U_EqCheck → 18 个 U_EqCheckC_R*C*（ColorMatch，ROI [591,184,1,1]，点 [763,446] 回本丸），全部启用；合战场 S_EqCheckC_* 同构且全部启用（历史"颜色匹配 node 未启用"问题现版本不存在）
- SF 状态机本身不含刀装保护（S_EqCheck 在主流程 S_ 系列）；UF 对应不含 UF_EqCheck

## 设计

### 1. UF_ 状态机（新增 57 个 node，完整对应 SF_）

| SF_ | UF_ |
|---|---|
| SF_DetectWhereAmI | UF_DetectWhereAmI（枢纽） |
| SF_HasExpeditionReturn_Exp / Title | UF_HasExpeditionReturn_Exp / Title |
| SF_CheckHomeBrightness | UF_CheckHomeBrightness |
| SF_IsEraSelect / SF_ClickFirstEra / SF_ConfirmEra | UF_IsEraSelect / UF_ClickFirstEra / UF_ConfirmEra |
| SF_IsRegionSelect / SF_ClickRegion1_1 | UF_IsRegionSelect / UF_ClickRegion1_1 |
| SF_IsSortieActivity | UF_IsSortieActivity |
| SF_IsTeamSelect / SF_CheckEquipmentPopup / SF_ClickTeamN | UF_IsTeamSelect / UF_CheckEquipmentPopup / UF_ClickTeamN |
| SF_DissolveHub / SF_DissolveCheck | UF_DissolveHub / UF_DissolveCheck |
| SF_CheckFatigue | UF_CheckFatigue（FatigueCheckAction threshold 100 reversed） |
| SF_ClickSortieNow | UF_ClickSortieNow |
| SF_IsMarching / SF_IsFormationSelect / SF_IsBattleResult_Exp / SF_IsBattleResult_Title / SF_IsSwordDrop | UF_IsMarching / UF_IsFormationSelect / UF_IsBattleResult_Exp / UF_IsBattleResult_Title / UF_IsSwordDrop |
| SF_Hub / SF_ClickMenu / SF_PostClickMenuHub / SF_IsMenuDirectory / SF_ClickSortieInMenu | UF_Hub / UF_ClickMenu / UF_PostClickMenuHub / UF_IsMenuDirectory / UF_ClickSortieInMenu |
| SF_UseRecord_Step1 / Step2_Rec1-5 / Step3 / Step4 | UF_UseRecord_Step1 / UF_UseRecord_Step2_Rec1-5 / UF_UseRecord_Step3 / UF_UseRecord_Step4 |
| SF_EqRefill_Step1 / Step2_Rec1-5 / Step3 / Step4 | UF_EqRefill_Step1 / UF_EqRefill_Step2_Rec1-5 / UF_EqRefill_Step3 / UF_EqRefill_Step4 |
| SF_IsAnnouncementPopup / SF_IsTrainingLetter / SF_IsLoginReward / SF_LoginRewardClick2 / SF_LoginRewardClick3 | UF_IsAnnouncementPopup / UF_IsTrainingLetter / UF_IsLoginReward / UF_LoginRewardClick2 / UF_LoginRewardClick3 |
| SF_IsGameIcon / SF_IsLoginButton / SF_IsGameUpdatePopup / SF_IsInGameUpdatePopup / SF_IsInternalReport | UF_IsGameIcon / UF_IsLoginButton / UF_IsGameUpdatePopup / UF_IsInGameUpdatePopup / UF_IsInternalReport |
| SF_IsHome / SF_NavigateBack / SF_FallbackWait / SF_RestartGame | UF_IsHome / UF_NavigateBack / UF_FallbackWait / UF_RestartGame |

识别参数（ROI、模板、颜色、点击坐标）与 SF_ 完全一致（合战场 1-1 画面），仅 node 名前缀替换。delay 字段：SF_ 系列已完成 wait_freezes 迁移（2026-08-08 合战场批次），UF_ 从 SF_ 复制时直接沿用对应 freeze 配置（全屏 100ms / 点击区域 100ms / target 场景值）；SF_ 中未迁移的 delay 按合战场模式处理（pre 清除、过渡等待迁移 freeze）。

### 2. 入口链（地下城主流程接入 UF）

- 新增 `U_FatigueCheck`（Custom FatigueCheckAction，mode check_first，threshold 30，与 S_FatigueCheck 一致）
- `U_DragCaptain` 的 next 由 [U_IsPreSortieConfirm] 改为 [U_FatigueCheck]
- `U_FatigueCheck`：ok（疲劳 ≥30）→ next [U_IsPreSortieConfirm]（继续出阵）；!ok（<30）→ on_error [UF_Hub]
- UF_Hub → UF_ClickMenu（部队选择页点"目"）→ 导航链 → 合战场 1-1 → 刷花循环

### 3. 出口链

疲劳满（UF_CheckFatigue =100）→ on_error [UF_UseRecord_Step1] → UF_UseRecord_Step2_Rec1-5 → UF_UseRecord_Step3/4 → UF_NavigateBack → U_DetectWhereAmI（地下城主枢纽，识别地下城活动页导航回地下城继续）

### 4. interface.json 选项接入

"U_疲劳处理-刷花"case override 更新为（参照 S_疲劳处理-刷花）：

```json
{
  "U_FatigueCheck": {"enabled": true},
  "U_FatigueDetect": {"enabled": true},
  "UF_Hub": {"enabled": true}
}
```

"U_换队长"case 启用 U_DragCaptain（现有配置确认/补充），使其 next 指向 U_FatigueCheck。

### 5. 冗余优化与 delay→freeze 迁移（地下城全 pipeline）

参照合战场已完成模式：

- 全部 pre_delay 100 清除（点击/DoNothing 前等待无收益，由识别轮询兜底）
- 过渡等待 post_delay 迁移 post_wait_freezes：全屏统一 time 100ms、点击区域一致的 100ms、分离区域按场景 100-200ms
- 有意保留：U_WaitRefresh 类（若有）、枢纽 timeout 120000
- 孤儿链/冗余 node 清理：全文件引用扫描，删除无外部入口的闭环链
- 迁移后全文件扫描确认无残留 delay（有意保留除外）

### 6. 刀装破坏处理

不新建。补充链（U_CheckEquipmentPopup 链）与保护链（U_EqCheck + 18 格 ColorMatch）验证可用，纳入 delay→freeze 优化范围。

## 验证方式

1. JSON 语义校验（node 集合无增删、引用完整）
2. 实机：U_疲劳处理-刷花 + U_换队长 开启，部队选择页疲劳 <30 → UF 导航合战场 1-1 → 刷花循环 → 疲劳满 → 回地下城继续
3. 实机：U_刀装破坏处理各 case（停止/补充刀装/刀装保护/补充+保护）行为正确
4. 对照日志时间线：无短间隔重复点击、无卡死循环
