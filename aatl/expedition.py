"""远征任务控制模块

通过 MaaFramework AgentServer 处理远征的复杂逻辑：
动态读取队伍配置、计算地图坐标、生成随机点击。
"""

import json
import random
from pathlib import Path

# ── 远征地图坐标 ─────────────────────────────────────────────

# 五个大地图（时代）的点击范围 [x, y, w, h]
BIG_MAP_ROI = {
    1: [276, 188, 37, 39],
    2: [469, 178, 50, 40],
    3: [661, 183, 50, 35],
    4: [891, 190, 50, 36],
    5: [1078, 191, 45, 34],
}

# 四个小地图（远征任务）的点击范围 [x, y, w, h]
SMALL_MAP_ROI = {
    1: [173, 365, 97, 96],
    2: [487, 373, 84, 113],
    3: [767, 370, 81, 115],
    4: [1098, 368, 67, 79],
}

# 部队选择按钮 [x, y, w, h]（仅横向随机偏移）
TEAM_BTN_ROI = {
    1: [154, 93, 1, 1],
    2: [282, 94, 1, 1],
    3: [398, 91, 1, 1],
    4: [519, 89, 1, 1],
    5: [635, 91, 1, 1],
}


def random_point(roi: list, offset_range: int = 6, vertical_offset: bool = True) -> tuple:
    """在给定 ROI 范围内生成随机点击坐标。

    如果 w=h=1（精确像素点），则以该点为中心 ±offset_range 随机偏移；
    否则在 ROI 内部随机选点，留出 offset_range 的边距。
    """
    x, y, w, h = roi

    if w <= 1 and h <= 1:
        # 精确像素点：以中心 ±offset_range 随机偏移
        cx = x + random.randint(-offset_range, offset_range)
        cy = y + random.randint(-offset_range, offset_range) if vertical_offset else y
    else:
        cx = x + random.randint(offset_range, w - offset_range)
        cy = y + random.randint(offset_range, h - offset_range) if vertical_offset else y
    return (cx, cy)


def parse_map(map_str: str) -> tuple | None:
    """解析地图字符串 "3-2" → (大图3, 小图2)；"休息" → None"""
    if not map_str or map_str == "休息":
        return None
    try:
        big, small = map_str.split("-")
        return (int(big), int(small))
    except (ValueError, AttributeError):
        return None


def get_map_click_points(map_str: str) -> tuple | None:
    """根据地图字符串获取两个点击坐标：(大图坐标, 小图坐标)，含随机偏移"""
    parsed = parse_map(map_str)
    if parsed is None:
        return None
    big_idx, small_idx = parsed
    if big_idx not in BIG_MAP_ROI or small_idx not in SMALL_MAP_ROI:
        return None
    return (
        random_point(BIG_MAP_ROI[big_idx]),
        random_point(SMALL_MAP_ROI[small_idx]),
    )


def load_team_config(config_path: Path = None) -> dict:
    """从实例配置读取各队伍的远征地图设置 {team_index: map_str}"""
    if config_path is None:
        config_path = Path(__file__).parent.parent / "GUI" / "config" / "instances" / "default.json"

    with open(config_path, encoding="utf-8") as f:
        config = json.load(f)

    team_maps = {}
    for item in config.get("TaskItems", []):
        if item.get("entry") != "Expedition":
            continue
        for opt in item.get("option", []):
            name = opt.get("name", "")
            if name.startswith("部队"):
                idx = _team_name_to_index(name)
                if idx is None:
                    continue
                selected_idx = opt.get("index", 0)
                cases = _get_option_cases(name)
                if cases and 0 <= selected_idx < len(cases):
                    team_maps[idx] = cases[selected_idx]["name"]
        break

    return team_maps


def _team_name_to_index(name: str) -> int | None:
    """部队一 → 1, 部队二 → 2, ..."""
    mapping = {"一": 1, "二": 2, "三": 3, "四": 4, "五": 5}
    for cn, idx in mapping.items():
        if cn in name:
            return idx
    return None


def _get_option_cases(option_name: str) -> list:
    """读取 interface.json 中某个 option 的 cases 列表"""
    interface_path = Path(__file__).parent.parent / "GUI" / "interface.json"
    with open(interface_path, encoding="utf-8") as f:
        interface = json.load(f)
    option = interface.get("option", {}).get(option_name, {})
    return option.get("cases", [])
