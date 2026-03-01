import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';
import { api, VuccFilters } from '../api/client';

const VUCC_BANDS = ['6m', '2m', '70cm', '23cm'];
const STATUS_OPTIONS = [
  { value: '', label: 'All' },
  { value: 'worked', label: 'Worked' },
  { value: 'confirmed', label: 'Confirmed' },
  { value: 'workedNotConfirmed', label: 'Worked, not confirmed' },
];

function SummaryBar({ label, value, confirmed, threshold }: { label: string; value: number; confirmed: number; threshold: number }) {
  const pct = threshold > 0 ? Math.min(100, Math.round((value / threshold) * 100)) : 0;
  const confPct = threshold > 0 ? Math.min(100, Math.round((confirmed / threshold) * 100)) : 0;
  return (
    <div className="flex items-center gap-2 text-xs">
      <span className="text-dark-300 w-14 shrink-0">{label}</span>
      <div className="flex-1 bg-dark-700 rounded-full h-1.5 relative">
        <div
          className="bg-accent-warning h-1.5 rounded-full transition-all absolute top-0 left-0"
          style={{ width: `${pct}%` }}
        />
        <div
          className="bg-accent-success h-1.5 rounded-full transition-all absolute top-0 left-0"
          style={{ width: `${confPct}%` }}
        />
      </div>
      <span className="text-gray-300 w-24 text-right">{value} / {threshold}</span>
    </div>
  );
}

export function VuccStatisticsTab() {
  const [filters, setFilters] = useState<VuccFilters>({});
  const [sortBy, setSortBy] = useState<'grid' | 'band' | 'qsos'>('grid');

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['vucc-statistics', filters],
    queryFn: () => api.getVuccStatistics(filters),
    staleTime: 60_000,
  });

  const sortedGrids = useMemo(() => {
    if (!data) return [];
    return [...data.grids].sort((a, b) => {
      if (sortBy === 'band') {
        const bandOrder = (band: string) => VUCC_BANDS.indexOf(band.toLowerCase());
        const cmp = bandOrder(a.band) - bandOrder(b.band);
        return cmp !== 0 ? cmp : a.grid.localeCompare(b.grid);
      }
      if (sortBy === 'qsos') return b.qsoCount - a.qsoCount;
      return a.grid.localeCompare(b.grid);
    });
  }, [data, sortBy]);

  const setFilter = (key: keyof VuccFilters, value: string) => {
    setFilters(prev => ({ ...prev, [key]: value || undefined }));
  };

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
  };

  return (
    <div className="flex flex-col h-full overflow-hidden">
      {/* Filters */}
      <div className="flex-shrink-0 px-4 py-2 border-b border-glass-100 flex flex-wrap gap-2 items-center">
        <select
          value={filters.band ?? ''}
          onChange={e => setFilter('band', e.target.value)}
          className="glass-input text-xs px-2 py-1"
        >
          <option value="">All bands</option>
          {VUCC_BANDS.map(b => <option key={b} value={b}>{b}</option>)}
        </select>

        <select
          value={filters.status ?? ''}
          onChange={e => setFilter('status', e.target.value)}
          className="glass-input text-xs px-2 py-1"
        >
          {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>

        <select
          value={sortBy}
          onChange={e => setSortBy(e.target.value as 'grid' | 'band' | 'qsos')}
          className="glass-input text-xs px-2 py-1"
        >
          <option value="grid">Sort: Grid</option>
          <option value="band">Sort: Band</option>
          <option value="qsos">Sort: QSOs</option>
        </select>

        {(filters.band || filters.status) && (
          <button
            onClick={() => setFilters({})}
            className="text-xs text-dark-300 hover:text-accent-danger transition-colors"
          >
            Reset
          </button>
        )}

        <button
          onClick={() => refetch()}
          className="ml-auto p-1 hover:bg-dark-600 rounded text-dark-300 hover:text-gray-100 transition-colors"
          title="Refresh"
        >
          <RefreshCw className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Summary */}
      {data && (
        <div className="flex-shrink-0 px-4 py-2 border-b border-glass-100 space-y-1">
          {VUCC_BANDS.map(band => {
            const summary = data.bandSummaries[band];
            if (!summary) return null;
            return (
              <SummaryBar
                key={band}
                label={band}
                value={summary.uniqueGrids}
                confirmed={summary.confirmedGrids}
                threshold={summary.awardThreshold}
              />
            );
          })}
          <div className="flex items-center gap-2 text-xs pt-0.5">
            <span className="text-dark-300">Total unique grids:</span>
            <span className="text-accent-secondary font-semibold">{data.totalUniqueGrids}</span>
            <span className="ml-4 flex items-center gap-2">
              <span className="inline-block w-3 h-3 rounded-sm bg-accent-success" />
              <span className="text-dark-300">Confirmed</span>
              <span className="inline-block w-3 h-3 rounded-sm bg-accent-warning ml-2" />
              <span className="text-dark-300">Worked</span>
            </span>
          </div>
        </div>
      )}

      {/* Table */}
      <div className="flex-1 overflow-auto">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 text-gray-500">
            <RefreshCw className="w-4 h-4 animate-spin mr-2" />
            Loading VUCC statistics...
          </div>
        ) : error ? (
          <div className="flex items-center justify-center py-12 text-accent-danger text-sm">
            Failed to load VUCC statistics
          </div>
        ) : !data || data.grids.length === 0 ? (
          <div className="flex items-center justify-center py-12 text-gray-500 text-sm">
            No grid squares found. Log some VHF/UHF QSOs with grid squares!
          </div>
        ) : (
          <table className="w-full text-xs font-ui border-collapse">
            <thead className="sticky top-0 bg-dark-800 z-10">
              <tr>
                <th className="text-left px-3 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 min-w-[80px]">
                  Grid
                </th>
                <th className="text-center px-2 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-16">
                  Band
                </th>
                <th className="text-center px-2 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-12">
                  QSOs
                </th>
                <th className="text-center px-2 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-20">
                  Status
                </th>
                <th className="text-center px-2 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-24">
                  First
                </th>
                <th className="text-center px-2 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-24">
                  Last
                </th>
              </tr>
            </thead>
            <tbody>
              {sortedGrids.map((grid, idx) => (
                <tr
                  key={`${grid.grid}-${grid.band}`}
                  className={`border-b border-glass-100/30 hover:bg-dark-700/50 transition-colors ${idx % 2 === 0 ? '' : 'bg-dark-800/30'}`}
                >
                  <td className="px-3 py-1.5 text-gray-200 font-medium font-mono">
                    {grid.grid}
                  </td>
                  <td className="text-center px-2 py-1.5 text-dark-300">
                    {grid.band}
                  </td>
                  <td className="text-center px-2 py-1.5 text-gray-400">
                    {grid.qsoCount}
                  </td>
                  <td className="text-center px-2 py-1.5">
                    <span
                      className={`inline-block w-3 h-3 rounded-sm ${grid.confirmed ? 'bg-accent-success' : 'bg-accent-warning'}`}
                      title={grid.confirmed ? 'Confirmed' : 'Worked'}
                    />
                  </td>
                  <td className="text-center px-2 py-1.5 text-dark-300">
                    {formatDate(grid.firstWorked)}
                  </td>
                  <td className="text-center px-2 py-1.5 text-dark-300">
                    {formatDate(grid.lastWorked)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {data && (
        <div className="flex-shrink-0 px-4 py-2 border-t border-glass-100 text-xs text-dark-300">
          {sortedGrids.length} grid{sortedGrids.length !== 1 ? 's' : ''} shown
        </div>
      )}
    </div>
  );
}
