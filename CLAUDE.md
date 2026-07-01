# AATL — 刀剑乱舞自动化助手

基于 MaaFramework 的刀剑乱舞（Touken Ranbu）自动化项目，运行于 MuMu 模拟器。

## 架构

```
AATL/
├── GUI/                            ← C# GUI 壳（.NET 10.0 + Avalonia UI）
│   ├── GUI.exe                     ← 桌面 GUI 主程序
│   ├── interface.json              ← GUI 资源配置
│   ├── resource/base/              ← MaaFramework 共享资源
│   │   ├── pipeline/               ← 自动化任务流水线（JSON）
│   │   ├── image/                  ← 模板匹配图片
│   │   └── model/ocr/              ← OCR 模型
├── venv/                           ← Python 虚拟环境
├── aatl/                           ← Python 自动化逻辑包
├── main.py                         ← Python 入口（AgentServer 模式）
├── interface.json                  ← AATL 任务配置
└── 启动GUI.bat                     ← 一键启动桌面 GUI
```

- **GUI**: [GUI](https://github.com/MaaXYZ/GUI) v2.12.1 — 跨平台桌面 GUI，基于 Avalonia UI + SukiUI，已集成在 `GUI/` 目录下
- **引擎**: [MaaFramework](https://github.com/MaaXYZ/MaaFramework) v5.10.5（Python）/ v5.10.2（Native，GUI 内置）
- **Python 层**: `maafw` 包（AgentServer 模式，用于 JSON 流水线不足时的自定义逻辑）
- **目标平台**: MuMu 模拟器（ADB 连接 `127.0.0.1:7555` 或 `127.0.0.1:16384`）
- **目标游戏**: 刀剑乱舞（Touken Ranbu）

## 环境

- Python 3.11.9 + venv（`venv/`，已安装 `maafw==5.10.5`）
- GUI 已集成在 `GUI/` 目录下，开箱即用（需安装 .NET 10.0 Desktop Runtime）
- 开发 IDE: PyCharm

## gstack Skills

This project uses gstack (https://github.com/garrytan/gstack) - a collection of
AI engineering workflow skills that turn Claude Code into a virtual development
team.

### Web Browsing
Use the `/browse` skill from gstack for all web browsing tasks. Do NOT use
`mcp__claude-in-chrome__*` tools. The browse skill runs real Chromium and handles
web content properly.

### Available Skills

**Think & Plan (start here):**
- `/office-hours` — YC Office Hours. Six forcing questions that reframe your product before writing code
- `/plan-ceo-review` — CEO-level review: find the 10-star product in the request
- `/plan-eng-review` — Lock architecture, data flow, edge cases, and tests
- `/plan-design-review` — Rate each design dimension 0-10 with improvement guidance
- `/plan-devex-review` — DX review: TTHW, magical moments, friction points
- `/autoplan` — One command runs CEO → design → eng → DX review
- `/design-consultation` — Build a complete design system from scratch

**Implementation & Review:**
- `/review` — Pre-landing PR review. Find bugs that pass CI but break in production
- `/codex` — Second opinion via OpenAI Codex
- `/investigate` — Systematic root-cause debugging
- `/design-review` — Live-site visual audit + fix loop with atomic commits
- `/design-shotgun` — Generate multiple AI design variants, comparison board, iterate
- `/design-html` — Generate production-quality Pretext-native HTML/CSS
- `/devex-review` — Live developer experience audit
- `/qa` — Open a real browser, find bugs, fix them, re-verify
- `/qa-only` — Same as /qa but report only, no code changes
- `/scrape` — Pull data from a web page
- `/skillify` — Codify a successful /scrape flow into a permanent browser-skill

**Release & Deploy:**
- `/ship` — Run tests, review, push, open PR
- `/land-and-deploy` — Merge PR, wait for CI/deploy, verify production health
- `/canary` — Post-deploy monitoring loop
- `/landing-report` — Read-only dashboard for the ship queue
- `/document-release` — Update all docs to match what you just shipped
- `/document-generate` — Generate Diataxis docs from code
- `/setup-deploy` — One-time deploy config detection
- `/benchmark` — Performance regression detection

**Security & Safety:**
- `/cso` — OWASP Top 10 + STRIDE security audit
- `/careful` — Warn before destructive commands
- `/freeze` — Lock edits to one directory
- `/guard` — Activate both careful + freeze
- `/unfreeze` — Remove directory edit restrictions

**Operational:**
- `/context-save` — Save working context (git state, decisions, remaining work)
- `/context-restore` — Resume from a saved context
- `/learn` — Manage what gstack learned across sessions
- `/retro` — Weekly retro with per-person breakdowns
- `/health` — Code quality dashboard
- `/gstack-upgrade` — Update gstack to the latest version

**Browser & Agent:**
- `/browse` — Headless browser: real Chromium, real clicks, ~100ms/command
- `/open-gstack-browser` — Launch the visible GStack Browser
- `/setup-browser-cookies` — Import cookies for authenticated testing
- `/pair-agent` — Pair a remote AI agent with your browser
- `/make-pdf` — Turn any markdown file into a publication-quality PDF

### Usage
When a task calls for code review, QA, planning, or any specialized role,
use the corresponding gstack slash command instead of handling it ad-hoc.
