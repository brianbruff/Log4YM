import { useEffect, useRef, useCallback, useState } from 'react';
import { Layout, Model, TabNode, TabSetNode, BorderNode, ITabSetRenderValues, Actions, DockLocation } from 'flexlayout-react';
import { X, Radio, Book, Zap, LayoutGrid, Antenna, Plus, Map, Compass, Gauge, User, Calendar, Sun, Clock, Bot, MapPin, Search } from 'lucide-react';
import { StatusBar } from './components/StatusBar';
import { SettingsPanel } from './components/SettingsPanel';
import { ConnectionOverlay } from './components/ConnectionOverlay';
import { SetupWizard } from './components/SetupWizard';
import { useSignalRConnection } from './hooks/useSignalR';
import { LogEntryPlugin, LogHistoryPlugin, ClusterPlugin, MapPlugin, RotatorPlugin, GlobePlugin, AntennaGeniusPlugin, PgxlPlugin, SmartUnlinkPlugin, RigPlugin, QrzProfilePlugin, ContestsPlugin, SolarPanelPlugin, AnalogClockPlugin, HeaderPlugin, DXpeditionsPlugin, ChatAiPlugin, POTAPlugin, PropagationPanelPlugin, CwKeyerPlugin } from './plugins';
import { Globe as Globe3D } from 'lucide-react';
import { useLayoutStore, defaultLayout } from './store/layoutStore';
import { useSettingsStore } from './store/settingsStore';
import { useSetupStore } from './store/setupStore';
import { useAppStore } from './store/appStore';
import { useTheme } from './hooks/useTheme';

import 'flexlayout-react/style/dark.css';

// Plugin registry
type PluginCategory = 'Logging' | 'Maps & Navigation' | 'Radio & Equipment' | 'Information' | 'Display';

const CATEGORY_ORDER: PluginCategory[] = ['Logging', 'Maps & Navigation', 'Radio & Equipment', 'Information', 'Display'];

interface PluginDef {
  name: string;
  icon: React.ReactNode;
  component: React.ComponentType;
  category: PluginCategory;
  tags?: string[];
}

const PLUGINS: Record<string, PluginDef> = {
  'log-entry': {
    name: 'Log Entry',
    icon: <Radio className="w-4 h-4" />,
    component: LogEntryPlugin,
    category: 'Logging',
  },
  'log-history': {
    name: 'Log History',
    icon: <Book className="w-4 h-4" />,
    component: LogHistoryPlugin,
    category: 'Logging',
  },
  'cluster': {
    name: 'DX Cluster',
    icon: <Zap className="w-4 h-4" />,
    component: ClusterPlugin,
    category: 'Information',
    tags: ['spots', 'dx'],
  },
  'map-2d': {
    name: '2D Map',
    icon: <Map className="w-4 h-4" />,
    component: MapPlugin,
    category: 'Maps & Navigation',
    tags: ['map'],
  },
  'rotator': {
    name: 'Rotator',
    icon: <Compass className="w-4 h-4" />,
    component: RotatorPlugin,
    category: 'Radio & Equipment',
    tags: ['antenna', 'bearing'],
  },
  'globe-3d': {
    name: '3D Globe',
    icon: <Globe3D className="w-4 h-4" />,
    component: GlobePlugin,
    category: 'Maps & Navigation',
    tags: ['map', 'earth'],
  },
  'antenna-genius': {
    name: 'Antenna Genius',
    icon: <Antenna className="w-4 h-4" />,
    component: AntennaGeniusPlugin,
    category: 'Radio & Equipment',
    tags: ['antenna', 'switch'],
  },
  'pgxl': {
    name: 'PGXL Amplifier',
    icon: <Gauge className="w-4 h-4" />,
    component: PgxlPlugin,
    category: 'Radio & Equipment',
    tags: ['amplifier', 'amp'],
  },
  'smart-unlink': {
    name: 'SmartUnlink',
    icon: <Radio className="w-4 h-4" />,
    component: SmartUnlinkPlugin,
    category: 'Radio & Equipment',
  },
  'rig': {
    name: 'Rig',
    icon: <Radio className="w-4 h-4" />,
    component: RigPlugin,
    category: 'Radio & Equipment',
    tags: ['transceiver', 'radio'],
  },
  'qrz-profile': {
    name: 'QRZ Profile',
    icon: <User className="w-4 h-4" />,
    component: QrzProfilePlugin,
    category: 'Information',
    tags: ['callsign', 'lookup'],
  },
  'contests': {
    name: 'Contests',
    icon: <Calendar className="w-4 h-4" />,
    component: ContestsPlugin,
    category: 'Information',
  },
  'solar-panel': {
    name: 'Solar Panel',
    icon: <Sun className="w-4 h-4" />,
    component: SolarPanelPlugin,
    category: 'Display',
    tags: ['solar', 'sfi', 'kindex'],
  },
  'analog-clock': {
    name: 'Analog Clock',
    icon: <Clock className="w-4 h-4" />,
    component: AnalogClockPlugin,
    category: 'Display',
    tags: ['time', 'utc'],
  },
  'header-bar': {
    name: 'Header Bar',
    icon: <Clock className="w-4 h-4" />,
    component: HeaderPlugin,
    category: 'Display',
    tags: ['time', 'utc', 'callsign'],
  },
  'dxpeditions': {
    name: 'DXpeditions',
    icon: <Compass className="w-4 h-4" />,
    component: DXpeditionsPlugin,
    category: 'Information',
    tags: ['dx', 'expedition'],
  },
  'chat-ai': {
    name: 'Chat AI',
    icon: <Bot className="w-4 h-4" />,
    component: ChatAiPlugin,
    category: 'Information',
    tags: ['assistant', 'ai'],
  },
  'pota': {
    name: 'POTA',
    icon: <MapPin className="w-4 h-4" />,
    component: POTAPlugin,
    category: 'Maps & Navigation',
    tags: ['parks', 'activations'],
  },
  'propagation': {
    name: 'Propagation',
    icon: <Radio className="w-4 h-4" />,
    component: PropagationPanelPlugin,
    category: 'Information',
    tags: ['bands', 'muf', 'hf'],
  },
  'cw-keyer': {
    name: 'CW Keyer',
    icon: <Zap className="w-4 h-4" />,
    component: CwKeyerPlugin,
    category: 'Radio & Equipment',
    tags: ['morse', 'cw'],
  },
};


export function App() {
  const layoutRef = useRef<Layout>(null);
  const { layout, setLayout, resetLayout: resetLayoutStore, loadFromMongo: loadLayout, syncToMongoSync } = useLayoutStore();
  const { loadSettings, openSettings, settings } = useSettingsStore();
  const { fetchStatus, status: setupStatus, isLoading: setupLoading } = useSetupStore();
  const { setStationInfo, setDatabaseConnected, setDatabaseProvider } = useAppStore();
  const [model, setModel] = useState<Model>(() => Model.fromJson(layout));
  const [showPanelPicker, setShowPanelPicker] = useState(false);
  const [targetTabSetId, setTargetTabSetId] = useState<string | null>(null);
  const [panelFilter, setPanelFilter] = useState('');

  // Apply theme from settings (dark/light/system)
  useTheme();

  // Check setup status on mount (for status display, not blocking)
  useEffect(() => {
    fetchStatus();
  }, [fetchStatus]);

  // Poll database connection status periodically (every 10 seconds)
  useEffect(() => {
    const checkDbHealth = async () => {
      try {
        const response = await fetch('/api/health');
        if (response.ok) {
          const data = await response.json();
          // Support both old (mongoDbConnected) and new (databaseConnected) field names
          setDatabaseConnected(data.databaseConnected ?? data.mongoDbConnected ?? false);
          if (data.databaseProvider) {
            setDatabaseProvider(data.databaseProvider);
          }
        }
      } catch (error) {
        console.error('Failed to check database health:', error);
      }
    };

    // Initial check
    checkDbHealth();

    // Poll every 10 seconds
    const interval = setInterval(checkDbHealth, 10000);

    return () => clearInterval(interval);
  }, [setDatabaseConnected, setDatabaseProvider]);

  // Initialize SignalR connection (only called here, not in plugins)
  useSignalRConnection();

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
  const pendingLayoutRef = useRef<Model | null>(null);

  // Save layout immediately (synchronous, for shutdown/unload)
  const saveLayoutImmediately = useCallback(() => {
    if (pendingLayoutRef.current) {
      const layoutJson = pendingLayoutRef.current.toJson();
      syncToMongoSync(layoutJson);
      pendingLayoutRef.current = null;
      if (saveTimeoutRef.current) {
        clearTimeout(saveTimeoutRef.current);
        saveTimeoutRef.current = null;
      }
    }
  }, [syncToMongoSync]);

  // Save layout changes (debounced)
  const handleModelChange = useCallback((newModel: Model) => {
    setModel(newModel);
    pendingLayoutRef.current = newModel;

    // Debounce the save - wait 1 second after last change
    if (saveTimeoutRef.current) {
      clearTimeout(saveTimeoutRef.current);
    }
    saveTimeoutRef.current = setTimeout(() => {
      setLayout(newModel.toJson());
      pendingLayoutRef.current = null;
    }, 1000);
  }, [setLayout]);

  // Save layout before app closes (browser/Electron window close)
  useEffect(() => {
    const handleBeforeUnload = () => {
      saveLayoutImmediately();
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload);
      // Also save on cleanup
      saveLayoutImmediately();
    };
  }, [saveLayoutImmediately]);

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
    setPanelFilter('');
  }, [model, targetTabSetId]);

  // Custom tab rendering
  const onRenderTab = useCallback((node: TabNode, renderValues: { leading: React.ReactNode; content: React.ReactNode }) => {
    const component = node.getComponent();
    const plugin = PLUGINS[component || ''];

    if (plugin) {
      renderValues.leading = (
        <span className="mr-2 text-accent-secondary">{plugin.icon}</span>
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

  const showSetupWizard = !setupLoading && setupStatus !== null && !setupStatus.isConfigured;

  return (
    <div className="h-screen flex flex-col bg-dark-900 text-gray-100 crt-scanlines relative">
      {/* Setup Wizard - blocks UI when not configured */}
      {showSetupWizard && (
        <SetupWizard onComplete={() => fetchStatus()} />
      )}

      <main className="flex-1 relative overflow-hidden">
        <Layout
          ref={layoutRef}
          model={model}
          factory={factory}
          onModelChange={handleModelChange}
          onRenderTab={onRenderTab}
          onRenderTabSet={onRenderTabSet}
          classNameMapper={(className) => {
            return className;
          }}
        />

        {/* Panel Picker Modal */}
        {showPanelPicker && (
          <div className="absolute inset-0 bg-dark-900/85 backdrop-blur-sm flex items-center justify-center z-50">
            <div className="glass-panel w-[32rem] max-h-[80vh] flex flex-col animate-fade-in">
              <div className="flex items-center justify-between px-4 py-3 border-b border-glass-100">
                <div className="flex items-center gap-2">
                  <LayoutGrid className="w-5 h-5 text-accent-secondary" />
                  <h3 className="font-semibold text-accent-success font-ui text-sm tracking-wide uppercase">Add Panel</h3>
                </div>
                <button
                  onClick={() => {
                    setShowPanelPicker(false);
                    setTargetTabSetId(null);
                    setPanelFilter('');
                  }}
                  className="p-1 hover:bg-dark-600 rounded transition-colors text-dark-300 hover:text-accent-danger"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="px-4 pt-3 pb-2">
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-dark-300" />
                  <input
                    type="text"
                    placeholder="Filter panels..."
                    value={panelFilter}
                    onChange={(e) => setPanelFilter(e.target.value)}
                    autoFocus
                    className="w-full pl-9 pr-3 py-2 bg-dark-700/50 border border-glass-100 rounded text-sm font-ui text-gray-100 placeholder-dark-300 focus:outline-none focus:border-accent-secondary/50"
                  />
                </div>
              </div>

              <div className="flex-1 overflow-y-auto px-4 pb-4">
                {(() => {
                  const existingPlugins = getExistingPlugins();
                  const filterLower = panelFilter.toLowerCase();
                  const availablePlugins = Object.entries(PLUGINS).filter(([id, plugin]) => {
                    if (existingPlugins.has(id)) return false;
                    if (!filterLower) return true;
                    return (
                      plugin.name.toLowerCase().includes(filterLower) ||
                      id.toLowerCase().includes(filterLower) ||
                      plugin.category.toLowerCase().includes(filterLower) ||
                      (plugin.tags?.some(tag => tag.toLowerCase().includes(filterLower)) ?? false)
                    );
                  });

                  if (availablePlugins.length === 0) {
                    return (
                      <div className="text-center py-6 text-dark-300">
                        {panelFilter ? 'No panels match your filter' : 'All panels have been added to the layout'}
                      </div>
                    );
                  }

                  // Group by category
                  const grouped: Partial<Record<PluginCategory, [string, PluginDef][]>> = {};
                  for (const entry of availablePlugins) {
                    const cat = entry[1].category;
                    if (!grouped[cat]) grouped[cat] = [];
                    grouped[cat]!.push(entry);
                  }

                  return CATEGORY_ORDER
                    .filter(cat => grouped[cat])
                    .map(cat => (
                      <div key={cat} className="mt-3 first:mt-0">
                        <h4 className="text-xs font-ui font-semibold text-dark-300 uppercase tracking-wider mb-2">{cat}</h4>
                        <div className="grid grid-cols-3 gap-2">
                          {grouped[cat]!.map(([id, plugin]) => (
                            <button
                              key={id}
                              onClick={() => handleAddPanel(id)}
                              className="glass-button flex flex-col items-center gap-2 p-3 hover:border-accent-secondary/40"
                            >
                              <span className="text-accent-secondary">{plugin.icon}</span>
                              <span className="text-xs font-ui">{plugin.name}</span>
                            </button>
                          ))}
                        </div>
                      </div>
                    ));
                })()}
              </div>

              <div className="px-4 py-3 border-t border-glass-100">
                <button
                  onClick={handleResetLayout}
                  className="text-sm text-dark-300 hover:text-accent-danger transition-colors font-ui"
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
