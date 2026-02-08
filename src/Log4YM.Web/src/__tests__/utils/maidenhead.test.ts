import { describe, it, expect } from 'vitest';
import {
  gridToLatLon,
  calculateDistance,
  calculateBearing,
  getAnimationDuration,
} from '../../utils/maidenhead';

describe('gridToLatLon', () => {
  it('returns null for empty input', () => {
    expect(gridToLatLon('')).toBeNull();
  });

  it('returns null for invalid length', () => {
    expect(gridToLatLon('FN3')).toBeNull();
    expect(gridToLatLon('FN31p')).toBeNull();
    expect(gridToLatLon('FN31prx')).toBeNull();
  });

  it('returns null for invalid field characters', () => {
    expect(gridToLatLon('ZZ00')).toBeNull(); // Z > R
    expect(gridToLatLon('SS00')).toBeNull(); // S > R
  });

  it('returns null for invalid subsquare characters', () => {
    expect(gridToLatLon('FN31YY')).toBeNull(); // Y > X
  });

  it('converts a valid 4-char grid to lat/lon (center of square)', () => {
    // IO63 (Ireland) -> center of the 2° x 1° square
    const result = gridToLatLon('IO63');
    expect(result).not.toBeNull();
    // IO: I=8 -> lon: 8*20-180 = -20, O=14 -> lat: 14*10-90 = 50
    // 63: 6*2 = 12, 3*1 = 3
    // center: lon = -20+12+1 = -7, lat = 50+3+0.5 = 53.5
    expect(result!.lon).toBe(-7);
    expect(result!.lat).toBe(53.5);
  });

  it('converts a valid 6-char grid to lat/lon (center of subsquare)', () => {
    // FN31pr
    const result = gridToLatLon('FN31pr');
    expect(result).not.toBeNull();
    // F=5: lon offset = 5*20 = 100 -> -180+100 = -80
    // N=13: lat offset = 13*10 = 130 -> -90+130 = 40
    // 31: lon += 3*2=6, lat += 1
    // pr: p=15, r=17
    // lon += 15*(2/24) = 1.25, lat += 17*(1/24) = 0.70833
    // center: lon += (2/24)/2 = 0.04167, lat += (1/24)/2 = 0.02083
    // total lon: -80+6+1.25+0.04167 = -72.708..., lat: 40+1+0.70833+0.02083 = 41.729...
    expect(result!.lat).toBeCloseTo(41.729, 2);
    expect(result!.lon).toBeCloseTo(-72.708, 2);
  });

  it('is case-insensitive', () => {
    const upper = gridToLatLon('FN31PR');
    const lower = gridToLatLon('fn31pr');
    const mixed = gridToLatLon('Fn31pR');
    expect(upper).toEqual(lower);
    expect(upper).toEqual(mixed);
  });

  it('handles grid square at origin AA00', () => {
    const result = gridToLatLon('AA00');
    expect(result).not.toBeNull();
    // A=0: lon = -180+0+0+1 = -179, lat = -90+0+0+0.5 = -89.5
    expect(result!.lon).toBe(-179);
    expect(result!.lat).toBe(-89.5);
  });

  it('handles grid square RR99 (max field)', () => {
    const result = gridToLatLon('RR99');
    expect(result).not.toBeNull();
    // R=17: lon = -180+17*20+9*2+1 = -180+340+18+1 = 179
    // R=17: lat = -90+17*10+9*1+0.5 = -90+170+9+0.5 = 89.5
    expect(result!.lon).toBe(179);
    expect(result!.lat).toBe(89.5);
  });
});

describe('calculateDistance', () => {
  it('returns 0 for same point', () => {
    expect(calculateDistance(40, -74, 40, -74)).toBe(0);
  });

  it('calculates NYC to London distance (~5570 km)', () => {
    // NYC: 40.7128° N, 74.0060° W
    // London: 51.5074° N, 0.1278° W
    const distance = calculateDistance(40.7128, -74.006, 51.5074, -0.1278);
    expect(distance).toBeGreaterThan(5500);
    expect(distance).toBeLessThan(5650);
  });

  it('calculates antipodal points (~20000 km)', () => {
    // North pole to South pole
    const distance = calculateDistance(90, 0, -90, 0);
    expect(distance).toBeCloseTo(20015, -2); // ~20015 km
  });

  it('calculates short distances accurately', () => {
    // Two points ~111 km apart (1 degree latitude at equator)
    const distance = calculateDistance(0, 0, 1, 0);
    expect(distance).toBeCloseTo(111.19, 0);
  });
});

describe('calculateBearing', () => {
  it('returns ~0 for due north', () => {
    const bearing = calculateBearing(0, 0, 10, 0);
    expect(bearing).toBeCloseTo(0, 0);
  });

  it('returns ~90 for due east at equator', () => {
    const bearing = calculateBearing(0, 0, 0, 10);
    expect(bearing).toBeCloseTo(90, 0);
  });

  it('returns ~180 for due south', () => {
    const bearing = calculateBearing(10, 0, 0, 0);
    expect(bearing).toBeCloseTo(180, 0);
  });

  it('returns ~270 for due west at equator', () => {
    const bearing = calculateBearing(0, 10, 0, 0);
    expect(bearing).toBeCloseTo(270, 0);
  });

  it('returns value in 0-360 range', () => {
    const bearing = calculateBearing(51.5, -0.1, 40.7, -74.0);
    expect(bearing).toBeGreaterThanOrEqual(0);
    expect(bearing).toBeLessThan(360);
  });
});

describe('getAnimationDuration', () => {
  it('returns 2 for < 500 km', () => {
    expect(getAnimationDuration(0)).toBe(2);
    expect(getAnimationDuration(100)).toBe(2);
    expect(getAnimationDuration(499)).toBe(2);
  });

  it('returns 3 for 500-2000 km', () => {
    expect(getAnimationDuration(500)).toBe(3);
    expect(getAnimationDuration(1000)).toBe(3);
    expect(getAnimationDuration(1999)).toBe(3);
  });

  it('returns 4 for 2000-5000 km', () => {
    expect(getAnimationDuration(2000)).toBe(4);
    expect(getAnimationDuration(3500)).toBe(4);
    expect(getAnimationDuration(4999)).toBe(4);
  });

  it('returns 5 for > 5000 km', () => {
    expect(getAnimationDuration(5000)).toBe(5);
    expect(getAnimationDuration(10000)).toBe(5);
    expect(getAnimationDuration(20000)).toBe(5);
  });
});
