# 地下城刷花状态机与流水线优化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为地下城（Underground.json）新建 UF_ 刷花状态机（完整对应合战场 SF_），接入 U_疲劳处理-刷花 选项，并完成地下城全 pipeline 的冗余清理与 wait_freezes 迁移。

**Architecture:** UF_ 状态机从 Sortie.json 的 SF_ 系列复制（前缀替换 + next/on_error 引用替换），识别参数完全一致（合战场 1-1 画面）；入口由地下城部队选择页 U_FatigueCheck（threshold 30）触发；出口 UF_NavigateBack 指向 U_DetectWhereAmI 回地下城主流程。delay 处理参照合战场已完成模式（全屏 freeze 100ms、点击区域 100ms、分离区域 100-200ms）。

**Tech Stack:** MaaFW pipeline JSON、interface.json 选项 override、Python 校验脚本。

## Global Constraints

- node 命名前缀：UF_（对应 SF_）
- 识别参数（ROI、模板、颜色、点击坐标）与 SF_ 完全一致，仅 node 名前缀替换
- delay 规则：全屏 freeze time 100ms；点击区域与 freeze 区域一致 time 100ms；分离区域 100-200ms
- 有意保留：枢纽 timeout 120000、U_WaitRefresh 类等待
- JSON：4 空格缩进、正斜杠路径、无 target_offset、每个 node 必须设置 on_error
- 禁止使用中文"节点"二字，统一使用英文 node
- 不跨任务引用（UF_ 全在 Underground.json 内；出口除外——UF_NavigateBack → U_DetectWhereAmI）

---

### Task 1: UF_ 状态机核心链创建（导航/刷花/疲劳/出口）

**Files:**
- Modify: `assets/resource/base/pipeline/Underground.json`（新增 26 个 UF_ node）
- Test: 校验脚本（python 内联）

**Interfaces:**
- Consumes: Sortie.json 的 SF_ 系列 node 定义（识别参数与 delay 配置）
- Produces: UF_DetectWhereAmI、UF_Hub、UF_ClickMenu、UF_PostClickMenuHub、UF_IsMenuDirectory、UF_ClickSortieInMenu、UF_IsEraSelect、UF_ClickFirstEra、UF_ConfirmEra、UF_IsRegionSelect、UF_ClickRegion1_1、UF_IsTeamSelect、UF_CheckEquipmentPopup、UF_ClickTeamN、UF_DissolveHub、UF_DissolveCheck、UF_CheckFatigue、UF_ClickSortieNow、UF_IsMarching、UF_IsFormationSelect、UF_IsBattleResult_Exp、UF_IsBattleResult_Title、UF_IsSwordDrop、UF_FallbackWait、UF_RestartGame、UF_NavigateBack

- [ ] **Step 1: 用脚本从 SF_ 复制生成 UF_ 核心 node**

```bash
python -c "
import json, io
s = json.load(io.open('assets/resource/base/pipeline/Sortie.json', encoding='utf-8'))
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
mapping = {
  'SF_DetectWhereAmI': 'UF_DetectWhereAmI', 'SF_Hub': 'UF_Hub',
  'SF_ClickMenu': 'UF_ClickMenu', 'SF_PostClickMenuHub': 'UF_PostClickMenuHub',
  'SF_IsMenuDirectory': 'UF_IsMenuDirectory', 'SF_ClickSortieInMenu': 'UF_ClickSortieInMenu',
  'SF_IsEraSelect': 'UF_IsEraSelect', 'SF_ClickFirstEra': 'UF_ClickFirstEra',
  'SF_ConfirmEra': 'UF_ConfirmEra', 'SF_IsRegionSelect': 'UF_IsRegionSelect',
  'SF_ClickRegion1_1': 'UF_ClickRegion1_1', 'SF_IsTeamSelect': 'UF_IsTeamSelect',
  'SF_CheckEquipmentPopup': 'UF_CheckEquipmentPopup', 'SF_ClickTeamN': 'UF_ClickTeamN',
  'SF_DissolveHub': 'UF_DissolveHub', 'SF_DissolveCheck': 'UF_DissolveCheck',
  'SF_CheckFatigue': 'UF_CheckFatigue', 'SF_ClickSortieNow': 'UF_ClickSortieNow',
  'SF_IsMarching': 'UF_IsMarching', 'SF_IsFormationSelect': 'UF_IsFormationSelect',
  'SF_IsBattleResult_Exp': 'UF_IsBattleResult_Exp', 'SF_IsBattleResult_Title': 'UF_IsBattleResult_Title',
  'SF_IsSwordDrop': 'UF_IsSwordDrop', 'SF_FallbackWait': 'UF_FallbackWait',
  'SF_RestartGame': 'UF_RestartGame', 'SF_NavigateBack': 'UF_NavigateBack',
}
for s_name, u_name in mapping.items():
    node = json.loads(json.dumps(s[s_name]))  # 深拷贝
    u[u_name] = node
io.open('assets/resource/base/pipeline/Underground.json', 'w', encoding='utf-8', newline='').write(
    json.dumps(u, ensure_ascii=False, indent=4))
print('added', len(mapping), 'nodes')
"
```

注意：此步骤只复制 node 定义，next/on_error 引用替换在 Step 2 处理。

- [ ] **Step 2: 替换 UF_ node 内部的 SF_ 引用**

对 Step 1 新增的 26 个 node，将其 next/on_error 中的 SF_ 前缀引用替换为 UF_（同名字映射），**出口例外**：

| 原引用 | 替换为 |
|---|---|
| SF_DetectWhereAmI | UF_DetectWhereAmI |
| SF_Hub | UF_Hub |
| SF_ClickMenu | UF_ClickMenu |
| SF_PostClickMenuHub | UF_PostClickMenuHub |
| SF_IsMenuDirectory | UF_IsMenuDirectory |
| SF_ClickSortieInMenu | UF_ClickSortieInMenu |
| SF_IsEraSelect | UF_IsEraSelect |
| SF_ClickFirstEra | UF_ClickFirstEra |
| SF_ConfirmEra | UF_ConfirmEra |
| SF_IsRegionSelect | UF_IsRegionSelect |
| SF_ClickRegion1_1 | UF_ClickRegion1_1 |
| SF_IsTeamSelect | UF_IsTeamSelect |
| SF_CheckEquipmentPopup | UF_CheckEquipmentPopup |
| SF_ClickTeamN | UF_ClickTeamN |
| SF_DissolveHub | UF_DissolveHub |
| SF_DissolveCheck | UF_DissolveCheck |
| SF_CheckFatigue | UF_CheckFatigue |
| SF_ClickSortieNow | UF_ClickSortieNow |
| SF_IsMarching | UF_IsMarching |
| SF_IsFormationSelect | UF_IsFormationSelect |
| SF_IsBattleResult_Exp | UF_IsBattleResult_Exp |
| SF_IsBattleResult_Title | UF_IsBattleResult_Title |
| SF_IsSwordDrop | UF_IsSwordDrop |
| SF_FallbackWait | UF_FallbackWait |
| SF_RestartGame | UF_RestartGame |
| SF_NavigateBack | UF_NavigateBack |
| **S_DetectWhereAmI（UF_NavigateBack 的 next）** | **U_DetectWhereAmI（出口，保持 U_ 前缀）** |

```bash
python -c "
import json, io, re
path = 'assets/resource/base/pipeline/Underground.json'
u = json.load(io.open(path, encoding='utf-8'))
uf_names = ['UF_DetectWhereAmI','UF_Hub','UF_ClickMenu','UF_PostClickMenuHub','UF_IsMenuDirectory',
  'UF_ClickSortieInMenu','UF_IsEraSelect','UF_ClickFirstEra','UF_ConfirmEra','UF_IsRegionSelect',
  'UF_ClickRegion1_1','UF_IsTeamSelect','UF_CheckEquipmentPopup','UF_ClickTeamN','UF_DissolveHub',
  'UF_DissolveCheck','UF_CheckFatigue','UF_ClickSortieNow','UF_IsMarching','UF_IsFormationSelect',
  'UF_IsBattleResult_Exp','UF_IsBattleResult_Title','UF_IsSwordDrop','UF_FallbackWait','UF_RestartGame','UF_NavigateBack']
for name in uf_names:
    node = u[name]
    def fix(refs):
        if not refs: return refs
        out = []
        for r in refs:
            if r.startswith('SF_'):
                out.append('UF_' + r[3:])
            elif r == 'S_DetectWhereAmI' and name == 'UF_NavigateBack':
                out.append('U_DetectWhereAmI')
            else:
                out.append(r)
        return out
    node['next'] = fix(node.get('next'))
    node['on_error'] = fix(node.get('on_error'))
io.open(path, 'w', encoding='utf-8', newline='').write(json.dumps(u, ensure_ascii=False, indent=4))
print('refs fixed')
"
```

- [ ] **Step 3: 校验 JSON 有效性与引用完整性**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
uf = [n for n in u if n.startswith('UF_')]
print('UF_ node 数:', len(uf))
# 引用完整性：UF_ node 的 next/on_error 引用的 node 必须存在
missing = []
for name in uf:
    node = u[name]
    for refs in (node.get('next') or [], node.get('on_error') or []):
        for r in refs:
            if r not in u:
                missing.append((name, r))
print('缺失引用:', missing if missing else '无')
assert not missing, '存在缺失引用'
print('JSON valid, 总 node:', len(u))
"
```

预期：UF_ node 26 个，无缺失引用，JSON 有效。

- [ ] **Step 4: 校验 UF_CheckFatigue 的语义配置**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
n = u['UF_CheckFatigue']
assert n['action']['custom_action_param'] == {'mode': 'check_first', 'threshold': 100, 'reversed': True}, n['action']
assert n['next'] == ['UF_ClickSortieNow'], n['next']
assert n['on_error'] == ['UF_UseRecord_Step1'], n['on_error']
print('UF_CheckFatigue 配置正确')
"
```

预期：threshold 100 / reversed true / next UF_ClickSortieNow / on_error UF_UseRecord_Step1（UF_UseRecord_Step1 在 Task 2 创建，此处仅校验配置不校验存在性）。

- [ ] **Step 5: 校验 UF_ 出口指向**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
n = u['UF_NavigateBack']
print('UF_NavigateBack next:', n.get('next'))
assert n.get('next') == ['U_DetectWhereAmI'], '出口必须指向 U_DetectWhereAmI'
print('出口正确')
"
```

预期：UF_NavigateBack next 为 [U_DetectWhereAmI]。

- [ ] **Step 6: Commit**

```bash
git add assets/resource/base/pipeline/Underground.json
git commit -m "feat: 地下城 UF_ 刷花状态机核心链（导航/刷花/疲劳/出口）"
```

---

### Task 2: UF_ 状态机附属链创建（使用记录/装备补充/闪退恢复）

**Files:**
- Modify: `assets/resource/base/pipeline/Underground.json`（新增 31 个 UF_ node）
- Test: 校验脚本

**Interfaces:**
- Consumes: Task 1 的 UF_ 核心链（UF_CheckFatigue on_error → UF_UseRecord_Step1）
- Produces: UF_UseRecord_Step1、UF_UseRecord_Step2_Rec1-5、UF_UseRecord_Step3、UF_UseRecord_Step4、UF_EqRefill_Step1、UF_EqRefill_Step2_Rec1-5、UF_EqRefill_Step3、UF_EqRefill_Step4、UF_IsAnnouncementPopup、UF_IsTrainingLetter、UF_IsLoginReward、UF_LoginRewardClick2、UF_LoginRewardClick3、UF_IsGameIcon、UF_IsLoginButton、UF_IsGameUpdatePopup、UF_IsInGameUpdatePopup、UF_IsInternalReport、UF_IsHome、UF_CheckHomeBrightness

- [ ] **Step 1: 从 SF_ 复制生成 UF_ 附属 node**

```bash
python -c "
import json, io
s = json.load(io.open('assets/resource/base/pipeline/Sortie.json', encoding='utf-8'))
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
sf_names = ['SF_UseRecord_Step1','SF_UseRecord_Step2_Rec1','SF_UseRecord_Step2_Rec2','SF_UseRecord_Step2_Rec3','SF_UseRecord_Step2_Rec4','SF_UseRecord_Step2_Rec5','SF_UseRecord_Step3','SF_UseRecord_Step4',
  'SF_EqRefill_Step1','SF_EqRefill_Step2_Rec1','SF_EqRefill_Step2_Rec2','SF_EqRefill_Step2_Rec3','SF_EqRefill_Step2_Rec4','SF_EqRefill_Step2_Rec5','SF_EqRefill_Step3','SF_EqRefill_Step4',
  'SF_IsAnnouncementPopup','SF_IsTrainingLetter','SF_IsLoginReward','SF_LoginRewardClick2','SF_LoginRewardClick3',
  'SF_IsGameIcon','SF_IsLoginButton','SF_IsGameUpdatePopup','SF_IsInGameUpdatePopup','SF_IsInternalReport',
  'SF_IsHome','SF_CheckHomeBrightness']
for sf in sf_names:
    u['UF_' + sf[3:]] = json.loads(json.dumps(s[sf]))
io.open('assets/resource/base/pipeline/Underground.json', 'w', encoding='utf-8', newline='').write(
    json.dumps(u, ensure_ascii=False, indent=4))
print('added', len(sf_names), 'nodes')
"
```

- [ ] **Step 2: 替换 UF_ 附属 node 内部引用**

同一替换规则：next/on_error 中 `SF_` 前缀 → `UF_` 前缀；`S_DetectWhereAmI` 保持不动（闪退恢复类 next 回 UF_DetectWhereAmI——注意这些 node 在 SF_ 中 next 是 SF_DetectWhereAmI，替换后为 UF_DetectWhereAmI）。

```bash
python -c "
import json, io
path = 'assets/resource/base/pipeline/Underground.json'
u = json.load(io.open(path, encoding='utf-8'))
uf = [n for n in u if n.startswith('UF_')]
for name in uf:
    node = u[name]
    for key in ('next', 'on_error'):
        refs = node.get(key)
        if not refs: continue
        node[key] = ['UF_' + r[3:] if r.startswith('SF_') else r for r in refs]
io.open(path, 'w', encoding='utf-8', newline='').write(json.dumps(u, ensure_ascii=False, indent=4))
print('refs fixed for', len(uf), 'UF_ nodes')
"
```

- [ ] **Step 3: 校验引用完整性与 JSON 有效**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
uf = [n for n in u if n.startswith('UF_')]
print('UF_ node 总数:', len(uf))
missing = []
for name in uf:
    node = u[name]
    for refs in (node.get('next') or [], node.get('on_error') or []):
        for r in refs:
            if r not in u:
                missing.append((name, r))
print('缺失引用:', missing if missing else '无')
assert not missing
assert len(uf) == 57, f'UF_ 应为 57 个, 实际 {len(uf)}'
print('JSON valid, 总 node:', len(u))
"
```

预期：UF_ node 57 个（26 核心 + 31 附属），无缺失引用。

- [ ] **Step 4: Commit**

```bash
git add assets/resource/base/pipeline/Underground.json
git commit -m "feat: 地下城 UF_ 刷花状态机附属链（使用记录/装备补充/闪退恢复）"
```

---

### Task 3: 入口链接入（U_FatigueCheck + U_DragCaptain）

**Files:**
- Modify: `assets/resource/base/pipeline/Underground.json`（新增 U_FatigueCheck、修改 U_DragCaptain）
- Test: 校验脚本

**Interfaces:**
- Consumes: 无
- Produces: U_FatigueCheck（FatigueCheckAction check_first threshold 30，next [U_IsPreSortieConfirm]，on_error [UF_Hub]）

- [ ] **Step 1: 新增 U_FatigueCheck node**

在 Underground.json 中 U_DragCaptain 定义之后新增：

```json
"U_FatigueCheck": {
    "enabled": false,
    "action": {
      "type": "Custom",
      "custom_action": "FatigueCheckAction",
      "custom_action_param": {
        "mode": "check_first",
        "threshold": 30
      }
    },
    "next": ["U_IsPreSortieConfirm"],
    "on_error": ["UF_Hub"]
  },
```

（与合战场 S_FatigueCheck 同构；enabled 由 interface.json 选项控制）

- [ ] **Step 2: 修改 U_DragCaptain 的 next**

U_DragCaptain 当前 next 为 [U_IsPreSortieConfirm]，改为 [U_FatigueCheck]：

```bash
python -c "
import json, io
path = 'assets/resource/base/pipeline/Underground.json'
u = json.load(io.open(path, encoding='utf-8'))
u['U_DragCaptain']['next'] = ['U_FatigueCheck']
io.open(path, 'w', encoding='utf-8', newline='').write(json.dumps(u, ensure_ascii=False, indent=4))
print('U_DragCaptain next:', u['U_DragCaptain']['next'])
"
```

- [ ] **Step 3: 校验**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
f = u['U_FatigueCheck']
assert f['action']['custom_action_param'] == {'mode': 'check_first', 'threshold': 30}
assert f['next'] == ['U_IsPreSortieConfirm']
assert f['on_error'] == ['UF_Hub']
assert u['U_DragCaptain']['next'] == ['U_FatigueCheck']
print('入口链正确')
"
```

预期：U_FatigueCheck 配置正确，U_DragCaptain 指向它。

- [ ] **Step 4: Commit**

```bash
git add assets/resource/base/pipeline/Underground.json
git commit -m "feat: 地下城疲劳检测入口链（U_FatigueCheck + U_DragCaptain）"
```

---

### Task 4: interface.json 选项接入

**Files:**
- Modify: `assets/interface.json`
- Test: 校验脚本

**Interfaces:**
- Consumes: Task 1-3 的 node（U_FatigueCheck、UF_Hub）
- Produces: "U_疲劳处理-刷花"case 的 override 更新；"U_换队长"case 确认

- [ ] **Step 1: 更新"U_疲劳处理-刷花"case 的 override**

找到 interface.json 中 "U_疲劳处理" 选项的 "刷花" case，将其 pipeline_override 从 `{"U_FatigueDetect": {"enabled": true}}` 改为：

```json
{
  "U_FatigueCheck": {"enabled": true},
  "U_FatigueDetect": {"enabled": true},
  "UF_Hub": {"enabled": true}
}
```

（参照 S_疲劳处理-刷花 case 的模式）

- [ ] **Step 2: 确认"U_换队长"case**

查看 interface.json 中 "U_换队长" 选项的 override，确认 U_DragCaptain 被启用（若无 override 或未启用，补充 `{"U_DragCaptain": {"enabled": true}}`）。

- [ ] **Step 3: 校验**

```bash
python -c "
import json, io
data = json.load(io.open('assets/interface.json', encoding='utf-8'))
def find(obj, name):
    if isinstance(obj, dict):
        if obj.get('name') == name:
            return obj
        for v in obj.values():
            r = find(v, name)
            if r: return r
    elif isinstance(obj, list):
        for item in obj:
            r = find(item, name)
            if r: return r
    return None
opt = find(data, 'U_疲劳处理')
for case in opt['cases']:
    if case['name'] == '刷花':
        ov = case['pipeline_override']
        print('刷花 override:', json.dumps(ov, ensure_ascii=False))
        assert ov.get('U_FatigueCheck', {}).get('enabled') is True
        assert ov.get('UF_Hub', {}).get('enabled') is True
        print('刷花 case 正确')
opt2 = find(data, 'U_换队长')
print('U_换队长 cases:', json.dumps(opt2.get('cases'), ensure_ascii=False)[:300])
print('interface.json valid')
"
```

预期：刷花 case 启用 U_FatigueCheck + UF_Hub；U_换队长 启用 U_DragCaptain。

- [ ] **Step 4: Commit**

```bash
git add assets/interface.json
git commit -m "feat: 地下城疲劳处理刷花选项接入 UF 状态机"
```

---

### Task 5: 地下城 delay→freeze 迁移

**Files:**
- Modify: `assets/resource/base/pipeline/Underground.json`
- Test: 全文件 delay 扫描脚本

**Interfaces:**
- Consumes: 无
- Produces: 无（行为等价优化）

- [ ] **Step 1: 扫描当前 delay 分布**

```bash
python -c "
import json, io
from collections import Counter
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
pre = Counter(); post = Counter()
for n, node in u.items():
    if node.get('pre_delay'): pre[node['pre_delay']] += 1
    if node.get('post_delay'): post[node['post_delay']] += 1
print('pre:', dict(pre))
print('post:', dict(post))
"
```

记录分布后逐类处理（与合战场模式一致）：

- [ ] **Step 2: 处理 UF_ 系列 delay（已从 SF_ 复制，SF_ 已完成迁移，校验无残留）**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
uf_delay = [(n, u[n].get('pre_delay'), u[n].get('post_delay')) for n in u if n.startswith('UF_') and (u[n].get('pre_delay') or u[n].get('post_delay'))]
print('UF_ 残留 delay:', uf_delay if uf_delay else '无')
"
```

预期：UF_ 系列无残留 delay（SF_ 复制时已含 freeze 配置）。

- [ ] **Step 3: 处理 U_ 系列 pre_delay 100（批量清除，排除有意保留）**

对 U_ 系列中所有 `"pre_delay": 100` 的 node 移除该字段（点击/DoNothing 前等待无收益）。注意排除：枢纽 U_DetectWhereAmI 的 pre_delay 0（无）；U_WaitRefresh 类有意保留（如存在 pre）。

```bash
python -c "
import json, io
path = 'assets/resource/base/pipeline/Underground.json'
u = json.load(io.open(path, encoding='utf-8'))
removed = []
for n, node in u.items():
    if node.get('pre_delay') == 100:
        del node['pre_delay']
        removed.append(n)
io.open(path, 'w', encoding='utf-8', newline='').write(json.dumps(u, ensure_ascii=False, indent=4))
print('移除 pre_delay 100 的 node:', len(removed))
"
```

- [ ] **Step 4: 处理 U_ 系列 post_delay → post_wait_freezes**

对每个带 post_delay 的 U_ node 按场景转换（与合战场规则一致）：点击后画面变化 → freeze；无变化 → 移除。转换值参照合战场同构 node（如 U_ClickSortieNow 参照 S_ClickSortieNow 的 freeze 配置 [1156,584,96,47] 100ms）。逐 node 确认后脚本替换。

```bash
# 示例：U_ClickSortieNow（出阵确认，参照 S_ClickSortieNow）
python -c "
import json, io
path = 'assets/resource/base/pipeline/Underground.json'
u = json.load(io.open(path, encoding='utf-8'))
# 逐 node 处理，此处为 U_ClickSortieNow 示例
n = u['U_ClickSortieNow']
if n.get('post_delay'):
    n['post_wait_freezes'] = {'time': 100, 'target': [1156, 584, 96, 47]}
    del n['post_delay']
io.open(path, 'w', encoding='utf-8', newline='').write(json.dumps(u, ensure_ascii=False, indent=4))
print('U_ClickSortieNow:', json.dumps(n.get('post_wait_freezes')))
"
```

（其余 post_delay node 逐个按场景处理，配置参照合战场同构 node 的 freeze 值：
- U_ClickFormation1/2 → 参照 S_ClickFormation1/2（已移除 delay，无 freeze）
- U_ClickCaptain1-4/U_ClickCaptainSlot → 参照 S_ClickCaptain1-4/Slot（全屏或点击位置 100ms）
- U_IsGameIcon/IsLoginButton → 参照 S 版（已移除，无 freeze）
- U_IsGameUpdatePopup/IsInGameUpdatePopup → 参照 S 版（已移除）
- U_CheckEquipmentPopup → 参照 S_CheckEquipmentPopup（target 弹窗 ROI [423,484,145,56]，100ms）
- U_ClickRegion 类 → 参照 S_ClickRegion（全屏 100ms）
- 无对应参照的 node：点击后画面变化 → 全屏 freeze 100ms；画面不变 → 移除）

- [ ] **Step 5: 全文件扫描确认**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
remaining = [(n, u[n].get('pre_delay', 0), u[n].get('post_delay', 0)) for n in u if u[n].get('pre_delay') or u[n].get('post_delay')]
print('剩余 delay:', remaining if remaining else '全部清除（有意保留除外）')
print('JSON valid, node 总数:', len(u))
"
```

预期：仅有意保留的 delay 残留（若有）。

- [ ] **Step 6: Commit**

```bash
git add assets/resource/base/pipeline/Underground.json
git commit -m "refactor: 地下城硬编码等待迁移 wait_freezes"
```

---

### Task 6: 冗余结构清理

**Files:**
- Modify: `assets/resource/base/pipeline/Underground.json`
- Test: 引用扫描脚本

**Interfaces:**
- Consumes: 无
- Produces: 无（删除孤儿 node）

- [ ] **Step 1: 扫描无外部引用的 node**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
referenced = set()
for name, node in u.items():
    for refs in (node.get('next') or [], node.get('on_error') or []):
        for r in refs:
            referenced.add(r)
# 入口 node（顶层任务入口）
referenced.add('Underground')
orphans = [n for n in u if n not in referenced and n != 'Underground']
print('孤儿 node:', orphans if orphans else '无')
"
```

- [ ] **Step 2: 人工确认孤儿 node 删除（排除有意保留的入口/配置 node）**

对 Step 1 输出的孤儿 node 列表，逐个执行以下检查后决定删除/保留：

1. 是否被 interface.json 的 pipeline_override 引用（选项启用）→ 保留
2. 是否被其他 pipeline 文件引用（跨文件共享，如 E_ 系列）→ 保留
3. 是否为任务入口 node（顶层 "Underground" 或 interface.json 任务引用）→ 保留
4. 其余孤儿 node → 确认删除

```bash
python -c "
import json, io, glob
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
iface = io.open('assets/interface.json', encoding='utf-8').read()
others = ''
for f in glob.glob('assets/resource/base/pipeline/*.json'):
    if 'Underground' not in f:
        others += io.open(f, encoding='utf-8').read()
orphans = [n for n in u if n not in referenced]  # referenced 来自 Step 1 脚本
for n in orphans:
    if n in iface:
        print(n, '-> 保留（interface.json 引用）')
    elif n in others:
        print(n, '-> 保留（其他 pipeline 引用）')
    else:
        print(n, '-> 确认可删除')
"
```

（Step 1 脚本需在内存中保留 referenced 集合，或将孤儿列表输出后手工填入；执行时直接在同一个 python 进程中完成两步）

- [ ] **Step 3: 删除确认的孤儿 node**

```bash
python -c "
import json, io
path = 'assets/resource/base/pipeline/Underground.json'
u = json.load(io.open(path, encoding='utf-8'))
to_delete = [n for n in u if n 满足 Step 2 确认删除条件]  # 执行时列出具体 node 名
for n in to_delete:
    del u[n]
io.open(path, 'w', encoding='utf-8', newline='').write(json.dumps(u, ensure_ascii=False, indent=4))
print('删除', len(to_delete), '个孤儿 node')
"
```

- [ ] **Step 4: 校验 JSON 有效与引用完整**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
print('JSON valid, node 总数:', len(u))
"
```

- [ ] **Step 5: Commit**

```bash
git add assets/resource/base/pipeline/Underground.json
git commit -m "refactor: 地下城冗余结构清理"
```

---

### Task 7: 全量验证

**Files:**
- Test: 校验脚本（只读）

**Interfaces:**
- Consumes: Task 1-6 全部产物

- [ ] **Step 1: 全量语义校验**

```bash
python -c "
import json, io
u = json.load(io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8'))
iface = json.load(io.open('assets/interface.json', encoding='utf-8'))
s = json.load(io.open('assets/resource/base/pipeline/Sortie.json', encoding='utf-8'))
# 1. UF_ 与 SF_ 对应 node 的识别参数一致性
import itertools
sf_names = sorted([n for n in s if n.startswith('SF_')])
uf_names = sorted([n for n in u if n.startswith('UF_')])
print('SF_/UF_ 数量:', len(sf_names), '/', len(uf_names))
mismatch = []
for sf in sf_names:
    uf = 'UF_' + sf[3:]
    if uf not in u:
        mismatch.append((sf, 'UF 缺失'))
        continue
    if s[sf].get('recognition') != u[uf].get('recognition'):
        mismatch.append((sf, 'recognition 不一致'))
    if s[sf].get('action', {}).get('param') != u[uf].get('action', {}).get('param'):
        mismatch.append((sf, 'action param 不一致'))
print('不一致:', mismatch if mismatch else '无')
# 2. 引用完整性
missing = []
for name, node in u.items():
    for refs in (node.get('next') or [], node.get('on_error') or []):
        for r in refs:
            if r not in u:
                missing.append((name, r))
print('缺失引用:', missing if missing else '无')
# 3. on_error 覆盖检查
no_oe = [n for n, node in u.items() if 'on_error' not in node and node.get('recognition')]
print('无 on_error 的识别 node:', no_oe if no_oe else '无')
# 4. 无 target_offset
content = io.open('assets/resource/base/pipeline/Underground.json', encoding='utf-8').read()
print('target_offset 出现次数:', content.count('target_offset'))
print('ALL CHECKS DONE')
"
```

预期：UF_ 与 SF_ 识别参数一致、无缺失引用、无 on_error 缺失、无 target_offset。

- [ ] **Step 2: 实机验证清单（人工执行）**

1. U_疲劳处理-刷花 + U_换队长 开启：部队选择页疲劳 <30 → UF 导航合战场 1-1 → 刷花循环（行军/战斗/掉落）→ 疲劳满 → 回地下城继续
2. U_疲劳处理-停止：疲劳 <30 → 回本丸停止（原行为不变）
3. U_刀装破坏处理各 case：停止/补充刀装/刀装保护/补充+保护 行为正确
4. 对照日志：无短间隔重复点击、无卡死循环

- [ ] **Step 3: 更新开发日志**

在 `docs/开发日志.md` 末尾追加本次地下城优化条目（按既有格式：分节 + 明细 + 验证）。

- [ ] **Step 4: Commit**

```bash
git add docs/开发日志.md
git commit -m "docs: 地下城刷花状态机与流水线优化开发日志"
```
