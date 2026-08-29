param(
    [string]$RuntimeIdentifier = "osx-arm64"
)

$ErrorActionPreference = "Stop"

function Get-MsBuildItems {
    param(
        [string]$Project,
        [string]$ItemName
    )

    $result = dotnet msbuild $Project "-p:RuntimeIdentifier=$RuntimeIdentifier" "-p:MATR_TARGET_RID=$RuntimeIdentifier" "-getItem:$ItemName" -nologo
    if ($LASTEXITCODE -ne 0) {
        throw "无法读取 $Project 的 $ItemName 项。"
    }

    return ($result -join "`n")
}

$markdownReferences = Get-MsBuildItems "_src/Markdown.Avalonia/Markdown.Avalonia.csproj" "ProjectReference"
if ($markdownReferences -match "Markdown\.Avalonia\.Math") {
    throw "macOS 构建不应引用 Markdown.Avalonia.Math。"
}

$mathPackages = Get-MsBuildItems "_src/Markdown.Avalonia.Math/Markdown.Avalonia.Math.csproj" "PackageReference"
if ($mathPackages -match "Sylinko\.CSharpMath\.Avalonia") {
    throw "macOS 构建不应还原 Sylinko.CSharpMath.Avalonia。"
}

$coreResources = Get-MsBuildItems "_src/MFAAvalonia/MFAAvalonia.csproj" "AvaloniaResource"
if ($coreResources -match "MdXaml\.axaml") {
    throw "macOS 构建不应编译带 MathPlugin 的 MdXaml.axaml。"
}

$desktopProject = Get-Content -Raw (Join-Path $PSScriptRoot "../_src/MFAAvalonia.Desktop/MFAAvalonia.Desktop.csproj")
if ($desktopProject -notmatch 'nulastudio\.NetBeauty' -or $desktopProject -notmatch 'MATR_TARGET_RID') {
    throw "macOS 构建不应还原 NetBeauty。"
}

$coreProject = Get-Content -Raw (Join-Path $PSScriptRoot "../_src/MFAAvalonia/MFAAvalonia.csproj")
if ($coreProject -notmatch 'Markdown\.Avalonia\.csproj" AdditionalProperties="MATR_TARGET_RID=\$\(MATR_TARGET_RID\)"') {
    throw "MFAAvalonia 必须将运行时标识传递给 Markdown 项目。"
}

Write-Host "macOS 数学插件排除配置验证通过。"
