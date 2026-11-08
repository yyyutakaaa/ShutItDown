; Inno Setup script for Shutdown Server.
; Publish first (from the repo root):
;   dotnet publish src\ShutItDown.Tray -c Release -r win-x64 --self-contained true -o publish
; Then: iscc installer\ShutdownServerApp.iss

#define MyAppName "Shutdown Server"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "yyyutakaaa"
#define MyAppURL "https://github.com/yyyutakaaa/ShutItDown"
#define MyAppExeName "ShutdownServer.exe"
#define MyAppIcon "..\src\ShutItDown.Tray\Assets\ShutdownServer.ico"
#define PublishDir "..\publish"

[Setup]
; Same AppId as 2.x, so this installs over the old version.
AppId={{7C908E58-0523-4B3D-BDEA-0B85C4F21E05}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=.\output
OutputBaseFilename=ShutdownServer-Setup
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Files from 2.x, which used a different exe name.
Type: files; Name: "{app}\ShutdownServerApp.*"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; Remove the "Start with Windows" entry when the app is uninstalled.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ShutdownServer"; Flags: dontcreatekey uninsdeletevalue

[UninstallDelete]
Type: files; Name: "{userstartup}\Shutdown Server.lnk"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Start {#MyAppName}"; Flags: nowait postinstall skipifsilent
