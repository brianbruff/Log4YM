import { useState } from 'react';
import { Wifi, WifiOff, Settings, Zap, Radio } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import type { PgxlStatusEvent } from '../api/signalr';

// Types for A/B slice configuration
interface SliceConfig {
  pttActive: boolean;
  band: string;
  mode: string;  // Bias mode from PGXL (e.g., "AB", "AAB", "A", "B")
  radioName: string;
}

export function PgxlPlugin() {
  const {
    pgxlDevices,
    pgxlTciLinkA,
    pgxlTciLinkB,
    radioStates,
    discoveredRadios,
  } = useAppStore();
  const { setPgxlOperate, setPgxlStandby, disablePgxlFlexRadioPairing } = useSignalR();
  const [showSettings, setShowSettings] = useState(false);

  // Convert Map to array for rendering
  const devices = Array.from(pgxlDevices.values());

  // Get linked TCI radio states
  const linkedRadioStateA = pgxlTciLinkA ? radioStates.get(pgxlTciLinkA) : null;
  const linkedRadioStateB = pgxlTciLinkB ? radioStates.get(pgxlTciLinkB) : null;
  const linkedRadioInfoA = pgxlTciLinkA ? discoveredRadios.get(pgxlTciLinkA) : null;
  const linkedRadioInfoB = pgxlTciLinkB ? discoveredRadios.get(pgxlTciLinkB) : null;

  // Safety feature: DISABLED for now to debug TCI disconnect issue
  // TODO: Re-enable once TCI connection is stable during TX
  // Track if linked radios have state data (more reliable than connection state)
  // const hadRadioStateA = useRef<boolean>(false);
  // const hadRadioStateB = useRef<boolean>(false);
  // useEffect(() => { ... }, []);

  // Check if linked radios are disconnected (for warning display)
  const linkedRadioDisconnectedA = pgxlTciLinkA && !linkedRadioStateA;
  const linkedRadioDisconnectedB = pgxlTciLinkB && !linkedRadioStateB;

  if (devices.length === 0) {
    return (
      <GlassPanel
        title="PowerGeniusXL"
        icon={<Zap className="w-5 h-5" />}
      >
        <div className="flex flex-col items-center justify-center h-full p-8 text-dark-300">
          <WifiOff className="w-12 h-12 mb-4 opacity-50" />
          <p className="text-center font-ui">No PGXL amplifiers found</p>
          <p className="text-sm text-dark-300 mt-2 text-center font-ui">
            Waiting for PGXL discovery on port 9008
          </p>
        </div>
      </GlassPanel>
    );
  }

  // For now, show the first device
  const device = devices[0];

  // Debug: log linked radio state
  if (linkedRadioStateA) {
    console.log('PGXL linked radio state A:', linkedRadioStateA);
  }

  // A/B slice config - use linked TCI radio info if available
  const sliceA: SliceConfig = linkedRadioStateA
    ? {
        pttActive: linkedRadioStateA.isTransmitting,
        band: linkedRadioStateA.band?.replace('m', '') || 'N/A',
        mode: device.biasA || 'N/A',
        radioName: linkedRadioInfoA?.model || 'TCI Radio',
      }
    : {
        pttActive: device.isTransmitting,
        band: device.band?.replace('m', '') || 'N/A',
        mode: device.biasA || 'N/A',
        radioName: 'Slice A',
      };

  const sliceB: SliceConfig = linkedRadioStateB
    ? {
        pttActive: linkedRadioStateB.isTransmitting,
        band: linkedRadioStateB.band?.replace('m', '') || 'N/A',
        mode: device.biasB || 'N/A',
        radioName: linkedRadioInfoB?.model || 'TCI Radio',
      }
    : {
        pttActive: false,
        band: 'N/A',
        mode: device.biasB || 'N/A',
        radioName: 'Slice B',
      };

  // Simulated voltage readings (will come from backend in future)
  const vdd = device.isOperating ? 52.0 : 0.0;
  const vac = 238;
  const tempA = device.meters.temperatureC;
  const tempB = device.meters.temperatureC + 1.2; // Simulated second temp

  return (
    <GlassPanel
      title="PowerGeniusXL"
      icon={<Zap className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          {device.isConnected ? (
            <span className="flex items-center gap-1.5 text-xs text-accent-success font-ui">
              <Wifi className="w-3.5 h-3.5" />
              Connected
            </span>
          ) : (
            <span className="flex items-center gap-1.5 text-xs text-dark-300 font-ui">
              <WifiOff className="w-3.5 h-3.5" />
              Disconnected
            </span>
          )}
        </div>
      }
    >
      <div className="flex flex-col h-full">
        {/* Main Display Area */}
        <div className="flex-1 bg-dark-800 rounded-lg mx-3 mt-3 overflow-hidden">
          {device.isOperating ? (
            <OperatingDisplay
              forwardPower={device.meters.forwardPowerWatts}
              swr={device.meters.swrRatio}
              paCurrent={device.meters.paCurrent}
            />
          ) : (
            <StandbyDisplay />
          )}
        </div>

        {/* A/B Slice Status and Readings */}
        <div className="flex gap-3 px-3 py-3">
          {/* A/B Slice Rows */}
          <div className="flex-1 space-y-1.5">
            <SliceStatusRow label="A" config={sliceA} />
            <SliceStatusRow label="B" config={sliceB} />
          </div>

          {/* Voltage/Temp Readings */}
          <div className="text-right text-sm space-y-0.5 min-w-[100px]">
            <div className="text-dark-300">
              <span className="text-dark-200 font-display">{tempA.toFixed(1)}</span>
              <span className="text-dark-300 font-ui"> / </span>
              <span className="text-dark-200 font-display">{tempB.toFixed(1)}</span>
              <span className="text-dark-300 font-ui"> °C</span>
            </div>
            <div className="text-dark-300">
              <span className="text-dark-300 font-ui">Vdd </span>
              <span className="text-dark-200 font-display">{vdd.toFixed(1)}</span>
              <span className="text-dark-300 font-ui"> V</span>
            </div>
            <div className="text-dark-300">
              <span className="text-dark-300 font-ui">Vac </span>
              <span className="text-dark-200 font-display">{vac}</span>
              <span className="text-dark-300 font-ui"> V</span>
            </div>
          </div>
        </div>

        {/* Bottom Buttons */}
        <div className="flex gap-2 px-3 pb-3">
          <button
            onClick={() => setShowSettings(true)}
            className="px-4 py-2 text-sm font-medium font-ui bg-dark-700 text-dark-200 rounded-lg hover:bg-dark-600 transition-all border border-glass-100 flex items-center gap-2"
          >
            <Settings className="w-4 h-4" />
            Settings
          </button>
          <div className="flex-1" />
          <button
            onClick={() => {
              if (device.isOperating) {
                setPgxlStandby(device.serial);
              } else {
                setPgxlOperate(device.serial);
              }
            }}
            className={`px-6 py-2 text-sm font-bold font-ui rounded-lg transition-all ${
              device.isOperating
                ? 'bg-accent-success/80 text-dark-900 hover:bg-accent-success'
                : 'bg-accent-secondary/80 text-dark-900 hover:bg-accent-secondary'
            }`}
          >
            {device.isOperating ? 'Standby' : 'Operate'}
          </button>
        </div>

        {/* Status Alerts */}
        {(device.setup.highSwr || device.setup.overTemp || device.setup.overCurrent ||
          linkedRadioDisconnectedA || linkedRadioDisconnectedB) && (
          <div className="flex gap-2 px-3 pb-3 flex-wrap">
            {device.setup.highSwr && (
              <StatusBadge label="HIGH SWR" variant="danger" />
            )}
            {device.setup.overTemp && (
              <StatusBadge label="OVER TEMP" variant="danger" />
            )}
            {device.setup.overCurrent && (
              <StatusBadge label="OVER CURRENT" variant="danger" />
            )}
            {linkedRadioDisconnectedA && (
              <StatusBadge label="SIDE A RADIO LOST" variant="warning" />
            )}
            {linkedRadioDisconnectedB && (
              <StatusBadge label="SIDE B RADIO LOST" variant="warning" />
            )}
          </div>
        )}
      </div>

      {/* Settings Modal */}
      {showSettings && (
        <SettingsModal
          device={device}
          onClose={() => setShowSettings(false)}
          linkedRadioIdA={pgxlTciLinkA}
          linkedRadioIdB={pgxlTciLinkB}
          onDisableFlexPairing={disablePgxlFlexRadioPairing}
        />
      )}
    </GlassPanel>
  );
}

function StandbyDisplay() {
  return (
    <div className="flex items-center justify-center h-full min-h-[120px]">
      <span className="text-4xl font-bold font-display text-accent-primary italic tracking-wider">
        STANDBY
      </span>
    </div>
  );
}

interface OperatingDisplayProps {
  forwardPower: number;
  swr: number;
  paCurrent: number;
}

function OperatingDisplay({ forwardPower, swr, paCurrent }: OperatingDisplayProps) {
  return (
    <div className="p-3 space-y-2">
      {/* Forward Power Meter */}
      <SegmentedMeter
        label="Fwd Pwr"
        value={forwardPower}
        segments={[
          { start: 0, end: 500, color: 'bg-accent-success' },
          { start: 500, end: 1500, color: 'bg-accent-primary' },
          { start: 1500, end: 2000, color: 'bg-accent-danger' },
        ]}
        markers={[
          { value: 0, label: '0' },
          { value: 500, label: '500' },
          { value: 1500, label: '1.5k' },
          { value: 2000, label: '2k' },
        ]}
        max={2000}
      />

      {/* SWR Meter */}
      <SegmentedMeter
        label="SWR"
        value={swr}
        segments={[
          { start: 1, end: 1.5, color: 'bg-accent-success' },
          { start: 1.5, end: 2.5, color: 'bg-accent-primary' },
          { start: 2.5, end: 3, color: 'bg-accent-danger' },
        ]}
        markers={[
          { value: 1, label: '1' },
          { value: 1.5, label: '1.5' },
          { value: 2.5, label: '2.5' },
          { value: 3, label: '3' },
        ]}
        min={1}
        max={3}
      />

      {/* PA Current Meter */}
      <SegmentedMeter
        label="Id"
        value={paCurrent}
        segments={[
          { start: 0, end: 40, color: 'bg-accent-success' },
          { start: 40, end: 60, color: 'bg-accent-primary' },
          { start: 60, end: 70, color: 'bg-accent-danger' },
        ]}
        markers={[
          { value: 10, label: '10' },
          { value: 20, label: '20' },
          { value: 30, label: '30' },
          { value: 40, label: '40' },
          { value: 50, label: '50' },
          { value: 60, label: '60' },
          { value: 70, label: '70' },
        ]}
        max={70}
        valueDisplay={`${paCurrent.toFixed(1)} A`}
      />
    </div>
  );
}

interface Segment {
  start: number;
  end: number;
  color: string;
}

interface Marker {
  value: number;
  label: string;
}

interface SegmentedMeterProps {
  label: string;
  value: number;
  segments: Segment[];
  markers: Marker[];
  min?: number;
  max: number;
  valueDisplay?: string;
}

function SegmentedMeter({
  label,
  value,
  segments,
  markers,
  min = 0,
  max,
  valueDisplay,
}: SegmentedMeterProps) {
  const range = max - min;

  return (
    <div className="relative">
      {/* Label and Markers Row */}
      <div className="flex items-center justify-between mb-1">
        <span className="text-xs text-dark-300 font-ui w-16">{label}</span>
        <div className="flex-1 relative h-4">
          {markers.map((marker) => (
            <span
              key={marker.value}
              className="absolute text-[10px] text-dark-300 font-mono transform -translate-x-1/2"
              style={{ left: `${((marker.value - min) / range) * 100}%` }}
            >
              {marker.label}
            </span>
          ))}
        </div>
        {valueDisplay && (
          <span className="text-xs text-accent-secondary font-display min-w-[50px] text-right">
            {valueDisplay}
          </span>
        )}
      </div>

      {/* Segmented Bar */}
      <div className="flex h-3 bg-dark-700 rounded overflow-hidden">
        {segments.map((segment, idx) => {
          const segmentStart = ((segment.start - min) / range) * 100;
          const segmentEnd = ((segment.end - min) / range) * 100;
          const segmentWidth = segmentEnd - segmentStart;

          // Calculate how much of this segment is filled
          const valuePercent = ((Math.max(min, Math.min(value, max)) - min) / range) * 100;
          const fillStart = Math.max(0, valuePercent - segmentStart);
          const fillWidth = Math.min(fillStart, segmentWidth);
          const fillPercent = segmentWidth > 0 ? (fillWidth / segmentWidth) * 100 : 0;

          return (
            <div
              key={idx}
              className="relative h-full"
              style={{ width: `${segmentWidth}%` }}
            >
              {/* Background segment outline */}
              <div className={`absolute inset-0 ${segment.color} opacity-20`} />
              {/* Filled portion */}
              {fillPercent > 0 && (
                <div
                  className={`absolute inset-y-0 left-0 ${segment.color}`}
                  style={{ width: `${fillPercent}%` }}
                />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

interface SliceStatusRowProps {
  label: string;
  config: SliceConfig;
}

function SliceStatusRow({ label, config }: SliceStatusRowProps) {
  const getModeColor = (mode: string) => {
    // Handle bias modes from PGXL (AB, AAB, A, B, or N/A)
    const modeUpper = mode.toUpperCase();
    if (modeUpper === 'AAB') {
      return 'bg-accent-success/80 text-dark-900';
    } else if (modeUpper === 'AB') {
      return 'bg-accent-secondary/80 text-dark-900';
    } else if (modeUpper === 'A') {
      return 'bg-accent-success/50 text-dark-200';
    } else if (modeUpper === 'B') {
      return 'bg-accent-secondary/50 text-dark-200';
    } else {
      return 'bg-dark-600 text-dark-200';
    }
  };

  return (
    <div className="flex items-center gap-2 text-sm">
      {/* Slice Label */}
      <span className="text-dark-300 font-ui font-medium w-4">{label}</span>

      {/* PTT Indicator */}
      <span
        className={`px-2 py-0.5 rounded text-xs font-medium font-ui ${
          config.pttActive
            ? 'bg-accent-danger text-dark-900'
            : 'bg-dark-700 text-dark-300'
        }`}
      >
        PTT
      </span>

      {/* Band */}
      <span
        className={`px-2 py-0.5 rounded text-xs font-mono min-w-[32px] text-center ${
          config.band !== 'N/A'
            ? 'bg-accent-secondary/80 text-dark-900'
            : 'bg-dark-700 text-dark-300'
        }`}
      >
        {config.band}
      </span>

      {/* Mode */}
      <span
        className={`px-2 py-0.5 rounded text-xs font-medium font-ui min-w-[36px] text-center ${getModeColor(
          config.mode
        )}`}
      >
        {config.mode}
      </span>

      {/* Radio Name */}
      <span className="text-dark-200 font-ui flex-1 truncate">{config.radioName}</span>
    </div>
  );
}

interface StatusBadgeProps {
  label: string;
  variant: 'danger' | 'warning' | 'success';
}

function StatusBadge({ label, variant }: StatusBadgeProps) {
  const colors = {
    danger: 'bg-accent-danger/20 text-accent-danger border-accent-danger/30',
    warning: 'bg-accent-primary/20 text-accent-primary border-accent-primary/30',
    success: 'bg-accent-success/20 text-accent-success border-accent-success/30',
  };

  return (
    <span
      className={`px-2 py-0.5 text-xs font-medium font-ui rounded border ${colors[variant]} animate-pulse`}
    >
      {label}
    </span>
  );
}

interface SettingsModalProps {
  device: PgxlStatusEvent;
  onClose: () => void;
  linkedRadioIdA: string | null;
  linkedRadioIdB: string | null;
  onDisableFlexPairing: (serial: string, slice: string) => Promise<void>;
}

function SettingsModal({ device, onClose, linkedRadioIdA, linkedRadioIdB, onDisableFlexPairing }: SettingsModalProps) {
  const { discoveredRadios, radioStates, setPgxlTciLink } = useAppStore();

  // Get connected radios (TCI or Hamlib) that can be linked
  // Use radioStates as the primary source since it's populated when we receive frequency data
  const connectedRadios = Array.from(radioStates.keys()).map((radioId) => {
    const discoveredInfo = discoveredRadios.get(radioId);
    const radioState = radioStates.get(radioId);
    return {
      id: radioId,
      model: discoveredInfo?.model || radioId,
      ipAddress: discoveredInfo?.ipAddress || '',
      band: radioState?.band,
    };
  });

  const handleLinkChange = (side: 'A' | 'B', radioId: string) => {
    setPgxlTciLink(side, radioId === '' ? null : radioId);
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-dark-800 rounded-xl border border-glass-100 shadow-2xl w-full max-w-md mx-4">
        <div className="flex items-center justify-between px-4 py-3 border-b border-glass-100">
          <h3 className="text-lg font-semibold font-ui text-dark-200">PGXL Settings</h3>
          <button
            onClick={onClose}
            className="text-dark-300 hover:text-dark-200 transition-colors"
          >
            ✕
          </button>
        </div>

        <div className="p-4 space-y-4">
          {/* Device Info */}
          <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
            <div className="text-xs text-dark-300 font-ui uppercase tracking-wider mb-2">
              Device Info
            </div>
            <div className="space-y-1 text-sm">
              <div className="flex justify-between">
                <span className="text-dark-300 font-ui">Serial:</span>
                <span className="text-dark-200 font-mono">{device.serial}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-dark-300 font-ui">IP Address:</span>
                <span className="text-dark-200 font-mono">{device.ipAddress}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-dark-300 font-ui">Band Source:</span>
                <span className="text-dark-200 font-ui">{device.setup.bandSource}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-dark-300 font-ui">Antenna:</span>
                <span className="text-dark-200 font-ui">ANT{device.setup.selectedAntenna}</span>
              </div>
            </div>
          </div>

          {/* FlexRadio Pairing */}
          <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
            <div className="text-xs text-dark-300 font-ui uppercase tracking-wider mb-2">
              FlexRadio Pairing
            </div>
            <p className="text-xs text-dark-300 font-ui mb-3">
              If the PGXL is paired with a FlexRadio, it will only accept PTT/band data from that radio.
              Disable pairing to use with TCI or other radios.
            </p>
            <div className="flex gap-2">
              <button
                onClick={() => onDisableFlexPairing(device.serial, 'A')}
                className="flex-1 px-3 py-1.5 text-sm font-medium font-ui bg-accent-primary/20 text-accent-primary rounded-lg hover:bg-accent-primary/30 transition-all border border-accent-primary/30"
              >
                Unpair Side A
              </button>
              <button
                onClick={() => onDisableFlexPairing(device.serial, 'B')}
                className="flex-1 px-3 py-1.5 text-sm font-medium font-ui bg-accent-primary/20 text-accent-primary rounded-lg hover:bg-accent-primary/30 transition-all border border-accent-primary/30"
              >
                Unpair Side B
              </button>
            </div>
          </div>

          {/* Radio Linking */}
          <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
            <div className="text-xs text-dark-300 font-ui uppercase tracking-wider mb-2 flex items-center gap-2">
              <Radio className="w-3.5 h-3.5" />
              Radio Linking
            </div>
            <p className="text-xs text-dark-300 font-ui mb-3">
              Link TCI/Hamlib radios to track band, mode, and PTT state
            </p>
            <div className="space-y-3">
              {/* Side A */}
              <div className="flex items-center gap-3">
                <span className="text-dark-300 font-ui text-sm w-12">Side A:</span>
                <select
                  value={linkedRadioIdA || ''}
                  onChange={(e) => handleLinkChange('A', e.target.value)}
                  className="flex-1 bg-dark-600 border border-glass-100 rounded-lg px-3 py-1.5 text-sm text-dark-200 font-ui focus:outline-none focus:ring-2 focus:ring-accent-primary"
                >
                  <option value="">Not linked</option>
                  {connectedRadios.map((radio) => (
                    <option key={radio.id} value={radio.id}>
                      {radio.model} ({radio.ipAddress})
                    </option>
                  ))}
                </select>
              </div>

              {/* Side B */}
              <div className="flex items-center gap-3">
                <span className="text-dark-300 font-ui text-sm w-12">Side B:</span>
                <select
                  value={linkedRadioIdB || ''}
                  onChange={(e) => handleLinkChange('B', e.target.value)}
                  className="flex-1 bg-dark-600 border border-glass-100 rounded-lg px-3 py-1.5 text-sm text-dark-200 font-ui focus:outline-none focus:ring-2 focus:ring-accent-primary"
                >
                  <option value="">Not linked</option>
                  {connectedRadios.map((radio) => (
                    <option key={radio.id} value={radio.id}>
                      {radio.model} ({radio.ipAddress})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {connectedRadios.length === 0 && (
              <p className="text-xs text-accent-primary font-ui mt-2">
                No radios connected. Connect a TCI or rigctld radio first.
              </p>
            )}
          </div>
        </div>

        <div className="flex justify-end gap-2 px-4 py-3 border-t border-glass-100">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium font-ui bg-dark-700 text-dark-200 rounded-lg hover:bg-dark-600 transition-all border border-glass-100"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
