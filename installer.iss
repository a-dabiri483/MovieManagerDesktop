; Script generated for MovieManager Desktop Installer
#define MyAppName "MovieManager"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "MovieManager Team"
#define MyAppURL "https://github.com/a-dabiri483/MovieManagerDesktop"
#define MyAppExeName "MovieManagerDesktop.exe"
#define MyAppSourceDir "publish"

[Setup]
AppId={{5D9A1491-0DF3-4A67-8C52-7BC29AC6D657}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=setup_output
OutputBaseFilename=MovieManager_Setup_v2.0
SetupIconFile=Assets\logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Main Executable
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; LibVLC folder (Native video/audio engine with all plugins and codecs)
Source: "bin\Release\net10.0-windows\win-x64\libvlc\*"; DestDir: "{app}\libvlc"; Flags: ignoreversion recursesubdirs createallsubdirs
; Assets (Folder templates, icons)
Source: "{#MyAppSourceDir}\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Posters\*,*.pdb"
; Direct source logo.ico
Source: "Assets\logo.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion
; Google Drive credentials
Source: "credentials.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\logo.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\logo.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
