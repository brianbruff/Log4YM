import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useLayoutStore, defaultLayout } from '../../store/layoutStore';
import type { IJsonModel } from 'flexlayout-react';

// Mock fetch for settings API
const mockFetch = vi.fn();
globalThis.fetch = mockFetch;

describe('layoutStore', () => {
  beforeEach(() => {
    mockFetch.mockReset();
    vi.restoreAllMocks();
    // Re-assign fetch mock after restoreAllMocks
    globalThis.fetch = mockFetch;
    // Reset store to defaults
    useLayoutStore.setState({
      layout: defaultLayout,
      isLoaded: false,
    });
  });

  describe('loadFromMongo', () => {
    it('loads layout from MongoDB successfully', async () => {
      const savedLayout: IJsonModel = {
        global: { tabEnableFloat: true },
        borders: [],
        layout: {
          type: 'row',
          weight: 100,
          children: [],
        },
      };

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({
          layoutJson: JSON.stringify(savedLayout),
        }),
      });

      await useLayoutStore.getState().loadFromMongo();

      const state = useLayoutStore.getState();
      expect(state.isLoaded).toBe(true);
      expect(state.layout).toEqual(savedLayout);
    });

    it('uses default layout when no saved layout exists', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({
          layoutJson: null,
        }),
      });

      await useLayoutStore.getState().loadFromMongo();

      const state = useLayoutStore.getState();
      expect(state.isLoaded).toBe(true);
      expect(state.layout).toEqual(defaultLayout);
    });

    it('handles API errors gracefully', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      await useLayoutStore.getState().loadFromMongo();

      const state = useLayoutStore.getState();
      expect(state.isLoaded).toBe(true);
      // Should keep default layout on error
      expect(state.layout).toEqual(defaultLayout);
    });

    it('handles non-OK response', async () => {
      mockFetch.mockResolvedValueOnce({ ok: false, status: 500 });

      await useLayoutStore.getState().loadFromMongo();

      const state = useLayoutStore.getState();
      expect(state.isLoaded).toBe(true);
      expect(state.layout).toEqual(defaultLayout);
    });
  });

  describe('syncToMongo', () => {
    it('saves layout to MongoDB using dedicated endpoint', async () => {
      const customLayout: IJsonModel = {
        global: { tabEnableFloat: false },
        borders: [],
        layout: {
          type: 'row',
          weight: 100,
          children: [],
        },
      };

      mockFetch.mockResolvedValueOnce({ ok: true });

      await useLayoutStore.getState().syncToMongo(customLayout);

      // Verify the correct endpoint was called with PUT
      expect(mockFetch).toHaveBeenCalledWith(
        '/api/settings/layout',
        expect.objectContaining({
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(JSON.stringify(customLayout)),
        })
      );
    });

    it('handles save errors gracefully without throwing', async () => {
      const customLayout: IJsonModel = {
        global: {},
        borders: [],
        layout: { type: 'row', weight: 100, children: [] },
      };

      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      // Should not throw
      await expect(
        useLayoutStore.getState().syncToMongo(customLayout)
      ).resolves.toBeUndefined();
    });

    it('logs error on non-OK response', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      const customLayout: IJsonModel = {
        global: {},
        borders: [],
        layout: { type: 'row', weight: 100, children: [] },
      };

      mockFetch.mockResolvedValueOnce({ ok: false, status: 500 });

      await useLayoutStore.getState().syncToMongo(customLayout);

      expect(consoleSpy).toHaveBeenCalledWith(
        '[layoutStore] Failed to save layout, status:',
        500
      );
      consoleSpy.mockRestore();
    });
  });

  describe('setLayout', () => {
    it('updates layout and triggers sync to MongoDB', async () => {
      const customLayout: IJsonModel = {
        global: { tabEnableFloat: true },
        borders: [],
        layout: {
          type: 'row',
          weight: 100,
          children: [],
        },
      };

      mockFetch.mockResolvedValueOnce({ ok: true });

      useLayoutStore.getState().setLayout(customLayout);

      const state = useLayoutStore.getState();
      expect(state.layout).toEqual(customLayout);
      expect(state.isLoaded).toBe(true);

      // Wait for async sync to complete
      await new Promise(resolve => setTimeout(resolve, 10));

      expect(mockFetch).toHaveBeenCalledWith(
        '/api/settings/layout',
        expect.objectContaining({
          method: 'PUT',
        })
      );
    });
  });

  describe('resetLayout', () => {
    it('resets to default layout and syncs to MongoDB', async () => {
      const customLayout: IJsonModel = {
        global: { tabEnableFloat: true },
        borders: [],
        layout: {
          type: 'row',
          weight: 100,
          children: [],
        },
      };

      // Set custom layout first
      useLayoutStore.setState({ layout: customLayout, isLoaded: true });
      mockFetch.mockReset();
      mockFetch.mockResolvedValueOnce({ ok: true });

      useLayoutStore.getState().resetLayout();

      const state = useLayoutStore.getState();
      expect(state.layout).toEqual(defaultLayout);
      expect(state.isLoaded).toBe(true);

      // Wait for async sync to complete
      await new Promise(resolve => setTimeout(resolve, 10));

      expect(mockFetch).toHaveBeenCalledWith(
        '/api/settings/layout',
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify(JSON.stringify(defaultLayout)),
        })
      );
    });
  });
});
