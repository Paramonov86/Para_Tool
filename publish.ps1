# ParaTool — local publish script for Windows
# Usage: .\publish.ps1 [-Target win-x64|linux-x64|all]

param(
    [ValidateSet("win-x64", "linux-x64", "all")]
    [string]$Target = "all"
)

$ErrorActionPreference = "Stop"
$Version = if ($env:PARATOOL_VERSION) { $env:PARATOOL_VERSION } else { "dev" }
$OutDir = "publish"

function Publish-Target {
    param([string]$Rid)

    $Name = "ParaTool-$Rid"
    Write-Host "=== Publishing $Name ===" -ForegroundColor Cyan

    dotnet publish ParaTool.App/ParaTool.App.csproj `
        -c Release `
        -r $Rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -o "$OutDir/$Name"

    Write-Host "  -> $OutDir/$Name/" -ForegroundColor Green

    # Archive
    $ZipPath = "$Name-$Version.zip"
    Compress-Archive -Path "$OutDir/$Name/*" -DestinationPath $ZipPath -Force
    Write-Host "  -> $ZipPath" -ForegroundColor Green
}

switch ($Target) {
    "win-x64"   { Publish-Target -Rid "win-x64" }
    "linux-x64" { Publish-Target -Rid "linux-x64" }
    "all" {
        Publish-Target -Rid "win-x64"
        Publish-Target -Rid "linux-x64"
    }
}

Write-Host ""
Write-Host "Done! Artifacts in $OutDir/" -ForegroundColor Green
