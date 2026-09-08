<#
.SYNOPSIS
    Installs the Agent Federation CLI for Windows.

.DESCRIPTION
    Downloads the latest self-contained federation.exe from GitHub Releases
    and installs it to %LOCALAPPDATA%\Federation, then adds it to the user PATH.

.EXAMPLE
    irm https://raw.githubusercontent.com/loringvogel/worldfederation/master/deploy/install.ps1 | iex
#>
[CmdletBinding()]
param(
    [string]$Version = "latest",
    [string]$InstallDir = "$env:LOCALAPPDATA\Federation"
)

$ErrorActionPreference = "Stop"
$Repo = "loringvogel/worldfederation"

Write-Host "Agent Federation Installer" -ForegroundColor Cyan
Write-Host "──────────────────────────" -ForegroundColor Cyan

# Detect architecture
$arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq "Arm64") { "win-arm64" } else { "win-x64" }

# Resolve version
if ($Version -eq "latest") {
    Write-Host "Checking latest release..."
    $release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest"
    $Version = $release.tag_name
}
Write-Host "Version : $Version"
Write-Host "Platform: $arch"
Write-Host "Install : $InstallDir"
Write-Host ""

# Download
$zipName = "federation-$arch.zip"
$url = "https://github.com/$Repo/releases/download/$Version/$zipName"
$tmpZip = Join-Path $env:TEMP $zipName

Write-Host "Downloading $zipName..." -NoNewline
Invoke-WebRequest -Uri $url -OutFile $tmpZip -UseBasicParsing
Write-Host " done" -ForegroundColor Green

# Install
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Expand-Archive -Path $tmpZip -DestinationPath $InstallDir -Force
Remove-Item $tmpZip

# Add to PATH if not already present
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($userPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("PATH", "$userPath;$InstallDir", "User")
    Write-Host "Added $InstallDir to user PATH" -ForegroundColor Green
    Write-Host "Restart your terminal for PATH to take effect."
}

Write-Host ""
Write-Host "Installed! Try:" -ForegroundColor Green
Write-Host "  federation help"
Write-Host "  federation register --relay http://<relay-ip>:5000 --name YourName"
