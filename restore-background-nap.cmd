@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0background-nap.ps1" -Action Restore
pause
