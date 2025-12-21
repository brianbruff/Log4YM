import { useEffect, useCallback } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { signalRService, type SmartUnlinkRadioDto } from '../api/signalr';
import { useAppStore } from '../store/appStore';

export function useSignalR() {
  const queryClient = useQueryClient();
  const {
    setConnected,
    setFocusedCallsign,
    setFocusedCallsignInfo,
    setLookingUpCallsign,
    setRotatorPosition,
    setRigStatus,
    setAntennaGeniusStatus,
    updateAntennaGeniusPort,
    removeAntennaGeniusDevice,
    setPgxlStatus,
    removePgxlDevice,
    addDiscoveredRadio,
    removeDiscoveredRadio,
    setRadioConnectionState,
    setRadioState,
    setRadioSlices,
    addSmartUnlinkRadio,
    updateSmartUnlinkRadio,
    removeSmartUnlinkRadio,
    setSmartUnlinkRadios,
    setQrzSyncProgress,
  } = useAppStore();

  useEffect(() => {
    const connect = async () => {
      try {
        signalRService.setHandlers({
          onCallsignFocused: (evt) => {
            setFocusedCallsign(evt.callsign);
          },
          onCallsignLookedUp: (evt) => {
            // Only apply result if it matches the current focused callsign
            // This prevents out-of-order responses from showing stale data
            const currentCallsign = useAppStore.getState().focusedCallsign;
            if (evt.callsign?.toUpperCase() === currentCallsign?.toUpperCase()) {
              setFocusedCallsignInfo(evt);
              setLookingUpCallsign(false);
            }
          },
          onQsoLogged: () => {
            // Invalidate QSO queries to refetch
            queryClient.invalidateQueries({ queryKey: ['qsos'] });
            queryClient.invalidateQueries({ queryKey: ['statistics'] });
          },
          onSpotReceived: () => {
            // Invalidate spots query
            queryClient.invalidateQueries({ queryKey: ['spots'] });
          },
          onRotatorPosition: (evt) => {
            console.log('Rotator position:', evt.currentAzimuth, 'moving:', evt.isMoving);
            setRotatorPosition(evt);
          },
          onRigStatus: (evt) => {
            setRigStatus(evt);
          },
          // Antenna Genius handlers
          onAntennaGeniusDiscovered: (evt) => {
            console.log('Antenna Genius discovered:', evt.name, evt.serial);
          },
          onAntennaGeniusDisconnected: (evt) => {
            console.log('Antenna Genius disconnected:', evt.serial);
            removeAntennaGeniusDevice(evt.serial);
          },
          onAntennaGeniusStatus: (evt) => {
            console.log('Antenna Genius status:', evt.deviceName, evt.isConnected);
            setAntennaGeniusStatus(evt);
          },
          onAntennaGeniusPortChanged: (evt) => {
            console.log('Antenna Genius port changed:', evt.portId, evt.rxAntenna);
            updateAntennaGeniusPort(evt.deviceSerial, {
              portId: evt.portId,
              auto: evt.auto,
              source: evt.source,
              band: evt.band,
              rxAntenna: evt.rxAntenna,
              txAntenna: evt.txAntenna,
              isTransmitting: evt.isTransmitting,
              isInhibited: evt.isInhibited,
            });
          },
          // PGXL handlers
          onPgxlDiscovered: (evt) => {
            console.log('PGXL discovered:', evt.serial, evt.ipAddress);
          },
          onPgxlDisconnected: (evt) => {
            console.log('PGXL disconnected:', evt.serial);
            removePgxlDevice(evt.serial);
          },
          onPgxlStatus: (evt) => {
            console.log('PGXL status:', evt.serial, 'isOperating:', evt.isOperating, 'isTransmitting:', evt.isTransmitting);
            setPgxlStatus(evt);
          },
          // Radio CAT Control handlers
          onRadioDiscovered: (evt) => {
            console.log('Radio discovered:', evt.model, evt.ipAddress);
            addDiscoveredRadio(evt);
          },
          onRadioRemoved: (evt) => {
            console.log('Radio removed:', evt.id);
            removeDiscoveredRadio(evt.id);
          },
          onRadioConnectionStateChanged: (evt) => {
            console.log('Radio connection state:', evt.radioId, evt.state);
            setRadioConnectionState(evt.radioId, evt.state);
          },
          onRadioStateChanged: (evt) => {
            console.log('Radio state:', evt.radioId, evt.frequencyHz, evt.mode);
            setRadioState(evt);
          },
          onRadioSlicesUpdated: (evt) => {
            console.log('Radio slices updated:', evt.radioId, evt.slices.length);
            setRadioSlices(evt.radioId, evt.slices);
          },
          // SmartUnlink handlers
          onSmartUnlinkRadioAdded: (evt) => {
            console.log('SmartUnlink radio added:', evt.name, evt.model);
            addSmartUnlinkRadio(evt);
          },
          onSmartUnlinkRadioUpdated: (evt) => {
            console.log('SmartUnlink radio updated:', evt.name, evt.enabled);
            updateSmartUnlinkRadio(evt);
          },
          onSmartUnlinkRadioRemoved: (evt) => {
            console.log('SmartUnlink radio removed:', evt.id);
            removeSmartUnlinkRadio(evt.id);
          },
          onSmartUnlinkStatus: (evt) => {
            console.log('SmartUnlink status:', evt.radios.length, 'radios');
            setSmartUnlinkRadios(evt.radios);
          },
          // QRZ Sync handler
          onQrzSyncProgress: (evt) => {
            console.log('QRZ sync progress:', evt.completed, '/', evt.total);
            setQrzSyncProgress(evt);
          },
        });

        await signalRService.connect();

        // Only request statuses if actually connected
        if (signalRService.isConnected) {
          // Request current device statuses after connection
          await signalRService.requestAntennaGeniusStatus();
          await signalRService.requestPgxlStatus();
          await signalRService.requestRadioStatus();
          await signalRService.requestSmartUnlinkStatus();
          await signalRService.requestRotatorStatus();
          setConnected(true);
        }
      } catch (error) {
        // Only log if it's not an abort error (which happens during HMR)
        if (!(error instanceof Error && error.name === 'AbortError')) {
          console.error('Failed to connect to SignalR:', error);
        }
        setConnected(false);
      }
    };

    connect();

    return () => {
      signalRService.disconnect();
      setConnected(false);
    };
  }, [queryClient, setConnected, setFocusedCallsign, setFocusedCallsignInfo, setLookingUpCallsign, setRotatorPosition, setRigStatus, setAntennaGeniusStatus, updateAntennaGeniusPort, removeAntennaGeniusDevice, setPgxlStatus, removePgxlDevice, addDiscoveredRadio, removeDiscoveredRadio, setRadioConnectionState, setRadioState, setRadioSlices, addSmartUnlinkRadio, updateSmartUnlinkRadio, removeSmartUnlinkRadio, setSmartUnlinkRadios, setQrzSyncProgress]);

  const focusCallsign = useCallback(async (callsign: string, source: string) => {
    setFocusedCallsign(callsign);
    setFocusedCallsignInfo(null); // Clear old info to prevent stale data showing
    setLookingUpCallsign(true);
    await signalRService.focusCallsign({ callsign, source });
  }, [setFocusedCallsign, setFocusedCallsignInfo, setLookingUpCallsign]);

  const selectSpot = useCallback(async (dxCall: string, frequency: number, mode?: string) => {
    await signalRService.selectSpot({ dxCall, frequency, mode });
  }, []);

  const commandRotator = useCallback(async (targetAzimuth: number, source: string) => {
    await signalRService.commandRotator({ rotatorId: 'default', targetAzimuth, source });
  }, []);

  const selectAntenna = useCallback(async (deviceSerial: string, portId: number, antennaId: number) => {
    await signalRService.selectAntenna(deviceSerial, portId, antennaId);
  }, []);

  const setPgxlOperate = useCallback(async (serial: string) => {
    await signalRService.setPgxlOperate(serial);
  }, []);

  const setPgxlStandby = useCallback(async (serial: string) => {
    await signalRService.setPgxlStandby(serial);
  }, []);

  const disablePgxlFlexRadioPairing = useCallback(async (serial: string, slice: string) => {
    await signalRService.disablePgxlFlexRadioPairing(serial, slice);
  }, []);

  // Radio CAT Control methods
  const startRadioDiscovery = useCallback(async (type: 'FlexRadio' | 'Tci') => {
    await signalRService.startRadioDiscovery(type);
  }, []);

  const stopRadioDiscovery = useCallback(async (type: 'FlexRadio' | 'Tci') => {
    await signalRService.stopRadioDiscovery(type);
  }, []);

  const connectRadio = useCallback(async (radioId: string) => {
    await signalRService.connectRadio(radioId);
  }, []);

  const disconnectRadio = useCallback(async (radioId: string) => {
    await signalRService.disconnectRadio(radioId);
  }, []);

  const selectRadioSlice = useCallback(async (radioId: string, sliceId: string) => {
    await signalRService.selectRadioSlice(radioId, sliceId);
  }, []);

  const selectRadioInstance = useCallback(async (radioId: string, instance: number) => {
    await signalRService.selectRadioInstance(radioId, instance);
  }, []);

  // Hamlib (rigctld) methods
  const connectHamlib = useCallback(async (host: string, port: number = 4532, name?: string) => {
    await signalRService.connectHamlib(host, port, name);
  }, []);

  const disconnectHamlib = useCallback(async (radioId: string) => {
    await signalRService.disconnectHamlib(radioId);
  }, []);

  // TCI direct connection methods
  const connectTci = useCallback(async (host: string, port: number = 50001, name?: string) => {
    await signalRService.connectTci(host, port, name);
  }, []);

  const disconnectTci = useCallback(async (radioId: string) => {
    await signalRService.disconnectTci(radioId);
  }, []);

  // SmartUnlink methods
  const addSmartUnlinkRadioFn = useCallback(async (dto: SmartUnlinkRadioDto) => {
    await signalRService.addSmartUnlinkRadio(dto);
  }, []);

  const updateSmartUnlinkRadioFn = useCallback(async (dto: SmartUnlinkRadioDto) => {
    await signalRService.updateSmartUnlinkRadio(dto);
  }, []);

  const removeSmartUnlinkRadioFn = useCallback(async (id: string) => {
    await signalRService.removeSmartUnlinkRadio(id);
  }, []);

  const setSmartUnlinkRadioEnabled = useCallback(async (id: string, enabled: boolean) => {
    await signalRService.setSmartUnlinkRadioEnabled(id, enabled);
  }, []);

  return {
    isConnected: signalRService.isConnected,
    focusCallsign,
    selectSpot,
    commandRotator,
    selectAntenna,
    setPgxlOperate,
    setPgxlStandby,
    disablePgxlFlexRadioPairing,
    // Radio CAT Control
    startRadioDiscovery,
    stopRadioDiscovery,
    connectRadio,
    disconnectRadio,
    selectRadioSlice,
    selectRadioInstance,
    // Hamlib (rigctld)
    connectHamlib,
    disconnectHamlib,
    // TCI direct connection
    connectTci,
    disconnectTci,
    // SmartUnlink
    addSmartUnlinkRadio: addSmartUnlinkRadioFn,
    updateSmartUnlinkRadio: updateSmartUnlinkRadioFn,
    removeSmartUnlinkRadio: removeSmartUnlinkRadioFn,
    setSmartUnlinkRadioEnabled,
  };
}
