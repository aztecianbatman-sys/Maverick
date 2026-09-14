import React, { useEffect, useMemo, useState } from 'react';
import {
  Activity, AlertTriangle, Bot, CheckCircle2, ChevronRight, Cpu, FileSearch,
  FolderOpen, HardDrive, LayoutDashboard, Menu, RefreshCw, Search, Send,
  Settings, ShieldAlert, ShieldCheck, X, Play, ListChecks, LockKeyhole, Plus
} from 'lucide-react';

type Page = 'overview' | 'scan' | 'activity' | 'processes' | 'startup' | 'quarantine' | 'ai' | 'settings';
const HACKER_CODE = 'MAVERICK-LUNAR-TEST';
const uid = () => crypto.randomUUID();

function newChat(): MaverickChat {
  const now = Date.now();
  return { id: uid(), title: 'New conversation', createdAt: now, updatedAt: now, messages: [] };
}

function compactTitle(value: string) {
  const clean = value.replace(/\s+/g, ' ').trim();
  return clean.length > 34 ? clean.slice(0, 34) + '…' : clean || 'New conversation';
}

function timeLabel(value: string | number) {
  return new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
}

export default function App() {
  const [store, setStore] = useState<MaverickStore | null>(null);
  const [page, setPage] = useState<Page>('overview');
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [coreState, setCoreState] = useState<'checking' | 'online' | 'offline'>('checking');
  const [events, setEvents] = useState<MaverickSecurityEvent[]>([]);
  const [processes, setProcesses] = useState<Array<{ pid: number; name: string; path: string | null; sessionId: number }>>([]);
  const [startup, setStartup] = useState<Array<{ location: string; name: string; value: string }>>([]);
  const [quarantine, setQuarantine] = useState<Array<{ path: string; name: string; sizeBytes: number; createdUtc: string }>>([]);
  const [scanPath, setScanPath] = useState('');
  const [scanBusy, setScanBusy] = useState(false);
  const [scanResult, setScanResult] = useState<any>(null);
  const [composer, setComposer] = useState('');
  const [busy, setBusy] = useState(false);
  const [hackerScreen, setHackerScreen] = useState(false);
  const [models, setModels] = useState<MaverickModel[]>([]);
  const [modelsBusy, setModelsBusy] = useState(false);
  const [modelSearch, setModelSearch] = useState('');
  const [modelFilter, setModelFilter] = useState<'all' | 'free'>('all');
  const [apiKeyDraft, setApiKeyDraft] = useState('');
  const [providerDraft, setProviderDraft] = useState('openrouter');
  const [baseUrlDraft, setBaseUrlDraft] = useState('https://openrouter.ai/api/v1');
  const [modelDraft, setModelDraft] = useState('');
  const [settingsSaved, setSettingsSaved] = useState(false);
  const [error, setError] = useState('');

  const activeChat = useMemo(() => {
    return store?.chats.find((chat) => chat.id === store.activeChatId) ?? null;
  }, [store]);

  const highRisk = events.filter((event) => (event.risk || '').toLowerCase() === 'high').length;
  const mediumRisk = events.filter((event) => (event.risk || '').toLowerCase() === 'medium').length;

  useEffect(() => {
    let mounted = true;
    async function boot() {
      try {
        const loaded = await window.maverick.getStore();
        let next = loaded;
        if (loaded.chats.length === 0) {
          const chat = newChat();
          next = { ...loaded, chats: [chat], activeChatId: chat.id };
          await window.maverick.saveStore({ chats: next.chats, activeChatId: next.activeChatId });
        }
        if (!mounted) return;
        setStore(next);
        setProviderDraft(next.settings.provider);
        setBaseUrlDraft(next.settings.baseUrl);
        setModelDraft(next.settings.model);
        setModels(await window.maverick.cachedModels(next.settings.provider));
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Maverick could not start.');
      }
    }
    void boot();
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    const checkCore = async () => {
      try {
        await window.maverick.coreRequest('status');
        setCoreState('online');
      } catch {
        setCoreState('offline');
      }
    };
    void checkCore();
    const timer = window.setInterval(checkCore, 10000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key.length !== 1) return;
      const target = event.target as HTMLElement | null;
      if (target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA') return;
      const bag = ((window as typeof window & { __maverickCode?: string }).__maverickCode || '') + event.key.toUpperCase();
      (window as typeof window & { __maverickCode?: string }).__maverickCode = bag.slice(-HACKER_CODE.length);
      if ((window as typeof window & { __maverickCode?: string }).__maverickCode === HACKER_CODE) {
        setHackerScreen(true);
        try {
          const speech = new SpeechSynthesisUtterance('Hackers not allowed. Maverick developer security screen activated.');
          speech.rate = 0.92;
          speech.pitch = 0.78;
          window.speechSynthesis.cancel();
          window.speechSynthesis.speak(speech);
        } catch {}
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  async function refreshJournal() {
    try {
      const result = await window.maverick.coreRequest('journal.recent', { limit: 300 });
      setEvents(Array.isArray(result) ? result as MaverickSecurityEvent[] : []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Maverick Core is unavailable.');
    }
  }

  async function refreshProcesses() {
    try {
      const result = await window.maverick.coreRequest('processes.list');
      setProcesses(Array.isArray(result) ? result as typeof processes : []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not read running processes.');
    }
  }

  async function refreshStartup() {
    try {
      const result = await window.maverick.coreRequest('startup.list');
      setStartup(Array.isArray(result) ? result as typeof startup : []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not inspect startup locations.');
    }
  }

  async function refreshQuarantine() {
    try {
      const result = await window.maverick.coreRequest('quarantine.list');
      setQuarantine(Array.isArray(result) ? result as typeof quarantine : []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not read quarantine.');
    }
  }

  async function pickFolder() {
    try {
      const selected = await window.maverick.pickFolder();
      if (selected) setScanPath(selected);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Folder picker failed.');
    }
  }

  async function runScan() {
    if (!scanPath.trim() || scanBusy) return;
    setScanBusy(true);
    setScanResult(null);
    try {
      const result = await window.maverick.coreRequest('scan', { path: scanPath.trim() });
      setScanResult(result);
      await refreshJournal();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Scan failed.');
    } finally {
      setScanBusy(false);
    }
  }

  async function saveStore(next: MaverickStore) {
    setStore(next);
    await window.maverick.saveStore({
      chats: next.chats,
      activeChatId: next.activeChatId,
      activity: next.activity
    });
  }

  async function sendMessage() {
    if (!store || !activeChat || busy) return;
    const content = composer.trim();
    if (!content) return;
    if (content.toUpperCase().includes(HACKER_CODE)) {
      setComposer('');
      setHackerScreen(true);
      try {
        const speech = new SpeechSynthesisUtterance('Hackers not allowed. Maverick developer security screen activated.');
        speech.rate = 0.92;
        speech.pitch = 0.78;
        window.speechSynthesis.cancel();
        window.speechSynthesis.speak(speech);
      } catch {}
      return;
    }

    setBusy(true);
    setError('');
    const userMessage: MaverickMessage = { id: uid(), role: 'user', content, createdAt: Date.now() };
    const messages = [...activeChat.messages, userMessage];
    const stagedChat = {
      ...activeChat,
      title: activeChat.messages.length === 0 ? compactTitle(content) : activeChat.title,
      messages,
      updatedAt: Date.now()
    };
    const stagedStore = { ...store, chats: store.chats.map((chat) => chat.id === activeChat.id ? stagedChat : chat) };
    setComposer('');
    await saveStore(stagedStore);

    try {
      let providerMessages = messages.map((message) => ({ role: message.role, content: message.content }));
      if (/security|threat|malware|virus|suspicious|activity|scan|device/i.test(content)) {
        const result = await window.maverick.coreRequest('journal.today', { limit: 200 });
        const localEvents = Array.isArray(result) ? result as MaverickSecurityEvent[] : [];
        setEvents(localEvents);
        providerMessages = [
          { role: 'system', content: 'You are Maverick AI. Security decisions belong to Maverick Core. Use only the supplied local journal as evidence. Never invent detections, events, or remediation.' },
          { role: 'system', content: 'Local security journal:\\n' + JSON.stringify(localEvents) },
          ...providerMessages
        ];
      }
      const response = await window.maverick.sendChat({
        model: store.settings.model || modelDraft,
        messages: providerMessages
      });
      const assistant: MaverickMessage = { id: uid(), role: 'assistant', content: response, createdAt: Date.now() };
      const finalChat = { ...stagedChat, messages: [...messages, assistant], updatedAt: Date.now() };
      await saveStore({
        ...stagedStore,
        chats: stagedStore.chats.map((chat) => chat.id === activeChat.id ? finalChat : chat),
        activity: [{ id: uid(), type: 'ai', summary: 'Maverick AI response generated', createdAt: Date.now() }, ...(store.activity || [])].slice(0, 500)
      });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'AI request failed.');
    } finally {
      setBusy(false);
    }
  }

  async function loadModels() {
    setModelsBusy(true);
    setError('');
    try {
      setModels(await window.maverick.listModels({ provider: providerDraft, baseUrl: baseUrlDraft }));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load models.');
    } finally {
      setModelsBusy(false);
    }
  }

  async function saveSettings() {
    setError('');
    try {
      const input: { provider: string; baseUrl: string; model: string; apiKey?: string } = {
        provider: providerDraft,
        baseUrl: baseUrlDraft,
        model: modelDraft
      };
      if (apiKeyDraft.trim()) input.apiKey = apiKeyDraft.trim();
      const settings = await window.maverick.saveSettings(input);
      setStore(store ? { ...store, settings } : store);
      setApiKeyDraft('');
      setSettingsSaved(true);
      window.setTimeout(() => setSettingsSaved(false), 1800);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not save settings.');
    }
  }

  function navigate(next: Page) {
    setPage(next);
    if (next === 'overview' || next === 'activity') void refreshJournal();
    if (next === 'processes') void refreshProcesses();
    if (next === 'startup') void refreshStartup();
    if (next === 'quarantine') void refreshQuarantine();
  }

  if (!store) return <div className="boot-screen"><div className="boot-mark">M</div><span>Starting Maverick…</span></div>;

  if (hackerScreen) {
    return <div className="hacker-screen">
      <div className="hacker-symbol">⚠</div>
      <div className="hacker-title">HACKERS NOT ALLOWED</div>
      <div className="hacker-rule"></div>
      <p>Maverick entered its developer security lock screen because the test trigger was detected. This does not represent a real intrusion verdict.</p>
      <div className="hacker-reason"><span>DEVELOPER TRIGGER</span><strong>{HACKER_CODE}</strong><small>No security components were changed.</small></div>
      <button className="hacker-button" onClick={() => { setHackerScreen(false); try { window.speechSynthesis.cancel(); } catch {} }}>Return to Maverick</button>
    </div>;
  }

  return <div className="crm-shell">
    <aside className={'crm-sidebar ' + (sidebarOpen ? 'open' : 'collapsed')}>
      <div className="brand">
        <img src="/assets/maverick-icon.svg" alt="" />
        {sidebarOpen && <div><strong>Maverick</strong><span>Device security</span></div>}
      </div>
      <button className="sidebar-toggle" onClick={() => setSidebarOpen(!sidebarOpen)} aria-label="Toggle sidebar"><Menu size={19}/></button>
      <nav className="nav">
        <NavItem icon={<LayoutDashboard size={18}/>} label="Overview" active={page === 'overview'} open={sidebarOpen} onClick={() => navigate('overview')} />
        <NavItem icon={<FileSearch size={18}/>} label="Scan" active={page === 'scan'} open={sidebarOpen} onClick={() => navigate('scan')} />
        <NavItem icon={<Activity size={18}/>} label="Activity" active={page === 'activity'} open={sidebarOpen} onClick={() => navigate('activity')} />
        <div className="nav-label">{sidebarOpen && 'DEVICE'}</div>
        <NavItem icon={<Cpu size={18}/>} label="Processes" active={page === 'processes'} open={sidebarOpen} onClick={() => navigate('processes')} />
        <NavItem icon={<ListChecks size={18}/>} label="Startup" active={page === 'startup'} open={sidebarOpen} onClick={() => navigate('startup')} />
        <NavItem icon={<HardDrive size={18}/>} label="Quarantine" active={page === 'quarantine'} open={sidebarOpen} onClick={() => navigate('quarantine')} />
        <div className="nav-label">{sidebarOpen && 'MAVERICK AI'}</div>
        <NavItem icon={<Bot size={18}/>} label="AI assistant" active={page === 'ai'} open={sidebarOpen} onClick={() => navigate('ai')} />
      </nav>
      <div className="sidebar-footer">
        <NavItem icon={<Settings size={18}/>} label="Settings" active={page === 'settings'} open={sidebarOpen} onClick={() => navigate('settings')} />
        {sidebarOpen && <div className="build-chip">Base · 0.5.1</div>}
      </div>
    </aside>

    <section className="crm-main">
      <header className="crm-topbar">
        <div className="crumb"><span>Maverick</span><ChevronRight size={14}/><strong>{labelFor(page)}</strong></div>
        <div className="top-actions">
          <button className={'health ' + coreState} onClick={() => navigate('activity')}><span></span>{coreState === 'online' ? 'Protected' : coreState === 'offline' ? 'Core offline' : 'Checking'}</button>
          <button className="top-icon" title="Refresh security state" onClick={() => { void refreshJournal(); }}><RefreshCw size={17}/></button>
        </div>
      </header>

      <main className="page">
        {error && <div className="global-error"><AlertTriangle size={16}/><span>{error}</span><button onClick={() => setError('')}><X size={14}/></button></div>}

        {page === 'overview' && <Overview coreState={coreState} highRisk={highRisk} mediumRisk={mediumRisk} events={events.slice(0,6)} onScan={() => navigate('scan')} onActivity={() => navigate('activity')} />}
        {page === 'scan' && <ScanPage path={scanPath} setPath={setScanPath} busy={scanBusy} result={scanResult} onBrowse={pickFolder} onScan={runScan} />}
        {page === 'activity' && <ActivityPage events={events} highRisk={highRisk} mediumRisk={mediumRisk} onRefresh={refreshJournal} />}
        {page === 'processes' && <InventoryPage title="Running processes" subtitle="Read-only inventory from Maverick Core." icon={<Cpu size={22}/>} headers={['Process','PID','Location','Session']} rows={processes.map((p)=><[React.ReactNode,React.ReactNode,React.ReactNode,React.ReactNode]><>{<strong>{p.name}</strong>}<small>PID {p.pid}</small></>,p.pid,<span className="truncate">{p.path || 'Access denied / unavailable'}</span>,p.sessionId])} action={<button className="outline" onClick={() => void refreshProcesses()}><RefreshCw size={15}/> Refresh</button>} empty="No process inventory loaded." />}
        {page === 'startup' && <InventoryPage title="Startup locations" subtitle="Read-only inspection of Windows Run keys and startup folders." icon={<ListChecks size={22}/>} headers={['Entry','Location','Value','']} rows={startup.map((item,i)=><[React.ReactNode,React.ReactNode,React.ReactNode,React.ReactNode]><strong key={i}>{item.name}</strong>,item.location,<span className="truncate">{item.value}</span>,<ChevronRight size={16}/>) } action={<button className="outline" onClick={() => void refreshStartup()}><RefreshCw size={15}/> Refresh</button>} empty="No startup entries loaded." />}
        {page === 'quarantine' && <QuarantinePage items={quarantine} onRefresh={refreshQuarantine} />}
        {page === 'ai' && <AIPage store={store} activeChat={activeChat} composer={composer} setComposer={setComposer} busy={busy} modelDraft={modelDraft} onSend={sendMessage} onConfigure={() => navigate('settings')} highRisk={highRisk} events={events.length} coreState={coreState} />}
        {page === 'settings' && <SettingsPage store={store} provider={providerDraft} setProvider={setProviderDraft} baseUrl={baseUrlDraft} setBaseUrl={setBaseUrlDraft} key={apiKeyDraft} setKey={setApiKeyDraft} model={modelDraft} setModel={setModelDraft} models={models} busy={modelsBusy} search={modelSearch} setSearch={setModelSearch} filter={modelFilter} setFilter={setModelFilter} onLoad={loadModels} onSave={saveSettings} saved={settingsSaved} />}
      </main>
    </section>
  </div>;
}

function NavItem({icon,label,active,open,onClick}:{icon:React.ReactNode;label:string;active:boolean;open:boolean;onClick:()=>void}) {
  return <button className={'nav-item ' + (active ? 'active ' : '') + (open ? 'expanded' : '')} onClick={onClick} title={open ? '' : label}>{icon}{open && <span>{label}</span>}</button>;
}

function labelFor(page: Page) {
  return ({overview:'Overview',scan:'Scan',activity:'Activity',processes:'Processes',startup:'Startup',quarantine:'Quarantine',ai:'AI assistant',settings:'Settings'})[page];
}

function Heading({title,subtitle,icon,action}:{title:string;subtitle:string;icon:React.ReactNode;action?:React.ReactNode}) {
  return <div className="page-heading"><div><div className="heading-icon">{icon}</div><div><h1>{title}</h1><p>{subtitle}</p></div></div>{action}</div>;
}

function Overview({coreState,highRisk,mediumRisk,events,onScan,onActivity}:{coreState:string;highRisk:number;mediumRisk:number;events:MaverickSecurityEvent[];onScan:()=>void;onActivity:()=>void}) {
  return <section className="section-page">
    <div className="hero-row"><div><span className="eyebrow">DEVICE SECURITY</span><h1>Your device at a glance</h1><p>Maverick keeps protection, local activity, and investigations in one place.</p></div><button className="primary large" onClick={onScan}><FileSearch size={17}/> Run a scan</button></div>
    <div className="overview-grid">
      <div className="protection-card"><div className="protection-ring"><ShieldCheck size={34}/></div><div><span className="eyebrow">PROTECTION STATUS</span><h2>{coreState === 'online' ? 'Maverick is running' : 'Maverick Core needs attention'}</h2><p>{coreState === 'online' ? 'The native Core is online and able to monitor configured locations.' : 'Start Maverick Core to enable native protection features.'}</p></div><div className={'state-tag ' + coreState}>{coreState === 'online' ? 'Protected' : coreState === 'offline' ? 'Offline' : 'Checking'}</div></div>
      <MiniCard title="High-risk events" value={highRisk} icon={<ShieldAlert size={18}/>} tone={highRisk ? 'danger' : 'good'}/><MiniCard title="Medium-risk events" value={mediumRisk} icon={<AlertTriangle size={18}/>} tone={mediumRisk ? 'warn' : 'good'}/>
    </div>
    <div className="lower-grid"><div className="table-card"><div className="section-card-head"><div><strong>Recent security activity</strong><span>Latest events from Maverick Core</span></div><button className="link-btn" onClick={onActivity}>View all <ChevronRight size={15}/></button></div>{events.length ? events.map((event)=><div className="activity-list-row" key={event.id}><div className="event-dot"/><div><strong>{event.summary}</strong><span>{event.file || event.process || event.source || 'Maverick Core'}</span></div><RiskBadge value={event.risk || 'unknown'}/><small>{timeLabel(event.createdUtc)}</small></div>) : <Empty text="Nothing to report yet."/>}</div><div className="quick-card"><div className="card-title">Quick actions</div><button onClick={onScan}><FileSearch size={17}/><span>Run a scan</span><ChevronRight size={16}/></button><button onClick={onActivity}><Activity size={17}/><span>Review activity</span><ChevronRight size={16}/></button></div></div>
  </section>;
}

function ScanPage({path,setPath,busy,result,onBrowse,onScan}:{path:string;setPath:(value:string)=>void;busy:boolean;result:any;onBrowse:()=>void;onScan:()=>void}) {
  return <section className="section-page"><Heading title="Scan your device" subtitle="Use the native Maverick Core scanner. This is not an AI-generated result." icon={<FileSearch size={22}/>} />
    <div className="scan-card"><div className="scan-icon"><ShieldCheck size={28}/></div><div className="scan-copy"><h3>Choose a folder to scan</h3><p>Maverick will traverse the folder and calculate file hashes using the Core.</p><div className="scan-picker"><input value={path} onChange={(e)=>setPath(e.target.value)} placeholder="C:\\Users\\You\\Downloads"/><button onClick={onBrowse}><FolderOpen size={16}/> Browse</button><button className="primary" disabled={!path.trim() || busy} onClick={onScan}>{busy?<RefreshCw className="spin" size={16}/>:<Play size={16}/>} {busy?'Scanning…':'Start scan'}</button></div></div></div>
    {result&&<div className="result-card"><CheckCircle2 size={20}/><div><strong>Scan completed</strong><span>{result.inspected ?? 0} files inspected · {result.failed ?? 0} unavailable · mode: {result.verdict || 'inventory-only'}</span></div></div>}
  </section>;
}

function ActivityPage({events,highRisk,mediumRisk,onRefresh}:{events:MaverickSecurityEvent[];highRisk:number;mediumRisk:number;onRefresh:()=>void}) {
  return <section className="section-page"><Heading title="Activity" subtitle="A local timeline of what Maverick observes and does." icon={<Activity size={22}/>} action={<button className="outline" onClick={()=>void onRefresh()}><RefreshCw size={15}/> Refresh</button>}/><div className="stats-row"><Stat label="Events" value={events.length}/><Stat label="High risk" value={highRisk} tone={highRisk?'danger':'normal'}/><Stat label="Medium risk" value={mediumRisk} tone={mediumRisk?'warn':'normal'}/></div><div className="table-card"><div className="table-head"><span>Event</span><span>Process / file</span><span>Risk</span><span>Time</span></div>{events.length?events.map((event)=><div className="table-row" key={event.id}><div><strong>{event.summary}</strong><small>{event.type} · {event.source}</small></div><div className="truncate">{event.process||event.file||'—'}</div><RiskBadge value={event.risk||'unknown'}/><div className="muted">{timeLabel(event.createdUtc)}</div></div>):<Empty text="No Core security events recorded yet."/>}</div></section>;
}

function InventoryPage({title,subtitle,icon,headers,rows,action,empty}:{title:string;subtitle:string;icon:React.ReactNode;headers:string[];rows:React.ReactNode[][];action?:React.ReactNode;empty:string}) {
  return <section className="section-page"><Heading title={title} subtitle={subtitle} icon={icon} action={action}/><div className="table-card"><div className="table-head">{headers.map((header,i)=><span key={i}>{header}</span>)}</div>{rows.length?rows.map((row,i)=><div className="table-row" key={i}>{row.map((cell,j)=><div key={j} className={j>0?'truncate':''}>{cell}</div>)}</div>):<Empty text={empty}/>}</div></section>;
}

function QuarantinePage({items,onRefresh}:{items:Array<{path:string;name:string;sizeBytes:number;createdUtc:string}>;onRefresh:()=>void}) {
  return <section className="section-page"><Heading title="Quarantine" subtitle="Maverick isolates confirmed threat files here." icon={<LockKeyhole size={22}/>} action={<button className="outline" onClick={()=>void onRefresh()}><RefreshCw size={15}/> Refresh</button>}/><div className="info-banner"><ShieldAlert size={17}/><span>Only Maverick's quarantine directory is eligible for quarantine-item deletion.</span></div><div className="table-card"><div className="table-head"><span>Item</span><span>Size</span><span>Created</span><span></span></div>{items.length?items.map(item=><div className="table-row" key={item.path}><div><strong>{item.name}</strong><small>{item.path}</small></div><div>{formatBytes(item.sizeBytes)}</div><div>{timeLabel(item.createdUtc)}</div><div><ChevronRight size={16}/></div></div>):<Empty text="Quarantine is empty."/ >}</div></section>;
}

function AIPage({activeChat,composer,setComposer,busy,modelDraft,onSend,onConfigure,highRisk,events,coreState}:{store:MaverickStore;activeChat:MaverickChat|null;composer:string;setComposer:(value:string)=>void;busy:boolean;modelDraft:string;onSend:()=>void;onConfigure:()=>void;highRisk:number;events:number;coreState:string}) {
  return <section className="section-page"><Heading title="Maverick AI" subtitle="Optional BYOK assistant with access to local Core evidence for security questions." icon={<Bot size={22}/>} action={<button className="outline" onClick={onConfigure}><Settings size={15}/> Configure</button>}/><div className="ai-layout"><div className="chat-card"><div className="chat-toolbar"><span>{modelDraft || 'No model selected'}</span><span className="chat-local">Local journal · optional AI</span></div><div className="chat-messages">{activeChat?.messages.length?activeChat.messages.map((message)=><div className={'chat-message '+message.role} key={message.id}><div className="avatar">{message.role==='assistant'?<img src="/assets/maverick-icon.svg" alt=""/>:'Y'}</div><div><small>{message.role==='assistant'?'Maverick':'You'}</small><p>{message.content}</p></div></div>):<div className="chat-empty"><img src="/assets/maverick-icon.svg" alt="Maverick"/><h3>Hello, I'm Maverick.</h3><p>Ask about your device, security activity, or what an event means.</p></div>}{busy&&<div className="chat-message assistant"><div className="avatar"><img src="/assets/maverick-icon.svg" alt=""/></div><div><small>Maverick</small><div className="typing"><span/><span/><span/></div></div></div>}</div><div className="chat-compose"><textarea value={composer} onChange={(e)=>setComposer(e.target.value)} onKeyDown={(e)=>{if(e.key==='Enter'&&!e.shiftKey){e.preventDefault();onSend();}}} placeholder="Ask Maverick…" rows={1}/><button className="send-chat" disabled={!composer.trim()||busy} onClick={onSend}><Send size={17}/></button></div></div><div className="side-card"><div className="side-card-head"><strong>Security snapshot</strong><RefreshCw size={15}/></div><div className="mini-stat"><span>Core</span><b className={coreState==='online'?'good':'bad'}>{coreState==='online'?'Online':'Offline'}</b></div><div className="mini-stat"><span>High-risk events</span><b>{highRisk}</b></div><div className="mini-stat"><span>Journal events</span><b>{events}</b></div></div></div></section>;
}

function SettingsPage({store,provider,setProvider,baseUrl,setBaseUrl,key,setKey,model,setModel,models,busy,search,setSearch,filter,setFilter,onLoad,onSave,saved}:{store:MaverickStore;provider:string;setProvider:(v:string)=>void;baseUrl:string;setBaseUrl:(v:string)=>void;key:string;setKey:(v:string)=>void;model:string;setModel:(v:string)=>void;models:MaverickModel[];busy:boolean;search:string;setSearch:(v:string)=>void;filter:'all'|'free';setFilter:(v:'all'|'free')=>void;onLoad:()=>void;onSave:()=>void;saved:boolean}) {
  const filtered=models.filter((m)=>m.name.toLowerCase().includes(search.toLowerCase())||m.id.toLowerCase().includes(search.toLowerCase())).filter((m)=>filter==='all'||m.free);
  return <section className="section-page"><Heading title="Settings" subtitle="BYOK, model selection, and local application data." icon={<Settings size={22}/>}/><div className="settings-grid"><div className="settings-card"><div className="card-title">AI provider</div><Field label="Provider"><select value={provider} onChange={(e)=>setProvider(e.target.value)}><option value="openrouter">OpenRouter</option><option value="custom">Custom OpenAI-compatible</option></select></Field><Field label="Base URL"><input value={baseUrl} onChange={(e)=>setBaseUrl(e.target.value)}/></Field><Field label="API key"><input type="password" value={key} onChange={(e)=>setKey(e.target.value)} placeholder={store.settings.hasApiKey?'Saved securely on this device':'Paste your API key'}/></Field><div className="button-row"><button className="outline" disabled={busy} onClick={onLoad}><RefreshCw size={15}/> {busy?'Loading…':'Load models'}</button><button className="primary" onClick={onSave}>Save settings</button></div>{saved&&<div className="saved"><CheckCircle2 size={15}/> Saved locally</div>}<div className="privacy-note"><ShieldCheck size={16}/><span>Your saved key stays in OS-protected local storage and is not exposed to the renderer.</span></div></div><div className="settings-card"><div className="card-title">Model browser</div><div className="model-filters"><div className="model-search"><Search size={15}/><input placeholder="Search models" value={search} onChange={(e)=>setSearch(e.target.value)}/></div><button className={filter==='all'?'filter-btn active':'filter-btn'} onClick={()=>setFilter('all')}>All</button><button className={filter==='free'?'filter-btn active':'filter-btn'} onClick={()=>setFilter('free')}>Free</button></div><div className="model-list">{filtered.slice(0,60).map((m)=><button className={'model-item '+(model===m.id?'selected':'')} key={m.id} onClick={async()=>{setModel(m.id);const settings=await window.maverick.saveSettings({model:m.id});setStoreLocal(settings);}}><div><strong>{m.name}</strong><small>{m.id}</small></div>{m.free&&<span>FREE</span>}</button>)}</div></div></div><div className="settings-card wide"><div className="card-title">Local data</div><div className="privacy-row"><div><strong>Conversations and security journal</strong><span>Stored on this device. Maverick AI is optional and BYOK.</span></div><span className="local-badge">Local</span></div></div></section>;

  async function setStoreLocal(settings:MaverickStore['settings']) {
    void settings;
  }
}

function Field({label,children}:{label:string;children:React.ReactNode}) { return <div className="field"><label>{label}</label>{children}</div>; }
function MiniCard({title,value,icon,tone}:{title:string;value:number;icon:React.ReactNode;tone:string}) { return <div className={'mini-overview '+tone}><div className="mini-icon">{icon}</div><span>{title}</span><strong>{value}</strong></div>; }
function Stat({label,value,tone='normal'}:{label:string;value:number;tone?:string}) { return <div className={'stat-card '+tone}><span>{label}</span><strong>{value}</strong></div>; }
function RiskBadge({value}:{value:string}) { const v=value.toLowerCase(); return <span className={'risk '+(v==='high'?'danger':v==='medium'?'warn':v==='low'?'good':'muted')}>{value}</span>; }
function Empty({text}:{text:string}) { return <div className="empty-state"><span>•</span>{text}</div>; }
function formatBytes(n:number){if(!n)return '0 B';const k=1024;const i=Math.floor(Math.log(n)/Math.log(k));return parseFloat((n/Math.pow(k,i)).toFixed(1))+' '+(['B','KB','MB','GB'][i]||'TB');}
