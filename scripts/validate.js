const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const requiredFiles = [
  "README.md",
  "README_VIBE_MIC.md",
  "CHANGELOG.md",
  "VIBE_MIC_VERSION.md",
  "LICENSE",
  "THIRD_PARTY_NOTICES.md",
  "QUICK_START_ZH.md",
  "SECURITY.md",
  "CONTRIBUTING.md",
  "RESTORE_BUILD_DEPS.ps1",
  "BUILD_INPUT_BRIDGE.cmd",
  "BUILD_VIBE_MIC_CAPTURE.cmd",
  "BUILD_VIBE_MIC.cmd",
  "BUILD_HARDWARE_CANDIDATE.ps1",
  "BUILD_RELEASE.ps1",
  "CREATE_APP_ICON.ps1",
  "START_VIBE_FLOW.cmd",
  "vibe-flow-logo.png",
  "vibe-mic-config.default.json",
  "scripts/Install-VBCable.ps1",
  "scripts/VibeMic.cs",
  "scripts/VibeMicAtvvCapture.cs",
  "scripts/VoxDeckInputBridge.cs",
  "scripts/capture-ui-screenshots.ps1",
  "installer/VibeFlow.iss",
  "installer/languages/ChineseSimplified.isl",
  "docs/USER_GUIDE_ZH.md",
  "docs/V1_2_1_TUTORIAL_ZH.md",
  "docs/VERSION_ARCHIVE_ZH.md",
  "docs/RELEASE_NOTES_ZH.md",
  "docs/GITHUB_RELEASE_BODY_ZH.md",
  "docs/CONTINUOUS_DICTATION_ZH.md",
  "docs/CODE_SIGNING_ZH.md",
  "docs/ARCHITECTURE.md",
  "docs/VOICE_PIPELINE_RESEARCH.md",
  "docs/V1_2_HARDWARE_ACCEPTANCE_ZH.md",
  "docs/images/00-first-run.png",
  "docs/images/00-setup-01-intro.png",
  "docs/images/00-setup-02-bluetooth.png",
  "docs/images/00-setup-03-pairing.png",
  "docs/images/00-setup-04-keys.png",
  "docs/images/00-setup-05-microphone.png",
  "docs/images/00-setup-06-vb-cable.png",
  "docs/images/00-setup-07-provider.png",
  "docs/images/00-setup-08-dictation.png",
  "docs/images/00-setup-09-buttons.png",
  "docs/images/00-setup-10-startup.png",
  "docs/images/00-setup-11-summary.png",
  "docs/images/01-overview.png",
  "docs/images/02-dictation.png",
  "docs/images/03-shortcuts.png",
  "docs/images/03-shortcuts-screenshot.png",
  "docs/images/04-diagnostics.png",
  "docs/images/05-settings.png",
  "docs/images/06-transcription-tools.png",
  "docs/images/vibe-flow-community.png",
  ".github/ISSUE_TEMPLATE/bug_report.yml",
  ".github/workflows/validate.yml",
];

function read(file) {
  return fs.readFileSync(path.join(root, file), "utf8").replace(/^\uFEFF/, "");
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function includesAll(source, values) {
  return values.every((value) => source.includes(value));
}

function section(source, start, end) {
  const startIndex = source.indexOf(start);
  const endIndex = source.indexOf(end, startIndex + start.length);
  assert(startIndex >= 0 && endIndex > startIndex, `Unable to locate source section: ${start}`);
  return source.slice(startIndex, endIndex);
}

for (const file of requiredFiles) {
  assert(fs.existsSync(path.join(root, file)), `Missing ${file}`);
}

const app = read("scripts/VibeMic.cs");
const capture = read("scripts/VibeMicAtvvCapture.cs");
const bridge = read("scripts/VoxDeckInputBridge.cs");
const release = read("BUILD_RELEASE.ps1");
const candidateBuild = read("BUILD_HARDWARE_CANDIDATE.ps1");
const dependencyRestore = read("RESTORE_BUILD_DEPS.ps1");
const captureBuild = read("BUILD_VIBE_MIC_CAPTURE.cmd");
const installer = read("installer/VibeFlow.iss");
const screenshotScript = read("scripts/capture-ui-screenshots.ps1");
const cableInstaller = read("scripts/Install-VBCable.ps1");
const workflow = read(".github/workflows/validate.yml");
const defaultConfig = JSON.parse(read("vibe-mic-config.default.json"));
const packageJson = JSON.parse(read("package.json"));
const readme = read("README.md");
const englishReadme = read("README_VIBE_MIC.md");
const guide = read("docs/USER_GUIDE_ZH.md");
const versionTutorial = read("docs/V1_2_1_TUTORIAL_ZH.md");
const versionArchive = read("docs/VERSION_ARCHIVE_ZH.md");
const quickStart = read("QUICK_START_ZH.md");
const releaseNotes = read("docs/RELEASE_NOTES_ZH.md");
const githubReleaseBody = read("docs/GITHUB_RELEASE_BODY_ZH.md");
const continuousGuide = read("docs/CONTINUOUS_DICTATION_ZH.md");
const architecture = read("docs/ARCHITECTURE.md");
const versionDoc = read("VIBE_MIC_VERSION.md");
const voiceResearch = read("docs/VOICE_PIPELINE_RESEARCH.md");
const hardwareAcceptance = read("docs/V1_2_HARDWARE_ACCEPTANCE_ZH.md");

for (const file of requiredFiles.filter((item) => item.startsWith("docs/images/") && item.endsWith(".png"))) {
  const png = fs.readFileSync(path.join(root, file));
  assert(png.length > 20000, `Screenshot is unexpectedly small: ${file}`);
  assert(png.toString("ascii", 1, 4) === "PNG", `Screenshot is not a PNG: ${file}`);
  assert(png.readUInt32BE(16) >= 900 && png.readUInt32BE(20) >= 600,
    `Screenshot dimensions are too small: ${file}`);
}

// Product identity, release metadata, and persistent configuration.
assert(includesAll(app, [
  'DisplayProductName = "言灵 · Vibe Flow Remote"',
  'ProductRelease = "1.2.1"',
  'AssemblyFileVersion("1.2.1.0")',
  'AssemblyInformationalVersion("1.2.1")',
  'ConfigSchemaVersion = 25',
  'CurrentOnboardingVersion = 8',
  'OnboardingStepCount = 11',
]), "Application identity or V1.2.1 configuration metadata is inconsistent");
assert(packageJson.version === "1.2.1", "package.json version is not aligned with the app");
assert(capture.includes('AssemblyFileVersion("1.2.1.0")') && bridge.includes('AssemblyFileVersion("1.2.1.0")'),
  "Shipped helper binaries do not carry the V1.2.1 file version");
assert(includesAll(app, [
  "LoadConfig();", "MigrateConfig(", "SyncKeyboardBridgeConfig();",
  "WriteTextAtomically(configPath", "WriteTextAtomically(bridgeConfigPath",
  'configPath + ".bak"', 'bridgeConfigPath + ".bak"', "File.Replace",
]), "Configuration migration or atomic persistence is incomplete");
const loadConfigIndex = app.indexOf("config = LoadConfig();");
const syncBridgeIndex = app.indexOf("SyncKeyboardBridgeConfig();", loadConfigIndex);
assert(loadConfigIndex >= 0 && syncBridgeIndex > loadConfigIndex,
  "The bridge can start before migrated mappings are synchronized");
assert(app.includes("Atomic configuration replacement failed"),
  "Atomic configuration replacement lacks a deterministic self-test");

// P0 hold-to-talk: one DOWN, real audio, one UP, one finalization.
assert(includesAll(app, [
  'private static string NormalizeVoiceMode(string value)', 'return "hold";',
  '"按住说话 · 松开结束"', '"聚焦输入框后按住录音键说话，松开后完成转译"',
  'SafeCaptureArgument(config.voiceMode)',
]), "The host does not enforce the single hold-to-talk interaction");
assert(includesAll(bridge, [
  "HandleVoicePhysicalTransition", "voiceTransitionLock",
  "Voice key duplicate DOWN ignored", "Voice key duplicate UP ignored",
  "Local\\\\VibeMicVoiceKeyHeld", "Local\\\\VibeMicVoiceKeyReleased", "changed && !held",
]), "Record-key DOWN/UP edges are not de-duplicated and delivered exactly once");
assert(includesAll(capture, [
  'RecordingKernelVersion = "v1.0.3"', '" recording_kernel=" + RecordingKernelVersion',
  "Local\\\\VibeMicVoiceKeyPressed", "Local\\\\VibeMicVoiceKeyHeld",
  "ShouldRecoverHeldVoiceRequest", "VOICE KEY coalesced duplicate_source",
  "waiting_for_natural_stream_ms=120", "ATVV MIC_OPEN recovery requested",
  "REMOTE STREAM START", "REMOTE STREAM STOP", "voice_state_machine=v11",
  "WeTypeVoiceSessionController", "ClockedVirtualMicSink",
  "capture-health.json", "VibeMicRecordingStartCue", "VibeMicRecordingStopCue",
]), "The V1.0.3 recording kernel or its V1.2.1 host compatibility hooks are incomplete");
for (const discardedStateMachineToken of [
  "LONG DICTATION", "MIC_EXTEND", "PushToTalkSessionModel", "VoiceModePolicy",
  "voiceKeyReleasedEvent", "HandleHoldVoiceKeyReleased", "HOLD RELEASE force_finalize",
]) {
  assert(!capture.includes(discardedStateMachineToken),
    "Discarded long-session state machine returned: " + discardedStateMachineToken);
}
assert(!capture.includes("Clipboard.Set") && !capture.includes("ClipboardDeliveryPolicy") &&
  !capture.includes('KeyboardShortcutSender.Tap("ctrl+v"'),
  "Voice delivery must remain provider-direct and clipboard-free");
assert(includesAll(capture, [
  "OnConnectionStatusChanged", "connectionLostEvent.Set()", "voiceController.Dispose()",
  'WriteCommand(CloseCommand(), "mic_close")', 'SetCaptureHealthState("disconnected")',
]), "The stable kernel cannot expose disconnect state or release its provider and microphone resources");

// Real audio drives UI and provider state; stable audio parameters are locked.
const liveAudioIndex = app.indexOf('lineText.IndexOf("AUDIO LIVE START session="');
const recordingStateIndex = app.indexOf('transientFeedbackState = "recording"', liveAudioIndex);
assert(liveAudioIndex >= 0 && recordingStateIndex > liveAudioIndex,
  "Recording UI is not driven by real AUDIO LIVE START data");
assert(includesAll(capture, [
  "ClockedVirtualMicSink", "BlockingCollection<AudioNotification>(256)",
  "AUDIO LIVE START", "AUDIO LIVE STOP", "AUDIO LIVE FAILED",
  "VIRTUAL MIC DRAIN COMPLETE", "SpeechLeveler", "raw_rms_pct", "output_rms_pct",
]), "The live Bluetooth-to-VB-CABLE path lacks clocking, truthful state, or audio diagnostics");
assert(defaultConfig.stableVoiceProfileVersion === 11 && defaultConfig.gain === 1.0 &&
  defaultConfig.audioProcessingMode === "speech" && defaultConfig.drainMs === 180 &&
  defaultConfig.audioEndpointName === "CABLE Input" && defaultConfig.autoRouteVirtualMicrophone === true &&
  defaultConfig.autoLevel === true, "Validated audio profile changed");
assert(defaultConfig.inputMethod === "wechat" && defaultConfig.inputMethodHotkey === "ctrl+win" &&
  defaultConfig.inputMethodTrigger === "toggle" && defaultConfig.providerStartupDelayMs === 80,
  "The first formal-release WeChat profile changed");

// Only verified fixed keys plus four single-action direction mappings are public.
const expectedMappings = {
  "确认键": "enter",
  Home: "win+d",
  TV: "task-switcher",
  "功能键": "ctrl+c",
  "功能键:short": "ctrl+c",
  "功能键:long": "ctrl+v",
  "上键": "up",
  "下键": "down",
  "左键": "left",
  "右键": "right",
};
assert(defaultConfig.schemaVersion === 25 && defaultConfig.onboardingVersion === 8 &&
  defaultConfig.voiceMode === "hold" && defaultConfig.theme === "light" &&
  !Object.prototype.hasOwnProperty.call(defaultConfig, "customButtons"),
  "Default configuration is not the schema-25 stable hold baseline");
assert(JSON.stringify(defaultConfig.mappings) === JSON.stringify(expectedMappings),
  "Default mappings expose an unsupported control or changed a verified action");
for (const unsupported of ["电源键", "返回键", "音量 +", "音量 -"]) {
  assert(!Object.prototype.hasOwnProperty.call(defaultConfig.mappings, unsupported),
    `Unsupported default mapping is present: ${unsupported}`);
}
const mappingsPage = section(app, "private void BuildMappingsPage()", "private void BuildMappingsPageLegacy()");
assert(includesAll(mappingsPage, [
  'string[] keys = { "上键", "下键", "左键", "右键" }',
  'string[] selectorLabels = { "↑  上键", "↓  下键", "←  左键", "→  右键" }',
  "Point[] selectorLocations", '"识别实体键"', '"禁用这个方向键"',
  '"立即测试"', '"设为区域截图"', '"恢复方向导航"', '"保存并应用"',
  '"开机、返回和独立音量键未检测到稳定按键报告，因此不做映射。"',
]), "The active shortcut page is not the four-direction graphical workflow");
assert(includesAll(app, [
  'new ShortcutChoice("系统 · 区域截图", "win+shift+s")',
  'IsSupportedDirectionAction("win+shift+s")', 'CustomActionText("win+shift+s") != "区域截图"',
]), "The direction screenshot action is absent from the whitelist, UI, or native self-test");
assert(!includesAll(mappingsPage, ["open-url:"]) && !mappingsPage.includes("launch-client:") &&
  !mappingsPage.includes('"电源键", "返回键", "TV"'),
  "The active shortcut page still exposes unverified app, browser, or legacy controls");

// TV opens persistent Windows Task View; directions navigate and Enter confirms.
assert(includesAll(bridge, [
  'TapKeyChord(0x5B, 0x09, "任务视图已打开")',
  'command == "left" || command == "up" || command == "right" || command == "down"',
  'command == "confirm" ? 0x0D : 0x1B',
  "30000", "CloseTaskSwitcherIfActive", "HandleTaskSwitcherNavigation",
  'ParseShortcut("win+shift+s")', 'screenshotShortcut[2] != 0x53',
]), "TV does not provide a persistent, directional Windows Task View workflow");
assert(!bridge.includes("taskSwitcherAltDown"), "Task switching still depends on a held synthetic Alt key");
assert(!bridge.includes("HandleFunctionLeaderKey") && !bridge.includes("HandleFunctionLeaderCombo"),
  "Unsupported multi-key leader combinations remain in the input bridge");

// Onboarding, self-check, startup recovery, and privacy.
const onboarding = section(app, "private void ShowSetupWizard()", "private void ShowSetupWizardLegacy()");
assert(includesAll(onboarding, [
  '"了解按住说话"', '"检查 Windows 蓝牙"', '"配对并连接遥控器"',
  '"验证实体按键"', '"检查遥控器麦克风"', '"安装 VB-CABLE"',
  '"选择语音工具"', '"完成真实转译"', '"配置四个方向键"',
  '"设置开机自动可用"', '"完成与检查结果"',
  "config.onboardingStep = currentStep", "config.resumeSetupAfterRestart",
]), "The active onboarding flow is not the persisted eleven-step V1.2.1 setup");
assert(includesAll(cableInstaller, [
  "Get-FileHash", "b950e39f01af1d04ea623c8f6d8eb9b6ea5c477c637295fabf20631c85116bfb",
  "Get-AuthenticodeSignature", "Start-Process -FilePath $setupPath",
]), "VB-CABLE setup is not pinned, signature checked, and launched safely");
const selfCheck = section(app, "private SelfCheckReport BuildSelfCheckReport()", "private SelfCheckReport BuildSelfCheckReportLegacy()");
const selfCheckItems = selfCheck.match(/new SelfCheckItem\(/g) || [];
assert(selfCheckItems.length === 10, `Expected 10 active self-check items, found ${selfCheckItems.length}`);
for (const id of ["components", "bluetooth", "remote", "keys", "microphone", "cable", "profile", "provider", "startup", "session"]) {
  assert(selfCheck.includes(`new SelfCheckItem("${id}"`), `Self-check is missing ${id}`);
}
assert(includesAll(app, [
  'IsStableCaptureRuntime(runtime)', 'recording_kernel=v1.0.3', 'voice_state_machine=v11',
  'IsStableCaptureRuntime("long_dictation_state_machine=v3")',
]), "Self-check still requires the removed long-dictation runtime marker");
assert(includesAll(app, [
  "new Mutex(true", '"--background"', "SystemEvents.PowerModeChanged", "SystemEvents.SessionSwitch",
  "RecoverServicesAfterSystemChange", "RotateLogFile", "ExportDiagnostics", "BuildProblemSummary",
]), "Single-instance startup, resume recovery, bounded logs, or diagnostics are incomplete");
assert(includesAll(bridge, [
  "WM_INPUT_DEVICE_CHANGE", "RIDEV_DEVNOTIFY", "ScheduleRawInputRebind", "input-bridge-health.json",
]), "Bluetooth HID reconnect handling is incomplete");
assert(capture.includes("privacy=explicit_user_action") && !capture.includes('"audio_packet"'),
  "Diagnostics can persist packet-level audio without explicit user action");

// Default light theme plus a restrained explicit dark theme.
assert(includesAll(app, [
  'string[] navText = { "首页", "快捷键", "语音", "自检", "设置" }',
  'string preference = config == null ? "light"', 'preference == "dark"',
  "pageBackground = Color.FromArgb(25, 26, 31)", "cardBackground = Color.FromArgb(35, 37, 44)",
  'SecondaryButton("白天模式"', 'SecondaryButton("夜间模式"',
  'ApplyThemePreference("light")', 'ApplyThemePreference("dark")', "RebuildShellForTheme",
  "RemoteVisual", "DrawRecordingRipples",
]), "Navigation, default light theme, restrained dark theme, or recording feedback is incomplete");
assert(includesAll(screenshotScript, [
  "CaptureFullOnboarding", "Wait-ForChildText", '"00-setup-01-intro.png"', '"00-setup-11-summary.png"',
  "Set-ComboSelectionByText", '"03-shortcuts-screenshot.png"',
]), "Screenshot automation is not aligned with the eleven-step setup");

// Release, installer, CI, signing, and isolated hardware candidate safety.
assert(includesAll(release, [
  'Copy-Item (Join-Path $root "vibe-mic-config.default.json") $packageDir',
  'Copy-Item (Join-Path $root "NAudio.Core.dll") $packageDir',
  'Copy-Item (Join-Path $root "docs\\V1_2_1_TUTORIAL_ZH.md") $packageDocs',
  'Copy-Item (Join-Path $root "docs\\VERSION_ARCHIVE_ZH.md") $packageDocs',
  '"VibeFlow-Setup.exe"', '"SHA256SUMS.txt"', "Invoke-VibeFlowCodeSign",
]), "Release packaging, checksums, or signing are incomplete");
assert(!release.includes('(Join-Path $packageDir "vibe-mic-config.json")'),
  "Release packaging can overwrite an existing user configuration");
assert(includesAll(candidateBuild, [
  'if ($version -ne "1.2.1")', '"hardware-candidate"', 'hardwareAcceptancePassed = $false',
  'recordingKernel = "v1.0.3"', '"CANDIDATE_MANIFEST.json"', '"SHA256SUMS.txt"',
  '"Programs\\Vibe Flow Remote"', '"docs\\V1_2_1_TUTORIAL_ZH.md"',
  '"docs\\VERSION_ARCHIVE_ZH.md"',
]) && !candidateBuild.includes("VibeFlow-Setup.exe"),
  "Hardware candidate packaging can overwrite or masquerade as the installed release");
assert(includesAll(dependencyRestore, [
  'Check = "lib\\netstandard2.0\\NAudio.Core.dll"', 'Check = "lib\\netstandard2.0\\NAudio.Wasapi.dll"',
]) && includesAll(captureBuild, [
  'naudio.core.2.2.1\\lib\\netstandard2.0\\NAudio.Core.dll',
  'naudio.wasapi.2.2.1\\lib\\netstandard2.0\\NAudio.Wasapi.dll',
]), "Build dependencies do not use the reproducible NuGet layout");
assert(includesAll(installer, [
  '#define MyAppVersion "1.2.1"', "PrivilegesRequired=lowest", "WaitForVibeFlowExit",
  "ConfigRequestsStartup", "RestoreConfiguredStartupRegistration", "vibe-mic-config.json.bak",
]), "Installer upgrades cannot safely preserve settings and startup state");
assert(includesAll(workflow, [
  "actions/checkout@v7", "actions/setup-node@v7", "actions/upload-artifact@v7",
  "WINDOWS_SIGNING_PFX_BASE64", "WINDOWS_SIGNING_PFX_PASSWORD",
]), "GitHub Actions validation, artifact upload, or signing secrets are incomplete");

// Current documentation must describe only the shipped V1.2.1 interaction.
const userDocs = { readme, englishReadme, guide, versionTutorial, versionArchive, quickStart, releaseNotes, githubReleaseBody, continuousGuide, architecture, versionDoc };
for (const [name, document] of Object.entries(userDocs)) {
  assert(document.includes("1.2.1"), `${name} does not identify V1.2.1`);
  assert(!document.includes("单击录音键开始") && !document.includes("再次单击结束"),
    `${name} still instructs users to use click-toggle recording`);
}
for (const [name, document] of Object.entries({ readme, guide, quickStart, releaseNotes, continuousGuide })) {
  assert(document.includes("按住") && document.includes("松开"), `${name} does not explain hold-to-talk`);
  assert(document.includes("60 秒") || document.includes("60-second"),
    `${name} does not explain the current RC003 session limit`);
}
for (const [name, document] of Object.entries({ readme, guide, quickStart, releaseNotes, githubReleaseBody })) {
  for (const claim of ["开机键、返回键和 TV 键", "音量 + / 音量 -", "应用、网页、系统动作"])
    assert(!document.includes(claim), `${name} still advertises unsupported controls: ${claim}`);
}
assert(includesAll(readme, [
  "docs/USER_GUIDE_ZH.md", "docs/images/01-overview.png", "VibeFlow-Setup.exe",
  "Source code (zip)", "vibe-flow-community.png", "docs/VERSION_ARCHIVE_ZH.md",
  "docs/V1_2_1_TUTORIAL_ZH.md", "docs/images/03-shortcuts-screenshot.png",
]), "README lacks the installer, tutorial, screenshot, or community entry points");
assert(includesAll(guide, [
  "CABLE Input", "CABLE Output", "Typeless", "豆包", "Windows 语音输入",
  "11 步", "10 项", "现象 | 先检查 | 处理方法", "vibe-flow-community.png",
  "系统 · 区域截图", "images/03-shortcuts-screenshot.png",
]), "The beginner guide lacks providers, onboarding, self-check, troubleshooting, or community help");
assert(includesAll(versionTutorial, [
  "用户友好稳定版", "images/vibe-flow-community.png", "11 步", "10 项",
  "CABLE Input", "CABLE Output", "系统 · 区域截图", "Win + Shift + S",
  "images/03-shortcuts-screenshot.png", "VERSION_ARCHIVE_ZH.md",
]), "The V1.2.1 illustrated tutorial is incomplete");
for (const releaseVersion of ["v1.2.1", "v1.2.0", "v1.1.0", "v1.0.3", "v1.0.2", "v1.0.1", "v1.0.0"]) {
  const base = `https://github.com/richlearntodo-debug/vibe-flow/releases/download/${releaseVersion}/`;
  for (const asset of ["VibeFlow-Setup.exe", "Vibe-Flow-Windows-x64.zip", "SHA256SUMS.txt"])
    assert(versionArchive.includes(base + asset), `Version archive is missing ${releaseVersion}/${asset}`);
}
assert(includesAll(versionDoc, [
  "Configuration schema: `25`", "Onboarding version: `8`", "Voice mode: `hold`",
  "Recording kernel: `v1.0.3`",
]), "Version metadata documentation is stale");
assert(includesAll(architecture, [
  "exact `v1.0.3` recording kernel", "natural ATVV stream-start and stream-stop",
  "no physical-segment continuation, `MIC_EXTEND`, or long-dictation controller",
]), "Architecture does not document the restored V1.0.3 recording kernel");
assert(voiceResearch.includes("Confirmed root cause of missing sentence tails") &&
  voiceResearch.includes("queue_drops=0") && voiceResearch.includes("wait 350 ms"),
  "The verified sentence-tail root cause was lost from engineering documentation");
assert(includesAll(hardwareAcceptance, [
  "100 次实体按下/松开", "约 60 秒", "无双麦克风", "聚焦输入框",
  "不提交版本标签", "增益 | `1.0`", "尾音排空 | `180 ms`", "录音内核 | `v1.0.3`",
]), "The V1.2.1 physical hardware release gate is incomplete");

console.log("Vibe Flow V1.2.1 validation passed.");
