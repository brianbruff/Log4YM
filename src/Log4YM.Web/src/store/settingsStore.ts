import { create } from 'zustand';
import { persist } from 'zustand/middleware';

// Simple obfuscation for credentials (not secure, just basic hiding)
const obfuscate = (text: string): string => {
  if (!text) return '';
  return btoa(text.split('').reverse().join(''));
};

const deobfuscate = (text: string): string => {
  if (!text) return '';
  try {
    return atob(text).split('').reverse().join('');
  } catch {
    return '';
  }
};

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
  password: string; // Stored obfuscated
  apiKey: string; // Stored obfuscated - for QRZ logbook uploads
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

export interface RadioSettings {
  followRadio: boolean;
}

export interface Settings {
  station: StationSettings;
  qrz: QrzSettings;
  appearance: AppearanceSettings;
  rotator: RotatorSettings;
  radio: RadioSettings;
}

export type SettingsSection = 'station' | 'qrz' | 'rotator' | 'logbook' | 'appearance' | 'about';

interface SettingsState {
  // Settings data
  settings: Settings;

  // UI state
  isOpen: boolean;
  activeSection: SettingsSection;
  isDirty: boolean;
  isSaving: boolean;

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

  // QRZ credentials with obfuscation
  setQrzPassword: (password: string) => void;
  getQrzPassword: () => string;
  setQrzApiKey: (apiKey: string) => void;
  getQrzApiKey: () => string;

  // Persistence
  saveSettings: () => Promise<void>;
  loadSettings: () => Promise<void>;
  resetSettings: () => void;
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
  },
};

export const useSettingsStore = create<SettingsState>()(
  persist(
    (set, get) => ({
      // Initial state
      settings: defaultSettings,
      isOpen: false,
      activeSection: 'station',
      isDirty: false,
      isSaving: false,

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

      // QRZ password with obfuscation
      setQrzPassword: (password) =>
        set((state) => ({
          settings: {
            ...state.settings,
            qrz: { ...state.settings.qrz, password: obfuscate(password) },
          },
          isDirty: true,
        })),

      getQrzPassword: () => deobfuscate(get().settings.qrz.password),

      // QRZ API key with obfuscation
      setQrzApiKey: (apiKey) =>
        set((state) => ({
          settings: {
            ...state.settings,
            qrz: { ...state.settings.qrz, apiKey: obfuscate(apiKey) },
          },
          isDirty: true,
        })),

      getQrzApiKey: () => deobfuscate(get().settings.qrz.apiKey),

      // Save to backend (MongoDB via API)
      saveSettings: async () => {
        set({ isSaving: true });
        try {
          const { settings } = get();
          // Deobfuscate QRZ credentials before sending to backend
          // The backend stores plain passwords for use with QRZ API
          const settingsToSave = {
            ...settings,
            qrz: {
              ...settings.qrz,
              password: deobfuscate(settings.qrz.password),
              apiKey: deobfuscate(settings.qrz.apiKey),
            },
          };
          const response = await fetch('/api/settings', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(settingsToSave),
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

      // Load from backend
      loadSettings: async () => {
        try {
          const response = await fetch('/api/settings');
          if (response.ok) {
            const settings = await response.json();
            // Backend stores plain passwords, so obfuscate for frontend storage
            const settingsToStore = {
              ...settings,
              qrz: {
                ...settings.qrz,
                password: obfuscate(settings.qrz?.password || ''),
                apiKey: obfuscate(settings.qrz?.apiKey || ''),
              },
            };
            set({ settings: settingsToStore, isDirty: false });
          }
        } catch (error) {
          console.error('Failed to load settings:', error);
        }
      },

      // Reset to defaults
      resetSettings: () =>
        set({
          settings: defaultSettings,
          isDirty: true,
        }),
    }),
    {
      name: 'log4ym-settings',
      partialize: (state) => ({ settings: state.settings }),
      merge: (persistedState, currentState) => {
        const persisted = persistedState as { settings?: Partial<Settings> };
        return {
          ...currentState,
          settings: {
            ...defaultSettings,
            ...persisted?.settings,
            // Deep merge each settings section with defaults
            station: { ...defaultSettings.station, ...persisted?.settings?.station },
            qrz: { ...defaultSettings.qrz, ...persisted?.settings?.qrz },
            appearance: { ...defaultSettings.appearance, ...persisted?.settings?.appearance },
            rotator: { ...defaultSettings.rotator, ...persisted?.settings?.rotator },
            radio: { ...defaultSettings.radio, ...persisted?.settings?.radio },
          },
        };
      },
    }
  )
);
