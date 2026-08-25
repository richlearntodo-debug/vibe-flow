@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0tools\system.runtime.4.3.1\ref\net462\System.Runtime.dll" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RESTORE_BUILD_DEPS.ps1"
if errorlevel 1 exit /b 1
if not exist "%~dp0tools\microsoft.windows.sdk.contracts\ref\netstandard2.0\Windows.WinMD" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RESTORE_BUILD_DEPS.ps1"
if errorlevel 1 exit /b 1
if not exist "%~dp0tools\naudio.core.2.2.1\lib\netstandard2.0\NAudio.Core.dll" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RESTORE_BUILD_DEPS.ps1"
if errorlevel 1 exit /b 1
if not exist "%~dp0tools\naudio.wasapi.2.2.1\lib\netstandard2.0\NAudio.Wasapi.dll" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RESTORE_BUILD_DEPS.ps1"
if errorlevel 1 exit /b 1
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:anycpu /out:"%~dp0VibeMicAtvvCapture.exe" /reference:System.Runtime.WindowsRuntime.dll /reference:System.Runtime.InteropServices.WindowsRuntime.dll /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll" /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll" /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll" /reference:"%~dp0tools\system.runtime.4.3.1\ref\net462\System.Runtime.dll" /reference:"%~dp0tools\naudio.core.2.2.1\lib\netstandard2.0\NAudio.Core.dll" /reference:"%~dp0tools\naudio.wasapi.2.2.1\lib\netstandard2.0\NAudio.Wasapi.dll" /reference:"%~dp0tools\microsoft.windows.sdk.contracts\ref\netstandard2.0\Windows.WinMD" /reference:"%~dp0tools\microsoft.windows.sdk.contracts\ref\netstandard2.0\Windows.Foundation.FoundationContract.winmd" /reference:"%~dp0tools\microsoft.windows.sdk.contracts\ref\netstandard2.0\Windows.Foundation.UniversalApiContract.winmd" "%~dp0scripts\VibeMicAtvvCapture.cs"
if errorlevel 1 exit /b 1
copy /y "%~dp0tools\naudio.core.2.2.1\lib\netstandard2.0\NAudio.Core.dll" "%~dp0NAudio.Core.dll" >nul
copy /y "%~dp0tools\naudio.wasapi.2.2.1\lib\netstandard2.0\NAudio.Wasapi.dll" "%~dp0NAudio.Wasapi.dll" >nul
echo Built %~dp0VibeMicAtvvCapture.exe
