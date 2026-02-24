param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$iss = Join-Path $root "installer\CrosshairFlex.iss"
(Get-Content $iss -Raw) `
    -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`"" `
    | Set-Content $iss -Encoding UTF8

$webVersionFile = Join-Path $root "web\app\version.ts"
(Get-Content $webVersionFile -Raw) `
    -replace 'export const APP_VERSION = ".*";', "export const APP_VERSION = `"$Version`";" `
    | Set-Content $webVersionFile -Encoding UTF8

Write-Host "Version updated to $Version"
