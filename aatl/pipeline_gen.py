"""根据 GUI 队伍配置生成远征 pipeline 坐标（嵌套格式）"""

import json
import random
from pathlib import Path

PROJECT = Path(__file__).parent.parent
PIPE_PATH = PROJECT / "resource" / "base" / "pipeline" / "Expedition.json"
CFG_PATH = PROJECT / "config" / "instances" / "default.json"

BIG_MAP = {1: [276, 188, 37, 39], 2: [469, 178, 50, 40], 3: [661, 183, 50, 35], 4: [891, 190, 50, 36], 5: [1078, 191, 45, 34]}
SMALL_MAP = {1: [173, 365, 97, 96], 2: [487, 373, 84, 113], 3: [767, 370, 81, 115], 4: [1098, 368, 67, 79]}
TEAM_BTN = {1: [154, 93, 1, 1], 2: [282, 94, 1, 1], 3: [398, 91, 1, 1], 4: [519, 89, 1, 1], 5: [635, 91, 1, 1]}
CN = ["一", "二", "三", "四", "五"]


def random_pt(roi: list, offset_x=6, offset_y=6) -> list:
    x, y, w, h = roi
    cx = x + random.randint(-offset_x, offset_x) if w <= 1 else x + random.randint(offset_x, w - offset_x)
    cy = y + random.randint(-offset_y, offset_y) if h <= 1 else y + random.randint(offset_y, h - offset_y)
    return [cx, cy, 1, 1]


def parse_map(s: str):
    if not s or s == "休息":
        return None
    try:
        b, sm = s.split("-")
        return int(b), int(sm)
    except (ValueError, AttributeError):
        return None


def generate():
    with open(CFG_PATH, encoding="utf-8") as f:
        config = json.load(f)

    interval_minutes = 1  # 默认
    team_maps = {}
    for item in config.get("TaskItems", []):
        if item.get("entry") != "Expedition":
            continue
        for opt in item.get("option", []):
            name = opt.get("name", "")
            for n, cn in enumerate(CN, 1):
                if cn in name:
                    team_maps[n] = None
                    idx = opt.get("index", 0)
                    # 从 interface.json 读取 cases
                    iface_path = PROJECT / "interface.json"
                    with open(iface_path, encoding="utf-8") as f2:
                        iface = json.load(f2)
                    cases = iface.get("option", {}).get(name, {}).get("cases", [])
                    if cases and 0 <= idx < len(cases):
                        team_maps[n] = cases[idx]["name"]
                    break
            if opt.get("name") == "RefreshInterval":
                data = opt.get("data", {})
                try:
                    interval_minutes = int(data.get("minutes", 1))
                except (ValueError, TypeError):
                    interval_minutes = 1
        break

    with open(PIPE_PATH, encoding="utf-8") as f:
        pipe = json.load(f)

    for i in range(1, 6):
        map_str = team_maps.get(i, "休息")
        parsed = parse_map(map_str)

        select_key = f"E_SelectTeam{i}"
        map_key = f"E_SelectMap{i}"
        small_key = f"E_ClickSmallMap{i}"
        btn_key = f"E_SelectTeamBtn{i}"

        if parsed is None:
            # 休息：禁用 CheckTeam 节点，引擎自动跳过
            check_key = f"E_CheckTeam{i}"
            pipe[check_key]["enabled"] = False
            pipe[select_key]["action"]["type"] = "DoNothing"
            pipe[select_key]["next"] = []
            print(f"  部队{i}: 休息 → enabled=false")
        else:
            b, s = parsed
            check_key = f"E_CheckTeam{i}"
            pipe[check_key]["enabled"] = True
            pipe[check_key]["next"] = [f"E_SelectTeam{i}"]
            pipe[select_key]["next"] = [f"E_VerifyScreen{i}"]
            pipe[map_key]["action"]["param"]["target"] = random_pt(BIG_MAP[b])
            pipe[small_key]["action"]["param"]["target"] = random_pt(SMALL_MAP[s])
            print(f"  部队{i}: {map_str} → 大{b} 小{s}")

        pipe[btn_key]["action"]["param"]["target"] = random_pt(TEAM_BTN[i], offset_x=8, offset_y=0)
        print(f"          按钮: {pipe[btn_key]['action']['param']['target']}")

    delay_ms = interval_minutes * 60000
    pipe["E_WaitRefresh"]["post_delay"] = delay_ms
    print(f"\n刷新间隔: {interval_minutes} 分钟 ({delay_ms}ms)")

    with open(PIPE_PATH, "w", encoding="utf-8") as f:
        json.dump(pipe, f, ensure_ascii=False, indent=4)

    print("\nPipeline 已更新，重启 GUI 生效。")


if __name__ == "__main__":
    generate()
