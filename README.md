# Smart Background Nap

![Smart Background Nap overview](docs/images/hero.svg)

**Smart Background Nap** is a lightweight Windows helper that keeps background apps from getting louder than they need to be.

It is made for people who leave browsers, chat, launchers, tools, music, and capture apps open while gaming, streaming, coding, or multitasking. Instead of closing your apps, it quietly lowers safe background pressure and then gets out of the way.

Created by **KaozyKing**.

- GitHub: [@kingkaozydev](https://github.com/kingkaozydev)
- Instagram: [@oeduardomacedo](https://www.instagram.com/oeduardomacedo/)

> Keep your apps open. Let Windows breathe.

## What It Does

Smart Background Nap watches the current Windows user session and applies a conservative "nap" to apps that are safe to quiet down.

![Before and after comparison](docs/images/before-after.svg)

For selected background apps, it can apply:

- below-normal process priority
- low memory priority
- Windows Power Throttling / EcoQoS where supported
- timer-resolution isolation for throttled background apps
- working set trimming above a configurable RAM threshold

It skips the things that should stay awake:

- Windows system processes
- services and session 0 processes
- the foreground app
- active high-CPU workloads
- configured protected apps and paths
- configured game folders

The goal is simple: reduce background noise without killing your workflow.

## Why It Exists

Modern PCs are fast, but day-to-day app stacks are noisy. A few browsers, chats, launchers, overlays, downloaders, and helper apps can keep waking the CPU, holding RAM, or competing for scheduler attention long after they stop being important.

Smart Background Nap gives those apps a softer background profile. Your apps stay open, your session stays intact, and Windows has a little more room for what you are actually doing now.

## Highlights

- One-click Windows app: open `SmartBackgroundNap.exe` and control everything from the dashboard.
- Start with Windows toggle for the tray indicator.
- Automatic optimization toggle for scheduled background passes.
- Automatic scheduled optimization every few minutes.
- Tray icon with status, apply-now, log, folder, and README shortcuts.
- No heavy always-running optimizer service.
- Foreground app protection.
- Active workload protection.
- Configurable JSON rules.
- Manual, automatic, watch, restore, and browser-only modes.
- Auditable PowerShell core.
- Lightweight compiled C# WinForms tray indicator.

![App dashboard](docs/images/app-dashboard.svg)

![Automatic flow](docs/images/automatic-flow.svg)

## Install

Download the latest release, extract it, and run:

```text
SmartBackgroundNap.exe
```

Then click:

```text
Install / update
```

That enables automatic optimization and the tray icon startup task.

Smart Background Nap creates two scheduled tasks:

```text
SmartBackgroundNap
SmartBackgroundNapTray
```

The optimizer task runs after logon, repeats every few minutes, applies a pass, writes a compact log, and exits.

The tray task starts `SmartBackgroundNap.exe --tray` so you can see that Smart Background Nap is available after every login.

## Tray Indicator

The tray indicator is optional but recommended. It gives you quick access to:

- Status
- Apply now
- Open log
- Open folder
- Open README
- Exit tray icon

Preferred tray app:

```text
SmartBackgroundNap.exe
```

Internal task target:

```text
bin\SmartBackgroundNap.exe
```

Legacy tray fallback:

```text
bin\SmartBackgroundNapTray.exe
```

PowerShell fallback for source/debug builds:

```text
smart-background-nap-tray.ps1
```

## App Controls

The dashboard includes:

- Automatic optimization
- Start tray with Windows
- Install / update
- Apply now
- Restore
- Open log
- Open config
- Open folder
- README
- GitHub
- Uninstall all

## Command Fallbacks

Automatic mode:

```text
install-auto-background-nap.cmd
uninstall-auto-background-nap.cmd
status-auto-background-nap.cmd
run-auto-background-nap-now.cmd
```

Tray icon:

```text
install-tray-icon.cmd
uninstall-tray-icon.cmd
status-tray-icon.cmd
start-tray-icon-now.cmd
```

Manual mode:

```text
status-background-nap.cmd
apply-background-nap.cmd
watch-background-nap.cmd
restore-background-nap.cmd
```

Browser-only mode:

```text
status-browser-nap.cmd
apply-browser-nap.cmd
watch-browser-nap.cmd
restore-browser-nap.cmd
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

## Logs And Restore

Smart Background Nap writes logs and restore state under:

```text
outputs\background-nap-auto.log
outputs\background-nap-state-latest.json
```

Use this to restore the latest snapshot:

```text
restore-background-nap.cmd
```

## Build The App

The main app source lives here:

```text
src\SmartBackgroundNap.cs
```

The legacy tray source lives here:

```text
src\SmartBackgroundNapTray.cs
```

Build it with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-tray-exe.ps1
```

Generated output:

```text
SmartBackgroundNap.exe
bin\SmartBackgroundNap.exe
bin\SmartBackgroundNapTray.exe
```

## What It Does Not Do

Smart Background Nap intentionally avoids risky or invasive tuning:

- no app killing
- no power plan switching
- no CPU affinity rules
- no CPU Sets
- no overclocking
- no undervolting
- no GPU tuning
- no driver changes
- no Windows service disabling

It is a background-pressure reducer, not a miracle FPS button. Results depend on your workload, hardware, Windows version, and app behavior.

## Recommended Topics

```text
windows
windows-11
gaming
performance
optimization
background-apps
process-priority
memory-management
ecoqos
power-throttling
tray-app
powershell
winforms
cpu-optimization
ram-optimizer
```

## License

MIT License. See `LICENSE`.
