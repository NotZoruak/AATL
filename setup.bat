@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ANSI 颜色
for /f %%a in ('echo prompt $E^| cmd') do set "ESC=%%a"
set "R=%ESC%[0m"
set "G=%ESC%[32m"
set "E=%ESC%[31m"
set "Y=%ESC%[33m"
set "C=%ESC%[36m"
set "B=%ESC%[1m"

echo.
echo %C%  AATL — 刀剑乱舞自动化助手  环境安装%R%
echo %C%  ==============================%R%
echo.

:: Python 检测
echo %B%[1/3]%R% 检测 Python 环境...
where python >nul 2>&1
if %errorlevel% neq 0 (
    echo %E%未找到 Python，请先安装 Python 3.11+ 并勾选 "Add to PATH"%R%
    echo %Y%下载: https://www.python.org/downloads/%R%
    pause
    exit /b 1
)

for /f "tokens=2" %%v in ('python --version 2^>^&1') do set "PYVER=%%v"
echo %G%已找到 Python %PYVER%%R%

:: 创建虚拟环境
echo.
echo %B%[2/3]%R% 创建虚拟环境 venv...
if exist "venv\" (
    echo %Y%venv 已存在，跳过创建%R%
) else (
    python -m venv venv
    if !errorlevel! neq 0 (
        echo %E%创建虚拟环境失败%R%
        pause
        exit /b 1
    )
    echo %G%虚拟环境创建成功%R%
)

:: 安装依赖
echo.
echo %B%[3/3]%R% 安装 Python 依赖...
call venv\Scripts\activate.bat
pip install -r requirements.txt -q
if %errorlevel% neq 0 (
    echo %E%依赖安装失败，请检查网络连接后重试%R%
    pause
    exit /b 1
)
echo %G%依赖安装完成%R%

:: 检查 .NET 运行时
echo.
echo %C%---%R%
reg query "HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost" /v Version >nul 2>&1
if %errorlevel% neq 0 (
    reg query "HKLM\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedhost" /v Version >nul 2>&1
)
if %errorlevel% neq 0 (
    echo %Y%未检测到 .NET Desktop Runtime，请运行 DependencySetup_依赖库安装_win.bat%R%
) else (
    echo %G%.NET Runtime 已安装%R%
)

echo.
echo %G%  AATL 环境准备完毕！双击 AATL.exe 即可启动%R%
echo.
pause
