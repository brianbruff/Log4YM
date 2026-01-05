import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { IJsonModel } from 'flexlayout-react';

// Default layout configuration
export const defaultLayout: IJsonModel = {
  global: {
    tabEnableFloat: false,
    tabSetMinWidth: 100,
    tabSetMinHeight: 100,
    borderMinSize: 100,
  },
  borders: [],
  layout: {
    type: 'row',
    weight: 100,
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

export const useLayoutStore = create<LayoutState>()(
  persist(
    (set, get) => ({
      layout: defaultLayout,
      isLoaded: false,

      setLayout: (layout) => {
        set({ layout, isLoaded: true });
        // Also sync to MongoDB in background
        get().syncToMongo(layout);
      },

      resetLayout: () => {
        set({ layout: defaultLayout, isLoaded: true });
        get().syncToMongo(defaultLayout);
      },

      // Sync layout to MongoDB (background, non-blocking)
      syncToMongo: async (layout) => {
        try {
          const response = await fetch('/api/settings');
          if (response.ok) {
            const settings = await response.json();
            settings.layoutJson = JSON.stringify(layout);
            await fetch('/api/settings', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(settings),
            });
          }
        } catch (e) {
          console.error('Failed to sync layout to MongoDB:', e);
        }
      },

      // Load layout from MongoDB (used on first load when localStorage is empty)
      loadFromMongo: async () => {
        try {
          const response = await fetch('/api/settings');
          if (response.ok) {
            const settings = await response.json();
            if (settings.layoutJson) {
              const layout = JSON.parse(settings.layoutJson);
              set({ layout, isLoaded: true });
            } else {
              set({ isLoaded: true });
            }
          } else {
            set({ isLoaded: true });
          }
        } catch (e) {
          console.error('Failed to load layout from MongoDB:', e);
          set({ isLoaded: true });
        }
      },
    }),
    {
      name: 'log4ym-layout',
      partialize: (state) => ({ layout: state.layout }),
    }
  )
);
