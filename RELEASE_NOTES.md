# Smart Background Nap 0.4.6

Update and site release: adds official GitHub release checks inside the launcher and publishes a public landing page with automatic release/change discovery.

## Highlights

- Single-file release: download and run SmartBackgroundNap.exe.
- Added official update checks against GitHub Releases, with automatic checks controlled by the user.
- Added launcher actions to update, manually check, ignore the current latest version, or disable automatic update reminders.
- Added a public landing site with release download, latest changes, creator links, and animated product sections.
- Updated trust notes to clarify that the app has no telemetry and no user data uploads.
- The Behavior Engine, PC Profile, grouped Live Manager, Smart Learning, Permission Guard, multilingual UI, and local safety model remain included.

## Included

- SmartBackgroundNap.exe
- MIT license

## Download Verification

SHA-256 for SmartBackgroundNap.exe:

```text
1C3F8DEC4A9B3E50A79202BDA272A1C06F4793D078CEF3B9E9D0538E6F49D5A0
```

## Trust Notes

Smart Background Nap has no telemetry, no user data uploads, no driver install, no service install, no startup registry key, and no permanent administrator elevation. The launcher can check the official GitHub Releases endpoint to notify about updates.

This release stays process-level and conservative. It avoids power plan switching, affinity rules, CPU Sets, overclocking, undervolting, service tweaks, driver tweaks, and app closing.

Windows SmartScreen reputation depends on Authenticode signing and Microsoft reputation. This release includes executable metadata and an explicit least-privilege manifest, but unsigned community builds can still show "Unknown Publisher" on some PCs until the project has signing and reputation.