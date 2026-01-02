// Type declarations for Electron IPC API exposed via preload script
interface ElectronAPI {
  onOpenSettings: (callback: () => void) => void;
  removeOpenSettingsListener: () => void;
}

declare global {
  interface Window {
    electronAPI?: ElectronAPI;
  }
}

export {};
