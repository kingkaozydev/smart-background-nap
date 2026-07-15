# Smart Background Nap 0.4.5

Behavior and launcher refinement release: adds local behavior profiling, clearer WebView2 controls, improved window handling, and fresher tray telemetry.

## Highlights

- Single-file release: download and run SmartBackgroundNap.exe.
- Added Behavior Engine, a local per-app profiler that learns from aggregate app behavior instead of listing every child process as a separate app.
- Behavior-aware nap decisions can soften apps that wake often or refault memory, and deepen apps that are proven idle and efficient to trim.
- Refined the dashboard with clearer intelligence telemetry, cleaner copy, responsive layout fixes, and better action badges.
- Added a refreshed README banner, project badges, and about panel with generic sample visuals for the GitHub page.
- Reworked the WebView2 window frame with a dark custom surface and reliable native drag handling.
- Made Start with Windows directly clickable from the top status area and startup card.
- Fixed tray tooltip refresh so hover/menu interactions request current RAM, managed app, and reclaimed-memory data.
- The PC Profile, grouped Live Manager, Smart Learning, Permission Guard, multilingual UI, and local-only trust model remain included.

## Included

- SmartBackgroundNap.exe
- MIT license

## Download Verification

SHA-256 for SmartBackgroundNap.exe:

```text
F0175A25A0FF5F1D1CD4B2FCC4E9BE6825D2CDDCF9BF568E43B5A1CE34CE1B96
```

## Trust Notes

Smart Background Nap has no telemetry, no network calls, no driver install, no service install, no startup registry key, and no permanent administrator elevation.

This release stays process-level and conservative. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.

Windows SmartScreen reputation depends on Authenticode signing and Microsoft reputation. This release includes executable metadata and an explicit least-privilege manifest, but unsigned community builds can still show "Unknown Publisher" on some PCs until the project has signing and reputation.
