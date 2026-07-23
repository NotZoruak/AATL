# 合战场（Sortie）缺失 `on_error` 的节点

> 仅列出**有识别但无 `on_error` 兜底**的节点。无识别的纯点击/Custom/Log 节点不需要 on_error。

## 统计

共计 **14** 个节点需要补充 `on_error`。

## 节点清单

- S_ClickDescending
- S_ConfirmSortAsc
- S_ClickMenuFromRepair
- S_FindSlot1
- S_FindSlot2
- S_FindSlot3
- S_ClickRepairDone
- S_IsHome
- S_IsPreDamage1
- S_IsPreDamage2
- S_StopOnDamagePopup
- S_StopOnDamageText
- S_VerifyMenuSortie
- S_ConfirmSelectSlot

---

## 已排除规则

1. 在 `S_DetectWhereAmI` next 列表中 — 失败后随扫描链恢复
2. 在 `S_CheckBossBeforeMarch` next 列表中 — 路由覆盖
3. 无 `recognition` 字段 — 纯点击/Custom/Log 节点无失败路径
