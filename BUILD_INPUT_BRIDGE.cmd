@echo off
setlocal
cd /d "%~dp0"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:anycpu /out:"%~dp0VoxDeckInputBridge.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "%~dp0scripts\VoxDeckInputBridge.cs"
if errorlevel 1 exit /b 1
echo Built %~dp0VoxDeckInputBridge.exe
