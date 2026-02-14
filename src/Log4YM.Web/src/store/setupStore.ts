import { create } from 'zustand';

export type DatabaseProvider = 'local' | 'mongodb';

export interface SetupStatus {
  isConfigured: boolean;
  isConnected: boolean;
  provider?: DatabaseProvider;
  configuredAt?: string;
  databaseName?: string;
  localDbPath?: string;
  localDbSizeBytes?: number;
  qsoCount?: number;
}

export interface TestConnectionResult {
  success: boolean;
  message: string;
  serverInfo?: {
    databaseCount: number;
    availableDatabases: string[];
  };
}

export interface ConfigureResult {
  success: boolean;
  message: string;
  restartRequired: boolean;
}

interface SetupState {
  // Status
  status: SetupStatus | null;
  isLoading: boolean;
  error: string | null;

  // Provider selection
  provider: DatabaseProvider;

  // Form state
  connectionString: string;
  databaseName: string;

  // Test connection state
  isTesting: boolean;
  testResult: TestConnectionResult | null;

  // Last configure result (for restart handling)
  lastConfigureResult: ConfigureResult | null;

  // Actions
  setProvider: (provider: DatabaseProvider) => void;
  setConnectionString: (value: string) => void;
  setDatabaseName: (value: string) => void;
  fetchStatus: () => Promise<void>;
  testConnection: () => Promise<boolean>;
  configure: () => Promise<ConfigureResult | null>;
  configureLocal: () => Promise<ConfigureResult | null>;
  clearError: () => void;
  clearTestResult: () => void;
  clearConfigureResult: () => void;
}

export const useSetupStore = create<SetupState>((set, get) => ({
  status: null,
  isLoading: false,
  error: null,
  provider: 'local',
  connectionString: '',
  databaseName: 'Log4YM',
  isTesting: false,
  testResult: null,
  lastConfigureResult: null,

  setProvider: (provider) => set({ provider }),
  setConnectionString: (value) => set({ connectionString: value, testResult: null }),
  setDatabaseName: (value) => set({ databaseName: value }),
  clearError: () => set({ error: null }),
  clearTestResult: () => set({ testResult: null }),
  clearConfigureResult: () => set({ lastConfigureResult: null }),

  fetchStatus: async () => {
    set({ isLoading: true, error: null });
    try {
      const response = await fetch('/api/setup/status');
      if (!response.ok) throw new Error('Failed to fetch status');
      const status = await response.json();
      // Sync provider from status response
      if (status.provider) {
        set({ status, provider: status.provider, isLoading: false });
      } else {
        set({ status, isLoading: false });
      }
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Failed to fetch status',
        isLoading: false,
      });
    }
  },

  testConnection: async () => {
    const { connectionString, databaseName } = get();
    set({ isTesting: true, testResult: null, error: null });

    try {
      const response = await fetch('/api/setup/test-connection', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ connectionString, databaseName }),
      });

      const result = await response.json();
      set({ testResult: result, isTesting: false });
      return result.success;
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Connection test failed',
        isTesting: false,
      });
      return false;
    }
  },

  configure: async () => {
    const { connectionString, databaseName } = get();
    set({ isLoading: true, error: null, lastConfigureResult: null });

    try {
      const response = await fetch('/api/setup/configure', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ provider: 'mongodb', connectionString, databaseName }),
      });

      const result: ConfigureResult = await response.json();

      if (result.success) {
        if (!result.restartRequired) {
          await get().fetchStatus();
        }
        set({ lastConfigureResult: result, isLoading: false });
        return result;
      } else {
        set({ error: result.message, isLoading: false });
        return null;
      }
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Configuration failed',
        isLoading: false,
      });
      return null;
    }
  },

  configureLocal: async () => {
    set({ isLoading: true, error: null, lastConfigureResult: null });
    try {
      const response = await fetch('/api/setup/configure', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ provider: 'local' }),
      });

      const result: ConfigureResult = await response.json();

      if (result.success) {
        if (!result.restartRequired) {
          await get().fetchStatus();
        }
        set({ lastConfigureResult: result, isLoading: false });
        return result;
      } else {
        set({ error: result.message, isLoading: false });
        return null;
      }
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Setup failed',
        isLoading: false,
      });
      return null;
    }
  },
}));
