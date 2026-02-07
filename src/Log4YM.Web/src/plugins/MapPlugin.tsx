import { useEffect, useRef, useCallback, useState } from 'react';
import { Map as MapIcon, MapPin, Target, Maximize2, ZoomIn, ZoomOut, Layers, Radio } from 'lucide-react';
import { MapContainer, TileLayer, Marker, Popup, useMapEvents, Circle, Polyline, CircleMarker } from 'react-leaflet';
import L from 'leaflet';
import { useAppStore } from '../store/appStore';
import { useSettingsStore } from '../store/settingsStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import { gridToLatLon, calculateDistance, getAnimationDuration } from '../utils/maidenhead';
import { api, type RbnSpot } from '../api/client';

import 'leaflet/dist/leaflet.css';

// Fix for default marker icons in webpack/vite
delete (L.Icon.Default.prototype as unknown as { _getIconUrl?: unknown })._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

// Default station location (IO52RN - Limerick area)
const DEFAULT_LAT = 52.6667;
const DEFAULT_LON = -8.6333;

// Custom station marker
const stationIcon = new L.DivIcon({
  className: 'custom-station-marker',
  html: `
    <div style="
      width: 24px;
      height: 24px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: 3px solid #fff;
      border-radius: 50%;
      box-shadow: 0 0 10px rgba(99, 102, 241, 0.6);
    "></div>
  `,
  iconSize: [24, 24],
  iconAnchor: [12, 12],
});

// Custom target marker
const targetIcon = new L.DivIcon({
  className: 'custom-target-marker',
  html: `
    <div style="
      width: 20px;
      height: 20px;
      background: linear-gradient(135deg, #f59e0b, #ef4444);
      border: 2px solid #fff;
      border-radius: 50%;
      box-shadow: 0 0 8px rgba(245, 158, 11, 0.6);
    "></div>
  `,
  iconSize: [20, 20],
  iconAnchor: [10, 10],
});

// Tile layer options for different map styles
const TILE_LAYERS = {
  osm: {
    name: 'OpenStreetMap',
    url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  },
  dark: {
    name: 'Dark',
    url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/">CARTO</a>',
  },
  satellite: {
    name: 'Satellite',
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attribution: '&copy; Esri',
  },
  terrain: {
    name: 'Terrain',
    url: 'https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png',
    attribution: '&copy; <a href="https://opentopomap.org">OpenTopoMap</a>',
  },
};

type TileLayerKey = keyof typeof TILE_LAYERS;

// Map click handler component
function MapClickHandler({
  stationLat,
  stationLon,
  onBearingClick
}: {
  stationLat: number;
  stationLon: number;
  onBearingClick: (azimuth: number) => void;
}) {
  useMapEvents({
    click(e) {
      const { lat, lng } = e.latlng;
      const azimuth = calculateAzimuth(stationLat, stationLon, lat, lng);
      onBearingClick(azimuth);
    },
  });
  return null;
}

// Calculate azimuth between two points
function calculateAzimuth(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const toRad = Math.PI / 180;
  const toDeg = 180 / Math.PI;

  const dLon = (lon2 - lon1) * toRad;
  const lat1Rad = lat1 * toRad;
  const lat2Rad = lat2 * toRad;

  const y = Math.sin(dLon) * Math.cos(lat2Rad);
  const x = Math.cos(lat1Rad) * Math.sin(lat2Rad) -
      Math.sin(lat1Rad) * Math.cos(lat2Rad) * Math.cos(dLon);

  let azimuth = Math.atan2(y, x) * toDeg;
  azimuth = (azimuth + 360) % 360;

  return Math.round(azimuth);
}

// Calculate destination point from start, azimuth, and distance
function getDestinationPoint(lat: number, lon: number, azimuth: number, distanceKm: number): [number, number] {
  const R = 6371;
  const toRad = Math.PI / 180;
  const toDeg = 180 / Math.PI;

  const lat1Rad = lat * toRad;
  const lon1Rad = lon * toRad;
  const azimuthRad = azimuth * toRad;
  const angularDistance = distanceKm / R;

  const lat2Rad = Math.asin(
    Math.sin(lat1Rad) * Math.cos(angularDistance) +
    Math.cos(lat1Rad) * Math.sin(angularDistance) * Math.cos(azimuthRad)
  );

  const lon2Rad = lon1Rad + Math.atan2(
    Math.sin(azimuthRad) * Math.sin(angularDistance) * Math.cos(lat1Rad),
    Math.cos(angularDistance) - Math.sin(lat1Rad) * Math.sin(lat2Rad)
  );

  return [lat2Rad * toDeg, ((lon2Rad * toDeg + 540) % 360) - 180];
}


export function MapPlugin() {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const lastTargetCoordsRef = useRef<{ lat: number; lon: number } | null>(null);
  const { stationGrid, rotatorPosition, focusedCallsignInfo } = useAppStore();
  const { settings, updateMapSettings, saveSettings } = useSettingsStore();
  const { commandRotator } = useSignalR();
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [currentAzimuth, setCurrentAzimuth] = useState(0);
  const [showLayerPicker, setShowLayerPicker] = useState(false);
  const [rbnSpots, setRbnSpots] = useState<RbnSpot[]>([]);
  const [showRbnPanel, setShowRbnPanel] = useState(false);

  // Get tile layer and RBN settings from persisted settings
  const tileLayer = settings.map.tileLayer;
  const rbnSettings = settings.map.rbn;

  // Rotator is enabled in settings
  const rotatorEnabled = settings.rotator.enabled;

  // Station coordinates - prioritize lat/lon from settings, fall back to grid square, then defaults
  let stationLat = DEFAULT_LAT;
  let stationLon = DEFAULT_LON;
  
  // First priority: explicit lat/lon in settings
  if (settings.station.latitude != null && settings.station.longitude != null) {
    stationLat = settings.station.latitude;
    stationLon = settings.station.longitude;
  } 
  // Second priority: convert grid square to coordinates
  else if (settings.station.gridSquare) {
    const coords = gridToLatLon(settings.station.gridSquare);
    if (coords) {
      stationLat = coords.lat;
      stationLon = coords.lon;
    }
  }
  // Third priority: use stationGrid from appStore (legacy support)
  else if (stationGrid) {
    const coords = gridToLatLon(stationGrid);
    if (coords) {
      stationLat = coords.lat;
      stationLon = coords.lon;
    }
  }

  // Update azimuth from rotator position only
  useEffect(() => {
    if (rotatorPosition?.currentAzimuth !== undefined) {
      setCurrentAzimuth(rotatorPosition.currentAzimuth);
    }
  }, [rotatorPosition]);

  // Update map center when station coordinates change
  useEffect(() => {
    if (mapRef.current) {
      mapRef.current.setView([stationLat, stationLon], mapRef.current.getZoom());
    }
  }, [stationLat, stationLon]);

  // Fly to target when focused callsign changes with distance-based animation
  useEffect(() => {
    if (!mapRef.current) return;

    const targetLat = focusedCallsignInfo?.latitude;
    const targetLon = focusedCallsignInfo?.longitude;

    // Only fly if we have valid target coordinates
    if (targetLat == null || targetLon == null) return;

    // Calculate distance for animation duration
    let duration = 2; // Default duration
    const lastCoords = lastTargetCoordsRef.current;

    if (lastCoords) {
      // Calculate distance from previous target
      const distance = calculateDistance(lastCoords.lat, lastCoords.lon, targetLat, targetLon);
      duration = getAnimationDuration(distance);
    } else {
      // First target - calculate distance from station
      const distance = calculateDistance(stationLat, stationLon, targetLat, targetLon);
      duration = getAnimationDuration(distance);
    }

    // Update last coordinates ref
    lastTargetCoordsRef.current = { lat: targetLat, lon: targetLon };

    // Fly to target with calculated duration
    mapRef.current.flyTo([targetLat, targetLon], 5, {
      duration: duration,
      easeLinearity: 0.1, // Smooth curved motion
    });
  }, [focusedCallsignInfo?.latitude, focusedCallsignInfo?.longitude, stationLat, stationLon]);

  // Handle click on map to set bearing (only when rotator enabled)
  const handleBearingClick = useCallback((azimuth: number) => {
    if (!rotatorEnabled) return; // Ignore clicks when rotator disabled
    setCurrentAzimuth(azimuth);
    commandRotator(azimuth, 'map');
  }, [commandRotator, rotatorEnabled]);

  // Toggle fullscreen
  const toggleFullscreen = useCallback(() => {
    if (!containerRef.current) return;

    if (!isFullscreen) {
      containerRef.current.requestFullscreen?.();
    } else {
      document.exitFullscreen?.();
    }
    setIsFullscreen(!isFullscreen);
  }, [isFullscreen]);

  // Generate rotator beam visualization line (indigo)
  const beamLinePoints: [number, number][] = [];
  const beamDistance = 5000; // 5000km beam visualization
  for (let d = 0; d <= beamDistance; d += 100) {
    beamLinePoints.push(getDestinationPoint(stationLat, stationLon, currentAzimuth, d));
  }

  // Generate target heading line (orange) - separate from rotator beam
  const targetBearing = focusedCallsignInfo?.bearing;
  const targetLinePoints: [number, number][] = [];
  if (targetBearing != null) {
    for (let d = 0; d <= beamDistance; d += 100) {
      targetLinePoints.push(getDestinationPoint(stationLat, stationLon, targetBearing, d));
    }
  }

  // Target location from focused callsign
  const targetLat = focusedCallsignInfo?.latitude;
  const targetLon = focusedCallsignInfo?.longitude;

  // Fetch RBN spots periodically when enabled
  useEffect(() => {
    if (!rbnSettings.enabled) {
      setRbnSpots([]);
      return;
    }

    const fetchRbnSpots = async () => {
      try {
        const data = await api.getRbnSpots(rbnSettings.timeWindowMinutes);
        if (data && data.spots) {
          // Filter for my callsign
          const myCallsign = settings.station.callsign;
          if (myCallsign) {
            const mySpots = data.spots.filter(s =>
              s.dx.toUpperCase() === myCallsign.toUpperCase()
            );
            setRbnSpots(mySpots);
          } else {
            setRbnSpots([]);
          }
        }
      } catch (error) {
        console.error('Failed to fetch RBN spots:', error);
      }
    };

    fetchRbnSpots();
    const interval = setInterval(fetchRbnSpots, 60000); // Update every minute

    return () => clearInterval(interval);
  }, [rbnSettings.enabled, rbnSettings.timeWindowMinutes, settings.station.callsign]);

  // Helper function to get SNR color
  const getSNRColor = (snr: number | undefined): string => {
    if (snr === null || snr === undefined) return '#888888';
    if (snr < 0) return '#ef4444';      // Red: Weak
    if (snr < 10) return '#f97316';     // Orange: Fair
    if (snr < 20) return '#fbbf24';     // Yellow: Good
    if (snr < 30) return '#84cc16';     // Light green: Very good
    return '#22c55e';                   // Bright green: Excellent
  };

  return (
    <GlassPanel
      title="2D Map"
      icon={<MapIcon className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          {rotatorEnabled ? (
            <span className="text-sm font-mono text-accent-primary">{currentAzimuth}°</span>
          ) : (
            <span className="text-sm text-gray-500">No Rotator</span>
          )}
          <button
            onClick={toggleFullscreen}
            className="glass-button p-1.5"
            title="Fullscreen"
          >
            <Maximize2 className="w-4 h-4" />
          </button>
        </div>
      }
    >
      <div ref={containerRef} className="relative h-full min-h-[400px]">
        <MapContainer
          center={[stationLat, stationLon]}
          zoom={5}
          className="w-full h-full"
          style={{ background: '#1e1e1e' }}
          ref={(map) => { mapRef.current = map ?? null; }}
          zoomControl={false}
        >
          <TileLayer
            url={TILE_LAYERS[tileLayer].url}
            attribution={TILE_LAYERS[tileLayer].attribution}
          />

          {/* Click handler */}
          <MapClickHandler
            stationLat={stationLat}
            stationLon={stationLon}
            onBearingClick={handleBearingClick}
          />

          {/* Station marker */}
          <Marker position={[stationLat, stationLon]} icon={stationIcon}>
            <Popup>
              <div className="text-center">
                <strong className="text-accent-primary">{stationGrid || 'Station'}</strong>
                <br />
                <span className="text-xs text-gray-500">Your Location</span>
              </div>
            </Popup>
          </Marker>

          {/* Station range circle */}
          <Circle
            center={[stationLat, stationLon]}
            radius={500000}
            pathOptions={{
              color: '#6366f1',
              fillColor: '#6366f1',
              fillOpacity: 0.05,
              weight: 1,
              dashArray: '5, 5',
            }}
          />

          {/* Rotator beam direction line - only show when rotator enabled (indigo) */}
          {rotatorEnabled && (
            <Polyline
              positions={beamLinePoints}
              pathOptions={{
                color: '#6366f1',
                weight: 3,
                opacity: 0.7,
                dashArray: '10, 5',
              }}
            />
          )}

          {/* Target heading line - show when we have a focused callsign with bearing (orange) */}
          {targetLinePoints.length > 0 && (
            <Polyline
              positions={targetLinePoints}
              pathOptions={{
                color: '#f97316',
                weight: 2,
                opacity: 0.8,
                dashArray: '5, 10',
              }}
            />
          )}

          {/* Target marker if we have focused callsign with coordinates */}
          {targetLat != null && targetLon != null && (
            <Marker position={[targetLat, targetLon]} icon={targetIcon}>
              <Popup>
                <div className="text-center">
                  <strong className="text-accent-warning">{focusedCallsignInfo?.callsign}</strong>
                  <br />
                  {focusedCallsignInfo?.grid && (
                    <span className="text-xs">{focusedCallsignInfo.grid}</span>
                  )}
                  {focusedCallsignInfo?.bearing != null && (
                    <>
                      <br />
                      <span className="text-xs text-accent-info">
                        {focusedCallsignInfo.bearing.toFixed(0)}° / {Math.round(focusedCallsignInfo.distance ?? 0)} km
                      </span>
                    </>
                  )}
                </div>
              </Popup>
            </Marker>
          )}

          {/* RBN Spots Overlay */}
          {rbnSettings.enabled && rbnSpots.map((spot, idx) => {
            if (!spot.skimmerLat || !spot.skimmerLon) return null;

            // Apply band filter
            if (rbnSettings.bands.length > 0 && !rbnSettings.bands.includes('all') && !rbnSettings.bands.includes(spot.band)) {
              return null;
            }

            // Apply mode filter
            if (rbnSettings.modes.length > 0 && !rbnSettings.modes.includes(spot.mode)) {
              return null;
            }

            // Apply SNR filter
            if (spot.snr !== undefined && spot.snr < rbnSettings.minSnr) {
              return null;
            }

            const color = getSNRColor(spot.snr);
            const size = spot.snr !== undefined && spot.snr >= 20 ? 8 : 6;

            return (
              <div key={`rbn-${idx}`}>
                {/* Path line from station to skimmer */}
                {rbnSettings.showPaths && (
                  <Polyline
                    positions={[
                      [stationLat, stationLon],
                      [spot.skimmerLat, spot.skimmerLon]
                    ]}
                    pathOptions={{
                      color: color,
                      weight: 2,
                      opacity: rbnSettings.opacity * 0.6,
                      dashArray: '5, 5',
                    }}
                  />
                )}

                {/* Skimmer marker */}
                <CircleMarker
                  center={[spot.skimmerLat, spot.skimmerLon]}
                  radius={size}
                  pathOptions={{
                    fillColor: color,
                    color: '#ffffff',
                    weight: 2,
                    opacity: rbnSettings.opacity,
                    fillOpacity: rbnSettings.opacity * 0.8,
                  }}
                >
                  <Popup>
                    <div style={{ fontFamily: 'monospace' }}>
                      <strong>📡 {spot.callsign}</strong><br />
                      Heard: <strong>{spot.dx}</strong><br />
                      SNR: <strong>{spot.snr ?? '?'} dB</strong><br />
                      Band: <strong>{spot.band}</strong><br />
                      Freq: <strong>{(spot.frequency/1000).toFixed(1)} kHz</strong><br />
                      {spot.grid && <>Grid: {spot.grid}<br /></>}
                      {spot.speed && <>Speed: {spot.speed} WPM<br /></>}
                      Time: {new Date(spot.timestamp).toLocaleTimeString()}
                    </div>
                  </Popup>
                </CircleMarker>
              </div>
            );
          })}
        </MapContainer>

        {/* Map controls overlay - positioned outside MapContainer to avoid event conflicts */}
        <div className="absolute top-2 right-2 z-[1000] flex flex-col gap-1">
          <button
            onClick={(e) => {
              e.stopPropagation();
              mapRef.current?.zoomIn();
            }}
            className="glass-button p-2"
            title="Zoom In"
          >
            <ZoomIn className="w-4 h-4" />
          </button>
          <button
            onClick={(e) => {
              e.stopPropagation();
              mapRef.current?.zoomOut();
            }}
            className="glass-button p-2"
            title="Zoom Out"
          >
            <ZoomOut className="w-4 h-4" />
          </button>
          <div className="relative">
            <button
              onClick={(e) => {
                e.stopPropagation();
                setShowLayerPicker(!showLayerPicker);
              }}
              className="glass-button p-2"
              title="Map Layers"
            >
              <Layers className="w-4 h-4" />
            </button>
            {showLayerPicker && (
              <div className="absolute right-full mr-2 top-0 glass-panel p-2 min-w-[180px]">
                <div className="text-xs text-gray-400 mb-2 px-2">Base Layers</div>
                {Object.entries(TILE_LAYERS).map(([key, layer]) => (
                  <button
                    key={key}
                    onClick={(e) => {
                      e.stopPropagation();
                      updateMapSettings({ tileLayer: key as TileLayerKey });
                      saveSettings();
                    }}
                    className={`w-full text-left px-2 py-1 text-sm rounded hover:bg-dark-600 ${
                      tileLayer === key ? 'text-accent-primary' : 'text-gray-300'
                    }`}
                  >
                    {layer.name}
                  </button>
                ))}
                <div className="border-t border-gray-600 my-2"></div>
                <div className="text-xs text-gray-400 mb-2 px-2">Overlays</div>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    updateMapSettings({
                      rbn: { ...rbnSettings, enabled: !rbnSettings.enabled }
                    });
                    saveSettings();
                  }}
                  className="w-full text-left px-2 py-1 text-sm rounded hover:bg-dark-600 flex items-center justify-between"
                >
                  <span className={rbnSettings.enabled ? 'text-accent-primary' : 'text-gray-300'}>
                    RBN Layer
                  </span>
                  {rbnSettings.enabled && <Radio className="w-3 h-3" />}
                </button>
                {rbnSettings.enabled && (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      setShowRbnPanel(!showRbnPanel);
                      setShowLayerPicker(false);
                    }}
                    className="w-full text-left px-2 py-1 text-xs text-gray-400 hover:text-gray-300"
                  >
                    ⚙️ RBN Settings
                  </button>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Station info overlay */}
        {stationGrid && (
          <div className="absolute bottom-4 left-4 glass-panel px-3 py-2 z-[1000]">
            <div className="flex items-center gap-2 text-accent-primary">
              <MapPin className="w-4 h-4" />
              <span className="font-mono text-sm">{stationGrid}</span>
            </div>
          </div>
        )}

        {/* Target info overlay */}
        {focusedCallsignInfo && (
          <div className="absolute top-4 left-4 glass-panel px-3 py-2 z-[1000]">
            <div className="flex items-center gap-2">
              <Target className="w-4 h-4 text-accent-warning" />
              <div>
                <p className="font-mono font-bold text-accent-primary">
                  {focusedCallsignInfo.callsign}
                </p>
                {focusedCallsignInfo.grid && (
                  <p className="text-xs text-gray-400">{focusedCallsignInfo.grid}</p>
                )}
                {focusedCallsignInfo.bearing != null && (
                  <p className="text-xs text-accent-info">
                    {focusedCallsignInfo.bearing.toFixed(0)}°
                    {focusedCallsignInfo.distance != null && ` / ${Math.round(focusedCallsignInfo.distance)} km`}
                  </p>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Instructions overlay */}
        <div className="absolute bottom-4 right-4 glass-panel px-3 py-2 z-[1000] text-xs text-gray-400">
          {rotatorEnabled ? 'Click on map to set bearing' : 'Rotator disabled'}
        </div>

        {/* RBN Settings Panel */}
        {showRbnPanel && (
          <div className="absolute top-20 right-4 glass-panel p-4 z-[1001] min-w-[300px]">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-bold text-accent-primary flex items-center gap-2">
                <Radio className="w-4 h-4" />
                RBN Settings
              </h3>
              <button
                onClick={() => setShowRbnPanel(false)}
                className="text-gray-400 hover:text-white"
              >
                ✕
              </button>
            </div>

            <div className="space-y-3 text-sm">
              {/* Spots count */}
              <div className="text-xs text-gray-400">
                Showing {rbnSpots.length} spot{rbnSpots.length !== 1 ? 's' : ''} for {settings.station.callsign || 'your callsign'}
              </div>

              {/* Opacity */}
              <div>
                <label className="block mb-1 text-gray-300">
                  Opacity: {Math.round(rbnSettings.opacity * 100)}%
                </label>
                <input
                  type="range"
                  min="0"
                  max="1"
                  step="0.1"
                  value={rbnSettings.opacity}
                  onChange={(e) => {
                    updateMapSettings({
                      rbn: { ...rbnSettings, opacity: parseFloat(e.target.value) }
                    });
                  }}
                  onMouseUp={() => saveSettings()}
                  className="w-full"
                />
              </div>

              {/* Show Paths */}
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={rbnSettings.showPaths}
                  onChange={(e) => {
                    updateMapSettings({
                      rbn: { ...rbnSettings, showPaths: e.target.checked }
                    });
                    saveSettings();
                  }}
                  className="rounded"
                />
                <span className="text-gray-300">Show signal paths</span>
              </label>

              {/* Time Window */}
              <div>
                <label className="block mb-1 text-gray-300">
                  Time Window: {rbnSettings.timeWindowMinutes} min
                </label>
                <input
                  type="range"
                  min="1"
                  max="15"
                  step="1"
                  value={rbnSettings.timeWindowMinutes}
                  onChange={(e) => {
                    updateMapSettings({
                      rbn: { ...rbnSettings, timeWindowMinutes: parseInt(e.target.value) }
                    });
                  }}
                  onMouseUp={() => saveSettings()}
                  className="w-full"
                />
              </div>

              {/* Min SNR */}
              <div>
                <label className="block mb-1 text-gray-300">
                  Min SNR: {rbnSettings.minSnr} dB
                </label>
                <input
                  type="range"
                  min="-30"
                  max="30"
                  step="5"
                  value={rbnSettings.minSnr}
                  onChange={(e) => {
                    updateMapSettings({
                      rbn: { ...rbnSettings, minSnr: parseInt(e.target.value) }
                    });
                  }}
                  onMouseUp={() => saveSettings()}
                  className="w-full"
                />
              </div>

              <div className="pt-2 border-t border-gray-600 text-xs text-gray-500">
                📡 Data from reversebeacon.net
              </div>
            </div>
          </div>
        )}
      </div>
    </GlassPanel>
  );
}
