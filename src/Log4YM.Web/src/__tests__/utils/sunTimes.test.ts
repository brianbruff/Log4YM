import { describe, it, expect } from 'vitest';
import { calculateSunTimes, formatTime } from '../../utils/sunTimes';

describe('calculateSunTimes', () => {
  it('calculates sunrise/sunset for a known location and date', () => {
    // London on June 21 (summer solstice) - known sunrise ~4:43 UTC, sunset ~20:21 UTC
    const date = new Date('2024-06-21T12:00:00Z');
    const result = calculateSunTimes(51.5074, -0.1278, date);

    expect(result).not.toBeNull();
    if (result) {
      // Sunrise should be early morning UTC (roughly 3-5 AM UTC)
      const sunriseHour = result.sunrise.getUTCHours();
      expect(sunriseHour).toBeGreaterThanOrEqual(3);
      expect(sunriseHour).toBeLessThanOrEqual(5);

      // Sunset should be evening UTC (roughly 19-21 UTC)
      const sunsetHour = result.sunset.getUTCHours();
      expect(sunsetHour).toBeGreaterThanOrEqual(19);
      expect(sunsetHour).toBeLessThanOrEqual(21);
    }
  });

  it('calculates sunrise/sunset for equatorial location', () => {
    // Equator on equinox - sunrise ~6:00, sunset ~18:00
    const date = new Date('2024-03-20T12:00:00Z');
    const result = calculateSunTimes(0, 0, date);

    expect(result).not.toBeNull();
    if (result) {
      const sunriseHour = result.sunrise.getUTCHours();
      const sunsetHour = result.sunset.getUTCHours();
      // At equator on equinox, sunrise ~6 AM, sunset ~6 PM
      expect(sunriseHour).toBeGreaterThanOrEqual(5);
      expect(sunriseHour).toBeLessThanOrEqual(7);
      expect(sunsetHour).toBeGreaterThanOrEqual(17);
      expect(sunsetHour).toBeLessThanOrEqual(19);
    }
  });

  it('returns null for polar regions in winter (sun never rises)', () => {
    // North Pole in December - should be polar night
    const date = new Date('2024-12-21T12:00:00Z');
    const result = calculateSunTimes(89, 0, date);
    expect(result).toBeNull();
  });

  it('returns null for polar regions in summer (sun never sets)', () => {
    // North Pole in June - should be midnight sun
    const date = new Date('2024-06-21T12:00:00Z');
    const result = calculateSunTimes(89, 0, date);
    expect(result).toBeNull();
  });

  it('sunrise is before sunset', () => {
    const date = new Date('2024-09-15T12:00:00Z');
    const result = calculateSunTimes(40.7128, -74.006, date);

    expect(result).not.toBeNull();
    if (result) {
      expect(result.sunrise.getTime()).toBeLessThan(result.sunset.getTime());
    }
  });

  it('uses today when no date provided', () => {
    const result = calculateSunTimes(40.7128, -74.006);
    // Should return a result for most non-polar locations
    expect(result).not.toBeNull();
  });
});

describe('formatTime', () => {
  it('formats time as HH:MM', () => {
    const date = new Date('2024-06-15T14:30:00');
    const result = formatTime(date);
    expect(result).toMatch(/^\d{2}:\d{2}$/);
  });

  it('pads single-digit hours', () => {
    const date = new Date('2024-06-15T05:07:00');
    const result = formatTime(date);
    expect(result).toBe('05:07');
  });

  it('handles midnight', () => {
    const date = new Date('2024-06-15T00:00:00');
    const result = formatTime(date);
    expect(result).toBe('00:00');
  });

  it('handles 23:59', () => {
    const date = new Date('2024-06-15T23:59:00');
    const result = formatTime(date);
    expect(result).toBe('23:59');
  });
});
