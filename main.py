"""AATL — 刀剑乱舞自动化助手

启动 AgentServer 模式，处理 JSON 流水线无法表达的复杂逻辑（如远征动态路由）。
"""

import logging
from pathlib import Path

from aatl.expedition import load_team_config, get_map_click_points
from aatl.agent import ExpeditionAgent

PROJECT_DIR = Path(__file__).parent

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
)
logger = logging.getLogger("aatl")


def main():
    logger.info("AATL AgentServer 启动")
    logger.info(f"项目路径: {PROJECT_DIR}")

    # 验证配置
    config = load_team_config()
    for idx in range(1, 6):
        map_str = config.get(idx, "（未配置）")
        if map_str and map_str != "休息":
            pts = get_map_click_points(map_str)
            logger.info(f"  部队{idx}: {map_str} → {pts}")
        else:
            logger.info(f"  部队{idx}: 休息")

    logger.info("AgentServer 就绪，等待 MaaFramework 连接...")

    # 初始化 Agent
    agent = ExpeditionAgent()

    # TODO: 注册 Agent 到 MaaFramework 并启动事件循环
    # 需要调用 maafw 的 Resource/Tasker API 来注册 custom_action 回调
    logger.info("提示: 自定义动作 DispatchExpedition / SelectTeamForDispatch 已就绪")
    logger.info("Agent 回调方法: agent.handle(action_name, param, maa_api)")


if __name__ == "__main__":
    main()
