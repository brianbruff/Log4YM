import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useSettingsStore } from '../../store/settingsStore';

// Mock fetch for settings API
const mockFetch = vi.fn();
globalThis.fetch = mockFetch;

describe('settingsStore', () => {
  beforeEach(() => {
    mockFetch.mockReset();
    vi.restoreAllMocks();
    // Re-assign fetch mock after restoreAllMocks
    globalThis.fetch = mockFetch;
    // Reset store
    useSettingsStore.setState({
      isOpen: false,
      activeSection: 'station',
      isDirty: false,
      isSaving: false,
      isLoaded: false,
      settings: {
        station: { callsign: '', operatorName: '', gridSquare: '', latitude: null, longitude: null, city: '', country: '' },
        qrz: { username: '', password: '', apiKey: '', enabled: false },
        appearance: { theme: 'dark', compactMode: false },
        rotator: { enabled: false, ipAddress: '127.0.0.1', port: 4533, pollingIntervalMs: 500, rotatorId: 'default', presets: [] },
        radio: { followRadio: true, activeRigType: null, autoReconnect: false, autoConnectRigId: null, tci: { host: 'localhost', port: 50001, name: '', autoConnect: false } },
        map: { tileLayer: 'dark' },
        cluster: { connections: [] },
        header: { timeFormat: '24h', sizeMultiplier: 1.0, showWeather: true, weatherLocation: '' },
        ai: { provider: 'anthropic', apiKey: '', model: 'claude-sonnet-4-5-20250929', autoGenerateTalkPoints: true, includeQrzProfile: true, includeQsoHistory: true, includeSpotComments: false },
      },
    });
  });

  describe('UI actions', () => {
    it('opens settings dialog', () => {
      useSettingsStore.getState().openSettings();
      expect(useSettingsStore.getState().isOpen).toBe(true);
    });

    it('closes settings dialog and clears dirty flag', () => {
      useSettingsStore.getState().openSettings();
      useSettingsStore.getState().updateStationSettings({ callsign: 'W1AW' });
      expect(useSettingsStore.getState().isDirty).toBe(true);

      useSettingsStore.getState().closeSettings();
      expect(useSettingsStore.getState().isOpen).toBe(false);
      expect(useSettingsStore.getState().isDirty).toBe(false);
    });

    it('sets active section', () => {
      useSettingsStore.getState().setActiveSection('qrz');
      expect(useSettingsStore.getState().activeSection).toBe('qrz');
    });
  });

  describe('settings updates', () => {
    it('updates station settings partially', () => {
      useSettingsStore.getState().updateStationSettings({ callsign: 'EI2KC' });
      const settings = useSettingsStore.getState().settings;
      expect(settings.station.callsign).toBe('EI2KC');
      expect(settings.station.operatorName).toBe(''); // unchanged
      expect(useSettingsStore.getState().isDirty).toBe(true);
    });

    it('updates QRZ settings', () => {
      useSettingsStore.getState().updateQrzSettings({ username: 'testuser', enabled: true });
      const settings = useSettingsStore.getState().settings;
      expect(settings.qrz.username).toBe('testuser');
      expect(settings.qrz.enabled).toBe(true);
      expect(settings.qrz.password).toBe(''); // unchanged
    });

    it('updates appearance settings', () => {
      useSettingsStore.getState().updateAppearanceSettings({ theme: 'light' });
      expect(useSettingsStore.getState().settings.appearance.theme).toBe('light');
    });

    it('updates TCI settings (nested)', () => {
      useSettingsStore.getState().updateTciSettings({ host: '192.168.1.100', port: 50002 });
      const tci = useSettingsStore.getState().settings.radio.tci;
      expect(tci.host).toBe('192.168.1.100');
      expect(tci.port).toBe(50002);
      expect(tci.name).toBe(''); // unchanged
    });

    it('updates AI settings', () => {
      useSettingsStore.getState().updateAiSettings({ provider: 'openai', model: 'gpt-4' });
      const ai = useSettingsStore.getState().settings.ai;
      expect(ai.provider).toBe('openai');
      expect(ai.model).toBe('gpt-4');
    });
  });

  describe('cluster connection CRUD', () => {
    it('adds a new cluster connection', () => {
      useSettingsStore.getState().addClusterConnection();
      const connections = useSettingsStore.getState().settings.cluster.connections;
      expect(connections).toHaveLength(1);
      expect(connections[0].name).toBe('Cluster 1');
      expect(connections[0].port).toBe(23);
      expect(connections[0].enabled).toBe(true);
    });

    it('adds multiple connections with incrementing names', () => {
      useSettingsStore.getState().addClusterConnection();
      useSettingsStore.getState().addClusterConnection();
      const connections = useSettingsStore.getState().settings.cluster.connections;
      expect(connections).toHaveLength(2);
      expect(connections[0].name).toBe('Cluster 1');
      expect(connections[1].name).toBe('Cluster 2');
    });

    it('updates a specific cluster connection', () => {
      useSettingsStore.getState().addClusterConnection();
      const id = useSettingsStore.getState().settings.cluster.connections[0].id;

      useSettingsStore.getState().updateClusterConnection(id, {
        host: 'dx.example.com',
        port: 7300,
      });

      const conn = useSettingsStore.getState().settings.cluster.connections[0];
      expect(conn.host).toBe('dx.example.com');
      expect(conn.port).toBe(7300);
      expect(conn.name).toBe('Cluster 1'); // unchanged
    });

    it('removes a cluster connection', () => {
      useSettingsStore.getState().addClusterConnection();
      useSettingsStore.getState().addClusterConnection();
      const id = useSettingsStore.getState().settings.cluster.connections[0].id;

      useSettingsStore.getState().removeClusterConnection(id);
      expect(useSettingsStore.getState().settings.cluster.connections).toHaveLength(1);
    });
  });

  describe('deep merge on load', () => {
    it('merges loaded settings with defaults', async () => {
      // Simulate partial settings from backend (missing some fields)
      const partialSettings = {
        station: { callsign: 'EI2KC', gridSquare: 'IO63' },
        // Missing qrz, appearance, etc.
      };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(partialSettings),
      });

      await useSettingsStore.getState().loadSettings();

      const settings = useSettingsStore.getState().settings;
      // Loaded values should be present
      expect(settings.station.callsign).toBe('EI2KC');
      expect(settings.station.gridSquare).toBe('IO63');
      // Default values should fill in missing fields
      expect(settings.station.operatorName).toBe('');
      expect(settings.qrz.enabled).toBe(false);
      expect(settings.appearance.theme).toBe('dark');
      expect(settings.radio.tci.port).toBe(50001);
      expect(useSettingsStore.getState().isLoaded).toBe(true);
    });

    it('handles API error gracefully', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      await useSettingsStore.getState().loadSettings();
      // Should still be marked as loaded to prevent blocking
      expect(useSettingsStore.getState().isLoaded).toBe(true);
    });

    it('handles non-OK response', async () => {
      mockFetch.mockResolvedValueOnce({ ok: false });

      await useSettingsStore.getState().loadSettings();
      expect(useSettingsStore.getState().isLoaded).toBe(true);
    });
  });

  describe('isLoaded guard', () => {
    it('prevents saving when not loaded', async () => {
      const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
      useSettingsStore.setState({ isLoaded: false });

      await useSettingsStore.getState().saveSettings();

      // Should not have made a fetch call
      expect(mockFetch).not.toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('allows saving when loaded', async () => {
      useSettingsStore.setState({ isLoaded: true });
      mockFetch.mockResolvedValueOnce({ ok: true });

      await useSettingsStore.getState().saveSettings();

      expect(mockFetch).toHaveBeenCalledWith('/api/settings', expect.objectContaining({
        method: 'POST',
      }));
    });
  });

  describe('reset', () => {
    it('resets settings to defaults', () => {
      useSettingsStore.getState().updateStationSettings({ callsign: 'W1AW' });
      useSettingsStore.getState().resetSettings();

      expect(useSettingsStore.getState().settings.station.callsign).toBe('');
      expect(useSettingsStore.getState().isDirty).toBe(true);
    });
  });

  describe('setNotLoaded', () => {
    it('resets loaded state', () => {
      useSettingsStore.setState({ isLoaded: true });
      useSettingsStore.getState().setNotLoaded();
      expect(useSettingsStore.getState().isLoaded).toBe(false);
    });
  });
});
