
@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ========================================
echo MeHere Receiver Build / Publish / Repack
echo ========================================

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build_MeHereReceiver.ps1"

echo.
if errorlevel 1 (
    echo BUILD FAILED
) else (
    echo BUILD FINISHED
)
echo.
pause
