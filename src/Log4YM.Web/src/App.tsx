import { useEffect, useRef, useCallback, useState } from 'react';
import { Layout, Model, TabNode, IJsonModel } from 'flexlayout-react';
import { X, Radio, Book, Zap, Globe, LayoutGrid, Antenna } from 'lucide-react';
import { Header } from './components/Header';
import { StatusBar } from './components/StatusBar';
import { useSignalR } from './hooks/useSignalR';
import { LogEntryPlugin, LogHistoryPlugin, ClusterPlugin, MapGlobePlugin, GlobePlugin, AntennaGeniusPlugin } from './plugins';
import { Globe as Globe3D } from 'lucide-react';

import 'flexlayout-react/style/dark.css';

// Plugin registry
const PLUGINS: Record<string, { name: string; icon: React.ReactNode; component: React.ComponentType }> = {
  'log-entry': {
    name: 'Log Entry',
    icon: <Radio className="w-4 h-4" />,
    component: LogEntryPlugin,
  },
  'log-history': {
    name: 'Log History',
    icon: <Book className="w-4 h-4" />,
    component: LogHistoryPlugin,
  },
  'cluster': {
    name: 'DX Cluster',
    icon: <Zap className="w-4 h-4" />,
    component: ClusterPlugin,
  },
  'map-globe': {
    name: 'Map & Rotator',
    icon: <Globe className="w-4 h-4" />,
    component: MapGlobePlugin,
  },
  'globe-3d': {
    name: '3D Globe',
    icon: <Globe3D className="w-4 h-4" />,
    component: GlobePlugin,
  },
  'antenna-genius': {
    name: 'Antenna Genius',
    icon: <Antenna className="w-4 h-4" />,
    component: AntennaGeniusPlugin,
  },
};

// Default layout configuration
const defaultLayout: IJsonModel = {
  global: {
    tabEnableFloat: true,
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

export function App() {
  const layoutRef = useRef<Layout>(null);
  const [model, setModel] = useState<Model>(() => Model.fromJson(defaultLayout));
  const [showPanelPicker, setShowPanelPicker] = useState(false);

  // Initialize SignalR connection
  useSignalR();

  // Load saved layout from localStorage
  useEffect(() => {
    const savedLayout = localStorage.getItem('log4ym-layout');
    if (savedLayout) {
      try {
        const parsed = JSON.parse(savedLayout);
        setModel(Model.fromJson(parsed));
      } catch (e) {
        console.error('Failed to load saved layout:', e);
      }
    }
  }, []);

  // Save layout changes
  const handleModelChange = useCallback((newModel: Model) => {
    setModel(newModel);
    localStorage.setItem('log4ym-layout', JSON.stringify(newModel.toJson()));
  }, []);

  // Factory function to render components
  const factory = useCallback((node: TabNode) => {
    const component = node.getComponent();
    const plugin = PLUGINS[component || ''];

    if (plugin) {
      const Component = plugin.component;
      return <Component />;
    }

    return (
      <div className="flex items-center justify-center h-full text-gray-500">
        Unknown component: {component}
      </div>
    );
  }, []);

  // Add a new panel
  const handleAddPanel = useCallback((pluginId: string) => {
    const plugin = PLUGINS[pluginId];
    if (!plugin) return;

    layoutRef.current?.addTabToActiveTabSet({
      type: 'tab',
      name: plugin.name,
      component: pluginId,
    });

    setShowPanelPicker(false);
  }, []);

  // Custom tab rendering
  const onRenderTab = useCallback((node: TabNode, renderValues: { leading: React.ReactNode; content: React.ReactNode }) => {
    const component = node.getComponent();
    const plugin = PLUGINS[component || ''];

    if (plugin) {
      renderValues.leading = (
        <span className="mr-2 text-accent-primary">{plugin.icon}</span>
      );
    }
  }, []);

  // Reset layout to default
  const handleResetLayout = useCallback(() => {
    setModel(Model.fromJson(defaultLayout));
    localStorage.removeItem('log4ym-layout');
  }, []);

  return (
    <div className="h-screen flex flex-col bg-dark-900 text-gray-100">
      <Header onAddPanel={() => setShowPanelPicker(true)} />

      <main className="flex-1 relative overflow-hidden">
        <Layout
          ref={layoutRef}
          model={model}
          factory={factory}
          onModelChange={handleModelChange}
          onRenderTab={onRenderTab}
          classNameMapper={(className) => {
            // Apply custom dark theme classes
            return className;
          }}
        />

        {/* Panel Picker Modal */}
        {showPanelPicker && (
          <div className="absolute inset-0 bg-dark-900/80 backdrop-blur-sm flex items-center justify-center z-50">
            <div className="glass-panel w-96 animate-fade-in">
              <div className="flex items-center justify-between px-4 py-3 border-b border-glass-100">
                <div className="flex items-center gap-2">
                  <LayoutGrid className="w-5 h-5 text-accent-primary" />
                  <h3 className="font-semibold">Add Panel</h3>
                </div>
                <button
                  onClick={() => setShowPanelPicker(false)}
                  className="p-1 hover:bg-dark-600 rounded transition-colors"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="p-4 grid grid-cols-2 gap-3">
                {Object.entries(PLUGINS).map(([id, plugin]) => (
                  <button
                    key={id}
                    onClick={() => handleAddPanel(id)}
                    className="glass-button flex flex-col items-center gap-2 p-4 hover:border-accent-primary/50"
                  >
                    <span className="text-accent-primary">{plugin.icon}</span>
                    <span className="text-sm">{plugin.name}</span>
                  </button>
                ))}
              </div>

              <div className="px-4 py-3 border-t border-glass-100">
                <button
                  onClick={handleResetLayout}
                  className="text-sm text-gray-500 hover:text-gray-300 transition-colors"
                >
                  Reset to default layout
                </button>
              </div>
            </div>
          </div>
        )}
      </main>

      <StatusBar />
    </div>
  );
}

export default App;
