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
  band: string;
  meters: PgxlMeters;
  setup: PgxlSetup;
}

export interface SetPgxlOperateCommand {
  serial: string;
}

export interface SetPgxlStandbyCommand {
  serial: string;
}

// Radio CAT Control types
export type RadioType = 'FlexRadio' | 'Tci';

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
};

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private handlers: EventHandlers = {};
  private maxReconnectAttempts = 10;

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/log')
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
            return null;
          }
          return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.setupEventHandlers();

    this.connection.onreconnecting(() => {
      console.log('SignalR reconnecting...');
    });

    this.connection.onreconnected(() => {
      console.log('SignalR reconnected');
    });

    this.connection.onclose(() => {
      console.log('SignalR connection closed');
    });

    try {
      await this.connection.start();
      console.log('SignalR connected');
    } catch (err) {
      console.error('SignalR connection error:', err);
      throw err;
    }
  }

  private setupEventHandlers(): void {
    if (!this.connection) return;

    this.connection.on('OnCallsignFocused', (evt: CallsignFocusedEvent) => {
      this.handlers.onCallsignFocused?.(evt);
    });

    this.connection.on('OnCallsignLookedUp', (evt: CallsignLookedUpEvent) => {
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
  }

  setHandlers(handlers: EventHandlers): void {
    this.handlers = { ...this.handlers, ...handlers };
  }

  // Client-to-server methods
  async focusCallsign(evt: CallsignFocusedEvent): Promise<void> {
    await this.connection?.invoke('FocusCallsign', evt);
  }

  async selectSpot(evt: SpotSelectedEvent): Promise<void> {
    await this.connection?.invoke('SelectSpot', evt);
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

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
  }
}

export const signalRService = new SignalRService();
