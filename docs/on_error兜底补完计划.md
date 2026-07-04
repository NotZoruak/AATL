# on_error 兜底补完计划

本文档记录所有 pipeline 中**有识别条件但缺少 `on_error`** 的节点。

## 已完成

| 文件 | 节点 | on_error |
|------|------|----------|
| FlowerBrush | `FB_ClickMenuForSortie` | `FB_DetectWhereAmI` |
| EdoCastle | `EC_ClickMenuForSortie` | `EC_DetectWhereAmI` |
| Sortie | `S_ClickMenuForRepair` | `S_DetectWhereAmI` |
| Underground | `U_ClickMenuForRepair` | `U_DetectWhereAmI` |
| Sortie | `S_ClickRepairInMenu` | `S_DetectWhereAmI` |
| Underground | `U_ClickRepairInMenu` | `U_DetectWhereAmI` |
| Sortie | `S_SelectDamagedSword` | `S_CheckRepairScreen` |
| Underground | `U_SelectDamagedSword` | `U_CheckRepairScreen` |
| Sortie | `S_ConfirmSelectSlot` | `S_LogNoDamagedSword` |
| Underground | `U_ConfirmSelectSlot` | `U_LogNoDamagedSword` |
| Sortie | `S_DirAfterRepair` | `S_DetectWhereAmI` |
| Underground | `U_DirAfterRepair` | `U_DetectWhereAmI` |
| Sortie | `S_SortieAfterRepair` | `S_DetectWhereAmI` |
| Underground | `U_SortieAfterRepair` | `U_DetectWhereAmI` |
| Sortie | `S_ConfirmCancelPreDamage2` | `S_DetectWhereAmI` |
| Underground | `U_ConfirmCancelPreDamage2` | `U_DetectWhereAmI` |
| Sortie | `S_ConfirmCancelMarchingDamage2` | `S_DetectWhereAmI` |
| Underground | `U_ConfirmCancelMarchingDamage2` | `U_DetectWhereAmI` |
| Sortie | `S_ConfirmReturnHome` | `S_DetectWhereAmI` |
| Underground | `U_ConfirmReturnHome` | `U_DetectWhereAmI` |
| Sortie | `S_IsMarchingDamage2` | `S_DetectWhereAmI` |
| Underground | `U_IsMarchingDamage2` | `U_DetectWhereAmI` |
| Sortie | `S_ClickMenu` | `S_DetectWhereAmI` |
| Underground | `U_ClickMenu` | `U_DetectWhereAmI` |
| LRentaisen | `LR_ClickMenu` | `LR_DetectWhereAmI` |
| Underground | `U_ClickUndergroundInMenu` | `U_DetectWhereAmI` |
| LRentaisen | `LR_ClickUndergroundInMenu` | `LR_DetectWhereAmI` |
| EdoCastle | `EC_ClickRestore3` | `EC_ClickRestore3`（自循环） |
| LRentaisen | `LR_ClickRestore3` | `LR_ClickRestore3`（自循环） |

## 新增节点

| 文件 | 节点 | 说明 |
|------|------|------|
| Sortie | `S_CheckRepairScreen` | OCR [554,56,175,49] "修复"，命中→SelectDamagedSword，未命中→DetectWhereAmI |
| Underground | `U_CheckRepairScreen` | 同上 |
| Sortie | `S_LogNoDamagedSword` | 日志"未检测到可修复刀剑" → DetectWhereAmI |
| Underground | `U_LogNoDamagedSword` | 同上 |

## 确认无需处理的节点

| 文件 | 节点 | 原因 |
|------|------|------|
| EdoCastle | `EC_HandleNoTicket` | 位于 `DetectTicket` 分支列表，不命中自动走 `ConfirmEnterMap` |
| LRentaisen | `LR_HandleNoTicket` | 同上 |
| EdoCastle | `EC_ReplenishTicket` | 位于 `ReplenishOrTerminate` 分支列表，不命中自动走 `TerminateNoTicket` |
| LRentaisen | `LR_ReplenishTicket` | 纯 Click 无识别，由上级分支兜底 |

## 无需处理的节点类别

- **枢纽检测节点**：位于 `DetectWhereAmI` 的 `next` 列表中，识别失败是正常的"非此状态"，由 FallbackWait 循环兜底
- **已有正确兜底的节点**：`*_IsAnnouncementPopup`、`*_IsLoginReward`、`*_IsGameIcon`、`*_IsLoginButton`、`*_IsGameUpdatePopup`、`*_IsInGameUpdatePopup`、`*_IsInternalReport`、`*_ClickSortieInMenu`、`*_VerifyMenuClosed`、`*_VerifyHomeMenuClosed`、`*_VerifyLeftMenuSortie`、`*_VerifySortieMenuClosed`、`*_IsSwordDropColor`、`*_MailboxCloseVerify` 等
