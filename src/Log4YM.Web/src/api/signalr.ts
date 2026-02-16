import * as signalR from '@microsoft/signalr';

export interface CallsignFocusedEvent {
  callsign: string;
  source: string;
  grid?: string;
  frequency?: number;
  mode?: string;
}

export interface CallsignLookedUpEvent {
  callsign: string;
  name?: string;
  grid?: string;
  latitude?: number;
  longitude?: number;
  country?: string;
  dxcc?: number;
  cqZone?: number;
  ituZone?: number;
  state?: string;
  imageUrl?: string;
  bearing?: number;
  distance?: number;
}

export interface QsoLoggedEvent {
  id: string;
  callsign: string;
  qsoDate: string;
  timeOn: string;
  band: string;
  mode: string;
  frequency?: number;
  rstSent?: string;
  rstRcvd?: string;
  grid?: string;
}

export interface SpotReceivedEvent {
  id: string;
  dxCall: string;
  spotter: string;
  frequency: number;
  mode?: string;
  comment?: string;
  timestamp: string;
  source: string;
  country?: string;
  dxcc?: number;
  grid?: string;
}

export interface SpotSelectedEvent {
  dxCall: string;
  frequency: number;
  mode?: string;
  grid?: string;
}

export interface RotatorPositionEvent {
  rotatorId: string;
  currentAzimuth: number;
  isMoving: boolean;
  targetAzimuth?: number;
}

export interface RotatorCommandEvent {
  rotatorId: string;
  targetAzimuth: number;
  source: string;
}

export interface RigStatusEvent {
  rigId: string;
  frequency: number;
  mode: string;
  isTransmitting: boolean;
}

// Antenna Genius types
export interface AntennaGeniusDiscoveredEvent {
  ipAddress: string;
  port: number;
  version: string;
  serial: string;
  name: string;
  radioPorts: number;
  antennaPorts: number;
  mode: string;
  uptime: number;
}

export interface AntennaGeniusDisconnectedEvent {
  serial: string;
}

export interface AntennaGeniusAntennaInfo {
  id: number;
  name: string;
  txBandMask: number;
  rxBandMask: number;
  inbandMask: number;
}

export interface AntennaGeniusBandInfo {
  id: number;
  name: string;
  freqStart: number;
  freqStop: number;
}

export interface AntennaGeniusPortStatus {
  portId: number;
  auto: boolean;
  source: string;
  band: number;
  rxAntenna: number;
  txAntenna: number;
  isTransmitting: boolean;
  isInhibited: boolean;
}

export interface AntennaGeniusStatusEvent {
  deviceSerial: string;
  deviceName: string;
  ipAddress: string;
  version: string;
  isConnected: boolean;
  antennas: AntennaGeniusAntennaInfo[];
  bands: AntennaGeniusBandInfo[];
  portA: AntennaGeniusPortStatus;
  portB: AntennaGeniusPortStatus;
}

export interface AntennaGeniusPortChangedEvent {
  deviceSerial: string;
  portId: number;
  auto: boolean;
  source: string;
  band: number;
  rxAntenna: number;
  txAntenna: number;
  isTransmitting: boolean;
  isInhibited: boolean;
}

export interface SelectAntennaCommand {
  deviceSerial: string;
  portId: number;
  antennaId: number;
}

// PGXL Amplifier types
export interface PgxlDiscoveredEvent {
  ipAddress: string;
  port: number;
  serial: string;
  model: string;
}

export interface PgxlDisconnectedEvent {
  serial: string;
}

export interface PgxlMeters {
  forwardPowerDbm: number;
  forwardPowerWatts: number;
  returnLossDb: number;
  swrRatio: number;
  drivePowerDbm: number;
  paCurrent: number;
  temperatureC: number;
}

export interface PgxlSetup {
  bandSource: string;
  selectedAntenna: number;
  attenuatorEnabled: boolean;
  biasOffset: number;
  pttDelay: number;
  keyDelay: number;
  highSwr: boolean;
  overTemp: boolean;
  overCurrent: boolean;
}

export interface PgxlStatusEvent {
  serial: string;
  ipAddress: string;
  isConnected: boolean;
  isOperating: boolean;
  isTransmitting: boolean;
  band: string;
  biasA: string;
  biasB: string;
  meters: PgxlMeters;
  setup: PgxlSetup;
}

export interface SetPgxlOperateCommand {
  serial: string;
}

export interface SetPgxlStandbyCommand {
  serial: string;
}

export interface DisablePgxlFlexRadioPairingCommand {
  serial: string;
  slice: string;
}

// Radio CAT Control types
export type RadioType = 'FlexRadio' | 'Tci' | 'Hamlib';

export type RadioConnectionState =
  | 'Disconnected'
  | 'Discovering'
  | 'Connecting'
  | 'Connected'
  | 'Monitoring'
  | 'Error';

export interface RadioDiscoveredEvent {
  id: string;
  type: RadioType;
  model: string;
  ipAddress: string;
  port: number;
  nickname?: string;
  slices?: string[];
}

export interface RadioRemovedEvent {
  id: string;
}

export interface RadioConnectionStateChangedEvent {
  radioId: string;
  state: RadioConnectionState;
  errorMessage?: string;
}

export interface RadioStateChangedEvent {
  radioId: string;
  frequencyHz: number;
  mode: string;
  isTransmitting: boolean;
  band: string;
  sliceOrInstance?: string;
}

export interface RadioSliceInfo {
  id: string;
  letter: string;
  frequencyHz: number;
  mode: string;
  isActive: boolean;
}

export interface RadioSlicesUpdatedEvent {
  radioId: string;
  slices: RadioSliceInfo[];
}

// CW Keyer types
export interface CwKeyerStatusEvent {
  radioId: string;
  isKeying: boolean;
  speedWpm: number;
  currentMessage?: string;
}

export interface SendCwKeyCommand {
  radioId: string;
  message: string;
  speedWpm?: number;
}

export interface StopCwKeyCommand {
  radioId: string;
}

export interface SetCwSpeedCommand {
  radioId: string;
  speedWpm: number;
}

export interface StartRadioDiscoveryCommand {
  type: RadioType;
}

export interface StopRadioDiscoveryCommand {
  type: RadioType;
}

export interface ConnectRadioCommand {
  radioId: string;
}

export interface DisconnectRadioCommand {
  radioId: string;
}

export interface SelectRadioSliceCommand {
  radioId: string;
  sliceId: string;
}

export interface SelectRadioInstanceCommand {
  radioId: string;
  instance: number;
}

// Hamlib Configuration types
export type HamlibConnectionType = 'Serial' | 'Network';
export type HamlibDataBits = 5 | 6 | 7 | 8;
export type HamlibStopBits = 1 | 2;
export type HamlibFlowControl = 'None' | 'Hardware' | 'Software';
export type HamlibParity = 'None' | 'Even' | 'Odd' | 'Mark' | 'Space';
export type HamlibPttType = 'None' | 'Rig' | 'Dtr' | 'Rts';

export interface HamlibRigModelInfo {
  modelId: number;
  manufacturer: string;
  model: string;
  version: string;
  displayName: string;
}

export interface HamlibRigCapabilities {
  canGetFreq: boolean;
  canGetMode: boolean;
  canGetVfo: boolean;
  canGetPtt: boolean;
  canGetPower: boolean;
  canGetRit: boolean;
  canGetXit: boolean;
  canGetKeySpeed: boolean;
  canSendMorse: boolean;
  defaultDataBits: number;
  defaultStopBits: number;
  isNetworkOnly: boolean;
  supportsSerial: boolean;
  supportsNetwork: boolean;
}

export interface HamlibRigConfigDto {
  modelId: number;
  modelName: string;
  connectionType: HamlibConnectionType;
  serialPort?: string;
  baudRate: number;
  dataBits: HamlibDataBits;
  stopBits: HamlibStopBits;
  flowControl: HamlibFlowControl;
  parity: HamlibParity;
  hostname?: string;
  networkPort: number;
  pttType: HamlibPttType;
  pttPort?: string;
  getFrequency: boolean;
  getMode: boolean;
  getVfo: boolean;
  getPtt: boolean;
  getPower: boolean;
  getRit: boolean;
  getXit: boolean;
  getKeySpeed: boolean;
  pollIntervalMs: number;
}

export interface HamlibRigListEvent {
  rigs: HamlibRigModelInfo[];
}

export interface HamlibRigCapsEvent {
  modelId: number;
  capabilities: HamlibRigCapabilities;
}

export interface HamlibSerialPortsEvent {
  ports: string[];
}

export interface HamlibConfigLoadedEvent {
  config: HamlibRigConfigDto | null;
}

export interface HamlibStatusEvent {
  isInitialized: boolean;
  isConnected: boolean;
  radioId: string | null;
  errorMessage: string | null;
}

export const HAMLIB_BAUD_RATES = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200] as const;

// SmartUnlink types
export const FLEX_RADIO_MODELS = [
  'FLEX-5100',   // Aurora series
  'FLEX-5200',   // Aurora series
  'FLEX-6400',   // Signature series
  'FLEX-6400M',  // Signature series with ATU
  'FLEX-6600',   // Signature series
  'FLEX-6600M',  // Signature series with ATU
  'FLEX-6700',   // Signature series
  'FLEX-8400',   // Maestro series
  'FLEX-8600',   // Maestro series
  'FlexRadio',   // Generic placeholder
] as const;

export type FlexRadioModel = typeof FLEX_RADIO_MODELS[number];

export interface SmartUnlinkRadioDto {
  id?: string;
  name: string;
  ipAddress: string;
  model: string;
  serialNumber: string;
  callsign?: string;
  enabled: boolean;
  version?: string;
}

export interface SmartUnlinkRadioAddedEvent {
  id: string;
  name: string;
  ipAddress: string;
  model: string;
  serialNumber: string;
  callsign?: string;
  enabled: boolean;
  version: string;
}

export interface SmartUnlinkRadioUpdatedEvent {
  id: string;
  name: string;
  ipAddress: string;
  model: string;
  serialNumber: string;
  callsign?: string;
  enabled: boolean;
  version: string;
}

export interface SmartUnlinkRadioRemovedEvent {
  id: string;
}

export interface SmartUnlinkStatusEvent {
  radios: SmartUnlinkRadioAddedEvent[];
}

// QRZ Sync types
export interface QrzSyncProgressEvent {
  total: number;
  completed: number;
  successful: number;
  failed: number;
  isComplete: boolean;
  currentCallsign: string | null;
  message: string | null;
}

// DX Cluster types
export interface ClusterStatusChangedEvent {
  clusterId: string;
  name: string;
  status: 'connected' | 'connecting' | 'disconnected' | 'error';
  errorMessage: string | null;
}

// Connection state for tracking
export type SignalRConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting' | 'rehydrating';

type ConnectionStateCallback = (state: SignalRConnectionState, attempt: number) => void;

type EventHandlers = {
  onCallsignFocused?: (evt: CallsignFocusedEvent) => void;
  onCallsignLookedUp?: (evt: CallsignLookedUpEvent) => void;
  onQsoLogged?: (evt: QsoLoggedEvent) => void;
  onSpotReceived?: (evt: SpotReceivedEvent) => void;
  onSpotSelected?: (evt: SpotSelectedEvent) => void;
  onRotatorPosition?: (evt: RotatorPositionEvent) => void;
  onRigStatus?: (evt: RigStatusEvent) => void;
  // Antenna Genius handlers
  onAntennaGeniusDiscovered?: (evt: AntennaGeniusDiscoveredEvent) => void;
  onAntennaGeniusDisconnected?: (evt: AntennaGeniusDisconnectedEvent) => void;
  onAntennaGeniusStatus?: (evt: AntennaGeniusStatusEvent) => void;
  onAntennaGeniusPortChanged?: (evt: AntennaGeniusPortChangedEvent) => void;
  // PGXL handlers
  onPgxlDiscovered?: (evt: PgxlDiscoveredEvent) => void;
  onPgxlDisconnected?: (evt: PgxlDisconnectedEvent) => void;
  onPgxlStatus?: (evt: PgxlStatusEvent) => void;
  // Radio CAT Control handlers
  onRadioDiscovered?: (evt: RadioDiscoveredEvent) => void;
  onRadioRemoved?: (evt: RadioRemovedEvent) => void;
  onRadioConnectionStateChanged?: (evt: RadioConnectionStateChangedEvent) => void;
  onRadioStateChanged?: (evt: RadioStateChangedEvent) => void;
  onRadioSlicesUpdated?: (evt: RadioSlicesUpdatedEvent) => void;
  // CW Keyer handlers
  onCwKeyerStatus?: (evt: CwKeyerStatusEvent) => void;
  // Hamlib configuration handlers
  onHamlibRigList?: (evt: HamlibRigListEvent) => void;
  onHamlibRigCaps?: (evt: HamlibRigCapsEvent) => void;
  onHamlibSerialPorts?: (evt: HamlibSerialPortsEvent) => void;
  onHamlibConfigLoaded?: (evt: HamlibConfigLoadedEvent) => void;
  onHamlibStatus?: (evt: HamlibStatusEvent) => void;
  // SmartUnlink handlers
  onSmartUnlinkRadioAdded?: (evt: SmartUnlinkRadioAddedEvent) => void;
  onSmartUnlinkRadioUpdated?: (evt: SmartUnlinkRadioUpdatedEvent) => void;
  onSmartUnlinkRadioRemoved?: (evt: SmartUnlinkRadioRemovedEvent) => void;
  onSmartUnlinkStatus?: (evt: SmartUnlinkStatusEvent) => void;
  // QRZ Sync handlers
  onQrzSyncProgress?: (evt: QrzSyncProgressEvent) => void;
  // DX Cluster handlers
  onClusterStatusChanged?: (evt: ClusterStatusChangedEvent) => void;
};

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private handlers: EventHandlers = {};
  private connectPromise: Promise<void> | null = null;
  private disconnectPending = false;
  private connectionStateCallback: ConnectionStateCallback | null = null;
  private onConnectedCallback: (() => Promise<void>) | null = null;
  private reconnectAttempt = 0;
  private isManualDisconnect = false;
  private reconnectTimeoutId: ReturnType<typeof setTimeout> | null = null;

  // Set callback for connection state changes
  setConnectionStateCallback(callback: ConnectionStateCallback): void {
    this.connectionStateCallback = callback;
  }

  // Set callback to run when connection is established (initial or reconnect)
  setOnConnectedCallback(callback: () => Promise<void>): void {
    this.onConnectedCallback = callback;
  }

  private notifyStateChange(state: SignalRConnectionState, attempt = 0): void {
    this.connectionStateCallback?.(state, attempt);
  }

  private async notifyConnected(): Promise<void> {
    if (this.onConnectedCallback) {
      try {
        await this.onConnectedCallback();
      } catch (err) {
        console.error('Error in onConnected callback:', err);
      }
    }
  }

  async connect(): Promise<void> {
    // Cancel any pending disconnect (handles React StrictMode double-render)
    this.disconnectPending = false;
    this.isManualDisconnect = false;

    // Clear any pending reconnect timeout
    if (this.reconnectTimeoutId) {
      clearTimeout(this.reconnectTimeoutId);
      this.reconnectTimeoutId = null;
    }

    // If already connected, return immediately
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      this.notifyStateChange('connected', 0);
      return;
    }

    // If currently connecting, wait for that attempt to complete
    if (this.connectPromise) {
      return this.connectPromise;
    }

    // Notify connecting state
    const isReconnect = this.reconnectAttempt > 0;
    this.notifyStateChange(isReconnect ? 'reconnecting' : 'connecting', this.reconnectAttempt);

    // If there's an existing connection in a bad state, clean it up
    if (this.connection &&
        this.connection.state !== signalR.HubConnectionState.Disconnected &&
        this.connection.state !== signalR.HubConnectionState.Connecting) {
      try {
        await this.connection.stop();
      } catch {
        // Ignore stop errors
      }
      this.connection = null;
    }

    // Only create new connection if we don't have one
    if (!this.connection) {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/log')
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Built-in reconnect: try up to 5 times with exponential backoff
            // After that, we fall back to our own unlimited reconnection loop
            if (retryContext.previousRetryCount >= 5) {
              return null;
            }
            const delay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
            this.reconnectAttempt = retryContext.previousRetryCount + 1;
            this.notifyStateChange('reconnecting', this.reconnectAttempt);
            return delay;
          }
        })
        .configureLogging(signalR.LogLevel.Warning) // Reduce log noise
        .build();

      this.setupEventHandlers();

      this.connection.onreconnecting(() => {
        console.log('SignalR reconnecting...');
        this.notifyStateChange('reconnecting', this.reconnectAttempt);
      });

      this.connection.onreconnected(async () => {
        console.log('SignalR reconnected, starting rehydration...');
        this.reconnectAttempt = 0;
        // Go to rehydrating state - callback will set to connected when done
        this.notifyStateChange('rehydrating', 0);
        // Rehydrate all data - callback is responsible for setting 'connected' when done
        await this.notifyConnected();
      });

      this.connection.onclose((error) => {
        console.log('SignalR connection closed', error ? `Error: ${error.message}` : '');
        this.connectPromise = null;

        // If this wasn't a manual disconnect, start our own reconnection loop
        if (!this.isManualDisconnect && !this.disconnectPending) {
          this.notifyStateChange('disconnected', this.reconnectAttempt);
          this.scheduleReconnect();
        }
      });
    }

    // Store the connection promise so concurrent calls can wait on it
    this.connectPromise = this.connection.start()
      .then(async () => {
        console.log('SignalR connected, starting rehydration...');
        this.reconnectAttempt = 0;
        // Go to rehydrating state - callback will set to connected when done
        this.notifyStateChange('rehydrating', 0);
        // Rehydrate all data - callback is responsible for setting 'connected' when done
        await this.notifyConnected();
      })
      .catch((err) => {
        // Only log non-abort errors (aborts happen during HMR/StrictMode)
        if (!(err instanceof Error && err.name === 'AbortError')) {
          console.error('SignalR connection error:', err);
        }
        this.connectPromise = null;

        // Schedule reconnection on failure (unless manually disconnecting)
        if (!this.isManualDisconnect && !this.disconnectPending) {
          this.notifyStateChange('disconnected', this.reconnectAttempt);
          this.scheduleReconnect();
        }

        throw err;
      });

    return this.connectPromise;
  }

  private scheduleReconnect(): void {
    // Don't schedule if manually disconnected or already scheduling
    if (this.isManualDisconnect || this.disconnectPending || this.reconnectTimeoutId) {
      return;
    }

    this.reconnectAttempt++;
    // Exponential backoff: 1s, 2s, 4s, 8s, 16s, max 30s
    const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempt - 1), 30000);

    console.log(`SignalR scheduling reconnect attempt ${this.reconnectAttempt} in ${delay}ms`);
    this.notifyStateChange('reconnecting', this.reconnectAttempt);

    this.reconnectTimeoutId = setTimeout(async () => {
      this.reconnectTimeoutId = null;

      if (this.isManualDisconnect || this.disconnectPending) {
        return;
      }

      try {
        // Clean up the old connection before reconnecting
        if (this.connection) {
          try {
            await this.connection.stop();
          } catch {
            // Ignore
          }
          this.connection = null;
        }

        await this.connect();
      } catch {
        // connect() will schedule another reconnect on failure
      }
    }, delay);
  }

  private setupEventHandlers(): void {
    if (!this.connection) return;

    this.connection.on('OnCallsignFocused', (evt: CallsignFocusedEvent) => {
      this.handlers.onCallsignFocused?.(evt);
    });

    this.connection.on('OnCallsignLookedUp', (evt: CallsignLookedUpEvent) => {
      console.log('QRZ Lookup received:', evt);
      this.handlers.onCallsignLookedUp?.(evt);
    });

    this.connection.on('OnQsoLogged', (evt: QsoLoggedEvent) => {
      this.handlers.onQsoLogged?.(evt);
    });

    this.connection.on('OnSpotReceived', (evt: SpotReceivedEvent) => {
      this.handlers.onSpotReceived?.(evt);
    });

    this.connection.on('OnSpotSelected', (evt: SpotSelectedEvent) => {
      this.handlers.onSpotSelected?.(evt);
    });

    this.connection.on('OnRotatorPosition', (evt: RotatorPositionEvent) => {
      this.handlers.onRotatorPosition?.(evt);
    });

    this.connection.on('OnRigStatus', (evt: RigStatusEvent) => {
      this.handlers.onRigStatus?.(evt);
    });

    // Antenna Genius events
    this.connection.on('OnAntennaGeniusDiscovered', (evt: AntennaGeniusDiscoveredEvent) => {
      this.handlers.onAntennaGeniusDiscovered?.(evt);
    });

    this.connection.on('OnAntennaGeniusDisconnected', (evt: AntennaGeniusDisconnectedEvent) => {
      this.handlers.onAntennaGeniusDisconnected?.(evt);
    });

    this.connection.on('OnAntennaGeniusStatus', (evt: AntennaGeniusStatusEvent) => {
      this.handlers.onAntennaGeniusStatus?.(evt);
    });

    this.connection.on('OnAntennaGeniusPortChanged', (evt: AntennaGeniusPortChangedEvent) => {
      this.handlers.onAntennaGeniusPortChanged?.(evt);
    });

    // PGXL events
    this.connection.on('OnPgxlDiscovered', (evt: PgxlDiscoveredEvent) => {
      this.handlers.onPgxlDiscovered?.(evt);
    });

    this.connection.on('OnPgxlDisconnected', (evt: PgxlDisconnectedEvent) => {
      this.handlers.onPgxlDisconnected?.(evt);
    });

    this.connection.on('OnPgxlStatus', (evt: PgxlStatusEvent) => {
      this.handlers.onPgxlStatus?.(evt);
    });

    // Radio CAT Control events
    this.connection.on('OnRadioDiscovered', (evt: RadioDiscoveredEvent) => {
      this.handlers.onRadioDiscovered?.(evt);
    });

    this.connection.on('OnRadioRemoved', (evt: RadioRemovedEvent) => {
      this.handlers.onRadioRemoved?.(evt);
    });

    this.connection.on('OnRadioConnectionStateChanged', (evt: RadioConnectionStateChangedEvent) => {
      this.handlers.onRadioConnectionStateChanged?.(evt);
    });

    this.connection.on('OnRadioStateChanged', (evt: RadioStateChangedEvent) => {
      this.handlers.onRadioStateChanged?.(evt);
    });

    this.connection.on('OnRadioSlicesUpdated', (evt: RadioSlicesUpdatedEvent) => {
      this.handlers.onRadioSlicesUpdated?.(evt);
    });

    // CW Keyer events
    this.connection.on('OnCwKeyerStatus', (evt: CwKeyerStatusEvent) => {
      this.handlers.onCwKeyerStatus?.(evt);
    });

    // Hamlib configuration events
    this.connection.on('OnHamlibRigList', (evt: HamlibRigListEvent) => {
      this.handlers.onHamlibRigList?.(evt);
    });

    this.connection.on('OnHamlibRigCaps', (evt: HamlibRigCapsEvent) => {
      this.handlers.onHamlibRigCaps?.(evt);
    });

    this.connection.on('OnHamlibSerialPorts', (evt: HamlibSerialPortsEvent) => {
      this.handlers.onHamlibSerialPorts?.(evt);
    });

    this.connection.on('OnHamlibConfigLoaded', (evt: HamlibConfigLoadedEvent) => {
      this.handlers.onHamlibConfigLoaded?.(evt);
    });

    this.connection.on('OnHamlibStatus', (evt: HamlibStatusEvent) => {
      this.handlers.onHamlibStatus?.(evt);
    });

    // SmartUnlink events
    this.connection.on('OnSmartUnlinkRadioAdded', (evt: SmartUnlinkRadioAddedEvent) => {
      this.handlers.onSmartUnlinkRadioAdded?.(evt);
    });

    this.connection.on('OnSmartUnlinkRadioUpdated', (evt: SmartUnlinkRadioUpdatedEvent) => {
      this.handlers.onSmartUnlinkRadioUpdated?.(evt);
    });

    this.connection.on('OnSmartUnlinkRadioRemoved', (evt: SmartUnlinkRadioRemovedEvent) => {
      this.handlers.onSmartUnlinkRadioRemoved?.(evt);
    });

    this.connection.on('OnSmartUnlinkStatus', (evt: SmartUnlinkStatusEvent) => {
      this.handlers.onSmartUnlinkStatus?.(evt);
    });

    // QRZ Sync events
    this.connection.on('OnQrzSyncProgress', (evt: QrzSyncProgressEvent) => {
      this.handlers.onQrzSyncProgress?.(evt);
    });

    // DX Cluster events
    this.connection.on('OnClusterStatusChanged', (evt: ClusterStatusChangedEvent) => {
      this.handlers.onClusterStatusChanged?.(evt);
    });
  }

  setHandlers(handlers: EventHandlers): void {
    this.handlers = { ...this.handlers, ...handlers };
  }

  // Client-to-server methods
  async focusCallsign(evt: CallsignFocusedEvent): Promise<void> {
    console.log('Sending FocusCallsign:', evt);
    await this.connection?.invoke('FocusCallsign', evt);
  }

  async selectSpot(evt: SpotSelectedEvent): Promise<void> {
    await this.connection?.invoke('SelectSpot', evt);
  }

  async persistCallsignMapImage(image: { callsign: string; imageUrl?: string; latitude: number; longitude: number; name?: string; country?: string; grid?: string }): Promise<void> {
    await this.connection?.invoke('PersistCallsignMapImage', image);
  }

  async commandRotator(evt: RotatorCommandEvent): Promise<void> {
    await this.connection?.invoke('CommandRotator', evt);
  }

  // Antenna Genius methods
  async selectAntenna(deviceSerial: string, portId: number, antennaId: number): Promise<void> {
    const cmd: SelectAntennaCommand = { deviceSerial, portId, antennaId };
    await this.connection?.invoke('SelectAntenna', cmd);
  }

  async requestAntennaGeniusStatus(): Promise<void> {
    await this.connection?.invoke('RequestAntennaGeniusStatus');
  }

  // PGXL methods
  async setPgxlOperate(serial: string): Promise<void> {
    const cmd: SetPgxlOperateCommand = { serial };
    await this.connection?.invoke('SetPgxlOperate', cmd);
  }

  async setPgxlStandby(serial: string): Promise<void> {
    const cmd: SetPgxlStandbyCommand = { serial };
    await this.connection?.invoke('SetPgxlStandby', cmd);
  }

  async requestPgxlStatus(): Promise<void> {
    await this.connection?.invoke('RequestPgxlStatus');
  }

  async disablePgxlFlexRadioPairing(serial: string, slice: string): Promise<void> {
    const cmd: DisablePgxlFlexRadioPairingCommand = { serial, slice };
    await this.connection?.invoke('DisablePgxlFlexRadioPairing', cmd);
  }

  // Radio CAT Control methods
  async startRadioDiscovery(type: RadioType): Promise<void> {
    const cmd: StartRadioDiscoveryCommand = { type };
    await this.connection?.invoke('StartRadioDiscovery', cmd);
  }

  async stopRadioDiscovery(type: RadioType): Promise<void> {
    const cmd: StopRadioDiscoveryCommand = { type };
    await this.connection?.invoke('StopRadioDiscovery', cmd);
  }

  async connectRadio(radioId: string): Promise<void> {
    const cmd: ConnectRadioCommand = { radioId };
    await this.connection?.invoke('ConnectRadio', cmd);
  }

  async disconnectRadio(radioId: string): Promise<void> {
    const cmd: DisconnectRadioCommand = { radioId };
    await this.connection?.invoke('DisconnectRadio', cmd);
  }

  async selectRadioSlice(radioId: string, sliceId: string): Promise<void> {
    const cmd: SelectRadioSliceCommand = { radioId, sliceId };
    await this.connection?.invoke('SelectRadioSlice', cmd);
  }

  async selectRadioInstance(radioId: string, instance: number): Promise<void> {
    const cmd: SelectRadioInstanceCommand = { radioId, instance };
    await this.connection?.invoke('SelectRadioInstance', cmd);
  }

  async requestRadioStatus(): Promise<void> {
    await this.connection?.invoke('RequestRadioStatus');
  }

  // CW Keyer methods
  async sendCwKey(radioId: string, message: string, speedWpm?: number): Promise<void> {
    const cmd: SendCwKeyCommand = { radioId, message, speedWpm };
    await this.connection?.invoke('SendCwKey', cmd);
  }

  async stopCwKey(radioId: string): Promise<void> {
    const cmd: StopCwKeyCommand = { radioId };
    await this.connection?.invoke('StopCwKey', cmd);
  }

  async setCwSpeed(radioId: string, speedWpm: number): Promise<void> {
    const cmd: SetCwSpeedCommand = { radioId, speedWpm };
    await this.connection?.invoke('SetCwSpeed', cmd);
  }

  async requestCwKeyerStatus(radioId: string): Promise<void> {
    await this.connection?.invoke('RequestCwKeyerStatus', radioId);
  }

  // Hamlib configuration methods
  async getHamlibRigList(): Promise<void> {
    await this.connection?.invoke('GetHamlibRigList');
  }

  async getHamlibRigCaps(modelId: number): Promise<void> {
    await this.connection?.invoke('GetHamlibRigCaps', modelId);
  }

  async getHamlibSerialPorts(): Promise<void> {
    await this.connection?.invoke('GetHamlibSerialPorts');
  }

  async getHamlibConfig(): Promise<void> {
    await this.connection?.invoke('GetHamlibConfig');
  }

  async getHamlibStatus(): Promise<void> {
    await this.connection?.invoke('GetHamlibStatus');
  }

  async connectHamlibRig(config: HamlibRigConfigDto): Promise<void> {
    await this.connection?.invoke('ConnectHamlibRig', config);
  }

  async saveHamlibConfig(config: HamlibRigConfigDto): Promise<void> {
    await this.connection?.invoke('SaveHamlibConfig', config);
  }

  async disconnectHamlibRig(): Promise<void> {
    await this.connection?.invoke('DisconnectHamlibRig');
  }

  async deleteHamlibConfig(): Promise<void> {
    await this.connection?.invoke('DeleteHamlibConfig');
  }

  async deleteTciConfig(): Promise<void> {
    await this.connection?.invoke('DeleteTciConfig');
  }

  // TCI direct connection methods
  async connectTci(host: string, port: number = 50001, name?: string): Promise<void> {
    await this.connection?.invoke('ConnectTci', host, port, name);
  }

  async disconnectTci(radioId: string): Promise<void> {
    await this.connection?.invoke('DisconnectTci', radioId);
  }

  // SmartUnlink methods
  async addSmartUnlinkRadio(dto: SmartUnlinkRadioDto): Promise<void> {
    await this.connection?.invoke('AddSmartUnlinkRadio', dto);
  }

  async updateSmartUnlinkRadio(dto: SmartUnlinkRadioDto): Promise<void> {
    await this.connection?.invoke('UpdateSmartUnlinkRadio', dto);
  }

  async removeSmartUnlinkRadio(id: string): Promise<void> {
    await this.connection?.invoke('RemoveSmartUnlinkRadio', id);
  }

  async setSmartUnlinkRadioEnabled(id: string, enabled: boolean): Promise<void> {
    await this.connection?.invoke('SetSmartUnlinkRadioEnabled', id, enabled);
  }

  async requestSmartUnlinkStatus(): Promise<void> {
    await this.connection?.invoke('RequestSmartUnlinkStatus');
  }

  // Rotator methods
  async requestRotatorStatus(): Promise<void> {
    await this.connection?.invoke('RequestRotatorStatus');
  }

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  async disconnect(): Promise<void> {
    // Set pending flag - if connect() is called before timeout, it will cancel
    this.disconnectPending = true;
    this.isManualDisconnect = true;

    // Clear any pending reconnect timeout
    if (this.reconnectTimeoutId) {
      clearTimeout(this.reconnectTimeoutId);
      this.reconnectTimeoutId = null;
    }

    // Small delay to allow React StrictMode's immediate re-mount
    await new Promise(resolve => setTimeout(resolve, 100));

    // If connect() was called during the delay, don't disconnect
    if (!this.disconnectPending) {
      this.isManualDisconnect = false;
      return;
    }

    this.disconnectPending = false;
    this.connectPromise = null;
    this.reconnectAttempt = 0;

    if (this.connection) {
      try {
        await this.connection.stop();
      } catch {
        // Ignore errors when stopping (might already be disconnected)
      }
      this.connection = null;
    }

    this.notifyStateChange('disconnected', 0);
  }

  // Manual reconnect - reset attempt counter and try immediately
  async reconnect(): Promise<void> {
    this.isManualDisconnect = false;
    this.reconnectAttempt = 0;

    // Clear any pending reconnect timeout
    if (this.reconnectTimeoutId) {
      clearTimeout(this.reconnectTimeoutId);
      this.reconnectTimeoutId = null;
    }

    // Clean up existing connection
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch {
        // Ignore
      }
      this.connection = null;
    }
    this.connectPromise = null;

    // Start fresh connection
    await this.connect();
  }
}

export const signalRService = new SignalRService();
