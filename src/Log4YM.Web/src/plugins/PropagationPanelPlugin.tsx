import { useState, useCallback, useMemo } from 'react';
import { Radio, ChevronLeft, ChevronRight } from 'lucide-react';
import { GlassPanel } from '../components/GlassPanel';

// HF Bands (80m through 10m)
const HF_BANDS = [
  { name: '80m', freq: 3.5 },
  { name: '60m', freq: 5.3 },
  { name: '40m', freq: 7.0 },
  { name: '30m', freq: 10.1 },
  { name: '20m', freq: 14.0 },
  { name: '17m', freq: 18.1 },
  { name: '15m', freq: 21.0 },
  { name: '12m', freq: 24.9 },
  { name: '10m', freq: 28.0 },
] as const;

// UTC Hours
const UTC_HOURS = Array.from({ length: 24 }, (_, i) => i);

// Views
type ViewType = 'heatmap' | 'bar-chart' | 'band-conditions';

// Band condition status
type BandStatus = 'EXCELLENT' | 'GOOD' | 'FAIR' | 'POOR' | 'CLOSED';

// Mock propagation data generator (in production, fetch from VOACAP API)
function generateMockPropagationData(): number[][] {
  // Returns reliability percentage [0-100] for each hour and band
  // Row = band index, Column = hour
  const data: number[][] = [];

  for (let bandIdx = 0; bandIdx < HF_BANDS.length; bandIdx++) {
    const bandData: number[] = [];
    const band = HF_BANDS[bandIdx];

    for (let hour = 0; hour < 24; hour++) {
      // Higher bands better during day, lower bands better at night
      let reliability = 0;

      if (band.freq >= 14.0) {
        // Higher bands (20m, 17m, 15m, 12m, 10m) - daytime bands
        const dayPeak = 12; // UTC noon
        const hourDiff = Math.abs(hour - dayPeak);
        reliability = Math.max(0, 85 - hourDiff * 6 + Math.random() * 20);
      } else if (band.freq >= 7.0) {
        // Mid bands (40m, 30m) - twilight bands
        const dawnPeak = 6;
        const duskPeak = 18;
        const dawnDiff = Math.abs(hour - dawnPeak);
        const duskDiff = Math.abs(hour - duskPeak);
        reliability = Math.max(0, 80 - Math.min(dawnDiff, duskDiff) * 5 + Math.random() * 20);
      } else {
        // Lower bands (80m, 60m) - nighttime bands
        const nightPeak = 0; // UTC midnight
        const hourDiff = Math.min(Math.abs(hour - nightPeak), Math.abs(hour - 24 + nightPeak));
        reliability = Math.max(0, 90 - hourDiff * 5 + Math.random() * 15);
      }

      bandData.push(Math.min(100, Math.max(0, reliability)));
    }
    data.push(bandData);
  }

  return data;
}

// Color mapping for reliability (0-100)
function getReliabilityColor(reliability: number): string {
  if (reliability >= 75) return '#ef4444'; // red (excellent)
  if (reliability >= 60) return '#f97316'; // orange
  if (reliability >= 45) return '#fbbf24'; // yellow
  if (reliability >= 30) return '#a3e635'; // lime
  return '#22c55e'; // green (poor)
}

// Get band status from reliability
function getBandStatus(reliability: number): BandStatus {
  if (reliability >= 80) return 'EXCELLENT';
  if (reliability >= 60) return 'GOOD';
  if (reliability >= 40) return 'FAIR';
  if (reliability >= 20) return 'POOR';
  return 'CLOSED';
}

// Get status color
function getStatusColor(status: BandStatus): string {
  switch (status) {
    case 'EXCELLENT': return '#ef4444'; // red
    case 'GOOD': return '#22c55e'; // green
    case 'FAIR': return '#fbbf24'; // yellow
    case 'POOR': return '#f97316'; // orange
    case 'CLOSED': return '#6b7280'; // gray
  }
}

// View 1: VOACAP Heatmap
function VOACAPHeatmapView({ data }: { data: number[][] }) {
  const currentHour = new Date().getUTCHours();

  return (
    <div className="flex flex-col gap-2 h-full p-2">
      <div className="text-xs text-gray-400 text-center mb-1">
        24-Hour Propagation Forecast (UTC)
      </div>

      <div className="flex-1 flex flex-col gap-1 overflow-auto">
        {/* Header row - UTC hours */}
        <div className="flex gap-0.5">
          <div className="w-12 flex-shrink-0" /> {/* Band label space */}
          {UTC_HOURS.map((hour) => (
            <div
              key={hour}
              className="flex-1 text-center text-[10px] font-mono text-gray-400 min-w-[16px]"
            >
              {hour.toString().padStart(2, '0')}
            </div>
          ))}
        </div>

        {/* Heatmap grid */}
        {HF_BANDS.map((band, bandIdx) => (
          <div key={band.name} className="flex gap-0.5">
            {/* Band label */}
            <div className="w-12 flex-shrink-0 flex items-center justify-end pr-2">
              <span className="text-xs font-mono font-bold text-accent-primary">
                {band.name}
              </span>
            </div>

            {/* Hour cells */}
            {data[bandIdx].map((reliability, hourIdx) => (
              <div
                key={hourIdx}
                className="flex-1 min-w-[16px] h-6 rounded-sm relative group cursor-pointer transition-transform hover:scale-110"
                style={{
                  backgroundColor: getReliabilityColor(reliability),
                  opacity: reliability < 20 ? 0.3 : 0.8,
                  border: hourIdx === currentHour ? '2px solid #fff' : 'none',
                }}
                title={`${band.name} @ ${hourIdx}:00 UTC - ${reliability.toFixed(0)}%`}
              >
                {/* Tooltip on hover */}
                <div className="absolute bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-2 py-1 bg-dark-800 border border-glass-100 rounded text-[10px] whitespace-nowrap opacity-0 group-hover:opacity-100 pointer-events-none z-10">
                  {reliability.toFixed(0)}%
                </div>
              </div>
            ))}
          </div>
        ))}
      </div>

      {/* Legend */}
      <div className="flex items-center justify-center gap-4 text-[10px] text-gray-400 mt-2">
        <div className="flex items-center gap-1">
          <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: '#22c55e' }} />
          <span>Poor</span>
        </div>
        <div className="flex items-center gap-1">
          <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: '#fbbf24' }} />
          <span>Fair</span>
        </div>
        <div className="flex items-center gap-1">
          <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: '#ef4444' }} />
          <span>Excellent</span>
        </div>
      </div>
    </div>
  );
}

// View 2: Bar Chart
function BarChartView({ data }: { data: number[][] }) {
  const currentHour = new Date().getUTCHours();

  // Get current hour reliability for each band
  const currentReliability = data.map((bandData) => bandData[currentHour]);

  return (
    <div className="flex flex-col gap-3 h-full p-4 justify-center">
      <div className="text-xs text-gray-400 text-center mb-2">
        Current Conditions ({currentHour.toString().padStart(2, '0')}:00 UTC)
      </div>

      <div className="flex gap-3 items-end justify-center h-64">
        {HF_BANDS.map((band, idx) => {
          const reliability = currentReliability[idx];
          const status = getBandStatus(reliability);
          const color = getReliabilityColor(reliability);
          const height = Math.max(10, (reliability / 100) * 100);

          return (
            <div key={band.name} className="flex flex-col items-center gap-2 flex-1 max-w-[60px]">
              {/* Bar */}
              <div className="w-full flex flex-col items-center justify-end" style={{ height: '200px' }}>
                <div
                  className="w-full rounded-t transition-all duration-300 relative group cursor-pointer hover:opacity-90"
                  style={{
                    height: `${height}%`,
                    backgroundColor: color,
                    opacity: 0.9,
                  }}
                >
                  {/* Percentage label */}
                  <div className="absolute -top-6 left-1/2 transform -translate-x-1/2 text-xs font-mono font-bold text-white">
                    {reliability.toFixed(0)}%
                  </div>
                </div>
              </div>

              {/* Band label */}
              <div className="text-xs font-mono font-bold text-accent-primary">
                {band.name}
              </div>

              {/* Status label */}
              <div
                className="text-[10px] font-bold px-1.5 py-0.5 rounded"
                style={{
                  backgroundColor: `${color}20`,
                  color: color,
                  border: `1px solid ${color}40`,
                }}
              >
                {status}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// View 3: Band Conditions Grid
function BandConditionsView({ data }: { data: number[][] }) {
  const currentHour = new Date().getUTCHours();

  // Get current hour reliability for each band
  const currentReliability = data.map((bandData) => bandData[currentHour]);

  // Calculate MUF and LUF (mock calculations)
  const muf = useMemo(() => {
    const maxBandIdx = currentReliability.reduce((maxIdx, reliability, idx) => {
      return reliability > 50 && reliability > currentReliability[maxIdx] ? idx : maxIdx;
    }, 0);
    return HF_BANDS[maxBandIdx].freq.toFixed(1);
  }, [currentReliability]);

  const luf = useMemo(() => {
    const minBandIdx = currentReliability.findIndex((reliability) => reliability > 30);
    return minBandIdx >= 0 ? HF_BANDS[minBandIdx].freq.toFixed(1) : '3.5';
  }, [currentReliability]);

  return (
    <div className="flex flex-col gap-4 h-full p-4">
      <div className="text-xs text-gray-400 text-center">
        Current Band Conditions ({currentHour.toString().padStart(2, '0')}:00 UTC)
      </div>

      {/* MUF and LUF display */}
      <div className="grid grid-cols-2 gap-3">
        <div className="glass-panel p-3 text-center">
          <div className="text-xs text-gray-400 mb-1">MUF</div>
          <div className="text-2xl font-mono font-bold text-accent-primary">{muf} MHz</div>
          <div className="text-[10px] text-gray-500 mt-1">Maximum Usable Frequency</div>
        </div>
        <div className="glass-panel p-3 text-center">
          <div className="text-xs text-gray-400 mb-1">LUF</div>
          <div className="text-2xl font-mono font-bold text-accent-secondary">{luf} MHz</div>
          <div className="text-[10px] text-gray-500 mt-1">Lowest Usable Frequency</div>
        </div>
      </div>

      {/* Band condition tiles */}
      <div className="grid grid-cols-3 gap-2 flex-1">
        {HF_BANDS.map((band, idx) => {
          const reliability = currentReliability[idx];
          const status = getBandStatus(reliability);
          const color = getStatusColor(status);

          return (
            <div
              key={band.name}
              className="glass-panel p-3 flex flex-col items-center justify-center gap-2 cursor-pointer transition-all hover:scale-105"
              style={{
                backgroundColor: `${color}15`,
                borderColor: `${color}40`,
              }}
            >
              <div className="text-xl font-mono font-bold text-accent-primary">
                {band.name}
              </div>
              <div
                className="text-sm font-bold px-2 py-1 rounded"
                style={{
                  backgroundColor: `${color}30`,
                  color: color,
                }}
              >
                {status}
              </div>
              <div className="text-xs font-mono text-gray-400">
                {reliability.toFixed(0)}%
              </div>
            </div>
          );
        })}
      </div>

      <div className="text-[10px] text-gray-500 text-center">
        Predictions update when DX target changes
      </div>
    </div>
  );
}

// Main component with view cycling
export function PropagationPanelPlugin() {
  const [currentView, setCurrentView] = useState<ViewType>('heatmap');

  // Generate mock data (in production, fetch from VOACAP API based on station and target)
  const propagationData = useMemo(() => generateMockPropagationData(), []);

  const views: ViewType[] = ['heatmap', 'bar-chart', 'band-conditions'];
  const viewTitles = {
    'heatmap': 'VOACAP Heatmap',
    'bar-chart': 'Bar Chart',
    'band-conditions': 'Band Conditions',
  };

  const currentIndex = views.indexOf(currentView);

  const nextView = useCallback(() => {
    setCurrentView(views[(currentIndex + 1) % views.length]);
  }, [currentIndex, views]);

  const prevView = useCallback(() => {
    setCurrentView(views[(currentIndex - 1 + views.length) % views.length]);
  }, [currentIndex, views]);

  return (
    <GlassPanel
      title="Propagation"
      icon={<Radio className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          <button
            onClick={prevView}
            className="glass-button p-1"
            title="Previous view"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="text-sm text-gray-400 min-w-[140px] text-center">
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
      <div className="h-full overflow-auto">
        {currentView === 'heatmap' && <VOACAPHeatmapView data={propagationData} />}
        {currentView === 'bar-chart' && <BarChartView data={propagationData} />}
        {currentView === 'band-conditions' && <BandConditionsView data={propagationData} />}
      </div>
    </GlassPanel>
  );
}
