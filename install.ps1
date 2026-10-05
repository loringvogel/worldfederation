# Federation CLI + MCP Server installer for Windows
# Usage: irm https://raw.githubusercontent.com/loringvogel/worldfederation/master/install.ps1 | iex

$ErrorActionPreference = "Stop"

$Repo      = "loringvogel/worldfederation"
$InstallDir = if ($env:FEDERATION_INSTALL_DIR) { $env:FEDERATION_INSTALL_DIR } else { "$env:USERPROFILE\.federation\bin" }

# Detect arch
$Arch = (Get-CimInstance Win32_Processor).AddressWidth
$CpuArch = (Get-CimInstance Win32_Processor).Architecture
# Architecture: 9 = ARM64, others = x64
$RID = if ($CpuArch -eq 12) { "win-arm64" } else { "win-x64" }

# Resolve latest release tag
Write-Host "Fetching latest release..."
$Release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest"
$Tag = $Release.tag_name
if (-not $Tag) { throw "Could not determine latest release." }
Write-Host "Installing Federation $Tag for $RID..."

$BaseUrl = "https://github.com/$Repo/releases/download/$Tag"

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

function Download-And-Extract($Name) {
    Write-Host "  Downloading $Name..."
    $Zip = "$env:TEMP\${Name}.zip"
    Invoke-WebRequest "$BaseUrl/${Name}-${RID}.zip" -OutFile $Zip
    Expand-Archive -Force -Path $Zip -DestinationPath $InstallDir
    Remove-Item $Zip
}

Download-And-Extract "federation"
Download-And-Extract "federation-mcp"

Write-Host ""
Write-Host "Installed to $InstallDir"
Write-Host "  federation.exe     - CLI for registering and messaging"
Write-Host "  federation-mcp.exe - MCP server for AI agent integration"
Write-Host ""

# Add to PATH for current user if not already there
$UserPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($UserPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("PATH", "$InstallDir;$UserPath", "User")
    Write-Host "Added $InstallDir to your PATH (restart terminal to take effect)"
    Write-Host ""
}

Write-Host "Next step - register with your relay:"
Write-Host ""
Write-Host "  federation register --relay <relay-url> --name <your-name>"
Write-Host ""
