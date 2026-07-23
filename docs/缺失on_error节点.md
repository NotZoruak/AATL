# 缺失 `on_error` 节点清单

> 排除：已在多 next parent node 列表中的 child node、纯 click/Custom/Log 无 recognition 节点。

## Sortie（合战场）— 3

| 节点                         | 类型      | 审核结果 |
| -------------------------- | ------- | ---- |
| `Sortie`                   | routing | 已通过  |
| `S_ReturnHome`             | routing | 已通过  |
| `S_CheckAccel`             | routing | 已通过  |


## Expedition（远征）— 21

| 节点 | 类型 |
|------|------|
| `E_GoHome` | routing |
| `E_PostClickDirectoryHub` | routing |
| `E_ExpSubHub1` | routing |
| `E_UseTeamRecord1` | routing |
| `E_UseTeamRecord2` | routing |
| `E_UseTeamRecord3` | routing |
| `E_UseTeamRecord4` | routing |
| `E_UseTeamRecord5` | routing |
| `E_UseTeamRecord_Step1_Rec1` | routing |
| `E_UseTeamRecord_Step1_Rec2` | routing |
| `E_UseTeamRecord_Step1_Rec3` | routing |
| `E_UseTeamRecord_Step1_Rec4` | routing |
| `E_UseTeamRecord_Step1_Rec5` | routing |
| `E_UseTeamRecord_Step2_Rec1` | routing |
| `E_UseTeamRecord_Step2_Rec2` | routing |
| `E_UseTeamRecord_Step2_Rec3` | routing |
| `E_UseTeamRecord_Step2_Rec4` | routing |
| `E_UseTeamRecord_Step2_Rec5` | routing |
| `E_FlowerNavigate` | routing |
| `E_FlowerIsHome` | recog |
| `E_FlowerDone` | routing |

## LRentaisen（陆联）— 5

| 节点 | 类型 | 审核结果 |
|------|------|----------|
| `LRentaisen` | routing | 无需 |
| `LR_DetectTicket` | routing | 无需 |
| `LR_ReplenishOrTerminate` | routing | 无需 |
| `LR_HandleTicketPopup` | routing | 无需 |
| `LR_PostSortieHub` | routing | 无需 |

## FlowerBrush（刷花）— 7

| 节点 | 类型 | 审核结果 |
|------|------|----------|
| `FB_SelectFormation` | routing | 无需 |
| `FB_Dissolve` | routing | 无需 |
| `FB_SelectEra` | routing | 无需 |
| `FB_SelectRegion` | routing | 无需 |
| `FB_NavigateToSortie` | routing | 无需 |
| `FB_PostSelectFormation` | routing | 无需 |
| `FB_PostHome` | recog | 无需（任务终点） |

## Underground（地下城）— 14

| 节点 | 类型 |
|------|------|
| `Underground` | routing |
| `U_CheckBeforeMarch` | routing |
| `U_CaptainHub` | routing |
| `U_CheckAccel` | routing |
| `U_ClickMenuFromRepair` | routing |
| `U_DecideDamage_Pre` | routing |
| `U_DetectSortOrder` | routing |
| `U_IsHome` | recog |
| `U_PostClickMenuHub` | routing |
| `U_PostSortieHub` | routing |
| `U_ReturnHome` | routing |
| `U_TryClickRepair` | routing |
| `U_ClickRepairDone` | recog |
| `U_NavigateToHomeFromStop` | routing |

## Mix（习合）— 3

| 节点                 | 类型      |
| ------------------ | ------- |
| `Mix`              | routing |
| `M_FilterSwordHub` | routing |
| `M_FilterPostHub`  | routing |

## Disassemble（刀解）— ✅

## GoHome（回到本丸）— ✅
