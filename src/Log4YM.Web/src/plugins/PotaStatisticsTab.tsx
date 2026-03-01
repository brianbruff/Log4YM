import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';
import { api, PotaFilters } from '../api/client';

const ACTIVITY_OPTIONS = [
  { value: '', label: 'All activity' },
  { value: 'Activator', label: 'Activator' },
  { value: 'Hunter', label: 'Hunter' },
];

const MILESTONES = [10, 50, 100, 250, 500, 1000];

function MilestoneBar({ label, value, milestone }: { label: string; value: number; milestone: number }) {
  const pct = milestone > 0 ? Math.min(100, Math.round((value / milestone) * 100)) : 0;
  return (
    <div className="flex items-center gap-2 text-xs">
      <span className="text-dark-300 w-14 shrink-0">{label}</span>
      <div className="flex-1 bg-dark-700 rounded-full h-1.5">
        <div
          className={`h-1.5 rounded-full transition-all ${pct >= 100 ? 'bg-accent-success' : 'bg-accent-secondary'}`}
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="text-gray-300 w-20 text-right">{value} / {milestone}</span>
    </div>
  );
}

export function PotaStatisticsTab() {
  const [filters, setFilters] = useState<PotaFilters>({});
  const [sortBy, setSortBy] = useState<'reference' | 'qsos' | 'date'>('reference');

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['pota-statistics', filters],
    queryFn: () => api.getPotaStatistics(filters),
    staleTime: 60_000,
  });

  const sortedParks = useMemo(() => {
    if (!data) return [];
    return [...data.parks].sort((a, b) => {
      if (sortBy === 'qsos') return b.qsoCount - a.qsoCount;
      if (sortBy === 'date') {
        const da = a.lastQso ?? '';
        const db = b.lastQso ?? '';
        return db.localeCompare(da);
      }
      return a.parkReference.localeCompare(b.parkReference);
    });
  }, [data, sortBy]);

  const setFilter = (key: keyof PotaFilters, value: string) => {
    setFilters(prev => ({ ...prev, [key]: value || undefined }));
  };

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
  };

  const totalParks = data ? data.parks.length : 0;

  return (
    <div className="flex flex-col h-full overflow-hidden">
      {/* Filters */}
      <div className="flex-shrink-0 px-4 py-2 border-b border-glass-100 flex flex-wrap gap-2 items-center">
        <select
          value={filters.activityType ?? ''}
          onChange={e => setFilter('activityType', e.target.value)}
          className="glass-input text-xs px-2 py-1"
        >
          {ACTIVITY_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>

        <input
          type="date"
          value={filters.fromDate ?? ''}
          onChange={e => setFilter('fromDate', e.target.value)}
          className="glass-input text-xs px-2 py-1"
          placeholder="From"
        />

        <input
          type="date"
          value={filters.toDate ?? ''}
          onChange={e => setFilter('toDate', e.target.value)}
          className="glass-input text-xs px-2 py-1"
          placeholder="To"
        />

        <select
          value={sortBy}
          onChange={e => setSortBy(e.target.value as 'reference' | 'qsos' | 'date')}
          className="glass-input text-xs px-2 py-1"
        >
          <option value="reference">Sort: Reference</option>
          <option value="qsos">Sort: QSOs</option>
          <option value="date">Sort: Last QSO</option>
        </select>

        {(filters.activityType || filters.fromDate || filters.toDate) && (
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
          {MILESTONES.map(milestone => (
            <MilestoneBar
              key={milestone}
              label={`${milestone}`}
              value={totalParks}
              milestone={milestone}
            />
          ))}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs pt-0.5">
            <span>
              <span className="text-dark-300">Activated:</span>{' '}
              <span className="text-accent-secondary font-semibold">{data.uniqueParksActivated}</span>
              <span className="text-dark-400 ml-1">({data.totalActivationQsos} QSOs)</span>
            </span>
            <span>
              <span className="text-dark-300">Hunted:</span>{' '}
              <span className="text-accent-secondary font-semibold">{data.uniqueParksHunted}</span>
              <span className="text-dark-400 ml-1">({data.totalHuntQsos} QSOs)</span>
            </span>
          </div>
        </div>
      )}

      {/* Table */}
      <div className="flex-1 overflow-auto">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 text-gray-500">
            <RefreshCw className="w-4 h-4 animate-spin mr-2" />
            Loading POTA statistics...
          </div>
        ) : error ? (
          <div className="flex items-center justify-center py-12 text-accent-danger text-sm">
            Failed to load POTA statistics
          </div>
        ) : !data || data.parks.length === 0 ? (
          <div className="flex items-center justify-center py-12 text-gray-500 text-sm">
            No POTA parks found. Log some POTA QSOs first!
          </div>
        ) : (
          <table className="w-full text-xs font-ui border-collapse">
            <thead className="sticky top-0 bg-dark-800 z-10">
              <tr>
                <th className="text-left px-3 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 min-w-[100px]">
                  Reference
                </th>
                <th className="text-center px-2 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-20">
                  Type
                </th>
                <th className="text-center px-2 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-12">
                  QSOs
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
              {sortedParks.map((park, idx) => (
                <tr
                  key={park.parkReference}
                  className={`border-b border-glass-100/30 hover:bg-dark-700/50 transition-colors ${idx % 2 === 0 ? '' : 'bg-dark-800/30'}`}
                >
                  <td className="px-3 py-1.5 text-gray-200 font-medium font-mono">
                    {park.parkReference}
                  </td>
                  <td className="text-center px-2 py-1.5">
                    <span className={`text-xs px-1.5 py-0.5 rounded ${
                      park.activityType === 'Activator'
                        ? 'bg-accent-success/20 text-accent-success'
                        : park.activityType === 'Hunter'
                          ? 'bg-accent-secondary/20 text-accent-secondary'
                          : 'bg-accent-warning/20 text-accent-warning'
                    }`}>
                      {park.activityType}
                    </span>
                  </td>
                  <td className="text-center px-2 py-1.5 text-gray-400">
                    {park.qsoCount}
                  </td>
                  <td className="text-center px-2 py-1.5 text-dark-300">
                    {formatDate(park.firstQso)}
                  </td>
                  <td className="text-center px-2 py-1.5 text-dark-300">
                    {formatDate(park.lastQso)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {data && (
        <div className="flex-shrink-0 px-4 py-2 border-t border-glass-100 text-xs text-dark-300">
          {sortedParks.length} park{sortedParks.length !== 1 ? 's' : ''} shown
        </div>
      )}
    </div>
  );
}
