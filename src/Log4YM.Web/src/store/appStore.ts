import { create } from 'zustand';
import type { CallsignLookedUpEvent, RotatorPositionEvent, RigStatusEvent } from '../api/signalr';

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
}));
