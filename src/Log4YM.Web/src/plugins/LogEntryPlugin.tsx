import { useState, useCallback, useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Send, Search, User, MapPin, Radio, Link, Unlink, Clock, Lock, LockOpen, Loader2 } from 'lucide-react';
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

export function LogEntryPlugin() {
  const queryClient = useQueryClient();
  const { focusCallsign } = useSignalR();
  const { focusedCallsignInfo, radioStates, selectedRadioId, isLookingUpCallsign, setFocusedCallsign, setFocusedCallsignInfo, setLogHistoryCallsignFilter, clearCallsignFromAllControls } = useAppStore();
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
      const frequencyMhz = (currentRadioState.frequencyHz / 1000000).toFixed(6);
      setFormData(prev => ({
        ...prev,
        frequency: frequencyMhz,
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

  const toggleFollowRadio = () => {
    updateRadioSettings({ followRadio: !followRadio });
  };

  const createQso = useMutation({
    mutationFn: (data: CreateQsoRequest) => api.createQso(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['qsos'] });
      queryClient.invalidateQueries({ queryKey: ['statistics'] });
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
          className={`flex items-center gap-1.5 px-2 py-1 text-xs rounded transition-all ${
            followRadio
              ? 'bg-accent-success/20 text-accent-success hover:bg-accent-success/30'
              : 'bg-dark-600 text-gray-400 hover:bg-dark-500'
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
      <form onSubmit={handleSubmit} className="p-3 space-y-3">
        {/* Callsign, Band, Mode on one line */}
        <div className="flex gap-2 items-end">
          <div className="flex-1">
            <label className="text-xs text-gray-400 flex items-center gap-1 mb-1">
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
            <label className="text-xs text-gray-400 mb-1 flex items-center gap-1">
              Band
              {followRadio && currentRadioState && (
                <span className="w-1.5 h-1.5 rounded-full bg-accent-success" title="From radio" />
              )}
            </label>
            <select
              value={formData.band}
              onChange={(e) => setFormData(prev => ({ ...prev, band: e.target.value }))}
              className={`glass-input w-full text-sm ${
                followRadio && currentRadioState ? 'border-accent-success/30' : ''
              }`}
            >
              {BANDS.map(band => (
                <option key={band} value={band}>{band}</option>
              ))}
            </select>
          </div>
          <div className="w-28">
            <label className="text-xs text-gray-400 mb-1 flex items-center gap-1">
              Mode
              {followRadio && currentRadioState && (
                <span className="w-1.5 h-1.5 rounded-full bg-accent-success" title="From radio" />
              )}
            </label>
            <select
              value={formData.mode}
              onChange={(e) => setFormData(prev => ({ ...prev, mode: e.target.value }))}
              className={`glass-input w-full text-sm ${
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
                <p className="font-medium text-gray-400 text-sm">Looking up callsign...</p>
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
                  <User className="w-5 h-5 text-gray-500" />
                </div>
              )}
              <div className="flex-1 min-w-0">
                <p className="font-medium text-gray-100 text-sm truncate">
                  {focusedCallsignInfo.name || 'No name on file'}
                </p>
                <div className="flex items-center gap-2 text-xs text-gray-400">
                  <span>{getCountryFlag(focusedCallsignInfo.country) || '🏳️'}</span>
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
                {getCountryFlag(focusedCallsignInfo.country, '🏳️')}
              </div>
            </div>
          </div>
        )}

        {/* Name field with lock pattern */}
        <div>
          <label className="text-xs text-gray-400 mb-1 flex items-center gap-1">
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
                  ? 'text-gray-500 hover:text-gray-300'
                  : 'text-amber-400 hover:text-amber-300'
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
            <label className="text-xs text-gray-400 mb-1 flex items-center gap-1">
              Freq (MHz)
              {followRadio && currentRadioState && (
                <span className="w-1.5 h-1.5 rounded-full bg-accent-success" title="From radio" />
              )}
            </label>
            <input
              type="text"
              value={formData.frequency}
              onChange={(e) => setFormData(prev => ({ ...prev, frequency: e.target.value }))}
              placeholder="14.250"
              className={`glass-input w-full font-mono text-sm ${
                followRadio && currentRadioState ? 'border-accent-success/30' : ''
              }`}
              readOnly={followRadio && !!currentRadioState}
            />
          </div>

          {/* TX RST */}
          <div>
            <label className="text-xs text-gray-400 mb-1 flex items-center gap-1">
              <span className="text-green-400">TX</span> RST
            </label>
            <div className="flex items-center gap-1">
              <input
                type="text"
                value={formData.rstSent}
                onChange={(e) => setFormData(prev => ({ ...prev, rstSent: e.target.value }))}
                className="glass-input w-14 font-mono text-sm"
                maxLength={3}
              />
              {formData.rstSent.endsWith('9') && (
                <>
                  <span className="text-gray-500">+</span>
                  <select
                    value={formData.rstSentPlus}
                    onChange={(e) => setFormData(prev => ({ ...prev, rstSentPlus: e.target.value }))}
                    className="glass-input w-16 font-mono text-sm"
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
            <span className="text-gray-600 text-lg">/</span>
          </div>

          {/* RX RST */}
          <div>
            <label className="text-xs text-gray-400 mb-1 flex items-center gap-1">
              <span className="text-blue-400">RX</span> RST
            </label>
            <div className="flex items-center gap-1">
              <input
                type="text"
                value={formData.rstRcvd}
                onChange={(e) => setFormData(prev => ({ ...prev, rstRcvd: e.target.value }))}
                className="glass-input w-14 font-mono text-sm"
                maxLength={3}
              />
              {formData.rstRcvd.endsWith('9') && (
                <>
                  <span className="text-gray-500">+</span>
                  <select
                    value={formData.rstRcvdPlus}
                    onChange={(e) => setFormData(prev => ({ ...prev, rstRcvdPlus: e.target.value }))}
                    className="glass-input w-16 font-mono text-sm"
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
          <label className="text-xs text-gray-400 mb-1 block">Comment</label>
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
          <label className="text-xs text-gray-400 mb-1 block">Notes</label>
          <input
            type="text"
            value={formData.notes}
            onChange={(e) => setFormData(prev => ({ ...prev, notes: e.target.value }))}
            placeholder="Personal notes..."
            className="glass-input w-full text-sm"
          />
        </div>

        {/* Timestamp - compact display with optional edit */}
        <div className="flex items-center gap-2 text-xs text-gray-400">
          <Clock className="w-3 h-3" />
          {timeLocked ? (
            <>
              <span className="font-mono">{qsoDate} {qsoTime} UTC</span>
              <button
                type="button"
                onClick={() => setTimeLocked(false)}
                className="text-gray-500 hover:text-gray-300 transition-colors"
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
                className="text-amber-400 hover:text-amber-300 transition-colors"
                title="Lock to system time"
                tabIndex={-1}
              >
                <Lock className="w-3 h-3" />
              </button>
            </>
          )}
        </div>

        {/* Submit */}
        <button
          type="submit"
          disabled={!formData.callsign || createQso.isPending}
          className="glass-button-success w-full flex items-center justify-center gap-2 py-2 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Send className="w-4 h-4" />
          {createQso.isPending ? 'Logging...' : 'Log QSO'}
        </button>
      </form>
    </GlassPanel>
  );
}
