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
  "scripts/Get-StableCaptureBinary.ps1",
  "scripts/Measure-HardwareAcceptance.ps1",
  "scripts/VibeMic.cs",
  "scripts/VibeMicAtvvCapture.cs",
  "scripts/VoxDeckInputBridge.cs",
  "scripts/capture-ui-screenshots.ps1",
  "scripts/diagnostics/Invoke-Rc003KeyboardIsolation.ps1",
  "scripts/diagnostics/Test-Rc003ExclusiveGatt.ps1",
  "driver/rc003-filter/README.md",
  "driver/rc003-filter/Build-Driver.ps1",
  "driver/rc003-filter/New-DriverCandidate.ps1",
  "driver/rc003-filter/src/public.h",
  "driver/rc003-filter/src/rc003_filter.h",
  "driver/rc003-filter/src/rc003_filter.c",
  "driver/rc003-filter/src/VibeFlowRc003Filter.inx",
  "driver/rc003-filter/src/VibeFlowRc003Filter.vcxproj",
  "installer/VibeFlow.iss",
  "installer/languages/ChineseSimplified.isl",
  "docs/USER_GUIDE_ZH.md",
  "docs/V1_2_1_TUTORIAL_ZH.md",
  "docs/V1_3_USER_GUIDE_ZH.md",
  "docs/VERSION_ARCHIVE_ZH.md",
  "docs/RELEASE_NOTES_ZH.md",
  "docs/GITHUB_RELEASE_BODY_ZH.md",
  "docs/CONTINUOUS_DICTATION_ZH.md",
  "docs/CODE_SIGNING_ZH.md",
  "docs/ARCHITECTURE.md",
  "docs/VOICE_PIPELINE_RESEARCH.md",
  "docs/V1_2_HARDWARE_ACCEPTANCE_ZH.md",
  "docs/V1_3_PREVIEW_ZH.md",
  "docs/V1_3_HARDWARE_ACCEPTANCE_ZH.md",
  "docs/V1_4_PREVIEW_ZH.md",
  "docs/RC003_DRIVER_LAB_ZH.md",
  "docs/images/00-first-run.png",
  "docs/images/00-setup-01-device.png",
  "docs/images/00-setup-02-remote.png",
  "docs/images/00-setup-03-audio.png",
  "docs/images/00-setup-04-dictation.png",
  "docs/images/00-setup-05-ready.png",
  "docs/images/01-overview.png",
  "docs/images/02-dictation.png",
  "docs/images/03-shortcuts.png",
  "docs/images/03-shortcuts-screenshot.png",
  "docs/images/04-diagnostics.png",
  "docs/images/05-settings.png",
  "docs/images/06-transcription-tools.png",
  "docs/images/vibe-flow-community.png",
  ".github/actionlint.yaml",
  ".github/ISSUE_TEMPLATE/bug_report.yml",
  ".github/workflows/driver-candidate.yml",
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
const stableCaptureResolver = read("scripts/Get-StableCaptureBinary.ps1");
const hardwareAcceptanceTool = read("scripts/Measure-HardwareAcceptance.ps1");
const workflow = read(".github/workflows/validate.yml");
const defaultConfig = JSON.parse(read("vibe-mic-config.default.json"));
const packageJson = JSON.parse(read("package.json"));
const readme = read("README.md");
const englishReadme = read("README_VIBE_MIC.md");
const guide = read("docs/USER_GUIDE_ZH.md");
const versionTutorial = read("docs/V1_2_1_TUTORIAL_ZH.md");
const v13Guide = read("docs/V1_3_USER_GUIDE_ZH.md");
const versionArchive = read("docs/VERSION_ARCHIVE_ZH.md");
const quickStart = read("QUICK_START_ZH.md");
const releaseNotes = read("docs/RELEASE_NOTES_ZH.md");
const githubReleaseBody = read("docs/GITHUB_RELEASE_BODY_ZH.md");
const continuousGuide = read("docs/CONTINUOUS_DICTATION_ZH.md");
const architecture = read("docs/ARCHITECTURE.md");
const versionDoc = read("VIBE_MIC_VERSION.md");
const voiceResearch = read("docs/VOICE_PIPELINE_RESEARCH.md");
const hardwareAcceptance = read("docs/V1_2_HARDWARE_ACCEPTANCE_ZH.md");
const previewGuide = read("docs/V1_3_PREVIEW_ZH.md");
const v14PreviewGuide = read("docs/V1_4_PREVIEW_ZH.md");
const v13HardwareAcceptance = read("docs/V1_3_HARDWARE_ACCEPTANCE_ZH.md");
const rc003DriverLabGuide = read("docs/RC003_DRIVER_LAB_ZH.md");
const gitignore = read(".gitignore");
const rc003FilterPublic = read("driver/rc003-filter/src/public.h");
const rc003FilterHeader = read("driver/rc003-filter/src/rc003_filter.h");
const rc003FilterSource = read("driver/rc003-filter/src/rc003_filter.c");
const rc003FilterInf = read("driver/rc003-filter/src/VibeFlowRc003Filter.inx");
const rc003FilterProject = read("driver/rc003-filter/src/VibeFlowRc003Filter.vcxproj");
const rc003FilterReadme = read("driver/rc003-filter/README.md");
const rc003DriverBuild = read("driver/rc003-filter/Build-Driver.ps1");
const rc003CandidateBuild = read("driver/rc003-filter/New-DriverCandidate.ps1");
const rc003CandidateWorkflow = read(".github/workflows/driver-candidate.yml");
const actionlintConfig = read(".github/actionlint.yaml");
const isolationHelper = read("scripts/diagnostics/Invoke-Rc003KeyboardIsolation.ps1");
const exclusiveGattTest = read("scripts/diagnostics/Test-Rc003ExclusiveGatt.ps1");

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
  'ProductRelease = "1.4.0"',
  'StableCaptureBinaryVersion = "1.2.1"',
  'AssemblyFileVersion("1.4.0.0")',
  'AssemblyInformationalVersion("1.4.0-preview")',
  'ConfigSchemaVersion = 31',
  'CurrentOnboardingVersion = 9',
  'OnboardingStepCount = 5',
]), "Application identity or V1.4 preview configuration metadata is inconsistent");
assert(packageJson.version === "1.4.0", "package.json version is not aligned with the preview app");
assert(capture.includes('AssemblyFileVersion("1.2.1.0")') && bridge.includes('AssemblyFileVersion("1.4.0.0")'),
  "The stable capture or preview bridge binary version is inconsistent");
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
assert(gitignore.includes("!scripts/Install-VBCable.ps1"),
  "The required VB-CABLE installer helper is still excluded from clean checkouts");
assert(gitignore.includes("!scripts/Get-StableCaptureBinary.ps1"),
  "The pinned stable-capture resolver is excluded from clean checkouts");
assert(gitignore.includes("!scripts/Measure-HardwareAcceptance.ps1"),
  "The V1.3 hardware acceptance evidence tool is excluded from clean checkouts");
assert(includesAll(app, [
  "Schema 26 migration discarded a valid gesture mapping",
  "Schema 29 shortcut Profile migration discarded the active mapping",
  "Profile-specific mapping did not survive a manual switch round trip",
  "Profile export leaked voice settings or lost mappings",
  "Configuration migration is not idempotent",
  "BuildKeyboardBridgeDocument", "ComputeBridgeConfigRevision",
  "UI configuration did not normalize to the expected bridge actions",
  "Bridge configuration revision did not change with its action mapping",
]), "Configuration migration or UI-to-runtime integrity regression coverage is incomplete");
assert(includesAll(app, [
  "Local\\\\VibeMicReloadKeyboardConfig", "WaitForBridgeConfigRevision",
  "BridgeHealthAcknowledgesRevision", 'health.TryGetValue("config_revision"',
  "config_ack_timeout", "expectedKeyboardConfigRevision",
]), "The host cannot require a runtime ACK for the saved mapping revision");

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

// Only controls with verified Windows input events are public. Power stays as
// an internal disabled compatibility mapping and is not shown in the editor.
const expectedMappings = {
  "确认键": "enter",
  Home: "win+d",
  "Home:short": "win+d",
  "Home:long": "none",
  TV: "task-switcher",
  "功能键": "ctrl+c",
  "功能键:short": "ctrl+c",
  "功能键:long": "ctrl+v",
  "上键": "up",
  "下键": "down",
  "左键": "left",
  "右键": "right",
};
assert(defaultConfig.schemaVersion === 31 && defaultConfig.onboardingVersion === 9 &&
  defaultConfig.voiceMode === "hold" && defaultConfig.theme === "light" &&
  defaultConfig.inputRoutingMode === "strict" && defaultConfig.mappingPreset === "general" &&
  !Object.prototype.hasOwnProperty.call(defaultConfig, "customButtons"),
  "Default configuration is not the schema-31 stable hold baseline");
assert(JSON.stringify(defaultConfig.mappings) === JSON.stringify(expectedMappings),
  "Default mappings expose an unsupported control or changed a verified action");
assert(defaultConfig.activeShortcutProfileId === "general" &&
  Array.isArray(defaultConfig.shortcutProfiles) && defaultConfig.shortcutProfiles.length === 4,
  "Default shortcut Profiles or active Profile are missing");
const expectedProfiles = new Map([
  ["general", "通用导航"], ["vibe-coding", "Vibe Coding"],
  ["browser-ai", "浏览器 AI"], ["terminal-agent", "Terminal Agent"],
]);
for (const profile of defaultConfig.shortcutProfiles) {
  assert(expectedProfiles.get(profile.id) === profile.name,
    `Unexpected official shortcut Profile: ${profile.id}/${profile.name}`);
  assert(profile.preset === profile.id && Object.keys(profile.mappings).length === 12,
    `Official Profile is malformed: ${profile.id}`);
  for (const forbidden of ["gain", "audioEndpointName", "inputMethod", "inputMethodHotkey", "voiceMode"])
    assert(!Object.prototype.hasOwnProperty.call(profile, forbidden),
      `Shortcut Profile leaked voice configuration: ${profile.id}/${forbidden}`);
}
assert(defaultConfig.shortcutProfiles.find(profile => profile.id === "browser-ai").mappings["左键"] === "browserback",
  "Browser AI does not use the dedicated Browser Back action");
for (const unsupported of ["电源键", "返回键", "音量 +", "音量 -"]) {
  assert(!Object.prototype.hasOwnProperty.call(defaultConfig.mappings, unsupported),
    `Unsupported default mapping is present: ${unsupported}`);
}
const mappingsPage = section(app, "private void BuildMappingsPage()", "private void BuildMappingsPageV13Legacy()");
assert(includesAll(mappingsPage, [
  'AddPageTitle("快捷键"', "动作只响应已确认的 RC003 设备事件", "安全直通", "RemoteVisual",
  '"Home:short", "Home:long"',
  "AddFixedVoiceOverviewCard", "ShowMappingActionPicker", "TestMappingAction",
  '"按住听写 · 松开结束"', 'NewLabel("手动切换"',
  "SwitchShortcutProfile", "CreateShortcutProfile", "RenameActiveShortcutProfile",
  "DeleteActiveShortcutProfile", "ImportShortcutProfile", "ExportActiveShortcutProfile",
  "shortEdit.Enabled = hardwareReady", "longEdit.Enabled = hardwareReady",
]), "The active shortcut page is not the verified device-protected mapping workflow");
assert(includesAll(app, [
  'new ShortcutChoice("系统 · 区域截图", "win+shift+s")',
  'IsSupportedMappingAction("win+shift+s")', 'CustomActionText("win+shift+s") != "区域截图"',
  '"select-app:prompt"', '"open-url:prompt"', '"shortcut:prompt"',
  "Schema 25 migration discarded a valid custom mapping", "GetInstalledApplicationChoices",
  '"custom-button-test-result.json"', "MappingActionTestResult", "测试成功", "测试失败",
]), "Custom app, web, shortcut, or screenshot actions are absent");
assert(!mappingsPage.includes('"返回键"') && !mappingsPage.includes('"音量 +"') &&
  !mappingsPage.includes('"音量 -"') && !mappingsPage.includes('"电源键"'),
  "The active shortcut page exposes an unsupported physical control");
assert(includesAll(app, [
  "IsPersistableMappingAction", "PersistedMappingMatches", "MAPPING SAVE persisted=true",
  "Local application action did not survive config persistence and bridge generation",
  'normalized == "none"', 'Convert.ToBoolean(generatedDown["enabled"])',
]), "Local application actions can report success without surviving persistence");
assert(includesAll(app, [
  'Type.GetTypeFromProgID("Shell.Application")', '"shell:AppsFolder"',
  'Type.GetTypeFromProgID("WScript.Shell")', 'Environment.SpecialFolder.CommonPrograms',
  '"SOFTWARE\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\App Paths"',
  '"SOFTWARE\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall"',
  'SHParseDisplayName', 'ShellFileInfoPidl', 'GetApplicationDisplayName',
  "Start application Unicode invariant failed", "FinalReleaseComObject",
  "name.IndexOf('\\uFFFD')", "APPLICATION PICKER loaded=true",
]), "Installed application discovery can corrupt localized Windows app names");
assert(includesAll(app, [
  'mappings["左键"] = "browserback"', 'action, "alt+left"',
  'generatedBrowserBack["shortcut"]', 'Schema 30 browser-back migration',
]) && includesAll(bridge, ['{"browserback", 0xA6}', 'VkFromName("browserback") != 0xA6']),
  "Browser Back can collide with the physical Left key or bypass migration");
assert(includesAll(app, [
  'CreateStarterShortcutProfile("general")', 'CreateStarterShortcutProfile("vibe-coding")',
  'CreateStarterShortcutProfile("browser-ai")', 'CreateStarterShortcutProfile("terminal-agent")',
  'format = "vibe-flow-shortcut-profile"', "CaptureActiveShortcutProfileMappings",
  "ProjectActiveShortcutProfile", "Profile 仅保存快捷键，不包含任何语音参数",
  'ApplyMappingPreset(presetFixture, "editing")', 'ApplyMappingPreset(presetFixture, "review")',
  "ConfirmMappingPresetChange", "Home 与功能键的自定义配置会保留",
  "Preset change detection invariant failed",
  "ImportConfig", "RestorePreviousConfig", "NormalizeImportedConfig",
  "Imported config did not preserve mappings and freeze voice settings",
  "SanitizeDiagnosticText", "Diagnostic privacy redaction invariant failed",
]), "Presets, configuration recovery, or privacy-safe diagnostics are incomplete");

assert(includesAll(bridge, [
  "ActionExecutionReceipt", "RecordActionExecution", "last_execution_sequence",
  'health["last_execution_button"]', 'health["last_execution_trigger"]',
  'health["last_execution_action"]', 'health["last_execution_profile_id"]',
  'health["last_execution_profile_name"]', 'health["last_execution_success"]',
  "activeShortcutProfileId", "activeShortcutProfileName",
]), "The bridge does not publish an actual action execution receipt");
assert(includesAll(app, [
  "LastExecutionSequence", "LastExecutionAction", "LastExecutionProfileName",
  "LastExecutionSuccess", "UpdateActionReceipt", "最近一次快捷操作",
  "执行失败，请打开自检查看",
]), "The homepage does not present the latest action execution receipt");

// Raw Input owns RC003 actions because it carries the device handle. A
// device-blind hook must pass non-voice candidates through; suppressing them
// prevents this Windows Bluetooth stack from delivering WM_INPUT at all. The
// optional signed filter provides exact-device suppression when available.
assert(includesAll(bridge, [
  "RawKeyboardEdgeTracker", "RouteAuthoritativeRawKeyboard",
  "returning 1 here prevents Windows from delivering",
  "RC003 action routed source=raw_input", "delivery=native_passthrough",
  'health["routing_authority"] = filterHealthy ? "device_filter" : "raw_input"',
  'health["routing_isolation"] = filterHealthy ? "exact_device" : "native_passthrough"',
  'health["raw_remote_edges"]', 'health["raw_action_edges"]',
  "Vibe Flow device-aware keyboard hook", "WM_APP_REINSTALL_HOOK",
]), "RC003 source isolation or its deterministic regression tests are incomplete");
assert(includesAll(app, [
  "Retired compatibility routing was not normalized to strict",
  "设备识别：只有带 RC003 身份的事件可以执行遥控器动作",
  "Raw Input 安全直通", "设备级精确隔离",
]) && !app.includes("compatibility.CheckedChanged"),
  "The known keyboard-hijacking compatibility mode is still user-selectable");
assert(!bridge.includes("RouteCompatibility") &&
  !bridge.includes("Compatibility remote inferred") &&
  !bridge.includes("UnconfirmedIsRemote"),
  "The bridge can still infer an unidentified physical-keyboard event as RC003");
assert(includesAll(bridge, [
  "Rc003FilterClient", "Rc003FilterProtocol", "BuildRc003FilterSuppressionMask",
  "RC003 device filter ready; ordinary keyboards are passthrough",
  "Raw Input native-passthrough fallback active", "IsGenerationActive", "user_mode_event_queue_full",
  'health["rc003_filter_healthy"]', 'health["rc003_filter_dropped_events"]',
  "IoctlGetInfo = 0x80006400U", "IoctlSetPolicy = 0x8000A404U",
  "IoctlHeartbeat = 0x8000A408U", "IoctlReadEvents = 0x8000640CU",
  "IoctlDisarm = 0x8000A410U",
]), "The optional RC003 filter client lacks protocol checks, health, or strict fallback");
const hookFilterGuard = bridge.indexOf("ShouldBypassHookForRc003Filter(IsRc003FilterHealthy()");
const hookNativePassthrough = bridge.indexOf("bool nonVoiceCandidate", hookFilterGuard);
assert(hookFilterGuard >= 0 && hookNativePassthrough > hookFilterGuard,
  "A healthy RC003 filter does not release matching physical-keyboard input before hook routing");

assert(includesAll(rc003FilterPublic, [
  "RC003_FILTER_API_VERSION 1U", "RC003_FILTER_MAGIC 0x43524656U",
  "RC003_FILTER_SCAN_CODE_COUNT 256U", "RC003_FILTER_EVENT_CAPACITY 128U",
  "RC003_FILTER_HEARTBEAT_TIMEOUT_100NS (2ULL * 1000ULL * 1000ULL * 10ULL)",
  "IOCTL_RC003_FILTER_GET_INFO", "IOCTL_RC003_FILTER_SET_POLICY",
  "IOCTL_RC003_FILTER_HEARTBEAT", "IOCTL_RC003_FILTER_READ_EVENTS",
  "IOCTL_RC003_FILTER_DISARM", "#pragma pack(push, 1)",
]), "RC003 filter public protocol is incomplete");
assert(includesAll(rc003FilterHeader, ["CONNECT_DATA UpperConnectData", "volatile LONG CountedAttached"]),
  "RC003 per-device lifecycle context is incomplete");
assert(includesAll(rc003FilterSource, [
  "sizeof(RC003_FILTER_INFO) == 40",
  "sizeof(RC003_FILTER_POLICY) == 272",
  "sizeof(RC003_FILTER_EVENT) == 32",
  "FIELD_OFFSET(RC003_FILTER_EVENT_BATCH, Events) == 24",
  "WdfFdoInitSetFilter", "Rc003KeyboardServiceCallback", "Rc003PolicyIsFreshLocked",
  "InterlockedExchange(&context->CountedAttached, 0)", "Rc003ClearEventsLocked();",
  "g_State.AttachedDeviceCount <= 0", "status = STATUS_NOT_IMPLEMENTED",
  "Rc003DisarmLocked();", "WdfControlFinishInitializing",
]), "RC003 filter lifecycle, queue isolation, or fail-open behavior is incomplete");
const policyClearIndex = rc003FilterSource.indexOf("Rc003ClearEventsLocked();", rc003FilterSource.indexOf("IOCTL_RC003_FILTER_SET_POLICY"));
const policyAssignIndex = rc003FilterSource.indexOf("g_State.Policy = *policy", policyClearIndex);
assert(policyClearIndex >= 0 && policyAssignIndex > policyClearIndex,
  "RC003 filter can expose stale events after a policy generation change");
assert(!rc003FilterSource.includes("VibeMicAtvvCapture") && !rc003FilterSource.includes("audio"),
  "Kernel input filtering crossed into the frozen voice implementation");
assert(includesAll(rc003FilterInf, [
  "Class=Keyboard", "ErrorControl=0", "HKR,,UpperFilters,0x00010008",
  "HID\\{00001812-0000-1000-8000-00805F9B34FB}_Dev_VID&012717_PID&32B8_REV&00A4",
]) && !rc003FilterInf.includes("Class\\{4D36E96B-E325-11CE-BFC1-08002BE10318}\\UpperFilters"),
  "RC003 INF is not an exact per-device append-only upper filter");
assert(includesAll(rc003FilterReadme, [
  "per-device upper filter", "never a keyboard class filter", "fail-open",
  "Microsoft-signed catalog", "separate driver-test computer", "vibe-flow-driver-lab",
  "docs/RC003_DRIVER_LAB_ZH.md",
]), "RC003 driver safety and release gates are undocumented");
assert(includesAll(rc003DriverBuild, [
  "vswhere.exe", "Windows Kits\\Installed Roots", "microsoft.windows.wdk.x64",
  "/restore", 'Filter "VibeFlowRc003Filter.cat"',
  "$inf.FullName,$sys.FullName,$cat.FullName",
]), "RC003 driver build cannot reliably locate tools or verify its catalog output");
assert(includesAll(rc003FilterProject, [
  "<TargetFramework>native</TargetFramework>",
  "native,Version=v0.0", "<RestorePackages>true</RestorePackages>",
  'PackageReference Include="Microsoft.Windows.WDK.x64" Version="10.0.26100.6584"',
  'PackageReference Include="Microsoft.Windows.SDK.cpp.x64" Version="10.0.26100.6584"',
  "<PrivateAssets>all</PrivateAssets>",
]), "RC003 driver project does not pin the official WDK build dependency");
assert(includesAll(rc003CandidateBuild, [
  "driver-lab-candidate", "exact-device-upper-filter-append", "heartbeatFailOpenMs = 2000",
  "microsoftSigned = $false", "productionInstallApproved = $false", "releaseApproved = $false",
  "DRIVER_CANDIDATE_MANIFEST.json", "SHA256SUMS.txt", "TEST_ONLY.txt",
  'HKR,,UpperFilters,0x00010008,\"VibeFlowRc003Filter\"',
]) && !rc003CandidateBuild.includes("pnputil") && !rc003CandidateBuild.includes("BUILD_RELEASE.ps1"),
  "The Driver Lab candidate is not isolated, auditable, or non-installing");
assert(includesAll(rc003CandidateWorkflow, [
  "workflow_dispatch", "runs-on: windows-2022", "vs-version: '[17.0,18.0)'",
  "msbuild-architecture: x64", "VibeFlow-RC003-CloudCompile-",
  "retention-days: 1", "run_driver_lab", "needs: cloud-compile",
  "runs-on: [self-hosted, Windows, X64, vibe-flow-driver-lab]",
  "environment: driver-lab", 'VIBE_FLOW_DRIVER_LAB -ne "1"',
  "New-DriverCandidate.ps1", "retention-days: 3", "actions/upload-artifact@v7",
]) && !rc003CandidateWorkflow.includes("softprops/action-gh-release") &&
  !rc003CandidateWorkflow.includes("gh release") &&
  !rc003CandidateWorkflow.includes("pnputil") &&
  !rc003CandidateWorkflow.includes("BUILD_RELEASE.ps1"),
  "The Driver Lab workflow can bypass isolation or publish/install its candidate");
assert(includesAll(actionlintConfig, [
  "self-hosted-runner:", "labels:", "vibe-flow-driver-lab", "config-variables: null",
]), "actionlint does not recognize the isolated Driver Lab runner label");
assert(includesAll(rc003DriverLabGuide, [
  "VIBE_FLOW_DRIVER_LAB=1", "productionInstallApproved", "releaseApproved",
  "pnputil /add-driver", "pnputil /delete-driver", "不要使用 `/force`",
  "云端只编译", "VibeFlow-RC003-CloudCompile-", "2 秒", "10,000",
  "Secure Boot", "Memory Integrity", "Microsoft 驱动签名",
]) && !rc003DriverLabGuide.includes("/delete-driver oemXX.inf /uninstall /force"),
  "The Driver Lab guide lacks isolation, rollback, fail-open, or release gates");
assert(!isolationHelper.includes('arguments += "/force"') &&
  !isolationHelper.includes('Invoke-PnpUtil "/disable-device"') &&
  isolationHelper.includes("exclusive-GATT experiment is retired"),
  "The diagnostic helper can still force-disable the critical keyboard child");
assert(exclusiveGattTest.includes("This experiment is retired") &&
  !exclusiveGattTest.includes("Stop-TestProcesses") && !exclusiveGattTest.includes("pnputil") &&
  !exclusiveGattTest.includes("Start-Process"),
  "The retired exclusive-GATT test can still stop the app or alter device state");
assert(includesAll(bridge, [
  "Local\\\\VibeMicReloadKeyboardConfig", "RegisterWaitForSingleObject",
  'ReloadConfig(true, "reload_event")', "ReloadConfigIfChanged();",
  'health["config_version"]', 'health["config_revision"]',
  'health["config_loaded_at"]', 'health["config_mapping_count"]',
  "Persisted bridge configuration did not resolve to its configured runtime actions",
]), "Hook, Raw Input, and HID do not share an acknowledged hot-reloaded configuration");
assert(includesAll(bridge, [
  "existing_window_activation_failed", "out bool existingWindowFound",
  "if (existingWindowFound)", "窗口切换失败",
]), "An APP activation failure can fall through and launch a duplicate process");
const rawInputHandler = section(bridge, "private static void HandleRawInput", "private static void QueueRawAction");
assert(rawInputHandler.includes("ReloadConfigIfChanged();"),
  "Raw Input can resolve actions before checking the current configuration");
const deviceIdentityIndex = rawInputHandler.indexOf("if (!IsRc003Device(deviceName))");
const rawActionIndex = rawInputHandler.indexOf("RouteAuthoritativeRawKeyboard(mapping");
assert(deviceIdentityIndex >= 0 && rawActionIndex > deviceIdentityIndex,
  "An RC003 action can run before Raw Input confirms the source device");
const nonVoiceHookBranch = section(bridge, "bool nonVoiceCandidate", "if (mapping != null && mapping.enabled)");
assert(nonVoiceHookBranch.includes("return CallNextHookEx") &&
  !nonVoiceHookBranch.includes("return (IntPtr)1") &&
  !nonVoiceHookBranch.includes("QueueMapping("),
  "The device-blind keyboard hook can suppress or execute a non-voice RC003 candidate");
assert(!bridge.includes("RouteDeviceScopedHookEvent(") && !bridge.includes("deviceInputGate."),
  "The retired Hook-to-Raw pairing route is still active");

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
const onboarding = section(app, "private void ShowSetupWizard()", "private void ShowSetupWizardElevenStepLegacy()");
assert(includesAll(onboarding, [
  '"确认设备与用法"', '"连接并测试遥控器"', '"准备本地音频通道"',
  '"选择工具并完成听写"', '"开机即用"',
  "config.onboardingStep = currentStep", "config.resumeSetupAfterRestart",
  'config.onboardingStep = 2', '"请确认转译文字已进入上方测试框"',
  "textInsertionConfirmed", "confirmedTextLength",
]), "The active onboarding flow is not the persisted five-task setup");
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
  "ResolveBluetoothSelfCheckState", "bluetoothConfirmedByBridge",
  "bridge.Healthy && bridge.RawInputDevicePresent",
  "Get-PnpDevice -Class HIDClass", "ReadToEndAsync",
  "Bluetooth self-check evidence fallback invariant failed",
]), "Bluetooth self-check can still report a false failure while the live RC003 route is healthy");
assert(includesAll(selfCheck, [
  '"音频与语音工具唤起链路"', "应用不会读取输入框文字",
  "最终文字请目视确认", "语音工具收到开始与结束指令",
]) && !selfCheck.includes('"完整音频与转译链路"'),
"Self-check still claims it can automatically verify external text insertion");
assert(includesAll(app, [
  "new Mutex(true", '"--background"', "SystemEvents.PowerModeChanged", "SystemEvents.SessionSwitch",
  "RecoverServicesAfterSystemChange", "RotateLogFile", "ExportDiagnostics", "BuildProblemSummary",
  "InspectProcessTopology", "duplicate_same_root", "root_conflict",
]), "Single-instance startup, resume recovery, bounded logs, or diagnostics are incomplete");
assert(includesAll(app, [
  "DisposePageControls", "DisposeOwnedControlResources", '"--ui-resource-test"',
  "GetGuiResources", "switches=300", "navigationActiveFont",
  "visualTimer.Interval != 500", "visualTimer.Interval != 250",
]) && !app.includes("content.Controls.Clear()"),
"Page navigation can leak WinForms controls or lacks a repeatable resource stress test");
assert(includesAll(app, [
  'Environment.SpecialFolder.LocalApplicationData', '"Vibe Flow Remote", "UserData"',
  "MigrateLegacyUserConfig", "Central user configuration was overwritten by legacy state",
  'Path.Combine(localAppData, "Programs", "Vibe Flow Remote")', "ReadStartupExecutableDirectory",
  "ReconcileLaunchAtStartupRegistration", "ShouldRegisterStartup",
]), "User state can still follow the executable directory or retain a stale startup entry");
const stopBridge = section(app, "private void StopKeyboardBridge()", "private bool WaitForBridgeConfigRevision");
assert(stopBridge.includes("runningPath.Equals(expected") &&
  !stopBridge.includes('foreach (Process process in Process.GetProcessesByName("VoxDeckInputBridge"))\n            {\n                try { if (!process.WaitForExit'),
  "Stopping one installation can still terminate bridges from other portable roots");
const stopOrphanCapture = section(app, "private void StopOrphanCaptureCore()", "private bool TryAttachExistingCapture()");
assert(stopOrphanCapture.includes("runningPath.Equals(expected") &&
  stopOrphanCapture.indexOf('SignalEvent("Local\\\\VibeMicStopCapture")') > stopOrphanCapture.indexOf("ownedOrphans.Count > 0"),
  "Orphan cleanup can still stop a capture process from another portable root");
const bridgeSnapshotReader = section(app, "private BridgeHealthSnapshot ReadKeyboardBridgeHealth()", "private static string[] ReadLogTailLines");
assert(bridgeSnapshotReader.includes("snapshot.HookInstalled && snapshot.RawInputRegistered") &&
  !bridgeSnapshotReader.includes("snapshot.HookInstalled && snapshot.RawInputRegistered && snapshot.RawInputDevicePresent"),
  "A sleeping RC003 is still treated as a crashed input bridge");
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
  'SecondaryButton("白天模式"', 'SecondaryButton("夜间模式"', 'SecondaryButton("跟随 Windows"',
  'ApplyThemePreference("light")', 'ApplyThemePreference("dark")', 'ApplyThemePreference("system")',
  "SystemEvents.UserPreferenceChanged", "OnUserPreferenceChanged", "RebuildShellForTheme",
  "TryEnableHighDpi", "SetProcessDpiAwarenessContext", "AutoScaleMode = AutoScaleMode.Dpi",
  "ClampWindowToWorkingArea", "UI DPI awareness=per_monitor_v2",
  "RemoteVisual", "DrawRecordingRipples",
]), "Navigation, default light theme, restrained dark theme, or recording feedback is incomplete");
assert(includesAll(screenshotScript, [
  "CaptureFullOnboarding", "Wait-ForChildText", '"00-setup-01-device.png"', '"00-setup-05-ready.png"',
  '"03-shortcuts-screenshot.png"', 'ValidateSet("Current", "Light", "Dark", "System")',
]), "Screenshot automation is not aligned with the five-task setup");

// Release, installer, CI, signing, and isolated hardware candidate safety.
assert(includesAll(release, [
  'Copy-Item (Join-Path $root "vibe-mic-config.default.json") $packageDir',
  'Copy-Item (Join-Path $root "NAudio.Core.dll") $packageDir',
  'Copy-Item (Join-Path $root "docs\\V1_2_1_TUTORIAL_ZH.md") $packageDocs',
  'Copy-Item (Join-Path $root "docs\\V1_3_USER_GUIDE_ZH.md") $packageDocs',
  'Copy-Item (Join-Path $root "docs\\VERSION_ARCHIVE_ZH.md") $packageDocs',
  '"VibeFlow-Setup.exe"', '"SHA256SUMS.txt"', "Invoke-VibeFlowCodeSign",
  'scripts\\Get-StableCaptureBinary.ps1', 'VibeFlow-StableCapture-v1.2.1.exe',
  '@("VibeMic.exe", "--self-test")', '@("VoxDeckInputBridge.exe", "--self-test")',
  '& $stableCapturePath --self-test',
]), "Release packaging, checksums, or signing are incomplete");
assert(!release.includes('BUILD_VIBE_MIC_CAPTURE.cmd') &&
  !release.includes('@("VibeMic.exe", "VibeMicAtvvCapture.exe", "VoxDeckInputBridge.exe")'),
  "The formal release can rebuild or re-sign the frozen capture binary");
assert(!release.includes("VibeFlowRc003Filter") &&
  !candidateBuild.includes("VibeFlowRc003Filter") &&
  !installer.includes("VibeFlowRc003Filter"),
  "An unsigned, unvalidated RC003 driver candidate can enter a user package");
assert(!release.includes('(Join-Path $packageDir "vibe-mic-config.json")'),
  "Release packaging can overwrite an existing user configuration");
assert(includesAll(candidateBuild, [
  'if ($version -ne "1.4.0")', '"hardware-candidate"', 'hardwareAcceptancePassed = $false',
  'recordingKernel = "v1.0.3"', '"CANDIDATE_MANIFEST.json"', '"SHA256SUMS.txt"',
  'B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683',
  'stableCaptureSha256 = $stableCaptureSha256',
  'Copy-Item -LiteralPath $stableCapturePath -Destination (Join-Path $candidateDir "VibeMicAtvvCapture.exe")',
  '"Programs\\Vibe Flow Remote"', '"docs\\V1_2_1_TUTORIAL_ZH.md"',
  '"docs\\V1_3_USER_GUIDE_ZH.md"',
  '"docs\\VERSION_ARCHIVE_ZH.md"', '"docs\\V1_3_PREVIEW_ZH.md"',
  '"docs\\V1_4_PREVIEW_ZH.md"',
  '"docs\\V1_3_HARDWARE_ACCEPTANCE_ZH.md"',
  '"scripts\\Measure-HardwareAcceptance.ps1"',
  'configurationSchema = 31', 'bridgeConfigurationSchema = 6',
  'powerKeySupport = "unsupported-no-stable-windows-event"',
]) && !candidateBuild.includes("VibeFlow-Setup.exe"),
  "Hardware candidate packaging can overwrite or masquerade as the installed release");
assert(!candidateBuild.includes('Copy-Item $stableCapturePath $candidateDir'),
  "Hardware candidate can package the frozen Capture under an unusable temporary filename");
assert(includesAll(hardwareAcceptanceTool, [
  'ValidateSet("Begin", "Complete", "Status")', 'ExpectedVoiceCycles = 100',
  'automaticEvidencePassed', 'releaseApproved = $false', 'REMOTE STREAM START session=',
  'TRANSCRIPTION SUBMIT .*sent=True audio_delivered=True', 'foregroundConfirmed',
  'VibeMicAtvvCapture.exe',
  'B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683',
]) && includesAll(v13HardwareAcceptance, [
  'Measure-HardwareAcceptance.ps1', '100 次', 'Home 长按', 'releaseApproved',
  '125%', '150%', '200%', 'Authenticode',
]), "V1.3 hardware acceptance cannot produce auditable evidence");
assert(includesAll(stableCaptureResolver, [
  "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683",
  "releases/download/v1.2.1/Vibe-Flow-Windows-x64.zip",
  "Get-FileHash", "VIBE_FLOW_STABLE_CAPTURE_PATH",
]), "The frozen capture binary cannot be reproduced from a pinned verified source");
assert(!candidateBuild.includes('$LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $stableCapturePath)') &&
  !release.includes('$LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $stableCapturePath)'),
  "Stable-capture resolution can be rejected by an unrelated stale process exit code");
assert(includesAll(dependencyRestore, [
  'Check = "lib\\netstandard2.0\\NAudio.Core.dll"', 'Check = "lib\\netstandard2.0\\NAudio.Wasapi.dll"',
]) && includesAll(captureBuild, [
  'naudio.core.2.2.1\\lib\\netstandard2.0\\NAudio.Core.dll',
  'naudio.wasapi.2.2.1\\lib\\netstandard2.0\\NAudio.Wasapi.dll',
]), "Build dependencies do not use the reproducible NuGet layout");
assert(includesAll(installer, [
  '#define MyAppVersion "1.4.0"', "PrivilegesRequired=lowest", "WaitForVibeFlowExit",
  "ConfigRequestsStartup", "RestoreConfiguredStartupRegistration", "vibe-mic-config.json.bak",
  "MigrateLegacyUserConfig", "Vibe Flow Remote\\UserData", "UserConfigPath",
]), "Installer upgrades cannot safely preserve settings and startup state");
assert(includesAll(hardwareAcceptanceTool, [
  'Join-Path $env:LOCALAPPDATA "Vibe Flow Remote\\UserData"',
  'Join-Path $userStateRoot "remote-voice-session"',
]), "Hardware acceptance still reads user state from a version-specific executable directory");
assert(includesAll(workflow, [
  "actions/checkout@v7", "actions/setup-node@v7", "actions/upload-artifact@v7",
  "WINDOWS_SIGNING_PFX_BASE64", "WINDOWS_SIGNING_PFX_PASSWORD",
]), "GitHub Actions validation, artifact upload, or signing secrets are incomplete");

// Current documentation must describe only the shipped V1.2.1 interaction.
const userDocs = { readme, englishReadme, guide, versionTutorial, versionArchive, quickStart, releaseNotes, githubReleaseBody, continuousGuide, architecture };
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
  "1.4.0 Preview", "Configuration schema: `31`", "Bridge configuration schema: `6`",
  "Stable capture file version: `1.2.1.0`", "Recording kernel: `v1.0.3`",
]), "Version metadata documentation is stale");
assert(includesAll(previewGuide, [
  "Raw Input 安全直通", "普通键盘", "打开 HTTPS 网页", "100 次录音按下与松开",
  "不替代已经发布且稳定的 `v1.2.1`", "input-bridge-log.txt", "Home 短按和长按",
  "开机、返回和独立音量加减", "不提供配置入口", "真实执行结果",
]), "V1.3 preview guide lacks source isolation, customization, or hardware acceptance guidance");
assert(includesAll(v14PreviewGuide, [
  "V1.3 本地稳定基线", "不修改语音链路", "我的快捷键", "Vibe Coding",
  "浏览器 AI", "Terminal Agent", "最近一次快捷操作", "配置 revision",
  "选择本机应用", "Browser Back", "正在运行与已安装应用",
  "100 次录音按下/松开", "普通键盘冲突", "125%", "150%", "不提交",
]), "V1.4 preview guide lacks frozen-baseline, Profile, receipt, or release-gate guidance");
assert(includesAll(v13Guide, [
  "五项首次设置", "00-setup-01-device.png", "00-setup-05-ready.png",
  "CABLE Input", "CABLE Output", "Typeless", "豆包输入法", "Windows 语音输入",
  "打开本机 APP", "通用导航", "Vibe Coding", "媒体控制",
  "备份配置", "导入配置", "恢复上次", "一键自检", "发布门禁",
]), "The V1.3 illustrated guide is incomplete");
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

console.log("Vibe Flow V1.4.0 preview validation passed.");
