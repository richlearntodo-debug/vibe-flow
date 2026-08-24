@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0CREATE_APP_ICON.ps1"
if errorlevel 1 exit /b 1
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:anycpu /win32icon:"%~dp0vibe-flow.ico" /out:"%~dp0VibeMic.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "%~dp0scripts\VibeMic.cs"
if errorlevel 1 exit /b 1
echo Built %~dp0VibeMic.exe
