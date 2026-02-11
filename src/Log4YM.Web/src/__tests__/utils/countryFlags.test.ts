import { describe, it, expect } from 'vitest';
import {
  getFlagByCountryName,
  getFlagByCountryCode,
  getCountryFlag,
} from '../../core/countryFlags';

describe('getFlagByCountryName', () => {
  it('returns flag for exact match', () => {
    expect(getFlagByCountryName('Ireland')).toBeDefined();
    expect(getFlagByCountryName('United States')).toBeDefined();
    expect(getFlagByCountryName('Germany')).toBeDefined();
  });

  it('is case-insensitive', () => {
    expect(getFlagByCountryName('ireland')).toBe(getFlagByCountryName('Ireland'));
    expect(getFlagByCountryName('IRELAND')).toBe(getFlagByCountryName('Ireland'));
    expect(getFlagByCountryName('IrElAnD')).toBe(getFlagByCountryName('Ireland'));
  });

  it('resolves aliases', () => {
    // UK aliases
    expect(getFlagByCountryName('UK')).toBe(getFlagByCountryName('United Kingdom'));
    expect(getFlagByCountryName('England')).toBe(getFlagByCountryName('United Kingdom'));
    expect(getFlagByCountryName('Great Britain')).toBe(getFlagByCountryName('United Kingdom'));

    // USA aliases
    expect(getFlagByCountryName('USA')).toBe(getFlagByCountryName('United States'));

    // Germany aliases
    expect(getFlagByCountryName('Fed. Rep. of Germany')).toBe(getFlagByCountryName('Germany'));
  });

  it('returns undefined for unknown countries', () => {
    expect(getFlagByCountryName('Atlantis')).toBeUndefined();
    expect(getFlagByCountryName('Narnia')).toBeUndefined();
  });

  it('returns undefined for null/undefined', () => {
    expect(getFlagByCountryName(null)).toBeUndefined();
    expect(getFlagByCountryName(undefined)).toBeUndefined();
    expect(getFlagByCountryName('')).toBeUndefined();
  });
});

describe('getFlagByCountryCode', () => {
  it('returns flag for valid code', () => {
    expect(getFlagByCountryCode('IE')).toBeDefined();
    expect(getFlagByCountryCode('US')).toBeDefined();
    expect(getFlagByCountryCode('DE')).toBeDefined();
  });

  it('is case-insensitive', () => {
    expect(getFlagByCountryCode('ie')).toBe(getFlagByCountryCode('IE'));
    expect(getFlagByCountryCode('us')).toBe(getFlagByCountryCode('US'));
  });

  it('returns undefined for invalid codes', () => {
    expect(getFlagByCountryCode('XX')).toBeUndefined();
    expect(getFlagByCountryCode('ZZ')).toBeUndefined();
  });

  it('returns undefined for null/undefined', () => {
    expect(getFlagByCountryCode(null)).toBeUndefined();
    expect(getFlagByCountryCode(undefined)).toBeUndefined();
  });
});

describe('getCountryFlag', () => {
  it('returns flag for known country', () => {
    const flag = getCountryFlag('Ireland');
    expect(flag).toBeDefined();
    expect(flag.length).toBeGreaterThan(0);
  });

  it('returns empty string fallback for unknown country', () => {
    expect(getCountryFlag('Atlantis')).toBe('');
  });

  it('returns custom fallback for unknown country', () => {
    expect(getCountryFlag('Atlantis', '?')).toBe('?');
  });

  it('returns empty string for null', () => {
    expect(getCountryFlag(null)).toBe('');
  });
});
