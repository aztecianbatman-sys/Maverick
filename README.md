# Maverick

Maverick is a Windows desktop security project built around one idea: security software should be serious without being miserable to use.

There are two planned desktop experiences:

**Maverick Security** is the protection-first app.

**Maverick AI** uses the same Maverick security core, then adds local activity history, investigations, and a BYOK chat interface so an AI model can help explain what is happening on the device.

## Current state

The project has finished the first three development phases.

### Phase 1 — AI shell

- Electron 37.2.6
- React + TypeScript + Vite
- Minimal 16:9 dark interface
- Collapsible sidebar and local conversations
- BYOK settings
- OpenAI-compatible API support
- Provider model browser
- Separate Free model filtering based on provider pricing metadata
- Local model cache and chat/activity persistence
- OS-protected local storage for the saved API key

### Phase 2 — Local security foundation

The first native Maverick Core is now in the repository as a C#/.NET 8 Windows Worker Service.

It currently provides:

- file inspection and SHA-256 hashing
- process inventory
- Windows service inventory
- scheduled-task inventory
- startup-location inventory
- configurable recursive filesystem monitoring
- SQLite local security events
- inventory-only scans
- quarantine metadata
- narrow Windows named-pipe IPC
- Electron-to-Core bridge
- service build/install/uninstall helpers

Phase 2 is **not a finished antivirus engine**. The scanner does not make malware verdicts, the quarantine layer does not move/delete files yet, and there is no kernel driver.

That distinction is deliberate.

### Phase 3 — Activity Journal

The local Core journal now has a real event schema instead of a loose summary log.

Each security event can carry:
- time
- event type
- process
- file
- action
- result
- risk
- evidence
- severity
- source
- summary
- structured details

The Core can return recent events, today’s events, and filtered searches. Existing Phase 2 databases are upgraded in place.

Maverick AI can also use today’s journal for security-oriented questions such as “what happened today?” The local events are retrieved from Maverick Core first and then supplied to the user’s selected BYOK model as evidence. Normal conversations do not receive journal data.

## Building

For the Electron app:

```bash
npm install
npm run dev
```

For the native Core on Windows with .NET installed:

```bash
npm run core:build
```

The published Core goes to `dist-core/`.

To install the Windows service, open **PowerShell as Administrator** and run:

```powershell
npm run core:install
```

To remove it later:

```powershell
npm run core:uninstall
```

## Core architecture

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

Electron talks to the native Core through a small named-pipe protocol. The Core owns native security operations; the renderer does not get arbitrary native execution access.

## Local data

The Core stores its database and configuration under:

```
%ProgramData%\Maverick\
├── maverick.db
├── config.json
└── Quarantine\
```

The Electron app keeps user-facing application data in its own Electron application-data directory.

API keys are stored by Electron using OS-backed secret storage. They are never written as plain text to the repository.

## What is next

**Phase 4** is where Maverick starts making security decisions: detection signals, reputation, behavioral correlation, safer containment, and stronger IPC authorization.

That comes only after the journal foundation is stable.

## Maverick VI

Maverick VI is the project's intended display typeface. The specimen artwork is a visual reference, not a finished WOFF2/TTF binary yet, so the repository does not pretend otherwise.

## The rule for Maverick

Build one phase at a time.

No pretend scans. No fake detections. No invented security claims. If something is experimental, incomplete, or inventory-only, Maverick says so.
