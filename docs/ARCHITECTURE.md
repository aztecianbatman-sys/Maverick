# Maverick Architecture

Maverick has two desktop faces and one shared security foundation.

```
                         MAVERICK
                            │
              ┌─────────────┴─────────────┐
              │                           │
       Maverick Security             Maverick AI
              │                           │
              └─────────────┬─────────────┘
                            │
                      Maverick Core
                            │
             ┌──────────────┼──────────────┐
             ▼              ▼              ▼
          Scanner        Monitor         Journal
             │              │              │
             └──────────────┴──────────────┘
                            │
                       Windows APIs
```

## Phase status

### Phase 0 — Architecture
Done.

### Phase 1 — Maverick AI shell
Done.

### Phase 2 — Local security foundation
Done.

The native core is a C#/.NET 8 Worker Service that is intended to run as the **Maverick Core** Windows service. It stays in user mode; there is no kernel driver and no arbitrary command execution surface.

### Phase 2 capabilities

**File inspection**
- metadata inspection
- SHA-256 hashing
- file-safe sharing during reads

**System inventory**
- running processes
- process IDs, names, sessions, and best-effort executable paths
- Windows services
- scheduled tasks through the Windows `schtasks.exe` utility
- startup entries from HKLM, HKLM WOW6432, loaded user hives, and the common Startup folder

**Filesystem monitoring**
- configurable directories
- recursive monitoring
- create/change/delete/rename events
- monitor error events
- configuration persisted locally

**Journal**
- SQLite-backed local security event journal
- UTC timestamps
- severity, source, summary, and structured details
- indexed recent-event queries

**Scan foundation**
- file and directory traversal
- SHA-256 inventory
- inaccessible-file counting
- cancellation support
- explicit `inventory-only` verdict so hashing is never presented as malware detection

**Quarantine foundation**
- metadata records for a prospective quarantine item
- original path
- hash and size when available
- status tracking
- no destructive file movement in this phase

**IPC**
- Windows named pipe: `MaverickCore`
- one JSON request per connection
- narrow command allow-list
- authenticated-user ACL
- Electron gets a small bridge instead of arbitrary native access

### Phase 2 IPC commands

```
status
file.inspect
processes.list
services.list
tasks.list
startup.list
journal.recent
scan
quarantine.register
monitor.configure
```

The desktop process can call these through `electron/core-client.js` and the preload bridge. The AI does not receive direct native execution access.

## Local storage

The Core service owns:

- `%ProgramData%\\Maverick\\maverick.db`
- `%ProgramData%\\Maverick\\config.json`
- `%ProgramData%\\Maverick\\Quarantine\\`

The Electron application keeps its own user-scoped store under Electron's application data directory.

## Service lifecycle

The repository includes PowerShell helpers:

```
npm run core:build
npm run core:install
npm run core:uninstall
```

Installation requires an elevated PowerShell session. The current installer registers the published Core executable with Windows Service Control Manager and configures automatic startup/restart behavior.

## Security boundary

Phase 2 intentionally avoids behavior-based malware verdicts, kernel callbacks, automatic deletion, and unrestricted process execution.

The Core service is a foundation for the next phase. Before public deployment, IPC authorization should be tightened from the current authenticated-user ACL to the intended interactive-user identity and the protocol should gain request authentication/replay protection.

## Next

### Phase 3 — Activity Journal
Implemented.

The Core journal now models security events as structured records with timestamp, event type, process, file, action, result, risk, evidence, source, severity, summary, and raw details.

It supports:
- recent events
- today's events
- filtering by event type and risk
- SQLite indexing
- backwards-safe schema upgrades for Phase 2 databases
- Electron display of Core activity
- selective AI context loading for security-oriented questions

When a security-oriented chat question is detected, Maverick asks the Core for today's journal and supplies those local events to the selected BYOK model as evidence. The AI is explicitly instructed not to invent events or verdicts. Ordinary chats do not receive journal data.

### Phase 4 — Real-time protection
In progress — **Phase 4A implemented**.

Phase 4A adds the first actual protection decision path in user mode:
- filesystem create/change events invoke the protection analyzer
- deterministic EICAR test-file detection returns Threat / High / Quarantine
- safe and heuristic suspicious results are journaled
- automatic containment is restricted to the deterministic EICAR test signature
- the Electron app configures the current user's Downloads, Desktop, and Documents directories for monitoring
- a lightweight process-start observer records new processes every two seconds

The heuristic analyzer intentionally uses conservative alert-only behavior at this stage. It does not claim that an executable in a temporary or Downloads folder is malware.

This phase will eventually grow into behavior correlation, persistence and download signals, but those are not enabled yet.

### Phase 4 — Investigation
Evidence timelines, threat narratives, AI investigation tools, and richer local journal queries.

### Phase 5 — Production hardening
Service packaging, signing, crash/recovery testing, performance testing, security regression tests, and release automation.
