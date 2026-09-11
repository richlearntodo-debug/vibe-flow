@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0CREATE_APP_ICON.ps1"
if errorlevel 1 exit /b 1
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:anycpu /win32icon:"%~dp0vibe-flow.ico" /out:"%~dp0VibeMic.exe" ^
 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll ^
 /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll" /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll" ^
 "%~dp0scripts\VibeMic.cs" "%~dp0scripts\features\ActionResult.cs" "%~dp0scripts\features\FocusTargetModels.cs" "%~dp0scripts\features\FocusTargetStore.cs" "%~dp0scripts\features\FocusTargetService.cs" ^
 "%~dp0scripts\features\ProjectSpaceModels.cs" "%~dp0scripts\features\ProjectSpaceStore.cs" "%~dp0scripts\features\ProjectSpaceRunner.cs" ^
 "%~dp0scripts\features\CaptureAskModels.cs" "%~dp0scripts\features\CaptureAskService.cs" "%~dp0scripts\features\CaptureAskWindows.cs" ^
 "%~dp0scripts\features\BrowserProfileTemplate.cs" "%~dp0scripts\features\BrowserProfileUndoStore.cs" "%~dp0scripts\features\BrowserRemoteTestService.cs" ^
 "%~dp0scripts\features\AudioEndpointService.cs" "%~dp0scripts\features\InputMethodDetector.cs" "%~dp0scripts\features\LinkQualityPolicy.cs" "%~dp0scripts\features\WorkflowCards.cs" "%~dp0scripts\features\LinkBaselineStore.cs" "%~dp0scripts\features\FavoriteAppStore.cs" "%~dp0scripts\features\InstalledAppCatalog.cs" "%~dp0scripts\features\PackagedAppIdentity.cs" "%~dp0scripts\features\GestureLayerPolicy.cs" "%~dp0scripts\features\GestureBindingStore.cs" "%~dp0scripts\features\GestureMacroRunner.cs" "%~dp0scripts\features\UsageStatsPolicy.cs" "%~dp0scripts\features\FavoriteAppStatus.cs" "%~dp0scripts\features\SnippetStore.cs" ^
 "%~dp0scripts\ui\DesignTokens.cs" "%~dp0scripts\ui\UiComponents.cs" "%~dp0scripts\ui\PageShell.cs" ^
 "%~dp0scripts\ui\LiveHudForm.cs" "%~dp0scripts\ui\ContextDeckForm.cs" ^
 "%~dp0scripts\ui\CaptureAskForm.cs" "%~dp0scripts\ui\CaptureAskIntegration.cs" "%~dp0scripts\ui\BrowserRemoteLiteForm.cs" "%~dp0scripts\ui\BrowserRemoteLiteIntegration.cs" "%~dp0scripts\ui\AppPickerDialog.cs" "%~dp0scripts\ui\FavoriteAppsPanel.cs"
if errorlevel 1 exit /b 1
echo Built %~dp0VibeMic.exe
