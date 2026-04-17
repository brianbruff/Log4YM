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
  },
  // Listen for about dialog command from main process
  onOpenAbout: (callback) => {
    ipcRenderer.on('open-about', () => callback());
  },
  removeOpenAboutListener: () => {
    ipcRenderer.removeAllListeners('open-about');
  },
  // Restart the app (used after database provider switch)
  restartApp: () => ipcRenderer.invoke('restart-app'),
  // Zoom level management
  getZoomLevel: () => ipcRenderer.invoke('get-zoom-level'),
  setZoomLevel: (level) => ipcRenderer.invoke('set-zoom-level', level),
  // Native file picker — returns the selected absolute path, or null if cancelled.
  // Used by the LOTW settings section to locate the TQSL binary.
  selectFile: (options) => ipcRenderer.invoke('select-file', options)
});
