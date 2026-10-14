; Inno Setup script for Agent Federation (Windows)
; Built by the release CI — call with /DAppVersion=x.y.z

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppName=Agent Federation
AppVersion={#AppVersion}
AppPublisher=World Federation
AppPublisherURL=https://loringvogel.github.io/worldfederation/
AppSupportURL=https://github.com/loringvogel/worldfederation/issues
DefaultDirName={autopf}\Agent Federation
DefaultGroupName=Agent Federation
OutputDir=dist
OutputBaseFilename=AgentFederation-win-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Install per-user by default (no UAC prompt); set to admin for machine-wide
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayName=Agent Federation
UninstallDisplayIcon={app}\AgentFederation.exe
; Show a friendly finish page with a launch option
DisableFinishedPage=no

[Files]
Source: "publish\installer-win\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Agent Federation"; Filename: "{app}\AgentFederation.exe"
Name: "{commondesktop}\Agent Federation"; Filename: "{app}\AgentFederation.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\AgentFederation.exe"; Description: "&Launch Agent Federation"; Flags: nowait postinstall skipifsilent
