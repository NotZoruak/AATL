"""标准化 MATR pipeline JSON：统一节点结构形态与键顺序。

处理规则:
- recognition / action 统一为对象形态（{"type": ..., "param": {...}}）
- 节点顶层键按固定顺序重排: enabled, recognition, action, pre_delay, post_delay, timeout, next, on_error
- recognition.param 内键序: roi 恒第一, 其余按识别类型固定顺序
- custom_action_param 内部保持原样（自定义动作参数, 不统一）
- 输出格式与 compress_json.py 一致（2 空格缩进 + 单行压缩规则）

用法: python tools/normalize_pipeline.py [路径...] [--dry-run]
无参数时自动处理 assets/resource/base/pipeline/ 下全部 JSON。
"""
import json
import sys
import glob
import os

from compress_json import clean_empty_param, fmt

# 节点顶层键标准顺序（未列出的键按原相对顺序追加到末尾, 防御未知字段）
TOP_LEVEL_ORDER = ["enabled", "recognition", "action", "pre_delay", "post_delay", "timeout", "next", "on_error"]

# recognition.param 内部键序（按识别类型; 未列出的键追加到末尾）
PARAM_ORDER = {
    "TemplateMatch": ["roi", "template", "green_mask", "threshold"],
    "OCR": ["roi", "expected"],
    "ColorMatch": ["roi", "upper", "lower", "method", "count", "connected"],
}

# 识别参数键全集（旧式写法中这些键直接挂在节点层或 recognition 扁平对象上）
RECOG_PARAM_KEYS = {"roi", "template", "green_mask", "threshold", "expected", "upper", "lower", "method", "count", "connected"}

# action 对象保留在对象层、不进 param 的键
ACTION_TOP_KEYS = {"type", "custom_action", "custom_action_param"}


def reorder_keys(d, order):
    """按 order 重排键, 未列出的键保持原相对顺序追加到末尾。"""
    result = {}
    for key in order:
        if key in d:
            result[key] = d[key]
    for key in d:
        if key not in result:
            result[key] = d[key]
    return result


def reorder_params(rec_type, params):
    """重排 recognition.param 内部键序, roi 恒第一。"""
    if not isinstance(params, dict):
        return params
    order = PARAM_ORDER.get(rec_type, [])
    return reorder_keys(params, order)


def normalize_recognition(node, rec):
    """归一化 recognition: 字符串 / 扁平对象 → {"type", "param"}。"""
    if isinstance(rec, str):
        # 旧式: 识别参数直接挂在节点层
        params = {k: node.pop(k) for k in list(node) if k in RECOG_PARAM_KEYS}
        return {"type": rec, "param": reorder_params(rec, params)}
    if isinstance(rec, dict):
        rec = dict(rec)
        rec_type = rec.get("type")
        if "param" in rec:
            rec["param"] = reorder_params(rec_type, rec["param"])
            return rec
        # 扁平对象: type 与识别参数平铺在同一层
        params = {k: rec.pop(k) for k in list(rec) if k != "type"}
        if params:
            return {"type": rec_type, "param": reorder_params(rec_type, params)}
        return rec
    return rec


def normalize_action(node, act):
    """归一化 action: 字符串 → {"type"}，扁平键归入 param。"""
    if isinstance(act, str):
        result = {"type": act}
        # 旧式: target / custom_action / custom_action_param 直接挂在节点层
        if "target" in node:
            result["param"] = {"target": node.pop("target")}
        if "custom_action" in node:
            result["custom_action"] = node.pop("custom_action")
        if "custom_action_param" in node:
            result["custom_action_param"] = node.pop("custom_action_param")
        return result
    if isinstance(act, dict):
        act = dict(act)
        if "param" in act:
            return act  # action.param 仅 target, 无需重排
        # 扁平键归入 param
        extra = {k: v for k, v in act.items() if k not in ACTION_TOP_KEYS}
        if extra:
            act["param"] = extra
            for key in extra:
                del act[key]
        return act
    return act


def normalize_node(node):
    """归一化单个节点并重排顶层键。"""
    node = dict(node)
    if "recognition" in node:
        node["recognition"] = normalize_recognition(node, node["recognition"])
    if "action" in node:
        node["action"] = normalize_action(node, node["action"])
    return reorder_keys(node, TOP_LEVEL_ORDER)


def diff_summary(old_node, new_node):
    """生成键顺序变化摘要, 用于 dry-run 展示。"""
    old_keys = ",".join(old_node.keys())
    new_keys = ",".join(new_node.keys())
    return f"键序 ({old_keys}) -> ({new_keys})"


def main():
    dry_run = "--dry-run" in sys.argv
    args = [a for a in sys.argv[1:] if a != "--dry-run"]

    if args:
        paths = args
    else:
        project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        paths = sorted(glob.glob(os.path.join(project_root, "assets", "resource", "base", "pipeline", "*.json")))

    if not paths:
        print("错误: 未找到 pipeline JSON 文件, 请检查项目结构或显式传入路径", file=sys.stderr)
        sys.exit(1)

    total_changed = 0
    for path in paths:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)

        changed = []
        new_data = {}
        for name, node in data.items():
            if not isinstance(node, dict):
                new_data[name] = node
                continue
            new_node = normalize_node(node)
            new_data[name] = new_node
            if json.dumps(node, ensure_ascii=False) != json.dumps(new_node, ensure_ascii=False):
                changed.append((name, node, new_node))

        total_changed += len(changed)
        if dry_run:
            print(f"[dry-run] {os.path.basename(path)}: {len(data)} 个节点, {len(changed)} 个有改动")
            for name, old_node, new_node in changed[:3]:
                print(f"  {name}: {diff_summary(old_node, new_node)}")
            continue

        with open(path, "w", encoding="utf-8") as f:
            f.write(fmt(clean_empty_param(new_data)) + "\n")
        print(f"Normalized: {os.path.basename(path)} ({len(changed)} 个节点有改动)")

    if dry_run:
        print(f"共 {total_changed} 个节点将有改动")
    else:
        print(f"完成, 共 {total_changed} 个节点被调整")


if __name__ == "__main__":
    main()
