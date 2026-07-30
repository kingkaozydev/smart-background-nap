# Smart Nap Core Service Architecture

## Direction

Smart Nap is moving toward three cooperating components:

```text
Smart Nap Core Service
        <-> secure local IPC
Smart Nap Session Agent
        <-> secure local IPC
Smart Nap Launcher
```

The Core Service is the long-running authority for engine state, health, operations, restore, events, and diagnostics. The Session Agent is the lightweight process inside the interactive user session that observes foreground, fullscreen, OBS/live context, input idle, and other desktop-only signals. The Launcher is the control panel and diagnostics client.

This split is intentional: a Windows service runs in Session 0 and should not become the component that guesses foreground windows or interactive desktop state.

## Current 0.7.1 Foundation

The first migration slice is deliberately narrow:

- Keep the existing scheduled user-session engine task as the component that performs interactive-session work.
- Keep the Core Service as a watchdog and bridge while the full IPC and Session Agent are introduced safely.
- Publish a versioned Core v1 snapshot at `smart-snap-core-service-latest.json`.
- Expose protocol metadata:
  - `ProtocolVersion = 1`
  - `MinimumSupportedProtocolVersion = 1`
  - `PipeName = SmartNap.Core.v1`
  - `ContextProvider = ScheduledUserSessionTask`
- Publish capabilities for the current bridge:
  - `hello`
  - `getCapabilities`
  - `getSnapshot`
  - `subscribe`
  - `getState`
  - `getEvents`
  - `getDiagnostics`
  - `ping`
  - `corePipe.v1`
  - `sessionAgent.v1`
  - `publishSessionContext`
  - `getSessionContext`
  - `watchdog`
  - `scheduledTaskBridge`
- Surface Core health in the launcher and event feed.
- Accept local Core Pipe v1 clients over `SmartNap.Core.v1` with an explicit pipe ACL, a versioned JSON envelope, message size limits, read-only diagnostic commands, and the Session Agent observation command.
- Add a Session Agent v1 executable mode in the interactive user session. It observes foreground process, fullscreen state, input idle time, streaming/live helpers, and publishes context observations without applying privileged optimizations.
- Install `SmartBackgroundNapSessionAgent` as a hidden per-user logon task using `InteractiveToken` and `LeastPrivilege`, then start it on demand after setup so context starts flowing without requiring a reboot.

## Responsibilities

### Core Service

- Own the future state machine and command queue.
- Publish snapshots, events, health, and diagnostics.
- Coordinate operations only after they become service-owned.
- Preserve journal and restore guarantees.
- Avoid Session 0 foreground/fullscreen/OBS decisions.

### Session Agent

- Observe the active interactive session.
- Send desktop context to the Core Service.
- Never become the source of truth for engine decisions.
- Never apply privileged optimizations directly.

### Launcher

- Render state from Core snapshots and events.
- Send commands with explicit operation IDs once IPC lands.
- Show only capabilities supported by the Core.
- Avoid optimistic success for critical operations.

## Migration Phases

1. Core v1 foundation: snapshot, health, diagnostics, watchdog bridge.
2. Secure named pipe: handshake, capabilities, `getSnapshot`, events, heartbeat.
3. Session Agent: foreground, fullscreen, OBS/live, idle and session context.
4. Operations: queue, operation IDs, optimize, cancel, pause, resume, restore.
5. Modules: migrate ForegroundGuard, BackgroundNap, memory, I/O, EcoQoS, Zero Ping, ShaderBoost, CPU-bound assist, and Live Guard one by one.
6. Observability and recovery: journal, ring buffer, diagnostics report, ETW/Event Log, safe mode, rollback-aware updates.

## Current Core Pipe V1 Commands

All commands use a JSON envelope with:

```text
protocolVersion
messageId
correlationId
clientType
clientVersion
sessionId
command
payload
createdAt
```

The current pipe intentionally supports read-only/diagnostic commands plus one observation input from the Session Agent:

- `hello`
- `getCapabilities`
- `getSnapshot`
- `getState`
- `getEvents`
- `getDiagnostics`
- `ping`
- `subscribe`
- `getSessionContext`
- `publishSessionContext`

`publishSessionContext` is intentionally not an operation command. It only accepts observed session context from `clientType = sessionAgent` and updates the session snapshot consumed by `getSnapshot`, diagnostics, and future decision code.

Mutable commands such as `optimizeNow`, `cancelOperation`, `pause`, `resume`, `restore`, `setMode`, and configuration writes are not exposed through IPC yet. They need the single command queue, operation IDs, journal, and rollback model before being accepted by the service.

## Current Session Agent V1

The first Session Agent slice adds:

- `--session-agent` for a lightweight interactive-session observer loop.
- `--session-agent-once` for diagnostics and one-shot verification.
- `--install-session-agent`, `--uninstall-session-agent`, and `--session-agent-status` for task lifecycle and diagnostics.
- `SmartBackgroundNapSessionAgent` as the per-user logon task that runs the agent in the interactive desktop.
- `smart-snap-session-agent-latest.json` as the local bridge snapshot.
- Foreground PID, process name, process start time, path, fullscreen/window state, game/live hints, idle seconds, context, confidence, and evidence.
- Core Pipe publication through `publishSessionContext` when the Core Service is available.

The agent does not decide the final mode and does not apply CPU, RAM, I/O, EcoQoS, power-plan, Zero Ping, or ShaderBoost changes.

## Non-Goals For The Foundation Slice

- Do not move foreground detection into the service.
- Do not apply game/session optimizations directly from Session 0.
- Do not replace Zero Ping or ShaderBoost detection with service-only logic.
- Do not expose unauthenticated TCP/HTTP control.
- Do not let launcher state become the source of truth.
- Do not run arbitrary commands from IPC or UI.
