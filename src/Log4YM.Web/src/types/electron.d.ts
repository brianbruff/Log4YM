// Type declarations for Electron IPC API exposed via preload script
interface ElectronAPI {
  onOpenSettings: (callback: () => void) => void;
  removeOpenSettingsListener: () => void;
  onOpenAbout: (callback: () => void) => void;
  removeOpenAboutListener: () => void;
  restartApp: () => Promise<void>;
  openExternal: (url: string) => Promise<void>;
  getZoomLevel: () => Promise<number>;
  setZoomLevel: (level: number) => Promise<void>;
  // Multi-window support
  openSecondaryWindow: (windowId: string) => Promise<void>;
  closeSecondaryWindow: (windowId: string) => Promise<void>;
}

declare global {
  interface Window {
    electronAPI?: ElectronAPI;
  }
}

export {};
