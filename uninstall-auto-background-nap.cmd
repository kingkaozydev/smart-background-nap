@echo off
if exist "%~dp0bin\SmartBackgroundNap.exe" (
  "%~dp0bin\SmartBackgroundNap.exe" --uninstall-auto
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0manage-background-nap.ps1" -Action Uninstall
)
pause
