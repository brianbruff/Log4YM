import { Radio, Wifi, WifiOff, Zap } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import type { AntennaGeniusStatusEvent, AntennaGeniusAntennaInfo, AntennaGeniusBandInfo } from '../api/signalr';

export function AntennaGeniusPlugin() {
  const { antennaGeniusDevices } = useAppStore();
  const { selectAntenna } = useSignalR();

  // Convert Map to array for rendering
  const devices = Array.from(antennaGeniusDevices.values());

  if (devices.length === 0) {
    return (
      <GlassPanel
        title="Antenna Genius"
        icon={<Radio className="w-5 h-5" />}
      >
        <div className="flex flex-col items-center justify-center h-full p-8 text-gray-500">
          <WifiOff className="w-12 h-12 mb-4 opacity-50" />
          <p className="text-center">No Antenna Genius devices found</p>
          <p className="text-sm text-gray-600 mt-2 text-center">
            Waiting for device discovery on UDP port 9007...
          </p>
        </div>
      </GlassPanel>
    );
  }

  // For now, just show the first device
  const device = devices[0];

  return (
    <GlassPanel
      title="Antenna Genius"
      icon={<Radio className="w-5 h-5" />}
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
        {/* Device Info */}
        <div className="text-sm text-gray-400">
          <span className="font-medium text-gray-200">{device.deviceName}</span>
          <span className="mx-2">|</span>
          <span>v{device.version}</span>
          <span className="mx-2">|</span>
          <span className="font-mono text-xs">{device.ipAddress}</span>
        </div>

        {/* Port Status Headers */}
        <div className="grid grid-cols-2 gap-4">
          <PortHeader
            portId={1}
            label="Radio A"
            band={device.bands.find(b => b.id === device.portA.band)}
            isTransmitting={device.portA.isTransmitting}
          />
          <PortHeader
            portId={2}
            label="Radio B"
            band={device.bands.find(b => b.id === device.portB.band)}
            isTransmitting={device.portB.isTransmitting}
          />
        </div>

        {/* Antenna List */}
        <div className="space-y-2">
          {device.antennas.map((antenna) => (
            <AntennaRow
              key={antenna.id}
              antenna={antenna}
              device={device}
              onSelectA={() => selectAntenna(device.deviceSerial, 1, antenna.id)}
              onSelectB={() => selectAntenna(device.deviceSerial, 2, antenna.id)}
            />
          ))}
        </div>
      </div>
    </GlassPanel>
  );
}

interface PortHeaderProps {
  portId: number;
  label: string;
  band?: AntennaGeniusBandInfo;
  isTransmitting: boolean;
}

function PortHeader({ portId, label, band, isTransmitting }: PortHeaderProps) {
  return (
    <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
      <div className="flex items-center justify-between">
        <span className={`text-sm font-medium ${portId === 1 ? 'text-accent-primary' : 'text-accent-success'}`}>
          {label}
        </span>
        {isTransmitting && (
          <span className="flex items-center gap-1 text-xs text-red-500 animate-pulse">
            <Zap className="w-3 h-3" />
            TX
          </span>
        )}
      </div>
      <div className="mt-1 text-lg font-bold text-gray-100">
        {band?.name || 'None'}
      </div>
    </div>
  );
}

interface AntennaRowProps {
  antenna: AntennaGeniusAntennaInfo;
  device: AntennaGeniusStatusEvent;
  onSelectA: () => void;
  onSelectB: () => void;
}

function AntennaRow({ antenna, device, onSelectA, onSelectB }: AntennaRowProps) {
  const isSelectedA = device.portA.rxAntenna === antenna.id || device.portA.txAntenna === antenna.id;
  const isSelectedB = device.portB.rxAntenna === antenna.id || device.portB.txAntenna === antenna.id;
  const isTxA = device.portA.isTransmitting && device.portA.txAntenna === antenna.id;
  const isTxB = device.portB.isTransmitting && device.portB.txAntenna === antenna.id;

  return (
    <div
      className={`
        flex items-center gap-2 p-2 rounded-lg border transition-all duration-200
        ${isSelectedA || isSelectedB
          ? 'bg-dark-600/70 border-glass-200'
          : 'bg-dark-700/30 border-glass-100 hover:bg-dark-700/50'
        }
      `}
    >
      {/* Port A Button */}
      <button
        onClick={onSelectA}
        className={`
          w-8 h-8 rounded flex items-center justify-center text-xs font-bold transition-all
          ${isSelectedA
            ? isTxA
              ? 'bg-red-500/80 text-white ring-2 ring-red-400 animate-pulse'
              : 'bg-accent-primary text-white'
            : 'bg-dark-600 text-gray-500 hover:bg-accent-primary/30 hover:text-accent-primary'
          }
        `}
        title={`Select for Radio A`}
      >
        A
      </button>

      {/* Antenna Name */}
      <div className="flex-1 min-w-0">
        <span
          className={`
            text-sm font-medium truncate block
            ${isSelectedA || isSelectedB ? 'text-gray-100' : 'text-gray-400'}
          `}
        >
          {antenna.name}
        </span>
      </div>

      {/* Port B Button */}
      <button
        onClick={onSelectB}
        className={`
          w-8 h-8 rounded flex items-center justify-center text-xs font-bold transition-all
          ${isSelectedB
            ? isTxB
              ? 'bg-red-500/80 text-white ring-2 ring-red-400 animate-pulse'
              : 'bg-accent-success text-white'
            : 'bg-dark-600 text-gray-500 hover:bg-accent-success/30 hover:text-accent-success'
          }
        `}
        title={`Select for Radio B`}
      >
        B
      </button>
    </div>
  );
}
