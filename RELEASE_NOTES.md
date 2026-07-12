# Smart Background Nap 0.1.0

Initial release by KaozyKing.

## Highlights

- Automatic scheduled background app optimizer for Windows.
- Designed for gaming, streaming, Discord, browsers, launchers, and multitasking.
- Keeps apps open instead of killing them.
- Protects existing tweak stacks like Process Lasso, ThrottleStop, MSI Afterburner, RTSS, and NVIDIA services.
- Includes a tray indicator with status, apply-now, logs, and folder shortcuts.
- Includes an auditable PowerShell core and a lightweight compiled C# WinForms tray app.

## Included

- `background-nap.ps1`
- `browser-nap.ps1`
- `manage-background-nap.ps1`
- `manage-background-nap-tray.ps1`
- `SmartBackgroundNapTray.exe`
- icon assets
- install/uninstall/status command shortcuts
- MIT license

## Known Notes

Smart Background Nap is conservative by design. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.
