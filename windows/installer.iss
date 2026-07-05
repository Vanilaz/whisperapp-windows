; Inno Setup script for Whisper - produces a per-user installer with an optional
; "run at startup" shortcut. Build with: iscc installer.iss /DPublishDir=dist
; (PublishDir must contain the output of publish.ps1, i.e. Whisper.exe + deps)

#ifndef PublishDir
  #define PublishDir "dist"
#endif

#define MyAppName "Whisper"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "Gamezxz"
#define MyAppURL "https://gamezxz.github.io/WhisperApp/"
#define MyAppExeName "Whisper.exe"

[Setup]
AppId={{9F6C6E6A-6B7B-4E0B-9C7A-0B1D8B3F1A50}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install - matches the app manifest's asInvoker execution level (no UAC prompt).
PrivilegesRequired=lowest
OutputBaseFilename=Whisper-Setup
OutputDir=.
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startupicon"; Description: "Start Whisper automatically when you sign in"; GroupDescription: "Additional options:"

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; publish.ps1 produces a single self-contained exe, so there's normally nothing else to
; copy - this just picks up any stray file if that ever changes (single-file publish
; failing, etc.) without breaking the build when it doesn't match anything.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist; Excludes: "{#MyAppExeName}"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
