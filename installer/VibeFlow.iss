#define MyAppName "言灵 Vibe Flow Remote"
#define MyAppVersion "2.0.0"
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
VersionInfoVersion=2.0.0.0
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
MinVersion=10.0
OutputDir=..\release
OutputBaseFilename=VibeFlow-Setup
SetupIconFile=..\vibe-flow.ico
WizardSmallImageFile=..\vibe-flow-logo.png
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "其他选项："; Flags: unchecked
Name: "installvbcable"; Description: "安装 VB-CABLE 虚拟音频线（语音输入必需 · 使用内置官方包 · VB-Audio 捐赠软件）"; GroupDescription: "语音输入组件："; Flags: checkedonce

[InstallDelete]
Type: files; Name: "{app}\docs\RELEASE_NOTES_V*.md"

[Files]
Source: "..\release\Vibe-Flow-Windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\言灵 Vibe Flow Remote"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\使用教程"; Filename: "{app}\docs\V2_0_USER_GUIDE_ZH.md"
Name: "{group}\卸载言灵"; Filename: "{uninstallexe}"
Name: "{autodesktop}\言灵 Vibe Flow Remote"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动并开始设置"; Flags: nowait postinstall skipifsilent
Filename: "{app}\docs\V2_0_USER_GUIDE_ZH.md"; Description: "打开使用教程"; Flags: postinstall shellexec skipifsilent unchecked
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\Install-VBCable.ps1"" -Install"; Description: "安装 VB-CABLE 虚拟音频线（内置官方包，需管理员确认）"; Tasks: installvbcable; Flags: nowait postinstall runascurrentuser skipifsilent

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

var
  PreflightPage: TOutputMsgMemoWizardPage;
  PreviousInstallDirectory: String;
  KeepUserDataOnUninstall: Boolean;

function OpenEvent(dwDesiredAccess: LongWord; bInheritHandle: Boolean; lpName: string): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function OpenMutex(dwDesiredAccess: LongWord; bInheritHandle: Boolean; lpName: string): THandle;
  external 'OpenMutexW@kernel32.dll stdcall';
function SetEvent(hEvent: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(hObject: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function WaitForVibeFlowExit: Boolean;
var
  AppMutex: THandle;
  Attempt: Integer;
begin
  Result := False;
  for Attempt := 1 to 48 do
  begin
    AppMutex := OpenMutex(SYNCHRONIZE, False, 'Local\VibeMic');
    if AppMutex = 0 then
    begin
      Result := True;
      Exit;
    end;
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
  end;
  if not WaitForVibeFlowExit then
    Result := 'Vibe Flow 仍在运行，安装已停止。请关闭应用后重试；现有文件和配置未被覆盖。';
end;

function UserDataDirectory: String;
begin
  Result := ExpandConstant('{localappdata}\Vibe Flow Remote\UserData');
end;

function UserConfigPath: String;
begin
  Result := UserDataDirectory + '\vibe-mic-config.json';
end;

function ReadPreviousInstallDirectory: String;
begin
  Result := '';
  RegQueryStringValue(HKEY_CURRENT_USER,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{99C65880-071A-4F75-9238-FA4E92A2E76D}_is1',
    'InstallLocation', Result);
  { The registry value ends with a backslash. Passed on inside quotes, that backslash escapes the closing
    quote, so the receiving application is handed a path containing a quote and fails: measured, the old
    configuration was reported as unmigratable on every install over an existing installation, while a clean
    install, which uses the application directory directly and has no trailing separator, worked. }
  Result := RemoveBackslashUnlessRoot(Result);
end;

function LegacyConfigRoot: String;
begin
  if PreviousInstallDirectory <> '' then
    Result := PreviousInstallDirectory
  else
    Result := ExpandConstant('{app}');
end;

function InstalledVersionText: String;
begin
  Result := '';
  RegQueryStringValue(HKEY_CURRENT_USER,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{99C65880-071A-4F75-9238-FA4E92A2E76D}_is1',
    'DisplayVersion', Result);
end;

function BuildPreflightText: String;
var
  Version: TWindowsVersion;
  VersionText: String;
  ArchitectureText: String;
  ExistingText: String;
  ConfigText: String;
  DesktopShortcutText: String;
begin
  GetWindowsVersionEx(Version);
  if Version.Major > 0 then
    VersionText := IntToStr(Version.Major) + '.' + IntToStr(Version.Minor) +
      ' (build ' + IntToStr(Version.Build) + ')'
  else
    VersionText := '无法读取；安装器不会伪造检测通过';

  if IsWin64 then
    ArchitectureText := 'x64 兼容系统 · 已检测'
  else
    ArchitectureText := '非 x64 兼容系统 · 不符合要求';

  ExistingText := InstalledVersionText;
  if ExistingText = '' then
    ExistingText := '未检测到已安装版本'
  else
    ExistingText := '检测到 Vibe Flow ' + ExistingText + '；将执行原位升级';

  if FileExists(UserConfigPath) then
    ConfigText := '已检测到用户配置；升级时将保留，迁移前不会覆盖中央用户数据'
  else if InstalledVersionText <> '' then
    ConfigText := '将在安装阶段检查旧安装目录并迁移配置；中央用户数据不会被覆盖'
  else
    ConfigText := '未检测到旧用户配置；首次启动将进入 5 项设置';

  if WizardIsTaskSelected('desktopicon') then
    DesktopShortcutText := '创建'
  else
    DesktopShortcutText := '不创建';

  Result :=
    '系统版本：' + VersionText + #13#10 +
    '系统架构：' + ArchitectureText + #13#10 +
    '旧版检测：' + ExistingText + #13#10 +
    '配置保护：' + ConfigText + #13#10 +
    '安装目录：' + WizardDirValue + #13#10 +
    '桌面快捷方式：' + DesktopShortcutText + #13#10#13#10 +
    '准备事项：RC003 / MI RC、Windows 蓝牙和一个语音工具。' + #13#10 +
    '首次启动后，应用内向导会继续检测遥控器、VB-CABLE 和真实听写。';
end;

procedure InitializeWizard;
begin
  PreviousInstallDirectory := ReadPreviousInstallDirectory;
  WizardForm.WelcomeLabel1.Caption := '欢迎安装言灵 Vibe Flow Remote';
  WizardForm.WelcomeLabel2.Caption :=
    '适用于 Windows 10 / 11 x64。请准备 RC003 / MI RC、蓝牙和语音工具。' + #13#10#13#10 +
    '安装后将通过 5 项应用内任务检测遥控器、VB-CABLE 与真实听写。' + #13#10 +
    'Vibe Flow 不保存普通录音与转译文字，也不会自动发送 AI 消息。';
  PreflightPage := CreateOutputMsgMemoPage(wpSelectTasks,
    '安装前检查', '确认系统、升级和配置保护信息',
    '以下内容来自本机检测；未检测到的项目不会显示为通过。', BuildPreflightText);
  WizardForm.FinishedHeadingLabel.Caption := 'Vibe Flow 已安装';
  WizardForm.FinishedLabel.Caption :=
    '首次启动将继续完成遥控器、VB-CABLE 和语音工具设置。' + #13#10 +
    '第三方语音工具不会由安装器静默安装。';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = PreflightPage.ID then
    PreflightPage.RichEditViewer.Lines.Text := BuildPreflightText;
end;

function MigrateLegacyUserConfig: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if (not DirExists(UserDataDirectory)) and
    (not ForceDirectories(UserDataDirectory)) then
    Exit;
  if not Exec(ExpandConstant('{app}\VibeFlow.exe'),
    '--installer-config-migrate ' + AddQuotes(LegacyConfigRoot) + ' ' +
      AddQuotes(UserDataDirectory), '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Exit;
  Result := ResultCode = 0;
end;

function TryReadConfigStartupFromPath(ConfigPath: String;
  var RequestsStartup: Boolean): Boolean;
var
  ResultCode: Integer;
begin
  RequestsStartup := False;
  Result := False;
  if not FileExists(ConfigPath) then
    Exit;
  if not Exec(ExpandConstant('{app}\VibeFlow.exe'),
    '--installer-config-startup-query ' + AddQuotes(ConfigPath), '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Exit;
  if ResultCode = 10 then
  begin
    RequestsStartup := True;
    Result := True;
  end
  else if ResultCode = 0 then
    Result := True;
end;

function TryReadConfigStartup(var RequestsStartup: Boolean): Boolean;
var
  ConfigFound: Boolean;
begin
  RequestsStartup := False;
  ConfigFound := False;
  Result := False;

  if FileExists(UserConfigPath) then
  begin
    ConfigFound := True;
    if TryReadConfigStartupFromPath(UserConfigPath, RequestsStartup) then
    begin
      Result := True;
      Exit;
    end;
  end;
  if FileExists(UserConfigPath + '.bak') then
  begin
    ConfigFound := True;
    if TryReadConfigStartupFromPath(UserConfigPath + '.bak', RequestsStartup) then
    begin
      Result := True;
      Exit;
    end;
  end;
  if FileExists(LegacyConfigRoot + '\vibe-mic-config.json') then
  begin
    ConfigFound := True;
    if TryReadConfigStartupFromPath(
      LegacyConfigRoot + '\vibe-mic-config.json', RequestsStartup) then
    begin
      Result := True;
      Exit;
    end;
  end;
  if FileExists(LegacyConfigRoot + '\vibe-mic-config.json.bak') then
  begin
    ConfigFound := True;
    if TryReadConfigStartupFromPath(
      LegacyConfigRoot + '\vibe-mic-config.json.bak', RequestsStartup) then
    begin
      Result := True;
      Exit;
    end;
  end;

  Result := not ConfigFound;
end;

function ConfigRequestsStartup: Boolean;
var
  RequestsStartup: Boolean;
begin
  Result := TryReadConfigStartup(RequestsStartup) and RequestsStartup;
end;

function RestoreConfiguredStartupRegistration: Boolean;
var
  Target: String;
  RequestsStartup: Boolean;
begin
  Result := False;
  if not TryReadConfigStartup(RequestsStartup) then
    Exit;
  Target := '"' + ExpandConstant('{app}\VibeFlow.exe') + '" --background';
  if RequestsStartup then
    Result := RegWriteStringValue(HKEY_CURRENT_USER, RUN_KEY, 'Vibe Flow', Target)
  else if RegValueExists(HKEY_CURRENT_USER, RUN_KEY, 'Vibe Flow') then
    Result := RegDeleteValue(HKEY_CURRENT_USER, RUN_KEY, 'Vibe Flow')
  else
    Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    WizardForm.StatusLabel.Caption := '正在安装应用文件';
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := '正在迁移旧版配置';
    if not MigrateLegacyUserConfig then
      RaiseException('无法迁移或保护旧版配置，安装未完成。请检查用户数据目录权限后重试。');
    WizardForm.StatusLabel.Caption := '正在恢复开机启动设置';
    if not RestoreConfiguredStartupRegistration then
      RaiseException('无法读取配置或恢复开机启动设置，安装未完成。请检查配置和注册表权限后重试。');
    WizardForm.StatusLabel.Caption := '正在完成安装';
  end;
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
begin
  if MaxProgress <= 0 then
    Exit;
  if CurProgress < (MaxProgress div 2) then
    WizardForm.StatusLabel.Caption := '正在安装应用文件'
  else
    WizardForm.StatusLabel.Caption := '正在完成应用文件安装';
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
    end;
    if not WaitForVibeFlowExit then
      RaiseException('Vibe Flow 仍在运行，卸载已停止。请关闭应用后重试。');
    if RegValueExists(HKEY_CURRENT_USER, RUN_KEY, 'Vibe Flow') and
      (not RegDeleteValue(HKEY_CURRENT_USER, RUN_KEY, 'Vibe Flow')) then
      RaiseException('无法移除 Vibe Flow 开机启动项。请检查当前用户注册表权限后重试卸载。');
  end;
  if (CurUninstallStep = usPostUninstall) and (not KeepUserDataOnUninstall) then
    if not DelTree(UserDataDirectory, True, True, True) then
      RaiseException('应用已卸载，但本地用户数据未能完全删除。请检查文件占用后重试。');
end;

function InitializeUninstall(): Boolean;
begin
  KeepUserDataOnUninstall := SuppressibleMsgBox(
    '是否保留 Vibe Flow 本地用户数据？' + #13#10 + #13#10 +
    '选择“是”：保留按键、语音工具、Profile 和输入目标，便于以后重新安装（推荐）。' + #13#10 +
    '选择“否”：卸载完成后删除上述本地数据；此操作无法由卸载器恢复。',
    mbConfirmation, MB_YESNO, IDYES) = IDYES;
  Result := True;
end;
