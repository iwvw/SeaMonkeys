param(
    [switch]$NoRestart,
    [string]$Configuration = "Debug",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\SeaMonkeys.App\SeaMonkeys.App.csproj"
$exe = Join-Path $root "src\SeaMonkeys.App\bin\$Platform\$Configuration\SeaMonkeys.exe"

Write-Host "[dev] stopping running SeaMonkeys..." -ForegroundColor Cyan
Get-Process SeaMonkeys -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

Write-Host "[dev] building ($Configuration/$Platform)..." -ForegroundColor Cyan
dotnet build $appProject -c $Configuration -p:Platform=$Platform -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "[dev] build FAILED" -ForegroundColor Red
    exit $LASTEXITCODE
}

if ($NoRestart) {
    Write-Host "[dev] build OK (no restart)" -ForegroundColor Green
    exit 0
}

if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "[dev] exe not found: $exe" -ForegroundColor Red
    exit 1
}

Write-Host "[dev] launching $exe" -ForegroundColor Green
Start-Process -FilePath $exe | Out-Null
