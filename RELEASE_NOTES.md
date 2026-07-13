# Smart Background Nap 0.4.0

Intelligence Engine release: safer automatic decisions, visible intent telemetry, per-app policies, and a stronger Live Manager.

## Highlights

- Single-file release: download and run `SmartBackgroundNap.exe`.
- Intent Engine classifies the current session as Desktop, Gaming, Media/Call, Download/Install, or Memory Pressure.
- Foreground Switch Accelerator learns fast app switching and gives frequent return targets a lighter wake path.
- Per-game profile state tracks pressure patterns during gaming sessions without changing power plans, drivers, CPU affinity, or CPU Sets.
- Contention Radar surfaces CPU, memory, burst, guard, and managed-process pressure for the dashboard.
- Media/Call Protection avoids touching active voice, streaming, recording, and media workloads.
- Download/Launcher Guard avoids false positives when launchers are downloading, installing, or updating games.
- Memory Pressure Governor 2.0 adds Normal, Moderate, Elevated, and Critical pressure bands.
- Live Manager now exposes app policies: Auto, Protect, Light, Balanced, and Deep.
- Dashboard rows show role, guard, intent, fast-wake, and policy badges.
- Extracted runtime files are versioned, so updates can move to a fresh engine even when an older runtime folder has restrictive permissions.
- Smart Learning remains optional and local.
- Permission Guard still offers a one-pass UAC elevation for apps that deny process changes.
- WebView2 dashboard, multilingual UI, tray indicator, safety report, and single-EXE embedded runtime remain included.

## Included

- `SmartBackgroundNap.exe`
- MIT license

## Download Verification

SHA-256 for `SmartBackgroundNap.exe`:

```text
B791FAE2D654FE80793EFDB6E77AC6B05FE63851A6A3481ED5BF00DA59A446B9
```

## Trust Notes

Smart Background Nap has no telemetry, no network calls, no driver install, no service install, no startup registry key, and no permanent administrator elevation.

This release stays process-level and conservative. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.

Windows SmartScreen reputation depends on Authenticode signing and Microsoft reputation. This release includes executable metadata and an explicit least-privilege manifest, but unsigned community builds can still show "Unknown Publisher" on some PCs until the project has signing and reputation.
