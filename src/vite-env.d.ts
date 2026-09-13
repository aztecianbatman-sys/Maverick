/// <reference types="vite/client" />

interface MaverickModel {
  id: string;
  name: string;
  description: string;
  contextLength: number;
  pricing: Record<string, unknown>;
  free: boolean;
  provider: string;
}

interface MaverickStore {
  settings: {
    provider: string;
    baseUrl: string;
    model: string;
    hasApiKey: boolean;
    theme: string;
  };
  chats: MaverickChat[];
  activeChatId: string | null;
  activity: MaverickActivity[];
  modelsCache: Record<string, { fetchedAt: number; models: MaverickModel[] }>;
}

interface MaverickChat {
  id: string;
  title: string;
  createdAt: number;
  updatedAt: number;
  messages: MaverickMessage[];
}

interface MaverickMessage {
  id: string;
  role: "user" | "assistant" | "system";
  content: string;
  createdAt: number;
}

interface MaverickSecurityEvent {
  id: string;
  createdUtc: string;
  type: string;
  process: string | null;
  file: string | null;
  action: string | null;
  result: string | null;
  risk: string | null;
  severity: string;
  source: string;
  summary: string;
  evidence: string;
  details: string;
}

interface MaverickActivity {
  id: string;
  type: string;
  summary: string;
  createdAt: number;
}

interface Window {
  maverick: {
    getStore(): Promise<MaverickStore>;
    saveStore(partial: Partial<MaverickStore>): Promise<MaverickStore>;
    saveSettings(settings: { provider?: string; baseUrl?: string; model?: string; apiKey?: string; theme?: string }): Promise<MaverickStore["settings"]>;
    listModels(input?: { provider?: string; baseUrl?: string }): Promise<MaverickModel[]>;
    cachedModels(provider: string): Promise<MaverickModel[]>;
    testApi(input?: { provider?: string; baseUrl?: string; apiKey?: string }): Promise<{ ok: boolean; modelCount: number }>;
    sendChat(input: { model: string; messages: Array<{ role: string; content: string }> }): Promise<string>;
    coreRequest(command: string, payload?: Record<string, unknown>): Promise<unknown>;
    openExternal(url: string): Promise<boolean>;
  };
}
