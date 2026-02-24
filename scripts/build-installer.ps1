param(
    [string]$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot "build-desktop.ps1")

$iss = Join-Path $root "installer\CrosshairFlex.iss"
if (-not (Test-Path $InnoSetupCompiler)) {
    throw "ISCC.exe not found at $InnoSetupCompiler. Install Inno Setup 6 or pass -InnoSetupCompiler."
}

& $InnoSetupCompiler $iss

$output = Join-Path $root "artifacts\installer\CrosshairFlex_Setup.exe"
Write-Host "Installer created: $output"
