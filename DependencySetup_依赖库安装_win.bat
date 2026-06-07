@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: 定义ANSI颜色代码
for /f %%a in ('echo prompt $E^| cmd') do set "ESC=%%a"
set "RESET=%ESC%[0m"
set "GREEN=%ESC%[32m"
set "RED=%ESC%[31m"
set "YELLOW=%ESC%[33m"
set "BLUE=%ESC%[34m"
set "CYAN=%ESC%[36m"
set "WHITE=%ESC%[37m"
set "BOLD=%ESC%[1m"

:: 初始化错误标志
set "ErrorOccurred=0"
set "UseWinget=1"

:: 检测系统架构
set "ARCH=%PROCESSOR_ARCHITECTURE%"
if /i "%ARCH%"=="AMD64" (
    set "ARCH_NAME=x64"
    set "VC_PACKAGE=Microsoft.VCRedist.2015+.x64"
    set "VC_DOWNLOAD=https://aka.ms/vs/17/release/vc_redist.x64.exe"
    set "DOTNET_DOWNLOAD=https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
) else if /i "%ARCH%"=="x86" (
    set "ARCH_NAME=x86"
    set "VC_PACKAGE=Microsoft.VCRedist.2015+.x86"
    set "VC_DOWNLOAD=https://aka.ms/vs/17/release/vc_redist.x86.exe"
    set "DOTNET_DOWNLOAD=https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x86.exe"
) else if /i "%ARCH%"=="ARM64" (
    set "ARCH_NAME=ARM64"
    set "VC_PACKAGE=Microsoft.VCRedist.2015+.arm64"
    set "VC_DOWNLOAD=https://aka.ms/vs/17/release/vc_redist.arm64.exe"
    set "DOTNET_DOWNLOAD=https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-arm64.exe"
) else (
    echo %RED%不支持的系统架构: %ARCH%%RESET%
    echo %RED%Unsupported system architecture: %ARCH%%RESET%
    pause
    exit /b 1
)

echo.
echo %CYAN%检测到系统架构 / Detected system architecture: %BOLD%%ARCH_NAME%%RESET%
echo.

:: 检测管理员权限（使用纯 cmd 方式）
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo %YELLOW%====================================================================================================%RESET%
    echo %BOLD%%YELLOW%需要管理员权限！%RESET%
    echo %BOLD%%YELLOW%Administrator privileges required!%RESET%
    echo.
    echo %WHITE%请右键点击此脚本，选择"以管理员身份运行"%RESET%
    echo %WHITE%Please right-click this script and select "Run as administrator"%RESET%
    echo %YELLOW%====================================================================================================%RESET%
    echo.
    pause
    exit /b 1
)

:: 检测 winget 是否可用
where winget >nul 2>&1
if %errorlevel% neq 0 (
    set "UseWinget=0"
    echo %YELLOW%winget 不可用，将使用直接下载方式安装%RESET%
    echo %YELLOW%winget is not available, will use direct download method%RESET%
    echo.
) else (
    :: 进一步测试 winget 是否能正常工作
    winget --version >nul 2>&1
    if !errorlevel! neq 0 (
        set "UseWinget=0"
        echo %YELLOW%winget 存在但无法正常工作，将使用直接下载方式安装%RESET%
        echo %YELLOW%winget exists but not working properly, will use direct download method%RESET%
        echo.
    ) else (
        echo %GREEN%检测到 winget 可用%RESET%
        echo %GREEN%winget is available%RESET%
        echo.
    )
)

:: 设置临时下载目录
set "TEMP_DIR=%TEMP%\MAADependencySetup"
if not exist "%TEMP_DIR%" mkdir "%TEMP_DIR%"

echo.
echo %BLUE%====================================================================================================%RESET%
echo %BOLD%%CYAN%正在安装 Microsoft Visual C++ Redistributable (%ARCH_NAME%)%RESET%
echo %BOLD%%CYAN%Installing Microsoft Visual C++ Redistributable (%ARCH_NAME%)%RESET%
echo %BLUE%====================================================================================================%RESET%
echo.

if %UseWinget%==1 (
    echo %YELLOW%如果是第一次使用 winget，可能会提示接受协议，请输入 Y 并按回车继续%RESET%
    echo %YELLOW%If this is your first time using winget, you may be prompted to accept the terms.%RESET%
    echo %YELLOW%Please enter Y and press Enter to continue.%RESET%
    echo.
    winget install "%VC_PACKAGE%" --override "/repair /passive /norestart" --uninstall-previous --accept-package-agreements --force
    if !errorlevel! neq 0 (
        set "ErrorOccurred=1"
        echo %YELLOW%winget 安装失败，尝试使用直接下载方式...%RESET%
        echo %YELLOW%winget installation failed, trying direct download method...%RESET%
        call :DownloadAndInstallVC
    )
) else (
    call :DownloadAndInstallVC
)

echo.
echo %BLUE%====================================================================================================%RESET%
echo %BOLD%%CYAN%正在安装 .NET Desktop Runtime 10.0 (%ARCH_NAME%)%RESET%
echo %BOLD%%CYAN%Installing .NET Desktop Runtime 10.0 (%ARCH_NAME%)%RESET%
echo %BLUE%====================================================================================================%RESET%
echo.

if %UseWinget%==1 (
    winget install "Microsoft.DotNet.DesktopRuntime.10" --override "/repair /passive /norestart" --uninstall-previous --accept-package-agreements --force
    if !errorlevel! neq 0 (
        set "ErrorOccurred=1"
        echo %YELLOW%winget 安装失败，尝试使用直接下载方式...%RESET%
        echo %YELLOW%winget installation failed, trying direct download method...%RESET%
        call :DownloadAndInstallDotNet
    )
) else (
    call :DownloadAndInstallDotNet
)

:: 清理临时文件
if exist "%TEMP_DIR%" rd /s /q "%TEMP_DIR%" >nul 2>&1

echo.
echo %BLUE%====================================================================================================%RESET%
if %ErrorOccurred% equ 0 (
    echo %BOLD%%GREEN%运行库安装完成，请重启电脑后再次尝试运行 MAA。%RESET%
    echo %BOLD%%GREEN%The runtime library installation is complete. Please restart your computer and try running MAA again.%RESET%
) else (
    echo %BOLD%%RED%运行库安装过程中可能出现错误%RESET%
    echo %BOLD%%RED%Errors may have occurred during runtime library installation%RESET%
    echo.
    echo %YELLOW%如果安装失败，您可以手动将以下两个链接复制到浏览器中打开，下载并安装所需组件。%RESET%
    echo %YELLOW%If installation failed, you can manually copy the following links into your browser to download and install.%RESET%
    echo.
    echo %WHITE%Microsoft Visual C++ Redistributable (%ARCH_NAME%):%RESET%
    echo %CYAN%%VC_DOWNLOAD%%RESET%
    echo.
    echo %WHITE%.NET Desktop Runtime 10.0 (%ARCH_NAME%):%RESET%
    echo %CYAN%%DOTNET_DOWNLOAD%%RESET%
)
echo %BLUE%====================================================================================================%RESET%

pause
exit /b 0

:: ============================================
:: 函数：下载并安装 VC++ Redistributable
:: ============================================
:DownloadAndInstallVC
set "VC_FILE=%TEMP_DIR%\vc_redist.exe"
echo %CYAN%正在下载 VC++ Redistributable...%RESET%
echo %CYAN%Downloading VC++ Redistributable...%RESET%

:: 尝试使用 certutil 下载
certutil -urlcache -split -f "%VC_DOWNLOAD%" "%VC_FILE%" >nul 2>&1
if !errorlevel! neq 0 (
    echo %YELLOW%certutil 下载失败，尝试使用 bitsadmin...%RESET%
    echo %YELLOW%certutil download failed, trying bitsadmin...%RESET%
    bitsadmin /transfer "VCRedist" /priority high "%VC_DOWNLOAD%" "%VC_FILE%" >nul 2>&1
    if !errorlevel! neq 0 (
        echo %RED%下载失败 / Download failed%RESET%
        set "ErrorOccurred=1"
        goto :eof
    )
)

if exist "%VC_FILE%" (
    echo %CYAN%正在安装 VC++ Redistributable...%RESET%
    echo %CYAN%Installing VC++ Redistributable...%RESET%
    "%VC_FILE%" /repair /passive /norestart
    if !errorlevel! neq 0 (
        if !errorlevel! neq 3010 (
            echo %RED%安装失败 / Installation failed%RESET%
            set "ErrorOccurred=1"
        ) else (
            echo %GREEN%安装成功（需要重启）/ Installation successful (restart required)%RESET%
        )
    ) else (
        echo %GREEN%安装成功 / Installation successful%RESET%
    )
    del "%VC_FILE%" >nul 2>&1
) else (
    echo %RED%下载的文件不存在 / Downloaded file not found%RESET%
    set "ErrorOccurred=1"
)
goto :eof

:: ============================================
:: 函数：下载并安装 .NET Desktop Runtime
:: ============================================
:DownloadAndInstallDotNet
set "DOTNET_FILE=%TEMP_DIR%\dotnet_runtime.exe"
echo %CYAN%正在下载 .NET Desktop Runtime...%RESET%
echo %CYAN%Downloading .NET Desktop Runtime...%RESET%

:: 尝试使用 certutil 下载
certutil -urlcache -split -f "%DOTNET_DOWNLOAD%" "%DOTNET_FILE%" >nul 2>&1
if !errorlevel! neq 0 (
    echo %YELLOW%certutil 下载失败，尝试使用 bitsadmin...%RESET%
    echo %YELLOW%certutil download failed, trying bitsadmin...%RESET%
    bitsadmin /transfer "DotNetRuntime" /priority high "%DOTNET_DOWNLOAD%" "%DOTNET_FILE%" >nul 2>&1
    if !errorlevel! neq 0 (
        echo %RED%下载失败 / Download failed%RESET%
        set "ErrorOccurred=1"
        goto :eof
    )
)

if exist "%DOTNET_FILE%" (
    echo %CYAN%正在安装 .NET Desktop Runtime...%RESET%
    echo %CYAN%Installing .NET Desktop Runtime...%RESET%
    "%DOTNET_FILE%" /repair /passive /norestart
    if !errorlevel! neq 0 (
        if !errorlevel! neq 3010 (
            echo %RED%安装失败 / Installation failed%RESET%
            set "ErrorOccurred=1"
        ) else (
            echo %GREEN%安装成功（需要重启）/ Installation successful (restart required)%RESET%
        )
    ) else (
        echo %GREEN%安装成功 / Installation successful%RESET%
    )
    del "%DOTNET_FILE%" >nul 2>&1
) else (
    echo %RED%下载的文件不存在 / Downloaded file not found%RESET%
    set "ErrorOccurred=1"
)
goto :eof
