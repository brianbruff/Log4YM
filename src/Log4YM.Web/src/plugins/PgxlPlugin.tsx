import { Wifi, WifiOff, Power, PowerOff, Thermometer, Zap } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';

export function PgxlPlugin() {
  const { pgxlDevices } = useAppStore();
  const { setPgxlOperate, setPgxlStandby } = useSignalR();

  // Convert Map to array for rendering
  const devices = Array.from(pgxlDevices.values());

  if (devices.length === 0) {
    return (
      <GlassPanel
        title="PGXL Amplifier"
        icon={<Zap className="w-5 h-5" />}
      >
        <div className="flex flex-col items-center justify-center h-full p-8 text-gray-500">
          <WifiOff className="w-12 h-12 mb-4 opacity-50" />
          <p className="text-center">No PGXL amplifiers found</p>
          <p className="text-sm text-gray-600 mt-2 text-center">
            Configure Pgxl:Addresses in appsettings.json
          </p>
        </div>
      </GlassPanel>
    );
  }

  // For now, show the first device
  const device = devices[0];

  return (
    <GlassPanel
      title="PGXL Amplifier"
      icon={<Zap className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          {device.isConnected ? (
            <span className="flex items-center gap-1.5 text-xs text-accent-success">
              <Wifi className="w-3.5 h-3.5" />
              Connected
            </span>
          ) : (
            <span className="flex items-center gap-1.5 text-xs text-gray-500">
              <WifiOff className="w-3.5 h-3.5" />
              Disconnected
            </span>
          )}
        </div>
      }
    >
      <div className="p-4 space-y-4">
        {/* Device Info & Operate/Standby Toggle */}
        <div className="flex items-center justify-between">
          <div className="text-sm text-gray-400">
            <span className="font-medium text-gray-200">{device.serial}</span>
            <span className="mx-2">|</span>
            <span className="font-mono text-xs">{device.ipAddress}</span>
          </div>

          <OperateStandbyToggle
            isOperating={device.isOperating}
            onOperate={() => setPgxlOperate(device.serial)}
            onStandby={() => setPgxlStandby(device.serial)}
          />
        </div>

        {/* Band Display */}
        <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
          <div className="text-xs text-gray-500 uppercase tracking-wider mb-1">Band</div>
          <div className="text-2xl font-bold text-accent-primary">{device.band}</div>
        </div>

        {/* Power Meters */}
        <div className="grid grid-cols-2 gap-3">
          <MeterDisplay
            label="Output Power"
            value={device.meters.forwardPowerWatts}
            unit="W"
            max={1500}
            color="accent-primary"
            showBar
          />
          <MeterDisplay
            label="SWR"
            value={device.meters.swrRatio}
            unit=":1"
            max={3}
            color={device.meters.swrRatio > 2 ? 'accent-danger' : 'accent-success'}
            showBar
            precision={1}
          />
        </div>

        {/* Secondary Meters */}
        <div className="grid grid-cols-3 gap-2">
          <SmallMeter
            label="Drive"
            value={device.meters.drivePowerDbm}
            unit="dBm"
          />
          <SmallMeter
            label="PA Current"
            value={device.meters.paCurrent}
            unit="A"
          />
          <SmallMeter
            label="Temp"
            value={device.meters.temperatureC}
            unit="°C"
            icon={<Thermometer className="w-3 h-3" />}
            warning={device.meters.temperatureC > 50}
          />
        </div>

        {/* Status Indicators */}
        {(device.setup.highSwr || device.setup.overTemp || device.setup.overCurrent) && (
          <div className="flex gap-2">
            {device.setup.highSwr && (
              <StatusBadge label="HIGH SWR" variant="danger" />
            )}
            {device.setup.overTemp && (
              <StatusBadge label="OVER TEMP" variant="danger" />
            )}
            {device.setup.overCurrent && (
              <StatusBadge label="OVER CURRENT" variant="danger" />
            )}
          </div>
        )}
      </div>
    </GlassPanel>
  );
}

interface OperateStandbyToggleProps {
  isOperating: boolean;
  onOperate: () => void;
  onStandby: () => void;
}

function OperateStandbyToggle({ isOperating, onOperate, onStandby }: OperateStandbyToggleProps) {
  return (
    <div className="flex rounded-lg overflow-hidden border border-glass-100">
      <button
        onClick={onStandby}
        className={`
          px-3 py-1.5 text-xs font-medium flex items-center gap-1.5 transition-all
          ${!isOperating
            ? 'bg-amber-500/20 text-amber-400 border-r border-amber-500/30'
            : 'bg-dark-700 text-gray-500 hover:bg-dark-600 border-r border-glass-100'
          }
        `}
      >
        <PowerOff className="w-3.5 h-3.5" />
        STBY
      </button>
      <button
        onClick={onOperate}
        className={`
          px-3 py-1.5 text-xs font-medium flex items-center gap-1.5 transition-all
          ${isOperating
            ? 'bg-green-500/20 text-green-400'
            : 'bg-dark-700 text-gray-500 hover:bg-dark-600'
          }
        `}
      >
        <Power className="w-3.5 h-3.5" />
        OPER
      </button>
    </div>
  );
}

interface MeterDisplayProps {
  label: string;
  value: number;
  unit: string;
  max: number;
  color: string;
  showBar?: boolean;
  precision?: number;
}

function MeterDisplay({ label, value, unit, max, color, showBar, precision = 0 }: MeterDisplayProps) {
  const percentage = Math.min((value / max) * 100, 100);
  const displayValue = precision > 0 ? value.toFixed(precision) : Math.round(value);

  return (
    <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
      <div className="text-xs text-gray-500 uppercase tracking-wider mb-1">{label}</div>
      <div className={`text-xl font-bold text-${color}`}>
        {displayValue}
        <span className="text-sm font-normal text-gray-500 ml-1">{unit}</span>
      </div>
      {showBar && (
        <div className="mt-2 h-1.5 bg-dark-600 rounded-full overflow-hidden">
          <div
            className={`h-full bg-${color} transition-all duration-300`}
            style={{ width: `${percentage}%` }}
          />
        </div>
      )}
    </div>
  );
}

interface SmallMeterProps {
  label: string;
  value: number;
  unit: string;
  icon?: React.ReactNode;
  warning?: boolean;
}

function SmallMeter({ label, value, unit, icon, warning }: SmallMeterProps) {
  return (
    <div className={`
      bg-dark-700/30 rounded-lg p-2 border border-glass-100
      ${warning ? 'border-amber-500/50' : ''}
    `}>
      <div className="flex items-center gap-1 text-xs text-gray-500 mb-0.5">
        {icon}
        {label}
      </div>
      <div className={`text-sm font-medium ${warning ? 'text-amber-400' : 'text-gray-300'}`}>
        {value.toFixed(1)}
        <span className="text-xs text-gray-500 ml-0.5">{unit}</span>
      </div>
    </div>
  );
}

interface StatusBadgeProps {
  label: string;
  variant: 'danger' | 'warning' | 'success';
}

function StatusBadge({ label, variant }: StatusBadgeProps) {
  const colors = {
    danger: 'bg-red-500/20 text-red-400 border-red-500/30',
    warning: 'bg-amber-500/20 text-amber-400 border-amber-500/30',
    success: 'bg-green-500/20 text-green-400 border-green-500/30',
  };

  return (
    <span className={`px-2 py-0.5 text-xs font-medium rounded border ${colors[variant]} animate-pulse`}>
      {label}
    </span>
  );
}
