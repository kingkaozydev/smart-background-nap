# Security Policy

## Supported Versions

The current public release line is supported.

## Reporting A Vulnerability

Please open a GitHub issue with:

- affected version or commit;
- Windows version;
- steps to reproduce;
- expected behavior;
- actual behavior;
- whether the issue requires administrator privileges.

Avoid posting secrets, private logs, or personal account data.

## Security Notes

Smart Background Nap uses Windows process APIs and per-user scheduled tasks. It does not require storing credentials or tokens.

The project does not intentionally collect telemetry.

For the complete model, see `docs/SECURITY_MODEL.md`.

At a glance:

- no telemetry;
- no network calls;
- no driver, service, browser extension, or startup registry install;
- no administrator elevation requested by the app manifest;
- no app killing or file deletion;
- local logs and restore snapshots only.
- background process I/O priority can be lowered and restored like the other process-level settings.
- foreground wake restore only restores process-level settings for apps that become active.
- temporary active-app protection is local JSON state under the app output folder.
