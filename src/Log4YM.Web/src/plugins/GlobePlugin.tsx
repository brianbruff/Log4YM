import { useEffect, useRef, useCallback, useState } from 'react';
import { Globe as GlobeIcon, Navigation, Target, Maximize2 } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import Globe from 'globe.gl';

// Default station location (can be overridden by store)
const DEFAULT_LAT = 52.6667; // IO52RN - Limerick
const DEFAULT_LON = -8.6333;

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

export function GlobePlugin() {
  const containerRef = useRef<HTMLDivElement>(null);
  const globeRef = useRef<GlobeInstance | null>(null);
  const animationRef = useRef<number | null>(null);

  const { stationGrid, rotatorPosition, focusedCallsignInfo } = useAppStore();
  const { commandRotator } = useSignalR();

  const [currentAzimuth, setCurrentAzimuth] = useState(0);
  const [isFullscreen, setIsFullscreen] = useState(false);

  // Get station coordinates from grid or use defaults
  const stationLat = DEFAULT_LAT;
  const stationLon = DEFAULT_LON;

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

  // Render the beam visualization
  const renderBeam = useCallback((azimuth: number) => {
    if (!globeRef.current) return;

    const pathsData: { path: [number, number, number][]; color: string; stroke: number }[] = [];
    const numSegments = 50;
    const maxDistance = 18000;

    // Subtle pulse effect
    const pulseTime = Date.now() / 3000;
    const pulseFactor = 0.6 + Math.sin(pulseTime * Math.PI * 2) * 0.1;

    // Create left and right edge paths
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
        color: `rgba(251, 191, 36, ${pulseFactor})`, // Yellow/amber for edges
        stroke: 3
      });
    }

    // Center line
    const centerPath: [number, number, number][] = [];
    for (let i = 0; i <= numSegments; i++) {
      const distance = (maxDistance / numSegments) * i;
      const point = getDestinationPoint(stationLat, stationLon, azimuth, distance);
      centerPath.push([point.lat, point.lng, 0.015]);
    }

    pathsData.push({
      path: centerPath,
      color: 'rgba(251, 191, 36, 0.7)', // Yellow/amber center line
      stroke: 2.5
    });

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

  // Animation loop - use ref to avoid dependency on currentAzimuth
  const currentAzimuthRef = useRef(currentAzimuth);
  currentAzimuthRef.current = currentAzimuth;

  // Store callbacks in refs to avoid re-initializing globe
  const commandRotatorRef = useRef(commandRotator);
  commandRotatorRef.current = commandRotator;
  const calculateAzimuthRef = useRef(calculateAzimuth);
  calculateAzimuthRef.current = calculateAzimuth;

  // Track last command time and commanded azimuth to avoid rotator position overwriting clicked value
  const lastCommandTimeRef = useRef<number>(0);
  const commandedAzimuthRef = useRef<number | null>(null);

  const animateBeam = useCallback(() => {
    renderBeam(currentAzimuthRef.current);
    animationRef.current = requestAnimationFrame(animateBeam);
  }, [renderBeam]);

  // Initialize globe - only runs once on mount
  useEffect(() => {
    if (!containerRef.current) return;

    // @ts-expect-error - globe.gl typings issue
    const globe = Globe() as GlobeInstance;

    globe(containerRef.current)
      // Use Google satellite tiles with labels - shows countries, cities as you zoom
      .globeTileEngineUrl((x, y, l) => `https://mt1.google.com/vt/lyrs=y&x=${x}&y=${y}&z=${l}`)
      .globeImageUrl('//unpkg.com/three-globe/example/img/earth-blue-marble.jpg')
      .bumpImageUrl('//unpkg.com/three-globe/example/img/earth-topology.png')
      .backgroundImageUrl('//unpkg.com/three-globe/example/img/night-sky.png')
      .showAtmosphere(true)
      .atmosphereColor('rgba(99, 102, 241, 0.4)')
      .atmosphereAltitude(0.25)
      .pointOfView({ lat: stationLat, lng: stationLon, altitude: 2.5 })
      .enablePointerInteraction(true);

    // Add station marker
    const markerData = [{
      lat: stationLat,
      lng: stationLon,
      label: stationGrid || 'Station',
      color: '#fbbf24', // Yellow to match beam color
      size: 0.05
    }];

    globe
      .pointsData(markerData)
      .pointLat('lat')
      .pointLng('lng')
      .pointColor('color')
      .pointAltitude(0.01)
      .pointRadius('size')
      .pointResolution(12)
      .pointLabel((d: unknown) => {
        const data = d as { label: string };
        return `<div style="text-align: center; padding: 5px; background: rgba(0, 0, 0, 0.8); border-radius: 3px; color: white;">
          <div style="font-weight: bold;">${data.label}</div>
          <div style="font-size: 0.8em;">Station Location</div>
        </div>`;
      });

    // Handle globe clicks to set azimuth - use refs to avoid stale closures
    globe.onGlobeClick((coords) => {
      if (coords && coords.lat !== undefined && coords.lng !== undefined) {
        const azimuth = calculateAzimuthRef.current(stationLat, stationLon, coords.lat, coords.lng);
        lastCommandTimeRef.current = Date.now();
        commandedAzimuthRef.current = azimuth;
        setCurrentAzimuth(azimuth);
        commandRotatorRef.current(azimuth, 'globe');
      }
    });

    // Set material opacity
    const material = globe.globeMaterial();
    material.opacity = 0.95;

    globeRef.current = globe;

    // Handle resize
    const handleResize = () => {
      if (containerRef.current && globeRef.current) {
        const width = containerRef.current.offsetWidth;
        const height = containerRef.current.offsetHeight;
        globeRef.current.width(width).height(height);
      }
    };

    window.addEventListener('resize', handleResize);
    handleResize();

    // Start animation
    animationRef.current = requestAnimationFrame(animateBeam);

    return () => {
      window.removeEventListener('resize', handleResize);
      if (animationRef.current) {
        cancelAnimationFrame(animationRef.current);
      }
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stationLat, stationLon, stationGrid]);

  // Update beam when rotator position changes
  // Ignore updates briefly after a command to prevent snapping back to old/stale position
  useEffect(() => {
    if (rotatorPosition?.currentAzimuth !== undefined) {
      const timeSinceCommand = Date.now() - lastCommandTimeRef.current;
      const commanded = commandedAzimuthRef.current;

      // If we recently sent a command, be selective about which updates we accept
      if (timeSinceCommand < 1000 && commanded !== null) {
        // Only accept updates if the rotator position is close to what we commanded
        // This prevents brief flips to 0 or stale positions
        const diff = Math.abs(rotatorPosition.currentAzimuth - commanded);
        const wrappedDiff = Math.min(diff, 360 - diff); // Handle wrap-around (e.g., 350° to 10°)
        if (wrappedDiff <= 15) {
          // Position is close to commanded - accept it
          setCurrentAzimuth(rotatorPosition.currentAzimuth);
        }
        // Otherwise ignore this update - keep displaying the commanded azimuth
      } else {
        // Enough time has passed, trust the rotator position
        commandedAzimuthRef.current = null;
        setCurrentAzimuth(rotatorPosition.currentAzimuth);
      }
    }
  }, [rotatorPosition]);

  // Update beam when focused callsign has bearing info
  useEffect(() => {
    if (focusedCallsignInfo?.bearing !== undefined) {
      setCurrentAzimuth(focusedCallsignInfo.bearing);
    }
  }, [focusedCallsignInfo]);

  const toggleFullscreen = useCallback(() => {
    if (!containerRef.current) return;

    if (!isFullscreen) {
      containerRef.current.requestFullscreen?.();
    } else {
      document.exitFullscreen?.();
    }
    setIsFullscreen(!isFullscreen);
  }, [isFullscreen]);

  return (
    <GlassPanel
      title="3D Globe"
      icon={<GlobeIcon className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          <span className="text-sm font-mono text-accent-primary">{currentAzimuth}°</span>
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
      <div className="relative h-full min-h-[400px]">
        {/* Globe container */}
        <div
          ref={containerRef}
          className="w-full h-full"
          style={{ cursor: 'crosshair' }}
        />

        {/* Compass overlay */}
        <div className="absolute top-4 right-4 w-32 h-32 bg-dark-800/90 backdrop-blur-sm rounded-full border-2 border-glass-200 flex items-center justify-center">
          <span className="absolute top-1 text-xs font-bold text-accent-danger">N</span>
          <span className="absolute bottom-1 text-xs font-bold text-gray-500">S</span>
          <span className="absolute left-1 text-xs font-bold text-gray-500">W</span>
          <span className="absolute right-1 text-xs font-bold text-gray-500">E</span>

          {/* Compass needle */}
          <div
            className="absolute w-1 h-12 origin-bottom transition-transform duration-300"
            style={{
              transform: `rotate(${currentAzimuth}deg)`,
              background: 'linear-gradient(to top, transparent, #fbbf24)',
              top: 'calc(50% - 48px)',
            }}
          />

          {/* Center display */}
          <div className="text-center z-10">
            <div className="text-xl font-bold text-accent-primary">{currentAzimuth}°</div>
            <div className="text-xs text-gray-500">Azimuth</div>
          </div>
        </div>

        {/* Info overlay */}
        <div className="absolute bottom-4 left-4 glass-panel px-3 py-2 text-xs">
          <div className="flex items-center gap-2 text-gray-400">
            <Navigation className="w-3 h-3" />
            <span>Click on globe to set bearing</span>
          </div>
        </div>

        {/* Target info */}
        {focusedCallsignInfo && (
          <div className="absolute top-4 left-4 glass-panel px-3 py-2">
            <div className="flex items-center gap-2">
              <Target className="w-4 h-4 text-accent-warning" />
              <div>
                <p className="font-mono font-bold text-accent-primary">
                  {focusedCallsignInfo.callsign}
                </p>
                {focusedCallsignInfo.grid && (
                  <p className="text-xs text-gray-400">{focusedCallsignInfo.grid}</p>
                )}
                {focusedCallsignInfo.bearing !== undefined && (
                  <p className="text-xs text-accent-info">
                    {focusedCallsignInfo.bearing}°
                    {focusedCallsignInfo.distance && ` / ${Math.round(focusedCallsignInfo.distance)} km`}
                  </p>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Quick bearing buttons */}
        <div className="absolute bottom-4 right-4 flex gap-2">
          {[0, 90, 180, 270].map((deg) => (
            <button
              key={deg}
              onClick={() => {
                lastCommandTimeRef.current = Date.now();
                commandedAzimuthRef.current = deg;
                setCurrentAzimuth(deg);
                commandRotator(deg, 'globe');
              }}
              className="glass-button px-2 py-1 text-xs font-mono"
            >
              {deg}°
            </button>
          ))}
        </div>
      </div>
    </GlassPanel>
  );
}
