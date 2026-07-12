@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0manage-background-nap.ps1" -Action Install
pause
