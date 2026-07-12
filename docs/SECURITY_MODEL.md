# Smart Background Nap Security Model

Smart Background Nap is designed to be transparent, local-only, and easy to audit.

## Core Guarantees

- No telemetry.
- No network calls.
- No accounts, passwords, cookies, browser profiles, documents, or game files are read.
- No drivers, kernel components, Windows services, browser extensions, or startup registry keys are installed.
- No administrator elevation is requested by the app manifest.
- No apps are killed and no user files are deleted.

## What It Changes

For selected background apps in the current user session, Smart Background Nap may apply:

- below-normal process priority;
- low process memory priority;
- Windows Power Throttling / EcoQoS where supported;
- timer-resolution isolation for throttled background apps;
- one working-set trim when the process is above the configured RAM threshold.

These changes are Windows process settings. They are not permanent patches to the app executable, Windows, drivers, or firmware.

## What It Skips

Smart Background Nap skips:

- Windows system processes;
- session 0 services;
- the current foreground app;
- high-CPU active workloads;
- configured protected process names;
- configured protected path fragments;
- known game/library paths from the default config.

## Persistence

Persistence is handled through two per-user scheduled tasks:

- `SmartBackgroundNap`: runs a short optimization pass and exits.
- `SmartBackgroundNapTray`: starts the tray/dashboard surface after logon.

Both tasks run with `InteractiveToken` and `LeastPrivilege`. They do not require administrator rights.

When the single-executable release enables automatic mode, it keeps a managed copy at:

```text
%LOCALAPPDATA%\Programs\SmartBackgroundNap\SmartBackgroundNap.exe
```

This keeps Windows startup tasks stable even if the original download is moved or removed.

## Local Data

Smart Background Nap writes only local operational files:

- extracted embedded runtime files;
- `game-session.config.json`;
- compact logs;
- restore snapshots for process settings;
- optional safety reports generated from the app.

The app does not upload these files.

## Windows Trust

The executable includes:

- product/version metadata;
- an embedded icon;
- an `asInvoker` manifest;
- a single-file release package with auditable source in the public repository.

Windows SmartScreen reputation ultimately depends on Authenticode signing and download reputation. Unsigned community builds can still show an "Unknown Publisher" warning even when the code is clean. The project is prepared for Authenticode signing when a public code-signing certificate is available.
