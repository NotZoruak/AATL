"""MaaFramework AgentServer — 处理自定义动作

远征 Pipeline 中的 DispatchExpedition 和 SelectTeamForDispatch
通过此模块路由到正确的逻辑。
"""

import json
import logging
import time
from pathlib import Path

logger = logging.getLogger("aatl.agent")


class ExpeditionAgent:
    """远征自定义动作处理器"""

    def __init__(self):
        self._last_click_time = 0
        self._click_interval = 0.5  # 点击最小间隔（秒）

    def handle(self, action_name: str, param: dict, maa_api) -> bool:
        """分发自定义动作"""
        if action_name == "DispatchExpedition":
            return self._dispatch_expedition(param, maa_api)
        elif action_name == "SelectTeamForDispatch":
            return self._select_team_for_dispatch(param, maa_api)
        else:
            logger.warning(f"未知自定义动作: {action_name}")
            return False

    def _dispatch_expedition(self, param: dict, maa_api) -> bool:
        """点击大地图和小地图，选择远征目的地"""
        from aatl.expedition import load_team_config, get_map_click_points

        team = param.get("team")
        if team is None:
            logger.error("DispatchExpedition: 缺少 team 参数")
            return False

        config = load_team_config()
        map_str = config.get(team)
        if not map_str:
            logger.info(f"部队{team}: 休息中，跳过")
            return False

        points = get_map_click_points(map_str)
        if points is None:
            logger.error(f"部队{team}: 无效地图 {map_str}")
            return False

        big_point, small_point = points
        logger.info(f"部队{team}: 选择地图 {map_str} → 大地图{big_point} 小地图{small_point}")

        # 点击大地图
        self._click(maa_api, *big_point)
        time.sleep(0.3)

        # 点击小地图
        self._click(maa_api, *small_point)
        time.sleep(0.3)

        return True

    def _select_team_for_dispatch(self, param: dict, maa_api) -> bool:
        """在部队选择页面，点击对应队伍的按钮"""
        from aatl.expedition import TEAM_BTN_ROI, random_point

        team = param.get("team")
        if team is None or team not in TEAM_BTN_ROI:
            logger.error(f"SelectTeamForDispatch: 无效 team 参数: {team}")
            return False

        # 仅横向随机偏移
        roi = TEAM_BTN_ROI[team]
        pt = random_point(roi, offset_range=3, vertical_offset=False)
        logger.info(f"部队{team}: 点击部队按钮 {pt}")

        self._click(maa_api, pt[0], pt[1])
        return True

    def _click(self, maa_api, x: int, y: int):
        """执行点击，确保最小间隔"""
        elapsed = time.time() - self._last_click_time
        if elapsed < self._click_interval:
            time.sleep(self._click_interval - elapsed)

        maa_api.click(int(x), int(y))
        self._last_click_time = time.time()
