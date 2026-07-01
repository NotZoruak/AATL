# 习合（Mix）任务流水线实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 AATL 创建习合（合成强化）自动化任务流水线，支持刀种筛选和搓糖模式。

**Architecture:** 基于刀解（Disassemble.json）复制并改造，新建 Mix.json（~60 节点），新建 MixLevelCheckAction.cs 自定义动作，修改 interface.json 注册任务。

**Tech Stack:** MaaFramework JSON Pipeline Protocol, C# 自定义动作（运行时编译）

**设计文档:** `docs/习合任务设计.md`

---

## 文件清单

| 操作 | 文件 | 职责 |
|---|---|---|
| 创建 | `resource/base/pipeline/Mix.json` | 习合流水线全部 JSON 节点 |
| 创建 | `resource/base/custom/MixLevelCheckAction.cs` | OCR 读取等级判断是否 < 阈值 |
| 修改 | `resource/interface.json` | 注册习合任务及选项 |

---

### Task 1: 创建 MixLevelCheckAction.cs 自定义动作

**Files:**
- Create: `resource/base/custom/MixLevelCheckAction.cs`

- [ ] **Step 1: 创建 MixLevelCheckAction.cs**

```csharp
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class MixLevelCheckAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(MixLevelCheckAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            // param: "roi_x,roi_y,roi_w,roi_h,threshold"
            var parts = args.ActionParam.Split(',');
            if (parts.Length < 5) return false;

            int rx = int.Parse(parts[0]);
            int ry = int.Parse(parts[1]);
            int rw = int.Parse(parts[2]);
            int rh = int.Parse(parts[3]);
            int threshold = int.Parse(parts[4]);

            using var image = context.GetImage();
            if (image == null) return false;

            var text = context.GetText(rx, ry, rw, rh, image);
            // OCR 可能把 0 识别成字母 O
            if (text == "O" || text == "o")
                text = "0";

            LoggerHelper.Info($"[MixLevelCheck] OCR 识别等级: '{text}', 阈值: {threshold}");

            if (int.TryParse(text, out int level))
                return level < threshold;

            return false;
        }
        catch (MaaStopException)
        {
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[MixLevelCheck] Error: {e.Message}");
            return false;
        }
    }
}
```

- [ ] **Step 2: 验证语法一致性**

确认 using 语句与同目录下 `DungeonFloorSelectAction.cs` 一致：`MaaFramework.Binding`、`MaaFramework.Binding.Custom`、`MFAAvalonia.Helper`、`System`。运行时编译自动加载。

---

### Task 2: 创建 Mix.json — 入口节点和主枢纽

**Files:**
- Create: `resource/base/pipeline/Mix.json`

- [ ] **Step 1: 创建入口节点和主枢纽**

```json
{
    "Mix": {
        "next": [
            "M_DetectWhereAmI"
        ]
    },
    "M_DetectWhereAmI": {
        "pre_delay": 0,
        "next": [
            "M_DetectEmptyMailbox",
            "M_HasExpeditionReturn_Exp",
            "M_HasExpeditionReturn_Title",
            "M_CheckHomeBrightness",
            "M_DetectMenu",
            "M_DetectRefine",
            "M_DetectMix",
            "M_DetectMixSub",
            "M_DetectFilter",
            "M_DetectPurchasePopup",
            "M_DetectMailbox",
            "M_FallbackWait"
        ]
    }
}
```

---

### Task 3: 添加仅改前缀的通用节点（#1-#4, #9-#11）

**Files:**
- Modify: `resource/base/pipeline/Mix.json`

将以下节点从 Disassemble.json 复制过来，全局替换 `D_` → `M_`：

- [ ] **Step 1: #1 空邮箱检测**

```json
    "M_DetectEmptyMailbox": {
        "recognition": "ColorMatch",
        "roi": [142, 178, 82, 75],
        "upper": [216, 214, 207],
        "lower": [206, 204, 197],
        "count": 6150,
        "action": { "type": "Click", "param": { "target": [1231, 8, 38, 39] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_EmptyMailboxClick2"],
        "on_error": ["M_DetectWhereAmI"]
    },
    "M_EmptyMailboxClick2": {
        "action": { "type": "Click", "param": { "target": [1231, 8, 38, 39] } },
        "pre_delay": 0,
        "next": []
    },
```

- [ ] **Step 2: #2-#3 远征归来双检测**

```json
    "M_HasExpeditionReturn_Exp": {
        "recognition": "OCR",
        "roi": [388, 59, 54, 25],
        "expected": "经验",
        "action": "Click",
        "target": [388, 59, 54, 25],
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"],
        "on_error": ["M_DetectWhereAmI"]
    },
    "M_HasExpeditionReturn_Title": {
        "recognition": "OCR",
        "roi": [558, 1, 162, 52],
        "expected": "远征结果",
        "action": "Click",
        "target": [388, 59, 54, 25],
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 3: #4 本丸检测**

```json
    "M_CheckHomeBrightness": {
        "recognition": "ColorMatch",
        "roi": [66, 58, 11, 13],
        "upper": [40, 155, 30],
        "lower": [23, 95, 15],
        "action": "DoNothing",
        "next": ["M_DetectHome"]
    },
    "M_DetectHome": {
        "recognition": {
            "type": "TemplateMatch",
            "param": {
                "roi": [8, 1, 71, 107],
                "template": "本丸.png",
                "green_mask": true,
                "threshold": [0.98]
            }
        },
        "action": {
            "type": "Click",
            "param": {
                "target": [1179, 8, 95, 56],
                "target_offset": [5, 3, 0, 0]
            }
        },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 4: #9-#11 购买弹窗、收件箱、兜底**

```json
    "M_DetectPurchasePopup": {
        "recognition": {
            "type": "OCR",
            "param": { "roi": [570, 162, 136, 44], "expected": "购买详情" }
        },
        "action": { "type": "Click", "param": { "target": [1231, 8, 38, 39] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_MailboxCloseFinal"],
        "on_error": ["M_DetectWhereAmI"]
    },
    "M_DetectMailbox": {
        "recognition": {
            "type": "OCR",
            "param": { "roi": [591, 29, 100, 53], "expected": "收件箱" }
        },
        "action": { "type": "Click", "param": { "target": [967, 113, 48, 22] } },
        "pre_delay": 1000,
        "post_delay": 1000,
        "next": ["M_DetectWhereAmI"],
        "on_error": ["M_DetectWhereAmI"]
    },
    "M_FallbackWait": {
        "action": "DoNothing",
        "next": ["M_DetectWhereAmI"]
    },
```

---

### Task 4: 添加业务专属节点（#5-#8, M_DetectMixSub）

**Files:**
- Modify: `resource/base/pipeline/Mix.json`

- [ ] **Step 1: #5 菜单目录 → 强化按钮**

```json
    "M_DetectMenu": {
        "recognition": {
            "type": "TemplateMatch",
            "param": {
                "roi": [581, 28, 112, 57],
                "template": "菜单目录.png",
                "green_mask": true,
                "threshold": [0.98]
            }
        },
        "action": { "type": "Click", "param": { "target": [453, 278, 84, 32] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 2: #6 强化页面 → 切换习合标签**

```json
    "M_DetectRefine": {
        "recognition": {
            "type": "OCR",
            "param": { "roi": [473, 4, 78, 44], "expected": "合成" }
        },
        "action": { "type": "Click", "param": { "target": [13, 315, 22, 52] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 3: #7 习合一级页面 → 隐藏切换 → 筛选**

```json
    "M_DetectMix": {
        "recognition": {
            "type": "OCR",
            "param": { "roi": [105, 82, 285, 42], "expected": "隐藏无法习合的刀剑" }
        },
        "action": "DoNothing",
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_HideUnmixable"],
        "on_error": ["M_DetectWhereAmI"]
    },
    "M_HideUnmixable": {
        "recognition": "ColorMatch",
        "roi": [137, 106, 2, 4],
        "upper": [180, 35, 34],
        "lower": [176, 31, 30],
        "action": "DoNothing",
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_ClickFilter"],
        "on_error": ["M_ClickHideUnmixable"]
    },
    "M_ClickHideUnmixable": {
        "action": { "type": "Click", "param": { "target": [137, 106, 2, 4] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickFilter": {
        "action": { "type": "Click", "param": { "target": [823, 85, 40, 20] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 4: M_DetectMixSub 习合二级窗口**

```json
    "M_DetectMixSub": {
        "recognition": {
            "type": "OCR",
            "param": { "roi": [158, 83, 240, 36], "expected": "显示保护中的刀剑男士" }
        },
        "action": "DoNothing",
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_ClickSelectAll"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 5: #8 筛选页面检测**

```json
    "M_DetectFilter": {
        "recognition": {
            "type": "OCR",
            "param": { "roi": [496, 63, 78, 44], "expected": "筛选" }
        },
        "action": { "type": "Click", "param": { "target": [752, 145, 100, 25] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_FilterSwordHub"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

---

### Task 5: 添加习合主循环链路

**Files:**
- Modify: `resource/base/pipeline/Mix.json`

- [ ] **Step 1: 一键选择 → 材料检测 → 习合 → 循环**

```json
    "M_ClickSelectAll": {
        "action": { "type": "Click", "param": { "target": [1158, 463, 80, 36] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_CheckMaterial"]
    },
    "M_CheckMaterial": {
        "recognition": "ColorMatch",
        "roi": [1166, 586, 71, 19],
        "upper": [25, 86, 182],
        "lower": [21, 82, 178],
        "action": "DoNothing",
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_ClickMixButton"],
        "on_error": ["M_ExitNoMaterial"]
    },
    "M_ExitNoMaterial": {
        "action": { "type": "Click", "param": { "target": [143, 11, 19, 25] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixButton": {
        "action": { "type": "Click", "param": { "target": [1166, 586, 71, 19] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_ConfirmMix"]
    },
    "M_ConfirmMix": {
        "action": { "type": "Click", "param": { "target": [746, 611, 82, 36] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_ClickSelectAll"]
    },
```

---

### Task 6: 添加五个位置检测链路

**Files:**
- Modify: `resource/base/pipeline/Mix.json`

坐标表：

| 位置 | roi | lock_click | level_param |
|---|---|---|---|
| 1 | `[88,127,7,8]` | `[1216,164,9,9]` | `604,147,37,20,7` |
| 2 | `[88,228,7,8]` | `[1216,265,9,9]` | `604,248,37,20,7` |
| 3 | `[88,329,7,8]` | `[1216,366,9,9]` | `604,349,37,20,7` |
| 4 | `[88,430,7,8]` | `[1216,467,9,9]` | `604,450,37,20,7` |
| 5 | `[88,531,7,8]` | `[1216,568,9,9]` | `604,551,37,20,7` |

- [ ] **Step 1: CheckPos 中心节点（无 recognition，纯路由）**

```json
    "M_CheckPos1": {
        "next": ["M_DetectEmpty1", "M_DetectLocked1", "M_CheckLevel1"]
    },
    "M_CheckPos2": {
        "next": ["M_DetectEmpty2", "M_DetectLocked2", "M_CheckLevel2"]
    },
    "M_CheckPos3": {
        "next": ["M_DetectEmpty3", "M_DetectLocked3", "M_CheckLevel3"]
    },
    "M_CheckPos4": {
        "next": ["M_DetectEmpty4", "M_DetectLocked4", "M_CheckLevel4"]
    },
    "M_CheckPos5": {
        "next": ["M_DetectEmpty5", "M_DetectLocked5", "M_CheckLevel5"]
    },
```

- [ ] **Step 2: 空槽检测节点（1-5）**

```json
    "M_DetectEmpty1": {
        "recognition": "ColorMatch",
        "roi": [88, 127, 7, 8],
        "upper": [222, 222, 222],
        "lower": [218, 218, 218],
        "action": "DoNothing",
        "next": ["M_GoToMailboxFromMix"],
        "on_error": ["M_DetectLocked1"]
    },
    "M_DetectEmpty2": {
        "recognition": "ColorMatch",
        "roi": [88, 228, 7, 8],
        "upper": [222, 222, 222],
        "lower": [218, 218, 218],
        "action": "DoNothing",
        "next": ["M_GoToMailboxFromMix"],
        "on_error": ["M_DetectLocked2"]
    },
    "M_DetectEmpty3": {
        "recognition": "ColorMatch",
        "roi": [88, 329, 7, 8],
        "upper": [222, 222, 222],
        "lower": [218, 218, 218],
        "action": "DoNothing",
        "next": ["M_GoToMailboxFromMix"],
        "on_error": ["M_DetectLocked3"]
    },
    "M_DetectEmpty4": {
        "recognition": "ColorMatch",
        "roi": [88, 430, 7, 8],
        "upper": [222, 222, 222],
        "lower": [218, 218, 218],
        "action": "DoNothing",
        "next": ["M_GoToMailboxFromMix"],
        "on_error": ["M_DetectLocked4"]
    },
    "M_DetectEmpty5": {
        "recognition": "ColorMatch",
        "roi": [88, 531, 7, 8],
        "upper": [222, 222, 222],
        "lower": [218, 218, 218],
        "action": "DoNothing",
        "next": ["M_GoToMailboxFromMix"],
        "on_error": ["M_DetectLocked5"]
    },
```

- [ ] **Step 3: 带锁检测节点（1-5）**

```json
    "M_DetectLocked1": {
        "recognition": "ColorMatch",
        "roi": [88, 127, 7, 8],
        "upper": [214, 175, 33],
        "lower": [210, 171, 29],
        "action": "DoNothing",
        "next": ["M_ClickMixBtn1"],
        "on_error": ["M_CheckLevel1"]
    },
    "M_DetectLocked2": {
        "recognition": "ColorMatch",
        "roi": [88, 228, 7, 8],
        "upper": [214, 175, 33],
        "lower": [210, 171, 29],
        "action": "DoNothing",
        "next": ["M_ClickMixBtn2"],
        "on_error": ["M_CheckLevel2"]
    },
    "M_DetectLocked3": {
        "recognition": "ColorMatch",
        "roi": [88, 329, 7, 8],
        "upper": [214, 175, 33],
        "lower": [210, 171, 29],
        "action": "DoNothing",
        "next": ["M_ClickMixBtn3"],
        "on_error": ["M_CheckLevel3"]
    },
    "M_DetectLocked4": {
        "recognition": "ColorMatch",
        "roi": [88, 430, 7, 8],
        "upper": [214, 175, 33],
        "lower": [210, 171, 29],
        "action": "DoNothing",
        "next": ["M_ClickMixBtn4"],
        "on_error": ["M_CheckLevel4"]
    },
    "M_DetectLocked5": {
        "recognition": "ColorMatch",
        "roi": [88, 531, 7, 8],
        "upper": [214, 175, 33],
        "lower": [210, 171, 29],
        "action": "DoNothing",
        "next": ["M_ClickMixBtn5"],
        "on_error": ["M_CheckLevel5"]
    },
```

- [ ] **Step 4: 习合按钮（1-5，带锁/不搓糖 → 直接习合）**

```json
    "M_ClickMixBtn1": {
        "action": { "type": "Click", "param": { "target": [1216, 164, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixBtn2": {
        "action": { "type": "Click", "param": { "target": [1216, 265, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixBtn3": {
        "action": { "type": "Click", "param": { "target": [1216, 366, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixBtn4": {
        "action": { "type": "Click", "param": { "target": [1216, 467, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixBtn5": {
        "action": { "type": "Click", "param": { "target": [1216, 568, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 5: 搓糖开——等级检测节点（1-5）**

```json
    "M_CheckLevel1": {
        "enabled": false,
        "recognition": {
            "type": "Custom",
            "param": {
                "custom_action": "MixLevelCheck",
                "custom_action_param": "604,147,37,20,7"
            }
        },
        "action": "DoNothing",
        "next": ["M_ClickMixBtn1"],
        "on_error": ["M_CheckPos2"]
    },
    "M_CheckLevel2": {
        "enabled": false,
        "recognition": {
            "type": "Custom",
            "param": {
                "custom_action": "MixLevelCheck",
                "custom_action_param": "604,248,37,20,7"
            }
        },
        "action": "DoNothing",
        "next": ["M_ClickMixBtn2"],
        "on_error": ["M_CheckPos3"]
    },
    "M_CheckLevel3": {
        "enabled": false,
        "recognition": {
            "type": "Custom",
            "param": {
                "custom_action": "MixLevelCheck",
                "custom_action_param": "604,349,37,20,7"
            }
        },
        "action": "DoNothing",
        "next": ["M_ClickMixBtn3"],
        "on_error": ["M_CheckPos4"]
    },
    "M_CheckLevel4": {
        "enabled": false,
        "recognition": {
            "type": "Custom",
            "param": {
                "custom_action": "MixLevelCheck",
                "custom_action_param": "604,450,37,20,7"
            }
        },
        "action": "DoNothing",
        "next": ["M_ClickMixBtn4"],
        "on_error": ["M_CheckPos5"]
    },
    "M_CheckLevel5": {
        "enabled": false,
        "recognition": {
            "type": "Custom",
            "param": {
                "custom_action": "MixLevelCheck",
                "custom_action_param": "604,551,37,20,7"
            }
        },
        "action": "DoNothing",
        "next": ["M_ClickMixBtn5"],
        "on_error": ["M_SwipeDown"]
    },
```

- [ ] **Step 6: 搓糖关——盲点习合节点（1-5）**

```json
    "M_ClickMixDirect1": {
        "enabled": true,
        "action": { "type": "Click", "param": { "target": [1216, 164, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixDirect2": {
        "enabled": true,
        "action": { "type": "Click", "param": { "target": [1216, 265, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixDirect3": {
        "enabled": true,
        "action": { "type": "Click", "param": { "target": [1216, 366, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixDirect4": {
        "enabled": true,
        "action": { "type": "Click", "param": { "target": [1216, 467, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
    "M_ClickMixDirect5": {
        "enabled": true,
        "action": { "type": "Click", "param": { "target": [1216, 568, 9, 9] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 7: 滑动节点 + 空槽邮箱跳转**

```json
    "M_SwipeDown": {
        "action": {
            "type": "Swipe",
            "param": {
                "begin": [88, 637, 7, 8],
                "end": [88, 127, 7, 8],
                "duration": 300
            }
        },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_CheckPos1"]
    },
    "M_GoToMailboxFromMix": {
        "action": { "type": "Click", "param": { "target": [1179, 8, 95, 56] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_VerifyMenuFromMix"]
    },
    "M_VerifyMenuFromMix": {
        "recognition": {
            "type": "TemplateMatch",
            "param": {
                "roi": [581, 28, 112, 57],
                "template": "菜单目录.png",
                "green_mask": true,
                "threshold": [0.98]
            }
        },
        "action": { "type": "Click", "param": { "target": [999, 372, 43, 45] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_DetectWhereAmI"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

---

### Task 7: 添加筛选链路和邮箱链路

**Files:**
- Modify: `resource/base/pipeline/Mix.json`

- [ ] **Step 1: 筛选刀种中枢和确认（从 Disassemble.json 复制，D_ → M_）**

```json
    "M_FilterSwordHub": {
        "next": [
            "M_FilterSword1", "M_FilterSword2", "M_FilterSword3",
            "M_FilterSword4", "M_FilterSword5", "M_FilterSword6",
            "M_FilterSword7", "M_FilterConfirm", "M_DetectWhereAmI"
        ]
    },
```

- [ ] **Step 2: M_FilterSword1~7（从 Disassemble.json 复制，替换前缀和 next 中的前缀）**

7 个刀种节点，每个 `enabled: false`，坐标与刀解一致。next 链从 D_ → M_ 替换。

- [ ] **Step 3: 筛选确认 → PostHub**

```json
    "M_FilterConfirm": {
        "action": { "type": "Click", "param": { "target": [588, 604, 99, 39] } },
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_FilterPostHub"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 4: FilterPostHub 和 PostFilter 节点**

```json
    "M_FilterPostHub": {
        "next": [
            "M_PostFilterReward", "M_PostFilterMix",
            "M_DetectEmptyMailbox", "M_PostFilterMailbox",
            "M_DetectWhereAmI"
        ]
    },
    "M_PostFilterMix": {
        "recognition": {
            "type": "OCR",
            "param": { "roi": [105, 82, 285, 42], "expected": "隐藏无法习合的刀剑" }
        },
        "action": "DoNothing",
        "pre_delay": 300,
        "post_delay": 500,
        "next": ["M_CheckPos1"],
        "on_error": ["M_DetectWhereAmI"]
    },
```

- [ ] **Step 5: 邮箱链路节点（从 Disassemble.json 复制，D_ → M_）**

复制以下节点并改前缀：`M_PostFilterReward`、`M_PostFilterMailbox`、`M_DetectReward`、`M_DetectPurchase`、`M_MailboxCloseFinal`、`M_MailboxCloseVerify`。

---

### Task 8: 更新 interface.json

**Files:**
- Modify: `resource/interface.json`

- [ ] **Step 1: 在刀解任务之后插入习合任务定义**

在 `"name": "刀解"` 任务块（第 140 行 `}` 之后）插入：

```json
    {
      "name": "习合",
      "entry": "Mix",
      "default_check": false,
      "repeatable": false,
      "repeat_count": 0,
      "option": [
        "短",
        "胁",
        "打",
        "太",
        "大太",
        "枪",
        "薙",
        "搓糖"
      ],
      "label": "习合",
      "description": "自动习合操作，勾选刀种参与习合。搓糖模式：习合至7级后跳过该刀剑"
    }
```

注意在刀解任务块的 `}` 后加逗号，与下一个任务分隔。

---

### Task 9: 构建、同步、验证

- [ ] **Step 1: 构建**

```bash
dotnet build _src/MFAAvalonia.Desktop/MFAAvalonia.Desktop.csproj
```

预期：0 errors。

- [ ] **Step 2: 同步产物**

```bash
cp _src/bin/AnyCPU/Debug/AATL.dll .
cp _src/bin/AnyCPU/Debug/AATL.exe .
cp _src/bin/AnyCPU/Debug/MFAAvalonia.Core.dll runtimes/libs/
```

- [ ] **Step 3: 启动 AATL.exe，验证习合任务出现在任务列表中**

检查左侧导航栏是否出现「习合」任务条目，选项是否包含 7 个刀种复选框和 1 个搓糖复选框。

- [ ] **Step 4: 运行冒烟测试**

在游戏中启动习合任务，验证主枢纽导航是否正常工作。确认：从本丸 → 菜单 → 强化 → 习合 → 筛选 → 习合一级 → 启用隐藏 → 位置检测的流程能否走通。

---
