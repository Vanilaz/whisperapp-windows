#!/usr/bin/env pwsh
# Build a self-contained, single-file release exe (no .NET runtime required on the target
# machine). Equivalent of the macOS make_app.sh / make_dmg.sh.
#
# Usage: ./publish.ps1 [-Runtime win-x64] [-OutDir dist]
param(
    [string]$Runtime = "win-x64",
    [string]$OutDir = "dist"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$proj = "WhisperApp.Windows/WhisperApp.Windows.csproj"
$out = Join-Path $PSScriptRoot $OutDir

dotnet publish $proj `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $out

Write-Host ""
Write-Host "Published -> $out\Whisper.exe" -ForegroundColor Green

# Optional: build an installer if Inno Setup's compiler is on PATH.
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($iscc) {
    Write-Host "Building installer with Inno Setup..."
    & $iscc.Path "installer.iss" "/DPublishDir=$out"
} else {
    Write-Host "Inno Setup (ISCC.exe) not found on PATH — skipping installer build." -ForegroundColor Yellow
    Write-Host "Install from https://jrsoftware.org/isinfo.php to produce a Whisper-Setup.exe installer."
}
