param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$ArtifactsDir,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$TemplatePath = "$PSScriptRoot/../release-notes-template.md",
    [string]$ChangesDir = "$PSScriptRoot/../release-notes",
    [string]$PreviousTag = ""
)

$ErrorActionPreference = 'Stop'

function Format-Size([string]$fileName) {
    $path = Join-Path $ArtifactsDir $fileName
    if (-not (Test-Path -LiteralPath $path)) { return '—' }
    $mb = (Get-Item -LiteralPath $path).Length / 1MB
    return ('{0:N1} MB' -f $mb)
}

$changesFile = Join-Path $ChangesDir "$Version.md"
if (Test-Path -LiteralPath $changesFile) {
    $changes = (Get-Content -LiteralPath $changesFile -Raw).Trim()
}
elseif (-not [string]::IsNullOrWhiteSpace($PreviousTag)) {
    $changes = "**Full Changelog**: https://github.com/iwvw/SeaMonkeys/compare/$PreviousTag...v$Version"
}
else {
    $changes = "**Full Changelog**: https://github.com/iwvw/SeaMonkeys/releases/tag/v$Version"
}

$template = Get-Content -LiteralPath $TemplatePath -Raw

$sizeMap = [ordered]@{
    '{{SIZE_X64_MERGED_SETUP}}'      = Format-Size "SeaMonkeys-$Version-x64-merged-setup.exe"
    '{{SIZE_X64_MERGED_PORTABLE}}'   = Format-Size "SeaMonkeys-$Version-x64-merged-portable.zip"
    '{{SIZE_ARM64_MERGED_PORTABLE}}' = Format-Size "SeaMonkeys-$Version-arm64-merged-portable.zip"
    '{{SIZE_X64_SPLIT_SETUP}}'       = Format-Size "SeaMonkeys-$Version-x64-split-setup.exe"
    '{{SIZE_X64_SPLIT_PORTABLE}}'    = Format-Size "SeaMonkeys-$Version-x64-split-portable.zip"
}

$body = $template.Replace('{{CHANGES}}', $changes).Replace('{{VERSION}}', $Version)
foreach ($kv in $sizeMap.GetEnumerator()) {
    $body = $body.Replace($kv.Key, $kv.Value)
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutputPath, $body, $utf8)
Write-Host "Release notes written to $OutputPath"
