const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");

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
  "BUILD_DEVELOPMENT.ps1",
  "BUILD_HARDWARE_CANDIDATE.ps1",
  "BUILD_RELEASE.ps1",
  "CREATE_APP_ICON.ps1",
  "START_VIBE_FLOW.cmd",
  "vibe-flow-logo.png",
  "vibe-mic-config.default.json",
  "scripts/Install-VBCable.ps1",
  "scripts/Prepare-DevelopmentRuntime.ps1",
  "scripts/Get-StableCaptureBinary.ps1",
  "scripts/Measure-HardwareAcceptance.ps1",
  "scripts/VibeMic.cs",
  "scripts/ui/DesignTokens.cs",
  "scripts/ui/UiComponents.cs",
  "scripts/ui/PageShell.cs",
  "scripts/features/ActionResult.cs",
  "scripts/features/FocusTargetModels.cs",
  "scripts/features/FocusTargetStore.cs",
  "scripts/features/FocusTargetService.cs",
  "scripts/features/ProjectSpaceModels.cs",
  "scripts/features/ProjectSpaceStore.cs",
  "scripts/features/ProjectSpaceRunner.cs",
  "scripts/features/CaptureAskModels.cs",
  "scripts/features/CaptureAskService.cs",
  "scripts/features/CaptureAskWindows.cs",
  "scripts/features/BrowserProfileTemplate.cs",
  "scripts/features/BrowserProfileUndoStore.cs",
  "scripts/features/BrowserRemoteTestService.cs",
  "scripts/features/AudioEndpointService.cs",
  "scripts/features/InputMethodDetector.cs",
  "scripts/features/LinkQualityPolicy.cs",
  "scripts/features/WorkflowCards.cs",
  "scripts/features/LinkBaselineStore.cs",
  "scripts/features/FavoriteAppStore.cs",
  "scripts/features/FavoriteAppStatus.cs",
  "scripts/features/UsageStatsPolicy.cs",
  "scripts/features/CrashReports.cs",
  "scripts/features/SnippetStore.cs",
  "scripts/Set-UsbSelectiveSuspend.ps1",
  "scripts/check-ui-geometry.ps1",
  "scripts/check-ui-matrix.ps1",
  "docs/V2_0_RELEASE_CHECKLIST_ZH.md",
  "scripts/ui/UiFonts.cs",
  "scripts/ui/UiDisplayScale.cs",
  "scripts/ui/LiveHudForm.cs",
  "scripts/ui/ContextDeckForm.cs",
  "scripts/ui/CaptureAskForm.cs",
  "scripts/ui/CaptureAskIntegration.cs",
  "scripts/ui/BrowserRemoteLiteForm.cs",
  "scripts/ui/BrowserRemoteLiteIntegration.cs",
  "scripts/ui/AppPickerDialog.cs",
  "scripts/tests/FocusTargetMultiWindowTests.cs",
  "scripts/tests/FocusTargetSmokeApp.cs",
  "scripts/tests/CaptureAskServiceTests.cs",
  "scripts/tests/Test-ReleaseDependencyPreflight.ps1",
  "scripts/tests/Test-ReleaseIdentity.ps1",
  "scripts/tests/Test-ReleaseArtifacts.ps1",
  "scripts/tests/CaptureAskUiTests.cs",
  "scripts/tests/BrowserRemoteLiteTests.cs",
  "scripts/tests/BrowserRemoteLiteUiTests.cs",
  "scripts/tests/LiveHudUiTests.cs",
  "scripts/tests/ShortcutActionTests.cs",
  "scripts/tests/Test-DevelopmentBuild.ps1",
  "scripts/tests/Test-DevelopmentRuntime.ps1",
  "scripts/tests/Test-InstallerConfigMigration.ps1",
  "scripts/tests/Test-InstallerRequirements.ps1",
  "scripts/tests/Test-V2FeatureSuite.ps1",
  "scripts/features/NotesModels.cs",
  "scripts/features/NotesStore.cs",
  "scripts/ui/NotesPage.cs",
  "docs/v2-notes/BASELINE.md",
  "docs/v2-notes/PLAN.md",
  "docs/v2-notes/PROGRESS.md",
  "docs/v2-notes/UI_SPEC.md",
  "docs/v2-notes/APP_COMPATIBILITY.md",
  "docs/v2-notes/QA_REPORT.md",
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
  "docs/V1_5_USER_GUIDE_ZH.md",
  "docs/FEATURES_ZH.md",
  "docs/VERSION_ARCHIVE_ZH.md",
  "docs/RELEASE_NOTES_ZH.md",
  "docs/GITHUB_RELEASE_BODY_ZH.md",
  "docs/GITHUB_RELEASE_BODY_V1_4_ZH.md",
  "docs/CONTINUOUS_DICTATION_ZH.md",
  "docs/CODE_SIGNING_ZH.md",
  "docs/ARCHITECTURE.md",
  "docs/VOICE_PIPELINE_RESEARCH.md",
  "docs/V1_2_HARDWARE_ACCEPTANCE_ZH.md",
  "docs/V1_3_PREVIEW_ZH.md",
  "docs/V1_3_HARDWARE_ACCEPTANCE_ZH.md",
  "docs/V1_4_PREVIEW_ZH.md",
  "docs/V1_5_PREVIEW_ZH.md",
  "docs/RC003_DRIVER_LAB_ZH.md",
  "docs/images/00-first-run.png",
  "docs/images/00-setup-01-device.png",
  "docs/images/00-setup-02-remote.png",
  "docs/images/00-setup-03-audio.png",
  "docs/images/00-setup-04-dictation.png",
  "docs/images/00-setup-05-ready.png",
  "docs/images/01-overview.png",
  "docs/images/02-notes.png",
  "docs/images/02-projects.png",
  "docs/images/02-dictation.png",
  "docs/images/03-shortcuts.png",
  "docs/images/03-shortcuts-screenshot.png",
  "docs/images/04-diagnostics.png",
  "docs/images/05-settings.png",
  "docs/images/06-transcription-tools.png",
  "docs/images/07-shortcut-actions.png",
  "docs/images/08-shortcut-recorder.png",
  "docs/images/09-smart-profile-apps.png",
  "docs/images/vibe-flow-community.png",
  ".github/actionlint.yaml",
  ".github/ISSUE_TEMPLATE/bug_report.yml",
  ".github/ISSUE_TEMPLATE/feature_request.yml",
  ".github/ISSUE_TEMPLATE/config.yml",
  ".github/workflows/driver-candidate.yml",
  ".github/workflows/validate.yml",
  "docs/COMPATIBILITY_MATRIX_ZH.md",
  "docs/ISSUE_2_REGRESSION_ZH.md",
  "docs/RELEASE_QUALITY_GATE_ZH.md",
  "docs/V2_0_USER_GUIDE_ZH.md",
  "docs/V2_0_CONFIGURATION_MIGRATION_ZH.md",
  "docs/V2_0_AUTOMATED_TEST_REPORT_ZH.md",
  "docs/V2_0_HARDWARE_TEST_MATRIX_ZH.md",
  "docs/V2_0_KNOWN_LIMITATIONS_ZH.md",
  "docs/V2_0_ROLLBACK_ZH.md",
  "docs/V2_0_RELEASE_NOTES_ZH.md",
  "docs/V2_0_INSTALLER_GUIDE_ZH.md",
  "scripts/Test-ReleaseLifecycle.ps1",
];

function read(file) {
  return fs.readFileSync(path.join(root, file), "utf8").replace(/^\uFEFF/, "");
}

function sha256(file) {
  return crypto.createHash("sha256").update(fs.readFileSync(path.join(root, file))).digest("hex").toUpperCase();
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
const uiDesignTokens = read("scripts/ui/DesignTokens.cs");
const uiComponents = read("scripts/ui/UiComponents.cs");
const pageShell = read("scripts/ui/PageShell.cs");
const actionResult = read("scripts/features/ActionResult.cs");
const focusTargetModels = read("scripts/features/FocusTargetModels.cs");
const focusTargetStore = read("scripts/features/FocusTargetStore.cs");
const focusTargetService = read("scripts/features/FocusTargetService.cs");
const installedAppCatalog = read("scripts/features/InstalledAppCatalog.cs");
const packagedAppIdentity = read("scripts/features/PackagedAppIdentity.cs");
const projectSpaceModels = read("scripts/features/ProjectSpaceModels.cs");
const projectSpaceStore = read("scripts/features/ProjectSpaceStore.cs");
const projectSpaceRunner = read("scripts/features/ProjectSpaceRunner.cs");
const captureAskModels = read("scripts/features/CaptureAskModels.cs");
const captureAskService = read("scripts/features/CaptureAskService.cs");
const captureAskWindows = read("scripts/features/CaptureAskWindows.cs");
const browserProfileTemplate = read("scripts/features/BrowserProfileTemplate.cs");
const browserProfileUndoStore = read("scripts/features/BrowserProfileUndoStore.cs");
const browserRemoteTestService = read("scripts/features/BrowserRemoteTestService.cs");
const audioEndpointService = read("scripts/features/AudioEndpointService.cs");
const inputMethodDetector = read("scripts/features/InputMethodDetector.cs");
const linkQualityPolicy = read("scripts/features/LinkQualityPolicy.cs");
const workflowCards = read("scripts/features/WorkflowCards.cs");
const linkBaselineStore = read("scripts/features/LinkBaselineStore.cs");
const favoriteAppStore = read("scripts/features/FavoriteAppStore.cs");
const favoriteAppStatus = read("scripts/features/FavoriteAppStatus.cs");
const usageStatsPolicy = read("scripts/features/UsageStatsPolicy.cs");
const snippetStore = read("scripts/features/SnippetStore.cs");
const appPickerDialog = read("scripts/ui/AppPickerDialog.cs");
const favoriteAppsPanel = read("scripts/ui/FavoriteAppsPanel.cs");
const usbSuspendScript = read("scripts/Set-UsbSelectiveSuspend.ps1");
// The Smart Focus learning UI is no longer a dialog of its own: the capture lives in the Host and the
// result surface is the favourite-app banner. This section is where the retired FocusTargetDialog.cs
// assertions now point, so the learning safety semantics stay asserted instead of being dropped.
const favoriteLearning = section(app, "private void CaptureFavoriteTarget(string processName)",
  "private void OpenFavoriteApp(string processName)");
const liveHudForm = read("scripts/ui/LiveHudForm.cs");
const contextDeckForm = read("scripts/ui/ContextDeckForm.cs");
const captureAskForm = read("scripts/ui/CaptureAskForm.cs");
const captureAskIntegration = read("scripts/ui/CaptureAskIntegration.cs");
const browserRemoteLiteForm = read("scripts/ui/BrowserRemoteLiteForm.cs");
const browserRemoteLiteIntegration = read("scripts/ui/BrowserRemoteLiteIntegration.cs");
const notesModels = read("scripts/features/NotesModels.cs");
const notesStore = read("scripts/features/NotesStore.cs");
const notesPage = read("scripts/ui/NotesPage.cs");
const notesDeckForm = read("scripts/ui/NotesDeckForm.cs");
const notesDeckIntegration = read("scripts/ui/NotesDeckIntegration.cs");
const capture = read("scripts/VibeMicAtvvCapture.cs");
const bridge = read("scripts/VoxDeckInputBridge.cs");
const hostBuild = read("BUILD_VIBE_MIC.cmd");
const release = read("BUILD_RELEASE.ps1");
// The interface matrix is what the release chain gates the interface on: it launches the host once per theme
// and window size, walks all six pages, and fails on overlapping controls, clipped text, or a theme that did
// not take effect. It exists because the dark theme shipped unable to start and the pages collided at 125%
// scaling — neither had ever been looked at by a run. The theme is taken from the captured pixels rather
// than trusted from the flag, and a machine with no interactive desktop is reported as skipped rather than
// passing quietly.
// An unhandled exception used to leave nothing on the machine but a Windows Error Reporting entry: the dark
// theme shipped unable to start for months because its crash happened in the host's constructor, where no
// code of ours was watching. Both handler paths are wired now, the report carries the rendering environment
// beside the exception, the next session's log names the previous crash, and the exported diagnostics include
// it. The writer is also exercised on a synthetic exception in the self-test, since a crash reporter that has
// never been run is a crash reporter that does not work.
const crashReports = read("scripts/features/CrashReports.cs");
assert(includesAll(crashReports, [
  "internal static class CrashReports",
  "internal static string Write(string source, Exception error)",
  "internal static List<string> ExistingReports()",
  "internal static string Summarize(string path, int maximumLines)",
  "AppendException(report, error, 0)",
  "PruneOldReports(directory)",
  "internal static class DisplayEnvironment",
  "internal static class OperatingSystemDescription",
]) && includesAll(app, [
  "Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);",
  "Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)",
  'CrashReports.Write("ui_thread", e.Exception)',
  "AppDomain.CurrentDomain.UnhandledException += delegate",
  'CrashReports.Write("app_domain", e.ExceptionObject as Exception)',
  "ReportPreviousCrashes();",
  "RunCrashReportSelfTests();",
  "private static void RunCrashReportSelfTests()",
  "The crash report is missing: ",
  "Crash reports are not pruned: ",
  "The crash writer cannot describe a missing exception",
  'report.AppendLine("Crashes recorded: " + crashReports.Count',
  'crashTestRequested = Array.Exists(args, delegate(string arg) {',
]) && hostBuild.includes("CrashReports.cs"),
  "An unhandled exception leaves no report, so a crash from another machine cannot be diagnosed");
// The installer passes a path taken straight from the registry, and that value ends with a backslash; inside
// quotes, the terminating backslash escapes the closing quote and the application is handed a path containing
// one. Measured: `VibeFlow.exe --installer-config-migrate "C:\...\Vibe Flow Remote\" "..."` died in
// Path.Combine with "路径中具有非法字符" (a crash report from the new crash logging named the frame), so every
// install over an existing installation told the user its old configuration could not be migrated, while a
// clean install — which uses the application directory, with no trailing separator — was unaffected. Both
// sides are pinned: the application trims quotes and separators, and the installer stops emitting one.
assert(includesAll(app, [
  "private static string NormalizeInstallerPath(string value)",
  "legacyRoot = NormalizeInstallerPath(legacyRoot);",
  "stateRoot = NormalizeInstallerPath(stateRoot);",
  "path = NormalizeInstallerPath(path);",
  "RunInstallerPathSelfTests();",
  "private static void RunInstallerPathSelfTests()",
  "Installer path normalization turned '",
]) && hostBuild.includes("CrashReports.cs") &&
  read("installer/VibeFlow.iss").includes("Result := RemoveBackslashUnlessRoot(Result);"),
  "An installer path with a trailing separator can reach the migration entry point again");
const interfaceMatrix = read("scripts/check-ui-matrix.ps1");
assert(includesAll(interfaceMatrix, [
  "foreach ($theme in $Themes)",
  "foreach ($size in $Sizes)",
  // The two smaller desktop sizes the release notes have always listed as unverified, plus the smallest
  // window the application allows at 100% scaling.
  "'1366x768', '1920x1080', '880x500'",
  "$expected = if ($appsLight -eq 0) { 'dark' } else { 'light' }",
  "$observed = if ($luminance -lt 100) { 'dark' } else { 'light' }",
  "skipped (no desktop)",
  "if ($failures -gt 0) {",
  // The matrix must not leave a capture process behind. Measured: a leftover VibeMicAtvvCapture.exe made the
  // installer's RestartManager report that it could not close an application, and with a suppressed message
  // box the installation was aborted and rolled back (exit code 5) — three times, until the installer's own
  // log (/LOG=) named the process. Cleaning up before the first case is not enough: the last case leaks it.
  "$leftovers = @('VibeMic', 'VoxDeckInputBridge', 'VibeMicAtvvCapture')",
  "Stop-SmokeLeftovers",
  "finally {",
]) && includesAll(release, ["scripts\\check-ui-matrix.ps1", 'throw "Interface matrix failed."']) &&
  includesAll(read("scripts/check-ui-geometry.ps1"), [
    "[string]$Theme = \"\"", "[string]$ExeArguments = \"--ui-smoke\"",
    // A surface with no title cannot be named: measured, the Live HUD is a borderless window with empty text,
    // which is why it was written off as needing hardware until a session was started and it appeared.
    "[long]$WindowHandle = 0",
    'if ($WindowHandle -gt 0) {',
  ]) &&
  includesAll(app, [
    'args[themeIndex].Equals("--ui-theme", StringComparison.OrdinalIgnoreCase)',
    'if (requested == "light" || requested == "dark" || requested == "system")',
  ]),
  "The interface is not gated per theme and window size, so a whole theme can ship unable to start");
const candidateBuild = read("BUILD_HARDWARE_CANDIDATE.ps1");
const dependencyRestore = read("RESTORE_BUILD_DEPS.ps1");
const captureBuild = read("BUILD_VIBE_MIC_CAPTURE.cmd");
const installer = read("installer/VibeFlow.iss");
const screenshotScript = read("scripts/capture-ui-screenshots.ps1");
// The screenshot script has to be DPI aware like the geometry check: Windows virtualizes window
// rectangles for an unaware process, so at 200% it asked for a bitmap half the window's real size and the
// images came out with their content clipped (measured: a 2026x1416 wizard captured into 1013x708, which
// looked like a broken layout rather than a broken capture).
assert(screenshotScript.includes("SetProcessDpiAwarenessContext") &&
  screenshotScript.includes("SetProcessDPIAware"),
  "The screenshot script is not DPI aware, so its captures are clipped above 100%");
// The screenshot script no longer depends on UI Automation. Measured on this machine, UIA cannot see this
// application's controls at all — a minimal Windows Forms application reports every control as
// ControlType.Pane with focusable=False — so a lookup by ControlType.CheckBox finds nothing. The one UIA
// step (unchecking a box before a capture) is now done through the window itself, and it verifies its own
// effect: BM_GETCHECK reads the state, BM_CLICK toggles it and raises the application's event, and the state
// is read again so a step that changed nothing throws instead of looking like it succeeded.
assert(!screenshotScript.includes("Windows.Automation") &&
  !screenshotScript.includes("UIAutomationClient") &&
  includesAll(screenshotScript, [
    "function Find-ChildCheckbox([IntPtr]$Parent, [string]$Text)",
    "0x00F0",
    "0x00F5",
    'throw "Checkbox did not clear: $Text"',
  ]),
  "The screenshot script still depends on UI Automation, which cannot see this application's controls");
// The dialogs are not pages, so the page sweep cannot see them. The check gained a mode that measures one
// window by title, which is how the application picker was measured at 200% (1160x1320 = twice its design
// size, so the scaling ran exactly once rather than twice) and how a box collision between its count label
// and its confirm button was found — a collision that exists at every scaling, because the label's box
// reached 28 px under the button.
assert(includesAll(read("scripts/check-ui-geometry.ps1"), [
  '[string]$WindowTitle = ""',
  '"dialog=" + $WindowTitle',
  '"dialog: overlaps="',
  '"dialog: clipped="',
]) && includesAll(read("scripts/ui/UiDisplayScale.cs"), ["control.Margin = new Padding("]) &&
  includesAll(read("scripts/ui/AppPickerDialog.cs"), ["hint.Size = new Size(320, 22);"]),
  "The dialogs cannot be measured, or an anchored control keeps an unscaled margin");
// The picker's rows carry an icon, the list filters live, and the dialog follows the night theme. Measured before
// the rewrite: many catalogue entries arrive with Icon == null, so rows alternated between a picture and a blank
// gap; the lookup now ends in a tile generated from the name, so no row is left empty. The dialog was hard-coded
// white, so it stayed white inside a night-themed application. Its filter was driven from outside with WM_SETTEXT
// and read back: no match shows the empty-state line and "当前筛选没有结果", "chrome" shows "显示 1 / 95 个应用",
// and clearing brings the placeholder back.
assert(includesAll(read("scripts/ui/AppPickerDialog.cs"), [
  "internal AppPickerDialog(IList<InstalledAppChoice> choices, bool darkTheme)",
  "internal void ApplyTheme(bool dark)",
  "private Image IconFor(InstalledAppChoice item)",
  "AppIcons.For(item.ProcessName, item.DisplayName, item.LaunchTarget, item.Icon, out source);",
  "AppIcons.DrawTile(e.Graphics, icon,",
  "PICKER ICON FALLBACK process=",
  "private void DrawRow(object sender, DrawItemEventArgs e)",
  "emptyState.Visible = applications.Items.Count == 0;",
  'filterPlaceholder.Text = "搜索应用名或进程名，例如 cursor / chrome"',
  "filterPlaceholder.BringToFront();",
]) && includesAll(app, [
  "new AppPickerDialog(choices, darkTheme, HostLog)",
  // The workflow rows now carry the application's own logo, through the same shared lookup: measured, this machine
  // has 91 real icons out of 95 catalogue entries, and the picker had them while this list drew a status dot.
  "Image rowIcon = AppIcons.For(card.ProcessName, card.Title, \"\", null, out iconSource);",
  "AppIcons.DrawTile(e.Graphics, rowIcon,",
]) && includesAll(read("scripts/features/InstalledAppCatalog.cs"), [
  "internal static class AppIcons",
  "internal static Image For(string processName, string displayName, string launchTarget, Icon catalogueIcon,",
  "internal static Image LetterTile(string name)",
  "internal static void DrawTile(Graphics graphics, Image icon, Rectangle bounds, Color tileColor, int radius)",
]), "The application picker lost its icon fallback, its live filter or its theme");
// The minimum window size is a design measurement too. Left unscaled, the window could be dragged down to
// 880x500 device pixels at 200% — 440x250 logical, below anything the layout was built for, with the
// sidebar alone taking more than half of it. It scales now, and the diagnostic reports it so the value is
// measurable rather than assumed: measured at 200%, minimum=1760x1000.
assert(includesAll(app, [
  "MinimumSize = new Size(ScaledDesign(880), ScaledDesign(500));",
  '" minimum=" + MinimumSize.Width',
]), "The minimum window size does not follow the display scaling");
// A themed ListView ignores BackColor for its items area, so in dark mode the table rows stayed white on a dark
// page. Measured before the fix: rows light, surface colour (35,37,44); after turning the control's theme off:
// rows (35,37,44). Its column header is a separate SysHeader32 window and stays light even with theming off
// (measured 240,240,240), which is recorded as needing custom draw rather than left implied as fixed.
assert(includesAll(read("scripts/ui/BrowserRemoteLiteForm.cs"), [
  "private static extern int SetWindowTheme(IntPtr handle, string subApplicationName, string subIdList);",
  'SetWindowTheme(listHandle, "", "");',
  "Colouring it needs custom draw.",
]), "The dark-mode table fix is gone, so the rows would render light on a dark page again");// The caveat also has to be where a downloader reads it, not only inside the application: the release body that
// BUILD_RELEASE.ps1 generates from docs/GITHUB_RELEASE_BODY_ZH.md, the README, and the quick-start. All three
// described using the record key without ever saying that without the signed filter the key uses hook isolation
// scoped to the remote being online, and that an ordinary keyboard's F5 passes through when it is not.
assert(includesAll(read("docs/GITHUB_RELEASE_BODY_ZH.md"), [
  "录音键隔离现状（安装前请先读）",
  "遥控器**不在线**时，普通键盘的 F5 **原样直通**",
  "不能与签名过滤器等同",
]) && includesAll(read("README.md"), [
  "**录音键隔离**",
  "普通键盘 F5 原样直通",
]) && includesAll(read("QUICK_START_ZH.md"), [
  "录音键隔离（安装前请先读）",
  "遥控器**不在线**时普通键盘的 F5 **原样直通**",
]), "The key-isolation caveat is missing from the documents a downloader reads");// The key-isolation caveat has to be on the first screen a new user sees, not only on the home page: while the
// device-level filter is not healthy the record key can reach whatever application is in front, and this is the
// project's top release concern. It is shown only when that is actually the case, and the step was captured after
// the change to confirm the line renders and the rest of the step is unchanged.
assert(includesAll(app, [
  '"onboardingKeyIsolationNotice"',
  "录音键当前为安全直通：前台应用可能同时收到录音键；设备级精确隔离仍在开发中。",
  "if (!ReadKeyboardBridgeHealth().FilterHealthy)",
]), "The setup wizard no longer states the key-isolation status");// The release checklist is the artifact a release decision is reviewed against, so it has to exist and has to
// keep saying the two things that are easy to lose: what was measured, and what was not.
assert(includesAll(read("docs/V2_0_RELEASE_CHECKLIST_ZH.md"), [
  "已实测通过",
  "未验证（需要条件或需要你做决定）",
  "发布建议",
  "RC003 设备级按键隔离",
  "代码签名",
  "如何复现上述验证",
]), "The release checklist is missing or no longer separates the verified from the unverified");// The exported diagnostics' content is built and checked in every smoke run, because the export itself goes
// through a save dialog that cannot be driven from a test. The report is the same string the export writes, so
// this is what a user's report contains; when crash reports exist the newest one has to be in it, which was
// verified while two were present (reports left by this session's own failing assertions).
assert(includesAll(app, [
  "private string BuildDiagnosticsReport()",
  "private void CheckDiagnosticsReport()",
  "if (uiSmokeMode) CheckDiagnosticsReport();",
  "The diagnostics report is missing: ",
  "does not state a crash count: ",
  "but does not include the newest one",
  'File.WriteAllText(dialog.FileName, BuildDiagnosticsReport(), new UTF8Encoding(false));',
]), "The exported diagnostics are neither testable nor checked");
// The keyboard walk asserts what it finds, because a walk that reports nothing looks exactly like a walk that
// found nothing wrong: every page must reach at least one control, must never reach a control that refuses
// focus (its own negative control), and must not collapse below the number of tab stops the page declares —
// that last bound is the regression test for the walk keyed on labels, which reported 1 reachable control on a
// page holding 34.
assert(includesAll(app, [
  "reaches no control at all",
  "reaches a control that is not a tab stop",
  "of \" + tabbable + \" tab stops",
]), "The keyboard order is measured but nothing about it is enforced");// Every wired form asserts its own scaling in each smoke run, and which value to compare is measured rather
// than assumed: a form already larger than its design size at the end of its constructor was scaled by Windows
// Forms, which scales the client area and leaves the frame alone, while a form still at its design size there is
// scaled here, which scales the window. The autoscale baseline does not separate them — measured, the Context
// Deck carries one and is still scaled by this code. Both families were found by accident: Browser Remote Lite
// was scaled twice and clamped to the working area, so it filled the screen.
assert(includesAll(app, [
  "private void CheckSurfaceScaling(string name, Form surface, Size designSize)",
  "bool autoscaledByWindowsForms = constructed.Width > designSize.Width + 4 ||",
  "compared=",
  "where its design size at this display scaling is",
  'CheckSurfaceScaling("BrowserRemoteLite",',
  'CheckSurfaceScaling("ContextDeck",',
]), "A wired form is no longer checked for its display scaling");// Capture & Ask is measured from inside a smoke run, through the same call the tray item makes, because it is
// the one surface with no route through the application's pages. The measurement immediately found a real
// defect: this form sets AutoScaleDimensions = (96,96) with AutoScaleMode.Dpi, so Windows Forms scales it, and
// UiDisplayScale scales it again at load — measured, 2536x1416 where its design size at this display's scaling
// is 1560x1400, clamped to the working area instead of taking its design size. It is logged as MISMATCH rather
// than asserted while the affected set is pinned down and fixed, so the release chain stays green without the
// defect going unrecorded.
assert(includesAll(app, [
  "private void MeasureTraySurfaceGeometry()",
  'HostLog("UI TRAY SURFACE captureAsk=" + (matches ? "ok" : "MISMATCH")',
  "if (uiSmokeMode) MeasureTraySurfaceGeometry();",
  // Every wired form reports its size at construction and after being shown, because "exactly twice its design
  // size" cannot tell one scaling from two: Browser Remote Lite's design size times two is the same number as
  // the working-area clamp. That measurement found Capture & Ask being scaled twice, after which its own fit
  // clamped it to the working area — it filled the screen instead of taking its design size. Removing the
  // second pass fixed it: it is now 1534x1329 with a client area of 1508x1258, which is exactly its design
  // client size times this display's scaling (Windows Forms scales the client area; the frame is not part of
  // that, which is why the window is 26x71 smaller than design-window times scaling).
  "private void CheckSurfaceScaling(string name, Form surface, Size designSize)",
  'CheckSurfaceScaling("LiveHud", new LiveHudForm(), new Size(400, 160));',
  "compared=",
  "Capture & Ask is ",
]), "The tray-only surface is no longer measured, and a double-scaled form would go unnoticed");
assert(!read("scripts/ui/CaptureAskForm.cs").includes("UiDisplayScale.Apply(this);") &&
  read("scripts/ui/CaptureAskForm.cs").includes("Not wired to UiDisplayScale") &&
  !read("scripts/ui/BrowserRemoteLiteForm.cs").includes("UiDisplayScale.Apply(this);") &&
  read("scripts/ui/BrowserRemoteLiteForm.cs").includes("Not wired to UiDisplayScale"),
  "A form that Windows Forms already scales is wired to UiDisplayScale again, so it fills the screen");// The Context Deck is opened only from the tray menu, so it has no route through the application's own
// interface, and reaching it from outside would mean driving the user's tray icon. Its geometry is asserted
// from inside instead, with the same rule the external check applies to the pages, and that assertion runs in
// the release chain on every build. Measured before it was written: a freshly shown deck is 1640x1392 where
// 820x696 times this display's scaling is exactly that, and giving the assertion a wrong design size makes the
// self-test fail with "Context Deck is 1640x1392 where its design size at this display scaling is 1600x1392".
assert(includesAll(app, [
  "internal static void AssertSurfaceGeometry(Form surface, Size designSize, string name)",
  "private static string FindSiblingOverlapText(Control root)",
  'AssertSurfaceGeometry(scaledDeck, new Size(820, 696), "Context Deck");',
  "where its design size at this display scaling is",
  "has overlapping controls: ",
]), "A tray-only surface is no longer checked for scaled geometry or overlapping controls");// The keyboard order is measured from inside the application, because nothing outside it can read it here:
// UI Automation reports every control of a Windows Forms application as ControlType.Pane on this machine (a
// minimal application built the same way does too), AttachThreadInput is refused, and SendKeys needs a
// foreground window the harness cannot take. The walk is a query against the control tree, it wraps so the
// count means "how many controls Tab can reach", and it tracks visits by control identity: the first version
// keyed on the label and stopped at the first repeated one, reporting 1 reachable control on a page that
// holds a column of identically named buttons. Its negative control is that it must never visit a control
// whose TabStop is false.
assert(includesAll(app, [
  "private void LogTabOrderDiagnostic()",
  'HostLog("UI TABORDER page=" + page + " stops=" + visited.Count',
  "if (visited.Contains(current)) break;",
  "visitedNonTabStop=",
  "if (uiSmokeMode) LogTabOrderDiagnostic();",
]), "The keyboard order is not measured, so it is only assumed");
const cableInstaller = read("scripts/Install-VBCable.ps1");
const stableCaptureResolver = read("scripts/Get-StableCaptureBinary.ps1");
const hardwareAcceptanceTool = read("scripts/Measure-HardwareAcceptance.ps1");
const workflow = read(".github/workflows/validate.yml");
const defaultConfig = JSON.parse(read("vibe-mic-config.default.json"));
const packageJson = JSON.parse(read("package.json"));
const startScript = read("START_VIBE_FLOW.cmd");
const readme = read("README.md");
const englishReadme = read("README_VIBE_MIC.md");
const guide = read("docs/USER_GUIDE_ZH.md");
const versionTutorial = read("docs/V1_2_1_TUTORIAL_ZH.md");
const v13Guide = read("docs/V1_3_USER_GUIDE_ZH.md");
const v15Guide = read("docs/V1_5_USER_GUIDE_ZH.md");
const featuresBoard = read("docs/FEATURES_ZH.md");
const versionArchive = read("docs/VERSION_ARCHIVE_ZH.md");
const quickStart = read("QUICK_START_ZH.md");
const releaseNotes = read("docs/RELEASE_NOTES_ZH.md");
const githubReleaseBody = read("docs/GITHUB_RELEASE_BODY_ZH.md");
const v14GithubReleaseBody = read("docs/GITHUB_RELEASE_BODY_V1_4_ZH.md");
const continuousGuide = read("docs/CONTINUOUS_DICTATION_ZH.md");
const architecture = read("docs/ARCHITECTURE.md");
const versionDoc = read("VIBE_MIC_VERSION.md");
const voiceResearch = read("docs/VOICE_PIPELINE_RESEARCH.md");
const hardwareAcceptance = read("docs/V1_2_HARDWARE_ACCEPTANCE_ZH.md");
const previewGuide = read("docs/V1_3_PREVIEW_ZH.md");
const v14PreviewGuide = read("docs/V1_4_PREVIEW_ZH.md");
const v15PreviewGuide = read("docs/V1_5_PREVIEW_ZH.md");
const v13HardwareAcceptance = read("docs/V1_3_HARDWARE_ACCEPTANCE_ZH.md");
const rc003DriverLabGuide = read("docs/RC003_DRIVER_LAB_ZH.md");
const compatibilityMatrix = read("docs/COMPATIBILITY_MATRIX_ZH.md");
const issue2Regression = read("docs/ISSUE_2_REGRESSION_ZH.md");
const releaseQualityGate = read("docs/RELEASE_QUALITY_GATE_ZH.md");
const issueTemplateConfig = read(".github/ISSUE_TEMPLATE/config.yml");
const featureRequestTemplate = read(".github/ISSUE_TEMPLATE/feature_request.yml");
const lifecycleTest = read("scripts/Test-ReleaseLifecycle.ps1");
const releaseIdentityTest = read("scripts/tests/Test-ReleaseIdentity.ps1");
const releaseArtifactsTest = read("scripts/tests/Test-ReleaseArtifacts.ps1");
const installerMigrationTest = read("scripts/tests/Test-InstallerConfigMigration.ps1");
const installerRequirementsTest = read("scripts/tests/Test-InstallerRequirements.ps1");
const v2FeatureSuite = read("scripts/tests/Test-V2FeatureSuite.ps1");
const gitignore = read(".gitignore");
const v2Guide = read("docs/V2_0_USER_GUIDE_ZH.md");
const v2Migration = read("docs/V2_0_CONFIGURATION_MIGRATION_ZH.md");
const v2AutomatedTests = read("docs/V2_0_AUTOMATED_TEST_REPORT_ZH.md");
const v2HardwareMatrix = read("docs/V2_0_HARDWARE_TEST_MATRIX_ZH.md");
const v2KnownLimitations = read("docs/V2_0_KNOWN_LIMITATIONS_ZH.md");
const v2Rollback = read("docs/V2_0_ROLLBACK_ZH.md");
const v2ReleaseNotes = read("docs/V2_0_RELEASE_NOTES_ZH.md");
const v2InstallerGuide = read("docs/V2_0_INSTALLER_GUIDE_ZH.md");
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

const dialogScreenshots = new Set([
  "docs/images/07-shortcut-actions.png",
  "docs/images/08-shortcut-recorder.png",
  "docs/images/09-smart-profile-apps.png",
]);
for (const file of requiredFiles.filter((item) => item.startsWith("docs/images/") && item.endsWith(".png"))) {
  const png = fs.readFileSync(path.join(root, file));
  assert(png.length > (dialogScreenshots.has(file) ? 10000 : 20000),
    `Screenshot is unexpectedly small: ${file}`);
  assert(png.toString("ascii", 1, 4) === "PNG", `Screenshot is not a PNG: ${file}`);
  const minimumWidth = dialogScreenshots.has(file) ? 600 : 900;
  const minimumHeight = dialogScreenshots.has(file) ? 300 : 600;
  assert(png.readUInt32BE(16) >= minimumWidth && png.readUInt32BE(20) >= minimumHeight,
    `Screenshot dimensions are too small: ${file}`);
}

// Product identity, release metadata, and persistent configuration.
assert(includesAll(app, [
  'DisplayProductName = "言灵 · Vibe Flow Remote"',
  'ProductRelease = "2.0.0"',
  'StableCaptureBinaryVersion = "1.2.1"',
  'AssemblyFileVersion("2.0.0.0")',
  'AssemblyInformationalVersion("2.0.0-candidate")',
  'ConfigSchemaVersion = 32',
  'CurrentOnboardingVersion = 9',
  'OnboardingStepCount = 5',
]), "Application identity or V2 candidate configuration metadata is inconsistent");
assert(packageJson.version === "2.0.0", "package.json version is not aligned with the V2 candidate");
assert(packageJson.scripts && packageJson.scripts.start === "cmd /c START_VIBE_FLOW.cmd" &&
  startScript.includes("release\\Vibe-Flow-Windows-x64\\VibeFlow.exe") &&
  startScript.includes("VibeMic.exe"),
  "The development and candidate startup entries are not aligned");
assert(includesAll(readme, [
  "V2.0.0 离键闭环候选版", "最新已发布稳定版 · V1.5.0",
  "Capture 文件继续显示 `1.2.1.0`", "docs/V2_0_USER_GUIDE_ZH.md",
]), "The homepage does not distinguish the V2 candidate, V1.5 release, and frozen Capture");
assert(includesAll(compatibilityMatrix, [
  "Windows 10 / 11 x64", "RC003", "微信输入法", "Typeless", "开机 / 返回 / 独立音量", "不支持",
]), "The public compatibility matrix is incomplete");
assert(includesAll(issue2Regression, [
  "真实音频覆盖 0.1%", "MIC_EXTEND 2 次", "V1.5", "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683",
]), "Issue #2 does not have a verifiable regression conclusion");
assert(includesAll(releaseQualityGate, [
  "干净的 GitHub Windows runner", "Windows 睡眠/唤醒", "100 次录音按下/松开", "不能由云端替代",
]), "The release quality gate omits clean-install or physical recovery coverage");
assert(issueTemplateConfig.includes("blank_issues_enabled: true") &&
  includesAll(featureRequestTemplate, ["功能建议", "使用场景", "设备与环境"]),
  "GitHub feedback remains restricted or lacks a structured feature form");
assert(includesAll(lifecycleTest, [
  "PreviousInstallerPath", "upgrade-preservation.marker", "VibeMicAtvvCapture.exe", "unins000.exe",
  "WaitForExit($TimeoutSeconds * 1000)", "Stop-InstalledProcesses $installDir",
]), "The clean Windows release lifecycle test is incomplete");
assert(capture.includes('AssemblyFileVersion("1.2.1.0")') && bridge.includes('AssemblyFileVersion("2.0.0.0")'),
  "The stable capture or release bridge binary version is inconsistent");
assert(sha256("scripts/VibeMicAtvvCapture.cs") ===
  "736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2",
  "The frozen Capture source hash changed");
assert(includesAll(app, [
  "LoadConfig();", "MigrateConfig(", "SyncKeyboardBridgeConfig();",
  "WriteTextAtomically(configPath", "WriteTextAtomically(bridgeConfigPath",
  'configPath + ".bak"', 'bridgeConfigPath + ".bak"', "File.Replace",
  "config = VibeMicConfig.Default();",
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

// V2 Smart Focus remains outside the frozen input path and fails closed.
assert(includesAll(focusTargetModels, [
  "FocusTargetDescriptor", "NormalizeProcessName", 'ControlType, "Edit"',
  'Strategy, "uia"', "FOCUS-TARGET-NOT-EDITABLE", "FOCUS-STRATEGY-UNSUPPORTED",
]), "Smart Focus descriptors do not reject unsupported strategies or non-edit controls");
assert(includesAll(focusTargetStore, [
  'CurrentSchemaVersion = 1', '"focus-targets.json"', 'storePath + ".bak"',
  "File.Replace", "FOCUS-SCHEMA-NEWER", "CopyUnknownFields", "SensitiveFields",
  '"windowTitle"', '"runtimeId"', '"boundingRectangle"',
]), "Smart Focus storage lacks schema, atomic backup, unknown-field, or privacy handling");
assert(includesAll(focusTargetService, [
  "Interlocked.CompareExchange(ref requestActive", "CancelForRecording", "FOCUS-BUSY",
  "FOCUS-CANCELED-VOICE", "FOCUS-PROCESS-MISMATCH", "FOCUS-TARGET-STALE",
  "FOCUS-TIMEOUT", "recordingCommitGate", "TryCommitFocusAction",
  "selected.SetFocus", "HasKeyboardFocus", "ControlType.Edit",
  "IsKeyboardFocusable", "ValuePattern.Pattern", "TextPattern.Pattern",
]), "Smart Focus does not enforce one request, recording priority, editable semantics, or verified focus");
// The learning UI is now the favourite-app flow, so the retired dialog's guarantees are asserted where
// they actually live: the Host captures with a bounded wait against the target process, and the shared
// service rejects unstable or non-editable targets. The old "non-activating dialog" requirement is
// retired with the dialog itself — there is no learning window left to steal focus from the target.
assert(includesAll(favoriteLearning, [
  "focusAutomationBackend.CaptureFocusedEditableTarget", "waited.ElapsedMilliseconds < 10000",
  "pendingFavoriteTarget = descriptor;", "ExecuteForVerification",
]) && includesAll(focusTargetService, [
  "CaptureFocusedEditableTarget", "FOCUS-DESCRIPTOR-UNSTABLE", "FOCUS-TARGET-NOT-EDITABLE",
  "VerificationStatusText",
]), "Favourite-app learning lacks the bounded capture, stability gate, test-before-save step, or status states");
assert(includesAll(focusTargetService, [
  "VerificationStatusText", "应用未运行", "Process.GetProcessesByName"
]), "Smart Focus status does not distinguish a verified target whose application is no longer running");
assert(includesAll(app, ["ExecuteDefaultFocusTarget", "配置工作流", "工作流"]) &&
  app.indexOf("ShowFocusTargetsDialog") < 0,
"Project and shortcut settings must route the shared target flow through the 工作流 page, and the retired dialog entry must be gone");
assert(includesAll(app, [
  "CancelV2ExternalActionsForRecording", "focusTargetService.CancelForRecording",
  "FocusTargetSummary()",
]), "The Host does not cancel Smart Focus for recording or publish the default target state");
assert(includesAll(hostBuild, [
  "UIAutomationClient.dll", "UIAutomationTypes.dll",
  '"%~dp0scripts\\features\\FocusTargetModels.cs"',
  '"%~dp0scripts\\features\\FocusTargetStore.cs"',
  '"%~dp0scripts\\features\\FocusTargetService.cs"',
]) && !hostBuild.includes("FocusTargetDialog.cs"),
  "The Host build does not compile the Smart Focus implementation and Windows UIA references, or still compiles the retired dialog");
assert(!/Clipboard\.|SendKeys\.|keybd_event|SendInput/.test(
  focusTargetModels + focusTargetStore + focusTargetService + favoriteLearning),
  "Smart Focus must not read the clipboard or dispatch keyboard input");
assert(!/\.Current\.(Name|HelpText)|ValuePattern\.Current|DocumentRange|RuntimeId|BoundingRectangle/.test(
  focusTargetService + favoriteLearning),
  "Smart Focus reads UI text, values, RuntimeId, or screen coordinates");
// The product renamed the user-visible concept 输入目标 → 工作流 (and the control itself → 输入框), because
// the page it lives on is now called 工作流 and the flow is 「选择常用应用」. The live paths must not drift
// back; the archived Capture & Ask and Project Spaces surfaces still carry the old term on purpose, since
// they are historical code that is not part of the current navigation.
assert(!focusTargetService.includes("输入目标") && !app.includes("未确认输入目标") &&
  !app.includes("未记录输入目标") && !app.includes("按需添加输入目标") &&
  !app.includes("先确认 APP 输入目标"),
  "Live guidance still uses the retired term 输入目标 instead of 工作流 / 输入框");
// The same rename has to hold in every document that ships with the installer, or the package would tell
// users to press a button that no longer exists.
for (const [name, document] of Object.entries({
  v2Guide, featuresBoard, readme, quickStart, guide, v2ReleaseNotes,
  v2HardwareMatrix, v2AutomatedTests, v2InstallerGuide, v2Migration,
})) {
  assert(!document.includes("输入目标"),
    `Shipped guidance still uses the retired term 输入目标: ${name}`);
}

// P0 hold-to-talk: one DOWN, real audio, one UP, one finalization.
assert(includesAll(app, [
  'private static string NormalizeVoiceMode(string value)', 'return "hold";',
  '"按住说话 · 松开结束"', '"聚焦输入框后按住录音键说话，松开结束录音；最终文字请目视确认"',
  'SafeCaptureArgument(config.voiceMode)',
]), "The host does not enforce the single hold-to-talk interaction");
assert(includesAll(app, [
  "FavoriteAppSummaryText", "voiceFocusTargetButton", "添加应用", "常用应用",
  "尚未配置工作流", "重新聚焦输入框后重试",
]), "Voice UI does not expose the favourite-application state or submit recovery path");
assert(app.includes("麦克风音频仍可用，但前台应用可能收到录音键"),
  "The RC003 filter warning does not distinguish audio availability from key-isolation risk");
assert(includesAll(bridge, [
  "HandleVoicePhysicalTransition", "voiceTransitionLock",
  "Voice key duplicate DOWN ignored", "Voice key duplicate UP ignored",
  "Local\\\\VibeMicVoiceKeyHeld", "Local\\\\VibeMicVoiceKeyReleased", "changed && !held",
]), "Record-key DOWN/UP edges are not de-duplicated and delivered exactly once");
assert(includesAll(app, [
  "PrepareVoiceFocusForWake", "PollFocusTargetForVoiceLock();",
  "VOICE EVENT LISTENER recoverable_error",
]) && !app.includes("VoiceFocusReady") && !app.includes("VoiceFocusRejected"),
  "Host does not expose truthful passive voice-focus observation and listener recovery");
const voiceSignalSection = section(bridge, "private static void SignalVoiceKeyPressed()",
  "private static bool ShouldRecoverVoiceHostAfterSignal");
assert(voiceSignalSection.indexOf("voiceKeyPressedEvent != null && voiceKeyPressedEvent.Set()") >= 0 &&
  voiceSignalSection.indexOf("SignalVoiceWakeRequested") >
  voiceSignalSection.indexOf("voiceKeyPressedEvent != null && voiceKeyPressedEvent.Set()"),
  "Bridge no longer preserves the V1.5 immediate voice-edge ordering");
// Measured constraint of this Windows Bluetooth stack: the low-level hook edge
// precedes Raw Input and hook suppression cancels the Raw Input packet, so the
// hook can never attribute a single F5 event to the RC003 device. The voice F5
// isolation must therefore be scoped to RC003-connected moments: while the RC003
// is physically present and the voice mapping is enabled with suppression, F5
// is captured at the hook and driven through the voice state machine from there
// (rc003_present_hook). Without the RC003, or with a healthy per-device filter,
// an ordinary keyboard F5 must pass through untouched.
const scopedHookVoiceIndex = bridge.indexOf("ShouldUseScopedHookVoice(");
assert(!bridge.includes("ShouldSuppressVoiceHookForRc003Fallback") &&
  !bridge.includes("keyboard_hook_rc003_fallback") &&
  !bridge.includes('HandleVoicePhysicalTransition(isDown, "keyboard_hook"') &&
  !bridge.includes('HandleVoicePhysicalTransition(!keyUp, "keyboard_hook"') &&
  bridge.includes('"rc003_present_hook"') &&
  bridge.includes("ShouldUseScopedHookVoice") &&
  bridge.includes("Rc003PresentRecently()") &&
  bridge.indexOf('if (isVoiceMapping && mapping.suppress)') > 0 &&
  scopedHookVoiceIndex > 0 &&
  bridge.indexOf("RC003_PRESENT_GRACE_MS") > 0 &&
  bridge.indexOf("rc003DevicePresentUtc") > 0,
  "Bridge expands device-blind low-level Hook interception for the recording key");
// A record key that keeps reporting "held" with no release edge used to wedge the voice
// state: the held event stayed set, so the frozen Capture re-armed a session on every ATVV
// reconnect (RecoverHeldVoiceRequestAtReady) and each one failed for want of audio. One
// real log held 52 recovered-at-ready sessions and 50 no-audio failures. Releasing that
// hold is what ends the loop, and the idle test alone can never do it because a repeating
// key refreshes the activity stamp about thirty times a second.
const stuckWatchdog = section(bridge,
  "private static void ReleaseStuckVoiceHoldIfIdle()", "private static void SignalVoiceWakeRequested");
assert(includesAll(stuckWatchdog, [
  "voiceHoldStaleLatched", "voiceHoldStartedTicks", "VOICE_HOLD_STUCK_BOUND_MS",
  "reason=held_past_device_bound", "VOICE STUCK LATCH CLEARED",
]) && /heldMs\s*>=\s*VOICE_HOLD_STUCK_BOUND_MS/.test(stuckWatchdog),
  "The stuck-hold watchdog measures idle time only, which a repeating record key never reaches");
assert(includesAll(bridge, [
  "VOICE_HOLD_LATCH_CLEAR_MS", "voiceHoldRepeatsSuppressed", "voiceHoldStaleReleaseCount",
  "The stuck-hold latch can clear before the restart guard expires",
]) && /VOICE_HOLD_LATCH_CLEAR_MS\s*<=\s*VOICE_RESTART_GUARD_MS/.test(bridge),
  "A stuck record key can be re-armed by its own repeats, or the next real press is lost");
// Logging every suppressed edge wrote about thirty lines a second and consumed a 2 MB log
// in ten minutes (13862 isolation lines plus 17486 in the rotated file, about four fifths
// of everything recorded). Repeats must be aggregated instead.
assert(includesAll(bridge, [
  "LogIsolationEdge", "repeat_held=true repeats=", "hold_summary repeats=",
  "ISOLATION_REPEAT_LOG_INTERVAL_MS",
]) && !bridge.includes('" rc003_connected_no_filter");\n                                return (IntPtr)1;'),
  "Held-key repeats are logged one line each again, which rotates away the useful history");
assert(includesAll(bridge, [
  'health["voice_hold_repeats_suppressed"]', 'health["voice_hold_stale_releases"]',
  'health["voice_hold_stale_latched"]',
]) && includesAll(app, [
  'TryGetValue("voice_hold_stale_latched"', "VoiceHoldStaleLatched", "VoiceHoldStaleReleases",
  "已自动释放 ", "一直没松开",
]), "The self-check cannot report a record key that never reports being released");
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
const providerPasteFallback = section(app,
  "internal static bool ShouldScheduleProviderPasteFallback",
  "private void SetSessionFeedback");
assert(providerPasteFallback.includes('provider, "wechat"') &&
  providerPasteFallback.includes('"WETYPE SESSION END"') &&
  providerPasteFallback.includes("audio_delivered=True") &&
  providerPasteFallback.includes("submitted=True") &&
  providerPasteFallback.includes('SendConfiguredHotkey("ctrl+v", false)') &&
  providerPasteFallback.includes("target_unverified") &&
  !providerPasteFallback.includes("SendConfiguredHotkey(\"enter\"") &&
  !providerPasteFallback.includes("Clipboard."),
  "WeChat paste fallback is not scoped to verified targets, sends Enter, or touches clipboard text");
assert(!app.includes("CaptureVoiceTargetSubmissionSnapshot") &&
  !app.includes("RestoreVoiceTargetFocusForSubmission") &&
  !app.includes("voiceSubmissionTarget") &&
  !app.includes("voiceSubmissionInitialForegroundProcess") &&
  !app.includes("VoiceInputFocusContext") &&
  !app.includes("CaptureVoiceInputFocusContext") &&
  !app.includes("HandleVoiceKeyReleased") &&
  !app.includes("voiceKeyReleasedEvent"),
  "Recording must not restore Smart Focus during asynchronous provider submission");
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
const sessionEndFeedback = section(app, "private static string ClassifySessionEndFeedback(string lineText)",
  "private static int RunHostSelfTests()");
const heroSurface = section(app, "private void PaintHeroSurface(object sender, PaintEventArgs e)",
  "private ActionResult RestartCaptureForAudioSettings()");
assert(!heroSurface.includes("Math.Sin") && !app.includes("remoteVisual.AnimationPhase +="),
  "Recording feedback must not use an animated fake waveform without real audio samples");
const runtimeFeedback = section(app, "private void HandleRuntimeFeedbackLine(string lineText)",
  "private void PlayFeedbackSound(bool success)");
const controlsPage = section(app, "private void BuildMappingsPage()",
  "private void BuildMappingsPageV13Legacy()");
const voicePage = section(app, "private void BuildVoicePage()", "private static string ProviderHotkeyHelp");
assert(includesAll(voicePage, [
  "CABLE Input（播放端）", "CABLE Output（录音端）", "audioEndpointName",
  "当前播放端点",
]), "Voice page does not expose separate VB-CABLE playback/recording endpoint evidence");
assert(!includesAll(runtimeFeedback, ["REMOTE STREAM STOP session=", "正在整理并回填文字"]) &&
  !app.includes("听写已完成，文字已由工具直接写入原输入框") &&
  !app.includes("转写完成，但工具未能直接写入原输入框") &&
  !runtimeFeedback.includes("input_target_ready"),
  "Recording completion is still reported as verified transcription completion");
assert(includesAll(sessionEndFeedback, [
  "audio_delivered=True", '"submission_failed"', "最终文字请目视确认",
  "重新按住录音键重试", "打开连接与自检",
]) && includesAll(runtimeFeedback, [
  "ClassifySessionEndFeedback(lineText)", "SessionEndFeedbackText(sessionEndFeedback)",
  "录音已结束 · 等待语音工具处理", "等待语音工具处理",
  'transientFeedbackState = waitingForTool ? "processing" : state',
]), "Recording completion lacks truthful third-party processing guidance");
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
assert(defaultConfig.schemaVersion === 32 && defaultConfig.onboardingVersion === 9 &&
  defaultConfig.voiceMode === "hold" && defaultConfig.captureSeconds === 0 &&
  defaultConfig.theme === "light" &&
  defaultConfig.inputRoutingMode === "strict" && defaultConfig.mappingPreset === "general" &&
  !Object.prototype.hasOwnProperty.call(defaultConfig, "customButtons"),
  "Default configuration is not the schema-32 stable hold baseline");
assert(!Object.prototype.hasOwnProperty.call(defaultConfig.mappings, "录音键"),
  "The recording button entered ordinary user mappings");
assert(JSON.stringify(defaultConfig.mappings) === JSON.stringify(expectedMappings),
  "Default mappings expose an unsupported control or changed a verified action");
assert(defaultConfig.activeShortcutProfileId === "general" &&
  defaultConfig.smartProfilesEnabled === false && defaultConfig.smartProfileLocked === false &&
  defaultConfig.smartProfileFallbackId === "general" &&
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
  assert(Array.isArray(profile.processNames), `Profile process bindings are missing: ${profile.id}`);
  for (const forbidden of ["gain", "audioEndpointName", "inputMethod", "inputMethodHotkey", "voiceMode"])
    assert(!Object.prototype.hasOwnProperty.call(profile, forbidden),
      `Shortcut Profile leaked voice configuration: ${profile.id}/${forbidden}`);
}
assert(defaultConfig.shortcutProfiles.find(profile => profile.id === "browser-ai").mappings["左键"] === "browserback",
  "Browser AI does not use the dedicated Browser Back action");
assert(defaultConfig.shortcutProfiles.find(profile => profile.id === "vibe-coding").processNames.includes("cursor") &&
  defaultConfig.shortcutProfiles.find(profile => profile.id === "browser-ai").processNames.includes("chrome") &&
  defaultConfig.shortcutProfiles.find(profile => profile.id === "terminal-agent").processNames.includes("windowsterminal"),
  "Recommended Smart Profile application bindings are incomplete");
for (const unsupported of ["电源键", "返回键", "音量 +", "音量 -"]) {
  assert(!Object.prototype.hasOwnProperty.call(defaultConfig.mappings, unsupported),
    `Unsupported default mapping is present: ${unsupported}`);
}
const mappingsPage = section(app, "private void BuildMappingsPage()", "private void BuildMappingsPageV13Legacy()");
assert(includesAll(mappingsPage, [
  // The page title matches the navigation entry ("快捷键"), which is the name the user clicked to get here:
  // the page used to be titled "按键" while the sidebar said "快捷键", and four of the six pages disagreed that way.
  'AddPageTitle("快捷键"', "管理遥控器实体键动作；录音键保持独立", "安全直通", "RemoteVisual",
  '"Home:short", "Home:long"',
  "AddFixedVoiceOverviewCard", "ShowMappingActionPicker", "TestMappingAction",
  '"按住听写 · 松开结束"', 'config.smartProfilesEnabled ? "回退 Profile" : "当前 Profile"', "录制键盘快捷键",
  "SetSmartProfilesEnabled", "ConfigureActiveSmartProfileApplications", "ToggleSmartProfileLock",
  "SwitchShortcutProfile", "CreateShortcutProfile", "RenameActiveShortcutProfile",
  "DeleteActiveShortcutProfile", "ImportShortcutProfile", "ExportActiveShortcutProfile",
  "layerEdit.Enabled = hardwareReady", "layerTest.Enabled = hardwareReady",
]), "The active shortcut page is not the verified device-protected mapping workflow");
// Gesture layering (短按 / 长按 / 双击) is verified end to end: the cards must render every row from the
// shared layer description, the extra layer must persist through the store, the bridge dispatch must
// resolve and run the layer sequence, and both builds must compile the layer sources.
const gestureCardBlock = section(app, "private void AddMappingOverviewCard", "private void EditGestureLayerAction");
assert(includesAll(gestureCardBlock, [
  "GestureBindingStore.DescribeLayer", "GestureKind.Short", "GestureKind.Long", "GestureKind.Double",
  "EditGestureLayerAction", "TestGestureLayer",
]), "The shortcut page does not render its three gesture rows from the shared layer description");
// Macros were built, judged to have no defensible use case, and removed. Every layer now binds exactly one
// action, and these NEGATIVE assertions keep the macro UI from creeping back before that problem is solved:
// a macro could only ride the double layer, and the first tap of a double tap also fired the short action,
// so invoking a macro ran it on top of an action the user never asked for.
assert(!gestureCardBlock.includes("Macro") && !app.includes("ShowGestureMacroEditor") &&
  !app.includes("GestureMacroErrorText") && !app.includes("GESTURE MACRO"),
  "The removed gesture-macro UI, its log lines or its card entry came back");
assert(includesAll(gestureCardBlock, ["layerEdit.Enabled = hardwareReady"]),
  "The double-tap row must bind one action, exactly like the other rows");
assert(includesAll(app, [
  "GestureBindingStore.UpsertLayer(document, gestureKey, kind,",
  "GestureLayers().TrySave(document)", "NormalizeGestureLayerAction", "TestMappingAction(label, action)",
]), "Gesture layers can be edited or tested without persisting through the store");
// Every one of the three rows must be able to persist: short and the Home/function-key long live in the
// mapping table, every other layer is owned by the store. Without this routing a 长按 row looks editable
// while silently saving nothing — which is exactly what it used to do for six keys.
assert(includesAll(app, [
  "IsConfigBackedGestureLayer(kind, longKey)", "internal static bool IsConfigBackedGestureLayer",
  "item.LongAction", "!hasConfigLongKey", "GestureBindingStore.UpsertLayer(document, gestureKey, kind,",
]), "A gesture row can be clicked without persisting the layer it shows");
assert(includesAll(bridge, [
  "GestureLayerPolicy.Classify(", "GestureBindingStore.ResolveSteps", "GestureMacroRunner.RunSteps",
  "GestureMacroRunner.DescribeResult", "doubleShortcut", "NormalizeGestureAction",
]) && !bridge.includes("macroSteps") && !bridge.includes("macroName"),
  "The input bridge does not dispatch gestures through the shared layer policy and runner, or can still run a removed macro");
// A fresh install received an empty layer table, which made the whole three-layer gesture surface
// invisible: every 长按 / 双击 row read 未配置 and nothing did anything until the user authored the
// table by hand. The recommended table is written exactly once, only when no table exists, so a user
// who clears it keeps it empty and an existing table is never overwritten. Home is deliberately left
// out -- its short press is 显示桌面 and the first tap of a double executes the short layer, so a Home
// double tap could not be completed at double-tap speed (measured on real hardware: 2235 ms apart).
assert(includesAll(read("scripts/features/GestureBindingStore.cs"), [
  "internal static GestureLayerDocument DefaultDocument()",
  'UpsertLayer(document, "up", GestureKind.Long, "pageup")',
  'UpsertLayer(document, "ok", GestureKind.Double, "mediaplaypause")',
  'UpsertLayer(document, "menu", GestureKind.Double, "volumeup")',
  'UpsertLayer(document, "tv", GestureKind.Long, "launch-client:chatgpt")',
]) && !read("scripts/features/GestureBindingStore.cs").includes('UpsertLayer(document, "home"'),
  "The recommended gesture layer table is missing, or it binds Home where its short press breaks the double tap");
assert(includesAll(app, [
  "EnsureGestureLayerDefaults();", "GestureBindingStore.DefaultDocument()",
  "GESTURE DEFAULTS seeded=true", 'reason=absent',
  "The recommended gesture defaults do not cover seven keys without touching Home or the record key",
  "A store without a layer file did not start empty",
  "The recommended gesture defaults did not survive a write and a reload",
]) && app.indexOf("EnsureGestureLayerDefaults();") > app.indexOf("hostLogPath = Path.Combine(sessionDir"),
  "A fresh install is not given the recommended gesture layers, the seeding is not pinned by a self-test, or the call runs before the host log exists so its only record is lost");
assert(includesAll(bridge, ['name.Equals("voice", StringComparison.OrdinalIgnoreCase)']),
  "The input bridge lets the record key enter gesture layering");
// A double-tap window tighter than the platform's own double-click default forces the user
// to tap faster than the remote can reliably report. Measured on a real RC003: a 320 ms
// window rejected a natural 378 ms double tap as two short presses, and tapping faster lost
// the key-down entirely (the log held two key-up edges and no key-down). The window now
// follows the user's own Windows double-click speed with the platform default as its floor,
// and the gesture card states the value it actually uses instead of a stale hard-coded one.
assert(includesAll(read("scripts/features/GestureLayerPolicy.cs"), [
  "DoubleTapWindowFloorMs = 500", "GetDoubleClickTime", "DoubleTapWindowCeilingMs",
  "internal static int DoubleTapWindowMs { get { return doubleTapWindowMs; } }",
]) && includesAll(app, [
  "The double-tap window is tighter than the Windows double-click default of 500 ms",
  "GestureLayerPolicy.DoubleTapWindowMs < GestureLayerPolicy.DoubleTapWindowFloorMs",
  "跟随 Windows 的双击速度设置",
]) && !app.includes("间隔 0.32 秒内") && !app.includes("双击可以挂一个宏"),
  "The double-tap window is tighter than the Windows double-click default, or the card still states a stale value");
assert(includesAll(hostBuild, [
  "GestureLayerPolicy.cs", "GestureBindingStore.cs", "GestureMacroRunner.cs",
]), "The host build does not compile the gesture layer sources");
assert(includesAll(read("BUILD_INPUT_BRIDGE.cmd"), [
  "GestureLayerPolicy.cs", "GestureBindingStore.cs", "GestureMacroRunner.cs",
]), "The input bridge build does not compile the gesture layer sources");
assert(includesAll(app, [
  'new ShortcutChoice("系统 · 区域截图", "win+shift+s")',
  'IsSupportedMappingAction("win+shift+s")', 'CustomActionText("win+shift+s") != "区域截图"',
  '"select-app:prompt"', '"open-url:prompt"', '"shortcut:prompt"',
  "Schema 25 migration discarded a valid custom mapping", "GetInstalledApplicationChoices",
  '"custom-button-test-result.json"', "MappingActionTestResult", "测试成功", "测试失败",
]), "Custom app, web, shortcut, or screenshot actions are absent");
assert(includesAll(app, [
  "KeyboardShortcutCaptureSession", "ShowKeyboardShortcutRecorder", "TryNormalizeCapturedShortcut",
  "TryNormalizeMappingShortcut", "MappingShortcutDisplay", "SetWindowsHookEx",
  "录制期间按键会被拦截", "一次只能录制一个主键", "Ctrl+Alt+Delete 由 Windows 保留",
  'new ShortcutChoice("录制键盘快捷键…", "shortcut:prompt")',
  "Keyboard shortcut recorder normalization invariant failed",
]), "Physical keyboard shortcut recording or its strict validation is incomplete");
assert(!mappingsPage.includes('"返回键"') && !mappingsPage.includes('"音量 +"') &&
  !mappingsPage.includes('"音量 -"') && !mappingsPage.includes('"电源键"'),
  "The active shortcut page exposes an unsupported physical control");
assert(includesAll(app, [
  "IsPersistableMappingAction", "PersistedMappingMatches", "MAPPING SAVE persisted=true",
  "Local application action did not survive config persistence and bridge generation",
  'normalized == "none"', 'Convert.ToBoolean(generatedDown["enabled"])',
]), "Local application actions can report success without surviving persistence");
const editMappingAction = section(app, "private void EditMappingAction", "private string ShowMappingActionPicker");
assert(includesAll(editMappingAction, [
  "SaveConfig(out mappingBridgeRevision)",
  "StartKeyboardBridgeForRevision(mappingBridgeRevision)",
]), "Active remote mapping feedback is not tied to the revision produced by the saved configuration");
assert(includesAll(app, [
  'Type.GetTypeFromProgID("Shell.Application")', '"shell:AppsFolder"',
  'Type.GetTypeFromProgID("WScript.Shell")', 'Environment.SpecialFolder.CommonPrograms',
  '"SOFTWARE\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\App Paths"',
  '"SOFTWARE\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall"',
  'SHParseDisplayName', 'ShellFileInfoPidl', 'GetApplicationDisplayName',
  "Start application Unicode invariant failed", "FinalReleaseComObject",
  "name.IndexOf('\\uFFFD')", "APPLICATION PICKER loaded=true",
]), "Installed application discovery can corrupt localized Windows app names");
// The add-application picker, reported broken by the user and reproduced from a screenshot of the
// real dialog: every "running now" row was listed with a blank gap instead of its icon, and with the
// raw process name ("catprox", "windowsterminal") for anything the curated name map does not cover.
// The running rows carried no icon at all, because that path never assigned one even though the
// executable of a running application yields one. The product's own process was offered as a target
// too. All three are pinned here, together with the icon fallback the catalogue needs for
// executables that carry no icon resource of their own (measured: Steam, BOOTICE).
assert(includesAll(app, [
  "internal static string RunningApplicationLabel(FocusApplicationChoice running, string cataloguedName,",
  "IsExecutableFileName", "choice.Icon = known != null && known.Icon != null",
  "InstalledAppCatalog.IconForExecutable(executable)", "InstalledAppCatalog.ExecutableForProcess(running.ProcessName)",
  "InstalledAppCatalog.DescribeExecutable(executable)",
  "A running application's picker name is resolved wrongly",
  "The picker can offer one of Vibe Flow's own processes as a target",
]) && app.indexOf("InstalledAppCatalog.List()") < app.indexOf("GetRunningApplications()"),
  "A running application is listed in the picker without its icon or its real name");
assert(includesAll(read("scripts/features/InstalledAppCatalog.cs"), [
  "internal static Icon IconForExecutable(string exePath)",
  "return extracted ?? LoadShellImage(exePath);",
  "private static Icon LoadShellImage(string parsingName)",
  "internal static string ExecutableForProcess(string processName)",
  "internal static string DescribeExecutable(string exePath)",
  "choice.Icon = IconForExecutable(target);",
  // shell:AppsFolder also lists desktop applications, so both sources are keyed by process name and
  // an AppUserModelID-registered desktop app resolves through its executable instead of its id.
  "HashSet<string> seenProcesses", "if (!seenProcesses.Add(processName)) continue;",
  '"System.Link.TargetParsingPath"',
  "string fromDesktopTarget = FocusTargetDescriptor.NormalizeProcessName(desktopTarget);",
]), "The installed-application catalogue can list one application twice or lose an icon");
assert(includesAll(read("scripts/features/FocusTargetService.cs"), [
  "internal static readonly string[] ExcludedProcesses",
  '"vibemic", "vibeflow", "voxdeckinputbridge", "vibemicatvvcapture"',
]), "Vibe Flow can offer one of its own processes as an application to learn");
// Page geometry is laid out by coordinate, and two rows on two pages collided: the voice page's
// CABLE status line ended at y=508 while the endpoint line below it began at y=502 (6 px), and the
// settings page's 安全检查更新 button ran 10 px into the product label beside it. Both were found
// by scripts/check-ui-geometry.ps1, which reports sibling controls whose rectangles intersect —
// nested parent/child overlap is normal, so a blanket rule would report hundreds of false
// positives. The check itself is the durable guard; these two pins keep the fixed coordinates from
// being reintroduced while a page is edited, and fail loudly if someone re-lays them out in a way
// that collides again.
assert(includesAll(app, [
  "cableState.Size = new Size(670, 24);",
  "cableEndpoint.Location = new Point(220, 506);",
  "cableEndpoint.Size = new Size(670, 18);",
  'SecondaryButton("安全检查更新", new Point(576, 184), new Size(108, 42))',
  "about.Location = new Point(694, 184);",
]), "Two page rows that collided on screen have been moved back on top of each other");
assert(includesAll(read("scripts/check-ui-geometry.ps1"), [
  "function Get-SiblingOverlaps",
  "EnumChildWindows already enumerates every descendant",
  "no overlapping sibling controls",
]) && gitignore.includes("!scripts/check-ui-geometry.ps1"),
  "The UI geometry check is missing, or it is not allowed through the scripts ignore rule");
// Every source file is BOM-less UTF-8, so the compiler has to be told the encoding instead of
// relying on its default. Measured: the same sources compiled with /codepage:1252 (an English build
// machine's code page) lose every Chinese literal, so a package built somewhere else could ship a
// mojibake interface while the build on the developer's machine looks perfect. The frozen Capture's
// build script is deliberately left alone — that binary is pinned by hash and is not rebuilt.
assert(includesAll(read("BUILD_VIBE_MIC.cmd"), ['/codepage:65001']) &&
  includesAll(read("BUILD_INPUT_BRIDGE.cmd"), ['/codepage:65001']) &&
  includesAll(v2FeatureSuite, ['"/codepage:65001"']) &&
  !read("BUILD_VIBE_MIC_CAPTURE.cmd").includes('/codepage'),
  "A build can decode the BOM-less sources with the build machine's code page and ship a mojibake interface");
// The interface asks for its fonts by family name, and a name that is not installed does not fail:
// GDI+ silently substitutes the default. For Chinese text that is survivable (measured by drawing two
// characters and confirming they do not come out as the same shape, i.e. Windows linked a CJK font),
// but the private-use icon glyphs have no such mapping — measured by drawing one through the
// substitute, it comes out as a hollow rectangle, which is a page of boxes where the section icons
// should be. The families are therefore resolved against what the machine has, the choice is logged
// and exported with the diagnostics so a garbled-interface report carries its own answer, and the
// self-test proves the check can see an absent family.
assert(includesAll(read("scripts/ui/UiFonts.cs"), [
  "internal static class UiFonts",
  '"Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "Microsoft JhengHei", "Segoe UI"',
  '"Segoe MDL2 Assets", "Segoe Fluent Icons", "Segoe UI Symbol"',
  "InstalledFontCollection", "internal static string Describe()",
  "internal static bool IsInstalled(string family)",
  "internal static Font Icon(float size, FontStyle style)",
]) && !app.includes('new Font("Segoe MDL2 Assets"'),
  "The interface can ask for a font family that is not installed and render its icon glyphs as boxes");
assert(includesAll(app, [
  "RunUiFontSelfTests();",
  '"UI RENDER " + UiFonts.Describe()',
  'report.AppendLine("UI rendering: " + UiFonts.Describe())',
  'report.AppendLine("Display: " + DescribeScreenGeometry())',
  "internal string DescribeScreenGeometry()",
  "The interface font check reports an absent family as installed, so it cannot detect the case that matters",
]) && hostBuild.includes('"%~dp0scripts\\ui\\UiFonts.cs"'),
  "The interface does not report or self-test which fonts and screen it renders with");
// Pages are built on navigation, which is long after Windows Forms applied its one-time autoscale, so
// their absolute coordinates never followed the display scaling while their fonts did: measured at 150%,
// the home page's fact row sat on the subtitle, its button row was cut by the card and 检查连接 was
// truncated, all while the window itself reported the same 1280x840 as at 100%. The pages and the shell
// are now scaled explicitly, the scroll canvas with them, and the ordering matters: the scale runs after
// the page is built.
assert(includesAll(app, [
  "private static void ScaleLayoutTree(Control parent, float scale)",
  "private int ScaledDesign(int designPixels)",
  "ScaleLayoutTree(content, DesignScale());",
  "ScaleLayoutTree(this, scale);",
  "sidebarPanel = sidebar;",
  '" sidebar=" + (sidebarPanel == null',
  '" scroll=" + (content == null',
  // The dock rule lives in the shared scaler now, and the sidebar is the control that needs it most: it is
  // docked left, so its Size is ignored and it stayed 232 px wide at 150% while everything around it grew.
  "UiDisplayScale.Tree(parent, scale);",
]) && includesAll(read("scripts/ui/UiDisplayScale.cs"), [
  "case DockStyle.Left:",
  "case DockStyle.Right: control.Width = width; break;",
]) && app.indexOf("BuildPage((VibePageId)currentPageIndex);") <
  app.indexOf("ScaleLayoutTree(content, DesignScale());"),
  "A page built on navigation is not scaled to the display, so its layout no longer matches its fonts");
// Two things are drawn rather than laid out, so scaling the control tree does not scale them: the
// sidebar's navigation icon is a bitmap drawn in 34x24 design units, and the remote illustration draws in
// 112x440 design units and caps how far it grows. Both stayed at their design size while their container
// doubled — measured at 200%: the icons looked shrunken and the illustration looked lost in a card twice
// its size. The icon surface is scaled and the illustration is told the display ratio by its owner.
assert(includesAll(app, [
  "graphics.ScaleTransform(scale, scale);",
  "var bitmap = new Bitmap(Math.Max(1, (int)Math.Round(34 * scale)),",
  "public float DesignScaleFactor = 1f;",
  "1.15f * Math.Max(1f, DesignScaleFactor)",
]) && app.indexOf("remoteVisual = new RemoteVisual();") <
  app.indexOf("remoteVisual.DesignScaleFactor = DesignScale();"),
  "A drawn element stays at its design size while the interface around it scales");
// The dark theme could not start at all. Its status borders lightened the accent with a fixed +62 per
// channel and no clamp, while the dark palette's violet (blue 213), amber and coral (red 205) exceed 193 —
// Color.FromArgb throws on a channel outside 0..255, and the home page builds a status border in its
// constructor. Measured: VibeFlow.exe exited with 0xE0434352, and the .NET Runtime event named
// StatusBorder as the faulting frame, so choosing 深色 — or 跟随系统 on a Windows that uses dark apps —
// meant the application would not launch.
assert(includesAll(app, [
  "internal static int LightenChannel(int channel)",
  "internal const int DarkBorderLighten = 62;",
  "return Math.Min(255, Math.Max(0, channel + DarkBorderLighten));",
  "return Color.FromArgb(LightenChannel(accent.R), LightenChannel(accent.G), LightenChannel(accent.B));",
  "RunThemePaletteSelfTests();",
  "The dark theme's border lightening leaves the byte range",
]) && !app.includes("accent.R + 62"),
  "The dark theme's status border can leave the byte range and stop the application from starting");
// The setup wizard is a separate form built at runtime, and its step pane is cleared and refilled on every
// step change by a builder that returns early from many branches — so scaling it "once at the end" never
// ran. At 200% the window was 2026x1416 while the 1000x680 content sat unscaled in the corner with
// doubled fonts: the heading was squeezed into its subtitle and the step labels were cut to
// "确认设备与" / "选择工具并". The wizard scales its chrome and installs a scale-on-add hook; the rail
// labels measure themselves instead of sitting in a fixed 146px box.
assert(includesAll(app, [
  "private void ScaleControlsAddedLater(Control root)",
  "UiDisplayScale.AddedLater(root, DesignScale());",
  "UiDisplayScale.Bounds(control, scale);",
  "ScaleControlsAddedLater(wizard);",
  "stepLabel.AutoSize = true;",
  "privacyRail.AutoSize = true;",
]) && includesAll(read("scripts/ui/UiDisplayScale.cs"), [
  "private static void InstallOnAdd(Control root, HashSet<Control> alreadyScaled, float scale)",
  "root.ControlAdded +=",
]) && app.indexOf("ScaleLayoutTree(wizard, DesignScale());") <
  app.indexOf("ScaleControlsAddedLater(wizard);"),
  "The setup wizard's content is not scaled to the display, so it renders at 96 dpi with doubled fonts");
// The dialogs are the same class of surface as the wizard and the pages: built at runtime, laid out at
// 96 dpi, with fonts that follow the display. Measured at 200%, the Smart Profile binding dialog drew its
// title with a doubled font inside a 1x box, cut its subtitle off, and stayed 766x661 while its fonts
// doubled. The algorithm lives in one shared class so the pages, the wizard and the dialogs cannot drift
// apart, and every dialog — eight inline ones plus the five in scripts/ui — is prepared with it.
assert(includesAll(read("scripts/ui/UiDisplayScale.cs"), [
  "internal static class UiDisplayScale",
  "internal static float ForControl(Control control)",
  "internal static void Apply(Form form)",
  "internal static void Tree(Control parent, float scale)",
  "internal static void Bounds(Control control, float scale)",
  "internal static void AddedLater(Control root, float scale)",
  "form.Load +=",
]) && includesAll(app, ["UiDisplayScale.Tree(parent, scale);", "UiDisplayScale.Bounds(control, scale);",
  "UiDisplayScale.AddedLater(root, DesignScale());"]) &&
  (app.match(/UiDisplayScale\.Apply\(dialog\);/g) || []).length >= 8 &&
  includesAll(read("scripts/ui/AppPickerDialog.cs"), ["UiDisplayScale.Apply(this);"]) &&
  includesAll(read("scripts/ui/LiveHudForm.cs"), ["UiDisplayScale.Apply(this);"]) &&
  includesAll(read("scripts/ui/ContextDeckForm.cs"), ["UiDisplayScale.Apply(this);"]) &&
  !read("scripts/ui/BrowserRemoteLiteForm.cs").includes("UiDisplayScale.Apply(this);") &&
  hostBuild.includes("UiDisplayScale.cs"),
  "A dialog is laid out at 96 dpi on a scaled display, so its fonts overflow the boxes they are drawn in");
// Capture & Ask is deliberately *not* in that list: it was scaled twice that way — once by Windows Forms, which
// scales a form carrying a (96,96) design baseline, and once by UiDisplayScale — and the double size was then
// clamped by its own working-area fit, so it filled the screen instead of taking its design size. Measured after
// removing the second pass: 1534x1329, client area 1508x1258, which is its design client size times this
// display's scaling. Its own assertion above keeps the wiring from coming back.
// A rounded region is cut from the control's size, so a control resized afterwards is clipped to the old
// shape. The page scaling resizes every control, so at 200% the profile status badges rendered as a small
// box with their text cut off — found by screenshot, because neither the overlap rule nor the text-fits
// rule can see a control clipped by its own region. The region is now rebuilt on resize.
assert(includesAll(app, [
  "private void ApplyRoundedRegion(Control control, int radius)",
  "control.Resize += delegate",
  "private void ApplyRoundedRegionCore(Control control, int designRadius)",
  "int radius = ScaledDesign(designRadius);",
]), "A rounded control is clipped to its original size after the layout is scaled");
// A client that is installed but never registers itself under "App Paths" can still be
// started the way Explorer starts it: from its own Start-menu shortcut. Measured on a real
// machine, Cursor lives in D:\cursor\ with a working Start-menu shortcut and no App Paths
// entry, so a cold start logged "Client launcher unavailable target=cursor" while the very
// same shortcut opened it in under five seconds. The launcher now tries the Start-menu
// shortcut between the packaged-app id and a bare executable name, and the Win32 start-app
// probe gets enough wall clock to answer on a busy machine instead of sitting on its edge.
assert(includesAll(bridge, [
  "TryLaunchInstalledStartApp(startAppNames)",
  "TryLaunchStartMenuShortcut(startAppNames)",
  "TryLaunchExecutable(executableNames)",
  "private static bool TryLaunchStartMenuShortcut(string[] applicationNames)",
  "private static List<string> FindStartMenuShortcuts(string applicationName)",
  "internal static List<string> FindStartMenuShortcuts(string applicationName, string[] searchRoots)",
  "private static void CollectShortcuts(string directory, string applicationName,",
  "internal static string ResolveShortcutTarget(string shortcutPath)",
  "private static void CreateProbeShortcut(string shortcutPath, string targetPath)",
  "private static bool StartMenuProbeContains(List<string> located, string shortcutPath)",
  'Directory.CreateDirectory(Path.Combine(probeRoot, "Vendor"))',
  "The start-menu search found ",
  'Directory.GetFiles(directory, "*.lnk")',
  "Environment.SpecialFolder.CommonStartMenu",
  'Type.GetTypeFromProgID("WScript.Shell")',
  "TargetPath",
  "Client launcher start_menu_shortcut name=",
  "process.WaitForExit(12000)",
  "Resolving a start-menu shortcut returned '",
]) && !bridge.includes("process.WaitForExit(6000)"),
  "An application installed without an App Paths entry cannot cold start from its Start-menu shortcut");
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
assert(includesAll(bridge, [
  "DEFAULT_LONG_PRESS_MS = 650", "HOLD_REPEAT_INITIAL_DELAY_MS = 420",
  "HOLD_REPEAT_INTERVAL_MS = 80", "VOICE_RESTART_GUARD_MS = 500",
  "SMART_PROFILE_POLL_MS = 250", "SMART_PROFILE_DEBOUNCE_MS = 350",
  "InitializeSmartProfileRuntime", "EvaluateSmartProfile", "ResolveSmartProfileTarget",
  "FindBridgeProfileForProcess", "GetForegroundProcessName", "IsVibeFlowProcess",
  'health["smart_profiles_enabled"]', 'health["smart_profile_effective_id"]',
  'health["smart_profile_match_state"]', "Smart Profile did not match the foreground process",
  "Smart Profile fallback was not deterministic", "Smart Profile lock did not retain the configured Profile",
]), "Smart Profile routing, health, or deterministic resolver coverage is incomplete");
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
  // The card's statement follows the actual state now: it used to assert device-level isolation unconditionally,
  // right next to a badge and a note saying that isolation was not there.
  "尚未逐设备隔离：遥控器按键与实体键盘可能同时生效（签名通道为可选增强）",
  "设备级隔离已启用：只有带 RC003 身份的事件会执行遥控器动作",
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
const nonVoiceHookBranch = section(bridge, "bool nonVoiceCandidate",
  "if (isVoiceMapping && mapping.suppress)");
assert(nonVoiceHookBranch.includes("return CallNextHookEx") &&
  !nonVoiceHookBranch.includes("return (IntPtr)1") &&
  !nonVoiceHookBranch.includes("QueueMapping("),
  "The device-blind keyboard hook can suppress or execute a non-voice RC003 candidate");
assert(bridge.includes("IsDeviceScopedVoiceSource") &&
  !bridge.includes('HandleVoicePhysicalTransition(isDown, "keyboard_hook"') &&
  !bridge.includes('HandleVoicePhysicalTransition(!keyUp, "keyboard_hook"') &&
  !bridge.includes('MarkRemoteInput("keyboard_hook")') &&
  bridge.includes('"rc003_present_hook"'),
  "The device-blind keyboard hook can still enter the voice recording state machine outside its RC003-connected scope");
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
  "PrepareOnboardingProgressState(config, currentStep", "config.resumeSetupAfterRestart",
  "BeginVbCableInstallMonitor", '"我已看到文字"',
  "目视确认测试框中的文字", "textInsertionConfirmed", "WaitForBridgeConfigRevision",
  "OnboardingProgressSaveResult", "OnboardingAudioEvidenceReady", "ApplySettingsChangeCore",
  "OnboardingRuntimeAckRequired", "initialStartupApplied",
  "AutoScaleMode.Dpi", "AutoScroll = true", "配置工作流",
  // The two product terms a first-time user meets in this step now carry a Chinese gloss in place.
  "配置浏览器遥控（Browser Remote Lite）", "是否启用 Smart Profiles（按应用自动切换键位方案 · 可选，默认关闭）",
  "smartProfilesChoice",
  "config.smartProfilesEnabled = smartProfilesChoice",
]), "The active onboarding flow is not the persisted five-task setup");
assert(!onboarding.includes("testInput.TextChanged") && !onboarding.includes("testInput.Text.Trim()") &&
  !onboarding.includes("confirmedTextLength"),
"The onboarding flow reads or stores third-party transcription text");
const onboardingProgressState = section(app, "private static void PrepareOnboardingProgressState",
  "private static void ClearOnboardingChoiceDraft");
assert(includesAll(onboardingProgressState, [
  "value.onboardingVersion = CurrentOnboardingVersion", "value.onboardingStep =",
  "StageOnboardingChoiceDraft",
]) && !["inputMethod =", "inputMethodHotkey =", "inputMethodTrigger ="]
  .some((token) => onboardingProgressState.includes(token)),
"Onboarding progress persistence can commit unconfirmed voice-provider settings");
const selfCheckActions = section(app, "private void HandleSelfCheckAction(string action)", "private void RunSelfCheckAndRefresh()");
const selfCheckCableAction = section(selfCheckActions, 'else if (action == "install-cable")', 'else if (action == "restore-profile")');
assert(includesAll(selfCheckCableAction, [
  "ActionResult installResult = LaunchVBCableInstaller()",
  "if (installResult.State != ActionState.Checking)",
  "BeginVbCableInstallMonitor(ShowActionToast)",
]) && !selfCheckCableAction.includes("config.resumeSetupAfterRestart = true"),
"Self-check can persist onboarding recovery before VB-CABLE reports a real installer state");
const vbCableMonitor = section(app, "private void BeginVbCableInstallMonitor", "private static ActionResult VbCableRestartRecoveryResult");
assert(includesAll(vbCableMonitor, [
  "TryReadVbCableInstallState", "VbCableInstallStateAllowsRecovery",
  "PrepareVbCableRestartRecovery", "ClearVbCableRestartRecoveryAfterFailure",
  "VbCableInstallStateIsTerminal",
]) && section(app, "private static bool VbCableInstallStateAllowsRecovery",
  "private static ActionResult VbCableInstallStateResult").includes('value == "installing" || value == "installed"'),
"VB-CABLE restart recovery is not gated by the script's verified installer state");
assert(includesAll(cableInstaller, [
  "Get-FileHash", "b950e39f01af1d04ea623c8f6d8eb9b6ea5c477c637295fabf20631c85116bfb",
  "Get-AuthenticodeSignature", "Start-Process -FilePath $setupPath",
  "officialSiteUrl", "VBCABLE_Driver_Pack45.zip", "vb-audio.com/Cable/",
  'Join-Path $PSScriptRoot "..\\tools\\VBCABLE_Driver_Pack45.zip"',
]), "VB-CABLE setup is not pinned, signature checked, bundled, or launched safely");
const thirdPartyNotices = read("THIRD_PARTY_NOTICES.md");
assert(thirdPartyNotices.includes("www.vb-cable.com") &&
  thirdPartyNotices.includes("donationware") &&
  thirdPartyNotices.includes("Distribution with other product"),
  "VB-CABLE bundling attribution or license notice is missing");
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
// Windows labels CABLE Output as a line-level endpoint, and voice tools that only list
// microphones then hide the remote audio. The repair must stay a device-class change
// driven by an explicit capability policy, never a clipboard or input-injection path.
assert(includesAll(audioEndpointService, [
  "AudioEndpointShapePolicy", "IsVirtualCableCaptureName", "NeedsMicrophoneShape",
  "NeedsLineLevelShape", "FormFactorLineLevel = 2", "FormFactorMicrophone = 4",
  "TryReadVirtualCableShape", "TrySetFormFactor", "PropVariantClear",
  "BCDE0395-E52F-467C-8E3D-C4579291692E", "A95664D2-9614-4F35-A746-DE8DB63617E6",
  "1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E", "A45C254E-DF1C-4EFD-8020-67D146A850E0",
  "OpenPropertyStore(StorageReadWrite", "ENDPOINT-NOT-FOUND", "ENDPOINT-STORE-READONLY",
]) && !/Clipboard\.|SendKeys\.|keybd_event|SendInput/.test(audioEndpointService),
"The audio-endpoint capability does not read the real endpoint class through Core Audio, or it fell back to clipboard and keyboard injection");
assert(includesAll(audioEndpointService, [
  "TryReadEndpointLevel", "TrySetEndpointLevel", "TryReadEndpointFormat",
  "IAudioEndpointVolume", "IAudioClient", "GetMasterVolumeLevelScalar", "GetMixFormat",
  "5CDF2C82-841E-4546-9722-0CF74078229A", "1CB9AD4C-DBFA-4C32-B178-C2F568A703B2",
]), "The audio-endpoint capability cannot read or repair the cable level and format");
assert(includesAll(app, [
  "RepairCableOutputLevel", "repair-cable-level", "恢复 CABLE 音量",
  "录音端音量", "CABLE-LEVEL-UNVERIFIED", "AudioEndpointService.TryReadEndpointLevel",
]), "The self-check does not detect or repair a muted or attenuated virtual cable");
assert(includesAll(audioEndpointService, [
  "TryListCaptureSessions", "IAudioSessionManager2", "IAudioSessionEnumerator",
  "IAudioSessionControl2", "GetProcessId", "77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F",
]), "The audio-endpoint capability cannot verify which process really records the cable");
assert(includesAll(app, [
  "CaptureSessionEvidence", "正在读取 CABLE Output", "BluetoothContentionNote",
  "bluetooth_devices=", "bluetooth_audio_endpoints=", "BluetoothDeviceCount", "BluetoothAudioEndpointCount",
  "争用空口带宽", "usb_selective_suspend_ac=", "usb_selective_suspend_dc=", "UsbSelectiveSuspendAc",
  "USB 选择性挂起已启用",
]), "The self-check does not prove the voice tool reads CABLE Output or flag Bluetooth contention");
// Dropout frequency is a per-release metric, so the app keeps a bounded metadata-only
// baseline of link measurements and never stores text.
assert(includesAll(linkBaselineStore, [
  "LinkBaselineSample", "LinkBaselineStore", "maxGapMs", "drops", "triggerToReadyMs",
  "Capacity = 20", "TryAppend", "CurrentSummary", "本机基线",
]) && !/Clipboard\.|SendKeys\.|keybd_event|SendInput|WindowTitle|windowTitle/.test(linkBaselineStore),
"The link baseline store is missing, unbounded, or stores more than link metadata");
assert(hostBuild.includes('"%~dp0scripts\\features\\LinkBaselineStore.cs"'),
  "The Host build does not compile the link baseline store");
assert(includesAll(app, [
  "linkBaselineStore", "LinkBaselineNote", "LINK BASELINE recorded=",
]) && app.indexOf("LINK BASELINE recorded=") > app.indexOf("private void ReportLinkQualityAfterSession"),
"The Host does not record or surface the link baseline");
// Verified on this machine: USB selective suspend parks the Bluetooth adapter and
// stalls the remote's audio for 271-537 ms, and this system has no Power Options
// entry for it at all. The app therefore performs the documented powercfg change
// itself, reversibly, behind UAC.
assert(includesAll(usbSuspendScript, [
  "2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226",
  "powercfg /setacvalueindex", "powercfg /setdcvalueindex", "powercfg /setactive",
  "-Disable", "-Restore", "Test-IsAdministrator", "-Verb RunAs",
  "ac_before", "ac_after", "elevation_required", "[switch]$StatusOnly", "if ($StatusOnly)",
]) && usbSuspendScript.indexOf("Start-Process -FilePath \"powershell.exe\"") > 0,
"The USB selective-suspend helper is missing its reversible, elevated implementation");
assert(includesAll(app, [
  "SetUsbSelectiveSuspend", "repair-usb-suspend", "禁用 USB 选择性挂起",
  "UsbSuspendSubgroup", "UsbSuspendSetting", "USBSUSPEND-SCRIPT-MISSING",
  "USB 选择性挂起已禁用",
]) && app.indexOf("SetUsbSelectiveSuspend(action ==") > 0,
"The self-check does not offer the one-click USB selective-suspend fix");
// The change is reversible, so the undo has to be reachable and honest: it is offered only when the
// script's own state file records that this app applied the disable, never merely because the value
// reads 0 -- a corporate image or the user's own tuning would also read 0, and silently re-enabling
// suspend there would be an unwanted change to their power policy.
assert(includesAll(app, [
  "restore-usb-suspend", "还原 USB 选择性挂起", "UsbSuspendWasAppliedByApp",
  '"Vibe Flow Remote", "usb-suspend", "state.json"', '"ac_after"',
]) && app.indexOf("UsbSuspendWasAppliedByApp()") < app.indexOf("restore-usb-suspend"),
  "The self-check cannot undo the USB selective-suspend repair, or offers it without knowing who applied it");
assert(app.indexOf("hardware.UsbSelectiveSuspendAc == 1") < app.indexOf("repair-usb-suspend"),
"The Bluetooth self-check does not use the measured selective-suspend value");
// The panel that is started or submitted twice can replace text that already
// already appeared; the app must count that and advise instead of blaming audio.
assert(includesAll(app, [
  "ObservePanelStimulus", "ReportPanelStimulusAfterSession", "PANEL STIMULUS start=",
  "WETYPE TOOLBAR CLICK", "panelStartStimulusCount", "panelSubmitStimulusCount",
  "如果文字出现后被替换或收回",
]) && app.indexOf("ReportPanelStimulusAfterSession();") >
  app.indexOf("SetSessionFeedback(sessionEndFeedback, SessionEndFeedbackText(sessionEndFeedback));"),
"The Host does not count repeated provider-panel stimulation or advise on withdrawn text");
// Which input method owns the keyboard decides whether a provider's own voice panel
// can answer at all. Both identifiers must be matched because TSF reports the TIP
// CLSID and the language-profile GUID, and they are easy to confuse.
assert(includesAll(inputMethodDetector, [
  "InputEngineCatalog", "ClassifyEngine", "DescribeEngine",
  "ProviderRequiresOwnInputMethod", "ActiveEngineBlocksProvider",
  "9D2B2E2B-3C93-4D2F-9D35-6EEB85F0D2B0", "2B4D4B3A-4D4F-4C0A-8E66-7F771A2B9C10",
  "86598FB9-66A2-463E-B9C2-AEB906D477AD", "607FDF85-FCC8-4DBD-A365-41296F980C9C",
  "81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", "FA550B04-5AD7-411F-A5AC-CA038EC515D7",
  "33C53A50-F456-4884-B049-85FD643ECFED", "71C6E74C-0F28-11D8-A82A-00065B84435C",
  "34745C63-B2F0-4784-8B67-5E12C8701A31", "GetKeyboardLayout",
  "TFInputProcessorProfile", "AllocHGlobal", "FreeHGlobal",
]) && !/Clipboard\.|SendKeys\.|keybd_event|SendInput/.test(inputMethodDetector),
"The input-method detector lost the verified identifiers, the TSF lookup, the caller-owned profile buffer, or gained an input-injection path");
// The TSF read is per-thread, so it answers "which input method is active for this process" and not
// "which one the application being typed into uses". The foreground window's own thread layout is now
// read as well, and described as a layout rather than as a named input method, because another
// process's TSF profile cannot be read from here.
assert(includesAll(inputMethodDetector, [
  "internal sealed class ForegroundInputLayout",
  "internal static ForegroundInputLayout ReadForegroundLayout(IntPtr window)",
  "GetForegroundWindow", "GetWindowThreadProcessId", "AttachThreadInput", "GetCurrentThreadId",
  "internal static string DescribeLanguage(int languageId)",
]) && !/Clipboard\.|SendKeys\.|keybd_event/.test(inputMethodDetector),
  "The input-method detector does not read the foreground window's own thread layout, or reached for an input path");
assert(includesAll(app, [
  "ForegroundInputLayout foregroundLayout = InputMethodDetector.ReadForegroundLayout(IntPtr.Zero)",
  "foreground_language=", "same_layout=", "foreground_layout=unavailable",
  "The foreground input layout reader returned null",
]), "The Host does not report the foreground window's input layout alongside the per-thread input method");
assert(hostBuild.includes('"%~dp0scripts\\features\\InputMethodDetector.cs"'),
  "The Host build does not compile the input-method detector");
assert(includesAll(app, [
  "RefreshActiveInputEngineIfStale", "RefreshActiveInputEngine",
  "INPUT ENGINE ACTIVE", "INPUT ENGINE conflict=true",
  "ActiveInputEngineSelfCheckNote", "ActiveInputEngineBlocksConfiguredProvider",
  "NotifyActiveInputEngineConflict",
]), "The Host does not detect, log, or surface the active input method");
assert(app.indexOf("RefreshActiveInputEngineIfStale();") > app.indexOf("PollFocusTargetForVoiceLock();") &&
  app.indexOf("RefreshActiveInputEngine();") > app.indexOf("CheckVirtualCableCaptureShapeAsync();"),
"The active-input-method refresh is missing from the startup and idle-poll paths");
// A finished driver install must switch the app back to the isolated audio path by
// itself (verified: reinstalling the bundled driver needed no reboot), and it must
// never claim a switch the endpoints or the active mode cannot deliver.
assert(includesAll(app, [
  "ApplyVbCableInstallCompletion", "VbCableInstallCompletionAction",
  "VB-CABLE INSTALL endpoints", "action=\" + action",
  "switch_to_full_mode", "already_full_mode", "reboot_required", "diagnostic_trigger_only",
]) && app.indexOf("ApplyVbCableInstallCompletion();") >
  app.indexOf("safePublish(visible);"),
"The VB-CABLE install monitor does not hand the finished install to the mode switch");
assert(includesAll(app, [
  "RunVbCableInstallCompletionSelfTests", "VB-CABLE install completion policy is wrong",
]), "The install-completion policy is not pinned by the host self-test");
// V2.0 retired the Doubao input method as a selectable voice tool: it filters every
// synthetic keystroke and its panel records its own microphone. The supported list
// must stay the three verified tools plus the custom entry, and a stored V1.5 value
// must be migrated visibly instead of silently behaving like another tool.
assert(includesAll(app, [
  '"微信输入法", "Typeless", "Windows 语音输入", "其他语音工具"',
  '"微信输入法", "Typeless", "Windows 语音输入", "其他自定义工具"',
]) && !app.includes('case "doubao"') && !app.includes('"豆包输入法", "Windows'),
"The voice tool list still offers the retired Doubao input method");
assert(includesAll(app, [
  "IsRetiredProviderValue", "retiredProviderMigrated", "PROVIDER MIGRATED retired=doubao",
  "豆包输入法不再作为言灵的语音工具选项", "if (IsRetiredProviderValue(value.inputMethod))",
  'value.inputMethodHotkey = DefaultHotkeyForProvider("wechat");',
]) && app.indexOf("retiredProviderMigrated = true;") > app.indexOf("private static bool MigrateConfig"),
"A stored retired provider value is not migrated visibly by the configuration migration");
assert(!app.includes("BeginDoubaoVoiceBarSession") && !app.includes("ShouldToggleProviderVoiceBarForSession"),
"The retired Doubao automation path is still wired into the voice wake handler");
// Stability is the user's first pain point: the link verdict must come from the
// measured session metrics, must mirror the published self-check gate, and must stay
// advisory — a good link is never reported as proof that text arrived.
assert(includesAll(linkQualityPolicy, [
  "LinkQualityVerdict", "LinkQualityPolicy", "Classify",
  "HealthyMaxGapMs = 250", "DegradedMaxGapMs = 600",
  "HealthyTriggerToReadyMs = 1500", "DegradedTriggerToReadyMs = 3000",
  "HealthyOutputRmsPercent = 0.8", "MinimumAssessableAudioMs = 700",
  "AUDIO_DROPS", "GAP_HIGH", "GAP_ELEVATED", "LATENCY_HIGH", "LEVEL_LOW",
  "SESSION_FAILED", "AUDIO_TOO_SHORT", "NO_RECEIPT", "HEALTHY", "NO_SESSION",
]) && !/Clipboard\.|SendKeys\.|keybd_event|SendInput/.test(linkQualityPolicy),
"The link-quality policy lost its measured thresholds or gained an input-injection path");
assert(hostBuild.includes('"%~dp0scripts\\features\\LinkQualityPolicy.cs"'),
  "The Host build does not compile the link-quality policy");
assert(includesAll(app, [
  "CurrentLinkQuality", "LinkQualitySelfCheckNote", "LinkQualitySelfCheckAdvice",
  "ReportLinkQualityAfterSession", "LINK QUALITY state=", "SESSION PREFLIGHT bridge=",
  '"链路质量"',
]) && app.indexOf("ReportLinkQualityAfterSession(generation);") >
  app.indexOf("SetSessionFeedback(sessionEndFeedback, SessionEndFeedbackText(sessionEndFeedback));"),
"The Host does not report, log, or surface the measured link quality");
assert(includesAll(app, [
  "RunLinkQualityPolicySelfTests", "Link quality reported a verdict without a session",
  "Link quality claimed a healthy link without a receipt",
]), "The link-quality policy is not pinned by the host self-test");
// The home status strip is refreshed by the idle poll as well, so both refresh
// paths must read the same verdict or the panel would contradict itself.
assert(includesAll(section(app, "private void UpdateOverviewStatus()", "private string GestureMappingSummary"),
  ["CurrentLinkQuality", "liveQuality.Summary", "liveQuality.IsGood", "triggerOnlyOverview"]),
"The live home status refresh does not use the measured link verdict");
// A workflow card composes the shortcut Profile, the learned input target and the
// voice tool for one application. It must stay a read-only composition (no new
// persistence), and every gap must name the missing part plus a one-click fix.
assert(includesAll(workflowCards, [
  "WorkflowAppBinding", "WorkflowTargetBinding", "WorkflowCard", "WorkflowCards",
  "GapNoProfile", "GapNoTarget", "GapTargetUnverified", "GapProviderNotRunning", "GapNeverObserved",
  "DescribeGap", "AdviseGap", "ActionTextForGap", "ActionForGap", "Summarize",
  "workflow-profile", "workflow-target", "workflow-learn", "打开应用并学习",
]) && !/Clipboard\.|SendKeys\.|keybd_event|SendInput|File\.|Directory\./.test(workflowCards),
"The workflow-card composition gained persistence or an input/activation path");
// The favourite-app surface may only offer 「保存」 for a target that was just learned, and the Host must
// refuse a save without one. The retired dialog's "verify once then auto-save" is now stricter: learn
// once, and the user presses 「保存」 themselves.
assert(includesAll(favoriteAppsPanel, [
  "已经识别到「", "点「保存」完成设置", "等待你点「保存」", "onSave(pendingProcess)",
]) && includesAll(app, [
  "BeginFavoriteAppLearning()", "FAVORITE SAVE blocked=true reason=no_successful_learning",
]), "The favourite-app flow can save a target that was never learned, or lost its pending-save banner");
// The surface that replaced the retired learning dialog must keep the layout guarantee that dialog
// carried: its measured height reserves a row and the save banner, and it never clips its own controls.
assert(includesAll(app, [
  "FavoriteAppsPanel.MeasureHeight", "FavoriteAppsPanel.Build", "FavoriteAppsPanel.RowHeight",
  "FavoriteAppsPanel.BannerHeight", "Favourite-app panel clipped one of its own controls",
]), "The favourite-app panel lost its measured-height or no-clipping layout guard");
// Usage statistics stay metadata-only: the policy aggregates receipt lines through a timestamp function
// and takes nothing else, and the settings page states the log-window scope next to the numbers.
assert(includesAll(usageStatsPolicy, [
  "Summarize(IList<string> lines, Func<string, DateTime?> timestampOf)",
  "WETYPE SESSION END", "TRANSCRIPTION SESSION END", "audio_delivered=True", "submitted=True",
  "LongestGapMs", "SuccessRatePercent",
]) && !/ExtractMetric|WindowTitle|TargetName|TranscriptionText/.test(usageStatsPolicy),
  "Usage statistics must aggregate receipt lines alone, without reading identifying metrics");
assert(includesAll(app, [
  "BuildUsageStats()", "UsageStatsPolicy.Summarize", "UsageStatsLine(usageStats)",
  "使用统计（仅元数据）", "只统计有结束回执的会话", "不包含录音、转写文字、窗口标题或设备地址",
  "RunUsageStatsSelfTests();",
]), "The settings page does not show metadata-only usage statistics or state their scope");
assert(hostBuild.includes('"%~dp0scripts\\features\\UsageStatsPolicy.cs"'),
  "The host build does not compile the usage statistics policy");
// Phrase packs hold the user's OWN text: stored locally, referenced from the mapping table by an opaque
// id only, and typed with per-character Unicode key events. The clipboard path is what the product
// criticises in paste-based dictation, so it is forbidden here outright.
assert(includesAll(snippetStore, [
  "snippets.json", 'ActionPrefix = "snippet:"', 'ManageAction = "snippet:manage"',
  "MaxTextLength", "NormalizeText", "IsSnippetAction", "Upsert", "Remove", "SnippetNaming",
]) && !/Clipboard\.|SendKeys\.|keybd_event/.test(snippetStore),
  "The snippet store lost its local-only persistence or action ids, or gained a clipboard path");
assert(includesAll(bridge, [
  'normalized.StartsWith("snippet:", StringComparison.OrdinalIgnoreCase)',
  "SnippetTextFor", "TypeUnicodeText", "SendUnicodeCharacter", "KEYEVENTF_UNICODE",
  "public BridgeSnippet[] snippets { get; set; }",
]) && !/Clipboard\.|SendKeys\.|keybd_event/.test(bridge),
  "The input bridge does not type snippets with Unicode key events, or reaches for the clipboard");
// The phrase itself must never be logged: only how much was typed.
assert(bridge.includes('" characters=" + snippetText.Length') && !bridge.includes("+ snippetText +"),
  "The input bridge logs the snippet text instead of only its length");
assert(includesAll(app, [
  "SnippetStore.ManageAction", "ShowSnippetManager", "用语片段 · 管理…", "SnippetNaming.ResolveName",
  "SnippetStore.IsSnippetAction(value)", "snippetStore.TrySave(document)", "RunSnippetSelfTests();",
  "只保存在本机", "不经过剪贴板",
]), "The host lost the snippet manager, its local-only notice, or the snippet action plumbing");
// A Profile switch changes the active mappings and nothing else. It used to be a hand-written field
// copy that dropped the phrase table, so with Smart Profiles enabled -- the normal case -- the first
// switch made every bound phrase unknown at dispatch time. Measured on the installed build: the same
// action that was rejected as `unknown_snippet` succeeded once the projection carried the table.
assert(includesAll(bridge, [
  "config = ProjectActiveProfile(source, target)",
  "private static BridgeConfig ProjectActiveProfile(BridgeConfig source, BridgeShortcutProfile target)",
  "snippets = source.snippets",
  "Switching a Profile dropped a field of the bridge configuration",
  "A snippet table that arrived as JSON did not deserialize into the Bridge config",
]) && !bridge.includes("var next = new BridgeConfig"),
  "Switching a Profile can drop the phrase table or another field of the bridge configuration");
assert(hostBuild.includes('"%~dp0scripts\\features\\SnippetStore.cs"'),
  "The host build does not compile the snippet store");
{
  const activatedStart = app.indexOf("protected override void OnActivated(EventArgs e)");
  const onActivated = activatedStart < 0 ? "" : app.substring(activatedStart, activatedStart + 700);
  assert(includesAll(app.substring(app.indexOf("protected override void OnActivated(EventArgs e)"),
  app.indexOf("protected override void OnActivated(EventArgs e)") + 700), [
  "refreshSelfCheckOnActivate = false;", "windowsHardwareProbeAt = DateTime.MinValue;",
]) && app.indexOf("new FocusTargetDialog(focusTargetStore") < 0,
"The deferred self-check rebuild no longer refreshes the self-check page, or the retired dialog guard is back");
}
assert(!/workflow-learn:"[\s\S]{0,260}refreshSelfCheckOnActivate = true;/.test(app),
"Opening the learning flow still schedules a self-check rebuild that closes the dialog");
// Consoles and terminal editors expose a focused Text/Document surface with no
// writable ValuePattern; they must be learnable as focus-only targets that the app
// locks and restores but never writes into itself.
assert(includesAll(focusTargetService, [
  "FocusOnlyStrategy", "uia_focus", "HasFocusOnlyPattern", "focusOnlyControl",
  "ControlType.Text", "ControlType.Document",
]), "The capture path cannot learn a console or terminal text surface");
assert(includesAll(focusTargetModels, [
  "uia_focus", "focusOnly", "FOCUS-TARGET-NOT-EDITABLE",
]), "Verification still rejects focus-only targets such as consoles");
// Being learnable is not enough: the descriptor also has to match again. Matching used to
// require ControlType.Edit for every target and a writable ValuePattern for every target,
// so a focus-only target was learned, saved, and then reported FOCUS-TARGET-STALE on every
// observation while the text was never delivered. The accepted control type and the proof
// of a match both follow the stored strategy now.
assert(includesAll(focusTargetService, [
  "IsFocusOnlyTarget", "AcceptsStoredControlType", "SatisfiesTargetEvidence",
  "IsFocusOnlyTarget(target) ? HasFocusOnlyPattern(element)",
]), "Focus-only targets cannot be matched again after being learned");
assert(!/current\.ControlType != ControlType\.Edit/.test(focusTargetService),
  "Matching demands ControlType.Edit again, which makes every focus-only target unverifiable");
assert(/IsFocusOnlyTarget\(target\)[\s\S]{0,300}OrCondition[\s\S]{0,400}ControlType\.Text[\s\S]{0,200}ControlType\.Document/.test(focusTargetService),
  "The candidate scan only looks for Edit controls, so a focus-only target is never inspected");
assert(includesAll(focusTargetService, [
  "bool writable = descriptorMatches && SatisfiesTargetEvidence(target, focused);",
]), "Target verification does not ask for the evidence the stored strategy requires");
assert(includesAll(app, [
  "BeginWorkflowTargetLearning", "WORKFLOW LEARN process=", "ShowWindowForProject",
  "SetForegroundWindowForProject", 'StartsWith("workflow-learn:"', "BeginFavoriteRelearn(processName)",
]) && app.indexOf("BeginFavoriteRelearn(processName)") > app.indexOf("private void BeginWorkflowTargetLearning") &&
  app.indexOf('StartsWith("workflow-learn:"') > app.indexOf("else if (action == \"workflow-profile\")"),
"The workflow card cannot bring the target application forward before learning its input target");
assert(hostBuild.includes('"%~dp0scripts\\features\\WorkflowCards.cs"'),
  "The Host build does not compile the workflow-card composition");
assert(hostBuild.includes("scripts\\features\\PackagedAppIdentity.cs"),
  "The Host build does not compile the packaged-application identity helper");
// A packaged application is launched through its AppUserModelID, which is neither a file
// on disk nor a process name. Deriving the process name from the id made every Store
// application invisible to Process.GetProcessesByName, so it was started again on every
// summon, and File.Exists refused to start it at all because a shell parsing path is not
// a file.
assert(includesAll(packagedAppIdentity, [
  "GetApplicationUserModelId", "IsStoreLaunchTarget", "AumidFromLaunchTarget",
  "RunningPackagedProcesses", "ResolveProcessName", "WaitForProcessName", "AppsFolderPrefix",
]), "The packaged-application identity helper lost its AppUserModelID resolution");
assert(includesAll(installedAppCatalog, [
  "PackagedAppIdentity.RunningPackagedProcesses()", "ResolveStoreProcessName",
  "PackagedAppIdentity.IsStoreLaunchTarget(launchTarget)",
]), "The application catalogue still treats a package key as a process name, or loads no icon for a Store entry");
// The catalogue used to be enumerated once per start-menu root, which re-walked the whole
// AppsFolder and appended the diagnostic twice.
assert(!/for \(string root in roots\)[\s\S]{0,300}CollectStoreApps/.test(installedAppCatalog),
  "The Store application enumeration runs once per start-menu root again");
assert(includesAll(app, [
  "PackagedAppIdentity.IsStoreLaunchTarget(launchTarget)", "ResolvePackagedProcessName",
  "PackagedAppIdentity.IsStoreLaunchTarget(launchPath)", "PeekFavoriteLaunchTarget",
  "FAVORITE LEARN packaged=true", "FAVORITE APP AUTOSTART reused=true",
]), "Adding, summoning or starting a packaged application no longer resolves its real process name");
// The home page entry to the 工作流 page has to be reachable without scrolling: at its old
// position it sat 156px below the fold of a default window. UI Automation cannot check
// this, because the scrolling panel reports children below the fold as on-screen, so the
// geometry is pinned against the content viewport height in the host self-test.
assert(includesAll(app, [
  "HomeWorkflowEntryTop", "HomeWorkflowEntryHeight", "RunHomeLayoutSelfTests",
  "The 工作流 entry on the home page is below the fold of the content viewport",
]) && /RunFavoriteAppSelfTests\(\);[\s\S]{0,120}RunHomeLayoutSelfTests\(\);/.test(app),
  "The home page entry to the 工作流 page is no longer pinned above the fold");
// The theme choice is a segmented control, not three commands. The current choice used to be a solid accent button
// — identical to the button you would press to make it the current choice — so it read as "press me" while it was
// already selected. Selection is carried by a tinted fill, an accent border, an accent label and a filled dot; the
// alternatives stay plain with a hollow dot. The labels were read back from the running page (● 白天模式, ○ 夜间模式,
// ○ 跟随 Windows) and the row measures with no clipped text at 880x500 and at 1280x1400.
assert(includesAll(app, [
  'Action<Button, bool, string> styleThemeSegment = delegate(Button segment, bool selected, string label)',
  'segment.Text = (selected ? "●  " : "○  ") + label;',
  'segment.BackColor = selected',
  "Color.FromArgb(238, 235, 255)",
  "segment.FlatAppearance.BorderColor = selected ? accent : line;",
  'styleThemeSegment(lightTheme, lightSelected, "白天模式");',
  'styleThemeSegment(systemTheme, systemSelected, "跟随 Windows");',
]), "The theme choice looks like three commands again, or lost its selected state");// The setup wizard had no way out for anyone without the hardware at hand: 完成本步，继续 refuses to advance until
// the step's evidence exists (a real remote direction key, or CABLE Input/Output plus the RC003 microphone), so the
// only exit was the window's close box. 稍后再说 closes the wizard and leaves the progress where it is, so it reopens
// on the same task — that is the reminder, and no step is ever marked complete by leaving.
//
// wizard.Close() and not Close(): the handler is a closure inside a method of the host form, so an unqualified
// Close() closes the main window and takes the application with it. Driving the button is what caught it.
assert(includesAll(app, [
  'var setupLater = SecondaryButton("稍后再说", new Point(430, 42), new Size(112, 42));',
  'setupLater.Name = "setupWizardLaterButton";',
  'HostLog("ONBOARDING later=true step=" + currentStep);',
  "wizard.Close();",
  "// On the last task 稍后再说 would only mean closing the window, so it is not offered there.",
  "setupLater.Visible = currentStep < OnboardingStepCount - 1;",
]), "The setup wizard has no non-destructive exit again, or its exit closes the main window");// The interface uses English product terms, and until now none of them was explained anywhere in the application.
// Each first occurrence carries a Chinese gloss and the settings page carries the glossary, so a new user can look
// them up in one place instead of hunting through six pages.
assert(includesAll(app, [
  "快捷键 Profile（应用专属的按键方案）",
  "配置浏览器遥控（Browser Remote Lite）",
  "是否启用 Smart Profiles（按应用自动切换键位方案 · 可选，默认关闭）",
  'SectionTitle("术语表"',
  '"快捷键 Profile", "某个应用专属的一套按键动作。绑定后，切到该应用会自动使用它。"',
  '"Context Deck", "遥控器、Profile、目标与最近一次动作的详细面板，从托盘菜单打开。"',
]), "A product term is used in the interface without a Chinese gloss, or the glossary is gone");// The workflow page has one card, not two. 常用应用 and 应用工作流 answered the same question in two stacked cards,
// so the user had to read both and work out which list was theirs. The favourites (the user's own list, with their
// open / edit / delete / make-current actions) come first and the applications a key Profile binds that still need a
// workflow follow inside the same card, in the one-line-per-application form that was chosen over listing everything.
assert(includesAll(app, [
  "private int BuildFavoriteAppsCard(Control page, int y, IList<WorkflowCard> pending, string summaryText)",
  "private void AddWorkflowStatusSection(Control card, IList<WorkflowCard> pending, string summaryText, int top)",
  "if (sectionHeight > 0) AddWorkflowStatusSection(card, pending, summaryText, 18 + panelHeight + 10);",
  "return panelHeight + 36 + sectionHeight;",
]) && !app.includes("BuildWorkflowStatusCard") &&
  // The favourites panel keeps every action it had: none of them may be dropped by the merge.
  includesAll(app, [
    "delegate(string process) { OpenFavoriteApp(process); }",
    "delegate(string process) { ConfirmRemoveFavorite(process); }",
    "delegate(string process) { ShowFavoriteAppEditor(process); }",
    "delegate(string process) { MakeFavoriteCurrent(process); }",
    "delegate { BeginFavoriteAppLearning(); }",
  ]),
  "The workflow page splits applications into two competing cards again, or the merge dropped a favourites action");// The shortcut page's profile row held seven equal-weight buttons, so 切换 (routine) and 删除 (destructive) looked
// the same. The five management actions moved into a 管理 menu that calls the same named methods the buttons called,
// which was verified by driving it: opening the menu and pressing Down twice then Enter opened the rename dialog.
// The menu is disposed as the page is rebuilt, because the page is rebuilt on every navigation.
assert(includesAll(app, [
  'profileMenuStrip = new ContextMenuStrip();',
  '"新建快捷键 Profile", null, delegate { CreateShortcutProfile(); }',
  '"重命名当前 Profile", null, delegate { RenameActiveShortcutProfile(); }',
  '"导入配置…", null, delegate { ImportShortcutProfile(); }',
  '"导出当前配置…", null, delegate { ExportActiveShortcutProfile(); }',
  '"删除当前 Profile…", null, delegate { DeleteActiveShortcutProfile(); }',
  'manageProfiles.Name = "profileManageButton";',
  "profileMenuStrip.Show(manageProfiles, new Point(0, manageProfiles.Height));",
  "if (profileMenuStrip != null)",
  "private ContextMenuStrip profileMenuStrip;",
]) && !app.includes("SecondaryButton(\"删除\", new Point(profileActionsStart + 174, 77)") &&
  !app.includes("SecondaryButton(\"新建\", new Point(profileActionsStart, 77)"),
  "The shortcut page's management actions are equal-weight buttons again instead of one 管理 menu");// P1 of the design review: one type scale, page titles that match the navigation, and one meaning per status
// colour.
//
// Fonts: the smallest UI text was 7.1-7.9 pt — roughly 10 px at 100% — which is below a comfortable reading size
// and got worse on a higher-resolution screen. Every 7.x label is now 8.0 pt; the only remaining 7.x is the
// decorative "xiaomi" mark drawn on the remote illustration, which is artwork rather than text.
// Titles: four of the six pages disagreed with the name the user clicked in the sidebar (按键/语音听写/一键自检/
// 偏好设置 against 快捷键/语音/自检/设置). The live pages and ExpectedPageTitle now agree.
// Colour: the wizard marked a completed step with an amber dot and an exclamation mark captioned 进度已保存，待复核,
// so a finished step looked like a problem. Completed steps are green with a tick and the caption 进度已保存, which
// still claims only that progress was saved; amber is left for what needs the user's attention.
assert(includesAll(app, [
  'AddPageTitle("快捷键", "管理遥控器实体键动作；录音键保持独立");',
  'AddPageTitle("语音", "遥控器负责收音；转写与整理能力由所选工具设置");',
  'AddPageTitle("自检", "逐项说明正确状态、当前状态、原因和修复入口");',
  'AddPageTitle("设置", "让言灵按你的习惯在后台运行");',
  '(verified || saved) ? "✓" : (i + 1).ToString()',
  'saved ? "\\r\\n进度已保存" : ""',
]) && includesAll(read("scripts/ui/PageShell.cs"), [
  'case VibePageId.Controls: return "快捷键";', 'case VibePageId.Voice: return "语音";',
  'case VibePageId.Diagnostics: return "自检";', 'case VibePageId.Settings: return "设置";',
]) &&
  // The literal, not the prose: the comment explaining this change quotes the old caption.
  !app.includes('"\\r\\n进度已保存，待复核"') &&
  !app.includes("7.9f") && !app.includes("7.8f") &&
  !app.includes("7.6f") && !app.includes("7.4f") && !app.includes("7.3f") && !app.includes("7.1f"),
  "A page title disagrees with the navigation, a status colour has two meanings, or 7.x pt UI text came back");// The settings page's key-source card used to assert device-level isolation unconditionally — a checked box
// reading "只有带 RC003 身份的事件可以执行遥控器动作" — while the badge and the note in the same card said the
// opposite ("言灵不会拦截来源未知的键"). The bold line and the checkbox state now follow the actual state, so the
// four parts of that card agree. A checked box promising what the same card denies is worse than saying nothing.
assert(includesAll(app, [
  '"尚未逐设备隔离：遥控器按键与实体键盘可能同时生效（签名通道为可选增强）"',
  '"设备级隔离已启用：只有带 RC003 身份的事件会执行遥控器动作"',
  "exactDeviceIsolation, new Point(32, 62));",
]) && !app.includes('"设备识别：只有带 RC003 身份的事件可以执行遥控器动作"'),
  "The key-source card promises device-level isolation regardless of the actual state again");// The workflow list names applications, so it has to name applications this machine has. It is composed from
// configured bindings, which outlive an uninstall and include the shipped defaults: measured on this machine,
// fourteen bindings resolved to four rows once the list was filtered. The catalogue behind the filter is cached
// because the 工作流 page is rebuilt on every navigation and the scan walks both start-menu roots and the shell
// AppsFolder; being merely *running* is not treated as installed, because that admitted msedge, cmd and powershell.
assert(includesAll(read("scripts/features/InstalledAppCatalog.cs"), [
  "internal static bool IsInstalled(string processName)",
  "private static void RebuildInstalledCache()",
  "installedCacheBuiltAt",
  "Being *running* is deliberately not part of this answer",
  "internal static bool IsSkipped(string text)",
  '"powershell", "pwsh"',
]) && includesAll(app, [
  "WorkflowAppExistsOnThisMachine",
  "InstalledAppCatalog.IsSkipped(processName)",
  "skipped_missing=",
  "InstalledAppCatalog.IsInstalled(name)",
  "A shell is offered as a place to dictate text into, or a real application is skipped",
]), "The workflow list can name applications that are not on this machine again");// The workflow cards belong to the 工作流 page and to nowhere else. They used to be rendered on the self-check
// page as well, where thirteen bound applications put thirteen four-line blocks — 需要配置 / 缺少工作流 /
// VF-WORKFLOW-* — above the system checks, on a page whose job is to say whether the components work. The
// self-check page now must not render them, and the 工作流 page must: one line per application that still needs
// something, the action taken from the card model, dispatched through the same handler.
assert(includesAll(app, [
  "BuildCurrentWorkflowCards", "WORKFLOW CARDS cards=",
  "WorkflowCards.Summarize(cards)", '"应用工作流"',
  // The section sits inside the workflow page's application card now, so the method that renders it changed name;
  // the contract did not: the rows exist only there, one line each, with the model's own action.
  "AddWorkflowStatusSection", "AddWorkflowStatusRow", "HandleSelfCheckAction(card.Action)",
  'else if (action == "workflow-profile")', 'else if (action == "workflow-target")',
]) && !app.includes("AddSelfCheckRow(workflows") &&
  !app.includes("WorkflowCards.Summarize(workflowCards)") &&
  !app.includes("BuildWorkflowCardItems("),
  "Workflow cards are no longer surfaced on the 工作流 page only, or came back to the self-check page");
assert(includesAll(read("scripts/features/WorkflowCards.cs"), [
  'case GapNoTarget: return "还没学习";',
  'case GapTargetUnverified: return "已学习未验证";',
]), "The workflow state labels read as faults again instead of as progress");
assert(includesAll(focusTargetService, [
  "SelectVoiceTarget", "FindTargetById",
]) && !/WindowTitle|BoundingRectangle|DocumentRange/.test(
  section(focusTargetService, "internal static FocusTargetDescriptor SelectVoiceTarget",
    "private static FocusTargetDescriptor FindTargetById")),
"The wake-path target selection is missing or reads window text instead of the stored descriptor");
assert(includesAll(app, [
  "VoiceWakeFocusTarget", "VOICE FOCUS TARGET source=", "foreground_process",
  "FocusTargetService.SelectVoiceTarget",
]) && app.indexOf("VoiceWakeFocusTarget(out targetSource)") > app.indexOf("private void ExecuteDefaultFocusTarget"),
"The Host wake path does not prefer the foreground application's own verified input target");
assert(includesAll(app, [
  "RunFavoriteAppSelfTests",
  "The explicitly selected favourite application was not used",
]), "The favourite-application model is not pinned by the host self-test");
// The redesigned Smart Focus promise: dictation goes to the current favourite
// application no matter where the cursor is, and a stopped favourite is started first.
assert(includesAll(favoriteAppStore, [
  "FavoriteApp", "FavoriteAppStore", "selectedProcess", "exePath", "targetId",
  "SyncFromTargets", "SelectedProcess", "Find",
]) && !/Clipboard\.|SendKeys\.|keybd_event|SendInput/.test(favoriteAppStore),
"The favourite-application store is missing or reads input text");
assert(hostBuild.includes('"%~dp0scripts\\features\\FavoriteAppStore.cs"'),
  "The Host build does not compile the favourite-application store");
assert(includesAll(app, [
  "favoriteAppStore", "SyncFavoriteApps", "VOICE FOCUS TARGET source=", "favorite_app",
  "EnsureFavoriteAppRunning", "FAVORITE APP AUTOSTART",
]) && app.indexOf('source = "favorite_app";') > app.indexOf("private FocusTargetDescriptor VoiceWakeFocusTarget"),
"The wake path does not target the current favourite application or start it when stopped");
// The redesigned entry point must be click-only: the application list is the machine's
// own inventory and no field is ever typed.
assert(includesAll(appPickerDialog, [
  "InstalledAppChoice", "确定", "SelectedProcessName", "SelectedLaunchTarget", "DisplayName",
]) , "The application picker lost its local inventory or its confirm action");
assert((appPickerDialog.match(/new TextBox\(\)/g) || []).length === 1 &&
  appPickerDialog.indexOf("filter.TextChanged") > 0 &&
  appPickerDialog.indexOf("ApplyFilter") > 0,
"The picker must keep exactly one text field, and it may only be the list filter");
assert(hostBuild.includes('"%~dp0scripts\\ui\\AppPickerDialog.cs"'),
  "The Host build does not compile the application picker");
assert(includesAll(app, [
  "BeginFavoriteAppLearning", "CaptureFavoriteTarget", "SavePendingFavorite", "OpenFavoriteApp",
  "ActivateProcessWindow", "FAVORITE LEARN captured=true", "FAVORITE SAVE process=",
  "FAVORITE OPEN process=", "没有学到输入框，请重试", "学习成功，点「保存」完成",
  // Saving also makes the application current, so the acknowledgement says so; the old sentence claimed text would
  // go into the application's input box while that still needed a separate 设为当前 click. The decision and the
  // wording are pinned by the host self-test, whose failure message is asserted here too.
  "ShouldMakeCurrentAfterSave", "FavoriteSaveMessage(saved, madeCurrent, processName)",
  "return saved && !alreadyCurrent;",
  "已添加并设为当前：以后按住录音键，文字都会进入 ",
  '" madeCurrent=" + madeCurrent',
  "Saving an application's input box no longer decides whether to make it current",
  "The save acknowledgement no longer states what actually happened",
]) && app.indexOf("BeginFavoriteAppLearning();") > app.indexOf("voiceFocusTargetButton.Click"),
"The voice page does not drive the learn / save / open flow");
assert(includesAll(app, ["SavePendingFavorite", "FAVORITE SAVE blocked=true reason=no_successful_learning"]),
"Saving is not gated on a successful learning step");
assert(includesAll(app, [
  "BuildWorkflowPage", "BuildFavoriteAppsCard", "FavoriteAppsPanel",
  "SetCurrentOrLearnFavorite", "BeginFavoriteRelearn", "ConfirmRemoveFavorite",
]) && app.indexOf("BuildFavoriteAppsCard(content, 100, pending, WorkflowCards.Summarize(cards));") >
    app.indexOf("private void BuildWorkflowPage") &&
  app.indexOf("BuildFavoriteAppsCard(content, 100, pending, WorkflowCards.Summarize(cards));") <
    app.indexOf("private void BuildVoicePage()"),
"The workflow page does not own the consumer favourites card");
{
  const voiceLink = app.indexOf("ShowPage((int)VibePageId.Workflow)");
  assert(voiceLink < 0 || voiceLink < app.indexOf("private void BuildVoicePage()"),
    "The voice page still links to the workflow page: pages must stay single-purpose");
// The home page carries the brief entry (current application plus a way in), while the
// voice page stays single-purpose: this is the split the user asked for.
assert(includesAll(app, ["配置工作流", "FavoriteAppSummaryText()", "workflowEntry"]) &&
  app.indexOf("ShowPage((int)VibePageId.Workflow)") > 0 &&
  app.indexOf("ShowPage((int)VibePageId.Workflow)") < app.indexOf("private void BuildVoicePage()"),
"The home page does not carry the brief workflow entry");
}
assert(includesAll(read("scripts/ui/DesignTokens.cs"), ["Workflow = 1"]) &&
  includesAll(read("scripts/ui/PageShell.cs"), ["\"工作流\"", "VibePageId.Workflow", "BuildWorkflowPage();"]),
"The navigation does not expose the dedicated workflow page");
{
  const panelStart = app.indexOf("panel = FavoriteAppsPanel.Build") >= 0 ? 0 : 0;
  assert(includesAll(favoriteAppsPanel, [
  "FavoriteAppsPanel", "学习", "保存", "打开", "重新学习", "删除",
  "还没有常用应用", "点右上角「添加应用」", "✓  学习成功", "点「保存」完成设置", "BuildBanner",
]) && !/TextBox/.test(favoriteAppsPanel),
"The consumer favourites panel is missing its buttons, states, guided empty state, or gained a text field");
assert(favoriteAppsPanel.indexOf("onLearn") > 0 && favoriteAppsPanel.indexOf("onSave") > 0 &&
  favoriteAppsPanel.indexOf("onOpen") > 0 && favoriteAppsPanel.indexOf("onDelete") > 0,
"The consumer favourites panel does not expose learn / save / open / delete actions");
  assert(hostBuild.includes('"%~dp0scripts\\ui\\FavoriteAppsPanel.cs"'),
    "The Host build does not compile the consumer favourites panel");
  assert(favoriteAppsPanel.indexOf("onLearn") > 0 && favoriteAppsPanel.indexOf("onSave") > 0 &&
  favoriteAppsPanel.indexOf("onOpen") > 0 && favoriteAppsPanel.indexOf("onRelearn") > 0 &&
  favoriteAppsPanel.indexOf("onDelete") > 0,
"The consumer favourites panel does not expose learn / save / open / relearn / delete actions");
// The two modes the user asked for must both be reachable and honestly labelled, and the
// summon / forget paths must exist so "打开" and "删除" cannot silently regress.
assert(includesAll(favoriteAppsPanel, [
  "ShortcutModeLabel", "WorkflowModeLabel", "onToggleMode", "IsWorkflowMode",
]) && !/TextBox/.test(favoriteAppsPanel),
"The favourite rows lost the shortcut / workflow mode switch");
assert(favoriteAppsPanel.indexOf("BuildBanner") > 0 && favoriteAppsPanel.indexOf("onSave") > 0 &&
  favoriteAppsPanel.indexOf("正在识别") < 0,
"The success acknowledgement and the save action are not the same element");
assert(includesAll(app, [
  "ToggleFavoriteMode", "FAVORITE MODE process=", "FAVORITE MODE workflow_selected=true",
  "FAVORITE OPEN process=", "FAVORITE FORGET process=", "RemoveLearnedTargetForProcess",
  "已改为工作流", "已改为快捷键",
]), "The host does not implement the mode switch, summon logging or target forgetting");
// A saved entry has to be more than a name and a delete button: its row states where it stands, and the
// page itself can name it, re-mode it, test its learned input box and make it the app that receives text.
assert(includesAll(favoriteAppStatus, [
  "FavoriteAppState", "NotLearned", "LearnedUnverified", "Verified", "TargetMissing",
  "CanBecomeCurrent", "DescribeForRow", "ModeTooltip", "IsWorkflowMode", "WorkflowMode", "ShortcutMode",
]), "The favourite-app state model or the mode semantics are missing");
assert(includesAll(favoriteAppsPanel, [
  "FavoriteAppStatus.DescribeForRow", "设为当前", "编辑", "FavoriteAppStatus.ModeTooltip", "stateOf",
]), "The favourite rows cannot state their status or offer the set-current / edit actions");
assert(includesAll(app, [
  "FavoriteStateOf", "MakeFavoriteCurrent", "ShowFavoriteAppEditor", "FavoriteTargetDetailText",
  "FAVORITE CURRENT process=", "FAVORITE EDIT process=", "FAVORITE TEST process=",
  "focusTargetService.ExecuteForVerification",
]), "The host cannot make a favourite current, edit it, or test its learned input box");
assert(hostBuild.includes('"%~dp0scripts\\features\\FavoriteAppStatus.cs"'),
  "The host build does not compile the favourite-app state model");
// The mode has to mean the runtime contract: a shortcut entry never takes the text, so it cannot stay the
// current app after being switched back.
assert(app.includes("dropped_current=") && app.includes("FavoriteAppStatus.ModeValue(toWorkflow)"),
  "Switching to shortcut mode can leave the text routed to an app that no longer claims it");
// The summon must retry while a freshly started application brings its input box up, and
// the retry must be pinned: an edit that silently drops it left the focus step failing.
assert(includesAll(app, [
  "FocusLearnedFavoriteTarget(app, 4000)", "while (waited.ElapsedMilliseconds < timeoutMs)",
]) && app.indexOf("while (waited.ElapsedMilliseconds < timeoutMs)") >
  app.indexOf("private bool FocusLearnedFavoriteTarget("),
"The summon does not retry focusing the learned input box");
assert(includesAll(app, [
  "IsFocusInProcess", "|| IsFocusInProcess(processName)", "AutomationElement.FocusedElement",
]), "The summon does not verify that focus ended up inside the target application");
assert(includesAll(favoriteAppStore, ["public string mode { get; set; }", "workflow"]),
"The favourite model does not persist the mode");
}
assert(includesAll(app, [
  "FocusTargetDescriptor.NormalizeProcessName(item.ProcessName)",
  "string.IsNullOrWhiteSpace(loaded.Document.DefaultTargetId)",
]), "Learning again for the same application must replace its own target instead of adding duplicates");
assert(includesAll(app, [
  "RunWorkflowCardsSelfTests", "A workflow card without an input target was not reported truthfully",
  "The workflow summary does not match the composed cards", "ShortGapLabel",
]), "The workflow-card composition is not pinned by the host self-test");
// The environment checks now start at the top of the self-check page: the workflow section that used to sit
// above them is gone from this page, so nothing shifts them and the count gate for that list stays exact.
assert(includesAll(app, [
  "int checksY = 302;", "int checksHeight = 66 + report.Items.Count * 112;",
  "int diagnosticsY = checksY + checksHeight;",
]) && !app.includes("workflowHeight"),
  "The self-check page still reserves space for the workflow section, or lost the checks it reports");
assert(hostBuild.includes('"%~dp0scripts\\features\\AudioEndpointService.cs"'),
  "The Host build does not compile the audio-endpoint capability");
assert(includesAll(app, [
  "CheckVirtualCableCaptureShapeAsync", "ApplyVirtualCableCaptureShape",
  "AUDIO ENDPOINT CHECK", "AUDIO ENDPOINT SHAPE",
  "repair-cable-shape", "restore-cable-shape",
  "只改设备类别，不改音频链路、音量或音质",
  "AudioEndpointService.TryReadVirtualCableShape",
]) && app.indexOf("CheckVirtualCableCaptureShapeAsync();") > app.indexOf("WarmConfiguredProviderAsync(false);"),
"The Host does not check the virtual-cable endpoint class at startup or expose the reversible self-check repair");
// The endpoint property store is documented as read-only for applications, and the
// virtual cable's recording endpoint disappeared on at least one machine while an
// automatic repair existed. The write therefore stays user-initiated, warned and
// verified, and must never run from the startup path.
assert(!/EnsureVirtualCableCaptureShapeAsync|TrySetFormFactor\(shape\.EndpointId, target/.test(
  app.substring(app.indexOf("private void OnShown"), app.indexOf("private ActionResult ApplyVirtualCableCaptureShape"))),
"The startup path still writes endpoint properties automatically");
assert(includesAll(app, [
  "MessageBoxIcon.Warning", "实验性的系统设备属性写入",
  "ENDPOINT-SHAPE-CANCELED", "ENDPOINT-SHAPE-UNVERIFIED", "verified=\" + verifiedOk",
]), "The endpoint-class repair is not confirmed, verified, or reversible");
// A machine without VB-CABLE cannot run the frozen recording kernel at all
// ("Install VB-CABLE first"), so the Host must degrade into a disclosed
// trigger-only mode instead of launching a capture that can only fail: the bridge
// keeps the remote keys working, the computer microphone stays the audio source,
// and the mode is reported instead of silently pretending to record.
assert(includesAll(app, [
  "ShouldUseTriggerOnlyVoiceMode", "TriggerOnlyModeSupportsProvider",
  "TriggerOnlyVoiceModeOption", "IsTriggerOnlyVoiceMode()",
  "CAPTURE START skipped=true reason=trigger_only_no_virtual_cable",
  "VOICE WAKE mode=trigger_only", "VOICE MODE trigger_only=true",
  "免驱动模式",
]) && app.indexOf("if (IsTriggerOnlyVoiceMode())") < app.indexOf("if (TryAttachExistingCapture())"),
"A missing VB-CABLE still launches the doomed capture instead of the disclosed trigger-only mode");
assert(includesAll(app, [
  "ONBOARDING trigger_only=true stage=audio step=1", "firstTriggerOnlyBaseline",
  "遥控器按键已唤起语音工具", "无法被免驱动模式唤起",
  "VoiceModeNote", "安装 VB-CABLE 可切换到完整模式",
]), "First-run setup still blocks without VB-CABLE instead of offering the disclosed trigger-only path");
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
  "internal sealed partial class VibeMicForm : Form",
  'string preference = config == null ? "light"', 'preference == "dark"',
  "pageBackground = Color.FromArgb(25, 26, 31)", "cardBackground = Color.FromArgb(35, 37, 44)",
  'SecondaryButton("白天模式"', 'SecondaryButton("夜间模式"', 'SecondaryButton("跟随 Windows"',
  'ApplyThemePreference("light")', 'ApplyThemePreference("dark")', 'ApplyThemePreference("system")',
  "SystemEvents.UserPreferenceChanged", "OnUserPreferenceChanged", "RebuildShellForTheme",
  "TryEnableHighDpi", "SetProcessDpiAwarenessContext", "AutoScaleMode = AutoScaleMode.Dpi",
  "ClampWindowToWorkingArea", "UI DPI awareness=per_monitor_v2",
  "RemoteVisual", "DrawRecordingRipples",
]), "Navigation, default light theme, restrained dark theme, or recording feedback is incomplete");
assert(includesAll(uiDesignTokens, [
  "internal enum VibePageId", "Home = 0", "Controls = 2",
  "Voice = 3", "Diagnostics = 4", "Settings = 5", "Notes = 6", "PageCount = 6",
  "NavigationButtonHeight", "ContentMinimumWidth",
]) && !uiDesignTokens.includes("Projects = 1") && !uiDesignTokens.includes("QuickEntries = 7"),
"The V2 UI tokens do not reserve stable removed-page IDs while bounding the five-page navigation");
assert(includesAll(read("scripts/ui/PageShell.cs"), [
  "首页", "工作流", "快捷键", "语音", "自检", "设置",
  "VibePageId.Workflow", "BuildWorkflowPage();",
]) && includesAll(read("scripts/ui/DesignTokens.cs"), ["Workflow = 1"]),
"The shell must expose 首页/工作流/快捷键/语音/自检/设置 with the workflow page wired");
assert(includesAll(uiComponents, [
  "CreateEmptyStateCard", "AccessibleName", "AutoEllipsis",
]), "Reusable UI components do not expose an accessible empty state");
assert(includesAll(projectSpaceModels, [
  "internal sealed class ProjectSpace", "RuntimeFocusTarget", "RuntimeFocusTarget.Copy()",
  "ProjectProfileSnapshot", "RuntimeProfile", "RuntimeProfile.Copy()",
  "TryNormalizeWorkspacePath", "TryNormalizeEditorExecutable", "TryNormalizePreviewUrl",
  "TryNormalizeHttpsUrl", "localhost", "127.0.0.1", "IPAddress.IPv6Loopback",
]), "Project Spaces do not preserve a deep execution snapshot or enforce resource allowlists");
assert(includesAll(projectSpaceModels + projectSpaceStore, [
  'CurrentSchemaVersion = 1', '"project-spaces.json"', 'storePath + ".bak"',
  "WriteTextAtomically", "File.Replace", "PROJECT-STORE-CORRUPT", "PROJECT-SCHEMA-NEWER",
  "UnknownFields", "WasMigrated", "RecoveredFromBackup",
]), "Project Spaces do not use independent versioned atomic storage with recovery");
assert(includesAll(projectSpaceRunner, [
  "OpenOrActivateApp", "OpenWorkspaceWithVerifiedAdapter", "OpenUrl", "SwitchProfile",
  "FocusTarget", "ShowNotification", "Interlocked.CompareExchange", "source.Copy()",
  "CancelCurrent", "CancelForRecording", "PROJECT-CANCELED-VOICE", "PROJECT-RUN-BUSY",
  "ProjectRunReservation", "TryReserve", "RunReserved", "PublishReport",
  "PROJECT-TIMEOUT", "if (!result.IsSuccess) return Report",
  "TryCreateStartInfo", "UseShellExecute = false", "QuoteSingleArgument",
]), "Project Space runner lacks its constrained linear, snapshot, single-run, or cancellation contract");
const projectSpaceProduction = projectSpaceModels + projectSpaceStore + projectSpaceRunner;
assert(!/\b(cmd\.exe|powershell\.exe|pwsh\.exe|command\.com)\b/i.test(projectSpaceProduction) &&
  !/public\s+string\s+(Command|Arguments|Script|Environment)/.test(projectSpaceProduction) &&
  !/VibeMicAtvvCapture|VoxDeckInputBridge|SetWindowsHookEx|RegisterRawInputDevices/.test(projectSpaceProduction),
  "Project Spaces can execute commands/scripts or depend on a frozen input/recording component");
assert(includesAll(app, [
  "InitializeProjectSpaces", "ReloadProjectSpaceDocument", "ExecuteProjectProcessRequest",
  "ExecuteProjectProfileStep", "ExecuteProjectFocusStep", "CompleteProjectSpaceRun",
  "TryActivateProjectApplication", "ProjectStepDisplayName", "CancelProjectSpaceForRecording",
]), "The retained Project Space backend is not fully owned by the Host after the Quick Entries UI removal");
assert(includesAll(app, [
  "RunProjectSpaceStoreSelfTests", "RunProjectSpaceRunnerSelfTests",
  "RunProjectSpaceProcessSelfTests", "RunProjectProfileGatewaySelfTests",
  "PROJECT-PROFILE-ACK-PENDING", "CancelProjectSpaceForRecording",
  "CancelV2ExternalActionsForRecording",
]), "Host self-tests or recording-priority/Profile-ACK integration are incomplete");
assert(includesAll(captureAskModels + captureAskService + captureAskWindows, [
  "CaptureAskPreparedImage", "CreateImageCopy", "ICaptureAskImageClipboard",
  "TrySetImage", "PasteToTarget", "TryValidateForExecution", "CAPTURE-ASK-FOCUS-LOST",
  "CancelForRecording", "CAPTURE-ASK-CANCELED-VOICE", "Interlocked.CompareExchange",
  "CaptureAskTargetResolver", "CAPTURE-ASK-PROJECT-TARGET-MISSING",
  "Clipboard.SetImage", "IsFocusedTarget", "HasKeyboardFocus", "TryPasteImage",
]), "Capture & Ask lacks immutable image, verified focus, single-run, or recording-priority boundaries");
const captureAskProduction = captureAskModels + captureAskService + captureAskWindows +
  captureAskForm + captureAskIntegration;
assert(!/Clipboard\.(Get|GetText|SetText|ContainsText)/.test(captureAskProduction) &&
  !/VibeMicAtvvCapture|VoxDeckInputBridge|SetWindowsHookEx|RegisterRawInputDevices/.test(captureAskProduction) &&
  !/SendKeys\.SendWait\s*\(\s*"\{ENTER\}"/.test(captureAskProduction),
  "Capture & Ask can read transcription text, auto-send Enter, or enter a frozen input path");
assert(includesAll(captureAskForm + captureAskIntegration + app, [
  'Name = "captureAskForm"', 'Name = "captureCurrentWindowButton"',
  'Name = "captureRegionButton"', 'Name = "captureAskPreview"',
  'Name = "captureAskTarget"', 'Name = "captureAskCopyButton"',
  'Name = "captureAskPasteButton"', 'Name = "captureAskRetakeButton"',
  'Name = "captureAskCancelButton"',
  'Name = "captureAskTrayMenuItem"', "CancelCaptureAskServiceForRecording",
  'Path.Combine(userStateRoot, "capture-ask-temp")',
]), "Capture & Ask is missing a complete production UI, entry point, or cleanup integration");
const browserRemoteProduction = browserProfileTemplate + browserProfileUndoStore +
  browserRemoteTestService + browserRemoteLiteForm + browserRemoteLiteIntegration;
const browserRemoteExecutable = browserProfileTemplate + browserProfileUndoStore +
  browserRemoteTestService + browserRemoteLiteIntegration;
assert(includesAll(browserProfileTemplate, [
  'ProfileId = "browser-ai"', '"上键", "pageup"', '"下键", "pagedown"',
  '"左键", "browserback"', '"确认键", "enter"', 'IsTestableAction',
]), "Browser Remote Lite does not expose its fixed browser-ai recommendation allowlist");
assert(!/VibeMicAtvvCapture|VoxDeckInputBridge|RegisterRawInputDevices|SetWindowsHookEx|录音键/.test(
  browserRemoteExecutable) && !browserProfileTemplate.includes('"Home"') &&
  !browserProfileTemplate.includes('"TV"'),
  "Browser Remote Lite enters a frozen recording/input path or manages Home/TV");
assert(includesAll(browserProfileTemplate + browserProfileUndoStore + browserRemoteLiteIntegration, [
  "File.Replace", "BackupPath", "TryRestore", "SaveConfig(out revision)",
  "BrowserRemoteBridgeAcknowledged(uiSmokeMode", "StartKeyboardBridgeForRevision(revision,",
  "BrowserRemoteRecordingHasPriority);",
  "BROWSER-PROFILE-CANCELED-VOICE", "BROWSER-PROFILE-ACK-PENDING", "BROWSER-UNDO-CONFLICT",
]), "Browser Remote Lite lacks atomic undo, exact restore, or Bridge acknowledgement");
assert(includesAll(browserRemoteTestService + browserRemoteLiteIntegration + bridge, [
  'value == "chrome"', 'value == "edge"', "ActivateAndVerify", "GetForegroundWindow",
  "GetWindowThreadProcessId", "BrowserRemoteRecordingHasPriority", "PrepareMappingActionTest",
  "BrowserRemoteDispatchTarget", "expected_process_id", "expected_window_handle",
  "expected_process_name", "ValidateBrowserRemoteDispatchNow", "TryClaim",
  "TryExecuteClaimed", "TryCancel", "TryWriteAtomic", "BROWSER-TEST-REQUEST-EXPIRED",
  "lock (voiceTransitionLock)",
  "动作已派发，请在浏览器中目视确认页面变化", "BROWSER-TEST-CANCELED-VOICE",
]), "Browser Remote Lite testing lacks verified foreground, recording priority, or honest receipt text");
assert(includesAll(browserRemoteLiteForm + app, [
  'Name = "browserRemoteLiteForm"', '"browserRemoteDiff"',
  '"browserRemoteRightChoice"', '"browserRemoteFunctionLongChoice"',
  '"browserRemoteBrowserChoice"', '"browserRemoteTestButton"',
  '"browserRemoteApplyButton"', '"browserRemoteUndoButton"',
  '"browserRemoteCloseButton"', '"browserRemoteLiteButton"',
  'AutoScaleMode = AutoScaleMode.Dpi', 'AutoScroll = true', '录音键、Home、TV',
]), "Browser Remote Lite is missing difference, explicit apply/undo, per-action test, or DPI UI");
assert(!app.includes("当前页面不会启动应用") &&
  controlsPage.includes('AddPageTitle("快捷键"'),
  "The five-page shell exposes internal safety wording or an inconsistent Controls title");
assert(includesAll(hostBuild, [
  '"%~dp0scripts\\features\\ActionResult.cs"',
  '"%~dp0scripts\\ui\\LiveHudForm.cs"',
  '"%~dp0scripts\\ui\\ContextDeckForm.cs"',
  '"%~dp0scripts\\features\\ProjectSpaceModels.cs"',
  '"%~dp0scripts\\features\\ProjectSpaceStore.cs"',
  '"%~dp0scripts\\features\\ProjectSpaceRunner.cs"',
  '"%~dp0scripts\\features\\CaptureAskModels.cs"',
  '"%~dp0scripts\\features\\CaptureAskService.cs"',
  '"%~dp0scripts\\features\\CaptureAskWindows.cs"',
  '"%~dp0scripts\\ui\\CaptureAskForm.cs"',
  '"%~dp0scripts\\ui\\CaptureAskIntegration.cs"',
  '"%~dp0scripts\\features\\BrowserProfileTemplate.cs"',
  '"%~dp0scripts\\features\\BrowserProfileUndoStore.cs"',
  '"%~dp0scripts\\features\\BrowserRemoteTestService.cs"',
  '"%~dp0scripts\\ui\\BrowserRemoteLiteForm.cs"',
  '"%~dp0scripts\\ui\\BrowserRemoteLiteIntegration.cs"',
]), "The Host build does not compile the feedback and Project Spaces surfaces");
assert(includesAll(actionResult, [
  "Idle", "Checking", "Running", "Success", "Warning", "Error", "Canceled",
  "VibeUiStatusSnapshot", "SanitizeOverlayText",
]), "The unified feedback model does not expose all seven states or a safe overlay snapshot");
assert(includesAll(liveHudForm, [
  "ShowWithoutActivation", "WS_EX_NOACTIVATE", "TopMost = true", "ShowInTaskbar = false",
]), "Live HUD does not declare the non-activating topmost window contract");
assert(!includesAll(liveHudForm + contextDeckForm, ["SendKeys", "SendWait"]) &&
  !/RegisterRawInput|SetWindowsHookEx|StartKeyboardBridge|ToggleCapture/.test(liveHudForm + contextDeckForm),
  "HUD or Context Deck can execute input, hooks, or bridge operations");
// The floating HUD and the in-window card report the same states, so their accents, glyphs,
// type steps, radii and lifetimes come from one place. Both used to carry a private copy of
// the same values: the colours happened to agree, but nothing kept them agreeing, and the
// lifetimes had already drifted apart (the HUD kept cancelled results on screen for twelve
// seconds while the card dropped them after 2.8).
assert(includesAll(uiDesignTokens, [
  "StatusAccent", "StatusGlyph", "InlineStatusGlyph", "StateForKind", "DurationForState",
  "FeedbackInfoDurationMs", "FeedbackProblemDurationMs",
  "FeedbackRadiusCompact", "FeedbackRadiusPanel", "FeedbackFontFamily",
]), "The feedback surfaces no longer share one set of design tokens");
assert(!/Color\.FromArgb\(10, 164, 104\)/.test(liveHudForm) &&
  includesAll(liveHudForm, ["UiDesignTokens.StatusAccent", "UiDesignTokens.StatusGlyph"]),
  "The Live HUD carries its own status palette again instead of the shared tokens");
assert(includesAll(app, [
  "UiDesignTokens.StatusAccent(toastState)", "UiDesignTokens.InlineStatusGlyph(toastState)",
  "UiDesignTokens.StateForKind(kind)", "UiDesignTokens.FeedbackRadiusCompact",
]) && !app.includes('kind == "error" ? "\\uEA39"') && !app.includes('kind == "error" ? coral'),
  "The in-window card carries its own status palette again instead of the shared tokens");
// One message, one outlet. The HUD exists so state stays visible when the app window is not,
// so an action raised while the window is in front must not also raise the always-on-top HUD
// — which, with the window in the bottom-right corner, covered the card underneath it.
assert(includesAll(app, [
  "ShouldPresentLiveHud", "ShouldPresentInlineToast", "WindowCountsAsInFront",
  "IsMainWindowForegroundAndVisible", "PublishFeedbackSnapshotInternal",
  "ShouldPresentLiveHud(liveHudExplicitlyRequested, windowInFront)",
  "PublishFeedbackSnapshotInternal(ShouldPresentLiveHud(",
  "if (!ShouldPresentInlineToast(liveHudExplicitlyRequested, windowInFront)) return;",
  "RunFeedbackOutletSelfTests",
]) && liveHudForm.includes("liveHudExplicitlyRequested = true;"),
  "An action message can still leave through both feedback surfaces at once");
// A message the floating panel carries must always be bounded. ActionResult.FromLegacyFeedback
// classifies any「正在…」text as Checking, so letting the live-state rule (Running/Checking never
// auto-hide) govern messages pinned an always-on-top bar in the bottom-right corner after an
// ordinary informational toast — which is exactly what made the panel read as a permanent dock.
// The live-state rule itself is untouched and stays pinned by LiveHudUiTests.
assert(includesAll(liveHudForm, [
  "FeedbackMessageLifetimeMilliseconds", "carryMessage", "UiDesignTokens.FeedbackProblemDurationMs",
]) && includesAll(app, [
  "FeedbackMessageLifetimeMilliseconds(false, ActionState.Checking)",
  "A checking or running action message can pin the always-on-top panel on screen forever",
  "PublishFeedbackSnapshotInternal(ShouldPresentLiveHud(liveHudExplicitlyRequested, windowInFront), true)",
]) && /if \(state == ActionState\.Running \|\| state == ActionState\.Checking\) return 0;/.test(liveHudForm),
  "An action message can still pin the always-on-top feedback panel open forever");
assert(includesAll(app, [
  "FlowLayoutPanel", "ShowPage(int index)", "Math.Min(UiDesignTokens.PageCount - 1, index)",
  "int page = NavigationPageIds[i % NavigationPageIds.Length]", "ShowPage(page)",
  "navButtons.Count != NavigationText.Length", "content.Controls.Count == 0", "ExpectedPageTitle(page)",
]), "The UI resource test does not construct and validate all visible pages with the five-item navigation");
assert(includesAll(hostBuild, [
  '"%~dp0scripts\\VibeMic.cs"', '"%~dp0scripts\\ui\\DesignTokens.cs"',
  '"%~dp0scripts\\ui\\UiComponents.cs"', '"%~dp0scripts\\ui\\PageShell.cs"',
]) && !hostBuild.includes("ProjectsPage.cs") &&
  !hostBuild.includes("QuickEntriesPage.cs") &&
  !hostBuild.includes("QuickEntryDialog.cs") &&
  !hostBuild.includes("QuickEntriesIntegration.cs") &&
  !hostBuild.includes("QuickEntryHotkeys.cs") &&
  !hostBuild.includes("ProjectSpaceWizard.cs") &&
  !hostBuild.includes('scripts\\features\\NotesStore.cs') &&
  !hostBuild.includes('scripts\\ui\\NotesPage.cs') &&
  !hostBuild.includes('scripts\\ui\\NotesDeckForm.cs'),
  "The Host build still exposes the removed Quick Entries or Notes feature");
assert(includesAll(screenshotScript, [
  "$controlsLabel", 'File = "03-shortcuts.png"',
]), "Screenshot automation cannot navigate the five-page V2 shell");
assert(!screenshotScript.includes("$notesLabel") && !screenshotScript.includes('"02-notes.png"'),
  "Screenshot automation still exposes the removed Notes page");
assert(includesAll(screenshotScript, [
  "CaptureFullOnboarding", "Wait-ForChildText", '"00-setup-01-device.png"', '"00-setup-05-ready.png"',
  '"03-shortcuts-screenshot.png"', 'ValidateSet("Current", "Light", "Dark", "System")',
  "Wait-ForProcessWindow", '"07-shortcut-actions.png"', '"08-shortcut-recorder.png"',
  '"09-smart-profile-apps.png"',
]), "Screenshot automation is not aligned with the five-task setup");

// Release, installer, CI, signing, and isolated hardware candidate safety.
assert(includesAll(release, [
  'Copy-Item (Join-Path $root "vibe-mic-config.default.json") $packageDir',
  'tools\\naudio.core.2.2.1\\lib\\netstandard2.0\\NAudio.Core.dll',
  'tools\\naudio.wasapi.2.2.1\\lib\\netstandard2.0\\NAudio.Wasapi.dll',
  'Copy-Item -LiteralPath $naudioCorePath -Destination $packageDir',
  'Copy-Item -LiteralPath $naudioWasapiPath -Destination $packageDir',
  'if ($releaseVersion -ne "2.0.0")',
  'Copy-Item (Join-Path $root "docs\\V1_5_USER_GUIDE_ZH.md") $packageDocs',
  '"V2_0_USER_GUIDE_ZH.md"', '"V2_0_CONFIGURATION_MIGRATION_ZH.md"',
  '"V2_0_AUTOMATED_TEST_REPORT_ZH.md"', '"V2_0_HARDWARE_TEST_MATRIX_ZH.md"',
  '"V2_0_KNOWN_LIMITATIONS_ZH.md"', '"V2_0_ROLLBACK_ZH.md"',
  '"V2_0_RELEASE_NOTES_ZH.md"', '"V2_0_INSTALLER_GUIDE_ZH.md"',
  'Copy-Item (Join-Path $root "docs\\VERSION_ARCHIVE_ZH.md") $packageDocs',
  'Copy-Item (Join-Path $root "docs\\FEATURES_ZH.md") $packageDocs',
  '"07-shortcut-actions.png"', '"08-shortcut-recorder.png"', '"09-smart-profile-apps.png"',
  '"vibe-flow-community.png"',
  '"VibeFlow-Setup.exe"', '"SHA256SUMS.txt"', "Invoke-VibeFlowCodeSign",
  'scripts\\Get-StableCaptureBinary.ps1', 'VibeFlow-StableCapture-v1.2.1.exe',
  'tools\\VBCABLE_Driver_Pack45.zip',
  'b950e39f01af1d04ea623c8f6d8eb9b6ea5c477c637295fabf20631c85116bfb',
  '@("VibeMic.exe", "--self-test")', '@("VoxDeckInputBridge.exe", "--self-test")',
  '& $stableCapturePath --self-test',
  'Test-ReleaseIdentity.ps1', 'Test-ReleaseArtifacts.ps1',
]), "Release packaging, checksums, or signing are incomplete");
// The self-check's one-click "disable USB selective suspend" runs this script from the
// install directory, where it is the measured fix for the remote's Bluetooth audio gaps.
// It is a runtime script, not a build helper, so both payload builders have to carry it and
// it has to survive cloning — an ignored script passes every local check and then makes the
// action fail on a real install with "缺少 USB 电源脚本".
assert(includesAll(release, [
  'Copy-Item (Join-Path $root "scripts\\Set-UsbSelectiveSuspend.ps1") (Join-Path $packageDir "scripts")',
]) && includesAll(candidateBuild, [
  'Copy-Item (Join-Path $root "scripts\\Set-UsbSelectiveSuspend.ps1") $candidateScripts',
]) && gitignore.includes("!scripts/Set-UsbSelectiveSuspend.ps1"),
  "A runtime repair script reaches a user install without travelling in the payload or surviving a clone");
assert(!release.includes('BUILD_VIBE_MIC_CAPTURE.cmd') &&
  !release.includes('@("VibeMic.exe", "VibeMicAtvvCapture.exe", "VoxDeckInputBridge.exe")'),
  "The formal release can rebuild or re-sign the frozen capture binary");
assert(!release.includes("VibeFlowRc003Filter") &&
  !candidateBuild.includes("VibeFlowRc003Filter") &&
  !installer.includes("VibeFlowRc003Filter"),
  "An unsigned, unvalidated RC003 driver candidate can enter a user package");
assert(!release.includes('(Join-Path $packageDir "vibe-mic-config.json")'),
  "Release packaging can overwrite an existing user configuration");
assert(includesAll(releaseIdentityTest, [
  'expectedVersion = "2.0.0"', 'expectedFileVersion = "2.0.0.0"',
  'Capture source SHA-256', 'Capture binary SHA-256', '"1.2.1.0"',
]), "Release identity test does not protect V2 and the frozen Capture identity");
assert(includesAll(releaseArtifactsTest, [
  'RELEASE_BODY_v2.0.0.md', 'V2_0_USER_GUIDE_ZH.md', '02-dictation.png',
  'VibeFlow-Setup.exe', 'Vibe-Flow-Windows-x64.zip', 'SHA256SUMS.txt',
  'Expand-Archive', 'Root and packaged binary hashes differ', 'Packaged Capture identity changed',
]), "Release artifact test does not verify the candidate payload and ZIP identity");
assert(includesAll(candidateBuild, [
  'if ($version -ne "2.0.0")', '"hardware-candidate"', 'hardwareAcceptancePassed = $false',
  'recordingKernel = "v1.0.3"', '"CANDIDATE_MANIFEST.json"', '"SHA256SUMS.txt"',
  'B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683',
  'stableCaptureSha256 = $stableCaptureSha256',
  'Copy-Item -LiteralPath $stableCapturePath -Destination (Join-Path $candidateDir "VibeMicAtvvCapture.exe")',
  '"Programs\\Vibe Flow Remote"', '"docs\\V1_2_1_TUTORIAL_ZH.md"',
  '"docs\\V1_3_USER_GUIDE_ZH.md"',
  '"docs\\VERSION_ARCHIVE_ZH.md"', '"docs\\V1_3_PREVIEW_ZH.md"',
  '"docs\\V1_4_PREVIEW_ZH.md"',
  '"docs\\V1_5_PREVIEW_ZH.md"',
  '"docs\\V1_3_HARDWARE_ACCEPTANCE_ZH.md"',
  '"scripts\\Measure-HardwareAcceptance.ps1"',
  'configurationSchema = 32', 'bridgeConfigurationSchema = 7',
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
assert(includesAll(release, [
  'Prepare-DevelopmentRuntime.ps1', '-StableCapturePath $stableCapturePath',
  '-NAudioCorePath $naudioCorePath', '-NAudioWasapiPath $naudioWasapiPath',
]) && release.indexOf('Prepare-DevelopmentRuntime.ps1') < release.indexOf('@("VibeMic.exe", "--self-test")'),
  "Release self-test can depend on stale runtime files left in the repository root");
assert(includesAll(installer, [
  '#define MyAppVersion "2.0.0"', "VersionInfoVersion=2.0.0.0", "PrivilegesRequired=lowest", "WaitForVibeFlowExit",
  "DisableWelcomePage=no",
  "ConfigRequestsStartup", "RestoreConfiguredStartupRegistration", "vibe-mic-config.json.bak",
  "MigrateLegacyUserConfig", "Vibe Flow Remote\\UserData", "UserConfigPath",
  'Filename: "{app}\\docs\\V2_0_USER_GUIDE_ZH.md"', "InitializeWizard", "GetWindowsVersionEx",
  "CreateOutputMsgMemoPage", "CurInstallProgressChanged", "不保存普通录音与转译文字",
  "启动并开始设置", "打开使用教程",
]), "Installer upgrades cannot safely preserve settings and startup state");
assert(includesAll(installer, [
  "TryReadConfigStartup", "if not MigrateLegacyUserConfig then",
  "if not RestoreConfiguredStartupRegistration then", "RaiseException",
  "RegValueExists", "ewWaitUntilTerminated",
]), "Installer migration or startup restoration can report completion after an unchecked failure");
assert(includesAll(installer, [
  "TryReadConfigStartupFromPath", "--installer-config-startup-query", "Exec(",
  "UserConfigPath + '.bak'",
  "LegacyConfigRoot + '\\vibe-mic-config.json'",
  "LegacyConfigRoot + '\\vibe-mic-config.json.bak'",
]), "Installer does not fall back through central and legacy configuration backups");
assert(includesAll(installer, [
  "MigrateLegacyUserConfig", "--installer-config-migrate", "ewWaitUntilTerminated",
]), "Installer does not delegate configuration recovery to the Host parser");
assert(includesAll(installer, [
  "InstallLocation", "PreviousInstallDirectory", "LegacyConfigRoot",
]) && section(installer, "function MigrateLegacyUserConfig", "function TryReadConfigStartupFromPath")
  .includes("LegacyConfigRoot"),
"Installer cannot migrate configuration when an upgrade changes the installation directory");
assert(includesAll(installer, [
  "function WaitForVibeFlowExit: Boolean", "if not WaitForVibeFlowExit then",
]) && section(installer, "function PrepareToInstall", "function UserDataDirectory")
  .includes("Result := '"),
"Installer can continue after the running Host failed to exit cooperatively");
assert(includesAll(installer, [
  "KeepUserDataOnUninstall", "function InitializeUninstall(): Boolean",
  "not KeepUserDataOnUninstall", "DelTree(UserDataDirectory",
]), "Uninstaller does not offer an explicit, default-safe user-data choice");
assert(includesAll(installer, [
  'Filename: "{app}\\docs\\V2_0_USER_GUIDE_ZH.md"',
]) && !installer.includes("blob/main/docs/V2_0_USER_GUIDE_ZH.md"),
"Installer tutorial actions can drift from the installed candidate documentation");
assert(section(installer, "[Tasks]", "[InstallDelete]").includes('Name: "installvbcable"') &&
  includesAll(section(installer, "[Tasks]", "[InstallDelete]"), [
    "VB-CABLE 虚拟音频线", "VB-Audio 捐赠软件", "checkedonce",
  ]) &&
  includesAll(section(installer, "[Run]", "[UninstallDelete]"), [
    'Install-VBCable.ps1"" -Install',
    "Tasks: installvbcable", "skipifsilent",
  ]),
"Installer does not offer the bundled VB-CABLE driver as a default-checked, silent-safe post-install task");
assert(includesAll(app, [
  "InstallerConfigStartupQueryArgument", "QueryConfigStartupForInstaller",
  "InstallerConfigMigrationArgument", "MigrateLegacyUserConfigForInstaller",
  "IsCompleteUserConfigDocument", "DeserializeObject", 'document.ContainsKey("launchAtStartup")',
  'document.TryGetValue("setupCompleted"', 'document.TryGetValue("resumeSetupAfterRestart"',
  "(bool)raw || (!setupCompleted && resumeSetupAfterRestart)",
]), "Installer startup restoration does not use the Host JSON parser for the top-level setting");
const installerStartupReader = section(installer, "function TryReadConfigStartup(var RequestsStartup", "function ConfigRequestsStartup");
assert(installerStartupReader.indexOf("UserConfigPath") < installerStartupReader.indexOf("UserConfigPath + '.bak'") &&
  installerStartupReader.indexOf("UserConfigPath + '.bak'") < installerStartupReader.indexOf("LegacyConfigRoot + '\\vibe-mic-config.json'") &&
  installerStartupReader.indexOf("LegacyConfigRoot + '\\vibe-mic-config.json'") < installerStartupReader.indexOf("LegacyConfigRoot + '\\vibe-mic-config.json.bak'"),
  "Installer configuration recovery order is not central main, central backup, legacy main, legacy backup");
const uninstallLifecycle = installer.slice(installer.indexOf("procedure CurUninstallStepChanged"));
assert(includesAll(uninstallLifecycle, [
  "RegValueExists", "not RegDeleteValue", "RaiseException",
]), "Uninstaller can silently leave a stale Vibe Flow startup registration");
assert(includesAll(lifecycleTest, [
  "Get-ConfigContractProjection", "inputMethod", "inputMethodHotkey", "inputMethodTrigger",
  "providerStartupDelayMs", "autoRouteVirtualMicrophone", "inputRoutingMode", "mappingPreset",
  "mappings", "customButtons", "shortcutProfiles", "smartProfilesEnabled", "activeShortcutProfileId",
  "smartProfileFallbackId", "stableVoiceProfileVersion", "audioEndpointName",
  "soundFeedbackEnabled", "autoCheckUpdates", "setupCompleted", "onboardingVersion",
  "onboardingStep", "resumeSetupAfterRestart",
]), "Release lifecycle test does not verify preservation of the V1.5 user configuration contract");
assert(includesAll(app, [
  "TryReadFutureUserConfigSchema", "FutureConfigurationSchemaException",
  "configurationWritesBlocked", "CONFIG-SCHEMA-NEWER",
  "schemaVersion > ConfigSchemaVersion", "futureSetting", "preserve-me",
]) && includesAll(section(app, "private bool SaveConfig(out string bridgeRevision)",
  "private bool PersistedMappingMatches"), ["configurationWritesBlocked", "return false"]),
"A newer configuration schema can be silently downgraded or overwritten");
const shownRuntimeStart = section(app, "protected override void OnShown(EventArgs e)",
  "protected override void OnFormClosing(FormClosingEventArgs e)");
const captureRuntimeStart = section(app, "private void StartCapture()", "private bool TryAttachExistingCapture()");
const bridgeRuntimeStart = section(app,
  "private bool StartKeyboardBridgeForRevision(string requestedRevision,", "private bool WaitForBridgeConfigRevision");
assert(includesAll(shownRuntimeStart, ["ConfigurationAllowsRuntimeServices", "future_schema"]) &&
  (captureRuntimeStart.match(/ConfigurationAllowsRuntimeServices/g) || []).length >= 2 &&
  includesAll(bridgeRuntimeStart, ["ConfigurationAllowsRuntimeServices", "future_schema"]),
  "Future-schema read-only mode can still start Bridge or Capture with fallback defaults");
assert(includesAll(installer, ["MinVersion=10.0", "ArchitecturesAllowed=x64compatible"]),
  "Installer does not fail closed outside the supported Windows 10/11 x64-compatible boundary");
assert(includesAll(installerRequirementsTest, [
  "MinVersion", "6.1", "6.3", "Windows 10", "Windows 11", "x64compatible",
]), "Installer requirement test does not exercise supported and unsupported OS boundaries");
assert(includesAll(v2FeatureSuite, [
  "FocusTargetMultiWindowTests", "CaptureAskServiceTests", "CaptureAskUiTests",
  "BrowserRemoteLiteTests", "BrowserRemoteLiteUiTests", "LiveHudUiTests",
  "ShortcutActionTests",
  "FeatureSurfaceTests",
  "Invoke-TestProcess", "$LASTEXITCODE", "V2 focused test failed",
]), "V2 focused tests are not compiled and executed by a repeatable suite");
assert(includesAll(pageShell, ["NavigationPageIds"]) &&
  !pageShell.includes("BuildQuickEntriesPage()") && !pageShell.includes("BuildProjectsPage()") &&
  !pageShell.includes('case VibePageId.QuickEntries:') && !pageShell.includes('case VibePageId.Projects:') &&
  !pageShell.includes("BuildNotesPage()") && !pageShell.includes('case VibePageId.Notes:'),
"The removed Quick Entries or Notes page is still wired into the product shell");
assert(includesAll(uiDesignTokens, ["PageCount = 6", "Notes = 6"]) &&
  !uiDesignTokens.includes("Projects = 1") && !uiDesignTokens.includes("QuickEntries = 7"),
  "Page count does not bound the five-page navigation while reserving stable removed-page IDs");
assert(includesAll(pageShell, [
  'NavigationText = { "首页", "工作流", "快捷键", "语音", "自检", "设置" }',
  "NavigationPageIds",
]), "The main navigation does not expose the stable shortcut and voice pages");
assert(includesAll(app, ["navButtons.Count != NavigationText.Length", "NavigationText.Length != 6"]),
  "The UI smoke gate does not assert the five-item main navigation");
const overviewSection = section(app, "private void BuildOverview()", "private void UpdateOverviewStatus()");
assert(!overviewSection.includes('SecondaryButton("锁定目标"') &&
  !overviewSection.includes('SecondaryButton("输入目标"') &&
  !overviewSection.includes('SecondaryButton("截图提问"') &&
  !overviewSection.includes('SecondaryButton("显示 Live HUD"'),
  "The home page still exposes retired target/project/Capture & Ask controls");
assert(!overviewSection.includes("便签") && !overviewSection.includes("openNotesHomeButton") &&
  !app.includes("AddAiSettingsCard()") && !app.includes("RefreshNotesDeckTheme()") &&
  !hostBuild.includes("NotesDeckIntegration.cs"),
  "The removed Notes feature remains reachable from the Host surface");
assert(!app.includes("BuildQuickEntriesPage") && !app.includes("QuickEntryDialog") &&
  !app.includes("RegisterQuickEntryHotkeys") && !app.includes("ShowQuickEntryDialog") &&
  !app.includes("StartProjectSpaceRun") && !app.includes("SetProjectSpaceEnabled") &&
  !app.includes("DeleteProjectSpace") && !app.includes("ProjectSpaceWizard") &&
  !app.includes("APP 快捷入口") && !app.includes("绑定快捷入口"),
"The Host still exposes Quick Entries UI entry points or the removed APP-shortcut binding");
assert(release.includes("Test-V2FeatureSuite.ps1") && release.includes("Test-InstallerRequirements.ps1"),
  "Release build does not execute the V2 focused suite and installer requirement boundary test");
assert(workflow.includes("Test-V2FeatureSuite.ps1") && workflow.includes("Test-InstallerRequirements.ps1"),
  "CI does not explicitly execute the V2 focused suite and installer requirement boundary test");
const automaticUpdateSetting = section(app,
  'var automaticUpdates = StyledCheck("自动检查 GitHub 正式版更新',
  'var setup = PrimaryButton("重新打开首次设置"');
assert(includesAll(automaticUpdateSetting, ["ApplySettingsChangeCore", "config.autoCheckUpdates = previous"]),
  "Automatic update preference can diverge between memory and disk after a failed save");
const contextDeckOpening = section(liveHudForm, "private void ShowContextDeck()", "private void PublishFeedbackSnapshot()");
assert(includesAll(contextDeckOpening, ["IsVoiceKeyHeld()", "ContextDeckOpeningBlockedByRecording"]) &&
  (contextDeckOpening.match(/ContextDeckOpeningBlockedByRecording/g) || []).length >= 2,
  "Context Deck is not guarded before and immediately before activation when the voice key is held");
const activeOnboardingCompletion = section(app,
  "PrepareOnboardingCompletionPending(config, setupWasCompleted);",
  "wizard.FormClosing += delegate(object sender, FormClosingEventArgs e)");
assert(includesAll(activeOnboardingCompletion, [
  "initialStartupApplied", "ReconcileLaunchAtStartupRegistration()", "ApplySettingsChangeCore",
  "SETTINGS-SYSTEM-APPLY-FAILED",
]), "Active onboarding can report completion without startup registration readback and rollback");
assert(includesAll(installerMigrationTest, [
  "customButtons", "legacy custom-button action", "legacy-home",
]), "Installer migration test does not preserve the legacy custom-button data needed by Host migration");
assert(includesAll(app, [
  "legacyCustomButtonConfig", 'legacyCustomButtonConfig.mappings["Home:short"]',
  "Legacy custom-button migration did not preserve the configured Home action",
]), "Host self-test does not verify legacy custom-button conversion into a supported mapping");
assert(includesAll(lifecycleTest, [
  "Assert-DisposableAccount", "Uninstall\\{99C65880-071A-4F75-9238-FA4E92A2E76D}_is1",
  'Get-ItemPropertyValue -Name "Vibe Flow"', "requires a disposable Windows account",
]), "Release lifecycle test can overwrite a real per-user installation or startup entry");
assert(includesAll(app, [
  "PrepareOnboardingCompletionPending", "config.setupCompleted = setupWasCompleted",
  "config.resumeSetupAfterRestart = !setupWasCompleted", "BeginVoiceHotkeyTest",
  "CancelVoiceHotkeyTest", "VOICE-HOTKEY-TEST-CANCELED",
  'PrimaryButton("重新打开首次设置"',
]), "Onboarding completion or voice-hotkey testing is not lifecycle-safe");
assert(includesAll(app, ["ShowPage(PageHome)", '"打开首页"']) &&
  !activeOnboardingCompletion.includes("ShowPage((int)VibePageId.Notes)"),
"Onboarding still points to the removed Notes page");
assert(includesAll(hardwareAcceptanceTool, [
  'Join-Path $env:LOCALAPPDATA "Vibe Flow Remote\\UserData"',
  'Join-Path $userStateRoot "remote-voice-session"',
]), "Hardware acceptance still reads user state from a version-specific executable directory");
assert(includesAll(workflow, [
  "actions/checkout@v7", "actions/setup-node@v7", "actions/upload-artifact@v7",
  "WINDOWS_SIGNING_PFX_BASE64", "WINDOWS_SIGNING_PFX_PASSWORD", "Vibe-Flow-Windows-v2-candidate",
]), "GitHub Actions validation, artifact upload, or signing secrets are incomplete");
assert(includesAll(workflow, [
  "VibeFlow-Setup-v1.5.0.exe", "releases/download/v1.5.0/VibeFlow-Setup.exe",
  "releases/download/v1.5.0/SHA256SUMS.txt", "Get-FileHash", "expectedPreviousHash",
]) && !workflow.includes("VibeFlow-Setup-v1.2.1.exe"),
"CI upgrade coverage does not use and verify the actual V1.5 installer");

// Current documentation must describe the V2 candidate while preserving the V1.5 release and voice boundary.
const currentUserDocs = {
  readme, englishReadme, guide, versionArchive, quickStart, releaseNotes, githubReleaseBody,
  v2Guide, v2Migration, v2AutomatedTests, v2HardwareMatrix, v2KnownLimitations, v2Rollback,
  v2ReleaseNotes, v2InstallerGuide,
};
for (const [name, document] of Object.entries(currentUserDocs)) {
  assert(document.includes("2.0") || document.includes("V2.0") || document.includes("V2 "),
    `${name} does not identify V2`);
  assert(!document.includes("单击录音键开始") && !document.includes("再次单击结束"),
    `${name} still instructs users to use click-toggle recording`);
}
assert(!readme.includes("松开完成转译"),
  "README still reports recording completion as transcription completion");
for (const [name, document] of Object.entries({ readme, v15Guide, v2Guide, quickStart, releaseNotes, githubReleaseBody })) {
  assert(document.includes("按住") && document.includes("松开"), `${name} does not explain hold-to-talk`);
  assert(document.includes("60 秒") || document.includes("60-second"),
    `${name} does not explain the current RC003 session limit`);
}
for (const [name, document] of Object.entries({ readme, v15Guide, v2Guide, quickStart, releaseNotes, githubReleaseBody })) {
  assert(document.includes("开机、返回和独立音量键") &&
    (document.includes("不提供") || document.includes("没有稳定")),
    `${name} does not explain unsupported RC003 controls`);
}
assert(includesAll(readme, [
  "docs/V2_0_USER_GUIDE_ZH.md", "docs/V1_5_USER_GUIDE_ZH.md", "docs/images/01-overview.png", "VibeFlow-Setup.exe",
  "Source code (zip/tar.gz)", "vibe-flow-community.png", "docs/VERSION_ARCHIVE_ZH.md",
  "releases/download/v1.5.0", "V2.0.0", "candidate",
  "docs/images/07-shortcut-actions.png", "docs/images/09-smart-profile-apps.png",
]), "README lacks the installer, tutorial, screenshot, or community entry points");
assert(includesAll(guide, [
  "V2_0_USER_GUIDE_ZH.md", "V1_5_USER_GUIDE_ZH.md", "vibe-flow-community.png",
  "V2_0_RELEASE_NOTES_ZH.md", "VERSION_ARCHIVE_ZH.md",
]), "The current guide index lacks tutorial, community, release notes, or version links");
assert(includesAll(v2Guide, [
  "首页", "快捷键", "Smart Focus", "自检", "不自动发送", "约 60 秒", "VOICE-RUNTIME-INCOMPLETE",
]), "The V2 guide lacks the current navigation, frozen boundary, or recovery guidance");
assert(includesAll(v2Migration, [
  "schema 继续为 `32`", "Bridge schema 继续为 `7`", "focus-targets.json",
  "quick-entries.json", "原子", ".bak", "未知字段", "Bridge ACK", "一次性 Windows 账户",
]), "The V2 migration guide lacks compatibility, recovery, or acknowledgement details");
assert(includesAll(v2AutomatedTests, [
  "Test-V2FeatureSuite.ps1", "Test-ReleaseIdentity.ps1", "Test-ReleaseArtifacts.ps1",
  "736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2",
  "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683",
]), "The automated test report lacks executable evidence or frozen hashes");
assert(includesAll(v2HardwareMatrix, [
  "100 次录音循环", "10 / 30 / 接近 60 秒音频", "Windows 10 x64 / Windows 11 x64",
  "125% / 150% / 200% DPI", "全新安装 / V1.5 升级 / 二次升级 / 卸载", "未验证",
]), "The V2 hardware matrix hides required manual checks");
assert(includesAll(v2KnownLimitations, [
  "未配置商业 Authenticode 签名", "普通键盘 F5", "Power、Back 和独立音量键",
  "不读取网页正文", "不自动发送 AI 消息",
]), "The V2 known-limitations document omits material candidate boundaries");
assert(includesAll(v2Rollback, [
  "回到 V1.5.0", "focus-targets.json", "project-spaces.json", "一键撤销",
  "不要通过改 Capture 哈希",
]), "The V2 rollback guide cannot restore the stable feature-disabled path");
assert(includesAll(v2ReleaseNotes, [
  "Release status: candidate", "首页", "快捷键", "Smart Focus",
  "1.2.1.0", "未验证",
]), "The V2 release notes omit candidate status, features, or frozen identity");
assert(includesAll(v2InstallerGuide, [
  "Windows 10 / 11 x64", "SHA256SUMS.txt", "五任务向导", "中央主配置",
  "V1.5 升级", "一次性 Windows 账户",
]), "The V2 installer guide omits requirements, migration, or lifecycle status");
assert(includesAll(v15Guide, [
  "CABLE Input", "CABLE Output", "Typeless", "豆包输入法", "Windows 语音输入",
  "5 项任务", "10 项", "现象 | 先检查 | 处理方式", "vibe-flow-community.png",
  "录制键盘快捷键", "Smart Profiles", "images/07-shortcut-actions.png",
  "images/08-shortcut-recorder.png", "images/09-smart-profile-apps.png",
]), "The V1.5 illustrated guide lacks providers, onboarding, Profiles, troubleshooting, or community help");
assert(includesAll(versionTutorial, [
  "用户友好稳定版", "images/vibe-flow-community.png", "11 步", "10 项",
  "CABLE Input", "CABLE Output", "系统 · 区域截图", "Win + Shift + S",
  "images/03-shortcuts-screenshot.png", "VERSION_ARCHIVE_ZH.md",
]), "The V1.2.1 illustrated tutorial is incomplete");
for (const releaseVersion of ["v1.5.0", "v1.2.1", "v1.2.0", "v1.1.0", "v1.0.3", "v1.0.2", "v1.0.1", "v1.0.0"]) {
  const base = `https://github.com/richlearntodo-debug/vibe-flow/releases/download/${releaseVersion}/`;
  for (const asset of ["VibeFlow-Setup.exe", "Vibe-Flow-Windows-x64.zip", "SHA256SUMS.txt"])
    assert(versionArchive.includes(base + asset), `Version archive is missing ${releaseVersion}/${asset}`);
}
assert(includesAll(versionArchive, [
  "V1.4.0", "不完整预览版，仅归档", "Vibe-Flow-v1.4.0-Incomplete-Preview.zip",
  "SHA256SUMS-v1.4.0.txt", "不提供",
]), "Version archive does not distinguish the incomplete V1.4 archive");
assert(includesAll(versionDoc, [
  "2.0.0", "Product version: `2.0.0`", "Configuration schema: `32`", "Bridge configuration schema: `7`",
  "Stable Capture file version: `1.2.1.0`", "Recording kernel: `v1.0.3`",
  "unsigned candidate", "V1.5.0 remains the recommended public release",
]), "Version metadata documentation is stale");
assert(includesAll(previewGuide, [
  "Raw Input 安全直通", "普通键盘", "打开 HTTPS 网页", "100 次录音按下与松开",
  "不替代已经发布且稳定的 `v1.2.1`", "input-bridge-log.txt", "Home 短按和长按",
  "开机、返回和独立音量加减", "不提供配置入口", "真实执行结果",
]), "V1.3 preview guide lacks source isolation, customization, or hardware acceptance guidance");
assert(includesAll(v14PreviewGuide, [
  "不完整预览归档", "普通用户请下载 V1.5", "不修改语音链路", "我的快捷键", "Vibe Coding",
  "浏览器 AI", "Terminal Agent", "最近一次快捷操作", "配置 revision",
  "选择本机应用", "Browser Back", "正在运行与已安装应用",
  "100 次录音按下/松开", "普通键盘冲突", "125%", "150%",
]), "V1.4 preview guide lacks frozen-baseline, Profile, receipt, or release-gate guidance");
assert(includesAll(v15PreviewGuide, [
  "发布验收记录", "推荐正式版", "直接录制键盘快捷键", "Smart Profiles", "默认关闭",
  "Ctrl + Win", "Ctrl + Alt + Delete", "250 ms", "350 ms", "锁定当前",
  "回退 Profile", "窗口标题", "100 次录音按下/松开", "125%", "150%",
  "正式发布", "VibeMicAtvvCapture.exe",
]), "V1.5 preview guide lacks shortcut recorder, Smart Profile, privacy, or release-gate guidance");
assert(includesAll(githubReleaseBody, [
  "2.0.0", "candidate", "VibeFlow-Setup.exe", "首页", "Smart Focus",
  "Live HUD", "Capture & Ask", "Browser Remote Lite", "不自动发送", "1.2.1.0",
]), "The V2 candidate release body is incomplete");
assert(includesAll(featuresBoard, [
  "功能看板", "语音输入", "语音工具适配", "快捷键录制", "应用控制",
  "Profiles", "Smart Profiles", "自检中心", "可配置按键", "稳定性边界",
  "Project Spaces", "Smart Focus", "Live HUD", "Capture & Ask", "Browser Remote Lite",
  "vibe-flow-community.png",
]), "The public feature board is incomplete");
assert(includesAll(v14GithubReleaseBody, [
  "V1.4.0 不完整预览归档", "普通用户请下载", "不提供安装版 EXE",
  "Vibe-Flow-v1.4.0-Incomplete-Preview.zip", "V1.5",
]), "The V1.4 archive release body does not prevent accidental installation");
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

console.log("Vibe Flow V2.0.0 candidate validation passed; Capture remains frozen at 1.2.1.0.");
