param(
    [string]$Version = "v0.5.4"
)

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$TempDir = "$Root\_temp_zip\AATL"
$ZipFile = "$Root\AATL-$Version.zip"

# Clean old temp dir and zip
if (Test-Path $TempDir) { Remove-Item -Recurse -Force $TempDir }
if (Test-Path $ZipFile) { Remove-Item -Force $ZipFile }

# Create temp dir
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null

# Copy files
Copy-Item "$Root\AATL.exe" $TempDir
Copy-Item "$Root\AATL.dll" $TempDir
Copy-Item "$Root\AATL.deps.json" $TempDir
Copy-Item "$Root\AATL.runtimeconfig.json" $TempDir
Copy-Item "$Root\libloader.dll" $TempDir
Copy-Item "$Root\DependencySetup_*.bat" $TempDir
Copy-Item "$Root\README.md" $TempDir
Copy-Item "$Root\LICENSE" $TempDir
Copy-Item "$Root\runtimes" -Recurse -Destination "$TempDir\runtimes"

# Keep only win-x64 native libs and managed DLLs
$KeepDirs = @("libs", "plugins", "win-x64")
Get-ChildItem "$TempDir\runtimes" -Directory | ForEach-Object {
    if ($KeepDirs -notcontains $_.Name) { Remove-Item -Recurse -Force $_.FullName }
}

Copy-Item "$Root\resource" -Recurse -Destination "$TempDir\resource"

# Remove user runtime data
if (Test-Path "$TempDir\resource\config") { Remove-Item -Recurse -Force "$TempDir\resource\config" }
if (Test-Path "$TempDir\resource\temp") { Remove-Item -Recurse -Force "$TempDir\resource\temp" }
if (Test-Path "$TempDir\resource\backup") { Remove-Item -Recurse -Force "$TempDir\resource\backup" }

# Package
Compress-Archive -Path $TempDir -DestinationPath $ZipFile -Force

# Cleanup
Remove-Item -Recurse -Force "$Root\_temp_zip"

Write-Host "打包完成: $ZipFile"
