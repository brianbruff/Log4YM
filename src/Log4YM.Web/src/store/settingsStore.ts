import { create } from 'zustand';

// Settings types
export interface StationSettings {
  callsign: string;
  operatorName: string;
  gridSquare: string;
  latitude: number | null;
  longitude: number | null;
  city: string;
  country: string;
}

export interface QrzSettings {
  username: string;
  password: string;
  apiKey: string; // For QRZ logbook uploads
  enabled: boolean;
}

export interface AppearanceSettings {
  theme: 'dark' | 'light' | 'system';
  compactMode: boolean;
}

export interface RotatorPreset {
  name: string;
  azimuth: number;
}

export interface RotatorSettings {
  enabled: boolean;
  ipAddress: string;
  port: number;
  pollingIntervalMs: number;
  rotatorId: string;
  presets: RotatorPreset[];
}

export interface TciSettings {
  host: string;
  port: number;
  name: string;
  autoConnect: boolean;
}

export type RigType = 'tci' | 'hamlib' | null;

export interface RadioSettings {
  followRadio: boolean;
  activeRigType: RigType;
  autoReconnect: boolean;
  tci: TciSettings;
}

export interface MapSettings {
  tileLayer: 'osm' | 'dark' | 'satellite' | 'terrain';
}

export interface Settings {
  station: StationSettings;
  qrz: QrzSettings;
  appearance: AppearanceSettings;
  rotator: RotatorSettings;
  radio: RadioSettings;
  map: MapSettings;
}

export type SettingsSection = 'station' | 'qrz' | 'rotator' | 'logbook' | 'database' | 'appearance' | 'about';

interface SettingsState {
  // Settings data
  settings: Settings;

  // UI state
  isOpen: boolean;
  activeSection: SettingsSection;
  isDirty: boolean;
  isSaving: boolean;
  isLoaded: boolean;

  // Actions
  openSettings: () => void;
  closeSettings: () => void;
  setActiveSection: (section: SettingsSection) => void;

  // Settings updates
  updateStationSettings: (station: Partial<StationSettings>) => void;
  updateQrzSettings: (qrz: Partial<QrzSettings>) => void;
  updateAppearanceSettings: (appearance: Partial<AppearanceSettings>) => void;
  updateRotatorSettings: (rotator: Partial<RotatorSettings>) => void;
  updateRadioSettings: (radio: Partial<RadioSettings>) => void;
  updateTciSettings: (tci: Partial<TciSettings>) => void;
  updateMapSettings: (map: Partial<MapSettings>) => void;

  // Persistence (MongoDB only - no localStorage)
  saveSettings: () => Promise<void>;
  loadSettings: () => Promise<void>;
  resetSettings: () => void;
  // Reset loaded state (for reconnection scenarios)
  setNotLoaded: () => void;
}

const defaultSettings: Settings = {
  station: {
    callsign: '',
    operatorName: '',
    gridSquare: '',
    latitude: null,
    longitude: null,
    city: '',
    country: '',
  },
  qrz: {
    username: '',
    password: '',
    apiKey: '',
    enabled: false,
  },
  appearance: {
    theme: 'dark',
    compactMode: false,
  },
  rotator: {
    enabled: false,
    ipAddress: '127.0.0.1',
    port: 4533,
    pollingIntervalMs: 500,
    rotatorId: 'default',
    presets: [
      { name: 'N', azimuth: 0 },
      { name: 'E', azimuth: 90 },
      { name: 'S', azimuth: 180 },
      { name: 'W', azimuth: 270 },
    ],
  },
  radio: {
    followRadio: true,
    activeRigType: null,
    autoReconnect: false,
    tci: {
      host: 'localhost',
      port: 50001,
      name: '',
      autoConnect: false,
    },
  },
  map: {
    tileLayer: 'dark',
  },
};

// Settings are stored in MongoDB only - no localStorage persistence
// This ensures sensitive data (QRZ credentials) are never stored client-side
export const useSettingsStore = create<SettingsState>()((set, get) => ({
  // Initial state
  settings: defaultSettings,
  isOpen: false,
  activeSection: 'station',
  isDirty: false,
  isSaving: false,
  isLoaded: false,

  // UI actions
  openSettings: () => set({ isOpen: true }),
  closeSettings: () => set({ isOpen: false, isDirty: false }),
  setActiveSection: (section) => set({ activeSection: section }),

  // Station settings
  updateStationSettings: (station) =>
    set((state) => ({
      settings: {
        ...state.settings,
        station: { ...state.settings.station, ...station },
      },
      isDirty: true,
    })),

  // QRZ settings
  updateQrzSettings: (qrz) =>
    set((state) => ({
      settings: {
        ...state.settings,
        qrz: { ...state.settings.qrz, ...qrz },
      },
      isDirty: true,
    })),

  // Appearance settings
  updateAppearanceSettings: (appearance) =>
    set((state) => ({
      settings: {
        ...state.settings,
        appearance: { ...state.settings.appearance, ...appearance },
      },
      isDirty: true,
    })),

  // Rotator settings
  updateRotatorSettings: (rotator) =>
    set((state) => ({
      settings: {
        ...state.settings,
        rotator: { ...state.settings.rotator, ...rotator },
      },
      isDirty: true,
    })),

  // Radio settings
  updateRadioSettings: (radio) =>
    set((state) => ({
      settings: {
        ...state.settings,
        radio: { ...state.settings.radio, ...radio },
      },
      isDirty: true,
    })),

  // TCI settings (nested under radio)
  updateTciSettings: (tci) =>
    set((state) => ({
      settings: {
        ...state.settings,
        radio: {
          ...state.settings.radio,
          tci: { ...state.settings.radio.tci, ...tci },
        },
      },
      isDirty: true,
    })),

  // Map settings
  updateMapSettings: (map) =>
    set((state) => ({
      settings: {
        ...state.settings,
        map: { ...state.settings.map, ...map },
      },
      isDirty: true,
    })),

  // Save to backend (MongoDB via API)
  saveSettings: async () => {
    const { isLoaded } = get();
    if (!isLoaded) {
      console.warn('Settings not loaded yet, refusing to save to prevent data loss');
      return;
    }
    set({ isSaving: true });
    try {
      const { settings } = get();
      const response = await fetch('/api/settings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings),
      });

      if (!response.ok) {
        throw new Error('Failed to save settings');
      }

      set({ isDirty: false });
    } catch (error) {
      console.error('Failed to save settings:', error);
      throw error;
    } finally {
      set({ isSaving: false });
    }
  },

  // Load from backend (MongoDB)
  loadSettings: async () => {
    try {
      const response = await fetch('/api/settings');
      if (response.ok) {
        const settings = await response.json();
        // Deep merge with defaults to handle missing fields
        const mergedSettings: Settings = {
          station: { ...defaultSettings.station, ...settings.station },
          qrz: { ...defaultSettings.qrz, ...settings.qrz },
          appearance: { ...defaultSettings.appearance, ...settings.appearance },
          rotator: { ...defaultSettings.rotator, ...settings.rotator },
          radio: {
            ...defaultSettings.radio,
            ...settings.radio,
            activeRigType: settings.radio?.activeRigType ?? null,
            autoReconnect: settings.radio?.autoReconnect ?? false,
            tci: { ...defaultSettings.radio.tci, ...settings.radio?.tci },
          },
          map: { ...defaultSettings.map, ...settings.map },
        };
        set({ settings: mergedSettings, isDirty: false, isLoaded: true });
      } else {
        set({ isLoaded: true });
      }
    } catch (error) {
      console.error('Failed to load settings:', error);
      set({ isLoaded: true });
    }
  },

  // Reset to defaults
  resetSettings: () =>
    set({
      settings: defaultSettings,
      isDirty: true,
    }),

  // Reset loaded state (for reconnection scenarios - prevents saving stale data)
  setNotLoaded: () => set({ isLoaded: false }),
}));
