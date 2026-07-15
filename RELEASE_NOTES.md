# Smart Background Nap 0.4.3

Launcher telemetry and social preview release: live CPU clock, richer PC profile data, and a polished GitHub banner.

## Highlights

- Single-file release: download and run `SmartBackgroundNap.exe`.
- The PC Profile panel now shows live CPU clock using native Windows power information instead of a frozen base frequency.
- GPU telemetry now includes model, VRAM, driver/display detail when Windows exposes it.
- System telemetry now includes RAM free, pagefile availability, and memory load instead of only a plain free-RAM line.
- Added a new GitHub social preview banner at `docs/images/smart-nap-social-preview.png` and updated the README hero image.
- The new banner is embedded in the EXE runtime so the local README opens cleanly from the app.
- The dark native WebView2 frame, grouped Live Manager, Smart Learning, Permission Guard, and local-only trust model remain included.

## Included

- `SmartBackgroundNap.exe`
- MIT license

## Download Verification

SHA-256 for `SmartBackgroundNap.exe`:

```text
9FEE82622FC78E225F013E8A0796D981EA99BAE7336A990E31E1BD5CC00B700F
```

## Trust Notes

Smart Background Nap has no telemetry, no network calls, no driver install, no service install, no startup registry key, and no permanent administrator elevation.

This release stays process-level and conservative. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.

Windows SmartScreen reputation depends on Authenticode signing and Microsoft reputation. This release includes executable metadata and an explicit least-privilege manifest, but unsigned community builds can still show "Unknown Publisher" on some PCs until the project has signing and reputation.