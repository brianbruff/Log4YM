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
});
