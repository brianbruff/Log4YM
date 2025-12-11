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
        });

        await signalRService.connect();

        // Request current Antenna Genius status after connection
        await signalRService.requestAntennaGeniusStatus();
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
  }, [queryClient, setConnected, setFocusedCallsign, setFocusedCallsignInfo, setRotatorPosition, setRigStatus, setAntennaGeniusStatus, updateAntennaGeniusPort, removeAntennaGeniusDevice]);

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

  return {
    isConnected: signalRService.isConnected,
    focusCallsign,
    selectSpot,
    commandRotator,
    selectAntenna,
  };
}
