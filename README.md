# AATL — 刀剑乱舞自动化助手

基于 [MaaFramework](https://github.com/MaaXYZ/MaaFramework) 的《刀剑乱舞》自动化工具，运行于 MuMu 模拟器。

## 功能

- **远征** — 自动收发 1~5 队远征，支持 20 张地图自由配置，休息队伍自动跳过
- 更多任务开发中（合战场、地下城、联队战...）

## 环境

- [.NET Desktop Runtime 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Python 3.11+](https://www.python.org/)（仅开发/构建用）
- [MuMu 模拟器 12](https://mumu.163.com/)

## 快速开始

1. 启动 MuMu 模拟器，进入《刀剑乱舞》
2. 双击 `AATL.lnk` 启动 GUI
3. 左侧导航选「主页」，点击连接目标下拉框选中 MuMu 设备
4. 在远征任务的设置面板中，为每个部队选择远征地图（或休息）
5. 勾选「远征」，点击「开始任务」

## 修改队伍配置后

GUI 里改完队伍地图 → 保存 → 关 GUI → 执行：

```bash
venv\Scripts\python aatl\pipeline_gen.py
```

再打开 GUI 即可生效。

## 从源码构建 GUI

依赖 .NET 10.0 SDK，安装后执行：

```bash
dotnet publish _src/MFAAvalonia.Desktop/MFAAvalonia.Desktop.csproj -c Release -o GUI
```

## 目录结构

```
AATL/
├── AATL.lnk              ← 启动快捷方式
├── GUI/                  ← 桌面 GUI
│   ├── interface.json    ← 任务、资源、选项配置
│   ├── config/           ← 用户配置（自动生成）
│   ├── resource/         ← OCR 模型、模板图片、流水线
│   └── AATL.ico          ← 自定义图标
├── MFAToolsPlus/         ← MFA 开发工具箱
├── _src/                 ← GUI 源码
├── aatl/                 ← Python 工具
│   ├── pipeline_gen.py   ← 根据配置生成远征流水线
│   └── expedition.py     ← 远征地图坐标常量
└── venv/                 ← Python 虚拟环境
```

## 流水线调试

用 MFAToolsPlus 连接模拟器后，可以：
- ROI 模式框选区域获取坐标
- 截图保存模板图片到 `resource/base/image/`
- 测试 OCR / 模板匹配的命中效果
