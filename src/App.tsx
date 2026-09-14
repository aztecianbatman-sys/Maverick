import { useEffect, useMemo, useState } from "react";
import {
  Activity,
  Bot,
  ChevronDown,
  CircleCheck,
  Clock3,
  ExternalLink,
  FileText,
  Menu,
  MessageSquare,
  Plus,
  RefreshCw,
  Search,
  Send,
  Settings,
  ShieldCheck,
  SlidersHorizontal,
  Sparkles,
  Trash2,
  X,
} from "lucide-react";

type Panel = "none" | "settings" | "models" | "activity";
const HACKER_CODE = "MAVERICK-LUNAR-TEST";

const uid = () => crypto.randomUUID();

function createChat(): MaverickChat {
  const now = Date.now();
  return { id: uid(), title: "New conversation", createdAt: now, updatedAt: now, messages: [] };
}

function titleFromMessage(content: string) {
  const cleaned = content.replace(/\s+/g, " ").trim();
  return cleaned.length > 34 ? cleaned.slice(0, 34) + "…" : cleaned || "New conversation";
}

export default function App() {
  const [store, setStore] = useState<MaverickStore | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [panel, setPanel] = useState<Panel>("none");
  const [composer, setComposer] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [modelSearch, setModelSearch] = useState("");
  const [modelFilter, setModelFilter] = useState<"all" | "free">("all");
  const [apiKeyDraft, setApiKeyDraft] = useState("");
  const [providerDraft, setProviderDraft] = useState("openrouter");
  const [baseUrlDraft, setBaseUrlDraft] = useState("https://openrouter.ai/api/v1");
  const [modelDraft, setModelDraft] = useState("");
  const [models, setModels] = useState<MaverickModel[]>([]);
  const [modelsBusy, setModelsBusy] = useState(false);
  const [settingsSaved, setSettingsSaved] = useState(false);

  function openHackerScreen() {
    setHackerScreen(true);
    try {
      const speech = new SpeechSynthesisUtterance("Hackers not allowed. Maverick has opened the developer security test screen.");
      speech.rate = 0.92;
      speech.pitch = 0.72;
      window.speechSynthesis.cancel();
      window.speechSynthesis.speak(speech);
    } catch {
      // Text screen remains usable when speech synthesis is unavailable.
    }
  }

  function closeHackerScreen() {
    setHackerScreen(false);
    try { window.speechSynthesis.cancel(); } catch {}
  }
  const [securityEvents, setSecurityEvents] = useState<MaverickSecurityEvent[]>([]);
  const [journalBusy, setJournalBusy] = useState(false);
  const [coreState, setCoreState] = useState<"checking" | "online" | "offline">("checking");
  const [hackerScreen, setHackerScreen] = useState(false);

  const activeChat = useMemo(() => store?.chats.find((c) => c.id === store.activeChatId) ?? null, [store]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const key = event.key.length === 1 ? event.key.toUpperCase() : "";
      if (!key) return;
      const current = ((window as typeof window & { __maverickCode?: string }).__maverickCode || "") + key;
      (window as typeof window & { __maverickCode?: string }).__maverickCode = current.slice(-HACKER_CODE.length);
      if ((window as typeof window & { __maverickCode?: string }).__maverickCode === HACKER_CODE) {
        openHackerScreen();
        (window as typeof window & { __maverickCode?: string }).__maverickCode = "";
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  useEffect(() => {
    let mounted = true;
    const checkCore = async () => {
      try {
        await window.maverick.coreRequest("status");
        if (mounted) setCoreState("online");
      } catch {
        if (mounted) setCoreState("offline");
      }
    };
    void checkCore();
    const timer = window.setInterval(checkCore, 15000);
    window.maverick.getStore().then((loaded) => {
      let normalized = loaded;
      if (loaded.chats.length === 0) {
        const first = createChat();
        normalized = { ...loaded, chats: [first], activeChatId: first.id };
        window.maverick.saveStore({ chats: normalized.chats, activeChatId: normalized.activeChatId });
      }
      setStore(normalized);
      setProviderDraft(loaded.settings.provider);
      setBaseUrlDraft(loaded.settings.baseUrl);
      setModelDraft(loaded.settings.model);
      window.maverick.cachedModels(loaded.settings.provider).then(setModels);
      window.maverick.configureDefaultMonitoring().catch(() => undefined);
    });
    return () => {
      mounted = false;
      window.clearInterval(timer);
    };
  }, []);

  async function persist(next: MaverickStore) {
    setStore(next);
    await window.maverick.saveStore({ chats: next.chats, activeChatId: next.activeChatId, activity: next.activity });
  }

  function isSecurityQuestion(text: string) {
    return /security|threat|malware|virus|device activity|security activity|recent events?|what happened to my device|what happened today|anything suspicious/i.test(text);
  }

  async function loadJournal() {
    setJournalBusy(true);
    try {
      const result = await window.maverick.coreRequest("journal.recent", { limit: 200 });
      setSecurityEvents(Array.isArray(result) ? result as MaverickSecurityEvent[] : []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Maverick Core is unavailable.");
    } finally {
      setJournalBusy(false);
    }
  }

  async function sendMessage() {
    if (!store || !activeChat || busy) return;
    const content = composer.trim();
    if (!content) return;
    if (content.toUpperCase().includes(HACKER_CODE)) {
      setComposer("");
      openHackerScreen();
      return;
    }

    setError("");
    setBusy(true);
    const userMessage: MaverickMessage = { id: uid(), role: "user", content, createdAt: Date.now() };
    const nextMessages = [...activeChat.messages, userMessage];
    const title = activeChat.messages.length === 0 ? titleFromMessage(content) : activeChat.title;
    const stagedChat = { ...activeChat, title, messages: nextMessages, updatedAt: Date.now() };
    const stagedStore = {
      ...store,
      chats: store.chats.map((chat) => chat.id === activeChat.id ? stagedChat : chat),
    };
    setComposer("");
    await persist(stagedStore);

    try {
      let messagesForProvider = nextMessages.map((m) => ({ role: m.role, content: m.content }));
      if (isSecurityQuestion(content)) {
        const result = await window.maverick.coreRequest("journal.today", { limit: 200 });
        const events = Array.isArray(result) ? result as MaverickSecurityEvent[] : [];
        setSecurityEvents(events);
        const journalContext = [
          "Maverick local security journal for today.",
          "Use this data as evidence. Do not invent events, verdicts, or actions.",
          "The journal is local device data supplied by the user to the selected BYOK provider.",
          JSON.stringify(events)
        ].join("\n");
        messagesForProvider = [
          { role: "system", content: journalContext },
          ...messagesForProvider
        ];
      }

      const response = await window.maverick.sendChat({
        model: stagedStore.settings.model || modelDraft,
        messages: messagesForProvider,
      });
      const assistantMessage: MaverickMessage = {
        id: uid(),
        role: "assistant",
        content: response,
        createdAt: Date.now(),
      };
      const finalChat = { ...stagedChat, messages: [...nextMessages, assistantMessage], updatedAt: Date.now() };
      const activity: MaverickActivity = {
        id: uid(),
        type: "ai",
        summary: `AI response generated with ${stagedStore.settings.model || modelDraft || "selected model"}`,
        createdAt: Date.now(),
      };
      await persist({
        ...stagedStore,
        chats: stagedStore.chats.map((chat) => chat.id === activeChat.id ? finalChat : chat),
        activity: [activity, ...(stagedStore.activity || [])].slice(0, 500),
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "The AI request failed.");
    } finally {
      setBusy(false);
    }
  }

  function newChat() {
    if (!store) return;
    const chat = createChat();
    const next = { ...store, chats: [chat, ...store.chats], activeChatId: chat.id };
    setError("");
    persist(next);
    setSidebarOpen(false);
  }

  function chooseChat(id: string) {
    if (!store) return;
    setStore({ ...store, activeChatId: id });
    window.maverick.saveStore({ activeChatId: id });
    setSidebarOpen(false);
  }

  async function loadModels() {
    setModelsBusy(true);
    setError("");
    try {
      const result = await window.maverick.listModels({ provider: providerDraft, baseUrl: baseUrlDraft });
      setModels(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load models.");
    } finally {
      setModelsBusy(false);
    }
  }

  async function saveSettings() {
    setError("");
    try {
      const settingsInput: { provider: string; baseUrl: string; model: string; apiKey?: string } = {
        provider: providerDraft,
        baseUrl: baseUrlDraft,
        model: modelDraft,
      };
      if (apiKeyDraft.trim()) settingsInput.apiKey = apiKeyDraft.trim();
      const saved = await window.maverick.saveSettings(settingsInput);
      if (store) setStore({ ...store, settings: saved });
      setApiKeyDraft("");
      setSettingsSaved(true);
      setTimeout(() => setSettingsSaved(false), 2200);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save settings.");
    }
  }

  async function testApi() {
    setError("");
    try {
      const result = await window.maverick.testApi({
        provider: providerDraft,
        baseUrl: baseUrlDraft,
        apiKey: apiKeyDraft || undefined,
      });
      setError(`Connection verified — ${result.modelCount} models available.`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Connection failed.");
    }
  }

  if (hackerScreen) {
    return (
      <div className="hacker-screen">
        <div className="hacker-symbol" aria-hidden="true">⚠</div>
        <div className="hacker-title">HACKERS NOT ALLOWED</div>
        <div className="hacker-rule" />
        <p className="hacker-copy">
          Maverick entered its developer security lock screen because the test trigger was detected.
          This is a test flow only. It does not mean a real attacker was identified.
        </p>
        <div className="hacker-reason">
          <span>DEVELOPER TRIGGER</span>
          <strong>{HACKER_CODE}</strong>
          <small>No system files were changed.</small>
        </div>
        <button className="hacker-button" onClick={closeHackerScreen}>Return to Maverick</button>
      </div>
    );
  }

  if (!store) {
    return <div className="boot">Starting Maverick…</div>;
  }

  const freeCount = models.filter((model) => model.free).length;
  const filteredModels = models
    .filter((model) => model.name.toLowerCase().includes(modelSearch.toLowerCase()) || model.id.toLowerCase().includes(modelSearch.toLowerCase()))
    .filter((model) => modelFilter === "all" || model.free);

  return (
    <div className="app-shell">
      {sidebarOpen && <button className="scrim" aria-label="Close sidebar" onClick={() => setSidebarOpen(false)} />}
      <aside className={`sidebar ${sidebarOpen ? "open" : ""}`}>
        <div className="sidebar-top">
          <div className="brand-row"><img src="/assets/maverick-icon.svg" alt="" /><span>Maverick</span></div>
          <button className="icon-button" onClick={() => setSidebarOpen(false)} aria-label="Close"><X size={18}/></button>
        </div>
        <button className="new-chat" onClick={newChat}><Plus size={17}/> New chat</button>
        <div className="sidebar-section">
          <div className="section-label">Conversations</div>
          {store.chats.slice(0, 12).map((chat) => (
            <button key={chat.id} className={`chat-row ${chat.id === store.activeChatId ? "selected" : ""}`} onClick={() => chooseChat(chat.id)}>
              <MessageSquare size={15}/><span>{chat.title}</span>
            </button>
          ))}
        </div>
        <div className="sidebar-bottom">
          <button onClick={() => { setPanel("activity"); void loadJournal(); }}><Activity size={16}/> Activity</button>
          <button onClick={() => setPanel("settings")}><Settings size={16}/> API & settings</button>
        </div>
      </aside>

      <main className="main">
        <header className="topbar">
          <button className="icon-button menu-button" aria-label="Open sidebar" onClick={() => setSidebarOpen(true)}><Menu size={21}/></button>
          <div className="topbar-title">Maverick</div>
          <div className="topbar-right">
            <button className="status-pill" onClick={() => setPanel("activity")} title="Open activity">
              <span className={`status-dot ${coreState === "offline" ? "offline" : ""}`} /> {coreState === "online" ? "Protected" : coreState === "offline" ? "Core offline" : "Checking"}
            </button>
            <button className="status-pill subtle" onClick={() => setPanel("settings")}>
              {store.settings.hasApiKey ? store.settings.model || "Choose model" : "Set up AI"} <ChevronDown size={14}/>
            </button>
          </div>
        </header>

        <section className={`conversation ${activeChat?.messages.length ? "has-messages" : ""}`}>
          {activeChat?.messages.length ? (
            <div className="messages">
              {activeChat.messages.map((message) => (
                <div key={message.id} className={`message ${message.role}`}>
                  <div className="message-role">{message.role === "user" ? "You" : "Maverick"}</div>
                  <div className="message-body">{message.content}</div>
                </div>
              ))}
              {busy && <div className="message assistant"><div className="message-role">Maverick</div><div className="typing"><span/><span/><span/></div></div>}
            </div>
          ) : (
            <div className="welcome">
              <img className="hero-logo" src="/assets/maverick-icon.png" alt="Maverick" />
              <div className="hello">HELLO!</div>
              <p>I'M MAVERICK.</p>
              <span className="welcome-note">Ask about your device, security, or anything else.</span>
            </div>
          )}
        </section>

        <div className="composer-wrap">
          {error && <div className="notice"><span>{error}</span><button onClick={() => setError("")}><X size={14}/></button></div>}
          <div className="composer">
            <button className="composer-action" onClick={() => setPanel("models")} title="Browse models"><SlidersHorizontal size={17}/></button>
            <textarea
              value={composer}
              onChange={(event) => setComposer(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && !event.shiftKey) {
                  event.preventDefault();
                  sendMessage();
                }
              }}
              placeholder="Ask Maverick…"
              rows={1}
              disabled={busy}
            />
            <button className="send-button" onClick={sendMessage} disabled={busy || !composer.trim()} aria-label="Send"><Send size={17}/></button>
          </div>
          <div className="composer-meta">
            <span>{store.settings.hasApiKey ? "BYOK • Local settings" : "Add a BYOK key to start chatting"}</span>
            <button onClick={() => setPanel("models")}>{store.settings.model || "Choose model"} <ChevronDown size={13}/></button>
          </div>
        </div>
      </main>

      {panel !== "none" && (
        <section className="drawer">
          <div className="drawer-head">
            <div>
              <div className="drawer-kicker">{panel === "models" ? "MODEL BROWSER" : panel === "settings" ? "MAVERICK AI" : "LOCAL JOURNAL"}</div>
              <h2>{panel === "models" ? "Choose a model" : panel === "settings" ? "API & settings" : "Activity"}</h2>
            </div>
            <button className="icon-button" onClick={() => setPanel("none")}><X size={18}/></button>
          </div>

          {panel === "settings" && (
            <div className="drawer-content">
              <div className="field"><label>Provider</label><select value={providerDraft} onChange={(e) => setProviderDraft(e.target.value)}><option value="openrouter">OpenRouter</option><option value="custom">Custom OpenAI-compatible</option></select></div>
              <div className="field"><label>API base URL</label><input value={baseUrlDraft} onChange={(e) => setBaseUrlDraft(e.target.value)} /></div>
              <div className="field"><label>API key</label><input type="password" placeholder={store.settings.hasApiKey ? "Saved securely on this device" : "Paste your provider key"} value={apiKeyDraft} onChange={(e) => setApiKeyDraft(e.target.value)} /></div>
              <div className="settings-actions"><button onClick={testApi}>Test connection</button><button className="primary" onClick={saveSettings}>Save settings</button></div>
              {settingsSaved && <div className="success-line"><CircleCheck size={16}/> Saved locally.</div>}
              <div className="privacy-card"><ShieldCheck size={18}/><div><strong>Your key stays local.</strong><span>Maverick stores it using the operating system's protected local storage. The renderer never receives the saved key.</span></div></div>
              <button className="link-button" onClick={() => setPanel("models")}>Browse models <ExternalLink size={14}/></button>
            </div>
          )}

          {panel === "models" && (
            <div className="drawer-content models-panel">
              <div className="model-toolbar">
                <div className="search"><Search size={15}/><input placeholder="Search models" value={modelSearch} onChange={(e) => setModelSearch(e.target.value)} /></div>
                <button className={modelFilter === "all" ? "filter active" : "filter"} onClick={() => setModelFilter("all")}>All</button>
                <button className={modelFilter === "free" ? "filter active free" : "filter"} onClick={() => setModelFilter("free")}>Free {freeCount ? `(${freeCount})` : ""}</button>
                <button className="refresh" onClick={loadModels} disabled={modelsBusy} title="Refresh models"><RefreshCw size={15} className={modelsBusy ? "spin" : ""}/></button>
              </div>
              {!store.settings.hasApiKey && <div className="empty-models"><Sparkles size={18}/><span>Add an API key in settings to browse provider models.</span><button onClick={() => setPanel("settings")}>Set up key</button></div>}
              {store.settings.hasApiKey && models.length === 0 && !modelsBusy && <div className="empty-models"><Clock3 size={18}/><span>No cached models yet.</span><button onClick={loadModels}>Load models</button></div>}
              <div className="model-list">
                {filteredModels.map((model) => (
                  <button key={model.id} className={`model-card ${modelDraft === model.id ? "selected" : ""}`} onClick={async () => {
                    setModelDraft(model.id);
                    const saved = await window.maverick.saveSettings({ model: model.id });
                    setStore({ ...store, settings: saved });
                  }}>
                    <div className="model-title"><span>{model.name}</span>{model.free && <span className="free-badge">FREE</span>}</div>
                    <div className="model-id">{model.id}</div>
                    {model.contextLength > 0 && <div className="model-meta">Context {model.contextLength.toLocaleString()}</div>}
                  </button>
                ))}
              </div>
            </div>
          )}

          {panel === "activity" && (
            <div className="drawer-content">
              <div className="journal-toolbar"><span>{journalBusy ? "Reading Core journal…" : securityEvents.length + " events loaded"}</span><button className="refresh" onClick={loadJournal} disabled={journalBusy} title="Refresh journal"><RefreshCw size={15} className={journalBusy ? "spin" : ""}/></button></div>
              {securityEvents.length ? securityEvents.slice(0, 100).map((item) => (
                <div className="activity-row" key={item.id}>
                  <div className="activity-icon"><Activity size={15}/></div>
                  <div>
                    <strong>{item.type}{item.risk ? " · " + item.risk : ""}</strong>
                    <span>{item.summary}</span>
                    <small>{item.process || item.file || item.source || "Maverick Core"} · {new Date(item.createdUtc).toLocaleString()}</small>
                  </div>
                </div>
              )) : <div className="empty-models"><Clock3 size={18}/><span>No Core events recorded yet.</span></div>}
              <button className="danger-button" onClick={async () => {
                const next = { ...store, activity: [] };
                await persist(next);
                setSecurityEvents([]);
              }}><Trash2 size={15}/> Delete AI activity</button>
            </div>
          )}
        </section>
      )}
    </div>
  );
}
