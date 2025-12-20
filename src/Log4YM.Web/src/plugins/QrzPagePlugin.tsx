import { useState } from 'react';
import { ExternalLink, Globe, Loader2, RefreshCw } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { GlassPanel } from '../components/GlassPanel';

export function QrzPagePlugin() {
  const { focusedCallsignInfo, isLookingUpCallsign } = useAppStore();
  const [isLoading, setIsLoading] = useState(true);
  const [key, setKey] = useState(0); // For forcing iframe refresh

  const callsign = focusedCallsignInfo?.callsign;
  const qrzUrl = callsign ? `https://www.qrz.com/db/${callsign}` : null;

  const handleOpenInNewTab = () => {
    if (qrzUrl) {
      window.open(qrzUrl, '_blank', 'noopener,noreferrer');
    }
  };

  const handleRefresh = () => {
    setIsLoading(true);
    setKey(prev => prev + 1);
  };

  return (
    <GlassPanel
      title="QRZ Page"
      icon={<Globe className="w-5 h-5" />}
      actions={
        callsign && (
          <div className="flex items-center gap-1">
            <button
              onClick={handleRefresh}
              className="glass-button p-1.5"
              title="Refresh page"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
            <button
              onClick={handleOpenInNewTab}
              className="glass-button p-1.5"
              title="Open in new tab"
            >
              <ExternalLink className="w-4 h-4" />
            </button>
          </div>
        )
      }
    >
      <div className="h-full min-h-[400px] flex flex-col">
        {/* Loading state */}
        {isLookingUpCallsign && !callsign && (
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <div className="w-16 h-16 mx-auto mb-4 rounded-full border-4 border-accent-primary/30 border-t-accent-primary animate-spin" />
              <p className="text-gray-400 text-sm">Looking up callsign...</p>
            </div>
          </div>
        )}

        {/* Empty state */}
        {!isLookingUpCallsign && !callsign && (
          <div className="flex-1 flex items-center justify-center p-8">
            <div className="text-center">
              <Globe className="w-16 h-16 text-gray-600 mx-auto mb-4" />
              <p className="text-gray-400 text-lg font-medium mb-2">No Callsign Selected</p>
              <p className="text-gray-500 text-sm">
                Enter a callsign in the Log Entry to view their QRZ page.
              </p>
            </div>
          </div>
        )}

        {/* QRZ iframe */}
        {callsign && qrzUrl && (
          <div className="flex-1 relative">
            {/* Loading overlay */}
            {isLoading && (
              <div className="absolute inset-0 bg-dark-800/90 flex items-center justify-center z-10">
                <div className="text-center">
                  <Loader2 className="w-10 h-10 text-accent-primary animate-spin mx-auto mb-3" />
                  <p className="text-gray-400 text-sm">Loading QRZ page...</p>
                </div>
              </div>
            )}

            {/* Iframe */}
            <iframe
              key={key}
              src={qrzUrl}
              className="w-full h-full border-0"
              title={`QRZ page for ${callsign}`}
              onLoad={() => setIsLoading(false)}
              onError={() => setIsLoading(false)}
              sandbox="allow-scripts allow-same-origin allow-forms allow-popups"
              referrerPolicy="no-referrer"
            />

            {/* Footer with URL and open button */}
            <div className="absolute bottom-0 left-0 right-0 bg-dark-800/95 backdrop-blur-sm border-t border-glass-100 px-3 py-2 flex items-center justify-between">
              <span className="text-xs text-gray-500 truncate flex-1 mr-2 font-mono">
                {qrzUrl}
              </span>
              <button
                onClick={handleOpenInNewTab}
                className="flex items-center gap-1.5 text-xs text-accent-primary hover:text-accent-primary/80 transition-colors"
              >
                <ExternalLink className="w-3 h-3" />
                Open in new tab
              </button>
            </div>
          </div>
        )}
      </div>
    </GlassPanel>
  );
}
