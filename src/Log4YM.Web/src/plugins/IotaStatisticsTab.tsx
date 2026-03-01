import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';
import { api, IotaFilters } from '../api/client';

const CONTINENTS = ['AF', 'AN', 'AS', 'EU', 'NA', 'OC', 'SA'];
const STATUS_OPTIONS = [
  { value: '', label: 'All' },
  { value: 'worked', label: 'Worked' },
  { value: 'confirmed', label: 'Confirmed' },
  { value: 'workedNotConfirmed', label: 'Worked, not confirmed' },
];
const MILESTONES = [100, 200, 300, 400, 500, 750, 1000];

function MilestoneBar({ value }: { value: number }) {
  const nextMilestone = MILESTONES.find(m => m > value) ?? MILESTONES[MILESTONES.length - 1];
  const pct = Math.min(100, Math.round((value / nextMilestone) * 100));
  return (
    <div className="flex items-center gap-2 text-xs">
      <span className="text-dark-300 w-14 shrink-0">Groups</span>
      <div className="flex-1 bg-dark-700 rounded-full h-1.5 relative">
        <div
          className="bg-accent-secondary h-1.5 rounded-full transition-all"
          style={{ width: `${pct}%` }}
        />
        {MILESTONES.map(m => {
          const pos = Math.min(100, Math.round((m / nextMilestone) * 100));
          if (pos > 100) return null;
          return (
            <div
              key={m}
              className="absolute top-0 h-1.5 w-px bg-dark-400"
              style={{ left: `${pos}%` }}
              title={`${m} groups`}
            />
          );
        })}
      </div>
      <span className="text-gray-300 w-20 text-right">{value} / {nextMilestone}</span>
    </div>
  );
}

export function IotaStatisticsTab() {
  const [filters, setFilters] = useState<IotaFilters>({});
  const [sortBy, setSortBy] = useState<'reference' | 'continent' | 'qsos' | 'date'>('reference');

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['iota-statistics', filters],
    queryFn: () => api.getIotaStatistics(filters),
    staleTime: 60_000,
  });

  const sortedGroups = useMemo(() => {
    if (!data) return [];
    return [...data.groups].sort((a, b) => {
      if (sortBy === 'continent') {
        const cmp = a.continent.localeCompare(b.continent);
        return cmp !== 0 ? cmp : a.iotaReference.localeCompare(b.iotaReference);
      }
      if (sortBy === 'qsos') return b.qsoCount - a.qsoCount;
      if (sortBy === 'date') {
        const da = a.firstWorked ?? '';
        const db = b.firstWorked ?? '';
        return db.localeCompare(da);
      }
      return a.iotaReference.localeCompare(b.iotaReference);
    });
  }, [data, sortBy]);

  const setFilter = (key: keyof IotaFilters, value: string) => {
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
          value={filters.continent ?? ''}
          onChange={e => setFilter('continent', e.target.value)}
          className="glass-input text-xs px-2 py-1"
        >
          <option value="">All continents</option>
          {CONTINENTS.map(c => <option key={c} value={c}>{c}</option>)}
        </select>

        <select
          value={filters.status ?? ''}
          onChange={e => setFilter('status', e.target.value)}
          className="glass-input text-xs px-2 py-1"
        >
          {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
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
          onChange={e => setSortBy(e.target.value as 'reference' | 'continent' | 'qsos' | 'date')}
          className="glass-input text-xs px-2 py-1"
        >
          <option value="reference">Sort: Reference</option>
          <option value="continent">Sort: Continent</option>
          <option value="qsos">Sort: QSOs</option>
          <option value="date">Sort: Date</option>
        </select>

        {(filters.continent || filters.status || filters.fromDate || filters.toDate) && (
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
          <MilestoneBar value={data.totalGroupsWorked} />
          <div className="flex items-center gap-4 text-xs pt-0.5">
            <span>
              <span className="text-dark-300">Worked:</span>{' '}
              <span className="text-accent-warning font-semibold">{data.totalGroupsWorked}</span>
            </span>
            <span>
              <span className="text-dark-300">Confirmed:</span>{' '}
              <span className="text-accent-success font-semibold">{data.totalGroupsConfirmed}</span>
            </span>
            <span>
              <span className="text-dark-300">QSOs:</span>{' '}
              <span className="text-accent-secondary font-semibold">{data.totalQsos}</span>
            </span>
            <span className="ml-auto flex items-center gap-2">
              <span className="inline-block w-3 h-3 rounded-sm bg-accent-success" />
              <span className="text-dark-300">Confirmed</span>
              <span className="inline-block w-3 h-3 rounded-sm bg-accent-warning ml-2" />
              <span className="text-dark-300">Worked</span>
            </span>
          </div>
          {Object.keys(data.groupsByContinent).length > 0 && (
            <div className="flex items-center gap-3 text-xs pt-0.5">
              {CONTINENTS.filter(c => data.groupsByContinent[c]).map(c => (
                <span key={c}>
                  <span className="text-dark-300">{c}:</span>{' '}
                  <span className="text-gray-300">{data.groupsByContinent[c]}</span>
                </span>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Table */}
      <div className="flex-1 overflow-auto">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 text-gray-500">
            <RefreshCw className="w-4 h-4 animate-spin mr-2" />
            Loading IOTA statistics...
          </div>
        ) : error ? (
          <div className="flex items-center justify-center py-12 text-accent-danger text-sm">
            Failed to load IOTA statistics
          </div>
        ) : !data || data.groups.length === 0 ? (
          <div className="flex items-center justify-center py-12 text-gray-500 text-sm">
            No IOTA groups found. Log some QSOs with IOTA references!
          </div>
        ) : (
          <table className="w-full text-xs font-ui border-collapse">
            <thead className="sticky top-0 bg-dark-800 z-10">
              <tr>
                <th className="text-left px-3 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 min-w-[100px]">
                  IOTA Ref
                </th>
                <th className="text-center px-2 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-12">
                  Cont
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
              {sortedGroups.map((group, idx) => (
                <tr
                  key={group.iotaReference}
                  className={`border-b border-glass-100/30 hover:bg-dark-700/50 transition-colors ${idx % 2 === 0 ? '' : 'bg-dark-800/30'}`}
                >
                  <td className="px-3 py-1.5 text-gray-200 font-medium font-mono">
                    {group.iotaReference}
                  </td>
                  <td className="text-center px-2 py-1.5 text-dark-300">
                    {group.continent}
                  </td>
                  <td className="text-center px-2 py-1.5 text-gray-400">
                    {group.qsoCount}
                  </td>
                  <td className="text-center px-2 py-1.5">
                    <span
                      className={`inline-block w-3 h-3 rounded-sm ${group.confirmed ? 'bg-accent-success' : 'bg-accent-warning'}`}
                      title={group.confirmed ? 'Confirmed' : 'Worked'}
                    />
                  </td>
                  <td className="text-center px-2 py-1.5 text-dark-300">
                    {formatDate(group.firstWorked)}
                  </td>
                  <td className="text-center px-2 py-1.5 text-dark-300">
                    {formatDate(group.lastWorked)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {data && (
        <div className="flex-shrink-0 px-4 py-2 border-t border-glass-100 text-xs text-dark-300">
          {sortedGroups.length} group{sortedGroups.length !== 1 ? 's' : ''} shown
        </div>
      )}
    </div>
  );
}
