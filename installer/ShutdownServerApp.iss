; Inno Setup script for ShutdownServerApp
; Run `dotnet publish -c Release -o ..\publish --self-contained false` before compiling this installer.

#define MyAppName "Shutdown Server"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "yyyutakaaa"
#define MyAppExeName "ShutdownServerApp.exe"
#define PublishDir "..\\publish"

[Setup]
AppId={{7C908E58-0523-4B3D-BDEA-0B85C4F21E05}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\installer\output
OutputBaseFilename=ShutdownServer-Setup
Compression=lzma
SolidCompression=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Start {#MyAppName}"; Flags: nowait postinstall skipifsilent
