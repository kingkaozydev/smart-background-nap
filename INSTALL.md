# Installation

## Quick Start

1. Download the latest release.
2. Open:

```text
SmartBackgroundNap.exe
```

3. Click:

```text
Run automatically
Start with Windows
```

Those toggles enable automatic optimization and the tray icon startup task.

The release is a single executable. Runtime scripts and default config are embedded inside the app.

When automatic mode or tray startup is enabled, the app keeps a managed copy at:

```text
%LOCALAPPDATA%\Programs\SmartBackgroundNap\SmartBackgroundNap.exe
```

This lets Windows startup continue working even if the downloaded EXE is moved later.

## What Gets Installed

Two Windows scheduled tasks may be created:

```text
SmartBackgroundNap
SmartBackgroundNapTray
```

`SmartBackgroundNap` is the optimizer. It runs `SmartBackgroundNap.exe --apply` after logon and every few minutes, applies rules, then exits.

`SmartBackgroundNapTray` starts `SmartBackgroundNap.exe --tray` at logon. It is optional, but recommended.

## Verify Installation

Open the dashboard. The top status cards show automatic mode, tray startup, last pass, and result.

## Uninstall

Open `Mais` -> `Disable background tasks`.

## Restore Process State

If you want to restore the latest known priority/throttling state for currently running processes:

Click `Restore` in the dashboard.

## Requirements

- Windows 10/11
- PowerShell 5+
- .NET Framework 4.x

The included main executable is built from `src\SmartBackgroundNap.cs`.

The legacy tray executable is built from `src\SmartBackgroundNapTray.cs`.
