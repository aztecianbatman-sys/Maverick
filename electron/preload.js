const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("maverick", {
  getStore: () => ipcRenderer.invoke("store:get"),
  saveStore: (partial) => ipcRenderer.invoke("store:save", partial),
  saveSettings: (settings) => ipcRenderer.invoke("store:set-settings", settings),
  listModels: (input) => ipcRenderer.invoke("models:list", input),
  cachedModels: (provider) => ipcRenderer.invoke("models:cached", provider),
  testApi: (input) => ipcRenderer.invoke("api:test", input),
  sendChat: (input) => ipcRenderer.invoke("chat:send", input),
  openExternal: (url) => ipcRenderer.invoke("shell:open-external", url),
});
