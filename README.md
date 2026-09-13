# Maverick

Maverick is a Windows desktop security project built around one idea: security software should be serious without being miserable to use.

There are two planned desktop experiences:

**Maverick Security** is the protection-first app.

**Maverick AI** uses the same Maverick security core, then adds local activity history, investigations, and a BYOK chat interface so an AI model can help explain what is happening on the device.

## Where the project is right now

Phase 1 is the first working desktop shell:

- Electron 37.2.6
- React + TypeScript + Vite
- Minimal 16:9 dark interface
- Collapsible sidebar and local conversation history
- BYOK provider settings
- OpenAI-compatible API support
- Provider model browsing
- Separate **Free** model filtering based on provider pricing metadata
- Local model cache, chat history, and AI activity log
- OS-protected storage for the saved API key
- No fake AI replies and no fake security detections

The actual security engine is deliberately **not** part of this phase yet. That comes after the interface and AI boundary are stable.

## Running it

Install Node.js, then run:

```bash
npm install
npm run dev
```

For the production renderer build:

```bash
npm run build
```

## API setup

Open **API & settings** in Maverick AI.

Choose **OpenRouter** for the built-in model browser, or **Custom OpenAI-compatible** for another compatible endpoint.

Paste your own key, test the connection, refresh the model list, and choose a model. Maverick stores the saved key locally using Electron's OS-backed secret storage; the renderer never receives the stored key.

For providers that expose zero-cost pricing, Maverick marks models as **FREE**. It does not invent a free label when the provider does not supply enough pricing information.

## Local data

Phase 1 keeps conversations, the AI activity log, cached model data, and application settings on the device. There is no Maverick cloud account in this version.

The local store is written under Electron's application data directory.

## Project shape

```
Maverick/
├── app/          # future shared app material
├── ai/           # AI adapters and contracts
├── assets/       # branding/assets
├── core/         # future native Windows security engine
├── data/         # local-data boundary
├── electron/     # Electron main process + IPC
├── src/          # Maverick AI renderer
└── docs/         # architecture notes
```

## Maverick VI

The Maverick VI artwork we designed is currently a **font specimen/reference**, not a real WOFF2/TTF font binary. Phase 1 keeps the display font role reserved for Maverick VI so the finished font can be dropped in without changing the UI, but the project does not pretend a font file exists yet.

## The rule for Maverick

Build one phase at a time.

No pretend scans. No fake detections. No invented security claims. The chat can be incomplete; the security engine can be incomplete; the product should never lie about either.
