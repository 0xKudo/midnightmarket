#define MyAppName "ArmsFair"
#define MyAppVersion "0.0.0"
#define MyAppPublisher "Studio Kudo"
#define MyAppExeName "ArmsFair.exe"
#define BuildDir "..\Builds"
#define OutputDir "..\installer\output"

[Setup]
AppId={{DA2BA612-843C-414D-B416-6E1E5E00ead9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=ArmsFairSetup-v0.0.0
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; Main executable
Source: "{#BuildDir}\ArmsFair.exe"; DestDir: "{app}"; Flags: ignoreversion

; Unity runtime files
Source: "{#BuildDir}\UnityPlayer.dll";           DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\UnityCrashHandler64.exe";   DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\DirectML.dll";              DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\dstorage.dll";              DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\dstoragecore.dll";          DestDir: "{app}"; Flags: ignoreversion

; D3D12 folder
Source: "{#BuildDir}\D3D12\*"; DestDir: "{app}\D3D12"; Flags: ignoreversion recursesubdirs createallsubdirs

; MonoBleedingEdge (Mono runtime)
Source: "{#BuildDir}\MonoBleedingEdge\*"; DestDir: "{app}\MonoBleedingEdge"; Flags: ignoreversion recursesubdirs createallsubdirs

; Game data (levels, assets, streaming assets incl. bundled server)
Source: "{#BuildDir}\ArmsFair_Data\*"; DestDir: "{app}\ArmsFair_Data"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";       Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall ArmsFair"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
