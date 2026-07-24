# `on_error` 使用规范

## 核心原则

> `on_error` 只在节点作为 **cur_node（当前执行节点）** 执行失败时触发，不影响上级节点的 `next` 列表遍历。

## 两个阶段

### 阶段一：上级节点的 `next` 列表识别

```
ParentNode
  └─ next: [A, B, C]
       遍历顺序：A → B → C
       匹配到谁，谁就成为 cur_node
```

此时 A/B/C 的 `on_error` **完全不参与**。它们只是识别目标，不是执行者。

### 阶段二：cur_node 执行自身流水线

```
C 成为 cur_node
  → 执行 C.action
  → 识别 C.next 列表
  → 匹配成功 → 进入子节点
  → 匹配超时 → C.on_error 触发
```

只有在这个阶段，C 的 `on_error` 才会生效。

## 何时需要 `on_error`

### 节点自身的 `next` 识别可能超时

核心判断：**节点的 `next` 列表中是否包含需要识别匹配的子节点**（OCR / TemplateMatch / ColorMatch 等）。如果是纯 Click 节点 + 带识别的 next，识别阶段可能超时，必须有 `on_error`。

```json
// 需要 on_error：Click + next 指向识别节点
"E_StartExpedition": {
    "action": { "type": "Click", ... },
    "next": ["E_VerifyConfirmPopup"],  // OCR 识别 "远征确认"
    "on_error": ["E_GoHome"]
}
```

### 路由节点（纯 `next`，无 action）

路由节点同样可能因 `next` 中所有子节点都未能匹配而失败。

### 包含识别字段的节点执行 action 后

如果节点自身有 `recognition`，匹配成功进入后执行 action，然后进入自己的 `next` 列表继续流转——此时它的 `next` 同样可能超时。

## 何时无需 `on_error`

### `next` 列表中不含任何识别节点

```json
// 无需 on_error：next 指向纯 Click（DirectHit 永真匹配）
"S_ClickFormation1": {
    "action": { "type": "Click", ... },
    "next": ["S_ClickFormation2"]  // 纯 Click，DirectHit，永真
}
```

```json
// 无需 on_error：next 指向枢纽（无 recognition，DirectHit）
"S_ConfirmEra": {
    "action": { "type": "Click", ... },
    "next": ["S_DetectWhereAmI"]  // 枢纽节点，DirectHit，永真
}
```

### 节点已在主枢纽的 `next` 扫描链中

属于枢纽直接扫描范围的节点，其匹配失败由枢纽的 `timeout` 和 `on_error` 兜底，不需要自身 `on_error`。但注意：这些节点一旦匹配成功成为 cur_node，如果它们自身的 `next` 也有识别需求，仍然需要 `on_error`。

## 多 `next` 列表与短路

如果一个 multi-next 列表中的某个 child node 有自己的 `on_error`，它成为 cur_node 后执行失败时，会走自己的 `on_error` 而不是退回上级。这是**设计意图**——上级的决策"选它"是正确的，它内部出错应当自己处理。

但在**枢纽的 `next` 扫描链**中，如果 child node 的 `on_error` 会截断后续遍历，则需要评估是否合理。详见 [[多next短路风险排查]]。

```json
// 例：S_TryClickHome 的 next 有两个分支
"S_TryClickHome": {
    "next": ["S_ClickHomeInMenu", "S_DetectWhereAmI"]
}
// S_ClickHomeInMenu 匹配成功后若超时，走自己的 on_error，
// 不会退回 S_TryClickHome 去试 S_DetectWhereAmI
```

## 回退目标选择

- 优先回退到**主枢纽**（`DetectWhereAmI`、`GoHome`、任务入口等），从全局状态重新判断
- 避免回退到同级别邻节点，形成死循环

## 检查清单

新增节点时逐项确认：

1. 节点 `next` 列表中是否含识别节点（OCR/TemplateMatch/ColorMatch）？
2. 如果是纯 Click，`next` 指向什么？是否需要 `on_error`？
3. 如果节点在多 `next` 列表中，其 `on_error` 是否会不合理地截断遍历？
4. `on_error` 的回退目标是否合理？
