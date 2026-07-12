# Installation

## Quick Start

1. Download or clone this repository.
2. Open the project folder.
3. Run:

```text
install-auto-background-nap.cmd
```

4. Optional, recommended if you want a visible tray indicator:

```text
install-tray-icon.cmd
```

## What Gets Installed

Two Windows scheduled tasks may be created:

```text
SmartBackgroundNap
SmartBackgroundNapTray
```

`SmartBackgroundNap` is the optimizer. It runs after logon and every few minutes, applies rules, then exits.

`SmartBackgroundNapTray` is only the tray indicator. It is optional.

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
- .NET Framework 4.x for building the tray app from source

The included tray executable is built from `src\SmartBackgroundNapTray.cs`.
