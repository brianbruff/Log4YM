import { Radio, Wifi, WifiOff, Zap, Activity, AlertTriangle, CheckCircle, RefreshCw, Power } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';

export function TunerGeniusPlugin() {
  const { tunerGeniusDevices } = useAppStore();
  const { tuneTunerGenius, bypassTunerGenius, operateTunerGenius, activateChannelTunerGenius } = useSignalR();

  const devices = Array.from(tunerGeniusDevices.values());

  if (devices.length === 0) {
    return (
      <GlassPanel
        title="Tuner Genius"
        icon={<Radio className="w-5 h-5" />}
      >
        <div className="flex flex-col items-center justify-center h-full p-8 text-dark-300">
          <WifiOff className="w-12 h-12 mb-4 opacity-50" />
          <p className="text-center font-ui">No Tuner Genius devices found</p>
          <p className="text-sm text-dark-300 mt-2 text-center font-ui">
            Waiting for device discovery on UDP port 9010...
          </p>
        </div>
      </GlassPanel>
    );
  }

  const device = devices[0];
  const isSO2R = !!device.portB;

  return (
    <GlassPanel
      title="Tuner Genius"
      icon={<Radio className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          <span className="text-xs text-dark-300 font-mono">{device.model}</span>
          {device.isConnected ? (
            <span className="flex items-center gap-1.5 text-xs text-accent-success">
              <Wifi className="w-3.5 h-3.5" />
              Connected
            </span>
          ) : (
            <span className="flex items-center gap-1.5 text-xs text-dark-300">
              <WifiOff className="w-3.5 h-3.5" />
              Disconnected
            </span>
          )}
        </div>
      }
    >
      <div className="p-4 space-y-4">
        {/* Device Info */}
        <div className="text-sm text-dark-300 font-mono">
          <span className="font-medium text-dark-200">{device.deviceName}</span>
          <span className="mx-2">|</span>
          <span>v{device.version}</span>
          <span className="mx-2">|</span>
          <span className="font-mono text-xs">{device.ipAddress}</span>
        </div>

        {/* Tuner State & Controls */}
        <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100 space-y-3">
          {/* Operate/Standby + Bypass row */}
          <div className="flex items-center gap-2">
            <button
              onClick={() => operateTunerGenius(device.deviceSerial, !device.isOperating)}
              className={`
                flex-1 px-3 py-2 text-sm font-medium font-ui flex items-center justify-center gap-2
                rounded-lg transition-all
                ${device.isOperating
                  ? 'bg-accent-success/20 text-accent-success border border-accent-success/30'
                  : 'bg-dark-600 text-dark-300 hover:bg-dark-500'
                }
              `}
            >
              <Power className="w-4 h-4" />
              {device.isOperating ? 'Operate' : 'Standby'}
            </button>

            <button
              onClick={() => bypassTunerGenius(device.deviceSerial, 1, !device.isBypassed)}
              className={`
                flex-1 px-3 py-2 text-sm font-medium font-ui flex items-center justify-center gap-2
                rounded-lg transition-all
                ${device.isBypassed
                  ? 'bg-accent-warning/20 text-accent-warning border border-accent-warning/30'
                  : 'bg-dark-600 text-dark-300 hover:bg-dark-500'
                }
              `}
              title={device.isBypassed ? 'Tuner is bypassed — click to enable' : 'Click to bypass tuner'}
            >
              {device.isBypassed ? 'Bypassed' : 'Tuner In'}
            </button>
          </div>

          {/* Auto-Tune button */}
          <button
            onClick={() => tuneTunerGenius(device.deviceSerial, 1)}
            disabled={device.isTuning || !device.isOperating}
            className={`
              w-full px-4 py-2.5 text-sm font-medium font-ui flex items-center justify-center gap-2
              rounded-lg transition-all disabled:opacity-50 disabled:cursor-not-allowed
              bg-accent-primary/20 text-accent-primary hover:bg-accent-primary/30
              border border-accent-primary/20
            `}
          >
            {device.isTuning ? (
              <>
                <RefreshCw className="w-4 h-4 animate-spin" />
                Tuning...
              </>
            ) : (
              <>
                <Activity className="w-4 h-4" />
                Auto Tune
              </>
            )}
          </button>
        </div>

        {/* Meters: Power + SWR */}
        <div className="grid grid-cols-2 gap-3">
          <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
            <div className="text-xs text-dark-300 uppercase tracking-wider mb-1 font-ui">Fwd Power</div>
            <div className="text-xl font-bold font-mono text-dark-100">
              {device.forwardPowerWatts > 0 ? `${device.forwardPowerWatts.toFixed(0)} W` : '--'}
            </div>
          </div>
          <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
            <div className="text-xs text-dark-300 uppercase tracking-wider mb-1 font-ui">SWR</div>
            <div className={`text-xl font-bold font-mono ${getSWRColor(device.swr)}`}>
              {device.forwardPowerWatts >= 1 && device.swr > 0 && device.swr < 99 ? `${device.swr.toFixed(1)} :1` : '--'}
            </div>
          </div>
        </div>

        {/* Matching Network: L / C1 / C2 */}
        <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100 space-y-2">
          <div className="text-xs text-dark-300 uppercase tracking-wider font-ui mb-2">Matching Network</div>
          <LCGauge label="C1" value={device.c1} />
          <LCGauge label="L" value={device.l} />
          <LCGauge label="C2" value={device.c2} />
        </div>

        {/* Radio Inputs */}
        <div className="space-y-2">
          <RadioInput
            label="Radio A"
            color="accent-primary"
            frequencyMhz={device.freqAMhz}
            band={device.portA?.band ?? ''}
            isActive={device.activeRadio === 1}
            isTransmitting={device.portA?.isTransmitting ?? false}
            isSO2R={isSO2R}
            onActivate={() => activateChannelTunerGenius(device.deviceSerial, 1)}
          />
          {isSO2R && (
            <RadioInput
              label="Radio B"
              color="accent-success"
              frequencyMhz={device.freqBMhz}
              band={device.portB?.band ?? ''}
              isActive={device.activeRadio === 2}
              isTransmitting={device.portB?.isTransmitting ?? false}
              isSO2R={true}
              onActivate={() => activateChannelTunerGenius(device.deviceSerial, 2)}
            />
          )}
        </div>
      </div>
    </GlassPanel>
  );
}

interface RadioInputProps {
  label: string;
  color: string;
  frequencyMhz: number;
  band: string;
  isActive: boolean;
  isTransmitting: boolean;
  isSO2R: boolean;
  onActivate: () => void;
}

function RadioInput({ label, color, frequencyMhz, band, isActive, isTransmitting, isSO2R, onActivate }: RadioInputProps) {
  return (
    <div className={`
      bg-dark-700/50 rounded-lg p-3 border transition-colors
      ${isActive ? `border-${color}/40` : 'border-glass-100'}
    `}>
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {isSO2R && (
            <button
              onClick={onActivate}
              className={`w-3 h-3 rounded-full border-2 transition-colors ${
                isActive
                  ? `bg-${color} border-${color}`
                  : 'bg-transparent border-dark-400 hover:border-dark-300'
              }`}
              title={isActive ? 'Active radio input' : 'Click to activate this radio'}
            />
          )}
          <span className={`text-sm font-medium font-ui text-${color}`}>{label}</span>
          {isActive && isSO2R && (
            <span className="text-xs text-dark-400 font-ui">(Active)</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {isTransmitting && (
            <span className="flex items-center gap-1 text-xs text-accent-danger animate-pulse">
              <Zap className="w-3 h-3" />
              TX
            </span>
          )}
          <span className="text-xs font-mono text-dark-300">{band || '--'}</span>
        </div>
      </div>
      <div className="mt-1.5 text-lg font-bold font-mono text-dark-200">
        {frequencyMhz > 0 ? `${frequencyMhz.toFixed(3)} MHz` : 'N/A'}
      </div>
    </div>
  );
}

interface LCGaugeProps {
  label: string;
  value: number;   // 0–255
}

function LCGauge({ label, value }: LCGaugeProps) {
  const pct = Math.round((value / 255) * 100);
  return (
    <div className="flex items-center gap-3">
      <span className="text-xs font-mono text-dark-300 w-6 text-right">{label}</span>
      <div className="flex-1 h-1.5 bg-dark-700 rounded-full overflow-hidden">
        <div
          className="h-full bg-accent-primary/70 rounded-full transition-all duration-300"
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="text-xs font-mono text-dark-300 w-8 text-right">{value}</span>
    </div>
  );
}

function getSWRColor(swr: number): string {
  if (swr <= 0 || swr >= 99) return 'text-dark-400';
  if (swr <= 1.5) return 'text-accent-success';
  if (swr <= 2.0) return 'text-accent-warning';
  return 'text-accent-danger';
}

// Keep StatusBadge for potential future use
export interface StatusBadgeProps {
  result: string;
}

export function StatusBadge({ result }: StatusBadgeProps) {
  if (result === 'OK') {
    return (
      <span className="flex items-center gap-1 text-xs text-accent-success">
        <CheckCircle className="w-3 h-3" />
        OK
      </span>
    );
  }

  if (result.includes('HighSWR') || result.includes('Timeout') || result.includes('Error')) {
    return (
      <span className="flex items-center gap-1 text-xs text-accent-danger">
        <AlertTriangle className="w-3 h-3" />
        {result}
      </span>
    );
  }

  return (
    <span className="text-xs text-dark-300">
      {result}
    </span>
  );
}
