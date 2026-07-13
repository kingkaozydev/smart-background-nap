# Smart Background Nap 0.1.3-test

Local test build for smart wake/protect behavior.

## Highlights

- Single-file release: download and run `SmartBackgroundNap.exe`.
- Runtime scripts, config, README text, and icon assets are embedded inside the EXE.
- Foreground Wake Restore from the tray.
- Temporary active-app protection.
- Fullscreen-aware thresholds.
- Burst watcher state.
- Nap Score report.
- New low process I/O priority pass for safe background apps.
- I/O priority snapshot/restore support.
- Product/version metadata and an `asInvoker` Windows manifest.
- Built-in safety report with executable SHA-256, runtime path, scheduled-task status, and local-only behavior summary.
- Public security model for advanced audits.
- Managed per-user startup copy under `%LOCALAPPDATA%\Programs\SmartBackgroundNap` when automatic startup is enabled.
- New all-in-one `SmartBackgroundNap.exe` dashboard.
- Toggle-based automatic mode and start-with-Windows tray controls.
- Inline action progress and result feedback.
- Single-instance behavior when opening the EXE while the tray is already running.
- Automatic background app optimization for Windows.
- Built for gaming, streaming, creative work, coding, and heavy multitasking.
- Keeps apps open instead of killing them.
- Lowers safe background CPU scheduling, memory priority, disk I/O priority, and EcoQoS pressure.
- Includes a tray indicator with status, apply-now, logs, and folder shortcuts.
- Includes an auditable PowerShell core and a lightweight compiled C# WinForms tray app.

## Included

- `SmartBackgroundNap.exe`
- MIT license

## Download Verification

SHA-256 for `SmartBackgroundNap.exe`:

```text
TEST_BUILD_LOCAL_ONLY
```

## Trust Notes

Smart Background Nap has no telemetry, no network calls, no driver install, no service install, no startup registry key, and no administrator elevation request.

Windows SmartScreen reputation depends on Authenticode signing and Microsoft reputation. This release includes cleaner executable metadata and an explicit least-privilege manifest, but an unsigned build can still show "Unknown Publisher" on some PCs.

## Notes

Smart Background Nap is conservative by design. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.
