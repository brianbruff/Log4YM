import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { api } from '../../api/client';

// Mock global fetch
const mockFetch = vi.fn();
globalThis.fetch = mockFetch;

function mockResponse<T>(data: T, ok = true, status = 200) {
  return {
    ok,
    status,
    json: () => Promise.resolve(data),
    blob: () => Promise.resolve(new Blob()),
  };
}

describe('ApiClient', () => {
  beforeEach(() => {
    mockFetch.mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('getQsos', () => {
    it('fetches QSOs without query params', async () => {
      const mockData = { items: [], totalCount: 0, page: 1, pageSize: 50, totalPages: 0 };
      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      const result = await api.getQsos();
      expect(result).toEqual(mockData);
      expect(mockFetch).toHaveBeenCalledWith('/api/qsos', expect.objectContaining({
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      }));
    });

    it('appends query params when provided', async () => {
      const mockData = { items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 };
      mockFetch.mockResolvedValueOnce(mockResponse(mockData));

      await api.getQsos({ callsign: 'W1AW', band: '20m', page: 1, pageSize: 10 });
      const calledUrl = mockFetch.mock.calls[0][0] as string;
      expect(calledUrl).toContain('callsign=W1AW');
      expect(calledUrl).toContain('band=20m');
      expect(calledUrl).toContain('page=1');
      expect(calledUrl).toContain('pageSize=10');
    });
  });

  describe('createQso', () => {
    it('sends POST with QSO data', async () => {
      const newQso = {
        callsign: 'EI2ABC',
        qsoDate: '2024-06-15',
        timeOn: '14:30',
        band: '20m',
        mode: 'SSB',
      };
      const mockResult = { id: '123', ...newQso, createdAt: '2024-06-15T14:30:00Z' };
      mockFetch.mockResolvedValueOnce(mockResponse(mockResult));

      const result = await api.createQso(newQso);
      expect(result.id).toBe('123');
      expect(mockFetch).toHaveBeenCalledWith('/api/qsos', expect.objectContaining({
        method: 'POST',
        body: JSON.stringify(newQso),
      }));
    });
  });

  describe('generateTalkPoints', () => {
    it('sends POST to AI talk-points endpoint', async () => {
      const request = { callsign: 'W1AW', currentBand: '20m', currentMode: 'SSB' };
      const mockResult = {
        callsign: 'W1AW',
        previousQsos: [],
        talkPoints: ['Nice to meet you!'],
        generatedText: 'Some text',
      };
      mockFetch.mockResolvedValueOnce(mockResponse(mockResult));

      const result = await api.generateTalkPoints(request);
      expect(result.callsign).toBe('W1AW');
      expect(mockFetch).toHaveBeenCalledWith('/api/ai/talk-points', expect.objectContaining({
        method: 'POST',
        body: JSON.stringify(request),
      }));
    });
  });

  describe('chat', () => {
    it('sends POST to AI chat endpoint', async () => {
      const request = { callsign: 'W1AW', question: 'Tell me about this station' };
      const mockResult = { answer: 'W1AW is the ARRL station...' };
      mockFetch.mockResolvedValueOnce(mockResponse(mockResult));

      const result = await api.chat(request);
      expect(result.answer).toContain('W1AW');
      expect(mockFetch).toHaveBeenCalledWith('/api/ai/chat', expect.objectContaining({
        method: 'POST',
      }));
    });
  });

  describe('error handling', () => {
    it('throws on non-OK response', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse({}, false, 500));

      await expect(api.getQsos()).rejects.toThrow('API error: 500');
    });

    it('throws on 404', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse({}, false, 404));

      await expect(api.getQso('nonexistent')).rejects.toThrow('API error: 404');
    });

    it('throws on network error', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      await expect(api.getHealth()).rejects.toThrow('Network error');
    });
  });

  describe('URL construction', () => {
    it('constructs correct URL for getSpots with query params', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse([]));

      await api.getSpots({ band: '20m', mode: 'CW', limit: 100 });
      const calledUrl = mockFetch.mock.calls[0][0] as string;
      expect(calledUrl).toContain('/api/spots');
      expect(calledUrl).toContain('band=20m');
      expect(calledUrl).toContain('mode=CW');
      expect(calledUrl).toContain('limit=100');
    });

    it('constructs correct URL for getHealth', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse({ status: 'healthy', timestamp: '' }));

      await api.getHealth();
      expect(mockFetch).toHaveBeenCalledWith('/api/health', expect.any(Object));
    });

    it('constructs correct URL for lookupCallsignQrz with encoding', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse({ callsign: 'W1/AW' }));

      await api.lookupCallsignQrz('W1/AW');
      const calledUrl = mockFetch.mock.calls[0][0] as string;
      expect(calledUrl).toContain('/api/qrz/lookup/');
      expect(calledUrl).toContain(encodeURIComponent('W1/AW'));
    });

    it('constructs correct URL for getContests with days param', async () => {
      mockFetch.mockResolvedValueOnce(mockResponse([]));

      await api.getContests(14);
      const calledUrl = mockFetch.mock.calls[0][0] as string;
      expect(calledUrl).toBe('/api/contests?days=14');
    });
  });
});
