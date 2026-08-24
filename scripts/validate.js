const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const requiredFiles = [
  "README.md",
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
  "BUILD_RELEASE.ps1",
  "CREATE_APP_ICON.ps1",
  "START_VIBE_FLOW.cmd",
  "vibe-flow-logo.png",
  "vibe-mic-config.default.json",
  "scripts/VibeMic.cs",
  "scripts/VibeMicAtvvCapture.cs",
  "scripts/VoxDeckInputBridge.cs",
  "scripts/capture-ui-screenshots.ps1",
  "installer/VibeFlow.iss",
  "installer/languages/ChineseSimplified.isl",
  "docs/USER_GUIDE_ZH.md",
  "docs/RELEASE_NOTES_ZH.md",
  "docs/ARCHITECTURE.md",
  "docs/images/00-first-run.png",
  "docs/images/00-setup-1-provider.png",
  "docs/images/00-setup-2-audio.png",
  "docs/images/00-setup-3-remote.png",
  "docs/images/00-setup-4-hotkey.png",
  "docs/images/00-setup-5-dictation.png",
  "docs/images/01-overview.png",
  "docs/images/02-dictation.png",
  "docs/images/03-shortcuts.png",
  "docs/images/04-diagnostics.png",
  "docs/images/05-settings.png",
  "docs/images/06-transcription-tools.png",
  ".github/ISSUE_TEMPLATE/bug_report.yml",
];

function read(file) {
  return fs.readFileSync(path.join(root, file), "utf8").replace(/^\uFEFF/, "");
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

for (const file of requiredFiles) {
  assert(fs.existsSync(path.join(root, file)), `Missing ${file}`);
}

const app = read("scripts/VibeMic.cs");
const capture = read("scripts/VibeMicAtvvCapture.cs");
const bridge = read("scripts/VoxDeckInputBridge.cs");
const release = read("BUILD_RELEASE.ps1");
const defaultConfig = JSON.parse(read("vibe-mic-config.default.json"));
const packageJson = JSON.parse(read("package.json"));
const readme = read("README.md");
const guide = read("docs/USER_GUIDE_ZH.md");
const quickStart = read("QUICK_START_ZH.md");
const installer = read("installer/VibeFlow.iss");
const releaseNotes = read("docs/RELEASE_NOTES_ZH.md");
const screenshotScript = read("scripts/capture-ui-screenshots.ps1");

for (const file of requiredFiles.filter((item) => item.startsWith("docs/images/") && item.endsWith(".png"))) {
  const png = fs.readFileSync(path.join(root, file));
  assert(png.length > 20000, `Screenshot is unexpectedly small: ${file}`);
  assert(png.toString("ascii", 1, 4) === "PNG", `Screenshot is not a PNG: ${file}`);
  assert(png.readUInt32BE(16) >= 900 && png.readUInt32BE(20) >= 600, `Screenshot dimensions are too small: ${file}`);
}

assert(app.includes('DisplayProductName = "言灵 · Vibe Flow Remote"') && app.includes("Text = DisplayProductName"), "Vibe Flow Remote window title is missing");
assert(app.includes('ProductRelease = "1.0.3"') && packageJson.version === "1.0.3", "Application and package versions are not aligned");
assert(app.includes('AssemblyFileVersion("1.0.3.0")') && app.includes('AssemblyInformationalVersion("1.0.3")'), "Windows executable version metadata is missing");
assert(app.includes('brandLogoPath = Path.Combine(root, "vibe-flow-logo.png")'), "Brand logo is not wired into the app");
assert(app.includes("ShowSetupWizard"), "First-run setup is missing");
assert(app.includes("VibeMicExitForUpdate") && app.includes("ExistingInstanceUsesDifferentPath"), "Cross-directory update handoff is missing");
assert(app.includes("CaptureExited(Process exitedCapture)") && app.includes("ReferenceEquals(captureProcess, exitedCapture)") && app.includes("CAPTURE EXIT superseded=true ignored=true"), "Superseded capture exits can corrupt the active capture lifecycle");
assert(capture.includes('AssemblyFileVersion("1.0.3.0")') && bridge.includes('AssemblyFileVersion("1.0.3.0")'), "Shipped helper binaries must carry the release version");
assert(app.includes("CABLE Output"), "Setup must explain the transcription-client microphone endpoint");
assert(app.includes("ExportDiagnostics"), "Redacted diagnostics export is missing");
assert(app.includes("CaptureNextAudioDiagnostic") && app.includes("Local\\\\VibeMicCaptureAudioDiagnostic"), "Opt-in one-shot audio diagnostics UI is missing");
assert(app.includes("BuildMappingsPage"), "Shortcut configuration page is missing");
assert(app.includes("value.schemaVersion = ConfigSchemaVersion") && app.includes("ConfigSchemaVersion = 15"), "Configuration migration must target schema 15");
assert(app.includes("选择转写工具") && app.includes("安装音频通道") && app.includes("完成首次听写"), "Five-step onboarding flow is incomplete");
assert(app.includes("firstDictationBaselineGeneration") && app.includes("言灵不会读取、保存或上传输入框中的文字"), "Privacy-safe first dictation validation is missing");
assert(app.includes("if (!firstDictationSucceeded)") && app.includes("请先完成一次真实听写"), "Setup must require a real end-to-end dictation");
assert(app.includes("soundFeedbackEnabled") && app.includes("CreateFeedbackWave") && app.includes("WETYPE SESSION END"), "Per-session sound feedback is missing");
assert(app.includes("BuildProblemSummary") && app.includes("复制问题摘要") && app.includes("GetLatestSessionHealth"), "User-readable shareable diagnostics are missing");
assert(app.includes("BuildSelfCheckReport") && app.includes("AddSelfCheckRow") && app.includes("HandleSelfCheckAction"), "Actionable self-check center is missing");
assert(app.includes("播放端点不是 CABLE Input") && app.includes("install-cable") && app.includes("restore-profile"), "Self-check repair actions do not cover audio endpoint failures");
assert(app.includes("StableVoiceProfileVersion = 11") && app.includes("ApplyStableVoiceProfile") && app.includes("HasStableVoiceProfile"), "Validated voice profile is not pinned or recoverable");
assert(app.includes("StableVoiceGain = 1.0") && app.includes("StableVoiceDrainMs = 180") && app.includes('StableVoiceEndpoint = "CABLE Input"') && app.includes('StableVoiceProcessing = "speech"'), "Validated voice constants changed");
assert(app.includes("调整高级参数") && app.includes("稳定档案已经通过真机反复验证"), "Stable audio controls are not protected from accidental changes");
assert(app.includes("管理语音桥接") && !app.includes('bridgeButton = PrimaryButton(IsCapturing ? "暂停语音桥接"'), "Overview still exposes accidental bridge shutdown as its primary action");
assert(app.includes("bool startupChoiceValue = config.launchAtStartup") && !app.includes("config.setupCompleted ? config.launchAtStartup : true"), "First-run startup consent must not be preselected");
assert(app.includes("PollInputFeedback") && app.includes("HighlightedControl") && app.includes("ShowCallouts"), "Remote button interaction feedback is missing");
assert(app.includes("RotateLogFile") && app.includes("4 * 1024 * 1024"), "Runtime log rotation is missing");
assert(app.includes("vibe-flow-host.log") && app.includes("VOICE WAKE recovery=restart_stalled_capture"), "Persistent startup recovery diagnostics are missing");
assert(app.includes("WarmConfiguredProviderAsync") && app.includes("PROVIDER READY provider="), "Configured transcription provider is not warmed during startup");
assert(app.includes("autoRouteVirtualMicrophone") && app.includes("听写时自动使用遥控器麦克风"), "Automatic virtual microphone routing must be configurable");
assert(app.includes("audioProcessingMode") && app.includes("清晰增强（推荐）") && app.includes("原始直通"), "Audio processing modes must be configurable and understandable");
assert(app.includes("Typeless") && app.includes("Voquill（开源）") && app.includes("Windows 语音输入"), "Transcription provider choices are incomplete");
assert(app.includes("遥控器功能速查") && app.includes("独立音量 +/-：此型号未检测到稳定事件"), "Hardware-validated remote quick reference is missing");
assert(!app.includes("功能组合键已按下"), "Unsupported Function leader UI remains");

assert(capture.includes("BluetoothLEDevice"), "Capture must use Windows BLE APIs");
assert(capture.includes("ClockedVirtualMicSink"), "Capture must route live audio through the clocked VB-CABLE virtual microphone");
assert(!capture.includes("WeChatHotkey"), "Capture must not inject keyboard shortcuts from BLE callbacks");
assert(capture.includes("MonitorConnection"), "Capture must monitor BLE and ATVV health");
assert(capture.includes("vibe-mic-runtime.log"), "Capture must write readable diagnostics");
assert(capture.includes("BluetoothCacheMode.Cached") && capture.includes("fallback=uncached"), "ATVV startup must use cached characteristic discovery with an uncached fallback");

assert(bridge.includes("WH_KEYBOARD_LL"), "Input bridge must use a low-level keyboard hook");
assert(bridge.includes("RegisterRawInputDevices"), "Input bridge must register device-scoped Raw Input");
assert(bridge.includes("keyboard.VKey == 0x74") && bridge.includes("SignalVoiceKeyPressed"), "Raw Input must provide a redundant RC003 voice-key signal");
assert(bridge.includes("Voice key signal delivered=") && bridge.includes("capture_not_running"), "Voice-key delivery must be diagnosable in release logs");
assert(bridge.includes("Local\\\\VibeMicVoiceKeyHeld") && bridge.includes("Local\\\\VibeMicVoiceWakeRequested"), "Voice-key startup handoff events are missing");
assert(bridge.includes("EnsureVoiceHostRunning") && bridge.includes("Voice host recovery started"), "The input bridge cannot recover a missing background host");
assert(!bridge.includes("wetype.statusbar.window") && !bridge.includes("VibeMicAtvvStream"), "The input bridge must not own WeType or ATVV session lifecycle");
assert(!bridge.includes("LegacyKeyEvent") && !bridge.includes("source_f5_quiet"), "Obsolete Ctrl+Win/F5-release fallback remains in the bridge");
assert(bridge.includes("launch-client:chatgpt") && bridge.includes("launch-client:deepseek") && bridge.includes("launch-client:claude"), "Client launcher provider whitelist is incomplete");
assert(bridge.includes("launch-client:cursor") && bridge.includes("launch-client:vscode") && bridge.includes("launch-client:windsurf"), "Development client launcher choices are incomplete");
assert(bridge.includes("SwitchToThisWindow") && bridge.includes("Get-StartApps"), "Client launcher must focus or start an installed desktop app");
assert(!bridge.includes("https://chatgpt.com/") && !bridge.includes("https://chat.deepseek.com/"), "Client launcher must not fall back to web pages");
assert(app.includes('"suppress", ""'), "Physical F5 must be suppressed without controlling the voice hotkey duration");
assert(capture.includes("mode=live") && capture.includes("AUDIO LIVE START") && capture.includes("AUDIO LIVE STOP"), "RC003 audio must stream to the selected provider while the record key is held");
assert(capture.includes("AUDIO LIVE FAILED"), "Failed live sessions must be discarded with a clear diagnostic");
assert(capture.includes("WeTypeVoiceSessionController") && capture.includes("WETYPE PANEL READY"), "WeType and ATVV must share one generation-aware session coordinator");
assert(capture.includes("HotkeyTranscriptionSessionController") && capture.includes("ITranscriptionSessionController"), "Generic transcription provider control is missing");
assert(capture.includes("provider=\" + provider") && capture.includes("TRANSCRIPTION READY"), "Provider timing diagnostics are missing");
assert(capture.includes("KeyboardShortcutSender") && capture.includes("SendInput"), "Provider shortcuts must use reliable SendInput injection");
assert(capture.includes("mic_open_recovery") && capture.includes("waiting_for_natural_stream_ms=120"), "A missing natural ATVV stream must use bounded MIC_OPEN recovery");
assert(capture.includes("RecoverHeldVoiceRequestAtReady") && capture.includes("released_before_capture_ready") && capture.includes("delayed_after_release=false"), "Held voice requests are not safely recovered after startup");
assert(capture.includes("AdpcmFrameAccumulator") && capture.includes("partial_frame_bytes"), "ATVV audio must be accumulated by the advertised 120-byte frame boundary");
assert(capture.includes("WriteSilence(drainMs)") && capture.includes('" trailing_silence_ms=" + drainMs'), "Live audio must end with the configured silence tail");
assert(capture.includes("OutputSampleRate = 48000") && capture.includes("OutputChannels = 2"), "VB-CABLE output must match the standard 48 kHz stereo endpoint format");
assert(capture.includes("BufferedWaveProvider") && capture.includes("BlockSamples = 320"), "Audio output must use a non-blocking 20 ms WASAPI buffer");
assert(capture.includes("WasapiOut") && capture.includes("AudioClientShareMode.Shared") && capture.includes("true, 20"), "The virtual microphone must use the Windows endpoint event clock");
assert(!capture.includes("waveOutOpen") && !capture.includes("timeBeginPeriod"), "Legacy WinMM timing must not return");
assert(capture.includes("ignored_stale_generation") && capture.includes("sink_queue_drops"), "Late audio and virtual microphone backpressure must be diagnosable and generation-safe");
assert(capture.includes("BlockingCollection<AudioNotification>(256)") && capture.includes("ordered ATVV audio decoder"), "BLE audio decoding must be serialized outside the notification callback");
assert(capture.includes("ordered_codec_sync=true") && capture.includes("ApplyOrderedCodecSync"), "Codec sync and audio frames must share one ordered decode path");
assert(capture.includes("SpeechLeveler") && capture.includes("raw_rms_pct") && capture.includes("output_rms_pct"), "Quiet speech must use bounded robust leveling with before/after diagnostics");
assert(capture.includes("robustCeiling") && capture.includes("sample_limiter=true") && capture.includes("LastAppliedGain"), "Speech leveling must resist isolated peaks and limit samples independently");
const toolbarStart = capture.indexOf('TryToolbarClick(generation, 1, true, "start")');
const hotkeyFallback = capture.indexOf('TapVoiceHotkey(generation, "start_fallback")');
assert(toolbarStart >= 0 && hotkeyFallback > toolbarStart, "WeType must use the validated toolbar before shortcut fallback");
assert(capture.includes("WETYPE TRANSCRIPTION SUBMIT") && capture.includes('TapVoiceHotkey(generation, "submit_after_audio_drained")'), "WeType transcription must be submitted only after VB-CABLE audio drains");
assert(capture.includes("--self-test") && capture.includes("voice pipeline self-test passed"), "The native voice pipeline must expose deterministic self-tests");
assert(capture.includes("--sink-clock-test") && capture.includes("Virtual microphone clock test"), "The virtual microphone must expose a real endpoint clock benchmark");
assert(capture.includes("--endpoint-route-test") && capture.includes("reversible_endpoint_test"), "Default microphone routing must expose a reversible Windows endpoint test");
assert(capture.includes("VIRTUAL MIC DRAIN COMPLETE") && capture.includes("pending_after="), "Each transcription submit must log the final virtual microphone drain state");
assert(!capture.includes('"audio_packet"') && !capture.includes('AppendEvent(type'), "BLE audio callbacks must not synchronously write packet payload logs");
assert(!capture.includes("AUDIO REPLAY") && !capture.includes("VoiceClip"), "Delayed audio replay must never overlap a later recording");
assert(!capture.includes("panelBecameReady") && !capture.includes("liveTail"), "A session must not begin delivering buffered speech after the remote has stopped");
assert(capture.includes("STALE STREAM STOP ignored"), "Overlapping ATVV sessions must ignore stale stop events");
assert(capture.includes("controller_dispose"), "Failed recovery must close a stale WeType panel before reconnecting");
assert(capture.includes("SessionPanelWaitPolicy") && capture.includes("WETYPE SESSION PREEMPTED"), "A new recording must preempt stale WeType completion waits");
assert(capture.includes("superseded_by_new_recording") && capture.includes("DiscardPending"), "Superseded virtual-microphone audio must be dropped instead of replayed late");
assert(capture.includes("AudioDiagnosticSession") && capture.includes("01-raw-decoded-16k-mono.wav") && capture.includes("03-cable-output.wav"), "One-shot stage-by-stage WAV diagnostics are missing");
assert(capture.includes("privacy=explicit_user_action") && capture.includes("next_session_only=true"), "Audio diagnostics must remain explicit and one-shot");
assert(capture.includes("DefaultCaptureEndpointLease") && capture.includes("SetDefaultEndpoint"), "Dictation must temporarily route the Windows default capture endpoint");
assert(capture.includes("default-capture-endpoint-lease.txt") && capture.includes("startup_recovery"), "Default endpoint routing must be crash recoverable");
assert(capture.includes("DEFAULT CAPTURE ROUTE ACQUIRED") && capture.includes("DEFAULT CAPTURE ROUTE RESTORED"), "Default endpoint routing must be diagnosable");
assert(bridge.includes("HandleDirectionVolumeFallback"), "Hold Up/Down volume behavior is missing");
assert(bridge.includes('command == "open"') && bridge.includes("taskSwitcherAltDown"), "TV task switcher behavior is missing");
assert(!bridge.includes("HandleFunctionLeaderKey") && !bridge.includes("HandleFunctionLeaderCombo"), "Unsupported Function combinations remain");
assert(!bridge.includes('command == "smart-back"'), "Unsupported smart-return behavior remains");

const mappingKeys = Object.keys(defaultConfig.mappings).sort();
const expectedKeys = ["Home", "TV", "上 / 下 / 左 / 右", "功能键", "确认键"].sort();
assert(defaultConfig.schemaVersion === 15, "Release configuration must use schema 15");
assert(defaultConfig.stableVoiceProfileVersion === 11, "Release must identify the validated v11 voice profile");
assert(defaultConfig.onboardingVersion === 3, "Release onboarding version is invalid");
assert(defaultConfig.soundFeedbackEnabled === true, "Completion sound feedback must be enabled for new users");
assert(defaultConfig.autoRouteVirtualMicrophone === true, "Automatic virtual microphone routing must be enabled for new users");
assert(defaultConfig.audioProcessingMode === "speech", "Robust quiet-speech leveling must be enabled for new users");
assert(defaultConfig.audioEndpointName === "CABLE Input" && defaultConfig.autoLevel === true && defaultConfig.voiceMode === "hold", "Validated endpoint or capture mode changed");
assert(defaultConfig.inputMethod === "wechat" && defaultConfig.inputMethodHotkey === "ctrl+win", "Default transcription provider is invalid");
assert(defaultConfig.inputMethodTrigger === "toggle" && defaultConfig.providerStartupDelayMs === 80, "Default provider trigger profile is invalid");
assert(defaultConfig.gain === 1.0 && defaultConfig.drainMs === 180 && defaultConfig.captureSeconds === 0, "Validated voice timing and sensitivity defaults changed");
assert(defaultConfig.setupCompleted === false, "Release must show first-run setup");
assert(defaultConfig.launchAtStartup === false, "Release must not register startup before consent");
assert(JSON.stringify(mappingKeys) === JSON.stringify(expectedKeys), "Release configuration exposes unverified mappings");
assert(defaultConfig.mappings["功能键"] === "launch-client:chatgpt", "Function must default to the ChatGPT client launcher");
assert(defaultConfig.mappings["上 / 下 / 左 / 右"] === "direction-volume-fallback", "Direction hold volume mapping is missing");
assert(defaultConfig.mappings["确认键"] === "enter" && defaultConfig.mappings.Home === "win+d" && defaultConfig.mappings.TV === "task-switcher", "Validated single-key defaults changed");

assert(release.includes('Copy-Item (Join-Path $root "vibe-mic-config.default.json") $packageDir'), "Release must include a non-destructive default configuration template");
assert(release.includes('Copy-Item (Join-Path $root "CHANGELOG.md") $packageDir') && release.includes('Copy-Item (Join-Path $root "VIBE_MIC_VERSION.md") $packageDir'), "Release must include version and change notes");
assert(release.includes('Copy-Item (Join-Path $root "NAudio.Core.dll") $packageDir') && release.includes('Copy-Item (Join-Path $root "NAudio.Wasapi.dll") $packageDir'), "Release must include the WASAPI runtime dependencies");
assert(!release.includes('(Join-Path $packageDir "vibe-mic-config.json")'), "Release must not overwrite an existing user configuration during upgrade");
assert(release.includes('"LICENSE"') && release.includes('"THIRD_PARTY_NOTICES.md"'), "Release must include license notices");
assert(release.includes('"VibeFlow-Setup.exe"') && release.includes('"SHA256SUMS.txt"') && release.includes("ISCC.exe"), "Formal release must build an installer and checksum manifest");
assert(release.includes('docs\\USER_GUIDE_ZH.md') && release.includes('docs\\RELEASE_NOTES_ZH.md') && release.includes('docs\\images\\*.png'), "Release must include the offline tutorial, current release notes, and screenshots");
assert(!release.includes('RELEASE_NOTES*.md'), "Release must not package historical release-note files");
assert(installer.includes('#define MyAppVersion "1.0.3"') && installer.includes("PrivilegesRequired=lowest"), "Installer version or per-user privilege policy is invalid");
assert(installer.includes("[InstallDelete]") && installer.includes('RELEASE_NOTES_V*.md'), "Installer must remove legacy release notes during upgrades");
assert(installer.includes("WaitForVibeFlowExit") && installer.includes("OpenMutex") && installer.includes("for Attempt := 1 to 48"), "Installer must wait for a clean background-service shutdown");
assert(installer.includes("VibeMicExitForUpdate") && installer.includes("vibe-mic-config.json") && installer.includes("[UninstallDelete]"), "Installer update or uninstall behavior is incomplete");
assert(readme.includes("docs/USER_GUIDE_ZH.md") && readme.includes("docs/images/01-overview.png"), "GitHub README must link the tutorial and screenshot");
assert(readme.includes("一键自检与修复") && readme.includes("1.0.3"), "GitHub README does not describe the current self-check release");
assert(readme.includes("VibeFlow-Setup.exe") && readme.includes("Source code (zip)"), "GitHub README does not distinguish the installer from source archives");
assert(readme.includes("最新正式版 · v1.0.3") && readme.indexOf("最新正式版 · v1.0.3") < readme.indexOf("docs/images/01-overview.png"), "GitHub README must put the latest release before product screenshots");
assert(readme.includes("正式版时间线") && readme.includes("2026-08-25") && readme.includes("releases/tag/v1.0.0"), "GitHub README must include a dated stable-release timeline");
assert(guide.includes("CABLE Input") && guide.includes("CABLE Output"), "Tutorial must explain the VB-CABLE route");
assert(guide.includes("VBCABLE_Setup_x64.exe") && guide.includes("以管理员身份运行") && guide.includes("重启 Windows"), "Tutorial must provide beginner-safe VB-CABLE installation steps");
assert(guide.includes("Typeless") && guide.includes("Voquill") && guide.includes("Windows 语音输入"), "Tutorial must explain supported transcription clients");
assert(guide.includes("连接与自检") && guide.includes("七个检查项"), "Tutorial must explain actionable self-checks");
assert(guide.includes("为什么没有组合键"), "Tutorial must explain the verified-control limitation");
assert(guide.includes("VibeFlow-Setup.exe") && guide.includes("两分钟验收") && guide.includes("教程截图复用"), "Tutorial does not cover installation, acceptance, and reusable screenshots");
assert(guide.includes("按你的情况开始") && guide.includes("Source code (tar.gz)"), "Tutorial does not provide a beginner-safe download path");
assert(guide.includes("images/06-transcription-tools.png") && guide.includes("现象 | 先检查 | 处理方法"), "Tutorial lacks provider visuals or symptom-based troubleshooting");
for (const step of ["provider", "audio", "remote", "hotkey", "dictation"]) {
  assert(guide.includes(`images/00-setup-${["provider", "audio", "remote", "hotkey", "dictation"].indexOf(step) + 1}-${step}.png`), `Tutorial lacks the ${step} onboarding screenshot`);
}
assert(screenshotScript.includes("CaptureFullOnboarding") && screenshotScript.includes("Wait-ForChildText"), "Full onboarding screenshot automation is missing");
assert(screenshotScript.includes("AllowUnhealthyDiagnostics") && screenshotScript.includes("healthySelfCheckText") && screenshotScript.includes("requires a healthy 7/7 self-check"), "Release screenshots can silently publish an unhealthy diagnostics state");
assert(quickStart.includes("VibeFlow-Setup.exe") && quickStart.includes("Source code (zip)"), "Offline quick start does not distinguish the installer from source archives");
assert(quickStart.includes("VBCABLE_Setup_x64.exe") && quickStart.includes("Install Driver"), "Offline quick start must explain the required VB-CABLE driver install");
assert(releaseNotes.includes("VibeFlow-Setup.exe") && releaseNotes.includes("发布验证") && releaseNotes.includes("已知边界"), "V1 GitHub release notes are incomplete");

console.log("Vibe Flow validation passed.");
