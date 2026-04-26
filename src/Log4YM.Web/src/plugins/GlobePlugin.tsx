import { useEffect, useRef, useCallback, useState } from 'react';
import { Globe as GlobeIcon, Navigation, Target, Maximize2, Radio, MapPin } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSettingsStore } from '../store/settingsStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import { gridToLatLon, calculateDistance, getAnimationDuration } from '../utils/maidenhead';
import { RotatorControls } from './RotatorPlugin';
import type { CallsignLookedUpEvent } from '../api/signalr';
// Globe is dynamically imported to catch WebGL errors at load time

// Default station location (can be overridden by store)
const DEFAULT_LAT = 52.6667; // IO52RN - Limerick
const DEFAULT_LON = -8.6333;

// Thresholds for showing overlays based on container height
const TOP_OVERLAY_THRESHOLD = 450;
const BOTTOM_OVERLAY_THRESHOLD = 550;

// Spherical linear interpolation (SLERP) along a great circle between two lat/lon points.
// t=0 returns start, t=1 returns end.
function interpolateGreatCircle(
  lat1: number, lon1: number,
  lat2: number, lon2: number,
  t: number
): { lat: number; lng: number } {
  const toRad = Math.PI / 180;
  const toDeg = 180 / Math.PI;

  const lat1Rad = lat1 * toRad;
  const lon1Rad = lon1 * toRad;
  const lat2Rad = lat2 * toRad;
  const lon2Rad = lon2 * toRad;

  const x1 = Math.cos(lat1Rad) * Math.cos(lon1Rad);
  const y1 = Math.cos(lat1Rad) * Math.sin(lon1Rad);
  const z1 = Math.sin(lat1Rad);

  const x2 = Math.cos(lat2Rad) * Math.cos(lon2Rad);
  const y2 = Math.cos(lat2Rad) * Math.sin(lon2Rad);
  const z2 = Math.sin(lat2Rad);

  const dot = x1 * x2 + y1 * y2 + z1 * z2;
  const omega = Math.acos(Math.max(-1, Math.min(1, dot)));

  if (Math.abs(omega) < 1e-10) {
    return { lat: lat1, lng: lon1 };
  }

  const sinOmega = Math.sin(omega);
  const a = Math.sin((1 - t) * omega) / sinOmega;
  const b = Math.sin(t * omega) / sinOmega;

  const x = a * x1 + b * x2;
  const y = a * y1 + b * y2;
  const z = a * z1 + b * z2;

  return {
    lat: Math.atan2(z, Math.sqrt(x * x + y * y)) * toDeg,
    lng: Math.atan2(y, x) * toDeg,
  };
}

// Marker data structure for globe points
interface GlobeMarkerData {
  lat: number;
  lng: number;
  label: string;
  color: string;
  size: number;
  type: 'station' | 'target';
}

interface GlobeInstance {
  (element: HTMLElement): GlobeInstance;
  globeImageUrl(url: string): GlobeInstance;
  globeTileEngineUrl(fn: (x: number, y: number, l: number) => string): GlobeInstance;
  bumpImageUrl(url: string): GlobeInstance;
  backgroundImageUrl(url: string): GlobeInstance;
  showAtmosphere(show: boolean): GlobeInstance;
  atmosphereColor(color: string): GlobeInstance;
  atmosphereAltitude(alt: number): GlobeInstance;
  pointOfView(pov: { lat: number; lng: number; altitude: number }): GlobeInstance;
  pointOfView(): { lat: number; lng: number; altitude: number };
  enablePointerInteraction(enable: boolean): GlobeInstance;
  pointsData(data: unknown[]): GlobeInstance;
  pointLat(accessor: string): GlobeInstance;
  pointLng(accessor: string): GlobeInstance;
  pointColor(accessor: string | ((d: unknown) => string)): GlobeInstance;
  pointAltitude(alt: number | ((d: unknown) => number)): GlobeInstance;
  pointRadius(accessor: string | ((d: unknown) => number)): GlobeInstance;
  pointResolution(res: number): GlobeInstance;
  pointLabel(fn: (d: unknown) => string): GlobeInstance;
  pathsData(data: unknown[]): GlobeInstance;
  pathPoints(accessor: string): GlobeInstance;
  pathColor(accessor: string): GlobeInstance;
  pathStroke(accessor: string): GlobeInstance;
  pathDashLength(len: number): GlobeInstance;
  pathDashGap(gap: number): GlobeInstance;
  pathDashAnimateTime(time: number): GlobeInstance;
  pathTransitionDuration(duration: number): GlobeInstance;
  ringsData(data: unknown[]): GlobeInstance;
  onGlobeClick(fn: (coords: { lat: number; lng: number }) => void): GlobeInstance;
  onZoom(fn: (pov: { lat: number; lng: number; altitude: number }) => void): GlobeInstance;
  width(w: number): GlobeInstance;
  height(h: number): GlobeInstance;
  globeMaterial(): { opacity: number };
}

export function GlobeCore({ hideOverlays, hideCompass }: { hideOverlays?: boolean; hideCompass?: boolean }) {
  const containerRef = useRef<HTMLDivElement>(null);
  const globeRef = useRef<GlobeInstance | null>(null);
  const cameraAnimationRef = useRef<number | null>(null);
  const lastTargetCoordsRef = useRef<{ lat: number; lng: number } | null>(null);

  const { stationGrid, rotatorPosition, focusedCallsignInfo, radioStates, selectedRadioId } = useAppStore();
  const { settings } = useSettingsStore();
  const { commandRotator } = useSignalR();

  const [currentAzimuth, setCurrentAzimuth] = useState(0);
  const [webglError, setWebglError] = useState<string | null>(null);
  const [containerHeight, setContainerHeight] = useState(0);
  const [globeReady, setGlobeReady] = useState(false);

  // Get current radio state if connected
  const selectedRadioState = selectedRadioId ? radioStates.get(selectedRadioId) : null;
  const isRadioConnected = !!selectedRadioState;

  // Rotator is enabled in settings
  const rotatorEnabled = settings.rotator.enabled;

  // Get station coordinates from settings - prioritize lat/lon, fall back to grid square, then defaults
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

  // Calculate azimuth between two points
  const calculateAzimuth = useCallback((lat1: number, lon1: number, lat2: number, lon2: number) => {
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
  }, []);

  // Calculate destination point from start point, azimuth and distance
  const getDestinationPoint = useCallback((lat: number, lon: number, azimuth: number, distance: number = 10000) => {
    const R = 6371;
    const toRad = Math.PI / 180;
    const toDeg = 180 / Math.PI;

    const lat1Rad = lat * toRad;
    const lon1Rad = lon * toRad;
    const azimuthRad = azimuth * toRad;
    const angularDistance = distance / R;

    const lat2Rad = Math.asin(
      Math.sin(lat1Rad) * Math.cos(angularDistance) +
      Math.cos(lat1Rad) * Math.sin(angularDistance) * Math.cos(azimuthRad)
    );

    const lon2Rad = lon1Rad + Math.atan2(
      Math.sin(azimuthRad) * Math.sin(angularDistance) * Math.cos(lat1Rad),
      Math.cos(angularDistance) - Math.sin(lat1Rad) * Math.sin(lat2Rad)
    );

    return {
      lat: lat2Rad * toDeg,
      lng: ((lon2Rad * toDeg + 540) % 360) - 180
    };
  }, []);

  // Track target DX station coordinates for the DE→DX path line
  const targetCoordsRef = useRef<{ lat: number; lng: number } | null>(null);

  // Track focused callsign info for label callback
  const focusedCallsignInfoRef = useRef<CallsignLookedUpEvent | null>(null);
  focusedCallsignInfoRef.current = focusedCallsignInfo;

  // Render the beam visualization (rotator beam + DE→DX line)
  const renderBeam = useCallback((azimuth: number, isConnected: boolean) => {
    if (!globeRef.current) return;

    const pathsData: { path: [number, number, number][]; color: string; stroke: number }[] = [];
    const numSegments = 50;
    const maxDistance = 18000;

    // Render rotator beam only when connected
    if (isConnected) {
      // Create left and right edge paths (amber accent)
      for (const edge of ['left', 'right']) {
        const pathPoints: [number, number, number][] = [];

        for (let i = 0; i <= numSegments; i++) {
          const distance = (maxDistance / numSegments) * i;
          const beamWidth = 1 + (19 * (i / numSegments));

          const edgeAzimuth = edge === 'left'
            ? (azimuth - beamWidth / 2 + 360) % 360
            : (azimuth + beamWidth / 2) % 360;

          const point = getDestinationPoint(stationLat, stationLon, edgeAzimuth, distance);
          pathPoints.push([point.lat, point.lng, 0.01]);
        }

        pathsData.push({
          path: pathPoints,
          color: 'rgba(255, 180, 50, 0.6)', // Amber accent for edges
          stroke: 3
        });
      }

      // Rotator center line (amber accent)
      const centerPath: [number, number, number][] = [];
      for (let i = 0; i <= numSegments; i++) {
        const distance = (maxDistance / numSegments) * i;
        const point = getDestinationPoint(stationLat, stationLon, azimuth, distance);
        centerPath.push([point.lat, point.lng, 0.015]);
      }

      pathsData.push({
        path: centerPath,
        color: 'rgba(255, 180, 50, 0.7)', // Amber accent center line
        stroke: 2.5
      });
    }

    // Render direct great-circle path from DE station to DX target
    const targetCoords = targetCoordsRef.current;
    if (targetCoords !== null) {
      const targetPath: [number, number, number][] = [];
      for (let i = 0; i <= numSegments; i++) {
        const t = i / numSegments;
        const point = interpolateGreatCircle(stationLat, stationLon, targetCoords.lat, targetCoords.lng, t);
        targetPath.push([point.lat, point.lng, 0.02]);
      }

      pathsData.push({
        path: targetPath,
        color: 'rgba(255, 68, 102, 0.8)', // Danger red (#ff4466) for DE→DX path
        stroke: 2
      });
    }

    globeRef.current
      .pathsData(pathsData)
      .pathPoints('path')
      .pathColor('color')
      .pathStroke('stroke')
      .pathDashLength(0)
      .pathDashGap(0)
      .pathDashAnimateTime(0)
      .pathTransitionDuration(0)
      .ringsData([]);
  }, [stationLat, stationLon, getDestinationPoint]);

  // Track rotator enabled status in ref for click handler (avoids stale closure)
  const rotatorEnabledRef = useRef(rotatorEnabled);
  rotatorEnabledRef.current = rotatorEnabled;

  // Store callbacks in refs to avoid re-initializing globe
  const commandRotatorRef = useRef(commandRotator);
  commandRotatorRef.current = commandRotator;
  const calculateAzimuthRef = useRef(calculateAzimuth);
  calculateAzimuthRef.current = calculateAzimuth;

  // Track last command time and commanded azimuth to avoid rotator position overwriting clicked value
  const lastCommandTimeRef = useRef<number>(0);
  const commandedAzimuthRef = useRef<number | null>(null);
  const displayedAzimuthRef = useRef<number>(0);

  // Initialize globe - only runs once on mount
  useEffect(() => {
    if (!containerRef.current) return;

    // Quick WebGL availability check — only tests for context existence.
    // The shader-compilation pre-check that was here produced false negatives on
    // Windows (ANGLE backend) and Linux (Mesa) even when WebGL was fully functional,
    // preventing the globe from loading on non-macOS platforms.
    // globe.gl's own Three.js initialization handles deeper WebGL failures via the
    // try/catch below.
    try {
      const testCanvas = document.createElement('canvas');
      const gl = testCanvas.getContext('webgl2') ?? testCanvas.getContext('webgl');
      if (!gl) {
        setWebglError('WebGL is not supported in your browser.');
        return;
      }
    } catch (e) {
      setWebglError('WebGL is not available in this environment.');
      return;
    }

    // Variables for cleanup
    let resizeObserver: ResizeObserver | null = null;
    let resizeTimeout: ReturnType<typeof setTimeout> | null = null;
    let debouncedResizeFn: (() => void) | null = null;
    let isCancelled = false;

    // Dynamically import globe.gl to catch WebGL errors at load time
    const initGlobe = async () => {
      // Wait for container to have valid dimensions
      const waitForDimensions = async (): Promise<boolean> => {
        for (let i = 0; i < 20; i++) { // Try for up to 2 seconds
          if (!containerRef.current || isCancelled) return false;
          const rect = containerRef.current.getBoundingClientRect();
          if (rect.width > 0 && rect.height > 0) {
            return true;
          }
          await new Promise(resolve => setTimeout(resolve, 100));
        }
        return false;
      };

      const hasDimensions = await waitForDimensions();
      if (!hasDimensions) {
        if (!isCancelled) {
          setWebglError('Globe container failed to initialize. Please try resizing the window.');
        }
        return;
      }

      let Globe;
      try {
        await import('three');
        const module = await import('globe.gl');
        Globe = module.default;
      } catch (e) {
        if (!isCancelled) {
          setWebglError('Failed to load globe.gl');
        }
        return;
      }

      if (isCancelled || !containerRef.current) return;

      let globe: GlobeInstance | null = null;

      try {
        // @ts-expect-error - globe.gl typings issue with dynamic import
        globe = Globe() as GlobeInstance;
      } catch (e) {
        if (!isCancelled) {
          setWebglError('Failed to initialize 3D Globe.');
        }
        return;
      }

      if (isCancelled || !containerRef.current) return;

      try {
        globe(containerRef.current)
        // Use Google satellite tiles with labels - shows countries, cities as you zoom
        .globeTileEngineUrl((x, y, l) => `https://mt1.google.com/vt/lyrs=y&x=${x}&y=${y}&z=${l}`)
        .globeImageUrl('//unpkg.com/three-globe/example/img/earth-blue-marble.jpg')
        .bumpImageUrl('//unpkg.com/three-globe/example/img/earth-topology.png')
        .backgroundImageUrl('//unpkg.com/three-globe/example/img/night-sky.png')
        .showAtmosphere(true)
        .atmosphereColor('rgba(10, 14, 20, 0.4)')
        .atmosphereAltitude(0.25)
        .pointOfView({ lat: stationLat, lng: stationLon, altitude: 1.7 })
        .enablePointerInteraction(true);

      // Configure marker appearance (data will be set by effect)
      globe
        .pointsData([]) // Initial empty - will be populated by effect
        .pointLat('lat')
        .pointLng('lng')
        .pointColor('color')
        .pointAltitude((d: unknown) => {
          const data = d as GlobeMarkerData;
          return data.type === 'target' ? 0.015 : 0.01;
        })
        .pointRadius('size')
        .pointResolution(12)
        .pointLabel((d: unknown) => {
          const data = d as GlobeMarkerData;
          if (data.type === 'station') {
            return `<div style="text-align: center; padding: 5px; background: rgba(10, 14, 20, 0.9); border-radius: 3px; color: #8899aa; border: 1px solid rgba(255, 180, 50, 0.12);">
              <div style="font-weight: bold; color: #ffb432;">${data.label}</div>
              <div style="font-size: 0.8em;">Station Location</div>
            </div>`;
          } else {
            // Target marker - use ref to access latest focusedCallsignInfo
            const info = focusedCallsignInfoRef.current;
            return `<div style="text-align: center; padding: 5px; background: rgba(10, 14, 20, 0.9); border-radius: 3px; color: #8899aa; border: 1px solid rgba(255, 180, 50, 0.12);">
              <div style="font-weight: bold; color: #ff4466;">${data.label}</div>
              ${info?.grid ? `<div style="font-size: 0.8em; color: #8899aa;">${info.grid}</div>` : ''}
              ${info?.bearing != null ? `<div style="font-size: 0.8em; color: #00ddff;">
                ${info.bearing.toFixed(0)}° / ${Math.round(info.distance ?? 0)} km
              </div>` : ''}
            </div>`;
          }
        });

      // Handle globe clicks to set azimuth - use refs to avoid stale closures
      globe.onGlobeClick((coords) => {
        if (!rotatorEnabledRef.current) return; // Ignore clicks when rotator disabled
        if (coords && coords.lat !== undefined && coords.lng !== undefined) {
          const azimuth = calculateAzimuthRef.current(stationLat, stationLon, coords.lat, coords.lng);
          lastCommandTimeRef.current = Date.now();
          commandedAzimuthRef.current = azimuth;
          displayedAzimuthRef.current = azimuth;
          setCurrentAzimuth(azimuth);
          commandRotatorRef.current(azimuth, 'globe');
        }
      });

      // Set material opacity
      const material = globe.globeMaterial();
      material.opacity = 0.95;

      globeRef.current = globe;

      // Handle resize
      let lastWidth = 0;
      let lastHeight = 0;
      let isResizing = false;

      const handleResize = () => {
        if (isResizing || !containerRef.current || !globeRef.current) return;

        const rect = containerRef.current.getBoundingClientRect();
        const width = Math.floor(rect.width);
        const height = Math.floor(rect.height);

        // Update container height state for conditional overlays
        setContainerHeight(height);

        const widthDiff = Math.abs(width - lastWidth);
        const heightDiff = Math.abs(height - lastHeight);

        if (width > 0 && height > 0 && (widthDiff > 5 || heightDiff > 5)) {
          isResizing = true;
          lastWidth = width;
          lastHeight = height;
          globeRef.current.width(width).height(height);
          setTimeout(() => { isResizing = false; }, 100);
        }
      };

      const debouncedResize = () => {
        if (resizeTimeout) {
          clearTimeout(resizeTimeout);
        }
        resizeTimeout = setTimeout(handleResize, 300);
      };

      debouncedResizeFn = debouncedResize;
      resizeObserver = new ResizeObserver(debouncedResize);
      resizeObserver.observe(containerRef.current);
      window.addEventListener('resize', debouncedResize);
      setTimeout(handleResize, 100);
      setGlobeReady(true);

      } catch (e) {
        if (!isCancelled) {
          setWebglError('Failed to initialize 3D Globe.');
        }
      }
    };

    // Start async initialization
    initGlobe();

    // Cleanup function
    return () => {
      isCancelled = true;
      setGlobeReady(false);
      globeRef.current = null;
      if (resizeObserver) {
        resizeObserver.disconnect();
      }
      if (debouncedResizeFn) {
        window.removeEventListener('resize', debouncedResizeFn);
      }
      if (resizeTimeout) {
        clearTimeout(resizeTimeout);
      }
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stationLat, stationLon, stationGrid]);

  // Render the beam whenever its inputs change. Replaces the old per-frame rAF
  // loop that drove the GPU helper to >100% CPU even while idle.
  useEffect(() => {
    if (!globeReady) return;
    renderBeam(currentAzimuth, rotatorEnabled);
  }, [
    globeReady,
    currentAzimuth,
    rotatorEnabled,
    focusedCallsignInfo?.latitude,
    focusedCallsignInfo?.longitude,
    renderBeam,
  ]);

  // Update beam when rotator position changes
  useEffect(() => {
    if (rotatorPosition?.currentAzimuth != null && typeof rotatorPosition.currentAzimuth === 'number') {
      const newPosition = rotatorPosition.currentAzimuth;
      const currentDisplay = displayedAzimuthRef.current;
      const isNearZero = currentDisplay <= 30 || currentDisplay >= 330;
      const isSuspiciousZero = newPosition === 0 && !isNearZero;
      if (isSuspiciousZero) return;
      const timeSinceCommand = Date.now() - lastCommandTimeRef.current;
      const commanded = commandedAzimuthRef.current;
      if (timeSinceCommand < 1000 && commanded !== null) {
        const diff = Math.abs(newPosition - commanded);
        const wrappedDiff = Math.min(diff, 360 - diff);
        if (wrappedDiff <= 15) {
          displayedAzimuthRef.current = newPosition;
          setCurrentAzimuth(newPosition);
        }
      } else {
        commandedAzimuthRef.current = null;
        displayedAzimuthRef.current = newPosition;
        setCurrentAzimuth(newPosition);
      }
    }
  }, [rotatorPosition]);

  // Update target DX coordinates when focused callsign changes
  useEffect(() => {
    if (focusedCallsignInfo?.latitude != null && focusedCallsignInfo?.longitude != null) {
      targetCoordsRef.current = { lat: focusedCallsignInfo.latitude, lng: focusedCallsignInfo.longitude };
    } else {
      targetCoordsRef.current = null;
    }
  }, [focusedCallsignInfo]);

  // Update globe markers when focused callsign changes
  useEffect(() => {
    if (!globeRef.current) return;

    const markerData: GlobeMarkerData[] = [{
      lat: stationLat,
      lng: stationLon,
      label: stationGrid || 'Station',
      color: '#ffb432',
      size: 0.05,
      type: 'station',
    }];

    if (focusedCallsignInfo?.latitude != null && focusedCallsignInfo?.longitude != null) {
      markerData.push({
        lat: focusedCallsignInfo.latitude,
        lng: focusedCallsignInfo.longitude,
        label: focusedCallsignInfo.callsign,
        color: '#ff4466',
        size: 0.04,
        type: 'target',
      });
    }

    globeRef.current.pointsData(markerData);
  }, [focusedCallsignInfo, stationLat, stationLon, stationGrid]);

  // Fly to target when focused callsign changes
  useEffect(() => {
    if (!globeRef.current) return;

    const targetLat = focusedCallsignInfo?.latitude;
    const targetLon = focusedCallsignInfo?.longitude;
    if (targetLat == null || targetLon == null) return;

    if (cameraAnimationRef.current) {
      cancelAnimationFrame(cameraAnimationRef.current);
      cameraAnimationRef.current = null;
    }

    let duration = 2;
    const lastCoords = lastTargetCoordsRef.current;
    if (lastCoords) {
      const distance = calculateDistance(lastCoords.lat, lastCoords.lng, targetLat, targetLon);
      duration = getAnimationDuration(distance);
    } else {
      const distance = calculateDistance(stationLat, stationLon, targetLat, targetLon);
      duration = getAnimationDuration(distance);
    }

    lastTargetCoordsRef.current = { lat: targetLat, lng: targetLon };

    const startPov = globeRef.current.pointOfView();
    const targetPov = { lat: targetLat, lng: targetLon, altitude: 1.7 };

    const startTime = performance.now();
    const durationMs = duration * 1000;

    const easeInOutCubic = (t: number): number => {
      return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
    };

    let startLng = startPov.lng;
    let endLng = targetPov.lng;
    const lngDiff = endLng - startLng;
    if (lngDiff > 180) startLng += 360;
    else if (lngDiff < -180) endLng += 360;

    const animateCamera = (currentTime: number) => {
      const elapsed = currentTime - startTime;
      const progress = Math.min(elapsed / durationMs, 1);
      const easedProgress = easeInOutCubic(progress);

      const currentLat = startPov.lat + (targetPov.lat - startPov.lat) * easedProgress;
      let currentLng = startLng + (endLng - startLng) * easedProgress;
      currentLng = ((currentLng + 540) % 360) - 180;
      const currentAlt = startPov.altitude + (targetPov.altitude - startPov.altitude) * easedProgress;

      if (globeRef.current) {
        globeRef.current.pointOfView({ lat: currentLat, lng: currentLng, altitude: currentAlt });
      }

      if (progress < 1) {
        cameraAnimationRef.current = requestAnimationFrame(animateCamera);
      } else {
        cameraAnimationRef.current = null;
      }
    };

    cameraAnimationRef.current = requestAnimationFrame(animateCamera);

    return () => {
      if (cameraAnimationRef.current) {
        cancelAnimationFrame(cameraAnimationRef.current);
        cameraAnimationRef.current = null;
      }
    };
  }, [focusedCallsignInfo?.latitude, focusedCallsignInfo?.longitude, stationLat, stationLon]);

  const formatFrequency = (hz: number): string => {
    const mhz = hz / 1_000_000;
    return mhz.toFixed(3);
  };

  const showTopOverlay = containerHeight >= TOP_OVERLAY_THRESHOLD;
  const showBottomOverlay = containerHeight >= BOTTOM_OVERLAY_THRESHOLD;

  return (
      <div className="relative w-full h-full min-h-[400px]">
        {/* WebGL Error Message */}
        {webglError && (
          <div className="absolute inset-0 flex items-center justify-center bg-dark-800/95 backdrop-blur-sm z-50">
            <div className="glass-panel p-6 max-w-md text-center">
              <GlobeIcon className="w-12 h-12 mx-auto mb-4 text-dark-300" />
              <h3 className="text-lg font-semibold mb-2 font-ui text-dark-200">WebGL Not Available</h3>
              <p className="text-sm text-dark-300 mb-4">{webglError}</p>
            </div>
          </div>
        )}

        {/* Globe container */}
        <div
          ref={containerRef}
          className="w-full h-full"
          style={{ cursor: rotatorEnabled ? 'crosshair' : 'default' }}
        />

        {/* Station and Rig Info Overlay (Top Left) */}
        {!hideOverlays && showTopOverlay && (
          <div className="absolute top-4 left-4 flex flex-col gap-2 pointer-events-none">
            <div className="glass-panel px-3 py-2 border-l-4 border-accent-primary">
              <div className="flex items-center gap-2 mb-1">
                <div className="p-1 bg-accent-primary/20 rounded">
                  <GlobeIcon className="w-3.5 h-3.5 text-accent-primary" />
                </div>
                <span className="font-display font-bold text-dark-100 tracking-wider">
                  {settings.station.callsign || 'STATION'}
                </span>
                <span className="text-[10px] font-mono text-dark-300 bg-dark-700 px-1.5 py-0.5 rounded">
                  {settings.station.gridSquare || stationGrid || '----'}
                </span>
              </div>
              
              {isRadioConnected && selectedRadioState && (
                <div className="flex flex-col gap-0.5 mt-1 pt-1 border-t border-glass-100">
                  <div className="flex items-center gap-1.5 text-xs">
                    <Radio className="w-3 h-3 text-accent-success" />
                    <span className="font-mono font-bold text-accent-success">
                      {formatFrequency(selectedRadioState.frequencyHz)}
                    </span>
                    <span className="text-[10px] text-dark-300 font-ui">MHz</span>
                    <span className="text-[10px] font-bold text-accent-secondary ml-auto bg-accent-secondary/10 px-1 rounded">
                      {selectedRadioState.mode}
                    </span>
                  </div>
                  <div className="text-[10px] text-dark-400 font-ui truncate max-w-[150px]">
                    {selectedRadioId?.startsWith('tci-') ? 'TCI' : 'Rig'}: {selectedRadioState.band}
                  </div>
                </div>
              )}
            </div>

            {/* Target DX Info (if active) */}
            {focusedCallsignInfo && (
              <div className="glass-panel px-3 py-2 border-l-4 border-accent-danger animate-fade-in pointer-events-auto">
                <div className="flex items-center gap-2">
                  <Target className="w-4 h-4 text-accent-danger" />
                  <div>
                    <p className="font-mono font-bold text-accent-danger">
                      {focusedCallsignInfo.callsign}
                    </p>
                    {focusedCallsignInfo.grid && (
                      <p className="text-[10px] font-mono text-dark-300">{focusedCallsignInfo.grid}</p>
                    )}
                    {focusedCallsignInfo.bearing != null && (
                      <p className="text-[10px] font-mono text-accent-info">
                        {focusedCallsignInfo.bearing.toFixed(0)}°
                        {focusedCallsignInfo.distance != null && ` / ${Math.round(focusedCallsignInfo.distance)}km`}
                      </p>
                    )}
                  </div>
                  {rotatorEnabled && focusedCallsignInfo.bearing != null && (
                    <button
                      onClick={() => {
                        const bearing = focusedCallsignInfo.bearing!;
                        lastCommandTimeRef.current = Date.now();
                        commandedAzimuthRef.current = bearing;
                        displayedAzimuthRef.current = bearing;
                        setCurrentAzimuth(bearing);
                        commandRotator(bearing, 'globe');
                      }}
                      className="glass-button-success px-2 py-1 flex items-center gap-1 text-[10px] ml-2"
                      title={`Rotate to ${focusedCallsignInfo.callsign}`}
                    >
                      <Navigation className="w-2.5 h-2.5" />
                      <span className="font-mono">{focusedCallsignInfo.bearing.toFixed(0)}°</span>
                    </button>
                  )}
                </div>
              </div>
            )}
          </div>
        )}

        {/* Azimuth and Coordinates Overlay (Bottom Center) */}
        {!hideOverlays && showBottomOverlay && (
          <div className="absolute bottom-6 left-1/2 -translate-x-1/2 flex flex-col items-center gap-1 pointer-events-none">
            {rotatorEnabled && (
              <div className="text-center">
                <div className="text-5xl font-display font-bold text-accent-primary drop-shadow-glow leading-none">
                  {currentAzimuth}°
                </div>
                <div className="text-[10px] font-ui font-bold uppercase tracking-[0.2em] text-accent-primary/60 mt-1">
                  Beam Heading
                </div>
              </div>
            )}
            
            <div className="mt-2 flex items-center gap-3 px-3 py-1 bg-dark-900/40 backdrop-blur-sm rounded-full border border-glass-100 text-[10px] font-mono text-dark-300">
              <div className="flex items-center gap-1">
                <MapPin className="w-2.5 h-2.5" />
                <span>{stationLat.toFixed(4)}°N</span>
              </div>
              <div className="w-px h-2 bg-glass-200" />
              <div>{Math.abs(stationLon).toFixed(4)}°{stationLon >= 0 ? 'E' : 'W'}</div>
            </div>
          </div>
        )}

        {/* Instructions (Bottom Left) - Hide if height too small */}
        {!hideOverlays && containerHeight > 400 && (
          <div className="absolute bottom-4 left-4 glass-panel px-3 py-2 text-[10px] pointer-events-none opacity-60">
            <div className="flex items-center gap-2 font-ui text-dark-300">
              <Navigation className="w-3 h-3" />
              <span>{rotatorEnabled ? 'Click globe to set beam' : 'Rotator disabled'}</span>
            </div>
          </div>
        )}

        {/* Compass Overlay (Top Right) */}
        {!hideCompass && rotatorEnabled && (
          <div className="absolute top-4 right-4 w-28 h-28 bg-dark-800/80 backdrop-blur-sm rounded-full border border-glass-100 flex items-center justify-center pointer-events-none">
            <span className="absolute top-1 text-[10px] font-bold font-ui text-accent-danger">N</span>
            <span className="absolute bottom-1 text-[10px] font-bold font-ui text-dark-300">S</span>
            <span className="absolute left-1 text-[10px] font-bold font-ui text-dark-300">W</span>
            <span className="absolute right-1 text-[10px] font-bold font-ui text-dark-300">E</span>

            {/* Compass needle */}
            <div
              className="absolute w-0.5 h-10 origin-bottom transition-transform duration-300"
              style={{
                transform: `rotate(${currentAzimuth}deg)`,
                background: 'linear-gradient(to top, transparent, #ffb432)',
                top: 'calc(50% - 40px)',
              }}
            />

            {/* Center hub */}
            <div className="w-1.5 h-1.5 bg-accent-primary rounded-full shadow-glow" />
          </div>
        )}
      </div>
  );
}

export function GlobePlugin() {
  const { settings } = useSettingsStore();
  const [isFullscreen, setIsFullscreen] = useState(false);
  
  const { rotatorPosition } = useAppStore();
  const currentAzimuth = rotatorPosition?.currentAzimuth ?? 0;

  const toggleFullscreen = useCallback(() => {
    const el = document.getElementById('globe-plugin-container');
    if (!el) return;
    if (!isFullscreen) el.requestFullscreen?.();
    else document.exitFullscreen?.();
    setIsFullscreen(!isFullscreen);
  }, [isFullscreen]);

  return (
    <GlassPanel
      title="3D Globe"
      icon={<GlobeIcon className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-4">
          {settings.rotator.enabled && <RotatorControls />}
          <div className="flex items-center gap-2">
            {settings.rotator.enabled ? (
              <span className="text-sm font-mono font-bold text-accent-primary min-w-[3ch]">{currentAzimuth}°</span>
            ) : (
              <span className="text-xs font-ui text-dark-400">No Rotator</span>
            )}
            <button
              onClick={toggleFullscreen}
              className="glass-button p-1.5"
              title="Fullscreen"
            >
              <Maximize2 className="w-4 h-4" />
            </button>
          </div>
        </div>
      }
    >
      <div id="globe-plugin-container" className="w-full h-full">
         <GlobeCore />
      </div>
    </GlassPanel>
  );
}
