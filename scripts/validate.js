const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const requiredFiles = [
  "README.md",
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
  "START_VIBE_FLOW.cmd",
  "vibe-flow-logo.png",
  "vibe-mic-config.default.json",
  "scripts/VibeMic.cs",
  "scripts/VibeMicAtvvCapture.cs",
  "scripts/VoxDeckInputBridge.cs",
  "docs/USER_GUIDE_ZH.md",
  "docs/ARCHITECTURE.md",
  "docs/images/00-first-run.png",
  "docs/images/01-overview.png",
  "docs/images/02-dictation.png",
  "docs/images/03-shortcuts.png",
  "docs/images/04-diagnostics.png",
  "docs/images/05-settings.png",
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
const readme = read("README.md");
const guide = read("docs/USER_GUIDE_ZH.md");

assert(app.includes('Text = "言灵 · Vibe Flow"'), "Vibe Flow window title is missing");
assert(app.includes('brandLogoPath = Path.Combine(root, "vibe-flow-logo.png")'), "Brand logo is not wired into the app");
assert(app.includes("ShowSetupWizard"), "First-run setup is missing");
assert(app.includes("CABLE Output"), "Setup must explain the WeChat microphone endpoint");
assert(app.includes("ExportDiagnostics"), "Redacted diagnostics export is missing");
assert(app.includes("BuildMappingsPage"), "Shortcut configuration page is missing");
assert(app.includes("value.schemaVersion = 8"), "Configuration migration must target schema 8");
assert(!app.includes("功能组合键已按下"), "Unsupported Function leader UI remains");

assert(capture.includes("BluetoothLEDevice"), "Capture must use Windows BLE APIs");
assert(capture.includes("WaveOutSink"), "Capture must route live audio to VB-CABLE");
assert(capture.includes("WECHAT HOTKEY DOWN ctrl+win"), "Capture must hold the WeChat voice shortcut");
assert(capture.includes("MonitorConnection"), "Capture must monitor BLE and ATVV health");
assert(capture.includes("vibe-mic-runtime.log"), "Capture must write readable diagnostics");

assert(bridge.includes("WH_KEYBOARD_LL"), "Input bridge must use a low-level keyboard hook");
assert(bridge.includes("RegisterRawInputDevices"), "Input bridge must register device-scoped Raw Input");
assert(bridge.includes("HandleDirectionVolumeFallback"), "Hold Up/Down volume behavior is missing");
assert(bridge.includes('command == "open"') && bridge.includes("taskSwitcherAltDown"), "TV task switcher behavior is missing");
assert(!bridge.includes("HandleFunctionLeaderKey") && !bridge.includes("HandleFunctionLeaderCombo"), "Unsupported Function combinations remain");
assert(!bridge.includes('command == "smart-back"'), "Unsupported smart-return behavior remains");

const mappingKeys = Object.keys(defaultConfig.mappings).sort();
const expectedKeys = ["Home", "TV", "上 / 下 / 左 / 右", "功能键", "确认键"].sort();
assert(defaultConfig.schemaVersion === 8, "Release configuration must use schema 8");
assert(defaultConfig.setupCompleted === false, "Release must show first-run setup");
assert(defaultConfig.launchAtStartup === false, "Release must not register startup before consent");
assert(JSON.stringify(mappingKeys) === JSON.stringify(expectedKeys), "Release configuration exposes unverified mappings");
assert(defaultConfig.mappings["功能键"] === "ctrl+shift+p", "Function must remain a single-tap shortcut");
assert(defaultConfig.mappings["上 / 下 / 左 / 右"] === "direction-volume-fallback", "Direction hold volume mapping is missing");

assert(release.includes('"vibe-mic-config.default.json"'), "Release must start from the default configuration");
assert(release.includes('"LICENSE"') && release.includes('"THIRD_PARTY_NOTICES.md"'), "Release must include license notices");
assert(readme.includes("docs/USER_GUIDE_ZH.md") && readme.includes("docs/images/01-overview.png"), "GitHub README must link the tutorial and screenshot");
assert(guide.includes("CABLE Input") && guide.includes("CABLE Output"), "Tutorial must explain the VB-CABLE route");
assert(guide.includes("为什么没有组合键"), "Tutorial must explain the verified-control limitation");

console.log("Vibe Flow validation passed.");
