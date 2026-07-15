# Smart Background Nap 0.4.4

Hardware telemetry accuracy release: corrected GPU VRAM reporting, live effective CPU clock, and cleaner PC Profile data.

## Highlights

- Single-file release: download and run `SmartBackgroundNap.exe`.
- Fixed GPU VRAM detection by preferring driver-reported video memory when Windows exposes capped adapter-memory values.
- Fixed CPU frequency display so the dashboard shows base clock plus live effective clock from Windows processor performance counters.
- Removed misleading frozen base-clock style output when Windows only exposes static CPU clock data.
- Fixed the native maximize button so the launcher fills the Windows work area instead of opening in a capped half-size state.
- GPU driver detail now shows vendor-friendly labels when safely inferable instead of exposing raw OS driver identifiers as the primary display value.
- The dark native WebView2 frame, grouped Live Manager, Smart Learning, Permission Guard, PC Profile, tray telemetry, and local-only trust model remain included.

## Included

- `SmartBackgroundNap.exe`
- MIT license

## Download Verification

SHA-256 for `SmartBackgroundNap.exe`:

```text
DB4C2AE4C235AF40BAE81302EC7A3459EEF998249B705CAB527A4BC09CF7B8D1
```

## Trust Notes

Smart Background Nap has no telemetry, no network calls, no driver install, no service install, no startup registry key, and no permanent administrator elevation.

This release stays process-level and conservative. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.

Windows SmartScreen reputation depends on Authenticode signing and Microsoft reputation. This release includes executable metadata and an explicit least-privilege manifest, but unsigned community builds can still show "Unknown Publisher" on some PCs until the project has signing and reputation.