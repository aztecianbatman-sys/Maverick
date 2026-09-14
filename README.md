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

### Threat intelligence and known-malware knowledge

Maverick now has two complementary intelligence layers.

The **fast path** is an exact SHA-256 catalog stored locally at `%ProgramData%\\Maverick\\known-bad-hashes.json`. Matching an exact entry can immediately produce a Threat verdict. The repository seeds this with the EICAR test file only; real malware samples are not bundled.

The **behavior knowledge layer** contains public, curated ATT&CK mappings for families including WannaCry, NotPetya, Emotet, TrickBot, LockBit 2.0/3.0, JCry, Conti, Ragnar Locker, and LokiBot. These profiles are used to recognize combinations of behaviors and enrich evidence; a behavior resemblance is never treated as proof of a family identity. MITRE's software entries document these malware families and their reported techniques. citeturn349768search5turn349768search0turn602021search0turn602021search1turn349768search2turn349768search3turn602021search4turn349768search6turn349768search9

For Windows script files, Maverick also uses **AMSI** as an additional detection source. Windows Defender itself uses layered client-side ML, behavior monitoring, heuristics, AMSI, memory scanning, and reputation rather than relying on a static family list alone. citeturn246404search1turn246404search3

A validated local hash feed can be imported through the constrained Core command `threatintel.import`. The feed contains metadata only; Maverick does not need to ship or execute malware samples to use exact-hash intelligence.

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

## Phase 4 — Real-time protection (started)

The first protection slice is now real and deliberately small:

- filesystem events can trigger a protection analysis
- files receive an explicit Safe / Suspicious / Threat verdict
- verdicts are recorded in the local security journal
- the EICAR antivirus test file is a deterministic Threat and can be automatically quarantined
- ordinary heuristic signals stay alert-only for now; they are not used for destructive containment
- user-specific Downloads, Desktop, and Documents folders are configured for monitoring when the desktop app opens
- new-process observation runs in user mode and records process-start events

Phase 4 now includes the first real containment layer. It is still not a finished antivirus product, but it can now correlate downloaded-file metadata, remediate exact malicious startup entries, react to high-volume destructive file activity, and terminate processes only under strict safety gates.

### Phase 4 — Real-time protection

The protection layer is now active in user mode.

It includes:
- deterministic EICAR test-file detection
- filesystem-triggered analysis
- protected quarantine moves
- guarded process containment for confirmed threats
- recent-process correlation for ransomware signals
- high-volume destructive-activity detection
- Windows download-origin metadata capture from `Zone.Identifier`
- targeted startup persistence remediation for exact Threat-verdict files

Maverick is intentionally conservative where the evidence is weak. A suspicious script or executable does not become a Threat just because of its extension or location. Automatic process termination is restricted to protected paths being excluded and strong containment conditions; arbitrary process killing is not exposed.

### Phase 4 limitations

`FileSystemWatcher` is a user-mode notification mechanism and can miss events under heavy load, so its signals are treated as evidence rather than a perfect record. Microsoft documents the possibility of buffer overflow and duplicate/missed filesystem notifications. citeturn287044search0turn287044search5

The current ransomware response therefore uses conservative thresholds and recent-process correlation rather than claiming kernel-level visibility. A future production engine would need stronger telemetry and recovery/snapshot strategy.

### Phase 5 — Maverick self-defense

The Core now has a first self-defense layer:

- Core is designed to run as **LocalService** instead of LocalSystem.
- The installer assigns a **restricted service SID**.
- Core binaries are installed under **Program Files\\Maverick\\Core** rather than a user-writable project directory.
- Core data and quarantine live under **ProgramData\\Maverick** with explicit ACLs.
- An integrity manifest records SHA-256 values for installed Core files.
- The Core checks its integrity periodically and records tamper events when a file changes or the manifest disappears.
- The desktop app polls the Core and shows whether the Core is online or offline.
- Windows Authenticode verification is provided as a release-time helper; production signing still requires a real code-signing certificate.

Windows services support per-service security configuration, including service SIDs and restricted service SIDs, which is why Maverick uses a dedicated service identity rather than giving the Core unrestricted access. citeturn962252search7turn962252search8

Maverick does **not** claim that this makes it immune to a compromised Windows kernel or administrator-level attacker. It is defense in depth.

## What is next

**Phase 4B** will add stronger behavior correlation and persistence/download signals, but only after this first protection slice is built and tested on Windows.

## Maverick VI

Maverick VI is the project's intended display typeface. The specimen artwork is a visual reference, not a finished WOFF2/TTF binary yet, so the repository does not pretend otherwise.

## The rule for Maverick

Build one phase at a time.

No pretend scans. No fake detections. No invented security claims. If something is experimental, incomplete, or inventory-only, Maverick says so.
