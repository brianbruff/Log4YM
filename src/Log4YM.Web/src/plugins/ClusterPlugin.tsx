import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Radio, Zap, Volume2, VolumeX, Filter } from 'lucide-react';
import { AgGridReact } from 'ag-grid-react';
import { ColDef, ICellRendererParams, RowClickedEvent } from 'ag-grid-community';
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';
import { api, Spot } from '../api/client';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import { getCountryFlag } from '../core/countryFlags';

const BAND_RANGES: Record<string, [number, number]> = {
  '160m': [1800, 2000],
  '80m': [3500, 4000],
  '40m': [7000, 7300],
  '30m': [10100, 10150],
  '20m': [14000, 14350],
  '17m': [18068, 18168],
  '15m': [21000, 21450],
  '12m': [24890, 24990],
  '10m': [28000, 29700],
  '6m': [50000, 54000],
};

const getBandFromFrequency = (freq: number): string => {
  for (const [band, [min, max]] of Object.entries(BAND_RANGES)) {
    if (freq >= min && freq <= max) {
      return band;
    }
  }
  return '?';
};

const formatFrequency = (freq: number) => {
  return (freq / 1000).toFixed(3);
};

const formatTime = (dateStr: string) => {
  if (!dateStr) return '--:--';
  try {
    const date = new Date(dateStr);
    if (isNaN(date.getTime())) return '--:--';
    return date.toISOString().slice(11, 16);
  } catch {
    return '--:--';
  }
};

const getAge = (dateStr: string) => {
  if (!dateStr) return '-';
  try {
    const now = new Date();
    const spotted = new Date(dateStr);
    if (isNaN(spotted.getTime())) return '-';
    const minutes = Math.floor((now.getTime() - spotted.getTime()) / 60000);
    if (minutes < 1) return 'now';
    if (minutes < 60) return `${minutes}m`;
    return `${Math.floor(minutes / 60)}h`;
  } catch {
    return '-';
  }
};

// Custom cell renderer for DX callsign
const DxCallCellRenderer = (props: ICellRendererParams<Spot>) => {
  return <span className="font-mono font-bold text-accent-primary">{props.value}</span>;
};

// Infer mode from frequency if not provided
const inferModeFromFrequency = (freq: number): string | null => {
  // Common FT8 frequencies (in kHz)
  const ft8Freqs = [1840, 3573, 7074, 10136, 14074, 18100, 21074, 24915, 28074, 50313];
  // Common FT4 frequencies
  const ft4Freqs = [3575, 7047, 10140, 14080, 18104, 21140, 24919, 28180];
  // Check if frequency is within 5 kHz of known digital frequencies
  for (const f of ft8Freqs) {
    if (Math.abs(freq - f) <= 5) return 'FT8';
  }
  for (const f of ft4Freqs) {
    if (Math.abs(freq - f) <= 5) return 'FT4';
  }
  // CW portions (lower part of bands)
  if ((freq >= 1800 && freq <= 1840) ||
      (freq >= 3500 && freq <= 3570) ||
      (freq >= 7000 && freq <= 7040) ||
      (freq >= 10100 && freq <= 10130) ||
      (freq >= 14000 && freq <= 14070) ||
      (freq >= 18068 && freq <= 18095) ||
      (freq >= 21000 && freq <= 21070) ||
      (freq >= 24890 && freq <= 24920) ||
      (freq >= 28000 && freq <= 28070)) {
    return 'CW';
  }
  // SSB portions (typically above CW/digital)
  if ((freq >= 1840 && freq <= 2000) ||
      (freq >= 3600 && freq <= 4000) ||
      (freq >= 7040 && freq <= 7300) ||
      (freq >= 14100 && freq <= 14350) ||
      (freq >= 18110 && freq <= 18168) ||
      (freq >= 21150 && freq <= 21450) ||
      (freq >= 24930 && freq <= 24990) ||
      (freq >= 28300 && freq <= 29700)) {
    return 'SSB';
  }
  return null;
};

// Custom cell renderer for mode badges
const ModeCellRenderer = (props: ICellRendererParams<Spot>) => {
  let mode = props.value;
  const freq = props.data?.frequency;

  // Try to infer mode if not provided
  if (!mode && freq) {
    mode = inferModeFromFrequency(freq);
  }

  if (!mode) return <span className="text-gray-500">?</span>;

  // Normalize mode display
  const displayMode = (m: string) => {
    const upper = m.toUpperCase();
    if (upper === 'USB' || upper === 'LSB') return 'SSB';
    return upper;
  };

  const getModeClass = (mode: string) => {
    switch (mode?.toUpperCase()) {
      case 'CW': return 'badge-cw';
      case 'SSB':
      case 'USB':
      case 'LSB': return 'badge-ssb';
      case 'FT8':
      case 'FT4': return 'badge-ft8';
      case 'RTTY':
      case 'PSK31': return 'badge-rtty';
      default: return 'bg-dark-600 text-gray-300';
    }
  };
  return <span className={`badge text-xs ${getModeClass(mode)}`}>{displayMode(mode)}</span>;
};

// Custom cell renderer for frequency
const FrequencyCellRenderer = (props: ICellRendererParams<Spot>) => {
  return <span className="frequency-display text-accent-info text-sm">{formatFrequency(props.value)}</span>;
};

// Custom cell renderer for country flag
const FlagCellRenderer = (props: ICellRendererParams<Spot>) => {
  const country = props.data?.dxStation?.country || props.data?.country;
  return <span className="text-lg">{getCountryFlag(country)}</span>;
};

// Custom cell renderer for time with age
const TimeCellRenderer = (props: ICellRendererParams<Spot>) => {
  // Use timestamp field (API returns 'timestamp', not 'time')
  const time = props.data?.timestamp || props.value;
  const age = getAge(time);
  return (
    <div className="flex items-center gap-2 text-gray-400">
      <span className="font-mono">{formatTime(time)}</span>
      <span className="text-xs text-gray-600">({age})</span>
    </div>
  );
};

export function ClusterPlugin() {
  const { selectSpot } = useSignalR();
  const [selectedBand, setSelectedBand] = useState<string>('');
  const [selectedMode, setSelectedMode] = useState<string>('');
  const [soundEnabled, setSoundEnabled] = useState(false);

  const { data: spots, isLoading } = useQuery({
    queryKey: ['spots', selectedBand, selectedMode],
    queryFn: () => api.getSpots({
      band: selectedBand || undefined,
      mode: selectedMode || undefined,
      limit: 100,
    }),
    refetchInterval: 30000,
  });

  const filteredSpots = spots?.filter(spot => {
    if (selectedBand) {
      const band = getBandFromFrequency(spot.frequency);
      if (band !== selectedBand) return false;
    }
    if (selectedMode && spot.mode?.toUpperCase() !== selectedMode.toUpperCase()) {
      return false;
    }
    return true;
  });

  const handleRowClick = async (event: RowClickedEvent<Spot>) => {
    const spot = event.data;
    if (spot) {
      await selectSpot(spot.dxCall, spot.frequency, spot.mode);
    }
  };

  const columnDefs = useMemo<ColDef<Spot>[]>(() => [
    {
      headerName: 'Time',
      field: 'timestamp',
      cellRenderer: TimeCellRenderer,
      width: 110,
      resizable: true,
    },
    {
      headerName: 'DX Call',
      field: 'dxCall',
      cellRenderer: DxCallCellRenderer,
      width: 110,
      resizable: true,
    },
    {
      headerName: 'Freq',
      field: 'frequency',
      cellRenderer: FrequencyCellRenderer,
      width: 90,
      resizable: true,
    },
    {
      headerName: 'Band',
      field: 'frequency',
      valueGetter: (params) => getBandFromFrequency(params.data?.frequency || 0),
      cellClass: 'font-mono text-gray-400',
      width: 60,
      resizable: true,
    },
    {
      headerName: 'Mode',
      field: 'mode',
      cellRenderer: ModeCellRenderer,
      width: 70,
      resizable: true,
    },
    {
      headerName: '',
      field: 'country',
      cellRenderer: FlagCellRenderer,
      width: 45,
      resizable: false,
      sortable: false,
    },
    {
      headerName: 'Country',
      valueGetter: (params) => params.data?.dxStation?.country || params.data?.country || '-',
      cellClass: 'text-gray-400',
      width: 100,
      resizable: true,
    },
    {
      headerName: 'Spotter',
      field: 'spotter',
      cellClass: 'font-mono text-gray-500',
      width: 100,
      resizable: true,
    },
    {
      headerName: 'Comment',
      field: 'comment',
      cellClass: 'text-gray-500 truncate',
      flex: 1,
      minWidth: 100,
      resizable: true,
    },
  ], []);

  const defaultColDef = useMemo<ColDef>(() => ({
    sortable: true,
    resizable: true,
  }), []);

  return (
    <GlassPanel
      title="DX Cluster"
      icon={<Zap className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          <span className="text-sm text-gray-400">
            {filteredSpots?.length || 0} spots
          </span>
          <button
            onClick={() => setSoundEnabled(!soundEnabled)}
            className={`glass-button p-1.5 ${soundEnabled ? 'text-accent-success' : 'text-gray-500'}`}
            title={soundEnabled ? 'Disable alerts' : 'Enable alerts'}
          >
            {soundEnabled ? <Volume2 className="w-4 h-4" /> : <VolumeX className="w-4 h-4" />}
          </button>
        </div>
      }
    >
      <div className="p-4 space-y-4">
        {/* Filters */}
        <div className="flex gap-3">
          <select
            value={selectedBand}
            onChange={(e) => setSelectedBand(e.target.value)}
            className="glass-input flex-1"
          >
            <option value="">All Bands</option>
            <option value="160m">160m</option>
            <option value="80m">80m</option>
            <option value="40m">40m</option>
            <option value="30m">30m</option>
            <option value="20m">20m</option>
            <option value="17m">17m</option>
            <option value="15m">15m</option>
            <option value="12m">12m</option>
            <option value="10m">10m</option>
            <option value="6m">6m</option>
          </select>

          <select
            value={selectedMode}
            onChange={(e) => setSelectedMode(e.target.value)}
            className="glass-input flex-1"
          >
            <option value="">All Modes</option>
            <option value="CW">CW</option>
            <option value="SSB">SSB</option>
            <option value="FT8">FT8</option>
            <option value="RTTY">RTTY</option>
          </select>

          <button className="glass-button p-2" title="Advanced filters">
            <Filter className="w-4 h-4" />
          </button>
        </div>

        {/* AG Grid Table */}
        <div className="ag-theme-alpine-dark h-[calc(100vh-280px)]">
          {isLoading ? (
            <div className="flex items-center justify-center py-8 text-gray-500">
              <Radio className="w-4 h-4 animate-spin mr-2" />
              Loading spots...
            </div>
          ) : filteredSpots?.length === 0 ? (
            <div className="text-center py-8 text-gray-500">
              No spots available
            </div>
          ) : (
            <AgGridReact<Spot>
              rowData={filteredSpots}
              columnDefs={columnDefs}
              defaultColDef={defaultColDef}
              rowHeight={36}
              headerHeight={40}
              suppressCellFocus={true}
              animateRows={true}
              onRowClicked={handleRowClick}
              rowClass="cursor-pointer hover:bg-dark-600/50"
              getRowId={(params) => params.data.id}
            />
          )}
        </div>
      </div>
    </GlassPanel>
  );
}
