param(
    [string]$Version = "v0.12.3-beta.2",
    [string]$PublishDir = ""
)

$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$TempBase = "$Root\_temp_zip"
$TempDir = "$TempBase\MATR"
$Platform = "win-x64"
$ZipFile = "$Root\MATR-$Version-$Platform.zip"
$SourceDir = if ([string]::IsNullOrWhiteSpace($PublishDir)) { $Root } else { (Resolve-Path $PublishDir).Path }

# Clean old temp dir and zip
if (Test-Path $TempBase) { Remove-Item -Recurse -Force $TempBase }
if (Test-Path $ZipFile) { Remove-Item -Force $ZipFile }

# Create temp dirs
New-Item -ItemType Directory -Force -Path "$TempDir\assets" | Out-Null

# Copy files
Copy-Item "$SourceDir\MATR.exe" $TempDir
Copy-Item "$SourceDir\MATR.dll" $TempDir
Copy-Item "$SourceDir\MATR.deps.json" $TempDir
Copy-Item "$SourceDir\MATR.runtimeconfig.json" $TempDir
Copy-Item "$SourceDir\libloader.dll" $TempDir
Copy-Item "$Root\DependencySetup_*.bat" $TempDir
Copy-Item "$Root\README.md" $TempDir
Copy-Item "$Root\LICENSE" $TempDir
Copy-Item "$Root\assets\interface.json" "$TempDir\assets\interface.json"
Copy-Item "$SourceDir\runtimes" -Recurse -Destination "$TempDir\runtimes"
$KeepDirs = @("libs", "plugins", $Platform)
Get-ChildItem "$TempDir\runtimes" -Directory | ForEach-Object {
    if ($KeepDirs -notcontains $_.Name) { Remove-Item -Recurse -Force $_.FullName }
}

# CI 发布目录中的通用库、设备代理和插件位于根目录，统一整理到应用运行时布局。
if (Test-Path "$SourceDir\libs") {
    New-Item -ItemType Directory -Force -Path "$TempDir\runtimes\libs" | Out-Null
    Copy-Item "$SourceDir\libs\*" -Recurse -Destination "$TempDir\runtimes\libs"
}
if (Test-Path "$SourceDir\MaaAgentBinary") {
    New-Item -ItemType Directory -Force -Path "$TempDir\runtimes\libs\MaaAgentBinary" | Out-Null
    Copy-Item "$SourceDir\MaaAgentBinary\*" -Recurse -Destination "$TempDir\runtimes\libs\MaaAgentBinary"
}
if (Test-Path "$SourceDir\plugins") {
    Copy-Item "$SourceDir\plugins" -Recurse -Destination "$TempDir\plugins"
}

# 移除 libs 中与 win-x64/native 重复的原生库（如 libSkiaSharp.dll），
# 运行时实际加载的是 win-x64/native 下的同名文件，libs 中的副本为发布冗余。
Get-ChildItem "$TempDir\runtimes\libs" -File | ForEach-Object {
    if (Test-Path "$TempDir\runtimes\$Platform\native\$($_.Name)") {
        Remove-Item -Force $_.FullName
    }
}

Copy-Item "$Root\assets\resource" -Recurse -Destination "$TempDir\assets\resource"

# 发布包不包含运行时配置和刀帐个人数据；这些文件只存在于开发区的 config/ 中。
if (Test-Path "$TempDir\config") { Remove-Item -Recurse -Force "$TempDir\config" }
if (Test-Path "$TempDir\assets\config") { Remove-Item -Recurse -Force "$TempDir\assets\config" }
if (Test-Path "$TempDir\assets\resource\config") { Remove-Item -Recurse -Force "$TempDir\assets\resource\config" }
if (Test-Path "$TempDir\assets\resource\temp") { Remove-Item -Recurse -Force "$TempDir\assets\resource\temp" }
if (Test-Path "$TempDir\assets\resource\backup") { Remove-Item -Recurse -Force "$TempDir\assets\resource\backup" }
if (Test-Path "$TempDir\assets\resource\base\image\unused") { Remove-Item -Recurse -Force "$TempDir\assets\resource\base\image\unused" }

# Package (compress temp dir contents directly, no wrapper folder)
Compress-Archive -Path "$TempDir\*" -DestinationPath $ZipFile -Force

# Cleanup
Remove-Item -Recurse -Force "$Root\_temp_zip"

Write-Host "打包完成: $ZipFile"
