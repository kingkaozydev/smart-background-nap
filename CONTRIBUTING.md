# Contributing

Thanks for considering a contribution to Smart Background Nap.

## Development Goals

- Keep the optimizer transparent and easy to audit.
- Prefer safe defaults over aggressive tweaks.
- Do not fight Process Lasso, ThrottleStop, MSI Afterburner, RTSS, or GPU drivers.
- Avoid always-running heavy background services.
- Keep changes reversible.

## Local Build

Build the tray app:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-tray-exe.ps1
```

Run a status pass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\background-nap.ps1 -Action Status
```

Run an apply pass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\background-nap.ps1 -Action Apply -StateMode Latest
```

## Pull Requests

Good PRs usually include:

- a clear reason for the change;
- notes about safety and reversibility;
- updated README/config docs when behavior changes;
- no unrelated formatting churn.

## Safety Rules

Do not add defaults that:

- close user apps automatically;
- disable Windows services;
- alter drivers;
- alter power plans;
- change CPU affinity or CPU Sets;
- alter clocks, voltages, or GPU settings.
