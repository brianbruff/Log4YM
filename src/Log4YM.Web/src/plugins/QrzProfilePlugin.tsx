import { User, MapPin, Navigation, Globe, ExternalLink } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { GlassPanel } from '../components/GlassPanel';
import { getCountryFlag } from '../core/countryFlags';

export function QrzProfilePlugin() {
  const { focusedCallsignInfo, isLookingUpCallsign } = useAppStore();

  const callsign = focusedCallsignInfo?.callsign;
  const qrzUrl = callsign ? `https://www.qrz.com/db/${callsign}` : null;

  const handleOpenQrz = () => {
    if (qrzUrl) {
      window.open(qrzUrl, '_blank', 'noopener,noreferrer');
    }
  };

  // Calculate long path bearing
  const shortPath = focusedCallsignInfo?.bearing;
  const longPath = shortPath !== undefined ? (shortPath + 180) % 360 : undefined;

  return (
    <GlassPanel
      title="QRZ Profile"
      icon={<User className="w-5 h-5" />}
    >
      <div className="p-4 h-full flex flex-col">
        {/* Loading state */}
        {isLookingUpCallsign && (
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <div className="w-16 h-16 mx-auto mb-4 rounded-full border-4 border-accent-primary/30 border-t-accent-primary animate-spin" />
              <p className="text-dark-300 text-sm font-ui">Looking up callsign...</p>
            </div>
          </div>
        )}

        {/* Empty state */}
        {!isLookingUpCallsign && !focusedCallsignInfo && (
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <User className="w-16 h-16 text-dark-300 mx-auto mb-4" />
              <p className="text-dark-300 text-lg font-medium font-ui mb-2">No Callsign Selected</p>
              <p className="text-dark-300 text-sm font-ui">
                Enter a callsign in the Log Entry to see profile information.
              </p>
            </div>
          </div>
        )}

        {/* Profile content */}
        {!isLookingUpCallsign && focusedCallsignInfo && (
          <div className="flex-1 flex flex-col">
            {/* Profile image */}
            <div className="flex justify-center mb-4">
              {focusedCallsignInfo.imageUrl ? (
                <img
                  src={focusedCallsignInfo.imageUrl}
                  alt={focusedCallsignInfo.callsign}
                  className="w-32 h-32 rounded-xl object-cover border-2 border-glass-100 shadow-lg"
                />
              ) : (
                <div className="w-32 h-32 rounded-xl bg-dark-700 border-2 border-glass-100 flex items-center justify-center">
                  <User className="w-16 h-16 text-dark-300" />
                </div>
              )}
            </div>

            {/* Callsign and name */}
            <div className="text-center mb-4">
              <div className="flex items-center justify-center gap-3 mb-2">
                <span className="text-4xl" title={focusedCallsignInfo.country}>
                  {getCountryFlag(focusedCallsignInfo.country, '🏳️')}
                </span>
                <h2 className="text-2xl font-mono font-bold text-accent-primary">
                  {focusedCallsignInfo.callsign}
                </h2>
              </div>
              <p className="text-lg text-dark-200 font-ui">
                {focusedCallsignInfo.name || 'No name on file'}
              </p>
            </div>

            {/* Location info */}
            <div className="space-y-3">
              {/* Country */}
              {focusedCallsignInfo.country && (
                <div className="flex items-center gap-3 text-dark-200 font-ui">
                  <Globe className="w-4 h-4 text-dark-300" />
                  <span>{focusedCallsignInfo.country}</span>
                  {focusedCallsignInfo.state && (
                    <span className="text-dark-300">({focusedCallsignInfo.state})</span>
                  )}
                </div>
              )}

              {/* Grid square */}
              {focusedCallsignInfo.grid && (
                <div className="flex items-center gap-3 text-dark-200">
                  <MapPin className="w-4 h-4 text-dark-300" />
                  <span className="font-mono">{focusedCallsignInfo.grid}</span>
                  {focusedCallsignInfo.latitude != null && focusedCallsignInfo.longitude != null && (
                    <span className="text-dark-300 text-sm font-mono">
                      ({focusedCallsignInfo.latitude.toFixed(2)}°, {focusedCallsignInfo.longitude.toFixed(2)}°)
                    </span>
                  )}
                </div>
              )}

              {/* Bearing and distance */}
              {shortPath != null && (
                <div className="flex items-center gap-3 text-dark-200">
                  <Navigation className="w-4 h-4 text-dark-300" />
                  <div className="flex items-center gap-4">
                    <span>
                      <span className="text-accent-primary font-mono">{shortPath.toFixed(0)}°</span>
                      <span className="text-dark-300 text-xs ml-1 font-ui">SP</span>
                    </span>
                    <span>
                      <span className="text-accent-primary font-mono">{longPath?.toFixed(0)}°</span>
                      <span className="text-dark-300 text-xs ml-1 font-ui">LP</span>
                    </span>
                    {focusedCallsignInfo.distance != null && (
                      <span className="text-accent-secondary font-mono">
                        {Math.round(focusedCallsignInfo.distance).toLocaleString()} km
                      </span>
                    )}
                  </div>
                </div>
              )}

              {/* DXCC / CQ / ITU zones */}
              {(focusedCallsignInfo.dxcc || focusedCallsignInfo.cqZone || focusedCallsignInfo.ituZone) && (
                <div className="flex items-center gap-4 text-sm text-dark-300 font-ui pt-2 border-t border-glass-100">
                  {focusedCallsignInfo.dxcc && (
                    <span>DXCC: <span className="text-dark-200 font-mono">{focusedCallsignInfo.dxcc}</span></span>
                  )}
                  {focusedCallsignInfo.cqZone && (
                    <span>CQ: <span className="text-dark-200 font-mono">{focusedCallsignInfo.cqZone}</span></span>
                  )}
                  {focusedCallsignInfo.ituZone && (
                    <span>ITU: <span className="text-dark-200 font-mono">{focusedCallsignInfo.ituZone}</span></span>
                  )}
                </div>
              )}

              {/* View on QRZ button */}
              <button
                onClick={handleOpenQrz}
                className="mt-4 w-full glass-button py-2 flex items-center justify-center gap-2 text-accent-primary hover:bg-dark-600 transition-colors font-ui"
              >
                <ExternalLink className="w-4 h-4" />
                <span className="text-sm font-medium">View Full QRZ Profile</span>
              </button>
            </div>
          </div>
        )}
      </div>
    </GlassPanel>
  );
}
