# Installation

## Quick Start

1. Download the latest release.
2. Extract the zip.
3. Open:

```text
SmartBackgroundNap.exe
```

4. Click:

```text
Run automatically
Start with Windows
```

Those toggles enable automatic optimization and the tray icon startup task.

## Advanced Command Fallbacks

The app is the recommended path. The command files remain available for advanced users and troubleshooting.

Install only the automatic optimizer:

```text
install-auto-background-nap.cmd
```

Install only the tray startup task:

```text
install-tray-icon.cmd
```

## What Gets Installed

Two Windows scheduled tasks may be created:

```text
SmartBackgroundNap
SmartBackgroundNapTray
```

`SmartBackgroundNap` is the optimizer. It runs `bin\SmartBackgroundNap.exe --apply` after logon and every few minutes, applies rules, then exits.

`SmartBackgroundNapTray` starts `bin\SmartBackgroundNap.exe --tray` at logon. It is optional, but recommended.

## Verify Installation

```text
status-auto-background-nap.cmd
status-tray-icon.cmd
```

## Uninstall

Remove the optimizer:

```text
uninstall-auto-background-nap.cmd
```

Remove the tray icon:

```text
uninstall-tray-icon.cmd
```

## Restore Process State

If you want to restore the latest known priority/throttling state for currently running processes:

```text
restore-background-nap.cmd
```

## Requirements

- Windows 10/11
- PowerShell 5+
- .NET Framework 4.x for building the app from source

The included main executable is built from `src\SmartBackgroundNap.cs`.

The legacy tray executable is built from `src\SmartBackgroundNapTray.cs`.
