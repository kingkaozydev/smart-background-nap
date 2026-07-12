@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0browser-nap.ps1" -Action Watch -WatchMinutes 90 -IntervalSeconds 30
pause
