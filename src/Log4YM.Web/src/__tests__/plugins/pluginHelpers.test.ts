import { describe, it, expect } from 'vitest';

// Since getBandFromFrequency, normalizeMode, and inferModeFromFrequency are
// defined inline in plugin components, we replicate the logic here for testing.
// This tests the actual algorithm rather than importing non-exported functions.

// --- getBandFromFrequency (from ChatAiPlugin) ---
// Input is frequency in Hz, returns band string
const BAND_RANGES: Record<string, [number, number]> = {
  '160m': [1800, 2000], '80m': [3500, 4000], '60m': [5330, 5410],
  '40m': [7000, 7300], '30m': [10100, 10150], '20m': [14000, 14350],
  '17m': [18068, 18168], '15m': [21000, 21450], '12m': [24890, 24990],
  '10m': [28000, 29700], '6m': [50000, 54000],
};

function getBandFromFrequency(freqHz: number): string | undefined {
  const freqKhz = freqHz / 1000;
  for (const [band, [min, max]] of Object.entries(BAND_RANGES)) {
    if (freqKhz >= min && freqKhz <= max) return band;
  }
  return undefined;
}

// --- getBandFromFrequency (from LogEntryPlugin) - Hz input ---
function getBandFromFrequencyLogEntry(freqHz: number): string | null {
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
}

// --- normalizeMode (from LogEntryPlugin) ---
const MODES = ['SSB', 'CW', 'FT8', 'FT4', 'RTTY', 'PSK31', 'AM', 'FM'];

function normalizeMode(mode: string): string {
  const upperMode = mode?.toUpperCase() || '';
  if (upperMode.includes('LSB') || upperMode.includes('USB')) return 'SSB';
  if (upperMode.includes('CW')) return 'CW';
  if (upperMode.includes('FT8')) return 'FT8';
  if (upperMode.includes('FT4')) return 'FT4';
  if (upperMode.includes('RTTY') || upperMode.includes('FSK')) return 'RTTY';
  if (upperMode.includes('PSK')) return 'PSK31';
  if (upperMode.includes('AM')) return 'AM';
  if (upperMode.includes('FM') || upperMode.includes('NFM')) return 'FM';
  return MODES.includes(upperMode) ? upperMode : 'SSB';
}

// --- inferModeFromFrequency (from ClusterPlugin) ---
// Input is frequency in kHz
function inferModeFromFrequency(freq: number): string | null {
  const ft8Freqs = [1840, 3573, 7074, 10136, 14074, 18100, 21074, 24915, 28074, 50313];
  const ft4Freqs = [3575, 7047, 10140, 14080, 18104, 21140, 24919, 28180];
  for (const f of ft8Freqs) {
    if (Math.abs(freq - f) <= 5) return 'FT8';
  }
  for (const f of ft4Freqs) {
    if (Math.abs(freq - f) <= 5) return 'FT4';
  }
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
}

describe('getBandFromFrequency (ChatAiPlugin version)', () => {
  it('identifies 20m band', () => {
    expect(getBandFromFrequency(14074000)).toBe('20m'); // 14074 kHz in Hz
    expect(getBandFromFrequency(14250000)).toBe('20m');
  });

  it('identifies 40m band', () => {
    expect(getBandFromFrequency(7074000)).toBe('40m');
  });

  it('identifies 10m band', () => {
    expect(getBandFromFrequency(28500000)).toBe('10m');
  });

  it('identifies 6m band', () => {
    expect(getBandFromFrequency(50313000)).toBe('6m');
  });

  it('returns undefined for out-of-band frequency', () => {
    expect(getBandFromFrequency(1000000)).toBeUndefined();   // 1 MHz
    expect(getBandFromFrequency(100000000)).toBeUndefined();  // 100 MHz
  });

  it('handles band edges', () => {
    expect(getBandFromFrequency(14000000)).toBe('20m'); // lower edge
    expect(getBandFromFrequency(14350000)).toBe('20m'); // upper edge
    expect(getBandFromFrequency(13999000)).toBeUndefined(); // just below
  });
});

describe('getBandFromFrequency (LogEntryPlugin version)', () => {
  it('identifies VHF/UHF bands', () => {
    expect(getBandFromFrequencyLogEntry(145000000)).toBe('2m');  // 145 MHz
    expect(getBandFromFrequencyLogEntry(435000000)).toBe('70cm'); // 435 MHz
  });

  it('identifies HF bands', () => {
    expect(getBandFromFrequencyLogEntry(14074000)).toBe('20m');
    expect(getBandFromFrequencyLogEntry(7074000)).toBe('40m');
  });

  it('returns null for out-of-band', () => {
    expect(getBandFromFrequencyLogEntry(1000000)).toBeNull();
  });
});

describe('normalizeMode', () => {
  it('maps USB/LSB to SSB', () => {
    expect(normalizeMode('USB')).toBe('SSB');
    expect(normalizeMode('LSB')).toBe('SSB');
    expect(normalizeMode('usb')).toBe('SSB');
  });

  it('maps CW variants', () => {
    expect(normalizeMode('CW')).toBe('CW');
    expect(normalizeMode('CW-R')).toBe('CW');
    expect(normalizeMode('cw')).toBe('CW');
  });

  it('maps digital modes', () => {
    expect(normalizeMode('FT8')).toBe('FT8');
    expect(normalizeMode('FT4')).toBe('FT4');
    expect(normalizeMode('RTTY')).toBe('RTTY');
    expect(normalizeMode('FSK')).toBe('RTTY');
    expect(normalizeMode('PSK31')).toBe('PSK31');
    expect(normalizeMode('PSK')).toBe('PSK31');
  });

  it('maps FM variants', () => {
    expect(normalizeMode('FM')).toBe('FM');
    expect(normalizeMode('NFM')).toBe('FM');
  });

  it('maps AM', () => {
    expect(normalizeMode('AM')).toBe('AM');
  });

  it('returns SSB as default for unknown modes', () => {
    expect(normalizeMode('UNKNOWN')).toBe('SSB');
    expect(normalizeMode('DIGITAL')).toBe('SSB');
  });
});

describe('inferModeFromFrequency', () => {
  it('identifies FT8 frequencies', () => {
    expect(inferModeFromFrequency(14074)).toBe('FT8');
    expect(inferModeFromFrequency(7074)).toBe('FT8');
    expect(inferModeFromFrequency(3573)).toBe('FT8');
    expect(inferModeFromFrequency(21074)).toBe('FT8');
  });

  it('identifies FT8 within 5 kHz tolerance', () => {
    expect(inferModeFromFrequency(14076)).toBe('FT8'); // 14074 + 2
    expect(inferModeFromFrequency(14071)).toBe('FT8'); // 14074 - 3
  });

  it('identifies FT4 frequencies', () => {
    expect(inferModeFromFrequency(14080)).toBe('FT4');
    expect(inferModeFromFrequency(7047)).toBe('FT4');
  });

  it('identifies CW portion of bands', () => {
    expect(inferModeFromFrequency(14030)).toBe('CW');
    expect(inferModeFromFrequency(7020)).toBe('CW');
    expect(inferModeFromFrequency(3530)).toBe('CW');
  });

  it('identifies SSB portion of bands', () => {
    expect(inferModeFromFrequency(14200)).toBe('SSB');
    expect(inferModeFromFrequency(7150)).toBe('SSB');
    expect(inferModeFromFrequency(3700)).toBe('SSB');
  });

  it('returns null for ambiguous frequencies', () => {
    expect(inferModeFromFrequency(1000)).toBeNull();
    expect(inferModeFromFrequency(100000)).toBeNull();
  });
});
