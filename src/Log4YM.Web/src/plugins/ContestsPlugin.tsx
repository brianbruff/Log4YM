import { useState, useMemo, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Calendar, ExternalLink, Radio } from 'lucide-react';
import { api, Contest } from '../api/client';
import { GlassPanel } from '../components/GlassPanel';

// Format time remaining as human-readable string
const formatTimeRemaining = (endTime: string): string => {
  const now = new Date();
  const end = new Date(endTime);
  const diff = end.getTime() - now.getTime();

  if (diff <= 0) return 'Ended';

  const hours = Math.floor(diff / (1000 * 60 * 60));
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));

  if (hours > 24) {
    const days = Math.floor(hours / 24);
    return `${days}d ${hours % 24}h`;
  } else if (hours > 0) {
    return `${hours}h ${minutes}m`;
  } else {
    return `${minutes}m`;
  }
};

// Format date/time in human-readable format
const formatDateTime = (dateStr: string): string => {
  const date = new Date(dateStr);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrow = new Date(today.getTime() + 24 * 60 * 60 * 1000);
  const contestDate = new Date(date.getFullYear(), date.getMonth(), date.getDate());

  let dayLabel = '';
  if (contestDate.getTime() === today.getTime()) {
    dayLabel = 'Today';
  } else if (contestDate.getTime() === tomorrow.getTime()) {
    dayLabel = 'Tomorrow';
  } else {
    dayLabel = date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  const time = date.toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });

  return `${dayLabel} ${time}`;
};

// Get mode color class
const getModeClass = (mode: string): string => {
  const modeUpper = mode.toUpperCase();
  switch (modeUpper) {
    case 'CW':
      return 'badge-cw';
    case 'SSB':
    case 'PHONE':
      return 'badge-ssb';
    case 'RTTY':
    case 'DIGI':
      return 'badge-rtty';
    case 'FT8':
    case 'FT4':
      return 'badge-ft8';
    default:
      return 'bg-dark-600 text-gray-300';
  }
};

export function ContestsPlugin() {
  const [selectedDays, setSelectedDays] = useState(7);
  const [, setCurrentTime] = useState(new Date());

  // Update current time every minute to refresh time remaining
  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentTime(new Date());
    }, 60000); // Update every minute

    return () => clearInterval(interval);
  }, []);

  const { data: contests, isLoading } = useQuery({
    queryKey: ['contests', selectedDays],
    queryFn: () => api.getContests(selectedDays),
    refetchInterval: 30 * 60 * 1000, // Refetch every 30 minutes
  });

  // Count live contests
  const liveCount = useMemo(() => {
    return contests?.filter((c) => c.isLive).length || 0;
  }, [contests]);

  // Separate live and upcoming contests
  const { liveContests, upcomingContests } = useMemo(() => {
    if (!contests) return { liveContests: [], upcomingContests: [] };

    const live = contests.filter((c) => c.isLive);
    const upcoming = contests.filter((c) => !c.isLive);

    return { liveContests: live, upcomingContests: upcoming };
  }, [contests]);

  return (
    <GlassPanel
      title="Contests"
      icon={<Calendar className="w-5 h-5" />}
      actions={
        <div className="flex items-center gap-3">
          {liveCount > 0 && (
            <div className="flex items-center gap-1.5">
              <span className="relative flex h-2.5 w-2.5">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-500 opacity-75"></span>
                <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-red-500"></span>
              </span>
              <span className="text-xs font-medium text-red-400">
                {liveCount} LIVE
              </span>
            </div>
          )}
          <select
            value={selectedDays}
            onChange={(e) => setSelectedDays(Number(e.target.value))}
            className="glass-input text-xs px-2 py-1 w-24"
          >
            <option value={3}>3 days</option>
            <option value={7}>7 days</option>
            <option value={14}>14 days</option>
            <option value={30}>30 days</option>
          </select>
        </div>
      }
    >
      <div className="flex flex-col h-full">
        {/* Scrollable contest list */}
        <div className="flex-1 overflow-y-auto p-4 space-y-3">
          {isLoading ? (
            <div className="flex items-center justify-center py-8 text-gray-500">
              <Radio className="w-4 h-4 animate-spin mr-2" />
              Loading contests...
            </div>
          ) : contests?.length === 0 ? (
            <div className="text-center py-8 text-gray-500">
              No contests in the next {selectedDays} days
            </div>
          ) : (
            <>
              {/* Live Contests Section */}
              {liveContests.length > 0 && (
                <div className="space-y-2">
                  <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wider px-2">
                    Live Now
                  </h4>
                  {liveContests.map((contest, index) => (
                    <ContestCard
                      key={`live-${index}`}
                      contest={contest}
                    />
                  ))}
                </div>
              )}

              {/* Upcoming Contests Section */}
              {upcomingContests.length > 0 && (
                <div className="space-y-2">
                  {liveContests.length > 0 && (
                    <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wider px-2 mt-4">
                      Upcoming
                    </h4>
                  )}
                  {upcomingContests.map((contest, index) => (
                    <ContestCard
                      key={`upcoming-${index}`}
                      contest={contest}
                    />
                  ))}
                </div>
              )}
            </>
          )}
        </div>

        {/* Footer with WA7BNM link */}
        <div className="flex-shrink-0 border-t border-glass-100 px-4 py-2 bg-dark-900/30">
          <a
            href="https://www.contestcalendar.com/"
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-accent-primary transition-colors"
          >
            <span>Data from WA7BNM Contest Calendar</span>
            <ExternalLink className="w-3 h-3" />
          </a>
        </div>
      </div>
    </GlassPanel>
  );
}

// Contest Card Component
function ContestCard({ contest }: { contest: Contest }) {
  const isLive = contest.isLive;
  const isStartingSoon = contest.isStartingSoon;

  return (
    <div
      className={`
        p-3 rounded-lg border transition-all
        ${isLive
          ? 'bg-red-500/10 border-red-500/30 shadow-[0_0_10px_rgba(239,68,68,0.3)]'
          : isStartingSoon
          ? 'bg-yellow-500/10 border-yellow-500/30'
          : 'bg-dark-700/50 border-glass-100 hover:bg-dark-600/50'
        }
      `}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1 min-w-0">
          {/* Contest Name */}
          <div className="flex items-center gap-2 mb-1">
            {isLive && (
              <span className="relative flex h-2 w-2">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-500 opacity-75"></span>
                <span className="relative inline-flex rounded-full h-2 w-2 bg-red-500"></span>
              </span>
            )}
            <h3 className="font-medium text-gray-100 text-sm truncate">
              {contest.name}
            </h3>
          </div>

          {/* Mode Badge */}
          <div className="flex items-center gap-2 mb-2">
            <span className={`badge text-xs ${getModeClass(contest.mode)}`}>
              {contest.mode.toUpperCase()}
            </span>
            {isLive && (
              <span className="badge text-xs bg-red-500/20 text-red-400 border border-red-500/30">
                LIVE
              </span>
            )}
            {isStartingSoon && !isLive && (
              <span className="badge text-xs bg-yellow-500/20 text-yellow-400 border border-yellow-500/30">
                SOON
              </span>
            )}
          </div>

          {/* Time Info */}
          <div className="text-xs text-gray-400 space-y-0.5">
            {isLive ? (
              <>
                <div className="flex items-center gap-1.5">
                  <span className="text-gray-500">Ends:</span>
                  <span className="text-gray-300">{formatDateTime(contest.endTime)}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <span className="text-gray-500">Time left:</span>
                  <span className="font-medium text-accent-warning">
                    {formatTimeRemaining(contest.endTime)}
                  </span>
                </div>
              </>
            ) : (
              <>
                <div className="flex items-center gap-1.5">
                  <span className="text-gray-500">Starts:</span>
                  <span className="text-gray-300">{formatDateTime(contest.startTime)}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <span className="text-gray-500">Ends:</span>
                  <span className="text-gray-300">{formatDateTime(contest.endTime)}</span>
                </div>
              </>
            )}
          </div>
        </div>

        {/* Link Icon */}
        {contest.url && (
          <a
            href={contest.url}
            target="_blank"
            rel="noopener noreferrer"
            className="flex-shrink-0 p-1.5 text-gray-500 hover:text-accent-primary transition-colors"
            title="View contest details"
          >
            <ExternalLink className="w-4 h-4" />
          </a>
        )}
      </div>
    </div>
  );
}
