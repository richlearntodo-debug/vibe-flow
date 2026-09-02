#define MyAppName "言灵 Vibe Flow Remote"
#define MyAppVersion "1.5.0"
#define MyAppPublisher "Vibe Flow Contributors"
#define MyAppURL "https://github.com/richlearntodo-debug/vibe-flow"
#define MyAppExeName "VibeFlow.exe"

[Setup]
AppId={{99C65880-071A-4F75-9238-FA4E92A2E76D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases/latest
VersionInfoVersion=1.5.0.0
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=RC003 Bluetooth remote voice input for Windows
DefaultDirName={localappdata}\Programs\Vibe Flow Remote
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\release
OutputBaseFilename=VibeFlow-Setup
SetupIconFile=..\vibe-flow.ico
WizardSmallImageFile=..\vibe-flow-logo.png
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "其他选项："; Flags: unchecked

[InstallDelete]
Type: files; Name: "{app}\docs\RELEASE_NOTES_V*.md"

[Files]
Source: "..\release\Vibe-Flow-Windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\言灵 Vibe Flow Remote"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\使用教程"; Filename: "https://github.com/richlearntodo-debug/vibe-flow/blob/v1.5.0/docs/V1_5_USER_GUIDE_ZH.md"
Name: "{group}\卸载言灵"; Filename: "{uninstallexe}"
Name: "{autodesktop}\言灵 Vibe Flow Remote"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动言灵 Vibe Flow Remote"; Flags: nowait postinstall

[UninstallDelete]
Type: files; Name: "{app}\vibe-mic-config.json"
Type: files; Name: "{app}\vibe-mic-config.json.bak"
Type: files; Name: "{app}\vibe-mic-config.json.tmp"
Type: files; Name: "{app}\voxdeck-shortcuts.json"
Type: files; Name: "{app}\voxdeck-shortcuts.json.bak"
Type: files; Name: "{app}\voxdeck-shortcuts.json.tmp"
Type: files; Name: "{app}\input-bridge-log.txt"
Type: files; Name: "{app}\input-bridge-log.txt.1"
Type: files; Name: "{app}\input-bridge-health.json"
Type: files; Name: "{app}\input-bridge-health.json.tmp"
Type: files; Name: "{app}\custom-button-capture-request.json"
Type: files; Name: "{app}\custom-button-capture-result.json"
Type: files; Name: "{app}\custom-button-capture-result.json.tmp"
Type: files; Name: "{app}\custom-button-test.json"
Type: filesandordirs; Name: "{app}\remote-voice-session"

[Code]
const
  EVENT_MODIFY_STATE = $0002;
  SYNCHRONIZE = $00100000;
  RUN_KEY = 'Software\Microsoft\Windows\CurrentVersion\Run';

function OpenEvent(dwDesiredAccess: LongWord; bInheritHandle: Boolean; lpName: string): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function OpenMutex(dwDesiredAccess: LongWord; bInheritHandle: Boolean; lpName: string): THandle;
  external 'OpenMutexW@kernel32.dll stdcall';
function SetEvent(hEvent: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(hObject: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

procedure WaitForVibeFlowExit;
var
  AppMutex: THandle;
  Attempt: Integer;
begin
  for Attempt := 1 to 48 do
  begin
    AppMutex := OpenMutex(SYNCHRONIZE, False, 'Local\VibeMic');
    if AppMutex = 0 then
      Exit;
    CloseHandle(AppMutex);
    Sleep(250);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitEvent: THandle;
begin
  Result := '';
  ExitEvent := OpenEvent(EVENT_MODIFY_STATE, False, 'Local\VibeMicExitForUpdate');
  if ExitEvent <> 0 then
  begin
    SetEvent(ExitEvent);
    CloseHandle(ExitEvent);
    WaitForVibeFlowExit;
  end;
end;

function UserDataDirectory: String;
begin
  Result := ExpandConstant('{localappdata}\Vibe Flow Remote\UserData');
end;

function UserConfigPath: String;
begin
  Result := UserDataDirectory + '\vibe-mic-config.json';
end;

procedure MigrateLegacyUserConfig;
var
  LegacyConfig: String;
  LegacyBackup: String;
  TargetConfig: String;
  TargetBackup: String;
begin
  LegacyConfig := ExpandConstant('{app}\vibe-mic-config.json');
  LegacyBackup := ExpandConstant('{app}\vibe-mic-config.json.bak');
  TargetConfig := UserConfigPath;
  TargetBackup := TargetConfig + '.bak';
  ForceDirectories(UserDataDirectory);
  if not FileExists(TargetConfig) then
  begin
    if FileExists(LegacyConfig) then
      CopyFile(LegacyConfig, TargetConfig, True)
    else if FileExists(LegacyBackup) then
      CopyFile(LegacyBackup, TargetConfig, True);
  end;
  if FileExists(LegacyBackup) and not FileExists(TargetBackup) then
    CopyFile(LegacyBackup, TargetBackup, True);
end;

function ConfigRequestsStartup: Boolean;
var
  ConfigPath: String;
  ConfigText: AnsiString;
begin
  Result := False;
  ConfigPath := UserConfigPath;
  if not FileExists(ConfigPath) then
    ConfigPath := ExpandConstant('{app}\vibe-mic-config.json');
  if not FileExists(ConfigPath) then
    Exit;
  try
    if not LoadStringFromFile(ConfigPath, ConfigText) then
      Exit;
    ConfigText := Lowercase(ConfigText);
    Result := (Pos('"launchatstartup":true', ConfigText) > 0) or
      (Pos('"launchatstartup": true', ConfigText) > 0);
  except
    Result := False;
  end;
end;

procedure RestoreConfiguredStartupRegistration;
var
  Target: String;
begin
  Target := '"' + ExpandConstant('{app}\VibeFlow.exe') + '" --background';
  if ConfigRequestsStartup then
    RegWriteStringValue(HKEY_CURRENT_USER, RUN_KEY, 'Vibe Flow', Target)
  else
    RegDeleteValue(HKEY_CURRENT_USER, RUN_KEY, 'Vibe Flow');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MigrateLegacyUserConfig;
    RestoreConfiguredStartupRegistration;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitEvent: THandle;
begin
  if CurUninstallStep = usUninstall then
  begin
    ExitEvent := OpenEvent(EVENT_MODIFY_STATE, False, 'Local\VibeMicExitForUpdate');
    if ExitEvent <> 0 then
    begin
      SetEvent(ExitEvent);
      CloseHandle(ExitEvent);
      WaitForVibeFlowExit;
    end;
    RegDeleteValue(HKEY_CURRENT_USER, RUN_KEY, 'Vibe Flow');
  end;
end;
