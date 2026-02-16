import { useState, useEffect, useMemo } from "react";
import { Radio, Wifi, WifiOff, Power, PowerOff, Plus, Settings, ChevronDown, ChevronUp, RefreshCw } from "lucide-react";
import { useAppStore } from "../store/appStore";
import { useSettingsStore } from "../store/settingsStore";
import { useSignalR } from "../hooks/useSignalR";
import { GlassPanel } from "../components/GlassPanel";
import { signalRService } from "../api/signalr";
import type {
  RadioConnectionState,
  HamlibRigModelInfo,
  HamlibRigCapabilities,
  HamlibRigConfigDto,
  HamlibDataBits,
  HamlibStopBits,
  HamlibFlowControl,
  HamlibParity,
  HamlibPttType,
} from "../api/signalr";
import { HAMLIB_BAUD_RATES } from "../api/signalr";

// Default Hamlib config
const defaultHamlibConfig: HamlibRigConfigDto = {
  modelId: 0,
  modelName: "",
  connectionType: "Serial",
  baudRate: 9600,
  dataBits: 8,
  stopBits: 1,
  flowControl: "None",
  parity: "None",
  hostname: "localhost",
  networkPort: 4532,
  pttType: "Rig",
  getFrequency: true,
  getMode: true,
  getVfo: true,
  getPtt: true,
  getPower: false,
  getRit: false,
  getXit: false,
  getKeySpeed: false,
  pollIntervalMs: 250,
};

export function RigPlugin() {
  const {
    discoveredRadios,
    radioConnectionStates,
    radioStates,
    selectedRadioId,
    setSelectedRadio,
    removeDiscoveredRadio,
  } = useAppStore();

  const {
    connectRadio,
    disconnectRadio,
    getHamlibRigList,
    getHamlibRigCaps,
    getHamlibSerialPorts,
    getHamlibConfig,
    saveHamlibConfig,
    disconnectHamlibRig,
    deleteHamlibConfig,
    disconnectTci,
    deleteTciConfig,
  } = useSignalR();

  // Radio settings from store (persisted to database)
  const { settings, updateRadioSettings, updateTciSettings, saveSettings } = useSettingsStore();
  const tciSettings = settings.radio.tci;
  const { autoReconnect, autoConnectRigId } = settings.radio;

  // TCI form state
  const [showTciForm, setShowTciForm] = useState(false);
  const [isConnectingTci, setIsConnectingTci] = useState(false);

  // Hamlib form state
  const [showHamlibForm, setShowHamlibForm] = useState(false);
  const [isConnectingHamlib, setIsConnectingHamlib] = useState(false);
  const [hamlibRigs, setHamlibRigs] = useState<HamlibRigModelInfo[]>([]);
  const [hamlibCaps, setHamlibCaps] = useState<HamlibRigCapabilities | null>(null);
  const [serialPorts, setSerialPorts] = useState<string[]>([]);
  const [hamlibConfig, setHamlibConfig] = useState<HamlibRigConfigDto>(defaultHamlibConfig);
  const [rigSearch, setRigSearch] = useState("");
  const [showRigDropdown, setShowRigDropdown] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  // Set up Hamlib event handlers
  useEffect(() => {
    signalRService.setHandlers({
      onHamlibRigList: (evt) => {
        console.log('Hamlib rig list received:', evt.rigs.length, 'rigs');
        setHamlibRigs(evt.rigs);
      },
      onHamlibRigCaps: (evt) => {
        console.log('Hamlib rig caps received:', evt.modelId);
        setHamlibCaps(evt.capabilities);
      },
      onHamlibSerialPorts: (evt) => {
        console.log('Serial ports received:', evt.ports);
        setSerialPorts(evt.ports);
      },
      onHamlibConfigLoaded: (evt) => {
        console.log('Hamlib config loaded:', evt.config?.modelName);
        if (evt.config) {
          setHamlibConfig(evt.config);
          // Also load caps for this model
          getHamlibRigCaps(evt.config.modelId);
        }
      },
    });
  }, [getHamlibRigCaps]);

  // Load Hamlib data when form opens
  useEffect(() => {
    if (showHamlibForm && hamlibRigs.length === 0) {
      getHamlibRigList();
      getHamlibSerialPorts();
      getHamlibConfig();
    }
  }, [showHamlibForm, hamlibRigs.length, getHamlibRigList, getHamlibSerialPorts, getHamlibConfig]);

  // Load capabilities when rig model changes
  useEffect(() => {
    if (hamlibConfig.modelId > 0) {
      getHamlibRigCaps(hamlibConfig.modelId);
    }
  }, [hamlibConfig.modelId, getHamlibRigCaps]);

  // Auto-select connection type based on capabilities only when caps are first loaded
  // This prevents overriding user's manual connection type selection
  useEffect(() => {
    if (hamlibCaps && hamlibConfig.modelId > 0) {
      // Only auto-adjust if current selection is not supported
      if (hamlibConfig.connectionType === "Serial" && !hamlibCaps.supportsSerial && hamlibCaps.supportsNetwork) {
        updateHamlibConfig({ connectionType: "Network", hostname: hamlibConfig.hostname || "localhost" });
      } else if (hamlibConfig.connectionType === "Network" && !hamlibCaps.supportsNetwork && hamlibCaps.supportsSerial) {
        updateHamlibConfig({ connectionType: "Serial" });
      } else if (hamlibConfig.connectionType === "Network" && !hamlibConfig.hostname) {
        // Default hostname to localhost for network connections
        updateHamlibConfig({ hostname: "localhost" });
      }
    }
    // Only run when caps change (i.e., when a new model is selected), not on every connection type change
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hamlibCaps]);

  // Filter rigs by search term
  const filteredRigs = useMemo(() => {
    if (!rigSearch) return hamlibRigs.slice(0, 50); // Show first 50 by default
    const search = rigSearch.toLowerCase();
    return hamlibRigs.filter(rig =>
      rig.displayName.toLowerCase().includes(search) ||
      rig.manufacturer.toLowerCase().includes(search) ||
      rig.model.toLowerCase().includes(search)
    ).slice(0, 50);
  }, [hamlibRigs, rigSearch]);

  // Convert Map to array for rendering
  const radios = Array.from(discoveredRadios.values());
  const selectedRadio = selectedRadioId
    ? discoveredRadios.get(selectedRadioId)
    : null;
  const selectedConnectionState = selectedRadioId
    ? radioConnectionStates.get(selectedRadioId)
    : null;
  const selectedRadioState = selectedRadioId
    ? radioStates.get(selectedRadioId)
    : null;

  // Reset local connecting state when we receive connection state or radio data from SignalR
  useEffect(() => {
    if (selectedRadioState) {
      setIsConnectingTci(false);
      setIsConnectingHamlib(false);
    } else if (selectedConnectionState && selectedConnectionState !== "Connecting") {
      setIsConnectingTci(false);
      setIsConnectingHamlib(false);
    }
  }, [selectedConnectionState, selectedRadioState]);

  const handleConnect = async (radioId: string) => {
    setSelectedRadio(radioId);
    await connectRadio(radioId);
  };

  // Auto-connect to saved rig if autoReconnect is enabled and we have a discovered radio.
  // IMPORTANT: We must wait for connection state to arrive before deciding whether to connect.
  // The OnRadioDiscovered event arrives before OnRadioConnectionStateChanged from RequestRadioStatus,
  // so if we connect when connState is undefined, we'd tear down an already-working backend connection.
  useEffect(() => {
    if (!autoReconnect || radios.length === 0 || selectedRadioId || isConnectingHamlib || isConnectingTci) return;

    // Find the specific rig targeted for auto-connect, or fall back to first radio
    const targetRadio = autoConnectRigId
      ? radios.find(r => r.id === autoConnectRigId)
      : radios[0];

    if (!targetRadio) return;

    const connState = radioConnectionStates.get(targetRadio.id);

    if (connState === "Connected" || connState === "Monitoring") {
      // Backend already has this rig connected — just select it, no reconnect needed
      console.log("Auto-selecting already-connected rig:", targetRadio.id);
      setSelectedRadio(targetRadio.id);
    } else if (connState === "Disconnected" || connState === "Error") {
      // Rig is explicitly not connected — initiate connection
      console.log("Auto-connecting to saved rig:", targetRadio.id);
      handleConnect(targetRadio.id);
    }
    // If connState is undefined, the connection state event hasn't arrived yet — wait for it.
    // The useEffect will re-fire when radioConnectionStates updates.
  }, [autoReconnect, autoConnectRigId, radios.length, selectedRadioId, isConnectingHamlib, isConnectingTci, radioConnectionStates]);

  const handleDisconnect = async () => {
    if (selectedRadioId) {
      const radio = discoveredRadios.get(selectedRadioId);
      if (radio?.type === "Hamlib" || selectedRadioId.startsWith("hamlib-")) {
        await disconnectHamlibRig();
      } else if (radio?.type === "Tci" || selectedRadioId.startsWith("tci-")) {
        await disconnectTci(selectedRadioId);
      } else {
        await disconnectRadio(selectedRadioId);
      }
      // Disable auto-reconnect when manually disconnecting
      updateRadioSettings({ autoReconnect: false });
      saveSettings();
      setSelectedRadio(null);
    }
  };

  const handleToggleAutoReconnect = async () => {
    if (!autoReconnect && selectedRadioId) {
      // Enabling: target the currently selected rig
      handleToggleAutoReconnectForRig(selectedRadioId);
      return;
    }
    // Disabling: clear targeting
    updateRadioSettings({
      autoReconnect: false,
      autoConnectRigId: null,
      activeRigType: null,
    });
    await saveSettings();
  };

  const handleToggleAutoReconnectForRig = async (radioId: string) => {
    if (autoConnectRigId === radioId && autoReconnect) {
      // Already targeting this rig — disable
      updateRadioSettings({
        autoReconnect: false,
        autoConnectRigId: null,
        activeRigType: null,
      });
    } else {
      // Enable and target this specific rig
      const radio = discoveredRadios.get(radioId);
      const rigType = radio?.type === "Hamlib" || radioId.startsWith("hamlib-")
        ? "hamlib" as const
        : radio?.type === "Tci" || radioId.startsWith("tci-")
          ? "tci" as const
          : null;
      updateRadioSettings({
        autoReconnect: true,
        autoConnectRigId: radioId,
        activeRigType: rigType,
      });
    }
    await saveSettings();
  };

  const handleRemoveRig = async (radioId: string) => {
    try {
      const radio = discoveredRadios.get(radioId);
      const isHamlib = radio?.type === "Hamlib" || radioId.startsWith("hamlib-");
      const isTci = radio?.type === "Tci" || radioId.startsWith("tci-");

      // Disconnect if currently connected
      const connState = radioConnectionStates.get(radioId);
      if (connState && connState !== "Disconnected") {
        if (isHamlib) {
          await disconnectHamlibRig();
        } else if (isTci) {
          await disconnectTci(radioId);
        } else {
          await disconnectRadio(radioId);
        }
      }

      // Delete saved configuration based on radio type
      if (isHamlib) {
        await deleteHamlibConfig();
        setHamlibConfig(defaultHamlibConfig);
        setRigSearch("");
      } else if (isTci) {
        await deleteTciConfig();
        updateTciSettings({ host: "localhost", port: 50001, name: "" });
      }

      // Remove from UI immediately
      removeDiscoveredRadio(radioId);
      setSelectedRadio(null);

      // Clear rig settings
      updateRadioSettings({ activeRigType: null, autoReconnect: false, autoConnectRigId: null });
      await saveSettings();
    } catch (error) {
      console.error("Failed to remove rig:", error);
    }
  };

  const handleAddHamlib = async () => {
    if (!hamlibConfig.modelId) return;

    // Validate based on connection type
    if (hamlibConfig.connectionType === "Serial" && !hamlibConfig.serialPort) {
      return;
    }
    if (hamlibConfig.connectionType === "Network" && !hamlibConfig.hostname) {
      return;
    }

    setShowHamlibForm(false);

    try {
      await saveHamlibConfig(hamlibConfig);
    } catch (error) {
      console.error('Failed to save Hamlib config:', error);
    }
  };

  const handleAddTci = async () => {
    const port = tciSettings.port;
    const host = tciSettings.host;
    if (!host || !port) return;

    setShowTciForm(false);

    try {
      await saveSettings();
      await signalRService.requestRadioStatus();
    } catch (error) {
      console.error('Failed to save TCI config:', error);
    }
  };

  const updateHamlibConfig = (updates: Partial<HamlibRigConfigDto>) => {
    setHamlibConfig(prev => ({ ...prev, ...updates }));
  };

  const handleRigSelect = (rig: HamlibRigModelInfo) => {
    updateHamlibConfig({
      modelId: rig.modelId,
      modelName: rig.displayName,
    });
    setRigSearch(rig.displayName);
    setShowRigDropdown(false);
  };

  const formatFrequency = (hz: number): string => {
    const mhz = hz / 1_000_000;
    return mhz.toFixed(6);
  };

  const getConnectionStateColor = (state?: RadioConnectionState): string => {
    switch (state) {
      case "Connected":
      case "Monitoring":
        return "text-accent-success";
      case "Connecting":
      case "Discovering":
        return "text-accent-primary";
      case "Error":
        return "text-accent-danger";
      default:
        return "text-dark-300";
    }
  };

  const getConnectionStateText = (state?: RadioConnectionState): string => {
    return state || "Disconnected";
  };

  const isConnecting = selectedConnectionState === "Connecting";
  const isConnected = (selectedConnectionState && ["Connected", "Monitoring"].includes(selectedConnectionState))
    || !!selectedRadioState;
  const isLocallyConnecting = isConnectingTci || isConnectingHamlib;
  const effectiveConnectionState: RadioConnectionState | undefined =
    selectedConnectionState ?? (selectedRadioState ? "Connected" : undefined);

  const getRadioInfoFromId = (radioId: string) => {
    if (radioId.startsWith("tci-")) {
      const hostPort = radioId.substring(4);
      return { model: "TCI Radio", ipAddress: hostPort, type: "Tci" as const };
    }
    if (radioId.startsWith("hamlib-")) {
      const modelId = radioId.substring(7);
      const rig = hamlibRigs.find(r => r.modelId.toString() === modelId);
      return { model: rig?.displayName || "Hamlib Radio", ipAddress: hamlibConfig.connectionType === "Network" ? `${hamlibConfig.hostname}:${hamlibConfig.networkPort}` : hamlibConfig.serialPort || "", type: "Hamlib" as const };
    }
    return null;
  };

  const radioInfo = selectedRadio || (selectedRadioId ? getRadioInfoFromId(selectedRadioId) : null);

  // Show connected view
  if (selectedRadioId && radioInfo && (isConnecting || isConnected || isLocallyConnecting)) {
    return (
      <GlassPanel
        title="Rig"
        icon={<Radio className="w-5 h-5" />}
        actions={
          <div className="flex items-center gap-2">
            <button
              onClick={handleToggleAutoReconnect}
              title={autoReconnect ? "Auto-reconnect enabled" : "Auto-reconnect disabled"}
              className={`p-1.5 rounded transition-all ${
                autoReconnect
                  ? "bg-accent-primary/20 text-accent-primary"
                  : "bg-dark-700 text-dark-300 hover:text-dark-200"
              }`}
            >
              <RefreshCw className={`w-3.5 h-3.5 ${autoReconnect ? "" : "opacity-50"}`} />
            </button>
            <span
              className={`flex items-center gap-1.5 text-xs font-mono ${getConnectionStateColor(
                effectiveConnectionState
              )}`}
            >
              <Wifi className="w-3.5 h-3.5" />
              {getConnectionStateText(effectiveConnectionState)}
            </span>
          </div>
        }
      >
        <div className="p-4 space-y-4">
          {/* Radio Info */}
          <div className="flex items-center justify-between">
            <div className="text-sm text-dark-300 font-ui">
              <span className="font-medium text-dark-200">
                {radioInfo.model}
              </span>
              <span className="mx-2">|</span>
              <span className="font-mono text-xs">
                {radioInfo.ipAddress}
              </span>
            </div>
            {(isConnecting || isLocallyConnecting) && !isConnected ? (
              <div className="px-3 py-1.5 text-xs font-medium font-ui flex items-center gap-1.5 bg-accent-primary/20 text-accent-primary rounded-lg">
                <Wifi className="w-3.5 h-3.5 animate-pulse" />
                Connecting...
              </div>
            ) : (
              <button
                onClick={handleDisconnect}
                className="px-3 py-1.5 text-xs font-medium font-ui flex items-center gap-1.5 bg-accent-danger/20 text-accent-danger rounded-lg hover:bg-accent-danger/30 transition-all"
              >
                <PowerOff className="w-3.5 h-3.5" />
                Disconnect
              </button>
            )}
          </div>

          {/* Frequency Display */}
          <div className="bg-dark-700/50 rounded-lg p-4 border border-glass-100">
            <div className="text-xs text-dark-300 uppercase tracking-wider mb-1 font-ui">
              Frequency
            </div>
            <div className="text-3xl font-bold text-accent-primary font-display">
              {selectedRadioState
                ? formatFrequency(selectedRadioState.frequencyHz)
                : (isConnecting || isLocallyConnecting) && !isConnected
                ? <span className="text-dark-300 text-lg animate-pulse font-ui">Waiting for data...</span>
                : "---"}
              {selectedRadioState && (
                <span className="text-lg font-normal text-dark-300 ml-2 font-ui">
                  MHz
                </span>
              )}
            </div>
          </div>

          {/* Mode and Band */}
          <div className="grid grid-cols-2 gap-3">
            <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
              <div className="text-xs text-dark-300 uppercase tracking-wider mb-1 font-ui">
                Mode
              </div>
              <div className="text-xl font-bold text-accent-secondary font-mono">
                {selectedRadioState?.mode || "---"}
              </div>
            </div>
            <div className="bg-dark-700/50 rounded-lg p-3 border border-glass-100">
              <div className="text-xs text-dark-300 uppercase tracking-wider mb-1 font-ui">
                Band
              </div>
              <div className="text-xl font-bold text-accent-primary font-mono">
                {selectedRadioState?.band || "---"}
              </div>
            </div>
          </div>

          {/* TX/RX Status */}
          <div className="flex items-center gap-3">
            <TxRxIndicator
              isTransmitting={selectedRadioState?.isTransmitting ?? false}
            />
            {selectedRadioState?.sliceOrInstance && (
              <span className="text-sm text-dark-300 font-ui">
                Slice:{" "}
                <span className="text-dark-200 font-mono">
                  {selectedRadioState.sliceOrInstance}
                </span>
              </span>
            )}
          </div>

        </div>
      </GlassPanel>
    );
  }

  // Discovery / Connection UI
  return (
    <GlassPanel
      title="Rig"
      icon={<Radio className="w-5 h-5" />}
    >
      <div className="p-4 space-y-4">
        {/* Radio Type Selection */}
        <div>
          <div className="text-xs text-dark-300 uppercase tracking-wider mb-2 font-ui">
            Radio Type
          </div>
          <div className="grid grid-cols-2 gap-2">
            <button
              onClick={() => {
                setShowTciForm(!showTciForm);
                setShowHamlibForm(false);
              }}
              className={`px-4 py-3 rounded-lg text-sm font-medium font-ui transition-all border ${
                showTciForm
                  ? "bg-accent-secondary/20 text-accent-secondary border-accent-secondary/30"
                  : "bg-dark-700 text-dark-200 hover:bg-dark-600 border-glass-100"
              }`}
            >
              <div className="flex flex-col items-center gap-1">
                <Radio className="w-5 h-5" />
                <span>TCI</span>
              </div>
            </button>
            <button
              onClick={() => {
                setShowHamlibForm(!showHamlibForm);
                setShowTciForm(false);
              }}
              className={`px-4 py-3 rounded-lg text-sm font-medium font-ui transition-all border ${
                showHamlibForm
                  ? "bg-accent-primary/20 text-accent-primary border-accent-primary/30"
                  : "bg-dark-700 text-dark-200 hover:bg-dark-600 border-glass-100"
              }`}
            >
              <div className="flex flex-col items-center gap-1">
                <Settings className="w-5 h-5" />
                <span>Hamlib</span>
              </div>
            </button>
          </div>
        </div>

        {/* Hamlib Configuration Form */}
        {showHamlibForm && (
          <div className="bg-dark-700/50 rounded-lg p-4 border border-accent-primary/30 space-y-4">
            <div className="text-xs text-accent-primary uppercase tracking-wider font-ui">
              Hamlib Rig Configuration
            </div>

            {/* Rig Model Selector */}
            <div className="relative">
              <label className="block text-xs text-dark-300 mb-1 font-ui">Rig Model</label>
              <input
                type="text"
                value={rigSearch}
                onChange={(e) => {
                  setRigSearch(e.target.value);
                  setShowRigDropdown(true);
                }}
                onFocus={() => setShowRigDropdown(true)}
                placeholder="Search for rig model..."
                className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
              />
              {showRigDropdown && filteredRigs.length > 0 && (
                <div className="absolute z-50 w-full mt-1 max-h-48 overflow-y-auto bg-dark-800 border border-glass-100 rounded-lg shadow-lg">
                  {filteredRigs.map((rig) => (
                    <button
                      key={rig.modelId}
                      onClick={() => handleRigSelect(rig)}
                      className="w-full px-3 py-2 text-left text-sm text-dark-200 hover:bg-dark-700 transition-colors"
                    >
                      <div className="font-medium font-ui">{rig.displayName}</div>
                      <div className="text-xs text-dark-300 font-mono">{rig.manufacturer} - {rig.model}</div>
                    </button>
                  ))}
                </div>
              )}
            </div>

            {/* Connection Type Toggle - only show options that are supported */}
            <div>
              <label className="block text-xs text-dark-300 mb-1 font-ui">Connection Type</label>
              <div className="flex gap-2">
                {/* Only show Serial option if supported */}
                {(!hamlibCaps || hamlibCaps.supportsSerial) && (
                  <button
                    onClick={() => updateHamlibConfig({ connectionType: "Serial" })}
                    className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium font-ui transition-all ${
                      hamlibConfig.connectionType === "Serial"
                        ? "bg-accent-primary/20 text-accent-primary border border-accent-primary/30"
                        : "bg-dark-800 text-dark-300 border border-glass-100 hover:bg-dark-700"
                    }`}
                  >
                    Serial
                  </button>
                )}
                {/* Only show Network option if supported */}
                {(!hamlibCaps || hamlibCaps.supportsNetwork) && (
                  <button
                    onClick={() => updateHamlibConfig({ connectionType: "Network", hostname: hamlibConfig.hostname || "localhost" })}
                    className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium font-ui transition-all ${
                      hamlibConfig.connectionType === "Network"
                        ? "bg-accent-primary/20 text-accent-primary border border-accent-primary/30"
                        : "bg-dark-800 text-dark-300 border border-glass-100 hover:bg-dark-700"
                    }`}
                  >
                    Network
                  </button>
                )}
              </div>
            </div>

            {/* Serial Settings */}
            {hamlibConfig.connectionType === "Serial" && (
              <div className="space-y-3">
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-dark-300 mb-1 font-ui">Serial Port</label>
                    <select
                      value={hamlibConfig.serialPort || ""}
                      onChange={(e) => updateHamlibConfig({ serialPort: e.target.value })}
                      className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                    >
                      <option value="">Select port...</option>
                      {serialPorts.map((port) => (
                        <option key={port} value={port}>{port}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs text-dark-300 mb-1 font-ui">Baud Rate</label>
                    <select
                      value={hamlibConfig.baudRate}
                      onChange={(e) => updateHamlibConfig({ baudRate: parseInt(e.target.value) })}
                      className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                    >
                      {HAMLIB_BAUD_RATES.map((rate) => (
                        <option key={rate} value={rate}>{rate}</option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="grid grid-cols-4 gap-2">
                  <div>
                    <label className="block text-xs text-dark-300 mb-1 font-ui">Data Bits</label>
                    <select
                      value={hamlibConfig.dataBits}
                      onChange={(e) => updateHamlibConfig({ dataBits: parseInt(e.target.value) as HamlibDataBits })}
                      className="w-full px-2 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                    >
                      <option value={5}>5</option>
                      <option value={6}>6</option>
                      <option value={7}>7</option>
                      <option value={8}>8</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs text-dark-300 mb-1 font-ui">Stop Bits</label>
                    <select
                      value={hamlibConfig.stopBits}
                      onChange={(e) => updateHamlibConfig({ stopBits: parseInt(e.target.value) as HamlibStopBits })}
                      className="w-full px-2 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                    >
                      <option value={1}>1</option>
                      <option value={2}>2</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs text-dark-300 mb-1 font-ui">Flow</label>
                    <select
                      value={hamlibConfig.flowControl}
                      onChange={(e) => updateHamlibConfig({ flowControl: e.target.value as HamlibFlowControl })}
                      className="w-full px-2 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                    >
                      <option value="None">None</option>
                      <option value="Hardware">HW</option>
                      <option value="Software">SW</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs text-dark-300 mb-1 font-ui">Parity</label>
                    <select
                      value={hamlibConfig.parity}
                      onChange={(e) => updateHamlibConfig({ parity: e.target.value as HamlibParity })}
                      className="w-full px-2 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                    >
                      <option value="None">None</option>
                      <option value="Even">Even</option>
                      <option value="Odd">Odd</option>
                      <option value="Mark">Mark</option>
                      <option value="Space">Space</option>
                    </select>
                  </div>
                </div>

                {/* PTT Configuration */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-dark-300 mb-1 font-ui">PTT Type</label>
                    <select
                      value={hamlibConfig.pttType}
                      onChange={(e) => updateHamlibConfig({ pttType: e.target.value as HamlibPttType })}
                      className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                    >
                      <option value="None">None</option>
                      <option value="Rig">CAT (RIG)</option>
                      <option value="Dtr">DTR</option>
                      <option value="Rts">RTS</option>
                    </select>
                  </div>
                  {(hamlibConfig.pttType === "Dtr" || hamlibConfig.pttType === "Rts") && (
                    <div>
                      <label className="block text-xs text-dark-300 mb-1 font-ui">PTT Port</label>
                      <select
                        value={hamlibConfig.pttPort || ""}
                        onChange={(e) => updateHamlibConfig({ pttPort: e.target.value })}
                        className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                      >
                        <option value="">Same as data</option>
                        {serialPorts.map((port) => (
                          <option key={port} value={port}>{port}</option>
                        ))}
                      </select>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Network Settings */}
            {hamlibConfig.connectionType === "Network" && (
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs text-dark-300 mb-1 font-ui">Hostname</label>
                  <input
                    type="text"
                    value={hamlibConfig.hostname || ""}
                    onChange={(e) => updateHamlibConfig({ hostname: e.target.value })}
                    placeholder="localhost"
                    className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                  />
                </div>
                <div>
                  <label className="block text-xs text-dark-300 mb-1 font-ui">Port</label>
                  <input
                    type="number"
                    value={hamlibConfig.networkPort}
                    onChange={(e) => updateHamlibConfig({ networkPort: parseInt(e.target.value) || 4532 })}
                    placeholder="4532"
                    className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                  />
                </div>
              </div>
            )}

            {/* Advanced Options Toggle */}
            <button
              onClick={() => setShowAdvanced(!showAdvanced)}
              className="flex items-center gap-1 text-xs text-dark-300 hover:text-dark-200 transition-colors font-ui"
            >
              {showAdvanced ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
              Advanced Options
            </button>

            {/* Advanced Options */}
            {showAdvanced && (
              <div className="space-y-3 pt-2 border-t border-glass-100">
                <div className="text-xs text-dark-300 mb-2 font-ui">Feature Toggles</div>
                <div className="grid grid-cols-2 gap-2">
                  <FeatureToggle
                    label="Get Frequency"
                    checked={hamlibConfig.getFrequency}
                    onChange={(v) => updateHamlibConfig({ getFrequency: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetFreq : false}
                  />
                  <FeatureToggle
                    label="Get Mode"
                    checked={hamlibConfig.getMode}
                    onChange={(v) => updateHamlibConfig({ getMode: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetMode : false}
                  />
                  <FeatureToggle
                    label="Get VFO"
                    checked={hamlibConfig.getVfo}
                    onChange={(v) => updateHamlibConfig({ getVfo: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetVfo : false}
                  />
                  <FeatureToggle
                    label="Get PTT"
                    checked={hamlibConfig.getPtt}
                    onChange={(v) => updateHamlibConfig({ getPtt: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetPtt : false}
                  />
                  <FeatureToggle
                    label="Get Power"
                    checked={hamlibConfig.getPower}
                    onChange={(v) => updateHamlibConfig({ getPower: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetPower : false}
                  />
                  <FeatureToggle
                    label="Get RIT"
                    checked={hamlibConfig.getRit}
                    onChange={(v) => updateHamlibConfig({ getRit: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetRit : false}
                  />
                  <FeatureToggle
                    label="Get XIT"
                    checked={hamlibConfig.getXit}
                    onChange={(v) => updateHamlibConfig({ getXit: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetXit : false}
                  />
                  <FeatureToggle
                    label="Get Key Speed"
                    checked={hamlibConfig.getKeySpeed}
                    onChange={(v) => updateHamlibConfig({ getKeySpeed: v })}
                    disabled={hamlibCaps ? !hamlibCaps.canGetKeySpeed : false}
                  />
                </div>

                <div>
                  <label className="block text-xs text-dark-300 mb-1 font-ui">Poll Interval (ms)</label>
                  <input
                    type="number"
                    value={hamlibConfig.pollIntervalMs}
                    onChange={(e) => updateHamlibConfig({ pollIntervalMs: parseInt(e.target.value) || 250 })}
                    min={50}
                    max={5000}
                    className="w-32 px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-primary/50"
                  />
                </div>
              </div>
            )}

            {/* Action Buttons */}
            <div className="flex gap-2 pt-2">
              <button
                onClick={handleAddHamlib}
                disabled={!hamlibConfig.modelId || (hamlibConfig.connectionType === "Serial" && !hamlibConfig.serialPort) || (hamlibConfig.connectionType === "Network" && !hamlibConfig.hostname)}
                className="flex-1 px-4 py-2 text-sm font-medium font-ui flex items-center justify-center gap-2 bg-accent-primary/20 text-accent-primary rounded-lg hover:bg-accent-primary/30 transition-all disabled:opacity-50"
              >
                <Plus className="w-4 h-4" />
                Add
              </button>
              <button
                onClick={() => setShowHamlibForm(false)}
                className="px-4 py-2 text-sm font-medium font-ui bg-dark-700 text-dark-300 rounded-lg hover:bg-dark-600 transition-all"
              >
                Cancel
              </button>
            </div>
          </div>
        )}

        {/* TCI Connection Form */}
        {showTciForm && (
          <div className="bg-dark-700/50 rounded-lg p-4 border border-accent-secondary/30 space-y-3">
            <div className="text-xs text-accent-secondary uppercase tracking-wider mb-2 font-ui">
              Connect to TCI Server
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-dark-300 mb-1 font-ui">Host</label>
                <input
                  type="text"
                  value={tciSettings.host}
                  onChange={(e) => updateTciSettings({ host: e.target.value })}
                  placeholder="localhost"
                  className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-secondary/50"
                />
              </div>
              <div>
                <label className="block text-xs text-dark-300 mb-1 font-ui">Port</label>
                <input
                  type="number"
                  value={tciSettings.port}
                  onChange={(e) => updateTciSettings({ port: parseInt(e.target.value) || 50001 })}
                  placeholder="50001"
                  className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-secondary/50"
                />
              </div>
            </div>
            <div>
              <label className="block text-xs text-dark-300 mb-1 font-ui">Name (optional)</label>
              <input
                type="text"
                value={tciSettings.name}
                onChange={(e) => updateTciSettings({ name: e.target.value })}
                placeholder="My TCI Radio"
                className="w-full px-3 py-2 bg-dark-800 border border-glass-100 rounded-lg text-sm text-dark-200 font-mono focus:outline-none focus:border-accent-secondary/50"
              />
            </div>
            <div className="flex gap-2 pt-2">
              <button
                onClick={handleAddTci}
                disabled={!tciSettings.host}
                className="flex-1 px-4 py-2 text-sm font-medium font-ui flex items-center justify-center gap-2 bg-accent-secondary/20 text-accent-secondary rounded-lg hover:bg-accent-secondary/30 transition-all disabled:opacity-50"
              >
                <Plus className="w-4 h-4" />
                Add
              </button>
              <button
                onClick={() => setShowTciForm(false)}
                className="px-4 py-2 text-sm font-medium font-ui bg-dark-700 text-dark-300 rounded-lg hover:bg-dark-600 transition-all"
              >
                Cancel
              </button>
            </div>
          </div>
        )}

        {/* Saved Rig - only show if we have a configured rig and not showing forms */}
        {radios.length > 0 && !showTciForm && !showHamlibForm ? (
          <div>
            <div className="text-xs text-dark-300 uppercase tracking-wider mb-2 font-ui">
              Saved Rig
            </div>
            <div className="space-y-2">
              {radios.map((radio) => {
                const connectionState = radioConnectionStates.get(radio.id);
                const isConnecting = connectionState === "Connecting";

                return (
                  <div
                    key={radio.id}
                    className="flex items-center justify-between p-3 bg-dark-700/50 rounded-lg border border-glass-100"
                  >
                    <div className="flex items-center gap-3">
                      <div
                        className={`p-2 rounded-lg ${
                          radio.type === "Hamlib"
                            ? "bg-accent-primary/20"
                            : "bg-accent-secondary/20"
                        }`}
                      >
                        {radio.type === "Hamlib" ? (
                          <Settings className="w-4 h-4 text-accent-primary" />
                        ) : (
                          <Radio className="w-4 h-4 text-accent-secondary" />
                        )}
                      </div>
                      <div>
                        <div className="text-sm font-medium text-dark-200 font-ui flex items-center gap-1.5">
                          {radio.nickname || radio.model}
                          {autoReconnect && autoConnectRigId === radio.id && (
                            <span title="Auto-connect enabled">
                              <RefreshCw className="w-3 h-3 text-accent-primary" />
                            </span>
                          )}
                        </div>
                        <div className="text-xs text-dark-300 font-mono">
                          {radio.ipAddress}{radio.port ? `:${radio.port}` : ""}
                        </div>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => handleToggleAutoReconnectForRig(radio.id)}
                        title={autoReconnect && autoConnectRigId === radio.id ? "Disable auto-reconnect" : "Enable auto-reconnect"}
                        className={`p-1.5 rounded transition-all ${
                          autoReconnect && autoConnectRigId === radio.id
                            ? "bg-accent-primary/20 text-accent-primary"
                            : "bg-dark-700 text-dark-300 hover:text-dark-200"
                        }`}
                      >
                        <RefreshCw className={`w-3.5 h-3.5 ${autoReconnect && autoConnectRigId === radio.id ? "" : "opacity-50"}`} />
                      </button>
                      <button
                        onClick={() => handleConnect(radio.id)}
                        disabled={isConnecting}
                        className="px-3 py-1.5 text-xs font-medium font-ui flex items-center gap-1.5 bg-accent-primary/20 text-accent-primary rounded-lg hover:bg-accent-primary/30 transition-all disabled:opacity-50"
                      >
                        {isConnecting ? (
                          <>
                            <Wifi className="w-3.5 h-3.5 animate-pulse" />
                            Connecting...
                          </>
                        ) : (
                          <>
                            <Power className="w-3.5 h-3.5" />
                            Connect
                          </>
                        )}
                      </button>
                      <button
                        onClick={() => handleRemoveRig(radio.id)}
                        className="px-3 py-1.5 text-xs font-medium font-ui flex items-center gap-1.5 bg-accent-danger/20 text-accent-danger rounded-lg hover:bg-accent-danger/30 transition-all"
                        title="Remove saved rig"
                      >
                        <PowerOff className="w-3.5 h-3.5" />
                        Remove
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        ) : !showTciForm && !showHamlibForm ? (
          <div className="flex flex-col items-center justify-center py-8 text-dark-300">
            <WifiOff className="w-8 h-8 mb-3 opacity-50" />
            <p className="text-sm text-center font-ui">No rig configured</p>
            <p className="text-xs text-dark-400 mt-1 text-center font-ui">
              Select a radio type above to configure
            </p>
          </div>
        ) : null}
      </div>
    </GlassPanel>
  );
}

interface FeatureToggleProps {
  label: string;
  checked: boolean;
  onChange: (value: boolean) => void;
  disabled?: boolean;
}

function FeatureToggle({ label, checked, onChange, disabled }: FeatureToggleProps) {
  return (
    <label className={`flex items-center gap-2 text-sm ${disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}>
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        disabled={disabled}
        className="w-4 h-4 rounded bg-dark-800 border-glass-100 text-accent-primary focus:ring-accent-primary/50"
      />
      <span className="text-dark-200 font-ui">{label}</span>
    </label>
  );
}

interface TxRxIndicatorProps {
  isTransmitting: boolean;
}

function TxRxIndicator({ isTransmitting }: TxRxIndicatorProps) {
  return (
    <div className="flex rounded-lg overflow-hidden border border-glass-100">
      <div
        className={`px-3 py-1.5 text-xs font-bold font-mono transition-all ${
          !isTransmitting
            ? "bg-accent-success/30 text-accent-success border-r border-accent-success/30"
            : "bg-dark-700 text-dark-600 border-r border-glass-100"
        }`}
      >
        RX
      </div>
      <div
        className={`px-3 py-1.5 text-xs font-bold font-mono transition-all ${
          isTransmitting
            ? "bg-accent-danger/30 text-accent-danger animate-pulse"
            : "bg-dark-700 text-dark-600"
        }`}
      >
        TX
      </div>
    </div>
  );
}
