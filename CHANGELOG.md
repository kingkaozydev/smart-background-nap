# Changelog

## 0.7.4 - 2026-08-01

- Added Memory Stability Guard in shadow mode through the Core Service snapshot, tracking physical RAM, commit headroom, pagefile headroom, Windows low-memory notification, top process consumers, and browser-burst signals without changing pagefile, trimming globally, or acting like a RAM cleaner.
- Added Commit Headroom Guard and Browser Burst Shield diagnostics so heavy game plus browser scenarios can be classified as memory-pressure risk before any future containment action is enabled.
- Exposed Memory Stability Guard as a contextual engine-activity insight only when relevant, keeping stable sessions quiet and showing honest diagnostic states instead of invented fixes.
- Hardened Core Service status reporting so read-only status checks, install verification, service install, and service start no longer overwrite the live service snapshot with process-local IPC state.
- Improved Core Service IPC resilience with concurrent subscribe handling, a bounded connection cap, and backpressure for busy pipe states.
- Tightened the Core Service named-pipe ACL by removing broad authenticated-user write access while keeping local administrators, LocalSystem, and the interactive user path functional.
- Added restore identity checks based on process start time so stale restore snapshots do not target a reused PID.
- Cleaned successful Session Agent install logging so localized Windows command output does not leak mojibake into normal operational logs.
- Stabilized Games library hover rendering so polling does not remount unchanged cards and hover remains CSS-only with pointer-safe poster overlays.

## 0.7.3 - 2026-08-01

- Refined the guided game preset modal responsiveness so wide layouts keep two columns with discreet internal sidebar scrolling, while vertical and narrow layouts switch to a compact game summary above the configuration flow.
- Separated the preset modal footer into summary, technical-details toggle, and action areas so the toggle stays compact, the primary action keeps hierarchy on vertical screens, and the adjustment list remains the only main scroll region.
- Polished the vertical preset modal with compact adjustment cards, one-column technical detail stacks, a real install/preset/backup preview in the game-info disclosure, and an applied-without-changes footer state that avoids a dominant disabled CTA.
- Added responsive guards for technical-details modal states across horizontal and vertical viewports to prevent footer clipping, oversized toggles, and squeezed two-column portrait layouts.

## 0.7.2 - 2026-07-30

- Evolved the Games tab into a professional optimization library with a compact functional header, real summary counts, search, supported filters, sorting, and persisted grid/list view preference.
- Reworked game cards around one contextual primary action, a secondary actions menu, clearer install/running/preset states, reduced cover dominance, and a compact list view for larger libraries.
- Stabilized Games grid and list density so filtered single-game results keep normal card dimensions, list actions stay compact, pending banners respect active filters, and Portuguese UI copy keeps accents.
- Added real preset-applied metadata to the launcher state from `game-presets.state.json`, including selected counts, backup count, last-applied timestamp, restored state, and platform hints derived from detected paths.
- Rebuilt the game preset dialog as a guided review flow with recommended, advanced, backups, and history tabs, plus a technical-details toggle and safer anti-cheat scope copy for VALORANT.
- Polished the game preset dialog with accented Portuguese copy, a Design System toggle for technical details, structured real-PC hardware summary, a neutral restore action, sticky decision footer, and a dynamic apply CTA that distinguishes applied, changed, updating, retry, and pending states without inventing recipe data.
- Fixed the game preset technical-details state so expanded optimization cards scroll inside the review area while the sidebar, toggle, summary, close button, and decision actions remain reachable.
- Kept existing folder selection, automatic scan, preset application, backup, restore, and game-mode confirmation flows intact while making the UI explain impact, risk, reversibility, and backup behavior more clearly.

## 0.7.1 - 2026-07-30

- Started the Core Service v1 foundation with explicit protocol version, pipe name, migration capability list, health classification, telemetry freshness, and a versioned shared snapshot for the launcher.
- Added the first Core Pipe v1 request/response IPC surface over a local Named Pipe with explicit ACL, message size limits, `hello`, `getCapabilities`, `getSnapshot`, `getState`, `getEvents`, `getDiagnostics`, `ping`, and a heartbeat-stream `subscribe` bootstrap.
- Added the first Session Agent v1 foundation with `--session-agent`, `--session-agent-once`, foreground/fullscreen/idle/live observation, a local session snapshot, and `publishSessionContext` / `getSessionContext` Core Pipe commands.
- Added a least-privileged `SmartBackgroundNapSessionAgent` per-user logon task so the interactive-session observer can run alongside the Core Service without moving desktop detection into Session 0.
- Exposed Core Service health to the launcher state and activity feed so stale telemetry, recovery kicks, stopped service, missing install, and attention states can be surfaced without treating the launcher as the engine authority.
- Hardened install/update setup so the installer verifies the Core Service, Session Agent, startup tasks, refreshed engine telemetry, and running service state before reporting setup as ready.
- Improved game executable path recovery for protected games whose process path is hidden, including Steam/Epic/library-root discovery, learned path persistence, and Zero Ping QoS recovery from `PathPending` to active protection.
- Polished the Dashboard with the approved realtime-control instrument, refined engine activity panel, expandable insights, cleaner motor-mode section, and corrected desktop/vertical layout flow.
- Kept the current service safely scoped as a watchdog/user-session bridge instead of moving foreground, fullscreen, OBS, game detection, or privileged optimization logic into Session 0.

## 0.7.0 - 2026-07-29

- Added Smart SNAP Core Service as a Windows watchdog/broker for the engine, keeping the user-session game detector in charge while the service restarts the scheduled engine task if telemetry goes stale.
- Added service install/start/stop/status command-line controls and a shared service state file for launcher visibility.
- Kept service behavior narrow and safe: no Session 0 foreground guessing, no shader/game file manipulation, and no direct replacement of Zero Ping or ShaderBoost game detection.
- Reworked launcher responsiveness across the WPF/WebView shell, Dashboard, Games library, live manager, cards, modals, portrait layouts, and DPI handling with automated responsive visual guards.

## 0.6.6 - 2026-07-29

- Added generic game executable detection for Zero Ping, ShaderBoost, CPU-bound assist, and GPU/VRAM optimization, so unknown games can be detected by foreground/fullscreen, UDP, CPU, executable path, install roots, and related process hints instead of relying only on preset game names.
- Added executable path fallback through `Win32_Process.ExecutablePath` plus cached PID lookup when `Process.Path` is unavailable.
- Expanded game executable resolution through saved user paths, related process roots, common store/library roots, and bounded `.exe` search while keeping launcher/service/helper exclusions.
- Kept EA launcher/local host/anti-cheat helper processes blocked from becoming the active game even when broad EA path fragments exist in user config.

## 0.6.5 - 2026-07-29

- Added VRAM Action Mode: during protected gameplay with VRAM pressure, background browsers and launcher helpers using GPU/VRAM can receive stronger temporary containment.
- Applied a more aggressive but guarded action path for eligible helpers: lower priority, lower memory/I/O priority, and tighter CPU affinity, while preserving the game, anti-cheat, foreground tree, streaming/audio/media, and protected apps.
- Added configurable thresholds for VRAM action containment so the mode can be tuned without changing code.
- Tightened game lock-on so EA launcher/local host/anti-cheat helper processes are not treated as the active game when the real game is closed.

## 0.6.4 - 2026-07-29

- Added a saved game path fallback for protected games whose executable path is hidden by anti-cheat, letting Zero Ping and ShaderBoost recover the trusted game executable from `game-paths.user.json`.
- Merged built-in game path fragments with configured fragments so store-specific roots such as Battlefield/EA folders remain recognized even when the config supplies its own list.
- Improved ShaderBoost game anchoring for protected games so `GameRoot`, game update detection, cache inventory, and Zero Ping QoS path handling can work from a validated local install path.

## 0.6.3 - 2026-07-29

- Added Frame Stability Mode for ShaderBoost sessions: when shader cache is healthy but VRAM/CPU pressure is hurting frametime, bursty background browsers can receive temporary CPU affinity containment.
- Kept the protection conservative: games, anti-cheat, foreground trees, protected apps, stream/audio/media, and Zero Ping protected processes are not affinity-limited by this mode.
- Added configurable frame-stability thresholds for browser affinity containment during gaming.

## 0.6.2 - 2026-07-29

- Added ShaderBoost Gameplay Light Scan so cache inventory uses a lighter scan and recent cached inventory while a game is active, reducing CPU/disk contention during gameplay.
- Added ShaderBoost frame stability telemetry to flag when FPS instability is more likely VRAM/CPU pressure than shader-cache health.
- Exposed ShaderBoost scan mode and frame-stability reason in dashboard telemetry.

## 0.6.1 - 2026-07-28

- Removed the active power plan summary card from the main dashboard card row so ShaderBoost, Zero Ping, and pass metrics realign without the extra empty row.
- Fixed ShaderBoost localization keys so the dashboard card and telemetry line render the proper ShaderBoost name instead of raw i18n keys.
- Fixed ShaderBoost game detection to reuse the Zero Ping game anchor even when anti-cheat or process permissions hide the executable path.
- Prepared this as the local 0.6.1 test build before publication.

## 0.6.0 - 2026-07-28

- Added the first ShaderBoost / Shader Optimization Engine foundation as a safe observe-first system for GPU/API/driver detection, shader cache inventory, cache health classification, readiness scoring, driver/game invalidation detection, and compiler-process protection.
- Added ShaderBoost dashboard telemetry and NapScore fields for state, readiness, API, GPU/vendor, cache manager, cache size, recommendation, warmup method, and possible shader compilation detection.
- Added regression guards to keep ShaderBoost from becoming a destructive cache cleaner: the coordinator and inventory are not allowed to delete, move, or clear cache data.
## 0.5.60 - 2026-07-28

- Restored the launcher to the approved 0.5.45 dashboard visual while keeping the 0.5.60 backend, telemetry, and feature set.
- Expanded the 0.5.45-style dashboard bindings for the 0.5.60 backend: Zero Ping confidence/shield/QoS detail, GPU/VRAM pressure, GPU optimization context, CPU-Bound Assist, Stream Guard, engine health, and rollback audit metadata.
- Cleaned the in-app About copy so product history/version details stay in the changelog instead of the About summary.
- Strengthened foreground restore so apps, games, development tools, media apps, and streaming tools recover priority, memory priority, I/O priority, EcoQoS state, and safe affinity state when they become important again.
- Added stronger process-state safeguards, rollback tracking, cooldowns, protected-process filtering, and regression checks so background tweaks do not remain stuck on foreground apps.
- Added CPU-Bound Assist for games and heavy workloads: the engine reduces safe background contention around the active workload instead of blindly boosting everything.
- Added GPU/VRAM pressure awareness, including GPU helper detection and VRAM-pressure behavior that reduces surrounding pressure without touching drivers, clocks, overclocking, DNS, Winsock, or game input settings.
- Added streamer-safe behavior for OBS, Streamlabs, TikTok Studio, Discord/voice, capture, browser helpers, and encoder-sensitive sessions.
- Added the stronger Zero Ping stack: session classification, game/helper correlation, launcher filtering, UDP confidence, netcode shield metadata, QoS/DSCP readiness, and stricter lock-on that only reports active protection after UDP is confirmed on the game or related process tree.
- Added Game Shockwave / Process Start Radar style behavior so real games and serious foreground workloads receive immediate protection before slower UDP confirmation finishes.
- Preserved the beta Games library groundwork: game discovery, per-game community preset structure, backup/restore flow, apply gating, and restore-to-default support for file-based tweaks.
- Improved installation and update robustness with the .NET WebView-only runtime path, idempotent startup handling, first-run/admin setup safeguards, cleanup of old runtimes, internal update flow, and preservation of existing user settings.
- Restored the approved 0.5.45 dashboard look for the 0.5.60 release while adapting only the bindings needed for the new backend fields.
- Added regression coverage for core engine invariants so future changes do not silently weaken foreground restore, game detection, Zero Ping context, or rollback safety.

## 0.5.45 - Dashboard spacing and stability polish

- Refined the main dashboard hero so the Zero Ping and engine control area use space cleanly instead of leaving a large empty block.
- Kept the recent 0.5.x engine upgrades: stronger Zero Ping lock-on, better game/process classification, CPU-bound assist, GPU/VRAM pressure awareness, streamer-safe containment, and rollback/audit feedback.
- Kept the beta Games library work: per-game presets, restore-to-default flow for file-based tweaks, better local game discovery, and clearer preset confirmation.
- Kept the premium WebView launcher refresh, new logo/tray identity, improved layout behavior, and safer update/install flow.
- Preserved user settings during updates and avoided resetting optional features such as Zero Ping and Smart Learning.

## 0.4.13 - 2026-07-15

- Fixed Live Manager policy buttons so A/P/L/B/D update immediately after selection.
- Made per-app policies match both executable path and process name, which improves behavior for browsers and multi-process apps.
- Kept manual policy state visible in the dashboard before the next engine pass refreshes score data.

## 0.4.12 - 2026-07-15

- Rebuilt the README and About artwork with clean, generic product visuals and no truncated labels.
- Refreshed embedded documentation images used by the local About view and public site.

## 0.4.11 - 2026-07-15

- Fixed sidebar telemetry overflow for long current-context/app names and added hover titles for full values.

## 0.4.10 - 2026-07-15

- Added hover explanations for Auto, Protect, Light, Balanced, Deep, tier badges, cooldown, skip, admin, and OK action badges in the Live Manager.
- Included the root site build manifest so production deployment can find the static site build entrypoint.

## 0.4.9 - 2026-07-15

- Fixed update popup button alignment and prevented the automatic update toggle text from wrapping.

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

