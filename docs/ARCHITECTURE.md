# Maverick Architecture

## Phase 0 boundary

Phase 0 defines the boundaries between the desktop UI, security core, AI layer, and local data. It does **not** implement antivirus behavior yet.

## System layout

```
                    MAVERICK
                       │
             ┌─────────┴─────────┐
             │                   │
      Maverick Security      Maverick AI
             │                   │
             └─────────┬─────────┘
                       │
                 Maverick Core
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
       Scanner     Monitor       Journal
          │            │            │
          └────────────┴────────────┘
                       │
                 Windows APIs
```

## Technology boundary

### Desktop shell
- Electron **37.2.6**
- React + TypeScript for the interface (Phase 1)

### Security core
- Native Windows service
- C#/.NET
- Windows APIs
- The core owns protection decisions; the UI does not.

### Data
- SQLite/local files
- Local journal and application state remain on-device.
- Secrets/API credentials must use Windows-protected storage rather than plain-text files.

### IPC
Electron communicates with the native security core through a narrow, authenticated IPC boundary. The browser/UI process must not receive arbitrary native execution capabilities.

### AI
The AI layer is an adapter over user-selected BYOK providers. The AI can explain and investigate structured security data, while protection remains independent of model availability.

## Two-app model

The two applications share the same Maverick Core and security data contracts:

```
Maverick Core
├── Maverick Security UI
└── Maverick AI UI
```

The AI edition adds conversation/investigation capabilities; it does not replace the protection engine.

## Phase boundaries

### Phase 0 — Architecture
- Repository structure
- Technology decisions
- Security/data boundaries
- Build foundations

### Phase 1 — Maverick AI shell
- Minimal 16:9 desktop UI
- Maverick VI typography
- Sidebar
- Chat interface
- BYOK provider support
- Model browsing and free-model filtering
- Local persistence

### Later phases
- Native security core
- Real-time monitoring
- Quarantine/containment
- Investigation engine
- Windows/Defender integration
- Testing, packaging, signing, release
