@echo off
if exist "%~dp0bin\SmartBackgroundNap.exe" (
  "%~dp0bin\SmartBackgroundNap.exe" --uninstall-startup
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0manage-background-nap-tray.ps1" -Action Uninstall
)
pause
