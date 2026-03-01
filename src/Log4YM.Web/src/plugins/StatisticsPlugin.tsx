import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { BarChart3, RefreshCw } from 'lucide-react';
import { api, DxccEntityStatus, DxccFilters } from '../api/client';
import { GlassPanel } from '../components/GlassPanel';
import { VuccStatisticsTab } from './VuccStatisticsTab';
import { PotaStatisticsTab } from './PotaStatisticsTab';
import { IotaStatisticsTab } from './IotaStatisticsTab';

type StatsTab = 'dxcc' | 'vucc' | 'pota' | 'iota';

const BANDS = ['160m', '80m', '40m', '20m', '17m', '15m', '10m', '6m'];
const CONTINENTS = ['AF', 'AN', 'AS', 'EU', 'NA', 'OC', 'SA'];
const STATUS_OPTIONS = [
  { value: '', label: 'All' },
  { value: 'worked', label: 'Worked' },
  { value: 'confirmed', label: 'Confirmed' },
  { value: 'workedNotConfirmed', label: 'Worked, not confirmed' },
];

function BandCell({ status }: { status?: { worked: boolean; confirmed: boolean; qsoCount: number } }) {
  if (!status || !status.worked) {
    return <td className="text-center px-1 py-1 text-dark-400 text-xs">-</td>;
  }
  return (
    <td className="text-center px-1 py-1">
      <span
        title={`${status.qsoCount} QSO${status.qsoCount !== 1 ? 's' : ''}${status.confirmed ? ' · Confirmed' : ''}`}
        className={`inline-block w-3 h-3 rounded-sm ${status.confirmed ? 'bg-accent-success' : 'bg-accent-warning'}`}
      />
    </td>
  );
}

function SummaryBar({ label, value, max }: { label: string; value: number; max: number }) {
  const pct = max > 0 ? Math.round((value / max) * 100) : 0;
  return (
    <div className="flex items-center gap-2 text-xs">
      <span className="text-dark-300 w-20 shrink-0">{label}</span>
      <div className="flex-1 bg-dark-700 rounded-full h-1.5">
        <div
          className="bg-accent-secondary h-1.5 rounded-full transition-all"
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="text-gray-300 w-16 text-right">{value} / {max}</span>
    </div>
  );
}

export function StatisticsPlugin() {
  const [activeTab, setActiveTab] = useState<StatsTab>('dxcc');
  const [filters, setFilters] = useState<DxccFilters>({});
  const [sortBy, setSortBy] = useState<'name' | 'continent' | 'qsos'>('name');

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['dxcc-statistics', filters],
    queryFn: () => api.getDxccStatistics(filters),
    staleTime: 60_000,
  });

  const visibleBands = useMemo(() => {
    if (!data) return BANDS;
    const bandsInData = new Set(data.entities.flatMap(e => Object.keys(e.bandStatus)));
    return BANDS.filter(b => bandsInData.has(b));
  }, [data]);

  const sortedEntities = useMemo(() => {
    if (!data) return [];
    return [...data.entities].sort((a, b) => {
      if (sortBy === 'continent') {
        const cmp = (a.continent ?? 'ZZ').localeCompare(b.continent ?? 'ZZ');
        return cmp !== 0 ? cmp : a.entityName.localeCompare(b.entityName);
      }
      if (sortBy === 'qsos') return b.totalQsos - a.totalQsos;
      return a.entityName.localeCompare(b.entityName);
    });
  }, [data, sortBy]);

  const setFilter = (key: keyof DxccFilters, value: string) => {
    setFilters(prev => ({ ...prev, [key]: value || undefined }));
  };

  return (
    <GlassPanel
      title="Statistics"
      icon={<BarChart3 className="w-5 h-5" />}
      actions={
        <button
          onClick={() => refetch()}
          className="p-1 hover:bg-dark-600 rounded text-dark-300 hover:text-gray-100 transition-colors"
          title="Refresh"
        >
          <RefreshCw className="w-3.5 h-3.5" />
        </button>
      }
    >
      <div className="flex flex-col h-full overflow-hidden">
        {/* Sub-tab navigation */}
        <div className="flex-shrink-0 px-4 pt-3 pb-0 border-b border-glass-100">
          <div className="flex gap-1">
            {(['dxcc', 'vucc', 'pota', 'iota'] as const).map(tab => (
              <button
                key={tab}
                onClick={() => setActiveTab(tab)}
                className={`px-3 py-1.5 text-xs font-ui transition-colors ${
                  activeTab === tab
                    ? 'font-semibold rounded-t border-b-2 border-accent-secondary text-accent-secondary bg-dark-700/50'
                    : 'text-dark-300 hover:text-gray-300'
                }`}
              >
                {tab.toUpperCase()}
              </button>
            ))}
          </div>
        </div>

        {activeTab !== 'dxcc' ? (
          activeTab === 'vucc' ? <VuccStatisticsTab /> :
          activeTab === 'pota' ? <PotaStatisticsTab /> :
          <IotaStatisticsTab />
        ) : (<>

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
            value={filters.band ?? ''}
            onChange={e => setFilter('band', e.target.value)}
            className="glass-input text-xs px-2 py-1"
          >
            <option value="">All bands</option>
            {BANDS.map(b => <option key={b} value={b}>{b}</option>)}
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
            onChange={e => setSortBy(e.target.value as 'name' | 'continent' | 'qsos')}
            className="glass-input text-xs px-2 py-1"
          >
            <option value="name">Sort: Name</option>
            <option value="continent">Sort: Continent</option>
            <option value="qsos">Sort: QSOs</option>
          </select>

          {(filters.continent || filters.band || filters.status) && (
            <button
              onClick={() => setFilters({})}
              className="text-xs text-dark-300 hover:text-accent-danger transition-colors"
            >
              Reset
            </button>
          )}
        </div>

        {/* Summary */}
        {data && (
          <div className="flex-shrink-0 px-4 py-2 border-b border-glass-100 space-y-1">
            <SummaryBar label="Worked" value={data.totalEntitiesWorked} max={340} />
            <SummaryBar label="Confirmed" value={data.totalEntitiesConfirmed} max={340} />
            <div className="flex items-center gap-2 text-xs pt-0.5">
              <span className="text-dark-300">DXCC Challenge:</span>
              <span className="text-accent-secondary font-semibold">{data.challengeScore}</span>
              <span className="ml-4 flex items-center gap-2">
                <span className="inline-block w-3 h-3 rounded-sm bg-accent-success" />
                <span className="text-dark-300">Confirmed</span>
                <span className="inline-block w-3 h-3 rounded-sm bg-accent-warning ml-2" />
                <span className="text-dark-300">Worked</span>
                <span className="inline-block w-3 h-3 rounded-sm bg-dark-600 border border-glass-100 ml-2" />
                <span className="text-dark-300">Not worked</span>
              </span>
            </div>
          </div>
        )}

        {/* Table */}
        <div className="flex-1 overflow-auto">
          {isLoading ? (
            <div className="flex items-center justify-center py-12 text-gray-500">
              <RefreshCw className="w-4 h-4 animate-spin mr-2" />
              Loading statistics...
            </div>
          ) : error ? (
            <div className="flex items-center justify-center py-12 text-accent-danger text-sm">
              Failed to load statistics
            </div>
          ) : !data || data.entities.length === 0 ? (
            <div className="flex items-center justify-center py-12 text-gray-500 text-sm">
              No DXCC entities found. Log some QSOs first!
            </div>
          ) : (
            <table className="w-full text-xs font-ui border-collapse">
              <thead className="sticky top-0 bg-dark-800 z-10">
                <tr>
                  <th className="text-left px-3 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 min-w-[160px]">
                    Entity
                  </th>
                  <th className="text-center px-1 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-8">
                    Cont
                  </th>
                  <th className="text-center px-1 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-10">
                    QSOs
                  </th>
                  {visibleBands.map(band => (
                    <th key={band} className="text-center px-1 py-2 text-dark-300 font-semibold uppercase tracking-wider border-b border-glass-100 w-10">
                      {band}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {sortedEntities.map((entity: DxccEntityStatus, idx: number) => (
                  <tr
                    key={entity.dxccCode ?? entity.entityName}
                    className={`border-b border-glass-100/30 hover:bg-dark-700/50 transition-colors ${idx % 2 === 0 ? '' : 'bg-dark-800/30'}`}
                  >
                    <td className="px-3 py-1.5 text-gray-200 font-medium">
                      {entity.entityName}
                    </td>
                    <td className="text-center px-1 py-1.5 text-dark-300">
                      {entity.continent ?? '-'}
                    </td>
                    <td className="text-center px-1 py-1.5 text-gray-400">
                      {entity.totalQsos}
                    </td>
                    {visibleBands.map(band => (
                      <BandCell key={band} status={entity.bandStatus[band]} />
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {data && (
          <div className="flex-shrink-0 px-4 py-2 border-t border-glass-100 text-xs text-dark-300">
            {sortedEntities.length} entities shown
          </div>
        )}

        </>)}
      </div>
    </GlassPanel>
  );
}
