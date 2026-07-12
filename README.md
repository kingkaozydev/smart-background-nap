# Smart Background Nap

**Smart Background Nap** is a lightweight Windows background app optimizer for gaming, streaming, and heavy multitasking.

Created by **KaozyKing**.

- GitHub: [@kingkaozydev](https://github.com/kingkaozydev)
- Instagram: [@oeduardomacedo](https://www.instagram.com/oeduardomacedo/)

It automatically reduces the CPU scheduling pressure and resident memory footprint of safe background apps, while preserving your existing performance tools such as Process Lasso, ThrottleStop, MSI Afterburner, RTSS, and NVIDIA services.

> Keep your apps open. Let Windows breathe.

## Why This Exists

Most PC optimization tools either close apps, disable risky services, or fight with existing tweak stacks. Smart Background Nap takes a different approach:

- it does not close Discord, browsers, launchers, chat apps, or utilities;
- it does not change power plans;
- it does not touch voltages, clocks, CPU affinity, CPU Sets, drivers, or Windows services;
- it does not replace Process Lasso, ThrottleStop, Afterburner, or RTSS.

Instead, it gently pushes safe background apps into a lower-impact state using Windows process APIs.

## Features

- Automatic scheduled optimization every few minutes.
- Tray icon with quick status, apply-now, log, and README actions.
- Low memory priority for background candidates.
- Below-normal process priority for safe apps.
- Windows Power Throttling / EcoQoS where supported.
- Working set trimming for apps above a configurable RAM threshold.
- Foreground app protection.
- Active CPU workload protection.
- Protected process and protected path rules.
- Browser-only fallback mode.
- Manual, watch, automatic, and restore modes.
- Configurable JSON settings.
- No always-running optimizer service.

## SEO Keywords

Windows gaming optimizer, background app optimizer, RAM optimizer, CPU optimizer, EcoQoS tweak, Power Throttling Windows, Process Lasso companion, ThrottleStop companion, MSI Afterburner companion, Windows 11 gaming tweak, background process optimizer, reduce Discord RAM, reduce browser RAM, Windows multitasking optimizer, low overhead tray optimizer.

## How It Works

Smart Background Nap scans your current user session and classifies processes.

It skips:

- Windows system processes
- services / session 0
- the foreground app
- active high-CPU workloads
- Process Lasso
- Process Governor
- ThrottleStop
- MSI Afterburner
- RTSS / RivaTuner
- NVIDIA container / overlay / share
- Codex and PowerShell
- configured game paths

For safe candidates, it can apply:

- `BelowNormal` process priority
- low process memory priority
- Power Throttling / EcoQoS
- ignore timer resolution for throttled background apps
- working set trim above the configured threshold

## Automatic Mode

Recommended for daily use.

The installer registers a scheduled task named:

```text
SmartBackgroundNap
```

Behavior:

- runs once after logon, with a 45-second delay;
- runs again every 5 minutes;
- applies the rules and exits;
- keeps a compact log;
- keeps a latest restore snapshot;
- does not keep a heavy optimizer daemon running.

Commands:

```text
install-auto-background-nap.cmd
uninstall-auto-background-nap.cmd
status-auto-background-nap.cmd
run-auto-background-nap-now.cmd
```

## Tray Indicator

The tray indicator is optional but recommended if you want a visible signal that the system is active.

It registers a separate task named:

```text
SmartBackgroundNapTray
```

The tray menu includes:

- Status
- Apply now
- Open log
- Open folder
- Open README
- Exit tray icon

Commands:

```text
install-tray-icon.cmd
uninstall-tray-icon.cmd
status-tray-icon.cmd
start-tray-icon-now.cmd
```

The preferred tray app is the compiled WinForms executable:

```text
bin\SmartBackgroundNapTray.exe
```

The PowerShell tray script is kept as an auditable fallback:

```text
smart-background-nap-tray.ps1
```

## Manual Mode

Use this when you want direct control:

```text
status-background-nap.cmd
apply-background-nap.cmd
watch-background-nap.cmd
restore-background-nap.cmd
```

`apply-background-nap.cmd` applies one pass.

`watch-background-nap.cmd` reapplies for 90 minutes and then exits. This is useful for long gaming sessions, but it keeps a PowerShell process open during that window.

## Browser-Only Mode

For a narrower tweak that only targets browsers:

```text
status-browser-nap.cmd
apply-browser-nap.cmd
watch-browser-nap.cmd
restore-browser-nap.cmd
```

## Build The Tray App

The tray app is built from:

```text
src\SmartBackgroundNapTray.cs
```

Build command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-tray-exe.ps1
```

Generated output:

```text
bin\SmartBackgroundNapTray.exe
```

## Configuration

Edit:

```text
game-session.config.json
```

Useful settings:

- `BackgroundNap.PriorityClass`
- `BackgroundNap.MemoryPriority`
- `BackgroundNap.TrimMinimumWorkingSetMB`
- `BackgroundNap.SkipHighCpuPercent`
- `BackgroundNap.HighCpuPercentThreshold`
- `BackgroundNap.ProtectedProcessNames`
- `BackgroundNap.ProtectedPathFragments`
- `Automation.IntervalMinutes`
- `Tray.RefreshSeconds`

## Logs And State

Outputs:

```text
outputs\background-nap-auto.log
outputs\background-nap-state-latest.json
```

The restore command uses the latest snapshot when available.

## What It Does Not Do

Smart Background Nap intentionally avoids:

- power plan switching
- CPU affinity rules
- ProBalance-style behavior
- CPU Sets
- overclocking
- undervolting
- GPU tuning
- driver changes
- service disabling
- closing apps

Those areas are better handled by dedicated tools or by Windows itself.

## Recommended GitHub Topics

```text
windows
windows-11
gaming
optimization
pc-tweaks
process-priority
memory-management
ecoqos
power-throttling
process-lasso
performance
tray-app
powershell
winforms
```

## License

MIT License. See `LICENSE`.

## Disclaimer

This project is a performance helper, not a miracle FPS booster. It is designed to reduce background pressure and improve responsiveness when many apps are open. Results depend on your workload, RAM, CPU, Windows version, and app behavior.
