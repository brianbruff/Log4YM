import { WifiOff, RefreshCw, Loader2, Database } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { signalRService } from '../api/signalr';
import { useState } from 'react';

export function ConnectionOverlay() {
  const { connectionState, reconnectAttempt } = useAppStore();
  const [isManualReconnecting, setIsManualReconnecting] = useState(false);

  // Only show overlay when NOT fully connected
  if (connectionState === 'connected' || connectionState === 'connecting') {
    return null;
  }

  const handleReconnect = async () => {
    setIsManualReconnecting(true);
    try {
      await signalRService.reconnect();
    } finally {
      setIsManualReconnecting(false);
    }
  };

  const isReconnecting = connectionState === 'reconnecting';
  const isRehydrating = connectionState === 'rehydrating';
  const isDisconnected = connectionState === 'disconnected';
  const showSpinner = isReconnecting || isRehydrating || isManualReconnecting;

  return (
    <div className="fixed inset-0 bg-dark-900/95 backdrop-blur-sm flex items-center justify-center z-[100]">
      <div className="glass-panel w-96 p-6 text-center">
        <div className="flex justify-center mb-4">
          {isRehydrating ? (
            <div className="relative">
              <Database className="w-16 h-16 text-accent-secondary animate-pulse" />
            </div>
          ) : showSpinner ? (
            <div className="relative">
              <Loader2 className="w-16 h-16 text-accent-primary animate-spin" />
            </div>
          ) : (
            <div className="w-16 h-16 rounded-full bg-accent-danger/20 flex items-center justify-center">
              <WifiOff className="w-8 h-8 text-accent-danger" />
            </div>
          )}
        </div>

        <h2 className="text-xl font-bold text-accent-primary mb-2 font-display tracking-wide">
          {isRehydrating ? 'Loading Data...' : isReconnecting ? 'Reconnecting...' : 'Connection Lost'}
        </h2>

        <p className="text-dark-300 mb-4 font-ui">
          {isRehydrating ? (
            <>
              Restoring application state from server.
              <span className="block text-sm mt-1">
                Please wait while settings and device states are loaded.
              </span>
            </>
          ) : isReconnecting ? (
            <>
              Attempting to reconnect to the server
              {reconnectAttempt > 0 && (
                <span className="block text-sm mt-1 font-mono text-accent-secondary">
                  Attempt {reconnectAttempt}
                </span>
              )}
            </>
          ) : (
            'Unable to connect to the Log4YM server. Please ensure the server is running.'
          )}
        </p>

        {isDisconnected && !isManualReconnecting && (
          <button
            onClick={handleReconnect}
            disabled={isManualReconnecting}
            className="glass-button px-6 py-2 flex items-center gap-2 mx-auto hover:border-accent-secondary/40 disabled:opacity-50"
          >
            <RefreshCw className="w-4 h-4" />
            <span>Reconnect Now</span>
          </button>
        )}

        {isDisconnected && (
          <div className="mt-6 pt-4 border-t border-glass-100">
            <p className="text-xs text-dark-300 font-ui">
              The application will automatically retry connection with exponential backoff.
              {reconnectAttempt > 0 && (
                <span className="block mt-1 font-mono text-accent-secondary">
                  Next retry in up to {Math.min(Math.pow(2, reconnectAttempt), 30)} seconds
                </span>
              )}
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
