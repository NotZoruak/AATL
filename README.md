<!-- markdownlint-disable -->

<div align="center">

<img alt="LOGO" src="./assets/resource/logo/MATR.png" width="256" />

# MATR — 刀剑乱舞自动化助手

<br>
<p align="center">
    <img src="https://img.shields.io/badge/Platform-Windows-0078D7?style=flat-square&logo=Windows" alt="Platform" />
    <img src="https://img.shields.io/badge/Language-C%23%20%2F%20Pipeline-%23239120?style=flat-square&logo=csharp" alt="Language" />
    <img alt="license" src="https://img.shields.io/github/license/NotZoruak/MATR?style=flat-square" />
    <a href="https://github.com/MaaXYZ/MaaFramework" target="_blank"><img alt="MaaFramework" src="https://raw.githubusercontent.com/MaaXYZ/MaaFramework/refs/heads/main/docs/static/maafw.svg" /></a>
    <br/>
    <img alt="stars" src="https://img.shields.io/github/stars/NotZoruak/MATR?style=flat-square&logo=github&color=darkgreen" />
    <img alt="downloads" src="https://img.shields.io/github/downloads/NotZoruak/MATR/total?style=flat-square&logo=github&color=darkgreen" />
</p>
<br>

<!-- markdownlint-restore -->

刀剑乱舞 PC 端自动化工具，通过 ADB 连接模拟器运行《刀剑乱舞》，由 MaaFramework 强力驱动！推荐使用 MuMu 模拟器 12，分辨率 1280×720。其他支持 ADB 的模拟器及分辨率亦可运行。

</div>

> [!Tip]
>
> 遇到问题请先前往 [Issues 页面](https://github.com/NotZoruak/MATR/issues) 搜索已有解答，或通过下方反馈渠道提交。

## ⚠️ 免责声明与风险提示

> [!Note]
>
> 本项目基于 [MaaFramework](https://github.com/MaaXYZ/MaaFramework)（LGPL v3）构建，采用 [GNU General Public License v3.0](LICENSE) 协议，**永久免费且开源**。
> - 本项目为非计算机专业人员心血来潮之作，大量依赖 vibe coding 完成，仅供学习交流使用。
>
> 本软件为第三方工具，通过识别游戏画面模拟常规交互动作，简化《刀剑乱舞-ONLINE-》的重复性操作。本项目遵循相关法律法规，绝不会修改任何游戏文件或数据。
>
> 因使用本软件而产生的任何问题，均与本项目及开发者无关。
> - 请勿在任何平台的《刀剑乱舞-ONLINE-》官方账号下提及 MATR。

> [!Caution]
>
> 根据游族网络《刀剑乱舞-ONLINE-》用户许可协议，严禁使用任何形式的妨碍游戏公平性辅助工具或程序（外挂）。官方已多次对违规账号采取封禁措施，包括但不限于封号警告、永久封禁等处罚。
>
> **您应充分了解并自愿承担使用本工具可能带来的所有风险，包括账号封禁、数据丢失等。**

## 快速开始

### 1. 下载

前往 [Releases](https://github.com/NotZoruak/MATR/releases) 页面，在最新版本下方点击「Assets」展开文件列表，下载命名为 `MATR-vx.x.x.zip` 的文件，解压到空目录。

> 已安装的用户可通过软件内设置面板检查更新，支持 GitHub 和 [Mirror酱](https://mirrorchyan.com) 双下载源切换（Mirror酱 需购买 CDK 激活）。
>
> 注意：Release 基于 x86_64 架构，Windows 系统。Arm 架构（如苹果 M 系列芯片、树莓派等）、Mac 系统、Linux 系统暂不支持。

### 2. 启动

双击 `matr.exe` 即可运行。首次启动耗时可能较长，请耐心等待。

> 启动时若弹出 ".NET Desktop Runtime 10.0" 或 "VCRUNTIME140.dll" 等系统错误提示，说明缺少运行依赖。右键 `DependencySetup_依赖库安装_win.bat` → **以管理员身份运行**，安装完成后重新启动 MATR。

## 功能介绍

**远征** — 自动检测空闲部队并派往指定地图，到期后收取奖励，循环执行。支持远征智能调度（按刷新间隔动态检查）、长期远征计划、多部队同步。

**合战场** — 通用出阵自动化，无限循环。支持自选时代、地域、部队和阵形，可选换队长、补充刀装、王点前撤退、道中撤退、重伤修刀/停止、同步远征。

**地下城** — 大阪地下城活动自动化，可选目标层数、换队长、补充刀装、每轮回本丸（配合远征）、动画跳过。

**战术强化训练** — 战术强化训练活动自动化，支持部队交替、换队长、难度选择。

**陆联** — 联队战活动自动化，支持部队交替、换队长、自动购买通行令牌。

**刷花** — 部队一单骑出阵 1-1 自动循环，快速提升刀剑男士疲劳度。

**刀解** — 勾选刀种后自动筛选并解体，自动收取邮箱。

**习合** — 自动刀剑习合，支持搓糖（乱舞 7 级跳过）和刀种筛选。

**卡死重启** — 任务卡死时自动重启模拟器实例与游戏进程，恢复后继续执行。需在全局设置中开启。

---

**限锻计算** — 输入锻刀公式、现有资源及道具，计算达到目标积分所需剩余资源，支持 OCR 一键识别。

**数据查找表** — 极化刀剑男士升级经验表、各地图远征收益对比。

## 反馈与建议

遇到 bug 或有功能建议？欢迎通过以下方式反馈：

- **GitHub Issues**：前往 [Issues 页面](https://github.com/NotZoruak/MATR/issues) 提交，请尽量包含以下信息方便快速定位
- QQ频道：pd68335487

提交反馈时请尽量包含以下信息：

- **Bug 报告**：描述操作步骤、预期结果和实际结果，附上截图或日志（`debug/` 目录下）
- **功能建议**：描述期望的功能场景和使用目的

> 提交前请先搜索已有 issue，避免重复。

## 目录结构

```
MATR/
├── matr.exe                              ← 桌面启动入口
├── appsettings.json                      ← 应用配置
├── assets/
│   ├── interface.json                    ← 任务与选项配置
│   └── resource/
│       ├── base/
│       │   ├── pipeline/                 ← 自动化任务流水线（JSON）
│       │   │   ├── Sortie.json           ← 合战场
│       │   │   ├── Underground.json      ← 地下城
│       │   │   ├── Expedition.json       ← 远征
│       │   │   ├── LRentaisen.json       ← 陆联（联队战）
│       │   │   ├── TacticalTraining.json ← 战术强化训练
│       │   │   ├── FlowerBrush.json      ← 刷花
│       │   │   ├── Disassemble.json      ← 刀解
│       │   │   ├── Mix.json              ← 习合
│       │   │   └── GoHome.json           ← 回到本丸
│       │   ├── image/                    ← 模板匹配图片
│       │   ├── custom/                   ← 自定义动作脚本
│       │   └── model/ocr/                ← OCR 模型
│       ├── logo/                         ← 程序图标
│       ├── silhouette/                   ← 剪影识别样板
│       └── announcement/                 ← 公告
├── _src/                                 ← C# 源代码
├── docs/                                 ← 项目文档
├── tools/                                ← 构建/发布脚本
├── runtimes/                             ← .NET 运行时库
├── .github/                              ← CI/CD workflows
│   └── workflows/
│       ├── mirrorchyan_release.yml       ← 发布时上传 Mirror酱
│       └── check.yml                     ← PR 检查
└── LICENSE
```

## 致谢

### 开源项目

- [MaaFramework](https://github.com/MaaXYZ/MaaFramework) — 基于图像识别的自动化黑盒测试框架
- [MFAAvalonia](https://github.com/SweetSmellFox/MFAAvalonia) — 基于 Avalonia UI 的 MaaFramework 通用 GUI 解决方案
- [Mirror酱](https://mirrorchyan.com) — 软件分发与 CDK 激活管理平台
- [MaaAssistantArknights](https://github.com/MaaAssistantArknights/MaaAssistantArknights) — 《明日方舟》小助手，全日常一键长草
- [MFAToolsPlus](https://github.com/SweetSmellFox/MFAToolsPlus) — MaaFramework 新一代开发辅助工具箱
- [MaaLogAnalyzer](https://github.com/MaaXYZ/MaaLogAnalyzer) — 可视化日志分析工具，告别手翻百万行日志

### 开发者

感谢以下开发者对 MATR 的贡献：

[![贡献者](https://contrib.rocks/image?repo=NotZoruak/MATR&max=1000)](https://github.com/NotZoruak/MATR/graphs/contributors)

## Star History

如果觉得软件对你有帮助，帮忙点个 Star 吧！（网页最上方右上角的小星星），这就是对我们最大的支持了！

<a href="https://star-history.com/#NotZoruak/MATR&Date">
  <img alt="Star History Chart" src="star-history.png" />
</a>