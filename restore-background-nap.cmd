@echo off
if exist "%~dp0bin\SmartBackgroundNap.exe" (
  "%~dp0bin\SmartBackgroundNap.exe" --restore
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0background-nap.ps1" -Action Restore
)
pause
