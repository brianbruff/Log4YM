import { useState, useMemo } from 'react';
import { Zap, Map, Settings, Plus, Trash2, X, Search } from 'lucide-react';
import { AgGridReact } from 'ag-grid-react';
import { ColDef, ICellRendererParams, RowClickedEvent, CellMouseOverEvent, CellMouseOutEvent } from 'ag-grid-community';
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import { MultiSelectDropdown, MultiSelectOption } from '../components/MultiSelectDropdown';
import { getCountryFlag } from '../core/countryFlags';
import { useSettingsStore, ClusterConnection } from '../store/settingsStore';
import { useAppStore, Spot } from '../store/appStore';

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

const BAND_OPTIONS: MultiSelectOption[] = [
  { value: '160m', label: '160m' },
  { value: '80m', label: '80m' },
  { value: '60m', label: '60m' },
  { value: '40m', label: '40m' },
  { value: '30m', label: '30m' },
  { value: '20m', label: '20m' },
  { value: '17m', label: '17m' },
  { value: '15m', label: '15m' },
  { value: '12m', label: '12m' },
  { value: '10m', label: '10m' },
  { value: '6m', label: '6m' },
];

const MODE_OPTIONS: MultiSelectOption[] = [
  { value: 'CW', label: 'CW' },
  { value: 'SSB', label: 'SSB' },
  { value: 'FT8', label: 'FT8' },
  { value: 'FT4', label: 'FT4' },
  { value: 'RTTY', label: 'RTTY' },
  { value: 'DIGI', label: 'Digital' },
];

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

  if (!mode) return <span className="text-dark-300">?</span>;

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
      default: return 'bg-dark-600 text-dark-200';
    }
  };
  return <span className={`badge text-xs ${getModeClass(mode)}`}>{displayMode(mode)}</span>;
};

// Custom cell renderer for frequency
const FrequencyCellRenderer = (props: ICellRendererParams<Spot>) => {
  return <span className="frequency-display font-mono text-accent-info text-sm">{formatFrequency(props.value)}</span>;
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
    <div className="flex items-center gap-2 text-dark-300">
      <span className="font-mono">{formatTime(time)}</span>
      <span className="text-xs text-dark-400">({age})</span>
    </div>
  );
};

// Cluster connection status type
type ClusterStatusType = 'connected' | 'connecting' | 'disconnected' | 'error';

// Cluster Settings Panel Component
function ClusterSettingsPanel({
  connections,
  onUpdateConnection,
  onAddConnection,
  onRemoveConnection,
  onConnect,
  onDisconnect,
  statuses,
  stationCallsign,
}: {
  connections: ClusterConnection[];
  onUpdateConnection: (id: string, updates: Partial<ClusterConnection>) => void;
  onAddConnection: () => void;
  onRemoveConnection: (id: string) => void;
  onConnect: (id: string) => void;
  onDisconnect: (id: string) => void;
  statuses: Record<string, ClusterStatusType>;
  stationCallsign: string;
}) {
  const canAddMore = connections.length < 4;

  const getStatusColor = (status?: ClusterStatusType) => {
    switch (status) {
      case 'connected': return 'bg-accent-success';
      case 'connecting': return 'bg-accent-primary animate-pulse';
      case 'error': return 'bg-accent-danger';
      default: return 'bg-dark-400';
    }
  };

  const getStatusText = (status?: ClusterStatusType) => {
    switch (status) {
      case 'connected': return 'Connected';
      case 'connecting': return 'Connecting...';
      case 'error': return 'Error';
      default: return 'Disconnected';
    }
  };

  return (
    <div className="space-y-4">
      {connections.map((conn, index) => {
        const status = statuses[conn.id];
        const isConnected = status === 'connected';
        const isConnecting = status === 'connecting';

        return (
          <div
            key={conn.id}
            className="p-4 bg-dark-800/50 border border-glass-100 rounded-lg space-y-3"
          >
            {/* Header with status and remove button */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className={`w-2.5 h-2.5 rounded-full ${getStatusColor(status)}`} />
                <span className="text-sm font-medium font-ui text-dark-200">
                  {conn.name || `Cluster ${index + 1}`}
                </span>
                <span className="text-xs text-dark-300">
                  ({getStatusText(status)})
                </span>
              </div>
              <button
                onClick={() => onRemoveConnection(conn.id)}
                className="p-1 text-dark-300 hover:text-accent-danger transition-colors"
                title="Remove cluster"
              >
                <Trash2 className="w-4 h-4" />
              </button>
            </div>

            {/* Configuration Grid */}
            <div className="grid grid-cols-2 gap-3">
              {/* Name */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">Name</label>
                <input
                  type="text"
                  value={conn.name}
                  onChange={(e) => onUpdateConnection(conn.id, { name: e.target.value })}
                  placeholder="Cluster name"
                  className="w-full px-2 py-1.5 bg-dark-900 border border-glass-100 rounded text-sm text-dark-200 placeholder-dark-400 focus:outline-none focus:border-accent-primary/50"
                />
              </div>

              {/* Host */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">Host</label>
                <input
                  type="text"
                  value={conn.host}
                  onChange={(e) => onUpdateConnection(conn.id, { host: e.target.value })}
                  placeholder="e.g., ve7cc.net"
                  className="w-full px-2 py-1.5 bg-dark-900 border border-glass-100 rounded text-sm text-dark-200 placeholder-dark-400 focus:outline-none focus:border-accent-primary/50"
                />
              </div>

              {/* Port */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">Port</label>
                <input
                  type="number"
                  value={conn.port}
                  onChange={(e) => onUpdateConnection(conn.id, { port: parseInt(e.target.value) || 23 })}
                  className="w-full px-2 py-1.5 bg-dark-900 border border-glass-100 rounded text-sm font-mono text-dark-200 focus:outline-none focus:border-accent-primary/50"
                />
              </div>

              {/* Callsign */}
              <div>
                <label className="block text-xs font-ui text-dark-300 mb-1">
                  Callsign <span className="text-dark-400">(blank = station)</span>
                </label>
                <input
                  type="text"
                  value={conn.callsign || ''}
                  onChange={(e) => onUpdateConnection(conn.id, { callsign: e.target.value || null })}
                  placeholder={stationCallsign || 'Your callsign'}
                  className="w-full px-2 py-1.5 bg-dark-900 border border-glass-100 rounded text-sm font-mono text-dark-200 placeholder-dark-400 focus:outline-none focus:border-accent-primary/50"
                />
              </div>
            </div>

            {/* Options Row */}
            <div className="flex items-center justify-between pt-2 border-t border-glass-100">
              <div className="flex items-center gap-4">
                {/* Enabled Toggle */}
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={conn.enabled}
                    onChange={(e) => onUpdateConnection(conn.id, { enabled: e.target.checked })}
                    className="w-4 h-4 rounded border-glass-200 bg-dark-900 text-accent-primary focus:ring-accent-primary/40"
                  />
                  <span className="text-sm font-ui text-dark-300">Enabled</span>
                </label>

                {/* Auto-Reconnect Toggle */}
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={conn.autoReconnect}
                    onChange={(e) => onUpdateConnection(conn.id, { autoReconnect: e.target.checked })}
                    className="w-4 h-4 rounded border-glass-200 bg-dark-900 text-accent-primary focus:ring-accent-primary/40"
                  />
                  <span className="text-sm font-ui text-dark-300">Auto-reconnect</span>
                </label>
              </div>

              {/* Connect/Disconnect Button */}
              <button
                onClick={() => isConnected ? onDisconnect(conn.id) : onConnect(conn.id)}
                disabled={isConnecting || !conn.host}
                className={`
                  px-3 py-1.5 rounded text-sm font-medium font-ui transition-colors
                  ${isConnected
                    ? 'bg-accent-danger/20 text-accent-danger hover:bg-accent-danger/30'
                    : 'bg-accent-primary/20 text-accent-primary hover:bg-accent-primary/30'
                  }
                  disabled:opacity-50 disabled:cursor-not-allowed
                `}
              >
                {isConnecting ? 'Connecting...' : isConnected ? 'Disconnect' : 'Connect'}
              </button>
            </div>
          </div>
        );
      })}

      {/* Add Cluster Button */}
      {canAddMore && (
        <button
          onClick={onAddConnection}
          className="w-full flex items-center justify-center gap-2 px-4 py-3 border-2 border-dashed border-glass-100 rounded-lg text-dark-300 hover:border-accent-primary/50 hover:text-accent-primary transition-colors font-ui"
        >
          <Plus className="w-4 h-4" />
          Add Cluster (max 4)
        </button>
      )}

      {connections.length === 0 && (
        <div className="text-center py-6 text-dark-300">
          <p className="mb-2 font-ui">No cluster connections configured</p>
          <button
            onClick={onAddConnection}
            className="inline-flex items-center gap-2 px-4 py-2 bg-accent-primary/20 text-accent-primary rounded-lg hover:bg-accent-primary/30 transition-colors font-ui"
          >
            <Plus className="w-4 h-4" />
            Add your first cluster
          </button>
        </div>
      )}
    </div>
  );
}

export function ClusterPlugin() {
  const { selectSpot } = useSignalR();
  const [selectedBands, setSelectedBands] = useState<string[]>([]);
  const [selectedModes, setSelectedModes] = useState<string[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [showSettings, setShowSettings] = useState(false);

  // Get spots from app store (ephemeral, in-memory only)
  const spots = useAppStore((state) => state.dxClusterSpots);

  // DX Cluster map overlay state from appStore
  const dxClusterMapEnabled = useAppStore((state) => state.dxClusterMapEnabled);
  const setDxClusterMapEnabled = useAppStore((state) => state.setDxClusterMapEnabled);
  const setHoveredSpotId = useAppStore((state) => state.setHoveredSpotId);

  // Get cluster settings from store
  const {
    settings,
    updateClusterConnection,
    addClusterConnection,
    removeClusterConnection,
    saveSettings,
  } = useSettingsStore();

  const clusterConnections = settings.cluster.connections;
  const stationCallsign = settings.station.callsign;

  // Get cluster statuses from app store (populated via SignalR)
  const clusterStatusesFromStore = useAppStore((state) => state.clusterStatuses);

  // Convert to simple status map for the settings panel
  const clusterStatuses = useMemo(() => {
    const statuses: Record<string, ClusterStatusType> = {};
    for (const [id, status] of Object.entries(clusterStatusesFromStore)) {
      statuses[id] = status.status;
    }
    return statuses;
  }, [clusterStatusesFromStore]);

  // Filter spots based on selected bands, modes, and search query
  const filteredSpots = useMemo(() => {
    if (!spots) return [];

    const query = searchQuery.trim().toLowerCase();

    return spots.filter(spot => {
      // Fuzzy search filter - matches against multiple fields
      if (query) {
        const searchableText = [
          spot.dxCall,
          spot.spotter,
          spot.dxStation?.country || spot.country,
          spot.comment,
          getBandFromFrequency(spot.frequency),
          spot.mode,
        ].filter(Boolean).join(' ').toLowerCase();

        if (!searchableText.includes(query)) return false;
      }

      // Band filter
      if (selectedBands.length > 0) {
        const band = getBandFromFrequency(spot.frequency);
        if (!selectedBands.includes(band)) return false;
      }

      // Mode filter
      if (selectedModes.length > 0) {
        let spotMode = spot.mode?.toUpperCase();
        // Normalize USB/LSB to SSB
        if (spotMode === 'USB' || spotMode === 'LSB') spotMode = 'SSB';
        // Try to infer mode if not provided
        if (!spotMode) {
          spotMode = inferModeFromFrequency(spot.frequency)?.toUpperCase() || undefined;
        }
        if (!spotMode || !selectedModes.includes(spotMode)) return false;
      }

      return true;
    });
  }, [spots, selectedBands, selectedModes, searchQuery]);

  const handleRowClick = async (event: RowClickedEvent<Spot>) => {
    const spot = event.data;
    if (spot) {
      await selectSpot(spot.dxCall, spot.frequency, spot.mode);
    }
  };

  const handleUpdateConnection = (id: string, updates: Partial<ClusterConnection>) => {
    updateClusterConnection(id, updates);
  };

  const handleAddConnection = () => {
    addClusterConnection();
  };

  const handleRemoveConnection = (id: string) => {
    removeClusterConnection(id);
  };

  const handleConnect = async (id: string) => {
    // Save settings first, then trigger connect via API
    await saveSettings();
    try {
      await fetch(`/api/cluster/connect/${id}`, { method: 'POST' });
    } catch (error) {
      console.error('Failed to connect cluster:', error);
    }
  };

  const handleDisconnect = async (id: string) => {
    try {
      await fetch(`/api/cluster/disconnect/${id}`, { method: 'POST' });
    } catch (error) {
      console.error('Failed to disconnect cluster:', error);
    }
  };

  const clearAllFilters = () => {
    setSelectedBands([]);
    setSelectedModes([]);
    setSearchQuery('');
  };

  const hasActiveFilters = selectedBands.length > 0 || selectedModes.length > 0 || searchQuery.trim().length > 0;
  const totalActiveFilters = selectedBands.length + selectedModes.length + (searchQuery.trim() ? 1 : 0);

  // Count connected clusters
  const connectedCount = Object.values(clusterStatuses).filter(s => s === 'connected').length;

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
      cellClass: 'font-mono text-dark-300',
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
      cellClass: 'text-dark-300',
      width: 100,
      resizable: true,
    },
    {
      headerName: 'Spotter',
      field: 'spotter',
      cellClass: 'font-mono text-dark-300',
      width: 120,
      resizable: true,
      valueGetter: (params) => {
        const spotter = params.data?.spotter || '-';
        const grid = params.data?.spotterStation?.grid;
        return grid ? `${spotter} (${grid})` : spotter;
      },
    },
    {
      headerName: 'Comment',
      field: 'comment',
      cellClass: 'text-dark-300 truncate',
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
          {/* Connected clusters indicator */}
          {clusterConnections.length > 0 && (
            <span className="text-xs font-ui text-dark-300">
              {connectedCount}/{clusterConnections.length} connected
            </span>
          )}
          <span className="text-sm font-mono text-dark-300">
            {filteredSpots?.length || 0} spots
          </span>
          <button
            onClick={() => setDxClusterMapEnabled(!dxClusterMapEnabled)}
            className={`glass-button p-1.5 ${dxClusterMapEnabled ? 'text-accent-info' : 'text-dark-300'}`}
            title={dxClusterMapEnabled ? 'Hide map overlay' : 'Show map overlay'}
          >
            <Map className="w-4 h-4" />
          </button>
          <button
            onClick={() => setShowSettings(!showSettings)}
            className={`glass-button p-1.5 ${showSettings ? 'text-accent-primary' : 'text-dark-300'}`}
            title="Cluster settings"
          >
            <Settings className="w-4 h-4" />
          </button>
        </div>
      }
    >
      <div className="flex flex-col h-full relative">
        {/* Settings Overlay Panel — covers the full panel area, scrollable */}
        <div
          className={`
            absolute inset-0 z-20 flex flex-col
            bg-dark-900/97 backdrop-blur-sm
            transition-opacity duration-200
            ${showSettings ? 'opacity-100 pointer-events-auto' : 'opacity-0 pointer-events-none'}
          `}
        >
          {/* Overlay Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-glass-100 flex-shrink-0">
            <h4 className="text-sm font-medium font-ui text-dark-200">Cluster Connections</h4>
            <button
              onClick={() => setShowSettings(false)}
              className="p-1.5 text-dark-300 hover:text-dark-200 hover:bg-dark-700 rounded transition-colors"
              title="Close settings"
            >
              <X className="w-4 h-4" />
            </button>
          </div>

          {/* Scrollable Settings Content */}
          <div className="flex-1 overflow-y-auto p-4">
            <ClusterSettingsPanel
              connections={clusterConnections}
              onUpdateConnection={handleUpdateConnection}
              onAddConnection={handleAddConnection}
              onRemoveConnection={handleRemoveConnection}
              onConnect={handleConnect}
              onDisconnect={handleDisconnect}
              statuses={clusterStatuses}
              stationCallsign={stationCallsign}
            />
          </div>

          {/* Sticky Save Footer — always visible regardless of content height */}
          {clusterConnections.length > 0 && (
            <div className="flex-shrink-0 flex items-center justify-between px-4 py-3 border-t border-glass-100 bg-dark-900/80">
              <span className="text-xs text-dark-400 font-ui">Changes take effect after saving</span>
              <button
                onClick={async () => { await saveSettings(); setShowSettings(false); }}
                className="px-4 py-2 bg-accent-primary text-white rounded-lg text-sm font-medium font-ui hover:bg-accent-primary/80 transition-colors"
              >
                Save &amp; Close
              </button>
            </div>
          )}
        </div>

        {/* Filters */}
        <div className="p-4 space-y-4 flex-shrink-0">
          <div className="flex gap-3 items-center">
            {/* Fuzzy Search Input */}
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-dark-300" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search call, country, spotter..."
                className="w-full pl-9 pr-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 placeholder-dark-400 focus:outline-none focus:border-accent-primary/50"
              />
              {searchQuery && (
                <button
                  onClick={() => setSearchQuery('')}
                  className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-dark-300 hover:text-dark-200"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              )}
            </div>

            <MultiSelectDropdown
              options={BAND_OPTIONS}
              selected={selectedBands}
              onChange={setSelectedBands}
              placeholder="All Bands"
              className="w-32"
            />

            <MultiSelectDropdown
              options={MODE_OPTIONS}
              selected={selectedModes}
              onChange={setSelectedModes}
              placeholder="All Modes"
              className="w-32"
            />

            {/* Clear All Filters Button */}
            {hasActiveFilters && (
              <button
                onClick={clearAllFilters}
                className="flex items-center gap-1.5 px-3 py-2 bg-accent-warning/20 text-accent-warning rounded-lg text-sm font-ui hover:bg-accent-warning/30 transition-colors whitespace-nowrap"
                title="Clear all filters"
              >
                <X className="w-4 h-4" />
                <span>Clear ({totalActiveFilters})</span>
              </button>
            )}
          </div>
        </div>

        {/* AG Grid Table */}
        <div className="flex-1 px-4 pb-4 min-h-0">
          <div className="ag-theme-alpine-dark h-full">
            {filteredSpots?.length === 0 ? (
              <div className="text-center py-8 text-dark-300">
                {hasActiveFilters ? (
                  <>
                    <p>No spots match your filters</p>
                    <button
                      onClick={clearAllFilters}
                      className="mt-2 text-accent-primary hover:underline font-ui"
                    >
                      Clear filters
                    </button>
                  </>
                ) : (
                  'No spots available'
                )}
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
                onCellMouseOver={(event: CellMouseOverEvent<Spot>) => {
                  if (event.data?.id) setHoveredSpotId(event.data.id);
                }}
                onCellMouseOut={(_event: CellMouseOutEvent<Spot>) => {
                  setHoveredSpotId(null);
                }}
                rowClass="cursor-pointer hover:bg-dark-600/50"
                getRowId={(params) => params.data.id}
              />
            )}
          </div>
        </div>
      </div>
    </GlassPanel>
  );
}
