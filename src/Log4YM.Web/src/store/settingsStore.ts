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
  theme: 'dark' | 'light' | 'amber';
  compactMode: boolean;
}

export interface RotatorPreset {
  name: string;
  azimuth: number;
}

export type RotatorConnectionType = 'network' | 'serial';

export interface RotatorSettings {
  enabled: boolean;
  // Connection type
  connectionType: RotatorConnectionType;
  // Network settings (for connecting to existing rotctld)
  ipAddress: string;
  port: number;
  // Serial settings (for direct serial connection)
  serialPort: string; // e.g., 'COM3' or '/dev/ttyUSB0'
  baudRate: number;
  // Hamlib configuration
  hamlibModelId: number | null; // Hamlib rotator model ID (e.g., 603 for Yaesu GS-232B)
  hamlibModelName: string; // Human-readable model name
  // Polling and identification
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
  autoConnectRigId: string | null;
  tci: TciSettings;
}

export interface RbnSettings {
  enabled: boolean;
  opacity: number;
  showPaths: boolean;
  timeWindowMinutes: number;
  minSnr: number;
  bands: string[];
  modes: string[];
}

export interface MapSettings {
  tileLayer: 'osm' | 'dark' | 'satellite' | 'terrain';
  showSatellites: boolean;
  selectedSatellites: string[];
  rbn: RbnSettings;
  showPotaOverlay: boolean;
  showDayNightOverlay: boolean;
  showGrayLine: boolean;
  showSunMarker: boolean;
  showMoonMarker: boolean;
  dayNightOpacity: number;
  grayLineOpacity: number;
  showCallsignImages: boolean;
  maxCallsignImages: number;
}

export interface HeaderSettings {
  timeFormat: '12h' | '24h';
  showWeather: boolean;
  weatherLocation: string;  // City name or coordinates for weather lookup
}

export interface ClusterConnection {
  id: string;
  name: string;
  host: string;
  port: number;
  callsign: string | null;  // If null, uses station callsign
  password: string | null;  // Optional password for closed clusters
  enabled: boolean;
  autoReconnect: boolean;
}

export interface ClusterSettings {
  connections: ClusterConnection[];
}

export interface AiSettings {
  provider: 'anthropic' | 'openai';
  apiKey: string;
  model: string;
  autoGenerateTalkPoints: boolean;
  includeQrzProfile: boolean;
  includeQsoHistory: boolean;
  includeSpotComments: boolean;
}

export interface Settings {
  station: StationSettings;
  qrz: QrzSettings;
  appearance: AppearanceSettings;
  rotator: RotatorSettings;
  radio: RadioSettings;
  map: MapSettings;
  cluster: ClusterSettings;
  header: HeaderSettings;
  ai: AiSettings;
}

export type SettingsSection = 'station' | 'qrz' | 'rotator' | 'database' | 'appearance' | 'map' | 'header' | 'ai' | 'about';

interface SettingsState {
  // Settings data
  settings: Settings;

  // UI state
  isOpen: boolean;
  activeSection: SettingsSection;
  isDirty: boolean;
  isSaving: boolean;
  isLoaded: boolean;
  error: string | null;

  // Actions
  openSettings: () => void;
  closeSettings: () => void;
  setActiveSection: (section: SettingsSection) => void;
  clearError: () => void;

  // Settings updates
  updateStationSettings: (station: Partial<StationSettings>) => void;
  updateQrzSettings: (qrz: Partial<QrzSettings>) => void;
  updateAppearanceSettings: (appearance: Partial<AppearanceSettings>) => void;
  updateRotatorSettings: (rotator: Partial<RotatorSettings>) => void;
  updateRadioSettings: (radio: Partial<RadioSettings>) => void;
  updateTciSettings: (tci: Partial<TciSettings>) => void;
  updateMapSettings: (map: Partial<MapSettings>) => void;
  updateClusterSettings: (cluster: Partial<ClusterSettings>) => void;
  updateClusterConnection: (connectionId: string, connection: Partial<ClusterConnection>) => void;
  updateHeaderSettings: (header: Partial<HeaderSettings>) => void;
  updateAiSettings: (ai: Partial<AiSettings>) => void;
  addClusterConnection: () => void;
  removeClusterConnection: (connectionId: string) => void;

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
    connectionType: 'network',
    ipAddress: '127.0.0.1',
    port: 4533,
    serialPort: '',
    baudRate: 9600,
    hamlibModelId: null,
    hamlibModelName: '',
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
    autoConnectRigId: null,
    tci: {
      host: '',
      port: 50001,
      name: '',
      autoConnect: false,
    },
  },
  map: {
    tileLayer: 'dark',
    showSatellites: false,
    selectedSatellites: ['ISS', 'AO-91', 'SO-50'],
    rbn: {
      enabled: false,
      opacity: 0.7,
      showPaths: true,
      timeWindowMinutes: 5,
      minSnr: -10,
      bands: ['all'],
      modes: ['CW', 'RTTY'],
    },
    showPotaOverlay: false,
    showDayNightOverlay: false,
    showGrayLine: false,
    showSunMarker: true,
    showMoonMarker: true,
    dayNightOpacity: 0.5,
    grayLineOpacity: 0.6,
    showCallsignImages: true,
    maxCallsignImages: 50,
  },
  cluster: {
    connections: [],
  },
  header: {
    timeFormat: '24h',
    showWeather: true,
    weatherLocation: '',
  },
  ai: {
    provider: 'anthropic',
    apiKey: '',
    model: 'claude-sonnet-4-5-20250929',
    autoGenerateTalkPoints: true,
    includeQrzProfile: true,
    includeQsoHistory: true,
    includeSpotComments: false,
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
  error: null,

  // UI actions
  openSettings: () => set({ isOpen: true, error: null }),
  closeSettings: () => set({ isOpen: false, isDirty: false, error: null }),
  setActiveSection: (section) => set({ activeSection: section }),
  clearError: () => set({ error: null }),

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

  // Header settings
  updateHeaderSettings: (header) =>
    set((state) => ({
      settings: {
        ...state.settings,
        header: { ...state.settings.header, ...header },
      },
      isDirty: true,
    })),

  // AI settings
  updateAiSettings: (ai) =>
    set((state) => ({
      settings: {
        ...state.settings,
        ai: { ...state.settings.ai, ...ai },
      },
      isDirty: true,
    })),

  // Cluster settings
  updateClusterSettings: (cluster) =>
    set((state) => ({
      settings: {
        ...state.settings,
        cluster: { ...state.settings.cluster, ...cluster },
      },
      isDirty: true,
    })),

  // Update a specific cluster connection by ID
  updateClusterConnection: (connectionId, connection) =>
    set((state) => ({
      settings: {
        ...state.settings,
        cluster: {
          ...state.settings.cluster,
          connections: state.settings.cluster.connections.map((c) =>
            c.id === connectionId ? { ...c, ...connection } : c
          ),
        },
      },
      isDirty: true,
    })),

  // Add a new cluster connection
  addClusterConnection: () =>
    set((state) => {
      const newConnection: ClusterConnection = {
        id: crypto.randomUUID(),
        name: `Cluster ${state.settings.cluster.connections.length + 1}`,
        host: '',
        port: 23,
        callsign: null,
        password: null,
        enabled: true,
        autoReconnect: false,
      };
      return {
        settings: {
          ...state.settings,
          cluster: {
            ...state.settings.cluster,
            connections: [...state.settings.cluster.connections, newConnection],
          },
        },
        isDirty: true,
      };
    }),

  // Remove a cluster connection by ID
  removeClusterConnection: (connectionId) =>
    set((state) => ({
      settings: {
        ...state.settings,
        cluster: {
          ...state.settings.cluster,
          connections: state.settings.cluster.connections.filter(
            (c) => c.id !== connectionId
          ),
        },
      },
      isDirty: true,
    })),

  // Save to backend (MongoDB via API)
  saveSettings: async () => {
    const { isLoaded } = get();
    if (!isLoaded) {
      const errorMsg = 'Settings not loaded yet, cannot save';
      console.warn(errorMsg);
      set({ error: errorMsg });
      return;
    }
    set({ isSaving: true, error: null });
    try {
      const { settings } = get();
      const response = await fetch('/api/settings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings),
      });

      if (!response.ok) {
        // Try to get error message from response body
        let errorMsg = `Failed to save settings (HTTP ${response.status})`;
        try {
          const errorData = await response.json();
          if (errorData.error) {
            errorMsg = errorData.error;
          }
        } catch {
          // If JSON parsing fails, use default error message
        }
        throw new Error(errorMsg);
      }

      set({ isDirty: false, error: null });
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : 'Failed to save settings';
      console.error('Failed to save settings:', error);
      set({ error: errorMsg });
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
            autoConnectRigId: settings.radio?.autoConnectRigId ?? null,
            tci: { ...defaultSettings.radio.tci, ...settings.radio?.tci, host: settings.radio?.tci?.host ?? '', name: settings.radio?.tci?.name ?? '' },
          },
          map: {
            ...defaultSettings.map,
            ...settings.map,
            rbn: { ...defaultSettings.map.rbn, ...settings.map?.rbn },
          },
          cluster: { ...defaultSettings.cluster, ...settings.cluster },
          header: { ...defaultSettings.header, ...settings.header },
          ai: { ...defaultSettings.ai, ...settings.ai },
        };
        set({ settings: mergedSettings, isDirty: false, isLoaded: true });
      } else {
        // Non-OK response (e.g., 404, 500) - database might not be available
        // Still mark as loaded so user can configure initial settings
        console.warn('Settings API returned non-OK status:', response.status);
        set({ isLoaded: true });
      }
    } catch (error) {
      // Network error or database not connected
      // Still mark as loaded so user can configure initial settings
      console.warn('Failed to load settings (database may not be connected):', error);
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
