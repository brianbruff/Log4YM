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
} from '../api/signalr';

interface AppState {
  // Connection status
  isConnected: boolean;
  setConnected: (connected: boolean) => void;

  // Current focused callsign
  focusedCallsign: string | null;
  focusedCallsignInfo: CallsignLookedUpEvent | null;
  setFocusedCallsign: (callsign: string | null) => void;
  setFocusedCallsignInfo: (info: CallsignLookedUpEvent | null) => void;

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
  clearDiscoveredRadios: () => void;

  // SmartUnlink
  smartUnlinkRadios: Map<string, SmartUnlinkRadioAddedEvent>;
  addSmartUnlinkRadio: (radio: SmartUnlinkRadioAddedEvent) => void;
  updateSmartUnlinkRadio: (radio: SmartUnlinkRadioAddedEvent) => void;
  removeSmartUnlinkRadio: (id: string) => void;
  setSmartUnlinkRadios: (radios: SmartUnlinkRadioAddedEvent[]) => void;
}

export const useAppStore = create<AppState>((set) => ({
  // Connection
  isConnected: false,
  setConnected: (connected) => set({ isConnected: connected }),

  // Focused callsign
  focusedCallsign: null,
  focusedCallsignInfo: null,
  setFocusedCallsign: (callsign) => set({ focusedCallsign: callsign }),
  setFocusedCallsignInfo: (info) => set({ focusedCallsignInfo: info }),

  // Rotator
  rotatorPosition: null,
  setRotatorPosition: (position) => set({ rotatorPosition: position }),

  // Rig
  rigStatus: null,
  setRigStatus: (status) => set({ rigStatus: status }),

  // Station
  stationCallsign: 'EI6LF',
  stationGrid: 'IO63',
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
}));
