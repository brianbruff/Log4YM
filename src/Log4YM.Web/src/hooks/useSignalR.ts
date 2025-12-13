import { useEffect, useCallback } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { signalRService } from '../api/signalr';
import { useAppStore } from '../store/appStore';

export function useSignalR() {
  const queryClient = useQueryClient();
  const {
    setConnected,
    setFocusedCallsign,
    setFocusedCallsignInfo,
    setRotatorPosition,
    setRigStatus,
    setAntennaGeniusStatus,
    updateAntennaGeniusPort,
    removeAntennaGeniusDevice,
    setPgxlStatus,
    removePgxlDevice,
    setSmartUnlinkRadio,
    setSmartUnlinkRadios,
    removeSmartUnlinkRadio,
  } = useAppStore();

  useEffect(() => {
    const connect = async () => {
      try {
        signalRService.setHandlers({
          onCallsignFocused: (evt) => {
            setFocusedCallsign(evt.callsign);
          },
          onCallsignLookedUp: (evt) => {
            setFocusedCallsignInfo(evt);
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
            setPgxlStatus(evt);
          },
          // SmartUnlink handlers
          onSmartUnlinkRadioAdded: (evt) => {
            console.log('SmartUnlink radio added:', evt.name, evt.ipAddress);
            setSmartUnlinkRadio(evt);
          },
          onSmartUnlinkRadioUpdated: (evt) => {
            console.log('SmartUnlink radio updated:', evt.name, evt.enabled);
            setSmartUnlinkRadio(evt);
          },
          onSmartUnlinkRadioRemoved: (evt) => {
            console.log('SmartUnlink radio removed:', evt.id);
            removeSmartUnlinkRadio(evt.id);
          },
          onSmartUnlinkStatus: (evt) => {
            console.log('SmartUnlink status received:', evt.radios.length, 'radios');
            setSmartUnlinkRadios(evt.radios);
          },
        });

        await signalRService.connect();

        // Request current device statuses after connection
        await signalRService.requestAntennaGeniusStatus();
        await signalRService.requestPgxlStatus();
        await signalRService.requestSmartUnlinkStatus();
        setConnected(true);
      } catch (error) {
        console.error('Failed to connect to SignalR:', error);
        setConnected(false);
      }
    };

    connect();

    return () => {
      signalRService.disconnect();
      setConnected(false);
    };
  }, [queryClient, setConnected, setFocusedCallsign, setFocusedCallsignInfo, setRotatorPosition, setRigStatus, setAntennaGeniusStatus, updateAntennaGeniusPort, removeAntennaGeniusDevice, setPgxlStatus, removePgxlDevice, setSmartUnlinkRadio, setSmartUnlinkRadios, removeSmartUnlinkRadio]);

  const focusCallsign = useCallback(async (callsign: string, source: string) => {
    setFocusedCallsign(callsign);
    await signalRService.focusCallsign({ callsign, source });
  }, [setFocusedCallsign]);

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

  // SmartUnlink callbacks
  const addSmartUnlinkRadio = useCallback(async (dto: Parameters<typeof signalRService.addSmartUnlinkRadio>[0]) => {
    await signalRService.addSmartUnlinkRadio(dto);
  }, []);

  const updateSmartUnlinkRadio = useCallback(async (dto: Parameters<typeof signalRService.updateSmartUnlinkRadio>[0]) => {
    await signalRService.updateSmartUnlinkRadio(dto);
  }, []);

  const removeSmartUnlinkRadioById = useCallback(async (id: string) => {
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
    addSmartUnlinkRadio,
    updateSmartUnlinkRadio,
    removeSmartUnlinkRadio: removeSmartUnlinkRadioById,
    setSmartUnlinkRadioEnabled,
  };
}
