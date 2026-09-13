const { app, BrowserWindow, ipcMain, safeStorage, shell } = require("electron");
const path = require("node:path");
const fs = require("node:fs/promises");

const isDev = Boolean(process.env.VITE_DEV_SERVER_URL);
const STORE_DIR = path.join(app.getPath("userData"), "maverick");
const STORE_FILE = path.join(STORE_DIR, "store.json");

const DEFAULT_STORE = {
  settings: {
    provider: "openrouter",
    baseUrl: "https://openrouter.ai/api/v1",
    model: "",
    apiKeyEncrypted: "",
    theme: "dark",
  },
  chats: [],
  activeChatId: null,
  activity: [],
  modelsCache: {},
};

async function ensureStore() {
  await fs.mkdir(STORE_DIR, { recursive: true });
  try {
    await fs.access(STORE_FILE);
  } catch {
    await fs.writeFile(STORE_FILE, JSON.stringify(DEFAULT_STORE, null, 2), "utf8");
  }
}

async function readStore() {
  await ensureStore();
  try {
    return JSON.parse(await fs.readFile(STORE_FILE, "utf8"));
  } catch {
    await fs.writeFile(STORE_FILE, JSON.stringify(DEFAULT_STORE, null, 2), "utf8");
    return structuredClone(DEFAULT_STORE);
  }
}

async function writeStore(store) {
  await ensureStore();
  const temp = STORE_FILE + ".tmp";
  await fs.writeFile(temp, JSON.stringify(store, null, 2), "utf8");
  await fs.rename(temp, STORE_FILE);
}

function encryptSecret(value) {
  if (!value) return "";
  if (!safeStorage.isEncryptionAvailable()) {
    throw new Error("Windows-protected local secret storage is unavailable.");
  }
  return safeStorage.encryptString(value).toString("base64");
}

function decryptSecret(value) {
  if (!value) return "";
  if (!safeStorage.isEncryptionAvailable()) return "";
  return safeStorage.decryptString(Buffer.from(value, "base64"));
}

function sanitizeSettings(store) {
  return {
    provider: store.settings.provider,
    baseUrl: store.settings.baseUrl,
    model: store.settings.model,
    hasApiKey: Boolean(store.settings.apiKeyEncrypted),
    theme: store.settings.theme,
  };
}

function joinApiEndpoint(baseUrl, endpoint) {
  const normalized = String(baseUrl || "").replace(/\/+$/, "");
  return normalized + "/" + String(endpoint || "").replace(/^\/+/, "");
}

function isFreeModel(model) {
  const prompt = Number(model?.pricing?.prompt ?? NaN);
  const completion = Number(model?.pricing?.completion ?? NaN);
  return model?.id?.endsWith(":free") || (Number.isFinite(prompt) && Number.isFinite(completion) && prompt === 0 && completion === 0);
}

function normalizeModels(provider, payload) {
  const data = Array.isArray(payload?.data) ? payload.data : [];
  return data.map((model) => ({
    id: String(model.id || ""),
    name: String(model.name || model.id || "Unnamed model"),
    description: String(model.description || ""),
    contextLength: Number(model.context_length || model.contextLength || 0),
    pricing: model.pricing || {},
    free: isFreeModel(model),
    provider,
  })).filter((model) => model.id);
}

async function fetchModels({ provider, baseUrl, apiKey }) {
  const url = new URL("/models", baseUrl.endsWith("/") ? baseUrl : baseUrl + "/").toString();
  const response = await fetch(url, {
    headers: apiKey ? { Authorization: `Bearer ${apiKey}` } : {},
  });
  const text = await response.text();
  let payload;
  try { payload = JSON.parse(text); } catch { throw new Error(`Model API returned HTTP ${response.status} with non-JSON content.`); }
  if (!response.ok) throw new Error(payload?.error?.message || `Model request failed (HTTP ${response.status}).`);
  return normalizeModels(provider, payload);
}

async function chatRequest({ provider, baseUrl, apiKey, model, messages }) {
  if (!apiKey) throw new Error("Add a provider API key first.");
  if (!model) throw new Error("Choose a model first.");

  const endpoint = new URL("/chat/completions", baseUrl.endsWith("/") ? baseUrl : baseUrl + "/").toString();
  const response = await fetch(endpoint, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${apiKey}`,
    },
    body: JSON.stringify({ model, messages, stream: false }),
  });
  const text = await response.text();
  let payload;
  try { payload = JSON.parse(text); } catch { throw new Error(`Chat API returned HTTP ${response.status} with non-JSON content.`); }
  if (!response.ok) throw new Error(payload?.error?.message || `Chat request failed (HTTP ${response.status}).`);
  const content = payload?.choices?.[0]?.message?.content;
  if (typeof content !== "string") throw new Error("The provider returned no text response.");
  return content;
}

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    minWidth: 900,
    minHeight: 620,
    backgroundColor: "#0d0f12",
    icon: path.join(__dirname, "..", "assets", "maverick-icon.png"),
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
    titleBarStyle: "hidden",
    titleBarOverlay: {
      color: "#0d0f12",
      symbolColor: "#f5f7fa",
      height: 32,
    },
  });

  if (isDev) {
    win.loadURL(process.env.VITE_DEV_SERVER_URL);
    win.webContents.openDevTools({ mode: "detach" });
  } else {
    win.loadFile(path.join(__dirname, "..", "dist", "index.html"));
  }
}

app.whenReady().then(async () => {
  await ensureStore();

  ipcMain.handle("store:get", async () => {
    const store = await readStore();
    return {
      ...store,
      settings: sanitizeSettings(store),
    };
  });

  ipcMain.handle("store:set-settings", async (_, input) => {
    const store = await readStore();
    const next = { ...store.settings, ...input };
    if (Object.prototype.hasOwnProperty.call(input, "apiKey")) {
      next.apiKeyEncrypted = encryptSecret(String(input.apiKey || ""));
    }
    delete next.apiKey;
    store.settings = next;
    await writeStore(store);
    return sanitizeSettings(store);
  });

  ipcMain.handle("store:save", async (_, partial) => {
    const store = await readStore();
    const next = { ...store, ...partial };
    await writeStore(next);
    return next;
  });

  ipcMain.handle("models:list", async (_, input) => {
    const store = await readStore();
    const provider = input?.provider || store.settings.provider;
    const baseUrl = input?.baseUrl || store.settings.baseUrl;
    const apiKey = decryptSecret(store.settings.apiKeyEncrypted);
    const models = await fetchModels({ provider, baseUrl, apiKey });
    store.modelsCache[provider] = { fetchedAt: Date.now(), models };
    await writeStore(store);
    return models;
  });

  ipcMain.handle("models:cached", async (_, provider) => {
    const store = await readStore();
    return store.modelsCache?.[provider]?.models || [];
  });

  ipcMain.handle("api:test", async (_, input) => {
    const store = await readStore();
    const apiKey = input?.apiKey ? String(input.apiKey) : decryptSecret(store.settings.apiKeyEncrypted);
    if (!apiKey) throw new Error("No API key configured.");
    const provider = input?.provider || store.settings.provider;
    const baseUrl = input?.baseUrl || store.settings.baseUrl;
    const models = await fetchModels({ provider, baseUrl, apiKey });
    return { ok: true, modelCount: models.length };
  });

  ipcMain.handle("chat:send", async (_, input) => {
    const store = await readStore();
    const apiKey = decryptSecret(store.settings.apiKeyEncrypted);
    return chatRequest({
      provider: store.settings.provider,
      baseUrl: store.settings.baseUrl,
      apiKey,
      model: input?.model || store.settings.model,
      messages: Array.isArray(input?.messages) ? input.messages : [],
    });
  });

  ipcMain.handle("shell:open-external", async (_, url) => {
    if (typeof url !== "string" || !/^https?:\/\//i.test(url)) return false;
    await shell.openExternal(url);
    return true;
  });

  createWindow();
  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
