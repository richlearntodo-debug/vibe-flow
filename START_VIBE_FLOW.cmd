@echo off
cd /d "%~dp0"
if exist "%~dp0VibeFlow.exe" (
    start "" "%~dp0VibeFlow.exe"
) else if exist "%~dp0release\Vibe-Flow-Windows-x64\VibeFlow.exe" (
    start "" "%~dp0release\Vibe-Flow-Windows-x64\VibeFlow.exe"
) else if exist "%~dp0VibeMic.exe" (
    start "" "%~dp0VibeMic.exe"
) else (
    echo Vibe Flow is not built in this folder.
    echo Run BUILD_DEVELOPMENT.ps1 or BUILD_RELEASE.ps1 first, then try again.
    exit /b 1
)
