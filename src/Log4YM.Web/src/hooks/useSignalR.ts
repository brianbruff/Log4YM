import { useEffect, useCallback, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { signalRService, type SmartUnlinkRadioDto, type HamlibRigConfigDto, type SignalRConnectionState } from '../api/signalr';
import { useAppStore, type ConnectionState } from '../store/appStore';
import { useSettingsStore } from '../store/settingsStore';

/**
 * Hook for managing the SignalR connection lifecycle.
 * Should ONLY be called once in App.tsx - not in plugins or other components.
 * This handles connection setup, event handlers, and cleanup.
 */
export function useSignalRConnection() {
  const queryClient = useQueryClient();
  const connectionInitialized = useRef(false);
  const {
    setConnectionState,
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
    clearRadioState,
    addSmartUnlinkRadio,
    updateSmartUnlinkRadio,
    removeSmartUnlinkRadio,
    setSmartUnlinkRadios,
    setQrzSyncProgress,
    setSelectedSpot,
    setLogHistoryCallsignFilter,
    setClusterStatus,
    setCwKeyerStatus,
  } = useAppStore();

  useEffect(() => {
    // Prevent double initialization (React StrictMode)
    if (connectionInitialized.current) {
      return;
    }
    connectionInitialized.current = true;
    // Set up connection state callback - maps SignalR state to app state
    signalRService.setConnectionStateCallback((state: SignalRConnectionState, attempt: number) => {
      setConnectionState(state as ConnectionState, attempt);

      // When disconnected, mark settings as not loaded to prevent saving stale data
      if (state === 'disconnected') {
        useSettingsStore.getState().setNotLoaded();
      }
    });

    // Set up callback to fully rehydrate when connected (or reconnected)
    // This callback is responsible for loading ALL data and then setting state to 'connected'
    signalRService.setOnConnectedCallback(async () => {
      console.log('Connection established, rehydrating application state...');
      try {
        // 1. Reload settings from MongoDB
        console.log('Reloading settings from database...');
        await useSettingsStore.getState().loadSettings();

        // 2. Request all device statuses via SignalR
        console.log('Requesting device statuses...');
        await Promise.all([
          signalRService.requestAntennaGeniusStatus(),
          signalRService.requestPgxlStatus(),
          signalRService.requestRadioStatus(),
          signalRService.requestSmartUnlinkStatus(),
          signalRService.requestRotatorStatus(),
        ]);

        // 3. Request cluster statuses to update connection states
        console.log('Requesting cluster statuses...');
        try {
          const response = await fetch('/api/cluster/status');
          if (response.ok) {
            const statuses = await response.json();
            // Update app store with current cluster statuses
            for (const [clusterId, status] of Object.entries(statuses)) {
              const clusterStatus = status as { clusterId: string; name: string; status: string; errorMessage: string | null };
              setClusterStatus(clusterId, {
                clusterId: clusterStatus.clusterId,
                name: clusterStatus.name,
                status: clusterStatus.status as 'connected' | 'connecting' | 'disconnected' | 'error',
                errorMessage: clusterStatus.errorMessage,
              });
            }
          }
        } catch (err) {
          console.error('Failed to fetch cluster statuses:', err);
        }

        // 4. Invalidate and refetch React Query caches
        console.log('Invalidating query caches...');
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: ['qsos'] }),
          queryClient.invalidateQueries({ queryKey: ['statistics'] }),
        ]);

        console.log('Rehydration complete');
        // 5. Now we can set state to fully connected
        setConnectionState('connected', 0);
      } catch (err) {
        console.error('Error during rehydration:', err);
        // Still set connected even if some things failed - user can manually refresh
        setConnectionState('connected', 0);
      }
    });

    const connect = async () => {
      try {
        setConnectionState('connecting', 0);

        signalRService.setHandlers({
          onCallsignFocused: (evt) => {
            setFocusedCallsign(evt.callsign);
          },
          onCallsignLookedUp: (evt) => {
            // Always add callsign image to map overlay (backend already saved to MongoDB,
            // but we need the in-memory store updated for the current session)
            if (evt.imageUrl && evt.latitude != null && evt.longitude != null) {
              useAppStore.getState().addCallsignMapImage({
                callsign: evt.callsign,
                imageUrl: evt.imageUrl,
                latitude: evt.latitude,
                longitude: evt.longitude,
                name: evt.name ?? undefined,
                country: evt.country ?? undefined,
                grid: evt.grid ?? undefined,
                savedAt: new Date().toISOString(),
              });
            }

            // Only apply focused marker/fly-to if it matches the current focused callsign
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
          onSpotReceived: (evt) => {
            // Add spot to ephemeral in-memory store
            const spot = {
              id: evt.id,
              dxCall: evt.dxCall,
              spotter: evt.spotter,
              frequency: evt.frequency,
              mode: evt.mode,
              comment: evt.comment,
              source: evt.source,
              timestamp: evt.timestamp,
              country: evt.country,
              dxStation: (evt.country || evt.dxcc || evt.grid) ? {
                country: evt.country,
                dxcc: evt.dxcc,
                grid: evt.grid,
              } : undefined,
            };
            useAppStore.getState().addDxClusterSpot(spot);
          },
          onSpotSelected: (evt) => {
            console.log('Spot selected:', evt.dxCall, evt.frequency, evt.mode);
            // Store selected spot for log entry auto-population
            setSelectedSpot(evt);
            // Set the callsign for log history filter
            setLogHistoryCallsignFilter(evt.dxCall);
            // Trigger QRZ lookup for the callsign (will populate name, grid, country)
            setFocusedCallsign(evt.dxCall);
            setFocusedCallsignInfo(null);
            setLookingUpCallsign(true);
            signalRService.focusCallsign({ callsign: evt.dxCall, source: 'cluster-spot' });
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
            // Clear stale frequency/mode data when disconnected or errored
            if (evt.state === 'Disconnected' || evt.state === 'Error') {
              clearRadioState(evt.radioId);
            }
          },
          onRadioStateChanged: (evt) => {
            console.log('Radio state:', evt.radioId, evt.frequencyHz, evt.mode);
            setRadioState(evt);
          },
          onRadioSlicesUpdated: (evt) => {
            console.log('Radio slices updated:', evt.radioId, evt.slices.length);
            setRadioSlices(evt.radioId, evt.slices);
          },
          // CW Keyer handlers
          onCwKeyerStatus: (evt) => {
            console.log('CW keyer status:', evt.radioId, 'isKeying:', evt.isKeying, 'speed:', evt.speedWpm);
            setCwKeyerStatus(evt);
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
          // DX Cluster handler
          onClusterStatusChanged: (evt) => {
            console.log('Cluster status changed:', evt.clusterId, evt.status);
            setClusterStatus(evt.clusterId, {
              clusterId: evt.clusterId,
              name: evt.name,
              status: evt.status,
              errorMessage: evt.errorMessage,
            });
          },
        });

        await signalRService.connect();
        // Device statuses are requested via the onConnectedCallback
        // Connection state is managed by the SignalR service callback
      } catch (error) {
        // Only log if it's not an abort error (which happens during HMR)
        if (!(error instanceof Error && error.name === 'AbortError')) {
          console.error('Failed to connect to SignalR:', error);
        }
        // Connection state is managed by the SignalR service callback
        // It will automatically try to reconnect
      }
    };

    connect();

    // Cleanup only runs when the App itself unmounts (app closing)
    // The connectionInitialized ref prevents this from running on StrictMode re-renders
    return () => {
      connectionInitialized.current = false;
      signalRService.disconnect();
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
}

/**
 * Hook for accessing SignalR methods.
 * Can be called from any component - does NOT manage connection lifecycle.
 * Connection is managed by useSignalRConnection in App.tsx.
 */
export function useSignalR() {
  const {
    setFocusedCallsign,
    setFocusedCallsignInfo,
    setLookingUpCallsign,
  } = useAppStore();

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

  // CW Keyer methods
  const sendCwKey = useCallback(async (radioId: string, message: string, speedWpm?: number) => {
    await signalRService.sendCwKey(radioId, message, speedWpm);
  }, []);

  const stopCwKey = useCallback(async (radioId: string) => {
    await signalRService.stopCwKey(radioId);
  }, []);

  const setCwSpeed = useCallback(async (radioId: string, speedWpm: number) => {
    await signalRService.setCwSpeed(radioId, speedWpm);
  }, []);

  // Hamlib methods (new native library integration)
  const getHamlibRigList = useCallback(async () => {
    await signalRService.getHamlibRigList();
  }, []);

  const getHamlibRigCaps = useCallback(async (modelId: number) => {
    await signalRService.getHamlibRigCaps(modelId);
  }, []);

  const getHamlibSerialPorts = useCallback(async () => {
    await signalRService.getHamlibSerialPorts();
  }, []);

  const getHamlibConfig = useCallback(async () => {
    await signalRService.getHamlibConfig();
  }, []);

  const getHamlibStatus = useCallback(async () => {
    await signalRService.getHamlibStatus();
  }, []);

  const connectHamlibRig = useCallback(async (config: HamlibRigConfigDto) => {
    await signalRService.connectHamlibRig(config);
  }, []);

  const disconnectHamlibRig = useCallback(async () => {
    await signalRService.disconnectHamlibRig();
  }, []);

  const deleteHamlibConfig = useCallback(async () => {
    await signalRService.deleteHamlibConfig();
  }, []);

  // Legacy rigctld methods (kept for backwards compatibility)
  const connectHamlib = useCallback(async (host: string, port: number = 4532, name?: string) => {
    // Map to new native Hamlib with network connection type
    const config: HamlibRigConfigDto = {
      modelId: 2,  // Hamlib NET rigctld protocol
      modelName: name || 'rigctld',
      connectionType: 'Network',
      hostname: host,
      networkPort: port,
      baudRate: 9600,
      dataBits: 8,
      stopBits: 1,
      flowControl: 'None',
      parity: 'None',
      pttType: 'Rig',
      getFrequency: true,
      getMode: true,
      getVfo: true,
      getPtt: true,
      getPower: false,
      getRit: false,
      getXit: false,
      getKeySpeed: false,
      pollIntervalMs: 250
    };
    await signalRService.connectHamlibRig(config);
  }, []);

  const disconnectHamlib = useCallback(async (_radioId: string) => {
    await signalRService.disconnectHamlibRig();
  }, []);

  const deleteTciConfig = useCallback(async () => {
    await signalRService.deleteTciConfig();
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

  // Manual reconnect function
  const reconnect = useCallback(async () => {
    await signalRService.reconnect();
  }, []);

  return {
    isConnected: signalRService.isConnected,
    reconnect,
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
    // CW Keyer
    sendCwKey,
    stopCwKey,
    setCwSpeed,
    // Hamlib (new native integration)
    getHamlibRigList,
    getHamlibRigCaps,
    getHamlibSerialPorts,
    getHamlibConfig,
    getHamlibStatus,
    connectHamlibRig,
    disconnectHamlibRig,
    deleteHamlibConfig,
    // Hamlib (legacy rigctld compatibility)
    connectHamlib,
    disconnectHamlib,
    // TCI direct connection
    connectTci,
    disconnectTci,
    deleteTciConfig,
    // SmartUnlink
    addSmartUnlinkRadio: addSmartUnlinkRadioFn,
    updateSmartUnlinkRadio: updateSmartUnlinkRadioFn,
    removeSmartUnlinkRadio: removeSmartUnlinkRadioFn,
    setSmartUnlinkRadioEnabled,
  };
}
