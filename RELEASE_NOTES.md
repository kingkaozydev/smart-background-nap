# Smart Background Nap 0.3.3

WebView2 launcher, adaptive nap engine, multilingual UI, and lighter tray behavior.

## Highlights

- Single-file release: download and run `SmartBackgroundNap.exe`.
- .NET 9 / WebView2 dashboard with live manager, telemetry, and event stream.
- First-run language picker with Portuguese BR, English, Russian, Spanish, French, and German.
- Adaptive nap tiers: Light, Balanced, and Deep.
- Native fast foreground wake restores priority, memory priority, I/O priority, and EcoQoS when apps return to the front.
- Cooldown-aware RAM trim to avoid repeatedly trimming the same process.
- Fullscreen-aware and burst-aware scoring for gaming and multitasking sessions.
- Dashboard WebView resources are released when the window is closed or minimized to tray.
- Runtime scripts, config, README text, security model, and icon assets are embedded inside the EXE.
- Product/version metadata and an `asInvoker` Windows manifest.
- Built-in safety report with executable SHA-256, runtime path, scheduled-task status, and local-only behavior summary.
- Managed per-user startup copy under `%LOCALAPPDATA%\Programs\SmartBackgroundNap` when automatic startup is enabled.
- Keeps apps open instead of killing them.
- Lowers safe background CPU scheduling, memory priority, disk I/O priority, and EcoQoS pressure.
- Includes an auditable PowerShell core and compiled C# tray/dashboard host.

## Included

- `SmartBackgroundNap.exe`
- MIT license

## Download Verification

SHA-256 for `SmartBackgroundNap.exe`:

```text
A73AA6D7CFC4C8E0229FF968D3D08B402CF25945BD58D6DB2821DE12CA5BAA02
```

## Trust Notes

Smart Background Nap has no telemetry, no network calls, no driver install, no service install, no startup registry key, and no administrator elevation request.

Windows SmartScreen reputation depends on Authenticode signing and Microsoft reputation. This release includes executable metadata and an explicit least-privilege manifest, but unsigned community builds can still show "Unknown Publisher" on some PCs until the project has signing and reputation.

## Notes

Smart Background Nap is conservative by design. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.
