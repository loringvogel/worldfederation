param([string]$Version = "dev")

$Out = "release\$Version"
New-Item -ItemType Directory -Force -Path $Out | Out-Null

$Arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq "Arm64") { "win-arm64" } else { "win-x64" }
$Flags = @("-c", "Release", "-r", $Arch, "--self-contained", "true",
           "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true", "-p:DebugType=embedded")

Write-Host "Building federation-relay ($Arch)..."
dotnet publish src/Federation.Relay @Flags -o "$Out\relay"

Write-Host "Building federation CLI ($Arch)..."
dotnet publish src/Federation.Cli   @Flags -o "$Out\cli"

Write-Host "`nBinaries in $Out\"
Get-ChildItem "$Out\relay\federation-relay.exe", "$Out\cli\federation.exe"
