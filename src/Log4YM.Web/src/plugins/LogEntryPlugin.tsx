import { useState, useCallback, useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Send, Search, User, MapPin, Radio, Link, Unlink, Clock, Lock, LockOpen, Loader2, X } from 'lucide-react';
import { api, CreateQsoRequest } from '../api/client';
import { useSignalR } from '../hooks/useSignalR';
import { useAppStore } from '../store/appStore';
import { useSettingsStore } from '../store/settingsStore';
import { GlassPanel } from '../components/GlassPanel';
import { getCountryFlag } from '../core/countryFlags';

// Helper to format date for input
const formatDateForInput = (date: Date): string => {
  return date.toISOString().slice(0, 10);
};

// Helper to format time for input (UTC)
const formatTimeForInput = (date: Date): string => {
  return date.toISOString().slice(11, 16);
};

const BANDS = ['160m', '80m', '40m', '30m', '20m', '17m', '15m', '12m', '10m', '6m', '2m', '70cm'];
const MODES = ['SSB', 'CW', 'FT8', 'FT4', 'RTTY', 'PSK31', 'AM', 'FM'];

// Common RST values for phone modes (SSB, AM, FM)
const RST_PHONE = ['59', '58', '57', '56', '55', '54', '53', '52', '51'];
// Common RST values for CW and digital modes
const RST_CW_DIGITAL = ['599', '589', '579', '569', '559', '549', '539', '529', '519'];

// Get default RST based on mode
const getDefaultRst = (mode: string): string => {
  return mode === 'CW' ? '599' : '59';
};

// CW doesn't use +dB enhancement
const supportsDbEnhancement = (mode: string): boolean => {
  return mode !== 'CW';
};

export function LogEntryPlugin() {
  const queryClient = useQueryClient();
  const { focusCallsign, persistCallsignMapImage } = useSignalR();
  const { focusedCallsignInfo, radioStates, selectedRadioId, isLookingUpCallsign, setFocusedCallsign, setFocusedCallsignInfo, setLogHistoryCallsignFilter, clearCallsignFromAllControls, selectedSpot, setSelectedSpot, addCallsignMapImage } = useAppStore();
  const { settings, updateRadioSettings } = useSettingsStore();
  const followRadio = settings.radio.followRadio;

  const [formData, setFormData] = useState({
    callsign: '',
    band: '20m',
    mode: 'SSB',
    rstSent: '59',
    rstSentPlus: '',
    rstRcvd: '59',
    rstRcvdPlus: '',
    frequency: '',
    name: '',
    grid: '',
    comment: '',
    notes: '',
  });

  // Timestamp state - locked means it follows system time
  const [timeLocked, setTimeLocked] = useState(true);
  const [qsoDate, setQsoDate] = useState(() => formatDateForInput(new Date()));
  const [qsoTime, setQsoTime] = useState(() => formatTimeForInput(new Date()));
  const timeIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Name state - locked means it auto-fills from QRZ
  const [nameLocked, setNameLocked] = useState(true);

  // Update time every second when locked
  useEffect(() => {
    if (timeLocked) {
      const updateTime = () => {
        const now = new Date();
        setQsoDate(formatDateForInput(now));
        setQsoTime(formatTimeForInput(now));
      };
      updateTime();
      timeIntervalRef.current = setInterval(updateTime, 1000);
      return () => {
        if (timeIntervalRef.current) {
          clearInterval(timeIntervalRef.current);
        }
      };
    }
  }, [timeLocked]);

  // Get current radio state
  const currentRadioState = selectedRadioId ? radioStates.get(selectedRadioId) : null;

  // Auto-populate from radio state when followRadio is enabled
  useEffect(() => {
    if (followRadio && currentRadioState) {
      const frequencyKhz = (currentRadioState.frequencyHz / 1000).toFixed(3);
      setFormData(prev => ({
        ...prev,
        frequency: frequencyKhz,
        band: currentRadioState.band || prev.band,
        mode: normalizeMode(currentRadioState.mode) || prev.mode,
      }));
    }
  }, [followRadio, currentRadioState?.frequencyHz, currentRadioState?.band, currentRadioState?.mode]);

  // Auto-populate name from QRZ when nameLocked is true
  useEffect(() => {
    if (nameLocked && focusedCallsignInfo?.name) {
      setFormData(prev => ({
        ...prev,
        name: focusedCallsignInfo.name || '',
      }));
    }
  }, [nameLocked, focusedCallsignInfo?.name]);

  // Auto-populate from DX cluster spot selection
  useEffect(() => {
    if (selectedSpot) {
      const frequencyKhz = (selectedSpot.frequency / 1000).toFixed(3);
      const band = getBandFromFrequency(selectedSpot.frequency);
      const mode = selectedSpot.mode ? normalizeMode(selectedSpot.mode) : formData.mode;

      setFormData(prev => ({
        ...prev,
        callsign: selectedSpot.dxCall,
        frequency: frequencyKhz,
        band: band || prev.band,
        mode: mode,
      }));

      // Clear the selected spot after processing to allow re-selection of same spot
      setSelectedSpot(null);
    }
  }, [selectedSpot, setSelectedSpot]);

  // Update RST defaults when mode changes
  useEffect(() => {
    const defaultRst = getDefaultRst(formData.mode);
    setFormData(prev => ({
      ...prev,
      rstSent: defaultRst,
      rstRcvd: defaultRst,
    }));
  }, [formData.mode]);

  // Helper to determine band from frequency in Hz
  const getBandFromFrequency = (freqHz: number): string | null => {
    const freqKhz = freqHz / 1000;
    if (freqKhz >= 1800 && freqKhz <= 2000) return '160m';
    if (freqKhz >= 3500 && freqKhz <= 4000) return '80m';
    if (freqKhz >= 7000 && freqKhz <= 7300) return '40m';
    if (freqKhz >= 10100 && freqKhz <= 10150) return '30m';
    if (freqKhz >= 14000 && freqKhz <= 14350) return '20m';
    if (freqKhz >= 18068 && freqKhz <= 18168) return '17m';
    if (freqKhz >= 21000 && freqKhz <= 21450) return '15m';
    if (freqKhz >= 24890 && freqKhz <= 24990) return '12m';
    if (freqKhz >= 28000 && freqKhz <= 29700) return '10m';
    if (freqKhz >= 50000 && freqKhz <= 54000) return '6m';
    if (freqKhz >= 144000 && freqKhz <= 148000) return '2m';
    if (freqKhz >= 420000 && freqKhz <= 450000) return '70cm';
    return null;
  };

  // Normalize mode names from radio to match our MODES list
  const normalizeMode = (mode: string): string => {
    const upperMode = mode?.toUpperCase() || '';
    // Map common variations
    if (upperMode.includes('LSB') || upperMode.includes('USB')) return 'SSB';
    if (upperMode.includes('CW')) return 'CW';
    if (upperMode.includes('FT8')) return 'FT8';
    if (upperMode.includes('FT4')) return 'FT4';
    if (upperMode.includes('RTTY') || upperMode.includes('FSK')) return 'RTTY';
    if (upperMode.includes('PSK')) return 'PSK31';
    if (upperMode.includes('AM')) return 'AM';
    if (upperMode.includes('FM') || upperMode.includes('NFM')) return 'FM';
    // Return original if in list, otherwise default
    return MODES.includes(upperMode) ? upperMode : 'SSB';
  };

  // Get RST options based on mode
  const getRstOptions = (mode: string): string[] => {
    return mode === 'CW' ? RST_CW_DIGITAL : RST_PHONE;
  };

  const toggleFollowRadio = () => {
    updateRadioSettings({ followRadio: !followRadio });
  };

  const createQso = useMutation({
    mutationFn: (data: CreateQsoRequest) => api.createQso(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['qsos'] });
      queryClient.invalidateQueries({ queryKey: ['statistics'] });
      // Save focused callsign to map overlay BEFORE clearing (only logged QSOs persist on map)
      const info = useAppStore.getState().focusedCallsignInfo;
      if (info?.latitude != null && info?.longitude != null) {
        const mapImage = {
          callsign: info.callsign,
          imageUrl: info.imageUrl ?? undefined,
          latitude: info.latitude,
          longitude: info.longitude,
          name: info.name ?? undefined,
          country: info.country ?? undefined,
          grid: info.grid ?? undefined,
          savedAt: new Date().toISOString(),
        };
        addCallsignMapImage(mapImage);
        persistCallsignMapImage(mapImage).catch(() => {});
      }
      // Clear callsign from all controls (QRZ profile, rotator, log history filter, etc.)
      clearCallsignFromAllControls();
      // Clear form
      setFormData({
        ...formData,
        callsign: '',
        name: '',
        grid: '',
        comment: '',
        notes: '',
        frequency: '',
        rstSentPlus: '',
        rstRcvdPlus: '',
      });
    },
  });

  const handleCallsignChange = useCallback(async (value: string) => {
    const callsign = value.toUpperCase();
    setFormData(prev => ({ ...prev, callsign }));

    // Update log history filter to show matching entries
    setLogHistoryCallsignFilter(callsign.length > 0 ? callsign : null);

    if (callsign.length >= 3) {
      await focusCallsign(callsign, 'log-entry');
    } else {
      // Clear the focused callsign info when callsign is cleared or too short
      setFocusedCallsign(null);
      setFocusedCallsignInfo(null);
    }
  }, [focusCallsign, setFocusedCallsign, setFocusedCallsignInfo, setLogHistoryCallsignFilter]);

  const handleClear = useCallback(() => {
    setFormData({
      callsign: '',
      band: formData.band,
      mode: formData.mode,
      rstSent: '59',
      rstSentPlus: '',
      rstRcvd: '59',
      rstRcvdPlus: '',
      frequency: followRadio && currentRadioState ? formData.frequency : '',
      name: '',
      grid: '',
      comment: '',
      notes: '',
    });
    setTimeLocked(true);
    setNameLocked(true);
    clearCallsignFromAllControls();
  }, [formData.band, formData.mode, formData.frequency, followRadio, currentRadioState, clearCallsignFromAllControls]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.callsign) return;

    // Format RST with plus values if present
    const rstSent = formData.rstSentPlus
      ? `${formData.rstSent}+${formData.rstSentPlus}`
      : formData.rstSent;
    const rstRcvd = formData.rstRcvdPlus
      ? `${formData.rstRcvd}+${formData.rstRcvdPlus}`
      : formData.rstRcvd;

    // Use the timestamp from state (either live or manual)
    const qsoDateTime = new Date(`${qsoDate}T${qsoTime}:00.000Z`);
    createQso.mutate({
      callsign: formData.callsign,
      qsoDate: qsoDateTime.toISOString(),
      timeOn: qsoTime.replace(':', '') + '00',
      band: formData.band,
      mode: formData.mode,
      frequency: formData.frequency ? parseFloat(formData.frequency) : undefined,
      rstSent,
      rstRcvd,
      name: formData.name || focusedCallsignInfo?.name,
      grid: formData.grid || focusedCallsignInfo?.grid,
      country: focusedCallsignInfo?.country,
      comment: formData.comment,
      notes: formData.notes,
    });
  };

  return (
    <GlassPanel
      title="Log Entry"
      icon={<Radio className="w-5 h-5" />}
      actions={
        <button
          type="button"
          onClick={toggleFollowRadio}
          className={`flex items-center gap-1.5 px-2 py-1 text-xs font-ui rounded transition-all ${
            followRadio
              ? 'bg-accent-success/20 text-accent-success hover:bg-accent-success/30'
              : 'bg-dark-600 text-dark-300 hover:bg-dark-500'
          }`}
          title={followRadio ? 'Following radio frequency' : 'Not following radio'}
        >
          {followRadio ? (
            <>
              <Link className="w-3.5 h-3.5" />
              <span>Following</span>
            </>
          ) : (
            <>
              <Unlink className="w-3.5 h-3.5" />
              <span>Manual</span>
            </>
          )}
        </button>
      }
    >
      <form onSubmit={handleSubmit} onKeyDown={(e) => { if (e.key === 'Escape') { e.preventDefault(); handleClear(); } }} className="p-3 space-y-3">
        {/* Callsign, Band, Mode on one line */}
        <div className="flex gap-2 items-end">
          <div className="flex-1">
            <label className="text-xs font-ui text-dark-300 flex items-center gap-1 mb-1">
              {isLookingUpCallsign ? (
                <Loader2 className="w-3 h-3 animate-spin text-accent-primary" />
              ) : (
                <Search className="w-3 h-3" />
              )}
              Callsign
              {isLookingUpCallsign && (
                <span className="text-accent-primary text-[10px]">Looking up...</span>
              )}
            </label>
            <input
              type="text"
              value={formData.callsign}
              onChange={(e) => handleCallsignChange(e.target.value)}
              placeholder="Callsign"
              className="glass-input w-full font-mono font-bold tracking-wider uppercase"
              autoFocus
            />
          </div>
          <div className="w-28">
            <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
              Band
              {followRadio && currentRadioState && (
                <span className="w-1.5 h-1.5 rounded-full bg-accent-success" title="From radio" />
              )}
            </label>
            <select
              value={formData.band}
              onChange={(e) => setFormData(prev => ({ ...prev, band: e.target.value }))}
              className={`glass-input w-full text-sm font-mono ${
                followRadio && currentRadioState ? 'border-accent-success/30' : ''
              }`}
            >
              {BANDS.map(band => (
                <option key={band} value={band}>{band}</option>
              ))}
            </select>
          </div>
          <div className="w-28">
            <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
              Mode
              {followRadio && currentRadioState && (
                <span className="w-1.5 h-1.5 rounded-full bg-accent-success" title="From radio" />
              )}
            </label>
            <select
              value={formData.mode}
              onChange={(e) => setFormData(prev => ({ ...prev, mode: e.target.value }))}
              className={`glass-input w-full text-sm font-mono ${
                followRadio && currentRadioState ? 'border-accent-success/30' : ''
              }`}
            >
              {MODES.map(mode => (
                <option key={mode} value={mode}>{mode}</option>
              ))}
            </select>
          </div>
        </div>

        {/* Callsign Info Card - Loading State */}
        {isLookingUpCallsign && formData.callsign && (
          <div className="bg-dark-700/50 rounded-lg p-2 border border-glass-100 animate-fade-in">
            <div className="flex items-center gap-2">
              <div className="w-10 h-10 rounded-lg bg-dark-600 flex items-center justify-center">
                <Loader2 className="w-5 h-5 text-accent-primary animate-spin" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="font-medium font-ui text-dark-300 text-sm">Looking up callsign...</p>
              </div>
            </div>
          </div>
        )}

        {/* Callsign Info Card - Data (only show if callsign matches to avoid stale data from out-of-order responses) */}
        {!isLookingUpCallsign && focusedCallsignInfo && formData.callsign &&
         focusedCallsignInfo.callsign?.toUpperCase() === formData.callsign.toUpperCase() && (
          <div className="bg-dark-700/50 rounded-lg p-2 border border-glass-100 animate-fade-in">
            <div className="flex items-center gap-2">
              {focusedCallsignInfo.imageUrl ? (
                <img
                  src={focusedCallsignInfo.imageUrl}
                  alt={focusedCallsignInfo.callsign}
                  className="w-10 h-10 rounded-lg object-cover"
                />
              ) : (
                <div className="w-10 h-10 rounded-lg bg-dark-600 flex items-center justify-center">
                  <User className="w-5 h-5 text-dark-300" />
                </div>
              )}
              <div className="flex-1 min-w-0">
                <p className="font-medium text-gray-100 text-sm truncate">
                  {focusedCallsignInfo.name || 'No name on file'}
                </p>
                <div className="flex items-center gap-2 text-xs text-dark-300">
                  <span>{getCountryFlag(focusedCallsignInfo.country) || ''}</span>
                  <span className="truncate">{focusedCallsignInfo.country || 'Unknown'}</span>
                  {focusedCallsignInfo.grid && (
                    <>
                      <MapPin className="w-3 h-3" />
                      <span className="font-mono">{focusedCallsignInfo.grid}</span>
                    </>
                  )}
                </div>
              </div>
              <div className="text-3xl" title={focusedCallsignInfo.country || 'Unknown country'}>
                {getCountryFlag(focusedCallsignInfo.country, '')}
              </div>
            </div>
          </div>
        )}

        {/* Name field with lock pattern */}
        <div>
          <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
            <User className="w-3 h-3" />
            Name
            {nameLocked && focusedCallsignInfo?.name && (
              <span className="w-1.5 h-1.5 rounded-full bg-accent-primary" title="From QRZ" />
            )}
          </label>
          <div className="flex items-center gap-2">
            <input
              type="text"
              value={formData.name}
              onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
              placeholder={nameLocked && focusedCallsignInfo?.name ? focusedCallsignInfo.name : 'Operator name...'}
              className={`glass-input flex-1 text-sm ${
                nameLocked && focusedCallsignInfo?.name ? 'border-accent-primary/30' : ''
              }`}
              readOnly={nameLocked && !!focusedCallsignInfo?.name}
            />
            <button
              type="button"
              onClick={() => setNameLocked(!nameLocked)}
              className={`p-1.5 rounded transition-colors ${
                nameLocked
                  ? 'text-dark-300 hover:text-dark-200'
                  : 'text-accent-primary hover:text-accent-primary/80'
              }`}
              title={nameLocked ? 'Unlock to edit name' : 'Lock to auto-fill from QRZ'}
              tabIndex={-1}
            >
              {nameLocked ? <LockOpen className="w-3.5 h-3.5" /> : <Lock className="w-3.5 h-3.5" />}
            </button>
          </div>
        </div>

        {/* Frequency, RST Sent, RST Rcvd on one line */}
        <div className="flex gap-3 items-end">
          <div className="w-28">
            <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
              Freq (kHz)
              {followRadio && currentRadioState && (
                <span className="w-1.5 h-1.5 rounded-full bg-accent-success" title="From radio" />
              )}
            </label>
            <input
              type="text"
              value={formData.frequency}
              onChange={(e) => {
                const frequency = e.target.value;
                const freqKhz = parseFloat(frequency);
                const newBand = !isNaN(freqKhz) ? getBandFromFrequency(freqKhz * 1000) : null;
                setFormData(prev => ({
                  ...prev,
                  frequency,
                  band: newBand || prev.band,
                }));
              }}
              placeholder="14250"
              className={`glass-input w-full font-mono text-sm ${
                followRadio && currentRadioState ? 'border-accent-success/30' : ''
              }`}
              readOnly={followRadio && !!currentRadioState}
            />
          </div>

          {/* TX RST */}
          <div>
            <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
              <span className="text-accent-success">TX</span> RST
            </label>
            <div className="flex items-center gap-1">
              <input
                type="text"
                list="rst-sent-options"
                value={formData.rstSent}
                onChange={(e) => setFormData(prev => ({ ...prev, rstSent: e.target.value }))}
                className="glass-input w-20 font-mono text-sm"
              />
              <datalist id="rst-sent-options">
                {getRstOptions(formData.mode).map(rst => (
                  <option key={rst} value={rst} />
                ))}
              </datalist>
              {supportsDbEnhancement(formData.mode) && formData.rstSent.endsWith('9') && (
                <>
                  <span className="text-dark-300">+</span>
                  <select
                    value={formData.rstSentPlus}
                    onChange={(e) => setFormData(prev => ({ ...prev, rstSentPlus: e.target.value }))}
                    className="glass-input w-20 font-mono text-sm"
                  >
                    <option value="">--</option>
                    <option value="10">10</option>
                    <option value="20">20</option>
                    <option value="30">30</option>
                    <option value="40">40</option>
                  </select>
                </>
              )}
            </div>
          </div>

          {/* Separator */}
          <div className="flex items-end pb-2">
            <span className="text-dark-600 text-lg">/</span>
          </div>

          {/* RX RST */}
          <div>
            <label className="text-xs font-ui text-dark-300 mb-1 flex items-center gap-1">
              <span className="text-accent-secondary">RX</span> RST
            </label>
            <div className="flex items-center gap-1">
              <input
                type="text"
                list="rst-rcvd-options"
                value={formData.rstRcvd}
                onChange={(e) => setFormData(prev => ({ ...prev, rstRcvd: e.target.value }))}
                className="glass-input w-20 font-mono text-sm"
              />
              <datalist id="rst-rcvd-options">
                {getRstOptions(formData.mode).map(rst => (
                  <option key={rst} value={rst} />
                ))}
              </datalist>
              {supportsDbEnhancement(formData.mode) && formData.rstRcvd.endsWith('9') && (
                <>
                  <span className="text-dark-300">+</span>
                  <select
                    value={formData.rstRcvdPlus}
                    onChange={(e) => setFormData(prev => ({ ...prev, rstRcvdPlus: e.target.value }))}
                    className="glass-input w-20 font-mono text-sm"
                  >
                    <option value="">--</option>
                    <option value="10">10</option>
                    <option value="20">20</option>
                    <option value="30">30</option>
                    <option value="40">40</option>
                  </select>
                </>
              )}
            </div>
          </div>
        </div>

        {/* Comment */}
        <div>
          <label className="text-xs font-ui text-dark-300 mb-1 block">Comment</label>
          <input
            type="text"
            value={formData.comment}
            onChange={(e) => setFormData(prev => ({ ...prev, comment: e.target.value }))}
            placeholder="QSO comment..."
            className="glass-input w-full text-sm"
          />
        </div>

        {/* Notes */}
        <div>
          <label className="text-xs font-ui text-dark-300 mb-1 block">Notes</label>
          <input
            type="text"
            value={formData.notes}
            onChange={(e) => setFormData(prev => ({ ...prev, notes: e.target.value }))}
            placeholder="Personal notes..."
            className="glass-input w-full text-sm"
          />
        </div>

        {/* Timestamp - compact display with optional edit */}
        <div className="flex items-center gap-2 text-xs text-dark-300">
          <Clock className="w-3 h-3" />
          {timeLocked ? (
            <>
              <span className="font-mono">{qsoDate} {qsoTime} UTC</span>
              <button
                type="button"
                onClick={() => setTimeLocked(false)}
                className="text-dark-300 hover:text-dark-200 transition-colors"
                title="Edit timestamp"
                tabIndex={-1}
              >
                <LockOpen className="w-3 h-3" />
              </button>
            </>
          ) : (
            <>
              <input
                type="date"
                value={qsoDate}
                onChange={(e) => setQsoDate(e.target.value)}
                className="glass-input px-1 py-0.5 font-mono text-xs w-28"
              />
              <input
                type="time"
                value={qsoTime}
                onChange={(e) => setQsoTime(e.target.value)}
                className="glass-input px-1 py-0.5 font-mono text-xs w-20"
              />
              <span>UTC</span>
              <button
                type="button"
                onClick={() => setTimeLocked(true)}
                className="text-accent-primary hover:text-accent-primary/80 transition-colors"
                title="Lock to system time"
                tabIndex={-1}
              >
                <Lock className="w-3 h-3" />
              </button>
            </>
          )}
        </div>

        {/* Submit / Clear */}
        <div className="flex gap-2">
          <button
            type="submit"
            disabled={!formData.callsign || createQso.isPending}
            className="glass-button-success flex-1 flex items-center justify-center gap-2 py-2 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Send className="w-4 h-4" />
            {createQso.isPending ? 'Logging...' : 'Log QSO'}
          </button>
          <button
            type="button"
            onClick={handleClear}
            disabled={createQso.isPending}
            className="glass-button flex items-center justify-center gap-2 py-2 px-4 disabled:opacity-50 disabled:cursor-not-allowed"
            title="Clear QSO details"
          >
            <X className="w-4 h-4" />
            Clear
          </button>
        </div>
      </form>
    </GlassPanel>
  );
}
