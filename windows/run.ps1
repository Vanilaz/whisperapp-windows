#!/usr/bin/env pwsh
# Dev loop: build + run the tray app. Equivalent of the macOS run.sh.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet build WhisperApp.Windows/WhisperApp.Windows.csproj -c Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project WhisperApp.Windows/WhisperApp.Windows.csproj -c Debug
