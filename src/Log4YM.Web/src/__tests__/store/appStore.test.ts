import { describe, it, expect, beforeEach } from 'vitest';
import { useAppStore } from '../../store/appStore';

describe('appStore', () => {
  beforeEach(() => {
    // Reset store to initial state
    useAppStore.setState({
      isConnected: false,
      connectionState: 'disconnected',
      reconnectAttempt: 0,
      focusedCallsign: null,
      focusedCallsignInfo: null,
      isLookingUpCallsign: false,
      logHistoryCallsignFilter: null,
      selectedSpot: null,
      clusterStatuses: {},
      potaSpots: [],
      dxClusterMapEnabled: false,
      hoveredSpotId: null,
      callsignMapImages: [],
    });
  });

  describe('connection state', () => {
    it('starts disconnected', () => {
      const state = useAppStore.getState();
      expect(state.isConnected).toBe(false);
      expect(state.connectionState).toBe('disconnected');
    });

    it('sets connected state', () => {
      useAppStore.getState().setConnected(true);
      const state = useAppStore.getState();
      expect(state.isConnected).toBe(true);
      expect(state.connectionState).toBe('connected');
      expect(state.reconnectAttempt).toBe(0);
    });

    it('sets disconnected state', () => {
      useAppStore.getState().setConnected(true);
      useAppStore.getState().setConnected(false);
      const state = useAppStore.getState();
      expect(state.isConnected).toBe(false);
      expect(state.connectionState).toBe('disconnected');
    });

    it('sets connection state with attempt count', () => {
      useAppStore.getState().setConnectionState('reconnecting', 3);
      const state = useAppStore.getState();
      expect(state.connectionState).toBe('reconnecting');
      expect(state.reconnectAttempt).toBe(3);
      expect(state.isConnected).toBe(false);
    });

    it('sets rehydrating state', () => {
      useAppStore.getState().setConnectionState('rehydrating');
      const state = useAppStore.getState();
      expect(state.connectionState).toBe('rehydrating');
      expect(state.isConnected).toBe(false);
    });
  });

  describe('focused callsign', () => {
    it('starts with null', () => {
      expect(useAppStore.getState().focusedCallsign).toBeNull();
    });

    it('sets focused callsign', () => {
      useAppStore.getState().setFocusedCallsign('W1AW');
      expect(useAppStore.getState().focusedCallsign).toBe('W1AW');
    });

    it('clears focused callsign', () => {
      useAppStore.getState().setFocusedCallsign('W1AW');
      useAppStore.getState().setFocusedCallsign(null);
      expect(useAppStore.getState().focusedCallsign).toBeNull();
    });

    it('sets looking up callsign loading state', () => {
      useAppStore.getState().setLookingUpCallsign(true);
      expect(useAppStore.getState().isLookingUpCallsign).toBe(true);

      useAppStore.getState().setLookingUpCallsign(false);
      expect(useAppStore.getState().isLookingUpCallsign).toBe(false);
    });

    it('clears callsign from all controls', () => {
      useAppStore.getState().setFocusedCallsign('W1AW');
      useAppStore.getState().setLookingUpCallsign(true);
      useAppStore.getState().setLogHistoryCallsignFilter('W1AW');

      useAppStore.getState().clearCallsignFromAllControls();

      const state = useAppStore.getState();
      expect(state.focusedCallsign).toBeNull();
      expect(state.focusedCallsignInfo).toBeNull();
      expect(state.logHistoryCallsignFilter).toBeNull();
      expect(state.isLookingUpCallsign).toBe(false);
      expect(state.selectedSpot).toBeNull();
    });
  });

  describe('station info', () => {
    it('sets station callsign and grid', () => {
      useAppStore.getState().setStationInfo('EI2KC', 'IO63');
      const state = useAppStore.getState();
      expect(state.stationCallsign).toBe('EI2KC');
      expect(state.stationGrid).toBe('IO63');
    });
  });

  describe('cluster statuses', () => {
    it('sets cluster status', () => {
      useAppStore.getState().setClusterStatus('cluster1', {
        clusterId: 'cluster1',
        name: 'DX Cluster 1',
        status: 'connected',
        errorMessage: null,
      });

      const state = useAppStore.getState();
      expect(state.clusterStatuses['cluster1']).toBeDefined();
      expect(state.clusterStatuses['cluster1'].status).toBe('connected');
    });

    it('updates existing cluster status', () => {
      useAppStore.getState().setClusterStatus('cluster1', {
        clusterId: 'cluster1',
        name: 'DX Cluster 1',
        status: 'connected',
        errorMessage: null,
      });
      useAppStore.getState().setClusterStatus('cluster1', {
        clusterId: 'cluster1',
        name: 'DX Cluster 1',
        status: 'error',
        errorMessage: 'Connection lost',
      });

      expect(useAppStore.getState().clusterStatuses['cluster1'].status).toBe('error');
    });
  });

  describe('POTA spots', () => {
    it('sets POTA spots', () => {
      const spots = [{ spotId: 1, activator: 'W1AW', frequency: '14074', mode: 'FT8', reference: 'K-0001', parkName: 'Test Park', spotTime: '', spotter: 'AI', comments: '', source: 'pota' }];
      useAppStore.getState().setPotaSpots(spots);
      expect(useAppStore.getState().potaSpots).toHaveLength(1);
    });

  });

  describe('DX cluster map', () => {
    it('toggles DX cluster map overlay', () => {
      useAppStore.getState().setDxClusterMapEnabled(true);
      expect(useAppStore.getState().dxClusterMapEnabled).toBe(true);
    });

    it('sets hovered spot ID', () => {
      useAppStore.getState().setHoveredSpotId('spot-123');
      expect(useAppStore.getState().hoveredSpotId).toBe('spot-123');

      useAppStore.getState().setHoveredSpotId(null);
      expect(useAppStore.getState().hoveredSpotId).toBeNull();
    });
  });

  describe('callsign map images', () => {
    it('starts with empty array', () => {
      expect(useAppStore.getState().callsignMapImages).toHaveLength(0);
    });

    it('sets callsign map images', () => {
      const images = [
        { callsign: 'W1AW', imageUrl: 'https://example.com/w1aw.jpg', latitude: 41.7, longitude: -72.7, savedAt: '2024-01-01T00:00:00Z' },
        { callsign: 'EI2KC', imageUrl: 'https://example.com/ei2kc.jpg', latitude: 52.6, longitude: -8.6, savedAt: '2024-01-02T00:00:00Z' },
      ];
      useAppStore.getState().setCallsignMapImages(images);
      expect(useAppStore.getState().callsignMapImages).toHaveLength(2);
      expect(useAppStore.getState().callsignMapImages[0].callsign).toBe('W1AW');
    });

    it('adds callsign map image with imageUrl', () => {
      useAppStore.getState().addCallsignMapImage({
        callsign: 'W1AW',
        imageUrl: 'https://example.com/w1aw.jpg',
        latitude: 41.7,
        longitude: -72.7,
        savedAt: '2024-01-01T00:00:00Z',
      });

      const images = useAppStore.getState().callsignMapImages;
      expect(images).toHaveLength(1);
      expect(images[0].callsign).toBe('W1AW');
      expect(images[0].imageUrl).toBe('https://example.com/w1aw.jpg');
    });

    it('adds callsign map image without imageUrl (placeholder)', () => {
      useAppStore.getState().addCallsignMapImage({
        callsign: 'JA1ABC',
        latitude: 35.6,
        longitude: 139.6,
        savedAt: '2024-01-01T00:00:00Z',
      });

      const images = useAppStore.getState().callsignMapImages;
      expect(images).toHaveLength(1);
      expect(images[0].callsign).toBe('JA1ABC');
      expect(images[0].imageUrl).toBeUndefined();
    });

    it('replaces existing entry for same callsign (case-insensitive)', () => {
      useAppStore.getState().addCallsignMapImage({
        callsign: 'W1AW',
        imageUrl: 'https://example.com/old.jpg',
        latitude: 41.7,
        longitude: -72.7,
        savedAt: '2024-01-01T00:00:00Z',
      });

      useAppStore.getState().addCallsignMapImage({
        callsign: 'w1aw',
        imageUrl: 'https://example.com/new.jpg',
        latitude: 41.7,
        longitude: -72.7,
        savedAt: '2024-01-02T00:00:00Z',
      });

      const images = useAppStore.getState().callsignMapImages;
      expect(images).toHaveLength(1);
      expect(images[0].callsign).toBe('w1aw');
      expect(images[0].imageUrl).toBe('https://example.com/new.jpg');
    });

    it('prepends new entry to beginning of list', () => {
      useAppStore.getState().addCallsignMapImage({
        callsign: 'W1AW',
        imageUrl: 'https://example.com/w1aw.jpg',
        latitude: 41.7,
        longitude: -72.7,
        savedAt: '2024-01-01T00:00:00Z',
      });

      useAppStore.getState().addCallsignMapImage({
        callsign: 'EI2KC',
        imageUrl: 'https://example.com/ei2kc.jpg',
        latitude: 52.6,
        longitude: -8.6,
        savedAt: '2024-01-02T00:00:00Z',
      });

      const images = useAppStore.getState().callsignMapImages;
      expect(images).toHaveLength(2);
      expect(images[0].callsign).toBe('EI2KC');
      expect(images[1].callsign).toBe('W1AW');
    });

    it('handles mixed image and no-image entries', () => {
      useAppStore.getState().addCallsignMapImage({
        callsign: 'W1AW',
        imageUrl: 'https://example.com/w1aw.jpg',
        latitude: 41.7,
        longitude: -72.7,
        savedAt: '2024-01-01T00:00:00Z',
      });

      useAppStore.getState().addCallsignMapImage({
        callsign: 'JA1ABC',
        latitude: 35.6,
        longitude: 139.6,
        savedAt: '2024-01-02T00:00:00Z',
      });

      useAppStore.getState().addCallsignMapImage({
        callsign: 'VK3XYZ',
        imageUrl: 'https://example.com/vk3xyz.jpg',
        latitude: -37.8,
        longitude: 144.9,
        savedAt: '2024-01-03T00:00:00Z',
      });

      const images = useAppStore.getState().callsignMapImages;
      expect(images).toHaveLength(3);

      const withImage = images.filter(i => i.imageUrl);
      const withoutImage = images.filter(i => !i.imageUrl);
      expect(withImage).toHaveLength(2);
      expect(withoutImage).toHaveLength(1);
      expect(withoutImage[0].callsign).toBe('JA1ABC');
    });

    it('update from no-image to with-image replaces entry', () => {
      useAppStore.getState().addCallsignMapImage({
        callsign: 'W1AW',
        latitude: 41.7,
        longitude: -72.7,
        savedAt: '2024-01-01T00:00:00Z',
      });

      expect(useAppStore.getState().callsignMapImages[0].imageUrl).toBeUndefined();

      useAppStore.getState().addCallsignMapImage({
        callsign: 'W1AW',
        imageUrl: 'https://example.com/w1aw.jpg',
        latitude: 41.7,
        longitude: -72.7,
        savedAt: '2024-01-02T00:00:00Z',
      });

      const images = useAppStore.getState().callsignMapImages;
      expect(images).toHaveLength(1);
      expect(images[0].imageUrl).toBe('https://example.com/w1aw.jpg');
    });
  });
});
