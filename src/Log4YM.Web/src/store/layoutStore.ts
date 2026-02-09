import { create } from 'zustand';
import type { IJsonModel } from 'flexlayout-react';

// Default layout configuration
export const defaultLayout: IJsonModel = {
  global: {
    tabEnableFloat: false,
    tabSetMinWidth: 100,
    tabSetMinHeight: 40,
    borderMinSize: 100,
  },
  borders: [],
  layout: {
    type: 'row',
    weight: 100,
    children: [
      {
        type: 'row',
        weight: 100,
        children: [
          // Header bar across the top (thin strip)
          {
            type: 'tabset',
            weight: 6,
            children: [
              {
                type: 'tab',
                name: 'Header Bar',
                component: 'header-bar',
                enableClose: false,
              },
            ],
          },
          // Main content area below
          {
            type: 'row',
            weight: 94,
            children: [
              {
                type: 'tabset',
                weight: 30,
                children: [
                  {
                    type: 'tab',
                    name: 'Log Entry',
                    component: 'log-entry',
                  },
                  {
                    type: 'tab',
                    name: '3D Globe',
                    component: 'globe-3d',
                  },
                ],
              },
              {
                type: 'row',
                weight: 70,
                children: [
                  {
                    type: 'tabset',
                    weight: 60,
                    children: [
                      {
                        type: 'tab',
                        name: 'Log History',
                        component: 'log-history',
                      },
                    ],
                  },
                  {
                    type: 'tabset',
                    weight: 40,
                    children: [
                      {
                        type: 'tab',
                        name: 'DX Cluster',
                        component: 'cluster',
                      },
                    ],
                  },
                ],
              },
            ],
          },
        ],
      },
    ],
  },
};

interface LayoutState {
  // Layout data
  layout: IJsonModel;
  isLoaded: boolean;

  // Actions
  setLayout: (layout: IJsonModel) => void;
  resetLayout: () => void;
  syncToMongo: (layout: IJsonModel) => Promise<void>;
  loadFromMongo: () => Promise<void>;
}

// Layout is stored in MongoDB only - no localStorage persistence
// This ensures layout survives app upgrades and reinstalls
export const useLayoutStore = create<LayoutState>()((set, get) => ({
  layout: defaultLayout,
  isLoaded: false,

  setLayout: (layout) => {
    set({ layout, isLoaded: true });
    // Sync to MongoDB in background
    get().syncToMongo(layout);
  },

  resetLayout: () => {
    set({ layout: defaultLayout, isLoaded: true });
    get().syncToMongo(defaultLayout);
  },

  // Sync layout to MongoDB (background, non-blocking)
  syncToMongo: async (layout) => {
    try {
      console.log('[layoutStore] Syncing layout to MongoDB');
      const layoutJson = JSON.stringify(layout);
      const response = await fetch('/api/settings/layout', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(layoutJson),
      });

      if (response.ok) {
        console.log('[layoutStore] Layout saved successfully');
      } else {
        console.error('[layoutStore] Failed to save layout, status:', response.status);
      }
    } catch (e) {
      console.error('[layoutStore] Failed to sync layout to MongoDB:', e);
    }
  },

  // Load layout from MongoDB on app startup
  loadFromMongo: async () => {
    try {
      console.log('[layoutStore] Loading layout from MongoDB');
      const response = await fetch('/api/settings');
      if (response.ok) {
        const settings = await response.json();
        if (settings.layoutJson) {
          const layout = JSON.parse(settings.layoutJson);
          console.log('[layoutStore] Layout loaded successfully');
          set({ layout, isLoaded: true });
        } else {
          console.log('[layoutStore] No saved layout found, using default');
          set({ isLoaded: true });
        }
      } else {
        console.error('[layoutStore] Failed to load layout, status:', response.status);
        set({ isLoaded: true });
      }
    } catch (e) {
      console.error('[layoutStore] Failed to load layout from MongoDB:', e);
      set({ isLoaded: true });
    }
  },
}));
