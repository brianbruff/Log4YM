import { useState, useCallback, useEffect, useRef, useLayoutEffect } from 'react';
import { Compass, Navigation, Target, RotateCw, Maximize2, Settings, X } from 'lucide-react';
import { useAppStore } from '../store/appStore';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import { useSettingsStore, RotatorPreset } from '../store/settingsStore';

export function RotatorControls() {
  const { rotatorPosition, focusedCallsignInfo } = useAppStore();
  const { commandRotator } = useSignalR();
  const { settings } = useSettingsStore();
  
  const [targetAzimuth, setTargetAzimuth] = useState('');
  const [isRotating, setIsRotating] = useState(false);
  const [pathMode, setPathMode] = useState<'short' | 'long'>('short');

  // Calculate long path from short path (add 180 degrees)
  const shortPathBearing = focusedCallsignInfo?.bearing;
  const longPathBearing = shortPathBearing != null ? (shortPathBearing + 180) % 360 : undefined;
  const selectedBearing = pathMode === 'short' ? shortPathBearing : longPathBearing;

  const presets = settings.rotator.presets || [
    { name: 'N', azimuth: 0 },
    { name: 'E', azimuth: 90 },
    { name: 'S', azimuth: 180 },
    { name: 'W', azimuth: 270 },
  ];
  
  const currentAzimuth = rotatorPosition?.currentAzimuth ?? 0;

  const handleRotate = useCallback(async () => {
    const azimuth = parseFloat(targetAzimuth);
    if (isNaN(azimuth) || azimuth < 0 || azimuth > 360) return;

    setIsRotating(true);
    try {
      await commandRotator(azimuth, 'rotator');
    } finally {
      setIsRotating(false);
    }
  }, [targetAzimuth, commandRotator]);

  const handleRotateToTarget = useCallback(async () => {
    if (selectedBearing === undefined) return;

    setIsRotating(true);
    try {
      await commandRotator(selectedBearing, 'rotator');
    } finally {
      setIsRotating(false);
    }
  }, [selectedBearing, commandRotator]);

  const handleQuickRotate = useCallback(async (deg: number) => {
    setTargetAzimuth(deg.toString());
    setIsRotating(true);
    try {
      await commandRotator(deg, 'rotator');
    } finally {
      setIsRotating(false);
    }
  }, [commandRotator]);

  return (
    <div className="flex flex-wrap items-center gap-2">
      {/* Preset buttons */}
      {presets.map((preset, index) => (
        <button
          key={index}
          onClick={() => handleQuickRotate(preset.azimuth)}
          disabled={isRotating}
          className={`glass-button px-2 py-1 text-xs ${
            Math.abs(currentAzimuth - preset.azimuth) < 5 ? 'border-accent-primary text-accent-primary' : ''
          }`}
          title={`${preset.azimuth}°`}
        >
          <span className="font-ui font-medium">{preset.name}</span>
        </button>
      ))}

      {/* Separator */}
      <div className="w-px h-5 bg-glass-200 mx-1" />

      {/* SP/LP Toggle */}
      {shortPathBearing != null && (
        <div className="flex rounded overflow-hidden border border-glass-200 text-xs mr-1">
          <button
            onClick={() => setPathMode('short')}
            className={`px-2 py-1 font-ui font-medium transition-colors ${
              pathMode === 'short'
                ? 'bg-accent-warning text-dark-900'
                : 'bg-dark-700 text-dark-300 hover:text-dark-200'
            }`}
            title="Short Path"
          >
            SP
          </button>
          <button
            onClick={() => setPathMode('long')}
            className={`px-2 py-1 font-ui font-medium transition-colors ${
              pathMode === 'long'
                ? 'bg-accent-warning text-dark-900'
                : 'bg-dark-700 text-dark-300 hover:text-dark-200'
            }`}
            title="Long Path"
          >
            LP
          </button>
        </div>
      )}

      {/* Manual azimuth input */}
      <div className="flex items-center gap-1">
        <input
          type="number"
          min="0"
          max="360"
          value={targetAzimuth}
          onChange={(e) => setTargetAzimuth(e.target.value)}
          placeholder="Az"
          className="glass-input w-16 font-mono text-xs text-center py-1"
          onKeyDown={(e) => {
            if (e.key === 'Enter') handleRotate();
          }}
        />
        <button
          onClick={handleRotate}
          disabled={isRotating || !targetAzimuth}
          className="glass-button p-1.5 disabled:opacity-50"
          title="Rotate to azimuth"
        >
          <Navigation className={`w-4 h-4 ${isRotating ? 'animate-spin' : ''}`} />
        </button>
      </div>

      {/* Rotate to target button */}
      {selectedBearing !== undefined && (
        <>
          <div className="w-px h-5 bg-glass-200 mx-1" />
          <button
            onClick={handleRotateToTarget}
            disabled={isRotating}
            className="glass-button-success px-2 py-1 flex items-center gap-1 text-xs disabled:opacity-50"
            title={`Rotate to ${focusedCallsignInfo?.callsign} (${pathMode === 'short' ? 'SP' : 'LP'})`}
          >
            <Target className="w-3.5 h-3.5" />
            <span className="font-mono">{selectedBearing?.toFixed(0)}°</span>
          </button>
        </>
      )}
    </div>
  );
}

export function RotatorCore({ hideControls, hideCompass, integratedMode }: { hideControls?: boolean; hideCompass?: boolean; integratedMode?: boolean }) {
  const { rotatorPosition, focusedCallsignInfo } = useAppStore();
  const { commandRotator } = useSignalR();
  const { settings, updateRotatorSettings } = useSettingsStore();
  const [targetAzimuth, setTargetAzimuth] = useState('');
  const [isRotating, setIsRotating] = useState(false);
  const [displayAzimuth, setDisplayAzimuth] = useState(0);
  const [showSettings, setShowSettings] = useState(false);
  const [pathMode, setPathMode] = useState<'short' | 'long'>('short');

  // Calculate long path from short path (add 180 degrees)
  const shortPathBearing = focusedCallsignInfo?.bearing;
  const longPathBearing = shortPathBearing != null ? (shortPathBearing + 180) % 360 : undefined;
  const selectedBearing = pathMode === 'short' ? shortPathBearing : longPathBearing;

  // Rotator is enabled in settings
  const rotatorEnabled = settings.rotator.enabled;

  // Get presets from settings, with fallback to defaults
  const presets = settings.rotator.presets || [
    { name: 'N', azimuth: 0 },
    { name: 'E', azimuth: 90 },
    { name: 'S', azimuth: 180 },
    { name: 'W', azimuth: 270 },
  ];

  // Track commanded azimuth and time to prevent flip to 0
  const lastCommandTimeRef = useRef<number>(0);
  const commandedAzimuthRef = useRef<number | null>(null);
  const displayedAzimuthRef = useRef<number>(0);

  // Update display when rotator position changes, with protection against spurious 0 values
  useEffect(() => {
    // Use != null to check for both null and undefined
    if (rotatorPosition?.currentAzimuth != null && typeof rotatorPosition.currentAzimuth === 'number') {
      const newPosition = rotatorPosition.currentAzimuth;
      const currentDisplay = displayedAzimuthRef.current;

      // Detect suspicious jump to 0: backend sometimes sends 0 during state transitions
      const isNearZero = currentDisplay <= 30 || currentDisplay >= 330;
      const isSuspiciousZero = newPosition === 0 && !isNearZero;

      // Ignore suspicious jumps to 0 - this is the main fix for the flip-to-0 issue
      // The backend sometimes sends 0 during state transitions, regardless of isMoving status
      if (isSuspiciousZero) {
        return;
      }

      const timeSinceCommand = Date.now() - lastCommandTimeRef.current;
      const commanded = commandedAzimuthRef.current;

      // If we recently sent a command, be selective about which updates we accept
      if (timeSinceCommand < 1000 && commanded !== null) {
        // Only accept updates if the rotator position is close to what we commanded
        const diff = Math.abs(newPosition - commanded);
        const wrappedDiff = Math.min(diff, 360 - diff);
        if (wrappedDiff <= 15) {
          displayedAzimuthRef.current = newPosition;
          setDisplayAzimuth(newPosition);
        }
        // Otherwise ignore this update - keep displaying the commanded azimuth
      } else {
        // Enough time has passed, trust the rotator position
        commandedAzimuthRef.current = null;
        displayedAzimuthRef.current = newPosition;
        setDisplayAzimuth(newPosition);
      }
    }
  }, [rotatorPosition]);

  const currentAzimuth = displayAzimuth;

  const handleRotate = useCallback(async () => {
    const azimuth = parseFloat(targetAzimuth);
    if (isNaN(azimuth) || azimuth < 0 || azimuth > 360) return;

    lastCommandTimeRef.current = Date.now();
    commandedAzimuthRef.current = azimuth;
    displayedAzimuthRef.current = azimuth;
    setDisplayAzimuth(azimuth);
    setIsRotating(true);
    try {
      await commandRotator(azimuth, 'rotator');
    } finally {
      setIsRotating(false);
    }
  }, [targetAzimuth, commandRotator]);

  const handleRotateToTarget = useCallback(async () => {
    if (selectedBearing === undefined) return;

    lastCommandTimeRef.current = Date.now();
    commandedAzimuthRef.current = selectedBearing;
    displayedAzimuthRef.current = selectedBearing;
    setDisplayAzimuth(selectedBearing);
    setIsRotating(true);
    try {
      await commandRotator(selectedBearing, 'rotator');
    } finally {
      setIsRotating(false);
    }
  }, [selectedBearing, commandRotator]);

  const handleQuickRotate = useCallback(async (deg: number) => {
    lastCommandTimeRef.current = Date.now();
    commandedAzimuthRef.current = deg;
    displayedAzimuthRef.current = deg;
    setDisplayAzimuth(deg);
    setTargetAzimuth(deg.toString());
    setIsRotating(true);
    try {
      await commandRotator(deg, 'rotator');
    } finally {
      setIsRotating(false);
    }
  }, [commandRotator]);

  // Refs for compass scaling
  const compassContainerRef = useRef<HTMLDivElement>(null);
  const [compassScale, setCompassScale] = useState(1);

  // Update compass scale when container resizes
  useLayoutEffect(() => {
    const container = compassContainerRef.current;
    if (!container) return;

    const updateScale = () => {
      const size = Math.min(container.clientWidth, container.clientHeight);
      setCompassScale(size / 224);
    };

    updateScale();
    const observer = new ResizeObserver(updateScale);
    observer.observe(container);

    return () => observer.disconnect();
  }, [rotatorEnabled, hideCompass]);

  return (
    <>
      {/* Disabled state */}
      {!rotatorEnabled && (
        <div className={`p-8 flex flex-col items-center justify-center text-center ${hideCompass ? '' : 'h-full'}`}>
          <Compass className="w-16 h-16 text-dark-400 mb-4" />
          <p className="text-dark-300 text-lg font-ui font-medium mb-2">Rotator Disabled</p>
          <p className="text-dark-300 text-sm font-ui">
            Enable rotator in Settings to control antenna direction.
          </p>
        </div>
      )}

      {/* Enabled state */}
      {rotatorEnabled && (
      <div className={`flex flex-col relative ${hideCompass ? '' : 'h-full'} ${integratedMode ? '' : (hideCompass ? 'pt-2' : 'p-3')}`}>
        {/* Settings button when controls are hidden */}
        {hideControls && !integratedMode && !hideCompass && (
          <button
            className="absolute top-2 right-2 glass-button p-1.5 z-10"
            title="Preset Settings"
            onClick={() => setShowSettings(true)}
          >
            <Settings className="w-4 h-4" />
          </button>
        )}
      
        {/* Controls Overlay (Only shown when not hidden) */}
        {!hideControls && (
          <div className="flex flex-wrap items-center gap-2 mb-4">
              {/* Preset buttons */}
              {presets.map((preset, index) => (
                <button
                  key={index}
                  onClick={() => handleQuickRotate(preset.azimuth)}
                  disabled={isRotating}
                  className={`glass-button px-2 py-1 text-xs ${
                    Math.abs(currentAzimuth - preset.azimuth) < 5 ? 'border-accent-primary text-accent-primary' : ''
                  }`}
                  title={`${preset.azimuth}°`}
                >
                  <span className="font-ui font-medium">{preset.name}</span>
                </button>
              ))}

              {/* Separator */}
              <div className="w-px h-5 bg-glass-200" />

              {/* SP/LP Toggle */}
              {shortPathBearing != null && (
                <div className="flex rounded overflow-hidden border border-glass-200 text-xs">
                  <button
                    onClick={() => setPathMode('short')}
                    className={`px-2 py-1 font-ui font-medium transition-colors ${
                      pathMode === 'short'
                        ? 'bg-accent-warning text-dark-900'
                        : 'bg-dark-700 text-dark-300 hover:text-dark-200'
                    }`}
                    title="Short Path"
                  >
                    SP
                  </button>
                  <button
                    onClick={() => setPathMode('long')}
                    className={`px-2 py-1 font-ui font-medium transition-colors ${
                      pathMode === 'long'
                        ? 'bg-accent-warning text-dark-900'
                        : 'bg-dark-700 text-dark-300 hover:text-dark-200'
                    }`}
                    title="Long Path"
                  >
                    LP
                  </button>
                </div>
              )}

              {/* Manual azimuth input */}
              <div className="flex items-center gap-1">
                <input
                  type="number"
                  min="0"
                  max="360"
                  value={targetAzimuth}
                  onChange={(e) => setTargetAzimuth(e.target.value)}
                  placeholder="Az"
                  className="glass-input w-16 font-mono text-xs text-center py-1"
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') handleRotate();
                  }}
                />
                <button
                  onClick={handleRotate}
                  disabled={isRotating || !targetAzimuth}
                  className="glass-button p-1.5 disabled:opacity-50"
                  title="Rotate to azimuth"
                >
                  <Navigation className={`w-4 h-4 ${isRotating ? 'animate-spin' : ''}`} />
                </button>
              </div>

              {/* Rotate to target button */}
              {selectedBearing !== undefined && (
                <>
                  <div className="w-px h-5 bg-glass-200" />
                  <button
                    onClick={handleRotateToTarget}
                    disabled={isRotating}
                    className="glass-button-success px-2 py-1 flex items-center gap-1 text-xs disabled:opacity-50"
                    title={`Rotate to ${focusedCallsignInfo?.callsign} (${pathMode === 'short' ? 'SP' : 'LP'})`}
                  >
                    <Target className="w-3.5 h-3.5" />
                    <span className="font-mono">{selectedBearing?.toFixed(0)}°</span>
                  </button>
                </>
              )}
          </div>
        )}

        {/* Compass Rose - conditionally hidden */}
        {!hideCompass && (
          <div className="flex-1 flex items-center justify-center min-h-0">
            {/* Inline compass for proper ref handling */}
            <div className="w-full h-full flex items-center justify-center">
              <div
                ref={compassContainerRef}
                className="relative aspect-square pointer-events-none"
                style={{
                  width: 'min(100%, 100%)',
                  height: 'min(100%, 100%)',
                  maxWidth: integratedMode ? 'none' : '400px',
                  maxHeight: integratedMode ? 'none' : '400px',
                }}
              >
                {/* Scale wrapper - scales the 224px base design to fit */}
                <div
                  className="absolute origin-center"
                  style={{
                    width: '224px',
                    height: '224px',
                    left: '50%',
                    top: '50%',
                    transform: `translate(-50%, -50%) scale(${compassScale})`,
                  }}
                >
                  {/* Outer ring with glow */}
                  <div className={`absolute inset-0 rounded-full border-[3px] pointer-events-none ${integratedMode ? 'border-[#ffb432]/40 bg-transparent' : 'border-glass-200 bg-dark-800/70 shadow-glow'}`} />

                  {/* Inner decorative ring */}
                  {!integratedMode && <div className="absolute inset-4 rounded-full border border-glass-100" />}

                  {/* Degree markers - major (every 30deg) */}
                  {[0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330].map((deg) => (
                    <div
                      key={`major-${deg}`}
                      className={`absolute w-0.5 h-4 ${integratedMode ? 'bg-[#ffb432]/80' : 'bg-glass-300'}`}
                      style={{
                        left: '50%',
                        top: '8px',
                        transformOrigin: '50% 104px',
                        transform: `translateX(-50%) rotate(${deg}deg)`,
                      }}
                    />
                  ))}

                  {/* Degree markers - minor (every 10deg) */}
                  {[10, 20, 40, 50, 70, 80, 100, 110, 130, 140, 160, 170, 190, 200, 220, 230, 250, 260, 280, 290, 310, 320, 340, 350].map((deg) => (
                    <div
                      key={`minor-${deg}`}
                      className={`absolute w-px h-2 ${integratedMode ? 'bg-[#ffb432]/40' : 'bg-glass-200'}`}
                      style={{
                        left: '50%',
                        top: '12px',
                        transformOrigin: '50% 100px',
                        transform: `translateX(-50%) rotate(${deg}deg)`,
                      }}
                    />
                  ))}

                  {/* Cardinal directions */}
                  <span className={`absolute ${integratedMode ? 'top-[22px]' : 'top-5'} left-1/2 -translate-x-1/2 text-[15px] font-bold text-accent-danger font-display drop-shadow-[0_2px_4px_rgba(0,0,0,0.9)]`}>N</span>
                  <span className={`absolute ${integratedMode ? 'bottom-[22px]' : 'bottom-5'} left-1/2 -translate-x-1/2 text-[15px] font-bold ${integratedMode ? 'text-gray-200' : 'text-dark-300'} font-display drop-shadow-[0_2px_4px_rgba(0,0,0,0.9)]`}>S</span>
                  <span className={`absolute ${integratedMode ? 'left-[22px]' : 'left-5'} top-1/2 -translate-y-1/2 text-[15px] font-bold ${integratedMode ? 'text-gray-200' : 'text-dark-300'} font-display drop-shadow-[0_2px_4px_rgba(0,0,0,0.9)]`}>W</span>
                  <span className={`absolute ${integratedMode ? 'right-[22px]' : 'right-5'} top-1/2 -translate-y-1/2 text-[15px] font-bold ${integratedMode ? 'text-gray-200' : 'text-dark-300'} font-display drop-shadow-[0_2px_4px_rgba(0,0,0,0.9)]`}>E</span>

                  {/* Intercardinal directions */}
                  {!integratedMode && (
                    <>
                      <span className="absolute top-8 right-8 text-xs text-dark-400 font-mono">NE</span>
                      <span className="absolute top-8 left-8 text-xs text-dark-400 font-mono">NW</span>
                      <span className="absolute bottom-8 right-8 text-xs text-dark-400 font-mono">SE</span>
                      <span className="absolute bottom-8 left-8 text-xs text-dark-400 font-mono">SW</span>
                    </>
                  )}

                  {/* Short path bearing indicator (amber line) */}
                  {shortPathBearing != null && (
                    <div
                      className="absolute left-1/2 top-1/2 w-1 h-24 -ml-0.5 origin-bottom"
                      style={{
                        transform: `translateY(-100%) rotate(${shortPathBearing}deg)`,
                        background: 'linear-gradient(to top, transparent, #ffb432)',
                        opacity: pathMode === 'short' ? 0.8 : 0.3,
                      }}
                    />
                  )}

                  {/* Long path bearing indicator (amber dashed line) */}
                  {longPathBearing != null && (
                    <div
                      className="absolute left-1/2 top-1/2 w-0.5 h-24 origin-bottom"
                      style={{
                        transform: `translateY(-100%) rotate(${longPathBearing}deg)`,
                        background: `repeating-linear-gradient(to top, transparent 0px, transparent 4px, #ffb432 4px, #ffb432 8px)`,
                        opacity: pathMode === 'long' ? 0.8 : 0.3,
                      }}
                    />
                  )}

                  {/* Rotator needle (main beam direction) */}
                  <div
                    className="absolute left-1/2 top-1/2 w-1.5 h-24 -ml-[3px] origin-bottom transition-transform duration-700 ease-out"
                    style={{
                      transform: `translateY(-100%) rotate(${currentAzimuth}deg)`,
                    }}
                  >
                    <div
                      className="w-full h-full rounded-full"
                      style={{
                        background: 'linear-gradient(to top, transparent 0%, #00ddff 30%, #00ff88 100%)',
                      }}
                    />
                    {/* Needle tip glow */}
                    <div className="absolute -top-1 left-1/2 -translate-x-1/2 w-4 h-4 bg-accent-primary rounded-full shadow-glow opacity-80" />
                  </div>

                  {/* Center hub */}
                  {!integratedMode && (
                    <div className="absolute left-1/2 top-1/2 w-8 h-8 -ml-4 -mt-4 bg-dark-700 border-2 border-glass-300 rounded-full flex items-center justify-center">
                      <div className="w-2 h-2 bg-accent-primary rounded-full" />
                    </div>
                  )}

                  {/* Azimuth display */}
                  <div className={`absolute left-1/2 ${integratedMode ? 'bottom-[35px]' : 'top-1/2 -translate-y-1/2 translate-y-10'} -translate-x-1/2 text-center pointer-events-none`}>
                    <span className={`text-3xl font-display font-bold text-accent-primary drop-shadow-glow ${integratedMode ? 'drop-shadow-[0_3px_6px_rgba(0,0,0,1)]' : ''}`}>
                      {currentAzimuth.toFixed(0)}&deg;
                    </span>
                  </div>

                  {/* Moving indicator */}
                  {rotatorPosition?.isMoving && !integratedMode && (
                    <div className="absolute left-1/2 -bottom-2 -translate-x-1/2 flex items-center gap-1 text-xs text-accent-warning font-ui">
                      <RotateCw className="w-3 h-3 animate-spin" />
                      <span>Moving</span>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Compact Target Info */}
        {!integratedMode && focusedCallsignInfo && (
          <div className="flex items-center justify-between text-xs bg-dark-800/50 rounded-lg px-3 py-2 border border-glass-100 mt-4">
            <div className="flex items-center gap-2">
              <Target className="w-3.5 h-3.5 text-accent-warning" />
              <span className="font-mono font-bold text-accent-primary">
                {focusedCallsignInfo.callsign}
              </span>
              {focusedCallsignInfo.grid && (
                <span className="text-dark-300 font-mono">{focusedCallsignInfo.grid}</span>
              )}
            </div>
            <div className="flex items-center gap-3 font-mono">
              <span className={pathMode === 'short' ? 'text-accent-warning' : 'text-dark-300'}>
                SP {shortPathBearing?.toFixed(0) ?? '---'}&deg;
              </span>
              <span className={pathMode === 'long' ? 'text-accent-warning' : 'text-dark-300'}>
                LP {longPathBearing?.toFixed(0) ?? '---'}&deg;
              </span>
              {focusedCallsignInfo.distance != null && (
                <span className="text-accent-info">
                  {Math.round(focusedCallsignInfo.distance)} km
                </span>
              )}
            </div>
          </div>
        )}

        {/* Status line - only show when moving */}
        {!integratedMode && rotatorPosition?.isMoving && rotatorPosition?.targetAzimuth != null && (
          <div className="flex items-center justify-center text-xs text-accent-warning font-ui gap-1 mt-2">
            <RotateCw className="w-3 h-3 animate-spin" />
            <span>Moving to {rotatorPosition.targetAzimuth.toFixed(0)}&deg;</span>
          </div>
        )}
      </div>
      )}

      {/* Settings Modal */}
      {showSettings && !integratedMode && (
        <RotatorSettingsModal
          presets={presets}
          onSave={(newPresets) => {
            updateRotatorSettings({ presets: newPresets });
            setShowSettings(false);
          }}
          onClose={() => setShowSettings(false)}
        />
      )}
    </>
  );
}

export function RotatorPlugin() {
  const { settings } = useSettingsStore();
  const [isFullscreen, setIsFullscreen] = useState(false);

  const toggleFullscreen = useCallback(() => {
    const el = document.getElementById('rotator-plugin-container');
    if (!el) return;

    if (!isFullscreen) {
      el.requestFullscreen?.();
    } else {
      document.exitFullscreen?.();
    }
    setIsFullscreen(!isFullscreen);
  }, [isFullscreen]);

  return (
    <GlassPanel
      title="Rotator"
      icon={<Compass className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-4">
          {settings.rotator.enabled && <RotatorControls />}
          <div className="flex items-center gap-2">
            {settings.rotator.enabled ? (
              <>
                {/* Fullscreen button */}
                <button className="glass-button p-1.5" title="Fullscreen" onClick={toggleFullscreen}>
                  <Maximize2 className="w-4 h-4" />
                </button>
              </>
            ) : (
              <span className="text-sm font-ui text-dark-300">Disabled</span>
            )}
          </div>
        </div>
      }
    >
      <div id="rotator-plugin-container" className="h-full">
         <RotatorCore hideControls={true} />
      </div>
    </GlassPanel>
  );
}

// Settings Modal Component
interface RotatorSettingsModalProps {
  presets: RotatorPreset[];
  onSave: (presets: RotatorPreset[]) => void;
  onClose: () => void;
}

function RotatorSettingsModal({ presets, onSave, onClose }: RotatorSettingsModalProps) {
  const [editedPresets, setEditedPresets] = useState<RotatorPreset[]>(presets);

  const handlePresetChange = (index: number, field: 'name' | 'azimuth', value: string) => {
    const newPresets = [...editedPresets];
    if (field === 'name') {
      newPresets[index] = { ...newPresets[index], name: value };
    } else {
      const azimuth = parseInt(value, 10);
      if (!isNaN(azimuth) && azimuth >= 0 && azimuth <= 360) {
        newPresets[index] = { ...newPresets[index], azimuth };
      }
    }
    setEditedPresets(newPresets);
  };

  const handleReset = () => {
    setEditedPresets([
      { name: 'N', azimuth: 0 },
      { name: 'E', azimuth: 90 },
      { name: 'S', azimuth: 180 },
      { name: 'W', azimuth: 270 },
    ]);
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-dark-800 rounded-xl border border-glass-100 shadow-2xl w-full max-w-md mx-4">
        <div className="flex items-center justify-between px-4 py-3 border-b border-glass-100">
          <h3 className="text-lg font-ui font-semibold text-dark-200">Rotator Presets</h3>
          <button
            onClick={onClose}
            className="text-dark-300 hover:text-dark-200 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-4 space-y-4">
          <p className="text-sm font-ui text-dark-300">
            Configure quick-access heading presets. Examples: N (0&deg;), VK LP (240&deg;), JA SP (30&deg;)
          </p>

          <div className="space-y-3">
            {editedPresets.map((preset, index) => (
              <div key={index} className="flex items-center gap-3">
                <span className="text-sm text-dark-300 font-mono w-6">{index + 1}.</span>
                <input
                  type="text"
                  value={preset.name}
                  onChange={(e) => handlePresetChange(index, 'name', e.target.value)}
                  placeholder="Name"
                  maxLength={12}
                  className="glass-input flex-1 text-sm"
                />
                <div className="flex items-center gap-1">
                  <input
                    type="number"
                    value={preset.azimuth}
                    onChange={(e) => handlePresetChange(index, 'azimuth', e.target.value)}
                    min="0"
                    max="360"
                    className="glass-input w-20 text-sm font-mono text-center"
                  />
                  <span className="text-dark-300 text-sm">&deg;</span>
                </div>
              </div>
            ))}
          </div>

          <div className="text-xs text-dark-300 pt-2 border-t border-glass-100">
            <p className="font-ui font-medium mb-1">Example configurations:</p>
            <ul className="space-y-0.5 list-disc list-inside font-mono">
              <li>NA (290&deg;) - North America</li>
              <li>VK LP (240&deg;) - VK Long Path</li>
              <li>VK SP (60&deg;) - VK Short Path</li>
              <li>JA SP (30&deg;) - Japan Short Path</li>
            </ul>
          </div>
        </div>

        <div className="flex justify-between gap-2 px-4 py-3 border-t border-glass-100">
          <button
            onClick={handleReset}
            className="px-3 py-2 text-sm font-ui font-medium text-dark-300 hover:text-dark-200 transition-colors"
          >
            Reset to N/E/S/W
          </button>
          <div className="flex gap-2">
            <button
              onClick={onClose}
              className="px-4 py-2 text-sm font-ui font-medium bg-dark-700 text-dark-200 rounded-lg hover:bg-dark-600 transition-all border border-glass-100"
            >
              Cancel
            </button>
            <button
              onClick={() => onSave(editedPresets)}
              className="px-4 py-2 text-sm font-ui font-medium bg-accent-primary text-white rounded-lg hover:bg-accent-primary/80 transition-all"
            >
              Save
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
