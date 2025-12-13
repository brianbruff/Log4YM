import { create } from 'zustand';
import type {
  CallsignLookedUpEvent,
  RotatorPositionEvent,
  RigStatusEvent,
  AntennaGeniusStatusEvent,
  AntennaGeniusPortStatus,
  PgxlStatusEvent,
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
}));
