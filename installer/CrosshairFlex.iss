#define MyAppName "CrosshairFlex"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "CrosshairFlex"
#define MyAppExeName "CrosshairFlex.exe"
#define MyAppURL "https://crosshairflex.gg"
#define MyBuildRoot "..\\artifacts\\publish\\win-x64"

[Setup]
AppId={{6A2C8A8A-43F9-4CF0-A31D-C8F98EBD08D4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\CrosshairFlex
DefaultGroupName=CrosshairFlex
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=CrosshairFlex_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
AppMutex=Global\CrosshairFlex.SingleInstance
SetupIconFile=..\assets\app.ico
; Signed-ready: uncomment and configure once cert is available.
; SignTool=signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "C:\certs\crosshairflex.pfx" /p "{#GetEnv('CERT_PASSWORD')}" $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyBuildRoot}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\CrosshairFlex"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall CrosshairFlex"; Filename: "{uninstallexe}"
Name: "{autodesktop}\CrosshairFlex"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,CrosshairFlex}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /IM {#MyAppExeName} /F /T"; Flags: runhidden; RunOnceId: "KillCrosshairFlexProcess"
