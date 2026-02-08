import { Radio, Wifi, WifiOff, MapPin, Clock, Loader2 } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useEffect, useState } from 'react';

export function StatusBar() {
  const { connectionState, reconnectAttempt, stationCallsign, stationGrid, rigStatus } = useAppStore();
  const [currentTime, setCurrentTime] = useState(new Date());

  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const formatUtcTime = (date: Date) => {
    return date.toISOString().slice(11, 19);
  };

  const formatFrequency = (freq: number) => {
    return (freq / 1000000).toFixed(3);
  };

  return (
    <div className="h-8 bg-dark-800/90 backdrop-blur-sm border-t border-glass-100 flex items-center justify-between px-4 text-sm font-ui">
      {/* Left side - Station info */}
      <div className="flex items-center gap-6">
        <div className="flex items-center gap-2">
          <Radio className="w-4 h-4 text-accent-primary" />
          <span className="font-mono font-bold text-accent-primary">{stationCallsign}</span>
        </div>

        <div className="flex items-center gap-2 text-dark-300">
          <MapPin className="w-3 h-3" />
          <span className="font-mono">{stationGrid}</span>
        </div>

        {rigStatus && (
          <div className="flex items-center gap-2">
            <span className="frequency-display text-accent-secondary">
              {formatFrequency(rigStatus.frequency)} MHz
            </span>
            <span className={`badge ${rigStatus.mode === 'CW' ? 'badge-cw' : rigStatus.mode === 'SSB' ? 'badge-ssb' : 'badge-ft8'}`}>
              {rigStatus.mode}
            </span>
            {rigStatus.isTransmitting && (
              <span className="px-2 py-0.5 bg-accent-danger/20 text-accent-danger rounded text-xs font-bold animate-pulse font-mono">
                TX
              </span>
            )}
          </div>
        )}
      </div>

      {/* Right side - Time and connection */}
      <div className="flex items-center gap-6">
        <div className="flex items-center gap-2 text-dark-300">
          <Clock className="w-3 h-3" />
          <span className="font-mono">{formatUtcTime(currentTime)} UTC</span>
        </div>

        <div className="flex items-center gap-2">
          {connectionState === 'connected' && (
            <>
              <Wifi className="w-4 h-4 text-accent-success" />
              <span className="text-accent-success text-xs font-mono">Connected</span>
            </>
          )}
          {connectionState === 'connecting' && (
            <>
              <Loader2 className="w-4 h-4 text-accent-secondary animate-spin" />
              <span className="text-accent-secondary text-xs font-mono">Connecting...</span>
            </>
          )}
          {connectionState === 'reconnecting' && (
            <>
              <Loader2 className="w-4 h-4 text-accent-warning animate-spin" />
              <span className="text-accent-warning text-xs font-mono">
                Reconnecting{reconnectAttempt > 0 ? ` (${reconnectAttempt})` : '...'}
              </span>
            </>
          )}
          {connectionState === 'rehydrating' && (
            <>
              <Loader2 className="w-4 h-4 text-accent-secondary animate-spin" />
              <span className="text-accent-secondary text-xs font-mono">Loading data...</span>
            </>
          )}
          {connectionState === 'disconnected' && (
            <>
              <WifiOff className="w-4 h-4 text-accent-danger" />
              <span className="text-accent-danger text-xs font-mono">Disconnected</span>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
