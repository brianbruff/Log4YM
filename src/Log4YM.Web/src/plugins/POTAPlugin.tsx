import { useMemo, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { MapPin, Radio, Map } from 'lucide-react';
import { AgGridReact } from 'ag-grid-react';
import { ColDef, ICellRendererParams, RowClickedEvent } from 'ag-grid-community';
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';
import { api, PotaSpot } from '../api/client';
import { useSignalR } from '../hooks/useSignalR';
import { GlassPanel } from '../components/GlassPanel';
import { useAppStore } from '../store/appStore';
import { useSettingsStore } from '../store/settingsStore';

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

// Custom cell renderer for activator callsign (green)
const ActivatorCellRenderer = (props: ICellRendererParams<PotaSpot>) => {
  return <span className="font-mono font-bold text-accent-success">{props.value}</span>;
};

// Custom cell renderer for park reference
const ReferenceCellRenderer = (props: ICellRendererParams<PotaSpot>) => {
  return <span className="font-mono text-accent-info">{props.value}</span>;
};

// Custom cell renderer for frequency
const FrequencyCellRenderer = (props: ICellRendererParams<PotaSpot>) => {
  const freq = parseFloat(props.value);
  if (isNaN(freq)) return <span className="text-gray-500">{props.value}</span>;
  return <span className="font-mono text-accent-warning">{(freq / 1000).toFixed(3)}</span>;
};

// Custom cell renderer for time with age
const TimeCellRenderer = (props: ICellRendererParams<PotaSpot>) => {
  const time = props.data?.spotTime || '';
  const age = getAge(time);
  return (
    <div className="flex items-center gap-2 text-gray-400">
      <span className="font-mono">{formatTime(time)}</span>
      <span className="text-xs text-gray-600">({age})</span>
    </div>
  );
};

export function POTAPlugin() {
  const { selectSpot } = useSignalR();
  const { setPotaSpots } = useAppStore();
  const { settings, updateMapSettings, saveSettings } = useSettingsStore();

  const { data: spots, isLoading } = useQuery({
    queryKey: ['pota-spots'],
    queryFn: () => api.getPotaSpots(),
    refetchInterval: 60000, // Refresh every minute
  });

  // Filter and sort spots
  const filteredSpots = useMemo(() => {
    if (!spots) return [];

    const now = new Date().getTime();
    const oneHourAgo = now - 60 * 60 * 1000;

    return spots
      .filter(spot => {
        // Filter out invalid spots
        if (spot.invalid) return false;

        // Filter out spots older than 1 hour
        try {
          const spotTime = new Date(spot.spotTime).getTime();
          if (spotTime < oneHourAgo) return false;
        } catch {
          return false;
        }

        return true;
      })
      .sort((a, b) => {
        // Sort newest first
        try {
          return new Date(b.spotTime).getTime() - new Date(a.spotTime).getTime();
        } catch {
          return 0;
        }
      });
  }, [spots]);

  // Sync filtered spots to app store for map visualization
  useEffect(() => {
    setPotaSpots(filteredSpots);
  }, [filteredSpots, setPotaSpots]);

  const handleRowClick = async (event: RowClickedEvent<PotaSpot>) => {
    const spot = event.data;
    if (spot) {
      const freqKhz = parseFloat(spot.frequency);
      await selectSpot(spot.activator, freqKhz, spot.mode);
    }
  };

  const columnDefs = useMemo<ColDef<PotaSpot>[]>(() => [
    {
      headerName: 'Time',
      field: 'spotTime',
      cellRenderer: TimeCellRenderer,
      width: 110,
      resizable: true,
    },
    {
      headerName: 'Activator',
      field: 'activator',
      cellRenderer: ActivatorCellRenderer,
      width: 120,
      resizable: true,
    },
    {
      headerName: 'Park',
      field: 'reference',
      cellRenderer: ReferenceCellRenderer,
      width: 100,
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
      headerName: 'Mode',
      field: 'mode',
      cellClass: 'font-mono text-gray-400',
      width: 70,
      resizable: true,
    },
    {
      headerName: 'Location',
      field: 'locationDesc',
      cellClass: 'text-gray-500 truncate',
      flex: 1,
      minWidth: 150,
      resizable: true,
    },
    {
      headerName: 'Comments',
      field: 'comments',
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
      title="POTA Activators"
      icon={<MapPin className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2">
          <span className="text-sm text-gray-400">
            {filteredSpots?.length || 0} active
          </span>
          <button
            onClick={() => {
              updateMapSettings({ showPotaOverlay: !settings.map.showPotaOverlay });
              saveSettings();
            }}
            className={`glass-button p-1.5 ${settings.map.showPotaOverlay ? 'text-accent-info' : 'text-dark-300'}`}
            title={settings.map.showPotaOverlay ? 'Hide map overlay' : 'Show map overlay'}
          >
            <Map className="w-4 h-4" />
          </button>
        </div>
      }
    >
      <div className="flex flex-col h-full">
        {/* AG Grid Table */}
        <div className="flex-1 px-4 pb-4 min-h-0">
          <div className="ag-theme-alpine-dark h-full">
            {isLoading ? (
              <div className="flex items-center justify-center py-8 text-gray-500">
                <Radio className="w-4 h-4 animate-spin mr-2" />
                Loading activators...
              </div>
            ) : filteredSpots?.length === 0 ? (
              <div className="text-center py-8 text-gray-500">
                No active POTA activators
              </div>
            ) : (
              <AgGridReact<PotaSpot>
                rowData={filteredSpots}
                columnDefs={columnDefs}
                defaultColDef={defaultColDef}
                rowHeight={36}
                headerHeight={40}
                suppressCellFocus={true}
                animateRows={true}
                onRowClicked={handleRowClick}
                rowClass="cursor-pointer hover:bg-dark-600/50"
                getRowId={(params) => params.data.spotId.toString()}
              />
            )}
          </div>
        </div>
      </div>
    </GlassPanel>
  );
}
