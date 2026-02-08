import { create } from 'zustand';
import type {
  CallsignLookedUpEvent,
  RotatorPositionEvent,
  RigStatusEvent,
  AntennaGeniusStatusEvent,
  AntennaGeniusPortStatus,
  PgxlStatusEvent,
  RadioDiscoveredEvent,
  RadioConnectionState,
  RadioStateChangedEvent,
  RadioSliceInfo,
  SmartUnlinkRadioAddedEvent,
  SpotSelectedEvent,
} from '../api/signalr';
import type { PotaSpot } from '../api/client';

// Connection state enum for detailed tracking
// - disconnected: No connection to backend
// - connecting: Initial connection attempt
// - reconnecting: Attempting to reconnect after disconnect
// - rehydrating: Connected, but reloading all data (settings, device states, etc.)
// - connected: Fully connected and all data loaded
export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting' | 'rehydrating';

interface AppState {
  // Connection status
  isConnected: boolean;
  connectionState: ConnectionState;
  reconnectAttempt: number;
  setConnected: (connected: boolean) => void;
  setConnectionState: (state: ConnectionState, attempt?: number) => void;

  // Current focused callsign
  focusedCallsign: string | null;
  focusedCallsignInfo: CallsignLookedUpEvent | null;
  isLookingUpCallsign: boolean;
  setFocusedCallsign: (callsign: string | null) => void;
  setFocusedCallsignInfo: (info: CallsignLookedUpEvent | null) => void;
  setLookingUpCallsign: (loading: boolean) => void;

  // Rotator
  rotatorPosition: RotatorPositionEvent | null;
  setRotatorPosition: (position: RotatorPositionEvent | null) => void;

  // Rig
  rigStatus: RigStatusEvent | null;
  setRigStatus: (status: RigStatusEvent | null) => void;

  // Station info
  stationCallsign: string;
  stationGrid: string;
  setStationInfo: (callsign: string, grid: string) => void;

  // Antenna Genius
  antennaGeniusDevices: Map<string, AntennaGeniusStatusEvent>;
  setAntennaGeniusStatus: (status: AntennaGeniusStatusEvent) => void;
  updateAntennaGeniusPort: (serial: string, portStatus: AntennaGeniusPortStatus) => void;
  removeAntennaGeniusDevice: (serial: string) => void;

  // PGXL Amplifier
  pgxlDevices: Map<string, PgxlStatusEvent>;
  setPgxlStatus: (status: PgxlStatusEvent) => void;
  removePgxlDevice: (serial: string) => void;
  // PGXL-TCI linking: radio IDs linked to PGXL sides A and B
  pgxlTciLinkA: string | null;
  pgxlTciLinkB: string | null;
  setPgxlTciLink: (side: 'A' | 'B', radioId: string | null) => void;

  // Radio CAT Control
  discoveredRadios: Map<string, RadioDiscoveredEvent>;
  radioConnectionStates: Map<string, RadioConnectionState>;
  radioStates: Map<string, RadioStateChangedEvent>;
  radioSlices: Map<string, RadioSliceInfo[]>;
  selectedRadioId: string | null;
  addDiscoveredRadio: (radio: RadioDiscoveredEvent) => void;
  removeDiscoveredRadio: (radioId: string) => void;
  setRadioConnectionState: (radioId: string, state: RadioConnectionState) => void;
  setRadioState: (state: RadioStateChangedEvent) => void;
  setRadioSlices: (radioId: string, slices: RadioSliceInfo[]) => void;
  setSelectedRadio: (radioId: string | null) => void;
  clearRadioState: (radioId: string) => void;
  clearDiscoveredRadios: () => void;

  // SmartUnlink
  smartUnlinkRadios: Map<string, SmartUnlinkRadioAddedEvent>;
  addSmartUnlinkRadio: (radio: SmartUnlinkRadioAddedEvent) => void;
  updateSmartUnlinkRadio: (radio: SmartUnlinkRadioAddedEvent) => void;
  removeSmartUnlinkRadio: (id: string) => void;
  setSmartUnlinkRadios: (radios: SmartUnlinkRadioAddedEvent[]) => void;

  // QRZ Sync
  qrzSyncProgress: QrzSyncProgress | null;
  setQrzSyncProgress: (progress: QrzSyncProgress | null) => void;

  // Log History Callsign Filter (shared between LogEntry and LogHistory)
  logHistoryCallsignFilter: string | null;
  setLogHistoryCallsignFilter: (callsign: string | null) => void;
  clearCallsignFromAllControls: () => void;

  // Selected DX Cluster spot (for auto-populating log entry)
  selectedSpot: SpotSelectedEvent | null;
  setSelectedSpot: (spot: SpotSelectedEvent | null) => void;

  // DX Cluster connection statuses
  clusterStatuses: Record<string, ClusterStatus>;
  setClusterStatus: (clusterId: string, status: ClusterStatus) => void;

  // POTA spots and map markers
  potaSpots: PotaSpot[];
  showPotaMapMarkers: boolean;
  setPotaSpots: (spots: PotaSpot[]) => void;
  setShowPotaMapMarkers: (show: boolean) => void;

  // DX Cluster spots and map paths
  showDxPathsOnMap: boolean;
  setShowDxPathsOnMap: (show: boolean) => void;
}

export interface ClusterStatus {
  clusterId: string;
  name: string;
  status: 'connected' | 'connecting' | 'disconnected' | 'error';
  errorMessage: string | null;
}

export interface QrzSyncProgress {
  total: number;
  completed: number;
  successful: number;
  failed: number;
  isComplete: boolean;
  currentCallsign: string | null;
  message: string | null;
}

export const useAppStore = create<AppState>((set) => ({
  // Connection
  isConnected: false,
  connectionState: 'disconnected' as ConnectionState,
  reconnectAttempt: 0,
  setConnected: (connected) => set({
    isConnected: connected,
    connectionState: connected ? 'connected' : 'disconnected',
    reconnectAttempt: connected ? 0 : undefined, // Keep attempt count when disconnected
  }),
  setConnectionState: (state, attempt) => set({
    connectionState: state,
    isConnected: state === 'connected',
    reconnectAttempt: attempt ?? 0,
  }),

  // Focused callsign
  focusedCallsign: null,
  focusedCallsignInfo: null,
  isLookingUpCallsign: false,
  setFocusedCallsign: (callsign) => set({ focusedCallsign: callsign }),
  setFocusedCallsignInfo: (info) => set({ focusedCallsignInfo: info, isLookingUpCallsign: false }),
  setLookingUpCallsign: (loading) => set({ isLookingUpCallsign: loading }),

  // Rotator
  rotatorPosition: null,
  setRotatorPosition: (position) => set({ rotatorPosition: position }),

  // Rig
  rigStatus: null,
  setRigStatus: (status) => set({ rigStatus: status }),

  // Station
  stationCallsign: '',
  stationGrid: '',
  setStationInfo: (callsign, grid) => set({ stationCallsign: callsign, stationGrid: grid }),

  // Antenna Genius
  antennaGeniusDevices: new Map(),
  setAntennaGeniusStatus: (status) =>
    set((state) => {
      const devices = new Map(state.antennaGeniusDevices);
      devices.set(status.deviceSerial, status);
      return { antennaGeniusDevices: devices };
    }),
  updateAntennaGeniusPort: (serial, portStatus) =>
    set((state) => {
      const devices = new Map(state.antennaGeniusDevices);
      const device = devices.get(serial);
      if (device) {
        const updated = {
          ...device,
          portA: portStatus.portId === 1 ? portStatus : device.portA,
          portB: portStatus.portId === 2 ? portStatus : device.portB,
        };
        devices.set(serial, updated);
      }
      return { antennaGeniusDevices: devices };
    }),
  removeAntennaGeniusDevice: (serial) =>
    set((state) => {
      const devices = new Map(state.antennaGeniusDevices);
      devices.delete(serial);
      return { antennaGeniusDevices: devices };
    }),

  // PGXL Amplifier
  pgxlDevices: new Map(),
  setPgxlStatus: (status) =>
    set((state) => {
      const devices = new Map(state.pgxlDevices);
      devices.set(status.serial, status);
      return { pgxlDevices: devices };
    }),
  removePgxlDevice: (serial) =>
    set((state) => {
      const devices = new Map(state.pgxlDevices);
      devices.delete(serial);
      return { pgxlDevices: devices };
    }),
  // PGXL-TCI linking (initialized from localStorage)
  pgxlTciLinkA: localStorage.getItem('pgxlTciLinkA') || null,
  pgxlTciLinkB: localStorage.getItem('pgxlTciLinkB') || null,
  setPgxlTciLink: (side, radioId) => {
    // Persist to localStorage
    if (radioId) {
      localStorage.setItem(side === 'A' ? 'pgxlTciLinkA' : 'pgxlTciLinkB', radioId);
    } else {
      localStorage.removeItem(side === 'A' ? 'pgxlTciLinkA' : 'pgxlTciLinkB');
    }
    return set(side === 'A' ? { pgxlTciLinkA: radioId } : { pgxlTciLinkB: radioId });
  },

  // Radio CAT Control
  discoveredRadios: new Map(),
  radioConnectionStates: new Map(),
  radioStates: new Map(),
  radioSlices: new Map(),
  selectedRadioId: null,
  addDiscoveredRadio: (radio) =>
    set((state) => {
      const radios = new Map(state.discoveredRadios);
      radios.set(radio.id, radio);
      return { discoveredRadios: radios };
    }),
  removeDiscoveredRadio: (radioId) =>
    set((state) => {
      const radios = new Map(state.discoveredRadios);
      radios.delete(radioId);
      const connectionStates = new Map(state.radioConnectionStates);
      connectionStates.delete(radioId);
      const radioStates = new Map(state.radioStates);
      radioStates.delete(radioId);
      return { discoveredRadios: radios, radioConnectionStates: connectionStates, radioStates };
    }),
  setRadioConnectionState: (radioId, connectionState) =>
    set((state) => {
      const connectionStates = new Map(state.radioConnectionStates);
      connectionStates.set(radioId, connectionState);
      return { radioConnectionStates: connectionStates };
    }),
  setRadioState: (radioState) =>
    set((state) => {
      const radioStates = new Map(state.radioStates);
      radioStates.set(radioState.radioId, radioState);
      return { radioStates };
    }),
  setRadioSlices: (radioId, slices) =>
    set((state) => {
      const radioSlices = new Map(state.radioSlices);
      radioSlices.set(radioId, slices);
      return { radioSlices };
    }),
  setSelectedRadio: (radioId) => set({ selectedRadioId: radioId }),
  clearRadioState: (radioId) =>
    set((state) => {
      const radioStates = new Map(state.radioStates);
      radioStates.delete(radioId);
      return { radioStates };
    }),
  clearDiscoveredRadios: () =>
    set({
      discoveredRadios: new Map(),
      radioConnectionStates: new Map(),
      radioStates: new Map(),
      radioSlices: new Map(),
      selectedRadioId: null,
    }),

  // SmartUnlink
  smartUnlinkRadios: new Map(),
  addSmartUnlinkRadio: (radio) =>
    set((state) => {
      const radios = new Map(state.smartUnlinkRadios);
      radios.set(radio.id, radio);
      return { smartUnlinkRadios: radios };
    }),
  updateSmartUnlinkRadio: (radio) =>
    set((state) => {
      const radios = new Map(state.smartUnlinkRadios);
      radios.set(radio.id, radio);
      return { smartUnlinkRadios: radios };
    }),
  removeSmartUnlinkRadio: (id) =>
    set((state) => {
      const radios = new Map(state.smartUnlinkRadios);
      radios.delete(id);
      return { smartUnlinkRadios: radios };
    }),
  setSmartUnlinkRadios: (radios) =>
    set(() => {
      const map = new Map<string, SmartUnlinkRadioAddedEvent>();
      radios.forEach((radio) => map.set(radio.id, radio));
      return { smartUnlinkRadios: map };
    }),

  // QRZ Sync
  qrzSyncProgress: null,
  setQrzSyncProgress: (progress) => set({ qrzSyncProgress: progress }),

  // Log History Callsign Filter
  logHistoryCallsignFilter: null,
  setLogHistoryCallsignFilter: (callsign) => set({ logHistoryCallsignFilter: callsign }),
  clearCallsignFromAllControls: () => set({
    focusedCallsign: null,
    focusedCallsignInfo: null,
    logHistoryCallsignFilter: null,
    isLookingUpCallsign: false,
    selectedSpot: null,
  }),

  // Selected DX Cluster spot
  selectedSpot: null,
  setSelectedSpot: (spot) => set({ selectedSpot: spot }),

  // DX Cluster connection statuses
  clusterStatuses: {},
  setClusterStatus: (clusterId, status) =>
    set((state) => ({
      clusterStatuses: {
        ...state.clusterStatuses,
        [clusterId]: status,
      },
    })),

  // POTA spots and map markers
  potaSpots: [],
  showPotaMapMarkers: false,
  setPotaSpots: (spots) => set({ potaSpots: spots }),
  setShowPotaMapMarkers: (show) => set({ showPotaMapMarkers: show }),

  // DX Cluster spots and map paths
  showDxPathsOnMap: false,
  setShowDxPathsOnMap: (show) => set({ showDxPathsOnMap: show }),
}));
