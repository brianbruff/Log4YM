import { useState, useEffect, useCallback } from 'react';
import { Sun, ChevronLeft, ChevronRight } from 'lucide-react';
import { GlassPanel } from '../components/GlassPanel';

// NASA SDO/AIA Wavelengths
const WAVELENGTHS = {
  '0094': { name: 'Corona (94Å)', color: '#00ff00' },
  '0193': { name: 'Chromosphere (193Å)', color: '#ffff00' },
  '0171': { name: 'Quiet Corona (171Å)', color: '#ff8800' },
  '0131': { name: 'Flaring (131Å)', color: '#ff0000' },
  '0304': { name: 'Chromosphere (304Å)', color: '#ff00ff' },
  '0211': { name: 'Active Regions (211Å)', color: '#8800ff' },
} as const;

type WavelengthKey = keyof typeof WAVELENGTHS;

// Mock data for solar indices (in production, fetch from NOAA API)
interface SolarIndices {
  sfi: number;
  sfiHistory: number[];
  kIndex: number;
  kIndexHistory: number[];
  ssn: number;
  ssnHistory: number[];
  timestamp: Date;
}

// Mock data for X-Ray flux
interface XRayFlux {
  current: number;
  peak6h: number;
  fluxClass: string;
  data: { time: number; value: number }[];
}

// Moon phase data
interface LunarPhase {
  phase: number; // 0-1 (0=New, 0.5=Full)
  phaseName: string;
  illumination: number;
  nextNew: Date;
  nextFull: Date;
}

// Views
type ViewType = 'solar-image' | 'indices' | 'xray' | 'lunar';

// K-Index color mapping
function getKIndexColor(kIndex: number): string {
  if (kIndex <= 3) return '#10b981'; // green
  if (kIndex <= 5) return '#fbbf24'; // yellow
  return '#ef4444'; // red
}

// X-Ray flux class calculation
function getFluxClass(flux: number): string {
  if (flux >= 1e-4) return 'X';
  if (flux >= 1e-5) return 'M';
  if (flux >= 1e-6) return 'C';
  if (flux >= 1e-7) return 'B';
  return 'A';
}

// Calculate moon phase from current date
function calculateLunarPhase(): LunarPhase {
  const now = new Date();

  // Known new moon (Jan 1, 2000)
  const knownNewMoon = new Date('2000-01-06T18:14:00Z').getTime();
  const synodicMonth = 29.530588853; // days

  const daysSinceKnown = (now.getTime() - knownNewMoon) / (1000 * 60 * 60 * 24);
  const phase = (daysSinceKnown / synodicMonth) % 1;

  let phaseName = '';
  if (phase < 0.03 || phase > 0.97) phaseName = 'New Moon';
  else if (phase < 0.22) phaseName = 'Waxing Crescent';
  else if (phase < 0.28) phaseName = 'First Quarter';
  else if (phase < 0.47) phaseName = 'Waxing Gibbous';
  else if (phase < 0.53) phaseName = 'Full Moon';
  else if (phase < 0.72) phaseName = 'Waning Gibbous';
  else if (phase < 0.78) phaseName = 'Last Quarter';
  else phaseName = 'Waning Crescent';

  const illumination = Math.abs(Math.cos(phase * Math.PI * 2)) * 100;

  // Calculate next new and full moon
  const daysUntilNew = ((1 - phase) * synodicMonth) % synodicMonth;
  const daysUntilFull = ((0.5 - phase + 1) % 1) * synodicMonth;

  const nextNew = new Date(now.getTime() + daysUntilNew * 24 * 60 * 60 * 1000);
  const nextFull = new Date(now.getTime() + daysUntilFull * 24 * 60 * 60 * 1000);

  return { phase, phaseName, illumination, nextNew, nextFull };
}

// View 1: Solar Image Component
function SolarImageView() {
  const [wavelength, setWavelength] = useState<WavelengthKey>('0193');
  const [imageLoaded, setImageLoaded] = useState(false);

  // NASA SDO/AIA latest image URL
  const imageUrl = `https://sdo.gsfc.nasa.gov/assets/img/latest/latest_512_${wavelength}.jpg`;

  useEffect(() => {
    setImageLoaded(false);
  }, [wavelength]);

  return (
    <div className="flex flex-col gap-3 h-full">
      <div className="flex items-center gap-2">
        <label className="text-sm text-gray-400">Wavelength:</label>
        <select
          value={wavelength}
          onChange={(e) => setWavelength(e.target.value as WavelengthKey)}
          className="glass-button px-3 py-1.5 text-sm"
        >
          {Object.entries(WAVELENGTHS).map(([key, { name }]) => (
            <option key={key} value={key}>
              {name}
            </option>
          ))}
        </select>
      </div>

      <div className="flex-1 flex items-center justify-center bg-dark-800 rounded-lg overflow-hidden relative">
        {!imageLoaded && (
          <div className="absolute inset-0 flex items-center justify-center">
            <div className="w-12 h-12 border-4 border-accent-primary/30 border-t-accent-primary rounded-full animate-spin" />
          </div>
        )}
        <img
          src={imageUrl}
          alt={`Solar image - ${WAVELENGTHS[wavelength].name}`}
          className="w-full h-full object-contain"
          onLoad={() => setImageLoaded(true)}
          onError={() => setImageLoaded(true)}
        />
      </div>

      <div className="text-xs text-gray-500 text-center">
        NASA SDO/AIA - Updated every ~15 minutes
      </div>
    </div>
  );
}

// View 2: Solar Indices Component
function SolarIndicesView() {
  const [indices, setIndices] = useState<SolarIndices>({
    sfi: 150,
    sfiHistory: [145, 148, 152, 149, 150],
    kIndex: 3,
    kIndexHistory: [2, 3, 4, 3, 3, 2, 3, 4],
    ssn: 75,
    ssnHistory: [72, 74, 76, 75, 75],
    timestamp: new Date(),
  });

  // In production, fetch from NOAA API
  useEffect(() => {
    // Mock data - replace with real API call
    const interval = setInterval(() => {
      setIndices(prev => ({
        ...prev,
        timestamp: new Date(),
      }));
    }, 60000);

    return () => clearInterval(interval);
  }, []);

  return (
    <div className="flex flex-col gap-6 h-full justify-center">
      {/* SFI */}
      <div className="glass-panel p-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-sm text-gray-400">Solar Flux Index</span>
          <span className="text-3xl font-mono font-bold text-accent-primary">{indices.sfi}</span>
        </div>
        <div className="flex gap-1 h-12">
          {indices.sfiHistory.map((val, i) => (
            <div
              key={i}
              className="flex-1 bg-accent-primary/20 rounded-sm"
              style={{ height: `${(val / 200) * 100}%`, alignSelf: 'flex-end' }}
            />
          ))}
        </div>
        <div className="text-xs text-gray-500 mt-1">Last 5 readings</div>
      </div>

      {/* K-Index */}
      <div className="glass-panel p-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-sm text-gray-400">K-Index</span>
          <span
            className="text-3xl font-mono font-bold"
            style={{ color: getKIndexColor(indices.kIndex) }}
          >
            {indices.kIndex}
          </span>
        </div>
        <div className="flex gap-1 h-12">
          {indices.kIndexHistory.map((val, i) => (
            <div
              key={i}
              className="flex-1 rounded-sm"
              style={{
                height: `${(val / 9) * 100}%`,
                alignSelf: 'flex-end',
                backgroundColor: getKIndexColor(val),
                opacity: 0.8,
              }}
            />
          ))}
        </div>
        <div className="text-xs text-gray-500 mt-1">Last 8 readings (3-hour periods)</div>
      </div>

      {/* SSN */}
      <div className="glass-panel p-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-sm text-gray-400">Sunspot Number</span>
          <span className="text-3xl font-mono font-bold text-accent-primary">{indices.ssn}</span>
        </div>
        <div className="flex gap-1 h-12">
          {indices.ssnHistory.map((val, i) => (
            <div
              key={i}
              className="flex-1 bg-accent-primary/20 rounded-sm"
              style={{ height: `${(val / 150) * 100}%`, alignSelf: 'flex-end' }}
            />
          ))}
        </div>
        <div className="text-xs text-gray-500 mt-1">Last 5 days</div>
      </div>

      <div className="text-xs text-gray-500 text-center">
        Updated: {indices.timestamp.toLocaleTimeString()}
      </div>
    </div>
  );
}

// View 3: X-Ray Flux Component
function XRayFluxView() {
  const [xrayData] = useState<XRayFlux>({
    current: 1.5e-6,
    peak6h: 2.1e-6,
    fluxClass: 'C1.5',
    data: Array.from({ length: 72 }, (_, i) => ({
      time: Date.now() - (72 - i) * 5 * 60 * 1000,
      value: 1e-6 + Math.random() * 5e-6,
    })),
  });

  const currentClass = getFluxClass(xrayData.current);
  const peakClass = getFluxClass(xrayData.peak6h);

  // Chart dimensions
  const chartHeight = 200;
  const chartWidth = 600;
  const padding = { top: 20, right: 40, bottom: 30, left: 60 };

  // Log scale mapping
  const minLog = -8; // 1e-8
  const maxLog = -3; // 1e-3

  const yScale = (value: number) => {
    const logValue = Math.log10(value);
    const normalized = (logValue - minLog) / (maxLog - minLog);
    return chartHeight - padding.bottom - (normalized * (chartHeight - padding.top - padding.bottom));
  };

  const xScale = (index: number) => {
    return padding.left + (index / (xrayData.data.length - 1)) * (chartWidth - padding.left - padding.right);
  };

  const pathData = xrayData.data
    .map((point, i) => `${i === 0 ? 'M' : 'L'} ${xScale(i)} ${yScale(point.value)}`)
    .join(' ');

  // Threshold lines
  const thresholds = [
    { value: 1e-4, label: 'X', color: '#ef4444' },
    { value: 1e-5, label: 'M', color: '#f97316' },
    { value: 1e-6, label: 'C', color: '#fbbf24' },
    { value: 1e-7, label: 'B', color: '#10b981' },
  ];

  return (
    <div className="flex flex-col gap-4 h-full">
      {/* Current and Peak values */}
      <div className="grid grid-cols-2 gap-4">
        <div className="glass-panel p-3 text-center">
          <div className="text-xs text-gray-400 mb-1">Current</div>
          <div className="text-2xl font-mono font-bold text-accent-primary">{currentClass}{xrayData.current.toExponential(1)}</div>
        </div>
        <div className="glass-panel p-3 text-center">
          <div className="text-xs text-gray-400 mb-1">6-Hour Peak</div>
          <div className="text-2xl font-mono font-bold text-accent-warning">{peakClass}{xrayData.peak6h.toExponential(1)}</div>
        </div>
      </div>

      {/* Chart */}
      <div className="flex-1 flex items-center justify-center bg-dark-800 rounded-lg p-4 overflow-auto">
        <svg width={chartWidth} height={chartHeight} className="text-xs">
          <defs>
            <linearGradient id="xrayGradient" x1="0%" y1="0%" x2="0%" y2="100%">
              <stop offset="0%" stopColor="#6366f1" stopOpacity="0.4" />
              <stop offset="100%" stopColor="#6366f1" stopOpacity="0.05" />
            </linearGradient>
          </defs>

          {/* Grid and thresholds */}
          {thresholds.map((threshold) => {
            const y = yScale(threshold.value);
            return (
              <g key={threshold.label}>
                <line
                  x1={padding.left}
                  y1={y}
                  x2={chartWidth - padding.right}
                  y2={y}
                  stroke={threshold.color}
                  strokeWidth="1"
                  strokeDasharray="3,3"
                  opacity="0.3"
                />
                <text
                  x={chartWidth - padding.right + 5}
                  y={y + 4}
                  fill={threshold.color}
                  fontSize="10"
                  fontWeight="bold"
                >
                  {threshold.label}
                </text>
              </g>
            );
          })}

          {/* Data line */}
          <path
            d={pathData}
            fill="none"
            stroke="#6366f1"
            strokeWidth="2"
          />

          {/* Area fill */}
          <path
            d={`${pathData} L ${xScale(xrayData.data.length - 1)} ${chartHeight - padding.bottom} L ${padding.left} ${chartHeight - padding.bottom} Z`}
            fill="url(#xrayGradient)"
          />

          {/* Axes labels */}
          <text
            x={chartWidth / 2}
            y={chartHeight - 5}
            textAnchor="middle"
            fill="#9ca3af"
            fontSize="10"
          >
            Time (6 hours)
          </text>
          <text
            x={15}
            y={chartHeight / 2}
            textAnchor="middle"
            fill="#9ca3af"
            fontSize="10"
            transform={`rotate(-90, 15, ${chartHeight / 2})`}
          >
            X-Ray Flux (W/m²)
          </text>
        </svg>
      </div>

      <div className="text-xs text-gray-500 text-center">
        GOES Satellite Data - 6-Hour Window
      </div>
    </div>
  );
}

// View 4: Lunar Phase Component
function LunarPhaseView() {
  const [lunar, setLunar] = useState<LunarPhase>(calculateLunarPhase());

  useEffect(() => {
    const interval = setInterval(() => {
      setLunar(calculateLunarPhase());
    }, 60000); // Update every minute

    return () => clearInterval(interval);
  }, []);

  const moonRadius = 80;
  const centerX = 100;
  const centerY = 100;

  // Calculate terminator position
  const terminatorX = centerX + Math.cos((lunar.phase - 0.5) * Math.PI) * moonRadius;

  return (
    <div className="flex flex-col items-center justify-center gap-6 h-full">
      {/* Moon SVG */}
      <svg width="200" height="200" viewBox="0 0 200 200">
        <defs>
          <radialGradient id="moonGradient">
            <stop offset="0%" stopColor="#f0f0f0" />
            <stop offset="100%" stopColor="#9ca3af" />
          </radialGradient>
        </defs>

        {/* Shadow circle (dark side) */}
        <circle
          cx={centerX}
          cy={centerY}
          r={moonRadius}
          fill="#1f2937"
        />

        {/* Illuminated part */}
        {lunar.phase < 0.5 ? (
          // Waxing (0 to 0.5)
          <ellipse
            cx={terminatorX}
            cy={centerY}
            rx={Math.abs(terminatorX - centerX)}
            ry={moonRadius}
            fill="url(#moonGradient)"
          />
        ) : (
          // Waning (0.5 to 1)
          <>
            <circle
              cx={centerX}
              cy={centerY}
              r={moonRadius}
              fill="url(#moonGradient)"
            />
            <ellipse
              cx={terminatorX}
              cy={centerY}
              rx={Math.abs(centerX - terminatorX)}
              ry={moonRadius}
              fill="#1f2937"
            />
          </>
        )}

        {/* Moon outline */}
        <circle
          cx={centerX}
          cy={centerY}
          r={moonRadius}
          fill="none"
          stroke="#6b7280"
          strokeWidth="2"
        />
      </svg>

      {/* Moon info */}
      <div className="text-center space-y-3">
        <div>
          <div className="text-2xl font-bold text-accent-primary mb-1">{lunar.phaseName}</div>
          <div className="text-lg text-gray-300">{lunar.illumination.toFixed(1)}% illuminated</div>
        </div>

        <div className="glass-panel px-4 py-3 space-y-2">
          <div className="flex justify-between gap-8 text-sm">
            <span className="text-gray-400">Next New Moon:</span>
            <span className="text-gray-200 font-mono">{lunar.nextNew.toLocaleDateString()}</span>
          </div>
          <div className="flex justify-between gap-8 text-sm">
            <span className="text-gray-400">Next Full Moon:</span>
            <span className="text-gray-200 font-mono">{lunar.nextFull.toLocaleDateString()}</span>
          </div>
        </div>
      </div>

      <div className="text-xs text-gray-500 text-center">
        Essential for EME (Moonbounce) operations
      </div>
    </div>
  );
}

// Main component with view cycling
export function SolarPanelPlugin() {
  const [currentView, setCurrentView] = useState<ViewType>('solar-image');
  const [autoRotate, setAutoRotate] = useState(false);

  const views: ViewType[] = ['solar-image', 'indices', 'xray', 'lunar'];
  const viewTitles = {
    'solar-image': 'Solar Image',
    'indices': 'Solar Indices',
    'xray': 'X-Ray Flux',
    'lunar': 'Lunar Phase',
  };

  const currentIndex = views.indexOf(currentView);

  const nextView = useCallback(() => {
    setCurrentView(views[(currentIndex + 1) % views.length]);
  }, [currentIndex, views]);

  const prevView = useCallback(() => {
    setCurrentView(views[(currentIndex - 1 + views.length) % views.length]);
  }, [currentIndex, views]);

  // Auto-rotate through views
  useEffect(() => {
    if (!autoRotate) return;

    const interval = setInterval(nextView, 15000); // 15 seconds per view
    return () => clearInterval(interval);
  }, [autoRotate, nextView]);

  return (
    <GlassPanel
      title="Solar Panel"
      icon={<Sun className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          <button
            onClick={() => setAutoRotate(!autoRotate)}
            className={`text-xs px-2 py-1 rounded ${
              autoRotate ? 'bg-accent-primary text-white' : 'text-gray-400 hover:text-gray-200'
            }`}
            title="Auto-rotate views"
          >
            Auto
          </button>
          <button
            onClick={prevView}
            className="glass-button p-1"
            title="Previous view"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="text-sm text-gray-400 min-w-[120px] text-center">
            {viewTitles[currentView]}
          </span>
          <button
            onClick={nextView}
            className="glass-button p-1"
            title="Next view"
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      }
    >
      <div className="p-4 h-full overflow-auto">
        {currentView === 'solar-image' && <SolarImageView />}
        {currentView === 'indices' && <SolarIndicesView />}
        {currentView === 'xray' && <XRayFluxView />}
        {currentView === 'lunar' && <LunarPhaseView />}
      </div>
    </GlassPanel>
  );
}
