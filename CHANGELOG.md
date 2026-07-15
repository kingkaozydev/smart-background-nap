# Changelog
## 0.4.8 - 2026-07-15

- Fixed the update popup JavaScript so the automatic update toggle can switch off/on correctly.
- Made the update check action explicit and clickable from the popup.

## 0.4.7 - 2026-07-15

- Fixed the automatic update preference so the popup can switch between on/off immediately and persist correctly.
- Removed the PC Profile snapshot label from the launcher.
- Added the static-site package manifest required by the deployment platform.

## 0.4.6 - 2026-07-15

- Added an official GitHub Releases update checker inside the launcher with update, manual check, ignore-this-version controls, and a user-managed automatic-check preference.
- Added a public landing site that pulls the latest release and recent repository changes from GitHub automatically.
- Updated trust documentation so the network model is explicit: no telemetry and no user data uploads; update checks only read official GitHub release metadata.
- Fixed the first-run language selector labels so multilingual choices render cleanly.
## 0.4.5 - 2026-07-15

- Added Behavior Engine, a local per-app profiler that learns from aggregate app behavior instead of treating every child process as a separate app.
- Behavior profiles track CPU sample, memory footprint, private memory, handle/thread pressure, burst history, working-set trim result, refault after trim, and foreground wake events.
- Nap tier decisions can now use behavior confidence to soften apps that wake often or refault memory, and to deepen apps proven to be idle and efficient to trim.
- Fixed tray tooltip refresh so hovering/opening the tray menu requests fresh RAM/app/purge data instead of feeling frozen.
- Added Behavior Engine telemetry and badges to the WebView2 launcher.
- Refined the dashboard intelligence section so core behavior profiling and optional Smart Learning are presented clearly without crowding the control center.
- Reworked the WebView2 launcher frame with a dark custom window surface, reliable native drag handling, responsive layout fixes, and a clearer clickable Start with Windows control.

## 0.4.4 - 2026-07-15

- Fixed GPU VRAM detection by preferring driver-reported video memory when Windows exposes capped adapter-memory values.
- Fixed CPU frequency display: the dashboard now labels base clock correctly and calculates live effective clock from processor performance counters when available.
- Removed misleading frozen base-clock style output when Windows only exposes static CPU clock data.
- Fixed the native maximize button so the WebView2 launcher fills the Windows work area instead of opening in a half-sized capped window.
- GPU driver detail now shows vendor-friendly labels when safely inferable instead of exposing raw OS driver identifiers as the primary display value.

## 0.4.3 - 2026-07-15

- Added live CPU clock telemetry using native Windows power information so the dashboard no longer shows a frozen base frequency.
- Expanded the PC Profile panel with GPU VRAM, driver/display details, pagefile availability, memory load, and richer system memory summary.
- Added a polished GitHub social preview banner at `docs/images/smart-nap-social-preview.png` and updated the README hero image.
- Embedded the new social preview asset in the single EXE runtime so the local README view resolves correctly.

## 0.4.2 - 2026-07-15

- Added a dark native Windows frame for the WebView2 launcher so the title bar no longer appears as a bright white strip on dark systems.
- Added a PC Profile panel with CPU, RAM, GPU, OS, installed memory, free memory, module count, RAM speed, and module model when Windows exposes those details.
- Added a richer tray tooltip with free RAM, managed app count, and reclaimed memory from the last optimization pass.
- Cached hardware discovery so the launcher can show system specs without keeping a heavy monitor running in the background.
- Refined dashboard spacing for the new telemetry block while preserving native move/resize behavior.

## 0.4.1 - 2026-07-15

- Grouped Live Manager and Nap Score entries by app identity, so multi-process apps such as browsers appear once with an instance count instead of repeating every process.
- Updated apply summaries to count unique apps as `targets` while keeping the touched process count available as `processes`.
- Kept per-process actions under the hood, so the engine still tunes each child process safely.

## 0.4.0 - 2026-07-13

- Added Intent Engine telemetry for Desktop, Gaming, Media/Call, Download/Install, and Memory Pressure sessions.
- Added Foreground Switch Accelerator state to detect apps that are brought back often and protect fast-wake targets more intelligently.
- Added per-game profile state so gaming sessions can learn pressure patterns without using broad power-plan or driver tweaks.
- Added Contention Radar JSON output and dashboard telemetry for CPU, memory, burst, guard, and managed-process pressure.
- Added Media/Call Protection and Download/Launcher Guard to avoid false positives on active voice, media, launcher, and install/update workloads.
- Added Memory Pressure Governor 2.0 with Normal, Moderate, Elevated, and Critical thresholds.
- Added per-app policies from the Live Manager: Auto, Protect, Light, Balanced, and Deep.
- Added policy, role, guard, intent, and fast-wake badges to the WebView2 Live Manager.
- Versioned the extracted runtime folder so updates can use a fresh engine even if an older runtime folder has restrictive permissions.
- Fixed a PowerShell `$PID` collision in foreground switch tracking.
- Improved status output so protected/guarded apps are visible during diagnostics.

## 0.3.6 - 2026-07-13

- Rebuilt the GitHub README as a professional product overview with a clearer pitch, visual tour, trust model, and install flow.
- Replaced the old SVG preview images with polished PNG product artwork.
- Added a reproducible README image renderer under `tools/art`.
- Embedded the README artwork in the EXE runtime so the local README shortcut can resolve the new images.
- Updated SEO and repository metadata copy for the refreshed positioning.

## 0.3.5 - 2026-07-13

- Fixed Smart Learning persistence when the runtime config is refreshed without the learning key.
- Added a dedicated local Smart Learning preference file and automatic sync back into the nap engine config.
- Added a writable per-user config override so older runtime config files with restrictive permissions cannot disable Smart Learning.
- Added migration from the latest learning toggle event in the local log, so existing users keep their last choice after updating.
- The apply path now syncs Smart Learning before each manual or automatic optimization pass.

## 0.3.4 - 2026-07-13

- Added optional Smart Learning mode as an extra power toggle inside the launcher.
- Smart Learning builds local per-app profiles from memory pressure, CPU bursts, nap tier outcomes, and foreground wake events.
- Learned fast-wake apps stay lighter so frequent Alt+Tab targets can recover faster.
- Heavy idle background apps can receive stronger nap decisions when system memory pressure rises.
- Added an in-app explanation/confirmation panel before enabling Smart Learning.
- Added dashboard telemetry for learned profiles and current memory pressure.
- Added Permission Guard: the launcher lists apps that refused process changes and can request one UAC administrator pass for them.
- Config extraction now merges new default settings without overwriting existing user choices.

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
