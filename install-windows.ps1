#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and installs Agent Federation on Windows.
.DESCRIPTION
    Publishes the MAUI desktop app and the relay server as self-contained executables,
    copies them to %LocalAppData%\AgentFederation, and creates Start Menu shortcuts.
.NOTES
    Run from the repo root. Requires .NET 9 SDK and the MAUI workload:
        dotnet workload install maui-windows
#>

param(
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$InstallDir = Join-Path $env:LocalAppData 'AgentFederation'
$StartMenu  = Join-Path $env:AppData 'Microsoft\Windows\Start Menu\Programs\Agent Federation'

Write-Host ""
Write-Host "======================================"
Write-Host "  Agent Federation — Windows Installer"
Write-Host "======================================"
Write-Host ""

# ── Build ──────────────────────────────────────────────────────────────────

if (-not $SkipBuild) {
    Write-Host "Building MAUI desktop app (self-contained, win-x64)..."
    dotnet publish src/Federation.App.Maui/Federation.App.Maui.csproj `
        -f net9.0-windows10.0.19041.0 `
        -c Release `
        -r win-x64 `
        --self-contained `
        -o "$InstallDir\app" `
        /p:WindowsPackageType=None
    if ($LASTEXITCODE -ne 0) { throw "MAUI app build failed." }

    Write-Host ""
    Write-Host "Building relay server (self-contained, win-x64)..."
    dotnet publish src/Federation.Relay/Federation.Relay.csproj `
        -c Release `
        -r win-x64 `
        --self-contained `
        -o "$InstallDir\relay"
    if ($LASTEXITCODE -ne 0) { throw "Relay build failed." }
} else {
    Write-Host "Skipping build (-SkipBuild specified)."
}

# ── Install directory ──────────────────────────────────────────────────────

Write-Host ""
Write-Host "Installing to: $InstallDir"
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path $StartMenu  | Out-Null

# ── Start Menu shortcuts ───────────────────────────────────────────────────

$WShell = New-Object -ComObject WScript.Shell

# App shortcut
$AppExe = Join-Path $InstallDir 'app\Federation.App.Maui.exe'
$AppLnk = Join-Path $StartMenu  'Agent Federation.lnk'
$s = $WShell.CreateShortcut($AppLnk)
$s.TargetPath       = $AppExe
$s.WorkingDirectory = Join-Path $InstallDir 'app'
$s.Description      = 'Agent Federation Node'
$s.Save()
Write-Host "  Created shortcut: $AppLnk"

# Relay shortcut
$RelayExe = Join-Path $InstallDir 'relay\federation-relay.exe'
$RelayLnk = Join-Path $StartMenu  'Agent Federation Relay.lnk'
$r = $WShell.CreateShortcut($RelayLnk)
$r.TargetPath       = $RelayExe
$r.WorkingDirectory = Join-Path $InstallDir 'relay'
$r.Description      = 'Agent Federation Relay Server'
$r.Save()
Write-Host "  Created shortcut: $RelayLnk"

# ── Done ───────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  Installation complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Start Menu > Agent Federation"
Write-Host ""
Write-Host "  To use:"
Write-Host "    1. Launch 'Agent Federation Relay' first (runs on http://localhost:5000)"
Write-Host "    2. Launch 'Agent Federation'"
Write-Host "    3. Go to the Setup tab and register with http://localhost:5000"
Write-Host "    4. Set your AI provider (Ollama, OpenAI, etc.)"
Write-Host ""
