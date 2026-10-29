<#
.SYNOPSIS
  每日自动刷新船名表：游戏有新构建时重新生成并上传，无变化时跳过。

.DESCRIPTION
  对比游戏 bin/ 下最新构建号与现有 ships.json 的 version 字段：
  - 一致：直接退出，不做任何生成与上传。
  - 不一致：调用 build/update-ships.ps1 重新生成并同步到 momomi.dmuk.org。

  供 Windows 计划任务调用，日志写入 %LOCALAPPDATA%\SeaMonkeys\logs\。

.EXAMPLE
  pwsh -File build/daily-update-ships.ps1
  pwsh -File build/daily-update-ships.ps1 -Force
#>
param(
    [string]$GamePath = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $root "src\SeaMonkeys.App\Assets\ships.json"
$updateScript = Join-Path $PSScriptRoot "update-ships.ps1"
$logDir = Join-Path $env:LOCALAPPDATA "SeaMonkeys\logs"
$logPath = Join-Path $logDir ("ships-update-{0}.log" -f (Get-Date -Format "yyyyMMdd"))

New-Item -ItemType Directory -Path $logDir -Force | Out-Null

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $line = "{0} [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Level, $Message
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    Write-Host $line
}

function Resolve-GamePath {
    param([string]$Explicit)

    if (-not [string]::IsNullOrWhiteSpace($Explicit) -and (Test-Path -LiteralPath $Explicit)) {
        return $Explicit
    }

    if (-not [string]::IsNullOrWhiteSpace($env:WOWS_GAME_PATH) -and (Test-Path -LiteralPath $env:WOWS_GAME_PATH)) {
        return $env:WOWS_GAME_PATH
    }

    $settingsPath = Join-Path $env:LOCALAPPDATA "SeaMonkeys\settings.json"
    if (Test-Path -LiteralPath $settingsPath) {
        try {
            $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace($settings.GamePath) -and (Test-Path -LiteralPath $settings.GamePath)) {
                return $settings.GamePath
            }
        }
        catch {
        }
    }

    $fallback = "D:\Game\Steam\steamapps\common\World of Warships"
    if (Test-Path -LiteralPath $fallback) {
        return $fallback
    }

    return $null
}

function Get-LatestBuild {
    param([string]$Root)

    $binDir = Join-Path $Root "bin"
    if (-not (Test-Path -LiteralPath $binDir)) {
        return $null
    }

    Get-ChildItem -LiteralPath $binDir -Directory |
        Where-Object { $_.Name -match '^\d+$' } |
        Where-Object {
            Test-Path -LiteralPath (Join-Path $_.FullName "res\texts\zh_sg\LC_MESSAGES\global.mo") -PathType Leaf
        } |
        Sort-Object { [long]$_.Name } -Descending |
        Select-Object -First 1
}

function Remove-OldLogs {
    param([string]$Dir, [int]$KeepDays = 30)

    $cutoff = (Get-Date).AddDays(-$KeepDays)
    Get-ChildItem -LiteralPath $Dir -Filter "ships-update-*.log" -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

Write-Log "=== start (force=$Force) ==="

$game = Resolve-GamePath -Explicit $GamePath
if ([string]::IsNullOrWhiteSpace($game)) {
    Write-Log "game path not found; abort" "ERROR"
    exit 1
}

if (-not (Test-Path -LiteralPath (Join-Path $game "WorldOfWarships.exe")) -and
    -not (Test-Path -LiteralPath (Join-Path $game "Korabli.exe"))) {
    Write-Log "not a game root: $game" "ERROR"
    exit 1
}

$latest = Get-LatestBuild -Root $game
if ($null -eq $latest) {
    Write-Log "no build with zh_sg translations under $game\bin; abort" "ERROR"
    exit 1
}

$latestBuild = $latest.Name
$currentVersion = ""
if (Test-Path -LiteralPath $catalogPath) {
    try {
        $current = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
        $currentVersion = "$($current.version)"
    }
    catch {
        Write-Log "existing catalog unreadable, will regenerate" "WARN"
    }
}

Write-Log "game=$game latestBuild=$latestBuild catalogVersion=$currentVersion"

if (-not $Force -and $currentVersion -eq $latestBuild) {
    Write-Log "already up to date; skip"
    Remove-OldLogs -Dir $logDir
    exit 0
}

Write-Log "build differs; regenerating catalog"

$output = & pwsh -NoProfile -NonInteractive -File $updateScript -GamePath $game 2>&1
$exitCode = $LASTEXITCODE
foreach ($line in $output) {
    Write-Log "$line"
}

if ($exitCode -ne 0) {
    Write-Log "update-ships.ps1 failed (exit $exitCode)" "ERROR"
    exit $exitCode
}

if (Test-Path -LiteralPath $catalogPath) {
    $after = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
    Write-Log "done: version=$($after.version) date=$($after.date) ships=$(@($after.ships.PSObject.Properties).Count)"
}

Remove-OldLogs -Dir $logDir
Write-Log "=== end ==="
exit 0
