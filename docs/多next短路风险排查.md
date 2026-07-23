# 多 next 列表短路风险排查

> child node on_error 会导致 parent node 的 next 列表中后续 child node 不被尝试。
> 仅列出 child node 非 DetectWhereAmI 的剩余风险。

## Expedition (1)

| parent node | child node | child on_error |
|-------------|------------|----------------|
| E_FlowerIsRegionSelect | E_FlowerBattleHub | ['E_FlowerRestartGame'] |

## Mix (25)

| parent node | child node | child on_error |
|-------------|------------|----------------|
| M_DetectMixSub | M_CheckSubLevel | ['M_ClickSelectAll'] |
| M_ClickSelectAll | M_CheckAndMix | ['M_ExitNoMaterial'] |
| M_ConfirmMix2 | M_CheckSubLevel | ['M_ClickSelectAll'] |
| M_CheckPos1 | M_DetectEmpty1 | ['M_DetectLocked1'] |
| M_CheckPos1 | M_DetectLocked1 | ['M_CheckLevel1'] |
| M_CheckPos1 | M_CheckLevel1 | ['M_ClickMixBtn1'] |
| M_CheckPos2 | M_DetectEmpty2 | ['M_DetectLocked2'] |
| M_CheckPos2 | M_DetectLocked2 | ['M_CheckLevel2'] |
| M_CheckPos2 | M_CheckLevel2 | ['M_ClickMixBtn2'] |
| M_CheckPos3 | M_DetectEmpty3 | ['M_DetectLocked3'] |
| M_CheckPos3 | M_DetectLocked3 | ['M_CheckLevel3'] |
| M_CheckPos3 | M_CheckLevel3 | ['M_ClickMixBtn3'] |
| M_CheckPos4 | M_DetectEmpty4 | ['M_DetectLocked4'] |
| M_CheckPos4 | M_DetectLocked4 | ['M_CheckLevel4'] |
| M_CheckPos4 | M_CheckLevel4 | ['M_ClickMixBtn4'] |
| M_CheckPos5 | M_DetectEmpty5 | ['M_DetectLocked5'] |
| M_CheckPos5 | M_DetectLocked5 | ['M_CheckLevel5'] |
| M_CheckPos5 | M_CheckLevel5 | ['M_ClickMixBtn5'] |
| M_CheckPos6 | M_DetectEmpty6 | ['M_DetectLocked6'] |
| M_CheckPos6 | M_DetectLocked6 | ['M_CheckLevel6'] |
| M_CheckPos6 | M_CheckLevel6 | ['M_ClickMixBtn6'] |
| M_FilterPostHub | M_PostFilterReward | ['M_FilterPostHub'] |
| M_FilterPostHub | M_PostFilterMailbox | ['M_FilterPostHub'] |
| M_PostFilterReward | M_DetectPurchase | ['M_MailboxCloseFinal'] |
| M_DetectReward | M_DetectPurchase | ['M_MailboxCloseFinal'] |

## Sortie (1)

| parent node | child node | child on_error |
|-------------|------------|----------------|
| S_TryClickRepair | S_SelectDamagedSword | ['S_CheckRepairScreen'] |

## Underground (5)

| parent node | child node | child on_error |
|-------------|------------|----------------|
| U_IsTeamSelect | U_CheckEquipmentPopup | ['U_ClickTeam'] |
| U_IsTeamSelect | U_StopOnEquipmentPopup | ['U_ClickTeam'] |
| U_PostSortieHub | U_ConfirmEnterUnderground | ['U_PostSortieHub'] |
| U_PostSortieHub | U_CheckEquipmentPopup | ['U_ClickTeam'] |
| U_TryClickRepair | U_SelectDamagedSword | ['U_CheckRepairScreen'] |

总计: 32 处
