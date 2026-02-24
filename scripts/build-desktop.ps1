param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [ValidateSet("framework-dependent", "self-contained")]
    [string]$Deployment = "framework-dependent"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\CrosshairFlex.Desktop\CrosshairFlex.Desktop.csproj"
$output = Join-Path $root "artifacts\publish\$Runtime"

dotnet restore $project

if ($Deployment -eq "self-contained") {
    dotnet publish $project `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        -o $output
}
else {
    dotnet publish $project `
        -c $Configuration `
        -r $Runtime `
        --self-contained false `
        /p:PublishSingleFile=true `
        /p:EnableCompressionInSingleFile=true `
        -o $output
}

Write-Host "Desktop publish completed ($Deployment): $output"
