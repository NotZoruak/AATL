param(
    [switch]$DryRun
)

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent

$Targets = @(
    "$Root\_src\bin",
    "$Root\_src\MFAAvalonia\bin",
    "$Root\_src\MFAAvalonia\obj",
    "$Root\_src\SukiUI\bin",
    "$Root\_src\SukiUI\obj",
    "$Root\_src\Markdown.Avalonia\bin",
    "$Root\_src\Markdown.Avalonia\obj",
    "$Root\_src\Markdown.Avalonia.SyntaxHigh\bin",
    "$Root\_src\Markdown.Avalonia.SyntaxHigh\obj",
    "$Root\_src\Markdown.Avalonia.Html\bin",
    "$Root\_src\Markdown.Avalonia.Html\obj",
    "$Root\_src\Markdown.Avalonia.Tight\bin",
    "$Root\_src\Markdown.Avalonia.Tight\obj",
    "$Root\_src\Markdown.Avalonia.Svg\bin",
    "$Root\_src\Markdown.Avalonia.Svg\obj",
    "$Root\_src\ColorTextBlock.Avalonia\bin",
    "$Root\_src\ColorTextBlock.Avalonia\obj",
    "$Root\_src\ColorDocument.Avalonia\bin",
    "$Root\_src\ColorDocument.Avalonia\obj"
)

$TotalSize = 0

foreach ($T in $Targets) {
    if (Test-Path $T) {
        $Size = (Get-ChildItem $T -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $SizeMB = [math]::Round($Size / 1MB, 1)
        $TotalSize += $Size

        if ($DryRun) {
            Write-Host "[DRY-RUN] 将删除: $T ($SizeMB MB)" -ForegroundColor Yellow
        } else {
            Write-Host "删除: $T ($SizeMB MB)"
            Remove-Item -Recurse -Force $T
        }
    }
}

$TotalMB = [math]::Round($TotalSize / 1MB, 1)
if ($DryRun) {
    Write-Host "`n[DRY-RUN] 预计释放: $TotalMB MB" -ForegroundColor Cyan
} else {
    Write-Host "`n已释放: $TotalMB MB" -ForegroundColor Green
}
