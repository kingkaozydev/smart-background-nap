Smart Nap 0.8.1 Hotfix

Highlights:
- Game VRAM Priority now starts during detected game sessions, not only after VRAM pressure is already high.
- Surrounding browser, launcher, and helper processes that consume GPU/VRAM can be softened earlier so the active game keeps priority.
- The hotfix preserves safety guards for the foreground tree, protected apps, Zero Ping protected games, voice/media, streaming, shader compilation, and realtime-friendly processes.
- Critical VRAM Pressure Guard remains available as the stronger reactive path when the adapter is already under pressure.
- Engine regression coverage now locks this behavior so VRAM priority cannot be accidentally moved behind generic helper handling.

Validation performed:
- Engine regression guard passed.
- Launcher regression guard passed.
- Responsive launcher guard passed.
- Responsive visual guard passed across 39 scenarios.
- Installed build verified as version 0.8.1 with the same SHA256 published in this release.

Executable:
- `SmartBackgroundNap.exe`
- SHA256: `86399367A52E70E9F64F5D492BFA24BE95AFE1A39CE40926F33D684D7C86B82E`

Safety model:
- No driver changes.
- No GPU clock, voltage, BIOS, or control-panel tuning.
- No global Windows VRAM tweak.
- No game-process affinity lock.
- No DNS, IP, Winsock, adapter, or firewall rewrites.
- No app killing.
