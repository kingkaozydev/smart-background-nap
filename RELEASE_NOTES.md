# Smart Background Nap 0.4.4

Hardware telemetry accuracy release: corrected GPU VRAM reporting, live effective CPU clock, and cleaner PC Profile data.

## Highlights

- Single-file release: download and run `SmartBackgroundNap.exe`.
- Fixed GPU VRAM detection by preferring the display driver's `HardwareInformation.qwMemorySize` value when WMI reports a capped `AdapterRAM` value.
- Fixed CPU frequency display so the dashboard shows base clock plus live effective clock from Windows processor performance counters.
- Removed misleading frozen `max 2.5 GHz` style output when Windows only exposes base clock data.
- The RTX 4060 class reading now resolves from 4 GB capped WMI output to the driver-reported ~8 GB value when exposed by Windows.
- Fixed the native maximize button so the launcher fills the Windows work area instead of opening in a capped half-size state.
- GPU driver detail now shows vendor-friendly labels, including NVIDIA package versions such as 595.79 instead of raw Windows WDDM versions such as 32.0.15.9579.
- The dark native WebView2 frame, grouped Live Manager, Smart Learning, Permission Guard, PC Profile, tray telemetry, and local-only trust model remain included.

## Included

- `SmartBackgroundNap.exe`
- MIT license

## Download Verification

SHA-256 for `SmartBackgroundNap.exe`:

```text
2CAD2E5444EB5D8A6DA91EC8A0E3C53011F7B7544C0BAD20BE444B8623E06F3F
```

## Trust Notes

Smart Background Nap has no telemetry, no network calls, no driver install, no service install, no startup registry key, and no permanent administrator elevation.

This release stays process-level and conservative. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.

Windows SmartScreen reputation depends on Authenticode signing and Microsoft reputation. This release includes executable metadata and an explicit least-privilege manifest, but unsigned community builds can still show "Unknown Publisher" on some PCs until the project has signing and reputation.