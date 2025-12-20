import { User, MapPin, Navigation, Globe } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { GlassPanel } from '../components/GlassPanel';
import { getCountryFlag } from '../core/countryFlags';

export function QrzProfilePlugin() {
  const { focusedCallsignInfo, isLookingUpCallsign } = useAppStore();

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
              <p className="text-gray-400 text-sm">Looking up callsign...</p>
            </div>
          </div>
        )}

        {/* Empty state */}
        {!isLookingUpCallsign && !focusedCallsignInfo && (
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <User className="w-16 h-16 text-gray-600 mx-auto mb-4" />
              <p className="text-gray-400 text-lg font-medium mb-2">No Callsign Selected</p>
              <p className="text-gray-500 text-sm">
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
                  className="w-32 h-32 rounded-xl object-cover border-2 border-glass-200 shadow-lg"
                />
              ) : (
                <div className="w-32 h-32 rounded-xl bg-dark-700 border-2 border-glass-200 flex items-center justify-center">
                  <User className="w-16 h-16 text-gray-600" />
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
              <p className="text-lg text-gray-200">
                {focusedCallsignInfo.name || 'Unknown Operator'}
              </p>
            </div>

            {/* Location info */}
            <div className="space-y-3">
              {/* Country */}
              {focusedCallsignInfo.country && (
                <div className="flex items-center gap-3 text-gray-300">
                  <Globe className="w-4 h-4 text-gray-500" />
                  <span>{focusedCallsignInfo.country}</span>
                  {focusedCallsignInfo.state && (
                    <span className="text-gray-500">({focusedCallsignInfo.state})</span>
                  )}
                </div>
              )}

              {/* Grid square */}
              {focusedCallsignInfo.grid && (
                <div className="flex items-center gap-3 text-gray-300">
                  <MapPin className="w-4 h-4 text-gray-500" />
                  <span className="font-mono">{focusedCallsignInfo.grid}</span>
                  {focusedCallsignInfo.latitude != null && focusedCallsignInfo.longitude != null && (
                    <span className="text-gray-500 text-sm">
                      ({focusedCallsignInfo.latitude.toFixed(2)}°, {focusedCallsignInfo.longitude.toFixed(2)}°)
                    </span>
                  )}
                </div>
              )}

              {/* Bearing and distance */}
              {shortPath != null && (
                <div className="flex items-center gap-3 text-gray-300">
                  <Navigation className="w-4 h-4 text-gray-500" />
                  <div className="flex items-center gap-4">
                    <span>
                      <span className="text-accent-warning font-mono">{shortPath.toFixed(0)}°</span>
                      <span className="text-gray-500 text-xs ml-1">SP</span>
                    </span>
                    <span>
                      <span className="text-accent-warning font-mono">{longPath?.toFixed(0)}°</span>
                      <span className="text-gray-500 text-xs ml-1">LP</span>
                    </span>
                    {focusedCallsignInfo.distance != null && (
                      <span className="text-accent-info">
                        {Math.round(focusedCallsignInfo.distance).toLocaleString()} km
                      </span>
                    )}
                  </div>
                </div>
              )}

              {/* DXCC / CQ / ITU zones */}
              {(focusedCallsignInfo.dxcc || focusedCallsignInfo.cqZone || focusedCallsignInfo.ituZone) && (
                <div className="flex items-center gap-4 text-sm text-gray-400 pt-2 border-t border-glass-100">
                  {focusedCallsignInfo.dxcc && (
                    <span>DXCC: <span className="text-gray-300">{focusedCallsignInfo.dxcc}</span></span>
                  )}
                  {focusedCallsignInfo.cqZone && (
                    <span>CQ: <span className="text-gray-300">{focusedCallsignInfo.cqZone}</span></span>
                  )}
                  {focusedCallsignInfo.ituZone && (
                    <span>ITU: <span className="text-gray-300">{focusedCallsignInfo.ituZone}</span></span>
                  )}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </GlassPanel>
  );
}
