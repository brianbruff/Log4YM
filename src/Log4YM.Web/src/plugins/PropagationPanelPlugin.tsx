import { useState, useCallback, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Radio, ChevronLeft, ChevronRight, Loader2, MapPin } from 'lucide-react';
import { GlassPanel } from '../components/GlassPanel';
import { useSettingsStore } from '../store/settingsStore';
import { useAppStore } from '../store/appStore';
import { api, type GenericBandConditions } from '../api/client';

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

// Convert N0NBH generic conditions to a simplified heatmap
function buildHeatmapFromGeneric(conditions: GenericBandConditions): number[][] {
  const bandOrder = ['80m', '60m', '40m', '30m', '20m', '17m', '15m', '12m', '10m'];

  return bandOrder.map((bandName) => {
    const entry = conditions.bands.find((b) => b.band === bandName);
    const dayRel = statusToReliability(entry?.dayStatus ?? 'Fair');
    const nightRel = statusToReliability(entry?.nightStatus ?? 'Fair');

    return Array.from({ length: 24 }, (_, hour) => {
      // Smooth day/night transitions (day = 06-18 UTC roughly)
      if (hour >= 7 && hour <= 16) return dayRel;
      if (hour <= 4 || hour >= 20) return nightRel;
      // Dawn/dusk transitions
      const blend = hour < 7 ? (hour - 4) / 3 : (20 - hour) / 3;
      return Math.round(nightRel + (dayRel - nightRel) * blend);
    });
  });
}

function statusToReliability(status: string): number {
  switch (status.toLowerCase()) {
    case 'good': return 75;
    case 'fair': return 50;
    case 'poor': return 25;
    default: return 10;
  }
}

// Color mapping for reliability (0-100)
function getReliabilityColor(reliability: number): string {
  if (reliability >= 75) return '#22c55e'; // green (excellent)
  if (reliability >= 60) return '#a3e635'; // lime
  if (reliability >= 45) return '#fbbf24'; // yellow
  if (reliability >= 30) return '#f97316'; // orange
  return '#ef4444'; // red (poor)
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
    case 'EXCELLENT': return '#22c55e';
    case 'GOOD': return '#a3e635';
    case 'FAIR': return '#fbbf24';
    case 'POOR': return '#f97316';
    case 'CLOSED': return '#6b7280';
  }
}

// View 1: Heatmap
function HeatmapView({ data, bandNames }: { data: number[][]; bandNames: string[] }) {
  const currentHour = new Date().getUTCHours();

  return (
    <div className="flex flex-col gap-2 h-full p-2">
      <div className="text-xs text-gray-400 text-center mb-1">
        24-Hour Propagation Forecast (UTC)
      </div>

      <div className="flex-1 flex flex-col gap-1 overflow-auto">
        {/* Header row - UTC hours */}
        <div className="flex gap-0.5">
          <div className="w-12 flex-shrink-0" />
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
        {bandNames.map((bandName, bandIdx) => (
          <div key={bandName} className="flex gap-0.5">
            <div className="w-12 flex-shrink-0 flex items-center justify-end pr-2">
              <span className="text-xs font-mono font-bold text-accent-primary">
                {bandName}
              </span>
            </div>

            {data[bandIdx]?.map((reliability, hourIdx) => (
              <div
                key={hourIdx}
                className="flex-1 min-w-[16px] h-6 rounded-sm relative group cursor-pointer transition-transform hover:scale-110"
                style={{
                  backgroundColor: getReliabilityColor(reliability),
                  opacity: reliability < 20 ? 0.3 : 0.8,
                  border: hourIdx === currentHour ? '2px solid #fff' : 'none',
                }}
                title={`${bandName} @ ${hourIdx}:00 UTC - ${reliability}%`}
              >
                <div className="absolute bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-2 py-1 bg-dark-800 border border-glass-100 rounded text-[10px] whitespace-nowrap opacity-0 group-hover:opacity-100 pointer-events-none z-10">
                  {reliability}%
                </div>
              </div>
            ))}
          </div>
        ))}
      </div>

      {/* Legend */}
      <div className="flex items-center justify-center gap-4 text-[10px] text-gray-400 mt-2">
        <div className="flex items-center gap-1">
          <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: '#ef4444' }} />
          <span>Poor</span>
        </div>
        <div className="flex items-center gap-1">
          <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: '#fbbf24' }} />
          <span>Fair</span>
        </div>
        <div className="flex items-center gap-1">
          <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: '#22c55e' }} />
          <span>Excellent</span>
        </div>
      </div>
    </div>
  );
}

// View 2: Bar Chart
function BarChartView({ data, bandNames }: { data: number[][]; bandNames: string[] }) {
  const currentHour = new Date().getUTCHours();
  const currentReliability = data.map((bandData) => bandData[currentHour]);

  return (
    <div className="flex flex-col gap-3 h-full p-4 justify-center">
      <div className="text-xs text-gray-400 text-center mb-2">
        Current Conditions ({currentHour.toString().padStart(2, '0')}:00 UTC)
      </div>

      <div className="flex gap-3 items-end justify-center h-64">
        {bandNames.map((bandName, idx) => {
          const reliability = currentReliability[idx];
          const status = getBandStatus(reliability);
          const color = getReliabilityColor(reliability);
          const height = Math.max(10, (reliability / 100) * 100);

          return (
            <div key={bandName} className="flex flex-col items-center gap-2 flex-1 max-w-[60px]">
              <div className="w-full flex flex-col items-center justify-end" style={{ height: '200px' }}>
                <div
                  className="w-full rounded-t transition-all duration-300 relative group cursor-pointer hover:opacity-90"
                  style={{
                    height: `${height}%`,
                    backgroundColor: color,
                    opacity: 0.9,
                  }}
                >
                  <div className="absolute -top-6 left-1/2 transform -translate-x-1/2 text-xs font-mono font-bold text-white">
                    {reliability}%
                  </div>
                </div>
              </div>

              <div className="text-xs font-mono font-bold text-accent-primary">
                {bandName}
              </div>

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
function BandConditionsView({
  data,
  bandNames,
  muf,
  luf,
}: {
  data: number[][];
  bandNames: string[];
  muf?: number;
  luf?: number;
}) {
  const currentHour = new Date().getUTCHours();
  const currentReliability = data.map((bandData) => bandData[currentHour]);

  // Use server-provided MUF/LUF if available, otherwise estimate from heatmap
  const displayMuf = useMemo(() => {
    if (muf != null) return muf.toFixed(1);
    const maxBandIdx = currentReliability.reduce((maxIdx, rel, idx) => {
      return rel > 50 && rel > currentReliability[maxIdx] ? idx : maxIdx;
    }, 0);
    return HF_BANDS[maxBandIdx].freq.toFixed(1);
  }, [muf, currentReliability]);

  const displayLuf = useMemo(() => {
    if (luf != null) return luf.toFixed(1);
    const minBandIdx = currentReliability.findIndex((rel) => rel > 30);
    return minBandIdx >= 0 ? HF_BANDS[minBandIdx].freq.toFixed(1) : '3.5';
  }, [luf, currentReliability]);

  return (
    <div className="flex flex-col gap-4 h-full p-4">
      <div className="text-xs text-gray-400 text-center">
        Current Band Conditions ({currentHour.toString().padStart(2, '0')}:00 UTC)
      </div>

      {/* MUF and LUF display */}
      <div className="grid grid-cols-2 gap-3">
        <div className="glass-panel p-3 text-center">
          <div className="text-xs text-gray-400 mb-1">MUF</div>
          <div className="text-2xl font-mono font-bold text-accent-primary">{displayMuf} MHz</div>
          <div className="text-[10px] text-gray-500 mt-1">Maximum Usable Frequency</div>
        </div>
        <div className="glass-panel p-3 text-center">
          <div className="text-xs text-gray-400 mb-1">LUF</div>
          <div className="text-2xl font-mono font-bold text-accent-secondary">{displayLuf} MHz</div>
          <div className="text-[10px] text-gray-500 mt-1">Lowest Usable Frequency</div>
        </div>
      </div>

      {/* Band condition tiles */}
      <div className="grid grid-cols-3 gap-2 flex-1">
        {bandNames.map((bandName, idx) => {
          const reliability = currentReliability[idx];
          const status = getBandStatus(reliability);
          const color = getStatusColor(status);

          return (
            <div
              key={bandName}
              className="glass-panel p-3 flex flex-col items-center justify-center gap-2 cursor-pointer transition-all hover:scale-105"
              style={{
                backgroundColor: `${color}15`,
                borderColor: `${color}40`,
              }}
            >
              <div className="text-xl font-mono font-bold text-accent-primary">
                {bandName}
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
                {reliability}%
              </div>
            </div>
          );
        })}
      </div>

      <div className="text-[10px] text-gray-500 text-center">
        {muf != null ? 'Path-specific prediction' : 'General conditions from N0NBH'}
      </div>
    </div>
  );
}

// Main component with view cycling
export function PropagationPanelPlugin() {
  const [currentView, setCurrentView] = useState<ViewType>('heatmap');

  // Get DE station location
  const station = useSettingsStore((s) => s.settings.station);
  const hasStationLocation = station.latitude != null && station.longitude != null;

  // Get DX target from focused callsign
  const focusedCallsignInfo = useAppStore((s) => s.focusedCallsignInfo);
  const focusedCallsign = useAppStore((s) => s.focusedCallsign);
  const hasDxTarget = !!(focusedCallsignInfo?.latitude != null && focusedCallsignInfo?.longitude != null);

  // Fetch path-specific propagation when we have both DE and DX
  const { data: prediction, isLoading: isPredictionLoading } = useQuery({
    queryKey: ['propagation', focusedCallsignInfo?.latitude, focusedCallsignInfo?.longitude],
    queryFn: () => api.getPropagation(
      focusedCallsignInfo!.latitude!,
      focusedCallsignInfo!.longitude!,
    ),
    enabled: hasStationLocation && hasDxTarget,
    refetchInterval: 5 * 60 * 1000,
    staleTime: 2 * 60 * 1000,
  });

  // Fetch generic conditions when no DX target
  const { data: genericConditions, isLoading: isGenericLoading } = useQuery({
    queryKey: ['propagation-conditions'],
    queryFn: () => api.getGenericConditions(),
    enabled: !hasDxTarget,
    refetchInterval: 15 * 60 * 1000,
    staleTime: 10 * 60 * 1000,
  });

  const isLoading = hasDxTarget ? isPredictionLoading : isGenericLoading;

  // Derive heatmap data and band names from API response
  const { heatmapData, bandNames } = useMemo(() => {
    if (prediction) {
      return { heatmapData: prediction.heatmapData, bandNames: prediction.bandNames };
    }
    if (genericConditions) {
      return {
        heatmapData: buildHeatmapFromGeneric(genericConditions),
        bandNames: HF_BANDS.map((b) => b.name),
      };
    }
    return { heatmapData: null, bandNames: HF_BANDS.map((b) => b.name) };
  }, [prediction, genericConditions]);

  // Subtitle
  const subtitle = hasDxTarget && prediction
    ? `${station.callsign || 'DE'} \u2192 ${focusedCallsign || 'DX'} (${prediction.distanceKm.toLocaleString()} km)`
    : 'General Conditions';

  const views: ViewType[] = ['heatmap', 'bar-chart', 'band-conditions'];
  const viewTitles: Record<ViewType, string> = {
    'heatmap': 'Heatmap',
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
        {!hasStationLocation ? (
          <div className="flex flex-col items-center justify-center h-full gap-3 text-gray-400 p-6">
            <MapPin className="w-8 h-8 opacity-50" />
            <div className="text-sm text-center">
              Set your station location in Settings to enable propagation predictions.
            </div>
          </div>
        ) : isLoading ? (
          <div className="flex flex-col items-center justify-center h-full gap-3 text-gray-400">
            <Loader2 className="w-6 h-6 animate-spin" />
            <div className="text-sm">Loading propagation data...</div>
          </div>
        ) : heatmapData ? (
          <>
            <div className="text-[10px] text-gray-500 text-center py-1">{subtitle}</div>
            {currentView === 'heatmap' && (
              <HeatmapView data={heatmapData} bandNames={bandNames} />
            )}
            {currentView === 'bar-chart' && (
              <BarChartView data={heatmapData} bandNames={bandNames} />
            )}
            {currentView === 'band-conditions' && (
              <BandConditionsView
                data={heatmapData}
                bandNames={bandNames}
                muf={prediction?.mufMHz}
                luf={prediction?.lufMHz}
              />
            )}
          </>
        ) : (
          <div className="flex items-center justify-center h-full text-sm text-gray-400">
            Unable to load propagation data.
          </div>
        )}
      </div>
    </GlassPanel>
  );
}
