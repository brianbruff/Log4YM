import { useEffect, useRef, useCallback, useState } from 'react';
import { Layout, Model, TabNode, TabSetNode, BorderNode, ITabSetRenderValues, Actions, DockLocation } from 'flexlayout-react';
import { X, Radio, Book, Zap, LayoutGrid, Antenna, Plus, Map, Compass, Gauge, User } from 'lucide-react';
import { Header } from './components/Header';
import { StatusBar } from './components/StatusBar';
import { SettingsPanel } from './components/SettingsPanel';
import { ConnectionOverlay } from './components/ConnectionOverlay';
import { useSignalR } from './hooks/useSignalR';
import { LogEntryPlugin, LogHistoryPlugin, ClusterPlugin, MapPlugin, RotatorPlugin, GlobePlugin, AntennaGeniusPlugin, PgxlPlugin, SmartUnlinkPlugin, RigPlugin, QrzProfilePlugin } from './plugins';
import { Globe as Globe3D } from 'lucide-react';
import { useLayoutStore, defaultLayout } from './store/layoutStore';
import { useSettingsStore } from './store/settingsStore';
import { useSetupStore } from './store/setupStore';
import { useAppStore } from './store/appStore';

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
  'map-2d': {
    name: '2D Map',
    icon: <Map className="w-4 h-4" />,
    component: MapPlugin,
  },
  'rotator': {
    name: 'Rotator',
    icon: <Compass className="w-4 h-4" />,
    component: RotatorPlugin,
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
  'pgxl': {
    name: 'PGXL Amplifier',
    icon: <Gauge className="w-4 h-4" />,
    component: PgxlPlugin,
  },
  'smart-unlink': {
    name: 'SmartUnlink',
    icon: <Radio className="w-4 h-4" />,
    component: SmartUnlinkPlugin,
  },
  'rig': {
    name: 'Rig',
    icon: <Radio className="w-4 h-4" />,
    component: RigPlugin,
  },
  'qrz-profile': {
    name: 'QRZ Profile',
    icon: <User className="w-4 h-4" />,
    component: QrzProfilePlugin,
  },
};


export function App() {
  const layoutRef = useRef<Layout>(null);
  const { layout, setLayout, resetLayout: resetLayoutStore, loadFromMongo: loadLayout } = useLayoutStore();
  const { loadSettings, openSettings, settings } = useSettingsStore();
  const { fetchStatus } = useSetupStore();
  const { setStationInfo } = useAppStore();
  const [model, setModel] = useState<Model>(() => Model.fromJson(layout));
  const [showPanelPicker, setShowPanelPicker] = useState(false);
  const [targetTabSetId, setTargetTabSetId] = useState<string | null>(null);

  // Check setup status on mount (for status display, not blocking)
  useEffect(() => {
    fetchStatus();
  }, [fetchStatus]);

  // Initialize SignalR connection
  useSignalR();

  // Load settings and layout from MongoDB on mount (will gracefully fail if not connected)
  useEffect(() => {
    loadSettings();
    loadLayout();
  }, [loadSettings, loadLayout]);

  // Sync station info to app store whenever settings change
  // This ensures map/globe components have access to station coordinates
  // even when the settings panel is not open
  useEffect(() => {
    if (settings.station.callsign || settings.station.gridSquare) {
      setStationInfo(settings.station.callsign, settings.station.gridSquare);
    }
  }, [settings.station.callsign, settings.station.gridSquare, setStationInfo]);

  // Listen for Electron menu commands (Settings via Cmd+,)
  useEffect(() => {
    // Check if running in Electron with IPC available
    if (window.electronAPI) {
      window.electronAPI.onOpenSettings(() => {
        openSettings();
      });

      return () => {
        window.electronAPI?.removeOpenSettingsListener();
      };
    }
  }, [openSettings]);

  // Update model when layout store changes (e.g., from MongoDB load)
  useEffect(() => {
    setModel(Model.fromJson(layout));
  }, [layout]);

  // Get list of existing plugin components in the layout
  const getExistingPlugins = useCallback((): Set<string> => {
    const existing = new Set<string>();
    model.visitNodes((node) => {
      if (node.getType() === 'tab') {
        const tabNode = node as TabNode;
        const component = tabNode.getComponent();
        if (component) {
          existing.add(component);
        }
      }
    });
    return existing;
  }, [model]);

  // Debounced save to MongoDB
  const saveTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Save layout changes (debounced)
  const handleModelChange = useCallback((newModel: Model) => {
    setModel(newModel);

    // Debounce the save - wait 1 second after last change
    if (saveTimeoutRef.current) {
      clearTimeout(saveTimeoutRef.current);
    }
    saveTimeoutRef.current = setTimeout(() => {
      setLayout(newModel.toJson());
    }, 1000);
  }, [setLayout]);

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

  // Add a new panel to a specific tabset
  const handleAddPanel = useCallback((pluginId: string) => {
    const plugin = PLUGINS[pluginId];
    if (!plugin || !targetTabSetId) return;

    model.doAction(
      Actions.addNode(
        {
          type: 'tab',
          name: plugin.name,
          component: pluginId,
        },
        targetTabSetId,
        DockLocation.CENTER,
        -1,
        true
      )
    );

    setShowPanelPicker(false);
    setTargetTabSetId(null);
  }, [model, targetTabSetId]);

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

  // Custom tabset rendering - add + button to each tabset
  const onRenderTabSet = useCallback((node: TabSetNode | BorderNode, renderValues: ITabSetRenderValues) => {
    if (node instanceof TabSetNode) {
      renderValues.stickyButtons.push(
        <button
          key="add-panel"
          title="Add panel to this tabset"
          className="flexlayout__tab_toolbar_button"
          onClick={() => {
            setTargetTabSetId(node.getId());
            setShowPanelPicker(true);
          }}
        >
          <Plus className="w-3.5 h-3.5" />
        </button>
      );
    }
  }, []);

  // Reset layout to default
  const handleResetLayout = useCallback(() => {
    setModel(Model.fromJson(defaultLayout));
    resetLayoutStore();
  }, [resetLayoutStore]);

  return (
    <div className="h-screen flex flex-col bg-dark-900 text-gray-100">
      <Header />

      <main className="flex-1 relative overflow-hidden">
        <Layout
          ref={layoutRef}
          model={model}
          factory={factory}
          onModelChange={handleModelChange}
          onRenderTab={onRenderTab}
          onRenderTabSet={onRenderTabSet}
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
                  onClick={() => {
                    setShowPanelPicker(false);
                    setTargetTabSetId(null);
                  }}
                  className="p-1 hover:bg-dark-600 rounded transition-colors"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="p-4 grid grid-cols-2 gap-3">
                {(() => {
                  const existingPlugins = getExistingPlugins();
                  const availablePlugins = Object.entries(PLUGINS).filter(([id]) => !existingPlugins.has(id));

                  if (availablePlugins.length === 0) {
                    return (
                      <div className="col-span-2 text-center py-4 text-gray-500">
                        All panels have been added to the layout
                      </div>
                    );
                  }

                  return availablePlugins.map(([id, plugin]) => (
                    <button
                      key={id}
                      onClick={() => handleAddPanel(id)}
                      className="glass-button flex flex-col items-center gap-2 p-4 hover:border-accent-primary/50"
                    >
                      <span className="text-accent-primary">{plugin.icon}</span>
                      <span className="text-sm">{plugin.name}</span>
                    </button>
                  ));
                })()}
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

      {/* Settings Panel (Modal) */}
      <SettingsPanel />

      {/* Connection Overlay - blocks UI when disconnected */}
      <ConnectionOverlay />
    </div>
  );
}

export default App;
