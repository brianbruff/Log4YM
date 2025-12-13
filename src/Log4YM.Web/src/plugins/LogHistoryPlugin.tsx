import { useState, useCallback, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Book, Search, Calendar, Radio, Filter, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, ChevronUp, ChevronDown, X } from 'lucide-react';
import { AgGridReact } from 'ag-grid-react';
import { ColDef, ICellRendererParams } from 'ag-grid-community';
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';
import { api, QsoResponse } from '../api/client';
import { GlassPanel } from '../components/GlassPanel';
import { getCountryFlag } from '../core/countryFlags';

// Custom cell renderer for mode badges
const ModeCellRenderer = (props: ICellRendererParams<QsoResponse>) => {
  const mode = props.value;
  const getModeClass = (mode: string) => {
    switch (mode) {
      case 'CW': return 'badge-cw';
      case 'SSB': return 'badge-ssb';
      case 'FT8':
      case 'FT4': return 'badge-ft8';
      case 'RTTY':
      case 'PSK31': return 'badge-rtty';
      default: return 'bg-dark-600 text-gray-300';
    }
  };
  return <span className={`badge text-xs ${getModeClass(mode)}`}>{mode}</span>;
};

// Custom cell renderer for callsign
const CallsignCellRenderer = (props: ICellRendererParams<QsoResponse>) => {
  return <span className="font-mono font-bold text-accent-primary">{props.value}</span>;
};

// Custom cell renderer for country flag
const FlagCellRenderer = (props: ICellRendererParams<QsoResponse>) => {
  const country = props.data?.station?.country || props.data?.country;
  return <span className="text-lg">{getCountryFlag(country)}</span>;
};

// Custom cell renderer for date with calendar icon
const DateCellRenderer = (props: ICellRendererParams<QsoResponse>) => {
  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: '2-digit'
    });
  };
  return (
    <div className="flex items-center gap-1 text-gray-400">
      <Calendar className="w-3 h-3" />
      {formatDate(props.value)}
    </div>
  );
};

export function LogHistoryPlugin() {
  const [callsignSearch, setCallsignSearch] = useState('');
  const [nameSearch, setNameSearch] = useState('');
  const [selectedBand, setSelectedBand] = useState<string>('');
  const [selectedMode, setSelectedMode] = useState<string>('');
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');
  const [currentPage, setCurrentPage] = useState(1);
  const [showFilters, setShowFilters] = useState(false);
  const [showSummary, setShowSummary] = useState(true);
  const pageSize = 50;

  const { data: response, isLoading } = useQuery({
    queryKey: ['qsos', callsignSearch, nameSearch, selectedBand, selectedMode, fromDate, toDate, currentPage],
    queryFn: () => api.getQsos({
      callsign: callsignSearch || undefined,
      name: nameSearch || undefined,
      band: selectedBand || undefined,
      mode: selectedMode || undefined,
      fromDate: fromDate || undefined,
      toDate: toDate || undefined,
      page: currentPage,
      pageSize,
    }),
  });

  const { data: stats } = useQuery({
    queryKey: ['statistics'],
    queryFn: () => api.getStatistics(),
  });

  const qsos = response?.items || [];
  const totalCount = response?.totalCount || 0;
  const totalPages = response?.totalPages || 1;

  const handlePageChange = useCallback((page: number) => {
    setCurrentPage(Math.max(1, Math.min(page, totalPages)));
  }, [totalPages]);

  const clearFilters = useCallback(() => {
    setCallsignSearch('');
    setNameSearch('');
    setSelectedBand('');
    setSelectedMode('');
    setFromDate('');
    setToDate('');
    setCurrentPage(1);
  }, []);

  const hasActiveFilters = callsignSearch || nameSearch || selectedBand || selectedMode || fromDate || toDate;

  const formatTime = (timeStr: string) => {
    if (timeStr && timeStr.length >= 4) {
      return `${timeStr.slice(0, 2)}:${timeStr.slice(2, 4)}`;
    }
    return timeStr || '';
  };

  const columnDefs = useMemo<ColDef<QsoResponse>[]>(() => [
    {
      headerName: 'Date',
      field: 'qsoDate',
      cellRenderer: DateCellRenderer,
      width: 110,
      resizable: true,
    },
    {
      headerName: 'Time',
      field: 'timeOn',
      valueFormatter: (params) => formatTime(params.value),
      cellClass: 'font-mono text-gray-400',
      width: 70,
      resizable: true,
    },
    {
      headerName: 'Callsign',
      field: 'callsign',
      cellRenderer: CallsignCellRenderer,
      width: 110,
      resizable: true,
    },
    {
      headerName: 'Band',
      field: 'band',
      cellClass: 'font-mono text-accent-info',
      width: 70,
      resizable: true,
    },
    {
      headerName: 'Mode',
      field: 'mode',
      cellRenderer: ModeCellRenderer,
      width: 80,
      resizable: true,
    },
    {
      headerName: 'RST S/R',
      valueGetter: (params) => `${params.data?.rstSent || '-'}/${params.data?.rstRcvd || '-'}`,
      cellClass: 'font-mono text-gray-400',
      width: 80,
      resizable: true,
    },
    {
      headerName: 'Name',
      valueGetter: (params) => params.data?.station?.name || params.data?.name || '-',
      cellClass: 'text-gray-300',
      width: 120,
      resizable: true,
    },
    {
      headerName: '',
      field: 'country',
      cellRenderer: FlagCellRenderer,
      width: 50,
      resizable: false,
      sortable: false,
    },
    {
      headerName: 'Country',
      valueGetter: (params) => params.data?.station?.country || params.data?.country || '-',
      cellClass: 'text-gray-400',
      width: 120,
      resizable: true,
    },
  ], []);

  const defaultColDef = useMemo<ColDef>(() => ({
    sortable: true,
    resizable: true,
  }), []);

  return (
    <GlassPanel
      title="Log History"
      icon={<Book className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-2 text-sm text-gray-400">
          <span>{totalCount.toLocaleString()} QSOs</span>
          <span className="text-glass-100">|</span>
          <span>{stats?.uniqueCountries || 0} DXCC</span>
        </div>
      }
    >
      <div className="p-4 space-y-4">
        {/* Collapsible Summary Section */}
        {stats && (
          <div className="bg-dark-700/50 rounded-lg overflow-hidden">
            <button
              onClick={() => setShowSummary(!showSummary)}
              className="w-full flex items-center justify-between p-3 hover:bg-dark-600/50 transition-colors"
            >
              <div className="flex items-center gap-2 text-sm text-gray-300">
                <span className="font-medium">Summary</span>
                <span className="text-gray-500">|</span>
                <span className="text-accent-primary font-bold">{stats.totalQsos.toLocaleString()}</span>
                <span className="text-gray-500">QSOs</span>
              </div>
              {showSummary ? (
                <ChevronUp className="w-4 h-4 text-gray-400" />
              ) : (
                <ChevronDown className="w-4 h-4 text-gray-400" />
              )}
            </button>
            {showSummary && (
              <div className="grid grid-cols-4 gap-4 p-4 pt-2 border-t border-glass-100">
                <div className="text-center">
                  <p className="text-2xl font-bold text-accent-primary">{stats.totalQsos.toLocaleString()}</p>
                  <p className="text-xs text-gray-500">Total QSOs</p>
                </div>
                <div className="text-center">
                  <p className="text-2xl font-bold text-accent-success">{stats.uniqueCountries}</p>
                  <p className="text-xs text-gray-500">Countries</p>
                </div>
                <div className="text-center">
                  <p className="text-2xl font-bold text-accent-info">{stats.uniqueGrids}</p>
                  <p className="text-xs text-gray-500">Grids</p>
                </div>
                <div className="text-center">
                  <p className="text-2xl font-bold text-accent-warning">{stats.qsosToday}</p>
                  <p className="text-xs text-gray-500">Today</p>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Search and Filters Row */}
        <div className="flex gap-3 flex-wrap">
          {/* Callsign Search */}
          <div className="flex-1 min-w-[150px] relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              type="text"
              value={callsignSearch}
              onChange={(e) => {
                setCallsignSearch(e.target.value.toUpperCase());
                setCurrentPage(1);
              }}
              placeholder="Search callsign..."
              className="glass-input w-full pl-10 font-mono"
            />
          </div>

          {/* Name Search */}
          <div className="flex-1 min-w-[150px] relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              type="text"
              value={nameSearch}
              onChange={(e) => {
                setNameSearch(e.target.value);
                setCurrentPage(1);
              }}
              placeholder="Search name..."
              className="glass-input w-full pl-10"
            />
          </div>

          {/* Band Select */}
          <select
            value={selectedBand}
            onChange={(e) => {
              setSelectedBand(e.target.value);
              setCurrentPage(1);
            }}
            className="glass-input w-24"
          >
            <option value="">Band</option>
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
            <option value="2m">2m</option>
          </select>

          {/* Mode Select */}
          <select
            value={selectedMode}
            onChange={(e) => {
              setSelectedMode(e.target.value);
              setCurrentPage(1);
            }}
            className="glass-input w-24"
          >
            <option value="">Mode</option>
            <option value="SSB">SSB</option>
            <option value="CW">CW</option>
            <option value="FT8">FT8</option>
            <option value="FT4">FT4</option>
            <option value="RTTY">RTTY</option>
            <option value="PSK31">PSK31</option>
            <option value="AM">AM</option>
            <option value="FM">FM</option>
          </select>

          {/* Filter Toggle */}
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={`glass-button p-2 ${showFilters ? 'bg-accent-primary/20' : ''}`}
            title="Date filters"
          >
            <Filter className="w-4 h-4" />
          </button>

          {/* Clear Filters */}
          {hasActiveFilters && (
            <button
              onClick={clearFilters}
              className="glass-button p-2 text-red-400 hover:text-red-300"
              title="Clear all filters"
            >
              <X className="w-4 h-4" />
            </button>
          )}
        </div>

        {/* Date Range Filters (Collapsible) */}
        {showFilters && (
          <div className="flex gap-3 items-center p-3 bg-dark-700/50 rounded-lg">
            <Calendar className="w-4 h-4 text-gray-500" />
            <div className="flex items-center gap-2">
              <label className="text-sm text-gray-400">From:</label>
              <input
                type="date"
                value={fromDate}
                onChange={(e) => {
                  setFromDate(e.target.value);
                  setCurrentPage(1);
                }}
                className="glass-input px-2 py-1 text-sm"
              />
            </div>
            <div className="flex items-center gap-2">
              <label className="text-sm text-gray-400">To:</label>
              <input
                type="date"
                value={toDate}
                onChange={(e) => {
                  setToDate(e.target.value);
                  setCurrentPage(1);
                }}
                className="glass-input px-2 py-1 text-sm"
              />
            </div>
          </div>
        )}

        {/* AG Grid Table */}
        <div className="ag-theme-alpine-dark h-[calc(100vh-380px)]">
          {isLoading ? (
            <div className="flex items-center justify-center py-8 text-gray-500">
              <Radio className="w-4 h-4 animate-spin mr-2" />
              Loading...
            </div>
          ) : (
            <AgGridReact<QsoResponse>
              rowData={qsos}
              columnDefs={columnDefs}
              defaultColDef={defaultColDef}
              rowHeight={36}
              headerHeight={40}
              suppressCellFocus={true}
              suppressRowClickSelection={true}
              animateRows={true}
              getRowId={(params) => params.data.id}
            />
          )}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between pt-2 border-t border-glass-100">
            <div className="text-sm text-gray-400">
              Showing {((currentPage - 1) * pageSize) + 1} - {Math.min(currentPage * pageSize, totalCount)} of {totalCount.toLocaleString()}
            </div>
            <div className="flex items-center gap-1">
              <button
                onClick={() => handlePageChange(1)}
                disabled={currentPage === 1}
                className="glass-button p-1.5 disabled:opacity-30 disabled:cursor-not-allowed"
                title="First page"
              >
                <ChevronsLeft className="w-4 h-4" />
              </button>
              <button
                onClick={() => handlePageChange(currentPage - 1)}
                disabled={currentPage === 1}
                className="glass-button p-1.5 disabled:opacity-30 disabled:cursor-not-allowed"
                title="Previous page"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <div className="flex items-center gap-1 px-2">
                <input
                  type="number"
                  value={currentPage}
                  onChange={(e) => {
                    const page = parseInt(e.target.value) || 1;
                    handlePageChange(page);
                  }}
                  min={1}
                  max={totalPages}
                  className="glass-input w-16 text-center text-sm py-1"
                />
                <span className="text-gray-400 text-sm">/ {totalPages}</span>
              </div>
              <button
                onClick={() => handlePageChange(currentPage + 1)}
                disabled={currentPage === totalPages}
                className="glass-button p-1.5 disabled:opacity-30 disabled:cursor-not-allowed"
                title="Next page"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
              <button
                onClick={() => handlePageChange(totalPages)}
                disabled={currentPage === totalPages}
                className="glass-button p-1.5 disabled:opacity-30 disabled:cursor-not-allowed"
                title="Last page"
              >
                <ChevronsRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}

      </div>
    </GlassPanel>
  );
}
