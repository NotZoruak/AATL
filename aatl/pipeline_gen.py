"""根据 GUI 队伍配置生成远征 pipeline

使用方法：在 GUI 里配好队伍目的地 → 保存 → 关 GUI → 运行此脚本 → 重开 GUI
"""

import json
import random
from pathlib import Path

PROJECT = Path(__file__).parent.parent

BIG_MAP = {1: [276, 188, 37, 39], 2: [469, 178, 50, 40], 3: [661, 183, 50, 35], 4: [891, 190, 50, 36], 5: [1078, 191, 45, 34]}
SMALL_MAP = {1: [173, 365, 97, 96], 2: [487, 373, 84, 113], 3: [767, 370, 81, 115], 4: [1098, 368, 67, 79]}
TEAM_BTN = {1: [154, 93, 1, 1], 2: [282, 94, 1, 1], 3: [398, 91, 1, 1], 4: [519, 89, 1, 1], 5: [635, 91, 1, 1]}

CN = ["一", "二", "三", "四", "五"]


def random_pt(roi):
    x, y, w, h = roi
    cx = x + random.randint(-5, 5) if w <= 1 else x + random.randint(6, w - 6)
    cy = y + random.randint(-3, 3) if h <= 1 else y + random.randint(6, h - 6)
    return [cx, cy, 1, 1]


def load_config(instance_path=None):
    if instance_path is None:
        instance_path = PROJECT / "GUI" / "config" / "instances" / "default.json"
    with open(instance_path, encoding="utf-8") as f:
        return json.load(f)


def load_interface():
    with open(PROJECT / "GUI" / "interface.json", encoding="utf-8") as f:
        return json.load(f)


def parse_map(map_str):
    if not map_str or map_str == "休息":
        return None
    try:
        b, s = map_str.split("-")
        return int(b), int(s)
    except (ValueError, AttributeError):
        return None


def generate():
    config = load_config()
    iface = load_interface()

    # 读配置
    team_maps = {}
    for item in config.get("TaskItems", []):
        if item.get("entry") != "Expedition":
            continue
        for opt in item.get("option", []):
            name = opt.get("name", "")
            idx = None
            for n, cn in enumerate(CN, 1):
                if cn in name:
                    idx = n
                    break
            if idx is None:
                continue
            selected = opt.get("index", 0)
            cases = iface.get("option", {}).get(name, {}).get("cases", [])
            if cases and 0 <= selected < len(cases):
                team_maps[idx] = cases[selected]["name"]
        break

    # 加载 pipeline 模板
    pipe_path = PROJECT / "GUI" / "resource" / "base" / "pipeline" / "Expedition.json"
    with open(pipe_path, encoding="utf-8") as f:
        pipe = json.load(f)

    for i in range(1, 6):
        map_str = team_maps.get(i, "休息")
        parsed = parse_map(map_str)

        select_map_key = f"SelectMap{i}"
        small_map_key = f"ClickSmallMap{i}"
        team_btn_key = f"SelectTeamBtn{i}"

        if parsed is None:
            # 休息 → 这队不派遣，SelectTeam 直接跳到下一队检查
            next_team = f"CheckTeam{i+1}" if i < 5 else "AllTeamsBusy"
            pipe[f"SelectTeam{i}"]["next"] = [next_team]
            status = "休息 → 跳过"
        else:
            b, s = parsed
            pipe[f"SelectTeam{i}"]["next"] = [f"VerifyScreen{i}"]
            pipe[select_map_key]["target"] = random_pt(BIG_MAP[b])
            pipe[small_map_key]["target"] = random_pt(SMALL_MAP[s])
            status = f"{map_str} → 大{b} {pipe[select_map_key]['target']} 小{s} {pipe[small_map_key]['target']}"

        pipe[team_btn_key]["target"] = random_pt(TEAM_BTN[i])
        print(f"  部队{i}: {status} | 按钮{pipe[team_btn_key]['target']}")

    with open(pipe_path, "w", encoding="utf-8") as f:
        json.dump(pipe, f, ensure_ascii=False, indent=4)

    print("\nPipeline 已生成。重启 GUI 后配置生效。")


if __name__ == "__main__":
    generate()
