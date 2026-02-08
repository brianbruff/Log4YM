import { useEffect, useRef, useCallback, useState } from 'react';
import { Map as MapIcon, MapPin, Target, Maximize2, ZoomIn, ZoomOut, Layers } from 'lucide-react';
import { MapContainer, TileLayer, Marker, Popup, useMapEvents, Circle, Polyline } from 'react-leaflet';
import L from 'leaflet';
import { useQuery } from '@tanstack/react-query';
import { useAppStore } from '../store/appStore';
import { useSettingsStore } from '../store/settingsStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import { DXNewsTicker } from '../components/DXNewsTicker';
import { gridToLatLon, calculateDistance, getAnimationDuration } from '../utils/maidenhead';
import { api, Spot } from '../api/client';

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
      background: linear-gradient(135deg, #00ddff, #00bbdd);
      border: 3px solid #fff;
      border-radius: 50%;
      box-shadow: 0 0 10px rgba(0, 221, 255, 0.6);
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
      background: linear-gradient(135deg, #ffb432, #ff4466);
      border: 2px solid #fff;
      border-radius: 50%;
      box-shadow: 0 0 8px rgba(255, 180, 50, 0.6);
    "></div>
  `,
  iconSize: [20, 20],
  iconAnchor: [10, 10],
});

// Custom POTA marker (green triangle)
const potaIcon = new L.DivIcon({
  className: 'custom-pota-marker',
  html: `
    <div style="
      width: 0;
      height: 0;
      border-left: 8px solid transparent;
      border-right: 8px solid transparent;
      border-bottom: 14px solid #10b981;
      filter: drop-shadow(0 0 4px rgba(16, 185, 129, 0.6));
    "></div>
  `,
  iconSize: [16, 14],
  iconAnchor: [8, 14],
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

// Band colors for DX Cluster paths (inspired by OpenHamClock)
const BAND_COLORS: Record<string, string> = {
  '160m': '#ff4444', // Red
  '80m': '#ff8844', // Orange
  '60m': '#ffaa44', // Light Orange
  '40m': '#ffcc44', // Yellow
  '30m': '#ddff44', // Yellow-Green
  '20m': '#88ff44', // Green
  '17m': '#44ff88', // Light Green
  '15m': '#44ffcc', // Cyan
  '12m': '#44ccff', // Light Blue
  '10m': '#4488ff', // Blue
  '6m': '#8844ff', // Purple
};

// Get band from frequency (in kHz)
const getBandFromFrequency = (freq: number): string => {
  const BAND_RANGES: Record<string, [number, number]> = {
    '160m': [1800, 2000],
    '80m': [3500, 4000],
    '60m': [5330, 5410],
    '40m': [7000, 7300],
    '30m': [10100, 10150],
    '20m': [14000, 14350],
    '17m': [18068, 18168],
    '15m': [21000, 21450],
    '12m': [24890, 24990],
    '10m': [28000, 29700],
    '6m': [50000, 54000],
  };

  for (const [band, [min, max]] of Object.entries(BAND_RANGES)) {
    if (freq >= min && freq <= max) {
      return band;
    }
  }
  return '?';
};

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
  const { stationGrid, rotatorPosition, focusedCallsignInfo, potaSpots, showPotaMapMarkers, showDxPathsOnMap } = useAppStore();
  const { settings, updateMapSettings, saveSettings } = useSettingsStore();
  const { commandRotator } = useSignalR();
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [currentAzimuth, setCurrentAzimuth] = useState(0);
  const [showLayerPicker, setShowLayerPicker] = useState(false);
  // Fetch DX cluster spots for map overlay
  const { data: dxSpots } = useQuery({
    queryKey: ['spots'],
    queryFn: () => api.getSpots({ limit: 100 }),
    refetchInterval: 30000,
    enabled: showDxPathsOnMap,
  });

  // Get tile layer from persisted settings
  const tileLayer = settings.map.tileLayer;

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

  // Invalidate map size when container is resized (e.g., FlexLayout panel drag)
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const observer = new ResizeObserver(() => {
      requestAnimationFrame(() => {
        mapRef.current?.invalidateSize();
      });
    });

    observer.observe(container);
    return () => observer.disconnect();
  }, []);

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

  // Generate rotator beam visualization line (cyan)
  const beamLinePoints: [number, number][] = [];
  const beamDistance = 5000; // 5000km beam visualization
  for (let d = 0; d <= beamDistance; d += 100) {
    beamLinePoints.push(getDestinationPoint(stationLat, stationLon, currentAzimuth, d));
  }

  // Generate target heading line (amber) - separate from rotator beam
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

  return (
    <GlassPanel
      title="2D Map"
      icon={<MapIcon className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          {rotatorEnabled ? (
            <span className="text-sm font-mono text-accent-primary">{currentAzimuth}°</span>
          ) : (
            <span className="text-sm font-ui text-dark-300">No Rotator</span>
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
          style={{ background: '#0a0e14' }}
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
                <strong style={{ color: '#ffb432' }} className="font-mono">{stationGrid || 'Station'}</strong>
                <br />
                <span className="text-xs font-ui" style={{ color: '#5a7090' }}>Your Location</span>
              </div>
            </Popup>
          </Marker>

          {/* Station range circle */}
          <Circle
            center={[stationLat, stationLon]}
            radius={500000}
            pathOptions={{
              color: '#00ddff',
              fillColor: '#00ddff',
              fillOpacity: 0.05,
              weight: 1,
              dashArray: '5, 5',
            }}
          />

          {/* Rotator beam direction line - only show when rotator enabled (cyan) */}
          {rotatorEnabled && (
            <Polyline
              positions={beamLinePoints}
              pathOptions={{
                color: '#00ddff',
                weight: 3,
                opacity: 0.7,
                dashArray: '10, 5',
              }}
            />
          )}

          {/* Target heading line - show when we have a focused callsign with bearing (amber) */}
          {targetLinePoints.length > 0 && (
            <Polyline
              positions={targetLinePoints}
              pathOptions={{
                color: '#ffb432',
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
                  <strong style={{ color: '#ffb432' }} className="font-mono">{focusedCallsignInfo?.callsign}</strong>
                  <br />
                  {focusedCallsignInfo?.grid && (
                    <span className="text-xs font-mono" style={{ color: '#8899aa' }}>{focusedCallsignInfo.grid}</span>
                  )}
                  {focusedCallsignInfo?.bearing != null && (
                    <>
                      <br />
                      <span className="text-xs font-mono" style={{ color: '#00ddff' }}>
                        {focusedCallsignInfo.bearing.toFixed(0)}° / {Math.round(focusedCallsignInfo.distance ?? 0)} km
                      </span>
                    </>
                  )}
                </div>
              </Popup>
            </Marker>
          )}

          {/* POTA markers - show when enabled and we have spot data with coordinates */}
          {showPotaMapMarkers && potaSpots.map((spot) => {
            if (!spot.latitude || !spot.longitude) return null;
            return (
              <Marker
                key={spot.spotId}
                position={[spot.latitude, spot.longitude]}
                icon={potaIcon}
              >
                <Popup>
                  <div className="text-center">
                    <strong className="text-accent-success font-mono">{spot.activator}</strong>
                    <br />
                    <span className="text-xs font-mono text-accent-info">{spot.reference}</span>
                    <br />
                    <span className="text-xs text-gray-400">
                      {(parseFloat(spot.frequency) / 1000).toFixed(3)} MHz • {spot.mode}
                    </span>
                    {spot.locationDesc && (
                      <>
                        <br />
                        <span className="text-xs text-gray-500">{spot.locationDesc}</span>
                      </>
                    )}
                  </div>
                </Popup>
              </Marker>

          {/* DX Cluster spot paths - show when enabled */}
          {showDxPathsOnMap && dxSpots?.map((spot) => {
            // Only render if we have grid square to derive coordinates
            if (!spot.dxStation?.grid) return null;
            
            const dxCoords = gridToLatLon(spot.dxStation.grid);
            if (!dxCoords) return null;
            
            // Calculate great circle path from station to DX
            const pathPoints: [number, number][] = [];
            const numPoints = 50; // Number of points along the path
            
            // Calculate bearing from station to DX
            const bearing = calculateAzimuth(stationLat, stationLon, dxCoords.lat, dxCoords.lon);
            
            // Calculate distance
            const distance = calculateDistance(stationLat, stationLon, dxCoords.lat, dxCoords.lon);
            
            // Generate points along the great circle path
            for (let i = 0; i <= numPoints; i++) {
              const fraction = i / numPoints;
              const d = distance * fraction;
              pathPoints.push(getDestinationPoint(stationLat, stationLon, bearing, d));
            }
            
            // Get band and color
            const band = getBandFromFrequency(spot.frequency);
            const color = BAND_COLORS[band] || '#888888';
            
            return (
              <Polyline
                key={spot.id}
                positions={pathPoints}
                pathOptions={{
                  color: color,
                  weight: 2,
                  opacity: 0.6,
                }}
              />
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
              <div className="absolute right-full mr-2 top-0 glass-panel p-2 min-w-[120px]">
                {Object.entries(TILE_LAYERS).map(([key, layer]) => (
                  <button
                    key={key}
                    onClick={(e) => {
                      e.stopPropagation();
                      updateMapSettings({ tileLayer: key as TileLayerKey });
                      saveSettings();
                      setShowLayerPicker(false);
                    }}
                    className={`w-full text-left px-2 py-1 text-sm font-ui rounded hover:bg-dark-600 ${
                      tileLayer === key ? 'text-accent-primary' : 'text-dark-200'
                    }`}
                  >
                    {layer.name}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Station info overlay */}
        {stationGrid && (
          <div className="absolute bottom-12 left-4 glass-panel px-3 py-2 z-[1000]">
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
              <Target className="w-4 h-4 text-accent-primary" />
              <div>
                <p className="font-mono font-bold text-accent-primary">
                  {focusedCallsignInfo.callsign}
                </p>
                {focusedCallsignInfo.grid && (
                  <p className="text-xs font-mono text-dark-300">{focusedCallsignInfo.grid}</p>
                )}
                {focusedCallsignInfo.bearing != null && (
                  <p className="text-xs font-mono text-accent-secondary">
                    {focusedCallsignInfo.bearing.toFixed(0)}°
                    {focusedCallsignInfo.distance != null && ` / ${Math.round(focusedCallsignInfo.distance)} km`}
                  </p>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Instructions overlay */}
        <div className="absolute bottom-12 right-4 glass-panel px-3 py-2 z-[1000] text-xs font-ui text-dark-300">
          {rotatorEnabled ? 'Click on map to set bearing' : 'Rotator disabled'}
        </div>

        {/* DX News Ticker */}

        {/* Band Color Legend - show when DX paths are enabled */}
        {showDxPathsOnMap && (
          <div className="absolute bottom-16 right-4 glass-panel px-3 py-2 z-[1000]">
            <div className="text-xs font-ui text-dark-200 mb-2 font-semibold">Band Colors</div>
            <div className="grid grid-cols-2 gap-x-3 gap-y-1">
              {Object.entries(BAND_COLORS).map(([band, color]) => (
                <div key={band} className="flex items-center gap-2">
                  <div
                    className="w-6 h-0.5"
                    style={{ backgroundColor: color }}
                  />
                  <span className="text-xs font-mono text-dark-300">{band}</span>
                </div>
              ))}
            </div>
          </div>
        )}
        <DXNewsTicker />
      </div>
    </GlassPanel>
  );
}
