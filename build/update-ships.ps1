param(
    [string]$GamePath = "",
    [string]$Accelerator = "https://ghfast.top/",
    [string]$UnpackExe = ""
)

# 从本地游戏本体刷新内置船名表 Assets\ships.json。
# 发布前运行，使应用内置表始终为最新官方译名。

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root "src\SeaMonkeys.App\Assets\ships.json"
$probe = Join-Path $root "src\SeaMonkeys.Probe\SeaMonkeys.Probe.csproj"

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $GamePath = $env:WOWS_GAME_PATH
}
if ([string]::IsNullOrWhiteSpace($GamePath)) {
    Write-Host "[ships] game path not provided; set -GamePath or WOWS_GAME_PATH" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path -LiteralPath $GamePath)) {
    Write-Host "[ships] game path not found: $GamePath" -ForegroundColor Red
    exit 1
}

# 准备 wowsunpack.exe（缺失时从 GitHub 工具包下载）。
if ([string]::IsNullOrWhiteSpace($UnpackExe)) {
    $UnpackExe = Join-Path $env:TEMP "SeaMonkeys\wowsunpack.exe"
}
if (-not (Test-Path -LiteralPath $UnpackExe)) {
    $toolsZipUrl = "https://github.com/landaire/wows-toolkit/releases/download/v1.0.1/wows_toolkit_tools_v1.0.1_win64.zip"
    if (-not [string]::IsNullOrWhiteSpace($Accelerator)) {
        $toolsZipUrl = "$Accelerator$toolsZipUrl"
    }
    $zip = Join-Path $env:TEMP "SeaMonkeys\wows_toolkit_tools.zip"
    New-Item -ItemType Directory -Path (Split-Path -Parent $zip) -Force | Out-Null
    Write-Host "[ships] downloading wows-toolkit tools..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $toolsZipUrl -OutFile $zip -TimeoutSec 600
    $extract = Join-Path $env:TEMP "SeaMonkeys\wows_toolkit_tools"
    Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
    $found = Get-ChildItem -LiteralPath $extract -Recurse -Filter "wowsunpack.exe" | Select-Object -First 1
    if (-not $found) { Write-Host "[ships] wowsunpack.exe not found in archive" -ForegroundColor Red; exit 1 }
    Copy-Item -LiteralPath $found.FullName -Destination $UnpackExe -Force
}
Write-Host "[ships] wowsunpack: $UnpackExe" -ForegroundColor Cyan

$env:WOWSUNPACK = $UnpackExe
$tmpOut = Join-Path $env:TEMP "SeaMonkeys\ships.new.json"
New-Item -ItemType Directory -Path (Split-Path -Parent $tmpOut) -Force | Out-Null

Write-Host "[ships] building probe..." -ForegroundColor Cyan
dotnet build $probe -c Release -v q | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "[ships] probe build FAILED" -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "[ships] generating ship catalog from game data..." -ForegroundColor Cyan
$probeExe = Join-Path $root "src\SeaMonkeys.Probe\bin\Release\net10.0-windows\SeaMonkeys.Probe.dll"
dotnet $probeExe shipgen "$GamePath" "$tmpOut"
if ($LASTEXITCODE -ne 0) { Write-Host "[ships] generation FAILED" -ForegroundColor Red; exit $LASTEXITCODE }

if (-not (Test-Path -LiteralPath $tmpOut)) {
    Write-Host "[ships] output not produced" -ForegroundColor Red
    exit 1
}

# 与现有表比对，输出概要。
$old = if (Test-Path -LiteralPath $target) { (Get-Content -LiteralPath $target -Raw | ConvertFrom-Json) } else { $null }
$new = Get-Content -LiteralPath $tmpOut -Raw | ConvertFrom-Json
$oldCount = if ($old) { @($old.ships.PSObject.Properties).Count } else { 0 }
$newCount = @($new.ships.PSObject.Properties).Count
Write-Host "[ships] old=$oldCount new=$newCount (version=$($new.version) date=$($new.date))" -ForegroundColor Green

Copy-Item -LiteralPath $tmpOut -Destination $target -Force
Write-Host "[ships] updated $target" -ForegroundColor Green
