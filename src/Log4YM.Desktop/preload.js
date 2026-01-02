const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods for renderer process to use
contextBridge.exposeInMainWorld('electronAPI', {
  // Listen for menu commands from main process
  onOpenSettings: (callback) => {
    ipcRenderer.on('open-settings', () => callback());
  },
  // Remove listener when component unmounts
  removeOpenSettingsListener: () => {
    ipcRenderer.removeAllListeners('open-settings');
  }
});
