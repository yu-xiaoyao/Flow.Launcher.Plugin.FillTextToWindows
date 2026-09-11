# 把插件安装到本机 Flow Launcher 的插件目录并重启 Flow Launcher，方便调试。
# 用法：pwsh -File debug.ps1
$ErrorActionPreference = "Stop"

dotnet publish Flow.Launcher.Plugin.FillTextToWindows -c Debug -r win-x64 --no-self-contained

$AppDataFolder = [Environment]::GetFolderPath("ApplicationData")
$pluginRoot = "$AppDataFolder\FlowLauncher\Plugins"
$targetFolder = "$pluginRoot\FillTextToWindows"
$publishFolder = "Flow.Launcher.Plugin.FillTextToWindows\bin\Debug\win-x64\publish"
$flowLauncherExe = "$env:LOCALAPPDATA\FlowLauncher\Flow.Launcher.exe"

if (-not (Test-Path $flowLauncherExe)) {
    Write-Host "Flow.Launcher.exe not found. Please install Flow Launcher first" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishFolder)) {
    Write-Host "publish folder not found: $publishFolder" -ForegroundColor Red
    exit 1
}

Stop-Process -Name "Flow.Launcher" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# 上一次异常退出可能留下 Plugins\publish，一并清掉，避免和下面的复制混在一起
if (Test-Path "$pluginRoot\publish") {
    Remove-Item -Recurse -Force "$pluginRoot\publish"
}

if (Test-Path $targetFolder) {
    Remove-Item -Recurse -Force $targetFolder
}

# 复制 publish 的内容而不是整个目录，省掉一次 rename
New-Item -ItemType Directory -Force -Path $targetFolder | Out-Null
Copy-Item "$publishFolder\*" $targetFolder -Recurse -Force

Write-Host "installed to $targetFolder" -ForegroundColor Green

Start-Sleep -Seconds 1
Start-Process $flowLauncherExe
Write-Host "Flow Launcher restarted" -ForegroundColor Green
