@echo off
setlocal
cd /d "%~dp0"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /platform:anycpu /out:"%~dp0VoxDeckInputBridge.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "%~dp0scripts\VoxDeckInputBridge.cs" "%~dp0scripts\features\BrowserRemoteTestService.cs" "%~dp0scripts\features\BrowserProfileTemplate.cs" "%~dp0scripts\features\ActionResult.cs" "%~dp0scripts\features\GestureLayerPolicy.cs" "%~dp0scripts\features\GestureBindingStore.cs" "%~dp0scripts\features\GestureMacroRunner.cs"
if errorlevel 1 exit /b 1
echo Built %~dp0VoxDeckInputBridge.exe
