# Changelog

## 0.3.3 - 2026-07-13

- Migrated the main launcher to .NET 9 with a WebView2 dashboard.
- Added a modern embedded web UI with live manager, telemetry, event stream, responsive layout, and language picker.
- Added first-run and persistent UI language support for Portuguese BR, English, Russian, Spanish, French, and German.
- Added adaptive nap tiers: Light, Balanced, and Deep.
- Added foreground restore through a native fast path for priority, memory priority, I/O priority, and EcoQoS restore.
- Reduced foreground wake latency for quicker app switching.
- Added cooldown-aware working-set trimming to avoid repeatedly hammering the same process.
- Improved fullscreen-aware and burst-aware scoring.
- Released WebView2 resources when the dashboard is closed or minimized to tray so the background helper stays lighter during games.
- Updated the single-EXE build path to `net9.0-windows`.

## 0.1.3 - 2026-07-12

- Added Foreground Wake Restore from the tray when the active window changes.
- Added temporary protection for newly foreground and active high-CPU apps.
- Added fullscreen-aware thresholds for safer aggressive passes during games or fullscreen workloads.
- Added burst history for repeated background CPU spikes.
- Added Nap Score JSON reports and app menu access.
- Kept the watcher lightweight: no persistent PowerShell worker is left running.

## 0.1.2 - 2026-07-12

- Removed external social references from the app, docs, license, SEO copy, release material, and dashboard preview image.
- Added optional low process I/O priority for safe background apps to reduce disk contention during gaming and multitasking.
- Added I/O priority state capture and restore support.
- Updated security documentation and safety report language for the new process-level I/O setting.

## 0.1.1 - 2026-07-12

- Added product/version metadata and an `asInvoker` Windows manifest.
- Added an in-app safety report with executable SHA-256, runtime path, task status, and security posture.
- Added a public security model document for advanced audits.
- Automatic mode now uses a managed per-user startup copy for the single-EXE release.
- Expanded release notes around Windows trust, privacy, and local-only behavior.

## 0.1.0 - 2026-07-12

Initial public release.

- Added the all-in-one `SmartBackgroundNap.exe` dashboard.
- Added single-file release packaging with embedded runtime scripts, config, README text, and icon asset.
- Added toggle-based automatic mode and startup controls.
- Added optimize-now, restore, logs, config, folder, README, and GitHub actions.
- Added inline action progress and result feedback.
- Added single-instance behavior so opening the EXE brings up the existing tray app.
- Added built-in "start with Windows" tray startup control.
- Updated scheduled tasks to call the EXE directly.
- Moved logs and restore snapshots into the app folder for portable releases.
- Added Smart Background Nap automatic scheduled optimizer.
- Added safe background process classification.
- Added protected app, system, and game path rules.
- Added active high-CPU workload protection.
- Added low memory priority, below-normal process priority, EcoQoS, and working set trim support.
- Added tray indicator with compiled C# WinForms executable.
- Added icon assets.
- Added browser-only fallback mode.
- Added manual, automatic, watch, status, restore, install, and uninstall commands.
